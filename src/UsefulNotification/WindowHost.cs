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
using Microsoft.Win32;

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
    readonly SemaphoreSlim SoundGate = new(1);
    bool Exiting;
    bool PanelWanted = !Environment.GetCommandLineArgs().Contains(AppConstants.Product.BackgroundArgument);
    bool ToastPrepared;
    bool PanelPrepared;
    HashSet<Guid> RenderedCards = [];
    int ToastHeight = AppConstants.Windows.Toast.Height;
    static NativeAppearance Chrome = new();
    static StartupRegistration Startup = new();
    WindowInteraction? Interaction;
    /// <summary>Tracks the held-pointer origin and original window rectangle, following ShowPicOnly's delta-based interaction.</summary>
    /// <param name="Hit">Validated caption or sizing-edge target that selects movement direction.</param>
    /// <param name="Cursor">Physical screen coordinates of the pointer when the gesture began.</param>
    /// <param name="Bounds">Original physical panel rectangle used for every delta calculation.</param>
    sealed record WindowInteraction(uint Hit, System.Drawing.Point Cursor, System.Drawing.Rectangle Bounds)
    {
        public System.Drawing.Point LastCursor = Cursor;
    }
    /// <summary>Whether panel content is requested; hidden panels can skip rendering their full history.</summary>
    public bool IsPanelVisible => PanelWanted;
    /// <summary>Requests a renderer-owned close flow so it can resolve unsaved drafts before hiding the native panel.</summary>
    public event Action? CloseRequested;
    /// <summary>Lists current display indexes with localized working-area dimensions for the monitor selector.</summary>
    public List<(int Index, string Name)> Monitors
    {
        get
        {
            var displays = DisplayArea.FindAll();
            var choices = new List<(int, string)>();
            for (int index = 0; index < displays.Count; index++)
            {
                var area = displays[index].WorkArea;
                choices.Add((index + 1, string.Format(Localization.Strings.MonitorNumber, index + 1, area.Width, area.Height)));
            }
            return choices;
        }
    }
    /// <summary>CSS zoom factor compensating the toast monitor's DPI when the configured size uses physical pixels.</summary>
    public double ToastContentScale => AppConstants.Windows.ToastSizing == DisplaySizing.PhysicalPixels && Toast is not null
        ? PInvoke.USER_DEFAULT_SCREEN_DPI / (double)PInvoke.GetDpiForWindow(Handle(Toast)) : 1;

    /// <summary>Defines the native frame styles removed before HTML is allowed on screen.</summary>
    sealed class NativeAppearance
    {
        public WINDOW_STYLE FrameStyles = WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_BORDER | WINDOW_STYLE.WS_DLGFRAME | WINDOW_STYLE.WS_THICKFRAME | WINDOW_STYLE.WS_SYSMENU | WINDOW_STYLE.WS_MINIMIZEBOX | WINDOW_STYLE.WS_MAXIMIZEBOX;
        public WINDOW_EX_STYLE EdgeStyles = WINDOW_EX_STYLE.WS_EX_WINDOWEDGE | WINDOW_EX_STYLE.WS_EX_CLIENTEDGE | WINDOW_EX_STYLE.WS_EX_STATICEDGE | WINDOW_EX_STYLE.WS_EX_DLGMODALFRAME;
        public HashSet<uint> InteractionAreas = [PInvoke.HTCAPTION, PInvoke.HTLEFT, PInvoke.HTRIGHT, PInvoke.HTTOP, PInvoke.HTBOTTOM, PInvoke.HTTOPLEFT, PInvoke.HTTOPRIGHT, PInvoke.HTBOTTOMLEFT, PInvoke.HTBOTTOMRIGHT];
    }
    /// <summary>Names the current user's optional sign-in registration.</summary>
    sealed class StartupRegistration
    {
        public string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    }

    /// <summary>Configures native chrome, starts the request listener, and creates the reusable toast window.</summary>
    /// <param name="window">The MAUI panel whose native handler has been created.</param>
    public void Initialize(MauiWindow window)
    {
        PanelWindow = window;
        Panel = (NativeWindow)window.Handler!.PlatformView!;
        Configure(Panel, false);
        Panel.Activated += (_, args) => { if (!PanelWanted && args.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated) Panel.AppWindow.Hide(); };
        RestorePanelBounds();
        Panel.AppWindow.Closing += (_, args) => { if (!Exiting) { args.Cancel = true; if (CloseRequested is not null) CloseRequested.Invoke(); else ShowPanel(false); } };
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
        if (Settings.StartWithWindows.Value) ApplyStartup();
        App.Hub.Changed += () => MainThread.BeginInvokeOnMainThread(() => RunSafely(Refresh));
        App.Hub.PanelRequested += () => MainThread.BeginInvokeOnMainThread(() => ShowPanel());
        App.Hub.ShutdownRequested += () => MainThread.BeginInvokeOnMainThread(Exit);
        App.Hub.SettingsReloaded += () => MainThread.BeginInvokeOnMainThread(() => RunSafely(() => { InstallHotkey(); ApplyStartup(); }));
        App.Hub.SoundRequested += entry => App.Hub.RunBackground(token => PlaySoundAsync(entry.Request.Sound, token));
        App.Hub.DeliveryIssue += entry =>
        {
            Log($"Delivery not confirmed; id={entry.Id}; source={entry.Request.Source}", PrintToConsole: false, isRun: IsLogEnabled);
            MainThread.BeginInvokeOnMainThread(() => { if (Tray is not null) Tray.ToolTipText = $"{AppConstants.Product.Name}: {Localization.Strings.Failed}"; });
        };
        RunSafely(HostDiscovery.Register);
        App.Hub.Start();
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
        Application.Current!.OpenWindow(toast);
        if (!PanelWanted) Panel.AppWindow.Hide();
    }

    /// <summary>Opens or hides the existing panel without stopping scheduling.</summary>
    /// <param name="show">Whether to show the panel.</param>
    public void ShowPanel(bool show = true)
    {
        RunSafely(() =>
        {
            if (!show)
            {
                try { App.Hub.SavePreferences(); }
                finally { Interaction = null; PanelWanted = false; Panel.AppWindow.Hide(); App.Hub.InvalidateViews(); }
                return;
            }
            PanelWanted = true; App.Hub.InvalidateViews();
            if (PanelPrepared) { var presenter = (OverlappedPresenter)Panel.AppWindow.Presenter; if (presenter.State == OverlappedPresenterState.Minimized) presenter.Restore(); Panel.AppWindow.Show(); Panel.Activate(); }
        });
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
            if (!PanelPrepared)
            {
                Configure(Panel, false); PanelPrepared = true;
                if (PanelWanted) Panel.AppWindow.Show(); else Panel.AppWindow.Hide();
            }
        }
    }

    /// <summary>Checks native visibility and screen intersection before recording rendered cards as delivered.</summary>
    /// <param name="receipts">Card viewport evidence for the current toast content revision.</param>
    public void ConfirmRendered(RenderReceipt[] receipts)
    {
        NativeWindow? native = Toast;
        if (native is null) return;
        bool visible = PInvoke.IsWindowVisible(Handle(native));
        RectInt32 work = WorkArea(native);
        if (visible && PInvoke.GetWindowRect(Handle(native), out RECT bounds))
            visible = bounds.right > work.X && bounds.bottom > work.Y && bounds.left < work.X + work.Width && bounds.top < work.Y + work.Height;
        else visible = false;
        App.Hub.ConfirmVisible(receipts.Select(receipt => receipt with { Visible = visible && receipt.Visible }));
    }

    /// <summary>Captures the actual pointer and rectangle when an HTML move/size target is pressed.</summary>
    /// <param name="hit">A named Windows caption or sizing-edge hit-test value supplied by the HTML control.</param>
    public void BeginWindowInteraction(uint hit)
    {
        Interaction = null;
        if (!PanelWanted || !Chrome.InteractionAreas.Contains(hit) || PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_LBUTTON) >= 0) return;
        if (!PInvoke.GetCursorPos(out var pointer)) throw new System.ComponentModel.Win32Exception();
        var position = Panel.AppWindow.Position; var size = Panel.AppWindow.Size;
        Interaction = new(hit, new(pointer.X, pointer.Y), new(position.X, position.Y, size.Width, size.Height));
    }

    /// <summary>Applies the held cursor's delta directly; no Windows modal move loop owns button release.</summary>
    public void UpdateWindowInteraction()
    {
        if (Interaction is not { } interaction) return;
        if (PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_LBUTTON) >= 0) { Interaction = null; return; }
        if (!PInvoke.GetCursorPos(out var pointer)) throw new System.ComponentModel.Win32Exception();
        var cursor = new System.Drawing.Point(pointer.X, pointer.Y);
        if (cursor == interaction.LastCursor) return;
        interaction.LastCursor = cursor;
        int deltaX = cursor.X - interaction.Cursor.X, deltaY = cursor.Y - interaction.Cursor.Y;
        var original = interaction.Bounds;
        if (interaction.Hit == PInvoke.HTCAPTION) { Panel.AppWindow.Move(new(original.X + deltaX, original.Y + deltaY)); return; }
        bool left = interaction.Hit is PInvoke.HTLEFT or PInvoke.HTTOPLEFT or PInvoke.HTBOTTOMLEFT;
        bool right = interaction.Hit is PInvoke.HTRIGHT or PInvoke.HTTOPRIGHT or PInvoke.HTBOTTOMRIGHT;
        bool top = interaction.Hit is PInvoke.HTTOP or PInvoke.HTTOPLEFT or PInvoke.HTTOPRIGHT;
        bool bottom = interaction.Hit is PInvoke.HTBOTTOM or PInvoke.HTBOTTOMLEFT or PInvoke.HTBOTTOMRIGHT;
        double scale = PInvoke.GetDpiForWindow(Handle(Panel)) / (double)PInvoke.USER_DEFAULT_SCREEN_DPI;
        int width = Math.Max((int)(AppConstants.Windows.MinimumPanel.Width * scale), original.Width + (right ? deltaX : left ? -deltaX : 0));
        int height = Math.Max((int)(AppConstants.Windows.MinimumPanel.Height * scale), original.Height + (bottom ? deltaY : top ? -deltaY : 0));
        Panel.AppWindow.MoveAndResize(new(left ? original.Right - width : original.X, top ? original.Bottom - height : original.Y, width, height));
    }

    /// <summary>Ends movement on pointer release, capture loss, blur, cancellation, or panel closure.</summary>
    public void EndWindowInteraction() => Interaction = null;

    /// <summary>Minimizes the panel while the notification broker stays available.</summary>
    public void Minimize() { Interaction = null; ((OverlappedPresenter)Panel.AppWindow.Presenter).Minimize(); }

    /// <summary>Restores placement inside an available screen after a monitor or display scaling change.</summary>
    void RestorePanelBounds()
    {
        var saved = App.Hub.Preferences.PanelBounds;
        RectInt32 work = WorkArea(Panel);
        if (saved is { } bounds)
        {
            var displays = DisplayArea.FindAll();
            long largestOverlap = 0;
            for (int index = 0; index < displays.Count; index++)
            {
                var area = displays[index].WorkArea;
                long overlap = Math.Max(0, Math.Min(bounds.Right, area.X + area.Width) - Math.Max(bounds.X, area.X)) * (long)Math.Max(0, Math.Min(bounds.Bottom, area.Y + area.Height) - Math.Max(bounds.Y, area.Y));
                if (overlap > largestOverlap) { largestOverlap = overlap; work = area; }
            }
        }
        double scale = PInvoke.GetDpiForWindow(Handle(Panel)) / (double)PInvoke.USER_DEFAULT_SCREEN_DPI;
        int width = Math.Clamp(saved?.Width ?? AppConstants.Windows.Panel.Width, Math.Min(work.Width, (int)(AppConstants.Windows.MinimumPanel.Width * scale)), work.Width);
        int height = Math.Clamp(saved?.Height ?? AppConstants.Windows.Panel.Height, Math.Min(work.Height, (int)(AppConstants.Windows.MinimumPanel.Height * scale)), work.Height);
        Panel.AppWindow.MoveAndResize(new(Math.Clamp(saved?.X ?? work.X + (work.Width - width) / 2, work.X, work.X + work.Width - width), Math.Clamp(saved?.Y ?? work.Y + (work.Height - height) / 2, work.Y, work.Y + work.Height - height), width, height));
    }

    /// <summary>Plays a local audio file or tone sequence, with a system-sound fallback after an actual failure.</summary>
    /// <param name="pattern">Optional sequence; null resolves the user's selected sound.</param>
    /// <param name="cancellationToken">Cancels waiting or playback together with the broker's shutdown token.</param>
    /// <returns>The complete sound operation.</returns>
    public async Task PlaySoundAsync(SoundPattern? pattern = null, CancellationToken cancellationToken = default)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, App.Hub.Lifetime);
        await RunSafelyAsync(async token =>
        {
            await SoundGate.WaitAsync(token);
            try
            {
                pattern = NotificationProtocol.Copy(pattern ?? App.Hub.Preferences.SelectedSound ?? AppConstants.Notifications.Sound);
                try
                {
                    if (pattern.AudioFile is { } audio)
                    {
                        using var player = new MediaPlayer();
                        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        player.MediaEnded += (_, _) => ended.TrySetResult();
                        player.MediaFailed += (_, args) => ended.TrySetException(new IOException(args.ErrorMessage));
                        player.Source = MediaSource.CreateFromUri(new Uri(audio.FullName));
                        player.Play();
                        await ended.Task.WaitAsync(token);
                    }
                    else
                        foreach (ToneStep step in pattern.Steps)
                        {
                            token.ThrowIfCancellationRequested();
                            for (TimeSpan remaining = step.Duration; remaining > TimeSpan.Zero;)
                            {
                                token.ThrowIfCancellationRequested();
                                var slice = remaining < AppConstants.Audio.PlaybackSlice ? remaining : AppConstants.Audio.PlaybackSlice;
                                await Task.Run(() => { if (!PInvoke.Beep((uint)step.Frequency, (uint)slice.TotalMilliseconds)) throw new System.ComponentModel.Win32Exception(); }, token);
                                remaining -= slice;
                            }
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
        }, stopping.Token);
    }

    /// <summary>Stops the listener, saves placement, removes the tray icon, and closes the process.</summary>
    public async void Exit()
    {
        if (Exiting) return;
        Exiting = true;
        await RunSafelyAsync(async token =>
        {
            try { App.Hub.SavePreferences(); }
            finally
            {
                await App.Hub.DisposeAsync();
                Tray?.Dispose();
                PInvoke.UnregisterHotKey(Handle(Panel), AppConstants.Windows.Hotkey.Id);
                Application.Current!.Quit();
            }
        });
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
            // WinUI can retain nonclient insets after its style changes. HTML owns the entire window rectangle.
            if (message == PInvoke.WM_NCCALCSIZE) return default;
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
        HOT_KEY_MODIFIERS modifiers = HOT_KEY_MODIFIERS.MOD_NOREPEAT;
        if (shortcut.Modifiers.HasFlag(ConsoleModifiers.Control)) modifiers |= HOT_KEY_MODIFIERS.MOD_CONTROL;
        if (shortcut.Modifiers.HasFlag(ConsoleModifiers.Alt)) modifiers |= HOT_KEY_MODIFIERS.MOD_ALT;
        if (shortcut.Modifiers.HasFlag(ConsoleModifiers.Shift)) modifiers |= HOT_KEY_MODIFIERS.MOD_SHIFT;
        if (Settings.GlobalHotkey.Value && !PInvoke.RegisterHotKey(handle, shortcut.Id, modifiers, (uint)shortcut.Key))
            Log("Panel shortcut could not be registered; another application may own it.", PrintToConsole: false, isRun: IsLogEnabled);
    }

    /// <summary>Applies the user's explicit sign-in preference without requiring administrator rights.</summary>
    public void ApplyStartup()
    {
        if (Settings.StartWithWindows.Value)
        {
            string launcher = Path.Combine(AppPaths.Root, AppConstants.Product.Executable);
            if (!File.Exists(launcher)) launcher = Path.Combine(AppPaths.PayloadDirectory, AppConstants.Product.Executable);
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(Startup.RegistryPath);
            key.SetValue(AppConstants.Product.Name, $"\"{launcher}\" {AppConstants.Product.BackgroundArgument}", RegistryValueKind.String);
        }
        else
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(Startup.RegistryPath, writable: true);
            key?.DeleteValue(AppConstants.Product.Name, throwOnMissingValue: false);
        }
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
