using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Small cross-platform persistence primitive for REBIRTH-owned XML records.
/// Writes always occur in the destination directory so replacement never crosses volumes.
/// Atomic replacement retains the previous primary bytes as .bak; their semantic validity is caller-owned.
/// </summary>
public static class RebirthAtomicXmlFile
{
    private static readonly object WriterSync = new object();
    private static readonly Dictionary<string, object> PathLocks =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    private static object GetPathLock(string path)
    {
        string key;
        try { key = Path.GetFullPath(path ?? string.Empty); }
        catch { key = path ?? string.Empty; }
        lock (WriterSync)
        {
            object gate;
            if (!PathLocks.TryGetValue(key, out gate))
            {
                gate = new object();
                PathLocks[key] = gate;
            }
            return gate;
        }
    }

    public static bool TryWrite(string path, XDocument document, out string error)
    {
        object gate = GetPathLock(path);
        lock (gate) return TryWriteUnlocked(path, document, out error);
    }

    private static bool TryWriteUnlocked(string path, XDocument document, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrEmpty(path))
        {
            error = "destination path is empty";
            return false;
        }
        if (document == null || document.Root == null)
        {
            error = "XML document/root is null";
            return false;
        }

        string temp = path + ".tmp";
        try
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                error = "destination directory is empty";
                return false;
            }
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(temp))
                File.Delete(temp);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                NewLineHandling = NewLineHandling.Entitize,
                CloseOutput = false
            };

            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                document.Save(writer);
                writer.Flush();
                // Flush candidate bytes to the storage device before publishing the final file.
                stream.Flush(true);
            }

            // Re-open the exact bytes that are about to become authoritative. This catches
            // incomplete/invalid serialization before the current final is disturbed.
            XDocument validation = XDocument.Load(temp, LoadOptions.None);
            if (validation.Root == null)
                throw new InvalidDataException("temporary XML has no root element");

            // If atomic replacement fails, retain the primary instead of deleting
            // it before a move. Reuse the common publisher rather than another fallback.
            if (RebirthDurableFileCommit.TryPublish(temp, path, out error))
                return true;
            // Preserve this helper's existing failed-candidate cleanup contract.
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            return false;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            return false;
        }
    }

    public static bool TryLoad(string path, out XDocument document, out string error)
    {
        document = null;
        error = string.Empty;
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = "file does not exist";
                return false;
            }
            document = XDocument.Load(path, LoadOptions.None);
            if (document.Root == null)
            {
                error = "XML root is missing";
                document = null;
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            document = null;
            return false;
        }
    }

    public static bool TryLoadFinalThenBackup(string path, out XDocument document, out bool usedBackup, out string error)
    {
        document = null;
        usedBackup = false;
        error = string.Empty;

        string finalError;
        if (TryLoad(path, out document, out finalError))
            return true;

        string backup = path + ".bak";
        string backupError;
        if (TryLoad(backup, out document, out backupError))
        {
            usedBackup = true;
            error = "final failed (" + finalError + "); recovered from backup";
            return true;
        }

        error = "final failed (" + finalError + "); backup failed (" + backupError + ")";
        return false;
    }
}
