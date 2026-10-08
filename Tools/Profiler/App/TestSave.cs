using System.Diagnostics;
using System.Text.Json;

namespace RebirthProfiler;

/// <summary>
/// Makes every run start from the same world: the disposable test save is replaced by a fresh copy of the baseline save
/// (the same thing "gamebridge launch -Reset" does). Without this, each run starts wherever the previous one ended:
/// a different time of day, different zombies, a possibly dead character.
/// Only the save named in Tools/GameBridge/gamebridge.config.json is ever deleted, and only if it differs from the baseline.
/// </summary>
static class TestSave
{
    static string ConfigPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "GameBridge", "gamebridge.config.json"));
    static string SavesRoot => Path.Combine(Environment.GetEnvironmentVariable("APPDATA") ?? "", "7DaysToDie", "Saves");

    public static bool IsConfigured()
    {
        try { using var d = JsonDocument.Parse(File.ReadAllText(ConfigPath)); return !string.IsNullOrWhiteSpace(d.RootElement.GetProperty("baselineSave").GetString()); }
        catch { return false; }
    }

    /// <returns>a description of what was done, for the status line.</returns>
    public static string Restore()
    {
        if (Process.GetProcessesByName("7DaysToDie").Length > 0) throw new InvalidOperationException("Cannot reset the test save while the game is running.");
        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        var r = doc.RootElement;
        string world = r.GetProperty("world").GetString() ?? "", save = r.GetProperty("save").GetString() ?? "", baseline = r.GetProperty("baselineSave").GetString() ?? "";
        if (world.Length == 0 || save.Length == 0 || baseline.Length == 0) throw new InvalidOperationException("gamebridge.config.json needs world, save and baselineSave.");
        if (string.Equals(save, baseline, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("save and baselineSave must be different (the test save gets overwritten).");
        if (save.IndexOfAny(new[] { '\\', '/', ':' }) >= 0 || save == "." || save == "..") throw new InvalidOperationException("Refusing to reset a save with a suspicious name: " + save);

        string worldDir = Path.Combine(SavesRoot, world);
        string source = Path.Combine(worldDir, baseline), target = Path.Combine(worldDir, save);
        if (!Directory.Exists(source)) throw new InvalidOperationException("Baseline save not found: " + source);
        if (!Path.GetFullPath(target).StartsWith(Path.GetFullPath(worldDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test save path is outside the saves folder: " + target);

        if (Directory.Exists(target)) Directory.Delete(target, true);
        CopyDirectory(source, target);
        return $"Test save '{save}' restored from '{baseline}'.";
    }

    static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(from)) CopyDirectory(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
