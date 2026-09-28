namespace SmoothDraw.Models;

/// <summary>
/// What you picked last, remembered across restarts in %APPDATA%\SmoothDraw\pen.json.
/// Kept apart from settings.json on purpose: saving that file pins every value in it,
/// so it is only ever written by hand (CLAUDE.md → Settings).
/// </summary>
public class PenState
{
    public static readonly string[] ThicknessNames = { "Thin", "Medium", "Thick" };
    /// <summary>× PenSize, for pen strokes and shape outlines. Medium is today's PenSize,
    /// so a PenSize pinned in settings.json keeps meaning what it meant.</summary>
    public static readonly double[] ThicknessScale = { 0.6, 1.0, 1.8 };

    public static readonly string[] TextSizeNames = { "Small", "Medium", "Large", "XL" };
    public static readonly double[] TextSizes = { 24, 32, 48, 72 };

    /// <summary>1-5, InkPalette.</summary>
    public int Color { get; set; } = 1;
    /// <summary>0-2: Thin, Medium, Thick.</summary>
    public int Thickness { get; set; } = 1;
    /// <summary>0-3: Small, Medium, Large, XL.</summary>
    public int TextSize { get; set; } = 1;

    public PenState Normalized() => new()
    {
        Color = Math.Clamp(Color, 1, InkPalette.Colors.Length),
        Thickness = Math.Clamp(Thickness, 0, ThicknessScale.Length - 1),
        TextSize = Math.Clamp(TextSize, 0, TextSizes.Length - 1)
    };
}
