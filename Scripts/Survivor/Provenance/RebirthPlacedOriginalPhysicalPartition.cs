using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private sealed class BuilderInput{internal ItemStack Item;internal bool Failed;}
    private sealed class Physical
    {
        internal Context Context;internal byte[] Selected;internal bool SelectionSeen,Failed,Adding,Constructed,Overflow,Requested,RefusalObserved;
        internal RebirthLogisticsPhysicalLossWitness.Projection Projection;internal EntityBackpack Builder;internal EntityCreationData Refused,ConstructedCreation;
        internal readonly List<BuilderInput> Inputs=new List<BuilderInput>();internal string FinalActor,FinalEquipment,Creation=string.Empty;internal PhysicalTrace Trace;
    }
    private sealed class BuilderToken{internal Physical Physical;internal ItemStack Input;internal ItemStack[] Before;}
    private sealed class PhysicalTrace
    {internal Observation Observation;internal byte[] Selected;internal string FinalActor,FinalEquipment,Creation,Image,Digest;internal bool Overflow;internal BuilderInput[] Inputs;}
    private static Physical physical;
    private static bool OwnsPhysical(Context c)=>physical!=null&&ReferenceEquals(physical.Context,c);
    private static Equipment EquipmentImage(byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>1048576)throw new InvalidDataException();using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);var equipment=Equipment.Read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length||equipment.GetSlotCount()!=13||!RebirthLogisticsDeleteWitness.EquipmentBytes(equipment).SequenceEqual(bytes))throw new InvalidDataException();return equipment;}
    }
    private static bool ValidInitialProjection(Observation o)
    {
        if(o.Equipment.Length==0)return TryProject(RebirthPlacedOriginal.ReadActor(o.Paid?o.Original.After:o.Original.Before),o.Mode,o.Loss,o.Degrade,o.Amount,out _);
        try{return o.Mode>=0&&o.Mode<=6&&EquipmentImage(Convert.FromBase64String(o.Equipment))!=null;}catch{return false;}
    }
    private static void BeginPhysical(Context c)
    {var actor=RebirthPlacedOriginal.ReadActor(c.Observation.Preclear);physical=new Physical{Context=c,Selected=new byte[(actor.Bag.Length+actor.Belt.Length+13+7)/8]};}
    private static bool PhysicalCurrent(Physical p)=>p!=null&&ReferenceEquals(physical,p)&&Current(p.Context)&&!p.Failed;
    private static bool Project(Observation o,byte[] selected,out RebirthLogisticsPhysicalLossWitness.Projection result)
    {
        var actor=RebirthPlacedOriginal.ReadActor(o.Preclear);int projectionMode=o.Mode==0||o.Mode==6?2:o.Mode;
        if(!RebirthLogisticsPhysicalLossWitness.TryProject(actor.Bag,actor.Belt,Convert.FromBase64String(o.Equipment),selected,projectionMode,o.Loss,o.Minimum,o.Maximum,o.Degrade,o.Amount,out result))return false;
        // Native None returns before any backpack transfer. Native DeleteAll
        // clears bag/belt and non-Keep equipment after the same removal/degrade.
        if(o.Mode==6){var equipment=EquipmentImage(result.DegradedEquipment);for(int i=0;i<13;i++){var item=equipment.GetSlotItem(i);if(item!=null&&!item.IsEmpty()&&!item.ItemClassOrMissing.KeepOnDeath())equipment.ItemGrid[i]=ItemStack.Empty.Clone();}result.FinalEquipment=RebirthLogisticsDeleteWitness.EquipmentBytes(equipment);}
        return true;
    }
    internal static void Selection(EntityPlayerLocal player)
    {
        var p=physical;try
        {
            if(!PhysicalCurrent(p)||!ReferenceEquals(p.Context.Player,player)||p.SelectionSeen)return;var actor=RebirthPlacedOriginal.ReadActor(p.Context.Observation.Preclear);var list=player.DestroyLocationSlotList;if(list==null||list.Count>actor.Bag.Length+actor.Belt.Length+13)throw new InvalidDataException();
            foreach(var item in list){int location=(int)item.Location,slot=item.Slot;int width=location==0?actor.Bag.Length:location==1?actor.Belt.Length:location==2?13:0;if(slot<0||slot>=width)throw new InvalidDataException();int bit=(location==0?0:location==1?actor.Bag.Length:actor.Bag.Length+actor.Belt.Length)+slot;if((p.Selected[bit/8]&(1<<(bit%8)))!=0)throw new InvalidDataException();p.Selected[bit/8]|=(byte)(1<<(bit%8));}
            if(!Project(p.Context.Observation,p.Selected,out _))throw new InvalidDataException();p.SelectionSeen=true;
        }catch{if(p!=null)p.Failed=true;}
    }
    internal static void AfterRemoval(EntityPlayerLocal player)
    {
        var p=physical;try
        {
            if(!PhysicalCurrent(p)||!ReferenceEquals(p.Context.Player,player))return;if(!Project(p.Context.Observation,p.Selected,out var projection))throw new InvalidDataException();
            if(!SameRows(projection.RemovedBag,player.bag.ItemGrid.items)||!SameRows(projection.RemovedBelt,player.inventory.ItemGrid.items)||!projection.RemovedEquipment.SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)))throw new InvalidDataException();p.SelectionSeen=true;p.Projection=projection;
        }catch{if(p!=null)p.Failed=true;}
    }
    internal static void Factory(int entityType,UnityEngine.Vector3 position,Entity entity)
    {
        var p=physical;try
        {
            if(!PhysicalCurrent(p)||p.Projection==null||entityType!="Backpack".GetHashCode())return;if(p.Builder!=null||!(entity is EntityBackpack builder)||!position.Equals(p.Context.Player.position+p.Context.Player.transform.up*2f)||builder.bag?.ItemGrid?.items==null||builder.bag.ItemGrid.items.Any(row=>row==null||!row.IsEmpty())||!p.Projection.DegradedEquipment.SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(p.Context.Player.equipment)))throw new InvalidDataException();p.Builder=builder;
        }catch{if(p!=null)p.Failed=true;}
    }
    internal static object BeforeBuilderAdd(Bag bag,ItemStack input)
    {
        var p=physical;try{if(!PhysicalCurrent(p)||p.Builder==null||!ReferenceEquals(p.Builder.bag,bag))return null;if(p.Adding||input==null||input.IsEmpty()||input.count<1||input.count>ushort.MaxValue||p.Inputs.Count>=525)throw new InvalidDataException();p.Adding=true;return new BuilderToken{Physical=p,Input=input.Clone(),Before=CloneRows(bag.ItemGrid.items)};}catch{if(p!=null)p.Failed=true;return null;}
    }
    internal static void AfterBuilderAdd(Bag bag,ItemStack input,bool result,object token)
    {
        if(!(token is BuilderToken call))return;var p=call.Physical;try{if(!PhysicalCurrent(p)||!p.Adding||!ReferenceEquals(p.Builder?.bag,bag))throw new InvalidDataException();if(result){if(!Conserved(call.Before.Concat(new[]{call.Input}),bag.ItemGrid.items))throw new InvalidDataException();}else if(!SameRows(call.Before,bag.ItemGrid.items)||!SameRows(new[]{call.Input},new[]{input}))throw new InvalidDataException();p.Inputs.Add(new BuilderInput{Item=call.Input,Failed=!result});}catch{p.Failed=true;}finally{p.Adding=false;}
    }
    private sealed class NativeOverflow:IOException{}
    private sealed class Counter:Stream
    {
        private long length;private readonly int maximum;internal Counter(int maximum){this.maximum=maximum;}public override bool CanRead=>false;public override bool CanWrite=>true;public override bool CanSeek=>false;public override long Length=>length;public override long Position{get=>length;set=>throw new NotSupportedException();}public override void Flush(){}public override void Write(byte[] bytes,int offset,int count){if(bytes==null||offset<0||count<0||offset>bytes.Length-count)throw new InvalidDataException();if(length>maximum-count)throw new NativeOverflow();length+=count;}public override void WriteByte(byte value){if(length>=maximum)throw new NativeOverflow();length++;}public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    internal static bool BeforeConstruction(EntityCreationData creation,Entity entity,StreamModeWrite mode)
    {
        var p=physical;if(!PhysicalCurrent(p)||!ReferenceEquals(p.Builder,entity)||mode!=StreamModeWrite.ToClient)return true;
        try
        {
            if(p.Constructed||p.Builder.RefPlayerId!=p.Context.Player.entityId||!Conserved(p.Inputs.Where(i=>!i.Failed).Select(i=>i.Item),p.Builder.bag.ItemGrid.items))throw new InvalidDataException();
            using(var stream=new Counter(ushort.MaxValue))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);p.Builder.Write(writer,mode);writer.Flush();if(stream.Length<1)throw new InvalidDataException();}
            p.Constructed=true;p.ConstructedCreation=creation;return true;
        }
        catch(NativeOverflow){p.Constructed=true;p.ConstructedCreation=creation;p.Overflow=true;p.Refused=creation;return false;}
        catch{p.Failed=true;throw new InvalidDataException("Placement original native body codec outcome unknown.");}
    }
    internal static bool BeforeRequest(EntityCreationData creation)
    {
        var p=physical;if(!PhysicalCurrent(p)||p.Builder==null||!ReferenceEquals(p.ConstructedCreation,creation))return true;
        if(p.Overflow&&ReferenceEquals(p.Refused,creation)){if(p.Requested||p.RefusalObserved||creation.id!=-1){p.Failed=true;throw new InvalidDataException("Placement refused original request changed.");}p.RefusalObserved=true;return false;}
        try
        {
            if(!p.Constructed||p.Overflow||p.Requested||creation==null||creation.id!=-1||creation.entityClass!=p.Builder.entityClass||!creation.pos.Equals(p.Builder.position)||!NativeBag(creation).SequenceEqual(BagBytes(p.Builder)))throw new InvalidDataException();
            p.Creation=Convert.ToBase64String(EcdBytes(creation));ReadEcd(p.Creation);PublishRequestIntent(p);p.Requested=true;return false;
        }catch{p.Failed=true;throw new InvalidDataException("Placement original native request scope changed.");}
    }
    private static void FinishPhysical(Context c,EntityPlayerLocal player,Exception error)
    {
        var p=physical;try
        {
            if(error!=null||!PhysicalCurrent(p)||!ReferenceEquals(p.Context,c)||!ReferenceEquals(player,c.Player)||p.Projection==null||p.Adding||p.Builder!=null&&!p.Constructed||p.Builder!=null&&!p.Overflow&&!p.Requested||p.Overflow&&!p.RefusalObserved)throw new InvalidDataException();
            var final=RebirthCamperEnergyActorImage.Capture(player);var succeeded=p.Builder==null?new ItemStack[0]:p.Builder.bag.ItemGrid.items;var failed=p.Inputs.Where(i=>i.Failed).Select(i=>i.Item);
            if(c.Observation.Mode==6){if(p.Builder!=null||p.Inputs.Count!=0||final.Bag.Concat(final.Belt).Any(row=>!row.IsEmpty())||!p.Projection.FinalEquipment.SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)))throw new InvalidDataException();}
            else if(!Conserved(p.Projection.DegradedBag.Concat(p.Projection.DegradedBelt).Concat(p.Projection.DroppedEquipment),final.Bag.Concat(final.Belt).Concat(succeeded).Concat(failed))||!p.Projection.DegradedEquipment.SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)))throw new InvalidDataException();
            p.FinalActor=RebirthPlacedOriginal.EncodeActor(final);p.FinalEquipment=Convert.ToBase64String(p.Projection.FinalEquipment);Stamp(c,2);
        }catch{if(p!=null)p.Failed=true;current=null;}
    }
    private static void FinishPhysicalBeforeRespawn(Context c,EntityPlayerLocal player)
    {
        var p=physical;try
        {
            if(!PhysicalCurrent(p)||!ReferenceEquals(p.Context,c)||!ReferenceEquals(c.Player,player)||c.Stage!=2||p.FinalActor==null||!RebirthCamperEnergyActorImage.Capture(player).Matches(RebirthPlacedOriginal.ReadActor(p.FinalActor))||!Convert.FromBase64String(p.FinalEquipment).SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)))return;
            if(c.Owed!=null){FinishOwedPhysicalBeforeRespawn(c,p,player);return;}var trace=new PhysicalTrace{Observation=c.Observation,Selected=(byte[])p.Selected.Clone(),FinalActor=p.FinalActor,FinalEquipment=p.FinalEquipment,Creation=p.Creation,Overflow=p.Overflow,Inputs=p.Inputs.Select(i=>new BuilderInput{Item=i.Item.Clone(),Failed=i.Failed}).ToArray()};trace=DecodePhysical(PhysicalBody(trace));if(!SavePhysical(trace,false))return;p.Trace=trace;Stamp(c,3,trace.Digest);GameManager.Instance.SaveLocalPlayerData();if(c.World.IsRemote()){var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;if(connection!=null&&!connection.IsServer&&connection.IsConnected)connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);}SendPhysical(player,trace);current=null;physical=null;
        }catch{if(p!=null)p.Failed=true;current=null;}
    }
    private static byte[] NativeBag(EntityCreationData e)=>e.bagData?.ToArray()??throw new InvalidDataException();
    private static byte[] BagBytes(EntityBackpack bag){using(var blob=StreamUtils.ToBlob(writer=>bag.bag.Write(writer,StreamModeWrite.ToClient)))return blob.ToArray();}
    private static byte[] EcdBytes(EntityCreationData e){using(var blob=StreamUtils.ToBlob(writer=>e.write(writer,StreamModeWrite.Persistency))){if(blob.Length>131072)throw new InvalidDataException();return blob.ToArray();}}
    private static EntityCreationData ReadEcd(string image){var bytes=Convert.FromBase64String(image);if(bytes.Length>131072)throw new InvalidDataException();using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);var e=new EntityCreationData();e.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length||!EcdBytes(e).SequenceEqual(bytes)||e.entityData==null||e.entityData.Length>ushort.MaxValue)return throwInvalid();return e;}}
    private static EntityCreationData throwInvalid(){throw new InvalidDataException();}
    private static byte[] Rows(ItemStack[] rows)
    {if(rows==null||rows.Length>607)throw new InvalidDataException();using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(rows.Length);foreach(var row in rows){if(row?.itemValue==null||row.count<0||row.count>ushort.MaxValue||row.IsEmpty()&&row.count!=0)throw new InvalidDataException();writer.Write(row.count);writer.Write(RebirthNativeItemCodec.Encode(row.IsEmpty()?ItemValue.None:row.itemValue));}writer.Flush();if(stream.Length>Maximum)throw new InvalidDataException();return stream.ToArray();}}
    private static ItemStack[] ReadRows(byte[] bytes){if(bytes==null||bytes.Length>Maximum)throw new InvalidDataException();using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){int count=reader.ReadInt32();if(count<0||count>607)throw new InvalidDataException();var rows=new ItemStack[count];for(int i=0;i<count;i++){int number=reader.ReadInt32();string image=reader.ReadString();if(image.Length>262144||!RebirthNativeItemCodec.TryDecode(image,out var value))throw new InvalidDataException();rows[i]=new ItemStack(value,number);}if(stream.Position!=stream.Length||!Rows(rows).SequenceEqual(bytes))throw new InvalidDataException();return rows;}}
    private static ItemStack[] CloneRows(ItemStack[] rows)=>ReadRows(Rows(rows));
    private static bool SameRows(ItemStack[] a,ItemStack[] b){try{return Rows(a).SequenceEqual(Rows(b));}catch{return false;}}
    private static bool Conserved(IEnumerable<ItemStack> before,IEnumerable<ItemStack> after)
    {try{Dictionary<string,long> Totals(IEnumerable<ItemStack> rows){var result=new Dictionary<string,long>(StringComparer.Ordinal);foreach(var row in rows){if(row==null||row.count<0||row.count>ushort.MaxValue||row.IsEmpty()&&row.count!=0)throw new InvalidDataException();if(row.IsEmpty())continue;string key=RebirthNativeItemCodec.Encode(row.itemValue);result.TryGetValue(key,out long count);result[key]=checked(count+row.count);}return result;}var a=Totals(before);var b=Totals(after);return a.Count==b.Count&&a.All(pair=>b.TryGetValue(pair.Key,out long amount)&&amount==pair.Value);}catch{return false;}}

    private static string PhysicalBody(PhysicalTrace t)=>new XElement("placedNativePhysical",new XAttribute("version",1),new XAttribute("overflow",t.Overflow),new XElement("observation",t.Observation.Image),new XElement("selected",Convert.ToBase64String(t.Selected)),new XElement("finalActor",t.FinalActor),new XElement("finalEquipment",t.FinalEquipment),new XElement("creation",t.Creation),new XElement("inputs",t.Inputs.Select(input=>new XElement("input",new XAttribute("failed",input.Failed),Convert.ToBase64String(Rows(new[]{input.Item})))))).ToString(SaveOptions.DisableFormatting);
    private static PhysicalTrace DecodePhysical(string image)
    {
        if(image==null||image.Length>Maximum)throw new InvalidDataException();XElement e;using(var input=new StringReader(image))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum}))e=XElement.Load(reader);if(e.Name!="placedNativePhysical"||(int?)e.Attribute("version")!=1)throw new InvalidDataException();
        var t=new PhysicalTrace{Overflow=(bool)e.Attribute("overflow"),Observation=Decode((string)e.Element("observation")),Selected=Convert.FromBase64String((string)e.Element("selected")),FinalActor=(string)e.Element("finalActor"),FinalEquipment=(string)e.Element("finalEquipment"),Creation=(string)e.Element("creation"),Inputs=e.Element("inputs").Elements("input").Select(input=>{var rows=ReadRows(Convert.FromBase64String(input.Value));if(rows.Length!=1||rows[0].IsEmpty())throw new InvalidDataException();return new BuilderInput{Failed=(bool)input.Attribute("failed"),Item=rows[0]};}).ToArray(),Image=image};
        if(t.Observation.Equipment.Length==0||t.Inputs.Length>525||t.Overflow&&t.Creation.Length!=0||t.Creation.Length==0&&!t.Overflow&&t.Inputs.Length!=0||!Project(t.Observation,t.Selected,out var projected)||!Convert.FromBase64String(t.FinalEquipment).SequenceEqual(projected.FinalEquipment)||PhysicalBody(t)!=image)throw new InvalidDataException();
        var actor=RebirthPlacedOriginal.ReadActor(t.FinalActor);var successes=t.Inputs.Where(input=>!input.Failed).Select(input=>input.Item).ToArray();
        if(t.Observation.Mode==6){if(t.Creation.Length!=0||t.Overflow||t.Inputs.Length!=0||actor.Bag.Concat(actor.Belt).Any(row=>!row.IsEmpty()))throw new InvalidDataException();}
        else if(!Conserved(projected.DegradedBag.Concat(projected.DegradedBelt).Concat(projected.DroppedEquipment),actor.Bag.Concat(actor.Belt).Concat(t.Inputs.Select(input=>input.Item))))throw new InvalidDataException();
        if(t.Observation.Mode==0&&(t.Creation.Length!=0||t.Overflow||t.Inputs.Length!=0||!SameRows(projected.DegradedBag,actor.Bag)||!SameRows(projected.DegradedBelt,actor.Belt)))throw new InvalidDataException();
        if(t.Creation.Length!=0)
        {
            var creation=ReadEcd(t.Creation);var body=ReadBodyBag(creation);var nativeBody=creation.entityData.ToArray();var nativeBag=NativeBag(creation);bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);if(creation.id!=-1||creation.entityClass!="Backpack".GetHashCode()||!Finite(creation.pos.x)||!Finite(creation.pos.y)||!Finite(creation.pos.z)||!Finite(creation.rot.x)||!Finite(creation.rot.y)||!Finite(creation.rot.z)||!Conserved(successes,body)||nativeBody.Length<nativeBag.Length+4||!nativeBody.Skip(nativeBody.Length-nativeBag.Length).SequenceEqual(nativeBag)||BitConverter.ToInt32(nativeBody,nativeBody.Length-nativeBag.Length-4)!=t.Observation.NativeOwnerEntity)throw new InvalidDataException();
        }
        else if(!t.Overflow&&successes.Length!=0)throw new InvalidDataException();
        t.Digest=Hash(image);return t;
    }
    private static ItemStack[] ReadBodyBag(EntityCreationData e)
    {var bytes=NativeBag(e);using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);var bag=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);if(stream.Position!=stream.Length||bag?.ItemGrid?.items==null||bag.SlotCount<1||bag.SlotCount>607)throw new InvalidDataException();return CloneRows(bag.ItemGrid.items);}}
    private static string PhysicalPath(PhysicalTrace t,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"physical-server":"physical-owner",t.Observation.Original.Operation.ToString("N")+".xml");
    private static bool ReadPhysical(string path,out PhysicalTrace t){t=null;try{if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;t=DecodePhysical(File.ReadAllText(path,new UTF8Encoding(false,true)));return true;}catch{return false;}}
    private static bool SavePhysical(PhysicalTrace t,bool server)
    {
        try{string path=PhysicalPath(t,server);if(File.Exists(path)||File.Exists(path+".bak"))return ReadPhysical(path,out var old)&&old.Image==t.Image;string folder=Path.GetDirectoryName(path);Directory.CreateDirectory(folder);if(Directory.EnumerateFiles(folder,t.Observation.Original.Operation.ToString("N")+".*.tmp").Any())return false;string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(t.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return ReadPhysical(temp,out var check)&&check.Image==t.Image&&RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadPhysical(path,out check)&&check.Image==t.Image;}catch{return false;}
    }
    private static void SendPhysical(EntityPlayerLocal player,PhysicalTrace t)
    {QueueNativeFinalTrace(player,false,t.Image);}
    private static void RetryPhysical(EntityPlayerLocal player,Observation o)
    {RetryBodyIntent(player,o.Original.Operation);string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","physical-owner",o.Original.Operation.ToString("N")+".xml");if(ReadPhysical(path,out var trace)&&trace.Observation.Image==o.Image)SendPhysical(player,trace);}
    private static void ReceivePhysical(World world,int entity,string image)
    {
        try{var actor=world.GetEntity(entity) as EntityPlayer;var trace=DecodePhysical(image);if(!RebirthPlacedOriginal.ValidatePartition(actor,trace.Observation.Original,trace.Observation.Policy,trace.Observation.NativeOwnerEntity)||!Save(trace.Observation,true)||!SavePhysical(trace,true))return;RebirthPlacedOriginalBodyPublication.ObserveTrace(world,trace.Observation.Original.Operation,trace.Creation);if(RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var file)&&file.id==actor.entityId)TrySavedPhysical(trace.Observation.Original,trace.Observation.Policy,actual,file,trace.Observation.Paid,out _,out _);}
        catch{}
    }
    private static bool TrySavedPhysical(RebirthPlacedOriginal.Record original,string policy,RebirthCamperEnergyActorImage actual,PlayerDataFile file,bool paid,out string proof,out string capsule)
    {
        proof=null;capsule=null;try
        {
            string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","physical-server",original.Operation.ToString("N")+".xml");if(!ReadPhysical(path,out var trace)||trace.Observation.Paid!=paid||trace.Observation.Policy!=policy||!RebirthPlacedOriginal.SamePartitionOriginal(original,trace.Observation.Original)||original.Deaths==int.MaxValue||file.deaths!=original.Deaths+1||!Marker(file,trace.Digest)||!actual.Matches(RebirthPlacedOriginal.ReadActor(trace.FinalActor))||!SavedEquipment(file).SequenceEqual(Convert.FromBase64String(trace.FinalEquipment)))return false;
            if(!CheckpointBodySource(trace,actual,file))return false;
            // Failed native builder rows and known byte refusal are owed material,
            // never a missing-body proof. Their immutable trace remains custody.
            string bodyProof=string.Empty;
            if(trace.Overflow||trace.Inputs.Any(input=>input.Failed)){RetainOwedPartition(trace,file,string.Empty);return false;}
            if(trace.Creation.Length!=0&&(!RebirthPlacedOriginalBodyPublication.TryPublished(original.Operation,trace.Creation,out bodyProof)||!RecordBodyInvocation(trace)))return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(trace.Image);writer.Write(bodyProof);writer.Write(file.id);writer.Write(file.deaths);foreach(var blob in new[]{file.bagData,file.inventoryData,file.buffData}){var bytes=blob.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}capsule=trace.Observation.Original.Changes;return true;
        }catch{return false;}
    }
    private static byte[] SavedEquipment(PlayerDataFile file){var bytes=file.equipmentData?.ToArray();EquipmentImage(bytes);return bytes;}
    internal static void RetryBodyPublication(World world){UpdateDeferredBodies(world);RebirthPlacedOriginalBodyPublication.Tick(world);}

    private sealed class RequestFrame{internal RequestFrame Previous;internal NetPackageRequestToSpawnEntity Packet;internal ClientInfo Sender;internal EntityPlayer Actor;internal World World;internal string Directory;internal RebirthPlacedOriginal.Record Original;internal string Creation;internal int NativeOwner;}
    [ThreadStatic]private static RequestFrame incoming;
    internal static object EnterRequest(NetPackageRequestToSpawnEntity packet,World world)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||packet?.Sender==null||packet.ecd==null||packet.ecd.entityClass!="Backpack".GetHashCode())return null;var sender=packet.Sender;var actor=world.GetEntity(sender.entityId) as EntityPlayer;if(actor==null||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender))return null;
            string creation=Convert.ToBase64String(EcdBytes(packet.ecd));var native=ReadEcd(creation);var bag=NativeBag(native);var body=native.entityData.ToArray();if(body.Length<bag.Length+4)return null;int owner=BitConverter.ToInt32(body,body.Length-bag.Length-4);if(owner!=sender.entityId||!packet.ValidEntityIdForSender(owner)||!RebirthPlacedOriginal.TryBodyOriginal(actor,owner,out var original))return null;
            var frame=new RequestFrame{Previous=incoming,Packet=packet,Sender=sender,Actor=actor,World=world,Directory=GameIO.GetSaveGameDir(),Original=original,Creation=creation,NativeOwner=owner};incoming=frame;return frame;
        }catch{return null;}
    }
    internal static void LeaveRequest(object token){if(token is TerminalRequestFrame terminal){if(ReferenceEquals(terminalIncoming,terminal))terminalIncoming=terminal.Previous;return;}if(token is OwedRequestToken owed){if(ReferenceEquals(owedIncoming,owed))owedIncoming=owed.Previous;LeaveRequest(owed.Parent);return;}if(token is RequestFrame frame&&ReferenceEquals(incoming,frame))incoming=frame.Previous;}
    internal static void ObserveAccepted(Entity entity)
    {
        try
        {
            if(!(entity is EntityBackpack body))return;if(ObserveTerminalNativeAccepted(body))return;if(ObserveDeferredBody(body))return;if(ObserveOwedBodyAccepted(body))return;var frame=incoming;
            if(frame!=null&&frame.Packet?.ecd!=null&&frame.Packet.ecd.id==body.entityId&&body.entityId>0&&ReferenceEquals(GameManager.Instance?.World,frame.World)&&ReferenceEquals(frame.World.GetEntity(body.entityId),body)&&frame.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(frame.Packet.Sender,frame.Sender)&&body.RefPlayerId==frame.NativeOwner&&ValidStoredBodyFrame(frame.World,frame.Original,frame.Creation))
            {RebirthPlacedOriginalBodyPublication.Admit(frame.World,frame.Original,frame.NativeOwner,body,frame.Creation);return;}
            var p=physical;if(p!=null&&!p.Failed&&p.Context.Owed==null&&ReferenceEquals(GameManager.Instance?.World,p.Context.World)&&p.Context.Directory==GameIO.GetSaveGameDir()&&p.Context.World.IsRemote()==false&&p.Requested&&!p.Overflow&&p.ConstructedCreation?.id==body.entityId&&body.entityId>0&&p.Creation.Length!=0&&body.RefPlayerId==p.Context.Observation.NativeOwnerEntity&&ReferenceEquals(p.Context.World.GetEntity(body.entityId),body)&&ValidStoredBodyFrame(p.Context.World,p.Context.Observation.Original,p.Creation))RebirthPlacedOriginalBodyPublication.Admit(p.Context.World,p.Context.Observation.Original,p.Context.Observation.NativeOwnerEntity,body,p.Creation);
        }catch{}
    }
    internal static bool HoldsBody(EntityBackpack body)=>RebirthPlacedOriginalBodyPublication.Holds(body);
    internal static void OnBodyRemoving(World world,EntityBackpack body)=>RebirthPlacedOriginalBodyPublication.OnRemoving(world,body);
}


