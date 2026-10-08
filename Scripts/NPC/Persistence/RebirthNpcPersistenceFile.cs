using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

#nullable disable

public static class RebirthNpcPersistenceFile
{
    private static readonly object FailureSync = new object();
    private static readonly Dictionary<string, string> RejectedReads = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static void BlockWrite(string path, string reason)
    {
        if (string.IsNullOrEmpty(path)) return;
        lock (FailureSync) RejectedReads[Path.GetFullPath(path)] = reason ?? "load rejected";
    }
    public static void VerifiedRead(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        lock (FailureSync) RejectedReads.Remove(Path.GetFullPath(path));
    }
    public static bool CanInitializeEmpty(string path)
    {
        if(string.IsNullOrEmpty(path)||File.Exists(path)||File.Exists(path+".bak")||HasQuarantinedState(path))return false;
        lock(FailureSync)return !RejectedReads.ContainsKey(Path.GetFullPath(path));
    }

    private static bool HasQuarantinedState(string path)
    {
        string full=Path.GetFullPath(path);
        string directory=Path.GetDirectoryName(full);
        if(!Directory.Exists(directory))return false;
        // A prior process may have moved the only readable candidate out of the way.
        // Keep that evidence until a valid primary/backup is supplied; never infer a new save from absence alone.
        foreach(string ignored in Directory.EnumerateFiles(directory,Path.GetFileName(full)+".corrupt.*",SearchOption.TopDirectoryOnly))return true;
        return false;
    }

    public static void AssertWritable(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("NPC persistence path is unavailable.");
        string reason;
        lock (FailureSync)
            if (RejectedReads.TryGetValue(Path.GetFullPath(path), out reason))
                throw new InvalidDataException("Refusing to overwrite rejected NPC state: " + Path.GetFileName(path) + ": " + reason);
    }
    public static void RequireCheckpointLoaded(bool loaded, string path)
    {
        if (!RebirthNpcPersistenceCoordinator.IsCheckpointWrite) return;
        if (!loaded) throw new InvalidOperationException("NPC store is not loaded: " + Path.GetFileName(path));
        AssertWritable(path);
    }

    public static bool TryLoad(string path, Func<XmlDocument, bool> validator,
        out XmlDocument document, out string source, out string error)
    {
        document = null;
        source = string.Empty;
        error = string.Empty;
        if (string.IsNullOrEmpty(path)) return false;

        bool hadStoredState = File.Exists(path) || File.Exists(path + ".bak") || HasQuarantinedState(path);
        string primaryError;
        if (TryLoadOne(path, validator, out document, out primaryError))
        {
            VerifiedRead(path);
            source = "primary";
            return true;
        }

        string backup = path + ".bak";
        string backupError;
        if (TryLoadOne(backup, validator, out document, out backupError))
        {
            VerifiedRead(path);
            source = "backup";
            error = primaryError;
            Quarantine(path);
            Log.Warning("[REBIRTH NPC] Recovered persistence file from backup: " + Path.GetFileName(path)
                + (string.IsNullOrEmpty(primaryError) ? string.Empty : " primaryError=" + primaryError));
            return true;
        }

        error = "primary=" + primaryError + "; backup=" + backupError;
        // Quarantine must not make an unreadable store look like a genuinely new empty one
        // to a later Save call. A verified primary/backup read is required to lift this guard.
        if (hadStoredState) BlockWrite(path, error);
        if (File.Exists(path)) Quarantine(path);
        return false;
    }

    public static void SaveAtomic(string path, XmlDocument document)
    {
        if (string.IsNullOrEmpty(path) || document == null)
        {
            if (RebirthNpcPersistenceCoordinator.IsCheckpointWrite) throw new InvalidOperationException("NPC checkpoint document/path unavailable.");
            return;
        }
        AssertWritable(path);
        // Use the shared flushed, verified same-directory writer. Preserve this store's
        // rejected-read guard and primary/backup recovery policy above.
        System.Xml.Linq.XDocument candidate;
        using (var reader = new XmlNodeReader(document))
            candidate = System.Xml.Linq.XDocument.Load(reader);
        string error;
        if (!RebirthAtomicXmlFile.TryWrite(path, candidate, out error))
            throw new IOException("NPC persistence publication failed: " + error);
    }

    private static bool TryLoadOne(string path, Func<XmlDocument, bool> validator,
        out XmlDocument document, out string error)
    {
        document = null;
        error = string.Empty;
        if (!File.Exists(path))
        {
            error = "missing";
            return false;
        }
        try
        {
            XmlDocument candidate = new XmlDocument();
            candidate.Load(path);
            if (validator != null && !validator(candidate))
                throw new InvalidDataException("Document validation failed.");
            document = candidate;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static void Quarantine(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            string quarantine = path + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Move(path, quarantine);
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH NPC] Failed to quarantine persistence file '" + path + "': "
                + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
