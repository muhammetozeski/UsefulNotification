using System.IO.Pipes;
using System.Net;
using System.Text.Json;
using UsefulNotification.Localization;

namespace UsefulNotification;

/// <summary>Owns the single-writer notification journal, scheduling, queue, and user acknowledgements.</summary>
internal sealed class NotificationHub : IDisposable
{
    const bool IsLogEnabled = true;
    readonly object Gate = new();
    readonly List<NotificationEntry> Entries;
    string Committed;
    readonly Dictionary<Guid, DateTimeOffset> Hovered = [];
    readonly CancellationTokenSource Stopping = new();
    readonly Timer Supervisor;
    public DesktopPreferences Preferences;
    public event Action? Changed;
    public event Action? PanelRequested;
    public event Action? ShutdownRequested;
    public event Action<NotificationEntry>? DeliveryIssue;
    public event Action<NotificationEntry>? SoundRequested;
    public Task? ServerTask { get; set; }

    /// <summary>Loads the journal and recovers deliveries interrupted by the preceding process.</summary>
    public NotificationHub()
    {
        Entries = PortableStore.Read(AppConstants.Storage.HistoryFile, new List<NotificationEntry>());
        Committed = JsonSerializer.Serialize(Entries, AppConstants.WireJson);
        Preferences = PortableStore.Read(AppConstants.Storage.PreferencesFile, new DesktopPreferences());
        foreach (var entry in Entries.Where(item => item.Status is NotificationStatus.Visible or NotificationStatus.Pending))
        {
            entry.Status = NotificationStatus.Pending;
            entry.DeliveryStartedAt = null;
            entry.VisibleAt = null;
            entry.ExpiresAt = null;
        }
        ThemeCatalog.Load();
        Supervisor = new Timer(_ => RunSafely(Tick), null, AppConstants.Notifications.SupervisorInterval, AppConstants.Notifications.SupervisorInterval);
        Log($"Journal loaded; count={Entries.Count}; root={AppPaths.Root}", PrintToConsole: false, isRun: IsLogEnabled);
    }

    /// <summary>Returns detached entries so rendering and SDK consumers cannot mutate the scheduler.</summary>
    /// <returns>A complete independent history snapshot.</returns>
    public List<NotificationEntry> Snapshot()
    {
        lock (Gate) return JsonSerializer.Deserialize<List<NotificationEntry>>(JsonSerializer.Serialize(Entries, AppConstants.WireJson), AppConstants.Json)!;
    }

    /// <summary>Validates a command, commits its result, then publishes changes to the window host.</summary>
    /// <param name="command">The operation and its optional content.</param>
    /// <returns>Persisted acceptance, the affected id, and requested snapshot data.</returns>
    public NotificationResponse Apply(NotificationCommand command)
    {
        if (command.Operation == NotificationOperation.ShowPanel) { PanelRequested?.Invoke(); return new() { Accepted = true }; }
        if (command.Operation == NotificationOperation.Shutdown) return new() { Accepted = true };
        if (command.Operation == NotificationOperation.ReloadSettings)
        {
            SettingsManager.LoadSettings(AppConstants.ConfigFileName);
            ThemeCatalog.Load();
            Changed?.Invoke();
            return new() { Accepted = true };
        }
        Guid? resultId = command.Id;
        lock (Gate)
        {
            if (command.Operation is NotificationOperation.List or NotificationOperation.Get)
                return new() { Accepted = true, Id = command.Id, Entries = Snapshot().Where(item => command.Id is null || item.Id == command.Id).ToList() };
            switch (command.Operation)
            {
                case NotificationOperation.Send:
                case NotificationOperation.Update:
                    NotificationRequest request = command.Notification ?? new();
                    Validate(request);
                    NotificationEntry? existing = Entries.FirstOrDefault(item => item.Id == (command.Id ?? request.Id));
                    existing ??= request.Tag is null ? null : Entries.FirstOrDefault(item => item.Request.Source == request.Source && item.Request.Tag == request.Tag);
                    if (command.Operation == NotificationOperation.Update && existing is null) throw new KeyNotFoundException("The notification to update was not found.");
                    if (existing is not null && command.Operation == NotificationOperation.Send && existing.Id == request.Id)
                        return new() { Accepted = true, Id = existing.Id };
                    if (existing is not null)
                    {
                        existing.Request = Merge(existing.Request, request);
                        if (request.Schedule is not null)
                        {
                            existing.DueAt = request.Schedule.At ?? DateTimeOffset.Now.Add(request.Schedule.After ?? TimeSpan.Zero);
                            existing.SeriesAnchor = existing.DueAt;
                            existing.Status = existing.DueAt > DateTimeOffset.Now ? NotificationStatus.Scheduled : NotificationStatus.Pending;
                            existing.DeliveryStartedAt = null;
                            existing.VisibleAt = null;
                            existing.ExpiresAt = null;
                        }
                        if (existing.Status == NotificationStatus.Visible && existing.Request.Kind == NotificationKind.Progress && existing.Request.Progress is { Value: { } value } progress && value >= progress.Maximum)
                            existing.ExpiresAt = DateTimeOffset.Now.Add(existing.Request.Duration ?? AppConstants.Notifications.Lifetime);
                        if (existing.Status is NotificationStatus.Dismissed or NotificationStatus.Expired or NotificationStatus.Failed)
                        { existing.Status = NotificationStatus.Pending; existing.VisibleAt = null; existing.DeliveryStartedAt = null; existing.Result = null; }
                        resultId = existing.Id;
                    }
                    else
                    {
                        DateTimeOffset now = DateTimeOffset.Now;
                        DateTimeOffset due = request.Schedule?.At ?? now.Add(request.Schedule?.After ?? TimeSpan.Zero);
                        request = request with
                        {
                            Source = string.IsNullOrWhiteSpace(request.Source) ? AppConstants.Product.Name : request.Source,
                            Level = request.Level ?? ThemeCatalog.Get(request.Theme).DefaultLevel ?? AppConstants.Notifications.Level, Priority = request.Priority ?? NotificationPriority.Normal,
                            Kind = request.Kind ?? (request.Progress is not null ? NotificationKind.Progress : request.Image is not null ? NotificationKind.Image : request.Input is not null ? NotificationKind.Input : request.Checklist.Count > 0 ? NotificationKind.Checklist : request.Actions.Count > 0 ? NotificationKind.Choice : request.Schedule is not null ? NotificationKind.Reminder : NotificationKind.Text)
                        };
                        request.Title = string.IsNullOrWhiteSpace(request.Title) ? request.Source : request.Title;
                        request.Message = string.IsNullOrWhiteSpace(request.Message) ? Strings.NewNotification : request.Message;
                        var entry = new NotificationEntry { Id = request.Id ?? Guid.NewGuid(), Request = request, DueAt = due, SeriesAnchor = due, Status = due > now ? NotificationStatus.Scheduled : NotificationStatus.Pending, Order = Entries.Count };
                        Entries.Add(entry);
                        resultId = entry.Id;
                    }
                    break;
                case NotificationOperation.Dismiss:
                case NotificationOperation.Snooze:
                    NotificationEntry selected = Entries.Single(item => item.Id == command.Id);
                    Close(selected, command.Operation == NotificationOperation.Snooze ? NotificationStatus.Snoozed : NotificationStatus.Dismissed,
                        command.Delay ?? AppConstants.Notifications.ResolveSnooze(selected.Request)[0]);
                    break;
                case NotificationOperation.Clear:
                    Entries.RemoveAll(item => item.Status is NotificationStatus.Dismissed or NotificationStatus.Expired or NotificationStatus.Failed);
                    break;
                case NotificationOperation.Delete:
                    Entries.RemoveAll(item => item.Id == command.Id);
                    Hovered.Remove(command.Id ?? Guid.Empty);
                    break;
                case NotificationOperation.Move:
                    var ordered = Entries.OrderBy(item => item.Order).ToList();
                    var moved = ordered.Single(item => item.Id == command.Id);
                    ordered.Remove(moved);
                    ordered.Insert(Math.Clamp(command.Order ?? 0, 0, ordered.Count), moved);
                    for (int index = 0; index < ordered.Count; index++) ordered[index].Order = index;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(command.Operation));
            }
            Persist();
        }
        Changed?.Invoke();
        Tick();
        return new() { Accepted = true, Id = resultId };
    }

    /// <summary>Promotes due reminders, supervises unconfirmed deliveries, and expires only visible cards.</summary>
    public void Tick()
    {
        List<NotificationEntry> sounds = [];
        List<NotificationEntry> failed = [];
        bool changed = false;
        lock (Gate)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            foreach (var entry in Entries)
            {
                if (entry.Status is NotificationStatus.Scheduled or NotificationStatus.Snoozed && entry.DueAt <= now)
                { entry.Status = NotificationStatus.Pending; entry.Result = null; entry.VisibleAt = null; changed = true; }
                if (entry.Status == NotificationStatus.Visible && !Hovered.ContainsKey(entry.Id) && entry.ExpiresAt <= now)
                { Close(entry, NotificationStatus.Expired); changed = true; }
                if (entry.Status == NotificationStatus.Pending && entry.DeliveryStartedAt is { } started && now - started > AppConstants.Notifications.DeliveryDeadline)
                { entry.Status = NotificationStatus.Failed; entry.Result = new() { Id = entry.Id, Status = NotificationStatus.Failed }; failed.Add(entry); changed = true; }
            }
            int active = Entries.Count(item => item.Status == NotificationStatus.Visible || item.Status == NotificationStatus.Pending && item.DeliveryStartedAt is not null);
            foreach (var entry in Entries.Where(item => item.Status == NotificationStatus.Pending && item.DeliveryStartedAt is null)
                .OrderByDescending(item => item.Request.Priority).ThenBy(item => item.Order).Take(Math.Max(0, Settings.MaximumVisible.Value - active)))
            { entry.DeliveryStartedAt = now; sounds.Add(entry); changed = true; }
            if (changed) Persist();
        }
        if (changed) Changed?.Invoke();
        foreach (var entry in sounds) if (entry.Request.Silent != true && Settings.PlaySound.Value) SoundRequested?.Invoke(entry);
        foreach (var entry in failed) DeliveryIssue?.Invoke(entry);
    }

    /// <summary>Records visibility only after the native host confirms a real on-screen window and rendered card.</summary>
    /// <param name="ids">Identities whose HTML has been rendered in a visible window.</param>
    public void ConfirmVisible(IEnumerable<Guid> ids)
    {
        bool changed = false;
        lock (Gate)
        {
            foreach (Guid id in ids)
            {
                NotificationEntry? entry = Entries.FirstOrDefault(item => item.Id == id);
                if (entry is null || entry.Status != NotificationStatus.Pending || entry.DeliveryStartedAt is null) continue;
                entry.Status = NotificationStatus.Visible;
                entry.VisibleAt = DateTimeOffset.Now;
                bool sticky = entry.Request.Sticky ?? (entry.Request.Kind is NotificationKind.Reminder or NotificationKind.Input or NotificationKind.Choice or NotificationKind.Checklist || entry.Request.Kind == NotificationKind.Progress && (entry.Request.Progress?.Value is null || entry.Request.Progress.Value < entry.Request.Progress.Maximum));
                if (!sticky && (entry.Request.Duration ?? AppConstants.Notifications.Lifetime) > TimeSpan.Zero)
                    entry.ExpiresAt = entry.VisibleAt + (entry.Request.Duration ?? AppConstants.Notifications.Lifetime);
                changed = true;
            }
            if (changed) Persist();
        }
        if (changed) Changed?.Invoke();
    }

    /// <summary>Handles hover, acknowledgement, replies, checklists, actions, and snooze in one user interaction.</summary>
    /// <param name="id">Notification identity.</param>
    /// <param name="action">Optional selected button.</param>
    /// <param name="reply">Optional entered text.</param>
    /// <param name="checkedItems">Optional selected checklist identities.</param>
    /// <param name="hover">A hover transition; null means this is an acknowledgement.</param>
    /// <param name="snooze">An explicit snooze duration.</param>
    public void Interact(Guid id, NotificationAction? action = null, string? reply = null, string[]? checkedItems = null, bool? hover = null, TimeSpan? snooze = null)
    {
        NotificationEntry entry;
        lock (Gate)
        {
            entry = Entries.Single(item => item.Id == id);
            if (hover is not null)
            {
                if (!Settings.PauseOnHover.Value) return;
                if (entry.Status is not (NotificationStatus.Visible or NotificationStatus.Pending)) return;
                if (hover.Value) Hovered.TryAdd(id, DateTimeOffset.Now);
                else if (Hovered.Remove(id, out DateTimeOffset since) && entry.ExpiresAt is not null) entry.ExpiresAt += DateTimeOffset.Now - since;
                return;
            }
            if (entry.Request.Input?.Required == true && action?.Kind == NotificationActionKind.Reply && string.IsNullOrWhiteSpace(reply)) throw new ArgumentException(Strings.ReplyRequired);
            if (action?.Kind == NotificationActionKind.OpenUri)
            {
                if (!Uri.TryCreate(action.Target, UriKind.Absolute, out Uri? target) || target.Scheme is not ("https" or "http" or "mailto")) throw new ArgumentException("Unsupported action URI.");
                using Process? process = Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true });
            }
            if (action?.Kind == NotificationActionKind.RunCommand)
            {
                var start = new ProcessStartInfo(action.Target ?? throw new ArgumentException("The action has no executable.")) { UseShellExecute = false };
                foreach (string argument in action.Arguments) start.ArgumentList.Add(argument);
                using Process? process = Process.Start(start);
            }
            TimeSpan? requestedSnooze = snooze ?? (action?.Kind == NotificationActionKind.Snooze ? action.Snooze ?? AppConstants.Notifications.ResolveSnooze(entry.Request)[0] : null);
            entry.Result = new() { Id = id, ActionId = action?.Id, Reply = reply, CheckedItems = checkedItems ?? [], Status = requestedSnooze is null ? NotificationStatus.Dismissed : NotificationStatus.Snoozed };
            Close(entry, entry.Result.Status, requestedSnooze);
            Persist();
        }
        Changed?.Invoke();
        Tick();
        if (entry.Request.CallbackPipe is { Length: > 0 } callback && entry.Result is { } result)
            _ = RunSafelyAsync(async token =>
            {
                using var pipe = new NamedPipeClientStream(".", callback, PipeDirection.Out, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(AppConstants.Transport.ConnectTimeout, token);
                using var writer = new StreamWriter(pipe, AppConstants.Transport.Encoding);
                await writer.WriteLineAsync(JsonSerializer.Serialize(result, AppConstants.WireJson).AsMemory(), token);
                await writer.FlushAsync(token);
            });
    }

    /// <summary>Saves editable sound and window preferences in the portable data directory.</summary>
    public void SavePreferences() { lock (Gate) PortableStore.Write(AppConstants.Storage.PreferencesFile, Preferences); }

    /// <summary>Accepts multiple same-user clients while preserving single-writer ordering in Apply.</summary>
    /// <returns>A task that ends when the application stops.</returns>
    public async Task ListenAsync()
    {
        while (!Stopping.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(AppConstants.Transport.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(Stopping.Token);
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(Stopping.Token);
                budget.CancelAfter(AppConstants.Transport.ConnectTimeout);
                using var reader = new StreamReader(pipe, AppConstants.Transport.Encoding, leaveOpen: true);
                using var writer = new StreamWriter(pipe, AppConstants.Transport.Encoding, leaveOpen: true);
                NotificationResponse response;
                NotificationCommand? command = null;
                try
                {
                    string payload = await reader.ReadLineAsync(budget.Token) ?? throw new InvalidDataException("Empty request.");
                    if (payload.Length > AppConstants.Transport.MaximumMessageCharacters) throw new InvalidDataException("The request exceeds the channel limit.");
                    command = JsonSerializer.Deserialize<NotificationCommand>(payload, AppConstants.Json) ?? throw new InvalidDataException("Invalid request.");
                    response = Apply(command);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    Log(error, PrintToConsole: false, isRun: IsLogEnabled);
                    response = new() { Error = error.Message };
                }
                await writer.WriteLineAsync(JsonSerializer.Serialize(response, AppConstants.WireJson).AsMemory(), budget.Token);
                await writer.FlushAsync(budget.Token);
                if (response.Accepted && command?.Operation == NotificationOperation.Shutdown) ShutdownRequested?.Invoke();
            }
            catch (OperationCanceledException) when (Stopping.IsCancellationRequested) { return; }
            catch (Exception error) { Log(error, PrintToConsole: false, isRun: IsLogEnabled); }
        }
    }

    /// <summary>Stops supervision and cancels the pipe listener.</summary>
    public void Dispose() { Supervisor.Dispose(); Stopping.Cancel(); Stopping.Dispose(); }

    /// <summary>Rejects malformed scheduling and progress before accepting the journal write.</summary>
    /// <param name="request">The submitted request.</param>
    static void Validate(NotificationRequest request)
    {
        if (request.Duration < TimeSpan.Zero || request.Schedule?.After < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request.Duration));
        if (request.Schedule?.Recurrence == RecurrencePattern.Interval && request.Schedule.Every is not { Ticks: > 0 }) throw new ArgumentException("Interval recurrence requires a positive Every duration.");
        if (request.Progress is { Maximum: <= 0 }) throw new ArgumentOutOfRangeException(nameof(request.Progress));
        if (request.Snooze?.Any(delay => delay <= TimeSpan.Zero) == true) throw new ArgumentException("Snooze durations must be positive.");
        if (request.Schedule?.MonthlyDays?.Any(day => day is < 1 or > 31) == true) throw new ArgumentException("Monthly days must be between 1 and 31.");
    }

    /// <summary>Merges omitted update fields while preserving interactive content and the original identity.</summary>
    /// <param name="previous">The accepted request.</param>
    /// <param name="update">Fields supplied in the update.</param>
    /// <returns>A complete replacement request.</returns>
    static NotificationRequest Merge(NotificationRequest previous, NotificationRequest update) => previous with
    {
        Title = update.Title ?? previous.Title, Message = update.Message ?? previous.Message, Source = update.Source ?? previous.Source,
        Kind = update.Kind ?? previous.Kind, Priority = update.Priority ?? previous.Priority,
        Appearance = update.Appearance ?? previous.Appearance,
        Level = update.Level ?? previous.Level, Theme = update.Theme ?? previous.Theme, Progress = update.Progress ?? previous.Progress,
        Sticky = update.Sticky ?? previous.Sticky, Silent = update.Silent ?? previous.Silent, Duration = update.Duration ?? previous.Duration,
        Image = update.Image ?? previous.Image, Input = update.Input ?? previous.Input, Schedule = update.Schedule ?? previous.Schedule,
        Sound = update.Sound ?? previous.Sound, Template = update.Template ?? previous.Template,
        Snooze = update.Snooze ?? previous.Snooze, CallbackPipe = update.CallbackPipe ?? previous.CallbackPipe,
        Actions = update.Actions.Count > 0 ? update.Actions : previous.Actions, Checklist = update.Checklist.Count > 0 ? update.Checklist : previous.Checklist,
        Data = update.Data.Count > 0 ? update.Data : previous.Data
    };

    /// <summary>Closes one occurrence while keeping a recurring series anchored to its original schedule.</summary>
    /// <param name="entry">Stored notification.</param>
    /// <param name="status">The occurrence result.</param>
    /// <param name="delay">Optional snooze duration.</param>
    void Close(NotificationEntry entry, NotificationStatus status, TimeSpan? delay = null)
    {
        Hovered.Remove(entry.Id);
        entry.Result ??= new() { Id = entry.Id, Status = status };
        entry.ClosedAt = DateTimeOffset.Now;
        entry.Status = status;
        entry.DeliveryStartedAt = null;
        entry.ExpiresAt = null;
        if (status == NotificationStatus.Snoozed) entry.DueAt = DateTimeOffset.Now.Add(delay ?? AppConstants.Notifications.Snooze[0]);
        else if (entry.Request.Schedule?.Recurrence is { } recurrence && recurrence != RecurrencePattern.None)
        {
            entry.DueAt = NextOccurrence(entry.SeriesAnchor, entry.Request.Schedule, DateTimeOffset.Now);
            entry.Status = NotificationStatus.Scheduled;
        }
    }

    /// <summary>Finds the first future occurrence without shifting the series after a snooze.</summary>
    /// <param name="anchor">Original series time.</param>
    /// <param name="schedule">Recurrence policy.</param>
    /// <param name="after">Exclusive lower bound.</param>
    /// <returns>The next due time.</returns>
    public static DateTimeOffset NextOccurrence(DateTimeOffset anchor, ScheduleOptions schedule, DateTimeOffset after)
    {
        DateTimeOffset next = anchor;
        if (schedule.Recurrence == RecurrencePattern.Interval)
        {
            TimeSpan every = schedule.Every ?? throw new ArgumentException("Missing recurrence interval.");
            if (every <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(schedule.Every));
            if (anchor > after) return anchor;
            return anchor.AddTicks((Math.Max(0, (after - anchor).Ticks / every.Ticks) + 1) * every.Ticks);
        }
        while (next <= after || !Matches(next))
        {
            next = schedule.Recurrence switch
            {
                RecurrencePattern.Hourly => next.AddHours(1),
                RecurrencePattern.Yearly => new DateTimeOffset(next.Year + 1, anchor.Month, Math.Min(anchor.Day, DateTime.DaysInMonth(next.Year + 1, anchor.Month)), anchor.Hour, anchor.Minute, anchor.Second, anchor.Offset),
                RecurrencePattern.Monthly when schedule.MonthlyDays is not { Length: > 0 } => new DateTimeOffset(next.AddMonths(1).Year, next.AddMonths(1).Month, Math.Min(anchor.Day, DateTime.DaysInMonth(next.AddMonths(1).Year, next.AddMonths(1).Month)), anchor.Hour, anchor.Minute, anchor.Second, anchor.Offset),
                RecurrencePattern.None => throw new ArgumentException("A one-time notification has no next occurrence."),
                _ => next.AddDays(1)
            };
        }
        return next;

        bool Matches(DateTimeOffset candidate) => schedule.Recurrence switch
        {
            RecurrencePattern.Weekdays => candidate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
            RecurrencePattern.Weekly => (schedule.WeeklyDays is { Length: > 0 } days ? days : [anchor.DayOfWeek]).Contains(candidate.DayOfWeek),
            RecurrencePattern.Monthly when schedule.MonthlyDays is { Length: > 0 } days => days.Contains(candidate.Day),
            _ => true
        };
    }

    /// <summary>Retains active work and caps only completed history before committing a snapshot.</summary>
    void Persist()
    {
        var removable = Entries.Where(item => item.Status is NotificationStatus.Dismissed or NotificationStatus.Expired or NotificationStatus.Failed).OrderByDescending(item => item.CreatedAt).Skip(AppConstants.Storage.HistoryCapacity).ToHashSet();
        Entries.RemoveAll(removable.Contains);
        try
        {
            PortableStore.Write(AppConstants.Storage.HistoryFile, Entries);
            Committed = JsonSerializer.Serialize(Entries, AppConstants.WireJson);
        }
        catch
        {
            Entries.Clear();
            Entries.AddRange(JsonSerializer.Deserialize<List<NotificationEntry>>(Committed, AppConstants.Json)!);
            throw;
        }
    }
}
