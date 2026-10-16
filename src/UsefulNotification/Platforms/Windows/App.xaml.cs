namespace UsefulNotification.Desktop.WinUI;

/// <summary>Owns single-instance activation before MAUI creates any window.</summary>
public partial class App : MauiWinUIApplication
{
    static Mutex? Instance;
    /// <summary>Forwards a second launch to the running panel and retains the first process's mutex.</summary>
    public App()
    {
        Instance = new Mutex(true, AppConstants.Transport.MutexName, out bool first);
        if (!first)
        {
            var result = RunSafely(() => NotificationClient.SendAsync(operation: NotificationOperation.ShowPanel, startApplication: false).GetAwaiter().GetResult());
            Environment.Exit(result.Succeeded ? 0 : 1);
            return;
        }
        InitializeComponent();
        UnhandledException += (_, args) => { Log(args.Exception, PrintToConsole: false); args.Handled = true; };
    }
    /// <summary>Constructs the shared MAUI application.</summary>
    /// <returns>The desktop application host.</returns>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
