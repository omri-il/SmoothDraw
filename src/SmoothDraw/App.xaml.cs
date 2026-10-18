using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SmoothDraw.Models;
using SmoothDraw.Services;
using SmoothDraw.Views;
using SmoothShared;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace SmoothDraw;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private WinForms.NotifyIcon? _trayIcon;
    private KeyboardHookService? _keyboardHook;
    private OverlayWindow? _overlayWindow;
    private ControlPipeServer? _control;
    private AnnotationSettings _settings = new();

    private static readonly string LogFile = AppIdentity.LogFile;

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // --toggle (the Start-menu entry, the pen's top button): switch drawing on/off in
        // the copy that is already running and leave quietly
        bool toggle = e.Args.Any(a => a.Equals("--toggle", StringComparison.OrdinalIgnoreCase));
        // --autostart (the logon task): already running is fine, say nothing
        bool autostart = e.Args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

        _mutex = new Mutex(true, AppIdentity.MutexName, out bool createdNew);
        if (!createdNew)
        {
            if (toggle)
            {
                // Not logged: this second copy would clear the running one's log on start
                ControlPipeServer.Send(PipeName, "draw toggle");
            }
            else if (!autostart)
            {
                System.Windows.MessageBox.Show($"{AppIdentity.Name} is already running.", AppIdentity.Name,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            Shutdown();
            return;
        }

        // Clear old log — only the copy that stays does this (a second launch left above)
        try { File.Delete(LogFile); } catch { }
        Log($"=== {AppIdentity.Name} starting ===");

        SetupCrashRecovery();

        // Taking over from SmoothAnnotate (this app's name until 2026-09-28): never two
        // drawing layers on one screen, and your settings and the dot's place come along
        StopOldCopy("at start");
        CarryOverOldSettings();

        HideToolTipsFromCapture();

        try
        {
            _settings = SettingsService.Load();
            Log("Settings loaded");

            _overlayWindow = new OverlayWindow(_settings);
            _overlayWindow.Show();
            Log("Overlay window created and shown");

            // Initialize services after window is created
            var laserColor = (Color)ColorConverter.ConvertFromString(_settings.LaserColor);
            var laserService = new LaserService(_overlayWindow.FindName("DrawCanvas") as System.Windows.Controls.InkCanvas
                ?? throw new InvalidOperationException("DrawCanvas not found"),
                _settings.LaserFadeMs, laserColor, _settings.LaserSize);
            var stopwatchService = new StopwatchService();
            _overlayWindow.InitializeServices(laserService, stopwatchService);
            Log("Services initialized");

            SetupTrayIcon();
            Log("Tray icon created");

            SetupKeyboardHook();
            Log("Keyboard hook installed");

            _control = new ControlPipeServer(PipeName, cmd => ControlPipeServer.OnUi(Dispatcher, () => HandleControl(cmd)));
            _control.Start();
            Log($"Control pipe {PipeName} listening");

            if (toggle) _overlayWindow.ToggleDrawOnOff();
        }
        catch (Exception ex)
        {
            Log($"STARTUP ERROR: {ex}");
            System.Windows.MessageBox.Show($"Startup error: {ex.Message}", $"{AppIdentity.Name} Error");
        }

        // Once more a minute later: at logon on a half-migrated machine, the old logon
        // task may start SmoothAnnotate after this app
        var again = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        again.Tick += (_, _) => { again.Stop(); StopOldCopy("60 s after start"); };
        again.Start();
    }

    // --- Taking over from SmoothAnnotate ---

    private static void StopOldCopy(string when)
    {
        foreach (var p in Process.GetProcessesByName(AppIdentity.OldName))
        {
            using (p)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(3000);
                    Log($"Stopped the old {AppIdentity.OldName} (pid {p.Id}, {when})");
                }
                catch (Exception ex)
                {
                    Log($"Could not stop the old {AppIdentity.OldName} (pid {p.Id}, {when}): {ex.Message}");
                }
            }
        }
    }

    /// <summary>Copies (never moves: the old app keeps working if it comes back) each file
    /// this app does not have yet.</summary>
    private static void CarryOverOldSettings()
    {
        foreach (var name in new[] { "settings.json", "dot-position.json" })
        {
            var to = Path.Combine(AppIdentity.SettingsDir, name);
            var from = Path.Combine(AppIdentity.OldSettingsDir, name);
            if (File.Exists(to) || !File.Exists(from)) continue;
            try
            {
                Directory.CreateDirectory(AppIdentity.SettingsDir);
                File.Copy(from, to);
                Log($"Carried over {name} from {AppIdentity.OldSettingsDir}");
            }
            catch (Exception ex)
            {
                Log($"Could not carry over {name} from {AppIdentity.OldSettingsDir}: {ex.Message}");
            }
        }
    }

    // --- Tooltips: hidden from recordings, like the toolbar they belong to ---

    private static void HideToolTipsFromCapture()
    {
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ToolTip),
            System.Windows.Controls.ToolTip.OpenedEvent, new RoutedEventHandler(OnToolTipOpened));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ToolTip),
            System.Windows.Controls.ToolTip.ClosedEvent, new RoutedEventHandler(OnToolTipClosed));
    }

    /// <summary>A tooltip opens in its own window, at opacity 0 (App.xaml). Exclude that
    /// window from capture first, then fade the tooltip in.</summary>
    private static void OnToolTipOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ToolTip tip) return;
        if (PresentationSource.FromVisual(tip) is HwndSource source)
            OverlayService.HideToolTipWindow(source.Handle);
        else
            Log("Tooltip: no window found to hide from capture");
        tip.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
    }

    private static void OnToolTipClosed(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ToolTip tip) return;
        tip.BeginAnimation(UIElement.OpacityProperty, null);
        tip.Opacity = 0; // the next opening starts invisible again
    }

    // --- Control line (Start menu, pen button: a second launch with --toggle; tests) ---

    public const string PipeName = AppIdentity.PipeName;

    /// <summary>
    /// Commands: status · draw toggle · draw off · laser toggle · clear · undo · redo ·
    /// color &lt;1-5&gt; · snapshot · dot &lt;x&gt; &lt;y&gt; (a new home for the dot, Win32
    /// pixels; for tests) · board toggle (F5) · countdown &lt;1-600&gt; (F6's timer, seconds).
    /// Every reply carries the current state: {ok, drawing, tool, color, thickness, undo,
    /// redo, board, countdown} (undo / redo = steps available; countdown = its seconds, 0 = none).
    /// </summary>
    private object HandleControl(string command)
    {
        var overlay = _overlayWindow;
        if (overlay == null) return new { ok = false, error = "not started" };

        string[]? files = null;
        switch (command)
        {
            case "status": break;
            case "draw toggle": overlay.ToggleDrawOnOff(); break;
            case "draw off": overlay.SelectToolByNumber(0); break;
            case "laser toggle": overlay.ToggleLaser(); break;
            case "clear": overlay.ClearAllStrokes(); break;
            case "undo": overlay.Undo(); break;
            case "redo": overlay.Redo(); break;
            case "snapshot": files = overlay.RenderToolbarSnapshots(AppIdentity.LocalDir).ToArray(); break;
            case "board toggle": overlay.ToggleBoard(); break;
            case var c when c.StartsWith("countdown ") && int.TryParse(c[10..], out int secs) && secs is >= 1 and <= 600:
                overlay.StartCountdown(secs);
                break;
            case var c when c.StartsWith("color ") && int.TryParse(c[6..], out int n) && n is >= 1 and <= 5:
                overlay.SetColor(n);
                break;
            case var c when c.StartsWith("dot ") && c[4..].Split(' ') is [var xs, var ys]
                                                 && int.TryParse(xs, out int x) && int.TryParse(ys, out int y):
                files = new[] { overlay.MoveDotHome(x, y) };
                break;
            default: return new { ok = false, error = $"unknown command: {command}" };
        }
        if (command != "status") Log($"control: {command}");
        return new
        {
            ok = true,
            drawing = overlay.IsDrawMode,
            tool = overlay.CurrentTool.ToString(),
            color = overlay.ColorIndex,
            thickness = PenState.ThicknessNames[overlay.ThicknessIndex],
            undo = overlay.UndoCount,
            redo = overlay.RedoCount,
            board = overlay.IsBoardOn,
            countdown = overlay.CountdownRunning ? overlay.CountdownSeconds : 0,
            files
        };
    }

    private void SetupCrashRecovery()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log($"UNHANDLED EXCEPTION: {args.ExceptionObject}");
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Log($"DISPATCHER EXCEPTION: {args.Exception}");
            args.Handled = true;
        };
    }

    private void SetupTrayIcon()
    {
        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Quit", null, OnQuitClicked);

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateIcon(),
            Text = AppIdentity.Name,
            Visible = true,
            ContextMenuStrip = contextMenu
        };
    }

    private static Drawing.Icon CreateIcon()
    {
        // The app's own icon (the pen — the same one the Start menu shows)
        if (Environment.ProcessPath is { } exe)
        {
            try
            {
                var icon = Drawing.Icon.ExtractIcon(exe, 0, WinForms.SystemInformation.SmallIconSize.Width);
                if (icon != null) return icon;
            }
            catch { /* fall back to the drawn icon */ }
        }

        var bitmap = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            var color = Drawing.Color.FromArgb(70, 130, 220);

            // Draw a pen/pencil shape
            using var pen = new Drawing.Pen(color, 2.5f);
            g.DrawLine(pen, 6, 26, 26, 6);   // Diagonal line (pen body)
            g.DrawLine(pen, 4, 28, 8, 24);    // Pen tip

            // "D" label for Draw
            using var font = new Drawing.Font("Segoe UI", 7f, Drawing.FontStyle.Bold);
            using var brush = new Drawing.SolidBrush(color);
            g.DrawString("D", font, brush, 16, 16);
        }
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private void SetupKeyboardHook()
    {
        _keyboardHook = new KeyboardHookService();
        // Drawing = a tool is on (draw mode with no tool is only confetti borrowing the overlay)
        _keyboardHook.IsDrawing = () =>
            _overlayWindow is { IsDrawMode: true } o && o.CurrentTool != AnnotationTool.None;
        _keyboardHook.IsTyping = () => _overlayWindow?.IsTyping == true;

        // The hook only decides; the work is queued, never run inside Windows' hook call
        void Post(Action<OverlayWindow> work) =>
            Dispatcher.BeginInvoke(() => { if (_overlayWindow is { } o) work(o); });

        // Core tools
        _keyboardHook.DrawModeToggled += () => Post(o => o.ToggleDrawMode());
        _keyboardHook.ClearInk += () => Post(o => o.ClearAllStrokes());
        _keyboardHook.LaserToggled += () => Post(o => o.ToggleLaser());
        _keyboardHook.TimerToggled += () => Post(o => o.ToggleTimer());
        _keyboardHook.TimerVisibilityToggled += () => Post(o => o.ToggleTimerVisibility());
        _keyboardHook.CountdownRequested += s => Post(o => o.StartCountdown(s));
        _keyboardHook.BoardToggled += () => Post(o => o.ToggleBoard());

        // Shape tools
        _keyboardHook.ArrowToggled += () => Post(o => o.ToggleArrow());
        _keyboardHook.RectangleToggled += () => Post(o => o.ToggleRectangle());
        _keyboardHook.CircleToggled += () => Post(o => o.ToggleCircle());
        _keyboardHook.TextToggled += () => Post(o => o.ToggleText());

        // Color picker
        _keyboardHook.ColorChanged += idx => Post(o => o.SetColor(idx));

        // Ctrl+1-8 tool shortcuts, Ctrl+0 = mouse
        _keyboardHook.ToolShortcut += num => Post(o => o.SelectToolByNumber(num));

        // Ctrl+V = paste image from clipboard onto overlay
        _keyboardHook.ClipboardPaste += () => Post(o => o.PasteImageFromClipboard());

        // Ctrl+Z / Ctrl+Y (Ctrl+Shift+Z)
        _keyboardHook.UndoRequested += () => Post(o => o.Undo());
        _keyboardHook.RedoRequested += () => Post(o => o.Redo());

        // Esc = stop drawing. The hook runs on this (UI) thread, so Invoke answers at once —
        // and it must answer, because whether Esc is kept from the app underneath depends on it.
        _keyboardHook.EscapePressed += () => Dispatcher.Invoke(() => _overlayWindow?.HandleEscape() ?? false);
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _overlayWindow?.Close();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _control?.Dispose();
        _keyboardHook?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
