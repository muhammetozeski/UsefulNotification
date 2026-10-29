using System.Runtime.CompilerServices;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsefulNotification;

/// <summary>Bundles HTML appearance, sound and delivery changes with one actual parent definition.</summary>
public sealed record NotificationTheme
{
    /// <summary>Stable identity independent of the editable name. Built-in identities begin with builtin:.</summary>
    public string Id { get; set; } = "custom:" + Guid.NewGuid().ToString("N");
    /// <summary>Actual parent definition. Its omitted fields remain live inheritance until a request is accepted.</summary>
    public NotificationTheme? Parent { get; set; }
    /// <summary>Only the fields owned by this definition. Clearing a member resumes inheritance from Parent.</summary>
    public NotificationOverrides Overrides { get; set; } = new();
    ThemePalette palette = new(); ThemeTypography typography = new(); ThemeLayout layout = new(); NotificationFrame frame = new();
    internal bool HasPalette, HasTypography, HasLayout, HasFrame;
    [JsonInclude] internal string[]? ExplicitSections
    {
        get => new[] { (nameof(Palette), HasPalette), (nameof(Typography), HasTypography), (nameof(Layout), HasLayout), (nameof(Frame), HasFrame) }.Where(section => section.Item2).Select(section => section.Item1).ToArray();
        set
        {
            if (value is null) return;
            HasPalette = value.Contains(nameof(Palette)); HasTypography = value.Contains(nameof(Typography)); HasLayout = value.Contains(nameof(Layout)); HasFrame = value.Contains(nameof(Frame));
        }
    }
    [JsonInclude] internal Dictionary<string, string>? CapturedOrigins { get; set; }
    [JsonInclude] internal HashSet<string> ExplicitFields { get; set; } = [];
    [JsonInclude] internal bool? CapturedContentLifetime { get; set; }
    [JsonInclude] internal bool? CapturedContentInteraction { get; set; }
    /// <summary>Rejects missing required sections before a request or file-backed theme reaches rendering.</summary>
    internal void Validate()
    {
        if (Palette is null || Palette.Levels is null || Typography is null || Layout is null || Frame is null || Frame.Thickness is null)
            throw new ArgumentException("A theme requires palette, level colors, typography, layout and frame sections.");
        if (string.IsNullOrWhiteSpace(Typography.Family) || new[] { Typography.Body, Typography.Small, Typography.Heading, Typography.CardTitle, Typography.NormalWeight, Typography.StrongWeight }.Any(value => value <= 0) ||
            !double.IsFinite(Typography.LineHeight) || Typography.LineHeight <= 0 || !double.IsFinite(Typography.HeadingLineHeight) || Typography.HeadingLineHeight <= 0)
            throw new ArgumentException("Theme font sizes, weights and line heights must be positive and the font family must be nonblank.");
        if (typeof(ThemeLayout).GetProperties().Any(property => (int)property.GetValue(Layout)! < 0) ||
            new[] { Frame.CornerRadius, Frame.Thickness.Top, Frame.Thickness.Right, Frame.Thickness.Bottom, Frame.Thickness.Left }.Any(value => !double.IsFinite(value) || value < 0) ||
            !Enum.IsDefined(Frame.Pattern) || (Frame.AccentEdges & ~BorderEdges.All) != 0)
            throw new ArgumentException("Theme measurements must be finite and nonnegative, with valid border patterns and edges.");
    }
    /// <summary>Editable theme identity used by host files and UI; initially Dark. Requests transfer the complete object rather than resolving this name.</summary>
    public string Name { get; set; } = nameof(NotificationThemes.Dark);
    /// <summary>Complete surface and semantic colors. Initialized independently for a newly constructed theme.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public ThemePalette Palette { get => palette; set { palette = value; HasPalette = true; } }
    /// <summary>Font family, text sizes and line metrics used to generate the HTML styles.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public ThemeTypography Typography { get => typography; set { typography = value; HasTypography = true; } }
    /// <summary>Spacing and control dimensions used to generate the HTML styles.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public ThemeLayout Layout { get => layout; set { layout = value; HasLayout = true; } }
    /// <summary>HTML border, corner and accent settings; native Windows chrome stays removed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public NotificationFrame Frame { get => frame; set { frame = value; HasFrame = true; } }
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
    /// <summary>Optional light-system card background; null uses Background.</summary>
    public Color? LightBackground { get; set; }
    /// <summary>Optional dark-system card background; null uses Background.</summary>
    public Color? DarkBackground { get; set; }
    /// <summary>Optional light-system accent; null uses Accent.</summary>
    public Color? LightAccent { get; set; }
    /// <summary>Optional dark-system accent; null uses Accent.</summary>
    public Color? DarkAccent { get; set; }
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
/// <summary>Actual inheritable theme definitions bundling appearance, sound and delivery policies.</summary>
public static class NotificationThemes
{
    /// <summary>Root paired light/dark appearance with a gentle bell, eight-second default and click acknowledgement.</summary>
    public static NotificationTheme Base { get; set; } = new()
    {
        Id = "builtin:base", Name = nameof(Base),
        Typography = new() { Body = 16, CardTitle = 20 },
        Frame = new() { Thickness = new() { Left = 1 }, AccentEdges = BorderEdges.None },
        Overrides = new()
        {
            Palette = new() { Background = new(Html("#18181b")), Accent = new(Html("#60a5fa")), LightBackground = new(Color.White), DarkBackground = new(Html("#18181b")), LightAccent = new(Html("#315d9c")), DarkAccent = new(Html("#60a5fa")) },
            Sound = new(NotificationSounds.Subtle), Priority = new(NotificationPriority.Normal)
        }
    };
    /// <summary>Ordinary notification definition inheriting every root field.</summary>
    public static NotificationTheme Daily { get; set; } = new() { Id = "builtin:daily", Name = nameof(Daily), Parent = Base };
    /// <summary>Positive paired colors, a gentle two-note sound and six-second lifetime.</summary>
    public static NotificationTheme Success { get; set; } = new()
    {
        Id = "builtin:success", Name = nameof(Success), Parent = Daily,
        Overrides = new() { Palette = Colors("#e9f5ed", "#193426", "#24734f", "#8ed6ac"), Sound = new(NotificationSounds.Completed), Lifetime = new(new ExpiringLifetime(TimeSpan.FromSeconds(6))) }
    };
    /// <summary>Attention colors and clear twin tones, with twelve seconds to read.</summary>
    public static NotificationTheme Attention { get; set; } = new()
    {
        Id = "builtin:attention", Name = nameof(Attention), Parent = Daily,
        Overrides = new() { Palette = Colors("#fff1d5", "#3b2c13", "#8a580c", "#f1c46f"), Sound = new(NotificationSounds.DoubleAlert), Lifetime = new(new ExpiringLifetime(TimeSpan.FromSeconds(12))) }
    };
    /// <summary>Time-sensitive attention child with exactly three flat square beeps, acknowledgement lifetime and high queue priority.</summary>
    public static NotificationTheme Urgent { get; set; } = new()
    {
        Id = "builtin:urgent", Name = nameof(Urgent), Parent = Attention,
        Overrides = new() { Palette = Colors("#fde5e9", "#511d28", "#b32d40", "#ffb6bf"), Sound = new(NotificationSounds.Attention), Lifetime = new(NotificationLifetimes.UntilAcknowledged), Priority = new(NotificationPriority.High) }
    };
    /// <summary>Daily's reminder sibling with warm plucked tones and acknowledgement lifetime.</summary>
    public static NotificationTheme Reminder { get; set; } = new()
    {
        Id = "builtin:reminder", Name = nameof(Reminder), Parent = Daily,
        Overrides = new() { Palette = Colors("#f0ebf8", "#2b2339", "#75549d", "#c8b4ea"), Sound = new(NotificationSounds.Reminder), Lifetime = new(NotificationLifetimes.UntilAcknowledged) }
    };
    /// <summary>Legacy neutral dark appearance retained for callers and the application panel.</summary>
    public static NotificationTheme Dark { get; set; } = new() { Id = "builtin:dark", Name = nameof(Dark) };
    /// <summary>Legacy light-surface appearance retained for existing callers.</summary>
    public static NotificationTheme Light { get; set; } = new() { Id = "builtin:light", Name = nameof(Light), Palette = new() { Background = Html("#f6f7f9"), Surface = Color.White, Border = Html("#c7ccd4"), Text = Html("#20252d"), Muted = Html("#606978"), Accent = Html("#2563b5") } };
    /// <summary>Legacy alias for the actual Daily definition.</summary>
    public static NotificationTheme Blue { get => Daily; set => Daily = value; }
    /// <summary>Legacy alias for the actual Success definition.</summary>
    public static NotificationTheme Emerald { get => Success; set => Success = value; }
    /// <summary>Legacy purple appearance retained for existing callers.</summary>
    public static NotificationTheme Purple { get; set; } = new() { Id = "builtin:purple", Name = nameof(Purple), Palette = new() { Accent = Html("#a68ac9") } };
    /// <summary>Legacy alias for the actual Attention definition.</summary>
    public static NotificationTheme Amber { get => Attention; set => Attention = value; }
    /// <summary>Legacy alias for the actual Urgent definition.</summary>
    public static NotificationTheme Crimson { get => Urgent; set => Urgent = value; }
    /// <summary>Legacy alias for the actual Daily definition.</summary>
    public static NotificationTheme Info { get => Daily; set => Daily = value; }
    /// <summary>Legacy alias for the actual Attention definition.</summary>
    public static NotificationTheme Warn { get => Attention; set => Attention = value; }
    /// <summary>Legacy alias for the actual Urgent definition.</summary>
    public static NotificationTheme Error { get => Urgent; set => Urgent = value; }
    /// <summary>Legacy alias for the actual Success definition.</summary>
    public static NotificationTheme Happy { get => Success; set => Success = value; }
    /// <summary>Legacy black/white appearance with a yellow accent.</summary>
    public static NotificationTheme HighContrast { get; set; } = new() { Id = "builtin:highcontrast", Name = nameof(HighContrast), Palette = new() { Background = Color.Black, Surface = Color.Black, Border = Color.White, Text = Color.White, Muted = Color.White, Accent = Color.Yellow, AccentText = Color.Black } };
    /// <summary>The six useful shared definitions in parent-first order. Existing accepted requests own detached snapshots.</summary>
    public static List<NotificationTheme> All => [Base, Daily, Success, Attention, Urgent, Reminder];
    internal static List<NotificationTheme> Legacy => [Dark, Light, Purple, HighContrast];
    /// <summary>Builds the four independent system-color overrides used by a semantic child.</summary>
    static ThemePaletteOverrides Colors(string lightBackground, string darkBackground, string lightAccent, string darkAccent) => new()
    {
        LightBackground = new(Html(lightBackground)), DarkBackground = new(Html(darkBackground)), LightAccent = new(Html(lightAccent)), DarkAccent = new(Html(darkAccent)),
        Background = new(Html(darkBackground)), Accent = new(Html(darkAccent))
    };
    /// <summary>Converts a built-in hexadecimal color literal.</summary>
    static Color Html(string color) => ColorTranslator.FromHtml(color);
}
/// <summary>Actual synthesized voices and notes; names are labels rather than sound lookup keys.</summary>
public static class NotificationSounds
{
    /// <summary>Soft, slowly decaying bell at 1047 hertz and low gain.</summary>
    public static SoundPattern Subtle { get; set; } = new()
    {
        Id = new("10000000-0000-0000-0000-000000000001"), Name = nameof(Subtle), Steps = [new(1047, TimeSpan.FromMilliseconds(1250), TimeSpan.Zero)],
        Synthesis = new() { Volume = .055, Waveform = new HarmonicWaveform { Partials = [new(1, 1), new(2.756, .16), new(5.4, .035)] }, Envelope = new() { Decay = TimeSpan.FromMilliseconds(1250), DecayCurve = 3.5 } }
    };
    /// <summary>Gentle positive rising pair at 784 and 1047 hertz.</summary>
    public static SoundPattern Completed { get; set; } = new()
    {
        Id = new("10000000-0000-0000-0000-000000000002"), Name = nameof(Completed), Steps = [new(784, TimeSpan.FromMilliseconds(850), TimeSpan.FromMilliseconds(30)), new(1047, TimeSpan.FromMilliseconds(1050), TimeSpan.Zero)],
        Synthesis = new() { Volume = .055, Waveform = new HarmonicWaveform { Partials = [new(1, 1), new(2, .12)] }, Envelope = new() { Decay = TimeSpan.FromMilliseconds(1100), DecayCurve = 3 } }
    };
    /// <summary>Two warm plucked notes at 523 and 392 hertz.</summary>
    public static SoundPattern Reminder { get; set; } = new()
    {
        Id = new("10000000-0000-0000-0000-000000000003"), Name = nameof(Reminder), Steps = [new(523, TimeSpan.FromMilliseconds(700), TimeSpan.FromMilliseconds(100)), new(392, TimeSpan.FromMilliseconds(800), TimeSpan.Zero)],
        Synthesis = new() { Volume = .05, Waveform = new HarmonicWaveform { Partials = [new(1, 1), new(2, .3), new(3, .12)] }, Envelope = new() { Decay = TimeSpan.FromMilliseconds(800), DecayCurve = 4 } }
    };
    /// <summary>Clear twin tones at 523 hertz separated by eighty milliseconds.</summary>
    public static SoundPattern DoubleAlert { get; set; } = new()
    {
        Id = new("10000000-0000-0000-0000-000000000004"), Name = nameof(DoubleAlert), Steps = [new(523, TimeSpan.FromMilliseconds(320), TimeSpan.FromMilliseconds(80)), new(523, TimeSpan.FromMilliseconds(420), TimeSpan.Zero)],
        Synthesis = new() { Volume = .085, Waveform = new HarmonicWaveform { Partials = [new(1, 1), new(2, .18)] }, Envelope = new() { Decay = TimeSpan.FromMilliseconds(450), Sustain = .1, DecayCurve = 1.4 } }
    };
    /// <summary>Exactly three long 740-hertz, flat square-wave beeps with 320-millisecond gaps.</summary>
    public static SoundPattern Attention { get; set; } = new()
    {
        Id = new("10000000-0000-0000-0000-000000000005"), Name = nameof(Attention), Steps = [new(740, TimeSpan.FromMilliseconds(720), TimeSpan.FromMilliseconds(320)), new(740, TimeSpan.FromMilliseconds(720), TimeSpan.FromMilliseconds(320)), new(740, TimeSpan.FromMilliseconds(880), TimeSpan.Zero)],
        Synthesis = new() { Volume = .085, Waveform = new SquareWaveform(), Envelope = new() { Attack = TimeSpan.Zero, Decay = TimeSpan.Zero, Release = TimeSpan.Zero, Sustain = 1 } }
    };
    /// <summary>Legacy alias for the actual positive two-note sound.</summary>
    public static SoundPattern ChimeUp { get => Completed; set => Completed = value; }
    /// <summary>Legacy alias for the actual clear twin-tone sound.</summary>
    public static SoundPattern DualPing { get => DoubleAlert; set => DoubleAlert = value; }
    /// <summary>Legacy alias for the actual three-beep urgent sound.</summary>
    public static SoundPattern HighUrgency { get => Attention; set => Attention = value; }
    /// <summary>A new list containing the actual five shared sound definitions.</summary>
    public static List<SoundPattern> All => [Subtle, Completed, Reminder, DoubleAlert, Attention];
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
    public override void Write(Utf8JsonWriter writer, NotificationTheme value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id); writer.WriteString("name", value.Name);
        if (value.Parent is { } parent) { writer.WritePropertyName("parent"); JsonSerializer.Serialize(writer, parent, options); }
        writer.WritePropertyName("overrides"); JsonSerializer.Serialize(writer, value.Overrides, options);
        if (value.Parent is null || value.HasPalette) { writer.WritePropertyName("palette"); JsonSerializer.Serialize(writer, value.Palette, options); }
        if (value.Parent is null || value.HasTypography) { writer.WritePropertyName("typography"); JsonSerializer.Serialize(writer, value.Typography, options); }
        if (value.Parent is null || value.HasLayout) { writer.WritePropertyName("layout"); JsonSerializer.Serialize(writer, value.Layout, options); }
        if (value.Parent is null || value.HasFrame) { writer.WritePropertyName("frame"); JsonSerializer.Serialize(writer, value.Frame, options); }
        if (value.DefaultLevel is { } level) { writer.WritePropertyName("defaultLevel"); JsonSerializer.Serialize(writer, level, options); }
        if (value.CapturedOrigins is { } origins) { writer.WritePropertyName("capturedOrigins"); JsonSerializer.Serialize(writer, origins, options); }
        if (value.ExplicitFields.Count > 0) { writer.WritePropertyName("explicitFields"); JsonSerializer.Serialize(writer, value.ExplicitFields, options); }
        writer.WritePropertyName("explicitSections"); JsonSerializer.Serialize(writer, value.ExplicitSections, options);
        if (value.CapturedContentLifetime is { } contentLifetime) writer.WriteBoolean("capturedContentLifetime", contentLifetime);
        if (value.CapturedContentInteraction is { } contentInteraction) writer.WriteBoolean("capturedContentInteraction", contentInteraction);
        writer.WriteEndObject();
    }

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
