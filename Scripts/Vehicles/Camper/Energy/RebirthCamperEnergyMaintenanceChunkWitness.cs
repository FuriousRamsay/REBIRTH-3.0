using System;
using System.IO;
using System.Linq;

// Reuse native snapshot scheduling and existing final region publication proof.
internal sealed class RebirthCamperEnergyMaintenanceChunkWitness:IDisposable
{
    private readonly TileEntityPowered tile;
    private readonly Func<bool> current;
    private readonly byte[] expected;
    private readonly RebirthStationSnapshotEvidence.Request request;
    private RebirthCamperEnergyMaintenanceChunkWitness(TileEntityPowered te,Func<bool> scope,RebirthStationSnapshotEvidence.Request native)
    {tile=te;current=scope;request=native;expected=RebirthCamperEnergyMaintenanceMarkers.Get(te).Encode();}
    internal static RebirthCamperEnergyMaintenanceChunkWitness TryQueue(World world,TileEntityPowered tile,Func<bool> current)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||tile==null||current==null||!current())return null;
        var chunk=tile.GetChunk();var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(chunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return null;
        string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;var position=tile.ToWorldPos();
        Func<bool> scope=()=>ThreadManager.IsMainThread()&&current()&&ReferenceEquals(GameManager.Instance?.World,world)&&
            ReferenceEquals(world.GetTileEntity(position),tile)&&ReferenceEquals(tile.GetChunk(),chunk)&&ReferenceEquals(provider,world.ChunkCache.ChunkProvider)&&
            ReferenceEquals(regions,provider.m_RegionFileManager)&&ReferenceEquals(access,regions.regionFileAccess)&&Path.GetFullPath(regions.saveDirectory)==directory;
        if(!scope())return null;
        RebirthCamperEnergyMaintenanceMarkers.ResetSpan(tile);
        var request=RebirthStationSnapshotEvidence.Watch(world,chunk,position.x>>4,position.z>>4,scope,tile);
        if(request==null)return null;
        var result=new RebirthCamperEnergyMaintenanceChunkWitness(tile,scope,request);
        try
        {
            lock(chunk){if(!scope()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){result.Dispose();return null;}chunk.InProgressSaving=true;}
            try{if(!scope()){result.Dispose();return null;}regions.SaveChunkSnapshot(chunk,true);}
            finally{lock(chunk)chunk.InProgressSaving=false;}
            return result;
        }
        catch{result.Dispose();return null;}
    }
    internal bool Saved()
    {
        // TryCopy acquires the same evidence gate as worker publication, before
        // reading Published; no unsynchronized early read of its worker flag.
        if(!current()||!RebirthStationSnapshotEvidence.TryCopy(request,out var serialized)||!request.Published||
            !RebirthCamperEnergyMaintenanceMarkers.Get(tile).Encode().SequenceEqual(expected))return false;
        // Exact target extension span in the native serializer frame whose whole
        // compressed payload the existing RegionFileV2 observer read back.
        return RebirthCamperEnergyMaintenanceMarkers.MatchesSnapshot(tile,serialized)&&current();
    }
    public void Dispose(){request.Dispose();}
}
