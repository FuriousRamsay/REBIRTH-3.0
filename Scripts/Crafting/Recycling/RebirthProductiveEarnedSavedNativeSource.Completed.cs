using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    // Historical actual-load receipt for metadata disposal only. It cannot
    // install a source, clear a marker, authorize output or skip disk history.
    internal sealed class CompletedNativeLoad:IDisposable
    {
        private readonly World world;private readonly string save,guid,worldKey;
        private readonly ChunkSnapshotUtil util;private readonly RegionFileAccessAbstract access;
        private readonly Guid birth,operation;private readonly byte[] digest,originalDigest;
        private IDisposable memory;
        internal CompletedNativeLoad(World actualWorld,CachedLoadRequest request,ChunkSnapshotUtil actualUtil,
            RegionFileAccessAbstract actualAccess,byte[] nativeDigest,byte[] originalImageDigest,IDisposable retained)
        {
            world=actualWorld;save=request.Directory;guid=request.Custody.WorldGuid;worldKey=request.Custody.Frame.World;
            util=actualUtil;access=actualAccess;birth=request.Custody.Frame.Birth;operation=request.Custody.Frame.Operation;
            digest=nativeDigest;originalDigest=originalImageDigest;memory=retained;
        }
        internal bool Current()=>memory!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&
            world?.IsRemote()==false&&string.Equals(Path.GetFullPath(GameIO.GetSaveGameDir()),save,RebirthStationCompletionOriginalScope.PathComparison)&&
            Guid.TryParse(RebirthWorkstationCompletionOutbox.CurrentGuid(world),out var current)&&Guid.TryParse(guid,out var saved)&&current!=Guid.Empty&&current==saved&&
            RebirthProductiveEarnedArtifact.WorldIdentity(world)==worldKey&&world.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&
            ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,util)&&ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,access);
        internal bool Matches(Guid expectedBirth,Guid expectedOperation,byte[] expectedOriginalDigest,byte[] native,int offset)
        {
            try{return Current()&&birth==expectedBirth&&operation==expectedOperation&&expectedOriginalDigest?.Length==32&&
                expectedOriginalDigest.SequenceEqual(originalDigest)&&native!=null&&
                RebirthProductiveEarnedNativeBody.ComparisonDigest(native,offset).SequenceEqual(digest);}
            catch{return false;}
        }
        public void Dispose()
        {lock(cachedLoadGate){completedNativeLoads.Remove(this);memory?.Dispose();memory=null;}}
    }
    private static readonly List<CompletedNativeLoad> completedNativeLoads=new List<CompletedNativeLoad>();
    private static bool RecordCompletedNativeLoad(CachedLoading loading)
    {
        IDisposable retained=null;CompletedNativeLoad value=null;
        try
        {
            var source=loading.Request.Custody;var world=GameManager.Instance?.World;
            // Producers already proved full read/factory/load return and exact
            // loaded marker absence. Backpressure preserves the registration.
            if(!ThreadManager.IsMainThread()||source.Frame.Birth==Guid.Empty||source.Frame.Operation==Guid.Empty)return false;
            if(!RebirthProductiveEarnedCoordinator.NeedsLoadedNativeCleanup(source.Frame.Birth,source.Frame.Operation)&&
                !RebirthProductiveEarnedRemoteHost.NeedsRemoteNativeClearCleanup(source.Frame.Birth,source.Frame.Operation))return true;
            byte[] originalDigest;using(var sha=SHA256.Create())originalDigest=sha.ComputeHash(source.Original);
            foreach(var old in completedNativeLoads.ToArray())if(!old.Current())old.Dispose();
            foreach(var old in completedNativeLoads)
                if(old.Matches(source.Frame.Birth,source.Frame.Operation,originalDigest,source.Native,source.NativeOffset))return true;
            if(completedNativeLoads.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(2048,out retained))return false;
            value=new CompletedNativeLoad(world,loading.Request,loading.Util,loading.Access,
                RebirthProductiveEarnedNativeBody.ComparisonDigest(source.Native,source.NativeOffset),originalDigest,retained);retained=null;
            if(!value.Current())return false;completedNativeLoads.Add(value);value=null;return true;
        }
        catch{return false;}
        finally{value?.Dispose();retained?.Dispose();}
    }
    internal static bool TryObservedCompletedNativeLoad(Guid birth,Guid operation,byte[] originalDigest,byte[] native,int offset,out CompletedNativeLoad proof)
    {
        proof=null;
        lock(cachedLoadGate)
        {
            var matching=completedNativeLoads.Where(value=>value.Matches(birth,operation,originalDigest,native,offset)).ToArray();
            if(matching.Length!=1)return false;proof=matching[0];return true;
        }
    }
    private static void ResetCompletedNativeLoads()
    {foreach(var value in completedNativeLoads.ToArray())value.Dispose();}
}
