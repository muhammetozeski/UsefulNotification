using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using MauiWindow = Microsoft.Maui.Controls.Window;
using NativeWindow = Microsoft.UI.Xaml.Window;

namespace UsefulNotification.Desktop;

/// <summary>Owns borderless panel/toast windows, tray activation, visibility evidence, and native sound.</summary>
internal sealed class WindowHost
{
    const bool IsLogEnabled = true;
    NativeWindow Panel = null!;
    NativeWindow? Toast;
    MauiWindow PanelWindow = null!;
    TaskbarIcon? Tray;
    SUBCLASSPROC? WindowProcedure;
    IDispatcherTimer? Scheduler;
    readonly SemaphoreSlim SoundGate = new(1);
    bool Exiting;
    bool PanelWanted = !Environment.GetCommandLineArgs().Contains("--background");
    bool ToastPrepared;
    bool PanelPrepared;
    HashSet<Guid> RenderedCards = [];
    int ToastHeight = AppConstants.Windows.Toast.Height;
    static NativeAppearance Chrome = new();
    public double ToastContentScale => AppConstants.Windows.ToastSizing == DisplaySizing.PhysicalPixels && Toast is not null
        ? PInvoke.USER_DEFAULT_SCREEN_DPI / (double)PInvoke.GetDpiForWindow(Handle(Toast)) : 1;

    /// <summary>Defines the native frame styles removed before HTML is allowed on screen.</summary>
    sealed class NativeAppearance
    {
        public WINDOW_STYLE FrameStyles = WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_BORDER | WINDOW_STYLE.WS_DLGFRAME | WINDOW_STYLE.WS_THICKFRAME | WINDOW_STYLE.WS_SYSMENU | WINDOW_STYLE.WS_MINIMIZEBOX | WINDOW_STYLE.WS_MAXIMIZEBOX;
        public WINDOW_EX_STYLE EdgeStyles = WINDOW_EX_STYLE.WS_EX_WINDOWEDGE | WINDOW_EX_STYLE.WS_EX_CLIENTEDGE | WINDOW_EX_STYLE.WS_EX_STATICEDGE | WINDOW_EX_STYLE.WS_EX_DLGMODALFRAME;
    }

    /// <summary>Configures native chrome, starts the request listener, and creates the reusable toast window.</summary>
    /// <param name="window">The MAUI panel whose native handler has been created.</param>
    public void Initialize(MauiWindow window)
    {
        PanelWindow = window;
        Panel = (NativeWindow)window.Handler!.PlatformView!;
        Configure(Panel, false);
        Panel.Activated += (_, args) => { if (!PanelWanted && args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated) Panel.AppWindow.Hide(); };
        if (App.Hub.Preferences.PanelBounds is { } saved) Panel.AppWindow.MoveAndResize(new(saved.X, saved.Y, saved.Width, saved.Height));
        else
        {
            RectInt32 work = WorkArea(Panel);
            Panel.AppWindow.MoveAndResize(new(work.X + (work.Width - AppConstants.Windows.Panel.Width) / 2, work.Y + (work.Height - AppConstants.Windows.Panel.Height) / 2, AppConstants.Windows.Panel.Width, AppConstants.Windows.Panel.Height));
        }
        Panel.AppWindow.Closing += (_, args) => { if (!Exiting) { args.Cancel = true; ShowPanel(false); } };
        Panel.AppWindow.Changed += (_, args) =>
        {
            if (args.DidPositionChange || args.DidSizeChange)
            {
                var bounds = Panel.AppWindow;
                App.Hub.Preferences.PanelBounds = new(bounds.Position.X, bounds.Position.Y, bounds.Size.Width, bounds.Size.Height);
            }
        };
        Tray = new TaskbarIcon
        {
            IconSource = ImageSource.FromFile(Path.Combine(AppPaths.PayloadDirectory, AppConstants.Product.Icon)),
            ToolTipText = AppConstants.Product.Name,
            LeftClickCommand = new Command(() => ShowPanel()),
            RightClickCommand = new Command(() => ShowPanel()),
            DoubleClickCommand = new Command(() => ShowPanel())
        };
        Tray.ForceCreate(false);
        InstallHotkey();
        App.Hub.Changed += () => MainThread.BeginInvokeOnMainThread(() => RunSafely(Refresh));
        App.Hub.PanelRequested += () => MainThread.BeginInvokeOnMainThread(() => ShowPanel());
        App.Hub.ShutdownRequested += () => MainThread.BeginInvokeOnMainThread(Exit);
        App.Hub.SoundRequested += entry => _ = PlaySoundAsync(entry.Request.Sound);
        App.Hub.DeliveryIssue += entry =>
        {
            Log($"Delivery not confirmed; id={entry.Id}; source={entry.Request.Source}", PrintToConsole: false, isRun: IsLogEnabled);
            MainThread.BeginInvokeOnMainThread(() => ShowPanel());
            PInvoke.MessageBeep(MESSAGEBOX_STYLE.MB_ICONWARNING);
        };
        App.Hub.ServerTask = Task.Run(() => RunSafelyAsync(async token => await App.Hub.ListenAsync()));
        Scheduler = Application.Current!.Dispatcher.CreateTimer();
        Scheduler.Interval = AppConstants.Notifications.SchedulerInterval;
        Scheduler.Tick += (_, _) => RunSafely(App.Hub.Tick);
        Scheduler.Start();
        MauiWindow toast = App.CreateSurface(true);
        toast.Created += (_, _) =>
        {
            Toast = (NativeWindow)toast.Handler!.PlatformView!;
            Configure(Toast, true);
            Toast.AppWindow.Resize(new(AppConstants.Windows.Toast.Width, AppConstants.Windows.Toast.Height));
            Toast.Activated += (_, args) => { if ((!ToastPrepared || RenderedCards.Count == 0) && args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated) Toast.AppWindow.Hide(); };
            Toast.AppWindow.Hide();
            Refresh();
        };
        Application.Current.OpenWindow(toast);
        if (Environment.GetCommandLineArgs().Contains("--background")) Panel.AppWindow.Hide();
    }

    /// <summary>Opens or hides the existing panel without stopping scheduling.</summary>
    /// <param name="show">Whether to show the panel.</param>
    public void ShowPanel(bool show = true)
    {
        PanelWanted = show;
        if (show) { Panel.AppWindow.Show(); Panel.Activate(); }
        else { App.Hub.SavePreferences(); Panel.AppWindow.Hide(); }
    }

    /// <summary>Restacks the active notifications on the selected monitor.</summary>
    public void Refresh()
    {
        if (Toast is null) return;
        var active = App.Hub.Snapshot().Where(item => item.Status == NotificationStatus.Visible || item.Status == NotificationStatus.Pending && item.DeliveryStartedAt is not null).ToArray();
        if (active.Length == 0) { Toast.AppWindow.Hide(); return; }
        if (!ToastPrepared || !RenderedCards.SetEquals(active.Select(entry => entry.Id))) return;
        nint extended = PInvoke.GetWindowLongPtr(Handle(Toast), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        bool acceptsInput = active.Any(entry => entry.Request.Input is not null || entry.Request.Kind == NotificationKind.Custom);
        PInvoke.SetWindowLongPtr(Handle(Toast), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, acceptsInput ? extended & ~(nint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE : extended | (nint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
        RectInt32 work = WorkArea(Toast);
        double scale = PInvoke.GetDpiForWindow(Handle(Toast)) / (double)PInvoke.USER_DEFAULT_SCREEN_DPI;
        int width = Math.Min(work.Width, AppConstants.Windows.ToastSizing == DisplaySizing.PhysicalPixels ? AppConstants.Windows.Toast.Width : (int)(AppConstants.Windows.Toast.Width * scale));
        int height = Math.Min(work.Height - AppConstants.Windows.ScreenMargin * 2, (int)(ToastHeight * scale));
        Toast.AppWindow.MoveAndResize(new(work.X + work.Width - width - AppConstants.Windows.ScreenMargin, work.Y + work.Height - height - AppConstants.Windows.ScreenMargin, width, height));
        Toast.AppWindow.Show(false);
    }

    /// <summary>Adjusts the native toast height to the actual HTML card contents.</summary>
    /// <param name="height">Content height in logical pixels.</param>
    public void ResizeToast(double height) { ToastHeight = (int)Math.Ceiling(height); Refresh(); }

    /// <summary>Publishes a fully rendered HTML surface before making the corresponding native window visible.</summary>
    /// <param name="toast">Whether the resident toast renderer completed a render.</param>
    /// <param name="ids">The card identities present in that completed render.</param>
    public void SurfacePrepared(bool toast, IEnumerable<Guid> ids)
    {
        if (toast)
        {
            if (!ToastPrepared && Toast is not null) { Configure(Toast, true); ToastPrepared = true; }
            RenderedCards = ids.ToHashSet();
            Refresh();
        }
        else
        {
            if (!PanelPrepared) { Configure(Panel, false); PanelPrepared = true; }
            if (!PanelWanted) Panel.AppWindow.Hide();
        }
    }

    /// <summary>Checks native visibility and screen intersection before recording rendered cards as delivered.</summary>
    /// <param name="ids">Rendered notification identities.</param>
    /// <param name="toast">Which native surface rendered the cards.</param>
    public void ConfirmRendered(IEnumerable<Guid> ids, bool toast)
    {
        NativeWindow? native = toast ? Toast : Panel;
        if (native is null || !PInvoke.IsWindowVisible(Handle(native)) || !PInvoke.GetWindowRect(Handle(native), out RECT bounds)) return;
        RectInt32 work = WorkArea(native);
        if (bounds.right <= work.X || bounds.bottom <= work.Y || bounds.left >= work.X + work.Width || bounds.top >= work.Y + work.Height) return;
        App.Hub.ConfirmVisible(ids);
    }

    /// <summary>Begins the normal Windows move operation from the HTML header.</summary>
    /// <param name="toast">Whether the dragged header belongs to a toast window.</param>
    public void Drag(bool toast = false)
    {
        NativeWindow? native = toast ? Toast : Panel;
        if (native is null) return;
        PInvoke.ReleaseCapture();
        PInvoke.SendMessage(Handle(native), PInvoke.WM_NCLBUTTONDOWN, new WPARAM((nuint)PInvoke.HTCAPTION), default);
    }

    /// <summary>Plays a local audio file or tone sequence, with a system-sound fallback after an actual failure.</summary>
    /// <param name="pattern">Optional sequence; null resolves the user's selected sound.</param>
    /// <returns>The complete sound operation.</returns>
    public async Task PlaySoundAsync(SoundPattern? pattern = null)
    {
        await RunSafelyAsync(async token =>
        {
            await SoundGate.WaitAsync(token);
            try
            {
                pattern ??= App.Hub.Preferences.Sounds.FirstOrDefault(sound => sound.Name == App.Hub.Preferences.SelectedSound) ?? AppConstants.Notifications.Sound;
                try
                {
                    if (!string.IsNullOrWhiteSpace(pattern.AudioFile))
                    {
                        using var player = new MediaPlayer();
                        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        player.MediaEnded += (_, _) => ended.TrySetResult();
                        player.MediaFailed += (_, args) => ended.TrySetException(new IOException(args.ErrorMessage));
                        player.Source = MediaSource.CreateFromUri(new Uri(Path.GetFullPath(pattern.AudioFile, AppPaths.Root)));
                        player.Play();
                        await ended.Task.WaitAsync(token);
                    }
                    else
                        foreach (ToneStep step in pattern.Steps)
                        {
                            token.ThrowIfCancellationRequested();
                            await Task.Run(() => { if (!PInvoke.Beep((uint)step.Frequency, (uint)step.Duration.TotalMilliseconds)) throw new System.ComponentModel.Win32Exception(); }, token);
                            await Task.Delay(step.Pause, token);
                        }
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    Log(error, PrintToConsole: false, isRun: IsLogEnabled);
                    if (!PInvoke.MessageBeep(MESSAGEBOX_STYLE.MB_OK)) throw new System.ComponentModel.Win32Exception();
                }
            }
            finally { SoundGate.Release(); }
        });
    }

    /// <summary>Stops the listener, saves placement, removes the tray icon, and closes the process.</summary>
    public void Exit()
    {
        Exiting = true;
        App.Hub.SavePreferences();
        Scheduler?.Stop();
        App.Hub.Dispose();
        Tray?.Dispose();
        PInvoke.UnregisterHotKey(Handle(Panel), AppConstants.Windows.Hotkey.Id);
        Application.Current!.Quit();
    }

    /// <summary>Configures the chrome and extended styles once for a native HTML surface.</summary>
    /// <param name="native">Native window.</param>
    /// <param name="toast">Whether the surface is a non-activating toast.</param>
    unsafe void Configure(NativeWindow native, bool toast)
    {
        native.SystemBackdrop = null;
        native.ExtendsContentIntoTitleBar = true;
        native.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        native.SetTitleBar(null);
        WindowProcedure ??= (window, message, wparam, lparam, subclass, data) =>
        {
            if (message == PInvoke.WM_STYLECHANGING && (nint)wparam.Value == (nint)WINDOW_LONG_PTR_INDEX.GWL_STYLE)
            {
                STYLESTRUCT* change = (STYLESTRUCT*)lparam.Value;
                change->styleNew = (uint)(((WINDOW_STYLE)change->styleNew & ~Chrome.FrameStyles) | WINDOW_STYLE.WS_POPUP);
            }
            if (message == PInvoke.WM_HOTKEY && (int)wparam.Value == AppConstants.Windows.Hotkey.Id) ShowPanel();
            return PInvoke.DefSubclassProc(window, message, wparam, lparam);
        };
        if (!PInvoke.SetWindowSubclass(Handle(native), WindowProcedure, (nuint)AppConstants.Windows.Hotkey.Id, 0)) throw new System.ComponentModel.Win32Exception();
        var presenter = (OverlappedPresenter)native.AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = !toast;
        presenter.IsMaximizable = !toast;
        presenter.IsMinimizable = !toast;
        presenter.IsAlwaysOnTop = toast;
        nint frameStyle = PInvoke.GetWindowLongPtr(Handle(native), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        PInvoke.SetWindowLongPtr(Handle(native), WINDOW_LONG_PTR_INDEX.GWL_STYLE, (nint)(((WINDOW_STYLE)(uint)frameStyle & ~Chrome.FrameStyles) | WINDOW_STYLE.WS_POPUP));
        nint edgeStyle = PInvoke.GetWindowLongPtr(Handle(native), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLongPtr(Handle(native), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, edgeStyle & ~(nint)Chrome.EdgeStyles);
        DWMNCRENDERINGPOLICY rendering = DWMNCRENDERINGPOLICY.DWMNCRP_DISABLED;
        PInvoke.DwmSetWindowAttribute(Handle(native), DWMWINDOWATTRIBUTE.DWMWA_NCRENDERING_POLICY, &rendering, (uint)sizeof(DWMNCRENDERINGPOLICY)).ThrowOnFailure();
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            DWM_WINDOW_CORNER_PREFERENCE corners = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            PInvoke.DwmSetWindowAttribute(Handle(native), DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &corners, (uint)sizeof(DWM_WINDOW_CORNER_PREFERENCE)).ThrowOnFailure();
        }
        PInvoke.SetWindowPos(Handle(native), default, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        native.AppWindow.SetIcon(Path.Combine(AppPaths.PayloadDirectory, AppConstants.Product.Icon));
        if (toast)
        {
            native.AppWindow.IsShownInSwitchers = false;
            nint style = PInvoke.GetWindowLongPtr(Handle(native), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            PInvoke.SetWindowLongPtr(Handle(native), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style | (nint)(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE));
        }
    }

    /// <summary>Registers the configurable global panel shortcut and keeps its callback alive.</summary>
    public void InstallHotkey()
    {
        var shortcut = AppConstants.Windows.Hotkey;
        HWND handle = Handle(Panel);
        PInvoke.UnregisterHotKey(handle, shortcut.Id);
        if (Settings.GlobalHotkey.Value && !PInvoke.RegisterHotKey(handle, shortcut.Id, HOT_KEY_MODIFIERS.MOD_CONTROL | HOT_KEY_MODIFIERS.MOD_ALT | HOT_KEY_MODIFIERS.MOD_NOREPEAT, (uint)VIRTUAL_KEY.VK_N))
            Log("Panel shortcut could not be registered; another application may own it.", PrintToConsole: false, isRun: IsLogEnabled);
    }

    /// <summary>Finds the selected monitor's physical working rectangle.</summary>
    /// <param name="native">Fallback window for monitor selection.</param>
    /// <returns>The monitor area excluding the taskbar.</returns>
    static RectInt32 WorkArea(NativeWindow native)
    {
        var monitors = DisplayArea.FindAll();
        return Settings.Monitor.Value > 0 && Settings.Monitor.Value <= monitors.Count ? monitors[Settings.Monitor.Value - 1].WorkArea : DisplayArea.GetFromWindowId(native.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
    }

    /// <summary>Retrieves a native handle from a WinUI window.</summary>
    /// <param name="native">The window whose handle is needed.</param>
    /// <returns>The typed Windows handle.</returns>
    static HWND Handle(NativeWindow native) => new(WinRT.Interop.WindowNative.GetWindowHandle(native));
}
