namespace UsefulNotification;

/// <summary>Groups colors, typography, and spacing for built-in and user-defined HTML themes.</summary>
public sealed record NotificationTheme
{
    public string Name { get; set; } = "Dark";
    public ThemePalette Palette { get; set; } = new();
    public ThemeTypography Typography { get; set; } = new();
    public ThemeLayout Layout { get; set; } = new();
    public NotificationFrame Frame { get; set; } = new();
    public NotificationLevel? DefaultLevel { get; set; }
}
/// <summary>Overrides selected parts of a named theme for one sender's notification.</summary>
public sealed record NotificationAppearance
{
    public ThemePalette? Palette { get; set; }
    public ThemeTypography? Typography { get; set; }
    public ThemeLayout? Layout { get; set; }
    public NotificationFrame? Frame { get; set; }
}
/// <summary>Selects HTML border edges that use the semantic notification accent.</summary>
[Flags]
public enum BorderEdges { None = 0, Top = 1, Right = 2, Bottom = 4, Left = 8, All = Top | Right | Bottom | Left }
/// <summary>Selects a standard HTML border pattern.</summary>
public enum BorderPattern { Solid, Dashed, Dotted, Double, None }
/// <summary>Defines all visible notification framing; none of these edges are drawn by Windows.</summary>
public sealed record NotificationFrame
{
    public BorderThickness Thickness { get; set; } = new();
    public double CornerRadius { get; set; } = 3;
    public BorderPattern Pattern { get; set; } = BorderPattern.Solid;
    public BorderEdges AccentEdges { get; set; } = BorderEdges.Left;
    public string? AccentColor { get; set; }
}
/// <summary>Stores independent HTML border widths in logical pixels.</summary>
public sealed record BorderThickness
{
    public double Top { get; set; } = 1;
    public double Right { get; set; } = 1;
    public double Bottom { get; set; } = 1;
    public double Left { get; set; } = 5;
}
/// <summary>Defines surface and semantic colors; no CSS variable is a second source of truth.</summary>
public sealed record ThemePalette
{
    public string Background { get; set; } = "#18191c";
    public string Surface { get; set; } = "#222428";
    public string Border { get; set; } = "#3a3d43";
    public string Text { get; set; } = "#f4f5f7";
    public string Muted { get; set; } = "#a7aeb9";
    public string Accent { get; set; } = "#4d97ff";
    public string AccentText { get; set; } = "#ffffff";
    public Dictionary<NotificationLevel, string> Levels { get; set; } = new()
    {
        [NotificationLevel.Normal] = "#939eac", [NotificationLevel.Info] = "#4d97ff", [NotificationLevel.Success] = "#49b58c",
        [NotificationLevel.Warning] = "#d5aa4c", [NotificationLevel.Error] = "#e06b70", [NotificationLevel.Urgent] = "#e34c58",
        [NotificationLevel.Deadline] = "#d98b55", [NotificationLevel.Meeting] = "#8496da", [NotificationLevel.Health] = "#69b8aa",
        [NotificationLevel.Break] = "#78ad79", [NotificationLevel.Personal] = "#b28ac7"
    };
}
/// <summary>Defines the font family and semantic text sizes used to generate CSS.</summary>
public sealed record ThemeTypography
{
    public string Family { get; set; } = "Segoe UI, Arial, sans-serif";
    public int Body { get; set; } = 14;
    public int Small { get; set; } = 12;
    public int Heading { get; set; } = 21;
    public int CardTitle { get; set; } = 18;
    public int NormalWeight { get; set; } = 400;
    public int StrongWeight { get; set; } = 600;
    public double LineHeight { get; set; } = 1.45;
    public double HeadingLineHeight { get; set; } = 1.2;
}
/// <summary>Defines the layout measurements as one editable theme object.</summary>
public sealed record ThemeLayout
{
    public int CompactGap { get; set; } = 4;
    public int Gap { get; set; } = 8;
    public int Padding { get; set; } = 16;
    public int PagePadding { get; set; } = 20;
    public int Border { get; set; } = 1;
    public int AccentBorder { get; set; } = 5;
    public int Radius { get; set; } = 3;
    public int ButtonHeight { get; set; } = 34;
    public int HeaderHeight { get; set; } = 46;
    public int IconSize { get; set; } = 22;
    public int SidebarWidth { get; set; } = 170;
    public int ImageHeight { get; set; } = 190;
    public int ProgressHeight { get; set; } = 6;
    public int ReplyRows { get; set; } = 3;
}
/// <summary>Loads custom theme documents and supplies the predefined palettes.</summary>
internal static class ThemeCatalog
{
    public static List<NotificationTheme> BuiltIn =
    [
        new(),
        new() { Name = "Light", Palette = new() { Background = "#f6f7f9", Surface = "#ffffff", Border = "#c7ccd4", Text = "#20252d", Muted = "#606978", Accent = "#2563b5" } },
        new() { Name = "Blue", Palette = new() { Accent = "#4d97ff" } },
        new() { Name = "Emerald", Palette = new() { Accent = "#49b58c" } },
        new() { Name = "Purple", Palette = new() { Accent = "#a68ac9" } },
        new() { Name = "Amber", Palette = new() { Accent = "#d5aa4c" } },
        new() { Name = "Crimson", Palette = new() { Accent = "#dc6571" } },
        new() { Name = "Info", DefaultLevel = NotificationLevel.Info, Frame = new() { AccentColor = "#4d97ff", AccentEdges = BorderEdges.All } },
        new() { Name = "Warn", DefaultLevel = NotificationLevel.Warning, Frame = new() { AccentColor = "#d5aa4c", AccentEdges = BorderEdges.All, Thickness = new() { Top = 2, Right = 2, Bottom = 2, Left = 6 } } },
        new() { Name = "Error", DefaultLevel = NotificationLevel.Error, Frame = new() { AccentColor = "#e06b70", AccentEdges = BorderEdges.All, Thickness = new() { Top = 2, Right = 2, Bottom = 2, Left = 6 } } },
        new() { Name = "Happy", DefaultLevel = NotificationLevel.Success, Frame = new() { AccentColor = "#49b58c", AccentEdges = BorderEdges.All } },
        new() { Name = "HighContrast", Palette = new() { Background = "#000000", Surface = "#000000", Border = "#ffffff", Text = "#ffffff", Muted = "#ffffff", Accent = "#ffff00", AccentText = "#000000" } }
    ];
    public static List<NotificationTheme> Themes = [.. BuiltIn];

    /// <summary>Reads theme JSON files without modifying built-in definitions.</summary>
    public static void Load()
    {
        string directory = Path.Combine(AppPaths.UserData, AppConstants.Storage.ThemesDirectory);
        Directory.CreateDirectory(directory);
        var loaded = new List<NotificationTheme>(BuiltIn);
        foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var result = RunSafely(() => System.Text.Json.JsonSerializer.Deserialize<NotificationTheme>(File.ReadAllText(path), AppConstants.Json) ?? throw new InvalidDataException(path));
            if (result.Succeeded && result.Value is { } theme)
            {
                loaded.RemoveAll(item => item.Name.Equals(theme.Name, StringComparison.OrdinalIgnoreCase));
                loaded.Add(theme);
            }
        }
        Themes = loaded;
    }

    /// <summary>Resolves a per-request theme or the selected application theme.</summary>
    /// <param name="name">Optional request theme name.</param>
    /// <returns>A matching theme, falling back to the default dark theme.</returns>
    public static NotificationTheme Get(string? name = null) => Themes.FirstOrDefault(theme => theme.Name.Equals(name ?? Settings.Theme.Value, StringComparison.OrdinalIgnoreCase)) ?? Themes[0];

    /// <summary>Combines a named theme with the sender's optional appearance objects.</summary>
    /// <param name="request">The notification's display request.</param>
    /// <returns>The effective complete theme.</returns>
    public static NotificationTheme For(NotificationRequest request)
    {
        NotificationTheme theme = Get(request.Theme);
        return request.Appearance is not { } custom ? theme : theme with
        { Palette = custom.Palette ?? theme.Palette, Typography = custom.Typography ?? theme.Typography, Layout = custom.Layout ?? theme.Layout, Frame = custom.Frame ?? theme.Frame };
    }
}
/// <summary>Stores user-created sound presets and persistent panel placement.</summary>
internal sealed record DesktopPreferences
{
    public List<SoundPattern> Sounds { get; set; } = [AppConstants.Notifications.Sound, .. AppConstants.Notifications.SoundPresets];
    public string SelectedSound { get; set; } = AppConstants.Notifications.Sound.Name;
    public System.Drawing.Rectangle? PanelBounds { get; set; }
    public Dictionary<string, TableLayout> Tables { get; set; } = [];
}
/// <summary>Stores the user's column arrangement and ordering without changing notification data.</summary>
public sealed record TableLayout
{
    public string Sort { get; set; } = nameof(NotificationEntry.CreatedAt);
    public bool Descending { get; set; } = true;
    public Dictionary<string, double> Widths { get; set; } = [];
    public List<string> Hidden { get; set; } = [];
}
