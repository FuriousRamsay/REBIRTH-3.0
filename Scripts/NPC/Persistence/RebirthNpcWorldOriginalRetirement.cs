using System;
using System.IO;
using System.Xml;

public static partial class RebirthNpcWorldIntegrationService
{
    private static object worldOriginalEpoch=new object();
    private static RebirthNpcOriginalSaveBinding worldOriginalBinding;
    private static void RequireWorldBindingCompatible()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("NPC world identity requires main thread");
        if(loadedNativeWorld!=null&&(!ReferenceEquals(loadedNativeWorld,GameManager.Instance?.World)||!string.Equals(loadedNativeDirectory,GameIO.GetSaveGameDir(),StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("NPC world identity belongs to retained original world/save");
    }
    private static void BindWorldOriginalNoLock()
    {worldOriginalBinding=RebirthNpcOriginalSaveBinding.Capture(loadedNativeWorld,Path.Combine(loadedNativeDirectory,FileName),worldOriginalEpoch);}
    private static void ObserveWorldMutationNoLock()
    {dirty=true;worldOriginalEpoch=new object();if(!ThreadManager.IsMainThread())worldOriginalBinding?.InvalidateMutation();else worldOriginalBinding?.ObserveMutation(worldOriginalEpoch);}
    private static bool WorldRetirementEmpty=>ByStable.Count==0&&ByAmbient.Count==0&&PendingSpawns.Count==0&&Completions.Count==0&&Replay.Count==0&&Markers.Count==0&&!SpawnAttempts.HasClaims&&!worldSnapshotPublicationUncertain;
    internal static bool CanRetireWorldBacking
    {
        get{lock(Sync)try
        {
            if(dirty||!WorldRetirementEmpty)return false;
            if(loadedNativeWorld==null)return !loaded;
            if(!loaded||worldOriginalBinding?.MatchesRetirement(worldOriginalEpoch)!=true)return false;
            string path=Path.Combine(loadedNativeDirectory,FileName);
            if(!File.Exists(path))return RebirthNpcPersistenceFile.CanInitializeEmpty(path);
            var doc=RebirthNpcRecoveryCurrentBacking.Read(path);
            return RebirthNpcWorldIdentityLoad.TryRead(doc.DocumentElement,out var identities,out var pending,out var replay,out var completions)&&identities.Count==0&&pending.Count==0&&replay.Count==0&&completions.Count==0;
        }catch{return false;}}
    }
    internal static bool TryFlushOriginalWorldBacking()
    {
        lock(Sync)try
        {
            if(loadedNativeWorld==null)return CanRetireWorldBacking;
            if(ReferenceEquals(loadedNativeWorld,GameManager.Instance?.World)&&string.Equals(loadedNativeDirectory,GameIO.GetSaveGameDir(),StringComparison.OrdinalIgnoreCase))
            {SaveIfDirty();return CanRetireWorldBacking;}
            if(!dirty)return CanRetireWorldBacking;
            if(!loaded||worldSnapshotPublicationUncertain||worldOriginalBinding==null||!worldOriginalBinding.TryPublish(CaptureRecoveryDocumentNoLock,
                doc=>RebirthNpcWorldIdentityLoad.TryRead(doc.DocumentElement,out _,out _,out _,out _),()=>worldOriginalEpoch,out _))return false;
            dirty=false;return CanRetireWorldBacking;
        }catch{return false;}
    }
    internal static bool TryResetWorldBacking(bool save)
    {
        if(save&&!TryFlushOriginalWorldBacking())return false;
        lock(Sync)
        {
            if(!CanRetireWorldBacking)return false;
            // Empty-only ordinary reset. Nonempty identities/completion/native
            // markers require the complete typed bundle, not receipt deletion.
            initialized=false;loaded=false;loadedNativeWorld=null;loadedNativeDirectory=string.Empty;worldOriginalBinding=null;worldOriginalEpoch=new object();return true;
        }
    }
}
