using System.Drawing;
using System.Reflection;
using System.Text.Json.Serialization;

namespace UsefulNotification;

/// <summary>Changes only supplied theme fields. A supplied null Sound is silence; a missing Sound inherits.</summary>
public sealed record NotificationOverrides
{
    /// <summary>Independent palette changes, including paired system light and dark colors.</summary>
    public ThemePaletteOverrides? Palette { get; set; }
    /// <summary>Independent font and text measurement changes.</summary>
    public ThemeTypographyOverrides? Typography { get; set; }
    /// <summary>Independent spacing and control measurement changes.</summary>
    public ThemeLayoutOverrides? Layout { get; set; }
    /// <summary>Independent HTML border and corner changes.</summary>
    public NotificationFrameOverrides? Frame { get; set; }
    /// <summary>Missing inherits; an explicit null suppresses sound.</summary>
    public Change<SoundPattern?>? Sound { get; set; }
    /// <summary>Missing inherits; an explicit null chooses a content-dependent lifetime.</summary>
    public Change<NotificationLifetime?>? Lifetime { get; set; }
    /// <summary>Missing inherits; an explicit null chooses normal queue precedence.</summary>
    public Change<NotificationPriority?>? Priority { get; set; }
    /// <summary>Missing inherits; an explicit null chooses interaction controls from content.</summary>
    public Change<NotificationInteraction?>? Interaction { get; set; }
}

/// <summary>Each supplied color replaces only that palette field; omitted values inherit.</summary>
public sealed record ThemePaletteOverrides
{
    /// <summary>Missing inherits LightBackground; a supplied value replaces that field.</summary>
    public Change<Color?>? LightBackground { get; set; }
    /// <summary>Missing inherits DarkBackground; a supplied value replaces that field.</summary>
    public Change<Color?>? DarkBackground { get; set; }
    /// <summary>Missing inherits LightAccent; a supplied value replaces that field.</summary>
    public Change<Color?>? LightAccent { get; set; }
    /// <summary>Missing inherits DarkAccent; a supplied value replaces that field.</summary>
    public Change<Color?>? DarkAccent { get; set; }
    /// <summary>Missing inherits Overlay; a supplied value replaces that field.</summary>
    public Change<Color>? Overlay { get; set; }
    /// <summary>Missing inherits Background; a supplied value replaces that field.</summary>
    public Change<Color>? Background { get; set; }
    /// <summary>Missing inherits Surface; a supplied value replaces that field.</summary>
    public Change<Color>? Surface { get; set; }
    /// <summary>Missing inherits Border; a supplied value replaces that field.</summary>
    public Change<Color>? Border { get; set; }
    /// <summary>Missing inherits Text; a supplied value replaces that field.</summary>
    public Change<Color>? Text { get; set; }
    /// <summary>Missing inherits Muted; a supplied value replaces that field.</summary>
    public Change<Color>? Muted { get; set; }
    /// <summary>Missing inherits Accent; a supplied value replaces that field.</summary>
    public Change<Color>? Accent { get; set; }
    /// <summary>Missing inherits AccentText; a supplied value replaces that field.</summary>
    public Change<Color>? AccentText { get; set; }
    /// <summary>Missing inherits Levels; a supplied value replaces that field.</summary>
    public Change<Dictionary<NotificationLevel, Color>>? Levels { get; set; }
}

/// <summary>Partial typography changes measured in CSS pixels.</summary>
public sealed record ThemeTypographyOverrides
{
    /// <summary>Missing inherits Family; a supplied value replaces that field.</summary>
    public Change<string>? Family { get; set; }
    /// <summary>Missing inherits Body; a supplied value replaces that field.</summary>
    public Change<int>? Body { get; set; }
    /// <summary>Missing inherits Small; a supplied value replaces that field.</summary>
    public Change<int>? Small { get; set; }
    /// <summary>Missing inherits Heading; a supplied value replaces that field.</summary>
    public Change<int>? Heading { get; set; }
    /// <summary>Missing inherits CardTitle; a supplied value replaces that field.</summary>
    public Change<int>? CardTitle { get; set; }
    /// <summary>Missing inherits NormalWeight; a supplied value replaces that field.</summary>
    public Change<int>? NormalWeight { get; set; }
    /// <summary>Missing inherits StrongWeight; a supplied value replaces that field.</summary>
    public Change<int>? StrongWeight { get; set; }
    /// <summary>Missing inherits LineHeight; a supplied value replaces that field.</summary>
    public Change<double>? LineHeight { get; set; }
    /// <summary>Missing inherits HeadingLineHeight; a supplied value replaces that field.</summary>
    public Change<double>? HeadingLineHeight { get; set; }
}

/// <summary>Partial changes to the existing logical CSS layout measurements.</summary>
public sealed record ThemeLayoutOverrides
{
    /// <summary>Missing inherits ResizeGrip; a supplied value replaces that field.</summary>
    public Change<int>? ResizeGrip { get; set; }
    /// <summary>Missing inherits NarrowPanel; a supplied value replaces that field.</summary>
    public Change<int>? NarrowPanel { get; set; }
    /// <summary>Missing inherits CompactGap; a supplied value replaces that field.</summary>
    public Change<int>? CompactGap { get; set; }
    /// <summary>Missing inherits Gap; a supplied value replaces that field.</summary>
    public Change<int>? Gap { get; set; }
    /// <summary>Missing inherits Padding; a supplied value replaces that field.</summary>
    public Change<int>? Padding { get; set; }
    /// <summary>Missing inherits PagePadding; a supplied value replaces that field.</summary>
    public Change<int>? PagePadding { get; set; }
    /// <summary>Missing inherits Border; a supplied value replaces that field.</summary>
    public Change<int>? Border { get; set; }
    /// <summary>Missing inherits AccentBorder; a supplied value replaces that field.</summary>
    public Change<int>? AccentBorder { get; set; }
    /// <summary>Missing inherits Radius; a supplied value replaces that field.</summary>
    public Change<int>? Radius { get; set; }
    /// <summary>Missing inherits ButtonHeight; a supplied value replaces that field.</summary>
    public Change<int>? ButtonHeight { get; set; }
    /// <summary>Missing inherits HeaderHeight; a supplied value replaces that field.</summary>
    public Change<int>? HeaderHeight { get; set; }
    /// <summary>Missing inherits IconSize; a supplied value replaces that field.</summary>
    public Change<int>? IconSize { get; set; }
    /// <summary>Missing inherits SidebarWidth; a supplied value replaces that field.</summary>
    public Change<int>? SidebarWidth { get; set; }
    /// <summary>Missing inherits ImageHeight; a supplied value replaces that field.</summary>
    public Change<int>? ImageHeight { get; set; }
    /// <summary>Missing inherits ProgressHeight; a supplied value replaces that field.</summary>
    public Change<int>? ProgressHeight { get; set; }
    /// <summary>Missing inherits ReplyRows; a supplied value replaces that field.</summary>
    public Change<int>? ReplyRows { get; set; }
}

/// <summary>Independent inherited border widths in CSS pixels.</summary>
public sealed record BorderThicknessOverrides
{
    /// <summary>Missing inherits Top; a supplied value replaces that field.</summary>
    public Change<double>? Top { get; set; }
    /// <summary>Missing inherits Right; a supplied value replaces that field.</summary>
    public Change<double>? Right { get; set; }
    /// <summary>Missing inherits Bottom; a supplied value replaces that field.</summary>
    public Change<double>? Bottom { get; set; }
    /// <summary>Missing inherits Left; a supplied value replaces that field.</summary>
    public Change<double>? Left { get; set; }
}

/// <summary>Partial HTML frame changes; thickness edges inherit independently.</summary>
public sealed record NotificationFrameOverrides
{
    /// <summary>Only supplied border edges replace inherited widths.</summary>
    public BorderThicknessOverrides? Thickness { get; set; }
    /// <summary>Optional corner radius in CSS pixels.</summary>
    public Change<double>? CornerRadius { get; set; }
    /// <summary>Optional HTML border stroke pattern.</summary>
    public Change<BorderPattern>? Pattern { get; set; }
    /// <summary>Optional accent-bearing edges.</summary>
    public Change<BorderEdges>? AccentEdges { get; set; }
    /// <summary>Missing inherits; explicit null returns to the palette accent.</summary>
    public Change<Color?>? AccentColor { get; set; }
}
/// <summary>Controls card acknowledgement and the standard dismissal and snooze affordances.</summary>
public sealed record NotificationInteraction
{
    /// <summary>True acknowledges a click on the card body without intercepting its interactive controls.</summary>
    public bool ClickToAcknowledge { get; set; }
    /// <summary>True displays a standard dismiss button, initially true.</summary>
    public bool ShowDismiss { get; set; } = true;
    /// <summary>True displays the request or host snooze choices, initially true.</summary>
    public bool ShowSnooze { get; set; } = true;
}
/// <summary>Reusable actual interaction policies; sends capture their values.</summary>
public static class NotificationInteractions
{
    /// <summary>Acknowledges the card body; keeps snooze available and omits a separate dismiss button.</summary>
    public static NotificationInteraction ClickToAcknowledge { get; set; } = new() { ClickToAcknowledge = true, ShowDismiss = false };
    /// <summary>Uses explicit action controls with standard dismissal and snooze.</summary>
    public static NotificationInteraction Actions { get; set; } = new();
}
/// <summary>Detached concrete appearance and policies retained with an accepted request.</summary>
public sealed record ResolvedNotification
{
    [JsonInclude] internal bool UsesContentLifetime { get; set; } = true;
    [JsonInclude] internal bool UsesContentInteraction { get; set; } = true;
    /// <summary>Flattened appearance with no live parent reference.</summary>
    public NotificationTheme Theme { get; set; } = new();
    /// <summary>Captured sound; null means silence.</summary>
    public SoundPattern? Sound { get; set; }
    /// <summary>Captured confirmed-display lifetime policy.</summary>
    public NotificationLifetime Lifetime { get; set; } = NotificationLifetimes.Default;
    /// <summary>Captured queue precedence.</summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    /// <summary>Captured card interaction policy.</summary>
    public NotificationInteraction Interaction { get; set; } = new();
    /// <summary>Field paths such as Palette.Accent and Sound mapped to the definition name or Request that supplied them.</summary>
    public Dictionary<string, string> FieldOrigins { get; set; } = [];
}
/// <summary>Resolves actual parent objects and captures every effective appearance and delivery value.</summary>
public static class ThemeResolver
{
    /// <summary>Creates a detached concrete snapshot, ignoring any incoming Resolved value.</summary>
    /// <param name="request">Request whose legacy fields are applied before its partial Overrides.</param>
    /// <returns>Complete appearance, sound and delivery policies owned by this resolution.</returns>
    public static ResolvedNotification Resolve(NotificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ResolvedNotification result = Build(request.Theme ?? ThemeCatalog.Get());
        if (request.Appearance is { } appearance)
        {
            if (appearance.Palette is { } palette) Replace(result, nameof(NotificationTheme.Palette), palette, "Request");
            if (appearance.Typography is { } typography) Replace(result, nameof(NotificationTheme.Typography), typography, "Request");
            if (appearance.Layout is { } layout) Replace(result, nameof(NotificationTheme.Layout), layout, "Request");
            if (appearance.Frame is { } frame) Replace(result, nameof(NotificationTheme.Frame), frame, "Request");
        }
        if (result.UsesContentLifetime && NeedsAcknowledgement(request)) result.Lifetime = NotificationLifetimes.UntilAcknowledged;
        if (result.UsesContentInteraction && HasControls(request)) result.Interaction = NotificationInteractions.Actions;
        if (request.Lifetime is { } lifetime) { result.Lifetime = lifetime; result.FieldOrigins[nameof(result.Lifetime)] = "Request"; }
        if (request.Priority is { } priority) { result.Priority = priority; result.FieldOrigins[nameof(result.Priority)] = "Request"; }
        if (request.Sound is { } sound) { result.Sound = sound; result.FieldOrigins[nameof(result.Sound)] = "Request"; }
        if (request.Interaction is { } interaction) { result.Interaction = interaction; result.FieldOrigins[nameof(result.Interaction)] = "Request"; }
        Apply(result, request.Overrides, "Request", request);
        if ((request.Level ?? result.Theme.DefaultLevel) is { } level && result.Theme.Frame.AccentColor is null && result.Theme.Palette.Levels.TryGetValue(level, out Color accent))
        {
            ThemePaletteOverrides? own = request.Overrides?.Palette;
            var inheritedFields = new List<string>();
            if (own?.Accent is null) { result.Theme.Palette.Accent = accent; inheritedFields.Add("Palette.Accent"); }
            if (own?.Accent is null && own?.LightAccent is null) { result.Theme.Palette.LightAccent = accent; inheritedFields.Add("Palette.LightAccent"); }
            if (own?.Accent is null && own?.DarkAccent is null) { result.Theme.Palette.DarkAccent = accent; inheritedFields.Add("Palette.DarkAccent"); }
            foreach (string field in inheritedFields)
            {
                result.FieldOrigins[field] = request.Level is not null ? "Request" : result.FieldOrigins.GetValueOrDefault(nameof(NotificationTheme.DefaultLevel)) ?? result.Theme.Name;
                result.Theme.ExplicitFields.Add(field);
            }
        }
        if (request.Silent == true) { result.Sound = null; result.FieldOrigins[nameof(result.Sound)] = "Request"; }
        ValidateResolved(result);
        return NotificationProtocol.Copy(result);
    }

    /// <summary>Resolves a theme without request content, retaining paired light and dark appearance values.</summary>
    /// <param name="theme">Actual definition and its parent chain.</param>
    /// <returns>A detached resolved appearance and the inherited default policies.</returns>
    public static ResolvedNotification ResolveTheme(NotificationTheme theme) => NotificationProtocol.Copy(Build(theme));

    /// <summary>Freezes a definition before per-send changes, retaining its inherited baseline for future partial updates.</summary>
    /// <param name="theme">Selected actual theme and its live parent chain.</param>
    /// <returns>A flat detached definition whose policies and field origins retain the acceptance-time baseline.</returns>
    public static NotificationTheme CaptureTheme(NotificationTheme theme)
    {
        ResolvedNotification resolved = ResolveTheme(theme);
        resolved.Theme.Overrides = new()
        {
            Sound = new(resolved.Sound), Lifetime = new(resolved.Lifetime), Priority = new(resolved.Priority), Interaction = new(resolved.Interaction)
        };
        resolved.Theme.CapturedOrigins = resolved.FieldOrigins;
        resolved.Theme.CapturedContentLifetime = resolved.UsesContentLifetime;
        resolved.Theme.CapturedContentInteraction = resolved.UsesContentInteraction;
        return resolved.Theme;
    }

    /// <summary>Copies an editor's own definition and overrides while preserving its actual shared parent.</summary>
    /// <param name="theme">Definition being edited.</param>
    /// <returns>A detached editor draft whose Parent is the original shared parent object.</returns>
    public static NotificationTheme CreateDraft(NotificationTheme theme)
    {
        ValidateChain(theme);
        NotificationTheme draft = NotificationProtocol.Copy(theme);
        draft.Parent = theme.Parent;
        return draft;
    }

    /// <summary>Rejects cyclic or identity-less actual parent chains before serialization.</summary>
    /// <param name="theme">Definition whose chain must be finite and have stable identities.</param>
    internal static void ValidateChain(NotificationTheme theme)
    {
        var seen = new HashSet<NotificationTheme>(ReferenceEqualityComparer.Instance);
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (NotificationTheme? node = theme; node is not null; node = node.Parent)
        {
            if (!seen.Add(node) || !identities.Add(node.Id)) throw new ArgumentException("A theme parent chain contains a cycle or repeated identity.");
            ArgumentException.ThrowIfNullOrWhiteSpace(node.Id);
            ArgumentException.ThrowIfNullOrWhiteSpace(node.Name);
            if (node.Overrides is null) throw new ArgumentException("A theme requires an override patch.");
            node.Validate();
        }
    }

    /// <summary>Resolves a captured actual parent chain without mutating any definition.</summary>
    static ResolvedNotification Build(NotificationTheme selected)
    {
        ValidateChain(selected);
        NotificationTheme captured = NotificationProtocol.Copy(selected);
        var chain = new Stack<NotificationTheme>();
        for (NotificationTheme? node = captured; node is not null; node = node.Parent) chain.Push(node);
        var result = new ResolvedNotification { Sound = NotificationSounds.Subtle, Interaction = NotificationInteractions.ClickToAcknowledge };
        while (chain.TryPop(out NotificationTheme? layer))
        {
            bool root = layer.Parent is null;
            if (root || layer.HasPalette) Replace(result, nameof(NotificationTheme.Palette), layer.Palette, layer.Name, layer.HasPalette);
            if (root || layer.HasTypography) Replace(result, nameof(NotificationTheme.Typography), layer.Typography, layer.Name, layer.HasTypography);
            if (root || layer.HasLayout) Replace(result, nameof(NotificationTheme.Layout), layer.Layout, layer.Name, layer.HasLayout);
            if (root || layer.HasFrame) Replace(result, nameof(NotificationTheme.Frame), layer.Frame, layer.Name, layer.HasFrame);
            if (layer.DefaultLevel is { } level) { result.Theme.DefaultLevel = level; result.FieldOrigins[nameof(NotificationTheme.DefaultLevel)] = layer.Name; }
            if (root)
            {
                result.FieldOrigins[nameof(result.Lifetime)] = layer.Name; result.FieldOrigins[nameof(result.Interaction)] = layer.Name;
                result.FieldOrigins[nameof(result.Sound)] = layer.Name; result.FieldOrigins[nameof(result.Priority)] = layer.Name;
            }
            Apply(result, layer.Overrides, layer.Name);
            if (layer.CapturedOrigins is { } origins)
            {
                result.FieldOrigins = new(origins); result.Theme.ExplicitFields = new(layer.ExplicitFields);
            }
            if (layer.CapturedContentLifetime is { } contentLifetime) result.UsesContentLifetime = contentLifetime;
            if (layer.CapturedContentInteraction is { } contentInteraction) result.UsesContentInteraction = contentInteraction;
        }
        result.Theme.Id = selected.Id; result.Theme.Name = selected.Name;
        ValidateResolved(result);
        return result;
    }

    /// <summary>Rejects invalid effective appearance, sound and delivery values after all inherited changes.</summary>
    static void ValidateResolved(ResolvedNotification result)
    {
        result.Theme.Validate();
        if (result.Sound is { } sound) SoundSynthesis.Validate(sound);
        if (result.Lifetime is ExpiringLifetime expiry && expiry.Duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(result), "An expiring lifetime must be positive.");
        if (!Enum.IsDefined(result.Priority)) throw new ArgumentOutOfRangeException(nameof(result), "The queue priority is invalid.");
    }

    /// <summary>Identifies content that needs explicit input controls.</summary>
    static bool HasControls(NotificationRequest request) => request.Actions.Count > 0 || request.Input is not null || request.Checklist.Count > 0;
    /// <summary>Retains the existing content-dependent acknowledgement rule.</summary>
    static bool NeedsAcknowledgement(NotificationRequest request) => request.Schedule is not null || HasControls(request) || request.Progress is { } progress && (progress.Value is null || progress.Value < progress.Maximum);

    /// <summary>Replaces one legacy complete appearance section and records its leaf origins.</summary>
    static void Replace(ResolvedNotification result, string section, object value, string origin, bool explicitSection = true)
    {
        typeof(NotificationTheme).GetProperty(section)!.SetValue(result.Theme, value);
        RecordOrigins(value, section, origin, result.FieldOrigins, explicitSection ? result.Theme.ExplicitFields : null);
    }

    /// <summary>Records the owner of every concrete appearance leaf.</summary>
    static void RecordOrigins(object value, string prefix, string origin, Dictionary<string, string> origins, HashSet<string>? explicitFields = null)
    {
        foreach (PropertyInfo property in value.GetType().GetProperties())
        {
            string path = prefix + "." + property.Name;
            if (property.PropertyType == typeof(BorderThickness)) RecordOrigins(property.GetValue(value)!, path, origin, origins, explicitFields);
            else { origins[path] = origin; explicitFields?.Add(path); }
        }
    }

    /// <summary>Applies a partial appearance and policy patch after its inherited baseline.</summary>
    static void Apply(ResolvedNotification result, NotificationOverrides? patch, string origin, NotificationRequest? request = null)
    {
        if (patch is null) return;
        foreach (string section in new[] { nameof(patch.Palette), nameof(patch.Typography), nameof(patch.Layout), nameof(patch.Frame) })
            if (typeof(NotificationOverrides).GetProperty(section)!.GetValue(patch) is { } values)
                ApplyFields(typeof(NotificationTheme).GetProperty(section)!.GetValue(result.Theme)!, values, section, origin, result.FieldOrigins, result.Theme.ExplicitFields);
        if (patch.Palette is { } palette)
        {
            if (palette.Accent is { } accent)
            {
                if (palette.LightAccent is null) { result.Theme.Palette.LightAccent = accent.Value; result.FieldOrigins["Palette.LightAccent"] = origin; }
                if (palette.DarkAccent is null) { result.Theme.Palette.DarkAccent = accent.Value; result.FieldOrigins["Palette.DarkAccent"] = origin; }
            }
            if (palette.Background is { } background)
            {
                if (palette.LightBackground is null) { result.Theme.Palette.LightBackground = background.Value; result.FieldOrigins["Palette.LightBackground"] = origin; }
                if (palette.DarkBackground is null) { result.Theme.Palette.DarkBackground = background.Value; result.FieldOrigins["Palette.DarkBackground"] = origin; }
            }
        }
        if (patch.Sound is { } sound) { result.Sound = sound.Value; result.FieldOrigins[nameof(result.Sound)] = origin; }
        if (patch.Lifetime is { } lifetime)
        {
            result.Lifetime = lifetime.Value ?? (request is not null && NeedsAcknowledgement(request) ? NotificationLifetimes.UntilAcknowledged : NotificationLifetimes.Default);
            result.UsesContentLifetime = lifetime.Value is null;
            result.FieldOrigins[nameof(result.Lifetime)] = origin;
        }
        if (patch.Priority is { } priority) { result.Priority = priority.Value ?? NotificationPriority.Normal; result.FieldOrigins[nameof(result.Priority)] = origin; }
        if (patch.Interaction is { } interaction)
        {
            result.Interaction = interaction.Value ?? (request is not null && HasControls(request) ? NotificationInteractions.Actions : NotificationInteractions.ClickToAcknowledge);
            result.UsesContentInteraction = interaction.Value is null;
            result.FieldOrigins[nameof(result.Interaction)] = origin;
        }
    }

    /// <summary>Assigns only explicit Change values and descends into partial border thickness.</summary>
    static void ApplyFields(object destination, object patch, string prefix, string origin, Dictionary<string, string> origins, HashSet<string> explicitFields)
    {
        foreach (PropertyInfo property in patch.GetType().GetProperties())
        {
            if (property.GetValue(patch) is not { } change) continue;
            PropertyInfo target = destination.GetType().GetProperty(property.Name)!;
            string path = prefix + "." + property.Name;
            if (change.GetType().IsGenericType && change.GetType().GetGenericTypeDefinition() == typeof(Change<>))
            {
                target.SetValue(destination, change.GetType().GetProperty(nameof(Change<object>.Value))!.GetValue(change));
                origins[path] = origin;
                explicitFields.Add(path);
            }
            else ApplyFields(target.GetValue(destination)!, change, path, origin, origins, explicitFields);
        }
    }
}

