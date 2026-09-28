namespace SmoothDraw.Models;

/// <summary>The five ink colours (1-5: the toolbar's swatches and Ctrl+Alt+1-5).</summary>
public static class InkPalette
{
    public static readonly Color[] Colors =
    {
        Color.FromRgb(255, 0, 0),     // 1: Red
        Color.FromRgb(50, 120, 255),  // 2: Blue
        Color.FromRgb(0, 200, 80),    // 3: Green
        Color.FromRgb(255, 255, 255), // 4: White
        Color.FromRgb(255, 230, 0)    // 5: Yellow
    };

    public static readonly string[] Names = { "Red", "Blue", "Green", "White", "Yellow" };

    public static Color Get(int index) => Colors[Math.Clamp(index, 1, Colors.Length) - 1];

    /// <summary>White and yellow vanish on the white toolbar: those get a grey ring.</summary>
    public static bool IsLight(int index) => index is 4 or 5;
}
