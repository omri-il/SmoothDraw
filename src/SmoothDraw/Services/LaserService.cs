using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Threading;

namespace SmoothDraw.Services;

public class LaserService
{
    private readonly InkCanvas _inkCanvas;
    private readonly DispatcherTimer _fadeTimer;
    private readonly List<(Stroke Stroke, DateTime Created)> _laserStrokes = new();
    private readonly int _fadeMs;
    private Color _baseColor;

    public LaserService(InkCanvas inkCanvas, int fadeMs, Color baseColor, double coreSize)
    {
        _inkCanvas = inkCanvas;
        _fadeMs = fadeMs;
        _baseColor = baseColor;

        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _fadeTimer.Tick += OnFadeTick;
    }

    public void Start() => _fadeTimer.Start();

    public void Stop()
    {
        _fadeTimer.Stop();
        RemoveAllLaserStrokes();
    }

    public void SetBaseColor(Color color) => _baseColor = color;

    public void RegisterStroke(Stroke stroke)
    {
        // Single-stroke approach: just track for fading, no extra strokes
        _laserStrokes.Add((stroke, DateTime.UtcNow));
    }

    /// <summary>Laser strokes share the ink canvas with the real ink: undo, clear and the
    /// eraser leave these alone.</summary>
    public bool IsLaserStroke(Stroke stroke) => _laserStrokes.Any(l => l.Stroke == stroke);

    public void ClearTracking() => _laserStrokes.Clear();

    private void OnFadeTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        for (int i = _laserStrokes.Count - 1; i >= 0; i--)
        {
            var (stroke, created) = _laserStrokes[i];
            double ageMs = (now - created).TotalMilliseconds;

            if (ageMs >= _fadeMs)
            {
                // Only if it is still there: something else (a clear) may have taken it,
                // and a failed remove would repeat on every tick
                _laserStrokes.RemoveAt(i);
                if (_inkCanvas.Strokes.Contains(stroke)) _inkCanvas.Strokes.Remove(stroke);
            }
            else
            {
                double progress = ageMs / _fadeMs;
                byte alpha = (byte)(200 * (1.0 - progress));
                stroke.DrawingAttributes.Color = Color.FromArgb(alpha, _baseColor.R, _baseColor.G, _baseColor.B);
            }
        }
    }

    private void RemoveAllLaserStrokes()
    {
        foreach (var (stroke, _) in _laserStrokes)
            if (_inkCanvas.Strokes.Contains(stroke)) _inkCanvas.Strokes.Remove(stroke);
        _laserStrokes.Clear();
    }
}
