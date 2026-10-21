using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using HarmonyLib;

internal static class RebirthPlacedOriginalPublication
{
    private sealed class Pending{internal World World;internal string Directory;internal RebirthPlacedOriginal.Record Original;internal Chunk Chunk;internal RebirthStationSnapshotEvidence.Request Watch;internal Func<bool> ProviderCurrent;internal float Started;internal byte[] SourceImage,ElectricalImage,TileImage;internal TileEntity Tile;internal string VerifiedProof;}
    private static readonly Dictionary<Guid,Pending> pending=new Dictionary<Guid,Pending>();
    private static string Key(RebirthPlacedOriginal.Record r)=>"rebirth.placed.receipt.v1:"+r.Operation.ToString("N");
    private static byte[] StampImage(RebirthPlacedOriginal.Record r)=>Encoding.UTF8.GetBytes(r.Operation.ToString("N")+"|"+RebirthPlacedOriginal.Digest(r)+"|"+r.TargetBirth+"|"+r.Changes+"|"+r.Provenance+(r.Electrical.Length==0?string.Empty:"|electrical:"+r.Electrical));
    internal static void Install(Harmony harmony){foreach(var type in new[]{typeof(RebirthStationSnapshotUpdatePatch),typeof(RebirthStationChunkSerializedPatch),typeof(RebirthStationSnapshotResetPatch),typeof(RebirthStationSnapshotWritePatch),typeof(RebirthStationRegionPublishedPatch),typeof(RebirthPlacedOriginalPoweredSerialized)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);}
    internal static bool Stamp(World world,RebirthPlacedOriginal.Record r)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||r.Phase!=3||!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var changes)||changes.Count!=1||world.GetBlock(r.Position).rawData!=RebirthPlacedOriginal.ExpectedBlock(r).rawData||RebirthPlacedWorkmanshipIdentity.Read(world,r.Position)!=r.Operation.ToString("N"))return false;
        var chunk=world.GetChunkSync(World.toChunkXZ(r.X),World.toChunkXZ(r.Z)) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;byte[] image=StampImage(r);if(image.Length>ushort.MaxValue)return false;
        string key=Key(r);if(chunk.ChunkCustomData.dict.TryGetValue(key,out var old))return old?.data!=null&&old.data.SequenceEqual(image)&&old.expiresInWorldTime==ulong.MaxValue&&!old.isSavedToNetwork&&old.TriggerWriteDataDelegate==null;
        if(chunk.ChunkCustomData.dict.Count>=ushort.MaxValue)return false;chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=image});chunk.isModified=true;return true;
    }
    private static bool Current(Pending p)
    {
        var r=p.Original;if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,p.World)||p.World.IsRemote()||p.World.worldState.Guid!=r.World||GameIO.GetSaveGameDir()!=p.Directory||!ReferenceEquals(p.World.GetChunkSync(p.Chunk.X,p.Chunk.Z),p.Chunk)||p.Chunk.InProgressUnloading||p.ProviderCurrent!=null&&!p.ProviderCurrent()||RebirthPlacedWorkmanshipIdentity.Read(p.World,r.Position)!=r.Operation.ToString("N")||!p.SourceImage.SequenceEqual(RebirthPlacedWorkmanshipIdentity.CopyMarker(p.World,r.Position)??Array.Empty<byte>())||!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var changes)||p.World.GetBlock(r.Position).rawData!=RebirthPlacedOriginal.ExpectedBlock(r).rawData)return false;
        if(p.Tile!=null&&!RebirthPlacedOriginalPowered.Current(p.World,r.Position,p.Tile,p.TileImage)||p.Tile==null&&p.World.GetTileEntity(r.Position)!=null)return false;
        if(r.Electrical.Length!=0&&(RebirthElectricalSourceIdentity.Read(p.World,r.Position)!=r.Operation.ToString("N")||!RebirthPlacedOriginalPowered.Current(p.World,r.Position,p.Tile,p.TileImage)||!p.ElectricalImage.SequenceEqual(RebirthElectricalSourceIdentity.CopyMarker(p.World,r.Position)??Array.Empty<byte>())))return false;
        return p.Chunk.ChunkCustomData.dict.TryGetValue(Key(r),out var marker)&&marker?.data!=null&&marker.data.SequenceEqual(StampImage(r))&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null;
    }
    internal static bool Observe(World world,RebirthPlacedOriginal.Record r,out string proof)
    {
        proof=null;
        try
        {
            if(!pending.TryGetValue(r.Operation,out var p))
            {
                if(pending.Count>=4)return false;var chunk=world.GetChunkSync(World.toChunkXZ(r.X),World.toChunkXZ(r.Z)) as Chunk;var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
                if(chunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased))return false;var access=regions.regionFileAccess;string directory=Path.GetFullPath(regions.saveDirectory);
                p=new Pending{World=world,Directory=GameIO.GetSaveGameDir(),Original=r,Chunk=chunk,SourceImage=RebirthPlacedWorkmanshipIdentity.CopyMarker(world,r.Position),ProviderCurrent=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory};if(r.Electrical.Length!=0){p.Tile=world.GetTileEntity(r.Position) as TileEntityPowered;p.TileImage=RebirthPlacedOriginalPowered.Image(p.Tile);p.ElectricalImage=RebirthElectricalSourceIdentity.CopyMarker(world,r.Position);if(p.TileImage==null||p.ElectricalImage==null)return false;}if(r.Electrical.Length==0&&RebirthPlacedOriginal.ExpectedBlock(r).Block is BlockLight){p.Tile=world.GetTileEntity(r.Position);if(p.Tile!=null){if(!(p.Tile is TileEntityLight))return false;p.TileImage=RebirthPlacedOriginalPowered.Image(p.Tile);if(p.TileImage==null)return false;}}if(p.SourceImage==null||!Current(p))return false;pending.Add(r.Operation,p);
            }
            if(!ReferenceEquals(world,p.World)||p.Original.Changes!=r.Changes)return false;if(p.VerifiedProof!=null){proof=p.VerifiedProof;return true;}if(p.ProviderCurrent?.Invoke()!=true){Complete(r.Operation);return false;}
            if(p.Watch!=null)
            {
                if(RebirthStationSnapshotEvidence.HasPublished(p.Watch)&&RebirthStationSnapshotEvidence.TryCopy(p.Watch,out var bytes)&&RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var changes)&&TrySnapshot(bytes,p.Chunk.X,p.Chunk.Z,r.Position,RebirthPlacedOriginal.ExpectedBlock(r).rawData,Key(r),StampImage(r),out proof,RebirthPlacedWorkmanshipIdentity.NativeKey(r.Position),p.SourceImage,changes[0].bChangeDensity?(int?)changes[0].density:null,r.Electrical.Length==0?null:RebirthElectricalSourceIdentity.NativeKey(r.Position),p.ElectricalImage)){p.VerifiedProof=proof;p.Watch.Dispose();p.Watch=null;return true;}
                if(UnityEngine.Time.realtimeSinceStartup-p.Started>15f){p.Watch.Dispose();p.Watch=null;}else return false;
            }
            if(!Current(p)){Complete(r.Operation);return false;}p.Watch=RebirthStationSnapshotEvidence.Watch(world,p.Chunk,p.Chunk.X,p.Chunk.Z,()=>Current(p),p.Tile,p.Tile==null?null:(Func<byte[],byte[],bool>)((first,last)=>first.SequenceEqual(p.TileImage)&&last.SequenceEqual(p.TileImage)));if(p.Watch==null)return false;p.Started=UnityEngine.Time.realtimeSinceStartup;
            lock(p.Chunk){if(!Current(p)||p.Chunk.InProgressSaving||p.Chunk.IsLocked||p.Chunk.NeedsDecoration||p.Chunk.InProgressDecorating||p.Chunk.NeedsLightCalculation||p.Chunk.InProgressLighting){p.Watch.Dispose();p.Watch=null;return false;}p.Chunk.InProgressSaving=true;}
            try{((ChunkProviderGenerateWorld)world.ChunkCache.ChunkProvider).m_RegionFileManager.SaveChunkSnapshot(p.Chunk,true);}finally{lock(p.Chunk)p.Chunk.InProgressSaving=false;}return false;
        }
        catch{if(pending.TryGetValue(r.Operation,out var p)){p.Watch?.Dispose();p.Watch=null;}return false;}
    }
    internal static bool RetireMarker(World world,RebirthPlacedOriginal.Record r)
    {
        if(r.Phase!=5||!RebirthPlacedOriginal.HasSavedTerminal(world,r.Operation,RebirthPlacedOriginal.Digest(r),r.Position,r.TargetBirth))return false;
        var chunk=world.GetChunkSync(World.toChunkXZ(r.X),World.toChunkXZ(r.Z)) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;
        string key=Key(r);if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))return true;if(marker?.data==null||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null||!marker.data.SequenceEqual(StampImage(r)))return false;chunk.ChunkCustomData.Remove(key);chunk.isModified=true;return true;
    }
    internal static bool HasStamp(WorldBase world,Vector3i position,Guid operation){var chunk=world?.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;return chunk!=null&&!chunk.InProgressUnloading&&chunk.ChunkCustomData.dict.ContainsKey("rebirth.placed.receipt.v1:"+operation.ToString("N"));}
    internal static void Complete(Guid operation){if(pending.TryGetValue(operation,out var p)){p.Watch?.Dispose();pending.Remove(operation);}}
    internal static void Retire(World world){foreach(var p in pending.Values.Where(value=>ReferenceEquals(value.World,world)).ToArray())Complete(p.Original.Operation);}
    // Exact v47 declared block layers and custom-data entries. The original
    // receipt and block postimage must occur in the SAME final native snapshot.
    internal static bool TrySnapshot(byte[] bytes,int x,int z,Vector3i position,uint expected,string key,byte[] stamp,out string proof,string sourceKey=null,byte[] sourceStamp=null,int? expectedDensity=null,string electricalKey=null,byte[] electricalStamp=null)
    {
        proof=null;
        try
        {
            if(Chunk.CurrentSaveVersion!=47||bytes==null||bytes.Length<28||bytes.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                if(reader.ReadUInt32()!=0x00637474U||reader.ReadUInt32()!=47||reader.ReadInt32()!=x||reader.ReadInt32()!=0||reader.ReadInt32()!=z||World.toChunkXZ(position.x)!=x||World.toChunkXZ(position.z)!=z||position.y<0||position.y>=256)return false;reader.ReadUInt64();uint block=0;int layer=position.y>>2,offset=(position.x&15)+((position.z&15)<<4)+(position.y&3)*256;
                for(int i=0;i<64;i++)
                {
                    if(!Flag(reader))continue;bool varied=Flag(reader);byte[] lower=Read(reader,varied?1024:1);bool upperPresent=Flag(reader);byte[] upper=upperPresent?Read(reader,3072):null;if(i==layer){block=lower[varied?offset:0];if(upper!=null)block|=(uint)(upper[offset*3]<<8|upper[offset*3+1]<<16|upper[offset*3+2]<<24);}
                }
                if(block!=expected)return false;Channel(reader,1);Read(reader,256+256+32+256+1536+2);int count=reader.ReadUInt16();bool found=false,sourceFound=false,electricalFound=false;
                for(int i=0;i<count;i++){string name=reader.ReadString();if(name.Length>4096)return false;ulong expires=reader.ReadUInt64();bool network=Flag(reader);byte[] data=Read(reader,reader.ReadUInt16());if(electricalKey!=null&&name==electricalKey){if(electricalFound||electricalStamp==null||expires!=ulong.MaxValue||network||!data.SequenceEqual(electricalStamp))return false;electricalFound=true;}if(sourceKey!=null&&name==sourceKey){if(sourceFound||sourceStamp==null||expires!=ulong.MaxValue||network||!data.SequenceEqual(sourceStamp))return false;sourceFound=true;}if(name==key){if(found||expires!=ulong.MaxValue||network||!data.SequenceEqual(stamp))return false;found=true;}}
                if(!found||sourceKey!=null&&!sourceFound||electricalKey!=null&&!electricalFound)return false;
                if(expectedDensity.HasValue){Read(reader,768);if(ChannelValue(reader,position)!=unchecked((byte)expectedDensity.Value))return false;}
                using(var hash=SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();return true;
            }
        }
        catch{return false;}
    }
    private static byte ChannelValue(BinaryReader reader,Vector3i position)
    {int layer=position.y>>2,offset=(position.x&15)+((position.z&15)<<4)+(position.y&3)*256;byte value=0;for(int i=0;i<64;i++){bool uniform=Flag(reader);var bytes=Read(reader,uniform?1:1024);if(i==layer)value=bytes[uniform?0:offset];}return value;}
    private static bool Flag(BinaryReader reader){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static byte[] Read(BinaryReader reader,int count){if(count<0||count>reader.BaseStream.Length-reader.BaseStream.Position)throw new EndOfStreamException();var bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return bytes;}
    private static void Channel(BinaryReader reader,int size){for(int i=0;i<64;i++)Read(reader,Flag(reader)?size:1024*size);}
}
