using System.Collections;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace UsefulNotification;

/// <summary>Owns executable-action and link validation policy.</summary>
internal sealed class ActionPolicy
{
    public HashSet<string> LinkSchemes { get; set; } = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto];
}
internal sealed class FileInfoConverter : JsonConverter<FileInfo>
{
    public override FileInfo Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => new(reader.GetString() ?? throw new JsonException("A file path is required."));
    public override void Write(Utf8JsonWriter writer, FileInfo value, JsonSerializerOptions options) => writer.WriteStringValue(value.FullName);
}
internal sealed class ColorConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        string text = reader.GetString() ?? throw new JsonException("A color is required.");
        if (text.Length == 9 && text[0] == '#') return Color.FromArgb(Convert.ToByte(text[7..9], 16), Convert.ToByte(text[1..3], 16), Convert.ToByte(text[3..5], 16), Convert.ToByte(text[5..7], 16));
        return ColorTranslator.FromHtml(text);
    }
    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToCss());
}
internal static class ColorFormatting
{
    public static string ToCss(this Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
}
/// <summary>Transfers supported start options explicitly; inherited environment and credentials never enter the journal.</summary>
internal sealed class ProcessStartConverter : JsonConverter<ProcessStartInfo>
{
    sealed record Definition(string FileName, string Arguments, string[] ArgumentList, string WorkingDirectory, bool UseShellExecute, bool CreateNoWindow, ProcessWindowStyle WindowStyle);
    public static void Validate(ProcessStartInfo start)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(start.FileName);
        if (start.RedirectStandardInput || start.RedirectStandardOutput || start.RedirectStandardError || !string.IsNullOrEmpty(start.UserName) || !string.IsNullOrEmpty(start.Verb))
            throw new ArgumentException("Notification commands do not transfer credentials, redirected streams, or shell verbs.");
        var inherited = Environment.GetEnvironmentVariables();
        if (start.Environment.Count != inherited.Count || start.Environment.Any(pair => !Equals(inherited[pair.Key], pair.Value)))
            throw new ArgumentException("Notification commands inherit the host environment; environment overrides cannot be transferred.");
        if (start.ArgumentList.Count > 0 && !string.IsNullOrEmpty(start.Arguments)) throw new ArgumentException("Use either Arguments or ArgumentList.");
    }
    public override ProcessStartInfo Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<Definition>(ref reader, options) ?? throw new JsonException("Command options are required.");
        var result = new ProcessStartInfo(value.FileName) { Arguments = value.Arguments, WorkingDirectory = value.WorkingDirectory, UseShellExecute = value.UseShellExecute, CreateNoWindow = value.CreateNoWindow, WindowStyle = value.WindowStyle };
        foreach (string argument in value.ArgumentList ?? []) result.ArgumentList.Add(argument);
        Validate(result);
        return result;
    }
    public override void Write(Utf8JsonWriter writer, ProcessStartInfo value, JsonSerializerOptions options)
    {
        Validate(value);
        JsonSerializer.Serialize(writer, new Definition(value.FileName, value.Arguments, value.ArgumentList.ToArray(), value.WorkingDirectory, value.UseShellExecute, value.CreateNoWindow, value.WindowStyle), options);
    }
}
