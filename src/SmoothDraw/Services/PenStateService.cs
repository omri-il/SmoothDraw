using System.IO;
using System.Text.Json;
using SmoothDraw.Models;

namespace SmoothDraw.Services;

public static class PenStateService
{
    private static readonly string PenFile = Path.Combine(AppIdentity.SettingsDir, "pen.json");

    /// <summary>The remembered pen, or — the first time — the colour settings.json's
    /// PenColor names (red by default), medium thickness, medium text.</summary>
    public static PenState Load(AnnotationSettings settings)
    {
        try
        {
            if (File.Exists(PenFile))
                return (JsonSerializer.Deserialize<PenState>(File.ReadAllText(PenFile)) ?? new PenState()).Normalized();
        }
        catch (Exception ex)
        {
            App.Log($"pen.json unreadable, starting fresh: {ex.Message}");
        }

        var first = new PenState();
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(settings.PenColor);
            int i = Array.FindIndex(InkPalette.Colors, p => p.R == c.R && p.G == c.G && p.B == c.B);
            if (i >= 0) first.Color = i + 1;
        }
        catch { /* not a colour: red */ }
        return first;
    }

    public static void Save(PenState state)
    {
        try
        {
            Directory.CreateDirectory(AppIdentity.SettingsDir);
            File.WriteAllText(PenFile, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            App.Log($"Could not save pen.json: {ex.Message}");
        }
    }
}
