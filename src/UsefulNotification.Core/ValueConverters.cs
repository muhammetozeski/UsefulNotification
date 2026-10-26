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
/// <summary>Stores owned files relative to one portable root and leaves external absolute files unchanged.</summary>
/// <param name="root">Application directory that owns relative asset references.</param>
internal sealed class PortablePathContext(DirectoryInfo root)
{
    /// <summary>Resolves a stored relative path against this root while retaining legacy absolute references.</summary>
    /// <param name="path">Stored relative asset path or an existing absolute path.</param>
    /// <returns>The absolute path used by the running application.</returns>
    public string Resolve(string path) => Path.GetFullPath(path, root.FullName);

    /// <summary>Relativizes only files and folders contained by this root, including the root itself.</summary>
    /// <param name="path">Asset or working-directory path to persist.</param>
    /// <returns>A root-relative owned path, or the unchanged external/nonabsolute path.</returns>
    public string Store(string path)
    {
        if (!Path.IsPathFullyQualified(path)) return path;
        string relative = Path.GetRelativePath(root.FullName, path);
        return Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) ? path : relative;
    }

    /// <summary>Marks a root-owned executable with a relative directory prefix so it cannot become a PATH command.</summary>
    /// <param name="path">Executable path or bare PATH command name.</param>
    /// <returns>An owned relative executable with an explicit directory prefix, or the original external/PATH value.</returns>
    public string StoreExecutable(string path)
    {
        string stored = Store(path);
        return Path.IsPathFullyQualified(path) && !Path.IsPathFullyQualified(stored) ? Path.Combine(".", stored) : stored;
    }

    /// <summary>Resolves relative executable paths while preserving a bare command name for PATH lookup.</summary>
    /// <param name="path">Stored executable reference.</param>
    /// <returns>An absolute owned/external path, or the unchanged bare PATH command.</returns>
    public string ResolveExecutable(string path) => path.Contains(Path.DirectorySeparatorChar) || path.Contains(Path.AltDirectorySeparatorChar) ? Resolve(path) : path;
}
/// <summary>Keeps wire FileInfo paths absolute and gives persisted references optional portable-root ownership.</summary>
/// <param name="paths">Storage-only ownership; null preserves ordinary wire serialization.</param>
internal sealed class FileInfoConverter(PortablePathContext? paths = null) : JsonConverter<FileInfo>
{
    /// <inheritdoc/>
    public override FileInfo Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        string path = reader.GetString() ?? throw new JsonException("A file path is required.");
        return new(paths?.Resolve(path) ?? path);
    }
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FileInfo value, JsonSerializerOptions options) => writer.WriteStringValue(paths?.Store(value.FullName) ?? value.FullName);
}
/// <summary>Relocates stored file URIs while preserving absolute web/data URIs and legacy file URI documents.</summary>
/// <param name="paths">The storage root that owns relative file URI references.</param>
internal sealed class FileUriConverter(PortablePathContext paths) : JsonConverter<Uri>
{
    /// <inheritdoc/>
    public override Uri Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        string text = reader.GetString() ?? throw new JsonException("An image URI is required.");
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) ? uri : new Uri(paths.Resolve(text));
    }
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
    {
        string stored = value.IsFile ? paths.Store(value.LocalPath) : value.AbsoluteUri;
        writer.WriteStringValue(value.IsFile && !Path.IsPathFullyQualified(stored) ? stored : value.AbsoluteUri);
    }
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
/// <param name="paths">Storage-only path ownership; null leaves wire executable/directory fields unchanged.</param>
internal sealed class ProcessStartConverter(PortablePathContext? paths = null) : JsonConverter<ProcessStartInfo>
{
    sealed record Definition(string FileName, string Arguments, string[] ArgumentList, string WorkingDirectory, bool UseShellExecute, bool CreateNoWindow, ProcessWindowStyle WindowStyle);
    public static void Validate(ProcessStartInfo start)
    {
        ArgumentNullException.ThrowIfNull(start);
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
        var result = new ProcessStartInfo(paths?.ResolveExecutable(value.FileName) ?? value.FileName) { Arguments = value.Arguments, WorkingDirectory = paths is not null && !string.IsNullOrEmpty(value.WorkingDirectory) ? paths.Resolve(value.WorkingDirectory) : value.WorkingDirectory, UseShellExecute = value.UseShellExecute, CreateNoWindow = value.CreateNoWindow, WindowStyle = value.WindowStyle };
        foreach (string argument in value.ArgumentList ?? []) result.ArgumentList.Add(argument);
        Validate(result);
        return result;
    }
    public override void Write(Utf8JsonWriter writer, ProcessStartInfo value, JsonSerializerOptions options)
    {
        Validate(value);
        JsonSerializer.Serialize(writer, new Definition(paths?.StoreExecutable(value.FileName) ?? value.FileName, value.Arguments, value.ArgumentList.ToArray(), paths is not null && !string.IsNullOrEmpty(value.WorkingDirectory) ? paths.Store(value.WorkingDirectory) : value.WorkingDirectory, value.UseShellExecute, value.CreateNoWindow, value.WindowStyle), options);
    }
}
