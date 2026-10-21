using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Collections.Generic;
using HarmonyLib;

#nullable disable

// One native owner-slot conversion and one saved tank mutation. Neither the
// wire request nor the server's live remote inventory is publication evidence.
internal static partial class AdvancedFarmingWaterExchangeOriginal
{
    private const string Marker="RebirthFarmingWaterOriginalV1";
    private const int Maximum=2*1024*1024;
    internal sealed class Record
    {
        internal Guid Operation,Source,Previous;
        internal string World,Owner,Creation,Before,Reserved,Output,Digest=string.Empty;
        internal int Deaths,X,Y,Z,Slot,Count,WaterBefore,WaterAfter,MaxWater,State;
        internal ulong Revision;internal uint Raw;internal long Generation;
        internal bool Invoking;
        internal string Variable=>"rbFarmingWater_phase";
    }
    private sealed class Attempt {internal Record Record;internal World World;internal string Directory;internal EntityPlayerLocal Player;internal float Next;internal int CustodyDeaths=-1,CustodySlot=-1;internal bool CustodyBag,Deferred;internal SlotPublication PendingSlot;}
    private static readonly Dictionary<Guid,Attempt> Owners=new Dictionary<Guid,Attempt>();
    private static readonly Dictionary<Guid,Record> Servers=new Dictionary<Guid,Record>();
    private static World activeWorld;private static string activeDirectory;private static bool quarantine,reconciled;private static EntityPlayerLocal reconciledOwner;private static int reconciledDeaths=-1;private static float nextRead;
    private static ItemStackGrid permittedGrid;private static int permittedSlot;private static ItemStack permittedValue;private static Attempt permittedAttempt;private static bool permitUsed;
    private static string Native(ItemValue item)=>RebirthNativeItemCodec.Encode(item);
    private static ItemValue Item(string image)
    {if(image==null||image.Length>262144||!RebirthNativeItemCodec.TryDecode(image,out var item)||item==null||item.IsEmpty()||item.ItemClass==null)throw new InvalidDataException();return item;}
    private static bool Exact(ItemStack stack,string image,int count)=>stack!=null&&stack.count==count&&Native(stack.itemValue)==image;
    private static string Creation(EntityPlayer player,string owner)=>RebirthSurvivorMode.IsEnabledForCurrentWorld()?RebirthCamperCharacterAccess.CurrentCreation(player):"native-mode:"+owner;
    private static string Encode(Record r)=>new XElement("waterOriginal",new XAttribute("version",1),new XAttribute("operation",r.Operation.ToString("N")),new XAttribute("source",r.Source.ToString("N")),new XAttribute("previous",r.Previous.ToString("N")),new XAttribute("world",r.World),new XAttribute("owner",r.Owner),new XAttribute("creation",r.Creation),new XAttribute("deaths",r.Deaths),new XAttribute("x",r.X),new XAttribute("y",r.Y),new XAttribute("z",r.Z),new XAttribute("slot",r.Slot),new XAttribute("count",r.Count),new XAttribute("waterBefore",r.WaterBefore),new XAttribute("waterAfter",r.WaterAfter),new XAttribute("max",r.MaxWater),new XAttribute("state",r.State),new XAttribute("revision",r.Revision),new XAttribute("raw",r.Raw),new XAttribute("generation",r.Generation),new XAttribute("digest",r.Digest),new XAttribute("invoking",r.Invoking),new XElement("before",r.Before),new XElement("reserved",r.Reserved),new XElement("output",r.Output)).ToString(SaveOptions.DisableFormatting);
    private static Record Decode(string image)
    {
        if(image==null||image.Length>Maximum)throw new InvalidDataException();XElement e;
        using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum}))e=XElement.Load(reader);
        if(e.Name!="waterOriginal"||(int?)e.Attribute("version")!=1)throw new InvalidDataException();
        var r=new Record{Operation=Guid.ParseExact((string)e.Attribute("operation"),"N"),Source=Guid.ParseExact((string)e.Attribute("source"),"N"),Previous=Guid.ParseExact((string)e.Attribute("previous"),"N"),World=(string)e.Attribute("world"),Owner=(string)e.Attribute("owner"),Creation=(string)e.Attribute("creation"),Deaths=(int)e.Attribute("deaths"),X=(int)e.Attribute("x"),Y=(int)e.Attribute("y"),Z=(int)e.Attribute("z"),Slot=(int)e.Attribute("slot"),Count=(int)e.Attribute("count"),WaterBefore=(int)e.Attribute("waterBefore"),WaterAfter=(int)e.Attribute("waterAfter"),MaxWater=(int)e.Attribute("max"),State=(int)e.Attribute("state"),Revision=(ulong)e.Attribute("revision"),Raw=(uint)e.Attribute("raw"),Generation=(long)e.Attribute("generation"),Digest=(string)e.Attribute("digest"),Invoking=(bool)e.Attribute("invoking"),Before=(string)e.Element("before"),Reserved=(string)e.Element("reserved"),Output=(string)e.Element("output")};
        if(r.Operation==Guid.Empty||!Guid.TryParse(r.World,out _)||string.IsNullOrEmpty(r.Owner)||r.Owner.Length>512||string.IsNullOrEmpty(r.Creation)||r.Creation.Length>512||r.Deaths<0||r.Slot<0||r.Slot>=20||r.Count<=0||r.Count>1000000||r.State<0||r.State>3||Encode(r)!=image)throw new InvalidDataException();
        var original=Item(r.Before);var reserved=Item(r.Reserved);var expected=Item(r.Output);var clone=Item(r.Reserved);clone.RemoveMetaData(Marker);
        // A previously completed marker is preserved on the input. The new
        // reservation replaces only this domain's marker on its canonical clone.
        if(original.TryGetMetadata(Marker,out string old))clone.SetMetadata(Marker,old);
        if(Native(clone)!=r.Before||!reserved.TryGetMetadata(Marker,out string marker)||marker!=r.Operation.ToString("N")||!expected.TryGetMetadata(Marker,out string outputMarker)||outputMarker!=marker)throw new InvalidDataException();
        var fresh=ItemClass.GetItem(AdvancedFarmingItemWaterExchange.ResolveChangedItemName(original.ItemClass.GetItemName()));if(fresh==null||fresh.IsEmpty())throw new InvalidDataException();
        // Native fresh ItemValue assigns a time-derived seed. The one original
        // output seed is captured once, never regenerated on retry or decode.
        fresh.Seed=expected.Seed;fresh.SetMetadata(Marker,marker);if(Native(fresh)!=r.Output)throw new InvalidDataException();
        if(r.Source!=Guid.Empty&&(r.Revision==0||r.MaxWater<=0||r.MaxWater>1000000||r.WaterBefore<0||r.WaterAfter<0||r.WaterBefore==r.WaterAfter||r.WaterBefore>r.MaxWater||r.WaterAfter>r.MaxWater||AdvancedFarmingItemWaterExchange.GetWaterDelta(original.ItemClass.GetItemName(),r.Count,r.WaterBefore,r.MaxWater)!=r.WaterAfter-r.WaterBefore))throw new InvalidDataException();
        if(r.Source==Guid.Empty&&(r.State!=0&&r.State!=3||r.Previous!=Guid.Empty||r.Revision!=0||r.Raw!=0||r.WaterBefore!=0||r.WaterAfter!=0||r.MaxWater!=0||r.Generation!=0||r.Digest.Length!=0))throw new InvalidDataException();
        if(r.State==0&&(r.Generation!=0||r.Digest.Length!=0)||r.Invoking&&(r.Source==Guid.Empty||r.State!=0)||(r.State>0&&r.State<3||r.State==3&&r.Source!=Guid.Empty)&&(r.Source==Guid.Empty||r.Digest.Length!=64||r.Digest.Any(ch=>!(ch>='0'&&ch<='9'||ch>='a'&&ch<='f'))||r.Generation<=0))throw new InvalidDataException();return r;
    }
    private static string PathFor(Record r,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","farming-water",server?"server":"owner",r.Operation.ToString("N")+".xml");
    private static bool SameOriginal(Record a,Record b)=>a.Operation==b.Operation&&a.World==b.World&&a.Owner==b.Owner&&a.Creation==b.Creation&&a.Deaths==b.Deaths&&a.X==b.X&&a.Y==b.Y&&a.Z==b.Z&&a.Slot==b.Slot&&a.Count==b.Count&&a.Before==b.Before&&a.Reserved==b.Reserved&&a.Output==b.Output;
    private static bool SameSource(Record a,Record b)=>SameOriginal(a,b)&&a.Source==b.Source&&a.Previous==b.Previous&&a.Revision==b.Revision&&a.Raw==b.Raw&&a.WaterBefore==b.WaterBefore&&a.WaterAfter==b.WaterAfter&&a.MaxWater==b.MaxWater;
    private static bool DirectoryPresent(string path)
    {try{if((File.GetAttributes(path)&FileAttributes.Directory)==0)throw new IOException("Water original store is not a directory.");return true;}catch(DirectoryNotFoundException){return false;}catch(FileNotFoundException){return false;}}
    private static bool Read(string path,out Record r)
    {r=null;try{if(new FileInfo(path).Length>Maximum*3L)return false;r=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));return true;}catch{return false;}}
    private static bool Save(Record r,bool server,string expected=null)
    {
        string path=PathFor(r,server),temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            string image=Encode(r);Decode(image);if(File.Exists(path)||File.Exists(path+".bak")){if(!Read(path,out var old)||!SameOriginal(r,old))return false;if(expected==null)return Encode(old)==image;if(Encode(old)!=expected)return false;}else if(expected!=null)return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            return Read(temporary,out var checkedRecord)&&Encode(checkedRecord)==image&&RebirthDurableFileCommit.TryPublish(temporary,path,out _);
        }catch{return false;}
    }
    private static bool SameOwner(EntityPlayer player,Record r)=>player!=null&&!player.IsDead()&&player.Died>=r.Deaths&&ReferenceEquals(GameManager.Instance?.World,player.world)&&player.world.worldState?.Guid==r.World&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&identity.CanonicalId==r.Owner&&Creation(player,r.Owner)==r.Creation;
    private static bool Actor(EntityPlayer player,Record r)=>SameOwner(player,r)&&player.Died==r.Deaths;
    private static bool Current(Attempt a)=>!a.Deferred&&SameOwner(a.Player,a.Record)&&a.Player.Died==(a.CustodyDeaths<0?a.Record.Deaths:a.CustodyDeaths)&&ReferenceEquals(a.World,GameManager.Instance?.World)&&a.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(a.World.GetPrimaryPlayer(),a.Player)&&ReferenceEquals(a.World.GetEntity(a.Player.entityId),a.Player)&&a.Player.IsSpawned();
    private static int Slot(Attempt a)=>a.CustodySlot<0?a.Record.Slot:a.CustodySlot;
    private static ItemStack Held(Attempt a)=>a.CustodyBag?a.Player.bag.ItemGrid.items[Slot(a)]:a.Player.inventory.GetItem(Slot(a));
    // Recovery follows one positively present native original. Death counters,
    // an absent slot, or an arbitrary same-type replacement never grant custody.
    internal static bool TryLocateCustody(Record r,ItemStack[] bag,ItemStack[] belt,ItemStack cursor,out bool inBag,out int slot)
    {
        inBag=false;slot=-1;int found=0;
        foreach(bool candidateBag in new[]{true,false})
        {
            var rows=candidateBag?bag:belt;if(rows==null)return false;
            for(int i=0;i<rows.Length;i++)
            {
                var value=rows[i];if(value==null||value.IsEmpty()||!value.itemValue.TryGetMetadata(Marker,out string marker)||marker!=r.Operation.ToString("N"))continue;
                if(!Exact(value,r.Reserved,r.Count)&&!Exact(value,r.Output,r.Count))return false;
                found++;inBag=candidateBag;slot=i;
            }
        }
        if(cursor!=null&&!cursor.IsEmpty()&&cursor.itemValue.TryGetMetadata(Marker,out string cursorMarker)&&cursorMarker==r.Operation.ToString("N"))return false;
        return found==1;
    }
    private static bool RebindCustody(Attempt a)
    {
        if(!SameOwner(a.Player,a.Record)||!a.Player.IsSpawned()||!Owners.Values.Any(Current)&&ForeignBusy(a.Player)||!TryLocateCustody(a.Record,a.Player.bag?.ItemGrid?.items,a.Player.inventory?.ItemGrid?.items,a.Player.DragAndDropItem,out bool bag,out int slot))return false;
        a.CustodyDeaths=a.Player.Died;a.CustodyBag=bag;a.CustodySlot=slot;return true;
    }
    private static bool ForeignBusy(EntityPlayerLocal player)=>RebirthDogLifecycleService.HasHireInventoryHold(player)||RebirthPlacedOriginal.IsHeld(player)||RebirthCookingOutputDelivery.IsHeld(player)||RebirthLogisticsOwnerReceipt.IsHeld(player)||AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthCamperEnergyOwnerHold.Blocks(player.inventory)||RebirthCamperStationNativeActorPayment.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.inventory);
    private static void PromoteOwner()
    {
        if(Owners.Values.Any(Current))return;
        foreach(var a in Owners.Values.OrderBy(value=>value.Record.Operation))
            if(a.Deferred&&RebindCustody(a)){a.Deferred=false;return;}
    }
    internal static bool IsHeld(EntityPlayerLocal player)=>Owners.Values.Any(a=>ReferenceEquals(a.Player,player)&&Current(a));
    // Exact requested-operation observation only. No reconciliation, admission,
    // directory creation, publication or inferred absence authority occurs here.
    internal static object CaptureAuditState(EntityPlayer actor,Guid operation)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||actor==null||operation==Guid.Empty||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return new{validScope=false,operation=operation.ToString("N")};
        var records=new List<object>();bool authoritative=!world.IsRemote();
        foreach(bool server in authoritative?actor is EntityPlayerLocal?new[]{true,false}:new[]{true}:new[]{false})
        {
            string role=server?"server":"owner",path=PathFor(new Record{Operation=operation},server);bool archived=false;
            if(!File.Exists(path)&&File.Exists(path+".done")){path+=".done";archived=true;}
            if(!File.Exists(path)){records.Add(new{role,status="missing",absenceIsSettlementProof=false});continue;}
            if(!Read(path,out var r)||r.Operation!=operation){records.Add(new{role,status="corrupt",absenceIsSettlementProof=false});continue;}
            if(r.World!=world.worldState.Guid||r.Owner!=owner.CanonicalId||r.Creation!=Creation(actor,r.Owner)){records.Add(new{role,status="outside-owner-scope",absenceIsSettlementProof=false});continue;}
            bool bag=false;int slot=-1;bool located=false;try{if(actor is EntityPlayerLocal local)located=TryLocateCustody(r,local.bag?.ItemGrid?.items,local.inventory?.ItemGrid?.items,local.DragAndDropItem,out bag,out slot);}catch{/* Unknown native image remains unproven. */}
            bool PositiveSaved(bool output){try{return authoritative&&Saved(actor,r,output);}catch{return false;}}
            Owners.TryGetValue(operation,out var attempt);records.Add(new{role,status="readable",archived,ownerAttemptPresent=attempt!=null,ownerAttemptDeferred=attempt?.Deferred??false,ownerAttemptCurrent=attempt!=null&&Current(attempt),state=r.State,originalDeaths=r.Deaths,currentDeaths=actor.Died,currentOriginalEpisode=Actor(actor,r),sameOwner=SameOwner(actor,r),sourceIncarnation=r.Source.ToString("N"),sourceRevision=r.Revision,sourceProofPresent=r.Digest.Length==64&&r.Generation>0,sourceProof=r.Digest,sourceGeneration=r.Generation,invoking=r.Invoking,nativeFatalReadable=ReadFatal(r,out _),ownerLocationObserved=actor is EntityPlayerLocal,uniqueOriginalLocated=located,locatedBag=located&&bag,locatedSlot=located?slot:-1,beforeFingerprint=Hash(Encoding.UTF8.GetBytes(r.Before)),reservedFingerprint=Hash(Encoding.UTF8.GetBytes(r.Reserved)),outputFingerprint=Hash(Encoding.UTF8.GetBytes(r.Output)),savedReserved=PositiveSaved(false),savedOutput=PositiveSaved(true),absenceIsSettlementProof=false});
        }
        return new{validScope=true,operation=operation.ToString("N"),authoritative,currentDeaths=actor.Died,records=records.ToArray()};
    }
    internal static bool Blocks(ItemStackGrid grid)=>grid!=null&&(permittedAttempt!=null&&Current(permittedAttempt)&&ReferenceEquals(permittedGrid,grid)||Owners.Values.Any(a=>Current(a)&&(ReferenceEquals(a.Player.bag.ItemGrid,grid)||ReferenceEquals(a.Player.inventory.ItemGrid,grid))));
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {if(permitUsed||permittedAttempt==null||!Current(permittedAttempt)||!ReferenceEquals(permittedGrid,grid)||permittedSlot!=slot||!ReferenceEquals(permittedValue,value))return false;permitUsed=true;return true;}
    private static bool Set(Attempt a,string from,string to)
    {
        var inventory=a.Player.inventory;if(!Current(a)||!Exact(Held(a),from,a.Record.Count))return false;var next=new ItemStack(Item(to),a.Record.Count);
        if(!BeginSlotPublication(a,from,to,next))return false;
        permittedAttempt=a;permittedGrid=a.CustodyBag?a.Player.bag.ItemGrid:inventory.ItemGrid;permittedSlot=Slot(a);permittedValue=next;permitUsed=false;
        // Installed Inventory.SetItem constructs a different ItemStack before
        // SetStackAt. Call that same native terminal setter with the one sealed
        // instance so the exact-reference permit cannot authorize another write.
        try{if(a.CustodyBag)a.Player.bag.SetSlot(Slot(a),next);else inventory.SetStackAt(Slot(a),next);return Current(a)&&permitUsed&&PublishSlot(a,to);}finally{permittedAttempt=null;permittedGrid=null;permittedValue=null;permitUsed=false;if(a.PendingSlot?.Observed!=true){a.PendingSlot?.Dispose();a.PendingSlot=null;}}
    }
    private static void Upload(EntityPlayerLocal player){GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
    internal static bool Begin(ItemInventoryData data,Vector3i position)
    {
        try
        {
            if(!AdvancedFarmingRuntimePolicy.Enabled||!ThreadManager.IsMainThread()||data?.holdingEntity is not EntityPlayerLocal player||player.IsDead()||!player.IsSpawned()||data.world is not World world||!ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
            Scope(world);if(!reconciled||!ReferenceEquals(reconciledOwner,player)||reconciledDeaths!=player.Died)Reconcile(world);if(quarantine||Owners.Values.Any(Current)||ForeignBusy(player)||player.DragAndDropItem!=null&&!player.DragAndDropItem.IsEmpty())return false;
            int slot=data.slotIdx;var stack=player.inventory.GetItem(slot);if(stack==null||stack.IsEmpty()||stack.count<=0||slot!=player.inventory.holdingItemIdx)return false;
            var output=ItemClass.GetItem(AdvancedFarmingItemWaterExchange.ResolveChangedItemName(stack.itemValue.ItemClass.GetItemName()));if(output==null||output.IsEmpty())return false;
            Guid operation=Guid.NewGuid();var reserved=Item(Native(stack.itemValue));reserved.SetMetadata(Marker,operation.ToString("N"));output.SetMetadata(Marker,operation.ToString("N"));
            var r=new Record{Operation=operation,World=world.worldState.Guid,Owner=identity.CanonicalId,Creation=Creation(player,identity.CanonicalId),Deaths=player.Died,X=position.x,Y=position.y,Z=position.z,Slot=slot,Count=stack.count,Before=Native(stack.itemValue),Reserved=Native(reserved),Output=Native(output)};
            Decode(Encode(r));if(!Save(r,false))return false;Owners.Add(operation,new Attempt{Record=r,Player=player,World=world,Directory=GameIO.GetSaveGameDir()});return true;
        }catch(Exception error){Log.Warning("[REBIRTH Farming] water original retained: "+error.GetType().Name);return false;}
    }
    private static bool Saved(EntityPlayer player,Record r,bool output)
    {
        if(!SameOwner(player,r)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var actor,out var file)||file.id!=player.entityId||file.deaths!=player.Died||!Buff(file,r,out float phase)||phase!=(output?2f:1f)||!TryLocateCustody(r,actor.Bag,actor.Belt,actor.Cursor,out bool bag,out int slot))return false;
        return Exact((bag?actor.Bag:actor.Belt)[slot],output?r.Output:r.Reserved,r.Count);
    }
    private static void SetPhase(EntityPlayerLocal player,Record r,float phase)
    {
        var bytes=r.Operation.ToByteArray();for(int i=0;i<8;i++)player.Buffs.SetCustomVar("rbFarmingWater_id"+i,(ushort)(bytes[2*i]|bytes[2*i+1]<<8),true);
        player.Buffs.SetCustomVar(r.Variable,phase,true);
    }
    private static bool Buff(PlayerDataFile file,Record r,out float result)
    {
        result=0;var bytes=file.buffData?.ToArray();if(bytes==null||bytes.Length>1024*1024)return false;
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);count=reader.ReadUInt16();var values=new Dictionary<string,float>();
            for(int i=0;i<count;i++){string name=reader.ReadString();float value=reader.ReadSingle();if(name!=r.Variable&&!(name.Length==18&&name.StartsWith("rbFarmingWater_id",StringComparison.Ordinal)&&name[17]>='0'&&name[17]<='7'))continue;if(values.ContainsKey(name)||float.IsNaN(value)||float.IsInfinity(value))return false;values.Add(name,value);}
            if(!values.TryGetValue(r.Variable,out result)||stream.Position!=stream.Length)return false;var id=new byte[16];for(int i=0;i<8;i++){if(!values.TryGetValue("rbFarmingWater_id"+i,out float value)||value<0||value>ushort.MaxValue||value!=Math.Floor(value))return false;int limb=(int)value;id[2*i]=(byte)limb;id[2*i+1]=(byte)(limb>>8);}return new Guid(id)==r.Operation;
        }
    }
    private static bool Focus(World world,EntityPlayer player,Vector3i position)
    {
        var p=player.GetBlockPosition();if(Math.Abs((long)p.x-position.x)>8||Math.Abs((long)p.y-position.y)>8||Math.Abs((long)p.z-position.z)>8)return false;
        var ray=player.GetLookRay();ray.origin+=ray.direction.normalized*.5f;
        return Voxel.Raycast(world,ray,Constants.cDigAndBuildDistance,-538480645,4095,0f)&&Voxel.voxelRayHitInfo.bHitValid&&Voxel.voxelRayHitInfo.hit.blockPos==position;
    }
    private static Guid IdentityObservation(Guid operation)
    {using(var hash=SHA256.Create())return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("farming-water-identity:"+operation.ToString("N"))).Take(16).ToArray());}
    private static bool MatchesStamp(Record r,TileEntityWaterTankRebirth tank)=>tank!=null&&tank.WaterIncarnation==r.Source&&tank.WaterExchangeOperation==r.Operation&&tank.WaterExchangeRevision==r.Revision&&tank.WaterExchangeBefore==r.WaterBefore&&tank.WaterExchangeAfter==r.WaterAfter;
    private static bool CanApplyPrepared(Record r,TileEntityWaterTankRebirth tank,uint raw)=>r.State==0&&!r.Invoking&&r.Source!=Guid.Empty&&r.Revision>0&&tank!=null&&tank.WaterIncarnation==r.Source&&raw==r.Raw&&tank.WaterExchangeOperation==r.Previous&&tank.WaterExchangeRevision==r.Revision-1&&tank.waterCount==r.WaterBefore;
    internal static bool CanCloseEndedNoEffect(Record r,TileEntityWaterTankRebirth tank,uint raw,int nativeDeaths,bool fatalObserved)=>fatalObserved&&r.Deaths<int.MaxValue&&nativeDeaths==r.Deaths+1&&CanApplyPrepared(r,tank,raw);
    private static void Reply(EntityPlayer owner,Record r)
    {if(owner is EntityPlayerLocal&&ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),owner))ReceiveClient((World)owner.world,owner.entityId,Encode(r));else SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageFarmingWaterOriginalReply>().Setup(owner.entityId,Encode(r)),_attachedToEntityId:owner.entityId);}
    private static void Decline(EntityPlayer owner,Record request)
    {
        var declined=Decode(Encode(request));declined.State=3;if(!Save(declined,true))return;
        Reply(owner,declined);Archive(declined,true);AdvancedFarmingHarvestPublication.Complete(IdentityObservation(request.Operation));
    }
    private static bool PreviousMarkerRetired(Record request)
    {
        var item=Item(request.Before);if(!item.HasMetadata(Marker))return true;
        if(!item.TryGetMetadata(Marker,out string value)||!Guid.TryParseExact(value,"N",out var operation)||operation==Guid.Empty)return false;
        string path=PathFor(new Record{Operation=operation},true);if(!File.Exists(path)&&File.Exists(path+".done"))path+=".done";
        return Read(path,out var prior)&&prior.Operation==operation&&prior.World==request.World&&(prior.State==2||prior.State==3&&prior.Count==request.Count&&prior.Reserved==request.Before);
    }
    internal static void ReceiveServer(World world,int playerId,string image)
    {
        try
        {
            if(world==null||world.IsRemote()||!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World)||!AdvancedFarmingRuntimePolicy.Enabled)return;Scope(world);if(!reconciled)Reconcile(world);if(quarantine)return;
            var request=Decode(image);var owner=world.GetEntity(playerId) as EntityPlayer;if(!SameOwner(owner,request))return;
            var position=new Vector3i(request.X,request.Y,request.Z);Record r;
            if(!Servers.TryGetValue(request.Operation,out r))
            {
                string path=PathFor(request,true);if(File.Exists(path+".done")){if(!Read(path+".done",out r)||r.State<2||!SameOriginal(r,request)){quarantine=true;return;}Reply(owner,r);return;}
                if(File.Exists(path)||File.Exists(path+".bak")){if(!Read(path,out r)||!SameOriginal(r,request)){quarantine=true;return;}Servers.Add(r.Operation,r);}
                else
                {
                    if(!Actor(owner,request)||request.State!=0||request.Source!=Guid.Empty||!Saved(owner,request,false))return;
                    if(!PreviousMarkerRetired(request)){Decline(owner,request);return;}
                    var block=world.GetBlock(position);var tank=world.GetTileEntity(position) as TileEntityWaterTankRebirth;if(!Focus(world,owner,position)||!AdvancedFarmingItemWaterExchange.IsAdvancedWaterStorage(block)||tank==null||Servers.Values.Any(x=>x.State==0&&x.X==request.X&&x.Y==request.Y&&x.Z==request.Z)){Decline(owner,request);return;}
                    int maximum=AdvancedFarmingItemWaterExchange.ResolveMaxWater(block,tank);int delta=AdvancedFarmingItemWaterExchange.GetWaterDelta(Item(request.Before).ItemClass.GetItemName(),request.Count,tank.waterCount,maximum);if(delta==0||tank.WaterExchangeRevision==ulong.MaxValue){Decline(owner,request);return;}
                    if(tank.WaterIncarnation==Guid.Empty){tank.WaterIncarnation=Guid.NewGuid();tank.setModified();}
                    // Persist the original source incarnation BEFORE preparing an
                    // effect. A legacy tank cannot be rebound after replacement
                    // or a crash between domain preparation and native mutation.
                    Guid birth=IdentityObservation(request.Operation),source=tank.WaterIncarnation;uint raw=block.rawData;
                    if(!AdvancedFarmingHarvestPublication.Observe(birth,source,world,position,()=>ReferenceEquals(world.GetTileEntity(position),tank)&&tank.WaterIncarnation==source&&world.GetBlock(position).rawData==raw,out _))return;
                    r=Decode(Encode(request));r.Source=tank.WaterIncarnation;r.Previous=tank.WaterExchangeOperation;r.Revision=tank.WaterExchangeRevision+1;r.Raw=block.rawData;r.WaterBefore=tank.waterCount;r.WaterAfter=tank.waterCount+delta;r.MaxWater=maximum;
                    // Retain the reservation even if publication is uncertain;
                    // another original cannot claim this same source meanwhile.
                    Servers.Add(r.Operation,r);if(!Save(r,true))return;AdvancedFarmingHarvestPublication.Complete(birth);
                }
            }
            if(!SameOriginal(r,request))return;
            if(r.State==1)AdvancedFarmingHarvestPublication.Complete(r.Operation);
            if(r.State>=2){Reply(owner,r);Archive(r,true);Servers.Remove(r.Operation);return;}
            if(r.State==0)
            {
                if(!Save(r,true))return;
                var current=world.GetTileEntity(position) as TileEntityWaterTankRebirth;if(current==null||current.WaterIncarnation!=r.Source||world.GetBlock(position).rawData!=r.Raw)return;
                bool Stamped()=>MatchesStamp(r,current)&&ReferenceEquals(world.GetTileEntity(position),current)&&world.GetBlock(position).rawData==r.Raw;
                if(!Stamped())
                {
                    // A reclaimed original may observe an already saved source
                    // result; it cannot newly execute an ended-life source intent.
                    if(File.Exists(FatalPath(r))||File.Exists(FatalPath(r)+".tmp")||!Actor(owner,r)||!CanApplyPrepared(r,current,world.GetBlock(position).rawData)||!Saved(owner,r,false))return;
                    string intent=Encode(r);var invoking=Decode(intent);invoking.Invoking=true;if(!Save(invoking,true,intent))return;Servers[r.Operation]=r=invoking;
                    AdvancedFarmingItemWaterExchange.ApplyWaterDelta(world,position,world.GetBlock(position),current,r.WaterAfter-r.WaterBefore);if(current.waterCount!=r.WaterAfter)return;
                    current.WaterExchangeBefore=r.WaterBefore;current.WaterExchangeAfter=r.WaterAfter;current.WaterExchangeOperation=r.Operation;current.WaterExchangeRevision=r.Revision;current.setModified();
                }
                if(!AdvancedFarmingHarvestPublication.Observe(r.Operation,r.Source,world,position,Stamped,out var proof))return;
                string prior=Encode(r);var next=Decode(prior);next.State=1;next.Invoking=false;next.Digest=proof.Digest;next.Generation=proof.Generation;if(!Save(next,true,prior)||!Read(PathFor(next,true),out var published)||Encode(published)!=Encode(next))return;Servers[r.Operation]=r=published;AdvancedFarmingHarvestPublication.Complete(r.Operation);
            }
            if(Saved(owner,r,true)){string prior=Encode(r);var next=Decode(prior);next.State=2;if(!Save(next,true,prior))return;Servers[r.Operation]=r=next;AdvancedFarmingHarvestPublication.Complete(r.Operation);}
            Reply(owner,r);if(r.State==2&&Archive(r,true))Servers.Remove(r.Operation);
        }catch(Exception error){Log.Warning("[REBIRTH Farming] water original source retained: "+error.GetType().Name);}
    }
    internal static void ReceiveClient(World world,int playerId,string image)
    {
        try
        {
            if(world==null||!ReferenceEquals(world,GameManager.Instance?.World))return;Scope(world);var r=Decode(image);var player=world.GetPrimaryPlayer();if(player==null||player.entityId!=playerId||!SameOwner(player,r))return;
            if(!Owners.TryGetValue(r.Operation,out var a)||!SameOriginal(a.Record,r)||r.State<a.Record.State||a.Record.State==3&&r.State!=3||r.State==3&&a.Record.Source!=Guid.Empty&&!SameSource(a.Record,r))return;
            if(a.Record.Source!=Guid.Empty&&(r.Source!=a.Record.Source||r.Revision!=a.Record.Revision||r.WaterBefore!=a.Record.WaterBefore||r.WaterAfter!=a.Record.WaterAfter||r.Raw!=a.Record.Raw))return;
            if(!Save(r,false,Encode(a.Record)))return;a.Record=r;
        }catch(Exception error){Log.Warning("[REBIRTH Farming] water original owner reply retained: "+error.GetType().Name);}
    }
    private static void Scope(World world)
    {string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(world,activeWorld)&&directory==activeDirectory)return;activeWorld=world;activeDirectory=directory;foreach(var owner in Owners.Values)owner.PendingSlot?.Dispose();Owners.Clear();Servers.Clear();quarantine=false;reconciled=false;reconciledOwner=null;reconciledDeaths=-1;nextRead=0;}
    private sealed class Fatal
    {internal Guid Operation;internal string Binding,World,Owner,Creation,FileProof;internal int Deaths,Entity,Mode;}
    private static string Binding(Record r)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(r.Operation.ToByteArray());foreach(string value in new[]{r.World,r.Owner,r.Creation,r.Before,r.Reserved,r.Output})writer.Write(value);
            foreach(int value in new[]{r.Deaths,r.X,r.Y,r.Z,r.Slot,r.Count})writer.Write(value);writer.Flush();return Hash(stream.ToArray());
        }
    }
    private static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-",string.Empty).ToLowerInvariant();}
    private static string FatalPath(Record r)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","farming-water","native-deaths",r.Operation.ToString("N")+".xml");
    private static string FatalImage(Fatal f)=>new XElement("waterNativeDeath",new XAttribute("version",1),new XAttribute("operation",f.Operation.ToString("N")),new XAttribute("binding",f.Binding),new XAttribute("world",f.World),new XAttribute("owner",f.Owner),new XAttribute("creation",f.Creation),new XAttribute("deaths",f.Deaths),new XAttribute("entity",f.Entity),new XAttribute("mode",f.Mode),new XAttribute("fileProof",f.FileProof)).ToString(SaveOptions.DisableFormatting);
    private static bool ReadFatal(Record r,out Fatal fatal,bool pending=false)
    {
        fatal=null;
        try
        {
            string path=FatalPath(r)+(pending?".tmp":string.Empty);if(!File.Exists(path)||new FileInfo(path).Length>16384)return false;string image=File.ReadAllText(path,new UTF8Encoding(false,true));XElement e;
            using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))e=XElement.Load(reader);
            var f=new Fatal{Operation=Guid.ParseExact((string)e.Attribute("operation"),"N"),Binding=(string)e.Attribute("binding"),World=(string)e.Attribute("world"),Owner=(string)e.Attribute("owner"),Creation=(string)e.Attribute("creation"),Deaths=(int)e.Attribute("deaths"),Entity=(int)e.Attribute("entity"),Mode=(int)e.Attribute("mode"),FileProof=(string)e.Attribute("fileProof")};
            if(FatalImage(f)!=image||f.Operation!=r.Operation||f.Binding!=Binding(r)||f.World!=r.World||f.Owner!=r.Owner||f.Creation!=r.Creation||f.Deaths!=r.Deaths||f.Entity<0||f.Mode<0||f.Mode>6||f.FileProof?.Length!=64||f.FileProof.Any(ch=>!(ch>='0'&&ch<='9'||ch>='a'&&ch<='f')))return false;
            fatal=f;return true;
        }catch{return false;}
    }
    // Invoked only by the existing actual server SetDead observation hook.
    // This records a positively present original, never a destruction inference.
    internal static void OnNativeFatal(EntityPlayer player)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(player?.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)||!player.IsDead()||player.Health>0||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return;
            Scope(world);if(!reconciled)Reconcile(world);if(quarantine)return;
            foreach(var r in Servers.Values.ToArray())
            {
                if(r.State>=2||r.Owner!=owner.CanonicalId||r.World!=world.worldState.Guid||r.Creation!=Creation(player,r.Owner)||r.Deaths==int.MaxValue||player.Died<r.Deaths||player.Died>r.Deaths+1||File.Exists(FatalPath(r)))continue;
                if(!RebirthCamperEnergySavedOwner.TryRead(owner,out var actor,out var file)||file.id!=player.entityId||file.deaths!=r.Deaths||!Buff(file,r,out float phase)||phase!=1f&&phase!=2f||!TryLocateCustody(r,actor.Bag,actor.Belt,actor.Cursor,out bool bag,out int slot)||!Exact((bag?actor.Bag:actor.Belt)[slot],phase==1f?r.Reserved:r.Output,r.Count))continue;
                byte[] proof;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(Binding(r));foreach(var blob in new[]{file.bagData,file.inventoryData,file.buffData}){var bytes=blob.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();proof=stream.ToArray();}
                var fatal=new Fatal{Operation=r.Operation,Binding=Binding(r),World=r.World,Owner=r.Owner,Creation=r.Creation,Deaths=r.Deaths,Entity=player.entityId,Mode=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DropOnDeath),FileProof=Hash(proof)};
                string path=FatalPath(r),temporary=path+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(path));
                if(File.Exists(temporary)){if(ReadFatal(r,out var retained,true)&&FatalImage(retained)==FatalImage(fatal))RebirthDurableFileCommit.TryPublish(temporary,path,out _);continue;}
                var image=new UTF8Encoding(false,true).GetBytes(FatalImage(fatal));using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(image,0,image.Length);stream.Flush(true);}if(ReadFatal(r,out _,true))RebirthDurableFileCommit.TryPublish(temporary,path,out _);
            }
        }catch(Exception error){Log.Warning("[REBIRTH Farming] native water death retained: "+error.GetType().Name);}
    }
    internal static void OnNativeOwnerSaved(ClientInfo sender)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||sender==null||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender))return;
            foreach(var r in Servers.Values.ToArray())TryCloseEndedNoEffect(world,r);
        }catch(Exception error){Log.Warning("[REBIRTH Farming] saved water death retained: "+error.GetType().Name);}
    }
    private static void TryCloseEndedNoEffect(World world,Record r)
    {
        if(r.State!=0||r.Invoking||r.Source==Guid.Empty)return;
        if(!File.Exists(FatalPath(r))&&ReadFatal(r,out _,true))RebirthDurableFileCommit.TryPublish(FatalPath(r)+".tmp",FatalPath(r),out _);
        if(!ReadFatal(r,out var fatal))return;
        // Reconnect may assign a new entity ID. The fatal ID authenticates the
        // original observation; current custody uses one exact stable owner.
        var candidates=world.Players.list.Where(p=>SameOwner(p,r)&&p.Died==r.Deaths+1&&ReferenceEquals(world.GetEntity(p.entityId),p)).ToArray();if(candidates.Length!=1)return;var player=candidates[0];
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out _,out var file)||file.id!=player.entityId||file.deaths!=r.Deaths+1)return;
        var position=new Vector3i(r.X,r.Y,r.Z);var tank=world.GetTileEntity(position) as TileEntityWaterTankRebirth;
        bool Unchanged()=>ReferenceEquals(GameManager.Instance?.World,world)&&world.IsRemote()==false&&ReferenceEquals(world.GetTileEntity(position),tank)&&CanCloseEndedNoEffect(r,tank,world.GetBlock(position).rawData,file.deaths,ReadFatal(r,out _));
        if(!Unchanged())return;Guid observation=EndedObservation(r.Operation);
        if(!AdvancedFarmingHarvestPublication.Observe(observation,r.Source,world,position,Unchanged,out var proof))return;
        string prior=Encode(r);var next=Decode(prior);next.State=3;next.Digest=proof.Digest;next.Generation=proof.Generation;
        if(!Save(next,true,prior))return;Servers[r.Operation]=next;AdvancedFarmingHarvestPublication.Complete(observation);
        // A no-effect source disposition releases only this source reservation.
        // The original input remains native custody until positively reclaimed.
        Reply(player,next);if(Archive(next,true))Servers.Remove(r.Operation);
    }
    private static Guid EndedObservation(Guid operation){using(var hash=SHA256.Create())return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("farming-water-ended:"+operation.ToString("N"))).Take(16).ToArray());}
    private static bool Archive(Record r,bool server)
    {
        try
        {
            string path=PathFor(r,server),done=path+".done";
            if(File.Exists(done))return Read(done,out var existing)&&Encode(existing)==Encode(r);
            if(!Read(path,out var current)||Encode(current)!=Encode(r)||r.State<2)return false;
            File.Move(path,done);return true;
        }catch{return false;}
    }
    private static void Reconcile(World world)
    {
        var player=world.GetPrimaryPlayer();
        nextRead=UnityEngine.Time.realtimeSinceStartup+2f;
        try
        {
            foreach(bool server in world.IsRemote()?new[]{false}:new[]{true,false})
            {
                string directory=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","farming-water",server?"server":"owner");if(!DirectoryPresent(directory))continue;
                foreach(string path in Directory.EnumerateFiles(directory,"*.xml"))
                {
                    if(!Read(path,out var r)||!string.Equals(Path.GetFileNameWithoutExtension(path),r.Operation.ToString("N"),StringComparison.OrdinalIgnoreCase)){quarantine=true;continue;}if(r.World!=world.worldState?.Guid)continue;
                    if(server)
                    {
                        if(Servers.TryGetValue(r.Operation,out var known))
                        {
                            if(!SameSource(known,r)||r.State<known.State||r.State==0&&known.Invoking&&!r.Invoking){quarantine=true;continue;}
                            Servers[r.Operation]=r;
                        }
                        if(r.State>=2){if(Archive(r,true))Servers.Remove(r.Operation);continue;}
                        if(r.State==1)AdvancedFarmingHarvestPublication.Complete(r.Operation);
                        if(r.Source==Guid.Empty){quarantine=true;continue;}
                        if(!Servers.ContainsKey(r.Operation))
                        {if(r.State==0&&Servers.Values.Any(other=>other.State==0&&other.X==r.X&&other.Y==r.Y&&other.Z==r.Z))quarantine=true;Servers.Add(r.Operation,r);}
                    }
                    else if(player!=null&&SameOwner(player,r))
                    {
                        if(Owners.TryGetValue(r.Operation,out var known))
                        {
                            if(!SameOriginal(known.Record,r)||r.State<known.Record.State||known.Record.Source!=Guid.Empty&&!SameSource(known.Record,r)){quarantine=true;continue;}
                            known.Record=r;if(!Current(known)&&RebindCustody(known))known.Deferred=Owners.Values.Any(other=>!ReferenceEquals(other,known)&&Current(other));
                        }
                        else
                        {
                            var recovered=new Attempt{Record=r,World=world,Directory=GameIO.GetSaveGameDir(),Player=player};
                            bool located=RebindCustody(recovered)||RebindTerminalCleanup(recovered);
                            if(!located)
                            {
                                bool marked=player.bag.ItemGrid.items.Concat(player.inventory.ItemGrid.items).Concat(new[]{player.DragAndDropItem}).Any(s=>s!=null&&!s.IsEmpty()&&s.itemValue.TryGetMetadata(Marker,out string marker)&&marker==r.Operation.ToString("N"));
                                if(marked||!Actor(player,r)||r.State!=0||!Exact(player.inventory.GetItem(r.Slot),r.Before,r.Count))continue;
                            }
                            recovered.Deferred=Owners.Values.Any(Current);Owners.Add(r.Operation,recovered);
                        }
                    }
                }
            }
            RestoreArchivedOwner(world,player);
        }
        catch(Exception error){quarantine=true;Log.Warning("[REBIRTH Farming] water original store retained: "+error.GetType().Name);}
        if(!quarantine){reconciled=true;reconciledOwner=player;reconciledDeaths=player?.Died??-1;}
    }
    private static void RestoreArchivedOwner(World world,EntityPlayerLocal player)
    {
        if(player==null||player.IsDead()||!player.IsSpawned())return;
        // A native save can retain the terminal marker after domain retirement
        // if metadata cleanup/upload was interrupted. Read only exact markers
        // currently present, never enumerate an unbounded terminal archive.
        var operations=new HashSet<Guid>();
        foreach(var stack in player.bag.ItemGrid.items.Concat(player.inventory.ItemGrid.items).Take(512))
            if(stack!=null&&!stack.IsEmpty()&&stack.itemValue.TryGetMetadata(Marker,out string marker)&&Guid.TryParseExact(marker,"N",out var operation)&&operation!=Guid.Empty)operations.Add(operation);
        foreach(var operation in operations)
        {
            if(Owners.ContainsKey(operation))continue;string path=PathFor(new Record{Operation=operation},false)+".done";if(!File.Exists(path))continue;
            if(!Read(path,out var r)||r.Operation!=operation||r.State<2){quarantine=true;continue;}
            if(!SameOwner(player,r))continue;
            var a=new Attempt{Record=r,World=world,Player=player,Directory=GameIO.GetSaveGameDir()};if(!RebindCustody(a))continue;
            if(!Exact(Held(a),r.State==2?r.Output:r.Reserved,r.Count))continue;
            a.Deferred=Owners.Values.Any(Current);Owners.Add(operation,a);
        }
    }
    internal static void Pump(World world)
    {
        if(world==null||!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World))return;Scope(world);var player=world.GetPrimaryPlayer();
        if(UnityEngine.Time.realtimeSinceStartup>=nextRead)Reconcile(world);
        if(quarantine)return;
        if(!world.IsRemote())foreach(var r in Servers.Values.ToArray())try{TryCloseEndedNoEffect(world,r);}catch(Exception error){Log.Warning("[REBIRTH Farming] ended water original retained: "+error.GetType().Name);}
        PromoteOwner();
        foreach(var a in Owners.Values.ToArray())
        {
            if(a.PendingSlot!=null&&a.PendingSlot.Deaths!=a.Player.Died){a.PendingSlot.Dispose();a.PendingSlot=null;}
            if(!Current(a)||UnityEngine.Time.realtimeSinceStartup<a.Next)continue;a.Next=UnityEngine.Time.realtimeSinceStartup+1f;var r=a.Record;
            try
            {
                if(r.State==2)
                {
                    // Only a saved server-terminal receipt can retire this
                    // item's own marker. A later gesture/item is never restored.
                    if(Exact(Held(a),r.Output,r.Count))
                    {var clean=Item(r.Output);clean.RemoveMetaData(Marker);if(!Set(a,r.Output,Native(clean)))continue;}
                    var cleaned=Item(r.Output);cleaned.RemoveMetaData(Marker);if(!HasSlotPublication(a,Native(cleaned)))continue;
                    SetPhase(player,r,3f);Upload(player);if(!SavedTerminalCleanup(a,Native(cleaned)))continue;
                    if(Archive(r,false))Owners.Remove(r.Operation);continue;
                }
                if(r.State==3)
                {
                    if(Exact(Held(a),r.Reserved,r.Count)&&!Set(a,r.Reserved,r.Before))continue;
                    if(!Exact(Held(a),r.Before,r.Count)||!HasSlotPublication(a,r.Before))continue;SetPhase(player,r,3f);Upload(player);if(!SavedTerminalCleanup(a,r.Before))continue;if(Archive(r,false))Owners.Remove(r.Operation);continue;
                }
                var held=Held(a);if(r.State==0)
                {
                    if(Exact(held,r.Before,r.Count)&&!Set(a,r.Before,r.Reserved))continue;
                    if(!Exact(Held(a),r.Reserved,r.Count)||!HasSlotPublication(a,r.Reserved))continue;SetPhase(player,r,1f);
                }
                else
                {
                    if(Exact(held,r.Reserved,r.Count)&&!Set(a,r.Reserved,r.Output))continue;
                    if(!Exact(Held(a),r.Output,r.Count)||!HasSlotPublication(a,r.Output))continue;SetPhase(player,r,2f);
                }
                Upload(player);
                if(world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageFarmingWaterOriginalRequest>().Setup(player.entityId,Encode(r)));else ReceiveServer(world,player.entityId,Encode(r));
            }catch(Exception error){Log.Warning("[REBIRTH Farming] water original retry retained: "+error.GetType().Name);}
        }
    }
    private static Harmony installed;
    internal static void Install()
    {if(installed!=null)return;var harmony=new Harmony("rebirth.farming.water-original");Install(harmony);installed=harmony;}
    private static void Install(Harmony harmony)
    {foreach(var type in new[]{typeof(AdvancedFarmingWaterOriginalGridSlot),typeof(AdvancedFarmingWaterOriginalGridBulk),typeof(AdvancedFarmingWaterOriginalGridAdd),typeof(AdvancedFarmingWaterOriginalGridCount),typeof(AdvancedFarmingWaterOriginalDecItem),typeof(AdvancedFarmingWaterOriginalBagAdd),typeof(AdvancedFarmingWaterOriginalBagPartial),typeof(AdvancedFarmingWaterOriginalHeldUse),typeof(AdvancedFarmingWaterOriginalActions),typeof(AdvancedFarmingWaterOriginalSlotGesture),typeof(AdvancedFarmingWaterOriginalCursorSet),typeof(AdvancedFarmingWaterOriginalCursorGesture)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);}
}

internal static class FarmingWaterOriginalWire
{
    internal static string Read(PooledBinaryReader r){int n=r.ReadInt32();if(n<0||n>2*1024*1024)throw new InvalidDataException();var bytes=r.ReadBytes(n);if(bytes.Length!=n)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);}
    internal static void Write(PooledBinaryWriter w,string image){var bytes=new UTF8Encoding(false,true).GetBytes(image);if(bytes.Length>2*1024*1024)throw new InvalidDataException();w.Write(bytes.Length);w.Write(bytes);}
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageFarmingWaterOriginalRequest:NetPackage
{
    private int player;private string image=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageFarmingWaterOriginalRequest Setup(int id,string value){player=id;image=value;return this;}
    public override void read(PooledBinaryReader r){player=r.ReadInt32();image=FarmingWaterOriginalWire.Read(r);}public override void write(PooledBinaryWriter w){base.write(w);w.Write(player);FarmingWaterOriginalWire.Write(w,image);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==false&&ValidEntityIdForSender(player))AdvancedFarmingWaterExchangeOriginal.ReceiveServer(world,player,image);}public int GetLength()=>8+Encoding.UTF8.GetByteCount(image);
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageFarmingWaterOriginalReply:NetPackage
{
    private int player;private string image=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageFarmingWaterOriginalReply Setup(int id,string value){player=id;image=value;return this;}
    public override void read(PooledBinaryReader r){player=r.ReadInt32();image=FarmingWaterOriginalWire.Read(r);}public override void write(PooledBinaryWriter w){base.write(w);w.Write(player);FarmingWaterOriginalWire.Write(w,image);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)AdvancedFarmingWaterExchangeOriginal.ReceiveClient(world,player,image);}public int GetLength()=>8+Encoding.UTF8.GetByteCount(image);
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.SetItem))]
internal static class AdvancedFarmingWaterOriginalGridSlot
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,int _index,ItemStack _stack,out object __state)
    {__state=null;if(!AdvancedFarmingWaterExchangeOriginal.Blocks(__instance))return true;if(!AdvancedFarmingWaterExchangeOriginal.Consume(__instance,_index,_stack))return false;__state=AdvancedFarmingWaterExchangeOriginal.EnterSlotPublication(__instance,_index,_stack);return __state!=null;}
    private static void Postfix(ItemStackGrid __instance,int _index,ItemStack _stack,bool __runOriginal,object __state)
        =>AdvancedFarmingWaterExchangeOriginal.ObserveSlotPublication(__instance,_index,_stack,__runOriginal,__state);
}
[HarmonyPatch]
internal static class AdvancedFarmingWaterOriginalGridBulk
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(string name in new[]{"SetItems","ClearItems","Compact","Sort","CompactSlots","CopyItemsFrom","CopyFrom","Reset","Clear","Resize","ReadInto","SetSlotLocks","SetLocked"})yield return AccessTools.Method(typeof(ItemStackGrid),name)??throw new MissingMethodException(typeof(ItemStackGrid).Name,name);}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance)=>!AdvancedFarmingWaterExchangeOriginal.Blocks(__instance);
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.AddItem))]
internal static class AdvancedFarmingWaterOriginalGridAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!AdvancedFarmingWaterExchangeOriginal.Blocks(__instance))return true;__result=-1;return false;}
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.ChangeCount))]
internal static class AdvancedFarmingWaterOriginalGridCount
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!AdvancedFarmingWaterExchangeOriginal.Blocks(__instance))return true;__result=0;return false;}
}
[HarmonyPatch]
internal static class AdvancedFarmingWaterOriginalDecItem
{
    private static IEnumerable<MethodBase> TargetMethods()
    {yield return AccessTools.Method(typeof(Bag),nameof(Bag.DecItem));yield return AccessTools.Method(typeof(Inventory),nameof(Inventory.DecItem));}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance,ref int __result)
    {
        var grid=__instance is Bag bag?bag.ItemGrid:(__instance as Inventory)?.ItemGrid;
        if(!AdvancedFarmingWaterExchangeOriginal.Blocks(grid))return true;__result=0;return false;
    }
}
[HarmonyPatch(typeof(Bag),nameof(Bag.AddItem))]
internal static class AdvancedFarmingWaterOriginalBagAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref bool __result){if(!AdvancedFarmingWaterExchangeOriginal.Blocks(__instance.ItemGrid))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(Bag),nameof(Bag.TryStackItem))]
internal static class AdvancedFarmingWaterOriginalBagPartial
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref (bool anyMoved,bool allMoved) __result){if(!AdvancedFarmingWaterExchangeOriginal.Blocks(__instance.ItemGrid))return true;__result=(false,false);return false;}
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.UseHoldingItem),new[]{typeof(int),typeof(bool)})]
internal static class AdvancedFarmingWaterOriginalHeldUse
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityAlive __instance,ref bool __result){if(!AdvancedFarmingWaterExchangeOriginal.IsHeld(__instance as EntityPlayerLocal))return true;__result=false;return false;}
}
[HarmonyPatch]
internal static class AdvancedFarmingWaterOriginalActions
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in new[]{typeof(ItemActionEntryUse),typeof(ItemActionEntryDrop),typeof(ItemActionEntryScrap),typeof(ItemActionEntryEquip),typeof(ItemActionEntryWear),typeof(ItemActionEntryRepair),typeof(ItemActionEntrySell),typeof(ItemActionEntryPurchase),typeof(ItemActionEntryTake),typeof(ItemActionEntryCraft),typeof(ItemActionEntryCombine),typeof(ItemActionEntryAssemble)})
            yield return AccessTools.DeclaredMethod(type,"OnActivated",Type.EmptyTypes)??throw new MissingMethodException(type.FullName,"OnActivated");
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(BaseItemActionEntry __instance)=>!AdvancedFarmingWaterExchangeOriginal.IsHeld(__instance?.ItemController?.xui?.playerUI?.entityPlayer);
}

// Fence the gesture before its first cursor/native-slot side effect. Guarding only
// the eventual bag setter is too late: native SwapItem writes the cursor first.
[HarmonyPatch]
internal static class AdvancedFarmingWaterOriginalSlotGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
            yield return AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes)??throw new MissingMethodException(typeof(XUiC_ItemStack).Name,name);
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_ItemStack __instance)
        =>!AdvancedFarmingWaterExchangeOriginal.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
[HarmonyPatch(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.SetCurrentStack))]
internal static class AdvancedFarmingWaterOriginalCursorSet
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_DragAndDropWindow __instance,ItemStack stack)
        =>!AdvancedFarmingWaterExchangeOriginal.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
[HarmonyPatch]
internal static class AdvancedFarmingWaterOriginalCursorGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var method in AccessTools.GetDeclaredMethods(typeof(XUiC_DragAndDropWindow)))
            if(method.Name=="DropCurrentItem"||method.Name=="PlaceItemBackInInventory")yield return method;
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_DragAndDropWindow __instance)
        =>!AdvancedFarmingWaterExchangeOriginal.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
