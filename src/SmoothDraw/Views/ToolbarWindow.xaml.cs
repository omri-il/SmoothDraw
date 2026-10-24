using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SmoothDraw.Models;
using SmoothDraw.Native;
using SmoothDraw.Services;

namespace SmoothDraw.Views;

/// <summary>
/// The toolbar, and the dot it shrinks to while you are not drawing.
///
/// Placement is done in Win32 pixels, with the window's own rect (GetWindowRect /
/// SetWindowPos) and the work area of the monitor it is on (MonitorFromRect, rcWork).
/// Those all answer in the same coordinates, whatever this process's DPI awareness:
/// physical pixels if it is per-monitor aware; if it is System-aware — which it is, see
/// CLAUDE.md → DPI — pixels at the system scale, with a screen of other scaling
/// stretched by Windows. SystemParameters.WorkArea is the main screen only, and WPF's
/// Left/Top are DIPs, so neither is used here.
///
/// The dot and the bar share one place. The bar opens at the dot's height (vertical
/// centres equal) and grows toward the middle of the screen: from a dot in the right half
/// its right edge sits on the dot's right edge, otherwise their left edges line up.
/// Dragging the bar moves the dot's home by the same rule in reverse.
/// </summary>
public partial class ToolbarWindow : Window
{
    // The dot's circle, and the transparent room around the dot and the bar for their
    // shadow (the Margin="10" in the XAML). The same on both, so lining up the two WINDOWS
    // lines up what you see.
    private const double DotSize = 42;
    private const double ShadowRoom = 10;

    private IntPtr _hwnd;
    private IntPtr Hwnd => _hwnd != IntPtr.Zero ? _hwnd : (_hwnd = new WindowInteropHelper(this).Handle);
    private readonly Button[] _colorButtons;
    private readonly Dictionary<AnnotationTool, Button> _toolButtons;
    private bool _isCollapsed = true;
    // The dot's home: its window rect in Win32 pixels
    private User32.RECT? _dotRect;

    public event Action<AnnotationTool>? ToolSelected;
    public event Action<int>? ColorSelected;
    public event Action? ClearRequested;
    public event Action? TextSizeCycled;
    public event Action? ThicknessCycled;
    public event Action? CloseRequested;
    public event Action? ConfettiRequested;
    public event Action? UndoRequested;
    public event Action? RedoRequested;
    public event Action<int>? PageRequested; // -1 back, +1 forward (the board's pages)
    public event Action<int>? CountdownRequested; // the thinking timer, its seconds (the hourglass)

    public ToolbarWindow()
    {
        InitializeComponent();

        _colorButtons = new[] { Color1, Color2, Color3, Color4, Color5 };
        _toolButtons = new Dictionary<AnnotationTool, Button>
        {
            { AnnotationTool.None, BtnMouse },
            { AnnotationTool.Select, BtnSelect },
            { AnnotationTool.Pen, BtnPen },
            { AnnotationTool.Highlighter, BtnHighlighter },
            { AnnotationTool.Eraser, BtnEraser },
            { AnnotationTool.Laser, BtnLaser },
            { AnnotationTool.Arrow, BtnArrow },
            { AnnotationTool.Rectangle, BtnRect },
            { AnnotationTool.Circle, BtnCircle },
            { AnnotationTool.Text, BtnText },
        };

        // The hourglass's choice closes with the bar
        IsVisibleChanged += (_, _) => { if (!IsVisible) SetCountdownChoiceOpen(false); };

        SourceInitialized += (_, _) =>
        {
            OverlayService.HideFromAltTab(Hwnd);
            // It floats over the recorded screen: you see the toolbar and the dot, videos don't
            OverlayService.HideFromCapture(Hwnd, "Toolbar");
        };

        // First show: the dot goes to its home, then becomes visible. (If drawing was
        // switched on before this, Expand is placing the bar and shows it itself.) With
        // SizeToContent, WPF raises Loaded during Show's first layout, BEFORE
        // SourceInitialized — hence Hwnd rather than a handle saved there.
        Loaded += (_, _) =>
        {
            // Created at the dot's size, the window comes out 136 px wide: Windows' minimum
            // window width applies while it is being created. A second size-to-content
            // pass, after creation, is not held to it.
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.WidthAndHeight;
            UpdateLayout();
            if (!_isCollapsed) return;
            PlaceDot();
            Opacity = 1;
        };

        SetActiveColor(1);
        SetActiveTool(AnnotationTool.None);
    }

    /// <summary>Force this window above the overlay using Win32 z-order.</summary>
    public void RaiseAboveOverlay()
    {
        if (Hwnd != IntPtr.Zero)
            OverlayService.RaiseToTop(Hwnd);
    }

    /// <summary>What you see — the bar or the dot, without the shadow room — in Win32
    /// pixels, padded for hover detection.</summary>
    public Rect GetScreenBounds()
    {
        const double pad = 12;
        return VisibleScreenRect() is Rect r ? new Rect(r.X - pad, r.Y - pad, r.Width + pad * 2, r.Height + pad * 2) : Rect.Empty;
    }

    /// <summary>The bar or the dot in Win32 screen pixels, or null before it is shown.</summary>
    public Rect? VisibleScreenRect()
    {
        FrameworkElement part = _isCollapsed ? MinimalButton : MainContent;
        if (PresentationSource.FromVisual(part) == null) return null;
        return new Rect(part.PointToScreen(new Point(0, 0)), part.PointToScreen(new Point(part.ActualWidth, part.ActualHeight)));
    }

    // --- Collapse / Expand ---

    public bool IsCollapsed => _isCollapsed;

    /// <summary>Bar → dot: the dot shrinks back into its home.</summary>
    public void CollapseToMinimal()
    {
        if (_isCollapsed) return;
        _isCollapsed = true;
        Opacity = 0;
        MainContent.Visibility = Visibility.Collapsed;
        MinimalButton.Visibility = Visibility.Visible;
        // After layout has shrunk the window
        Dispatcher.BeginInvoke(() =>
        {
            if (!_isCollapsed) return;
            PlaceDot();
            Opacity = 1;
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Dot → bar, opened where the dot is.</summary>
    public void ExpandFromMinimal()
    {
        if (!_isCollapsed) return;
        _isCollapsed = false;
        var dot = _dotRect ??= DotHome();
        Opacity = 0;
        MinimalButton.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;
        // After layout has grown the window: only then is the bar's size known
        Dispatcher.BeginInvoke(() =>
        {
            if (_isCollapsed) return;
            PlaceBarAt(dot);
            Opacity = 1;
        }, DispatcherPriority.Loaded);
    }

    private void PlaceDot()
    {
        var dot = _dotRect ??= DotHome();
        MoveWindowTo(dot.Left, dot.Top);
        App.Log($"Toolbar closed: dot at {dot}");
    }

    /// <summary>The pipe's "dot X Y" (tests): a new home for the dot, top-left in Win32
    /// pixels, kept inside that screen's work area. Returns where it ended up.</summary>
    public User32.RECT MoveDotHome(int x, int y)
    {
        var probe = new User32.RECT(x, y, x + 1, y + 1);
        var monitor = MonitorOf(probe);
        int size = DotWindowPx(monitor);
        var (left, top) = ClampInto(WorkArea(monitor), x, y, size, size);
        _dotRect = new User32.RECT(left, top, left + size, top + size);
        SaveDotPosition(_dotRect.Value);
        App.Log($"Dot home set to {_dotRect}");
        if (_isCollapsed && IsLoaded) PlaceDot();
        return _dotRect.Value;
    }

    private void PlaceBarAt(User32.RECT dot)
    {
        var bar = WindowRect();
        int w = bar.Width, h = bar.Height;
        var work = WorkAreaOf(dot);
        bool rightHalf = (dot.Left + dot.Right) / 2 > (work.Left + work.Right) / 2;
        int left = rightHalf ? dot.Right - w : dot.Left;
        int top = (dot.Top + dot.Bottom) / 2 - h / 2;
        (left, top) = ClampInto(work, left, top, w, h);
        MoveWindowTo(left, top);
        App.Log($"Toolbar open: dot {dot} -> bar {WindowRect()} ({(rightHalf ? "right" : "left")} edges), work {work}");
    }

    /// <summary>The dot's home for a bar at <paramref name="bar"/> — the opening rule in reverse.</summary>
    private static User32.RECT DotForBar(User32.RECT bar)
    {
        var work = WorkAreaOf(bar);
        int size = DotWindowPx(MonitorOf(bar));
        bool rightHalf = (bar.Left + bar.Right) / 2 > (work.Left + work.Right) / 2;
        int left = rightHalf ? bar.Right - size : bar.Left;
        int top = (bar.Top + bar.Bottom) / 2 - size / 2;
        (left, top) = ClampInto(work, left, top, size, size);
        return new User32.RECT(left, top, left + size, top + size);
    }

    private void MoveWindowTo(int left, int top)
    {
        if (Hwnd == IntPtr.Zero) return;
        User32.SetWindowPos(Hwnd, IntPtr.Zero, left, top, 0, 0,
            User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);
        // Moving onto a screen with other scaling makes WPF resize the window, which can
        // shift it: put the top-left back where it was asked to be
        var now = WindowRect();
        if (now.Left != left || now.Top != top)
            User32.SetWindowPos(Hwnd, IntPtr.Zero, left, top, 0, 0,
                User32.SWP_NOSIZE | User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);
    }

    private void KeepInsideWorkArea()
    {
        if (!IsVisible || _isCollapsed || Hwnd == IntPtr.Zero) return;
        var r = WindowRect();
        var (left, top) = ClampInto(WorkAreaOf(r), r.Left, r.Top, r.Width, r.Height);
        if (left != r.Left || top != r.Top) MoveWindowTo(left, top);
    }

    private User32.RECT WindowRect()
    {
        User32.GetWindowRect(Hwnd, out var r);
        return r;
    }

    // --- Monitors (Win32 pixels) ---

    private static IntPtr MonitorOf(User32.RECT r) => User32.MonitorFromRect(ref r, User32.MONITOR_DEFAULTTONEAREST);

    private static User32.RECT WorkAreaOf(User32.RECT r) => WorkArea(MonitorOf(r));

    private static User32.RECT WorkArea(IntPtr monitor)
    {
        var mi = new User32.MONITORINFO { cbSize = Marshal.SizeOf<User32.MONITORINFO>() };
        return User32.GetMonitorInfo(monitor, ref mi) ? mi.rcWork : new User32.RECT(0, 0, 1920, 1080);
    }

    private static double ScaleOf(IntPtr monitor) =>
        User32.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1.0;

    /// <summary>The dot's window (circle + shadow room) on that monitor, in pixels.</summary>
    private static int DotWindowPx(IntPtr monitor) =>
        (int)Math.Round((DotSize + 2 * ShadowRoom) * ScaleOf(monitor));

    private static (int Left, int Top) ClampInto(User32.RECT work, int left, int top, int w, int h) =>
        (Math.Max(work.Left, Math.Min(left, work.Right - w)), Math.Max(work.Top, Math.Min(top, work.Bottom - h)));

    // --- The dot's home, kept across restarts (Win32 pixels) ---

    private static readonly string DotFile = Path.Combine(AppIdentity.SettingsDir, "dot-position.json");

    private sealed record DotPosition(double X, double Y, string? Units);

    /// <summary>Where the dot lives: the saved place if it is still on a screen, otherwise
    /// top-centre of the main screen.</summary>
    private static User32.RECT DotHome()
    {
        if (LoadDotPosition() is { } saved) return saved;
        var primary = User32.MonitorFromPoint(new User32.POINT(), User32.MONITOR_DEFAULTTOPRIMARY);
        var work = WorkArea(primary);
        int size = DotWindowPx(primary);
        int left = work.Left + (work.Width - size) / 2;
        return new User32.RECT(left, work.Top, left + size, work.Top + size);
    }

    private static User32.RECT? LoadDotPosition()
    {
        try
        {
            if (!File.Exists(DotFile)) return null;
            var p = JsonSerializer.Deserialize<DotPosition>(File.ReadAllText(DotFile));
            if (p == null) return null;
            int x = (int)Math.Round(p.X), y = (int)Math.Round(p.Y);
            bool old = p.Units != "px";
            if (old)
            {
                // SmoothAnnotate's file: WPF units of the main screen, for a window with no
                // shadow room. Read once, then saved in pixels.
                double s = ScaleOf(User32.MonitorFromPoint(new User32.POINT(), User32.MONITOR_DEFAULTTOPRIMARY));
                x = (int)Math.Round((p.X - ShadowRoom) * s);
                y = (int)Math.Round((p.Y - ShadowRoom) * s);
            }
            var probe = new User32.RECT(x, y, x + 1, y + 1);
            var monitor = User32.MonitorFromRect(ref probe, User32.MONITOR_DEFAULTTONULL);
            if (monitor == IntPtr.Zero)
            {
                App.Log($"Dot position ({x},{y}) is on no screen any more: back to top-centre");
                return null;
            }
            int size = DotWindowPx(monitor);
            var rect = new User32.RECT(x, y, x + size, y + size);
            if (old)
            {
                App.Log($"Dot position converted from WPF units ({p.X},{p.Y}) to pixels {rect}");
                SaveDotPosition(rect);
            }
            return rect;
        }
        catch (Exception ex)
        {
            App.Log($"Dot position unreadable ({ex.Message}): back to top-centre");
            return null;
        }
    }

    private static void SaveDotPosition(User32.RECT r)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DotFile)!);
            File.WriteAllText(DotFile, JsonSerializer.Serialize(new DotPosition(r.Left, r.Top, "px")));
        }
        catch (Exception ex)
        {
            App.Log($"Could not save the dot's position: {ex.Message}");
        }
    }

    // --- Event handlers ---

    /// <summary>The bar's background and grip: drag it to move it (and the dot's home with it).</summary>
    private void Bar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 1) return;
        var before = WindowRect();
        try
        {
            DragMove(); // returns when the button is released
        }
        catch (InvalidOperationException)
        {
            return; // the button was already up
        }
        var after = WindowRect();
        if (Math.Abs(after.Left - before.Left) + Math.Abs(after.Top - before.Top) <= 4) return;
        _dotRect = DotForBar(after);
        SaveDotPosition(_dotRect.Value);
        App.Log($"Toolbar moved: bar {after} -> dot home {_dotRect}");
    }

    /// <summary>The dot: drag it to move it, click it to draw.</summary>
    private void MinimalButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var before = WindowRect();
        try
        {
            DragMove(); // returns when the button is released
        }
        catch (InvalidOperationException)
        {
            // the button was already up (a quick tap) — a click
        }
        var after = WindowRect();
        if (Math.Abs(after.Left - before.Left) + Math.Abs(after.Top - before.Top) > 4)
        {
            _dotRect = after;
            SaveDotPosition(after);
            App.Log($"Dot moved to {after}");
            return;
        }
        ExpandFromMinimal();
        ToolSelected?.Invoke(AnnotationTool.Pen);
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && Enum.TryParse<AnnotationTool>(tagStr, out var tool))
            ToolSelected?.Invoke(tool);
    }

    private void MouseButton_Click(object sender, RoutedEventArgs e) => ToolSelected?.Invoke(AnnotationTool.None);

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            ColorSelected?.Invoke(idx);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
    private void ConfettiButton_Click(object sender, RoutedEventArgs e) => ConfettiRequested?.Invoke();
    private void CountdownButton_Click(object sender, RoutedEventArgs e) =>
        SetCountdownChoiceOpen(CountdownGroup.Visibility != Visibility.Visible);
    private void CountdownChoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int seconds))
            CountdownRequested?.Invoke(seconds);
    }
    private void TextSizeButton_Click(object sender, RoutedEventArgs e) => TextSizeCycled?.Invoke();
    private void ThicknessButton_Click(object sender, RoutedEventArgs e) => ThicknessCycled?.Invoke();
    private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearRequested?.Invoke();
    private void UndoButton_Click(object sender, RoutedEventArgs e) => UndoRequested?.Invoke();
    private void RedoButton_Click(object sender, RoutedEventArgs e) => RedoRequested?.Invoke();
    private void PagePrev_Click(object sender, RoutedEventArgs e) => PageRequested?.Invoke(-1);
    private void PageNext_Click(object sender, RoutedEventArgs e) => PageRequested?.Invoke(+1);

    // --- Visual state ---

    public void SetActiveTool(AnnotationTool tool)
    {
        foreach (var (t, btn) in _toolButtons)
            ToolbarProps.SetIsActive(btn, t == tool);

        // The options slot: thickness for what draws lines, text size for text
        bool lines = tool is AnnotationTool.Pen or AnnotationTool.Arrow or AnnotationTool.Rectangle or AnnotationTool.Circle;
        BtnThickness.Visibility = lines ? Visibility.Visible : Visibility.Hidden;
        BtnTextSize.Visibility = tool == AnnotationTool.Text ? Visibility.Visible : Visibility.Hidden;
    }

    /// <summary>The swatch, the pen button's badge and the dot's badge all show the ink colour.</summary>
    public void SetActiveColor(int index)
    {
        for (int i = 0; i < _colorButtons.Length; i++)
            ToolbarProps.SetIsActive(_colorButtons[i], i + 1 == index);

        var fill = new SolidColorBrush(InkPalette.Get(index));
        // White keeps the badge apart from the pencil; a light colour needs grey to show on white
        var ring = InkPalette.IsLight(index) ? (System.Windows.Media.Brush)FindResource("Edge") : Brushes.White;
        foreach (var badge in new[] { PenBadge, DotBadge })
        {
            badge.Fill = fill;
            badge.Stroke = ring;
        }
    }

    public void SetThickness(int index)
    {
        ThicknessLine.StrokeThickness = index switch { 0 => 1.5, 2 => 5, _ => 3 };
        BtnThickness.ToolTip = $"Thickness: {PenState.ThicknessNames[index]} · click to change";
    }

    public void SetTextSize(int index)
    {
        TextSizeGlyph.FontSize = index switch { 0 => 11, 2 => 17, 3 => 20, _ => 14 };
        BtnTextSize.ToolTip = $"Text size: {PenState.TextSizeNames[index]} · click to change";
    }

    /// <summary>The board's pages, "2/3", shown only while the board is up (page 0 = hidden).
    /// Back dims on the first page; forward never does: past the last page is a new one.</summary>
    public void SetPage(int page, int count)
    {
        bool on = page > 0;
        var want = on ? Visibility.Visible : Visibility.Collapsed;
        if (PageGroup.Visibility != want)
        {
            PageGroup.Visibility = want;
            // The bar grows to the right by the arrows: a bar flush with the screen's right
            // edge would hang off it, so it is pulled back in once laid out
            Dispatcher.BeginInvoke(KeepInsideWorkArea, DispatcherPriority.Loaded);
        }
        if (!on) return;
        PageText.Text = $"{page}/{count}";
        BtnPagePrev.IsEnabled = page > 1;
        BtnPageNext.ToolTip = page < count ? "Next page · PageDown / →" : "New page · PageDown / →";
    }

    /// <summary>The hourglass's 10 / 30 / 60 beside it, or not. The bar grows by them, so it is
    /// pulled back inside the screen once laid out, as for the page arrows.</summary>
    private void SetCountdownChoiceOpen(bool open)
    {
        var want = open ? Visibility.Visible : Visibility.Collapsed;
        ToolbarProps.SetIsActive(BtnCountdown, open);
        if (CountdownGroup.Visibility == want) return;
        CountdownGroup.Visibility = want;
        if (open) Dispatcher.BeginInvoke(KeepInsideWorkArea, DispatcherPriority.Loaded);
    }

    /// <summary>The thinking timer running with these seconds (0 = none): its chip is lit.</summary>
    public void SetCountdown(int seconds)
    {
        foreach (var chip in new[] { BtnCountdown10, BtnCountdown30, BtnCountdown60 })
            ToolbarProps.SetIsActive(chip, (string)chip.Tag == seconds.ToString());
    }

    public void SetUndoRedo(bool canUndo, bool canRedo)
    {
        BtnUndo.IsEnabled = canUndo;
        BtnRedo.IsEnabled = canRedo;
    }

    // --- Snapshots (the pipe's "snapshot"): the toolbar can't be screenshotted, since it
    // is hidden from capture, so it renders itself ---

    /// <summary>Writes toolbar-*.png to <paramref name="dir"/>: the live window as it is,
    /// and a set of states from a fresh copy that is never shown. Returns the paths.</summary>
    public List<string> RenderSnapshots(string dir)
    {
        Directory.CreateDirectory(dir);
        var files = new List<string>();
        if (PresentationSource.FromVisual(Root) != null)
            files.Add(SavePng(Root, Path.Combine(dir, "toolbar-live.png"), layout: false));

        var copy = new ToolbarWindow();
        try
        {
            void State(string name, bool bar, AnnotationTool tool, int color, int thickness = 1, int textSize = 1,
                bool undo = false, bool redo = false, int timer = -1)
            {
                copy.SetCountdownChoiceOpen(timer >= 0);
                copy.SetCountdown(Math.Max(timer, 0));
                copy.MainContent.Visibility = bar ? Visibility.Visible : Visibility.Collapsed;
                copy.MinimalButton.Visibility = bar ? Visibility.Collapsed : Visibility.Visible;
                copy.SetActiveTool(tool);
                copy.SetActiveColor(color);
                copy.SetThickness(thickness);
                copy.SetTextSize(textSize);
                copy.SetUndoRedo(undo, redo);
                files.Add(SavePng(copy.Root, Path.Combine(dir, $"toolbar-{name}.png"), layout: true));
            }

            State("dot-red", false, AnnotationTool.None, 1);
            State("dot-white", false, AnnotationTool.None, 4);
            State("bar-pen-red", true, AnnotationTool.Pen, 1, undo: true);
            State("bar-text-blue", true, AnnotationTool.Text, 2, textSize: 2, undo: true, redo: true);
            State("bar-eraser-yellow", true, AnnotationTool.Eraser, 5);
            State("bar-arrow-thick-white", true, AnnotationTool.Arrow, 4, thickness: 2, redo: true);
            State("bar-mouse-green-thin", true, AnnotationTool.None, 3, thickness: 0);
            State("bar-timer-30", true, AnnotationTool.Pen, 2, timer: 30);
        }
        finally
        {
            copy.Close();
        }
        return files;
    }

    /// <param name="layout">False for the live window: it is laid out already, and
    /// arranging it here would fight its own layout.</param>
    private static string SavePng(FrameworkElement root, string path, bool layout)
    {
        if (layout)
        {
            root.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            root.Arrange(new Rect(root.DesiredSize));
            root.UpdateLayout();
        }

        const double scale = 2; // crisp enough to judge a 20 px icon
        int w = (int)Math.Ceiling(root.ActualWidth * scale), h = (int)Math.Ceiling(root.ActualHeight * scale);
        var bare = new RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bare.Render(root);

        // On a light grey "desktop", so the white bar, its edge and its shadow show
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xDD, 0xE2, 0xE8)), null, new Rect(0, 0, w, h));
            dc.DrawImage(bare, new Rect(0, 0, w, h));
        }
        var final = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        final.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(final));
        using var fs = File.Create(path);
        encoder.Save(fs);
        return path;
    }
}
