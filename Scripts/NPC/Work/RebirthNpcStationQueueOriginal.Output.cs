using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Output attribution and pickup for the NPC native queue original (v2 rows).
// Attribution never infers ownership from an item count: admission proved an empty
// Output/CraftCompleteList pre-image and a single NPC-owned queue row, so exactly one
// output stack plus exactly one NPC-entity receipt row can only be that job. Anything
// else becomes "unresolved" (no pickup, no award). Pickup is the only item movement and
// is enabled (source-complete; the single profiler batch validates it).
internal static partial class RebirthNpcStationQueueOriginal
{
    // Source gate. Every step is saved before and after, credit-first, with idempotent crash recovery (see the P7 doc).
    internal static readonly bool PickupQualified=true;
    // Predicate for UI (Desk Craft): finished-output pickup is implemented and enabled in this build.
    internal static bool PickupAvailable=>PickupQualified;
    internal sealed class PickupReceipt
    {
        internal RebirthNpcStableId Npc;internal ulong AssignmentId;internal Guid Operation,CreditId;internal int BagSlot;
        internal string Frame,ItemName,OutputProof,PickupProof;internal long CompletedUtcTicks;
    }
    // Finished-output receipt (queue admission never yields one): true only for a completed v2 row whose output attribution and
    // pickup are both positively saved, whose NPC bag credit reads back, and whose produced-item outcome is committed.
    internal static bool TryReadPickupReceipt(RebirthNpcStableId npc,ulong assignmentId,out PickupReceipt receipt)
    {
        receipt=null;
        try
        {
            var op=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId);
            if(!TryRead(npc,op,out var row)||!V2(row)||(string)row.Attribute("phase")!="completed"||
                ((string)row.Attribute("outputProof"))?.Length!=64||((string)row.Attribute("pickupProof"))?.Length!=64||
                !CreditRecorded(row)||!RebirthNpcWorkOutcomeService.HasCommittedLiterature(assignmentId,npc)||
                !Guid.TryParseExact((string)row.Attribute("pickupCredit"),"N",out var credit))return false;
            string frame=(string)row.Attribute("outFrame");
            receipt=new PickupReceipt{Npc=npc,AssignmentId=assignmentId,Operation=op,CreditId=credit,BagSlot=(int)row.Attribute("pickupBag"),
                Frame=frame,ItemName=RebirthHumanEquipment.Decode(frame).itemValue.ItemClass.GetItemName(),
                OutputProof=(string)row.Attribute("outputProof"),PickupProof=(string)row.Attribute("pickupProof"),CompletedUtcTicks=(long)row.Attribute("completedTicks")};
            return true;
        }
        catch{receipt=null;return false;}
    }
    // Shared by every chunk snapshot request of this original; call inside lock(chunk).
    private static bool ChunkBusy(Chunk chunk)=>chunk.IsLocked||chunk.InProgressSaving||chunk.InProgressUnloading||chunk.NeedsDecoration||
        chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting;
    private sealed class TerminalWatch{internal RebirthStationSnapshotEvidence.Request Request;internal World World;internal string Digest;internal long TouchedTicks;}
    private const long WatchIdleTicks=TimeSpan.TicksPerSecond*120,ObservedTimeoutTicks=TimeSpan.TicksPerMinute*10;
    // Releases this assignment's station-publication watch (a global slot, max 4). Safe to call at any time, from any settle/cancel path.
    internal static void ReleaseWatch(RebirthNpcStableId npc,ulong assignmentId)=>ReleaseWatch(RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId));
    internal static void ReleaseWatch(Guid operation)
    {if(terminalWatches.TryGetValue(operation,out var w)){try{w.Request.Dispose();}catch{}terminalWatches.Remove(operation);}}
    // Watches not touched by their own job for WatchIdleTicks are leaked (job cancelled/never ticked again); runs from every job tick.
    private static void ReapWatches()
    {
        if(terminalWatches.Count==0)return;long now=DateTime.UtcNow.Ticks;
        foreach(var key in terminalWatches.Where(x=>now-x.Value.TouchedTicks>WatchIdleTicks).Select(x=>x.Key).ToArray())ReleaseWatch(key);
    }
    private static readonly Dictionary<Guid,TerminalWatch> terminalWatches=new Dictionary<Guid,TerminalWatch>();
    // True when the row can never act again and holds nothing: P6's cancellation may treat the assignment as safe to remove.
    internal static bool IsReleasedTerminal(RebirthNpcStableId npc,ulong assignmentId)
    {
        if(!TryRead(npc,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId),out var row))return false;
        string phase=(string)row.Attribute("phase");
        return phase=="refunded"||V2(row)&&(phase=="unresolved"||phase=="output-attributed"&&!PickupQualified);
    }
    // Terminal for the executor (stops retry, releases session/reservation); the saved row stays readable.
    private static RebirthNpcWorkMutationResult Terminal(string detail)=>new RebirthNpcWorkMutationResult{
        Disposition=RebirthNpcWorkExecutionDisposition.Failed,FailureCategory=RebirthNpcWorkFailureCategory.PersistenceRecovery,Detail=detail};
    private static bool V2(XElement row)=>(string)row.Attribute("v2")=="1";
    internal static bool IsOutputPhase(string phase)=>phase=="cooking"||phase=="output-observed"||phase=="output-attributed"||
        phase=="pickup-attempted"||phase=="pickup-credited"||phase=="pickup-settled"||phase=="unresolved";
    // Cooking and unresolved must not freeze the station (it has to tick) or the NPC bag.
    private static bool Holds(XElement row)
    {
        switch((string)row.Attribute("phase"))
        {
            case "completed":case "cooking":case "unresolved":return false;
            // A finished refund releases the station and the NPC bag; refund-attempted and every withdraw-* phase keep holding.
            case "refunded":return false;
            // output-attributed does not hold: until the NPC arrives and saves pickup-attempted the player keeps the station.
            case "output-attributed":return false;
            case "pickup-attempted":case "pickup-credited":case "pickup-settled":return true;
            default:return true;
        }
    }
    private static RebirthNpcWorkMutationResult Finish(RebirthNpcConcreteWorkContext c,RebirthNpcWorkAssignment a,XElement row)=>
        V2(row)?AdvanceOutput(c,a,row):FinishSavedAdmission(a,row);
    private static RebirthNpcWorkMutationResult Unresolved(RebirthNpcWorkAssignment a,XElement row,string note)
    {
        var next=new XElement(row);next.SetAttributeValue("phase","unresolved");next.SetAttributeValue("note",note);
        if(!Save(a.NpcId,row,next))return Hold("Unresolved output state is settling; it is retried.");
        var op=Guid.ParseExact((string)row.Attribute("operation"),"N");
        if(terminalWatches.TryGetValue(op,out var own)){own.Request.Dispose();terminalWatches.Remove(op);}
        return Terminal(note+" No output is attributed or collected and no practice is awarded.");
    }
    private static byte[] ReceiptBytes(CraftCompleteData data)
    {
        using(var stream=new System.IO.MemoryStream())
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);data.Write(writer);writer.Flush();return stream.ToArray();}
    }
    private static bool Terminal(TileEntityWorkstation station,out RebirthStationTerminalSnapshot terminal)=>
        RebirthStationTerminalSnapshot.TryCapture(station,out terminal);
    private static bool TerminalIs(TileEntityWorkstation station,string digest)=>
        Terminal(station,out var terminal)&&terminal.Digest()==digest;
    private static bool OutputCell(TileEntityWorkstation station,XElement row,out int slot,out ItemStack stack)
    {
        slot=-1;stack=null;var output=station.Output;if(output==null)return false;
        for(int i=0;i<output.Length;i++)
            if(output[i]!=null&&!output[i].IsEmpty()){if(slot>=0)return false;slot=i;}
        if(slot<0)return false;stack=output[slot];
        return stack.count==OutCount(row)&&stack.itemValue!=null&&stack.itemValue.type==(int)row.Attribute("outType")&&!stack.itemValue.HasModSlots&&
            !string.IsNullOrEmpty(RebirthHumanEquipment.Frame(stack));
    }
    private static bool AdmittedEntity(XElement row,out int entity)
    {
        entity=0;
        if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(row.Element("queueAfter").Value,out var queue))return false;
        var own=queue.Where(x=>x?.Recipe!=null).ToArray();if(own.Length!=1)return false;
        entity=own[0].StartingEntityId;return true;
    }
    private static TileEntityWorkstation SavedStation(XElement row,out World world)
    {
        world=GameManager.Instance?.World;
        if(world==null||world.IsRemote()||!ThreadManager.IsMainThread())return null;
        return world.GetTileEntity(Position(row)) as TileEntityWorkstation;
    }
    private static RebirthNpcWorkMutationResult AdvanceOutput(RebirthNpcConcreteWorkContext c,RebirthNpcWorkAssignment a,XElement row)
    {
        if(!BoundAssignment(a,row)||(string)row.Attribute("debitProof")!=DebitProof(row)||
            ((string)row.Attribute("leaseProof"))?.Length!=64||((string)row.Attribute("queueProof"))?.Length!=64)
            return Hold("Saved native admission does not match this exact assignment original.");
        switch((string)row.Attribute("phase"))
        {
            case "queue-published":
            {
                var next=new XElement(row);next.SetAttributeValue("phase","cooking");
                return Save(a.NpcId,row,next)?Pending("Queue admission saved; the native station is cooking."):Hold("Cooking phase publication is settling.");
            }
            case "cooking":return ObserveCooking(a,row);
            case "output-observed":
                // Safe to give up: nothing has moved. The output stays in the station for the player, no outcome or award exists,
                // and the spent ingredient is the only loss, exactly as for any other unresolved state.
                if(((long?)row.Attribute("observedTicks")??DateTime.UtcNow.Ticks)+ObservedTimeoutTicks<DateTime.UtcNow.Ticks)
                    return Unresolved(a,row,"Saved publication of the finished output did not resolve in time.");
                return PublishTerminal(a,row,"terminalAfter","output-attributed","outputProof","Finished output observed; saved station publication is pending.");
            case "output-attributed":
                return PickupQualified?BeginPickup(c,a,row):
                    Terminal("Finished output is attributed to this assignment but its pickup is release-held; it stays in the station output.");
            case "pickup-attempted":return PickupCredit(c,a,row);
            case "pickup-credited":return PickupDebit(a,row);
            case "pickup-settled":case "completed":return FinishPickup(a,row);
            default:return Terminal((string)row.Attribute("note")??"Output attribution is unresolved.");
        }
    }
    private static RebirthNpcWorkMutationResult ObserveCooking(RebirthNpcWorkAssignment a,XElement row)
    {
        var station=SavedStation(row,out var world);
        if(station==null)return Pending("Station is not loaded; cooking is observed when it returns.");
        if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out string incarnation)||incarnation!=(string)row.Attribute("incarnation"))
            return Unresolved(a,row,"The station incarnation changed while the job was cooking.");
        if(!AdmittedEntity(row,out int entity)||!Terminal(station,out var terminal))return Hold("Cooking cannot be observed exactly.");
        string recipe=(string)row.Attribute("recipe");
        var active=(station.Queue??Array.Empty<RecipeQueueItem>()).Where(x=>x?.Recipe!=null&&x.Multiplier>0).ToArray();
        if(terminal.Digest()==(string)row.Attribute("terminalBefore"))
        {
            if(active.Length==1&&active[0].Recipe.GetName()==recipe&&active[0].StartingEntityId==entity&&active[0].RepairItem==null)
                return Pending("Native station is cooking the admitted recipe.");
            return Unresolved(a,row,"The admitted queue row left the station without a finished output.");
        }
        var receipts=station.CraftCompleteList;
        if(active.Length==0&&receipts?.Count==1&&OutputCell(station,row,out int slot,out var stack)&&
            receipts[0].CraftedItemStack?.itemValue!=null&&receipts[0].CraftedItemStack.count==OutCount(row)&&
            receipts[0].CraftedItemStack.itemValue.type==stack.itemValue.type)
        {
            var expected=new CraftCompleteData(entity,receipts[0].CraftedItemStack.Clone(),recipe,string.Empty,
                CraftingManager.GetRecipe(recipe).craftExpGain,1);
            if(ReceiptBytes(expected).SequenceEqual(ReceiptBytes(receipts[0])))
            {
                var next=new XElement(row);next.SetAttributeValue("phase","output-observed");next.SetAttributeValue("observedTicks",DateTime.UtcNow.Ticks);next.SetAttributeValue("outSlot",slot);
                next.SetAttributeValue("outFrame",RebirthHumanEquipment.Frame(stack));next.SetAttributeValue("terminalAfter",terminal.Digest());
                return Save(a.NpcId,row,next)?Pending("Exact finished output attributed to the NPC receipt; saved publication is pending."):
                    Hold("Output attribution is settling.");
            }
        }
        return Unresolved(a,row,"The station output or receipt rows do not equal exactly one finished job from this assignment.");
    }
    private static RebirthNpcWorkMutationResult PublishTerminal(RebirthNpcWorkAssignment a,XElement row,string digestAttribute,string nextPhase,string proofAttribute,string pending)
    {
        var station=SavedStation(row,out var world);
        if(station==null||!RebirthRemoteResourceBlockHost.Ready(world,Position(row),out var chunk,out var provider,out var providerCurrent))
            return Pending("Station chunk is not ready for saved publication.");
        string digest=(string)row.Attribute(digestAttribute);var op=Guid.ParseExact((string)row.Attribute("operation"),"N");
        bool CurrentImage()=>providerCurrent()&&ReferenceEquals(world.GetTileEntity(Position(row)),station)&&TerminalIs(station,digest)&&
            (digestAttribute!="terminalAfter"||OutputCell(station,row,out int s,out var held)&&s==(int)row.Attribute("outSlot")&&RebirthHumanEquipment.Frame(held)==(string)row.Attribute("outFrame"));
        if(!CurrentImage())return Unresolved(a,row,"The station terminal image changed before it was saved.");
        // Watches are keyed by operation; only this operation's own entry is ever disposed.
        if(terminalWatches.TryGetValue(op,out var mine)&&(!ReferenceEquals(mine.World,world)||mine.Digest!=digest||mine.Request.Observe?.Invoke()!=true))
        {mine.Request.Dispose();terminalWatches.Remove(op);mine=null;}
        if(mine!=null)
        {
            mine.TouchedTicks=DateTime.UtcNow.Ticks;
            if(!RebirthStationSnapshotEvidence.TryGetPublished(mine.Request,out var publication))return Pending(pending);
            var next=new XElement(row);next.SetAttributeValue("phase",nextPhase);next.SetAttributeValue(proofAttribute,publication.PayloadDigest);
            if(!Save(a.NpcId,row,next))return Hold("Terminal publication proof is settling.");
            mine.Request.Dispose();terminalWatches.Remove(op);return Pending(pending);
        }
        if(!RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queue)||!Terminal(station,out var wanted))return Hold("Station terminal cannot be captured exactly.");
        var request=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,CurrentImage,station,
            (inputs,q)=>queue.MatchesSerializedQueue(q),(outputs,receipts)=>wanted.MatchesSerializedTerminal(outputs,receipts));
        if(request==null)return Pending(pending);
        terminalWatches[op]=new TerminalWatch{Request=request,World=world,Digest=digest,TouchedTicks=DateTime.UtcNow.Ticks};
        lock(chunk)
        {
            if(!CurrentImage()){request.Dispose();terminalWatches.Remove(op);return Pending(pending);}
            // Same state guard as AdvancedFarmingHarvestPublication: never snapshot a chunk that is saving/unloading/decorating/lighting; retry next tick.
            if(ChunkBusy(chunk))
            {request.Dispose();terminalWatches.Remove(op);return Pending(pending);}
            chunk.InProgressSaving=true;
        }
        try{provider.m_RegionFileManager.SaveChunkSnapshot(chunk,true);}
        finally{lock(chunk)chunk.InProgressSaving=false;}
        return Pending(pending);
    }
    // ---- Pickup (release-held) -------------------------------------------------
    private static bool PickupCurrent(RebirthNpcWorkAssignment a,XElement row,out EntityRebirthHumanoidNPC npc,out TileEntityWorkstation station)
    {
        npc=null;station=SavedStation(row,out var world);
        var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(station==null||intent==null||!RebirthNpcRecipeQueueWork.Allows(intent)||!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||
            intent.PrimaryId!=a.AssignmentId||intent.Actor is not EntityRebirthHumanoidNPC actual||actual.IsDead()||actual.IsPreparedRestorationPending||
            actual.Hand==null||actual.Hand.IsBusy||actual.GetAttackTarget()!=null||actual.bag?.ItemGrid==null||actual.inventory?.ItemGrid?.items==null||
            actual.equipment?.ItemGrid?.items==null||(actual.position-a.TargetPosition).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared||
            RebirthNpcInteractionSessionService.IsInventoryAccessing(a.NpcId)||
            !string.Equals(actual.RebirthRuntimeState?.OwnerId,a.ActorId,StringComparison.OrdinalIgnoreCase)||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out string incarnation)||incarnation!=(string)row.Attribute("incarnation"))return false;
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        if(owner==null||owner.IsDead()||owner.Died!=(int)row.Attribute("deaths")||
            !RebirthOriginalActionScope.Matches(owner,(string)row.Attribute("owner"),(string)row.Attribute("creation")))return false;
        npc=actual;return true;
    }
    // Bag only: belt/equipment legitimately change while the NPC waits, only the credit slot's bag must be the saved pre-image.
    private static string PhysicalDigest(EntityRebirthHumanoidNPC npc)=>
        RebirthRemoteResourceDurableJournal.Digest(NativePhysical(npc).Element("bag").ToString(SaveOptions.DisableFormatting));
    private static bool OutputHeld(TileEntityWorkstation station,XElement row)=>TerminalIs(station,(string)row.Attribute("terminalAfter"))&&
        OutputCell(station,row,out int slot,out var stack)&&slot==(int)row.Attribute("outSlot")&&RebirthHumanEquipment.Frame(stack)==(string)row.Attribute("outFrame");
    private static RebirthNpcWorkMutationResult BeginPickup(RebirthNpcConcreteWorkContext c,RebirthNpcWorkAssignment a,XElement row)
    {
        if(!PickupCurrent(a,row,out var npc,out var station))return Pending("The embodied NPC must be at its station to collect the finished output.");
        if(!OutputHeld(station,row))return Unresolved(a,row,"The attributed output was removed before the NPC collected it.");
        if(RebirthNpcWorkNativeTransferOriginal.HasPending(a.NpcId)||
            RemoteResourceTransactions.ActionBlocksNpc(a.NpcId)||RebirthNpcMetabolismCustody.Blocks(a.NpcId))return Pending("NPC inventory custody is busy.");
        var equipment=RebirthHumanEquipment.Read(npc);if(equipment.Element("swap")!=null||equipment.Element("bookMove")!=null)return Pending("NPC inventory move is in progress.");
        var locks=npc.bag.ItemGrid.SlotLocks;int bag=-1;
        for(int i=0;i<npc.bag.SlotCount&&bag<0;i++)
            if((npc.bag.ItemGrid[i]==null||npc.bag.ItemGrid[i].IsEmpty())&&locks!=null&&i<locks.Length&&!locks[i]&&
                !(equipment.Element("bindings")?.Elements("binding").Any(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==i)==true))bag=i;
        if(bag<0)return Pending("The NPC bag has no free slot for the finished output.");
        var next=new XElement(row);next.SetAttributeValue("phase","pickup-attempted");next.SetAttributeValue("pickupBag",bag);
        next.SetAttributeValue("pickupCredit",Guid.NewGuid().ToString("N"));next.SetAttributeValue("pickupPre",PhysicalDigest(npc));
        return Save(a.NpcId,row,next)?Pending("Pickup original saved; the NPC bag credit follows."):Hold("Pickup original is settling.");
    }
    private static string CreditRecord(XElement row)=>(string)row.Attribute("pickupCredit")+"|"+(string)row.Attribute("pickupBag")+"|"+(string)row.Attribute("outFrame");
    private static bool CreditRecorded(XElement row)=>((string)row.Attribute("pickupCredit"))?.Length==32&&(string)row.Attribute("creditSaved")==CreditRecord(row);
    private static bool SavedPickupCredit(XElement row)
    {
        try
        {
            if(!RebirthNpcStableId.TryParse((string)row.Attribute("npc"),out var id)||!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var inventory)||inventory.NativeStacks==null)return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            var binding=equipment?.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&
                (int)x.Attribute("index")==(int)row.Attribute("pickupBag"));
            if((string)binding?.Attribute("id")!=(string)row.Attribute("pickupCredit"))return false;
            var stack=RebirthHumanEquipment.Decode((string)row.Attribute("outFrame"));
            var credited=inventory.NativeStacks.Write().Elements().Select(x=>RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null)
                .SingleOrDefault(x=>x?.StackId.ToString("N")==(string)row.Attribute("pickupCredit"));
            return credited!=null&&credited.Count==stack.count&&credited.Payload==RebirthNativeItemCodec.Encode(stack.itemValue);
        }
        catch{return false;}
    }
    // True when nothing of the pickup credit exists in the SAVED NPC state: no native stack and no bag binding carries the credit id.
    // Any read doubt answers "exists" so a release is never granted on a guess.
    private static bool CreditAbsentSaved(XElement row)
    {
        try
        {
            string credit=(string)row.Attribute("pickupCredit");
            if(credit?.Length!=32||!RebirthNpcStableId.TryParse((string)row.Attribute("npc"),out var id)||
                !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var inventory)||inventory.NativeStacks==null)return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            if(equipment?.Element("bindings")?.Elements("binding").Any(x=>(string)x.Attribute("id")==credit)==true)return false;
            return !inventory.NativeStacks.Write().Elements().Any(x=>RebirthNpcNativeStackRecord.TryRead(x,out var item)&&item!=null&&item.StackId.ToString("N")==credit);
        }
        catch{return false;}
    }
    // Explicit release of a pickup that was saved (pickup-attempted) but whose bag credit never reached the saved NPC state, for a worker
    // that cannot be re-embodied (the row otherwise holds the station and the NPC bag until it is). The caller (cancel/Desk) must be acting on
    // the owner's confirmed request. Provable no-op for items: the output is still in the station, no credit exists saved or live. The row
    // returns to output-attributed (not holding); a later pickup restarts with a new credit id. Never releases once a credit exists.
    internal static bool TryAbandonUncreditedPickup(RebirthNpcStableId npc,ulong assignmentId,out string detail)
    {
        detail="The pickup cannot be released.";
        try
        {
            if(!ThreadManager.IsMainThread()||!TryRead(npc,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId),out var row)||!V2(row)||
                (string)row.Attribute("phase")!="pickup-attempted"){detail="No uncredited pickup is pending for this assignment.";return false;}
            if(row.Attribute("creditSaved")!=null||!CreditAbsentSaved(row)){detail="The finished output was already credited to the worker.";return false;}
            if(RebirthNpcWorkNavigationService.TryResolveNpc(npc,out var live))
            {
                // An embodied worker may hold the credit in memory (not yet saved): its bag slot must still be the saved pre-image.
                if(live is not EntityRebirthHumanoidNPC human||human.bag?.ItemGrid==null||PhysicalDigest(human)!=(string)row.Attribute("pickupPre"))
                {detail="The worker is active; the pickup finishes on its own.";return false;}
            }
            var next=new XElement(row);next.SetAttributeValue("phase","output-attributed");
            next.Attribute("pickupBag")?.Remove();next.Attribute("pickupCredit")?.Remove();next.Attribute("pickupPre")?.Remove();
            if(!Save(npc,row,next)){detail="The pickup release is settling.";return false;}
            detail="The pickup was released; the finished output stays in the station.";return true;
        }
        catch{return false;}
    }
    private static RebirthNpcWorkMutationResult PickupCredit(RebirthNpcConcreteWorkContext c,RebirthNpcWorkAssignment a,XElement row)
    {
        if(!PickupCurrent(a,row,out var npc,out var station))return Pending("The embodied NPC must be at its station to finish the pickup.");
        // The source cell is still untouched in this phase (debit follows the saved credit).
        int bag=(int)row.Attribute("pickupBag");var stack=RebirthHumanEquipment.Decode((string)row.Attribute("outFrame"));
        var current=npc.bag.ItemGrid[bag];
        if(!OutputHeld(station,row))
        {
            // Credit already saved: the debit phase finishes the station side idempotently. Nothing credited yet: nothing moved, give up.
            if(SavedPickupCredit(row))
            {
                var done=new XElement(row);done.SetAttributeValue("phase","pickup-credited");done.SetAttributeValue("creditSaved",CreditRecord(row));
                return Save(a.NpcId,row,done)?Pending("Credit was already saved; the station side is finished."):Hold("Pickup credit phase is settling.");
            }
            if((current==null||current.IsEmpty())&&PhysicalDigest(npc)==(string)row.Attribute("pickupPre"))
                return Unresolved(a,row,"The attributed output was removed before the credit was made.");
            return Hold("Attributed output changed while an unsaved NPC credit exists; the original is retained.");
        }
        if(current==null||current.IsEmpty())
        {
            if(PhysicalDigest(npc)!=(string)row.Attribute("pickupPre"))return Hold("NPC inventory differs from the saved pickup pre-image.");
            if(!CreditSlot(npc,bag,stack,()=>PickupCurrent(a,row,out var live,out var exact)&&ReferenceEquals(live,npc)&&ReferenceEquals(exact,station)&&OutputHeld(station,row)))
                return Hold("Native bag credit did not return; it is not repeated.");
        }
        else if(RebirthHumanEquipment.Frame(current)!=(string)row.Attribute("outFrame"))return Hold("The pickup bag slot holds a different item.");
        var equipment=RebirthHumanEquipment.Read(npc);var bindings=equipment.Element("bindings");
        if(bindings==null){bindings=new XElement("bindings");equipment.Add(bindings);}
        string credit=(string)row.Attribute("pickupCredit");
        var existing=bindings.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==bag);
        if(existing!=null&&(string)existing.Attribute("id")!=credit||bindings.Elements("binding").Any(x=>!ReferenceEquals(x,existing)&&(string)x.Attribute("id")==credit))
            return Hold("The pickup bag slot is already bound to another native stack.");
        if(existing==null)bindings.Add(new XElement("binding",new XAttribute("area","bag"),new XAttribute("index",bag),new XAttribute("id",credit)));
        RebirthHumanEquipment.Save(npc,equipment);
        if(!SavedPickupCredit(row))return Hold("NPC bag credit is awaiting saved readback.");
        var next=new XElement(row);next.SetAttributeValue("phase","pickup-credited");next.SetAttributeValue("creditSaved",CreditRecord(row));
        return Save(a.NpcId,row,next)?Pending("Exact output credited to the NPC bag; the station cell debit follows."):Hold("Pickup credit phase is settling.");
    }
    private static RebirthNpcWorkMutationResult PickupDebit(RebirthNpcWorkAssignment a,XElement row)
    {
        // The saved creditSaved record (written with the pickup-credited phase) proves the bag credit; the live bag binding may
        // legitimately move afterwards (inventory screen, eating, equipping) and must not freeze the station.
        if(!CreditRecorded(row))return Hold("Saved NPC bag credit record is unavailable; the station cell is not cleared.");
        var station=SavedStation(row,out var world);
        if(station==null)return Pending("Station is not loaded; the debit is completed when it returns.");
        string before=(string)row.Attribute("terminalBefore");
        if(!TerminalIs(station,before))
        {
            // Exact attributed cell and its single receipt row. The Output array goes through the guarded setter under a
            // one-use operation-bound permit; the receipt list has no guard and is trimmed right after (same frame).
            if(!station.Output.All(x=>x==null||x.IsEmpty()))
            {
                if(!OutputHeld(station,row)||station.CraftCompleteList?.Count!=1)return Hold("Station output differs from the saved credited pickup original.");
                var next=ItemStack.Clone(station.Output);next[(int)row.Attribute("outSlot")]=ItemStack.Empty.Clone();
                if(!SetOutput(station,next,()=>ReferenceEquals(SavedStation(row,out _),station)&&OutputHeld(station,row)&&CreditRecorded(row)))
                    return Hold("Station output setter did not return; the credited original is retained.");
            }
            if(station.CraftCompleteList?.Count==1&&station.Output.All(x=>x==null||x.IsEmpty())){station.CraftCompleteList.RemoveAt(0);station.setModified();}
            if(!TerminalIs(station,before))return Hold("Station output debit did not reach the expected empty image.");
        }
        return PublishTerminal(a,row,"terminalBefore","pickup-settled","pickupProof","Station debit made; saved station publication is pending.");
    }
    private sealed class OutputPermit{internal TileEntityWorkstation Station;internal ItemStack[] Value;internal bool Used,Returned,Failed;internal Func<bool> Current;}
    [ThreadStatic]private static OutputPermit outputPermit;
    // Read-only for the two set_Output guard prefixes: exact station, exact array reference, unused, still current.
    internal static bool AllowsOutputSetter(TileEntityWorkstation station,ItemStack[] value)
    {
        var p=outputPermit;
        return p!=null&&!p.Used&&!p.Failed&&ReferenceEquals(p.Station,station)&&ReferenceEquals(p.Value,value)&&p.Current?.Invoke()==true;
    }
    internal static void ObserveOutputSetter(TileEntityWorkstation station,ItemStack[] value,bool ran,Exception error)
    {
        var p=outputPermit;if(p==null||!ReferenceEquals(p.Station,station)||!ReferenceEquals(p.Value,value))return;
        if(error!=null)p.Failed=true;else if(ran){p.Used=true;p.Returned=true;}
    }
    private static bool SetOutput(TileEntityWorkstation station,ItemStack[] value,Func<bool> current)
    {
        if(outputPermit!=null||!current())return false;
        var p=new OutputPermit{Station=station,Value=value,Current=current};outputPermit=p;
        try{station.Output=value;return p.Returned&&!p.Failed;}finally{outputPermit=null;}
    }
    private static RebirthNpcWorkMutationResult FinishPickup(RebirthNpcWorkAssignment a,XElement row)
    {
        if(!CreditRecorded(row)||((string)row.Attribute("outputProof"))?.Length!=64||((string)row.Attribute("pickupProof"))?.Length!=64)
            return Hold("Pickup receipts are not positively saved.");
        if(row.Attribute("completedTicks")==null)
        {var next=new XElement(row);next.SetAttributeValue("completedTicks",DateTime.UtcNow.Ticks);if(!Save(a.NpcId,row,next))return Hold("Output outcome timestamp is settling.");row=next;}
        // Order (no cycle): the row is saved `completed` FIRST so the outcome's emit (B13 bridge -> TryReadPickupReceipt) can read it, then the
        // outcome is committed idempotently. A crash between the two leaves a non-Completed assignment, so this method re-runs and commits it.
        if((string)row.Attribute("phase")!="completed")
        {var next=new XElement(row);next.SetAttributeValue("phase","completed");if(!Save(a.NpcId,row,next))return Hold("Terminal metadata publication is settling.");row=next;}
        if(!RebirthNpcWorkOutcomeService.TryRecordCommitted(Outcome(a,row)))return Hold("Collected output awaits saved outcome publication.");
        // The owner-award evidence must be durably journaled before the assignment may finish (a lost supply would leave the intent unresolvable).
        if(!SupplyOwnerMetric(a,row))return Hold("Owner award evidence is awaiting the journal.");
        // Practice is queued, never awaited: the row keeps `awarded` absent until the receipt-keyed award succeeds (here or in RetryPendingAwards),
        // so a transient progression failure can neither hold the item/assignment nor forfeit the award, and a replay is a no-op by receipt.
        if((string)row.Attribute("awarded")!="1"&&AwardCraft(a.NpcId,row))
        {var awarded=new XElement(row);awarded.SetAttributeValue("awarded","1");if(Save(a.NpcId,row,awarded))row=awarded;}
        var outcome=Outcome(a,row);
        return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,
            Detail=outcome.Detail,Consumed=outcome.Consumed,Produced=outcome.Produced};
    }
    // Action-derived effort for the owner (Management/Leadership) award: the admitted queue item's time per craft (already NPC-skill and
    // single Management adjusted, as saved in queueAfter) x produced units. P1 treats it as idempotent and immutable after the first fill.
    // Returns false only while the journal is Unavailable (retry); a row whose saved craft time cannot be derived degrades to ONE discrete
    // receipt-backed unit (the pickup is still a saved physical completion) so the intent never waits forever.
    private static bool SupplyOwnerMetric(RebirthNpcWorkAssignment a,XElement row)
    {
        RebirthNpcWorkOutcomeRecord outcome=Outcome(a,row);
        float effort=0f;
        try
        {
            if(RebirthRemoteResourcePlacedStationQueue.TryDecode(row.Element("queueAfter")?.Value,out var queue))
            {
                var own=queue.Where(x=>x?.Recipe!=null).ToArray();
                if(own.Length==1)effort=own[0].OneItemCraftTime*OutCount(row);
            }
        }
        catch{effort=0f;}
        var result=float.IsNaN(effort)||float.IsInfinity(effort)||effort<=0f
            ?RebirthP1SharedWorkAward.SupplyOwnerDiscrete(outcome)
            :RebirthP1SharedWorkAward.SupplyOwnerMetric(outcome,effort);
        return result!=RebirthP1AwardQueueResult.Unavailable;
    }
    private static bool AwardCraft(RebirthNpcStableId npc,XElement row)
    {
        string recipe=(string)row.Attribute("recipe");string skill=RebirthServiceCraftSkillService.ClassifyRecipe(recipe);
        if(string.IsNullOrEmpty(skill))return true;
        float raw=RebirthServiceCraftSkillService.GetCraftAward(skill,OutCount(row));
        if(raw<=0f)return true;
        // Acknowledged/Journaled: P1 owns the once-only delivery by receipt. Invalid: never valid later, so mark done (logged).
        // Unavailable: leave `awarded` absent; RetryPendingAwards calls again.
        var result=RebirthP1SharedWorkAward.QueueAward(npc,skill,"npc:craft-original:"+(string)row.Attribute("operation"),raw,"p7:craft:npc:"+recipe,
            "saved finished-output attribution, guarded station debit and native NPC bag credit");
        if(result==RebirthP1AwardQueueResult.Invalid)
            Log.Warning("[REBIRTH NPC Work] Craft practice award for operation "+(string)row.Attribute("operation")+" was rejected as invalid and is skipped.");
        return result!=RebirthP1AwardQueueResult.Unavailable;
    }
    private static int OutCount(XElement row)=>(int?)row.Attribute("outCount")??1;
    private static float nextAwardDrain;
    private static readonly Dictionary<string,int> awardFailures=new Dictionary<string,int>();
    // Durable pending-award drain keyed by the exact operation receipt (the saved completed row without `awarded`). Throttled, bounded,
    // main thread. Called from every P7 job tick; P6 may also call it from a world tick. The count only throttles logging.
    internal static void RetryPendingAwards()
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||UnityEngine.Time.unscaledTime<nextAwardDrain)return;
        nextAwardDrain=UnityEngine.Time.unscaledTime+30f;int budget=4;
        try
        {
            foreach(var person in RebirthNpcAggregatePersistenceStore.SnapshotViews())
            {
                if(budget<=0)break;
                var id=person.Identity.StableNpcId;
                foreach(var text in person.UnknownOptionalElements == null
                    ? (IEnumerable<string>)Array.Empty<string>() : person.UnknownOptionalElements)
                {
                    if(budget<=0||!text.Contains(Element))continue;
                    var seen=XElement.Parse(text);
                    if(seen.Name!=Element||!V2(seen)||(string)seen.Attribute("phase")!="completed"||(string)seen.Attribute("awarded")=="1"||
                        !Guid.TryParseExact((string)seen.Attribute("operation"),"N",out var op)||!TryRead(id,op,out var row)||
                        !ulong.TryParse((string)row.Attribute("assignment"),out var assignment)||!TryReadPickupReceipt(id,assignment,out _))continue;
                    budget--;
                    if(AwardCraft(id,row))
                    {var next=new XElement(row);next.SetAttributeValue("awarded","1");Save(id,row,next);awardFailures.Remove((string)row.Attribute("operation"));}
                    else
                    {
                        string key=(string)row.Attribute("operation");awardFailures.TryGetValue(key,out int count);awardFailures[key]=++count;
                        if(count==1||count%20==0)Log.Warning("[REBIRTH NPC Work] Craft practice award for operation "+key+" is still pending (attempt "+count+"); it stays queued on the saved row.");
                    }
                }
            }
        }
        catch{}
    }
}
