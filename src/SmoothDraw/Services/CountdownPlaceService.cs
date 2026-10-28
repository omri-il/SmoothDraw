using System.IO;
using System.Text.Json;
using SmoothDraw.Models;

namespace SmoothDraw.Services;

/// <summary>
/// Where the thinking timer sits, picked from the OBS remote's ⚙️ (the pipe's `countdown place
/// &lt;pos&gt;`, since 2026-10-28). Kept in countdown.json, apart from settings.json: that file is
/// only ever written by hand, because a saved one would pin every value (as pen.json).
/// </summary>
public static class CountdownPlaceService
{
    public static readonly string[] Places =
        { "top-right", "top-center", "top-left", "bottom-right", "bottom-center", "bottom-left", "center" };

    private static readonly string PlaceFile = Path.Combine(AppIdentity.SettingsDir, "countdown.json");

    private sealed class PlaceFileModel
    {
        [System.Text.Json.Serialization.JsonPropertyName("place")]
        public string? Place { get; set; }
    }

    /// <summary>countdown.json's place, else settings.json's CountdownPosition, else top-right.</summary>
    public static string Load(AnnotationSettings settings)
    {
        try
        {
            if (File.Exists(PlaceFile)
                && Normalize(JsonSerializer.Deserialize<PlaceFileModel>(File.ReadAllText(PlaceFile))?.Place) is { } saved)
                return saved;
        }
        catch (Exception ex)
        {
            App.Log($"countdown.json unreadable, using settings: {ex.Message}");
        }
        return Normalize(settings.CountdownPosition) ?? "top-right";
    }

    /// <summary>One of <see cref="Places"/>, or null.</summary>
    public static string? Normalize(string? place)
    {
        string p = (place ?? "").Trim().ToLowerInvariant();
        return Array.IndexOf(Places, p) >= 0 ? p : null;
    }

    /// <summary>Written to a temporary file first, then moved over the old one.</summary>
    public static void Save(string place)
    {
        try
        {
            Directory.CreateDirectory(AppIdentity.SettingsDir);
            string tmp = PlaceFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new PlaceFileModel { Place = place }));
            File.Move(tmp, PlaceFile, overwrite: true);
        }
        catch (Exception ex)
        {
            App.Log($"Could not save countdown.json: {ex.Message}");
        }
    }
}
