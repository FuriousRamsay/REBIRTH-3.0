using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using Platform;

// Original custom-grid gestures are coupled to exact native owner images.
// The local journal owns grid custody; it never invents an inventory refund.
internal static partial class RebirthCookingGridCustody
{
    private sealed class Intent
    {
        internal Guid Operation,Key,ParentAdmission;internal string World,Creation,Owner,Location,Directory,NativeReceipt,Action;internal long Revision;
        internal EntityPlayerLocal Player;internal World WorldObject;internal XUiC_RebirthCookingWorkspace Workspace;
        internal RebirthCamperEnergyActorImage Before,After;internal ItemStack[] GridBefore,GridAfter;
        internal bool EvidenceOnly;internal int Deaths,ServerPhase,WitnessPhase,PickupType=-1;internal long FetchId;internal RebirthCamperTransferJournal.Record Record;
        internal Guid WitnessNonce;internal string WitnessHash;internal object Peer,Connection;internal float NextWitness;
    }
    private sealed class Permit{internal object Target;internal int Slot;internal ItemStack Value;internal bool Used;internal Intent Intent;}
    private static readonly List<Intent> Active=new List<Intent>();
    private static Permit permit;
    private static bool installed;
    private static World bootstrapWorld;private static EntityPlayerLocal bootstrapPlayer;private static string bootstrapCreation;private static int bootstrapDeaths=-1;
    [ThreadStatic]private static bool bootstrapping;private static bool bootstrapUnknown,bootstrapRetry;
    internal static bool Adopting(Guid parent,EntityPlayerLocal owner)=>Active.Any(intent=>intent.ParentAdmission==parent&&ReferenceEquals(intent.Player,owner)&&ReferenceEquals(intent.WorldObject,owner.world));
    private static readonly Dictionary<int,float> WitnessRate=new Dictionary<int,float>();private static World witnessWorld;
    private static string indexDirectory;
    private static readonly Dictionary<Guid,Guid> LatestOperations=new Dictionary<Guid,Guid>();
    private sealed class AuditCounts{internal long Prepared,Applied;internal string Action;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,AuditCounts> Audit=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,AuditCounts>();
    internal static Newtonsoft.Json.Linq.JObject CaptureAuditState(EntityPlayer actor)
    {
        AuditCounts counts=null;if(actor!=null)Audit.TryGetValue(actor,out counts);
        return new Newtonsoft.Json.Linq.JObject{["prepared"]=counts?.Prepared??0,["applied"]=counts?.Applied??0,["action"]=counts?.Action,
            ["pending"]=Active.Count(intent=>ReferenceEquals(intent.Player,actor)&&ReferenceEquals(intent.WorldObject,GameManager.Instance?.World)),
            ["evidenceOnly"]=Active.Count(intent=>ReferenceEquals(intent.Player,actor)&&intent.EvidenceOnly),
            ["unknown"]=bootstrapUnknown&&ReferenceEquals(bootstrapPlayer,actor)};
    }
    private static RebirthCamperTransferJournal Journal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridCustody"));
    private static string Receipt(Guid operation)=>"rbCookingGrid_"+operation.ToString("N");
    internal static string ReceiptForAudit(Guid operation)=>Receipt(operation);
    // Both host-local and remote UI owners use the same native saved-original
    // protocol. A server replica of a remote player is never an owning UI.
    private static bool CurrentOwner(EntityPlayerLocal player)=>ThreadManager.IsMainThread()&&player?.world!=null&&
        ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&
        ReferenceEquals(player.world.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead();
    private static bool Same(ItemStack a,ItemStack b)=>RebirthStationGridIngredients.IsSameStackSnapshot(a,b);
    private static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Select((item,index)=>Same(item,b[index])).All(value=>value);
    internal static bool IsHeld(EntityPlayerLocal player)
    {
        if(player==null||!player.IsSpawned()||player.IsDead())return false;
        Bootstrap(player);return bootstrapUnknown&&ReferenceEquals(bootstrapPlayer,player)&&ReferenceEquals(bootstrapWorld,player.world)&&bootstrapDeaths==player.Died||
            Active.Any(intent=>ReferenceEquals(intent.Player,player)&&ReferenceEquals(intent.WorldObject,player.world)&&intent.Deaths==player.Died);
    }
    internal static bool IsHeld(XUiC_RebirthCookingWorkspace workspace)=>workspace!=null&&Active.Any(intent=>ReferenceEquals(intent.Workspace,workspace)&&ReferenceEquals(intent.WorldObject,GameManager.Instance?.World));
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {
        var p=permit;if(p==null)return RebirthRemoteResourceOwnerAction.Consume(grid,slot,value);
        if(p.Used||!ReferenceEquals(p.Target,grid)||p.Slot!=slot||!ReferenceEquals(value,p.Value)||!Current(p.Intent))return false;
        p.Used=true;return true;
    }
    internal static bool AllowCursor(XUiC_DragAndDropWindow window,ItemStack value)
    {
        var p=permit;if(p==null||p.Used||!ReferenceEquals(p.Target,window)||!ReferenceEquals(value,p.Value)||!Current(p.Intent))return false;
        p.Used=true;return true;
    }
    internal static bool WithExternalDelta(Guid operation,Func<bool> advance)
    {
        var held=Active.FirstOrDefault(intent=>intent.Operation==operation&&intent.NativeReceipt!=null);
        if(held!=null)Active.Remove(held);
        try{return advance();}
        finally{if(held!=null&&!RebirthCookingOwnerDelta.HasOwnHold(held.Player)&&!Active.Contains(held))Active.Add(held);}
    }
    private static void Bootstrap(EntityPlayerLocal player)
    {
        if(bootstrapping||!CurrentOwner(player)||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player))return;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        if(!bootstrapRetry&&ReferenceEquals(bootstrapWorld,player.world)&&ReferenceEquals(bootstrapPlayer,player)&&bootstrapCreation==creation&&bootstrapDeaths==player.Died)return;
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||player.PlayerUI?.xui?.DragAndDropWindow==null)return;
        bootstrapping=true;
        try
        {
            bootstrapWorld=player.world;bootstrapPlayer=player;bootstrapCreation=creation;bootstrapDeaths=player.Died;bootstrapUnknown=false;bootstrapRetry=false;
            RebirthCookingNativeReturn.Refresh();
            Active.RemoveAll(intent=>!ReferenceEquals(intent.WorldObject,player.world));
            var originals=Records().Where(record=>record.OwnerCreation==creation&&(record.State==RebirthCamperTransferJournal.Phase.Prepared||record.State==RebirthCamperTransferJournal.Phase.Uncertain)).ToArray();
            foreach(var record in originals)
            {
                var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));
                if((string)root.Attribute("world")!=player.world.worldState.Guid||(string)root.Attribute("owner")!=identity.CanonicalId||(int?)root.Attribute("deaths")!=player.Died)continue;
                if(Active.Any(value=>value.Operation==record.Operation&&ReferenceEquals(value.Player,player)&&ReferenceEquals(value.WorldObject,player.world)))continue;
                var intent=Parse(record,null,player);
                if(!RebirthCookingRemoteAdmissionBackend.NativePhase(identity,intent.NativeReceipt??Receipt(intent.Operation),out float phase,out bool found))
                {intent.EvidenceOnly=true;Active.Add(intent);continue;}
                float live=player.Buffs.GetCustomVar(intent.NativeReceipt??Receipt(intent.Operation));
                if(live!=(found?phase:0f))
                {
                    // A spawned owner's native server load can precede its local
                    // cache save. Never infer phase zero from that older cache.
                    bootstrapUnknown=true;
                    if(live>=(found?phase:0f)&&live>=1f&&live<=4f){GameManager.Instance.SaveLocalPlayerData();bootstrapRetry=true;}
                    return;
                }
                if(intent.NativeReceipt!=null)
                {
                    if(!intent.NativeReceipt.StartsWith("rbCookingGridReturn_",StringComparison.Ordinal))continue;
                    if(found&&phase==3f){if(!CompleteExternal(intent.Operation)){bootstrapUnknown=true;bootstrapRetry=true;}continue;}
                    if(!Guid.TryParseExact((string)root.Attribute("sourceOriginal"),"N",out var source))throw new InvalidDataException();
                    if(found&&phase==4f&&!RejectUnappliedExternal(intent.Operation,player)){bootstrapUnknown=true;bootstrapRetry=true;continue;}
                    if(found&&(phase==1f||phase==2f)) {intent.EvidenceOnly=true;Active.Add(intent);}
                    RebirthCookingRemoteAdmission.RecoverDetachedReturn(player,intent.Location,source,intent.Operation,intent.GridBefore,intent.GridAfter,found&&(phase==2f||phase==3f));
                    continue;
                }
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared&&!found)
                {Journal().Advance(record.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);continue;}
                if(!found||(phase!=1f&&phase!=2f))intent.EvidenceOnly=true;
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain&&intent.FetchId==0)intent.ServerPhase=1;
                Active.Add(intent);
            }
        }
        catch(Exception error){bootstrapUnknown=true;Log.Warning("[Cooking] Original owner bootstrap retained: "+error.GetType().Name);}
        finally{bootstrapping=false;}
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.cooking.original-grid.v1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCookingGridGesture));
        ModEvents.GameUpdate.RegisterHandler(Tick);installed=true;
    }
    private static bool Current(Intent intent)=>ThreadManager.IsMainThread()&&Active.Contains(intent)&&ReferenceEquals(GameManager.Instance?.World,intent.WorldObject)&&
        ReferenceEquals(intent.WorldObject.GetPrimaryPlayer(),intent.Player)&&ReferenceEquals(intent.WorldObject.GetEntity(intent.Player.entityId),intent.Player)&&
        intent.Player.IsSpawned()&&!intent.Player.IsDead()&&intent.Deaths==intent.Player.Died&&!(bootstrapUnknown&&ReferenceEquals(bootstrapPlayer,intent.Player)&&ReferenceEquals(bootstrapWorld,intent.WorldObject))&&GameIO.GetSaveGameDir()==intent.Directory&&intent.Player.world.worldState.Guid==intent.World&&
        RebirthCamperCharacterAccess.CurrentCreation(intent.Player)==intent.Creation&&!RebirthCookingOutputDelivery.HasOwnHold(intent.Player)&&
        !RebirthCookingOwnerDelta.HasOwnHold(intent.Player)&&!RebirthRemoteResourceOwnerAction.IsHeld(intent.Player)&&!AdvancedFarmingHarvestOwnerDelta.IsHeld(intent.Player)&&!AdvancedFarmingWaterExchangeOriginal.IsHeld(intent.Player)&&!RebirthGearOwnerReservation.IsHeld(intent.Player)&&!RebirthBackpackLibraryReservation.IsHeld(intent.Player)&&
        !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(intent.Player)&&!RebirthCamperStationNativeActorPayment.Blocks(intent.Player.bag)&&
        !RebirthCamperStationNativeActorPayment.Blocks(intent.Player.inventory)&&!RebirthCamperEnergyOwnerHold.Blocks(intent.Player.bag);
    private static bool CanAdmit(EntityPlayerLocal player)=>player!=null&&!player.IsDead()&&!RebirthLogisticsOwnerReceipt.IsHeld(player)&&!RebirthPlacedOriginal.IsHeld(player)&&!RebirthCookingOutputDelivery.HasOwnHold(player)&&!RebirthCookingOwnerDelta.HasOwnHold(player)&&!RebirthRemoteResourceOwnerAction.IsHeld(player)&&
        !AdvancedFarmingHarvestOwnerDelta.IsHeld(player)&&!AdvancedFarmingWaterExchangeOriginal.IsHeld(player)&&!RebirthGearOwnerReservation.IsHeld(player)&&!RebirthBackpackLibraryReservation.IsHeld(player)&&
        !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)&&!RebirthCamperStationNativeActorPayment.Blocks(player.bag)&&
        !RebirthCamperStationNativeActorPayment.Blocks(player.inventory)&&!RebirthCamperEnergyOwnerHold.Blocks(player.bag);
    private static string Actor(RebirthCamperEnergyActorImage image)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){image.Write(writer);writer.Flush();if(stream.Length>4*1024*1024)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());}}
    private static RebirthCamperEnergyActorImage Actor(string image)
    {if(image==null||image.Length>6*1024*1024)throw new InvalidDataException();using(var stream=new MemoryStream(Convert.FromBase64String(image),false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){var actor=RebirthCamperEnergyActorImage.Read(reader);if(stream.Position!=stream.Length||Actor(actor)!=image)throw new InvalidDataException();return actor;}}
    private static string Grid(ItemStack[] image)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(image));
    private static IEnumerable<RebirthCamperTransferJournal.Record> Records()
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridCustody");if(!Directory.Exists(directory))yield break;
        foreach(string file in Directory.EnumerateFiles(directory,"*.0.rcj"))
        {string name=Path.GetFileName(file);if(name.Length!=38||!Guid.TryParseExact(name.Substring(0,32),"N",out var operation))throw new InvalidDataException();yield return RebirthCamperTransferJournal.ReadExisting(directory,operation);}
    }
    private static RebirthCamperTransferJournal.Record SourceHistoryRecord(Guid operation)
        =>RebirthCamperTransferJournal.ReadExisting(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridCustody"),operation);
    private static Guid Key(EntityPlayerLocal player,string owner,string location)=>RebirthCookingRemoteAdmissionBackend.StationKey(player.world.worldState.Guid,owner+"|"+location);
    private static RebirthCamperTransferJournal.Record Latest(Guid key)
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridCustody");
        if(indexDirectory!=directory)
        {
            var loaded=new Dictionary<Guid,RebirthCamperTransferJournal.Record>();var pending=new Dictionary<Guid,Guid>();
            foreach(var record in Records())
            {
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared||record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                {if(pending.ContainsKey(record.Vehicle))throw new InvalidDataException("Conflicting original grid intents.");pending[record.Vehicle]=record.Operation;}
                if(loaded.TryGetValue(record.Vehicle,out var prior))
                {if(prior.Revision==record.Revision)throw new InvalidDataException();if(prior.Revision>record.Revision)continue;}
                loaded[record.Vehicle]=record;
            }
            foreach(var pair in pending)if(loaded[pair.Key].Operation!=pair.Value)throw new InvalidDataException("Pending grid intent was superseded without settlement.");
            LatestOperations.Clear();foreach(var pair in loaded)LatestOperations.Add(pair.Key,pair.Value.Operation);indexDirectory=directory;
        }
        return LatestOperations.TryGetValue(key,out var operation)?Journal().Read(operation):null;
    }
    private static void AssertAvailable(Guid key,Guid operation,RebirthCamperTransferJournal.Record latest)
    {
        if(latest!=null&&(latest.State==RebirthCamperTransferJournal.Phase.Prepared||latest.State==RebirthCamperTransferJournal.Phase.Uncertain)&&latest.Operation!=operation)
            throw new InvalidOperationException("Settle the retained original grid intent first.");
        var same=Journal().Read(operation);if(same!=null&&same.Vehicle!=key)throw new InvalidDataException("Original grid identity reused.");
    }
    internal static bool HasFetchClaim(EntityPlayerLocal player,long id,out bool completed)
    {
        completed=false;if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return true;
        RebirthCamperTransferJournal.Record match=null;
        foreach(var record in Records())
        {
            if(record.State==RebirthCamperTransferJournal.Phase.Rejected)continue;
            var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));
            if((long?)root.Attribute("fetchId")!=id||(string)root.Attribute("owner")!=identity.CanonicalId||
                (string)root.Attribute("world")!=player.world.worldState.Guid)continue;
            if(match!=null)throw new InvalidDataException();
            match=record;
        }
        if(match==null)return false;
        bool terminal=HasFetchedTerminal(match,out bool terminalObserved);
        completed=match.State==RebirthCamperTransferJournal.Phase.Applied&&(terminalObserved?terminal&&FreshFetchedTerminal(match):
            RebirthCookingRemoteAdmissionBackend.Phase(identity,Receipt(match.Operation),out float phase)&&(phase==2f||phase==3f));
        return true;
    }
    private static Intent Parse(RebirthCamperTransferJournal.Record record,XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player)
    {
        var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));if(root.Name!="cookingGridOriginal"||(int?)root.Attribute("version")!=1)throw new InvalidDataException();
        var intent=new Intent{Operation=record.Operation,Key=record.Vehicle,Revision=record.Revision,Record=record,Player=player,WorldObject=player.world,Workspace=workspace,
            World=(string)root.Attribute("world"),Owner=(string)root.Attribute("owner"),Creation=(string)root.Attribute("creation"),Location=(string)root.Attribute("location"),NativeReceipt=(string)root.Attribute("nativeReceipt"),Action=(string)root.Attribute("action"),Directory=GameIO.GetSaveGameDir(),Deaths=(int?)root.Attribute("deaths")??-1,FetchId=(long?)root.Attribute("fetchId")??0,
            Before=Actor(root.Element("beforeActor").Value),After=Actor(root.Element("afterActor").Value),GridBefore=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("beforeGrid").Value)),
            GridAfter=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("afterGrid").Value)),PickupType=(int?)root.Attribute("pickupType")??-1};
        if(root.Attribute("parentAdmission")!=null&&!Guid.TryParseExact((string)root.Attribute("parentAdmission"),"N",out intent.ParentAdmission))throw new InvalidDataException();
        if(intent.PickupType!=-1&&!Enum.IsDefined(typeof(XUiC_ItemStack.StackLocationTypes),intent.PickupType))throw new InvalidDataException();
        bool terminal=record.State==RebirthCamperTransferJournal.Phase.Applied||record.State==RebirthCamperTransferJournal.Phase.Rejected;
        bool inputHandoff=intent.NativeReceipt?.StartsWith("rbCookingAdmission_",StringComparison.Ordinal)==true;
        bool detachedGrid=ValidDetachedLocation(intent.Location)&&intent.GridBefore.Length>=12&&intent.GridBefore.Length<=4096&&intent.GridAfter.Length==intent.GridBefore.Length;
        if(!detachedGrid&&(intent.GridBefore.Length!=12||intent.GridAfter.Length!=12)||!terminal&&!inputHandoff&&intent.Deaths!=player.Died||intent.World!=player.world.worldState.Guid||intent.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.CanonicalId!=intent.Owner||intent.Key!=Key(player,intent.Owner,intent.Location))throw new InvalidDataException();return intent;
    }
    private static bool Begin(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,string action,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,ItemStack[] gridBefore,ItemStack[] gridAfter,long fetchId=0,IList<ItemStack> fetched=null,int pickupType=-1,int sourceDeaths=-1,XElement sourceReturn=null,string detachedLocation=null,IList<ItemStack> overflow=null)
    {
        Install();Active.RemoveAll(intent=>!ReferenceEquals(intent.WorldObject,GameManager.Instance?.World));
        bool historical=workspace==null&&action=="fetched-return"&&fetchId>0&&sourceDeaths>=0&&player!=null&&sourceDeaths<player.Died&&
            gridBefore.Length==12&&gridAfter.Length==12&&gridBefore.All(item=>item.IsEmpty())&&gridAfter.All(item=>item.IsEmpty());
        bool detached=workspace==null&&player?.world?.IsRemote()==false&&ValidDetachedLocation(detachedLocation)&&
            (action=="fetched"&&fetchId>0&&detachedLocation=="fetch-return:"+fetchId||action=="source-return"&&sourceReturn!=null||action=="return");
        if(!CurrentOwner(player)||!CanAdmit(player)||IsHeld(player)||!historical&&!detached&&RebirthCookingRemoteAdmission.IsHeld(workspace)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||!historical&&!detached&&workspace?.CustodyStation==null)return false;
        if(!before.Matches(RebirthCamperEnergyActorImage.Capture(player))||!historical&&!detached&&!Same(gridBefore,workspace.ReadCustodyGrid()))return false;
        string location=historical?"fetch-refund:"+fetchId:detached?detachedLocation:RebirthCookingStationLocator.Encode(workspace.CustodyStation);Guid key=Key(player,identity.CanonicalId,location);var journal=Journal();
        if(fetchId>0)
        {
            if(!(historical?RebirthCookingLegacyPull.ValidateHistoricalReturnOriginal(player,fetchId,fetched,sourceDeaths):RebirthCookingLegacyPull.ValidateOriginal(player,fetchId,fetched)))throw new InvalidDataException("Original fetched receipt unavailable.");
            foreach(var candidate in Records())
            {
                if(candidate.State==RebirthCamperTransferJournal.Phase.Rejected)continue;
                var prior=XElement.Parse(Encoding.UTF8.GetString(candidate.Before));
                if((long?)prior.Attribute("fetchId")==fetchId&&(string)prior.Attribute("owner")==identity.CanonicalId&&
                    (string)prior.Attribute("world")==player.world.worldState.Guid)return true;
            }
        }
        if(!(overflow!=null?player.world.IsRemote()==false&&action=="return"&&sourceReturn==null&&overflow.Count>0&&overflow.Count<=255&&Conserved(after,gridAfter,before,gridBefore,overflow):sourceReturn==null?Conserved(before,gridBefore,after,gridAfter,fetched):
            action=="source-return"&&player.world.IsRemote()==false&&fetchId==0&&before.Matches(after)&&Conserved(after,gridAfter,before,gridBefore,SourceReturnItems(sourceReturn))))throw new InvalidDataException("Cooking gesture would change original item metadata or count.");
        var latest=Latest(key);Guid operation=Guid.NewGuid();AssertAvailable(key,operation,latest);
        if(latest!=null)
        {
            var prior=Parse(latest,workspace,player);if(!Same(latest.State==RebirthCamperTransferJournal.Phase.Rejected?prior.GridBefore:prior.GridAfter,gridBefore))throw new InvalidOperationException("Original cooking grid ownership changed.");
        }
        else if(gridBefore.Any(item=>!item.IsEmpty()))throw new InvalidOperationException("Existing cooking grid requires its original custody witness.");
        var root=new XElement("cookingGridOriginal",new XAttribute("version",1),new XAttribute("world",player.world.worldState.Guid),new XAttribute("owner",identity.CanonicalId),
            new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(player)),new XAttribute("deaths",player.Died),new XAttribute("location",location),new XAttribute("action",action),
            new XElement("beforeActor",Actor(before)),new XElement("afterActor",Actor(after)),new XElement("beforeGrid",Grid(gridBefore)),new XElement("afterGrid",Grid(gridAfter)));
        if(latest!=null&&(latest.State==RebirthCamperTransferJournal.Phase.Applied||latest.State==RebirthCamperTransferJournal.Phase.Rejected))
        {
            var prior=Parse(latest,workspace,player);
            // Only a superseded plain gesture receipt is disposable. External
            // admission/refund and fetched-result receipts keep their own proof.
            if(prior.NativeReceipt==null&&prior.FetchId==0)root.SetAttributeValue("retireGesture",latest.Operation.ToString("N"));
        }
        if(fetchId>0){root.SetAttributeValue("fetchId",fetchId);root.SetElementValue("fetched",Grid(fetched.ToArray()));}
        if(historical)root.SetAttributeValue("sourceDeaths",sourceDeaths);
        if(sourceReturn!=null)root.Add(new XElement(sourceReturn));
        if(overflow!=null){root.SetElementValue("overflow",Grid(overflow.ToArray()));root.SetAttributeValue("overflowEntity",player.entityId);}
        if(pickupType!=-1)root.SetAttributeValue("pickupType",pickupType);
        byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>8*1024*1024)throw new InvalidDataException();
        var record=journal.Prepare(operation,key,RebirthCamperCharacterAccess.CurrentCreation(player),checked((latest?.Revision??0)+1),bytes,bytes);
        LatestOperations[key]=operation;
        if(detached)DetachedKeys.Add(key);
        if(sourceReturn?.Element("native")!=null)RebirthCookingNativeReturn.Refresh();
        var audit=Audit.GetValue(player,_=>new AuditCounts());audit.Prepared++;audit.Action=action;
        var intent=Parse(record,workspace,player);Active.Add(intent);if(!Current(intent))return true;
        player.Buffs.SetCustomVar(Receipt(operation),1f,true);GameManager.Instance.SaveLocalPlayerData();return true;
    }
    private static bool Conserved(RebirthCamperEnergyActorImage before,ItemStack[] gridBefore,RebirthCamperEnergyActorImage after,ItemStack[] gridAfter,IList<ItemStack> added)
    {
        Dictionary<string,long> Count(RebirthCamperEnergyActorImage actor,ItemStack[] grid,IList<ItemStack> extra)
        {
            var result=new Dictionary<string,long>(StringComparer.Ordinal);
            foreach(var stack in actor.Bag.Concat(actor.Belt).Concat(new[]{actor.Cursor}).Concat(grid).Concat(extra??Array.Empty<ItemStack>()))
            {if(stack==null||stack.IsEmpty())continue;string image=RebirthNativeItemCodec.Encode(stack.itemValue);result.TryGetValue(image,out long count);result[image]=checked(count+stack.count);}return result;
        }
        var a=Count(before,gridBefore,added);var b=Count(after,gridAfter,null);return a.Count==b.Count&&a.All(pair=>b.TryGetValue(pair.Key,out long count)&&count==pair.Value);
    }
    internal static void PrepareExternal(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,Guid operation,string nativeReceipt,ItemStack[] beforeGrid,ItemStack[] afterGrid,RebirthCookingOwnerDelta.Plan delta=null,Guid sourceOriginal=default(Guid))
    {
        if(!CurrentOwner(player)||operation==Guid.Empty||nativeReceipt==null||nativeReceipt.Length>96||beforeGrid?.Length!=12||afterGrid?.Length!=12||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))throw new InvalidDataException();
        var journal=Journal();var existing=journal.Read(operation);
        if(existing!=null)
        {
            var prior=Parse(existing,workspace,player);if(prior.NativeReceipt!=nativeReceipt||!Same(prior.GridBefore,beforeGrid)||!Same(prior.GridAfter,afterGrid))throw new InvalidDataException();return;
        }
        if(workspace==null)throw new InvalidDataException();string location=RebirthCookingStationLocator.Encode(workspace.CustodyStation);Guid key=Key(player,identity.CanonicalId,location);
        var latest=Latest(key);AssertAvailable(key,operation,latest);
        if(latest!=null)
        {
            var prior=Parse(latest,workspace,player);if(!Same(latest.State==RebirthCamperTransferJournal.Phase.Rejected?prior.GridBefore:prior.GridAfter,beforeGrid))throw new InvalidDataException("Cooking grid handoff source changed.");
        }
        else if(beforeGrid.Any(item=>!item.IsEmpty()))throw new InvalidDataException("Cooking grid handoff has no original source witness.");
        var before=RebirthCamperEnergyActorImage.Capture(player);var after=before.Copy();
        if(delta!=null){before.Bag=ItemStack.Clone(delta.BeforeBag);before.Belt=ItemStack.Clone(delta.BeforeBelt);after.Bag=ItemStack.Clone(delta.AfterBag);after.Belt=ItemStack.Clone(delta.AfterBelt);}
        var root=new XElement("cookingGridOriginal",new XAttribute("version",1),new XAttribute("world",player.world.worldState.Guid),new XAttribute("owner",identity.CanonicalId),
            new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(player)),new XAttribute("deaths",player.Died),new XAttribute("location",location),new XAttribute("action","external"),new XAttribute("nativeReceipt",nativeReceipt),
            new XElement("beforeActor",Actor(before)),new XElement("afterActor",Actor(after)),new XElement("beforeGrid",Grid(beforeGrid)),new XElement("afterGrid",Grid(afterGrid)));
        if(sourceOriginal!=Guid.Empty)root.SetAttributeValue("sourceOriginal",sourceOriginal.ToString("N"));
        byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>8*1024*1024)throw new InvalidDataException();
        journal.Prepare(operation,key,RebirthCamperCharacterAccess.CurrentCreation(player),checked((latest?.Revision??0)+1),bytes,bytes);
        LatestOperations[key]=operation;
    }
    internal static bool CompleteExternal(Guid operation)
    {
        try
        {
            var player=GameManager.Instance?.World?.GetPrimaryPlayer();
            if(operation==Guid.Empty||!ThreadManager.IsMainThread()||player==null||!player.IsSpawned()||player.IsDead()||
                !ReferenceEquals(player.world,GameManager.Instance.World)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
            var journal=Journal();var record=journal.Read(operation);
            if(record==null||record.State==RebirthCamperTransferJournal.Phase.Rejected||!record.Before.SequenceEqual(record.After))return false;
            var intent=Parse(record,null,player);int deaths=player.Died;var world=player.world;var worldState=world.worldState;string directory=GameIO.GetSaveGameDir();
            if(intent.Action!="external"||intent.Deaths!=deaths||
                (intent.NativeReceipt!=RebirthCookingRemoteAdmission.Receipt(operation)&&intent.NativeReceipt!="rbCookingGridReturn_"+operation.ToString("N"))||
                !RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.deaths!=deaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,intent.NativeReceipt,out float phase,out bool present)||!present||(phase!=2f&&phase!=3f))return false;
            bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead()&&player.Died==deaths&&
                ReferenceEquals(world.worldState,worldState)&&world.worldState.Guid==intent.World&&RebirthCamperCharacterAccess.CurrentCreation(player)==intent.Creation&&
                directory==GameIO.GetSaveGameDir()&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var currentOwner)&&currentOwner.CanonicalId==identity.CanonicalId;
            if(!Current())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(!Current())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            var terminal=journal.Read(operation);
            return Current()&&terminal?.State==RebirthCamperTransferJournal.Phase.Applied&&terminal.Operation==record.Operation&&terminal.Vehicle==record.Vehicle&&
                terminal.OwnerCreation==record.OwnerCreation&&terminal.Revision==record.Revision&&terminal.Before.SequenceEqual(record.Before)&&terminal.After.SequenceEqual(record.After);
        }
        catch(Exception error){Log.Warning("[Cooking] External grid completion retained: "+error.GetType().Name);return false;}
    }
    internal static void ConfirmAdmissionTerminal(Guid operation,bool refused)
    {
        var journal=Journal();var record=journal.Read(operation);if(record==null)return;var player=GameManager.Instance?.World?.GetPrimaryPlayer();if(player==null)return;
        var intent=Parse(record,null,player);
        if(intent.NativeReceipt!=RebirthCookingRemoteAdmission.Receipt(operation))throw new InvalidDataException();
        // Called only from the matching authenticated canonical host terminal
        // response. The terminal host journal already witnessed native payment;
        // this transfers off-bag grid ownership, never an actor inventory image.
        if(refused){if(record.State==RebirthCamperTransferJournal.Phase.Prepared)journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);return;}
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
    }
    internal static bool TryExternalGrid(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,Guid operation,out ItemStack[] before,out ItemStack[] after)
    {
        before=after=null;var record=Journal().Read(operation);if(record==null)return false;var intent=Parse(record,workspace,player);
        if(intent.NativeReceipt==null||workspace!=null&&intent.Location!=RebirthCookingStationLocator.Encode(workspace.CustodyStation))throw new InvalidDataException();
        before=ItemStack.Clone(intent.GridBefore);after=ItemStack.Clone(intent.GridAfter);return true;
    }
    internal static void RejectExternal(Guid operation)
    {
        var journal=Journal();var record=journal.Read(operation);if(record?.State!=RebirthCamperTransferJournal.Phase.Prepared)return;
        var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(player==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||(string)root.Attribute("world")!=player.world.worldState.Guid||
            (string)root.Attribute("owner")!=identity.CanonicalId||!RebirthCookingRemoteAdmissionBackend.Phase(identity,(string)root.Attribute("nativeReceipt"),out float phase)||phase!=1f)return;
        journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
    }
    internal static bool RejectUnappliedExternal(Guid operation,EntityPlayerLocal player)
    {
        if(operation==Guid.Empty||!ThreadManager.IsMainThread()||player==null||player.IsDead()||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player))return false;
        var journal=Journal();var record=journal.Read(operation);
        if(record==null)return true; // Positively absent local grid intent; owner receipt is checked by caller.
        var intent=Parse(record,null,player);
        if(record.State==RebirthCamperTransferJournal.Phase.Rejected)return true;
        if(record.State!=RebirthCamperTransferJournal.Phase.Prepared||intent.Deaths!=player.Died)return false;
        // Phase four is minted only by the exact original owner's verified
        // no-effect abandonment, after the authenticated host rejected it.
        if(intent.NativeReceipt==null||player.Buffs.GetCustomVar(intent.NativeReceipt)!=4f||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||
            !RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.deaths!=intent.Deaths||
            !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,intent.NativeReceipt,out float phase,out bool present)||!present||phase!=4f)return false;
        journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
        var terminal=journal.Read(operation);
        return terminal?.State==RebirthCamperTransferJournal.Phase.Rejected&&terminal.Operation==record.Operation&&
            terminal.Vehicle==record.Vehicle&&terminal.OwnerCreation==record.OwnerCreation&&terminal.Revision==record.Revision&&
            terminal.Before.SequenceEqual(record.Before)&&terminal.After.SequenceEqual(record.After);
    }
    internal static bool HasOwnedSnapshot(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,ItemStack[] image)
    {
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
            var latest=Latest(Key(player,identity.CanonicalId,RebirthCookingStationLocator.Encode(workspace.CustodyStation)));if(latest==null)return image.All(item=>item.IsEmpty());
            var prior=Parse(latest,workspace,player);
            if(prior.NativeReceipt!=null&&(latest.State==RebirthCamperTransferJournal.Phase.Prepared||latest.State==RebirthCamperTransferJournal.Phase.Uncertain))
            {
                if(!RebirthCookingRemoteAdmissionBackend.NativePhase(identity,prior.NativeReceipt,out float phase,out bool found))return false;
                return Same(found&&phase>=2f&&phase<=3f?prior.GridAfter:prior.GridBefore,image);
            }
            return latest.State==RebirthCamperTransferJournal.Phase.Applied&&Same(prior.GridAfter,image)||latest.State==RebirthCamperTransferJournal.Phase.Rejected&&Same(prior.GridBefore,image);
        }
        catch{return false;}
    }
    internal static bool Gesture(XUiC_RebirthCookingSlot slot,string method)
    {
        var player=slot?.xui?.playerUI?.entityPlayer as EntityPlayerLocal;if(player?.world==null)return false;
        var workspace=slot.GetParentByType<XUiC_RebirthCookingWorkspace>();if(workspace==null)return false;
        if(!CurrentOwner(player))return true;
        slot.lastClicked=false; // Native HandleClickComplete's input latch; no inventory effects.
        if(method=="HandleStackSwap"||method=="SwapItem"||method=="HandleMoveToPreferredLocation")slot.xui.PopupMenuWindow.Close();
        if(IsHeld(player)||RebirthCookingRemoteAdmission.IsHeld(workspace))return true;
        try
        {
            var before=RebirthCamperEnergyActorImage.Capture(player);var after=before.Copy();var grid=workspace.ReadCustodyGrid();var next=ItemStack.Clone(grid);int index=slot.SlotNumber;
            if(index<0||index>=12||!slot.AllowDropping)return true;var cursor=after.Cursor;var item=next[index];bool pickup=false;
            if(method=="HandleMoveToPreferredLocation")
            {
                if(item.IsEmpty()||slot.StackLock)return true;
                int remaining=PlanPreferred(player,after,item);if(remaining==item.count)return true;
                next[index]=remaining==0?ItemStack.Empty.Clone():new ItemStack(item.itemValue.Clone(),remaining);
            }
            else if(method=="HandlePartialStackPickup")
            {
                if(cursor.IsEmpty()&&!item.IsEmpty()&&item.count/2>0){int amount=item.count/2;after.Cursor=new ItemStack(item.itemValue.Clone(),amount);next[index]=new ItemStack(item.itemValue.Clone(),item.count-amount);pickup=true;}
            }
            else if(method=="HandleDropOne")
            {
                if(cursor.IsEmpty()||!cursor.CanMoveTo(slot.StackLocation)||!slot.CanSwap(cursor))return true;
                int maximum=slot.OverrideStackCount==-1?cursor.itemValue.ItemClassOrMissing.MaxCount:Math.Min(cursor.itemValue.ItemClassOrMissing.MaxCount,slot.OverrideStackCount);
                if(item.IsEmpty()){next[index]=new ItemStack(cursor.itemValue.Clone(),1);after.Cursor=cursor.count==1?ItemStack.Empty.Clone():new ItemStack(cursor.itemValue.Clone(),cursor.count-1);}
                else if(item.itemValue.type==cursor.itemValue.type&&item.count+1<=maximum){next[index]=new ItemStack(item.itemValue.Clone(),item.count+1);after.Cursor=cursor.count==1?ItemStack.Empty.Clone():new ItemStack(cursor.itemValue.Clone(),cursor.count-1);}
            }
            else if(method=="SwapItem")
            {
                if(!cursor.IsEmpty()&&!slot.CanSwap(cursor)||!cursor.CanMoveTo(slot.StackLocation,index))return true;
                next[index]=cursor.Clone();after.Cursor=item.Clone();pickup=true;
            }
            else
            {
                if(!cursor.IsEmpty()&&!slot.CanSwap(cursor)||!cursor.CanMoveTo(slot.StackLocation,index))return true;
                var itemClass=cursor.itemValue.ItemClassOrMissing;int maximum=itemClass==null?0:slot.OverrideStackCount==-1?itemClass.MaxCount:Math.Min(itemClass.MaxCount,slot.OverrideStackCount);
                bool limited=!cursor.IsEmpty()&&item.IsEmpty()&&maximum<cursor.count;
                if(limited){next[index]=new ItemStack(cursor.itemValue.Clone(),maximum);after.Cursor=new ItemStack(cursor.itemValue.Clone(),cursor.count-maximum);pickup=true;}
                else if(!item.IsEmpty()&&!cursor.IsEmpty()&&item.itemValue.ItemClassOrMissing.CanStack()&&itemClass.CanStack()&&item.itemValue.type==cursor.itemValue.type&&!cursor.itemValue.HasQuality&&!item.itemValue.HasQuality)
                {int moved=Math.Min(cursor.count,maximum-item.count);if(moved<0)return true;next[index]=new ItemStack(item.itemValue.Clone(),item.count+moved);after.Cursor=moved==cursor.count?ItemStack.Empty.Clone():new ItemStack(cursor.itemValue.Clone(),cursor.count-moved);pickup=moved<cursor.count;}
                else{next[index]=cursor.Clone();after.Cursor=item.Clone();pickup=true;}
            }
            if(Same(grid,next)&&before.Matches(after))return true;
            Begin(workspace,player,method,before,after,grid,next,pickupType:pickup?(int)slot.StackLocation:-1);return true;
        }
        catch(Exception error){workspace.CustodyStatus("Original cooking gesture retained: "+error.GetType().Name);return true;}
    }
    internal static bool Return(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player)
    {
        if(player?.world==null)return false;
        if(!CurrentOwner(player))return true;
        if(IsHeld(player)||RebirthCookingRemoteAdmission.IsHeld(workspace))return true;
        try
        {
            var grid=workspace.ReadCustodyGrid();var items=grid.Where(item=>!item.IsEmpty()).ToArray();if(items.Length==0)return true;
            if(player.world.IsRemote()==false&&TryBeginSourceReturn(workspace,player,grid))return true;
            var before=RebirthCamperEnergyActorImage.Capture(player);var after=before.Copy();
            var next=ItemStack.Clone(grid);var overflow=new List<ItemStack>();
            for(int i=0;i<next.Length;i++)if(!next[i].IsEmpty())
            {
                int remaining=PlanPreferred(player,after,next[i]);
                if(remaining>0&&player.world.IsRemote()==false&&overflow.Count<255){overflow.Add(new ItemStack(next[i].itemValue.Clone(),remaining));remaining=0;}
                next[i]=remaining==0?ItemStack.Empty.Clone():new ItemStack(next[i].itemValue.Clone(),remaining);
            }
            if(Same(grid,next)){workspace.CustodyStatus("Original ingredients remain in cooking custody; free native inventory space or use them in the next batch.");return true;}
            Begin(workspace,player,"return",before,after,grid,next,overflow:overflow.Count==0?null:overflow);return true;
        }
        catch(Exception error){workspace.CustodyStatus("Original ingredient return retained: "+error.GetType().Name);return true;}
    }
    // Exact ingredient branch of native XUiM_PlayerInventory.AddItem: partial
    // existing stacks, preferred belt slots, bag allocation, then belt allocation.
    // Armor auto-equipment is outside the authored cooking ingredient contract.
    private static int PlanPreferred(EntityPlayerLocal player,RebirthCamperEnergyActorImage image,ItemStack original)
    {
        var incoming=original.Clone();if(!incoming.CanMoveTo(XUiC_ItemStack.StackLocationTypes.ToolBelt)||incoming.itemValue.ItemClass is ItemClassArmor)throw new InvalidDataException();
        int beltCount=RebirthToolbeltCapacity.GetOwnedSlotCount(player,image.Belt.Length);int[] preferred=(int[])player.inventory.preferredItemTypes.Clone();
        bool CanPartial(ItemStack[] cells,int limit)=>cells.Take(limit).Any(item=>item.CanStackPartlyWith(incoming,out int _));
        bool CanFull(ItemStack[] cells,int limit,bool belt)=>cells.Take(limit).Select((item,index)=>
            (!belt||player.inventory.CanMoveToSlot(incoming,index))&&(item.IsEmpty()||item.CanStackWith(incoming))).Any(value=>value);
        void Partial(ItemStack[] cells,int limit,bool belt)
        {
            var previous=RebirthCookingPartialMergeScope.Incoming;RebirthCookingPartialMergeScope.Incoming=incoming.itemValue;
            try
            {
            for(int i=0;i<limit&&incoming.count>0;i++)
            {
                if(belt&&!player.inventory.CanMoveToSlot(incoming,i))continue;int amount=incoming.count;var item=cells[i];
                if(item.itemValue.type!=incoming.itemValue.type||!RebirthCookingItemStats.Compatible(item.itemValue,incoming.itemValue)||!item.CanStackPartly(ref amount))continue;
                cells[i]=new ItemStack(item.itemValue.Clone(),checked(item.count+amount));incoming.count-=amount;
            }
            }
            finally{RebirthCookingPartialMergeScope.Incoming=previous;}
        }
        if(CanPartial(image.Bag,image.Bag.Length))Partial(image.Bag,image.Bag.Length,false);else if(CanPartial(image.Belt,beltCount))Partial(image.Belt,beltCount,true);
        if(incoming.count>0)
        {if(CanFull(image.Bag,image.Bag.Length,false))Partial(image.Bag,image.Bag.Length,false);else if(CanFull(image.Belt,beltCount,true))Partial(image.Belt,beltCount,true);}
        bool Add(ItemStack[] cells,int limit,ItemStack portion)
        {
            for(int i=0;i<limit;i++)if(cells[i].CanStackWith(portion)){cells[i]=new ItemStack(cells[i].itemValue.Clone(),checked(cells[i].count+portion.count));return true;}
            for(int i=0;i<limit;i++)if(cells[i].IsEmpty()){cells[i]=portion.Clone();return true;}return false;
        }
        int maximum=incoming.itemValue.ItemClass?.Stacknumber!=null?incoming.itemValue.ItemClass.MaxCount:1;if(maximum<1)throw new InvalidDataException();
        while(incoming.count>0)
        {
            var portion=new ItemStack(incoming.itemValue.Clone(),Math.Min(incoming.count,maximum));bool placed=false;
            for(int i=0;i<beltCount&&i<preferred.Length;i++)if(preferred[i]==portion.itemValue.type)
            {
                var existing=image.Belt[i];
                if(existing.itemValue.type==portion.itemValue.type&&existing.CanStackWith(portion))
                {image.Belt[i]=new ItemStack(existing.itemValue.Clone(),checked(existing.count+portion.count));placed=true;break;}
                if(existing.IsEmpty()&&player.inventory.CanMoveToSlot(portion,i)){image.Belt[i]=portion.Clone();placed=true;break;}
            }
            if(!placed&&portion.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack))placed=Add(image.Bag,image.Bag.Length,portion);
            if(!placed)placed=Add(image.Belt,beltCount,portion);if(!placed)break;incoming.count-=portion.count;
        }
        return incoming.count;
    }
    internal static bool Pull(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,IList<ItemStack> needs,ItemStack[] ghosts)
    {
        if(player?.world==null)return false;
        if(!CurrentOwner(player))return true;
        if(IsHeld(player)||RebirthCookingRemoteAdmission.IsHeld(workspace))return true;
        try
        {
            var before=RebirthCamperEnergyActorImage.Capture(player);var after=before.Copy();var grid=workspace.ReadCustodyGrid();var next=ItemStack.Clone(grid);
            foreach(var need in needs)
            {
                if(need==null||need.IsEmpty())throw new InvalidDataException();var required=need.itemValue.Clone();required.Metadata?.Remove("rebirth.cooking.fullIngredient");
                int destination=Array.FindIndex(ghosts,item=>item!=null&&!item.IsEmpty()&&item.itemValue.type==required.type);if(destination<0)throw new InvalidDataException();
                long left=need.count;
                for(int area=0;area<2&&left>0;area++)
                {
                    var cells=area==0?after.Bag:after.Belt;int limit=area==0?cells.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,cells.Length);
                    for(int i=0;i<limit&&left>0;i++)
                    {
                        var item=cells[i];if(item.IsEmpty()||item.itemValue.type!=required.type||!RebirthCookingItemStats.Compatible(item.itemValue,required)||!RebirthCookingItemStats.FullIngredient(item.itemValue))continue;
                        int count=(int)Math.Min(left,item.count);var loaded=next[destination];
                        if(!loaded.IsEmpty()&&!RebirthCookingItemStats.Compatible(loaded.itemValue,item.itemValue))throw new InvalidDataException();
                        long total=(long)(loaded.IsEmpty()?0:loaded.count)+count;if(total>int.MaxValue)throw new InvalidDataException();
                        next[destination]=new ItemStack((loaded.IsEmpty()?item.itemValue:loaded.itemValue).Clone(),(int)total);
                        cells[i]=item.count==count?ItemStack.Empty.Clone():new ItemStack(item.itemValue.Clone(),item.count-count);left-=count;
                    }
                }
                if(left!=0){workspace.CustodyStatus("Not enough full ingredients remain in the original owner inventory.");return true;}
            }
            Begin(workspace,player,"pull",before,after,grid,next);return true;
        }
        catch(Exception error){workspace.CustodyStatus("Original cooking pull retained: "+error.GetType().Name);return true;}
    }
    internal static bool PlaceFetched(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,long id,IList<ItemStack> fetched,IList<ItemStack> localNeeds,ItemStack[] planned)
    {
        try
        {
            if(!RebirthCookingLegacyPull.ValidateOriginal(player,id,fetched)||IsHeld(player)||RebirthCookingRemoteAdmission.IsHeld(workspace))return false;
            var before=RebirthCamperEnergyActorImage.Capture(player);var after=before.Copy();
            foreach(var need in localNeeds)
            {
                var required=need.itemValue.Clone();required.Metadata?.Remove("rebirth.cooking.fullIngredient");long left=need.count;
                for(int area=0;area<2&&left>0;area++)
                {
                    var cells=area==0?after.Bag:after.Belt;int limit=area==0?cells.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,cells.Length);
                    for(int i=0;i<limit&&left>0;i++)
                    {
                        var item=cells[i];if(item.IsEmpty()||item.itemValue.type!=required.type||!RebirthCookingItemStats.Compatible(item.itemValue,required)||!RebirthCookingItemStats.FullIngredient(item.itemValue))continue;
                        int take=(int)Math.Min(left,item.count);cells[i]=take==item.count?ItemStack.Empty.Clone():new ItemStack(item.itemValue.Clone(),item.count-take);left-=take;
                    }
                }
                if(left!=0)return false;
            }
            return Begin(workspace,player,"fetched",before,after,workspace.ReadCustodyGrid(),planned,id,fetched);
        }
        catch(Exception error){workspace.CustodyStatus("Original fetched grid custody retained: "+error.GetType().Name);return false;}
    }
    internal static bool ReturnFetched(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,long id,IList<ItemStack> fetched)
    {
        try
        {
            if(!RebirthCookingLegacyPull.ValidateOriginal(player,id,fetched)||IsHeld(player)||RebirthCookingRemoteAdmission.IsHeld(workspace))return false;
            if(fetched.Count==0&&RebirthCookingLegacyPull.IsNoPaymentOriginal(player,id))return true;
            if(player.world.IsRemote()==false)return BeginDetachedFetchedReturn(player,id,fetched);
            var before=RebirthCamperEnergyActorImage.Capture(player);var after=before.Copy();
            if(!RebirthRecyclerOwnerDelivery.Plan(before.Bag,before.Belt,before.BagLocks,RebirthCamperEnergyItems.Locks(player.inventory.ItemGrid.SlotLocks),player.inventory.Length,
                fetched,out after.Bag,out after.Belt))return false;
            var grid=workspace.ReadCustodyGrid();return Begin(workspace,player,"fetched-return",before,after,grid,grid,id,fetched);
        }
        catch(Exception error){workspace.CustodyStatus("Original fetched ingredient receipt retained: "+error.GetType().Name);return false;}
    }
    private static bool Saved(Intent intent,int phase,RebirthCamperEnergyActorImage expected)
    {
        return RebirthStablePlayerIdentity.TryResolveServerEntity(intent.Player,out var identity)&&RebirthCamperEnergySavedOwner.TryRead(identity,out var saved,out var file)&&file.deaths==intent.Deaths&&
            RebirthCookingRemoteAdmissionBackend.NativePhase(file,Receipt(intent.Operation),out float actual,out bool present)&&present&&actual==phase&&expected.Matches(saved);
    }
    internal static string Digest(string image)
    {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(image))).Replace("-","");}
    internal static string ReadWireString(BinaryReader reader,int maximum)
    {
        int count=reader.ReadInt32();if(count<0||count>maximum)throw new InvalidDataException();
        byte[] bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);
    }
    internal static void WriteWireString(BinaryWriter writer,string value,int maximum)
    {
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(value??"");if(bytes.Length>maximum)throw new InvalidDataException();writer.Write(bytes.Length);writer.Write(bytes);
    }
    private static bool HostSaved(Intent intent,int phase,RebirthCamperEnergyActorImage image)
    {
        if(intent.ServerPhase>=phase)return true;if(!Current(intent)||UnityEngine.Time.realtimeSinceStartup<intent.NextWitness)return false;
        if(!intent.WorldObject.IsRemote())
        {
            bool accepted=VerifyWitness(intent.Player,intent.Operation,phase,intent.World,intent.Owner,intent.Creation,intent.Deaths,Actor(image),
                intent.ParentAdmission==Guid.Empty&&intent.FetchId==0?"":Encoding.UTF8.GetString(intent.Record.Before));
            if(!accepted||!Current(intent)){intent.NextWitness=UnityEngine.Time.realtimeSinceStartup+1f;return false;}
            intent.NextWitness=0f;
            intent.ServerPhase=Math.Max(intent.ServerPhase,phase);return true;
        }
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;var channels=connection?.GetConnectionToServer();
        var packet=NetPackageManager.GetPackage<NetPackageRebirthCookingGridOwnerWitness>();int channel=packet.Channel;
        var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(intent.Player.entityId);
        if(connection==null||channels==null||channel<0||channel>=channels.Length||channels[channel]==null||channels[channel].IsDisconnected()||persistent?.PrimaryId==null)return false;
        string expected=Actor(image);intent.WitnessNonce=Guid.NewGuid();intent.WitnessHash=Digest(expected);intent.WitnessPhase=phase;intent.Connection=connection;intent.Peer=channels[channel];intent.NextWitness=UnityEngine.Time.realtimeSinceStartup+1f;
        connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(intent.Player),false);
        connection.SendToServer(packet.Setup(intent.Player.entityId,persistent.PrimaryId,intent.Operation,intent.WitnessNonce,phase,intent.World,intent.Owner,intent.Creation,intent.Deaths,expected,
            intent.ParentAdmission==Guid.Empty&&intent.FetchId==0?"":Encoding.UTF8.GetString(intent.Record.Before)));return false;
    }
    internal static bool VerifyWitness(EntityPlayer player,Guid operation,int phase,string world,string owner,string creation,int deaths,string expected,string original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,player.world)||player.IsDead()||player.Died!=deaths||operation==Guid.Empty||(phase!=1&&phase!=2)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.world.worldState.Guid!=world||!RebirthSurvivorRequestScope.Matches(creation,RebirthCamperCharacterAccess.CurrentCreation(player))||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity.CanonicalId!=owner)return false;
            if(!ReferenceEquals(player.world.GetPrimaryPlayer(),player))
            {
                if(!ReferenceEquals(witnessWorld,player.world)){WitnessRate.Clear();witnessWorld=player.world;}
                float now=UnityEngine.Time.realtimeSinceStartup;if(WitnessRate.TryGetValue(player.entityId,out float next)&&now<next)return false;
                if(WitnessRate.Count>=128&&!WitnessRate.ContainsKey(player.entityId))return false;WitnessRate[player.entityId]=now+.25f;
            }
            var image=Actor(expected);if(!RebirthCamperEnergySavedOwner.TryRead(identity,out var saved,out var file)||file.deaths!=deaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(file,Receipt(operation),out float actual,out bool present)||!present||actual!=phase||!image.Matches(saved))return false;
            if(string.IsNullOrEmpty(original))return true;
            var handoff=RebirthCookingRemoteAdmission.ReadProtocolXml(original,8*1024*1024);
            if(handoff.Attribute("parentAdmission")!=null&&!RebirthCookingRemoteAdmissionBackend.WitnessGridAdoption(player,operation,phase,expected,original))return false;
            if(((long?)handoff.Attribute("fetchId")??0)>0&&!(handoff.Attribute("sourceDeaths")!=null?
                RebirthRemoteResourceDurableOutcome.WitnessHistoricalCookingRefund(player,operation,phase,expected,original):
                RebirthRemoteResourceDurableOutcome.WitnessCookingGrid(player,operation,phase,expected,original)))return false;
            return handoff.Attribute("parentAdmission")!=null||((long?)handoff.Attribute("fetchId")??0)>0;
        }
        catch{return false;}
    }
    internal static void ReceiveWitness(World world,Guid operation,Guid nonce,int phase,string hash,bool accepted)
    {
        if(!accepted||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world))return;
        var intent=Active.FirstOrDefault(value=>value.Operation==operation&&ReferenceEquals(value.WorldObject,world));if(intent==null||!Current(intent)||intent.WitnessNonce!=nonce||intent.WitnessPhase!=phase||intent.WitnessHash!=hash)return;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;var channels=connection?.GetConnectionToServer();int channel=NetPackageManager.GetPackage<NetPackageRebirthCookingGridOwnerWitness>().Channel;
        if(!ReferenceEquals(connection,intent.Connection)||channels==null||channel<0||channel>=channels.Length||!ReferenceEquals(channels[channel],intent.Peer))return;
        intent.ServerPhase=Math.Max(intent.ServerPhase,phase);
    }
    private static bool Compatible(ItemStack[] live,ItemStack[] before,ItemStack[] after)=>live!=null&&live.Length==before.Length&&live.Length==after.Length&&
        live.Select((item,index)=>Same(item,before[index])||Same(item,after[index])).All(value=>value);
    private static void Apply(Intent intent,bool projectGrid=true)
    {
        if(!Current(intent))throw new InvalidOperationException("Original cooking owner scope changed.");
        var player=intent.Player;var actual=RebirthCamperEnergyActorImage.Capture(player);var grid=intent.Workspace?.ReadCustodyGrid()??intent.GridBefore;
        if(!Compatible(actual.Bag,intent.Before.Bag,intent.After.Bag)||!Compatible(actual.Belt,intent.Before.Belt,intent.After.Belt)||
            !Same(actual.Cursor,intent.Before.Cursor)&&!Same(actual.Cursor,intent.After.Cursor)||!Compatible(grid,intent.GridBefore,intent.GridAfter))throw new InvalidOperationException("Original cooking image changed.");
        void Write(ItemStackGrid target,ItemStack[] live,ItemStack[] desired,bool bag)
        {
            for(int i=0;i<desired.Length;i++)if(!Same(live[i],desired[i]))
            {
                var value=desired[i].Clone();permit=new Permit{Target=target,Slot=i,Value=value,Intent=intent};
                try{if(bag)player.bag.SetSlot(i,value);else player.inventory.SetItem(i,value);if(!permit.Used||!Same(target.items[i],value))throw new InvalidOperationException("Original cooking native setter refused.");}
                finally{permit=null;}
            }
        }
        Write(player.bag.ItemGrid,actual.Bag,intent.After.Bag,true);Write(player.inventory.ItemGrid,actual.Belt,intent.After.Belt,false);
        if(!Same(actual.Cursor,intent.After.Cursor))
        {
            var value=intent.After.Cursor.Clone();permit=new Permit{Target=player.PlayerUI.xui.DragAndDropWindow,Value=value,Intent=intent};
            try{player.PlayerUI.xui.DragAndDropWindow.SetCurrentStack(value,false);if(!permit.Used)throw new InvalidOperationException("Original cooking cursor setter refused.");}finally{permit=null;}
        }
        if(intent.PickupType!=-1)player.PlayerUI.xui.DragAndDropWindow.PickUpType=(XUiC_ItemStack.StackLocationTypes)intent.PickupType;
        if(projectGrid&&intent.Workspace!=null)for(int i=0;i<intent.GridAfter.Length;i++)if(!Same(intent.Workspace.ReadCustodyGrid()[i],intent.GridAfter[i]))
        {if(!Current(intent))throw new InvalidOperationException("Original cooking owner scope changed.");intent.Workspace.WriteCustodyGrid(i,intent.GridAfter[i].Clone());}
        if(!Current(intent)||!intent.After.Matches(RebirthCamperEnergyActorImage.Capture(player))||projectGrid&&intent.Workspace!=null&&!Same(intent.GridAfter,intent.Workspace.ReadCustodyGrid()))throw new InvalidOperationException("Original cooking delta incomplete.");
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        Bootstrap(GameManager.Instance?.World?.GetPrimaryPlayer());
        if(Active.Count==0)return;
        foreach(var intent in Active.ToArray())
        {
            try
            {
                if(ReferenceEquals(intent.WorldObject,GameManager.Instance?.World)&&intent.Action=="return"&&
                    TryRecoverSavedOwnerReturn(intent.Workspace,intent.Player,intent.Record,out bool ownerRecovered)&&ownerRecovered)continue;
                if(!Current(intent)||intent.EvidenceOnly)continue;
                if(intent.Record.State==RebirthCamperTransferJournal.Phase.Prepared)
                {
                    if(intent.FetchId>0&&Saved(intent,2,intent.After))
                    {
                        if(!intent.After.Matches(RebirthCamperEnergyActorImage.Capture(intent.Player))||!HostSaved(intent,2,intent.After)||!Current(intent)||
                            !intent.After.Matches(RebirthCamperEnergyActorImage.Capture(intent.Player)))continue;
                    }
                    else
                    {
                        if(!Saved(intent,1,intent.Before)){if(intent.Before.Matches(RebirthCamperEnergyActorImage.Capture(intent.Player))){intent.Player.Buffs.SetCustomVar(Receipt(intent.Operation),1f,true);GameManager.Instance.SaveLocalPlayerData();}continue;}
                        if(!HostSaved(intent,1,intent.Before))continue;
                    }
                    intent.Record=Journal().Advance(intent.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                }
                if(intent.Record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                {
                    var original=XElement.Parse(Encoding.UTF8.GetString(intent.Record.Before));
                    bool ownerOverflow=original.Element("overflow")!=null;
                    if(ownerOverflow&&!AdvanceOwnerReturnOverflow(intent))continue;
                    if(original.Element("sourceReturn")!=null)
                    {
                        var sourceResult=AdvanceSourceReturn(intent);
                        if(sourceResult<0)continue;
                        if(!Current(intent))continue;
                        if(sourceResult==0)
                        {intent.Record=Journal().Advance(intent.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Rejected);Active.Remove(intent);intent.Workspace?.CustodyTransitionFinished(intent.Action);continue;}
                    }
                    bool savedFetchedAfter=intent.FetchId>0&&Saved(intent,2,intent.After);
                    if(ownerOverflow){} // Owner credit/drop facts already qualified; project no actor image again.
                    else if(savedFetchedAfter)
                    {
                        // A saved native effect is not another Apply permission.
                        // Changed live owner data remains unresolved; never
                        // restore it from the old BEFORE/AFTER snapshots.
                        if(!intent.After.Matches(RebirthCamperEnergyActorImage.Capture(intent.Player))||!HostSaved(intent,2,intent.After)||!Current(intent)||
                            !intent.After.Matches(RebirthCamperEnergyActorImage.Capture(intent.Player)))continue;
                        if(intent.Workspace!=null)
                        {
                            var grid=intent.Workspace.ReadCustodyGrid();if(!Compatible(grid,intent.GridBefore,intent.GridAfter))throw new InvalidOperationException("Saved cooking grid ownership changed.");
                            for(int i=0;i<intent.GridAfter.Length;i++)if(!Same(grid[i],intent.GridAfter[i]))
                            {if(!Current(intent))break;intent.Workspace.WriteCustodyGrid(i,intent.GridAfter[i].Clone());}
                            if(!Current(intent)||!Same(intent.Workspace.ReadCustodyGrid(),intent.GridAfter))continue;
                        }
                    }
                    else
                    {
                        if(intent.FetchId>0&&intent.ServerPhase<1&&!HostSaved(intent,1,intent.Before))continue;
                        Apply(intent);intent.Player.Buffs.SetCustomVar(Receipt(intent.Operation),2f,true);GameManager.Instance.SaveLocalPlayerData();
                        if(!Saved(intent,2,intent.After))continue;
                        if(!HostSaved(intent,2,intent.After))continue;
                    }
                    if(savedFetchedAfter&&(!Current(intent)||!intent.After.Matches(RebirthCamperEnergyActorImage.Capture(intent.Player))))continue;
                    intent.Record=Journal().Advance(intent.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
                    if(intent.FetchId>0)RebirthCookingLegacyPull.CompleteGridOriginal(intent.Player,intent.FetchId);
                    if(intent.ParentAdmission!=Guid.Empty)RebirthCookingRemoteAdmission.CompleteUnpaidAdoption(intent.Player,intent.ParentAdmission);
                    if(Guid.TryParseExact((string)original.Attribute("retireGesture"),"N",out var retired)&&retired!=intent.Operation)
                    {
                        var priorRecord=Journal().Read(retired);var prior=priorRecord==null?null:Parse(priorRecord,null,intent.Player);
                        if(prior!=null&&prior.NativeReceipt==null&&prior.FetchId==0&&prior.Key==intent.Key&&prior.Revision<intent.Revision&&
                            (priorRecord.State==RebirthCamperTransferJournal.Phase.Applied||priorRecord.State==RebirthCamperTransferJournal.Phase.Rejected))
                        {intent.Player.Buffs.RemoveCustomVar(Receipt(retired));GameManager.Instance.SaveLocalPlayerData();}
                    }
                    Audit.GetValue(intent.Player,_=>new AuditCounts()).Applied++;
                    Active.Remove(intent);intent.Workspace?.CustodyStatus("");intent.Workspace?.CustodyTransitionFinished(intent.Action);
                }
            }
            catch(Exception error){intent.Workspace?.CustodyStatus("Original cooking grid custody retained: "+error.GetType().Name);}
        }
        AdvanceDetachedSourceReturns();
    }
}

[HarmonyPatch]
internal static class RebirthCookingGridGesture
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","HandleMoveToPreferredLocation","SwapItem"})yield return AccessTools.Method(typeof(XUiC_ItemStack),name)??throw new MissingMethodException(typeof(XUiC_ItemStack).Name,name);}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_ItemStack __instance,System.Reflection.MethodBase __originalMethod)=>__instance is not XUiC_RebirthCookingSlot slot||!RebirthCookingGridCustody.Gesture(slot,__originalMethod.Name);
}

[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthCookingGridOwnerWitness:NetPackage
{
    private const uint Version=0x32475743;private int playerId,phase,deaths;private PlatformUserIdentifierAbs user;
    private Guid operation,nonce;private string worldId,owner,creation,expected,handoff;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthCookingGridOwnerWitness Setup(int actor,PlatformUserIdentifierAbs identity,Guid original,Guid correlation,int stage,string world,string canonical,string character,int deathCount,string image,string originalHandoff)
    {playerId=actor;user=identity;operation=original;nonce=correlation;phase=stage;worldId=world;owner=canonical;creation=character;deaths=deathCount;expected=image;handoff=originalHandoff;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadUInt32()!=Version)throw new InvalidDataException();playerId=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);operation=new Guid(reader.ReadBytes(16));nonce=new Guid(reader.ReadBytes(16));phase=reader.ReadByte();
        worldId=RebirthCookingGridCustody.ReadWireString(reader,64);owner=RebirthCookingGridCustody.ReadWireString(reader,2048);creation=RebirthCookingGridCustody.ReadWireString(reader,512);deaths=reader.ReadInt32();expected=RebirthCookingGridCustody.ReadWireString(reader,6*1024*1024);
        handoff=RebirthCookingGridCustody.ReadWireString(reader,8*1024*1024);
        if(operation==Guid.Empty||nonce==Guid.Empty||(phase!=1&&phase!=2)||worldId.Length>64||owner.Length>512||creation.Length>128||deaths<0||expected.Length>6*1024*1024)throw new InvalidDataException();
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write(Version);writer.Write(playerId);user.ToStream(writer);writer.Write(operation.ToByteArray());writer.Write(nonce.ToByteArray());writer.Write((byte)phase);RebirthCookingGridCustody.WriteWireString(writer,worldId,64);RebirthCookingGridCustody.WriteWireString(writer,owner,2048);RebirthCookingGridCustody.WriteWireString(writer,creation,512);writer.Write(deaths);RebirthCookingGridCustody.WriteWireString(writer,expected,6*1024*1024);RebirthCookingGridCustody.WriteWireString(writer,handoff,8*1024*1024);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(callbacks,GameManager.Instance)||
            manager?.IsServer!=true||!ValidEntityIdForSender(playerId)||!ValidUserIdForSender(user)||Sender?.loginDone!=true||Sender.disconnecting||
            !ReferenceEquals(manager.Clients?.ForEntityId(playerId),Sender)||!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(this,out var received)||received.IsDisconnected()||
            Channel<0||Sender.netConnection==null||Channel>=Sender.netConnection.Length||!ReferenceEquals(Sender.netConnection[Channel],received))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        if(player==null||!ReferenceEquals(player.world,world)||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(player)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var stable)||!RebirthStablePlayerIdentity.TryFromClientInfo(Sender,out var authenticated)||
            authenticated.CanonicalId!=stable.CanonicalId||owner!=stable.CanonicalId)return;
        bool accepted=RebirthCookingGridCustody.VerifyWitness(player,operation,phase,worldId,owner,creation,deaths,expected,handoff);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCookingGridOwnerWitnessResult>().Setup(operation,nonce,phase,RebirthCookingGridCustody.Digest(expected),accepted),_attachedToEntityId:playerId);
    }
    public int GetLength()=>0;
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthCookingGridOwnerWitnessResult:NetPackage
{
    private const uint Version=0x31584743;private Guid operation,nonce;private int phase;private string hash;private bool accepted;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCookingGridOwnerWitnessResult Setup(Guid original,Guid correlation,int stage,string digest,bool saved)
    {operation=original;nonce=correlation;phase=stage;hash=digest;accepted=saved;return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadUInt32()!=Version)throw new InvalidDataException();operation=new Guid(reader.ReadBytes(16));nonce=new Guid(reader.ReadBytes(16));phase=reader.ReadByte();hash=RebirthCookingGridCustody.ReadWireString(reader,64);accepted=reader.ReadBoolean();if(operation==Guid.Empty||nonce==Guid.Empty||(phase!=1&&phase!=2)||hash.Length!=64)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write(Version);writer.Write(operation.ToByteArray());writer.Write(nonce.ToByteArray());writer.Write((byte)phase);RebirthCookingGridCustody.WriteWireString(writer,hash,64);writer.Write(accepted);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(callbacks,GameManager.Instance)||
            manager?.IsServer!=false||!manager.IsConnected||!RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(this,out var received)||received.IsDisconnected()||
            Channel<0||manager.connectionToServer==null||Channel>=manager.connectionToServer.Length||!ReferenceEquals(manager.connectionToServer[Channel],received))return;
        RebirthCookingGridCustody.ReceiveWitness(world,operation,nonce,phase,hash,accepted);
    }
    public int GetLength()=>0;
}
