using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
using HarmonyLib;

#nullable disable
// Placement owns its accepted native action, native one-slot payment and target
// publication. The shared native change codec has no admission authority.
internal static class RebirthPlacedOriginal
{
    private const int Maximum=2*1024*1024;
    private const string PhaseKey="rbPlacedOriginal_phase",IdPrefix="rbPlacedOriginal_id";
    internal sealed class Record
    {
        internal Guid Operation;internal string World,Owner,Creation,Input,Before,After,Changes=string.Empty,TargetProof=string.Empty,Provenance=string.Empty,TargetBirth=string.Empty,Electrical=string.Empty,Footprint=string.Empty;
        internal int NativeEntity,Deaths,Slot,Count,X,Y,Z,Phase;internal uint BeforeBlock;internal bool Attempted,Cancelled,Declined;
        internal Vector3i Position=>new Vector3i(X,Y,Z);
    }
    private sealed class Attempt{internal Record Record;internal EntityPlayerLocal Player;internal World World;internal string Directory;internal ItemActionPlaceAsBlock Action;internal ItemActionData Data;internal bool Released,Running,Debit,DebitObserved,CallbackFinished,CoroutineCreated,CoroutineFinished,ConversionFactory,NativePaidUploaded;internal float Next;}
    private static readonly Dictionary<Guid,Attempt> owners=new Dictionary<Guid,Attempt>();
    private static readonly Dictionary<Guid,Record> servers=new Dictionary<Guid,Record>();
    private static readonly Dictionary<Guid,RebirthOriginalNativeBlockIdentity.Admission> targets=new Dictionary<Guid,RebirthOriginalNativeBlockIdentity.Admission>();
    [ThreadStatic] private static Attempt native;
    [ThreadStatic] private static Attempt coroutine;
    [ThreadStatic] private static Record hostNative;
    [ThreadStatic] private static Attempt returning;
    private static World scope;private static string directory;private static float nextScan;private static bool blocked,installed,scanReady;private static IEnumerator<string> scan,localOwnerScan;private static bool localOwnerReady;private static float nextOwnerScan;private static readonly HashSet<Guid> projected=new HashSet<Guid>();private static readonly Queue<Guid> projectionOrder=new Queue<Guid>();private static readonly Dictionary<string,Guid> episodes=new Dictionary<string,Guid>(StringComparer.Ordinal);
    internal static string EncodeActor(RebirthCamperEnergyActorImage actor){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){actor.Write(writer);writer.Flush();return Convert.ToBase64String(stream.ToArray());}}
    private static RebirthCamperEnergyActorImage ActorImage(string image){var bytes=Convert.FromBase64String(image);if(bytes.Length>Maximum)throw new InvalidDataException();using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream)){var actor=RebirthCamperEnergyActorImage.Read(reader);if(stream.Position!=stream.Length||EncodeActor(actor)!=image)throw new InvalidDataException();return actor;}}
    private static ItemValue Item(string image){if(image==null||image.Length>262144||!RebirthNativeItemCodec.TryDecode(image,out var item)||item==null||item.IsEmpty())throw new InvalidDataException();return item;}
    private static string Body(Record r)=>new XElement("placedOriginal",new XAttribute("version",1),new XAttribute("operation",r.Operation.ToString("N")),new XAttribute("world",r.World),new XAttribute("owner",r.Owner),new XAttribute("creation",r.Creation),new XAttribute("deaths",r.Deaths),new XAttribute("nativeEntity",r.NativeEntity),new XAttribute("slot",r.Slot),new XAttribute("count",r.Count),new XAttribute("x",r.X),new XAttribute("y",r.Y),new XAttribute("z",r.Z),new XAttribute("beforeBlock",r.BeforeBlock),new XAttribute("phase",r.Phase),new XAttribute("attempted",r.Attempted),new XAttribute("cancelled",r.Cancelled),new XAttribute("declined",r.Declined),new XElement("input",r.Input),new XElement("before",r.Before),new XElement("after",r.After),new XElement("changes",r.Changes),new XElement("targetProof",r.TargetProof),new XElement("provenance",r.Provenance),new XElement("targetBirth",r.TargetBirth),r.Electrical.Length==0?null:new XElement("electrical",r.Electrical),r.Footprint.Length==0?null:new XElement("footprint",r.Footprint)).ToString(SaveOptions.DisableFormatting);
    private static string Encode(Record r)
    {string body=Body(r);string checksum;using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-","").ToLowerInvariant();var document=XElement.Parse(body);document.SetAttributeValue("checksum",checksum);return document.ToString(SaveOptions.DisableFormatting);}
    private static Record Decode(string text)
    {
        if(text==null||text.Length>Maximum)throw new InvalidDataException();XElement e;using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum}))e=XElement.Load(reader);
        if(e.Name!="placedOriginal"||(int?)e.Attribute("version")!=1)throw new InvalidDataException();
        var r=new Record{Operation=Guid.ParseExact((string)e.Attribute("operation"),"N"),World=(string)e.Attribute("world"),Owner=(string)e.Attribute("owner"),Creation=(string)e.Attribute("creation"),Deaths=(int)e.Attribute("deaths"),NativeEntity=(int)e.Attribute("nativeEntity"),Slot=(int)e.Attribute("slot"),Count=(int)e.Attribute("count"),X=(int)e.Attribute("x"),Y=(int)e.Attribute("y"),Z=(int)e.Attribute("z"),BeforeBlock=(uint)e.Attribute("beforeBlock"),Phase=(int)e.Attribute("phase"),Attempted=(bool)e.Attribute("attempted"),Cancelled=(bool)e.Attribute("cancelled"),Declined=(bool)e.Attribute("declined"),Input=(string)e.Element("input"),Before=(string)e.Element("before"),After=(string)e.Element("after"),Changes=(string)e.Element("changes"),TargetProof=(string)e.Element("targetProof"),Provenance=(string)e.Element("provenance"),TargetBirth=(string)e.Element("targetBirth"),Electrical=(string)e.Element("electrical")??string.Empty,Footprint=(string)e.Element("footprint")??string.Empty};
        if(r.Operation==Guid.Empty||!Guid.TryParse(r.World,out _)||!RebirthOriginalActionScope.Validate(r.Owner,r.Creation)||r.NativeEntity<=0||r.Deaths<0||r.Slot<0||r.Slot>=20||r.Count<1||r.Count>ushort.MaxValue||r.Y<0||r.Y>=255||r.Phase<0||r.Phase>5||Encode(r)!=text)throw new InvalidDataException();
        var before=ActorImage(r.Before);var after=ActorImage(r.After);if(r.Slot>=before.Belt.Length||before.Belt[r.Slot].count!=r.Count||RebirthNativeItemCodec.Encode(before.Belt[r.Slot].itemValue)!=r.Input)throw new InvalidDataException();Item(r.Input);
        var expected=ExpectedAfter(before,r.Slot,Item(r.Input),after.Belt[r.Slot].itemValue);if(!expected.Matches(after))throw new InvalidDataException();
        if(r.Changes.Length>2048||r.Changes.Length!=0&&(!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var changes)||changes.Count!=1||!changes[0].blockValueRef.TryGetBlockPos(out var pos)||pos!=r.Position||!changes[0].bChangeBlockValue||changes[0].blockValue.isair||changes[0].blockValue.ischild||changes[0].bChangeDamage||changes[0].bChangeTexture))throw new InvalidDataException();
        RebirthPlacedOriginalFootprint.Decode(r.Footprint);if(r.Electrical.Length>24000||r.Provenance.Length>12000||r.TargetBirth.Length!=0&&(!Guid.TryParseExact(r.TargetBirth,"N",out var birth)||birth==Guid.Empty))throw new InvalidDataException();
        if(r.Declined&&(r.Cancelled||r.Phase<4)||r.Cancelled&&(r.Phase!=5||r.TargetProof.Length!=0)||!r.Cancelled&&r.Phase>=2&&r.Changes.Length==0||r.TargetProof.Length!=0&&(r.TargetProof.Length!=64||r.TargetProof.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))||!r.Cancelled&&r.Phase>=4&&r.TargetProof.Length!=64)throw new InvalidDataException();return r;
    }
    private static string Episode(Record r)=>r.World+"|"+r.Owner+"|"+r.Creation+"|"+r.Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static void Index(Record r)
    {if(r.Phase==5)return;string key=Episode(r);if(episodes.TryGetValue(key,out var existing)&&existing!=r.Operation)episodes[key]=Guid.Empty;else episodes[key]=r.Operation;}
    private static bool OwnEpisode(Record r)=>scanReady&&episodes.TryGetValue(Episode(r),out var operation)&&operation==r.Operation;
    private static void ReleaseEpisode(Record r){if(r.Phase==5&&episodes.TryGetValue(Episode(r),out var operation)&&operation==r.Operation)episodes.Remove(Episode(r));}
    private static bool Same(Record a,Record b)=>a.Operation==b.Operation&&a.World==b.World&&a.Owner==b.Owner&&a.Creation==b.Creation&&a.NativeEntity==b.NativeEntity&&a.Deaths==b.Deaths&&a.Slot==b.Slot&&a.Count==b.Count&&a.Position==b.Position&&a.BeforeBlock==b.BeforeBlock&&a.Input==b.Input&&a.Before==b.Before&&a.After==b.After;
    private static string PathFor(Record r,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"server":"owner",r.Operation.ToString("N")+".xml");
    private static bool TemporaryCandidate(string path){string folder=Path.GetDirectoryName(path);return Directory.Exists(folder)&&Directory.EnumerateFiles(folder,Path.GetFileNameWithoutExtension(path)+".*.tmp").Any();}
    private static bool Read(string path,out Record r){r=null;try{if(new FileInfo(path).Length>Maximum*3L)return false;r=Decode(File.ReadAllText(path,new UTF8Encoding(false,true)));return Path.GetFileName(path).StartsWith(r.Operation.ToString("N")+".",StringComparison.Ordinal);}catch{return false;}}
    private static string TerminalPath(string path)=>Path.Combine(Path.GetDirectoryName(path),"terminal",Path.GetFileName(path));
    private static bool ReadOriginal(string path,out Record r)
    {
        r=null;string terminal=TerminalPath(path);if(File.Exists(path)){if(!Read(path,out r))return false;if(File.Exists(terminal)&&(!Read(terminal,out var archived)||archived.Phase!=5||Encode(archived)!=Encode(r)))return false;return true;}
        return Read(terminal,out r)&&r.Phase==5;
    }
    private static bool ArchiveTerminal(Record r,bool server)
    {
        if(r.Phase!=5)return false;string active=PathFor(r,server),terminal=TerminalPath(active);
        try
        {
            string text=Encode(r);if(File.Exists(terminal)){if(!Read(terminal,out var old)||Encode(old)!=text)return false;}
            else{Directory.CreateDirectory(Path.GetDirectoryName(terminal));string temp=terminal+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(text);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(!Read(temp,out var check)||Encode(check)!=text||!RebirthDurableFileCommit.TryPublish(temp,terminal,out _))return false;}
            if(!Read(terminal,out var saved)||Encode(saved)!=text)return false;
            if(File.Exists(active)){if(!Read(active,out var current)||Encode(current)!=text)return false;File.Delete(active);}
            // Old backup is evidence, not an active admission candidate. Keep it
            // beside the positively saved terminal instead of deleting custody.
            if(File.Exists(active+".bak")&&!File.Exists(terminal+".prior"))File.Move(active+".bak",terminal+".prior");return true;
        }catch{return false;}
    }
    private static bool Save(Record r,bool server,string expected=null)
    {
        try{string path=PathFor(r,server),text=Encode(r);Decode(text);if(File.Exists(path)||File.Exists(path+".bak")){if(!Read(path,out var old)||!Same(old,r)||expected==null&&Encode(old)!=text||expected!=null&&Encode(old)!=expected)return false;}else if(expected!=null)return false;Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(text);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return Read(temp,out var check)&&Encode(check)==text&&RebirthDurableFileCommit.TryPublish(temp,path,out _);}catch{return false;}
    }
    private static bool Current(EntityPlayer actor,Record r)=>ThreadManager.IsMainThread()&&actor!=null&&!actor.IsDead()&&actor.Died==r.Deaths&&actor.world?.worldState?.Guid==r.World&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&RebirthCamperCharacterAccess.CurrentCreation(actor)==r.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var id)&&id.StorageKey==r.Owner;
    private static bool CanRelay(Attempt a)=>SameNativeOwner(a.Player,a.Record)&&ReferenceEquals(a.World,GameManager.Instance?.World)&&a.Directory==GameIO.GetSaveGameDir()&&a.Player.IsSpawned()&&a.Player.Died>a.Record.Deaths&&a.Record.Phase<5;
    private static bool Current(Attempt a)=>Current(a.Player,a.Record)&&ReferenceEquals(a.World,GameManager.Instance?.World)&&a.Directory==GameIO.GetSaveGameDir()&&a.Player.IsSpawned();
    internal static bool IsHeld(EntityPlayerLocal player)=>RebirthPlacedOriginalDeathPartition.IsOwedHeld(player)||owners.Values.Any(a=>ReferenceEquals(a.Player,player)&&a.Record.Phase<5&&Current(a));
    internal static bool Blocks(ItemStackGrid grid)=>RebirthPlacedOriginalDeathPartition.BlocksOwed(grid)||owners.Values.Any(a=>a.Record.Phase<5&&Current(a)&&(ReferenceEquals(grid,a.Player.bag?.ItemGrid)||ReferenceEquals(grid,a.Player.inventory?.ItemGrid)));
    internal static bool Consume(ItemStackGrid grid,int index,ItemStack value)
    {
        if(RebirthPlacedOriginalDeathPartition.ConsumeOwed(grid,index,value))return true;var a=coroutine??returning??native;if(a==null||!Current(a)||a.Debit||!ReferenceEquals(grid,a.Player.inventory.ItemGrid)||index!=a.Record.Slot||!RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(returning==a?a.Record.After:a.Record.Before))||!RebirthCamperEnergyItems.Same(value,ActorImage(returning==a?a.Record.Before:a.Record.After).Belt[index]))return false;a.Debit=true;return true;
    }
    internal static void ObserveDebit(ItemStackGrid grid)
    {
        var a=coroutine??native;if(a==null||!Current(a)||!a.Debit||!ReferenceEquals(grid,a.Player.inventory.ItemGrid))return;
        try{if(RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(a.Record.After)))a.DebitObserved=true;}catch{}
    }
    internal static bool CanAdmitOwed(EntityPlayerLocal p)=>p!=null&&!IsHeld(p)&&!Busy(p);
    private static bool Busy(EntityPlayerLocal p)=>RebirthDogLifecycleService.HasHireInventoryHold(p)||RebirthCookingOutputDelivery.IsHeld(p)||RebirthLogisticsOwnerReceipt.IsHeld(p)||AdvancedFarmingHarvestOwnerDelta.IsHeld(p)||AdvancedFarmingWaterExchangeOriginal.IsHeld(p)||RebirthGearOwnerReservation.IsHeld(p)||RebirthBackpackLibraryReservation.IsHeld(p)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(p)||RebirthCamperEnergyOwnerHold.Blocks(p.bag)||RebirthCamperEnergyOwnerHold.Blocks(p.inventory)||RebirthCamperStationNativeActorPayment.Blocks(p.bag)||RebirthCamperStationNativeActorPayment.Blocks(p.inventory);
    private static void SetPhase(EntityPlayerLocal player,Record r,int phase){var bytes=r.Operation.ToByteArray();for(int i=0;i<8;i++)player.Buffs.SetCustomVar(IdPrefix+i,1+(bytes[2*i]|bytes[2*i+1]<<8),true);player.Buffs.SetCustomVar(PhaseKey,phase,true);}
    private static bool SavedPhase(PlayerDataFile file,Record r,int phase)
    {
        var bytes=file.buffData?.ToArray();if(bytes==null||bytes.Length>1024*1024)return false;
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);count=reader.ReadUInt16();var own=new Dictionary<string,float>();for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key==PhaseKey||key.StartsWith(IdPrefix,StringComparison.Ordinal)){if(own.ContainsKey(key)||float.IsNaN(value)||float.IsInfinity(value))return false;own.Add(key,value);}}if(stream.Position!=stream.Length||!own.TryGetValue(PhaseKey,out float actual)||actual!=phase)return false;var id=r.Operation.ToByteArray();for(int i=0;i<8;i++)if(!own.TryGetValue(IdPrefix+i,out float limb)||limb!=1+(id[2*i]|id[2*i+1]<<8))return false;return true;}
    }
    private static bool Saved(EntityPlayer player,Record r,int phase)
    {return Current(player,r)&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&RebirthCamperEnergySavedOwner.TryRead(identity,out var actor,out var file)&&file.id==player.entityId&&file.deaths==r.Deaths&&SavedPhase(file,r,phase)&&actor.Matches(ActorImage(phase==1||phase==4||r.Declined&&phase==3?r.Before:r.After));}
    private static void Upload(EntityPlayerLocal p){GameManager.Instance.SaveLocalPlayerData();if(p.world.IsRemote())GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
    internal static bool BeforeAction(ItemActionPlaceAsBlock action,ItemActionData data,bool released)
    {
        if(native!=null)return ReferenceEquals(native.Action,action)&&ReferenceEquals(native.Data,data)&&Current(native);
        var p=data?.invData?.holdingEntity as EntityPlayerLocal;var world=p?.world as World;
        if(p==null||world==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!released)return true;
        if(!world.IsRemote()&&(!ReferenceEquals(world.GetPrimaryPlayer(),p)||!RebirthPlacedWorkmanshipService.CanCaptureOriginal(world)))return false;
        Scope(world);if(!scanReady)Reconcile(world);if(!world.IsRemote()&&!localOwnerReady)ReconcileLocalOwner(world,p);if(!scanReady||!world.IsRemote()&&!localOwnerReady||IsHeld(p)||Busy(p)||blocked)return false;
        if(UnityEngine.Time.time-data.lastUseTime<action.Delay||UnityEngine.Time.time-data.lastUseTime<Constants.cBuildIntervall)return true;
        // Unsupported special producers retain their ordinary native semantics;
        // they are not falsely attributed through this bounded voxel original.
        if(data.invData.item==null||data.invData.itemValue==null||!p.HitInfo.bHitValid||p.HitInfo.lastBlockPos==Vector3i.zero)return true;
        if(!TryTarget(p,new Record{Input=RebirthNativeItemCodec.Encode(data.invData.itemValue)},out var value))return true;if(!world.IsRemote()&&RebirthInfrastructureWorkService.IsElectricalBlock(value.Block)&&!RebirthInfrastructureWorkService.CanCaptureOriginal(world))return false;
        try{if(!RebirthStablePlayerIdentity.TryResolveServerEntity(p,out var id))return false;var before=RebirthCamperEnergyActorImage.Capture(p);int slot=p.inventory.holdingItemIdx;if(slot<0||slot>=before.Belt.Length||before.Belt[slot].IsEmpty()||!before.Cursor.IsEmpty())return false;var position=p.HitInfo.lastBlockPos;if(!world.GetBlock(position).isair)return false;string input=RebirthNativeItemCodec.Encode(before.Belt[slot].itemValue);if(input!=RebirthNativeItemCodec.Encode(data.invData.itemValue))return false;var after=ExpectedAfter(before,slot,Item(input));var r=new Record{Operation=Guid.NewGuid(),World=((World)world).worldState.Guid,Owner=id.StorageKey,Creation=RebirthCamperCharacterAccess.CurrentCreation(p),Deaths=p.Died,NativeEntity=p.entityId,Slot=slot,Count=before.Belt[slot].count,X=position.x,Y=position.y,Z=position.z,BeforeBlock=world.GetBlock(position).rawData,Input=input,Before=EncodeActor(before),After=EncodeActor(after)};if(!Save(r,false))return false;owners.Add(r.Operation,new Attempt{Record=r,Player=p,World=world,Directory=GameIO.GetSaveGameDir(),Action=action,Data=data,Released=released});SetPhase(p,r,1);Upload(p);return false;}catch{return false;}
    }
    // Prefix of the actual native SetBlocksRPC. Preserve native owner local
    // ChangeBlocks/event visibility; suppress only that original's vanilla send.
    internal static bool CaptureNative(GameManager game,List<BlockChangeInfo> changes,PlatformUserIdentifierAbs identity)
    {
        var a=native;if(a==null){var host=hostNative;if(host!=null&&IsLightNormalization(host,changes)){game.ChangeBlocks(identity,changes);return false;}return true;}
        if(!Current(a)||!SupportedChanges(a.Player,a.Record,changes)||a.Record.Changes.Length!=0||!RebirthOriginalNativeBlockChange.TryEncode(changes,out var image)||image.Length>2048)throw new InvalidOperationException("Original placement capsule unavailable");
        string old=Encode(a.Record);var next=Decode(old);next.Changes=image;next.Phase=2;if(!Save(next,false,old))throw new IOException("Original placement capsule not saved");a.Record=next;
        if(a.World.IsRemote())game.ChangeBlocks(identity,changes);return false;
    }
    internal static IEnumerator WrapDebit(ItemInventoryData data,int index,IEnumerator original)
    {
        var a=native;if(a==null||!ReferenceEquals(a.Data?.invData,data)||index!=a.Record.Slot)return original;
        a.CoroutineCreated=true;return NativeDebit(a,original);
    }
    private static IEnumerator NativeDebit(Attempt a,IEnumerator original)
    {
        try{while(true){bool more;object yielded;var previous=coroutine;try{coroutine=a;if(!Current(a))yield break;more=original.MoveNext();yielded=more?original.Current:null;}finally{coroutine=previous;}if(!more)yield break;yield return yielded;}}
        finally{a.CoroutineFinished=true;(original as IDisposable)?.Dispose();PublishPaid(a);}
    }
    private static bool RestoreDeclined(Attempt a)
    {
        if(!Current(a)||a.Record.Phase!=4||!a.Record.Declined)return false;var actor=RebirthCamperEnergyActorImage.Capture(a.Player);if(actor.Matches(ActorImage(a.Record.Before)))return true;if(!actor.Matches(ActorImage(a.Record.After)))return false;
        var previous=returning;try{returning=a;a.Debit=false;var original=ActorImage(a.Record.Before).Belt[a.Record.Slot];a.Player.inventory.SetItem(a.Record.Slot,original);return a.Debit&&RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(a.Record.Before));}finally{returning=previous;}
    }
    private static bool PublishPaid(Attempt a)
    {
        try
        {
            if(!Current(a)||a.Record.Phase!=2||!RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(a.Record.After)))return false;
            SetPhase(a.Player,a.Record,2);Upload(a.Player);
            if(a.World.IsRemote()&&!a.NativePaidUploaded)
            {
                var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
                if(connection!=null&&!connection.IsServer&&connection.IsConnected){connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(a.Player),false);a.NativePaidUploaded=true;}
            }
            Send(a);return true;
        }catch{return false;}
    }
    private static RebirthCamperEnergyActorImage ExpectedAfter(RebirthCamperEnergyActorImage before,int slot,ItemValue input,ItemValue admittedOutput=null)
    {
        var action=input.ItemClass.Actions?.OfType<ItemActionPlaceAsBlock>().SingleOrDefault();if(action==null)throw new InvalidDataException();
        var after=before.Copy();
        if(action.changeItemTo!=null){var output=FreshConversion(action.changeItemTo,admittedOutput);if(output.IsEmpty())throw new InvalidDataException();after.Belt[slot]=new ItemStack(output,1);}
        else after.Belt[slot]=before.Belt[slot].count==1?ItemStack.Empty.Clone():new ItemStack(input,before.Belt[slot].count-1);
        return after;
    }
    private static ItemValue FreshConversion(string name,ItemValue admitted)
    {
        if(admitted==null)return ItemClass.GetItem(name);var type=ItemClass.GetItemClass(name);if(type==null||admitted.type!=type.Id)throw new InvalidDataException();int quality=1;
        if(type.HasQuality){var random=GameRandomManager.Instance.CreateGameRandom(admitted.Seed);try{quality=random.RandomRange(1,7);}finally{GameRandomManager.Instance.FreeGameRandom(random);}}
        var expected=new ItemValue(type.Id,quality,quality);expected.Seed=admitted.Seed;if(RebirthNativeItemCodec.Encode(expected)!=RebirthNativeItemCodec.Encode(admitted))throw new InvalidDataException();return admitted.Clone();
    }
    // The native factory is evaluated exactly once when this original is frozen.
    // Its original full fresh result is reused only at this native call site;
    // no input metadata is copied and no global ItemClass factory is patched.
    internal static ItemValue ResolveConversion(string name,bool insensitive)
    {
        var a=native;if(a==null)return ItemClass.GetItem(name,insensitive);
        if(!Current(a)||a.ConversionFactory||insensitive||a.Action.changeItemTo!=name)throw new InvalidOperationException("Original placement conversion unavailable");
        var output=ActorImage(a.Record.After).Belt[a.Record.Slot];if(output.count!=1)throw new InvalidDataException();var value=FreshConversion(name,output.itemValue);a.ConversionFactory=true;return value;
    }
    private static bool TryTarget(EntityPlayer owner,Record r,out BlockValue target)
    {
        target=BlockValue.Air;
        try
        {
            var input=Item(r.Input);var action=input.ItemClass.Actions?.OfType<ItemActionPlaceAsBlock>().SingleOrDefault();if(action==null)return false;if(action.changeItemTo!=null&&ItemClass.GetItemClass(action.changeItemTo)==null)return false;
            var source=input.ItemClass.OnConvertToBlockValue(input,action.blockToPlace);if(source.Block==null||source.Block.GetType().GetMethod(nameof(Block.OnBlockPlaceBefore)).DeclaringType!=typeof(Block))return false;
            target=source;
            if(source.Block.SelectAlternates){target=source.Block.GetAltBlockValue(input.Meta);target.rotation=source.rotation;}
            else if(source.Block.placeAltBlockNames!=null&&source.Block.placeAltBlockNames.Length!=0)return false; // Do not rerun native random selection.
            var block=target.Block;if(block is BlockPowered&&!(block is BlockPoweredLight)&&block.GetType()!=typeof(BlockSwitch)&&block.GetType().GetMethod(nameof(Block.OnBlockAdded)).DeclaringType==typeof(Block))return false;return block!=null&&!target.isair&&(!block.isMultiBlock||block.multiBlockPos!=null&&block.multiBlockPos.Length<=16)&&!(block is BlockPlantGrowingRebirth)&&block.GetType().GetMethod(nameof(Block.PlaceBlock)).DeclaringType==typeof(Block)&&SupportedAdded(block)&&(block.GetType().GetMethod(nameof(Block.OnBlockDamaged)).DeclaringType==typeof(Block)||block.GetType().GetMethod(nameof(Block.OnBlockDamaged)).DeclaringType==typeof(BlockBladeTrap)||block.GetType().GetMethod(nameof(Block.OnBlockDamaged)).DeclaringType==typeof(BlockElectricWire));
        }catch{return false;}
    }
    private static bool SupportedAdded(Block block)
    {var declared=block.GetType().GetMethod(nameof(Block.OnBlockAdded)).DeclaringType;return declared==typeof(Block)||declared==typeof(BlockPowerSource)||declared==typeof(BlockRanged)||declared==typeof(BlockPoweredTrap)||declared==typeof(BlockLauncher)||declared==typeof(BlockTimerRelay)||declared==typeof(BlockPressurePlate)||declared==typeof(BlockMotionSensor)||declared==typeof(BlockElectricWire)||declared==typeof(BlockLight);}
    internal static BlockValue ExpectedBlock(Record r)
    {
        if(!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var changes)||changes.Count!=1)throw new InvalidDataException();var value=changes[0].blockValue;
        if(value.Block is BlockLight light&&light.OriginalLightState(value)!=light.IsLightOn(value))value.meta=(byte)((value.meta&0xfe)|(light.IsLightOn(value)?1:0));return value;
    }
    private static bool IsLightNormalization(Record r,List<BlockChangeInfo> changes)
    {
        if(r.Phase!=3||changes==null||changes.Count!=1||!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var original)||original.Count!=1||!(original[0].blockValue.Block is BlockLight)||ExpectedBlock(r).rawData==original[0].blockValue.rawData)return false;
        var change=changes[0];return change.blockValueRef.TryGetBlockPos(out var pos)&&pos==r.Position&&change.bChangeBlockValue&&!change.bChangeDensity&&!change.bChangeDamage&&!change.bChangeTexture&&!change.bForceDensity&&change.blockValue.rawData==ExpectedBlock(r).rawData;
    }
    internal static string Digest(Record r)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {writer.Write(r.Operation.ToByteArray());writer.Write(r.World);writer.Write(r.Owner);writer.Write(r.Creation);writer.Write(r.NativeEntity);writer.Write(r.Deaths);writer.Write(r.Slot);writer.Write(r.Count);writer.Write(r.X);writer.Write(r.Y);writer.Write(r.Z);writer.Write(r.BeforeBlock);writer.Write(r.Input);writer.Write(r.Before);writer.Write(r.After);writer.Flush();using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
    }
    internal static bool CanInvokeOwner(Record r)=>r!=null&&r.Phase==1&&!r.Attempted&&!r.Cancelled&&!r.Declined&&r.Changes.Length==0&&r.TargetBirth.Length==32&&r.Provenance.Length!=0;
    internal static bool CanContinueEnded(Record r,bool positivePaid)=>r!=null&&r.Phase>=1&&r.Phase<=2&&!r.Cancelled&&!r.Declined&&r.TargetBirth.Length==32&&r.Provenance.Length!=0&&positivePaid;
    internal static bool CanInvokeHost(Record r)=>r!=null&&r.Phase==2&&r.Attempted&&!r.Cancelled&&!r.Declined&&r.Changes.Length!=0&&r.TargetBirth.Length==32&&r.Provenance.Length!=0;
    // Read-only observation: no original creation, scope reset or native save.
    internal static object CaptureAuditState(EntityPlayer actor,Guid operation)
    {
        if(!ThreadManager.IsMainThread()||actor?.world==null||operation==Guid.Empty||!ReferenceEquals(GameManager.Instance?.World,actor.world))return new{status="unavailable"};
        try
        {
            bool server=!actor.world.IsRemote();string path=PathFor(new Record{Operation=operation},server);
            bool exists=File.Exists(path)||File.Exists(TerminalPath(path));
            if(!ReadOriginal(path,out var r))return new{status=exists?"corrupt-or-conflicting":"missing"};
            bool nativeFatal=ReadDeath(r,out var death)&&death.Fatal;
            return new{status="present",nativeFatal,historicalPaid=nativeFatal&&death.PaidProof.Length==64,historicalPaidProof=nativeFatal?death.PaidProof:string.Empty,endedUnusedProof=nativeFatal?death.UnusedProof:string.Empty,operation=r.Operation.ToString("N"),r.Phase,r.Attempted,r.Cancelled,r.Declined,current=Current(actor,r),archived=File.Exists(TerminalPath(path)),position=new[]{r.X,r.Y,r.Z},r.TargetBirth,originalDigest=Digest(r),paidInputHash=AuditHash(r.Input),beforeHash=AuditHash(r.Before),afterHash=AuditHash(r.After),nativeChangesHash=AuditHash(r.Changes),provenanceHash=AuditHash(r.Provenance),electricalHash=AuditHash(r.Electrical),footprintHash=AuditHash(r.Footprint),footprintCells=RebirthPlacedOriginalFootprint.Decode(r.Footprint).Count,targetProof=r.TargetProof,terminal=r.Phase==5,exclusive=OwnEpisode(r)};
        }
        catch{return new{status="unavailable"};}
    }
    private static string AuditHash(string text){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text??string.Empty))).Replace("-","").ToLowerInvariant();}
    internal static bool TryReadTerminal(World world,Guid operation,out Record r)
    {r=null;return ThreadManager.IsMainThread()&&world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,world)&&ReadOriginal(PathFor(new Record{Operation=operation},true),out r)&&r.World==((World)world).worldState.Guid&&r.Phase==5;}
    internal static bool HasSavedTerminal(World world,Guid operation,string digest,Vector3i position,string beforeBirth)
    {
        try{if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||operation==Guid.Empty)return false;var probe=new Record{Operation=operation};return ReadOriginal(PathFor(probe,true),out var r)&&r.Operation==operation&&r.World==((World)world).worldState.Guid&&(r.Position==position&&r.TargetBirth==beforeBirth||RebirthPlacedOriginalFootprint.HasTerminalCell(r,position,beforeBirth))&&Digest(r)==digest&&r.Phase==5&&(r.Cancelled||r.TargetProof.Length==64);}catch{return false;}
    }
    private static bool SupportedChanges(EntityPlayer owner,Record r,List<BlockChangeInfo> changes)
    {
        if(changes.Count!=1)return false;var change=changes[0];if(change.changedByEntityId!=r.NativeEntity||!change.blockValueRef.TryGetBlockPos(out var pos)||pos!=r.Position||!change.bChangeBlockValue||change.bChangeDamage||change.bChangeTexture||change.bForceDensity||change.blockValue.isair||change.blockValue.ischild)return false;
        if(!TryTarget(owner,r,out var expected)||expected.type!=change.blockValue.type)return false;
        bool density=!expected.Block.IsTerrainDecoration;if(change.bChangeDensity!=density||density&&change.density!=(expected.Block.shape.IsTerrain()?expected.Block.Density:MarchingCubes.DensityAir))return false;
        return true;
    }
    private static void Send(Attempt a)=>SendRecord(a,a.Record);
    private static void SendRecord(Attempt a,Record r)
    {if(!Current(a)&&!CanRelay(a))return;if(a.World.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedOriginalRequest>().Setup(a.Player.entityId,Encode(r)));else if(ReferenceEquals(a.World.GetPrimaryPlayer(),a.Player))ReceiveServer(a.World,a.Player.entityId,Encode(r));}
    private static void Reply(World world,EntityPlayer owner,Record r){if(r.Declined||r.Cancelled&&r.Changes.Length!=0){if(!CorrectPrediction(world,owner,r))return;}else if(r.Phase>=4&&!r.Cancelled&&!projected.Contains(r.Operation)&&CorrectPrediction(world,owner,r,true))MarkProjected(r.Operation);if(owner is EntityPlayerLocal local&&ReferenceEquals(world.GetPrimaryPlayer(),local))ReceiveClient(world,local.entityId,Encode(r));else SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPlacedOriginalReply>().Setup(owner.entityId,Encode(r)),_attachedToEntityId:owner.entityId);}
    private static void MarkProjected(Guid operation){if(!projected.Add(operation))return;projectionOrder.Enqueue(operation);while(projectionOrder.Count>256)projected.Remove(projectionOrder.Dequeue());}
    private static bool CorrectPrediction(World world,EntityPlayer owner,Record r,bool all=false)
    {
        var chunk=world.GetChunkSync(World.toChunkXZ(r.X),World.toChunkXZ(r.Z)) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;
        var positions=new List<Vector3i>{r.Position};
        if(r.Changes.Length!=0&&RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var original)&&original.Count==1&&original[0].blockValue.Block.isMultiBlock){var parent=original[0].blockValue;var footprint=parent.Block.multiBlockPos;if(footprint==null||footprint.Length>16)return false;for(int i=0;i<footprint.Length;i++){var position=r.Position+footprint.Get(i,parent.type,parent.rotation);if(!positions.Contains(position))positions.Add(position);}}
        var changes=new List<BlockChangeInfo>();foreach(var position in positions){var currentChunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;if(currentChunk==null||currentChunk.InProgressUnloading)return false;var reference=new BlockValueRef(position);changes.Add(new BlockChangeInfo(reference,world.GetBlock(position),world.ChunkCache.GetDensity(reference),owner?.entityId??-1));}
        var player=owner!=null?GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId):GameManager.Instance.GetPersistentLocalPlayer();if(owner!=null&&player?.PrimaryId==null)return false;
        var packet=NetPackageManager.GetPackage<NetPackageSetBlock>().Setup(player,changes,owner?.entityId??(GameManager.IsDedicatedServer?-1:GameManager.Instance.myPlayerId));if(all)GameManager.Instance.SetBlocksOnClients(-1,packet);else SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(packet,_attachedToEntityId:owner.entityId);if(all&&r.Electrical.Length!=0&&world.GetTileEntity(r.Position) is TileEntityPowered tile)tile.SetModified();return true;
    }
    internal static void ReceiveServer(World world,int playerId,string image)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||blocked||!ReferenceEquals(GameManager.Instance?.World,world))return;Scope(world);var request=Decode(image);var owner=world.GetEntity(playerId) as EntityPlayer;bool current=Current(owner,request);bool ended=!current&&SameNativeOwner(owner,request)&&owner.Died>request.Deaths;
            if(!current&&!ended||!RebirthOriginalActionScope.Matches(owner,request.Owner,request.Creation))return;
            string path=PathFor(request,true);Record r;
            if(File.Exists(path)||File.Exists(path+".bak")||File.Exists(TerminalPath(path))||TemporaryCandidate(path)){if(!ReadOriginal(path,out r)||!Same(r,request))return;}
            else
            {
                if(!scanReady||ended)return;
                if(episodes.ContainsKey(Episode(request)))return;
                if(request.Phase==5&&request.Cancelled&&!request.Attempted&&request.Footprint.Length==0&&request.Changes.Length==0&&request.Provenance.Length==0&&request.Electrical.Length==0&&request.TargetBirth.Length==0&&Saved(owner,request,4)){if(Save(request,true))Reply(world,owner,request);return;}
                if(request.Phase!=0||request.Footprint.Length!=0||request.NativeEntity!=owner.entityId||request.Attempted||request.Changes.Length!=0||request.Provenance.Length!=0||request.Electrical.Length!=0||request.TargetBirth.Length!=0||!Saved(owner,request,1))return;
                if(!TargetRawBefore(world,owner,request)){var rejected=Decode(Encode(request));rejected.Phase=5;rejected.Cancelled=true;if(Save(rejected,true))Reply(world,owner,rejected);return;}
                r=Decode(Encode(request));if(!TryTarget(owner,r,out var target))return;r.Provenance=RebirthPlacedWorkmanshipService.FreezeRemotePlacement(world,r.Position,owner,Item(r.Input),target);if(r.Provenance==null){RejectBeforeCallback(world,owner,request);return;}if(RebirthInfrastructureWorkService.IsElectricalBlock(target.Block)){r.Electrical=RebirthInfrastructureWorkService.FreezeOriginalPlacement(world,r.Position,owner,Item(r.Input),target);if(r.Electrical==null){RejectBeforeCallback(world,owner,request);return;}}r.Phase=0;if(!Save(r,true))return;
            }
            if(ended&&r.Phase!=5&&(request.Phase<2||!request.Attempted||request.Changes.Length==0||r.Phase<1||r.Phase<=2&&!CanContinueEnded(r,HasHistoricalPaid(r))||r.Declined||r.Cancelled))return;
            servers[r.Operation]=r;Index(r);
            if(r.Phase==0&&OwnEpisode(r)&&!(request.Phase==5&&request.Cancelled))
            {
                if(!Saved(owner,r,1)||!TargetRawBefore(world,owner,r))return;
                if(!targets.TryGetValue(r.Operation,out var admission)||!admission.Current())
                {
                    admission?.Dispose();var bound=r;
                    admission=r.TargetBirth.Length==0?RebirthOriginalNativeBlockIdentity.Begin(world,r.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,Digest(r),()=>Current(owner,bound)&&TargetRawBefore(world,owner,bound)):RebirthOriginalNativeBlockIdentity.Rebind(world,r.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,Digest(r),r.TargetBirth,()=>Current(owner,bound)&&TargetRawBefore(world,owner,bound));
                    if(admission==null)return;targets[r.Operation]=admission;
                }
                if(r.TargetBirth.Length==0){string old=Encode(r);var bound=Decode(old);bound.TargetBirth=admission.Id;if(!Save(bound,true,old))return;servers[r.Operation]=r=bound;}
                if(!admission.Saved()||!Saved(owner,r,1)||!TargetBefore(world,owner,r))return;
                string before=Encode(r);var permitted=Decode(before);permitted.Phase=1;if(!Save(permitted,true,before))return;servers[r.Operation]=r=permitted;admission.Dispose();targets.Remove(r.Operation);
            }
            if(r.TargetBirth.Length==0&&RebirthOriginalNativeBlockIdentity.TryReadClaim(world,r.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,Digest(r),out string beforeBirth,out _))
            {string old=Encode(r);var bound=Decode(old);bound.TargetBirth=beforeBirth;if(!Save(bound,true,old))return;servers[r.Operation]=r=bound;}
            if((r.Phase==0||r.Phase==1)&&request.Phase==5&&request.Cancelled&&Saved(owner,r,4)){if(request.Changes.Length!=0&&(r.Phase!=1||request.Provenance!=r.Provenance||request.Electrical!=r.Electrical||request.TargetBirth!=r.TargetBirth||!RebirthOriginalNativeBlockChange.TryDecode(request.Changes,out var unusedChanges)||!SupportedChanges(owner,request,unusedChanges)))return;string old=Encode(r);var cancelled=Decode(old);cancelled.Phase=5;cancelled.Cancelled=true;cancelled.Attempted=request.Attempted;cancelled.Changes=request.Changes;if(!Save(cancelled,true,old))return;servers[r.Operation]=r=cancelled;RebirthOriginalNativeBlockIdentity.Retire(world,r.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,Digest(r),r.TargetBirth);RebirthPlacedOriginalFootprint.Retire(world,r);Reply(world,owner,r);if(r.Phase==5){ReleaseEpisode(r);RebirthPlacedOriginalPublication.RetireMarker(world,r);RebirthPlacedOriginalFootprintPublication.Retire(world,r);ArchiveTerminal(r,true);}return;}
            if(OwnEpisode(r)&&r.Phase==1&&request.Phase==2&&request.Attempted&&request.Changes.Length!=0)
            {
                if(request.Provenance!=r.Provenance||request.Electrical!=r.Electrical||request.TargetBirth!=r.TargetBirth||!RebirthOriginalNativeBlockChange.TryDecode(request.Changes,out var changes)||!SupportedChanges(owner,request,changes)||!(current&&Saved(owner,request,2)||ended&&HasHistoricalPaid(r)))return;
                string old=Encode(r);var next=Decode(old);next.Changes=request.Changes;next.Attempted=true;next.Phase=2;if(!Save(next,true,old))return;servers[r.Operation]=r=next;
            }
            if(OwnEpisode(r)&&CanInvokeHost(r))
            {
                if(!(current&&Saved(owner,r,2)||ended&&HasHistoricalPaid(r))||!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var changes)||!SupportedChanges(owner,r,changes))return;
                if(r.Footprint.Length==0){if(!RebirthPlacedOriginalFootprint.TryCreate(world,r,changes[0].blockValue,out var plan)){if(!ended&&TryDecline(world,r,out var rejected)){servers[r.Operation]=r=rejected;Reply(world,owner,r);}return;}string previous=Encode(r);var planned=Decode(previous);planned.Footprint=plan;if(!Save(planned,true,previous))return;servers[r.Operation]=r=planned;}
                if(!RebirthPlacedOriginalFootprint.MatchesPlan(r,changes[0].blockValue))return;
                bool targetValid=RebirthPlacedWorkmanshipService.CanCaptureOriginal(world)&&(r.Electrical.Length==0||RebirthInfrastructureWorkService.CanCaptureOriginal(world))&&TargetBefore(world,owner,r,ended);
                if(targetValid){int readiness=RebirthPlacedOriginalFootprint.Prepare(world,r,()=>((current&&Current(owner,r))||(ended&&SameNativeOwner(owner,r)&&HasHistoricalPaid(r)))&&TargetBefore(world,owner,r,ended),out var amended);if(amended!=null){string previous=Encode(r);var planned=Decode(previous);planned.Footprint=amended;if(!Save(planned,true,previous))return;servers[r.Operation]=r=planned;}if(readiness==0)return;targetValid=readiness==1&&RebirthPlacedOriginalFootprint.BeforeCurrent(world,r);}
                if(!targetValid&&ended){RebirthPlacedOriginalFootprint.ReleaseWatches(r);return;}
                if(!targetValid){RebirthPlacedOriginalFootprint.ReleaseWatches(r);string declinedBefore=Encode(r);var declined=Decode(declinedBefore);declined.Phase=4;declined.Declined=true;using(var hash=SHA256.Create())declined.TargetProof=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("never-host-invoked:"+Digest(r)+"|"+r.Changes))).Replace("-","").ToLowerInvariant();if(!Save(declined,true,declinedBefore))return;servers[r.Operation]=r=declined;Reply(world,owner,r);return;}
                string old=Encode(r);var attempted=Decode(old);attempted.Phase=3;if(!Save(attempted,true,old))return;servers[r.Operation]=r=attempted;RebirthPlacedOriginalFootprint.ReleaseWatches(r);if(!TargetBefore(world,owner,r,ended)||!RebirthPlacedOriginalFootprint.BeforeCurrent(world,r))return;
                var nativeId=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId)?.PrimaryId;if(nativeId==null)return;
                // Rebind only native actor attribution to the same authenticated stable
                // owner after reconnect; frozen original wire bytes stay untouched.
                foreach(var change in changes)change.changedByEntityId=owner.entityId;
                object originalScope=RebirthPlacedWorkmanshipIdentity.BeginPlacement(world,r.Position);
                var priorHost=hostNative;
                try{hostNative=r;GameManager.Instance.ChangeBlocks(nativeId,changes);if(!RebirthPlacedOriginalPowered.CompleteFreshSwitch(world,r,nativeId))return;if(!RebirthPlacedWorkmanshipService.ApplyRemotePlacement(world,r.Position,ExpectedBlock(r),r.Provenance)||r.Electrical.Length!=0&&!RebirthInfrastructureWorkService.ApplyOriginalPlacement(world,r.Position,r.Electrical))return;}
                finally{hostNative=priorHost;RebirthPlacedWorkmanshipIdentity.EndPlacement(originalScope);}
                if(world.GetBlock(r.Position).rawData!=ExpectedBlock(r).rawData)return;
                if(!RebirthPlacedOriginalFootprint.TryCaptureAfter(world,r,out var postimage))return;{string previous=Encode(r);var captured=Decode(previous);captured.Footprint=postimage;if(!Save(captured,true,previous))return;servers[r.Operation]=r=captured;}
                RebirthPlacedOriginalPublication.Stamp(world,r);
                // Other peers receive only the positively published original
                // (or the latest authoritative current projection), never an
                // unproved sidecar/native intent.
            }
            if(r.Phase==3&&TrySettlePublication(world,r,out var published))servers[r.Operation]=r=published;
            if(r.Phase==4&&!r.Declined&&TryTerminalPublished(world,r,out var terminal))servers[r.Operation]=r=terminal;
            if(r.Phase==4&&r.Declined&&request.Phase==5&&Saved(owner,r,3)){string old=Encode(r);var next=Decode(old);next.Phase=5;if(!Save(next,true,old))return;servers[r.Operation]=r=next;RebirthOriginalNativeBlockIdentity.Retire(world,r.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,Digest(r),r.TargetBirth);RebirthPlacedOriginalFootprint.Retire(world,r);}
            Reply(world,owner,r);if(r.Phase==5){ReleaseEpisode(r);RebirthPlacedOriginalPublication.RetireMarker(world,r);RebirthPlacedOriginalFootprintPublication.Retire(world,r);ArchiveTerminal(r,true);servers.Remove(r.Operation);}
        }catch(Exception ex){Log.Warning("[REBIRTH Provenance Original] retained server original: "+ex.GetType().Name);}
    }
    private static void RejectBeforeCallback(World world,EntityPlayer owner,Record request)
    {var rejected=Decode(Encode(request));rejected.Phase=5;rejected.Cancelled=true;if(Save(rejected,true)){Reply(world,owner,rejected);ArchiveTerminal(rejected,true);}}
    private static bool TryDecline(World world,Record r,out Record declined)
    {
        declined=r;if(r.Phase!=2||!r.Attempted||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||!Read(PathFor(r,true),out var saved)||Encode(saved)!=Encode(r))return false;
        string old=Encode(r);var next=Decode(old);next.Phase=4;next.Declined=true;next.TargetProof=AuditHash("never-host-invoked:"+Digest(r)+"|"+r.Changes);if(!Save(next,true,old))return false;RebirthPlacedOriginalFootprint.ReleaseWatches(r);declined=next;return true;
    }
    private static bool TryTerminalPublished(World world,Record r,out Record terminal)
    {
        terminal=r;if(r.Phase!=4||r.Declined||r.Cancelled||r.TargetProof.Length!=64||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||r.World!=((World)world).worldState.Guid||!Read(PathFor(r,true),out var saved)||Encode(saved)!=Encode(r))return false;
        string old=Encode(r);var next=Decode(old);next.Phase=5;if(!Save(next,true,old))return false;terminal=next;ReleaseEpisode(next);
        RebirthOriginalNativeBlockIdentity.Retire(world,next.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,next.Operation,Digest(next),next.TargetBirth);RebirthPlacedOriginalFootprint.Retire(world,next);RebirthPlacedOriginalPublication.RetireMarker(world,next);RebirthPlacedOriginalFootprintPublication.Retire(world,next);ArchiveTerminal(next,true);return true;
    }
    private static bool TrySettlePublication(World world,Record r,out Record published)
    {
        published=r;
        if(world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||r.World!=((World)world).worldState.Guid||r.Phase!=3||!RebirthPlacedOriginalPublication.Observe(world,r,out var parent)||!RebirthPlacedOriginalFootprintPublication.Observe(world,r,out var children))return false;
        string old=Encode(r);var next=Decode(old);next.Phase=4;next.TargetProof=RebirthPlacedOriginalFootprintPublication.Combine(parent,children);if(!Save(next,true,old))return false;
        RebirthPlacedOriginalPublication.Complete(r.Operation);RebirthPlacedOriginalFootprintPublication.Complete(r);published=next;return true;
    }
    internal static string EncodeRecord(Record r)=>Encode(r);
    internal static Record DecodeRecord(string image)=>Decode(image);
    internal static RebirthCamperEnergyActorImage ReadActor(string image)=>ActorImage(image);
    internal static bool SamePartitionOwner(EntityPlayer actor,Record r)=>SameNativeOwner(actor,r);
    internal static bool SamePartitionOriginal(Record host,Record observed)=>Same(host,observed)&&host.TargetBirth==observed.TargetBirth&&host.Provenance==observed.Provenance&&host.Electrical==observed.Electrical&&(host.Changes.Length==0||host.Changes==observed.Changes);
    internal static void RetireTerminalNativeOwnerMetadata(EntityPlayerLocal player,Guid operation,string originalHash)
    {
        if(!ThreadManager.IsMainThread()||!RebirthPlacedOriginalDeathPartition.HasTerminalNativeRetirement(player,operation,false,originalHash))return;
        if(owners.TryGetValue(operation,out var old)&&ReferenceEquals(old.Player,player)&&AuditHash(Encode(old.Record))==originalHash)owners.Remove(operation);
        // Preserve the old full source sidecar/cvars. The distinct authenticated
        // mirror prevents cold rearm; no item delta, head credit or marker of a
        // newer native original is overwritten by metadata retirement.
    }    internal static bool TryOwnerPartition(EntityPlayerLocal player,out Record original,out bool debitObserved)
    {
        original=null;debitObserved=false;if(!ThreadManager.IsMainThread()||player?.world==null||!ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||!player.IsDead())return false;
        var matches=owners.Values.Where(a=>ReferenceEquals(a.Player,player)&&ReferenceEquals(a.World,player.world)&&a.Directory==GameIO.GetSaveGameDir()&&SameNativeOwner(player,a.Record)&&a.Record.Deaths<int.MaxValue&&player.Died==a.Record.Deaths+1&&a.Record.Phase>=1&&a.Record.Phase<=2&&!a.Record.Cancelled&&!a.Record.Declined&&(a.Record.Phase==1&&!a.Record.Attempted&&a.Record.Changes.Length==0||a.Record.Phase==2&&a.Record.Attempted&&a.Record.Changes.Length!=0)).Take(2).ToArray();if(matches.Length!=1)return false;original=Decode(Encode(matches[0].Record));debitObserved=matches[0].DebitObserved;return true;
    }
    internal static bool TryBodyOriginal(EntityPlayer actor,int nativeOwner,out Record original)
    {
        original=null;if(!ThreadManager.IsMainThread()||actor==null||actor.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world))return false;
        var matches=servers.Values.Where(r=>r.Phase>=1&&r.Phase<=2&&!r.Cancelled&&!r.Declined&&r.Deaths<int.MaxValue&&actor.Died==r.Deaths+1&&SameNativeOwner(actor,r)&&ReadDeath(r,out var death)&&death.Fatal&&death.Entity==nativeOwner).Take(2).ToArray();if(matches.Length!=1)return false;original=Decode(Encode(matches[0]));return true;
    }
    internal static bool ValidatePartition(EntityPlayer actor,Record observed,string policy,int nativeOwnerEntity)
    {
        if(!SameNativeOwner(actor,observed)||observed.Deaths==int.MaxValue||actor.Died!=observed.Deaths+1||!ReadOriginal(PathFor(observed,true),out var host)||host.Phase<1||host.Phase>2||host.Cancelled||host.Declined||!SamePartitionOriginal(host,observed)||!ReadDeath(host,out var death)||!death.Fatal||death.Entity!=nativeOwnerEntity||death.Policy!=policy||observed.Changes.Length!=0&&(!RebirthOriginalNativeBlockChange.TryDecode(observed.Changes,out var changes)||!SupportedChanges(actor,observed,changes)))return false;return true;
    }
    internal static bool ValidateStoredBodyOriginal(World world,Record observed,string policy,int nativeOwner)
    {return world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,world)&&((World)world).worldState.Guid==observed.World&&ReadOriginal(PathFor(observed,true),out var host)&&host.Phase>=1&&host.Phase<=2&&!host.Cancelled&&!host.Declined&&SamePartitionOriginal(host,observed)&&ReadDeath(host,out var death)&&death.Fatal&&death.Entity==nativeOwner&&death.Policy==policy;}
    internal static bool ValidateBodyInvocationOwner(EntityPlayer actor,Record observed,string policy,int nativeOwnerEntity)
    {return SameNativeOwner(actor,observed)&&observed.Deaths<int.MaxValue&&actor.Died>=observed.Deaths+1&&ReadOriginal(PathFor(observed,true),out var host)&&SamePartitionOriginal(host,observed)&&ReadDeath(host,out var death)&&death.Fatal&&death.Entity==nativeOwnerEntity&&death.Policy==policy;}
    private sealed class NativeDeath
    {internal bool Fatal;internal Guid Operation;internal string Binding,World,Owner,Creation,Policy=string.Empty,PaidProof=string.Empty,UnusedProof=string.Empty;internal int Deaths,Entity,SavedDeaths=-1;}
    private static string DeathPath(Record r)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","native-deaths",r.Operation.ToString("N")+".xml");
    private static string DeathImage(NativeDeath d)=>new XElement("placedNativeDeath",new XAttribute("version",1),new XAttribute("fatal",d.Fatal),new XAttribute("policy",d.Policy),new XAttribute("operation",d.Operation.ToString("N")),new XAttribute("binding",d.Binding),new XAttribute("world",d.World),new XAttribute("owner",d.Owner),new XAttribute("creation",d.Creation),new XAttribute("deaths",d.Deaths),new XAttribute("entity",d.Entity),new XAttribute("savedDeaths",d.SavedDeaths),new XAttribute("paidProof",d.PaidProof),new XAttribute("unusedProof",d.UnusedProof)).ToString(SaveOptions.DisableFormatting);
    private static bool ReadDeath(Record r,out NativeDeath death,string path=null)
    {
        death=null;try
        {
            path=path??DeathPath(r);if(!File.Exists(path)||new FileInfo(path).Length>16384)return false;string text=File.ReadAllText(path,new UTF8Encoding(false,true));XElement e;
            using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))e=XElement.Load(reader);
            if(e.Name!="placedNativeDeath"||(int?)e.Attribute("version")!=1)return false;
            var d=new NativeDeath{Fatal=(bool)e.Attribute("fatal"),Policy=(string)e.Attribute("policy"),Operation=Guid.ParseExact((string)e.Attribute("operation"),"N"),Binding=(string)e.Attribute("binding"),World=(string)e.Attribute("world"),Owner=(string)e.Attribute("owner"),Creation=(string)e.Attribute("creation"),Deaths=(int)e.Attribute("deaths"),Entity=(int)e.Attribute("entity"),SavedDeaths=(int)e.Attribute("savedDeaths"),PaidProof=(string)e.Attribute("paidProof"),UnusedProof=(string)e.Attribute("unusedProof")};
            if(DeathImage(d)!=text||d.Policy==null||d.Policy.Length>128||d.Fatal&&!RebirthPlacedOriginalDeathPartition.ValidPolicy(d.Policy)||!d.Fatal&&(d.Policy.Length!=0||d.PaidProof?.Length!=64)||d.Operation!=r.Operation||d.Binding!=Digest(r)||d.World!=r.World||d.Owner!=r.Owner||d.Creation!=r.Creation||d.Deaths!=r.Deaths||d.Entity<=0||d.PaidProof==null||d.UnusedProof==null||d.PaidProof.Length!=0&&d.UnusedProof.Length!=0||new[]{d.PaidProof,d.UnusedProof}.Any(proof=>proof.Length!=0&&(proof.Length!=64||proof.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))))||d.UnusedProof.Length!=0&&(long)d.SavedDeaths!=(long)r.Deaths+1||d.PaidProof.Length==0&&d.UnusedProof.Length==0&&d.SavedDeaths!=-1||(d.PaidProof.Length!=0||d.UnusedProof.Length!=0)&&(d.SavedDeaths<r.Deaths||(long)d.SavedDeaths>(long)r.Deaths+1))return false;
            death=d;return true;
        }catch{return false;}
    }
    private static void RecoverDeath(Record r)
    {
        try
        {
            string path=DeathPath(r);if(File.Exists(path)||File.Exists(path+".bak"))return;string folder=Path.GetDirectoryName(path);if(!Directory.Exists(folder))return;
            var candidates=Directory.EnumerateFiles(folder,r.Operation.ToString("N")+".*.tmp").Take(2).ToArray();if(candidates.Length!=1||!ReadDeath(r,out _,candidates[0]))return;
            // Retry only the exact retained canonical native observation. Unknown
            // or conflicting temporary candidates remain preserved and held.
            RebirthDurableFileCommit.TryPublish(candidates[0],path,out _);
        }catch{}
    }
    private static bool HasHistoricalPaid(Record r)=>ReadDeath(r,out var death)&&death.Fatal&&death.PaidProof.Length==64;
    private static bool SaveDeath(Record r,NativeDeath death,string expected=null)
    {
        try
        {
            string path=DeathPath(r),image=DeathImage(death);if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadDeath(r,out var old)||expected==null&&DeathImage(old)!=image||expected!=null&&DeathImage(old)!=expected)return false;}else if(expected!=null||TemporaryCandidate(path))return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            return ReadDeath(r,out var check,temporary)&&DeathImage(check)==image&&RebirthDurableFileCommit.TryPublish(temporary,path,out _)&&ReadDeath(r,out check)&&DeathImage(check)==image;
        }catch{return false;}
    }
    private static bool SameNativeOwner(EntityPlayer actor,Record r)=>ThreadManager.IsMainThread()&&actor!=null&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&actor.world?.worldState?.Guid==r.World&&RebirthCamperCharacterAccess.CurrentCreation(actor)==r.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.StorageKey==r.Owner;
    // Called solely from the existing server native SetDead observer. No stats
    // counter, empty inventory or later callback can manufacture this event.
    internal static void OnNativeFatal(EntityPlayer actor)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(actor?.world,world)||!actor.IsDead()||actor.Health>0)return;
            RebirthPlacedOriginalDeathPartition.ObserveCurrentOwedFatal(actor);
            foreach(var r in servers.Values.ToArray())
            {
                if(r.Phase<=2&&SameNativeOwner(actor,r))RebirthPlacedOriginalDeathPartition.ObserveOwedFatal(actor,r.Operation);
                if(r.Phase>2||r.Cancelled||r.Declined||r.Deaths==int.MaxValue||!SameNativeOwner(actor,r)||actor.Died<r.Deaths||actor.Died>r.Deaths+1)continue;
                RecoverDeath(r);if(!ReadDeath(r,out var death)){death=new NativeDeath{Fatal=true,Policy=RebirthPlacedOriginalDeathPartition.ServerPolicy(),Operation=r.Operation,Binding=Digest(r),World=r.World,Owner=r.Owner,Creation=r.Creation,Deaths=r.Deaths,Entity=actor.entityId};if(!SaveDeath(r,death))continue;}else if(!death.Fatal){string prior=DeathImage(death);death.Fatal=true;death.Policy=RebirthPlacedOriginalDeathPartition.ServerPolicy();death.Entity=actor.entityId;if(!SaveDeath(r,death,prior))continue;}
                ObserveNativePaid(actor,r);
            }
        }catch(Exception error){Log.Warning("[REBIRTH Provenance Original] native death retained: "+error.GetType().Name);}
    }
    internal static void OnNativeOwnerSaved(ClientInfo sender)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||sender==null||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender))return;
            var actor=world.GetEntity(sender.entityId) as EntityPlayer;RebirthPlacedOriginalDeathPartition.ObserveNativeBodySourceSnapshot(actor);RebirthPlacedOriginalDeathPartition.ObserveCurrentOwedSaved(actor);foreach(var r in servers.Values.ToArray())if(r.Phase<=2&&SameNativeOwner(actor,r)){RebirthPlacedOriginalDeathPartition.ObserveOwedSaved(actor,r.Operation);ObserveNativePaid(actor,r);TryCloseEndedUnused(world,actor,r);}
        }catch(Exception error){Log.Warning("[REBIRTH Provenance Original] saved death retained: "+error.GetType().Name);}
    }
    private static void ObserveNativePaid(EntityPlayer actor,Record r)
    {
        RecoverDeath(r);
        if(SameNativeOwner(actor,r)&&ReadDeath(r,out var retainedDeath)&&retainedDeath.Fatal&&retainedDeath.PaidProof.Length==0&&retainedDeath.UnusedProof.Length==0&&RebirthPlacedOriginalDeathPartition.TryOwedTerminal(r,retainedDeath.Policy,true,out var owedProof,out _)){string previous=DeathImage(retainedDeath);retainedDeath.PaidProof=owedProof;retainedDeath.SavedDeaths=r.Deaths+1;SaveDeath(r,retainedDeath,previous);return;}
        if(!SameNativeOwner(actor,r)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var image,out var file)||file.id!=actor.entityId||file.deaths<r.Deaths||(long)file.deaths>(long)r.Deaths+1 )return;
        bool exists=ReadDeath(r,out var death);if(exists&&(death.PaidProof.Length!=0||death.UnusedProof.Length!=0))return;
        string partitionProof=null;bool partition=exists&&death.Fatal&&(SavedPhase(file,r,1)||SavedPhase(file,r,2))&&RebirthPlacedOriginalDeathPartition.TrySavedProof(r,death.Policy,image,file,out partitionProof);
        if(!partition&&(!SavedPhase(file,r,2)||!image.Matches(ActorImage(r.After))))return;if(!exists)death=new NativeDeath{Operation=r.Operation,Binding=Digest(r),World=r.World,Owner=r.Owner,Creation=r.Creation,Deaths=r.Deaths,Entity=actor.entityId};
        // The exact full original After and original nonce occur in ONE actual
        // primary native file. This observation never rewrites new-life slots.
        string proof=partitionProof;if(!partition)using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(Digest(r));writer.Write(file.id);writer.Write(file.deaths);writer.Write(r.After);foreach(var blob in new[]{file.bagData,file.inventoryData,file.buffData}){var bytes=blob.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
        string old=exists?DeathImage(death):null;death.PaidProof=proof;death.SavedDeaths=file.deaths;SaveDeath(r,death,old);
    }
    private static bool TryCloseEndedUnused(World world,EntityPlayer actor,Record r)
    {
        if(r.Phase>1||r.Cancelled||r.Declined||!SameNativeOwner(actor,r)||r.Deaths==int.MaxValue||actor.Died<r.Deaths+1||!ReadDeath(r,out var death)||!death.Fatal||death.PaidProof.Length!=0)return false;
        string capsule=string.Empty;
        if(death.UnusedProof.Length==0&&RebirthPlacedOriginalDeathPartition.TryOwedTerminal(r,death.Policy,false,out var owedUnused,out var owedCapsule)){string unusedBefore=DeathImage(death);death.UnusedProof=owedUnused;death.SavedDeaths=r.Deaths+1;if(!SaveDeath(r,death,unusedBefore))return false;capsule=owedCapsule;}
        if(death.UnusedProof.Length==0)
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var image,out var file)||file.id!=actor.entityId||file.deaths!=r.Deaths+1)return false;
            string unusedNativeProof=null,unusedCapsule=null;bool typed=(SavedPhase(file,r,1)||SavedPhase(file,r,2))&&RebirthPlacedOriginalDeathPartition.TrySavedUnused(r,death.Policy,image,file,out unusedNativeProof,out unusedCapsule);
            if(!typed&&(!SavedPhase(file,r,4)||!image.Matches(ActorImage(r.Before))))return false;
            string old=DeathImage(death);
            if(typed){capsule=unusedCapsule;death.UnusedProof=unusedNativeProof;}
            else using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write("ended-native-before:");writer.Write(Digest(r));writer.Write(file.id);writer.Write(file.deaths);writer.Write(r.Before);foreach(var blob in new[]{file.bagData,file.inventoryData,file.buffData}){var bytes=blob.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=SHA256.Create())death.UnusedProof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
            death.SavedDeaths=file.deaths;if(!SaveDeath(r,death,old))return false;
        }
        else RebirthPlacedOriginalDeathPartition.TryReadUnusedCapsule(r,out capsule);
        // Native Before was positively bound BEFORE transformation, or explicit
        // original unused4 survived. Host Root0/1 never invoked; no refund/write.
        string previous=Encode(r);var terminal=Decode(previous);terminal.Phase=5;terminal.Cancelled=true;if(!string.IsNullOrEmpty(capsule)){terminal.Changes=capsule;terminal.Attempted=true;}if(!Save(terminal,true,previous))return false;servers[r.Operation]=terminal;ReleaseEpisode(terminal);
        RebirthOriginalNativeBlockIdentity.Retire(world,r.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,Digest(r),r.TargetBirth);RebirthPlacedOriginalFootprint.Retire(world,terminal);RebirthPlacedOriginalPublication.RetireMarker(world,terminal);RebirthPlacedOriginalFootprintPublication.Retire(world,terminal);if(ArchiveTerminal(terminal,true))servers.Remove(r.Operation);return true;
    }
    internal static void RetireIfTerminal(WorldBase world,Guid operation)
    {if(world is World native&&native.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,native)&&ReadOriginal(PathFor(new Record{Operation=operation},true),out var r)&&r.Phase==5&&r.World==native.worldState.Guid)RebirthPlacedOriginalPublication.RetireMarker(native,r);}
    internal static bool IsPendingTarget(WorldBase world,Vector3i position)
    {
        if(world==null)return false;if(!world.IsRemote()&&IsUnpublished(world,position))return true;
        return owners.Values.Any(a=>ReferenceEquals(a.World,world)&&a.Record.Phase<5&&Current(a)&&MatchesPendingPosition(a.Record,position));
    }
    private static bool MatchesPendingPosition(Record r,Vector3i position)
    {
        if(r.Position==position)return true;
        try{if(RebirthPlacedOriginalFootprint.Decode(r.Footprint).Any(c=>c.Position==position))return true;if(!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var changes)||changes.Count!=1)return false;var parent=changes[0].blockValue;if(!parent.Block.isMultiBlock||parent.Block.multiBlockPos.Length>16)return false;for(int i=0;i<parent.Block.multiBlockPos.Length;i++)if(r.Position+parent.Block.multiBlockPos.Get(i,parent.type,parent.rotation)==position)return true;return false;}catch{return false;}
    }
    internal static bool IsUnpublished(WorldBase world,Vector3i position)
    {
        if(world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))return false;
        var physical=world.GetBlock(position);if(physical.ischild){var parent=physical.Block.multiBlockPos?.GetParentPos(position,physical);if(parent.HasValue&&parent.Value!=position)return IsUnpublished(world,parent.Value);}
        string token=RebirthPlacedWorkmanshipIdentity.Read(world,position);if(!Guid.TryParseExact(token,"N",out var operation))return false;
        var probe=new Record{Operation=operation};
        if(ReadOriginal(PathFor(probe,true),out var r))return r.World==((World)world).worldState.Guid&&r.Position==position&&!r.Declined&&!r.Cancelled&&r.Phase<4;
        // A failed apply/stamp must not expose a known attempted original to a
        // fresh device owner. Ordinary native GUIDs have neither own journal nor
        // own receipt and remain outside this original publication gate.
        return RebirthPlacedOriginalPublication.HasStamp(world,position,operation);
    }
    internal static string HostSourceBirth(WorldBase world,Vector3i position)
    {var r=hostNative;return r!=null&&r.Phase==3&&r.Position==position&&world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,world)&&((World)world).worldState.Guid==r.World&&Read(PathFor(r,true),out var saved)&&Encode(saved)==Encode(r)?r.Operation.ToString("N"):null;}
    internal static void OnHostNativeCommit(WorldBase world,Vector3i position,BlockValue after)
    {
        var r=hostNative;if(r==null||r.Phase!=3||r.Position!=position||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||((World)world).worldState.Guid!=r.World||!Read(PathFor(r,true),out var saved)||Encode(saved)!=Encode(r)||!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var changes)||changes.Count!=1||ExpectedBlock(r).rawData!=after.rawData)return;
        if(RebirthPlacedWorkmanshipService.ApplyRemotePlacement(world,position,after,r.Provenance)&&(r.Electrical.Length==0||RebirthInfrastructureWorkService.ApplyOriginalPlacement(world,position,r.Electrical)))RebirthPlacedOriginalPublication.Stamp((World)world,r);
    }
    private static bool TargetBefore(World world,EntityPlayer owner,Record r,bool retained=false)=>r.TargetBirth.Length!=0&&RebirthOriginalNativeBlockIdentity.TryRead(world,r.Position,out string token)&&token==r.TargetBirth&&TargetRawBefore(world,owner,r,retained);
    private static bool TargetRawBefore(World world,EntityPlayer owner,Record r,bool retained=false)
    {
        var chunk=world.GetChunkSync(World.toChunkXZ(r.X),World.toChunkXZ(r.Z)) as Chunk;if(world.GetTileEntity(r.Position)!=null||chunk==null||chunk.InProgressUnloading||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.ChunkCustomData.dict.Count>ushort.MaxValue-2||world.GetBlock(r.Position).rawData!=r.BeforeBlock||!world.GetBlock(r.Position).isair)return false;
        if(!TryTarget(owner,r,out var intended))return false;if(RebirthInfrastructureWorkService.IsElectricalBlock(intended.Block)&&(!PowerManager.HasInstance||PowerManager.Instance.PowerItemDictionary.ContainsKey(r.Position)))return false;if(r.Changes.Length!=0){if(!RebirthOriginalNativeBlockChange.TryDecode(r.Changes,out var original)||original.Count!=1)return false;intended=original[0].blockValue;}if(!intended.Block.CanPlaceBlockAt(world,r.Position,intended))return false;
        var delta=owner.position-r.Position.ToVector3();return (retained||delta.sqrMagnitude<=64f)&&world.CanPlaceBlockAt(r.Position,GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId));
    }
    internal static void ReceiveClient(World world,int playerId,string image)
    {
        try{var r=Decode(image);if(!owners.TryGetValue(r.Operation,out var a)||!Current(a)&&!CanRelay(a)||a.Player.entityId!=playerId||!ReferenceEquals(a.World,world)||!Same(a.Record,r)||r.Phase<a.Record.Phase)return;bool unusedTerminal=r.Phase==5&&r.Cancelled&&r.Changes.Length==0;if(!unusedTerminal&&(a.Record.Changes.Length!=0&&r.Changes!=a.Record.Changes||a.Record.Attempted&&!r.Attempted))return;string old=Encode(a.Record);if(!Save(r,false,old))return;a.Record=r;if(r.Phase==5){ArchiveTerminal(r,false);owners.Remove(r.Operation);}}catch{}
    }
    private static void Scope(World world){if(ReferenceEquals(scope,world)&&directory==GameIO.GetSaveGameDir())return;RebirthPlacedOriginalPublication.Retire(scope);RebirthPlacedOriginalFootprintPublication.Reset(scope);RebirthPlacedOriginalFootprint.Reset(scope);scope=world;directory=GameIO.GetSaveGameDir();owners.Clear();servers.Clear();projected.Clear();projectionOrder.Clear();episodes.Clear();scanReady=false;foreach(var admission in targets.Values)admission.Dispose();targets.Clear();scan?.Dispose();scan=null;localOwnerScan?.Dispose();localOwnerScan=null;localOwnerReady=false;nextOwnerScan=0;blocked=false;nextScan=0;}
    private static void ReconcileLocalOwner(World world,EntityPlayerLocal player)
    {
        nextOwnerScan=UnityEngine.Time.realtimeSinceStartup+1f;if(world.IsRemote()||!ReferenceEquals(world.GetPrimaryPlayer(),player))return;
        try
        {
            string folder=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","owner");if(!Directory.Exists(folder)){localOwnerReady=true;return;}
            if(localOwnerScan==null)localOwnerScan=Directory.EnumerateFiles(folder,"*").GetEnumerator();
            for(int i=0;i<2;i++)
            {
                if(!localOwnerScan.MoveNext()){localOwnerScan.Dispose();localOwnerScan=null;localOwnerReady=true;nextOwnerScan=UnityEngine.Time.realtimeSinceStartup+10f;break;}
                string file=localOwnerScan.Current;
                if(!file.EndsWith(".xml",StringComparison.OrdinalIgnoreCase))
                {if(file.EndsWith(".bak",StringComparison.OrdinalIgnoreCase)||file.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)){string name=Path.GetFileName(file);int dot=name.IndexOf('.');if(dot!=32||!Guid.TryParseExact(name.Substring(0,dot),"N",out var operation)||!ReadOriginal(Path.Combine(folder,operation.ToString("N")+".xml"),out _)){blocked=true;return;}}continue;}
                if(!Read(file,out var r)){if(Read(TerminalPath(file),out var terminal)&&terminal.Phase==5)continue;blocked=true;return;}
                if(r.Phase==5){ArchiveTerminal(r,false);continue;}if(RebirthPlacedOriginalDeathPartition.HasTerminalNativeRetirement(player,r.Operation,false,AuditHash(Encode(r))))continue;
                var recovered=new Attempt{Record=r,Player=player,World=world,Directory=GameIO.GetSaveGameDir()};if((Current(recovered)||CanRelay(recovered))&&!owners.ContainsKey(r.Operation))owners.Add(r.Operation,recovered);
            }
        }catch{blocked=true;}
    }
    private static void Reconcile(World world)
    {
        nextScan=UnityEngine.Time.realtimeSinceStartup+1f;var p=world.GetPrimaryPlayer() as EntityPlayerLocal;if(world.IsRemote()&&p==null)return;
        string folder=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",world.IsRemote()?"owner":"server");if(!Directory.Exists(folder)){scanReady=true;return;}
        if(scan==null)scan=Directory.EnumerateFiles(folder,"*").GetEnumerator();for(int index=0;index<2;index++){if(!scan.MoveNext()){scan.Dispose();scan=null;scanReady=true;nextScan=UnityEngine.Time.realtimeSinceStartup+10f;break;}string file=scan.Current;if(!file.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)){if(file.EndsWith(".bak",StringComparison.OrdinalIgnoreCase)||file.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)){string name=Path.GetFileName(file);int dot=name.IndexOf('.');if(dot!=32||!Guid.TryParseExact(name.Substring(0,dot),"N",out var orphan)||!ReadOriginal(Path.Combine(folder,orphan.ToString("N")+".xml"),out _)){blocked=true;return;}}continue;}if(!Read(file,out var r)){if(Read(TerminalPath(file),out var archived)&&archived.Phase==5)continue;blocked=true;return;}if(world.IsRemote()&&RebirthPlacedOriginalDeathPartition.HasTerminalNativeRetirement(p,r.Operation,false,AuditHash(Encode(r))))continue;if(r.Phase==5){if(!world.IsRemote()){ReleaseEpisode(r);if(!r.Declined&&!r.Cancelled&&!projected.Contains(r.Operation)&&CorrectPrediction(world,null,r,true))MarkProjected(r.Operation);}if(!world.IsRemote()){RebirthPlacedOriginalPublication.RetireMarker(world,r);RebirthPlacedOriginalFootprintPublication.Retire(world,r);RebirthOriginalNativeBlockIdentity.Retire(world,r.Position,RebirthOriginalNativeBlockIdentity.PlacementPurpose,r.Operation,Digest(r),r.TargetBirth);RebirthPlacedOriginalFootprint.Retire(world,r);}ArchiveTerminal(r,!world.IsRemote());continue;}if(world.IsRemote()){var recovered=new Attempt{Record=r,Player=p,World=world,Directory=GameIO.GetSaveGameDir()};if((Current(recovered)||CanRelay(recovered))&&!owners.ContainsKey(r.Operation))owners.Add(r.Operation,recovered);}else{if(r.Phase<=2){var matching=world.Players.list.Where(actor=>SameNativeOwner(actor,r)&&actor.Died>r.Deaths).Take(2).ToArray();if(matching.Length==1){ObserveNativePaid(matching[0],r);if(TryCloseEndedUnused(world,matching[0],r))continue;}}if(r.Phase==3&&TrySettlePublication(world,r,out var published))r=published;if(r.Phase==4&&!r.Declined&&TryTerminalPublished(world,r,out var terminal))r=terminal;if(r.Phase>=4&&!r.Declined&&!r.Cancelled&&!projected.Contains(r.Operation)){var projectionOwner=world.GetEntity(r.NativeEntity) as EntityPlayer;if(!Current(projectionOwner,r))projectionOwner=null;if(CorrectPrediction(world,projectionOwner,r,true))MarkProjected(r.Operation);}if(r.Phase==5)servers.Remove(r.Operation);else{servers[r.Operation]=r;Index(r);}}}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;if(world==null||!ThreadManager.IsMainThread())return;Scope(world);if(!world.IsRemote()&&world.GetPrimaryPlayer() is EntityPlayerLocal local&&UnityEngine.Time.realtimeSinceStartup>=nextOwnerScan)ReconcileLocalOwner(world,local);RebirthPlacedOriginalFootprintPublication.Update(world);RebirthPlacedOriginalDeathPartition.RetryBodyPublication(world);RebirthPlacedOriginalDeathPartition.UpdateOwed(world);if(UnityEngine.Time.realtimeSinceStartup>=nextScan)Reconcile(world);if(blocked)return;
        foreach(var a in owners.Values.ToArray())
        {
            if((!Current(a)&&!CanRelay(a))||UnityEngine.Time.realtimeSinceStartup<a.Next)continue;a.Next=UnityEngine.Time.realtimeSinceStartup+1f;
            try
            {
                var r=a.Record;
                if(!Current(a)){RebirthPlacedOriginalDeathPartition.Retry(a.Player,r);Send(a);continue;} // Ended-life relay has no native setters, callback, nonce or refund.
                if(r.Phase<=1&&!r.Attempted&&RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(r.Before))&&(a.Data==null||a.Player.inventory.holdingItemIdx!=r.Slot||a.Player.HitInfo.lastBlockPos!=r.Position||a.World.GetBlock(r.Position).rawData!=r.BeforeBlock))
                {SetPhase(a.Player,r,4);Upload(a.Player);var cancelled=Decode(Encode(r));cancelled.Phase=5;cancelled.Cancelled=true;SendRecord(a,cancelled);continue;}
                if(CanInvokeOwner(r)&&a.Action!=null&&a.Data!=null&&a.Player.inventory.holdingItemIdx==r.Slot&&a.Player.HitInfo.lastBlockPos==r.Position&&a.Player.HitInfo.bHitValid&&RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(r.Before)))
                {
                    string old=Encode(r);var attempted=Decode(old);attempted.Attempted=true;if(!Save(attempted,false,old))continue;a.Record=r=attempted;var previous=native;
                    try{native=a;a.Running=true;a.Action.ExecuteAction(a.Data,a.Released);}finally{a.Running=false;a.CallbackFinished=true;native=previous;}
                }
                r=a.Record;
                if(r.Phase<=1){if(r.Phase==1&&(r.Attempted||a.Data==null)&&RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(r.Before))){SetPhase(a.Player,r,4);Upload(a.Player);var cancelled=Decode(Encode(r));cancelled.Phase=5;cancelled.Cancelled=true;SendRecord(a,cancelled);continue;}SetPhase(a.Player,r,1);Upload(a.Player);Send(a);continue;}
                if(r.Phase==2&&RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(r.Before))&&(a.Data==null||a.CallbackFinished&&(!a.CoroutineCreated||a.CoroutineFinished)))
                {SetPhase(a.Player,r,4);Upload(a.Player);var unused=Decode(Encode(r));unused.Phase=5;unused.Cancelled=true;SendRecord(a,unused);continue;}
                if(r.Phase==2||r.Phase==3)
                {if(r.Phase==2){PublishPaid(a);continue;}if(!RebirthCamperEnergyActorImage.Capture(a.Player).Matches(ActorImage(r.After)))continue;SetPhase(a.Player,r,2);Upload(a.Player);Send(a);continue;}
                if(r.Phase==4){if(r.Declined&&!RestoreDeclined(a))continue;SetPhase(a.Player,r,3);Upload(a.Player);var requested=Decode(Encode(r));requested.Phase=5;SendRecord(a,requested);}
            }catch(Exception ex){Log.Warning("[REBIRTH Provenance Original] retained owner original: "+ex.GetType().Name);}
        }
    }
    internal static void Install(Harmony harmony)
    {
        if(installed)return;
        RebirthOriginalNativeBlockIdentity.Install();
        if(!RebirthOriginalNativeBlockIdentity.RegisterPlacementTerminalReader(HasSavedTerminal))throw new InvalidOperationException("Placement target terminal reader conflict");
        RebirthPlacedOriginalPublication.Install(harmony);RebirthPlacedOriginalBodyPublication.Install(harmony);
        foreach(var type in new[]{typeof(RebirthPlacedOriginalActionPatch),typeof(RebirthPlacedOriginalSetBlocksPatch),typeof(RebirthPlacedOriginalDelayedDebitPatch),typeof(RebirthPlacedOriginalPickupRequestPatch),typeof(RebirthPlacedOriginalNativePickupPatch),typeof(RebirthPlacedOriginalDamagePatch),typeof(RebirthPlacedOriginalFootprintChunkRead),typeof(RebirthPlacedOriginalActivationPatch)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        RebirthPlacedOriginalGuards.Install(harmony);ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Update));installed=true;
    }
}
[HarmonyPatch(typeof(ItemActionPlaceAsBlock),nameof(ItemActionPlaceAsBlock.ExecuteAction))]
internal static class RebirthPlacedOriginalActionPatch
{
 private static bool Prefix(ItemActionPlaceAsBlock __instance,ItemActionData __0,bool __1)=>RebirthPlacedOriginal.BeforeAction(__instance,__0,__1);
 private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
 {
    var list=instructions.ToList();var original=AccessTools.Method(typeof(ItemClass),nameof(ItemClass.GetItem),new[]{typeof(string),typeof(bool)});var replacement=AccessTools.Method(typeof(RebirthPlacedOriginal),nameof(RebirthPlacedOriginal.ResolveConversion));int count=0;
    foreach(var instruction in list)if(instruction.Calls(original)){instruction.opcode=OpCodes.Call;instruction.operand=replacement;count++;}
    if(count!=1)throw new InvalidOperationException("Native placement conversion call boundary changed");return list;
 }
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.SetBlocksRPC),new[]{typeof(List<BlockChangeInfo>),typeof(PlatformUserIdentifierAbs)})]
internal static class RebirthPlacedOriginalSetBlocksPatch{private static bool Prefix(GameManager __instance,List<BlockChangeInfo> __0,PlatformUserIdentifierAbs __1)=>RebirthPlacedOriginal.CaptureNative(__instance,__0,__1);}
[HarmonyPatch(typeof(ItemActionPlaceAsBlock),nameof(ItemActionPlaceAsBlock.decInventoryLater))]
internal static class RebirthPlacedOriginalDelayedDebitPatch{private static void Postfix(ItemInventoryData data,int index,ref IEnumerator __result)=>__result=RebirthPlacedOriginal.WrapDebit(data,index,__result);}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedOriginalRequest:NetPackage
{
    private int player;private string image=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthPlacedOriginalRequest Setup(int id,string text){player=id;image=text;return this;}public override void read(PooledBinaryReader reader){player=reader.ReadInt32();image=RebirthPlacedOriginalWire.Read(reader);}public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(player);RebirthPlacedOriginalWire.Write(writer,image);}public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==false&&ValidEntityIdForSender(player))RebirthPlacedOriginal.ReceiveServer(world,player,image);}public int GetLength()=>8+Encoding.UTF8.GetByteCount(image);
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedOriginalReply:NetPackage
{
    private int player;private string image=string.Empty;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthPlacedOriginalReply Setup(int id,string text){player=id;image=text;return this;}public override void read(PooledBinaryReader reader){player=reader.ReadInt32();image=RebirthPlacedOriginalWire.Read(reader);}public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(player);RebirthPlacedOriginalWire.Write(writer,image);}public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)RebirthPlacedOriginal.ReceiveClient(world,player,image);}public int GetLength()=>8+Encoding.UTF8.GetByteCount(image);
}

internal static class RebirthPlacedOriginalWire
{
 internal static string Read(PooledBinaryReader reader){int length=reader.ReadInt32();if(length<0||length>2*1024*1024)throw new InvalidDataException();var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);}
 internal static void Write(PooledBinaryWriter writer,string text){var bytes=new UTF8Encoding(false,true).GetBytes(text);if(bytes.Length>2*1024*1024)throw new InvalidDataException();writer.Write(bytes.Length);writer.Write(bytes);}
}
