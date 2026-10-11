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
public sealed class BaselineNpcSaveScopeSnapshot
{
    public string SaveDirectory;
    public string Fingerprint;
    public int ProcessSaveGeneration;
    public bool IsSecondOrLaterSave;
}

public static class BaselineNpcSaveScope
{
    private static readonly object Sync = new object();
    private static string observedSaveDirectory = string.Empty;
    private static int processSaveGeneration;

    public static BaselineNpcSaveScopeSnapshot ObserveCurrent()
    {
        // GameIO can retain the previously selected save while the main menu is active.
        // Do not advance process-save generation until an actual World exists.
        if (GameManager.Instance == null || GameManager.Instance.World == null)
        {
            return new BaselineNpcSaveScopeSnapshot
            {
                SaveDirectory = string.Empty,
                Fingerprint = string.Empty,
                ProcessSaveGeneration = processSaveGeneration,
                IsSecondOrLaterSave = processSaveGeneration > 1
            };
        }

        string directory = string.Empty;
        try { directory = GameIO.GetSaveGameDir() ?? string.Empty; }
        catch { directory = string.Empty; }

        if (string.IsNullOrWhiteSpace(directory))
        {
            return new BaselineNpcSaveScopeSnapshot
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
                observedSaveDirectory = directory;
                processSaveGeneration++;
            }

            return new BaselineNpcSaveScopeSnapshot
            {
                SaveDirectory = directory,
                Fingerprint = ComputeFingerprint(directory),
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
