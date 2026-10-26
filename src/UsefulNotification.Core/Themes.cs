using System.Runtime.CompilerServices;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsefulNotification;

/// <summary>Groups colors, typography, and spacing for built-in and user-defined HTML themes.</summary>
public sealed record NotificationTheme
{
    /// <summary>Rejects missing required sections before a request or file-backed theme reaches rendering.</summary>
    internal void Validate()
    {
        if (Palette is null || Palette.Levels is null || Typography is null || Layout is null || Frame is null || Frame.Thickness is null)
            throw new ArgumentException("A theme requires palette, level colors, typography, layout and frame sections.");
    }
    /// <summary>Editable theme identity used by host files and UI; initially Dark. Requests transfer the complete object rather than resolving this name.</summary>
    public string Name { get; set; } = nameof(NotificationThemes.Dark);
    /// <summary>Complete surface and semantic colors. Initialized independently for a newly constructed theme.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public ThemePalette Palette { get; set; } = new();
    /// <summary>Font family, text sizes and line metrics used to generate the HTML styles.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public ThemeTypography Typography { get; set; } = new();
    /// <summary>Spacing and control dimensions used to generate the HTML styles.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public ThemeLayout Layout { get; set; } = new();
    /// <summary>HTML border, corner and accent settings; native Windows chrome stays removed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public NotificationFrame Frame { get; set; } = new();
    /// <summary>Optional default level applied when a new request supplies no Level.</summary>
    public NotificationLevel? DefaultLevel { get; set; }
}
/// <summary>Overrides selected parts of a named theme for one sender's notification.</summary>
public sealed record NotificationAppearance
{
    /// <summary>Optional complete palette replacement; null inherits the selected theme palette.</summary>
    public ThemePalette? Palette { get; set; }
    /// <summary>Optional complete typography replacement; null inherits the selected theme typography.</summary>
    public ThemeTypography? Typography { get; set; }
    /// <summary>Optional complete layout replacement; null inherits the selected theme layout.</summary>
    public ThemeLayout? Layout { get; set; }
    /// <summary>Optional complete HTML frame replacement; null inherits the selected theme frame.</summary>
    public NotificationFrame? Frame { get; set; }
}
/// <summary>Selects HTML border edges that use the semantic notification accent.</summary>
[Flags]
public enum BorderEdges
{
    /// <summary>Uses the regular palette border on every edge.</summary>
    None = 0,
    /// <summary>Uses the notification accent on the top edge.</summary>
    Top = 1,
    /// <summary>Uses the notification accent on the right edge.</summary>
    Right = 2,
    /// <summary>Uses the notification accent on the bottom edge.</summary>
    Bottom = 4,
    /// <summary>Uses the notification accent on the left edge.</summary>
    Left = 8,
    /// <summary>Uses the notification accent on all four edges.</summary>
    All = Top | Right | Bottom | Left
}
/// <summary>Selects a standard HTML border pattern.</summary>
public enum BorderPattern
{
    /// <summary>Draws a continuous HTML border stroke.</summary>
    Solid,
    /// <summary>Draws a dashed HTML border stroke.</summary>
    Dashed,
    /// <summary>Draws a dotted HTML border stroke.</summary>
    Dotted,
    /// <summary>Draws the HTML double-line border pattern.</summary>
    Double,
    /// <summary>Suppresses the HTML border stroke.</summary>
    None
}
/// <summary>Defines all visible notification framing; none of these edges are drawn by Windows.</summary>
public sealed record NotificationFrame
{
    /// <summary>Independent border widths in CSS pixels; defaults to one on top/right/bottom and five on the left.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public BorderThickness Thickness { get; set; } = new();
    /// <summary>HTML corner radius in CSS pixels, initially 0.</summary>
    public double CornerRadius { get; set; } = 0;
    /// <summary>HTML border stroke pattern, initially Solid.</summary>
    public BorderPattern Pattern { get; set; } = BorderPattern.Solid;
    /// <summary>Border edges using AccentColor or semantic level color, initially Left.</summary>
    public BorderEdges AccentEdges { get; set; } = BorderEdges.Left;
    /// <summary>Explicit accent color; null selects the effective level color, then palette accent.</summary>
    public Color? AccentColor { get; set; }
}
/// <summary>Stores independent HTML border widths in logical pixels.</summary>
public sealed record BorderThickness
{
    /// <summary>Top HTML border width in CSS pixels, initially 1.</summary>
    public double Top { get; set; } = 1;
    /// <summary>Right HTML border width in CSS pixels, initially 1.</summary>
    public double Right { get; set; } = 1;
    /// <summary>Bottom HTML border width in CSS pixels, initially 1.</summary>
    public double Bottom { get; set; } = 1;
    /// <summary>Left HTML border width in CSS pixels, initially 5.</summary>
    public double Left { get; set; } = 5;
}
/// <summary>Defines surface and semantic colors; no CSS variable is a second source of truth.</summary>
public sealed record ThemePalette
{
    /// <summary>Modal backdrop color, initially translucent black.</summary>
    public Color Overlay { get; set; } = Color.FromArgb(160, Color.Black);
    /// <summary>Page and card background, initially the dark preset surface.</summary>
    public Color Background { get; set; } = ColorTranslator.FromHtml("#191919");
    /// <summary>Secondary control surface color.</summary>
    public Color Surface { get; set; } = ColorTranslator.FromHtml("#242424");
    /// <summary>Unaccented border and divider color.</summary>
    public Color Border { get; set; } = ColorTranslator.FromHtml("#393939");
    /// <summary>Primary foreground color.</summary>
    public Color Text { get; set; } = ColorTranslator.FromHtml("#f2f2f2");
    /// <summary>Secondary foreground color for metadata and timestamps.</summary>
    public Color Muted { get; set; } = ColorTranslator.FromHtml("#a8a8a8");
    /// <summary>Fallback accent color when no level mapping or explicit frame accent applies.</summary>
    public Color Accent { get; set; } = ColorTranslator.FromHtml("#3478ed");
    /// <summary>Text foreground for accent-filled controls.</summary>
    public Color AccentText { get; set; } = ColorTranslator.FromHtml("#ffffff");
    /// <summary>Editable semantic color map. Missing level entries fall back to Accent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public Dictionary<NotificationLevel, Color> Levels { get; set; } = new()
    {
        [NotificationLevel.Normal] = ColorTranslator.FromHtml("#939eac"), [NotificationLevel.Info] = ColorTranslator.FromHtml("#3478ed"), [NotificationLevel.Success] = ColorTranslator.FromHtml("#49b58c"),
        [NotificationLevel.Warning] = ColorTranslator.FromHtml("#d5aa4c"), [NotificationLevel.Error] = ColorTranslator.FromHtml("#e06b70"), [NotificationLevel.Urgent] = ColorTranslator.FromHtml("#e34c58"),
        [NotificationLevel.Deadline] = ColorTranslator.FromHtml("#d98b55"), [NotificationLevel.Meeting] = ColorTranslator.FromHtml("#8496da"), [NotificationLevel.Health] = ColorTranslator.FromHtml("#69b8aa"),
        [NotificationLevel.Break] = ColorTranslator.FromHtml("#78ad79"), [NotificationLevel.Personal] = ColorTranslator.FromHtml("#b28ac7")
    };
}
/// <summary>Defines the font family and semantic text sizes used to generate CSS.</summary>
public sealed record ThemeTypography
{
    /// <summary>CSS font-family fallback list, initially Segoe UI, Arial, sans-serif.</summary>
    public string Family { get; set; } = "Segoe UI, Arial, sans-serif";
    /// <summary>Body text size in CSS pixels, initially 13.</summary>
    public int Body { get; set; } = 13;
    /// <summary>Metadata text size in CSS pixels, initially 12.</summary>
    public int Small { get; set; } = 12;
    /// <summary>Panel heading size in CSS pixels, initially 21.</summary>
    public int Heading { get; set; } = 21;
    /// <summary>Notification heading size in CSS pixels, initially 18.</summary>
    public int CardTitle { get; set; } = 18;
    /// <summary>Normal CSS font weight, initially 400.</summary>
    public int NormalWeight { get; set; } = 400;
    /// <summary>Emphasized CSS font weight, initially 600.</summary>
    public int StrongWeight { get; set; } = 600;
    /// <summary>Body line-height multiplier, initially 1.45.</summary>
    public double LineHeight { get; set; } = 1.45;
    /// <summary>Heading line-height multiplier, initially 1.2.</summary>
    public double HeadingLineHeight { get; set; } = 1.2;
}
/// <summary>Defines the layout measurements as one editable theme object.</summary>
public sealed record ThemeLayout
{
    /// <summary>Invisible HTML sizing-edge target width in CSS pixels, initially 5.</summary>
    public int ResizeGrip { get; set; } = 5;
    /// <summary>Width below which panel forms use one column, in CSS pixels, initially 720.</summary>
    public int NarrowPanel { get; set; } = 720;
    /// <summary>Tight spacing in CSS pixels, initially 4.</summary>
    public int CompactGap { get; set; } = 4;
    /// <summary>Standard spacing in CSS pixels, initially 8.</summary>
    public int Gap { get; set; } = 8;
    /// <summary>Card/control padding in CSS pixels, initially 12.</summary>
    public int Padding { get; set; } = 12;
    /// <summary>Panel page padding in CSS pixels, initially 20.</summary>
    public int PagePadding { get; set; } = 20;
    /// <summary>General control border width in CSS pixels, initially 1.</summary>
    public int Border { get; set; } = 1;
    /// <summary>Default accent strip width in CSS pixels, initially 5.</summary>
    public int AccentBorder { get; set; } = 5;
    /// <summary>General control corner radius in CSS pixels, initially 0.</summary>
    public int Radius { get; set; } = 0;
    /// <summary>Button minimum height in CSS pixels, initially 30.</summary>
    public int ButtonHeight { get; set; } = 30;
    /// <summary>Panel header height in CSS pixels, initially 40.</summary>
    public int HeaderHeight { get; set; } = 40;
    /// <summary>Standard icon dimension in CSS pixels, initially 22.</summary>
    public int IconSize { get; set; } = 22;
    /// <summary>Panel navigation width in CSS pixels, initially 170.</summary>
    public int SidebarWidth { get; set; } = 170;
    /// <summary>Image content height limit in CSS pixels, initially 190.</summary>
    public int ImageHeight { get; set; } = 190;
    /// <summary>Progress track height in CSS pixels, initially 6.</summary>
    public int ProgressHeight { get; set; } = 6;
    /// <summary>Initial multiline reply row count, initially 0.</summary>
    public int ReplyRows { get; set; } = 3;
}
/// <summary>Exposes complete mutable theme objects directly to C# callers.</summary>
public static class NotificationThemes
{
    /// <summary>Mutable dark preset and initial host theme. Send captures its current complete value.</summary>
    public static NotificationTheme Dark { get; set; } = new();
    /// <summary>Mutable light-surface preset with dark text.</summary>
    public static NotificationTheme Light { get; set; } = new() { Name = nameof(Light), Palette = new() { Background = ColorTranslator.FromHtml("#f6f7f9"), Surface = ColorTranslator.FromHtml("#ffffff"), Border = ColorTranslator.FromHtml("#c7ccd4"), Text = ColorTranslator.FromHtml("#20252d"), Muted = ColorTranslator.FromHtml("#606978"), Accent = ColorTranslator.FromHtml("#2563b5") } };
    /// <summary>Mutable dark preset with a blue accent.</summary>
    public static NotificationTheme Blue { get; set; } = new() { Name = nameof(Blue) };
    /// <summary>Mutable dark preset with a success-green accent.</summary>
    public static NotificationTheme Emerald { get; set; } = new() { Name = nameof(Emerald) };
    /// <summary>Mutable dark preset with a purple accent.</summary>
    public static NotificationTheme Purple { get; set; } = new() { Name = nameof(Purple), Palette = new() { Accent = ColorTranslator.FromHtml("#a68ac9") } };
    /// <summary>Mutable dark preset with a warning-amber accent.</summary>
    public static NotificationTheme Amber { get; set; } = new() { Name = nameof(Amber) };
    /// <summary>Mutable dark preset with a red accent.</summary>
    public static NotificationTheme Crimson { get; set; } = new() { Name = nameof(Crimson), Palette = new() { Accent = ColorTranslator.FromHtml("#dc6571") } };
    /// <summary>Mutable information preset with a single semantic accent strip.</summary>
    public static NotificationTheme Info { get; set; } = new() { Name = nameof(Info), DefaultLevel = NotificationLevel.Info, Frame = new() };
    /// <summary>Mutable warning preset with a single semantic accent strip.</summary>
    public static NotificationTheme Warn { get; set; } = new() { Name = nameof(Warn), DefaultLevel = NotificationLevel.Warning, Frame = new() };
    /// <summary>Mutable error preset with a single semantic accent strip.</summary>
    public static NotificationTheme Error { get; set; } = new() { Name = nameof(Error), DefaultLevel = NotificationLevel.Error, Frame = new() };
    /// <summary>Mutable success preset with a single semantic accent strip.</summary>
    public static NotificationTheme Happy { get; set; } = new() { Name = nameof(Happy), DefaultLevel = NotificationLevel.Success, Frame = new() };
    /// <summary>Mutable black/white preset with a yellow accent.</summary>
    public static NotificationTheme HighContrast { get; set; } = new() { Name = nameof(HighContrast), Palette = new() { Background = ColorTranslator.FromHtml("#000000"), Surface = ColorTranslator.FromHtml("#000000"), Border = ColorTranslator.FromHtml("#ffffff"), Text = ColorTranslator.FromHtml("#ffffff"), Muted = ColorTranslator.FromHtml("#ffffff"), Accent = ColorTranslator.FromHtml("#ffff00"), AccentText = ColorTranslator.FromHtml("#000000") } };
    /// <summary>A new list containing the actual shared preset objects. Editing a preset affects later sends; already accepted requests own their snapshots.</summary>
    public static List<NotificationTheme> All => [Dark, Light, Blue, Emerald, Purple, Amber, Crimson, Info, Warn, Error, Happy, HighContrast];

    /// <summary>Derives preset accents from their complete palette objects.</summary>
    static NotificationThemes()
    {
        Emerald.Palette.Accent = Dark.Palette.Levels[NotificationLevel.Success];
        Amber.Palette.Accent = Dark.Palette.Levels[NotificationLevel.Warning];
        foreach (var theme in new[] { Info, Warn, Error, Happy })
        {
            theme.Palette.Accent = theme.Palette.Levels[theme.DefaultLevel!.Value]; theme.Frame.AccentColor = theme.Palette.Accent;
            if (theme != Info) theme.Palette.AccentText = Dark.Palette.Background;
        }
    }
}
/// <summary>Exposes ready-to-use sound sequences without names or numeric preset indexes.</summary>
public static class NotificationSounds
{
    /// <summary>Mutable three-tone rising chime. The current complete sequence is captured by Send.</summary>
    public static SoundPattern ChimeUp { get; set; } = new() { Name = nameof(ChimeUp), Steps = [new(900, TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(60)), new(1200, TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(60)), new(1500, TimeSpan.FromMilliseconds(300), TimeSpan.Zero)] };
    /// <summary>Mutable two-tone repeated ping.</summary>
    public static SoundPattern DualPing { get; set; } = new() { Name = nameof(DualPing), Steps = [new(1050, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(100)), new(1050, TimeSpan.FromMilliseconds(150), TimeSpan.Zero)] };
    /// <summary>Mutable two-tone descending attention signal.</summary>
    public static SoundPattern HighUrgency { get; set; } = new() { Name = nameof(HighUrgency), Steps = [new(1600, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(70)), new(1200, TimeSpan.FromMilliseconds(120), TimeSpan.Zero)] };
    /// <summary>Mutable single 700-hertz tone lasting 90 milliseconds.</summary>
    public static SoundPattern Subtle { get; set; } = new() { Name = nameof(Subtle), Steps = [new(700, TimeSpan.FromMilliseconds(90), TimeSpan.Zero)] };
    /// <summary>A new list containing the actual shared sound objects; use a detached copy when editing a local draft.</summary>
    public static List<SoundPattern> All => [ChimeUp, DualPing, HighUrgency, Subtle];
}
/// <summary>Loads file-backed themes at text boundaries; application code passes theme objects.</summary>
internal static class ThemeCatalog
{
    public static long Revision;
    public static List<NotificationTheme> Themes = NotificationThemes.All;

    /// <summary>Reads theme JSON files without modifying built-in definitions.</summary>
    public static void Load()
    {
        string directory = Path.Combine(AppPaths.UserData, AppConstants.Storage.ThemesDirectory);
        Directory.CreateDirectory(directory);
        var loaded = NotificationThemes.All;
        foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var result = RunSafely(() =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("A theme file must contain a theme object.");
                var theme = document.RootElement.Deserialize<NotificationTheme>(AppConstants.Json) ?? throw new InvalidDataException(path);
                ArgumentException.ThrowIfNullOrWhiteSpace(theme.Name);
                theme.Validate();
                return theme;
            });
            if (result.Succeeded && result.Value is { } theme)
            {
                loaded.RemoveAll(item => item.Name.Equals(theme.Name, StringComparison.OrdinalIgnoreCase));
                loaded.Add(theme);
            }
        }
        Themes = loaded;
        Interlocked.Increment(ref Revision);
    }

    /// <summary>Resolves a per-request theme or the selected application theme.</summary>
    /// <param name="name">Optional request theme name.</param>
    /// <param name="required">True rejects an unknown name after reloading file-backed themes; false falls back to Dark.</param>
    /// <returns>A matching theme, falling back to the default dark theme.</returns>
    public static NotificationTheme Get(string? name = null, bool required = false)
    {
        NotificationTheme? selected = Themes.FirstOrDefault(theme => theme.Name.Equals(name ?? Settings.Theme.Value, StringComparison.OrdinalIgnoreCase));
        if (selected is null && required)
        {
            Load();
            selected = Themes.FirstOrDefault(theme => theme.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        return selected ?? (required ? throw new ArgumentException($"Unknown notification theme: {name}") : NotificationThemes.Dark);
    }

    /// <summary>Combines a named theme with the sender's optional appearance objects.</summary>
    /// <param name="request">The notification's display request.</param>
    /// <returns>The effective complete theme.</returns>
    public static NotificationTheme For(NotificationRequest request)
    {
        NotificationTheme theme = request.Theme ?? Get();
        return request.Appearance is not { } custom ? theme : theme with
        { Palette = custom.Palette ?? theme.Palette, Typography = custom.Typography ?? theme.Typography, Layout = custom.Layout ?? theme.Layout, Frame = custom.Frame ?? theme.Frame };
    }
}
/// <summary>Accepts legacy wire names while preserving full structured themes in requests and journals.</summary>
internal sealed class NotificationThemeConverter : JsonConverter<NotificationTheme>
{
    readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> ObjectOptions = new();

    /// <summary>Reads a legacy wire name or a complete theme object.</summary>
    public override NotificationTheme Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType == JsonTokenType.String
        ? throw new JsonException("Theme names must be resolved by the host text boundary.")
        : JsonSerializer.Deserialize<NotificationTheme>(ref reader, Structured(options)) ?? throw new JsonException("A theme object is required.");

    /// <summary>Writes the full object so sender changes cannot be lost through name lookup.</summary>
    public override void Write(Utf8JsonWriter writer, NotificationTheme value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, Structured(options));

    /// <summary>Reuses serialization options without this boundary converter to avoid recursion.</summary>
    JsonSerializerOptions Structured(JsonSerializerOptions options) => ObjectOptions.GetValue(options, source =>
    {
        var structured = new JsonSerializerOptions(source);
        structured.Converters.Remove(this);
        return structured;
    });
}
/// <summary>Stores user-created sound presets and persistent panel placement.</summary>
internal sealed record DesktopPreferences
{
    public List<SoundPattern> Sounds { get; set; } = NotificationProtocol.Copy(NotificationSounds.All);
    public Guid SelectedSoundId { get; set; } = NotificationSounds.ChimeUp.Id;
    [JsonIgnore] public SoundPattern SelectedSound { get => Sounds.FirstOrDefault(sound => sound.Id == SelectedSoundId) ?? Sounds.FirstOrDefault() ?? NotificationSounds.ChimeUp; set => SelectedSoundId = value.Id; }
    public System.Drawing.Rectangle? PanelBounds { get; set; }
}
