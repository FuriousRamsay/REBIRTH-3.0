using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Explicit, owner-confirmed cancel of a P7 craft operation, including the refund of a SAVED ingredient debit.
// Scope: a debit is refundable only while the native queue insertion was never entered (phase debit-attempted: queue-attempted is
// saved before set_Queue, and RecoverUnreturned only returns a row to debit-attempted when the loaded station is the exact pre-image).
// Past that point the ingredient lives in the station's native queue and no refund exists (see the P7 doc, "Addendum 17").
// Crash model: refund-attempted is written first (write-ahead); every later step is derived from the saved row plus the live and saved bag,
// so a repeated call with the same operation id converges (credit once, bind once, mark refunded once). Nothing resumes by itself:
// refund-attempted is not dispatchable (D-25), only another owner-confirmed call finishes it, and a new craft needs a new assignment id.
internal static partial class RebirthNpcStationQueueOriginal
{
    // Live worker for a refund: server, main thread, embodied and idle enough to own the bag write, same owner scope as the row.
    private static bool RefundWorker(RebirthNpcStableId id,XElement row,out EntityRebirthHumanoidNPC npc)
    {
        npc=null;var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!RebirthNpcWorkNavigationService.TryResolveNpc(id,out var live)||
            live is not EntityRebirthHumanoidNPC human||human.IsDead()||human.IsPreparedRestorationPending||human.bag?.ItemGrid==null||
            !ReferenceEquals(human.world,world)||!ReferenceEquals(world.GetEntity(human.entityId),human)||human.RebirthRuntimeState?.StableId!=id||
            RebirthNpcInteractionSessionService.IsInventoryAccessing(id)||RebirthNpcWorkNativeTransferOriginal.HasPendingExcept(id,(string)row.Attribute("operation"))||
            RemoteResourceTransactions.ActionBlocksNpcExceptStationQueue(id)||RebirthNpcMetabolismCustody.Blocks(id)||
            (string)row.Attribute("owner")!=RebirthStablePlayerIdentity.ComputeStorageKey(human.RebirthRuntimeState.OwnerId))return false;
        npc=human;return true;
    }
    // Bag-only comparison (belt/equipment legitimately change while the NPC waits). cleared(i) = unit i's slot is expected empty.
    private static bool BagIs(EntityRebirthHumanoidNPC npc,XElement row,Func<int,bool> cleared)
    {
        var expected=new XElement(row.Element("physical").Element("bag"));var units=Units(row);
        for(int i=0;i<units.Count;i++)
            if(cleared(i))expected.Elements().ElementAt(units[i].BagSlot).SetAttributeValue("frame",string.Empty);
        return XNode.DeepEquals(expected,NativePhysical(npc).Element("bag"));
    }
    // The refund is durable when every unit has its explicit bag binding and its exact native stack record in the SAVED state.
    private static bool SavedRefund(RebirthNpcStableId id,XElement row)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var inventory)||inventory.NativeStacks==null)return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse).SingleOrDefault(x=>x.Name=="humanEquipment");
            var records=inventory.NativeStacks.Write().Elements().Select(x=>RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null).Where(x=>x!=null).ToArray();
            foreach(var unit in Units(row))
            {
                var binding=equipment?.Element("bindings")?.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==unit.BagSlot);
                if((string)binding?.Attribute("id")!=unit.Credit)return false;
                var stack=RebirthHumanEquipment.Decode(unit.Frame);
                var record=records.SingleOrDefault(x=>x.StackId.ToString("N")==unit.Credit);
                if(record==null||record.Count!=stack.count||record.Payload!=RebirthNativeItemCodec.Encode(stack.itemValue))return false;
            }
            return true;
        }
        catch{return false;}
    }
    // Empties the slots this call refilled so an unsaved partial refund can never be persisted by an autosave.
    private static void RollbackRefund(EntityRebirthHumanoidNPC npc,RebirthNpcStableId id,XElement row,List<Unit> units,int upTo)
    {
        try
        {
            for(int j=0;j<upTo&&j<units.Count;j++)
            {
                var slot=npc.bag.ItemGrid[units[j].BagSlot];
                if(slot==null||slot.IsEmpty()||RebirthHumanEquipment.Frame(slot)!=units[j].Frame)continue;
                DebitSlot(npc,units[j].BagSlot,()=>RefundWorker(id,row,out var live)&&ReferenceEquals(live,npc));
            }
        }
        catch{}
    }
    private static bool MarkTerminal(RebirthNpcStableId id,XElement row,string phase,string note)
    {
        var next=new XElement(row);next.SetAttributeValue("phase",phase);next.SetAttributeValue("note",note);
        ReleaseWatch(Guid.ParseExact((string)row.Attribute("operation"),"N"));
        return Save(id,row,next);
    }
    // Credits the N debited stacks back (exact saved frames), re-binds their credit ids, saves the NPC, then marks the row refunded.
    // The original npcStationQueueDebit receipts stay untouched; once the row is `refunded` the custody provider no longer treats those
    // stacks as managed (TryReadManagedDebit answers Guid.Empty), so they are ordinary bound bag stacks again.
    private static bool ContinueRefund(RebirthNpcStableId id,XElement row,out string detail)
    {
        detail="The refund is settling; the original is retained.";
        if(!RefundWorker(id,row,out var npc)){detail="The worker must be present and idle for its ingredients to be returned.";return false;}
        var units=Units(row);int credited=0;
        var equipment=RebirthHumanEquipment.Read(npc);
        if(equipment.Element("swap")!=null||equipment.Element("bookMove")!=null){detail="An NPC inventory move is in progress.";return false;}
        var bindings=equipment.Element("bindings");
        if(bindings==null){bindings=new XElement("bindings");equipment.Add(bindings);}
        // A bag slot bound to anything but this unit's credit id means the slot is not the one the debit emptied.
        foreach(var unit in units)
        {
            var bound=bindings.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==unit.BagSlot);
            if(bound!=null&&(string)bound.Attribute("id")!=unit.Credit){detail="A returned ingredient slot is bound to a different stack.";return false;}
            if(bindings.Elements("binding").Any(x=>(string)x.Attribute("id")==unit.Credit&&!ReferenceEquals(x,bound))){detail="A returned ingredient id is bound twice.";return false;}
        }
        if(BagIs(npc,row,i=>true))
        {
            for(int k=0;k<units.Count;k++)
            {
                int from=k;
                if(!CreditSlot(npc,units[k].BagSlot,RebirthHumanEquipment.Decode(units[k].Frame),()=>RefundWorker(id,row,out var live)&&
                    ReferenceEquals(live,npc)&&BagIs(npc,row,i=>i>=from)))
                {RollbackRefund(npc,id,row,units,k+1);detail="The native bag credit did not return; it is not repeated blindly.";return false;}
                credited=k+1;
            }
        }
        if(!BagIs(npc,row,i=>false)){RollbackRefund(npc,id,row,units,credited);detail="The worker bag differs from the saved pre-debit image.";return false;}
        foreach(var unit in units)
            if(!bindings.Elements("binding").Any(x=>(string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==unit.BagSlot))
                bindings.Add(new XElement("binding",new XAttribute("area","bag"),new XAttribute("index",unit.BagSlot),new XAttribute("id",unit.Credit)));
        try{RebirthHumanEquipment.Save(npc,equipment);}
        catch{RollbackRefund(npc,id,row,units,credited);detail="The refunded ingredients could not be saved; nothing was kept.";return false;}
        if(!SavedRefund(id,row)){detail="The refund is not yet readable from the saved NPC state.";return false;}
        var done=new XElement(row);done.SetAttributeValue("phase","refunded");done.SetAttributeValue("refundTicks",DateTime.UtcNow.Ticks);
        if(!Save(id,row,done)){detail="The refund completion is settling.";return false;}
        ReleaseWatch(Guid.ParseExact((string)row.Attribute("operation"),"N"));
        if(row.Attribute("wPrior")!=null)FinishWithdrawalCleanup(done);
        detail="The ingredients were returned to the worker bag exactly.";return true;
    }
    // Explicit owner-confirmed cancel/abandon entry for Desk/P6. Main thread. Idempotent by assignment (operation) id; returns true when the
    // operation is (now) released with nothing owed, false with the exact reason otherwise. Never frees an ingredient that is already in the
    // station queue or a finished output already in the NPC bag.
    internal static bool TryCancelOwnerConfirmed(EntityPlayer owner,RebirthNpcStableId npc,ulong assignmentId,bool ownerConfirmed,out string detail)
    {
        detail="The craft cannot be cancelled.";
        try
        {
            if(!ownerConfirmed){detail="The owner has not confirmed the cancellation.";return false;}
            var op=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId);
            if(!ThreadManager.IsMainThread()||!TryRead(npc,op,out var row)){detail="No saved craft original exists for this assignment.";return false;}
            if(owner==null||!RebirthOriginalActionScope.Matches(owner,(string)row.Attribute("owner"),(string)row.Attribute("creation"))){detail="Only the owner of this craft can cancel it.";return false;}
            switch((string)row.Attribute("phase"))
            {
                case "refunded":detail="The ingredients were already returned.";return true;
                case "unresolved":detail="The craft was already released.";return true;
                case "refund-attempted":return ContinueRefund(npc,row,out detail);
                case "prepared":
                    // Nothing was consumed: the debit starts only after debit-attempted is saved.
                    if(!MarkTerminal(npc,row,"unresolved","Cancelled by the owner before any ingredient was debited.")){detail="The cancellation is settling.";return false;}
                    detail="Cancelled; no ingredient had been consumed.";return true;
                case "debit-attempted":
                {
                    if(!SavedDebit(npc,row))
                    {
                        // Unsaved debit: only releasable from the intact pre-image (saved inventory and live bag), else keep the original.
                        if(!RefundWorker(npc,row,out var worker)||!BagIs(worker,row,i=>false)||
                            !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(npc,out var before)||before.NativeStacks==null||
                            before.Revision!=(uint)row.Attribute("revision")||!XNode.DeepEquals(before.NativeStacks.Write(),row.Element("inventoryBefore").Elements().Single()))
                        {detail="The unsaved debit is not in its intact pre-image; the original is retained.";return false;}
                        if(!MarkTerminal(npc,row,"unresolved","Cancelled by the owner; the debit had not been saved.")){detail="The cancellation is settling.";return false;}
                        detail="Cancelled; no ingredient had been consumed.";return true;
                    }
                    // Saved debit, queue insertion never entered: the station must still be the exact pre-image and unstamped.
                    var station=SavedStation(row,out _);
                    if(station==null||!FullStation(station,row,"before")||
                        station.GetChunk()?.ChunkCustomData?.dict==null||station.GetChunk().ChunkCustomData.dict.ContainsKey(StampKey(row)))
                    {detail="The station is not in its pre-queue image; the debit is retained.";return false;}
                    if(!RefundWorker(npc,row,out _)){detail="The worker must be present and idle for its ingredients to be returned.";return false;}
                    var attempted=new XElement(row);attempted.SetAttributeValue("phase","refund-attempted");attempted.SetAttributeValue("refundTicks",DateTime.UtcNow.Ticks);
                    if(!Save(npc,row,attempted)){detail="The refund write-ahead is settling.";return false;}
                    return ContinueRefund(npc,attempted,out detail);
                }
                case "pickup-attempted":
                    if(!TryAbandonUncreditedPickup(npc,assignmentId,out detail))return false;
                    if(!TryRead(npc,op,out row))return false;
                    goto case "output-attributed";
                case "output-attributed":
                    // The finished output stays in the station for the players; no pickup, outcome or award will exist.
                    if(!MarkTerminal(npc,row,"unresolved","Cancelled by the owner; the finished output stays in the station.")){detail="The cancellation is settling.";return false;}
                    detail="Cancelled; the finished output stays in the station.";return true;
                case "pickup-credited":case "pickup-settled":case "completed":
                    detail="The finished output is already in the worker bag.";return false;
                case "queue-attempted":case "queue-published":case "cooking":
                case "withdraw-attempted":case "withdraw-captured":case "withdraw-frozen":case "withdraw-entered":case "withdrawn":case "withdraw-published":
                    // The ingredient is in the native station queue: withdraw that one entry (guarded set_Queue, exact images), then refund.
                    return ContinueWithdraw(owner,npc,row,out detail);
                default:
                    detail="The ingredient was already cooked into a finished output in the station; there is nothing left to withdraw.";return false;
            }
        }
        catch{return false;}
    }
    // Assignments of this NPC whose refund was written ahead but not finished (Desk may offer "complete refund"; nothing resumes by itself).
    internal static ulong[] PendingRefunds(RebirthNpcStableId npc)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return Array.Empty<ulong>();
            return (person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .Where(x=>x.Name==Element&&IsCancelPhase((string)x.Attribute("phase"))&&(string)x.Attribute("phase")!="refunded").Select(x=>(ulong)x.Attribute("assignment")).ToArray();
        }
        catch{return Array.Empty<ulong>();}
    }
}
