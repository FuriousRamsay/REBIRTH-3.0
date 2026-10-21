using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

// Positive native world publication for the existing restoration coordinator.
// Scheduling alone is never a commit receipt. No owner mutation occurs here.
internal sealed class RebirthVehicleAssemblySave:IDisposable
{
    private sealed class Span {internal long Offset=-1;internal byte[] Bytes;}
    private static readonly ConditionalWeakTable<TileEntityRepairableVehicleRebirth,Span> spans=new ConditionalWeakTable<TileEntityRepairableVehicleRebirth,Span>();
    private readonly Func<bool> current;
    private readonly string expected;
    private readonly EntityVehicle entity;
    private readonly TileEntityRepairableVehicleRebirth tile;
    private RebirthCamperSavedVehicle.Expected vehicle;
    private RebirthStationSnapshotEvidence.Request chunk;
    private bool disposed;
    private RebirthVehicleAssemblySave(Func<bool> scope,string payload,EntityVehicle e,TileEntityRepairableVehicleRebirth t)
    {current=scope;expected=payload;entity=e;tile=t;}
    internal static void Serialized(TileEntityRepairableVehicleRebirth tile,Stream stream,long offset,byte[] payload)
    {
        if(offset<0||stream==null||payload==null||!spans.TryGetValue(tile,out var span))return;
        using(var buffer=new MemoryStream())using(var writer=new BinaryWriter(buffer))
        {writer.Write(payload.Length);writer.Write(payload);writer.Flush();lock(span){span.Bytes=buffer.ToArray();span.Offset=offset;}}
    }
    internal static RebirthVehicleAssemblySave Begin(World world,RebirthVehicleTransferPlan plan,Func<bool> scope,string requiredPayload=null)
    {
        string payload=requiredPayload??plan?.AfterPayload;
        if(!ThreadManager.IsMainThread()||scope==null||!scope()||plan==null||world==null||world.IsRemote()||
            !RebirthVehicleTransferResolver.TryObserve(world,plan,scope,out var assembly,out var position,out var id)||
            RebirthVehicleAssemblySerializer.ToBase64(assembly)!=payload)return null;
        var e=id>=0?world.GetEntity(id) as EntityVehicle:null;
        var t=id<0?world.GetTileEntity(position) as TileEntityRepairableVehicleRebirth:null;
        var result=new RebirthVehicleAssemblySave(scope,payload,e,t);
        try
        {
            if(e!=null){result.vehicle=RebirthCamperSavedVehicle.RequestSave(e);return scope()?result:DisposeFailed(result);}
            var nativeChunk=t?.GetChunk();var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(nativeChunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return DisposeFailed(result);
            string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
            Func<bool> exact=()=>scope()&&ReferenceEquals(world,GameManager.Instance?.World)&&ReferenceEquals(world.GetTileEntity(position),t)&&
                ReferenceEquals(t.GetChunk(),nativeChunk)&&ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&
                ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
            var span=spans.GetOrCreateValue(t);lock(span){span.Offset=-1;span.Bytes=null;}
            result.chunk=RebirthStationSnapshotEvidence.Watch(world,nativeChunk,position.x>>4,position.z>>4,exact,t);
            if(result.chunk==null)return DisposeFailed(result);
            lock(nativeChunk)
            {
                if(!exact()||nativeChunk.IsLocked||nativeChunk.InProgressSaving||nativeChunk.NeedsDecoration||nativeChunk.InProgressDecorating||nativeChunk.NeedsLightCalculation||nativeChunk.InProgressLighting)return DisposeFailed(result);
                nativeChunk.InProgressSaving=true;
            }
            try{if(!exact())return DisposeFailed(result);regions.SaveChunkSnapshot(nativeChunk,true);}
            finally{lock(nativeChunk)nativeChunk.InProgressSaving=false;}
            return result;
        }
        catch{result.Dispose();return null;}
    }
    private static RebirthVehicleAssemblySave DisposeFailed(RebirthVehicleAssemblySave result){result.Dispose();return null;}
    internal bool Saved()
    {
        if(disposed||!ThreadManager.IsMainThread()||!current())return false;
        if(entity!=null)
            return RebirthVehicleAssemblyIndex.TryGetEntity(entity,out var live)&&RebirthVehicleAssemblySerializer.ToBase64(live)==expected&&
                RebirthCamperSavedVehicle.Verify(entity,vehicle,current)&&current();
        if(tile==null||chunk==null||!spans.TryGetValue(tile,out var span)||RebirthVehicleAssemblySerializer.ToBase64(tile.Assembly)!=expected)return false;
        long offset;byte[] bytes;lock(span){offset=span.Offset;bytes=span.Bytes;}
        byte[] payload=Convert.FromBase64String(expected);
        return offset>=0&&bytes!=null&&bytes.Length==payload.Length+4&&bytes.Skip(4).SequenceEqual(payload)&&
            BitConverter.ToInt32(bytes,0)==payload.Length&&RebirthStationSnapshotEvidence.TryValidatePublishedSnapshotSpans(chunk,new[]{offset},new[]{bytes},out _)&&current()&&
            RebirthVehicleAssemblySerializer.ToBase64(tile.Assembly)==expected;
    }
    public void Dispose(){if(disposed)return;disposed=true;vehicle?.Dispose();vehicle=null;chunk?.Dispose();chunk=null;}
}
