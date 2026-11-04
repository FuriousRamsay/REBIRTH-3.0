using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// Only the selected, successfully deleted save's client-local preferences live
// outside its native save directory. Never sweep unrelated world directories.
internal static class RebirthSaveLocalDataCleanup
{
    internal sealed class Plan
    {
        internal string SaveDirectory, UserDirectory;
        internal string[] WorldKeys;
    }
    internal static string WorldKey(string saveDirectory, Guid worldId)
    {
        if (worldId == Guid.Empty) throw new ArgumentException("Native world GUID required");
        return Hash(Normalize(saveDirectory) + "|" + worldId.ToString("N"));
    }
    internal static Plan Capture(string userDirectory, string saveDirectory, Guid worldId, bool existed)
    {
        if (!existed || worldId == Guid.Empty) return null;
        return new Plan { SaveDirectory = Path.GetFullPath(saveDirectory), UserDirectory = Path.GetFullPath(userDirectory),
            WorldKeys = new[] { WorldKey(saveDirectory, worldId), Hash(Normalize(saveDirectory)) } };
    }
    internal static bool DeleteAfterNativeConfirmation(Plan plan, bool nativeStillExists, Action<string> retireWorld, out int files)
    {
        files = 0;
        if (plan == null || nativeStillExists || Directory.Exists(plan.SaveDirectory) || File.Exists(plan.SaveDirectory)) return false;
        // Cancel detached/dirty snapshots before deleting files, so a queued
        // preference flush cannot recreate an orphan after native deletion.
        foreach (string key in plan.WorldKeys) retireWorld(key);
        foreach (string key in plan.WorldKeys)
            foreach (string kind in new[] { "HudTracking", "Journal" })
                files += DeleteScope(plan.UserDirectory, kind, key);
        return true;
    }
    private static int DeleteScope(string userDirectory, string kind, string key)
    {
        if (!Regex.IsMatch(key ?? "", "\\A[0-9a-f]{64}\\z")) throw new InvalidDataException("Exact world storage key required");
        string root = Path.GetFullPath(Path.Combine(userDirectory, "RebirthData", kind));
        string folder = Path.GetFullPath(Path.Combine(root, key));
        // No recursive removal or calculated shell deletion. Reparse entries
        // (including dangling links) are refused before any file mutation.
        RequirePlainChain(folder);
        if (!Directory.Exists(folder)) return 0;
        var candidates = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly);
        if (Directory.GetDirectories(folder).Length != 0) throw new InvalidDataException("Unexpected nested local save data retained: " + folder);
        foreach (string path in candidates)
        {
            RequirePlainChain(path);
            if (!Regex.IsMatch(Path.GetFileName(path), "\\A[0-9a-fA-F]{64}\\.xml(?:\\.guides\\.xml)?(?:\\.bak|\\.tmp)?\\z"))
                throw new InvalidDataException("Unexpected local save file retained: " + path);
        }
        foreach (string path in candidates) { RequirePlainChain(path); File.Delete(path); }
        RequirePlainChain(folder);
        Directory.Delete(folder, false);
        return candidates.Length;
    }
    private static void RequirePlainChain(string path)
    {
        for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Reparse local-save path refused: " + current); }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    private static string Normalize(string path) => Path.GetFullPath(path.Trim()).Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
    private static string Hash(string value)
    {
        using (var sha = SHA256.Create())
        {
            var result = new StringBuilder(64);
            foreach (byte b in sha.ComputeHash(Encoding.UTF8.GetBytes(value))) result.Append(b.ToString("x2"));
            return result.ToString();
        }
    }
}
