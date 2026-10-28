using System.IO.Pipes;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using UsefulNotification.Localization;
using Windows.Win32;
namespace UsefulNotification;

/// <summary>Owns the journal, occurrence transitions, immutable published views and supervised broker lifetime.</summary>
internal sealed class NotificationHub : IAsyncDisposable
{
    const bool IsLogEnabled = true;
    readonly object Gate = new();
    readonly List<NotificationEntry> Entries;
    readonly HashSet<Guid> Hovered = [];
    readonly CancellationTokenSource Stopping = new();
    readonly List<Task> Tasks = [];
    string Committed;
    List<NotificationEntry> Published;
    bool Started;
    public DesktopPreferences Preferences;
    public RuntimeSettings Configuration { get; private set; } = RuntimeSettings.Capture();
    public CancellationToken Lifetime => Stopping.Token;
    public event Action? Changed;
    public event Action? PanelRequested;
    public event Action? ShutdownRequested;
    public event Action? SettingsReloaded;
    public event Action<NotificationEntry>? DeliveryIssue;
    public event Action<NotificationEntry>? SoundRequested;

    public NotificationHub()
    {
        Entries = PortableStore.Read(AppConstants.Storage.HistoryFile, new List<NotificationEntry>());
        Preferences = PortableStore.Read(AppConstants.Storage.PreferencesFile, new DesktopPreferences());
        ThemeCatalog.Load();
        Committed = JsonSerializer.Serialize(Entries, AppConstants.WireJson);
        Published = NotificationProtocol.Copy(Entries);
        bool recovered = false;
        foreach (var entry in Entries)
        {
            if (entry.Acceptances.Count == 0)
            {
                entry.Acceptances.Add(new(entry.Id, entry.AcceptedOccurrenceId ?? entry.Current.Id));
                if (entry.Request.Id is { } latest && latest != entry.Id) entry.Acceptances.Add(new(latest, entry.Current.Id));
                recovered = true;
            }
            var occurrence = entry.Current;
            if (occurrence.Action?.Status == ActionStatus.Pending)
            {
                occurrence.Action = new(ActionStatus.Interrupted, "The process stopped before the action outcome was recorded; it will not be repeated automatically.");
                var accepted = occurrence.PendingResult;
                Complete(entry, NotificationStatus.Failed, accepted?.ActionId, accepted?.Reply, accepted?.CheckedItems, action: occurrence.Action);
                recovered = true;
            }
            else if (occurrence.Status is NotificationStatus.Visible or NotificationStatus.Pending)
            {
                occurrence.Status = NotificationStatus.Pending;
                occurrence.DeliveryStartedAt = null; occurrence.RenderedAt = null; occurrence.VisibleAt = null; occurrence.ExpiresAt = null; occurrence.PausedAt = null;
                recovered = true;
            }
        }
        if (recovered) Persist();
        Log($"Journal loaded; count={Entries.Count}; root={AppPaths.Root}", PrintToConsole: false, isRun: IsLogEnabled);
    }

    public List<NotificationEntry> Snapshot() { lock (Gate) return Published; }
    public void InvalidateViews() => Changed?.Invoke();

    public void Start()
    {
        lock (Gate) { if (Started) return; Started = true; }
        RunBackground(ListenAsync);
        RunBackground(async token =>
        {
            using var timer = new PeriodicTimer(AppConstants.Notifications.SchedulerInterval);
            while (await timer.WaitForNextTickAsync(token)) RunSafely(Tick, token);
        });
    }

    public void RunBackground(Func<CancellationToken, Task> operation)
    {
        lock (Gate)
        {
            if (Stopping.IsCancellationRequested) return;
            Tasks.RemoveAll(task => task.IsCompleted);
            Tasks.Add(Task.Run(async () => await RunSafelyAsync(operation, Stopping.Token)));
        }
    }

    public NotificationReceipt Apply(NotificationCommand command)
    {
        if (Stopping.IsCancellationRequested) throw new OperationCanceledException(Stopping.Token);
        if (command.Operation == NotificationOperation.ShowPanel) { PanelRequested?.Invoke(); return new() { Accepted = true }; }
        if (command.Operation == NotificationOperation.Shutdown) return new() { Accepted = true };
        if (command.Operation == NotificationOperation.ReloadSettings) { ReloadSettings(); return new() { Accepted = true }; }
        NotificationReceipt response;
        var completed = new List<(string? Pipe, NotificationResult Result)>();
        lock (Gate)
        {
            if (command.Operation is NotificationOperation.List or NotificationOperation.Get)
                return new() { Accepted = true, Id = command.Id, Entries = Published.Where(item => command.Id is null || item.Id == command.Id).ToList() };
            if (command.Operation == NotificationOperation.Send && command.Notification?.Id is { } incoming)
                foreach (var accepted in Entries)
                    if (accepted.Acceptances.FirstOrDefault(item => item.RequestId == incoming) is { } acceptance)
                        return Receipt(accepted, acceptance.OccurrenceId);
            NotificationEntry? entry = Entries.FirstOrDefault(item => item.Id == (command.Id ?? command.Notification?.Id));
            switch (command.Operation)
            {
                case NotificationOperation.Send:
                case NotificationOperation.Replace:
                    var request = NotificationProtocol.Copy(command.Notification ?? new());
                    if (command.Operation == NotificationOperation.Send && entry is not null) return Receipt(entry, entry.AcceptedOccurrenceId ?? entry.Current.Id);
                    entry ??= request.Tag is null ? null : Entries.FirstOrDefault(item => item.Request.Source == request.Source && item.Request.Tag == request.Tag);
                    request.Source = string.IsNullOrWhiteSpace(request.Source) ? AppConstants.Product.Name : request.Source;
                    request.Title = string.IsNullOrWhiteSpace(request.Title) ? request.Source : request.Title;
                    request.Message = string.IsNullOrWhiteSpace(request.Message) ? Strings.NewNotification : request.Message;
                    request.Level ??= ThemeCatalog.For(request).DefaultLevel ?? AppConstants.Notifications.Level;
                    request.Priority ??= NotificationPriority.Normal;
                    Validate(request);
                    if (entry is null)
                    {
                        entry = new() { Id = request.Id ?? Guid.NewGuid(), Request = request, Order = Entries.Count };
                        Schedule(entry); entry.AcceptedOccurrenceId = entry.Current.Id; Entries.Add(entry);
                    }
                    else
                    {
                        if (entry.Current.Action?.Status == ActionStatus.Pending) throw new InvalidOperationException("An external action is in progress.");
                        bool reschedule = entry.Current.Result is not null || JsonSerializer.Serialize(request.Schedule, AppConstants.WireJson) != JsonSerializer.Serialize(entry.Request.Schedule, AppConstants.WireJson);
                        bool lifetimeChanged = !Equals(AppConstants.Notifications.ResolveLifetime(entry.Request), AppConstants.Notifications.ResolveLifetime(request));
                        if (reschedule) FinishForReplacement(entry, completed);
                        entry.Request = request; entry.Current.ContentRevision++;
                        if (reschedule) Schedule(entry, true);
                        else if (lifetimeChanged) ApplyLifetime(entry);
                    }
                    entry.Acceptances.Add(new(request.Id ?? entry.Id, entry.Current.Id));
                    response = Receipt(entry);
                    break;
                case NotificationOperation.Update:
                    entry = entry ?? throw new KeyNotFoundException("The notification to update was not found.");
                    if (entry.Current.Action?.Status == ActionStatus.Pending) throw new InvalidOperationException("An external action is in progress.");
                    if (command.OccurrenceId is { } key && entry.Current.Id != key) throw new InvalidOperationException("The display occurrence has changed.");
                    var update = NotificationProtocol.Copy(command.Update ?? throw new ArgumentException("A partial update is required."));
                    var updated = Merge(entry.Request, update);
                    Validate(updated);
                    bool resetLifetime = update.Lifetime is not null || !Equals(AppConstants.Notifications.ResolveLifetime(entry.Request), AppConstants.Notifications.ResolveLifetime(updated));
                    entry.Request = updated; entry.Current.ContentRevision++;
                    if (update.Schedule is not null) { FinishForReplacement(entry, completed); Schedule(entry, true); }
                    else if (entry.Current.Result is not null) Schedule(entry, true);
                    if (resetLifetime) ApplyLifetime(entry);
                    response = Receipt(entry);
                    break;
                case NotificationOperation.Retry:
                    entry = entry ?? throw new KeyNotFoundException("The notification to retry was not found.");
                    FinishForReplacement(entry, completed);
                    entry.Occurrences.Add(new() { DueAt = DateTimeOffset.Now });
                    response = Receipt(entry);
                    break;
                case NotificationOperation.Dismiss:
                case NotificationOperation.Snooze:
                    entry = entry ?? throw new KeyNotFoundException("The notification was not found.");
                    EnsureInteraction(entry, command.OccurrenceId, null);
                    var result = Complete(entry, command.Operation == NotificationOperation.Snooze ? NotificationStatus.Snoozed : NotificationStatus.Dismissed, delay: command.Delay ?? AppConstants.Notifications.ResolveSnooze(entry.Request)[0]);
                    completed.Add((entry.Request.CallbackPipe, result)); response = Receipt(entry, result.OccurrenceId);
                    break;
                case NotificationOperation.Clear:
                    Entries.RemoveAll(item => item.Current.Result is not null); response = new() { Accepted = true }; break;
                case NotificationOperation.ClearHistory:
                    foreach (var history in Entries.Where(item => command.Id is null || item.Id == command.Id).ToArray())
                    {
                        history.Occurrences.RemoveAll(item => item.Result is not null && (command.OccurrenceId is null || item.Id == command.OccurrenceId));
                        if (history.Occurrences.Count == 0) Entries.Remove(history);
                    }
                    response = new() { Accepted = true }; break;
                case NotificationOperation.Delete:
                    if (entry?.Current.Action?.Status == ActionStatus.Pending) throw new InvalidOperationException("An external action is in progress.");
                    Entries.RemoveAll(item => item.Id == command.Id); response = new() { Accepted = true }; break;
                case NotificationOperation.Move:
                    var ordered = Entries.OrderBy(item => item.Order).ToList();
                    var moved = ordered.Single(item => item.Id == command.Id); ordered.Remove(moved);
                    ordered.Insert(Math.Clamp(command.Order ?? 0, 0, ordered.Count), moved);
                    for (int index = 0; index < ordered.Count; index++) ordered[index].Order = index;
                    response = new() { Accepted = true, Id = command.Id }; break;
                default: throw new ArgumentOutOfRangeException(nameof(command.Operation));
            }
            Persist();
        }
        Changed?.Invoke();
        foreach (var completion in completed) PublishResult(completion.Pipe, completion.Result);
        Tick();
        return response;
    }

    public void Tick()
    {
        var completed = new List<(string? Pipe, NotificationResult Result)>();
        var failures = new List<Guid>();
        bool changed = false;
        lock (Gate)
        {
            if (Stopping.IsCancellationRequested) return;
            var now = DateTimeOffset.Now;
            foreach (var entry in Entries)
            {
                var occurrence = entry.Current;
                if (entry.Status is NotificationStatus.Scheduled or NotificationStatus.Snoozed && entry.DueAt <= now) { entry.Status = NotificationStatus.Pending; changed = true; }
                if (entry.Status == NotificationStatus.Visible && occurrence.Action is null && occurrence.PausedAt is null && occurrence.ExpiresAt <= now)
                { completed.Add((entry.Request.CallbackPipe, Complete(entry, NotificationStatus.Expired))); changed = true; }
                else if (entry.Status == NotificationStatus.Pending && occurrence.Action is null && occurrence.RenderedAt is null && occurrence.DeliveryStartedAt is { } started && now - started > AppConstants.Notifications.DeliveryDeadline)
                { completed.Add((entry.Request.CallbackPipe, Complete(entry, NotificationStatus.Failed))); failures.Add(entry.Id); changed = true; }
            }
            int active = Entries.Count(item => item.Current.Result is null && (item.Status == NotificationStatus.Visible || item.DeliveryStartedAt is not null));
            foreach (var entry in Entries.Where(item => item.Status == NotificationStatus.Pending && item.DeliveryStartedAt is null && item.Current.Action is null)
                .OrderByDescending(item => item.Request.Priority).ThenBy(item => item.Order).Take(Math.Max(0, Configuration.MaximumVisible - active)))
            { entry.Current.DeliveryStartedAt = now; changed = true; }
            if (changed) Persist();
        }
        if (changed) Changed?.Invoke();
        foreach (var completion in completed) PublishResult(completion.Pipe, completion.Result);
        foreach (Guid id in failures) if (Published.FirstOrDefault(item => item.Id == id) is { } failed) DeliveryIssue?.Invoke(failed);
    }

    public void ConfirmVisible(IEnumerable<RenderReceipt> receipts)
    {
        var sounds = new List<Guid>(); bool changed = false;
        lock (Gate)
        {
            foreach (var receipt in receipts)
            {
                var entry = Entries.FirstOrDefault(item => item.Id == receipt.NotificationId);
                if (entry is null || entry.Current.Id != receipt.OccurrenceId || entry.Current.ContentRevision != receipt.ContentRevision || entry.Current.Result is not null || entry.Current.Action is not null || entry.DeliveryStartedAt is null) continue;
                var occurrence = entry.Current;
                if (occurrence.RenderedAt is null) { occurrence.RenderedAt = DateTimeOffset.Now; changed = true; }
                if (occurrence.InViewport != receipt.Visible) { occurrence.InViewport = receipt.Visible; changed = true; }
                if (receipt.Visible && occurrence.Status == NotificationStatus.Pending)
                {
                    occurrence.Status = NotificationStatus.Visible; occurrence.VisibleAt = DateTimeOffset.Now;
                    ApplyLifetime(entry);
                    sounds.Add(entry.Id); changed = true;
                }
                changed |= Pause(occurrence, !receipt.Visible || Configuration.PauseOnHover && Hovered.Contains(entry.Id));
            }
            if (changed) Persist();
        }
        if (changed) Changed?.Invoke();
        foreach (Guid id in sounds) if (Published.FirstOrDefault(item => item.Id == id) is { } entry && entry.Request.Silent != true && Configuration.PlaySound) SoundRequested?.Invoke(entry);
    }

    /// <summary>Owns a user interaction through completion so shutdown waits for an accepted external action.</summary>
    public Task InteractAsync(Guid id, NotificationAction? action = null, string? reply = null, string[]? checkedItems = null, bool? hover = null, TimeSpan? snooze = null, Guid? occurrenceId = null, long? revision = null)
    {
        lock (Gate)
        {
            Stopping.Token.ThrowIfCancellationRequested();
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Tasks.RemoveAll(task => task.IsCompleted);
            Tasks.Add(finished.Task);
            return Task.Run(async () =>
            {
                try { await InteractCoreAsync(id, action, reply, checkedItems, hover, snooze, occurrenceId, revision); }
                finally { finished.TrySetResult(); }
            });
        }
    }
    async Task InteractCoreAsync(Guid id, NotificationAction? action, string? reply, string[]? checkedItems, bool? hover, TimeSpan? snooze, Guid? occurrenceId, long? revision)
    {
        NotificationAction? acceptedAction; Guid acceptedOccurrence;
        (string? Pipe, NotificationResult Result)? completed = null;
        lock (Gate)
        {
            var entry = Entries.Single(item => item.Id == id);
            if (hover is not null)
            {
                if (entry.Current.Result is not null || occurrenceId is { } hoveredOccurrence && entry.Current.Id != hoveredOccurrence || hover.Value && revision is { } hoveredRevision && entry.Current.ContentRevision != hoveredRevision) return;
                if (hover.Value) Hovered.Add(id); else Hovered.Remove(id);
                Pause(entry.Current, !entry.Current.InViewport || Configuration.PauseOnHover && Hovered.Contains(id));
                return;
            }
            EnsureInteraction(entry, occurrenceId, revision);
            acceptedAction = action is null ? null : entry.Request.Actions.FirstOrDefault(item => item.Id == action.Id)
                ?? (action is ReplyAction && entry.Request.Input is not null ? action : throw new ArgumentException("The action is not declared by this notification."));
            acceptedAction?.Validate();
            if (acceptedAction is ReplyAction && entry.Request.Input?.Required == true && string.IsNullOrWhiteSpace(reply)) throw new ArgumentException(Strings.ReplyRequired);
            if ((checkedItems ?? []).Except(entry.Request.Checklist.Select(item => item.Id)).Any()) throw new ArgumentException("Unknown checklist item.");
            acceptedOccurrence = entry.Current.Id;
            if (acceptedAction?.External == true)
            {
                entry.Current.Action = new(ActionStatus.Pending);
                entry.Current.PendingResult = new() { Id = id, OccurrenceId = acceptedOccurrence, ActionId = acceptedAction.Id, Reply = reply, CheckedItems = checkedItems ?? [], Status = NotificationStatus.Dismissed };
                Persist();
            }
            else
            {
                TimeSpan? delay = snooze ?? (acceptedAction as SnoozeAction)?.Delay;
                completed = (entry.Request.CallbackPipe, Complete(entry, delay is null ? NotificationStatus.Dismissed : NotificationStatus.Snoozed, acceptedAction?.Id, reply, checkedItems, delay));
                Persist();
            }
        }
        Changed?.Invoke();
        if (completed is { } immediate) { PublishResult(immediate.Pipe, immediate.Result); Tick(); return; }
        ActionOutcome outcome;
        try { await acceptedAction!.ExecuteAsync(Stopping.Token); outcome = new(ActionStatus.Succeeded); }
        catch (OperationCanceledException) when (Stopping.IsCancellationRequested) { outcome = new(ActionStatus.Interrupted, "The application is stopping."); }
        catch (Exception error) { Log(error, PrintToConsole: false, isRun: IsLogEnabled); outcome = new(ActionStatus.Failed, error.Message); }
        lock (Gate)
        {
            var entry = Entries.Single(item => item.Id == id);
            if (entry.Current.Id != acceptedOccurrence || entry.Current.Action?.Status != ActionStatus.Pending) return;
            var input = entry.Current.PendingResult!;
            completed = (entry.Request.CallbackPipe, Complete(entry, outcome.Status == ActionStatus.Succeeded ? NotificationStatus.Dismissed : NotificationStatus.Failed, input.ActionId, input.Reply, input.CheckedItems, action: outcome));
            Persist();
        }
        Changed?.Invoke();
        PublishResult(completed.Value.Pipe, completed.Value.Result); Tick();
    }

    static void EnsureInteraction(NotificationEntry entry, Guid? occurrence, long? revision)
    {
        if (entry.Current.Result is not null || entry.Current.Action is not null || entry.Status is not (NotificationStatus.Pending or NotificationStatus.Visible) || occurrence is { } id && entry.Current.Id != id || revision is { } version && entry.Current.ContentRevision != version)
            throw new InvalidOperationException("This display is no longer accepting that interaction.");
    }
    static bool Pause(NotificationOccurrence occurrence, bool paused)
    {
        if (occurrence.Status != NotificationStatus.Visible) return false;
        if (paused && occurrence.PausedAt is null) { occurrence.PausedAt = DateTimeOffset.Now; return true; }
        if (!paused && occurrence.PausedAt is { } since) { if (occurrence.ExpiresAt is not null) occurrence.ExpiresAt += DateTimeOffset.Now - since; occurrence.PausedAt = null; return true; }
        return false;
    }
    /// <summary>Starts a newly selected lifetime now while retaining an existing viewport/hover pause.</summary>
    static void ApplyLifetime(NotificationEntry entry)
    {
        if (entry.Status != NotificationStatus.Visible) return;
        var now = DateTimeOffset.Now;
        entry.Current.ExpiresAt = AppConstants.Notifications.ResolveLifetime(entry.Request) is ExpiringLifetime expiry ? now + expiry.Duration : null;
        if (entry.Current.PausedAt is not null) entry.Current.PausedAt = now;
    }

    NotificationResult Complete(NotificationEntry entry, NotificationStatus status, string? actionId = null, string? reply = null, string[]? checkedItems = null, TimeSpan? delay = null, ActionOutcome? action = null, bool advance = true)
    {
        var occurrence = entry.Current;
        if (occurrence.Result is not null) throw new InvalidOperationException("The display is already completed.");
        DateTimeOffset? nextDue = null;
        if (advance && status == NotificationStatus.Snoozed)
        {
            var snooze = delay ?? AppConstants.Notifications.ResolveSnooze(entry.Request)[0];
            if (snooze <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
            nextDue = DateTimeOffset.Now.Add(snooze);
        }
        else if (advance && entry.Request.Schedule?.Recurrence is { } recurrence)
            nextDue = recurrence.Next(entry.SeriesAnchor, DateTimeOffset.Now);
        var result = new NotificationResult { Id = entry.Id, OccurrenceId = occurrence.Id, Status = status, ActionId = actionId, Reply = reply, CheckedItems = checkedItems ?? [], Action = action };
        occurrence.Result = result; occurrence.Status = status; occurrence.Action = action; occurrence.PendingResult = null;
        occurrence.ClosedAt = result.At; occurrence.ExpiresAt = null; occurrence.PausedAt = null; Hovered.Remove(entry.Id);
        if (nextDue is { } due) entry.Occurrences.Add(new() { Status = status == NotificationStatus.Snoozed ? NotificationStatus.Snoozed : NotificationStatus.Scheduled, DueAt = due });
        return result;
    }

    void FinishForReplacement(NotificationEntry entry, List<(string? Pipe, NotificationResult Result)> completed)
    {
        if (entry.Current.Action?.Status == ActionStatus.Pending) throw new InvalidOperationException("An external action is in progress.");
        if (entry.Current.Result is null)
        {
            completed.Add((entry.Request.CallbackPipe, Complete(entry, NotificationStatus.Dismissed, advance: false)));
        }
    }
    static void Schedule(NotificationEntry entry, bool next = false)
    {
        var due = entry.Request.Schedule?.At ?? DateTimeOffset.Now.Add(entry.Request.Schedule?.After ?? TimeSpan.Zero);
        entry.SeriesAnchor = due;
        if (next) entry.Occurrences.Add(new());
        entry.Current.DueAt = due; entry.Current.Status = due > DateTimeOffset.Now ? NotificationStatus.Scheduled : NotificationStatus.Pending;
    }

    static NotificationRequest Merge(NotificationRequest old, NotificationUpdate change)
    {
        var request = old with
        {
            Source = change.Source is { } source ? source.Value : old.Source, Title = change.Title is { } title ? title.Value : old.Title,
            Message = change.Message is { } message ? message.Value : old.Message, Level = change.Level is { } level ? level.Value : old.Level,
            Priority = change.Priority is { } priority ? priority.Value : old.Priority, Group = change.Group is { } group ? group.Value : old.Group,
            Tag = change.Tag is { } tag ? tag.Value : old.Tag, Theme = change.Theme is { } theme ? theme.Value : old.Theme,
            Appearance = change.Appearance is { } appearance ? appearance.Value : old.Appearance, Lifetime = change.Lifetime is { } lifetime ? lifetime.Value : old.Lifetime,
            Silent = change.Silent is { } silent ? silent.Value : old.Silent, Schedule = change.Schedule is { } schedule ? schedule.Value : old.Schedule,
            Sound = change.Sound is { } sound ? sound.Value : old.Sound, Snooze = change.Snooze is { } snooze ? snooze.Value : old.Snooze,
            Actions = change.Actions is { } actions ? actions.Value : old.Actions, Image = change.Image is { } image ? image.Value : old.Image,
            Input = change.Input is { } input ? input.Value : old.Input, Checklist = change.Checklist is { } checklist ? checklist.Value : old.Checklist,
            Template = change.Template is { } template ? template.Value : old.Template, Data = change.Data is { } data ? data.Value : old.Data,
            CallbackPipe = change.CallbackPipe is { } callback ? callback.Value : old.CallbackPipe,
            Progress = change.ProgressContent is { } content ? content.Value : old.Progress
        };
        if (change.Progress is { } update)
        {
            var progress = request.Progress ?? new();
            request.Progress = progress with { Value = update.Value is { } value ? value.Value : progress.Value, Maximum = update.Maximum is { } maximum ? maximum.Value : progress.Maximum, Label = update.Label is { } label ? label.Value : progress.Label };
        }
        return request;
    }
    static void Validate(NotificationRequest request)
    {
        if (request.Actions is null || request.Checklist is null || request.Data is null) throw new ArgumentException("Actions, checklist and data collections cannot be null.");
        if (request.Actions.Any(action => action is null || string.IsNullOrWhiteSpace(action.Id))) throw new ArgumentException("Actions must have usable identities.");
        if (request.Checklist.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id)) || request.Checklist.Select(item => item.Id).Distinct().Count() != request.Checklist.Count) throw new ArgumentException("Checklist items must have unique usable identities.");
        ThemeCatalog.For(request).Validate();
        if (request.Lifetime is ExpiringLifetime expiry && expiry.Duration <= TimeSpan.Zero) throw new ArgumentException("Expiry must be positive.");
        if (request.Schedule?.At is not null && request.Schedule.After is not null) throw new ArgumentException("Choose an absolute time or delay.");
        if (request.Schedule?.After < TimeSpan.Zero) throw new ArgumentException("Delay cannot be negative.");
        if (request.Schedule?.After is { } after) _ = DateTimeOffset.Now.Add(after);
        request.Schedule?.Recurrence?.Validate();
        if (request.Progress is { } progress && (!double.IsFinite(progress.Maximum) || progress.Maximum <= 0 || progress.Value is { } value && !double.IsFinite(value))) throw new ArgumentException("Invalid progress.");
        if (request.Snooze?.Any(delay => delay <= TimeSpan.Zero) == true) throw new ArgumentException("Snooze choices must be positive.");
        foreach (var action in request.Actions) action.Validate();
        if (request.Actions.Select(action => action.Id).Distinct().Count() != request.Actions.Count) throw new ArgumentException("Action ids must be unique.");
        if (request.Template?.File is { } file && !file.Exists) throw new FileNotFoundException("The HTML template was not found.", file.FullName);
        if (request.Image is { } image && (image.Source is null || !image.Source.IsAbsoluteUri || !AppConstants.RenderMemory.ImageSchemes.Contains(image.Source.Scheme) || image.Source.IsFile && !File.Exists(image.Source.LocalPath))) throw new ArgumentException("The image source is unavailable or unsupported.");
    }

    public void SavePreferences(DesktopPreferences? preferences = null)
    {
        lock (Gate)
        {
            var candidate = NotificationProtocol.Copy(preferences ?? Preferences);
            if (preferences is not null) candidate.PanelBounds = Preferences.PanelBounds;
            if (candidate.Sounds.Count == 0 || candidate.Sounds.Any(sound => sound.Steps.Any(step => !AppConstants.Audio.Frequency.IsValid(step.Frequency) || !AppConstants.Audio.DurationMilliseconds.IsValid(step.Duration.TotalMilliseconds) || !AppConstants.Audio.PauseMilliseconds.IsValid(step.Pause.TotalMilliseconds)))) throw new ArgumentException("Invalid sound preset.");
            PortableStore.Write(AppConstants.Storage.PreferencesFile, candidate); Preferences = candidate;
        }
    }
    public void SaveConfiguration(RuntimeSettings candidate)
    {
        candidate.Validate();
        lock (Gate)
        {
            var previous = Configuration;
            try
            {
                var language = ApplicationServices.ChangeLanguage(candidate.Language, persist: false);
                if (!language.Succeeded) throw language.Error!;
                candidate.Apply(); SettingsManager.SaveSettings(AppConstants.ConfigFileName);
                Configuration = candidate with { };
            }
            catch { previous.Apply(); ApplicationServices.ChangeLanguage(previous.Language, persist: false); throw; }
        }
        SettingsReloaded?.Invoke(); Changed?.Invoke();
    }
    void ReloadSettings()
    {
        RuntimeSettings previous = Configuration;
        try { ThemeCatalog.Load(); SettingsManager.LoadSettings(AppConstants.ConfigFileName); SaveConfiguration(RuntimeSettings.Capture()); }
        catch { previous.Apply(); throw; }
    }
    void Persist()
    {
        try
        {
            foreach (var entry in Entries)
            {
                if (entry.Occurrences.Count > AppConstants.Storage.HistoryCapacity)
                    entry.Occurrences.RemoveRange(0, entry.Occurrences.Count - AppConstants.Storage.HistoryCapacity);
                var retained = entry.Occurrences.Select(item => item.Id).ToHashSet();
                entry.Acceptances.RemoveAll(item => !retained.Contains(item.OccurrenceId));
                if (entry.Acceptances.Count > AppConstants.Storage.HistoryCapacity) entry.Acceptances.RemoveRange(0, entry.Acceptances.Count - AppConstants.Storage.HistoryCapacity);
            }
            var remove = Entries.Where(item => item.Current.Result is not null).OrderByDescending(item => item.CreatedAt).Skip(AppConstants.Storage.HistoryCapacity).ToHashSet();
            Entries.RemoveAll(remove.Contains);
            PortableStore.Write(AppConstants.Storage.HistoryFile, Entries);
            Committed = JsonSerializer.Serialize(Entries, AppConstants.WireJson);
            Published = JsonSerializer.Deserialize<List<NotificationEntry>>(Committed, AppConstants.Json)!;
        }
        catch { Entries.Clear(); Entries.AddRange(JsonSerializer.Deserialize<List<NotificationEntry>>(Committed, AppConstants.Json)!); throw; }
    }
    static NotificationReceipt Receipt(NotificationEntry entry, Guid? occurrence = null) => new() { Accepted = true, Id = entry.Id, OccurrenceId = occurrence ?? entry.Current.Id };
    void PublishResult(string? pipeName, NotificationResult result)
    {
        if (string.IsNullOrWhiteSpace(pipeName)) return;
        string payload = JsonSerializer.Serialize(result, AppConstants.WireJson);
        RunBackground(async token =>
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token); budget.CancelAfter(AppConstants.Timeout.Timeout);
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(AppConstants.Transport.ConnectTimeout, budget.Token);
            await NotificationProtocol.WriteLineAsync(pipe, payload, budget.Token);
        });
    }
    public async Task ListenAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The notification host requires Windows.");
        using var identity = WindowsIdentity.GetCurrent();
        var access = new PipeSecurity();
        access.SetAccessRuleProtection(true, false);
        access.SetOwner(identity.User!);
        access.AddAccessRule(new(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        access.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.ReadWrite, AccessControlType.Deny));
        while (!cancellationToken.IsCancellationRequested)
        {
            using var pipe = NamedPipeServerStreamAcl.Create(AppConstants.Transport.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, access);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(AppConstants.Timeout.Timeout);
                NotificationReceipt response; NotificationCommand? command = null;
                try
                {
                    string payload = await NotificationProtocol.ReadLineAsync(pipe, budget.Token);
                    command = NotificationProtocol.ReadCommand(payload);
                    if (command.Operation == NotificationOperation.Send && string.IsNullOrWhiteSpace(command.Notification?.Source) && OperatingSystem.IsWindowsVersionAtLeast(10) && PInvoke.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint senderId))
                    {
                        using var sender = Process.GetProcessById((int)senderId); command.Notification = (command.Notification ?? new()) with { Source = sender.ProcessName };
                    }
                    response = Apply(command);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    Log(error, PrintToConsole: false, isRun: IsLogEnabled);
                    response = new() { Failure = new(error is KeyNotFoundException ? NotificationError.NotFound : error is IOException ? NotificationError.StorageFailure : error is ArgumentException or JsonException ? NotificationError.InvalidRequest : NotificationError.OperationFailed, error.Message) };
                }
                await NotificationProtocol.WriteLineAsync(pipe, NotificationProtocol.WriteResponse(response), budget.Token);
                if (response.Accepted && command?.Operation == NotificationOperation.Shutdown) ShutdownRequested?.Invoke();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception error) { Log(error, PrintToConsole: false, isRun: IsLogEnabled); }
        }
    }
    public async ValueTask DisposeAsync()
    {
        Task[] tasks;
        lock (Gate) { Stopping.Cancel(); tasks = Tasks.ToArray(); }
        await Task.WhenAll(tasks); Stopping.Dispose();
    }
}
