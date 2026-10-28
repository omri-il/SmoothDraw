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
    private CursorRingService? _ring;
    private WinForms.ToolStripMenuItem? _ringMenuItem;
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

            _ring = CreateRing(_settings);

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

    /// <summary>`tool &lt;name&gt;`: the index is SelectToolByNumber's (and Ctrl+0-8's).</summary>
    private static readonly string[] ToolNames =
        { "none", "pen", "highlighter", "laser", "eraser", "arrow", "rectangle", "circle", "text" };

    /// <summary>
    /// Commands: status · draw toggle · draw off · laser toggle · clear · undo · redo ·
    /// color &lt;1-5&gt; · snapshot · dot &lt;x&gt; &lt;y&gt; (a new home for the dot, Win32
    /// pixels; for tests) · board toggle (F5) · page next · page prev (the board's pages) ·
    /// countdown &lt;1-600&gt; (F6's timer, seconds; the same length again stops it, also while
    /// paused) · countdown place &lt;pos&gt; (top-right, top-center, top-left, bottom-right,
    /// bottom-center, bottom-left, center: saved to countdown.json, a running timer moves) ·
    /// countdown pause · countdown resume · countdown stop (the OBS take paused / resumed /
    /// restarted; each does nothing when there is nothing to do) · tool &lt;name&gt; (none, pen, highlighter,
    /// laser, eraser, arrow, rectangle, circle, text: picks it and switches drawing on, as the
    /// toolbar does; none = the mouse. The OBS dashboard's remote, one button per tool) ·
    /// ring toggle · ring on · ring off (the cursor ring, Ctrl+Alt+H).
    /// Every reply carries the current state: {ok, drawing, tool, color, thickness, undo,
    /// redo, board, page, pages, countdown, countdown_paused, countdown_place, ring} (undo /
    /// redo = steps available; page = the board's page on show, 1-based, 0 = no board;
    /// countdown = its seconds, 0 = none; ring = the cursor ring on).
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
            case "page next": overlay.TurnPage(+1); break;
            case "page prev": overlay.TurnPage(-1); break;
            case "ring toggle": _ring?.Toggle(); break;
            case "ring on": _ring?.SetActive(true); break;
            case "ring off": _ring?.SetActive(false); break;
            case "countdown pause": overlay.PauseCountdown(); break;
            case "countdown resume": overlay.ResumeCountdown(); break;
            case "countdown stop": overlay.StopCountdown(); break;
            case var c when c.StartsWith("countdown place"):
                if (!overlay.SetCountdownPlace(c[15..]))
                    return new { ok = false, error = $"unknown place: {c[15..].Trim()} (top-right, top-center, top-left, bottom-right, bottom-center, bottom-left, center)" };
                break;
            case var c when c.StartsWith("countdown ") && int.TryParse(c[10..], out int secs) && secs is >= 1 and <= 600:
                overlay.StartCountdown(secs);
                break;
            case var c when c.StartsWith("tool ") && Array.IndexOf(ToolNames, c[5..]) is var t and >= 0:
                overlay.SelectToolByNumber(t);
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
            page = overlay.IsBoardOn ? overlay.BoardPage : 0,
            pages = overlay.BoardPageCount,
            countdown = overlay.CountdownRunning ? overlay.CountdownSeconds : 0,
            countdown_paused = overlay.CountdownPaused,
            countdown_place = overlay.CountdownPlace,
            ring = _ring?.IsActive ?? false,
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

    // --- The cursor ring (SmoothZoom's until 2026-10-18) ---

    /// <summary>Each value: this app's settings.json, else SmoothZoom's (the ring's old home, so
    /// a machine's own ring size comes along), else the default.</summary>
    private static CursorRingService CreateRing(AnnotationSettings s)
    {
        var ring = new CursorRingService();
        var old = ReadSmoothZoomSettings();
        double? OldNumber(string key) =>
            old?.TryGetProperty(key, out var v) == true && v.TryGetDouble(out var d) ? d : null;
        string? OldText(string key) =>
            old?.TryGetProperty(key, out var v) == true && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
        bool? OldBool(string key) =>
            old?.TryGetProperty(key, out var v) == true && v.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False ? v.GetBoolean() : null;
        Color? ParseColor(string? text)
        {
            try { return text == null ? null : (Color)ColorConverter.ConvertFromString(text); }
            catch { return null; }
        }

        ring.RingSize = s.RingSize ?? OldNumber("HighlightRingSize") ?? ring.RingSize;
        ring.RingThickness = s.RingThickness ?? OldNumber("HighlightThickness") ?? ring.RingThickness;
        ring.RingColor = ParseColor(s.RingColor) ?? ParseColor(OldText("HighlightColor")) ?? ring.RingColor;
        ring.RingFill = ParseColor(s.RingFill) ?? ParseColor(OldText("HighlightFill")) ?? ring.RingFill;
        ring.ClickRipple = s.ClickRipple ?? OldBool("ClickRipple") ?? ring.ClickRipple;
        Log($"Ring: size {ring.RingSize}, thickness {ring.RingThickness}, colour {ring.RingColor}, " +
            $"ripple {ring.ClickRipple}{(old != null ? " (unset values from SmoothZoom's settings.json)" : "")}");
        return ring;
    }

    private static System.Text.Json.JsonElement? ReadSmoothZoomSettings()
    {
        var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmoothZoom", "settings.json");
        try
        {
            if (!File.Exists(file)) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            Log($"Ring: could not read {file}: {ex.Message}");
            return null;
        }
    }

    private void SetupTrayIcon()
    {
        var contextMenu = new WinForms.ContextMenuStrip();
        _ringMenuItem = new WinForms.ToolStripMenuItem("Cursor ring (Ctrl+Alt+H)", null, (_, _) => _ring?.Toggle());
        contextMenu.Items.Add(_ringMenuItem);
        if (_ring != null) _ring.Changed += () => _ringMenuItem.Checked = _ring.IsActive;
        contextMenu.Items.Add(new WinForms.ToolStripSeparator());
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
        _keyboardHook.IsBoardOn = () => _overlayWindow?.IsBoardOn == true;

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
        _keyboardHook.RingToggled += () => Dispatcher.BeginInvoke(() => _ring?.Toggle());
        _keyboardHook.PageTurned += d => Post(o => o.TurnPage(d));

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
        _ring?.Dispose();
        _keyboardHook?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
