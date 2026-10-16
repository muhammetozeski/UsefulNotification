using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace UsefulNotification;

/// <summary>Owns the policy objects used by transport, storage, scheduling, and rendering.</summary>
public static class AppConstants
{
    public static ProductOptions Product = new();
    public static StorageOptions Storage = new();
    public static NotificationDefaults Notifications = new();
    public static TransportOptions Transport = new();
    public static WindowOptions Windows = new();
    public static JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    public static RetryStrategyOptions Retry = new()
    {
        MaxRetryAttempts = 2, Delay = TimeSpan.FromMilliseconds(120), BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder().Handle<IOException>(),
        OnRetry = static attempt => { ExceptionBoundary.LogSafely($"Retry {attempt.AttemptNumber}: {attempt.Outcome.Exception}"); return ValueTask.CompletedTask; }
    };
    public static TimeoutStrategyOptions Timeout = new() { Timeout = TimeSpan.FromSeconds(4) };
    public static string AppTitle => Product.Name;
    public static string ConfigFileName => Path.Combine(AppPaths.UserData, Storage.SettingsFile);
}
/// <summary>Names the product and executable payloads.</summary>
public sealed class ProductOptions
{
    public string Name { get; set; } = "UsefulNotification";
    public string Executable { get; set; } = "UsefulNotification.exe";
    public string CommandExecutable { get; set; } = "UsefulNotification.Command.exe";
    public string Icon { get; set; } = "app.ico";
}
/// <summary>Names the files owned by the portable application.</summary>
public sealed class StorageOptions
{
    public string SettingsFile { get; set; } = "settings.txt";
    public string HistoryFile { get; set; } = "notifications.json";
    public string PreferencesFile { get; set; } = "preferences.json";
    public string ThemesDirectory { get; set; } = "Themes";
    public string TemplatesDirectory { get; set; } = "Templates";
    public string BackupExtension { get; set; } = ".bak";
    public string TemporaryExtension { get; set; } = ".pending";
    public int HistoryCapacity { get; set; } = 2000;
}
/// <summary>Configures the local request channel and message budgets.</summary>
public sealed class TransportOptions
{
    public string PipeName { get; set; } = "UsefulNotification.v1";
    public string MutexName { get; set; } = "Local\\UsefulNotification.v1";
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(200);
    public int MaximumMessageCharacters { get; set; } = 1024 * 1024;
}
/// <summary>Supplies complete behavior when a sender omits notification options.</summary>
public sealed class NotificationDefaults
{
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(8);
    public TimeSpan SchedulerInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan SupervisorInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan DeliveryDeadline { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan[] Snooze { get; set; } = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];
    public TimeSpan[] QuickDelays { get; set; } = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];
    public string TimeFormat { get; set; } = "yyyy.MM.dd HH.mm.ss";
    public NotificationLevel Level { get; set; } = NotificationLevel.Info;
    public int MaximumVisible { get; set; } = 3;
    public SoundPattern Sound { get; set; } = new() { Name = "Chime Up", Steps = [new(900, TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(60)), new(1200, TimeSpan.FromMilliseconds(160), TimeSpan.FromMilliseconds(60)), new(1500, TimeSpan.FromMilliseconds(300), TimeSpan.Zero)] };
    public List<SoundPattern> SoundPresets { get; set; } =
    [
        new() { Name = "Dual Ping", Steps = [new(1050, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(100)), new(1050, TimeSpan.FromMilliseconds(150), TimeSpan.Zero)] },
        new() { Name = "High Urgency", Steps = [new(1600, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(70)), new(1200, TimeSpan.FromMilliseconds(120), TimeSpan.Zero)] },
        new() { Name = "Subtle Notification", Steps = [new(700, TimeSpan.FromMilliseconds(90), TimeSpan.Zero)] }
    ];
}
/// <summary>Groups window sizes, screen placement, and display capacity.</summary>
public sealed class WindowOptions
{
    public System.Drawing.Size Panel { get; set; } = new(860, 720);
    public System.Drawing.Size Toast { get; set; } = new(430, 220);
    public System.Drawing.Size MinimumPanel { get; set; } = new(620, 460);
    public int ScreenMargin { get; set; } = 16;
    public int CardGap { get; set; } = 8;
}
