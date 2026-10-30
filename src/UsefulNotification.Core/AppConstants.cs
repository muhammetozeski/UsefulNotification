using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace UsefulNotification;

/// <summary>Owns the policy objects used by transport, storage, scheduling, and rendering.</summary>
internal static class AppConstants
{
    public static ProductOptions Product = new();
    public static StorageOptions Storage = new();
    public static NotificationDefaults Notifications = new();
    public static TransportOptions Transport = new();
    public static WindowOptions Windows = new();
    public static RenderMemoryOptions RenderMemory = new();
    public static HtmlTemplateOptions HtmlTemplates = new();
    public static ActionPolicy Actions = new();
    public static AudioPolicy Audio = new();
    public static CodexMessagingOptions CodexMessaging = new();
    public static JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, AllowOutOfOrderMetadataProperties = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new NotificationThemeConverter(), new FileInfoConverter(), new ColorConverter(), new ProcessStartConverter(), new ChangeConverterFactory() } };
    public static JsonSerializerOptions WireJson = new(Json) { WriteIndented = false };
    static readonly Lazy<JsonSerializerOptions> StoredJson = new(() => CreateStorageJson(new DirectoryInfo(AppPaths.Root)));
    /// <summary>Uses portable root-relative asset references only for persisted documents; wire requests retain absolute paths.</summary>
    public static JsonSerializerOptions StorageJson => StoredJson.Value;

    /// <summary>Builds an independent storage serializer for the supplied application root.</summary>
    /// <param name="root">Root against which owned asset references are stored and restored.</param>
    /// <returns>Options retaining the ordinary contract converters and adding portable path ownership.</returns>
    public static JsonSerializerOptions CreateStorageJson(DirectoryInfo root)
    {
        var options = new JsonSerializerOptions(Json);
        foreach (var converter in options.Converters.Where(converter => converter is FileInfoConverter or ProcessStartConverter).ToArray()) options.Converters.Remove(converter);
        var paths = new PortablePathContext(root);
        options.Converters.Add(new FileInfoConverter(paths));
        options.Converters.Add(new FileUriConverter(paths));
        options.Converters.Add(new ProcessStartConverter(paths));
        return options;
    }
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
/// <summary>Names the files owned by the portable application.</summary>
internal sealed class StorageOptions
{
    public string SettingsFile { get; set; } = "settings.txt";
    public string HistoryFile { get; set; } = "notifications.json";
    public string PreferencesFile { get; set; } = "preferences.json";
    public string ThemesDirectory { get; set; } = "Themes";
    public string TemplatesDirectory { get; set; } = "Templates";
    public string BrowserCacheDirectory { get; set; } = "WebView2";
    public string BrowserCacheEnvironmentVariable { get; set; } = "WEBVIEW2_USER_DATA_FOLDER";
    public string BackupExtension { get; set; } = ".bak";
    public string TemporaryExtension { get; set; } = ".pending";
    public string UnreadableExtension { get; set; } = ".unreadable";
    public int HistoryCapacity { get; set; } = 2000;
    public int HistoryPageSize { get; set; } = 40;
}
/// <summary>Configures the local request channel and message budgets.</summary>
internal sealed class TransportOptions
{
    public HostRegistrationOptions Registration { get; set; } = new();
    public FileInfo? Application { get; set; }
    public System.Text.Encoding Encoding { get; set; } = new System.Text.UTF8Encoding(false);
    public string PipeName { get; set; } = "UsefulNotification.v1";
    public string MutexName { get; set; } = "Local\\UsefulNotification.v1";
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(200);
    public int MaximumMessageCharacters { get; set; } = 1024 * 1024;
}
/// <summary>Owns per-user discovery of the currently launched portable host.</summary>
internal sealed class HostRegistrationOptions
{
    public string RegistryPath { get; set; } = @"Software\UsefulNotification";
    public string ExecutableValue { get; set; } = "Executable";
}
/// <summary>Supplies complete behavior when a sender omits notification options.</summary>
internal sealed class NotificationDefaults
{
    /// <summary>Resolves a sender lifetime or the content-dependent default used for actual delivery and updates.</summary>
    public NotificationLifetime ResolveLifetime(NotificationRequest request) => request.Resolved?.Lifetime ?? request.Lifetime ??
        (request.Schedule is not null || request.Input is not null || request.Actions.Count > 0 || request.Checklist.Count > 0 || request.Progress is { } progress && (progress.Value is null || progress.Value < progress.Maximum)
            ? NotificationLifetimes.UntilAcknowledged : NotificationLifetimes.Default);
    /// <summary>Resolves a notification's own snooze choices or the shared fallback choices.</summary>
    /// <param name="request">Optional notification-specific options.</param>
    /// <returns>The effective nonempty snooze choices.</returns>
    public TimeSpan[] ResolveSnooze(NotificationRequest? request = null) => request?.Snooze is { Length: > 0 } choices ? choices : Snooze;
    public TimeSpan SchedulerInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan DeliveryDeadline { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan[] Snooze { get; set; } = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];
    public TimeSpan[] QuickDelays { get; set; } = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];
    public string TimeFormat { get; set; } = "yyyy.MM.dd HH.mm.ss";
    public NotificationLevel Level { get; set; } = NotificationLevel.Info;
    public int MaximumVisible { get; set; } = 3;
    public SoundPattern Sound { get; set; } = NotificationSounds.ChimeUp;
}
/// <summary>Groups window sizes, screen placement, and display capacity.</summary>
internal sealed class WindowOptions
{
    public HotkeyOptions Hotkey { get; set; } = new();
    public System.Drawing.Size Panel { get; set; } = new(860, 720);
    public System.Drawing.Size Toast { get; set; } = new(410, 160);
    public DisplaySizing ToastSizing { get; set; } = DisplaySizing.DeviceIndependent;
    public System.Drawing.Size MinimumPanel { get; set; } = new(620, 460);
    public int ScreenMargin { get; set; } = 16;
    public int CardGap { get; set; } = 8;
}
/// <summary>Shares Windows tone constraints and interruptible playback slices between editors, validation and native audio.</summary>
internal sealed class AudioPolicy
{
    public int SynthesizedCacheCapacity { get; set; } = 32;
    public int SynthesizedCacheByteCapacity { get; set; } = 16 * 1024 * 1024;
    public System.ComponentModel.DataAnnotations.RangeAttribute Frequency { get; set; } = new(37, 32767);
    public System.ComponentModel.DataAnnotations.RangeAttribute DurationMilliseconds { get; set; } = new(1d, int.MaxValue);
    public System.ComponentModel.DataAnnotations.RangeAttribute PauseMilliseconds { get; set; } = new(0d, int.MaxValue);
    public TimeSpan PlaybackSlice { get; set; } = TimeSpan.FromMilliseconds(50);
}
/// <summary>Determines whether notification dimensions follow physical pixels or system text scaling.</summary>
internal enum DisplaySizing { PhysicalPixels, DeviceIndependent }
/// <summary>Identifies the native registration used by the panel shortcut.</summary>
internal sealed class HotkeyOptions
{
    public int Id { get; set; } = 1;
    public ConsoleKey Key { get; set; } = ConsoleKey.N;
    public ConsoleModifiers Modifiers { get; set; } = ConsoleModifiers.Control | ConsoleModifiers.Alt;
}
/// <summary>Bounds the resident renderer's stylesheet, template, and image caches.</summary>
internal sealed class RenderMemoryOptions
{
    /// <summary>Supported local, web and embedded image resource schemes shared by validation and the renderer.</summary>
    public HashSet<string> ImageSchemes { get; set; } = new(StringComparer.OrdinalIgnoreCase) { Uri.UriSchemeFile, Uri.UriSchemeHttp, Uri.UriSchemeHttps, "data" };
    public int Styles { get; set; } = 64;
    public int Templates { get; set; } = 32;
    public int Images { get; set; } = 32;
}
/// <summary>Names the shipped HTML template and the reserved unprivileged template action.</summary>
internal sealed class HtmlTemplateOptions
{
    public string DefaultFile { get; set; } = "Card.html";
    public string ResourceName { get; set; } = "UsefulNotification.Templates.Card.html";
    public string DismissAction { get; set; } = "dismiss";
}
