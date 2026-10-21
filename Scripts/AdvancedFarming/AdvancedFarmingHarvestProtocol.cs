using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using System.Text;
using System.Xml;

#nullable disable

internal static class AdvancedFarmingHarvestProtocol
{
    private sealed class Token {internal World World;internal uint Raw;internal string Source;internal long Epoch;}
    private sealed class Client {internal World World;internal EntityPlayerLocal Player;internal AdvancedFarmingHarvestReceipt.Record Record;internal AdvancedFarmingHarvestOwnerDelta.Plan Plan;internal int Phase=-1;internal float Next;internal bool OwnerRetired,Prepared;}
    private sealed class Server {internal AdvancedFarmingHarvestReceipt.Record Record;internal EntityPlayer Owner;internal AdvancedFarmingHarvestPublication.Proof Proof;}
    private static readonly Dictionary<Vector3i,Token> Tokens=new Dictionary<Vector3i,Token>();
    private static readonly Dictionary<Vector3i,long> Epochs=new Dictionary<Vector3i,long>();
    private static readonly Dictionary<Guid,Client> Clients=new Dictionary<Guid,Client>();
    private static readonly Dictionary<Guid,Server> Servers=new Dictionary<Guid,Server>();
    private static World currentWorld;private static string currentDirectory;private static Harmony harmony;
    internal static string ReadPayload(PooledBinaryReader input)
    {var reader=(BinaryReader)input;int length=reader.ReadInt32();if(length<0||length>8*1024*1024)throw new InvalidDataException();var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);}
    internal static void WritePayload(PooledBinaryWriter output,string image)
    {var bytes=new UTF8Encoding(false,true).GetBytes(image);if(bytes.Length>8*1024*1024)throw new InvalidDataException();var writer=(BinaryWriter)output;writer.Write(bytes.Length);writer.Write(bytes);}
    internal static XElement ParsePayload(string image)
    {using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024})){var element=XElement.Load(reader);if(element.Name!="harvest")throw new InvalidDataException();return element;}}
    internal static XElement ParseTerminalPayload(string image)
    {if(image==null||image.Length>4*1024*1024)throw new InvalidDataException();using(var text=new StringReader(image))using(var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024})){var root=XElement.Load(reader);if(root.Name!="terminalPlot")throw new InvalidDataException();return root;}}
        internal static bool CompatibleSourceRaw(uint known,uint current)
    {if(known==current)return true;var before=new BlockValue{rawData=known};var after=new BlockValue{rawData=current};return before.Block is BlockFarmPlotRebirth&&after.Block is BlockFarmPlotRebirth&&before.type==after.type&&before.meta==after.meta&&before.meta2==after.meta2&&before.rotation==after.rotation;}
    internal static bool TrySource(Vector3i position,BlockValue expected,out Guid source)
    {source=Guid.Empty;Epochs.TryGetValue(position,out long epoch);return Tokens.TryGetValue(position,out var token)&&ReferenceEquals(token.World,GameManager.Instance?.World)&&token.Epoch==epoch&&CompatibleSourceRaw(token.Raw,expected.rawData)&&Guid.TryParseExact(token.Source,"N",out source);}
    private static readonly Queue<Guid> Recovery=new Queue<Guid>();private static bool recoveryLoaded;private static float nextRecovery;
    internal static void ObservePreparedOriginal(Guid operation)
    {if(operation!=Guid.Empty&&ThreadManager.IsMainThread()&&GameManager.Instance?.World?.IsRemote()==false&&!Servers.ContainsKey(operation)&&!Recovery.Contains(operation))Recovery.Enqueue(operation);}
    private static void SetWorld(World world)
    {string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(currentWorld,world)&&currentDirectory==directory)return;if(currentWorld!=null){AdvancedFarmingHarvestOwnerDelta.ReleaseObservationWorld(currentWorld);AdvancedFarmingHarvestPublication.RetireObservationWorld(currentWorld);AdvancedFarmingHarvestDrops.RetireWorld(currentWorld);}currentWorld=world;currentDirectory=directory;Tokens.Clear();Epochs.Clear();Servers.Clear();Clients.Clear();Recovery.Clear();recoveryLoaded=false;nextRecovery=0f;}
    internal static void Install()
    {
        if(harmony!=null)return;harmony=new Harmony("rebirth.farming.original-harvest");
        AdvancedFarmingHarvestTerminalPlot.Install(harmony);AdvancedFarmingHarvestSettlement.InstallSourceIdentity(harmony);AdvancedFarmingHarvestOwnerDelta.Install(harmony);AdvancedFarmingHarvestPublication.Install(harmony);AdvancedFarmingHarvestDrops.Install(harmony);
    }
    internal static void SourceChanged(WorldBase world,Vector3i position)
    {if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World))return;if(Epochs.Count>=16384){Epochs.Clear();Tokens.Clear();}Epochs.TryGetValue(position,out long epoch);Epochs[position]=epoch+1;Tokens.Remove(position);}
    private static bool Local(Client attempt)=>ReferenceEquals(GameManager.Instance?.World,attempt.World)&&ReferenceEquals(attempt.World.GetPrimaryPlayer(),attempt.Player)&&ReferenceEquals(attempt.World.GetEntity(attempt.Player.entityId),attempt.Player)&&!attempt.Player.IsDead()&&attempt.Player.Died==attempt.Record.Deaths&&attempt.World.worldState?.Guid==attempt.Record.World&&RebirthCamperCharacterAccess.CurrentCreation(attempt.Player)==attempt.Record.Creation;
    private static XElement Envelope(EntityPlayer player,string kind)=>new XElement("harvest",new XAttribute("kind",kind),new XAttribute("world",player.world.worldState.Guid),new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(player)),new XAttribute("deaths",player.Died));
    private static bool Owner(World world,EntityPlayer player,XElement request)=>world!=null&&!world.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,world)&&player!=null&&ReferenceEquals(player.world,world)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&!player.IsDead()&&world.worldState?.Guid==(string)request.Attribute("world")&&RebirthCamperCharacterAccess.CurrentCreation(player)==(string)request.Attribute("creation")&&player.Died==(int)request.Attribute("deaths");
    private static void Send(EntityPlayerLocal player,XElement request)
    {if(player.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageFarmingHarvestRequestRebirth>().Setup(player.entityId,request.ToString(SaveOptions.DisableFormatting)));else ReceiveServer((World)player.world,player.entityId,request);}
    private static void Reply(EntityPlayer player,XElement response)
    {if(player is EntityPlayerLocal&&ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),player))ReceiveClient((World)player.world,response);else SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageFarmingHarvestReplyRebirth>().Setup(response.ToString(SaveOptions.DisableFormatting)),_attachedToEntityId:player.entityId);}
    internal static void ReplyEvents(EntityPlayer player,AdvancedFarmingHarvestReceipt.Record r,int index,bool done)
    {var response=Envelope(player,"events");response.Add(new XAttribute("index",index),new XAttribute("done",done),new XElement("record",AdvancedFarmingHarvestReceipt.Encode(r)));Reply(player,response);}
    internal static void RequestEvents(EntityPlayerLocal player,Guid operation)
    {var request=Envelope(player,"events");request.Add(new XAttribute("operation",operation.ToString("N")));Send(player,request);}
    internal static bool CanCaptureOriginal(WorldBase world,Vector3i position,BlockValue expected,EntityPlayer player)
    {try{return CanCaptureOriginalCore(world,position,expected,player);}catch(Exception error){Log.Warning("[REBIRTH Farming] original source probe refused: "+error.GetType().Name);return false;}}
    private static bool CanCaptureOriginalCore(WorldBase world,Vector3i position,BlockValue expected,EntityPlayer player)
    {
        if(player is not EntityPlayerLocal local||world is not World concrete||!ReferenceEquals(concrete.GetPrimaryPlayer(),local)||local.IsDead()||Clients.Values.Any(c=>Local(c))||AdvancedFarmingHarvestOwnerDelta.IsHeld(local)||RebirthCookingOutputDelivery.IsHeld(local)||RebirthLogisticsOwnerReceipt.IsHeld(local)||
            RebirthDogLifecycleService.HasHireInventoryHold(local)||AdvancedFarmingWaterExchangeOriginal.IsHeld(local)||RebirthCamperEnergyOwnerHold.Blocks(local.bag)||RebirthCamperEnergyOwnerHold.Blocks(local.inventory))return false;
        SetWorld(concrete);if(string.IsNullOrEmpty(RebirthCamperCharacterAccess.CurrentCreation(local))||!Guid.TryParse(concrete.worldState?.Guid,out _))return false;
        Epochs.TryGetValue(position,out long epoch);
        if(Tokens.TryGetValue(position,out var token)&&ReferenceEquals(token.World,concrete)&&token.Epoch==epoch&&CompatibleSourceRaw(token.Raw,expected.rawData))return true;
        var probe=Envelope(local,"probe");probe.Add(new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),new XAttribute("raw",expected.rawData),new XAttribute("epoch",epoch));Send(local,probe);
        // A probe response never replays a stale action against later regrowth.
        return !world.IsRemote()&&Tokens.TryGetValue(position,out token)&&CompatibleSourceRaw(token.Raw,expected.rawData);
    }
    internal static bool Begin(WorldBase world,Vector3i position,BlockValue expected,EntityPlayer owner,IList<ItemStack> outputs,bool replant,string terminal="")
    {try{return BeginCore(world,position,expected,owner,outputs,replant,terminal);}catch(Exception error){Log.Warning("[REBIRTH Farming] original output capsule refused: "+error.GetType().Name);return false;}}
    private static bool BeginCore(WorldBase world,Vector3i position,BlockValue expected,EntityPlayer owner,IList<ItemStack> outputs,bool replant,string terminal)
    {
        if(owner is not EntityPlayerLocal player||world is not World concrete||!CanCaptureOriginal(world,position,expected,owner)||!Tokens.TryGetValue(position,out var token)||!RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity))return false;
        var beforeBag=ItemStack.Clone(player.bag.ItemGrid.items);var beforeBelt=ItemStack.Clone(player.inventory.ItemGrid.items);
        bool fits=RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),RebirthCamperEnergyItems.Locks(player.inventory.ItemGrid.SlotLocks),RebirthToolbeltCapacity.GetOwnedSlotCount(player,beforeBelt.Length),outputs,out var bag,out var belt);
        // A full destination retains the complete original output for the domain
        // native-drop publication path; never call unreceipted ItemDropServer.
        if(!fits){bag=ItemStack.Clone(beforeBag);belt=ItemStack.Clone(beforeBelt);}
        var plan=new AdvancedFarmingHarvestOwnerDelta.Plan{Operation=Guid.NewGuid(),World=concrete.worldState.Guid,Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Owner=identity.CanonicalId,Deaths=player.Died,BeforeBag=beforeBag,BeforeBelt=beforeBelt,AfterBag=bag,AfterBelt=belt,SourceHash=AdvancedFarmingHarvestOwnerDelta.SourceHash(beforeBag,beforeBelt,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),player.inventory.Length)};
        var record=new AdvancedFarmingHarvestReceipt.Record{Operation=plan.Operation,Source=Guid.ParseExact(token.Source,"N"),World=plan.World,Owner=plan.Owner,Creation=plan.Creation,Deaths=plan.Deaths,X=position.x,Y=position.y,Z=position.z,Raw=expected.rawData,Block=checked((ushort)expected.type),Meta=checked((byte)expected.meta),Meta2=checked((byte)expected.meta2),Rotation=checked((byte)expected.rotation),Replant=replant,Terminal=terminal,PlanImage=AdvancedFarmingHarvestOwnerDelta.Encode(plan),Outputs=AdvancedFarmingHarvestReceipt.OutputImage(outputs.ToArray())};
        var dropPosition=player.GetPosition();record.DropX=dropPosition.x;record.DropY=dropPosition.y;record.DropZ=dropPosition.z;
        record.EventsRequired=AdvancedFarmingHarvestEvents.Required(record);AdvancedFarmingHarvestReceipt.Decode(AdvancedFarmingHarvestReceipt.Encode(record));Clients.Add(record.Operation,new Client{World=concrete,Player=player,Record=record,Plan=plan});return true;
    }
    private static bool PrepareAfterOriginalAction(Client attempt)
    {
        var player=attempt.Player;
        if(!Local(attempt)||player.Buffs.GetCustomVar(attempt.Plan.Receipt)!=0f||RebirthCookingOutputDelivery.IsHeld(player)||RebirthLogisticsOwnerReceipt.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||RebirthCamperStationNativeActorPayment.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.inventory))return false;
        var beforeBag=ItemStack.Clone(player.bag.ItemGrid.items);var beforeBelt=ItemStack.Clone(player.inventory.ItemGrid.items);
        if(!RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),RebirthCamperEnergyItems.Locks(player.inventory.ItemGrid.SlotLocks),RebirthToolbeltCapacity.GetOwnedSlotCount(player,beforeBelt.Length),AdvancedFarmingHarvestReceipt.ReadOutputs(attempt.Record.Outputs),out var afterBag,out var afterBelt)){afterBag=ItemStack.Clone(beforeBag);afterBelt=ItemStack.Clone(beforeBelt);}
        attempt.Plan.BeforeBag=beforeBag;attempt.Plan.BeforeBelt=beforeBelt;attempt.Plan.AfterBag=afterBag;attempt.Plan.AfterBelt=afterBelt;
        attempt.Plan.SourceHash=AdvancedFarmingHarvestOwnerDelta.SourceHash(beforeBag,beforeBelt,RebirthCamperStationNativeActorPayment.Locks(player.bag.LockedSlots),player.inventory.Length);
        attempt.Record.PlanImage=AdvancedFarmingHarvestOwnerDelta.Encode(attempt.Plan);attempt.Prepared=true;return true;
    }
    internal static void ReceiveServer(World world,int entityId,XElement request)
    {
        try
        {
            SetWorld(world);var player=world?.GetEntity(entityId) as EntityPlayer;if(!Owner(world,player,request))return;
            string kind=(string)request.Attribute("kind");
            if(kind=="probe")
            {
                var position=new Vector3i((int)request.Attribute("x"),(int)request.Attribute("y"),(int)request.Attribute("z"));uint raw=(uint)request.Attribute("raw");
                if((player.GetPosition()-position.ToVector3()).sqrMagnitude>64f||world.IsWithinTraderArea(position)||!CompatibleSourceRaw(raw,world.GetBlock(position).rawData))return;
                string source=AdvancedFarmingHarvestSettlement.EnsureSource(world,position);if(source==null)return;
                var answer=Envelope(player,"token");answer.Add(new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),new XAttribute("raw",raw),new XAttribute("epoch",(long)request.Attribute("epoch")),new XAttribute("source",source));Reply(player,answer);return;
            }
            if(kind=="events"){if(Guid.TryParseExact((string)request.Attribute("operation"),"N",out var original))AdvancedFarmingHarvestEvents.Request(world,player,original);return;}
            if(kind!="submit")return;
            var record=AdvancedFarmingHarvestReceipt.Decode((string)request.Element("record"));
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||record.Owner!=identity.CanonicalId||record.World!=world.worldState.Guid||record.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||record.Deaths!=player.Died)return;
            if(AdvancedFarmingHarvestReceipt.TryRead(record.World,record.Operation,out var existing))
            {
                if(existing.PlanImage!=record.PlanImage||existing.Outputs!=record.Outputs||existing.Terminal!=record.Terminal||existing.Source!=record.Source)return;record=existing;
            }
            else if(record.EventsRequired!=AdvancedFarmingHarvestEvents.Required(record)||Servers.Count>=4||!AdvancedFarmingHarvestAdmission.TryAccept(player,record,new BlockValue{rawData=record.Raw}))
            {
                if(!AdvancedFarmingHarvestReceipt.ExistsOriginal(record.World,record.Operation)&&(AdvancedFarmingHarvestAdmission.PositivelyClaimedByOther(world,record)||!AdvancedFarmingHarvestSettlement.MatchesSource(world,new Vector3i(record.X,record.Y,record.Z),record.Source.ToString("N"))||!AdvancedFarmingHarvestTerminalPlot.SourceStillMatches(world,record)||!AdvancedFarmingHarvestTerminalPlot.ValidYield(world,player,record,AdvancedFarmingHarvestReceipt.ReadOutputs(record.Outputs))))
                {var refused=Envelope(player,"refused");refused.Add(new XAttribute("operation",record.Operation.ToString("N")),new XAttribute("source",record.Source.ToString("N")));Reply(player,refused);}
                return;
            }
            if(record.State!=AdvancedFarmingHarvestReceipt.Phase.Retired&&Servers.Count<4&&!Servers.ContainsKey(record.Operation))Servers.Add(record.Operation,new Server{Owner=player,Record=record});
            Status(player,record);
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original harvest request retained: "+error.GetType().Name);}
    }
    private static void Status(EntityPlayer player,AdvancedFarmingHarvestReceipt.Record record)
    {var reply=Envelope(player,"status");reply.Add(new XElement("record",AdvancedFarmingHarvestReceipt.Encode(record)));Reply(player,reply);}
    internal static void ReceiveClient(World world,XElement response)
    {
        try
        {
            var player=world?.GetPrimaryPlayer();if(player==null||world.worldState?.Guid!=(string)response.Attribute("world")||RebirthCamperCharacterAccess.CurrentCreation(player)!=(string)response.Attribute("creation")||player.Died!=(int)response.Attribute("deaths"))return;
            if((string)response.Attribute("kind")=="refused")
            {
                if(Guid.TryParseExact((string)response.Attribute("operation"),"N",out var operation)&&Clients.TryGetValue(operation,out var original)&&Local(original)&&original.Phase<0&&original.Record.Source.ToString("N")== (string)response.Attribute("source")&&AdvancedFarmingHarvestOwnerDelta.AbandonRejectedWithoutEffect(player,original.Plan))Clients.Remove(operation);
                return;
            }
            if((string)response.Attribute("kind")=="token")
            {
                var position=new Vector3i((int)response.Attribute("x"),(int)response.Attribute("y"),(int)response.Attribute("z"));Epochs.TryGetValue(position,out long epoch);
                uint raw=(uint)response.Attribute("raw");if(epoch!=(long)response.Attribute("epoch")||!CompatibleSourceRaw(raw,world.GetBlock(position).rawData)||!Guid.TryParseExact((string)response.Attribute("source"),"N",out var source))return;
                if(Tokens.Count>=2048)Tokens.Clear();Tokens[position]=new Token{World=world,Epoch=epoch,Raw=raw,Source=source.ToString("N")};return;
            }
            if((string)response.Attribute("kind")=="events"){var original=AdvancedFarmingHarvestReceipt.Decode((string)response.Element("record"));AdvancedFarmingHarvestEvents.Receive(world,original,(int)response.Attribute("index"),(bool)response.Attribute("done"));return;}
            if((string)response.Attribute("kind")!="status")return;var record=AdvancedFarmingHarvestReceipt.Decode((string)response.Element("record"));
            if(!Clients.TryGetValue(record.Operation,out var attempt))
            {
                if(record.State==AdvancedFarmingHarvestReceipt.Phase.Retired||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||owner.CanonicalId!=record.Owner)return;
                var plan=AdvancedFarmingHarvestOwnerDelta.Decode(record.PlanImage);float phase=player.Buffs.GetCustomVar(plan.Receipt);if(phase<1f||phase>3f)return;
                attempt=new Client{World=world,Player=player,Record=record,Plan=plan,Prepared=true};Clients.Add(record.Operation,attempt);
            }
            if(!Local(attempt)||attempt.Record.PlanImage!=record.PlanImage||attempt.Record.Outputs!=record.Outputs||attempt.Record.Terminal!=record.Terminal||attempt.Record.Source!=record.Source)return;
            if((int)record.State<attempt.Phase)return;attempt.Phase=(int)record.State;
        }
        catch(Exception error){Log.Warning("[REBIRTH Farming] original harvest response retained: "+error.GetType().Name);}
    }
    internal static void Pump(World world)
    {
        if(world==null)return;
        SetWorld(world);AdvancedFarmingHarvestEvents.Pump(world);
        foreach(var attempt in Clients.Values.ToArray())
        {
            if(!Local(attempt))continue;
            try
            {
                // Native attack durability/stamina effects finish after the block
                // callback. Capture the inventory once on the subsequent update;
                // the original yield/source capsule is never recalculated.
                if(!attempt.Prepared&&!PrepareAfterOriginalAction(attempt))continue;
                if(!attempt.OwnerRetired)
                {
                    AdvancedFarmingHarvestOwnerDelta.Advance(attempt.Player,attempt.Plan,attempt.Phase,()=>Local(attempt),()=>attempt.Phase>=1);
                    if(attempt.Phase>=2)attempt.OwnerRetired=AdvancedFarmingHarvestOwnerDelta.Retire(attempt.Player,attempt.Plan,true);
                }
                if(attempt.Phase>=3&&attempt.OwnerRetired)
                {
                    Clients.Remove(attempt.Record.Operation);
                    // Retire item delivery before cosmetic feedback. Durable native
                    // event obligations run separately and never regrant items.
                    var source=new BlockValue{rawData=attempt.Record.Raw};var outputs=AdvancedFarmingHarvestReceipt.ReadOutputs(attempt.Record.Outputs);
                    if(RebirthUtilities.IsPlayerGrownHarvestCrop(source)){var terminal=AdvancedFarmingHarvestTerminalPlot.Decode(attempt.Record.Terminal);if(terminal!=null)foreach(var item in AdvancedFarmingHarvestReceipt.ReadOutputs(terminal.PlotOutputs))attempt.Player.AddUIHarvestingItem(item.Clone(),false);}
                    else {attempt.Player.PlayOneShot("item_plant_pickup",false);foreach(var item in outputs)attempt.Player.AddUIHarvestingItem(item.Clone(),false);}
                    continue;
                }
                if(UnityEngine.Time.realtimeSinceStartup<attempt.Next)continue;attempt.Next=UnityEngine.Time.realtimeSinceStartup+1f;
                var request=Envelope(attempt.Player,"submit");request.Add(new XElement("record",AdvancedFarmingHarvestReceipt.Encode(attempt.Record)));Send(attempt.Player,request);
            }
            catch(Exception error){Log.Warning("[REBIRTH Farming] original owner harvest retained: "+error.GetType().Name);}
        }
        if(world.IsRemote())return;
        if(!recoveryLoaded){foreach(var operation in AdvancedFarmingHarvestAdmission.Pending(world))Recovery.Enqueue(operation);recoveryLoaded=true;}
        if(UnityEngine.Time.realtimeSinceStartup>=nextRecovery)
        {
            nextRecovery=UnityEngine.Time.realtimeSinceStartup+1f;int count=Math.Min(4,Recovery.Count);
            for(int i=0;i<count;i++)
            {
                Guid operation=Recovery.Dequeue();if(Servers.ContainsKey(operation))continue;
                if(!AdvancedFarmingHarvestReceipt.TryRead(world.worldState.Guid,operation,out var record)||record.State==AdvancedFarmingHarvestReceipt.Phase.Retired)continue;
                EntityPlayer owner=null;foreach(var candidate in world.Players.list)if(candidate.Died==record.Deaths&&!candidate.IsDead()&&RebirthStablePlayerIdentity.TryResolveServerEntity(candidate,out var identity)&&identity.CanonicalId==record.Owner&&RebirthCamperCharacterAccess.CurrentCreation(candidate)==record.Creation){owner=candidate;break;}
                var chunk=world.GetChunkSync(World.toChunkXZ(record.X),World.toChunkXZ(record.Z)) as Chunk;
                if(owner!=null&&chunk!=null&&!chunk.InProgressUnloading&&Servers.Count<4){Servers.Add(operation,new Server{Owner=owner,Record=record});Status(owner,record);}else Recovery.Enqueue(operation);
            }
        }
        foreach(var attempt in Servers.Values.ToArray())
        {
            try
            {
                var r=attempt.Record;var owner=attempt.Owner;
                if(world.worldState?.Guid!=r.World||!Owner(world,owner,Envelope(owner,"scope"))||owner.Died!=r.Deaths||RebirthCamperCharacterAccess.CurrentCreation(owner)!=r.Creation||!RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)||identity.CanonicalId!=r.Owner){Servers.Remove(r.Operation);Recovery.Enqueue(r.Operation);continue;}
                var position=new Vector3i(r.X,r.Y,r.Z);var original=new BlockValue{rawData=r.Raw};var destination=RebirthUtilities.HarvestDestination(original,r.Replant);
                var loaded=world.GetChunkSync(World.toChunkXZ(r.X),World.toChunkXZ(r.Z)) as Chunk;if(loaded==null||loaded.InProgressUnloading){Servers.Remove(r.Operation);Recovery.Enqueue(r.Operation);continue;}
                if(r.State==AdvancedFarmingHarvestReceipt.Phase.Reserved)
                {
                    if(!AdvancedFarmingHarvestEvents.PrepareStatistics(owner,r))continue;
                    if(AdvancedFarmingHarvestSettlement.MatchesSource(world,position,r.Source.ToString("N")))
                    {if(!AdvancedFarmingHarvestOwnerDelta.SavedUnspent(identity,AdvancedFarmingHarvestOwnerDelta.Decode(r.PlanImage))||!RebirthUtilities.CommitHarvestSource(world,position,original,r.Replant,r.Operation,r.Source))continue;}
                    if(!AdvancedFarmingHarvestTerminalPlot.Commit(world,owner,r))continue;
                    Func<bool> postimage=()=>AdvancedFarmingHarvestSettlement.HasCommit(world,position,r.Operation,r.Source,r.Raw,destination.rawData)&&AdvancedFarmingHarvestTerminalPlot.Committed(world,r);
                    if(attempt.Proof==null&&!AdvancedFarmingHarvestPublication.Observe(r.Operation,r.Source,world,position,postimage,out attempt.Proof))continue;
                    if(!AdvancedFarmingHarvestReceipt.TryPublishSource(r,attempt.Proof,out var next))continue;attempt.Record=r=next;AdvancedFarmingHarvestPublication.Complete(r.Operation);AdvancedFarmingHarvestSettlement.SourceRecorded(world,position,r.Operation);AdvancedFarmingHarvestTerminalPlot.SourceRecorded(world,r);
                }
                if(r.State==AdvancedFarmingHarvestReceipt.Phase.SourcePublished&&AdvancedFarmingHarvestOwnerDelta.Saved(identity,AdvancedFarmingHarvestOwnerDelta.Decode(r.PlanImage),2)&&AdvancedFarmingHarvestReceipt.TryAdvance(r,AdvancedFarmingHarvestReceipt.Phase.OwnerPublished,out var after))attempt.Record=r=after;
                if(r.State==AdvancedFarmingHarvestReceipt.Phase.OwnerPublished)
                {
                    var remaining=AdvancedFarmingHarvestReceipt.Remaining(r);AdvancedFarmingHarvestDrops.Proof drops=null;
                    if(remaining.Length!=0&&!AdvancedFarmingHarvestDrops.Observe(world,owner,r,out drops))continue;
                    if(RebirthUtilities.IsPlayerGrownHarvestCrop(original))
                    {
                        var terminal=AdvancedFarmingHarvestTerminalPlot.Decode(r.Terminal);int units=AdvancedFarmingHarvestReceipt.ReadOutputs(terminal==null?r.Outputs:terminal.CropOutputs).Sum(item=>item.count);
                        if(!RebirthPhase8WorldOutputTrainingService.TryAwardAdvancedFarmingOriginal(owner,original.Block.GetBlockName(),units,r.Operation.ToString("N")))continue;
                    }
                    if(!AdvancedFarmingHarvestEvents.Ensure(r))continue;
                    if(remaining.Length==0&&AdvancedFarmingHarvestReceipt.TryAdvance(r,AdvancedFarmingHarvestReceipt.Phase.Retired,out var retired)){attempt.Record=r=retired;AdvancedFarmingHarvestAdmission.Retired(r);}
                    else if(drops!=null&&AdvancedFarmingHarvestReceipt.TryPublishDrops(r,drops,out var dropped)){attempt.Record=r=dropped;AdvancedFarmingHarvestDrops.Complete(r);AdvancedFarmingHarvestAdmission.Retired(r);}
                }
                Status(owner,r);if(r.State==AdvancedFarmingHarvestReceipt.Phase.Retired)Servers.Remove(r.Operation);
            }
            catch(Exception error){Log.Warning("[REBIRTH Farming] original server harvest retained: "+error.GetType().Name);}
        }
    }
}

public class NetPackageFarmingHarvestRequestRebirth:NetPackage
{
    private int entity;private string image;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageFarmingHarvestRequestRebirth Setup(int owner,string payload){entity=owner;image=payload;return this;}
    public override void read(PooledBinaryReader reader){entity=((BinaryReader)reader).ReadInt32();image=AdvancedFarmingHarvestProtocol.ReadPayload(reader);}
    public override void write(PooledBinaryWriter writer){base.write(writer);((BinaryWriter)writer).Write(entity);AdvancedFarmingHarvestProtocol.WritePayload(writer,image);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world!=null&&!world.IsRemote()&&ValidEntityIdForSender(entity))try{AdvancedFarmingHarvestProtocol.ReceiveServer(world,entity,AdvancedFarmingHarvestProtocol.ParsePayload(image));}catch(InvalidDataException){}catch(XmlException){}}
}
public class NetPackageFarmingHarvestReplyRebirth:NetPackage
{
    private string image;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageFarmingHarvestReplyRebirth Setup(string payload){image=payload;return this;}
    public override void read(PooledBinaryReader reader){image=AdvancedFarmingHarvestProtocol.ReadPayload(reader);}
    public override void write(PooledBinaryWriter writer){base.write(writer);AdvancedFarmingHarvestProtocol.WritePayload(writer,image);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)try{AdvancedFarmingHarvestProtocol.ReceiveClient(world,AdvancedFarmingHarvestProtocol.ParsePayload(image));}catch(InvalidDataException){}catch(XmlException){}}
}
