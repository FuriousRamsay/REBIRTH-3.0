using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class CancellationNativeSaveState
    {internal readonly object Gate=new object();internal long Epoch;internal int Active;}
    private static readonly ConditionalWeakTable<RegionFileManager,CancellationNativeSaveState> cancellationNativeSaves=new ConditionalWeakTable<RegionFileManager,CancellationNativeSaveState>();
    private static CancellationNativeSaveState NewCancellationNativeSaveState(RegionFileManager manager)=>new CancellationNativeSaveState();
    private static readonly ConditionalWeakTable<RegionFileManager,CancellationNativeSaveState>.CreateValueCallback newCancellationNativeSaveState=NewCancellationNativeSaveState;
    private sealed class CancellationNativeSaveStamp
    {
        private readonly RegionFileManager manager;private readonly CancellationNativeSaveState state;private readonly long epoch,key;private readonly Chunk retired;
        private CancellationNativeSaveStamp(RegionFileManager manager,CancellationNativeSaveState state,long epoch,long key,Chunk retired)
        {this.manager=manager;this.state=state;this.epoch=epoch;this.key=key;this.retired=retired;}
        internal sealed class Lease:IDisposable
        {
            private CancellationNativeSaveStamp stamp;internal Lease(CancellationNativeSaveStamp value){stamp=value;}
            internal bool Current{get{var value=stamp;if(value==null)return false;lock(value.state.Gate)return value.state.Active==0&&value.state.Epoch==value.epoch;}}
            public void Dispose(){var value=Interlocked.Exchange(ref stamp,null);if(value!=null)Monitor.Exit(value.manager.saveLock);}
        }
        private static bool QueuesEmpty(RegionFileManager manager,long key,Chunk retired)
        {
            if(!Monitor.TryEnter(manager.chunkMemoryStreamsToSave))return false;
            try{if(manager.chunkMemoryStreamsToSave.ContainsKey(key))return false;}finally{Monitor.Exit(manager.chunkMemoryStreamsToSave);}
            if(!Monitor.TryEnter(manager.chunksToSave))return false;
            try{if(manager.chunksToSave.ContainsKey(key)||manager.chunkKeyCurrentlySaved==key)return false;}finally{Monitor.Exit(manager.chunksToSave);}
            if(!Monitor.TryEnter(manager.chunksToUnloadLater))return false;
            try{foreach(var chunk in manager.chunksToUnloadLater)if(ReferenceEquals(chunk,retired))return false;}finally{Monitor.Exit(manager.chunksToUnloadLater);}
            return true;
        }
        internal static bool TryCapture(RegionFileManager manager,long key,Chunk retired,out CancellationNativeSaveStamp stamp)
        {
            stamp=null;if(manager==null||!Monitor.TryEnter(manager.saveLock))return false;
            try
            {
                var state=cancellationNativeSaves.GetValue(manager,newCancellationNativeSaveState);long epoch;
                lock(state.Gate){if(state.Active!=0)return false;epoch=state.Epoch;}
                if(!QueuesEmpty(manager,key,retired))return false;
                lock(state.Gate){if(state.Active!=0||state.Epoch!=epoch)return false;}
                stamp=new CancellationNativeSaveStamp(manager,state,epoch,key,retired);return true;
            }
            finally{Monitor.Exit(manager.saveLock);}
        }
        internal bool TryAcquire(out Lease lease)
        {
            lease=null;if(!Monitor.TryEnter(manager.saveLock))return false;bool retained=false;
            try
            {
                lock(state.Gate)if(state.Active!=0||state.Epoch!=epoch)return false;
                if(!QueuesEmpty(manager,key,retired))return false;
                lock(state.Gate)if(state.Active!=0||state.Epoch!=epoch)return false;
                lease=new Lease(this);retained=true;return true;
            }
            finally{if(!retained)Monitor.Exit(manager.saveLock);}
        }
        internal bool Current{get{if(!TryAcquire(out var lease))return false;using(lease)return lease.Current;}}
    }
    // Qualified installed MVID1a9a4203: native DoSaveChunks can Reset/free the
    // old chunk before WriteSnapshot, then frees the snapshot in its finally.
    // Observe that entire native writer and snapshot-producer lifetime, not
    // just the Reset or the publication callback of one selected snapshot.
    [HarmonyPatch]
    private static class CancellationNativeSavePipeline
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {yield return AccessTools.DeclaredMethod(typeof(RegionFileManager),nameof(RegionFileManager.DoSaveChunks),Type.EmptyTypes);
         yield return AccessTools.DeclaredMethod(typeof(RegionFileManager),nameof(RegionFileManager.SaveChunkSnapshot),new[]{typeof(Chunk),typeof(bool)});}
        [HarmonyPriority(Priority.Last)]private static void Prefix(RegionFileManager __instance,bool __runOriginal,out object __state)
        {
            __state=null;if(!__runOriginal)return;
            var state=cancellationNativeSaves.GetValue(__instance,newCancellationNativeSaveState);
            lock(state.Gate){state.Epoch++;state.Active++;}__state=state;
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {
            if(__state is CancellationNativeSaveState state)lock(state.Gate){state.Active--;state.Epoch++;}
            return __exception;
        }
    }
    private static bool CurrentCancellationQueueAfterPayload(CancellationServer original,CancellationNativeEvidence saved)
    {
        var token=original.LoadOriginal;if(token==null||saved?.Payload==null||
            !CancellationNativeSaveStamp.TryCapture(token.Manager,original.Chunk.Key,token.OldChunk,out var stamp)||!stamp.TryAcquire(out var lease))return false;
        using(lease)
        {
            string path=RegionFile.ConstructFullFilePath(token.Manager.saveDirectory,original.Chunk.X>>5,original.Chunk.Z>>5,"7rg");
            using(var stream=SdFile.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                return lease.Current&&RebirthStationRegionPayload.MatchesWrittenPayload(stream,RebirthStationRegionPayload.NativeLocalIndex(original.Chunk.X),RebirthStationRegionPayload.NativeLocalIndex(original.Chunk.Z),saved.Payload)&&lease.Current;
        }
    }
}
