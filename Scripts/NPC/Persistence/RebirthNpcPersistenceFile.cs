using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

#nullable disable

public static partial class RebirthNpcPersistenceFile
{
    private static readonly object FailureSync = new object();
    private static readonly Dictionary<string, string> RejectedReads = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,Guid> Recoveries=new Dictionary<string,Guid>(StringComparer.OrdinalIgnoreCase);
    private sealed class WriterState
    {
        internal int Thread, Count;
        internal World World;
        internal string Directory;
        internal ulong Generation;
        internal Func<bool> OriginalProof;
    }
    private static readonly Dictionary<string,WriterState> Writers=new Dictionary<string,WriterState>(StringComparer.OrdinalIgnoreCase);
    internal static string RecoveryIntentPath(string path)=>Path.GetFullPath(path)+".recovery-intent.xml";
    internal static bool IsRecoveryFenced(string path)
    {
        if(string.IsNullOrEmpty(path))return true;
        lock(FailureSync)if(Recoveries.ContainsKey(Path.GetFullPath(path)))return true;
        return RebirthNpcLiveRecoveryGroup.Fences(path)||
            ((File.Exists(RecoveryIntentPath(path))||File.Exists(Path.GetFullPath(path)+".recovery-publications.xml"))&&
             !RebirthNpcRecoveryPublications.IsInstalled(path));
    }
    internal static bool IsStoreMutationAllowed(string fileName)
    {try{if(HasQuarantineWriterFence)return false;string dir=GameIO.GetSaveGameDir();return !string.IsNullOrEmpty(dir)&&!IsRecoveryFenced(Path.Combine(dir,fileName));}catch{return false;}}
    private sealed class ActiveWriterLease:IDisposable
    {
        private readonly string path;private bool disposed;internal ActiveWriterLease(string path){this.path=path;}
        public void Dispose(){lock(FailureSync){if(disposed)return;disposed=true;var state=Writers[path];if(--state.Count==0)Writers.Remove(path);}}
    }
    internal static IDisposable BeginWrite(string path)
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)
            throw new InvalidOperationException("NPC store publication requires the authoritative game thread.");
        string full=Path.GetFullPath(path);lock(FailureSync)
        {
            if(HasQuarantineWriterFence)throw new InvalidOperationException("NPC typed quarantine owns the publication fence");
            AssertWritable(full);
            int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
            if(Writers.TryGetValue(full,out var state))
            {
                if(state.Thread!=thread)throw new InvalidOperationException("NPC store already has a writer.");
                VerifyWriter(full,state);
                state.Count++;
            }
            else
            {
                if(!string.Equals(Path.GetDirectoryName(full),Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Ordinary NPC writer cannot publish into another save; original retirement lease required.");
                state=new WriterState{Thread=thread,Count=1,World=GameManager.Instance?.World,Directory=GameIO.GetSaveGameDir(),Generation=RebirthNpcRecoveryGeneration.RequireCurrent()};
                Writers.Add(full,state);
            }
            return new ActiveWriterLease(full);
        }
    }    private sealed class RecoveryWriterLease:IDisposable
    {
        private readonly string path;private readonly Guid operation;private bool disposed;
        internal RecoveryWriterLease(string path,Guid operation){this.path=path;this.operation=operation;}
        public void Dispose(){lock(FailureSync){if(disposed)return;disposed=true;if(Recoveries.TryGetValue(path,out var current)&&current==operation)Recoveries.Remove(path);}}
    }
    internal static bool TryBeginWrite(string path,out IDisposable lease,out string error)
    {
        lease=null;error=string.Empty;
        try{lease=BeginWrite(path);return true;}catch(Exception exception){error=exception.GetType().Name+": "+exception.Message;return false;}
    }
    internal static bool TryFenceRecovery(string path,Guid operation,out IDisposable lease,out string error)
    {
        lease=null;error=string.Empty;
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||string.IsNullOrEmpty(path)||operation==Guid.Empty){error="recovery writer fence requires authoritative game thread";return false;}
        path=Path.GetFullPath(path);lock(FailureSync)
        {
            if(HasQuarantineWriterFence){error="typed quarantine publication fence is active";return false;}
            if(Writers.TryGetValue(path,out var writer)&&writer.Count!=0){error="store writer is active";return false;}
            if(Recoveries.ContainsKey(path)){error="store recovery is already active";return false;}
            Recoveries.Add(path,operation);lease=new RecoveryWriterLease(path,operation);return true;
        }
    }
    // Positive ownership, not the broader "some recovery forbids writes" query.
    // No disk read, acquisition, mutation or receipt is implied by this proof.
    internal static bool OwnsRecoveryWriterFence(string path,Guid operation)
    {
        if(operation==Guid.Empty||!ThreadManager.IsMainThread()||string.IsNullOrEmpty(path))return false;
        lock(FailureSync)return Recoveries.TryGetValue(Path.GetFullPath(path),out var owner)&&owner==operation&&
            (!Writers.TryGetValue(Path.GetFullPath(path),out var writer)||writer.Count==0);
    }

    private static void VerifyWriter(string path,WriterState state)
    {
        if(state.OriginalProof!=null)
        {
            if(state.Thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||!state.OriginalProof())
                throw new InvalidOperationException("NPC original-save retirement proof changed: "+Path.GetFileName(path));
            return;
        }
        if(state.Thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||!ReferenceEquals(state.World,GameManager.Instance?.World)||
            !string.Equals(state.Directory,GameIO.GetSaveGameDir(),StringComparison.OrdinalIgnoreCase)||state.Generation!=RebirthNpcRecoveryGeneration.RequireCurrent())
            throw new InvalidOperationException("NPC writer world/recovery generation changed before publication: "+Path.GetFileName(path));
    }
    // A typed store supplies a captured original binding and detached epoch
    // proof. This lease never borrows the newly active world's generation.
    internal static IDisposable BeginOriginalWrite(string path,Func<bool> originalProof)
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||originalProof==null)
            throw new InvalidOperationException("Original-save retirement requires the authoritative game thread.");
        string full=Path.GetFullPath(path);
        lock(FailureSync)
        {
            if(HasQuarantineWriterFence)throw new InvalidOperationException("NPC typed quarantine owns the publication fence");
            AssertWritable(full);
            if(Writers.ContainsKey(full)||!originalProof())throw new InvalidOperationException("Original-save writer/head is not exclusively owned.");
            Writers.Add(full,new WriterState{Thread=System.Threading.Thread.CurrentThread.ManagedThreadId,Count=1,OriginalProof=originalProof});
            return new ActiveWriterLease(full);
        }
    }
    internal static bool HasRecoveryLease(string path,Guid operation)
    {lock(FailureSync)return Recoveries.TryGetValue(Path.GetFullPath(path),out var current)&&current==operation;}
    internal static bool HasRecoveryOperation(Guid operation)
    {lock(FailureSync)return operation!=Guid.Empty&&Recoveries.ContainsValue(operation);}
    internal static bool HasWriterLease(string path)
    {
        lock(FailureSync)
        {
            if(!Writers.TryGetValue(Path.GetFullPath(path),out var state))return false;
            VerifyWriter(path,state);return true;
        }
    }
    internal static void RequireCurrentWriter(string path)
    {
        lock(FailureSync)
        {
            if(!Writers.TryGetValue(Path.GetFullPath(path),out var writer))throw new InvalidOperationException("NPC publication has no captured writer lease.");
            VerifyWriter(path,writer);AssertWritable(path);
        }
    }

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
        if(HasQuarantineWriterFence)return false;
        if(RebirthNpcPersistenceTimelineService.IsAlignmentFailed)return false;
        if(IsRecoveryFenced(path))return false;
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
        if(RebirthNpcRetainedWorkCustody.BlocksWrite(path))throw new InvalidOperationException("Original retained work custody owns this exact saved execution/checkpoint publication.");
        if(HasQuarantineWriterFence)throw new InvalidOperationException("NPC typed quarantine owns the publication fence");
        if(RebirthNpcPersistenceTimelineService.IsAlignmentFailed)throw new InvalidOperationException("NPC timeline alignment is unresolved.");
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("NPC persistence path is unavailable.");
        if(IsRecoveryFenced(path))throw new InvalidOperationException("NPC recovery convergence is pending for "+Path.GetFileName(path));
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
        if(RebirthNpcPersistenceTimelineService.IsAlignmentFailed){error="NPC timeline alignment is unresolved.";return false;}
        if (string.IsNullOrEmpty(path)) return false;
        if(IsRecoveryFenced(path)){error="NPC live recovery must reconcile its installed generation before ordinary loading.";return false;}

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
        if(string.IsNullOrEmpty(path)||document==null)
        {
            if(RebirthNpcPersistenceCoordinator.IsCheckpointWrite)throw new InvalidOperationException("NPC checkpoint document/path unavailable.");
            return;
        }
        using(var writerLease=BeginWrite(path))
        {
            lock(FailureSync)VerifyWriter(Path.GetFullPath(path),Writers[Path.GetFullPath(path)]);
            if(!RebirthNpcRecoveryPublications.TryPublishInstalled(path,document,out bool handled,out var recoveryError))
                throw new IOException("NPC installed-generation publication failed: "+recoveryError);
            if(handled)return;
            System.Xml.Linq.XDocument candidate;
            using(var reader=new XmlNodeReader(document))candidate=System.Xml.Linq.XDocument.Load(reader);
            if(!RebirthAtomicXmlFile.TryWrite(path,candidate,out var error))throw new IOException("NPC persistence publication failed: "+error);
        }
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
