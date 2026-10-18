using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SmoothDraw.Native;

namespace SmoothDraw.Services;

/// <summary>
/// The cursor ring: a yellow ring that follows the mouse, with a ripple on each click (left =
/// the ring's colour, right = red). Moved here from SmoothZoom on 2026-10-18 (Omri: "the only
/// thing I want from it is the ring"), looking and behaving the same: an ordinary topmost
/// click-through window, so OBS's Display Capture records it, and it never takes a click from
/// the toolbar or the canvas. Never on by itself: Ctrl+Alt+H, the tray, the pipe.
/// </summary>
public sealed class CursorRingService : IDisposable
{
    private Window? _window;
    private Canvas? _canvas;
    private readonly DispatcherTimer _timer;
    private int _ticks;

    // The click ripple's mouse hook, only while the ring is on.
    // CRITICAL: the delegate is a field, so the GC never collects it under Windows' feet
    private readonly User32.LowLevelMouseProc _mouseProc;
    private IntPtr _mouseHook = IntPtr.Zero;

    public double RingSize { get; set; } = 70;
    public double RingThickness { get; set; } = 4;
    public Color RingColor { get; set; } = Color.FromArgb(220, 255, 230, 50); // yellow
    public Color RingFill { get; set; } = Color.FromArgb(48, 255, 230, 50);   // faint yellow
    public bool ClickRipple { get; set; } = true;

    // The ripple grows from the ring to RippleScale × the ring, fading out
    private const double RippleScale = 2.0;
    private const int RippleMs = 350;
    private static readonly Color RightClickColor = Color.FromArgb(230, 255, 80, 80); // red

    public event Action? Changed;

    public CursorRingService()
    {
        _mouseProc = MouseHookCallback;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
    }

    public bool IsActive => _window != null;

    public void Toggle() => SetActive(!IsActive);

    public void SetActive(bool on)
    {
        if (on == IsActive) return;
        if (on) Activate(); else Deactivate();
        App.Log($"Ring {(on ? "on" : "off")}");
        Changed?.Invoke();
    }

    private void Activate()
    {
        // Room for the glow and the click ripple
        const double glow = 8;
        double size = RingSize * RippleScale + RingThickness * 2 + glow * 2 + 4;

        _window = new Window
        {
            Title = "SmoothDraw Ring",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false, // the app in front keeps the keyboard
            Width = size,
            Height = size,
            ResizeMode = ResizeMode.NoResize,
        };
        _canvas = new Canvas { Width = size, Height = size, IsHitTestVisible = false };

        var ring = new Ellipse
        {
            Width = RingSize,
            Height = RingSize,
            Stroke = new SolidColorBrush(RingColor),
            StrokeThickness = RingThickness,
            Fill = new SolidColorBrush(RingFill),
            Effect = new DropShadowEffect { Color = RingColor, BlurRadius = glow, ShadowDepth = 0, Opacity = 0.6 },
        };
        RenderOptions.SetEdgeMode(ring, EdgeMode.Unspecified);
        double offset = (size - RingSize) / 2;
        Canvas.SetLeft(ring, offset);
        Canvas.SetTop(ring, offset);
        _canvas.Children.Add(ring);

        _window.Content = _canvas;
        _window.SourceInitialized += (_, _) =>
        {
            // Click-through and never activated, before it is first shown
            var hwnd = new WindowInteropHelper(_window).Handle;
            int style = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
            User32.SetWindowLong(hwnd, User32.GWL_EXSTYLE,
                style | User32.WS_EX_TRANSPARENT | User32.WS_EX_TOOLWINDOW | User32.WS_EX_NOACTIVATE);
        };
        _window.Show();
        OnTick(null, EventArgs.Empty); // in place before the first frame
        _timer.Start();

        if (ClickRipple)
        {
            _mouseHook = User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseProc, Kernel32.GetModuleHandle(null), 0);
            if (_mouseHook == IntPtr.Zero)
                App.Log($"Ring: mouse hook FAILED, error {Marshal.GetLastWin32Error()} (no click ripple)");
        }
    }

    private void Deactivate()
    {
        _timer.Stop();
        if (_mouseHook != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        _window?.Close();
        _window = null;
        _canvas = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_window == null) return;

        User32.GetCursorPos(out var cursor);
        var source = PresentationSource.FromVisual(_window);
        double sx = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double sy = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
        _window.Left = cursor.X * sx - _window.Width / 2;
        _window.Top = cursor.Y * sy - _window.Height / 2;

        // Once a second back on top: the board and the toolbar are topmost windows too
        if (++_ticks % 60 == 0 && new WindowInteropHelper(_window).Handle is var hwnd && hwnd != IntPtr.Zero)
            OverlayService.RaiseToTop(hwnd);
    }

    // Runs on the UI thread inside Windows' hook call: it only queues the ripple
    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is User32.WM_LBUTTONDOWN or User32.WM_RBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
                // Real clicks only (SmoothZoom's rule: a click a program re-sends ripples once)
                if ((data.flags & User32.LLMHF_INJECTED) == 0)
                {
                    bool right = msg == User32.WM_RBUTTONDOWN;
                    _canvas?.Dispatcher.BeginInvoke(() => ShowClick(right));
                }
            }
        }
        return User32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    /// <summary>A short ripple out of the ring. UI thread.</summary>
    private void ShowClick(bool rightButton)
    {
        if (_canvas == null) return;

        var ripple = new Ellipse
        {
            Width = RingSize,
            Height = RingSize,
            Stroke = new SolidColorBrush(rightButton ? RightClickColor : RingColor),
            StrokeThickness = RingThickness,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        var scale = new ScaleTransform(1, 1);
        ripple.RenderTransform = scale;
        double offset = (_canvas.Width - RingSize) / 2;
        Canvas.SetLeft(ripple, offset);
        Canvas.SetTop(ripple, offset);
        _canvas.Children.Add(ripple);

        var duration = TimeSpan.FromMilliseconds(RippleMs);
        var grow = new DoubleAnimation(1, RippleScale, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        var fade = new DoubleAnimation(1, 0, duration);
        var canvas = _canvas;
        fade.Completed += (_, _) => canvas.Children.Remove(ripple);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        ripple.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    public void Dispose() => Deactivate();
}
