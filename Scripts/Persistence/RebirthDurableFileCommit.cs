using System;
using System.IO;

#nullable disable

/// <summary>
/// Shared same-volume publication helper for non-XML Rebirth persistence stores.
/// Callers write and flush a complete temporary file first. Publication never deletes the
/// authoritative primary before the replacement exists; on failure the old primary remains
/// authoritative and the caller keeps its dirty/retry state.
/// </summary>
public static class RebirthDurableFileCommit
{
    public static bool TryPublish(string tempPath, string finalPath, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(finalPath))
        {
            error = "path-missing";
            return false;
        }
        if (!File.Exists(tempPath))
        {
            error = "temporary-file-missing";
            return false;
        }

        try
        {
            string directory = Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (!File.Exists(finalPath))
            {
                File.Move(tempPath, finalPath);
                return true;
            }

            // File.Replace performs the same-volume primary/backup swap without a
            // delete-before-move window. If the platform cannot provide this contract,
            // fail closed and leave the existing primary untouched.
            File.Replace(tempPath, finalPath, finalPath + ".bak", true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }
}
