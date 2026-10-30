using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using HarmonyLib;

#nullable disable

// Demand-scoped original harvest postimage publication. No producer calls this
// until the original-action protocol supplies a sealed postimage predicate.
internal static class AdvancedFarmingHarvestPublication
{
    internal sealed class Proof {internal Guid Operation,Source;internal string World,Digest;internal int X,Y,Z;internal long Generation;}
    private sealed class Attempt
    {
        internal World World;internal string Directory,WorldId,RegionDirectory;
        internal Vector3i Position;internal Chunk Chunk;internal ChunkProviderGenerateWorld Provider;
        internal Func<bool> Original;internal Func<bool> ProviderCurrent;
        internal RebirthStationSnapshotEvidence.Request Request;internal float Started;
        internal Guid Source;
        internal volatile Proof Proof;
        internal bool Scope()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&World.worldState?.Guid==WorldId&&GameIO.GetSaveGameDir()==Directory&&ProviderCurrent();
        internal bool Postimage()=>Scope()&&ReferenceEquals(World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&!Chunk.InProgressUnloading&&Original();
    }
    private static readonly Dictionary<Guid,Attempt> Attempts=new Dictionary<Guid,Attempt>();
    internal static void Install(Harmony harmony)
    {
        foreach(var type in new[]{typeof(RebirthStationSnapshotUpdatePatch),typeof(RebirthStationChunkSerializedPatch),typeof(RebirthStationSnapshotResetPatch),typeof(RebirthStationSnapshotWritePatch),typeof(RebirthStationRegionPublishedPatch)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
    }
    internal static bool Observe(Guid operation,Guid source,World world,Vector3i position,Func<bool> originalPostimage,out Proof proof)
    {
        proof=null;
        if(operation==Guid.Empty||source==Guid.Empty||world==null||world.IsRemote()||!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||originalPostimage==null)return false;
        try
        {
            if(!Attempts.TryGetValue(operation,out var attempt))
            {
                if(Attempts.Count>=4||!originalPostimage())return false;
                var chunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
                var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
                if(chunk==null||chunk.InProgressUnloading||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased))return false;
                var access=regions.regionFileAccess;string regionDirectory=Path.GetFullPath(regions.saveDirectory);
                attempt=new Attempt{World=world,Directory=GameIO.GetSaveGameDir(),WorldId=world.worldState?.Guid,Position=position,Chunk=chunk,Provider=provider,Original=originalPostimage,RegionDirectory=regionDirectory,Source=source};
                attempt.ProviderCurrent=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==regionDirectory;
                if(!attempt.Postimage())return false;Attempts.Add(operation,attempt);
            }
            if(!ReferenceEquals(attempt.World,world)||attempt.Source!=source||attempt.Position!=position||!attempt.Scope())return false;
            if(attempt.Proof!=null){proof=attempt.Proof;return true;}
            if(attempt.Request!=null)
            {
                // Native publication belongs to the captured generation. Subsequent
                // legitimate growth need not undo an already published original image.
                if(RebirthStationSnapshotEvidence.HasPublished(attempt.Request)&&RebirthStationSnapshotEvidence.TryCopy(attempt.Request,out var bytes))
                {
                    using(var hash=SHA256.Create())proof=new Proof{Operation=operation,Source=source,World=attempt.WorldId,X=position.x,Y=position.y,Z=position.z,Digest=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(),Generation=attempt.Request.Generation};
                    attempt.Proof=proof;attempt.Request.Dispose();attempt.Request=null;return true;
                }
                if(UnityEngine.Time.realtimeSinceStartup-attempt.Started>15f){attempt.Request.Dispose();attempt.Request=null;}
                else return false;
            }
            if(!attempt.Postimage())return false;
            var current=attempt;
            attempt.Request=RebirthStationSnapshotEvidence.Watch(world,attempt.Chunk,attempt.Chunk.X,attempt.Chunk.Z,current.Postimage);
            if(attempt.Request==null)return false;attempt.Started=UnityEngine.Time.realtimeSinceStartup;
            // Preserve this exact successfully read-back generation before the native
            // writer returns its snapshot to the pool. Never retain the pooled object.
            attempt.Request.OnPublished=(generation,digest)=>current.Proof=new Proof{Operation=operation,Source=source,World=current.WorldId,X=position.x,Y=position.y,Z=position.z,Digest=digest,Generation=generation};
            lock(attempt.Chunk)
            {
                var chunk=attempt.Chunk;
                if(chunk.IsLocked||chunk.InProgressSaving||chunk.InProgressUnloading||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){attempt.Request.Dispose();attempt.Request=null;return false;}
                chunk.InProgressSaving=true;
            }
            try{attempt.Provider.m_RegionFileManager.SaveChunkSnapshot(attempt.Chunk,true);}
            finally{lock(attempt.Chunk)attempt.Chunk.InProgressSaving=false;}
            return false;
        }
        catch(Exception error)
        {
            if(Attempts.TryGetValue(operation,out var retained)){retained.Request?.Dispose();retained.Request=null;}
            Log.Warning("[REBIRTH Farming] original source publication retained: "+error.GetType().Name);return false;
        }
    }
    // Caller invokes only after this exact proof has reached its durable receipt.
    internal static void Complete(Guid operation)
    {if(Attempts.TryGetValue(operation,out var attempt)){attempt.Request?.Dispose();Attempts.Remove(operation);}}
    // Dispose only observation buffers for a retired world, never its receipts or outputs.
    internal static void RetireObservationWorld(World retiring)
    {
        var remove=new List<Guid>();foreach(var pair in Attempts)if(ReferenceEquals(pair.Value.World,retiring)){pair.Value.Request?.Dispose();remove.Add(pair.Key);}
        foreach(var operation in remove)Attempts.Remove(operation);
    }
}
