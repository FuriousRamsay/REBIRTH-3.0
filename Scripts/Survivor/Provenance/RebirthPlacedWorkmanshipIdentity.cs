using System;
using System.IO;
using System.Text;
using System.Linq;
using HarmonyLib;

// Own-domain native committed source identity. Native unload is never removal.
internal static class RebirthPlacedWorkmanshipIdentity
{
    internal const string Prefix="rebirth.placed.original.v2:";
    private sealed class Scope{internal WorldBase World;internal Vector3i Position;internal bool Accepted,Upgrade;internal Scope Previous;}
    [ThreadStatic] private static Scope active;
    internal static object BeginPlacement(WorldBase world,Vector3i position){if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||!ThreadManager.IsMainThread())return null;var value=new Scope{World=world,Position=position,Previous=active};active=value;return value;}
    internal static object BeginUpgrade(WorldBase world,Vector3i position){var value=BeginPlacement(world,position) as Scope;if(value!=null)value.Upgrade=true;return value;}
    internal static void EndPlacement(object value){if(value is Scope scope&&ReferenceEquals(scope,active))active=scope.Previous;}
    internal static bool AcceptedUpgrade(WorldBase world,Vector3i position)=>active!=null&&active.Upgrade&&active.Accepted&&ReferenceEquals(active.World,world)&&active.Position==position;
    internal static bool AcceptedPlacement(WorldBase world,Vector3i position)=>active!=null&&!active.Upgrade&&active.Accepted&&ReferenceEquals(active.World,world)&&active.Position==position;
    internal static void Install(Harmony harmony){RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthPlacedWorkmanshipBlockCommit));}
    private static string Key(Vector3i position)=>Prefix+(position.x&15)+":"+position.y+":"+(position.z&15);
    private static Chunk ChunkAt(WorldBase world,Vector3i position)=>world?.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
    private static bool Current(WorldBase world,Chunk chunk)=>ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,world)&&chunk!=null&&!chunk.InProgressUnloading;
    internal static string NativeKey(Vector3i position)=>Key(position);
    internal static byte[] CopyMarker(WorldBase world,Vector3i position){if(Read(world,position)==null)return null;var bytes=ChunkAt(world,position).ChunkCustomData.dict[Key(position)].data;return bytes==null?null:(byte[])bytes.Clone();}
    internal static string Read(WorldBase world,Vector3i position)
    {
        var chunk=ChunkAt(world,position);if(!Current(world,chunk)||!chunk.ChunkCustomData.dict.TryGetValue(Key(position),out var marker)||marker?.data==null||marker.data.Length<19||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null)return null;
        var bytes=marker.data;if(BitConverter.ToUInt16(bytes,16)!=world.GetBlock(position).type)return null;var guidBytes=new byte[16];Array.Copy(bytes,guidBytes,16);var id=new Guid(guidBytes);return id==Guid.Empty?null:id.ToString("N");
    }
    internal static bool Matches(WorldBase world,RebirthPlacedWorkmanshipRecord record)=>record!=null&&!string.IsNullOrEmpty(record.SourceIncarnation)&&Read(world,record.Position)==record.SourceIncarnation;
    private static bool Write(WorldBase world,Vector3i position,string id,RebirthPlacedWorkmanshipRecord record)
    {
        var chunk=ChunkAt(world,position);if(!Current(world,chunk))return false;
        byte[] image=EncodeRecord(id,checked((ushort)world.GetBlock(position).type),record);
        if(image.Length>8192||record!=null&&Decode(image,id,position,world.GetBlock(position).Block.GetBlockName())==null)return false;
        string key=Key(position);if(!chunk.ChunkCustomData.dict.ContainsKey(key)&&chunk.ChunkCustomData.dict.Count>=ushort.MaxValue)return false;chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=image});chunk.isModified=true;return true;
    }
    private static byte[] EncodeRecord(string id,ushort type,RebirthPlacedWorkmanshipRecord record)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {writer.Write(Guid.ParseExact(id,"N").ToByteArray());writer.Write(type);writer.Write(record!=null);if(record!=null){writer.Write(record.ClrIdx);writer.Write(record.BlockName);writer.Write((byte)record.Kind);writer.Write(record.CreatorStableId);writer.Write(record.BackgroundId);writer.Write(record.BonusId);writer.Write(record.SkillId);writer.Write(record.SkillValue);writer.Write(record.Workmanship);writer.Write(record.OriginalBaseMaxDurability);writer.Write(record.CreatedWorldTime);}writer.Flush();return stream.ToArray();}
    }
    internal static string Freeze(RebirthPlacedWorkmanshipRecord record,ushort type)=>Convert.ToBase64String(EncodeRecord(Guid.Empty.ToString("N"),type,record));
    internal static RebirthPlacedWorkmanshipRecord ReadFrozen(string image,Vector3i position,BlockValue target)
    {
        try{var bytes=Convert.FromBase64String(image);if(bytes.Length>8192||!bytes.Take(16).SequenceEqual(Guid.Empty.ToByteArray())||BitConverter.ToUInt16(bytes,16)!=target.type)return null;var r=Decode(bytes,string.Empty,position,target.Block.GetBlockName());return r!=null&&Freeze(r,checked((ushort)target.type))==image?r:null;}catch{return null;}
    }

    internal static void ClearRecord(WorldBase world,Vector3i position){string id=Read(world,position);if(id!=null)Write(world,position,id,null);}
    internal static bool Store(WorldBase world,RebirthPlacedWorkmanshipRecord record)=>Matches(world,record)&&Write(world,record.Position,record.SourceIncarnation,record);
    internal static RebirthPlacedWorkmanshipRecord ReadRecord(WorldBase world,Vector3i position)
    {
        string id=Read(world,position);if(id==null)return null;
        return Decode(ChunkAt(world,position).ChunkCustomData.dict[Key(position)].data,id,position,world.GetBlock(position).Block.GetBlockName());
    }
    private static RebirthPlacedWorkmanshipRecord Decode(byte[] bytes,string id,Vector3i position,string blockName)
    {
        try{if(bytes==null||bytes.Length>8192)return null;using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {stream.Position=18;if(!reader.ReadBoolean())return null;var r=new RebirthPlacedWorkmanshipRecord{SourceIncarnation=id,Position=position,ClrIdx=reader.ReadInt32(),BlockName=reader.ReadString(),Kind=(RebirthPlacedProvenanceKind)reader.ReadByte(),CreatorStableId=reader.ReadString(),BackgroundId=reader.ReadString(),BonusId=reader.ReadString(),SkillId=reader.ReadString(),SkillValue=reader.ReadSingle(),Workmanship=reader.ReadSingle(),OriginalBaseMaxDurability=reader.ReadInt32(),CreatedWorldTime=reader.ReadUInt64()};if(stream.Position!=stream.Length||r.ClrIdx!=0||string.IsNullOrEmpty(r.CreatorStableId)||r.BlockName!=blockName||float.IsNaN(r.SkillValue)||float.IsInfinity(r.SkillValue)||float.IsNaN(r.Workmanship)||float.IsInfinity(r.Workmanship)||r.Workmanship<0||r.Workmanship>1||r.OriginalBaseMaxDurability<0||(r.Kind!=RebirthPlacedProvenanceKind.ConstructionPlacement&&r.Kind!=RebirthPlacedProvenanceKind.TechnicalTrapPlacement))return null;return r;}}
        catch{return null;}
    }
    internal static void Committed(Chunk chunk,WorldBase world,int x,int y,int z,BlockValue before)
    {
        if(!Current(world,chunk))return;var local=new Vector3i(x,y,z);var after=chunk.GetBlock(local);if(before.type==after.type)return;
        var position=chunk.GetWorldPos()+local;string key=Key(position);string previous=null;RebirthPlacedWorkmanshipRecord retained=null;
        bool upgrade=active!=null&&active.Upgrade&&ReferenceEquals(active.World,world)&&active.Position==position&&before.Block!=null&&!before.Block.UpgradeBlock.isair;
        if(upgrade&&chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)&&marker?.data?.Length>=19&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null){var bytes=new byte[16];Array.Copy(marker.data,bytes,16);var guid=new Guid(bytes);previous=guid==Guid.Empty?null:guid.ToString("N"); // Read original record against its previous native type.
            ushort currentType=BitConverter.ToUInt16(marker.data,16);if(currentType==before.type)retained=Decode(marker.data,previous,position,before.Block.GetBlockName());}
        if(chunk.ChunkCustomData.dict.TryGetValue(key,out var retiring)&&retiring?.data?.Length>=19){var bytes=new byte[16];Array.Copy(retiring.data,bytes,16);RebirthPlacedOriginal.RetireIfTerminal(world,new Guid(bytes));}
        RebirthPlacedWorkmanshipService.NativeReplaced(world,position,before,after,upgrade,previous);
        if(chunk.ChunkCustomData.dict.ContainsKey(key)){chunk.ChunkCustomData.Remove(key);chunk.isModified=true;}
        if(after.isair||after.ischild||active==null||!ReferenceEquals(active.World,world)||active.Position!=position)return;
        string id=upgrade&&previous!=null?previous:RebirthPlacedOriginal.HostSourceBirth(world,position)??Guid.NewGuid().ToString("N");if(retained!=null){retained.SourceIncarnation=id;retained.BlockName=after.Block.GetBlockName();retained.OriginalBaseMaxDurability=Math.Max(0,after.Block.MaxDamage);}
        if(Write(world,position,id,retained)&&active!=null&&ReferenceEquals(active.World,world)&&active.Position==position){active.Accepted=true;RebirthPlacedOriginal.OnHostNativeCommit(world,position,after);}
    }

}
[HarmonyPatch(typeof(Chunk),"SetBlock",new[]{typeof(WorldBase),typeof(int),typeof(int),typeof(int),typeof(BlockValue),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(int)})]
internal static class RebirthPlacedWorkmanshipBlockCommit
{
    private static void Prefix(Chunk __instance,int __1,int __2,int __3,out BlockValue __state)=>__state=__instance.GetBlock(new Vector3i(__1,__2,__3));
    private static void Postfix(Chunk __instance,WorldBase __0,int __1,int __2,int __3,BlockValue __state)=>RebirthPlacedWorkmanshipIdentity.Committed(__instance,__0,__1,__2,__3,__state);
    private static Exception Finalizer(Chunk __instance,WorldBase __0,int __1,int __2,int __3,BlockValue __state,Exception __exception){if(__exception!=null)try{RebirthPlacedWorkmanshipIdentity.Committed(__instance,__0,__1,__2,__3,__state);}catch{}return __exception;}
}
