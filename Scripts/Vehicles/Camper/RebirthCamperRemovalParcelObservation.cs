using System;
using System.IO;
using System.Threading;

// Removal-bound native Backpack publication evidence. This observer has no
// spawn, inventory or source-retirement capability.
internal sealed class RebirthCamperRemovalParcelObservation:IDisposable
{
    private EntityBackpack body;private World world;private Chunk chunk;private ItemStack[] slots;
    private Func<bool> originalCurrent,storageCurrent;private RebirthStationSnapshotEvidence.Request request;
    private IDisposable allocation;private byte[] native;private string region;private bool closed;
    internal byte[] CopyNative()=>native==null?null:(byte[])native.Clone();
    private bool Current()
    {
        try{return !closed&&ThreadManager.IsMainThread()&&originalCurrent?.Invoke()==true&&
            ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&ReferenceEquals(body?.world,world)&&
            ReferenceEquals(world.GetEntity(body.entityId),body)&&!body.IsMarkedForUnload()&&body.addedToChunk&&
            body.chunkPosAddedEntityTo.x==chunk.X&&body.chunkPosAddedEntityTo.z==chunk.Z&&
            ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)&&!chunk.InProgressUnloading&&
            RebirthCamperEnergyItems.Same(body.bag?.ItemGrid?.items,slots)&&storageCurrent?.Invoke()==true;}
        catch{return false;}
    }
    internal static RebirthCamperRemovalParcelObservation Begin(EntityBackpack accepted,ItemStack[] expected,Func<bool> source,
        IRebirthStationSnapshotByteBudget budget,Action<RebirthStationSnapshotEvidence.EntityImage,string,byte[]> captured)
    {
        if(!ThreadManager.IsMainThread()||accepted?.world is not World world||world.IsRemote()||expected==null||source==null||budget==null||captured==null||
            !accepted.addedToChunk||!ReferenceEquals(GameManager.Instance?.World,world))return null;
        var chunk=world.GetChunkSync(accepted.chunkPosAddedEntityTo.x,accepted.chunkPosAddedEntityTo.z) as Chunk;
        var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(chunk==null||regions?.regionFileAccess is not RegionFileAccessSectorBased||string.IsNullOrEmpty(regions.saveDirectory))return null;
        var result=new RebirthCamperRemovalParcelObservation{body=accepted,world=world,chunk=chunk,slots=ItemStack.Clone(expected),originalCurrent=source};bool handed=false;
        try
        {
            if(!budget.TryReserveAllocation(64L*1024*1024,out result.allocation))return null;
            var native=RebirthLogisticsBackpackPublication.CapturePlanImage(accepted);if(native?.Fits(expected)!=true)return null;
            result.native=native.OriginalFrame();if(result.native==null||result.native.Length>131072)return null;
            string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
            result.region=RegionFile.ConstructFullFilePath(directory,chunk.X>>5,chunk.Z>>5,"7rg");
            result.storageCurrent=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&
                ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
            if(!result.Current()||!Monitor.TryEnter(regions.saveLock,0))return null;
            try
            {
                if(!RebirthLogisticsParcelBaselines.CanAdmitNativeSnapshot(regions))return null;
                result.request=RebirthStationSnapshotEvidence.WatchEntity(world,chunk,chunk.X,chunk.Z,accepted.entityId,accepted.entityClass,result.native,result.Current,budget,
                    image=>{if(!result.Current())throw new InvalidDataException("Removal parcel native capture changed.");captured(image,result.region,result.CopyNative());});
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
    {if(closed)return;closed=true;request?.Dispose();request=null;allocation?.Dispose();allocation=null;body=null;world=null;chunk=null;slots=null;native=null;originalCurrent=null;storageCurrent=null;}
}
