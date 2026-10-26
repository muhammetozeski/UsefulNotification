using UsefulNotification.Localization;
namespace UsefulNotification;

/// <summary>Publishes complete validated settings; editors use a detached copy until Save succeeds.</summary>
internal sealed record RuntimeSettings
{
    public string Theme { get; set; } = NotificationThemes.Dark.Name;
    public string Language { get; set; } = "system";
    public int MaximumVisible { get; set; } = AppConstants.Notifications.MaximumVisible;
    public int Monitor { get; set; }
    public bool PlaySound { get; set; } = true;
    public bool PauseOnHover { get; set; } = true;
    public bool GlobalHotkey { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool EnableLogging { get; set; } = true;
    public static RuntimeSettings Capture() => new()
    {
        Theme = Settings.Theme.Value, Language = Settings.Language.Value, MaximumVisible = Settings.MaximumVisible.Value,
        Monitor = Settings.Monitor.Value, PlaySound = Settings.PlaySound.Value, PauseOnHover = Settings.PauseOnHover.Value,
        GlobalHotkey = Settings.GlobalHotkey.Value, StartWithWindows = Settings.StartWithWindows.Value, EnableLogging = Settings.EnableLogging.Value
    };
    public void Validate()
    {
        if (MaximumVisible <= 0 || Monitor < 0) throw new ArgumentException("Visible capacity must be positive and monitor index cannot be negative.");
        _ = ThemeCatalog.Get(Theme, required: true);
        _ = LocManager.Resolve(Language);
    }
    public void Apply()
    {
        Settings.Theme.Value = Theme; Settings.Language.Value = Language; Settings.MaximumVisible.Value = MaximumVisible;
        Settings.Monitor.Value = Monitor; Settings.PlaySound.Value = PlaySound; Settings.PauseOnHover.Value = PauseOnHover;
        Settings.GlobalHotkey.Value = GlobalHotkey; Settings.StartWithWindows.Value = StartWithWindows; Settings.EnableLogging.Value = EnableLogging;
        Logger.ActivateLogging = EnableLogging;
    }
}
