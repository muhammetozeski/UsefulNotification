using System.Globalization;
using System.Text.Json;
using UsefulNotification;

return await CommandLine.ExecuteAsync(args);

/// <summary>Parses the public command contract and uses the same request model as C# callers.</summary>
internal static class CommandLine
{
    /// <summary>Identifies a command option's actual request field or behavior.</summary>
    enum Field { Title, Message, Source, Level, Kind, Priority, Theme, Sticky, Silent, Duration, At, After, Every, Recurrence, Id, Tag, Group, Progress, Image, Template, Input, Items, Actions, Sound, Json, Stdin, Wait, Help, Panel, List, Clear, Update, Dismiss, Snooze, Settings, Exit }
    /// <summary>Describes one accepted command token and whether it consumes a value.</summary>
    /// <param name="Name">Public token.</param>
    /// <param name="Field">The request field or operation.</param>
    /// <param name="Value">Whether a following value is required.</param>
    record Option(string Name, Field Field, bool Value = false);
    static List<Option> Options =
    [
        new("--title", Field.Title, true), new("--message", Field.Message, true), new("--source", Field.Source, true),
        new("--level", Field.Level, true), new("--type", Field.Kind, true), new("--priority", Field.Priority, true), new("--theme", Field.Theme, true),
        new("--sticky", Field.Sticky), new("--silent", Field.Silent), new("--duration", Field.Duration, true), new("--at", Field.At, true),
        new("--after", Field.After, true), new("--every", Field.Every, true), new("--repeat", Field.Recurrence, true), new("--id", Field.Id, true),
        new("--tag", Field.Tag, true), new("--group", Field.Group, true), new("--progress", Field.Progress, true), new("--image", Field.Image, true),
        new("--template", Field.Template, true), new("--input", Field.Input), new("--items", Field.Items, true), new("--actions", Field.Actions, true),
        new("--sound", Field.Sound, true), new("--json", Field.Json, true), new("--stdin", Field.Stdin), new("--wait", Field.Wait), new("--help", Field.Help),
        new("panel", Field.Panel), new("list", Field.List), new("clear", Field.Clear), new("update", Field.Update), new("dismiss", Field.Dismiss),
        new("snooze", Field.Snooze), new("settings", Field.Settings), new("exit", Field.Exit)
    ];

    /// <summary>Sends an empty or detailed notification, reports machine-readable acceptance, and optionally waits for acknowledgement.</summary>
    /// <param name="arguments">Command tokens; no arguments means an empty information notification.</param>
    /// <returns>Zero for accepted work, one for rejected work, two for invalid arguments.</returns>
    public static async Task<int> ExecuteAsync(string[] arguments)
    {
        try
        {
            var request = new NotificationRequest { Source = SenderIdentity.Parent() };
            var operation = NotificationOperation.Send;
            bool wait = false;
            List<string> positional = [];
            TimeSpan? delay = null;
            foreach (var pair in arguments.Select((argument, index) => (argument, index)))
            {
                if (pair.index > 0 && Options.Any(option => option.Name == arguments[pair.index - 1] && option.Value)) continue;
                Option? option = Options.FirstOrDefault(candidate => candidate.Name.Equals(pair.argument, StringComparison.OrdinalIgnoreCase));
                if (option is null)
                {
                    if (pair.argument.StartsWith('-')) throw new ArgumentException($"Unknown option: {pair.argument}");
                    positional.Add(pair.argument); continue;
                }
                string value = option.Value ? pair.index + 1 < arguments.Length ? arguments[pair.index + 1] : throw new ArgumentException($"Missing value: {option.Name}") : string.Empty;
                switch (option.Field)
                {
                    case Field.Help:
                        Console.WriteLine("UsefulNotification.Command [title] [message] [options]\nNo arguments sends an information notification. Durations accept .NET TimeSpan format or a number of seconds.");
                        foreach (Option documented in Options) Console.WriteLine($"  {documented.Name}{(documented.Value ? " <value>" : string.Empty)}");
                        return 0;
                    case Field.Title: request.Title = value; break;
                    case Field.Message: request.Message = value; break;
                    case Field.Source: request.Source = value; break;
                    case Field.Level: request.Level = Enum.Parse<NotificationLevel>(value, true); break;
                    case Field.Kind: request.Kind = Enum.Parse<NotificationKind>(value, true); break;
                    case Field.Priority: request.Priority = Enum.Parse<NotificationPriority>(value, true); break;
                    case Field.Theme: request.Theme = value; break;
                    case Field.Sticky: request.Sticky = true; break;
                    case Field.Silent: request.Silent = true; break;
                    case Field.Duration: request.Duration = ParseDuration(value); break;
                    case Field.At: request.Schedule = (request.Schedule ?? new()) with { At = DateTimeOffset.Parse(value, CultureInfo.CurrentCulture) }; break;
                    case Field.After: request.Schedule = (request.Schedule ?? new()) with { After = ParseDuration(value) }; delay = ParseDuration(value); break;
                    case Field.Every: request.Schedule = (request.Schedule ?? new()) with { Every = ParseDuration(value), Recurrence = RecurrencePattern.Interval }; break;
                    case Field.Recurrence: request.Schedule = (request.Schedule ?? new()) with { Recurrence = Enum.Parse<RecurrencePattern>(value, true) }; break;
                    case Field.Id: request.Id = Guid.Parse(value); break;
                    case Field.Tag: request.Tag = value; break;
                    case Field.Group: request.Group = value; break;
                    case Field.Progress: request.Kind = NotificationKind.Progress; request.Progress = new() { Value = double.Parse(value, CultureInfo.InvariantCulture) }; break;
                    case Field.Image: request.Kind = NotificationKind.Image; request.Image = value; break;
                    case Field.Template: request.Kind = NotificationKind.Custom; request.Template = value; break;
                    case Field.Input: request.Kind = NotificationKind.Input; request.Input = new(); request.Sticky = true; break;
                    case Field.Items: request.Kind = NotificationKind.Checklist; request.Checklist = value.Split('|').Select(text => new ChecklistItem { Id = text, Text = text }).ToList(); break;
                    case Field.Actions: request.Kind = NotificationKind.Choice; request.Actions = value.Split('|').Select(label => new NotificationAction { Id = label, Label = label }).ToList(); break;
                    case Field.Sound: request.Sound = new() { AudioFile = value }; break;
                    case Field.Json: request = JsonSerializer.Deserialize<NotificationRequest>(File.ReadAllText(value), AppConstants.Json) ?? new(); request.Source ??= SenderIdentity.Parent(); break;
                    case Field.Stdin: request = JsonSerializer.Deserialize<NotificationRequest>(await Console.In.ReadToEndAsync(), AppConstants.Json) ?? new(); request.Source ??= SenderIdentity.Parent(); break;
                    case Field.Wait: wait = true; break;
                    case Field.Panel: operation = NotificationOperation.ShowPanel; break;
                    case Field.List: operation = NotificationOperation.List; break;
                    case Field.Clear: operation = NotificationOperation.Clear; break;
                    case Field.Update: operation = NotificationOperation.Update; break;
                    case Field.Dismiss: operation = NotificationOperation.Dismiss; break;
                    case Field.Snooze: operation = NotificationOperation.Snooze; break;
                    case Field.Exit: operation = NotificationOperation.Shutdown; break;
                    case Field.Settings:
                        if (!ApplicationServices.Initialize().Succeeded) return 1;
                        SettingsManager.OpenSettingsMenu(AppConstants.ConfigFileName);
                        await NotificationClient.SendAsync(operation: NotificationOperation.ReloadSettings);
                        return 0;
                }
            }
            if (positional.Count > 2) throw new ArgumentException("Use quotes around a title or message that contains spaces.");
            if (positional.Count > 0) request.Title = positional[0];
            if (positional.Count > 1) request.Message = positional[1];
            NotificationResponse response = await NotificationClient.SendAsync(request, operation, request.Id, delay);
            Console.WriteLine(JsonSerializer.Serialize(response, AppConstants.Json));
            if (wait && response.Accepted && response.Id is { } id) Console.WriteLine(JsonSerializer.Serialize(await NotificationClient.WaitAsync(id), AppConstants.Json));
            return response.Accepted ? 0 : 1;
        }
        catch (Exception error)
        {
            ExceptionBoundary.LogSafely(error.ToString());
            Console.Error.WriteLine(JsonSerializer.Serialize(new NotificationResponse { Error = error.Message }, AppConstants.Json));
            return 2;
        }
    }

    /// <summary>Parses numeric seconds or the standard .NET duration representation.</summary>
    /// <param name="value">Duration text.</param>
    /// <returns>The represented duration.</returns>
    static TimeSpan ParseDuration(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ? TimeSpan.FromSeconds(seconds) : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
}
