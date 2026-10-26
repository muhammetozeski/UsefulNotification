using System.Globalization;
using System.Net;
using System.Reflection;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace UsefulNotification.Desktop;

/// <summary>Generates all shipped CSS from C# theme objects and renders isolated custom HTML templates.</summary>
internal static class ThemeStyle
{
    static ConcurrentDictionary<string, string> StyleCache = new();
    static ConcurrentDictionary<string, (DateTime Modified, string Content)> TemplateCache = new(StringComparer.OrdinalIgnoreCase);
    static ConcurrentDictionary<string, (DateTime Modified, string Content)> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Prepares the built-in HTML styles before any notification is requested.</summary>
    public static void WarmUp()
    {
        foreach (NotificationTheme theme in ThemeCatalog.Themes) _ = Render(theme);
        string directory = Path.Combine(AppPaths.UserData, AppConstants.Storage.TemplatesDirectory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, AppConstants.HtmlTemplates.DefaultFile);
        if (!File.Exists(path))
        {
            using Stream resource = typeof(ThemeStyle).Assembly.GetManifestResourceStream(AppConstants.HtmlTemplates.ResourceName) ?? throw new InvalidDataException("The default HTML template is missing.");
            using var reader = new StreamReader(resource);
            File.WriteAllText(path, reader.ReadToEnd());
        }
    }

    /// <summary>Generates the stylesheet for the selected palette, typography, and layout.</summary>
    /// <param name="theme">Optional theme; null uses the current application theme.</param>
    /// <param name="selector">Optional card selector used to scope the generated rules.</param>
    /// <returns>CSS without custom property declarations.</returns>
    public static string Render(NotificationTheme? theme = null, string? selector = null)
    {
        theme ??= ThemeCatalog.Get();
        string key = JsonSerializer.Serialize(theme, AppConstants.WireJson) + selector;
        if (StyleCache.TryGetValue(key, out string? prepared)) return prepared;
        var colors = theme.Palette;
        var text = theme.Typography;
        var layout = theme.Layout;
        string css = FormattableString.Invariant($$"""
        * { box-sizing: border-box; }
        html, body { margin: 0; padding: 0; background: {{colors.Background.ToCss()}}; color: {{colors.Text.ToCss()}}; font-family: {{text.Family}}; font-size: {{text.Body}}px; line-height: {{text.LineHeight}}; }
        body { overflow: hidden; }
        button, input, textarea, select { font: inherit; color: inherit; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; border-radius: {{layout.Radius}}px; background: {{colors.Surface.ToCss()}}; }
        button { min-height: {{layout.ButtonHeight}}px; padding: {{layout.CompactGap}}px {{layout.Padding}}px; cursor: pointer; }
        button:hover { border-color: {{colors.Accent.ToCss()}}; }
        button.primary, button.selected { background: {{colors.Accent.ToCss()}}; color: {{colors.AccentText.ToCss()}}; border-color: {{colors.Accent.ToCss()}}; }
        button:focus-visible, input:focus, textarea:focus, select:focus { outline: {{layout.Border}}px solid {{colors.Accent.ToCss()}}; outline-offset: {{layout.Border}}px; }
        input, textarea, select { padding: {{layout.Gap}}px; min-width: 0; }
        textarea { resize: vertical; }
        input[type=checkbox] { accent-color: {{colors.Accent.ToCss()}}; }
        h1, h2, h3, p { margin: 0; }
        h1 { font-size: {{text.Heading}}px; font-weight: {{text.StrongWeight}}; }
        h2 { font-size: {{text.CardTitle}}px; line-height: {{text.HeadingLineHeight}}; font-weight: {{text.StrongWeight}}; }
        small, time, .muted { font-size: {{text.Small}}px; color: {{colors.Muted.ToCss()}}; }
        .shell { height: 100vh; display: flex; flex-direction: column; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; }
        .window-header { min-height: {{layout.HeaderHeight}}px; display: flex; align-items: center; padding-left: {{layout.PagePadding}}px; border-bottom: {{layout.Border}}px solid {{colors.Border.ToCss()}}; user-select: none; }
        .window-header strong { flex: 1; }
        .window-header button { border: none; border-radius: 0; background: transparent; min-height: {{layout.HeaderHeight}}px; }
        .window-header button:hover { background: {{colors.Surface.ToCss()}}; }
        .toolbar, .row, .actions, .tabs, .quick { display: flex; align-items: center; gap: {{layout.Gap}}px; flex-wrap: wrap; }
        .toolbar, .tabs { padding: {{layout.Padding}}px {{layout.PagePadding}}px; border-bottom: {{layout.Border}}px solid {{colors.Border.ToCss()}}; }
        .toolbar input { flex: 1; }
        .tabs button { background: transparent; border-color: transparent; }
        .tabs button.selected { color: {{colors.Accent.ToCss()}}; border-bottom-color: {{colors.Accent.ToCss()}}; }
        .content { overflow: auto; flex: 1; padding: {{layout.PagePadding}}px; }
        .list, .editor, .settings, .sound-editor { display: flex; flex-direction: column; gap: {{layout.Padding}}px; }
        .quick { margin-bottom: {{layout.Padding}}px; }
        body:has(.toast-stack) { overflow: auto; }
        .card { position: relative; background: {{colors.Background.ToCss()}}; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; border-left-width: {{layout.AccentBorder}}px; padding: {{layout.Padding}}px; overflow-wrap: anywhere; }
        .card-header { display: flex; align-items: center; gap: {{layout.Gap}}px; margin-bottom: {{layout.CompactGap}}px; }
        .card-header .sender { font-weight: {{text.StrongWeight}}; }
        .card-header .meta { display: flex; flex-wrap: wrap; align-items: center; gap: {{layout.Gap}}px; flex: 1; }
        .card-header button { background: transparent; border: none; padding: 0; min-height: {{layout.IconSize}}px; width: {{layout.IconSize}}px; }
        .card-content { margin-left: {{layout.IconSize + layout.Gap}}px; display: flex; flex-direction: column; gap: {{layout.Gap}}px; }
        .card-footer { margin-top: {{layout.Gap}}px; display: flex; align-items: center; justify-content: space-between; gap: {{layout.Gap}}px; flex-wrap: wrap; }
        .card-footer time { font-size: {{text.Small}}px; white-space: nowrap; }
        .card-actions { display: flex; align-items: center; gap: {{layout.Gap}}px; flex-wrap: wrap; margin-left: auto; }
        .card-actions button { min-height: {{layout.ButtonHeight}}px; padding-left: {{layout.Padding}}px; padding-right: {{layout.Padding}}px; }
        .snooze { display: inline-flex; align-items: stretch; }
        .snooze > button { border-top-right-radius: 0; border-bottom-right-radius: 0; }
        .snooze details { position: relative; }
        .snooze summary { height: 100%; min-height: {{layout.ButtonHeight}}px; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; border-left: none; padding: {{layout.CompactGap}}px {{layout.Gap}}px; cursor: pointer; list-style: none; display: flex; align-items: center; }
        .snooze-options { position: absolute; right: 0; bottom: 100%; display: flex; flex-direction: column; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; background: {{colors.Surface.ToCss()}}; white-space: nowrap; }
        .snooze-options button { border: none; text-align: left; }
        .card p { white-space: pre-wrap; }
        .card-icon { width: {{layout.IconSize}}px; height: {{layout.IconSize}}px; flex-shrink: 0; }
        .card img { max-width: 100%; max-height: {{layout.ImageHeight}}px; object-fit: contain; align-self: flex-start; }
        .card iframe { width: 100%; height: {{layout.ImageHeight}}px; border: none; background: {{colors.Background.ToCss()}}; }
        .custom-template { display: block; width: 100%; height: {{AppConstants.Windows.Toast.Height}}px; border: none; background: {{colors.Background.ToCss()}}; }
        .card progress { width: 100%; height: {{layout.ProgressHeight}}px; accent-color: {{colors.Accent.ToCss()}}; }
        .card input, .card textarea { width: 100%; }
        .card label input[type=checkbox] { width: auto; }
        .card-tools { display: flex; gap: {{layout.Gap}}px; margin-top: {{layout.Gap}}px; }
        .card-tools button { font-size: {{text.Small}}px; color: {{colors.Muted.ToCss()}}; }
        .editor { border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; padding: {{layout.Padding}}px; margin-bottom: {{layout.PagePadding}}px; }
        .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: {{layout.Padding}}px; }
        label.field { display: flex; flex-direction: column; gap: {{layout.CompactGap}}px; }
        label.check { display: flex; flex-direction: row; align-items: center; gap: {{layout.Gap}}px; }
        .span-all { grid-column: 1 / -1; }
        .empty { padding: {{layout.PagePadding}}px; color: {{colors.Muted.ToCss()}}; text-align: center; }
        .error { border-left: {{layout.AccentBorder}}px solid {{colors.Levels[NotificationLevel.Error].ToCss()}}; padding: {{layout.Padding}}px; margin-bottom: {{layout.Padding}}px; }
        .toast-stack { display: flex; flex-direction: column; gap: {{AppConstants.Windows.CardGap}}px; }
        .tone { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)) auto; gap: {{layout.Gap}}px; }
        .sound-editor { margin-top: {{layout.Padding}}px; }
        """);
        if (selector is not null)
            css = Regex.Replace(css, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}", rule =>
            {
                string[] targets = rule.Groups["selectors"].Value.Split(',').Select(target => target.Trim())
                    .Where(target => !target.StartsWith("html", StringComparison.Ordinal) && !target.StartsWith("body", StringComparison.Ordinal))
                    .Select(target => Regex.IsMatch(target, @"^\.card(?=\s|[:.#\[]|$)") ? Regex.Replace(target, @"^\.card", selector) : selector + " " + target).ToArray();
                return targets.Length == 0 ? string.Empty : string.Join(",", targets) + "{" + rule.Groups["body"].Value + "}";
            });
        if (StyleCache.Count >= AppConstants.RenderMemory.Styles) StyleCache.Clear();
        StyleCache[key] = css;
        return css;
    }

    /// <summary>Gets the per-card palette without introducing an independent CSS token system.</summary>
    /// <param name="entry">The notification whose theme and semantic color are resolved.</param>
    /// <returns>Inline style generated from its theme objects.</returns>
    public static string CardStyle(NotificationEntry entry)
    {
        NotificationTheme theme = ThemeCatalog.For(entry.Request);
        ThemePalette palette = theme.Palette;
        NotificationFrame frame = theme.Frame;
        var accent = frame.AccentColor ?? palette.Levels.GetValueOrDefault(entry.Request.Level ?? AppConstants.Notifications.Level, palette.Accent);
        string Color(BorderEdges edge) => (frame.AccentEdges.HasFlag(edge) ? accent : palette.Border).ToCss();
        return FormattableString.Invariant($"background:{palette.Background.ToCss()};color:{palette.Text.ToCss()};border-style:{frame.Pattern.ToString().ToLowerInvariant()};border-width:{frame.Thickness.Top}px {frame.Thickness.Right}px {frame.Thickness.Bottom}px {frame.Thickness.Left}px;border-color:{Color(BorderEdges.Top)} {Color(BorderEdges.Right)} {Color(BorderEdges.Bottom)} {Color(BorderEdges.Left)};border-radius:{frame.CornerRadius}px;padding:{theme.Layout.Padding}px;font-family:{theme.Typography.Family};font-size:{theme.Typography.Body}px");
    }

    /// <summary>Resolves a translated enum label using the same localized field keys.</summary>
    /// <param name="value">The value displayed in a selector or status label.</param>
    /// <returns>The localized label or the public enum name for extensions.</returns>
    public static string Label(Enum value) => Label(value.ToString());
    public static string Label(string key) => typeof(Strings).GetField(key, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)?.ToString() ?? key;

    /// <summary>Loads a custom HTML file and substitutes escaped content; scripts stay inside a sandboxed frame.</summary>
    /// <param name="entry">The custom notification.</param>
    /// <returns>The complete HTML source.</returns>
    public static string Template(NotificationEntry entry)
    {
        string path = Path.GetFullPath(entry.Request.Template?.File?.FullName ?? AppConstants.HtmlTemplates.DefaultFile, Path.Combine(AppPaths.UserData, AppConstants.Storage.TemplatesDirectory));
        DateTime modified = File.GetLastWriteTimeUtc(path);
        if (!TemplateCache.TryGetValue(path, out var prepared) || prepared.Modified != modified)
        {
            if (TemplateCache.Count >= AppConstants.RenderMemory.Templates) TemplateCache.Clear();
            prepared = (modified, File.ReadAllText(path));
            TemplateCache[path] = prepared;
        }
        string html = prepared.Content;
        var values = new Dictionary<string, string?>
        {
            ["title"] = entry.Request.Title, ["message"] = entry.Request.Message, ["source"] = entry.Request.Source,
            ["time"] = entry.CreatedAt.ToString(AppConstants.Notifications.TimeFormat), ["id"] = entry.Id.ToString(),
            ["dismissAction"] = AppConstants.HtmlTemplates.DismissAction, ["dismissLabel"] = Strings.Dismiss
        };
        foreach (var pair in entry.Request.Data) values[$"data.{pair.Key}"] = pair.Value;
        foreach (var pair in values) html = html.Replace("{{" + pair.Key + "}}", WebUtility.HtmlEncode(pair.Value), StringComparison.Ordinal);
        html = html.Replace("{{themeCss}}", Render(ThemeCatalog.For(entry.Request)), StringComparison.Ordinal)
            .Replace("{{frameCss}}", CardStyle(entry), StringComparison.Ordinal);
        string sizing = "<script>new ResizeObserver(()=>parent.postMessage({height:document.body.getBoundingClientRect().height},'*')).observe(document.body);</script>";
        int bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd >= 0 ? html.Insert(bodyEnd, sizing) : html + sizing;
    }

    /// <summary>Converts a local image to an embedded image or preserves a supplied web/data URI.</summary>
    /// <param name="source">Local path or image URI.</param>
    /// <returns>A source usable inside BlazorWebView.</returns>
    public static string ImageSource(NotificationImage image)
    {
        Uri uri = image.Source;
        if (!uri.IsFile && uri.Scheme is "https" or "http" or "data") return uri.AbsoluteUri;
        if (!uri.IsFile) throw new ArgumentException("Unsupported image URI.");
        string path = uri.LocalPath;
        DateTime modified = File.GetLastWriteTimeUtc(path);
        if (ImageCache.TryGetValue(path, out var prepared) && prepared.Modified == modified) return prepared.Content;
        string type = Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".svg" => "image/svg+xml", ".webp" => "image/webp", _ => "image/png" };
        string content = $"data:{type};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
        if (ImageCache.Count >= AppConstants.RenderMemory.Images) ImageCache.Clear();
        ImageCache[path] = (modified, content);
        return content;
    }
}
