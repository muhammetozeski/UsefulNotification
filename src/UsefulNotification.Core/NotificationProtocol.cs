using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
namespace UsefulNotification;

/// <summary>Contains text compatibility at the host boundary; ordinary object serialization performs no file lookup.</summary>
internal static class NotificationProtocol
{
    public static async Task WriteLineAsync(Stream stream, string text, CancellationToken cancellationToken)
    {
        byte[] bytes = AppConstants.Transport.Encoding.GetBytes(text + '\n');
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
    public static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, AppConstants.Transport.Encoding, leaveOpen: true);
        char[] buffer = System.Buffers.ArrayPool<char>.Shared.Rent(4096);
        var text = new StringBuilder();
        try
        {
            while (true)
            {
                int count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (count == 0) throw new EndOfStreamException("The peer closed the channel before a complete line.");
                int end = buffer.AsSpan(0, count).IndexOf('\n');
                int length = end >= 0 ? end : count;
                if (text.Length + length > AppConstants.Transport.MaximumMessageCharacters) throw new InvalidDataException("The message exceeds the channel limit.");
                text.Append(buffer, 0, length);
                if (end >= 0) return text.ToString().TrimEnd('\r');
            }
        }
        finally { System.Buffers.ArrayPool<char>.Shared.Return(buffer); }
    }
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, AppConstants.WireJson), AppConstants.Json)!;

    public static NotificationCommand ReadCommand(string text)
    {
        var command = JsonNode.Parse(text, new JsonNodeOptions { PropertyNameCaseInsensitive = true })?.AsObject() ?? throw new JsonException("A command object is required.");
        bool update = command["operation"]?.GetValue<string>().Equals("update", StringComparison.OrdinalIgnoreCase) == true;
        if (command["notification"] is JsonObject request)
        {
            NormalizeRequest(request, update);
            if (update && command["update"] is null) { command["update"] = request.DeepClone(); command.Remove("notification"); }
        }
        if (command["update"] is JsonObject patch) NormalizeRequest(patch, true);
        return command.Deserialize<NotificationCommand>(AppConstants.Json) ?? throw new JsonException("Invalid command.");
    }

    public static string WriteResponse(NotificationReceipt response)
    {
        var root = JsonSerializer.SerializeToNode(response, AppConstants.Json)!.AsObject();
        if (root["entries"] is JsonArray entries && response.Entries is { } models)
            for (int index = 0; index < entries.Count; index++)
            {
                var item = entries[index]!.AsObject(); var model = models[index];
                item["status"] = JsonSerializer.SerializeToNode(model.Status, AppConstants.Json);
                item["dueAt"] = JsonValue.Create(model.DueAt); item["visibleAt"] = JsonValue.Create(model.VisibleAt);
                item["expiresAt"] = JsonValue.Create(model.ExpiresAt); item["deliveryStartedAt"] = JsonValue.Create(model.DeliveryStartedAt);
                item["result"] = JsonSerializer.SerializeToNode(model.Result, AppConstants.Json);
            }
        return root.ToJsonString(AppConstants.WireJson);
    }

    public static T ReadStored<T>(string text)
    {
        var root = JsonNode.Parse(text, new JsonNodeOptions { PropertyNameCaseInsensitive = true }) ?? throw new JsonException("An object is required.");
        if (typeof(T) == typeof(List<NotificationEntry>) && root is JsonArray entries)
            foreach (var node in entries)
            {
                var entry = node!.AsObject();
                if (entry["request"] is JsonObject request) NormalizeRequest(request, false);
                if (entry["occurrences"] is null)
                {
                    var occurrence = new JsonObject { ["id"] = Guid.NewGuid().ToString() };
                    foreach (var fieldName in new[] { "dueAt", "status", "deliveryStartedAt", "visibleAt", "expiresAt", "closedAt", "result" })
                        if (entry[fieldName] is { } fieldValue) occurrence[fieldName] = fieldValue.DeepClone();
                    if (occurrence["result"] is JsonObject result) result["occurrenceId"] = occurrence["id"]!.DeepClone();
                    entry["occurrences"] = new JsonArray(occurrence);
                }
            }
        T value = root.Deserialize<T>(AppConstants.StorageJson) ?? throw new JsonException("Stored data is empty.");
        if (value is DesktopPreferences preferences && root["selectedSound"] is JsonValue selected && selected.TryGetValue<string>(out string? name))
            preferences.SelectedSound = preferences.Sounds.FirstOrDefault(sound => sound.Name == name) ?? preferences.SelectedSound;
        return value;
    }

    static void NormalizeRequest(JsonObject request, bool patch)
    {
        if (request["theme"] is JsonValue theme && theme.TryGetValue<string>(out string? name))
            request["theme"] = JsonSerializer.SerializeToNode(ThemeCatalog.Get(name, required: true), AppConstants.Json);
        string? kind = request["kind"]?.GetValue<string>();
        if (request["lifetime"] is null && (request.ContainsKey("sticky") || request.ContainsKey("duration")))
            request["lifetime"] = request["sticky"]?.GetValue<bool>() == true || request["duration"]?.GetValue<string>() == TimeSpan.Zero.ToString()
                ? new JsonObject { ["mode"] = "acknowledge" }
                : new JsonObject { ["mode"] = "expire", ["duration"] = request["duration"]?.DeepClone() ?? JsonValue.Create(NotificationLifetimes.Default.Duration.ToString()) };
        if (request["template"] is JsonValue template && template.TryGetValue<string>(out string? path))
            request["template"] = new JsonObject { ["file"] = Path.GetFullPath(path!, Path.Combine(AppPaths.UserData, AppConstants.Storage.TemplatesDirectory)) };
        if (kind?.Equals("custom", StringComparison.OrdinalIgnoreCase) == true && request["template"] is null) request["template"] = new JsonObject();
        if (!patch && kind?.Equals("reminder", StringComparison.OrdinalIgnoreCase) == true && request["schedule"] is null) request["schedule"] = new JsonObject();
        if (request["image"] is JsonValue image && image.TryGetValue<string>(out string? source))
        {
            var uri = Uri.TryCreate(source, UriKind.Absolute, out var parsed) ? parsed : new Uri(Path.GetFullPath(source!, AppPaths.Root));
            request["image"] = new JsonObject { ["source"] = uri.AbsoluteUri };
        }
        if (request["schedule"] is JsonObject schedule && schedule["recurrence"] is JsonValue recurrence && recurrence.TryGetValue<string>(out string? pattern))
        {
            NotificationRecurrence? policy = pattern!.ToLowerInvariant() switch
            {
                "none" => null, "hourly" => NotificationRecurrences.Hourly, "daily" => NotificationRecurrences.Daily,
                "weekdays" => NotificationRecurrences.Weekdays,
                "weekly" => new WeeklyRecurrence(schedule["weeklyDays"]?.Deserialize<DayOfWeek[]>(AppConstants.Json) ?? []),
                "monthly" => new MonthlyRecurrence(schedule["monthlyDays"]?.Deserialize<int[]>(AppConstants.Json) ?? []),
                "yearly" => NotificationRecurrences.Yearly,
                "interval" => new IntervalRecurrence(schedule["every"]?.Deserialize<TimeSpan>(AppConstants.Json) ?? throw new JsonException("A recurrence interval is required.")),
                _ => throw new JsonException("Unknown recurrence.")
            };
            schedule["recurrence"] = JsonSerializer.SerializeToNode(policy, AppConstants.Json);
        }
        if (request["actions"] is JsonArray actions)
            foreach (var node in actions)
            {
                var action = node!.AsObject();
                string actionKind = action["kind"]?.GetValue<string>() ?? "dismiss";
                action["kind"] = actionKind.ToLowerInvariant() switch
                {
                    "dismiss" => "dismiss", "reply" => "reply", "callback" => "callback", "snooze" => "snooze",
                    "openuri" => "openUri", "runcommand" => "runCommand", _ => throw new JsonException("Unknown action kind.")
                };
                if (actionKind.Equals("snooze", StringComparison.OrdinalIgnoreCase) && action["delay"] is null)
                    action["delay"] = action["snooze"]?.DeepClone() ?? JsonValue.Create(AppConstants.Notifications.Snooze[0].ToString());
                if (actionKind.Equals("runCommand", StringComparison.OrdinalIgnoreCase) && action["startInfo"] is null)
                    action["startInfo"] = new JsonObject { ["fileName"] = action["target"]?.DeepClone(), ["arguments"] = "", ["argumentList"] = action["arguments"]?.DeepClone() ?? new JsonArray(), ["workingDirectory"] = AppPaths.Root };
            }
        request.Remove("kind"); request.Remove("sticky"); request.Remove("duration");
    }
}

/// <summary>Preserves explicit nulls inside changes while absent properties remain unchanged.</summary>
internal sealed class ChangeConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Change<>);
    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) => (JsonConverter)Activator.CreateInstance(typeof(ChangeConverter<>).MakeGenericType(type.GetGenericArguments()))!;
    sealed class ChangeConverter<T> : JsonConverter<Change<T>>
    {
        public override bool HandleNull => true;
        public override Change<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => new(JsonSerializer.Deserialize<T>(ref reader, options)!);
        public override void Write(Utf8JsonWriter writer, Change<T> value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue(); else JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
