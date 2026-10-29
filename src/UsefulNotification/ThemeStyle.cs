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
    /// <summary>Keeps application controls on a neutral surface independently of a notification's selected theme.</summary>
    public static NotificationTheme PanelTheme => ThemeCatalog.PanelTheme;
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
        theme ??= PanelTheme;
        if (theme.Palette.LightBackground is not null || theme.Palette.DarkBackground is not null || theme.Palette.LightAccent is not null || theme.Palette.DarkAccent is not null)
        {
            NotificationTheme dark = theme with { Palette = SchemePalette(theme, light: false) };
            NotificationTheme light = theme with { Palette = SchemePalette(theme, light: true) };
            return Render(dark, selector) + "@media (prefers-color-scheme: light){" + Render(light, selector) + "}";
        }
        string key = JsonSerializer.Serialize(theme, AppConstants.WireJson) + selector;
        if (StyleCache.TryGetValue(key, out string? prepared)) return prepared;
        var colors = theme.Palette;
        var text = theme.Typography;
        var layout = theme.Layout;
        string css = FormattableString.Invariant($$"""
        * { box-sizing: border-box; }
        html { color-scheme: light dark; }
        html, body { margin: 0; padding: 0; background: {{colors.Background.ToCss()}}; color: {{colors.Text.ToCss()}}; font-family: {{text.Family}}; font-size: {{text.Body}}px; line-height: {{text.LineHeight}}; }
        body { overflow: hidden; }
        button, input, textarea, select, summary { font: inherit; color: inherit; }
        button, input, textarea, select { border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; border-radius: {{layout.Radius}}px; background: {{colors.Surface.ToCss()}}; }
        button { min-height: {{layout.ButtonHeight}}px; padding: {{layout.CompactGap}}px {{layout.Padding}}px; cursor: pointer; }
        button:hover:not(:disabled) { border-color: {{colors.Accent.ToCss()}}; }
        button.primary { background: {{colors.Accent.ToCss()}}; color: {{colors.AccentText.ToCss()}}; border-color: {{colors.Accent.ToCss()}}; }
        button:disabled { background: {{colors.Surface.ToCss()}}; color: {{colors.Muted.ToCss()}}; border-color: {{colors.Border.ToCss()}}; cursor: default; }
        button.danger { color: {{colors.Levels.GetValueOrDefault(NotificationLevel.Error, colors.Accent).ToCss()}}; }
        button:focus-visible, summary:focus-visible, input:focus, textarea:focus, select:focus { outline: {{layout.Border}}px solid {{colors.Accent.ToCss()}}; outline-offset: {{layout.Border}}px; }
        input, textarea, select { padding: {{layout.Gap}}px; min-width: 0; }
        input:disabled, textarea:disabled { color: {{colors.Muted.ToCss()}}; background: {{colors.Surface.ToCss()}}; }
        textarea { resize: vertical; }
        input[type=checkbox] { accent-color: {{colors.Accent.ToCss()}}; width: auto; }
        h1, h2, h3, p { margin: 0; }
        h1 { font-size: {{text.Heading}}px; font-weight: {{text.StrongWeight}}; }
        h2 { font-size: {{text.CardTitle}}px; line-height: {{text.HeadingLineHeight}}; font-weight: {{text.StrongWeight}}; }
        small, time, .muted { font-size: {{text.Small}}px; color: {{colors.Muted.ToCss()}}; }
        .shell { height: 100vh; display: flex; flex-direction: column; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; }
        .window-header { flex-shrink: 0; height: {{layout.HeaderHeight}}px; display: flex; align-items: stretch; border-bottom: {{layout.Border}}px solid {{colors.Border.ToCss()}}; user-select: none; }
        .window-drag-region { display: flex; align-items: center; gap: {{layout.Gap}}px; padding-left: {{layout.Padding}}px; flex: 1; touch-action: none; }
        .app-icon { width: {{layout.IconSize}}px; height: {{layout.IconSize}}px; color: {{colors.Accent.ToCss()}}; }
        .window-header button { width: {{layout.HeaderHeight}}px; padding: 0; border: none; background: transparent; }
        .window-header button:hover { background: {{colors.Surface.ToCss()}}; }
        .workspace { display: flex; flex: 1; min-height: 0; }
        .sidebar { width: {{layout.SidebarWidth}}px; flex-shrink: 0; padding: {{layout.Padding}}px {{layout.Gap}}px; border-right: {{layout.Border}}px solid {{colors.Border.ToCss()}}; display: flex; flex-direction: column; gap: {{layout.CompactGap}}px; }
        .nav-button { display: flex; align-items: center; justify-content: space-between; gap: {{layout.Gap}}px; text-align: left; background: transparent; border-color: transparent; padding: {{layout.Gap}}px {{layout.Padding}}px; }
        .nav-button[aria-current=page] { background: {{colors.Surface.ToCss()}}; border-left-color: {{colors.Accent.ToCss()}}; color: {{colors.Text.ToCss()}}; font-weight: {{text.StrongWeight}}; }
        .nav-button:nth-last-child(2) { margin-top: auto; }
        .page { flex: 1; min-width: 0; min-height: 0; display: flex; flex-direction: column; }
        .page-header { display: flex; align-items: center; justify-content: space-between; gap: {{layout.Gap}}px; padding: {{layout.PagePadding}}px; }
        .toolbar, .row, .actions, .quick, .list-heading, .pagination { display: flex; align-items: center; gap: {{layout.Gap}}px; flex-wrap: wrap; }
        .toolbar { align-items: end; padding: 0 {{layout.PagePadding}}px {{layout.Padding}}px; }
        .toolbar .search-field { flex: 1; }
        .toolbar select { max-width: 100%; }
        .quick { padding: {{layout.Gap}}px {{layout.PagePadding}}px; border-block: {{layout.Border}}px solid {{colors.Border.ToCss()}}; }
        .quick button, .list-heading button { background: transparent; border-color: transparent; font-size: {{text.Small}}px; }
        .list-heading { justify-content: space-between; padding: {{layout.Gap}}px {{layout.PagePadding}}px; }
        .content { overflow: auto; flex: 1; min-height: 0; padding: 0 {{layout.PagePadding}}px {{layout.PagePadding}}px; }
        .list, .editor, .settings, .sound-editor { display: flex; flex-direction: column; gap: {{layout.Padding}}px; }
        .pagination { flex-shrink: 0; justify-content: space-between; padding: {{layout.Gap}}px {{layout.PagePadding}}px; border-top: {{layout.Border}}px solid {{colors.Border.ToCss()}}; }
        body:has(.toast-stack) { overflow: auto; }
        .card { position: relative; background: {{colors.Background.ToCss()}}; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; border-left-width: {{layout.AccentBorder}}px; padding: {{layout.Padding}}px; overflow-wrap: anywhere; }
        .card-header { display: flex; align-items: center; gap: {{layout.Gap}}px; margin-bottom: {{layout.CompactGap}}px; }
        .card-header .sender { font-weight: {{text.StrongWeight}}; color: {{colors.Accent.ToCss()}}; }
        .card-header .meta { display: flex; flex-wrap: wrap; align-items: center; gap: {{layout.Gap}}px; flex: 1; min-width: 0; }
        .card-header button { background: transparent; border: none; padding: 0; min-height: {{layout.IconSize}}px; width: {{layout.IconSize}}px; }
        .card-content { margin-left: {{layout.IconSize + layout.Gap}}px; display: flex; flex-direction: column; gap: {{layout.Gap}}px; }
        .card-footer { margin-top: {{layout.Gap}}px; display: flex; justify-content: flex-end; gap: {{layout.Gap}}px; flex-wrap: wrap; }
        .card-actions { display: flex; align-items: center; gap: {{layout.Gap}}px; flex-wrap: wrap; }
        .card-actions button { min-height: {{layout.ButtonHeight}}px; }
        .snooze { display: inline-flex; align-items: stretch; }
        .snooze > button + button { border-left: none; padding-inline: {{layout.Gap}}px; }
        .snooze-options { display: flex; flex-wrap: wrap; gap: {{layout.CompactGap}}px; border-block: {{layout.Border}}px solid {{colors.Border.ToCss()}}; padding-block: {{layout.Gap}}px; }
        .card p { white-space: pre-wrap; }
        .card-icon { width: {{layout.IconSize}}px; height: {{layout.IconSize}}px; flex-shrink: 0; color: {{colors.Accent.ToCss()}}; }
        .card img { max-width: 100%; max-height: {{layout.ImageHeight}}px; object-fit: contain; align-self: flex-start; }
        .custom-template { display: block; width: 100%; height: {{AppConstants.Windows.Toast.Height}}px; border: none; background: {{colors.Background.ToCss()}}; }
        .card progress { width: 100%; height: {{layout.ProgressHeight}}px; accent-color: {{colors.Accent.ToCss()}}; }
        .card input, .card textarea { width: 100%; }
        .card label input[type=checkbox] { width: auto; }
        .notification-record { border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; padding: {{layout.Padding}}px; overflow-wrap: anywhere; }
        .notification-record > p { white-space: pre-wrap; }
        .notification-record .card { margin-top: {{layout.Gap}}px; }
        .record-heading, .record-meta { display: flex; justify-content: space-between; align-items: center; gap: {{layout.Gap}}px; flex-wrap: wrap; }
        .record-heading h2 { font-size: {{text.Body}}px; }
        .record-meta { color: {{colors.Muted.ToCss()}}; font-size: {{text.Small}}px; }
        .record-actions { margin-top: {{layout.Gap}}px; }
        details > summary { cursor: pointer; color: {{colors.Muted.ToCss()}}; padding: {{layout.CompactGap}}px 0; }
        details[open] > summary { margin-bottom: {{layout.Gap}}px; }
        .card-tools { display: flex; flex-wrap: wrap; gap: {{layout.Gap}}px; }
        .card-tools button { font-size: {{text.Small}}px; }
        .reply-result { border-left: {{layout.Border}}px solid {{colors.Border.ToCss()}}; padding-left: {{layout.Gap}}px; }
        .editor-header { display: flex; align-items: center; justify-content: space-between; gap: {{layout.Gap}}px; }
        .editor-items { display: flex; flex-direction: column; gap: {{layout.Gap}}px; }
        .editor-items .field { flex: 1; }
        .form-section { min-width: 0; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; padding: {{layout.Padding}}px; margin: 0; }
        .form-section legend { padding: 0 {{layout.Gap}}px; color: {{colors.Muted.ToCss()}}; font-weight: {{text.StrongWeight}}; }
        .form-section > .actions { margin-top: {{layout.Padding}}px; }
        .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: {{layout.Padding}}px; }
        label.field { display: flex; flex-direction: column; gap: {{layout.CompactGap}}px; }
        label.check { display: flex; align-items: center; gap: {{layout.Gap}}px; }
        .span-all { grid-column: 1 / -1; }
        .empty { display: flex; flex-direction: column; align-items: center; gap: {{layout.Padding}}px; padding: {{layout.PagePadding * 2}}px; color: {{colors.Muted.ToCss()}}; }
        .empty h2 { font-size: {{text.Body}}px; font-weight: {{text.NormalWeight}}; }
        .error, .status { padding: {{layout.Gap}}px {{layout.Padding}}px; border-left: {{layout.AccentBorder}}px solid {{colors.Levels.GetValueOrDefault(NotificationLevel.Error, colors.Accent).ToCss()}}; }
        .status { border-left-color: {{colors.Levels.GetValueOrDefault(NotificationLevel.Success, colors.Accent).ToCss()}}; }
        .page > .error, .page > .status { margin-inline: {{layout.PagePadding}}px; margin-bottom: {{layout.Padding}}px; }
        .tone { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: {{layout.Gap}}px; padding-block: {{layout.Gap}}px; border-bottom: {{layout.Border}}px solid {{colors.Border.ToCss()}}; }
        .tone-actions { grid-column: 1 / -1; display: flex; gap: {{layout.Gap}}px; }
        .application-exit { border-top: {{layout.Border}}px solid {{colors.Border.ToCss()}}; padding-top: {{layout.PagePadding}}px; }
        .resize-edge { position: fixed; touch-action: none; }
        .resize-edge.left, .resize-edge.right { top: {{layout.ResizeGrip}}px; bottom: {{layout.ResizeGrip}}px; width: {{layout.ResizeGrip}}px; cursor: ew-resize; }
        .resize-edge.left { left: 0; } .resize-edge.right { right: 0; }
        .resize-edge.top, .resize-edge.bottom { left: {{layout.ResizeGrip}}px; right: {{layout.ResizeGrip}}px; height: {{layout.ResizeGrip}}px; cursor: ns-resize; }
        .resize-edge.top { top: 0; } .resize-edge.bottom { bottom: 0; }
        .resize-edge.top-left, .resize-edge.top-right, .resize-edge.bottom-left, .resize-edge.bottom-right { width: {{layout.ResizeGrip}}px; height: {{layout.ResizeGrip}}px; }
        .resize-edge.top-left { top: 0; left: 0; cursor: nwse-resize; } .resize-edge.top-right { top: 0; right: 0; cursor: nesw-resize; }
        .resize-edge.bottom-left { bottom: 0; left: 0; cursor: nesw-resize; } .resize-edge.bottom-right { bottom: 0; right: 0; cursor: nwse-resize; }
        .dialog-backdrop { position: fixed; inset: 0; background: {{colors.Overlay.ToCss()}}; display: grid; place-items: center; padding: {{layout.PagePadding}}px; }
        .dialog { width: min(100%, {{AppConstants.Windows.MinimumPanel.Width - layout.PagePadding * 2}}px); background: {{colors.Background.ToCss()}}; border: {{layout.Border}}px solid {{colors.Border.ToCss()}}; padding: {{layout.PagePadding}}px; display: flex; flex-direction: column; gap: {{layout.Padding}}px; }
        .dialog .actions { justify-content: flex-end; }
        """);
        css += FormattableString.Invariant($$"""
        .shell { background: {{colors.Background.ToCss()}}; }
        .sidebar { background: {{colors.Background.ToCss()}}; }
        .page-header { flex-shrink: 0; }
        .card { isolation: isolate; border-radius: {{theme.Frame.CornerRadius}}px; }
        .card-header { gap: 10px; margin-bottom: 12px; }
        .sender-icon { display: grid; place-items: center; width: 32px; height: 32px; flex: 0 0 32px; border-radius: 8px; font-weight: 600; color: {{colors.Accent.ToCss()}}; background: {{System.Drawing.Color.FromArgb(28, colors.Accent).ToCss()}}; }
        .card-header .sender { color: {{colors.Text.ToCss()}}; font-size: {{text.Small}}px; }
        .card-header .meta { justify-content: space-between; }
        .card-content { margin-left: 0; gap: 10px; }
        .card-content h2, .card-content p { overflow-wrap: anywhere; white-space: pre-wrap; }
        .card-content h2 { font-size: {{text.CardTitle}}px; }
        .card-content p { font-size: {{text.Body}}px; }
        .card-actions { width: 100%; }
        .card-actions .domain-action { margin-left: auto; }
        .card-footer { margin-top: 8px; }
        .card.has-acknowledge { box-shadow: inset 0 0 0 1px {{System.Drawing.Color.FromArgb(153, colors.Accent).ToCss()}}, inset 0 0 29px 3px {{System.Drawing.Color.FromArgb(69, colors.Accent).ToCss()}}, 0 0 18px {{System.Drawing.Color.FromArgb(69, colors.Accent).ToCss()}}; }
        .acknowledge-target { position: absolute; inset: 0; z-index: 1; width: 100%; height: 100%; border: 0; border-radius: inherit; padding: 0; background: transparent; }
        .acknowledge-target:hover:not(:disabled) { background: {{System.Drawing.Color.FromArgb(10, colors.Accent).ToCss()}}; }
        .card.has-acknowledge > .card-header, .card.has-acknowledge > .card-content { position: relative; z-index: 2; pointer-events: none; }
        .card.has-acknowledge button:not(.acknowledge-target), .card.has-acknowledge input, .card.has-acknowledge textarea, .card.has-acknowledge select, .card.has-acknowledge label.check, .card.has-acknowledge a, .card.has-acknowledge summary { position: relative; pointer-events: auto; }
        .notification-record { display: grid; grid-template-columns: 38px minmax(0,1fr); column-gap: 14px; border-radius: 10px; background: {{colors.Surface.ToCss()}}; }
        .record-body { min-width: 0; display: flex; flex-direction: column; gap: 8px; }
        .record-body h2 { font-size: 18px; }
        .record-body p { white-space: pre-wrap; }
        .record-image { max-width: 100%; max-height: {{layout.ImageHeight}}px; object-fit: contain; align-self: flex-start; }
        .record-meta { justify-content: flex-start; gap: 10px; }
        .record-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; }
        .theme-badge { border: 1px solid {{colors.Border.ToCss()}}; border-radius: 5px; padding: 2px 6px; font-size: 12px; }
        .quiet { background: transparent; border-color: transparent; color: {{colors.Muted.ToCss()}}; }
        .themes-layout { display: grid; grid-template-columns: 200px minmax(0,1fr); gap: 24px; }
        .theme-tree { display: flex; flex-direction: column; gap: 4px; }
        .theme-tree button { text-align: left; border-color: transparent; background: transparent; }
        .theme-tree button[aria-current=true] { border-color: {{colors.Border.ToCss()}}; background: {{colors.Surface.ToCss()}}; }
        .theme-workspace { display: flex; flex-direction: column; gap: 18px; min-width: 0; }
        .theme-field { display: flex; flex-direction: column; gap: 6px; }
        .field-origin { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; font-size: 12px; color: {{colors.Muted.ToCss()}}; }
        .field-origin button { min-height: 24px; padding: 0 4px; font-size: inherit; border: 0; background: transparent; text-decoration: underline; }
        .theme-preview { max-width: 460px; }
        .theme-preview .card { padding: 18px; }
        input[type=color] { width: 100%; min-height: 38px; padding: 3px; }
        .editor .form-section > fieldset { border: 0; padding: 0; margin: 0; }
        .theme-summary { display: flex; flex-wrap: wrap; gap: 12px; color: {{colors.Muted.ToCss()}}; font-size: {{text.Small}}px; }
        .theme-summary span + span::before { content: '·'; margin-right: 12px; }
        .theme-workspace details h3 { margin-top: 20px; margin-bottom: 12px; }
        .theme-workspace > .actions { padding-bottom: 4px; }
        .theme-workspace .form-section > .form-grid { margin-top: 8px; }
        """);
        if (selector is not null)
            css = Regex.Replace(css, @"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}", rule =>
            {
                string[] targets = rule.Groups["selectors"].Value.Split(',').Select(target => target.Trim())
                    .Where(target => !target.StartsWith("html", StringComparison.Ordinal) && !target.StartsWith("body", StringComparison.Ordinal))
                    .Select(target => Regex.IsMatch(target, @"^\.card(?=\s|[:.#\[]|$)") ? Regex.Replace(target, @"^\.card", selector) : selector + " " + target).ToArray();
                return targets.Length == 0 ? string.Empty : string.Join(",", targets) + "{" + rule.Groups["body"].Value + "}";
            });
        if (selector is null) css += FormattableString.Invariant($$"""
        @media (max-width: {{layout.NarrowPanel}}px) {
            .sidebar { width: {{layout.SidebarWidth - layout.PagePadding}}px; }
            .form-grid, .tone { grid-template-columns: minmax(0, 1fr); }
            .toolbar { align-items: stretch; }
            .toolbar .search-field { flex-basis: 100%; }
            .themes-layout { grid-template-columns: minmax(0, 1fr); }
            .theme-tree { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); }
        }
        """);
        if (StyleCache.Count >= AppConstants.RenderMemory.Styles) StyleCache.Clear();
        StyleCache[key] = css;
        return css;
    }

    /// <summary>Formats minute choices with the active language's singular or plural resource.</summary>
    /// <param name="minutes">Duration expressed in minutes.</param>
    /// <returns>The localized duration label.</returns>
    public static string MinutesLabel(double minutes) => string.Format(minutes == 1 ? Strings.MinuteSingular : Strings.MinuteFormat, minutes);
    /// <summary>Gets the per-card palette without introducing an independent CSS token system.</summary>
    /// <param name="entry">The notification whose theme and semantic color are resolved.</param>
    /// <returns>Inline style generated from its theme objects.</returns>
    public static string CardStyle(NotificationEntry entry)
    {
        NotificationTheme theme = entry.Current.Resolved?.Theme ?? ThemeCatalog.For(entry.Request);
        ThemePalette dark = SchemePalette(theme, light: false), light = SchemePalette(theme, light: true);
        NotificationFrame frame = theme.Frame;
        string Pair(System.Drawing.Color first, System.Drawing.Color second) => $"light-dark({first.ToCss()},{second.ToCss()})";
        string Color(BorderEdges edge) => Pair(frame.AccentEdges.HasFlag(edge) ? frame.AccentColor ?? light.Accent : light.Border, frame.AccentEdges.HasFlag(edge) ? frame.AccentColor ?? dark.Accent : dark.Border);
        return FormattableString.Invariant($"background:{Pair(light.Background, dark.Background)};color:{Pair(light.Text, dark.Text)};border-style:{frame.Pattern.ToString().ToLowerInvariant()};border-width:{frame.Thickness.Top}px {frame.Thickness.Right}px {frame.Thickness.Bottom}px {frame.Thickness.Left}px;border-color:{Color(BorderEdges.Top)} {Color(BorderEdges.Right)} {Color(BorderEdges.Bottom)} {Color(BorderEdges.Left)};border-radius:{frame.CornerRadius}px;padding:{theme.Layout.Padding}px;font-family:{theme.Typography.Family};font-size:{theme.Typography.Body}px");
    }

    /// <summary>Projects paired theme colors into one system scheme while retaining explicitly customized foreground fields.</summary>
    /// <param name="theme">Captured theme with paired palette colors and explicit-field provenance.</param>
    /// <param name="light">True selects its light-background and light-accent fields.</param>
    /// <returns>A concrete palette without scheme pairs, suitable for generating a scoped stylesheet.</returns>
    static ThemePalette SchemePalette(NotificationTheme theme, bool light)
    {
        ThemePalette palette = theme.Palette;
        var background = (light ? palette.LightBackground : palette.DarkBackground) ?? palette.Background;
        var accent = (light ? palette.LightAccent : palette.DarkAccent) ?? palette.Accent;
        bool isLight = UsesDarkText(background);
        System.Drawing.Color Pick(string lightColor, string darkColor) => System.Drawing.ColorTranslator.FromHtml(isLight ? lightColor : darkColor);
        bool paired = palette.LightBackground is not null || palette.DarkBackground is not null;
        bool IsOwned(string field) => theme.ExplicitFields.Contains("Palette." + field);
        return palette with
        {
            Background = background, Accent = accent, LightBackground = null, DarkBackground = null, LightAccent = null, DarkAccent = null,
            Text = paired && !IsOwned(nameof(palette.Text)) ? Pick("#202020", "#f2f2f2") : palette.Text,
            Muted = paired && !IsOwned(nameof(palette.Muted)) ? Pick("#636363", "#b4b4b4") : palette.Muted,
            Surface = paired && !IsOwned(nameof(palette.Surface)) ? Pick("#ffffff", "#242424") : palette.Surface,
            Border = paired && !IsOwned(nameof(palette.Border)) ? Pick("#d2d2d2", "#414141") : palette.Border,
            AccentText = paired && !IsOwned(nameof(palette.AccentText)) ? UsesDarkText(accent) ? System.Drawing.ColorTranslator.FromHtml("#171717") : System.Drawing.Color.White : palette.AccentText
        };
    }

    /// <summary>Selects dark foreground where its relative-luminance contrast exceeds a white foreground.</summary>
    /// <param name="color">Background or accent color behind the foreground.</param>
    /// <returns>True chooses dark text, including on saturated yellow and green surfaces.</returns>
    static bool UsesDarkText(System.Drawing.Color color)
    {
        static double Linear(byte channel) { double value = channel / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B) > .179;
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
        html = html.Replace("{{themeCss}}", Render(entry.Current.Resolved?.Theme ?? ThemeCatalog.For(entry.Request)), StringComparison.Ordinal)
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
        if (!uri.IsFile && AppConstants.RenderMemory.ImageSchemes.Contains(uri.Scheme)) return uri.AbsoluteUri;
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
