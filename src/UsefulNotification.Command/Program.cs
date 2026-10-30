using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using UsefulNotification;
using UsefulNotification.Localization;

return await CommandLine.ExecuteAsync(args);

/// <summary>Translates command-line text to wire requests; the host owns legacy name resolution.</summary>
internal static class CommandLine
{
    /// <summary>Identifies an argument's field or command behavior at the text boundary.</summary>
    enum Field { Title, Message, Source, Level, Kind, Priority, Theme, Sticky, Silent, Duration, BodyClick, NotifyLLM, At, After, Every, Recurrence, Id, Tag, Group, Progress, Image, Template, Input, Items, Actions, Sound, Json, Stdin, Wait, Help, Panel, List, Get, Clear, Update, Dismiss, Snooze, Settings, Exit }
    /// <summary>Describes a public token and whether it consumes the next argument.</summary>
    /// <param name="Name">The accepted command or option spelling.</param>
    /// <param name="Field">The field or behavior selected by this token.</param>
    /// <param name="Value">Whether a following argument is required.</param>
    record Option(string Name, Field Field, bool Value = false);
    static List<Option> Options =
    [
        new("--Title", Field.Title, true), new("--Message", Field.Message, true), new("--Source", Field.Source, true),
        new("--Level", Field.Level, true), new("--Type", Field.Kind, true), new("--Priority", Field.Priority, true), new("--Theme", Field.Theme, true),
        new("--Sticky", Field.Sticky), new("--Silent", Field.Silent), new("--Duration", Field.Duration, true), new("--BodyClick", Field.BodyClick), new("--At", Field.At, true),
        new("--NotifyLLM", Field.NotifyLLM, true), new("-l", Field.NotifyLLM, true),
        new("--After", Field.After, true), new("--Every", Field.Every, true), new("--Repeat", Field.Recurrence, true), new("--Id", Field.Id, true),
        new("--Tag", Field.Tag, true), new("--Group", Field.Group, true), new("--Progress", Field.Progress, true), new("--Image", Field.Image, true),
        new("--Template", Field.Template, true), new("--Input", Field.Input), new("--Items", Field.Items, true), new("--Actions", Field.Actions, true),
        new("--Sound", Field.Sound, true), new("--Json", Field.Json, true), new("--Stdin", Field.Stdin), new("--Wait", Field.Wait), new("--Help", Field.Help),
        new("Panel", Field.Panel), new("List", Field.List), new("Get", Field.Get), new("Clear", Field.Clear), new("Update", Field.Update),
        new("Dismiss", Field.Dismiss), new("Snooze", Field.Snooze), new("Settings", Field.Settings), new("Exit", Field.Exit)
    ];

    /// <summary>Sends an empty notification, a complete request or a partial update, and optionally waits for its exact display result.</summary>
    /// <param name="arguments">Command tokens; quoted option values are consumed once even when equal to another token.</param>
    /// <returns>Zero for accepted work, one for rejected work and two for invalid arguments or execution failure.</returns>
    public static async Task<int> ExecuteAsync(string[] arguments)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            if (!ApplicationServices.Initialize().Succeeded) { Console.Error.WriteLine(Strings.OperationFailed); return 1; }
            var request = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true });
            var operation = NotificationOperation.Send;
            bool wait = false, settingsMenu = false;
            List<string> positional = [];
            TimeSpan? delay = null;
            Guid? selectedId = null;
            CodexThreadTarget? llmTarget = null;
            for (int index = 0; index < arguments.Length; index++)
            {
                string token = arguments[index].Equals("--acknowledge-on-click", StringComparison.OrdinalIgnoreCase) ? "--BodyClick" : arguments[index];
                Option? option = Options.FirstOrDefault(candidate => candidate.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
                if (option is null)
                {
                    if (arguments[index].StartsWith('-')) throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.UnknownOption, arguments[index]));
                    positional.Add(arguments[index]);
                    continue;
                }
                string value = option.Value ? ++index < arguments.Length ? arguments[index] : throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.MissingValue, option.Name)) : string.Empty;
                try
                {
                    switch (option.Field)
                    {
                        case Field.Help:
                            Console.WriteLine($"{Strings.CommandUsage}\n{Strings.CommandEmpty}\n{Strings.CommandDuration}\n{Strings.CommandNotifyLlm}");
                            foreach (Option documented in Options) Console.WriteLine($"  {documented.Name}{(documented.Field == Field.NotifyLLM ? " <ThreadId> Codex" : documented.Value ? $" <{Strings.CommandValue}>" : string.Empty)}");
                            return 0;
                        case Field.Title: request["title"] = value; break;
                        case Field.Message: request["message"] = value; break;
                        case Field.Source: request["source"] = value; break;
                        case Field.Level: request["level"] = EnumText<NotificationLevel>(value); break;
                        case Field.Kind: request["kind"] = EnumText<NotificationKind>(value); break;
                        case Field.Priority: Content("overrides")["priority"] = EnumText<NotificationPriority>(value); break;
                        case Field.Theme: request["theme"] = value; break;
                        case Field.Sticky: Content("overrides")["lifetime"] = new JsonObject { ["mode"] = "acknowledge" }; break;
                        case Field.Silent: Content("overrides")["sound"] = null; break;
                        case Field.Duration: Content("overrides")["lifetime"] = new JsonObject { ["mode"] = "expire", ["duration"] = ParseDuration(value).ToString() }; break;
                        case Field.BodyClick: Content("interaction")["clickToAcknowledge"] = true; Content("interaction")["showSnooze"] = true; break;
                        case Field.NotifyLLM:
                            llmTarget = new(Guid.Parse(value));
                            if (++index >= arguments.Length || !arguments[index].Equals(AppConstants.CodexMessaging.Provider, StringComparison.OrdinalIgnoreCase))
                                throw new ArgumentException(Strings.CommandNotifyLlm);
                            break;
                        case Field.At: Content("schedule")["at"] = JsonValue.Create(DateTimeOffset.Parse(value, CultureInfo.CurrentCulture)); break;
                        case Field.After: delay = ParseDuration(value); Content("schedule")["after"] = delay.Value.ToString(); break;
                        case Field.Every:
                            Content("schedule")["every"] = ParseDuration(value).ToString();
                            Content("schedule")["recurrence"] = "interval";
                            break;
                        case Field.Recurrence: Content("schedule")["recurrence"] = value; break;
                        case Field.Id: selectedId = Guid.Parse(value); break;
                        case Field.Tag: request["tag"] = value; break;
                        case Field.Group: request["group"] = value; break;
                        case Field.Progress: Content("progress")["value"] = double.Parse(value, CultureInfo.InvariantCulture); break;
                        case Field.Image: request["image"] = value; break;
                        case Field.Template: request["template"] = value; break;
                        case Field.Input: request["input"] = new JsonObject(); Content("overrides")["lifetime"] = new JsonObject { ["mode"] = "acknowledge" }; break;
                        case Field.Items:
                            request["checklist"] = new JsonArray(value.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(text => (JsonNode)new JsonObject { ["id"] = text, ["text"] = text }).ToArray());
                            break;
                        case Field.Actions:
                            request["actions"] = new JsonArray(value.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(label => (JsonNode)new JsonObject { ["id"] = label, ["label"] = label, ["kind"] = "dismiss" }).ToArray());
                            break;
                        case Field.Sound: Content("overrides")["sound"] = new JsonObject { ["audioFile"] = Path.GetFullPath(value) }; break;
                        case Field.Json: request = ReadObject(await File.ReadAllTextAsync(value, cancellation.Token)); break;
                        case Field.Stdin: request = ReadObject(await Console.In.ReadToEndAsync(cancellation.Token)); break;
                        case Field.Wait: wait = true; break;
                        case Field.Panel: operation = NotificationOperation.ShowPanel; break;
                        case Field.List: operation = NotificationOperation.List; break;
                        case Field.Get: operation = NotificationOperation.Get; break;
                        case Field.Clear: operation = NotificationOperation.Clear; break;
                        case Field.Update: operation = NotificationOperation.Update; break;
                        case Field.Dismiss: operation = NotificationOperation.Dismiss; break;
                        case Field.Snooze: operation = NotificationOperation.Snooze; break;
                        case Field.Exit: operation = NotificationOperation.Shutdown; break;
                        case Field.Settings: settingsMenu = true; operation = NotificationOperation.ReloadSettings; break;
                    }
                }
                catch (Exception error) when (error is FormatException or OverflowException or JsonException or ArgumentException)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.InvalidOptionValue, option.Name, value), error);
                }
            }
            if (positional.Count > 2) throw new ArgumentException(Strings.PositionalLimit);
            if (positional.Count > 0) request["title"] = positional[0];
            if (positional.Count > 1) request["message"] = positional[1];
            if (llmTarget is not null) request["notifyLLM"] = JsonSerializer.SerializeToNode(llmTarget, AppConstants.Json);
            Guid? id = selectedId ?? request["id"]?.Deserialize<Guid>(AppConstants.Json);
            if (operation is NotificationOperation.Update or NotificationOperation.Dismiss or NotificationOperation.Snooze or NotificationOperation.Get && id is null) throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.MissingValue, "--Id"));
            if (settingsMenu) SettingsManager.OpenSettingsMenu(AppConstants.ConfigFileName);
            if (operation == NotificationOperation.Send)
            {
                request["source"] ??= SenderIdentity.Parent();
                id ??= Guid.NewGuid();
                request["id"] = id.Value.ToString();
            }
            var command = new JsonObject { ["operation"] = JsonSerializer.SerializeToNode(operation, AppConstants.Json) };
            if (id is { } identity) command["id"] = identity.ToString();
            if (operation is NotificationOperation.Send or NotificationOperation.Update) command[operation == NotificationOperation.Update ? "update" : "notification"] = request;
            if (operation == NotificationOperation.Snooze && delay is { } snooze) command["delay"] = snooze.ToString();
            NotificationReceipt response = await NotificationClient.SendWireAsync(command.ToJsonString(AppConstants.WireJson), operation, cancellation.Token);
            Console.WriteLine(NotificationProtocol.WriteResponse(response));
            if (wait && response.Accepted && response.OccurrenceId is not null)
                Console.WriteLine(JsonSerializer.Serialize(await NotificationClient.WaitAsync(response, cancellationToken: cancellation.Token), AppConstants.WireJson));
            return response.Accepted ? 0 : 1;

            /// <summary>Reuses or creates the nested request object being explicitly edited by an option.</summary>
            /// <param name="name">The wire field owned by that content object.</param>
            /// <returns>The mutable nested object; unrelated fields remain intact.</returns>
            JsonObject Content(string name)
            {
                if (request[name] is JsonObject content) return content;
                var created = new JsonObject();
                request[name] = created;
                return created;
            }
        }
        catch (Exception error)
        {
            ExceptionBoundary.LogSafely(error.ToString());
            var failure = new NotificationReceipt { Failure = new(error is ArgumentException ? NotificationError.InvalidRequest : NotificationError.OperationFailed, error is ArgumentException ? error.Message : Strings.OperationFailed) };
            Console.Error.WriteLine(JsonSerializer.Serialize(failure, AppConstants.WireJson));
            return 2;
        }
        finally { Console.CancelKeyPress -= cancel; }
    }

    /// <summary>Reads JSON request or patch text without materializing model defaults or dropping explicit null fields.</summary>
    /// <param name="text">A complete JSON object.</param>
    /// <returns>The independently owned object, preserving property presence.</returns>
    static JsonObject ReadObject(string text) => JsonNode.Parse(text, new JsonNodeOptions { PropertyNameCaseInsensitive = true }) as JsonObject ?? throw new ArgumentException(Strings.RequestObjectRequired);

    /// <summary>Validates a finite command-line enum and returns its canonical wire spelling.</summary>
    /// <typeparam name="T">The actual finite contract enum.</typeparam>
    /// <param name="value">A case-insensitive named enum value.</param>
    /// <returns>The canonical member name; undefined numeric values are rejected.</returns>
    static string EnumText<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed.ToString() : throw new FormatException(value);

    /// <summary>Parses numeric seconds or the standard .NET duration representation.</summary>
    /// <param name="value">Duration text.</param>
    /// <returns>The represented duration, with overflow rejected.</returns>
    static TimeSpan ParseDuration(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ? TimeSpan.FromSeconds(seconds) : TimeSpan.Parse(value, CultureInfo.InvariantCulture);
}
