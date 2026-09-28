using System.IO;

namespace SmoothDraw;

/// <summary>
/// Every name this app is known by outside itself, in one place. Until 2026-09-28 the app
/// was SmoothAnnotate, inside the smoothzoom repo; the Old* names exist only to take over
/// from that copy (its settings are carried over once, and a running one is stopped).
/// </summary>
public static class AppIdentity
{
    public const string Name = "SmoothDraw";

    /// <summary>One copy per Windows session.</summary>
    public const string MutexName = @"Global\SmoothDrawMutex";

    /// <summary>\\.\pipe\SmoothDraw.control — the Start menu, the pen button and tests.</summary>
    public const string PipeName = "SmoothDraw.control";

    /// <summary>%APPDATA%\SmoothDraw: settings.json, dot-position.json, pen.json.</summary>
    public static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);

    /// <summary>%LOCALAPPDATA%\SmoothDraw: debug.log and the pipe's snapshot PNGs.</summary>
    public static readonly string LocalDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);

    public static readonly string LogFile = Path.Combine(LocalDir, "debug.log");

    // --- The old name ---

    public const string OldName = "SmoothAnnotate";

    public static readonly string OldSettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), OldName);
}
