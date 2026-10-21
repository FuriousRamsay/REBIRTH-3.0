using System;
using System.IO;
using System.Linq;

// Existing conversion removes a native block before spawning its same-identity
// vehicle. This proves that removal's exact native chunk snapshot reached disk.
internal sealed class RebirthVehicleConversionChunkSave:IDisposable
{
    private readonly Func<bool> current;
    private readonly byte[] expected;
    private readonly RebirthStationSnapshotEvidence.Request request;
    private RebirthVehicleConversionChunkSave(Func<bool> scope,byte[] bytes,RebirthStationSnapshotEvidence.Request native)
    {current=scope;expected=bytes;request=native;}
    internal static RebirthVehicleConversionChunkSave Begin(World world,Vector3i position,Chunk chunk,Func<bool> scope)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||chunk==null||scope==null||!scope())return null;
        var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return null;
        string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
        bool Current()=>scope()&&ReferenceEquals(world,GameManager.Instance?.World)&&world.GetBlock(position).isair&&world.GetTileEntity(position)==null&&
            ReferenceEquals(world.GetChunkSync(position.x>>4,position.z>>4),chunk)&&
            ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
        if(!Current())return null;
        RebirthStationSnapshotEvidence.Request request=null;
        try
        {
            byte[] bytes;
            lock(chunk)
            {
                if(!Current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return null;
                using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(false))
                {
                    writer.SetBaseStream(stream);writer.Write((byte)116);writer.Write((byte)116);writer.Write((byte)99);writer.Write((byte)0);writer.Write(47u);
                    chunk.saveBlockIds();chunk.write(writer,false);writer.Flush();if(stream.Length<9||stream.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return null;bytes=stream.ToArray();
                }
                if(!Current())return null;
                request=RebirthStationSnapshotEvidence.Watch(world,chunk,position.x>>4,position.z>>4,Current);
                if(request==null)return null;chunk.InProgressSaving=true;
            }
            try{if(!Current()){request.Dispose();return null;}regions.SaveChunkSnapshot(chunk,true);}
            finally{lock(chunk)chunk.InProgressSaving=false;}
            return new RebirthVehicleConversionChunkSave(Current,bytes,request);
        }
        catch{request?.Dispose();return null;}
    }
    internal bool Saved()=>current()&&RebirthStationSnapshotEvidence.TryCopy(request,out var bytes)&&RebirthStationSnapshotEvidence.HasPublished(request)&&bytes.SequenceEqual(expected)&&current();
    public void Dispose()=>request.Dispose();
}
