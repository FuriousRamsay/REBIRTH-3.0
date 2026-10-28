using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

#nullable disable

/// <summary>
/// Process-local observer plus durable fingerprint for the currently selected save.
/// The generation intentionally survives normal world-cache resets so a second save
/// loaded in the same 7DTD process can be distinguished from the first one.
/// </summary>
public sealed class RebirthNpcSaveScopeSnapshot
{
    public string SaveDirectory;
    public string Fingerprint;
    public int ProcessSaveGeneration;
    public bool IsSecondOrLaterSave;
}

public static class RebirthNpcSaveScope
{
    private static readonly object Sync = new object();
    private static string observedSaveDirectory = string.Empty;
    private static string observedFingerprint = string.Empty;
    private static int processSaveGeneration;
    private static string pathRoot, pathWorld, pathGame, cachedDirectory;
    private static Platform.UserDataStorageType pathStorage;

    // GameIO concatenates the same save path at each call. Its root is already
    // cached natively; retain the combined path until one of its inputs changes.
    // This also detects save changes within the same process without relying on
    // a world-load event arriving before an early recovery check.
    internal static string CurrentDirectory()
    {
        var storage=(Platform.UserDataStorageType)GamePrefs.GetInt(EnumGamePrefs.GameSaveStorageType);
        string root=GameIO.GetSaveGameRootDir(storage);
        string world=GamePrefs.GetString(EnumGamePrefs.GameWorld);
        string game=GamePrefs.GetString(EnumGamePrefs.GameName);
        lock(Sync)
        {
            if(cachedDirectory!=null&&pathStorage==storage&&string.Equals(pathRoot,root,StringComparison.Ordinal)&&
                string.Equals(pathWorld,world,StringComparison.Ordinal)&&string.Equals(pathGame,game,StringComparison.Ordinal))
                return cachedDirectory;
            string directory=GameIO.GetSaveGameDir(world,game,storage);
            pathRoot=root;pathWorld=world;pathGame=game;pathStorage=storage;
            return cachedDirectory=directory;
        }
    }

    public static RebirthNpcSaveScopeSnapshot ObserveCurrent()
    {
        // GameIO can retain the previously selected save while the main menu is active.
        // Do not advance process-save generation until an actual World exists.
        if (GameManager.Instance == null || GameManager.Instance.World == null)
        {
            return new RebirthNpcSaveScopeSnapshot
            {
                SaveDirectory = string.Empty,
                Fingerprint = string.Empty,
                ProcessSaveGeneration = processSaveGeneration,
                IsSecondOrLaterSave = processSaveGeneration > 1
            };
        }

        string directory = string.Empty;
        try { directory = CurrentDirectory() ?? string.Empty; }
        catch { directory = string.Empty; }

        if (string.IsNullOrWhiteSpace(directory))
        {
            return new RebirthNpcSaveScopeSnapshot
            {
                SaveDirectory = string.Empty,
                Fingerprint = string.Empty,
                ProcessSaveGeneration = processSaveGeneration,
                IsSecondOrLaterSave = processSaveGeneration > 1
            };
        }

        directory = NormalizeDirectory(directory);
        lock (Sync)
        {
            if (string.IsNullOrEmpty(observedSaveDirectory) ||
                !string.Equals(observedSaveDirectory, directory, StringComparison.OrdinalIgnoreCase))
            {
                // The fingerprint depends only on the normalized directory, not mutable world state.
                observedFingerprint = ComputeFingerprint(directory);
                observedSaveDirectory = directory;
                processSaveGeneration++;
            }

            return new RebirthNpcSaveScopeSnapshot
            {
                SaveDirectory = directory,
                Fingerprint = observedFingerprint,
                ProcessSaveGeneration = processSaveGeneration,
                IsSecondOrLaterSave = processSaveGeneration > 1
            };
        }
    }

    public static string ComputeFingerprint(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return string.Empty;
        string normalized = NormalizeDirectory(directory).ToLowerInvariant();
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("REBIRTH-NPC-SAVE-SCOPE-v1|" + normalized));
            StringBuilder b = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) b.Append(bytes[i].ToString("x2"));
            return b.ToString();
        }
    }

    private static string NormalizeDirectory(string directory)
    {
        string result = directory ?? string.Empty;
        try { result = Path.GetFullPath(result); } catch { }
        result = result.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return result.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}
