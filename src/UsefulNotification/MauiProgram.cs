using H.NotifyIcon;
namespace UsefulNotification.Desktop;

/// <summary>Builds the MAUI host and the Blazor renderer; application services remain explicit static objects.</summary>
public static class MauiProgram
{
    /// <summary>Initializes portable application services before building the desktop host.</summary>
    /// <returns>The configured MAUI application.</returns>
    public static MauiApp CreateMauiApp()
    {
        if (!ApplicationServices.Initialize().Succeeded) throw new InvalidOperationException("Portable settings or language initialization failed. See AppCache/Logs.");
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>().UseNotifyIcon();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        return builder.Build();
    }
}
