using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SmoothDraw.Views;

/// <summary>
/// The thinking timer (F6 = 10 s, Shift+F6 = 30 s, Ctrl+F6 = 60 s; the pad's ⏳ keys): a ring
/// that empties around the seconds left, on the real screen, so a recording and a Zoom share
/// both show it. It fades in, its last three seconds turn red and pop, and at zero it flashes
/// and fades away by itself. Purely visual: anything the PC plays would land in the recording.
/// Never takes input (IsHitTestVisible false), so it can sit over the board or over an app.
/// </summary>
public sealed class CountdownHud : Grid
{
    // Colours: amber while there is time, red in the last seconds (the clock's red: the OBS
    // remote's REC #E5383B), on a dark disc that reads over any screen and over the board
    private static readonly Color Amber = Color.FromRgb(0xFF, 0xC8, 0x3D);
    private static readonly Color Red = Color.FromRgb(0xE5, 0x38, 0x3B);
    private const int RedFrom = 3;               // seconds left when the ring turns red
    private const double FadeInMs = 220, FadeOutMs = 420, CancelMs = 220;

    private readonly Path _arc = new() { StrokeThickness = 1, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Ellipse _track = new();
    private readonly TextBlock _number = new()
    {
        FontFamily = new FontFamily("Segoe UI"),
        FontWeight = FontWeights.Bold,
        Foreground = Brushes.White,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center
    };
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly ScaleTransform _numberScale = new(1, 1);
    private readonly SolidColorBrush _ringBrush = new(Amber);

    private DateTime _start;
    private TimeSpan _total;
    private int _shown = -1;
    private bool _ticking;

    /// <summary>Counting down, or flashing at the end (a press then starts a new one).</summary>
    public bool IsRunning { get; private set; }
    /// <summary>The seconds this countdown was started with (0 when none).</summary>
    public int Seconds { get; private set; }
    /// <summary>It stopped running: done (after its flash) or stopped early.</summary>
    public event Action? Ended;

    public CountdownHud(double size)
    {
        Width = Height = size;
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        Opacity = 0;
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _scale;

        double ring = size * 0.085;
        var disc = new Ellipse
        {
            Fill = new SolidColorBrush(Color.FromArgb(0xE0, 0x14, 0x18, 0x1F)),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = size * 0.12, ShadowDepth = size * 0.02, Opacity = 0.45
            }
        };
        _track.Stroke = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));
        _track.StrokeThickness = ring;
        _track.Margin = new Thickness(ring * 0.9);
        _arc.Stroke = _ringBrush;
        _arc.StrokeThickness = ring;
        _number.FontSize = size * 0.4;
        _number.RenderTransformOrigin = new Point(0.5, 0.5);
        _number.RenderTransform = _numberScale;
        // Hebrew pages around it never flip the digits
        _number.FlowDirection = System.Windows.FlowDirection.LeftToRight;

        Children.Add(disc);
        Children.Add(_track);
        Children.Add(_arc);
        Children.Add(_number);
    }

    public void Start(int seconds)
    {
        StopAnimations();
        Seconds = seconds;
        _total = TimeSpan.FromSeconds(seconds);
        _start = DateTime.UtcNow;
        _shown = -1;
        IsRunning = true;
        _ringBrush.Color = Amber;
        Visibility = Visibility.Visible;
        Draw(1.0, seconds);

        Fade(Opacity, 1, FadeInMs);
        var grow = new DoubleAnimation(0.85, 1, TimeSpan.FromMilliseconds(FadeInMs * 1.4))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
        };
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);

        if (!_ticking)
        {
            CompositionTarget.Rendering += OnFrame;
            _ticking = true;
        }
    }

    /// <summary>Stopped early (the same key again): a quick fade, no flash.</summary>
    public void Cancel()
    {
        if (Visibility != Visibility.Visible) return;
        StopTicking();
        IsRunning = false;
        Seconds = 0;
        Hide(CancelMs);
        Ended?.Invoke();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var left = _total - (DateTime.UtcNow - _start);
        if (left <= TimeSpan.Zero)
        {
            StopTicking();
            Draw(0, 0);
            Finish();
            return;
        }
        Draw(left.TotalMilliseconds / _total.TotalMilliseconds, (int)Math.Ceiling(left.TotalSeconds));
    }

    /// <summary>fraction = how much of the ring is left (1 = full); secs = the number shown.</summary>
    private void Draw(double fraction, int secs)
    {
        _arc.Data = ArcGeometry(fraction);
        if (secs == _shown) return;
        _shown = secs;
        _number.Text = secs.ToString();
        // One more digit (60) gets a little less room, so it never touches the ring
        _number.FontSize = Width * (secs >= 10 ? 0.36 : 0.42);
        if (secs <= RedFrom)
        {
            _ringBrush.Color = Red;
            // Each of the last seconds pops, so the end is seen coming
            if (secs > 0)
            {
                var pop = new DoubleAnimation(1.28, 1, TimeSpan.FromMilliseconds(320))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                _numberScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                _numberScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            }
        }
    }

    /// <summary>Zero: two quick flashes of the whole disc, then it fades away.</summary>
    private void Finish()
    {
        App.Log($"Countdown {Seconds}s: done");
        var flash = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700) };
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1.14, KeyTime.FromPercent(0.18)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.4)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1.14, KeyTime.FromPercent(0.62)));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.84)));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, flash);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, flash);

        var blink = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(700) };
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.45, KeyTime.FromPercent(0.3)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromPercent(0.5)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.45, KeyTime.FromPercent(0.75)));
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromPercent(0.95)));
        blink.Completed += (_, _) =>
        {
            // A new countdown started during the flash keeps the HUD
            if (_ticking) return;
            IsRunning = false;
            Seconds = 0;
            Hide(FadeOutMs);
            Ended?.Invoke();
        };
        BeginAnimation(OpacityProperty, blink);
    }

    private void Hide(double ms)
    {
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(ms));
        fade.Completed += (_, _) =>
        {
            if (IsRunning) return; // started again meanwhile
            Visibility = Visibility.Collapsed;
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void Fade(double from, double to, double ms) =>
        BeginAnimation(OpacityProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)));

    private void StopAnimations()
    {
        // Hold the current values, then let go of the animations
        double opacity = Opacity;
        BeginAnimation(OpacityProperty, null);
        Opacity = opacity;
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _numberScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _numberScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
    }

    private void StopTicking()
    {
        if (!_ticking) return;
        CompositionTarget.Rendering -= OnFrame;
        _ticking = false;
    }

    /// <summary>The part of the ring that is left: from 12 o'clock, clockwise. It shrinks back
    /// towards 12 as time runs out.</summary>
    private Geometry ArcGeometry(double fraction)
    {
        double ring = _arc.StrokeThickness;
        double r = Width / 2 - ring * 0.9 - ring / 2;
        var c = new Point(Width / 2, Height / 2);
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction >= 0.9999) return new EllipseGeometry(c, r, r);
        if (fraction <= 0.0001) return Geometry.Empty;

        double angle = fraction * 2 * Math.PI;
        var from = new Point(c.X, c.Y - r);
        var to = new Point(c.X + r * Math.Sin(angle), c.Y - r * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = from, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment(to, new System.Windows.Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }
}
