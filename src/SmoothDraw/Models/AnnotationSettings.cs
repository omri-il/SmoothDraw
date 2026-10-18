namespace SmoothDraw.Models;

public class AnnotationSettings
{
    public double PenSize { get; set; } = 5.0;
    public string PenColor { get; set; } = "#FF0000";
    public double HighlighterSize { get; set; } = 20.0;
    public string HighlighterColor { get; set; } = "#FFFF00";
    public double EraserSize { get; set; } = 20.0;
    public double LaserSize { get; set; } = 8.0;
    public string LaserColor { get; set; } = "#FF0000";
    public int LaserFadeMs { get; set; } = 1500;
    public double TimerFontSize { get; set; } = 28.0;
    // Not drawing: false = the toolbar shrinks to a small dot (click to draw, drag to move);
    // true = it goes away completely and only F9 / the Start menu / the pen bring it back.
    // Either way no recording shows it: the toolbar window is hidden from screen capture.
    public bool HideToolbarWhenIdle { get; set; } = false;
    // On a touch screen with a pen (the laptop): a finger draws, but not while the pen is
    // near the screen, so a resting palm leaves no marks. False = a finger always draws.
    public bool IgnoreTouchNearPen { get; set; } = true;
    // While drawing, clicks over this window go to it, not to the canvas — the OBS
    // dashboard's remote, so REC / stop stay pressable while you draw.
    public string PassThroughWindowTitle { get; set; } = "מרכז השליטה של OBS";
    // The thinking timer (F6): where on the screen and how big (DIP). Positions: top-right,
    // top-left, top-center, bottom-right, bottom-left, center. Bottom is above the taskbar:
    // OBS crops the taskbar out of every recording.
    public string CountdownPosition { get; set; } = "top-right";
    public double CountdownSize { get; set; } = 170;
    // The board (F5): the page that covers the screen. Khan Academy's near-black by default.
    public string BoardColor { get; set; } = "#121417";
    // The cursor ring (Ctrl+Alt+H, moved here from SmoothZoom 2026-10-18). Unset = SmoothZoom's
    // own value from its settings.json (HighlightRingSize, HighlightThickness, HighlightColor,
    // HighlightFill, ClickRipple), else 70 / 4 / #DCFFE632 / #30FFE632 / true — so the laptop's
    // ring 60 came along without a word here.
    public double? RingSize { get; set; }
    public double? RingThickness { get; set; }
    public string? RingColor { get; set; }
    public string? RingFill { get; set; }
    public bool? ClickRipple { get; set; }
}
