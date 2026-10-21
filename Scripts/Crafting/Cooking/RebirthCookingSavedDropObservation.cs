using System;
using System.IO;
using System.Threading;

// Caller owns the immutable per-output original and the native invocation fence.
// This class never spawns, grants, removes source or retries the drop invocation.
internal sealed class RebirthCookingSavedDropObservation:IDisposable
{
    private EntityPlayer owner;private EntityItem item;private World world;private Chunk chunk;
    private ItemStack expected;private Func<bool> originalCurrent,storageCurrent;
    private int deaths;private string creation;private bool closed;
    private RebirthStationSnapshotEvidence.Request request;
    private IDisposable nativeAllocation;
    private byte[] nativeFrame;
    private string regionPath;
    internal byte[] CopyNativeFrame()=>nativeFrame==null?null:(byte[])nativeFrame.Clone();
    internal bool SnapshotImage(out RebirthStationSnapshotEvidence.EntityImage image,out string path)
    {path=null;image=null;if(!Current()||!RebirthStationSnapshotEvidence.TryGetSerializedEntity(request,out image))return false;path=regionPath;return path!=null&&Current();}
    private bool Current()
    {
        try{return !closed&&ThreadManager.IsMainThread()&&originalCurrent?.Invoke()==true&&
            ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&
            ReferenceEquals(owner.world,world)&&ReferenceEquals(world.GetEntity(owner.entityId),owner)&&
            (owner is not EntityPlayerLocal||ReferenceEquals(world.GetPrimaryPlayer(),owner))&&owner.IsSpawned()&&!owner.IsDead()&&
            owner.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(owner)==creation&&
            ReferenceEquals(world.GetEntity(item.entityId),item)&&ReferenceEquals(item.world,world)&&!item.IsMarkedForUnload()&&
            item.belongsPlayerId==owner.entityId&&item.addedToChunk&&item.chunkPosAddedEntityTo.x==chunk.X&&item.chunkPosAddedEntityTo.z==chunk.Z&&
            ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)&&!chunk.InProgressUnloading&&
            RebirthStationGridIngredients.IsSameStackSnapshot(item.itemStack,expected)&&storageCurrent?.Invoke()==true;}
        catch{return false;}
    }
    private sealed class BoundedStream:MemoryStream
    {
        private const int Maximum=1024*1024;
        public override void Write(byte[] bytes,int offset,int count){if(count<0||Position>Maximum-count)throw new InvalidDataException("Native cooking drop frame exceeds bound.");base.Write(bytes,offset,count);}
        public override void WriteByte(byte value){if(Position>=Maximum)throw new InvalidDataException("Native cooking drop frame exceeds bound.");base.WriteByte(value);}
        public override void SetLength(long value){if(value<0||value>Maximum)throw new InvalidDataException();base.SetLength(value);}
    }
    internal static RebirthCookingSavedDropObservation Begin(EntityPlayer actor,EntityItem accepted,ItemStack frozenOutput,
        Func<bool> currentOriginal,IRebirthStationSnapshotByteBudget budget,Action<RebirthStationSnapshotEvidence.EntityImage,string,byte[]> captured=null)
    {
        if(!ThreadManager.IsMainThread()||actor?.world is not World world||world.IsRemote()||accepted==null||
            frozenOutput==null||frozenOutput.IsEmpty()||frozenOutput.count<1||currentOriginal==null||budget==null||
            !accepted.addedToChunk||!ReferenceEquals(GameManager.Instance?.World,world))return null;
        var chunk=world.GetChunkSync(accepted.chunkPosAddedEntityTo.x,accepted.chunkPosAddedEntityTo.z) as Chunk;
        var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(chunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return null;
        var result=new RebirthCookingSavedDropObservation{owner=actor,item=accepted,world=world,chunk=chunk,
            deaths=actor.Died,creation=RebirthCamperCharacterAccess.CurrentCreation(actor),originalCurrent=currentOriginal};
        bool handed=false;
        try
        {
            if(string.IsNullOrEmpty(result.creation)||!budget.TryReserveAllocation(64L*1024*1024,out result.nativeAllocation))return null;
            // Independent native hydration keeps the caller's original and the
            // spawned item's mutable object graph out of the evidence snapshot.
            long remaining=1024*1024;
            byte[] stack=RemoteResourceTransactions.CaptureOriginalNativeFrame(new[]{frozenOutput},ref remaining);
            var restored=RebirthCamperInventorySnapshot.Restore(stack);
            if(restored==null||restored.Length!=1)return null;result.expected=restored[0];
            string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
            result.regionPath=RegionFile.ConstructFullFilePath(directory,chunk.X>>5,chunk.Z>>5,"7rg");
            result.storageCurrent=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&
                ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
            if(!result.Current()||!Monitor.TryEnter(regions.saveLock,0))return null;
            try
            {
                if(!RebirthLogisticsParcelBaselines.CanAdmitNativeSnapshot(regions))return null;
                byte[] frame;EntityCreationData native=null;
                try
                {
                    native=new EntityCreationData(accepted,StreamModeWrite.Persistency);
                    using(var stream=new BoundedStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
                    {writer.SetBaseStream(stream);native.write(writer,StreamModeWrite.Persistency);writer.Flush();frame=stream.ToArray();}
                }
                finally{native?.entityData?.Dispose();native?.bagData?.Dispose();}
                if(!result.Current())return null;result.nativeFrame=frame;
                result.request=RebirthStationSnapshotEvidence.WatchEntity(world,chunk,chunk.X,chunk.Z,accepted.entityId,accepted.entityClass,frame,result.Current,budget,
                    image=>{if(!result.Current())throw new InvalidDataException("Cooking drop capture scope changed.");captured?.Invoke(image,result.regionPath,result.CopyNativeFrame());});
                if(result.request==null)return null;
                lock(chunk)
                {
                    if(!result.Current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return null;
                    chunk.InProgressSaving=true;
                }
                try{if(!result.Current())return null;regions.SaveChunkSnapshot(chunk,true);}
                finally{lock(chunk)chunk.InProgressSaving=false;}
                if(!result.Current()||!RebirthLogisticsParcelBaselines.NativeQueueFits(regions))return null;
                handed=true;return result;
            }
            finally{Monitor.Exit(regions.saveLock);}
        }
        catch{return null;}
        finally{if(!handed)result.Dispose();}
    }
    internal bool Saved(out RebirthStationSnapshotEvidence.PublishedSnapshot proof)
    {proof=null;return Current()&&RebirthStationSnapshotEvidence.TryGetPublishedEntity(request,out proof)&&Current();}
    public void Dispose()
    {
        if(closed)return;closed=true;request?.Dispose();request=null;nativeAllocation?.Dispose();nativeAllocation=null;
        owner=null;item=null;world=null;chunk=null;expected=null;nativeFrame=null;regionPath=null;originalCurrent=null;storageCurrent=null;
    }
}
