using Microsoft.AspNetCore.Components.WebView.Maui;
namespace UsefulNotification.Desktop;

/// <summary>Owns the notification journal and creates HTML surfaces inside native borderless windows.</summary>
public sealed class App : Application
{
    internal static NotificationHub Hub = null!;
    internal static WindowHost Host = null!;

    /// <summary>Creates the shared journal and window controller.</summary>
    public App()
    {
        Hub = new();
        ThemeStyle.WarmUp();
        Host = new();
    }

    /// <summary>Creates the main panel and connects it when its native window exists.</summary>
    /// <param name="activationState">Platform activation information.</param>
    /// <returns>The main panel window.</returns>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = CreateSurface(false);
        window.Created += (_, _) => Host.Initialize(window);
        return window;
    }

    /// <summary>Creates a panel or toast surface using the same Blazor components.</summary>
    /// <param name="toast">Whether this window displays transient notification cards.</param>
    /// <returns>A MAUI window containing the HTML renderer.</returns>
    internal static Window CreateSurface(bool toast)
    {
        var web = new BlazorWebView { HostPage = "wwwroot/index.html" };
        web.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Components.Surface), Parameters = new Dictionary<string, object?> { [nameof(Components.Surface.Toast)] = toast } });
        var page = new ContentPage { Content = web, Padding = 0, BackgroundColor = Colors.Transparent };
        return new Window(page) { Title = AppConstants.Product.Name };
    }
}
