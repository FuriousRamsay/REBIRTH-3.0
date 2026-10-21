using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

// Detached native None/Keep/Delete projection only. No effect, payment or
// destruction authority is supplied by this non-injective transform alone.
internal static partial class RebirthPlacedOriginalDeathPartition
{
    internal static bool TryProject(RebirthCamperEnergyActorImage original,int mode,int lossMode,int degradeType,float degradeAmount,out RebirthCamperEnergyActorImage projected)
    {
        projected=null;
        try
        {
            if(original?.Bag==null||original.Belt==null||original.Cursor==null||!original.Cursor.IsEmpty()||lossMode!=0||mode<0||mode>6||degradeType<0||degradeType>3||float.IsNaN(degradeAmount)||float.IsInfinity(degradeAmount)||degradeAmount<0f||degradeAmount>1f)return false;
            bool dropsBag=mode==1||mode==3||mode==5,dropsBelt=mode==1||mode==2||mode==5;
            if(mode!=6&&(dropsBag&&original.Bag.Any(row=>row==null||!row.IsEmpty()&&!row.itemValue.ItemClassOrMissing.KeepOnDeath())||dropsBelt&&original.Belt.Any(row=>row==null||!row.IsEmpty()&&!row.itemValue.ItemClassOrMissing.KeepOnDeath())))return false;
            var next=original.Copy();
            foreach(var rows in new[]{next.Bag,next.Belt})for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];if(row==null||row.count<0||row.count>ushort.MaxValue||row.IsEmpty()&&row.count!=0)return false;
                if(mode==6){rows[i]=ItemStack.Empty.Clone();continue;}
                if(row.IsEmpty()||!row.itemValue.HasQuality)continue;var value=row.itemValue;
                // Exact EntityPlayerLocal.degradeItemsOnDeath native order:
                // modifier first, then the resulting MaxUseTimes and use delta.
                if(degradeType==2||degradeType==3)value.MaxDurabilityModifier=Math.Max(value.MaxDurabilityModifier-degradeAmount,degradeAmount);
                if(degradeType==1||degradeType==3){int maximum=value.MaxUseTimes;value.UseTimes=Math.Min(value.UseTimes+(int)((float)maximum*degradeAmount),(float)maximum);}
            }
            projected=next;return true;
        }
        catch{return false;}
    }

    private const string Prefix="rbPlacedDeathPartition";
    private const int Maximum=4*1024*1024;
    private sealed class Observation
    {internal bool Paid;internal RebirthPlacedOriginal.Record Original;internal string OriginalImage,Preclear,Equipment=string.Empty,Policy,Image,Digest;internal int Mode,Loss,Degrade,Minimum,Maximum,NativeOwnerEntity;internal float Amount;}
    private sealed class Context
    {internal Observation Observation;internal OwedChild Owed;internal bool OwedDelivered;internal EntityPlayerLocal Player;internal World World;internal string Directory;internal RebirthCamperEnergyActorImage Expected;internal int Stage;}
    private static Context current;
    internal static string Policy(int mode,int loss,int degrade,float amount,int minimum=0,int maximum=0)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((byte)2);writer.Write(mode);writer.Write(loss);writer.Write(degrade);writer.Write(amount);writer.Write(minimum);writer.Write(maximum);writer.Flush();return Convert.ToBase64String(stream.ToArray());}}
    internal static bool ValidPolicy(string policy)
    {try{using(var stream=new MemoryStream(Convert.FromBase64String(policy),false))using(var reader=new BinaryReader(stream)){if(reader.ReadByte()!=2)return false;int mode=reader.ReadInt32(),loss=reader.ReadInt32(),degrade=reader.ReadInt32();float amount=reader.ReadSingle();int minimum=reader.ReadInt32(),maximum=reader.ReadInt32();return stream.Position==stream.Length&&mode>=0&&mode<=6&&loss>=0&&loss<=5&&degrade>=0&&degrade<=3&&!float.IsNaN(amount)&&!float.IsInfinity(amount)&&amount>=0&&amount<=1&&minimum>=0&&maximum>=minimum&&maximum<=1024&&Policy(mode,loss,degrade,amount,minimum,maximum)==policy;}}catch{return false;}}
    internal static string ServerPolicy()=>Policy(SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DropOnDeath),SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DegradeItemsOnDeath),SandboxOptions.SandboxOptionManager.GetFloat(global::SandboxOptions.SandboxOptions.DegradeAmountOnDeath),EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax);
    internal static string ReadWire(PooledBinaryReader reader){int length=reader.ReadInt32();if(length<0||length>Maximum)throw new InvalidDataException();var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);}
    internal static void WriteWire(PooledBinaryWriter writer,string image){var bytes=new UTF8Encoding(false,true).GetBytes(image);if(bytes.Length>Maximum)throw new InvalidDataException();writer.Write(bytes.Length);writer.Write(bytes);}
    private static string Hash(string value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    private static string Body(Observation o)=>new XElement("placedNativePartition",new XAttribute("version",1),new XAttribute("policy",o.Policy),new XAttribute("paid",o.Paid),new XAttribute("nativeOwnerEntity",o.NativeOwnerEntity),new XElement("original",o.OriginalImage),new XElement("preclear",o.Preclear),o.Equipment.Length==0?null:new XElement("equipment",o.Equipment)).ToString(SaveOptions.DisableFormatting);
    private static Observation Decode(string image)
    {
        if(image==null||image.Length>Maximum)throw new InvalidDataException();XElement e;using(var input=new StringReader(image))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum}))e=XElement.Load(reader);
        if(e.Name!="placedNativePartition"||(int?)e.Attribute("version")!=1)throw new InvalidDataException();var o=new Observation{Paid=(bool)e.Attribute("paid"),NativeOwnerEntity=(int)e.Attribute("nativeOwnerEntity"),Policy=(string)e.Attribute("policy"),OriginalImage=(string)e.Element("original"),Preclear=(string)e.Element("preclear"),Equipment=(string)e.Element("equipment")??string.Empty,Image=image};
        using(var stream=new MemoryStream(Convert.FromBase64String(o.Policy),false))using(var reader=new BinaryReader(stream)){if(reader.ReadByte()!=2)throw new InvalidDataException();o.Mode=reader.ReadInt32();o.Loss=reader.ReadInt32();o.Degrade=reader.ReadInt32();o.Amount=reader.ReadSingle();o.Minimum=reader.ReadInt32();o.Maximum=reader.ReadInt32();if(stream.Position!=stream.Length||!ValidPolicy(o.Policy))throw new InvalidDataException();}
        o.Original=RebirthPlacedOriginal.DecodeRecord(o.OriginalImage);var r=o.Original;if(o.NativeOwnerEntity<=0||r.Cancelled||r.Declined||r.TargetBirth.Length!=32||r.Provenance.Length==0||o.Paid&&(r.Phase!=2||!r.Attempted||r.Changes.Length==0)||!o.Paid&&!(r.Phase<=1&&!r.Attempted&&r.Changes.Length==0||r.Phase==2&&r.Attempted&&r.Changes.Length!=0)||!RebirthPlacedOriginal.ReadActor(o.Preclear).Matches(RebirthPlacedOriginal.ReadActor(o.Paid?r.After:r.Before))||!ValidInitialProjection(o)||Body(o)!=image)throw new InvalidDataException();o.Digest=Hash(image);return o;
    }
    private static string PathFor(Observation o,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"partition-server":"partition-owner",o.Original.Operation.ToString("N")+".xml");
    private static bool Save(Observation o,bool server)
    {
        try
        {
            string path=PathFor(o,server);if(File.Exists(path)||File.Exists(path+".bak"))return Read(path,out var old)&&old.Image==o.Image;
            string folder=Path.GetDirectoryName(path);Directory.CreateDirectory(folder);if(Directory.EnumerateFiles(folder,o.Original.Operation.ToString("N")+".*.tmp").Any())return false;
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(o.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return Read(temp,out var check)&&check.Image==o.Image&&RebirthDurableFileCommit.TryPublish(temp,path,out _)&&Read(path,out check)&&check.Image==o.Image;
        }catch{return false;}
    }
    private static bool Read(string path,out Observation o){o=null;try{if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;o=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));return true;}catch{return false;}}
    private static bool Current(Context c)=>c?.Owed!=null?CurrentOwedPhysical(c):c!=null&&ReferenceEquals(current,c)&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,c.World)&&ReferenceEquals(c.World.GetPrimaryPlayer(),c.Player)&&ReferenceEquals(c.World.GetEntity(c.Player.entityId),c.Player)&&c.Directory==GameIO.GetSaveGameDir()&&c.Player.IsDead()&&c.Player.Died==c.Observation.Original.Deaths+1&&RebirthPlacedOriginal.SamePartitionOwner(c.Player,c.Observation.Original)&&Policy((int)EntityPlayerLocal.DropOnDeathOption,SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),(int)EntityPlayerLocal.DegradeOnDeathType,EntityPlayerLocal.DegradeAmountOnDeath,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax)==c.Observation.Policy;
    private static void Stamp(Context c,int stage,string digest=null)
    {c.Player.Buffs.SetCustomVar(Prefix+"Stage",0,true);for(int i=0;i<16;i++)c.Player.Buffs.SetCustomVar(Prefix+i,Convert.ToUInt16((digest??c.Observation.Digest).Substring(i*4,4),16),true);c.Player.Buffs.SetCustomVar(Prefix+"Stage",stage,true);c.Stage=stage;}
    internal static void OnNativeDropStarting(EntityPlayerLocal player)
    {
        try
        {
            ObserveOwedUnusedStarting(player);ObserveOwedDeleteStarting(player);TryBeginOwedPhysical(player);if(current!=null&&!Current(current))current=null;if(current!=null||!RebirthPlacedOriginal.TryOwnerPartition(player,out var r,out bool debitObserved))return;
            // This is the actual pre-removal native actor, NOT a saved-primary
            // publication. The actual callback must already have paid locally.
            var actualBefore=RebirthCamperEnergyActorImage.Capture(player);bool paid=actualBefore.Matches(RebirthPlacedOriginal.ReadActor(r.After));bool unused=actualBefore.Matches(RebirthPlacedOriginal.ReadActor(r.Before));if(paid==unused||paid&&(!debitObserved||r.Phase!=2||!r.Attempted||r.Changes.Length==0)||unused&&debitObserved)return;
            int mode=(int)EntityPlayerLocal.DropOnDeathOption,loss=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),degrade=(int)EntityPlayerLocal.DegradeOnDeathType;float amount=EntityPlayerLocal.DegradeAmountOnDeath;
            bool simple=TryProject(actualBefore,mode,loss,degrade,amount,out var projected);if(!simple&&(mode<0||mode>6)||mode!=0&&string.IsNullOrEmpty(player.dropInventoryBlock))return;
            var o=new Observation{Paid=paid,NativeOwnerEntity=player.entityId,Original=r,OriginalImage=RebirthPlacedOriginal.EncodeRecord(r),Preclear=RebirthPlacedOriginal.EncodeActor(actualBefore),Equipment=simple?string.Empty:Convert.ToBase64String(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)),Policy=Policy(mode,loss,degrade,amount,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax)};o=Decode(Body(o));if(player.world.IsRemote()==false&&!RebirthPlacedOriginal.ValidatePartition(player,r,o.Policy,o.NativeOwnerEntity))return;if(!Save(o,false))return;
            current=new Context{Observation=o,Player=player,World=(World)player.world,Directory=GameIO.GetSaveGameDir(),Expected=projected};if(!simple)BeginPhysical(current);Stamp(current,1);Send(current);
        }catch{current=null;}
    }
    internal static void OnNativeDropFinished(EntityPlayerLocal player,Exception error)
    {
        var c=current;try{if(OwnsPhysical(c)){FinishPhysical(c,player,error);return;}if(error!=null||!Current(c)||!ReferenceEquals(c.Player,player)||c.Stage!=1||!RebirthCamperEnergyActorImage.Capture(player).Matches(c.Expected)){if(ReferenceEquals(c?.Player,player))current=null;return;}Stamp(c,2);Send(c);}catch{current=null;}
    }
    internal static void OnNativeBeforeRespawn(EntityPlayerLocal player)
    {
        var c=current;try
        {
            ObserveOwedUnusedBeforeRespawn(player);ObserveOwedDeleteBeforeRespawn(player);ObserveOwedBeforeRespawn(player);if(OwnsPhysical(c)){FinishPhysicalBeforeRespawn(c,player);return;}if(!Current(c)||!ReferenceEquals(c.Player,player)||c.Stage!=2||!RebirthCamperEnergyActorImage.Capture(player).Matches(c.Expected))return;
            // Native OnDeathUpdate already performed its equipment clear. Only
            // now may the real final owner save/upload carry completion3.
            Stamp(c,3);GameManager.Instance.SaveLocalPlayerData();if(c.World.IsRemote()){var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;if(connection!=null&&!connection.IsServer&&connection.IsConnected)connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);}Send(c);current=null;
        }catch{current=null;}
    }
    private static void Send(Context c)
    {if(c.World.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedPartition>().Setup(c.Player.entityId,c.Observation.Image));else Receive(c.World,c.Player.entityId,c.Observation.Image);}
    internal static void Retry(EntityPlayerLocal player,RebirthPlacedOriginal.Record original)
    {try{string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","partition-owner",original.Operation.ToString("N")+".xml");if(!Read(path,out var o)||!RebirthPlacedOriginal.SamePartitionOwner(player,o.Original)||player.Died<=original.Deaths)return;RetryPhysical(player,o);RetryOwed(player,original.Operation);if(player.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedPartition>().Setup(player.entityId,o.Image));else Receive((World)player.world,player.entityId,o.Image);}catch{}}
    internal static void Receive(World world,int entity,string image)
    {try{if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))return;var owner=world.GetEntity(entity) as EntityPlayer;if(image.StartsWith("<placedNativeBodyIntent",StringComparison.Ordinal)){ReceiveBodyIntent(world,entity,image);return;}if(image.StartsWith("<placedNativePhysical",StringComparison.Ordinal)){ReceivePhysical(world,entity,image);return;}var o=Decode(image);if(!RebirthPlacedOriginal.ValidatePartition(owner,o.Original,o.Policy,o.NativeOwnerEntity))return;Save(o,true);}catch{}}
    private static bool Marker(PlayerDataFile file,string expected)
    {
        try
        {
            var bytes=file.buffData?.ToArray();if(bytes==null||bytes.Length>1024*1024)return false;var values=new System.Collections.Generic.Dictionary<string,float>(StringComparer.Ordinal);
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);count=reader.ReadUInt16();for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key.StartsWith(Prefix,StringComparison.Ordinal)){if(values.ContainsKey(key)||float.IsNaN(value)||float.IsInfinity(value))return false;values.Add(key,value);}}if(stream.Position!=stream.Length)return false;}
            if(!values.TryGetValue(Prefix+"Stage",out float stage)||stage!=3)return false;var text=new StringBuilder(64);for(int i=0;i<16;i++){if(!values.TryGetValue(Prefix+i,out float part)||part<0||part>ushort.MaxValue||part!=(int)part)return false;text.Append(((ushort)part).ToString("x4"));}return text.ToString()==expected;
        }catch{return false;}
    }
    internal static bool TryReadUnusedCapsule(RebirthPlacedOriginal.Record original,out string capsule)
    {capsule=string.Empty;string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","partition-server",original.Operation.ToString("N")+".xml");if(!Read(path,out var o)||o.Paid||!RebirthPlacedOriginal.SamePartitionOriginal(original,o.Original))return false;capsule=o.Original.Changes;return true;}
    internal static bool TrySavedUnused(RebirthPlacedOriginal.Record original,string policy,RebirthCamperEnergyActorImage actual,PlayerDataFile file,out string proof,out string capsule)
    {
        proof=null;capsule=null;try
        {
            if(TrySavedPhysical(original,policy,actual,file,false,out proof,out capsule))return true;
            string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","partition-server",original.Operation.ToString("N")+".xml");if(!Read(path,out var o)||o.Paid||o.Policy!=policy||!RebirthPlacedOriginal.SamePartitionOriginal(original,o.Original)||original.Deaths==int.MaxValue||file.deaths!=original.Deaths+1||!Marker(file,o.Digest)||!TryProject(RebirthPlacedOriginal.ReadActor(original.Before),o.Mode,o.Loss,o.Degrade,o.Amount,out var expected)||!actual.Matches(expected))return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(o.Image);writer.Write(file.id);writer.Write(file.deaths);foreach(var blob in new[]{file.bagData,file.inventoryData,file.buffData}){var bytes=blob.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}capsule=o.Original.Changes;return true;
        }catch{return false;}
    }
    internal static bool TrySavedProof(RebirthPlacedOriginal.Record original,string policy,RebirthCamperEnergyActorImage actual,PlayerDataFile file,out string proof)
    {
        proof=null;try
        {
            if(TrySavedPhysical(original,policy,actual,file,true,out proof,out _))return true;
            string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","partition-server",original.Operation.ToString("N")+".xml");if(!Read(path,out var o)||!o.Paid||o.Policy!=policy||!RebirthPlacedOriginal.SamePartitionOriginal(original,o.Original)||original.Deaths==int.MaxValue||file.deaths!=original.Deaths+1||!Marker(file,o.Digest)||!TryProject(RebirthPlacedOriginal.ReadActor(original.After),o.Mode,o.Loss,o.Degrade,o.Amount,out var expected)||!actual.Matches(expected))return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(o.Image);writer.Write(file.id);writer.Write(file.deaths);foreach(var blob in new[]{file.bagData,file.inventoryData,file.buffData}){var bytes=blob.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}return true;
        }catch{return false;}
    }
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedPartition:NetPackage
{
    private int entity;private string image=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthPlacedPartition Setup(int player,string body){entity=player;image=body;return this;}
    public override void read(PooledBinaryReader reader){entity=reader.ReadInt32();image=RebirthPlacedOriginalDeathPartition.ReadWire(reader);}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(entity);RebirthPlacedOriginalDeathPartition.WriteWire(writer,image);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==false&&ValidEntityIdForSender(entity))RebirthPlacedOriginalDeathPartition.Receive(world,entity,image);}
    public int GetLength()=>8+Encoding.UTF8.GetByteCount(image);
}
