using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Positive, durable ownership of REBIRTH scratch profiles. A name/prefix alone is
/// never deletion authority. Pending records are deliberately NOT purgeable: a
/// failed native save/ownership commit has an unknown outcome and is preserved.
/// This BCL-only store can be exercised against a disposable directory.
/// </summary>
internal static class RebirthTemporaryProfileOwnership
{
    private const string Prefix = "RBTemp_";
    private const string Header = "REBIRTH_TEMP_PROFILE_V1";

    internal static bool IsReservedName(string name)
    {
        return !string.IsNullOrEmpty(name) && name.Trim().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static string NewName(out string token)
    {
        token = Guid.NewGuid().ToString("N");
        // 29 ASCII characters, below the existing 30-character profile limit.
        return Prefix + token.Substring(0, 22);
    }

    private static bool ValidName(string name)
    {
        if (name == null || name.Length != 29 || !name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
        for (int i = Prefix.Length; i < name.Length; i++)
            if (!Uri.IsHexDigit(name[i])) return false;
        return true;
    }

    private static string RecordPath(string directory, string name, string suffix)
    {
        if (!ValidName(name)) throw new ArgumentException("Not an owned temporary profile name.", "name");
        return Path.Combine(directory, name.ToLowerInvariant() + suffix);
    }

    private static bool Matches(string path, string name)
    {
        if (!ValidName(name) || !File.Exists(path)) return false;
        string[] lines = File.ReadAllLines(path);
        Guid token;
        return lines.Length == 3 && lines[0] == Header && Guid.TryParseExact(lines[1], "N", out token) &&
            string.Equals(lines[2], name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Prefix + token.ToString("N").Substring(0, 22), name, StringComparison.OrdinalIgnoreCase);
    }

    internal static void Begin(string directory, string name, string token)
    {
        Guid parsed;
        if (!Guid.TryParseExact(token, "N", out parsed) ||
            !string.Equals(Prefix + parsed.ToString("N").Substring(0, 22), name, StringComparison.Ordinal))
            throw new ArgumentException("Temporary profile token/name mismatch.");
        Directory.CreateDirectory(directory);
        string pending = RecordPath(directory, name, ".pending");
        if (File.Exists(RecordPath(directory, name, ".owned"))) throw new IOException("Ownership record already exists.");
        using (FileStream stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.WriteLine(Header);
            writer.WriteLine(token);
            writer.WriteLine(name);
        }
    }

    internal static void Commit(string directory, string name)
    {
        string pending = RecordPath(directory, name, ".pending");
        if (!Matches(pending, name)) throw new IOException("Temporary profile ownership intent is missing or invalid.");
        // No overwrite; publication occurs only after the native profile save succeeds.
        File.Move(pending, RecordPath(directory, name, ".owned"));
    }

    internal static bool IsOwned(string directory, string name)
    {
        if (!ValidName(name)) return false;
        try { return Matches(RecordPath(directory, name, ".owned"), name); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static IEnumerable<string> GetOwnedNames(string directory, int limit)
    {
        if (limit <= 0 || !Directory.Exists(directory)) yield break;
        int inspected = 0;
        foreach (string path in Directory.EnumerateFiles(directory, "*.owned"))
        {
            if (inspected++ >= limit) yield break;
            string name = Path.GetFileNameWithoutExtension(path);
            if (IsOwned(directory, name)) yield return name;
        }
    }

    internal static void Forget(string directory, string name)
    {
        if (!ValidName(name)) return;
        string owned = RecordPath(directory, name, ".owned");
        if (Matches(owned, name)) File.Delete(owned);
        // Pending records are retained for explicit recovery, never used for cleanup.
    }
}
