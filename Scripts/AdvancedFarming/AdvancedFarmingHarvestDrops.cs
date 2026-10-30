using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;

#nullable disable

// Original overflow only. Native ItemValue bytes are never stamped or changed.
internal static class AdvancedFarmingHarvestDrops
{
    internal sealed class Proof {internal Guid Operation,Source;internal string World,OutputDigest,NativeDigest;internal long Generation;}
    private sealed class Drop
    {
        public int Version=1,EntityId,Index;public string Operation,Parent,Source,World,Output,Before,Process,Native;public ulong Born;public long Generation;public bool Published;public float X,Y,Z;
    }
    private sealed class Attempt
    {internal UnityEngine.Vector3 Position;internal Guid Operation;internal World World;internal string Directory;internal AdvancedFarmingHarvestReceipt.Record Receipt;internal Drop Drop;internal EntityItem Entity;internal bool Kinematic;internal AdvancedFarmingHarvestPublication.Proof Before,After;}
    private static readonly string Process=Guid.NewGuid().ToString("N");
    private static readonly Dictionary<Guid,Attempt> Attempts=new Dictionary<Guid,Attempt>();
    private static readonly Dictionary<EntityItem,Attempt> HeldEntities=new Dictionary<EntityItem,Attempt>();
    [ThreadStatic] private static Attempt spawning;
    private const string Prefix="RebirthHarvestDropV1:";
    private static string Key(Guid operation)=>Prefix+operation.ToString("N");
    private static string DirectoryPath()=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","farming-harvest","v1","drops");
    private static string PathFor(Guid operation)=>Path.Combine(DirectoryPath(),operation.ToString("N")+".json");
    private static Vector3i Cell(Attempt a)=>new Vector3i(UnityEngine.Mathf.FloorToInt(a.Position.x),UnityEngine.Mathf.FloorToInt(a.Position.y),UnityEngine.Mathf.FloorToInt(a.Position.z));
    private static Chunk Chunk(World world,Vector3i cell)=>world.GetChunkSync(World.toChunkXZ(cell.x),World.toChunkXZ(cell.z)) as Chunk;
    private static string Encode(Drop value)=>JsonConvert.SerializeObject(value,Formatting.None);
    private static Drop Read(Guid operation)
    {
        string path=PathFor(operation);if(!File.Exists(path)){if(File.Exists(path+".bak"))throw new InvalidDataException("Missing original drop primary.");return null;}
        if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException();string image=File.ReadAllText(path,new UTF8Encoding(false,true));var r=JsonConvert.DeserializeObject<Drop>(image);
        if(r==null||r.Version!=1||r.Operation!=operation.ToString("N")||!Guid.TryParseExact(r.Parent,"N",out _)||r.Index<0||r.Index>127||float.IsNaN(r.X)||float.IsInfinity(r.X)||float.IsNaN(r.Y)||float.IsInfinity(r.Y)||float.IsNaN(r.Z)||float.IsInfinity(r.Z)||!Guid.TryParseExact(r.Source,"N",out _)||!Guid.TryParse(r.World,out _)||!Digest(r.Before)||r.EntityId<0||r.Generation<0||r.EntityId>0&&!Guid.TryParseExact(r.Process,"N",out _)||r.Published&&(r.EntityId==0||r.Generation<=0||!Digest(r.Native))||Encode(r)!=image)throw new InvalidDataException();
        AdvancedFarmingHarvestReceipt.ReadOutputs(r.Output);return r;
    }
    private static bool Digest(string value){if(value==null||value.Length!=64)return false;foreach(char c in value)if(!(c>='0'&&c<='9')&&!(c>='a'&&c<='f'))return false;return true;}
    private static bool Save(Drop value,string expected=null)
    {
        string path=PathFor(Guid.ParseExact(value.Operation,"N")),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            if(File.Exists(path)||File.Exists(path+".bak"))
            {
                var prior=Read(Guid.ParseExact(value.Operation,"N"));if(prior==null)return false;
                if(expected==null)return Encode(prior)==Encode(value);
                if(Encode(prior)!=expected)return false;
            }
            else if(expected!=null)return false;
            Directory.CreateDirectory(DirectoryPath());var bytes=new UTF8Encoding(false,true).GetBytes(Encode(value));
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            if(File.ReadAllText(temp,new UTF8Encoding(false,true))!=Encode(value))return false;
            return RebirthDurableFileCommit.TryPublish(temp,path,out _);
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original overflow retained: "+error.GetType().Name);return false;}
    }
    private static Guid Observation(Guid operation,string role)
    {using(var hash=System.Security.Cryptography.SHA256.Create()){var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(operation.ToString("N")+role));var id=new byte[16];Array.Copy(bytes,id,16);id[0]|=128;return new Guid(id);}}
    private static UnityEngine.Vector3 Position(World world,AdvancedFarmingHarvestReceipt.Record receipt,ItemStack output,Drop drop)
    {
        var center=AdvancedFarmingHarvestTerminalPlot.DropPosition(receipt,output,out var extent);
        if(drop!=null)
        {
            if(Math.Abs(drop.X-center.x)>extent.x+0.00001f||Math.Abs(drop.Y-center.y)>extent.y+0.00001f||Math.Abs(drop.Z-center.z)>extent.z+0.00001f)throw new InvalidDataException();
            return new UnityEngine.Vector3(drop.X,drop.Y,drop.Z);
        }
        // Native ItemDropServer's third argument is RANDOM POSITION extent,
        // not velocity. Roll it once before the baseline save, then durably seal
        // the chosen cell/position before spawning with zero additional jitter.
        if(extent.x!=0f||extent.y!=0f||extent.z!=0f)return center+new UnityEngine.Vector3(world.RandomRange(-extent.x,extent.x),world.RandomRange(-extent.y,extent.y),world.RandomRange(-extent.z,extent.z));
        return center;
    }
    private static bool Current(Attempt a)=>ReferenceEquals(GameManager.Instance?.World,a.World)&&GameIO.GetSaveGameDir()==a.Directory&&!a.World.IsRemote()&&a.World.worldState?.Guid==a.Receipt.World;
    private static bool Exact(Attempt a,EntityItem entity)=>entity!=null&&ReferenceEquals(entity.world,a.World)&&entity.entityId==a.Drop.EntityId&&entity.WorldTimeBorn==a.Drop.Born&&AdvancedFarmingHarvestReceipt.OutputImage(new[]{entity.itemStack})==a.Drop.Output;
    private static bool Marker(Chunk chunk,Guid operation,Drop drop)
    {
        if(chunk==null||!chunk.ChunkCustomData.dict.TryGetValue(Key(operation),out var entry)||entry?.data?.Length!=60)return false;
        using(var stream=new MemoryStream(entry.data,false))using(var reader=new BinaryReader(stream))
        {return new Guid(reader.ReadBytes(16))==operation&&reader.ReadInt32()==drop.EntityId&&reader.ReadUInt64()==drop.Born&&BitConverter.ToString(reader.ReadBytes(32)).Replace("-","").ToLowerInvariant()==AdvancedFarmingHarvestReceipt.OutputDigest(drop.Output);}
    }
    private static void WriteMarker(Attempt a)
    {
        var chunk=Chunk(a.World,Cell(a));if(chunk==null||chunk.InProgressUnloading)throw new InvalidOperationException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {writer.Write(a.Operation.ToByteArray());writer.Write(a.Drop.EntityId);writer.Write(a.Drop.Born);string digest=AdvancedFarmingHarvestReceipt.OutputDigest(a.Drop.Output);for(int i=0;i<32;i++)writer.Write(Convert.ToByte(digest.Substring(i*2,2),16));writer.Flush();string key=Key(a.Operation);chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=stream.ToArray()});chunk.isModified=true;}
    }
    private static void Hold(Attempt a,EntityItem entity)
    {a.Entity=entity;if(!HeldEntities.ContainsKey(entity)){a.Kinematic=entity.itemRB!=null&&entity.itemRB.isKinematic;HeldEntities.Add(entity,a);}if(entity.itemRB!=null)entity.itemRB.isKinematic=true;}
    internal static bool Held(Entity entity)=>entity is EntityItem item&&HeldEntities.TryGetValue(item,out var a)&&Current(a)&&!a.Drop.Published;
    internal static bool Capture(Entity entity)
    {
        if(entity is not EntityItem item)return true;
        if(spawning!=null)
        {
            var a=spawning;if(!Current(a)||item.entityId<=0||AdvancedFarmingHarvestReceipt.OutputImage(new[]{item.itemStack})!=a.Drop.Output)return false;
            string before=Encode(a.Drop);a.Drop.EntityId=item.entityId;a.Drop.Born=item.WorldTimeBorn;a.Drop.Process=Process;
            if(!Save(a.Drop,before)){a.Drop=JsonConvert.DeserializeObject<Drop>(before);return false;}WriteMarker(a);Hold(a,item);return true;
        }
        // Native loaded item may become collectable before the first GameUpdate.
        var world=GameManager.Instance?.World;if(world==null||world.IsRemote())return true;
        var cell=new Vector3i(UnityEngine.Mathf.FloorToInt(item.position.x),UnityEngine.Mathf.FloorToInt(item.position.y),UnityEngine.Mathf.FloorToInt(item.position.z));var chunk=Chunk(world,cell);if(chunk==null)return true;
        foreach(string key in chunk.ChunkCustomData.dict.Keys.ToArray())
        {
            if(!key.StartsWith(Prefix,StringComparison.Ordinal)||!Guid.TryParseExact(key.Substring(Prefix.Length),"N",out var operation))continue;
            var bytes=chunk.ChunkCustomData.dict[key]?.data;if(bytes==null||bytes.Length!=60||BitConverter.ToInt32(bytes,16)!=item.entityId)continue;
            try
            {
                var drop=Read(operation);if(drop==null||!AdvancedFarmingHarvestReceipt.TryRead(world.worldState.Guid,Guid.ParseExact(drop.Parent,"N"),out var receipt))return false;
                var remaining=AdvancedFarmingHarvestReceipt.Remaining(receipt);
                if(drop.Index>=remaining.Length||AdvancedFarmingHarvestReceipt.OutputImage(new[]{remaining[drop.Index]})!=drop.Output||operation!=(remaining.Length==1?receipt.Operation:Observation(receipt.Operation,"output-"+drop.Index)))return false;
                var position=Position(world,receipt,remaining[drop.Index],drop);var a=new Attempt{Position=position,Operation=operation,World=world,Directory=GameIO.GetSaveGameDir(),Receipt=receipt,Drop=drop};if(!Exact(a,item)||!Marker(chunk,operation,drop))return false;
                if(!drop.Published){Attempts[operation]=a;Hold(a,item);}return true;
            }
            catch(Exception error){Log.Warning("[REBIRTH Farming] native original drop quarantined: "+error.GetType().Name);return false;}
        }
        return true;
    }
    private static bool FinalEqualsBefore(Attempt a)
    {
        // Only a different process may prove a failed prior spawn absent. A live
        // process can still have an older snapshot queued for publication.
        if(a.Drop.Process==Process)return false;
        var cell=Cell(a);var chunk=Chunk(a.World,cell);var provider=a.World.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(chunk==null||chunk.InProgressUnloading||chunk.pendingEntityCreateOps.Count!=0||chunk.entityStubs.Any(e=>e.id==a.Drop.EntityId)||regions==null||regions.regionFileAccess is not RegionFileAccessSectorBased)return false;
        string path=RegionFile.ConstructFullFilePath(regions.saveDirectory,chunk.X>>5,chunk.Z>>5,"7rg");
        using(var file=SdFile.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
        {
            if(!RebirthStationRegionPayload.TryRead(file,RebirthStationRegionPayload.NativeLocalIndex(chunk.X),RebirthStationRegionPayload.NativeLocalIndex(chunk.Z),out var payload)||!RebirthStationChunkInflate.TryDecode(payload,RebirthStationSnapshotEvidence.MaximumSnapshotBytes,out var body))return false;
            var bytes=new byte[body.Length+8];Array.Copy(payload,bytes,8);Array.Copy(body,0,bytes,8,body.Length);
            using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()==a.Drop.Before;
        }
    }
    private static bool ObserveOne(World world,EntityPlayer owner,AdvancedFarmingHarvestReceipt.Record receipt,ItemStack output,int index,Guid operation,out Proof proof)
    {
        proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||receipt.State!=AdvancedFarmingHarvestReceipt.Phase.OwnerPublished||world.worldState?.Guid!=receipt.World)return false;
            var remaining=new[]{output};
            if(!Attempts.TryGetValue(operation,out var a)||!ReferenceEquals(a.World,world))
            {if(Attempts.Count>=8&&!Attempts.ContainsKey(operation))return false;var drop=Read(operation);var position=Position(world,receipt,output,drop);a=new Attempt{Position=position,Operation=operation,World=world,Directory=GameIO.GetSaveGameDir(),Receipt=receipt,Drop=drop};Attempts[operation]=a;}
            if(!Current(a))return false;
            var cell=Cell(a);var chunk=Chunk(world,cell);if(chunk==null||chunk.InProgressUnloading||chunk.IsLocked||chunk.pendingEntityCreateOps.Count!=0)return false;
            string image=AdvancedFarmingHarvestReceipt.OutputImage(remaining);
            if(a.Drop==null)
            {
                if(chunk.ChunkCustomData.dict.ContainsKey(Key(operation)))return false;
                if(a.Before==null&&!AdvancedFarmingHarvestPublication.Observe(Observation(operation,"before-drop"),receipt.Source,world,cell,()=>Current(a)&&!chunk.ChunkCustomData.dict.ContainsKey(Key(operation)),out a.Before))return false;
                a.Drop=new Drop{Operation=operation.ToString("N"),Parent=receipt.Operation.ToString("N"),Index=index,Source=receipt.Source.ToString("N"),World=receipt.World,Output=image,Before=a.Before.Digest,X=a.Position.x,Y=a.Position.y,Z=a.Position.z};
                if(!Save(a.Drop)){a.Drop=null;return false;}AdvancedFarmingHarvestPublication.Complete(Observation(operation,"before-drop"));
            }
            if(a.Drop.Parent!=receipt.Operation.ToString("N")||a.Drop.Index!=index||a.Drop.Source!=receipt.Source.ToString("N")||a.Drop.World!=receipt.World||a.Drop.Output!=image)return false;
            if(a.Drop.Published){proof=new Proof{Operation=receipt.Operation,Source=receipt.Source,World=receipt.World,OutputDigest=AdvancedFarmingHarvestReceipt.OutputDigest(image),NativeDigest=a.Drop.Native,Generation=a.Drop.Generation};return true;}
            if(a.Entity==null&&a.Drop.EntityId>0)
            {
                var existing=world.GetEntity(a.Drop.EntityId) as EntityItem;
                if(Exact(a,existing)&&Marker(chunk,operation,a.Drop))Hold(a,existing);
                else if(chunk.ChunkCustomData.dict.ContainsKey(Key(operation))||!FinalEqualsBefore(a))return false;
            }
            if(a.Entity==null)
            {
                spawning=a;try{GameManager.Instance.ItemDropServer(remaining[0].Clone(),a.Position,UnityEngine.Vector3.zero,owner.entityId,60f,false);}finally{spawning=null;}
                if(a.Entity==null||!ReferenceEquals(world.GetEntity(a.Entity.entityId),a.Entity))return false;
            }
            Hold(a,a.Entity);
            Func<bool> postimage=()=>Current(a)&&ReferenceEquals(world.GetEntity(a.Entity.entityId),a.Entity)&&Exact(a,a.Entity)&&Marker(chunk,operation,a.Drop)&&UnityEngine.Vector3.SqrMagnitude(a.Entity.position-a.Position)<0.000001f;
            if(a.After==null&&!AdvancedFarmingHarvestPublication.Observe(Observation(operation,"after-drop"),receipt.Source,world,cell,postimage,out a.After))return false;
            string prior=Encode(a.Drop);a.Drop.Native=a.After.Digest;a.Drop.Generation=a.After.Generation;a.Drop.Published=true;
            if(!Save(a.Drop,prior)){a.Drop=JsonConvert.DeserializeObject<Drop>(prior);return false;}
            AdvancedFarmingHarvestPublication.Complete(Observation(operation,"after-drop"));
            proof=new Proof{Operation=receipt.Operation,Source=receipt.Source,World=receipt.World,OutputDigest=AdvancedFarmingHarvestReceipt.OutputDigest(image),NativeDigest=a.Drop.Native,Generation=a.Drop.Generation};return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original native overflow pending: "+error.GetType().Name);return false;}
    }
    internal static bool Observe(World world,EntityPlayer owner,AdvancedFarmingHarvestReceipt.Record receipt,out Proof proof)
    {
        proof=null;var remaining=AdvancedFarmingHarvestReceipt.Remaining(receipt);if(remaining.Length==0||remaining.Length>2)return false;
        var parts=new List<Proof>();
        for(int i=0;i<remaining.Length;i++)
        {
            Guid operation=remaining.Length==1?receipt.Operation:Observation(receipt.Operation,"output-"+i);
            if(!ObserveOne(world,owner,receipt,remaining[i],i,operation,out var part))return false;parts.Add(part);
        }
        proof=new Proof{Operation=receipt.Operation,Source=receipt.Source,World=receipt.World,OutputDigest=AdvancedFarmingHarvestReceipt.OutputDigest(AdvancedFarmingHarvestReceipt.OutputImage(remaining)),NativeDigest=parts.Count==1?parts[0].NativeDigest:AdvancedFarmingHarvestReceipt.OutputDigest(string.Join(":",parts.Select(part=>part.NativeDigest))),Generation=parts.Max(part=>part.Generation)};return true;
    }
    internal static void Complete(AdvancedFarmingHarvestReceipt.Record receipt)
    {
        if(receipt.State!=AdvancedFarmingHarvestReceipt.Phase.Retired)return;
        foreach(var pair in Attempts.Where(pair=>pair.Value.Receipt.Operation==receipt.Operation).ToArray())
        {
            var a=pair.Value;if(!a.Drop.Published)continue;
            if(a.Entity!=null){HeldEntities.Remove(a.Entity);if(a.Entity.itemRB!=null)a.Entity.itemRB.isKinematic=a.Kinematic;}
            var chunk=Chunk(a.World,Cell(a));if(chunk!=null&&Marker(chunk,a.Operation,a.Drop)){chunk.ChunkCustomData.Remove(Key(a.Operation));chunk.isModified=true;}
            Attempts.Remove(pair.Key);
        }
    }
    internal static void RetireWorld(World world)
    {foreach(var entry in Attempts.Where(pair=>ReferenceEquals(pair.Value.World,world)).ToArray())Attempts.Remove(entry.Key);foreach(var entity in HeldEntities.Keys.Where(entity=>ReferenceEquals(entity.world,world)).ToArray())HeldEntities.Remove(entity);}
    internal static void Install(Harmony harmony)
    {foreach(var type in new[]{typeof(AdvancedFarmingHarvestDropSpawn),typeof(AdvancedFarmingHarvestDropCollect),typeof(AdvancedFarmingHarvestDropNativeCollect),typeof(AdvancedFarmingHarvestDropUpdate),typeof(AdvancedFarmingHarvestDropUnload)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);}
}
[HarmonyPatch(typeof(World),nameof(World.SpawnEntityInWorld),new[]{typeof(Entity)})]
internal static class AdvancedFarmingHarvestDropSpawn {private static bool Prefix(Entity __0)=>AdvancedFarmingHarvestDrops.Capture(__0);}
[HarmonyPatch(typeof(NetPackageEntityCollect),nameof(NetPackageEntityCollect.ProcessPackage))]
internal static class AdvancedFarmingHarvestDropCollect {private static bool Prefix(NetPackageEntityCollect __instance,World __0)=>__0?.IsRemote()!=false||!AdvancedFarmingHarvestDrops.Held(__0.GetEntity(__instance.entityId));}
[HarmonyPatch(typeof(Entity),nameof(Entity.Collect))]
internal static class AdvancedFarmingHarvestDropNativeCollect {private static bool Prefix(Entity __instance)=>!AdvancedFarmingHarvestDrops.Held(__instance);}
[HarmonyPatch]
internal static class AdvancedFarmingHarvestDropUpdate
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods(){foreach(string name in new[]{"OnUpdateEntity","updateTransform","LateUpdate"})yield return AccessTools.DeclaredMethod(typeof(EntityItem),name)??throw new MissingMethodException(typeof(EntityItem).Name,name);}
    private static bool Prefix(EntityItem __instance)=>!AdvancedFarmingHarvestDrops.Held(__instance);
}
[HarmonyPatch(typeof(Entity),nameof(Entity.MarkToUnload))]
internal static class AdvancedFarmingHarvestDropUnload {private static bool Prefix(Entity __instance)=>!AdvancedFarmingHarvestDrops.Held(__instance);}
