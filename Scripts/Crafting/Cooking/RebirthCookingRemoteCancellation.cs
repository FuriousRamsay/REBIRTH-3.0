using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Cancellation retains the original paid native ingredients until both the
// owner's saved refund and the station's saved removal have been witnessed.
internal static partial class RebirthCookingRemoteCancellation
{
    private const string Marker="rebirth.cooking.queue.cancellation";
    private sealed class Client
    {
        internal EntityPlayerLocal Player;internal World World;internal string Owner,Creation,Location,Token,Legacy;internal int Deaths,LegacySlot=-1;
        internal RebirthCookingOwnerDelta.Plan Plan;internal bool Waiting,Terminal;internal float Next;internal Func<bool> Project;
    }
    private static readonly List<Client> Clients=new List<Client>();
    private static bool CurrentClient(Client client)=>ThreadManager.IsMainThread()&&client?.Player!=null&&
        ReferenceEquals(GameManager.Instance?.World,client.World)&&ReferenceEquals(client.Player.world,client.World)&&
        ReferenceEquals(client.World.GetPrimaryPlayer(),client.Player)&&ReferenceEquals(client.World.GetEntity(client.Player.entityId),client.Player)&&
        client.Player.IsSpawned()&&!client.Player.IsDead()&&client.Player.Died==client.Deaths&&
        RebirthCamperCharacterAccess.CurrentCreation(client.Player)==client.Creation&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(client.Player,out var owner)&&owner.CanonicalId==client.Owner;
    internal static bool PresentationHeld(TileEntityWorkstation station)
    {
        if(station==null)return false;
        try{string location=RebirthCookingStationLocator.Encode(station);return Clients.Any(client=>CurrentClient(client)&&client.Location==location);}
        catch{return true;}
    }
    private sealed class Counts{internal long Completed;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,Counts> Audit=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,Counts>();
    internal static Newtonsoft.Json.Linq.JObject CaptureAuditState(EntityPlayer actor)
    {
        Counts counts=null;if(actor!=null)Audit.TryGetValue(actor,out counts);
        var client=Clients.FirstOrDefault(value=>ReferenceEquals(value.Player,actor)&&ReferenceEquals(value.World,GameManager.Instance?.World));
        return new Newtonsoft.Json.Linq.JObject{["pending"]=client!=null,["completed"]=counts?.Completed??0,["token"]=client?.Token,["plan"]=client?.Plan?.Operation.ToString("N")};
    }
    private static RebirthCamperTransferJournal Journal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingCancellations"));
    private static RebirthCamperTransferJournal Deltas()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingCancellationRefunds"));
    private static bool Token(Recipe recipe,out Guid token)
    {
        token=Guid.Empty;return RebirthCookingHeat.Managed(recipe)&&recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.queue.token",out string text)&&Guid.TryParseExact(text,"N",out token)&&token!=Guid.Empty;
    }
    internal static bool TryCancel(XUiC_RecipeStack card)
    {
        var player=card?.xui?.playerUI?.entityPlayer;
        if(player?.world==null)return false;
        // Canonical paid batches use the same saved station/owner handshake
        // for host-local players; SessionService already has a local dispatch.
        if(!RebirthCookingHeat.Managed(card.recipe)||RebirthCookingHeat.Ready(card.recipe))return false;
        Clients.RemoveAll(value=>!CurrentClient(value)); // Durable server originals survive; obsolete UI references do not.
        if(player is not EntityPlayerLocal local)return true;
        bool canonical=Token(card.recipe,out var token);
        string legacy=canonical?null:LegacySignature(card.recipe,card.recipeCount,card.OutputQuality);
        if(!canonical&&legacy==null)return true;
        int legacySlot=-1;
        if(!canonical)
        {
            var cards=(card.Owner as XUiC_CraftingQueue)?.GetRecipesToCraft();if(cards==null||cards.Length>byte.MaxValue)return true;
            legacySlot=Array.IndexOf(cards,card);if(legacySlot<0)return true;
        }
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(local,out var identity))return true;
        var candidate=new Client{Player=local,World=local.world,Creation=RebirthCamperCharacterAccess.CurrentCreation(local),Owner=identity.CanonicalId,Deaths=local.Died};
        if(!CurrentClient(candidate))return true;
        var station=card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;
        if(station==null)return true;
        string location=RebirthCookingStationLocator.Encode(station);
        candidate.Location=location;candidate.Project=()=>RebirthCookingCancellationProjection.Refresh(card,candidate.Location,candidate.Token,()=>CurrentClient(candidate));
        if(!Clients.Any(value=>ReferenceEquals(value.Player,local)&&value.Location==location&&ReferenceEquals(value.World,local.world)&&
            (canonical?value.Token==token.ToString("N"):value.Legacy==legacy&&value.LegacySlot==legacySlot)))
        {
            if(Clients.Count>=4)return true;
            candidate.Location=location;candidate.Token=(canonical?token:Guid.NewGuid()).ToString("N");candidate.Legacy=legacy;candidate.LegacySlot=legacySlot;Clients.Add(candidate);
        }
        TickClients();return true;
    }
    internal static bool Held(TileEntityWorkstation station)
    {
        if(station?.Queue==null)return false;
        foreach(var row in station.Queue)
        {
            if(!Token(row?.Recipe,out var token))continue;
            if(row.Recipe.ingredients[0].itemValue.HasMetadata(Marker))return true;
            if(GameManager.Instance?.World?.IsRemote()!=false)continue;
            var pending=Journal().FindPending(token);if(pending==null)continue;
            var root=XElement.Parse(Encoding.UTF8.GetString(pending.Before));
            if((string)root.Attribute("world")==GameManager.Instance.World.worldState.Guid&&(string)root.Attribute("location")==RebirthCookingStationLocator.Encode(station))return true;
        }
        return false;
    }
    internal static void TickClients()
    {
        if(Clients.Count==0)return;
        // Only presentation runners retire here; saved station/delta originals
        // and owner custody are not financial outcomes of this cleanup.
        Clients.RemoveAll(client=>!CurrentClient(client));
        foreach(var client in Clients.ToArray())
        {
            if(client.Waiting||UnityEngine.Time.realtimeSinceStartup<client.Next)continue;
            if(client.Terminal){client.Next=UnityEngine.Time.realtimeSinceStartup+.5f;FinishClient(client);continue;}
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;if(client.World.IsRemote()&&connection==null)continue;
            client.Waiting=true;client.Next=UnityEngine.Time.realtimeSinceStartup+1f;
            try
            {
            if(client.World.IsRemote())connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(client.Player),false);
            else GameManager.Instance.SaveLocalPlayerData();
            var hint=new ItemValue(1);if(client.Plan!=null)hint.SetMetadata("rebirth.cooking.cancellation.delta",client.Plan.Operation.ToString("N"));
            if(client.Legacy!=null)hint.SetMetadata("rebirth.cooking.cancellation.legacy",client.Legacy);
            if(client.Legacy!=null)hint.SetMetadata("rebirth.cooking.cancellation.legacySlot",client.LegacySlot);
            RebirthCookingSessionService.Request(client.Player,"cancelRemoteCookingBatch",client.Token,client.Creation,client.Location,item:hint,reply:response=>
            {
                client.Waiting=false;
                if(!Clients.Contains(client)||!CurrentClient(client))return;
                try
                {
                    if(response=="cancel-unused:"+client.Token&&client.Plan==null)
                    {Clients.Remove(client);return;} // positive saved no-cancellation outcome; queue/owner remain unchanged
                    bool done=response.StartsWith("cancelled:",StringComparison.Ordinal),retry=response.StartsWith("retry-cancel:",StringComparison.Ordinal);
                    int phase=0;string payload;
                    if(done)payload=response.Substring(10);else if(retry)payload=response.Substring(13);
                    else if(response.StartsWith("cancel:0:",StringComparison.Ordinal)||response.StartsWith("cancel:1:",StringComparison.Ordinal))
                    {phase=response[7]=='1'?1:0;payload=response.Substring(9);}else return;
                    if(payload.Length>6*1024*1024)return;
                    var plan=RebirthCookingOwnerDelta.Decode(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                    if(plan.World!=client.World.worldState.Guid||plan.Creation!=client.Creation||plan.Owner!=client.Owner||plan.Deaths<0||plan.Deaths>client.Deaths)return;
                    if(client.Plan!=null&&client.Plan.Operation!=plan.Operation)return;
                    if(plan.Deaths<client.Deaths)
                    {
                        // The authoritative terminal reply requires the old
                        // saved refund delta and saved station removal. A new
                        // life may retire metadata, never execute that old delta.
                        if(!done)return;client.Plan=plan;
                        if(RebirthCookingOwnerDelta.RetireHistoricalMetadata(client.Player,plan,()=>Clients.Contains(client)&&ReferenceEquals(client.Plan,plan)&&CurrentClient(client)))
                        {client.Terminal=true;FinishClient(client);}
                        return;
                    }
                    if(retry){if(RebirthCookingOwnerDelta.AbandonRejectedWithoutEffect(client.Player,plan))client.Plan=null;return;}
                    client.Plan=plan;
                    bool Current()=>Clients.Contains(client)&&ReferenceEquals(client.Plan,plan)&&CurrentClient(client);
                    if(!RebirthCookingOwnerDelta.Advance(client.Player,plan,done?2:phase,Current,()=>Current()))return;
                    if(done&&RebirthCookingOwnerDelta.Retire(client.Player,plan)){client.Terminal=true;FinishClient(client);}
                }
                catch(Exception error){Log.Warning("[Cooking] Cancellation owner receipt retained: "+error.GetType().Name);}
            });
            }
            catch(Exception error){client.Waiting=false;Log.Warning("[Cooking] Cancellation request retained: "+error.GetType().Name);}
        }
    }
    private static void FinishClient(Client client)
    {
        if(!client.Terminal||!CurrentClient(client))return;
        try
        {
            if(client.Project?.Invoke()!=true||!CurrentClient(client))return;
            Audit.GetValue(client.Player,_=>new Counts()).Completed++;Clients.Remove(client);
        }
        catch(Exception error){Log.Warning("[Cooking] Settled cancellation projection retained: "+error.GetType().Name);}
    }
    internal static ItemStack[] Refunds(Recipe recipe,int multiplier)
    {
        if(multiplier<1||recipe.ingredients==null||recipe.ingredients.Count<1||recipe.ingredients.Count>12)throw new InvalidDataException();
        var result=recipe.ingredients.Select(input=>
        {
            long count=(long)input.count*multiplier;if(count<1||count>int.MaxValue)throw new InvalidDataException();
            var value=input.itemValue.Clone();RebirthRecipeOutputs.ClearFrozen(value);value.Metadata?.Remove("rebirth.cooking.registeredOwner");
            if(value.Metadata!=null)foreach(string key in value.Metadata.Keys.Where(key=>key.StartsWith("rebirth.cooking.queue.",StringComparison.Ordinal)).ToArray())value.Metadata.Remove(key);
            return new ItemStack(value,(int)count);
        }).ToArray();
        return result;
    }
    internal static string Handle(EntityPlayer player,string batch,string creation,string location,ItemValue hint=null)
    {
        try
        {
            int enteredDeaths=player?.Died??-1;var enteredWorld=player?.world;
            var enteredNetwork=player==null?null:RebirthRemoteResourceNetworkScope.Capture(player);
            bool InvocationCurrent()=>enteredNetwork?.Current==true&&player.Died==enteredDeaths&&ReferenceEquals(player.world,enteredWorld)&&ReferenceEquals(GameManager.Instance?.World,enteredWorld);
            if(!InvocationCurrent()||!Guid.TryParseExact(batch,"N",out var token)||token==Guid.Empty||!RebirthCookingRemoteAdmissionBackend.Current(player,location,creation,out var station,out var owner)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return RebirthCookingRemoteAdmission.Pending;
            if(LegacyPublishing.Contains(token))return RebirthCookingRemoteAdmission.Pending;
            var journal=Journal();var completed=journal.FindApplied(token,creation).ToArray();if(completed.Length>1)throw new InvalidDataException();
            var record=completed.SingleOrDefault()??journal.FindPending(token);
            if(record==null)
            {
                var unused=journal.FindRejected(token,creation).ToArray();
                if(unused.Length>0)
                {
                    if(unused.Length!=1||!unused[0].Before.SequenceEqual(unused[0].After))return RebirthCookingRemoteAdmission.Pending;
                    var refused=XElement.Parse(Encoding.UTF8.GetString(unused[0].Before));
                    if((int?)refused.Attribute("legacyTagProtocol")!=1||(string)refused.Attribute("world")!=player.world.worldState.Guid||
                        (string)refused.Attribute("owner")!=identity.CanonicalId||(string)refused.Attribute("creation")!=creation||
                        (string)refused.Attribute("location")!=location||(string)refused.Attribute("token")!=batch||refused.Attribute("legacy")==null)return RebirthCookingRemoteAdmission.Pending;
                    if(hint!=null&&hint.TryGetMetadata("rebirth.cooking.cancellation.legacy",out string offeredLegacy)&&(string)refused.Attribute("legacy")!=offeredLegacy)return RebirthCookingRemoteAdmission.Pending;
                    if(hint!=null&&hint.HasMetadata("rebirth.cooking.cancellation.legacySlot")&&
                        (!hint.TryGetMetadata("rebirth.cooking.cancellation.legacySlot",out int offeredSlot)||(int?)refused.Attribute("legacySlot")!=offeredSlot))return RebirthCookingRemoteAdmission.Pending;
                    return "cancel-unused:"+batch;
                }
            }
            if(hint!=null&&hint.TryGetMetadata("rebirth.cooking.cancellation.legacy",out string legacy))
            {
                int slot=-1;
                if(hint.HasMetadata("rebirth.cooking.cancellation.legacySlot")&&(!hint.TryGetMetadata("rebirth.cooking.cancellation.legacySlot",out slot)||slot<0||slot>byte.MaxValue))return RebirthCookingRemoteAdmission.Pending;
                if(record==null)record=PrepareLegacy(player,station,identity,token,creation,location,legacy,slot);
                if(record==null)return RebirthCookingRemoteAdmission.Pending;
                var retainedLegacy=XElement.Parse(Encoding.UTF8.GetString(record.Before));
                if((string)retainedLegacy.Attribute("legacy")!=legacy||slot>=0&&(int?)retainedLegacy.Attribute("legacySlot")!=slot)return RebirthCookingRemoteAdmission.Pending;
            }
            if(record==null)
            {
                if(RebirthRemoteResourceSourceCustody.BlocksStation(station))return RebirthCookingRemoteAdmission.Pending;
                if(!RebirthCookingHiddenOutputRecovery.TryStationBirth(player,station,location,creation,out string birth)||!InvocationCurrent())return RebirthCookingRemoteAdmission.Pending;
                var row=station.Queue.SingleOrDefault(value=>Token(value?.Recipe,out var candidate)&&candidate==token);
                if(row==null||RebirthCookingHeat.Ready(row.Recipe)||RebirthCookingRemoteAdmissionBackend.Held(station))return RebirthCookingRemoteAdmission.Pending;
                if(!owner.Progression.Cooking.Tickets.TryGetValue(batch,out var ticket)||ticket.StationPosition!=location||ticket.Recipe!=row.Recipe.GetName()||ticket.Portions!=row.Multiplier||ticket.OutputCount!=row.Recipe.count)
                    return RebirthCookingRemoteAdmission.Pending;
                if(row.Recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.registeredOwner",out string registered)&&registered!=identity.CanonicalId)return RebirthCookingRemoteAdmission.Pending;
                string before=RebirthCookingRemoteAdmissionBackend.Queue(station.Queue);var paused=RebirthCookingRemoteAdmissionBackend.ReadQueue(before);
                foreach(var item in paused)if(Token(item.Recipe,out var candidate)&&candidate==token)item.Recipe.ingredients[0].itemValue.SetMetadata(Marker,1);
                var empty=RebirthCookingRemoteAdmissionBackend.ReadQueue(before);
                foreach(var item in empty)if(Token(item.Recipe,out var candidate)&&candidate==token){item.Recipe=null;item.Multiplier=0;}
                var root=new XElement("cookingCancellation",new XAttribute("version",1),new XAttribute("world",player.world.worldState.Guid),new XAttribute("owner",identity.CanonicalId),
                    new XAttribute("creation",creation),new XAttribute("location",location),new XAttribute("token",batch),new XAttribute("birth",birth),new XAttribute("block",station is RebirthCamperStationState?"mobile":station.block?.GetBlockName()??""),
                    new XElement("before",before),new XElement("paused",RebirthCookingRemoteAdmissionBackend.Queue(paused)),new XElement("empty",RebirthCookingRemoteAdmissionBackend.Queue(empty)),
                    new XElement("refund",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(
                        RebirthCookingRemoteAdmissionBackend.PaidInputs(token,player.world.worldState.Guid,creation,location,identity.CanonicalId)??Refunds(row.Recipe,row.Multiplier)))));
                byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>4*1024*1024)throw new InvalidDataException();
                var operation=Guid.NewGuid();journal.AssertAvailable(token,operation);record=journal.Prepare(operation,token,creation,0,bytes,bytes);
                if(!InvocationCurrent()||!RebirthCookingRemoteAdmissionBackend.Current(player,location,creation,out var still,out _)||!ReferenceEquals(still,station)||
                    !CancellationBirthMatches(player,station,location,root)||RebirthCookingRemoteAdmissionBackend.Queue(station.Queue)!=before)return RebirthCookingRemoteAdmission.Pending;
                station.Queue=paused;station.lastTickTime=0;station.ResetTickTime();station.setModified();
            }
            var original=XElement.Parse(Encoding.UTF8.GetString(record.Before));
            if(record.OwnerCreation!=creation||(string)original.Attribute("owner")!=identity.CanonicalId||(string)original.Attribute("world")!=player.world.worldState.Guid||
                (string)original.Attribute("location")!=location||(string)original.Attribute("token")!=batch||!CancellationBirthMatches(player,station,location,original))return RebirthCookingRemoteAdmission.Pending;
            var deltaJournal=Deltas();var applied=deltaJournal.FindApplied(record.Operation,creation).ToArray();if(applied.Length>1)throw new InvalidDataException();
            if(hint!=null&&hint.TryGetMetadata("rebirth.cooking.cancellation.delta",out string expectedText))
            {
                if(!Guid.TryParseExact(expectedText,"N",out var expected))return RebirthCookingRemoteAdmission.Pending;
                var retained=deltaJournal.Read(expected);
                if(retained==null||retained.Vehicle!=record.Operation||retained.OwnerCreation!=creation)return RebirthCookingRemoteAdmission.Pending;
                if(retained.State==RebirthCamperTransferJournal.Phase.Rejected)
                {RebirthCookingOwnerDelta.ObserveRejected(identity,retained);return "retry-cancel:"+Convert.ToBase64String(retained.Before);}
            }
            var delta=applied.SingleOrDefault()??deltaJournal.FindPending(record.Operation);
            if(record.State==RebirthCamperTransferJournal.Phase.Applied)
            {
                if(delta?.State==RebirthCamperTransferJournal.Phase.Applied)
                {
                    var terminal=RebirthCookingOwnerDelta.Decode(Encoding.UTF8.GetString(delta.Before));
                    if(terminal.Overflow!=null&&!RebirthCookingOverflowOriginal.Terminal(record,player,identity.CanonicalId,creation,terminal.Overflow,terminal.Deaths,terminal.OverflowOwnerEntity))return RebirthCookingRemoteAdmission.Pending;
                }
                return delta?.State==RebirthCamperTransferJournal.Phase.Applied?"cancelled:"+Convert.ToBase64String(delta.Before):RebirthCookingRemoteAdmission.Pending;
            }
            string pausedImage=original.Element("paused").Value,emptyImage=original.Element("empty").Value;
            string live=RebirthCookingRemoteAdmissionBackend.Queue(station.Queue);
            var canonical=RebirthCookingRemoteAdmissionBackend.ReadQueue(pausedImage).Select(row=>row.Recipe).First(recipe=>Token(recipe,out var candidate)&&candidate==token);
            bool Current(string image)=>InvocationCurrent()&&RebirthCookingRemoteAdmissionBackend.Current(player,location,creation,out var current,out _)&&ReferenceEquals(current,station)&&
                CancellationBirthMatches(player,station,location,original)&&(station is RebirthCamperStationState||station.block?.GetBlockName()==(string)original.Attribute("block"))&&RebirthCookingRemoteAdmissionBackend.Queue(station.Queue)==image;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                // Only the fresh legacy publisher may tag an unlabelled row.
                // Cold/status replay must never select a later identical job.
                if(original.Attribute("legacy")!=null&&live==original.Element("before").Value)
                {
                    // Positive versioned Prepared phase plus exact live Before
                    // proves no cancellation credit/removal was admitted. Do
                    // not tag a later matching row; retire only this old intent.
                    if((int?)original.Attribute("legacyTagProtocol")!=1||!Current(live)||applied.Length!=0||delta!=null||deltaJournal.FindRejected(record.Operation,creation).Any())return RebirthCookingRemoteAdmission.Pending;
                    journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
                    var unused=journal.Read(record.Operation);
                    return unused?.State==RebirthCamperTransferJournal.Phase.Rejected&&unused.Operation==record.Operation&&unused.Vehicle==record.Vehicle&&unused.OwnerCreation==record.OwnerCreation&&
                        unused.Before.SequenceEqual(record.Before)&&unused.After.SequenceEqual(record.After)?"cancel-unused:"+batch:RebirthCookingRemoteAdmission.Pending;
                }
                if(live==original.Element("before").Value&&Current(live))
                {station.Queue=RebirthCookingRemoteAdmissionBackend.ReadQueue(pausedImage);station.lastTickTime=0;station.ResetTickTime();station.setModified();live=pausedImage;}
                if(live!=pausedImage||!RebirthCookingRemoteAdmissionBackend.Published(record.Operation,player,station,pausedImage,()=>Current(pausedImage),canonical))return RebirthCookingRemoteAdmission.Pending;
                record=journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            }
            if(live!=pausedImage&&live!=emptyImage)return RebirthCookingRemoteAdmission.Pending;
            RebirthCookingOwnerDelta.Plan plan;
            if(delta==null)
            {
                foreach(var rejected in deltaJournal.FindRejected(record.Operation,creation))
                    if(!RebirthCookingOwnerDelta.ObserveRejected(identity,rejected))return "retry-cancel:"+Convert.ToBase64String(rejected.Before);
                var outputs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Element("refund").Value));
                if(!RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out var bag,out var belt,out var locks))return RebirthCookingRemoteAdmission.Pending;
                if(!RebirthRecyclerOwnerDelivery.PlanWithOverflow(bag,belt,locks,null,player.inventory.Length,outputs,out var afterBag,out var afterBelt,out var overflow))return RebirthCookingRemoteAdmission.Pending;
                plan=new RebirthCookingOwnerDelta.Plan{Operation=Guid.NewGuid(),World=player.world.worldState.Guid,Creation=creation,Owner=identity.CanonicalId,Deaths=player.Died,
                    SourceHash=RebirthCookingRemoteAdmission.SourceHash(bag,belt,locks,player.inventory.Length),BeforeBag=bag,AfterBag=afterBag,BeforeBelt=belt,AfterBelt=afterBelt,Overflow=overflow.Length==0?null:overflow,OverflowOwnerEntity=player.entityId};
                byte[] bytes=Encoding.UTF8.GetBytes(RebirthCookingOwnerDelta.Encode(plan));deltaJournal.AssertAvailable(record.Operation,plan.Operation);delta=deltaJournal.Prepare(plan.Operation,record.Operation,creation,0,bytes,bytes);
            }
            else plan=RebirthCookingOwnerDelta.Decode(Encoding.UTF8.GetString(delta.Before));
            if(plan.Owner!=identity.CanonicalId||plan.Creation!=creation||plan.World!=player.world.worldState.Guid||
                delta.State!=RebirthCamperTransferJournal.Phase.Applied&&plan.Deaths!=player.Died)return RebirthCookingRemoteAdmission.Pending;
            if(delta.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                if(RebirthCookingOwnerDelta.Saved(identity,plan,1))delta=deltaJournal.Advance(delta.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                else if(RebirthCookingOwnerDelta.TryReadUnspentSource(identity,plan,out var bag,out var belt,out var locks)&&
                    RebirthCookingRemoteAdmission.SourceHash(bag,belt,locks,player.inventory.Length)!=plan.SourceHash)
                {deltaJournal.Advance(delta.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);return "retry-cancel:"+Convert.ToBase64String(delta.Before);}
            }
            if(delta.State==RebirthCamperTransferJournal.Phase.Uncertain&&RebirthCookingOwnerDelta.Saved(identity,plan,2))
                delta=deltaJournal.Advance(delta.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            if(delta.State!=RebirthCamperTransferJournal.Phase.Applied)return "cancel:"+(delta.State==RebirthCamperTransferJournal.Phase.Uncertain?"1:":"0:")+Convert.ToBase64String(delta.Before);
            if(plan.Overflow!=null&&!RebirthCookingOverflowOriginal.Advance(record,player,identity.CanonicalId,creation,plan.Overflow,plan.Deaths,plan.OverflowOwnerEntity,()=>Current(pausedImage)||Current(emptyImage)))return RebirthCookingRemoteAdmission.Pending;
            if(live==pausedImage){if(!Current(pausedImage))return RebirthCookingRemoteAdmission.Pending;station.Queue=RebirthCookingRemoteAdmissionBackend.ReadQueue(emptyImage);station.lastTickTime=0;station.ResetTickTime();station.setModified();}
            if(!RebirthCookingRemoteAdmissionBackend.Published(record.Operation,player,station,emptyImage,()=>Current(emptyImage),canonical))return RebirthCookingRemoteAdmission.Pending;
            journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return "cancelled:"+Convert.ToBase64String(delta.Before);
        }
        catch(Exception error){Log.Warning("[Cooking] Canonical cancellation retained: "+error.GetType().Name);return RebirthCookingRemoteAdmission.Pending;}
    }
}
