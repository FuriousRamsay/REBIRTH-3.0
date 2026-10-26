using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Guarded withdrawal of the ONE admitted native queue entry, then the existing saved-debit refund (Refund.cs).
// Native facts (Mono.Cecil on Assembly-CSharp.dll, see the P7 doc Addendum 19): TileEntityWorkstation has NO remove/cancel method. The vanilla cancel is
// XUiC_RecipeStack.HandleOnPress (UI): it refunds recipe.ingredients[i].count*recipeCount to the LOCAL PLAYER inventory (overflow dropped), clears the stack,
// and the window group later writes the rebuilt array through XUiM_Workstation.SetRecipeQueueItems -> TileEntityWorkstation.set_Queue. A cancelled slot is
// the native empty shape of ResetCraftingQueue/cycleRecipeQueue (Recipe null, Multiplier 0, times 0, IsCrafting false, Quality 0, StartingEntityId -1).
// So the equivalent, and only, native queue write is the same guarded set_Queue the admission already uses, with that slot replaced by the empty shape.
// Phases (all saved before they act; the row Binding excludes the w* attributes). The station image on DISK is always exactly one of two PROVEN images:
//   withdraw-attempted  write-ahead; holds the station (stationHolds), which freezes UpdateTick/HandleRecipeQueue/HandleFuel through the placed-station guards
//   withdraw-captured   exact frozen full-station image wFrom, the exact image wTo (same station, queue minus the entry) and the queue text wQueueTo
//   withdraw-frozen     wFrom is PUBLISHED: a forced chunk snapshot read back positively (wFromProof). Only now may the setter be entered
//   withdraw-entered    saved before the one-use set_Queue permit; never replayed in the same session
//   withdrawn           in-memory readback proved wTo; its chunk publication is pending
//   withdraw-published  wTo is PUBLISHED (wToProof): the disk shows the queue without the entry; only now the refund-attempted write-ahead and ContinueRefund
// Crash per phase: attempted -> recapture; captured -> needs wFrom, publishes it; frozen -> enters; entered -> wTo = persisted (continue), wFrom in a LATER
// session = never persisted (back to frozen, redone once), neither in a later session but the admitted entry still intact = back to attempted (recapture, safe:
// no refund exists before wToProof), anything else or same session = held; withdrawn -> publish wTo (a later session that finds wFrom goes back to frozen);
// published -> refund write-ahead; refund phases as in Refund.cs. The owner may abandon (keep the craft) up to withdraw-frozen with a STATION-ONLY check.
internal static partial class RebirthNpcStationQueueOriginal
{
    private sealed class WithdrawEnv{internal EntityRebirthHumanoidNPC Npc;internal TileEntityWorkstation Station;internal Chunk Chunk;}
    internal static bool IsCancelPhase(string phase)=>phase=="refund-attempted"||phase=="refunded"||phase=="withdraw-attempted"||phase=="withdraw-captured"||
        phase=="withdraw-frozen"||phase=="withdraw-entered"||phase=="withdrawn"||phase=="withdraw-published";
    private static readonly string[] WithdrawAttributes={"wPrior","wFrom","wTo","wQueueTo","wSession","wTicks","wFromProof","wToProof"};
    private static bool FullStationAttr(TileEntityWorkstation station,XElement row,string attribute)=>
        RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var bytes)&&
        Convert.ToBase64String(bytes)==(string)row.Attribute(attribute);
    // The native empty slot (identical to TileEntityWorkstation.ResetCraftingQueue and the tail of cycleRecipeQueue).
    private static RecipeQueueItem NativeEmptySlot()=>new RecipeQueueItem{Recipe=null,Multiplier=0,CraftingTimeLeft=0f,OneItemCraftTime=0f,IsCrafting=false,Quality=0,StartingEntityId=-1};
    // The queue as the native wire writes it (RecipeQueueItem.Write -> Recipe.Write): exactly what survives a TE read from disk or the network.
    // Runtime-only Recipe fields (UseIngredientModifier, craftingTier, tags ...) are reset by Recipe.Read and must never decide a withdrawal.
    private static string QueueWire(RecipeQueueItem[] rows)=>RebirthCookingRemoteAdmissionBackend.Queue(rows);
    // The live queue must equal the admitted queue image except for the one entry's remaining time (0 <= left <= admitted time); every other slot, the
    // recipe wire (ingredients included), multiplier, quality, entity id and repair state are compared through the native wire (reload-stable).
    // `to` = that same queue with the entry replaced by the native empty slot.
    private static bool AdmittedImage(TileEntityWorkstation station,XElement row,out RecipeQueueItem[] to,out string toText)
    {
        to=null;toText=null;
        try
        {
            if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(row.Element("queueAfter").Value,out var expected)||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(row.Element("queueAfter").Value,out var removed)||
                station?.Queue==null||station.Queue.Length!=expected.Length)return false;
            int slot=-1;
            for(int i=0;i<expected.Length;i++)if(expected[i].Recipe!=null){if(slot>=0)return false;slot=i;}
            if(slot<0)return false;
            var live=station.Queue[slot];
            if(live==null||live.Recipe==null||float.IsNaN(live.CraftingTimeLeft)||live.CraftingTimeLeft<0f||live.CraftingTimeLeft>expected[slot].CraftingTimeLeft)return false;
            expected[slot].CraftingTimeLeft=live.CraftingTimeLeft;
            if(QueueWire(expected)!=QueueWire(station.Queue))return false;
            removed[slot]=NativeEmptySlot();
            to=removed;toText=RebirthRemoteResourcePlacedStationQueue.Encode(removed);return true;
        }
        catch{to=null;toText=null;return false;}
    }
    // Server main thread, owner scope, the same station incarnation and block TYPE (damage is tolerated), nobody using it, no OTHER custodian
    // (this operation's own hold is expected and checked once below), chunk provider ready. With workerRequired: also the embodied idle worker whose bag still
    // has the N debited slots empty and the saved debit intact. Abandoning (workerRequired=false) needs only the station.
    private static bool WithdrawEnvOf(EntityPlayer owner,RebirthNpcStableId id,XElement row,bool workerRequired,out WithdrawEnv env,out string detail)
    {
        env=null;detail="The station or the worker is not in the state this withdrawal requires.";
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||owner==null||
                !RebirthOriginalActionScope.Matches(owner,(string)row.Attribute("owner"),(string)row.Attribute("creation"))){detail="Only the owner of this craft can withdraw it.";return false;}
            EntityRebirthHumanoidNPC npc=null;
            if(workerRequired&&!RefundWorker(id,row,out npc)){detail="The worker must be present and idle for its ingredients to be returned.";return false;}
            var station=world.GetTileEntity(Position(row)) as TileEntityWorkstation;
            var admittedBlock=new BlockValue{rawData=(uint)row.Attribute("blockRaw")};
            if(station==null||station is RebirthCamperStationState||station.IsUserAccessing()||world.GetBlock(Position(row)).type!=admittedBlock.type||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out string incarnation)||incarnation!=(string)row.Attribute("incarnation")||
                !RebirthRemoteResourceBlockHost.Ready(world,Position(row),out var chunk,out _,out _)||chunk?.ChunkCustomData?.dict==null||
                RebirthRemoteResourceSourceCustody.BlocksStationExceptNpcQueue(station)||RemoteResourceTransactions.ActionBlocksNative(station))
            {detail="The station is in use, changed, or held by another operation.";return false;}
            if(!Index()||stationHolds.TryGetValue(PositionKey(Position(row)),out var held)&&held!=id){detail="Another operation holds this station.";return false;}
            if(workerRequired&&(!SavedDebit(id,row)||!BagIs(npc,row,i=>true))){detail="The saved debit or the worker bag no longer matches the paid image.";return false;}
            env=new WithdrawEnv{Npc=npc,Station=station,Chunk=chunk};return true;
        }
        catch{env=null;return false;}
    }
    private static bool SameWithdrawSession(XElement row,World world)=>world!=null&&(string)row.Attribute("wSession")==WorldSession(world);
    private static XElement Withdraw(XElement row,string phase)
    {var next=new XElement(row);next.SetAttributeValue("phase",phase);return next;}
    private static XElement WithoutWithdrawAttributes(XElement row,params string[] keep)
    {var next=new XElement(row);foreach(string name in WithdrawAttributes)if(!keep.Contains(name))next.Attribute(name)?.Remove();return next;}
    // Forces a chunk snapshot of the HELD station and reads it back (the same mechanism as PublishQueue/PublishTerminal). Returns true only with the
    // positive saved readback (proof = payload digest). `tag` separates the two images of one operation; `image` is the exact in-memory identity that
    // must hold for the whole watch. A pending result is retried by the next owner-confirmed call (the watch is kept in terminalWatches).
    private static bool PublishHeld(Guid op,TileEntityWorkstation station,string tag,Func<bool> image,out string proof,out string detail)
    {
        proof=null;detail="The station publication is settling; the original is retained.";
        try
        {
            var world=GameManager.Instance?.World;if(world==null||station==null)return false;
            var pos=station.ToWorldPos();
            if(!RebirthRemoteResourceBlockHost.Ready(world,pos,out var chunk,out var provider,out var providerCurrent))return false;
            bool Image()=>providerCurrent()&&ReferenceEquals(world.GetTileEntity(pos),station)&&image();
            if(!Image())return false;
            string key="withdraw:"+tag;
            if(terminalWatches.TryGetValue(op,out var mine)&&(!ReferenceEquals(mine.World,world)||mine.Digest!=key||mine.Request.Observe?.Invoke()!=true))
            {ReleaseWatch(op);mine=null;}
            if(mine==null)
            {
                if(!RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queue)||!RebirthStationTerminalSnapshot.TryCapture(station,out var wanted))return false;
                var request=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,Image,station,
                    (inputs,q)=>queue.MatchesSerializedQueue(q),(outputs,receipts)=>wanted.MatchesSerializedTerminal(outputs,receipts));
                if(request==null)return false;
                mine=new TerminalWatch{Request=request,World=world,Digest=key,TouchedTicks=DateTime.UtcNow.Ticks};terminalWatches[op]=mine;
                lock(chunk)
                {
                    if(!Image()||ChunkBusy(chunk)){ReleaseWatch(op);return false;}
                    chunk.InProgressSaving=true;
                }
                try{provider.m_RegionFileManager.SaveChunkSnapshot(chunk,true);}
                finally{lock(chunk)chunk.InProgressSaving=false;}
            }
            mine.TouchedTicks=DateTime.UtcNow.Ticks;
            if(!RebirthStationSnapshotEvidence.TryGetPublished(mine.Request,out var publication))return false;
            proof=publication.PayloadDigest;ReleaseWatch(op);
            if(proof?.Length!=64){proof=null;return false;}
            return true;
        }
        catch{proof=null;return false;}
    }
    // Owner-confirmed cancel of an entry that is already in the native queue. Idempotent by operation id; every call re-derives its step from the saved row
    // and the loaded images. Returns true only when the ingredients are back in the worker bag (row refunded).
    private static bool ContinueWithdraw(EntityPlayer owner,RebirthNpcStableId id,XElement row,out string detail)
    {
        detail="The withdrawal is settling; the original is retained.";
        var op=Guid.ParseExact((string)row.Attribute("operation"),"N");bool justEntered=false;
        for(int guard=0;guard<12;guard++)
        {
            string phase=(string)row.Attribute("phase");
            if(!WithdrawEnvOf(owner,id,row,true,out var env,out detail))return false;
            var station=env.Station;var snap=row;
            switch(phase)
            {
                case "queue-attempted":case "queue-published":case "cooking":
                {
                    if(!AdmittedImage(station,row,out _,out _)||!TerminalIs(station,(string)row.Attribute("terminalBefore")))
                    {detail="The station no longer holds exactly the admitted, unfinished entry (it may be finished or changed); nothing was withdrawn.";return false;}
                    if(phase=="queue-attempted"&&!(env.Chunk.ChunkCustomData.dict.TryGetValue(StampKey(row),out var stamp)&&stamp?.data!=null&&stamp.data.SequenceEqual(Stamp(row))))
                    {detail="The entered queue insertion has no returned-call witness yet; it is resolved by the job before it can be withdrawn.";return false;}
                    if(phase=="cooking"&&(BlocksStation(station)||RebirthRemoteResourcePlacedStationCustody.Blocks(station)))
                    {detail="The cooking station is held by another operation.";return false;}
                    // The placed-station guards are installed (or provably cannot be) BEFORE the write-ahead: an uninstallable guard must never leave a held station.
                    try{RebirthRemoteResourcePlacedStationGuards.Install();}catch{detail="The station guards are not available; nothing was withdrawn.";return false;}
                    if(!RebirthRemoteResourcePlacedStationGuards.IsInstalled){detail="The station guards are not available; nothing was withdrawn.";return false;}
                    var next=Withdraw(row,"withdraw-attempted");next.SetAttributeValue("wPrior",phase);
                    if(!Save(id,row,next)){detail="The withdrawal write-ahead is settling.";return false;}
                    // The write-ahead row now holds the station (Save indexes it); in-flight watches of the old phases are released.
                    if(queueWatch!=null&&watchedOperation==op){queueWatch.Dispose();queueWatch=null;}
                    ReleaseWatch(op);row=next;continue;
                }
                case "withdraw-attempted":
                {
                    if(!AdmittedImage(station,row,out var to,out var toText)||!TerminalIs(station,(string)row.Attribute("terminalBefore"))||
                        !RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var fromBody)||
                        !RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,to,station.IsBurning,out var toBody))
                    {detail="The frozen station is not the exact admitted entry; the withdrawal can be abandoned by the owner.";return false;}
                    var next=Withdraw(row,"withdraw-captured");next.SetAttributeValue("wFrom",Convert.ToBase64String(fromBody));
                    next.SetAttributeValue("wTo",Convert.ToBase64String(toBody));next.SetAttributeValue("wQueueTo",toText);
                    if(!Save(id,row,next)){detail="The withdrawal image capture is settling.";return false;}
                    row=next;continue;
                }
                case "withdraw-captured":
                {
                    // The frozen image must be on disk (positive readback) before anything may change it, so a crash can never leave an unproven station.
                    Func<bool> frozen=()=>FullStationAttr(station,snap,"wFrom")&&TerminalIs(station,(string)snap.Attribute("terminalBefore"));
                    if(!frozen()){detail="The station changed after its withdrawal image was captured; the withdrawal is held (the owner can abandon it).";return false;}
                    if(!PublishHeld(op,station,"from",frozen,out string proof,out detail))return false;
                    var next=Withdraw(row,"withdraw-frozen");next.SetAttributeValue("wFromProof",proof);
                    if(!Save(id,row,next)){detail="The frozen station proof is settling.";return false;}
                    row=next;continue;
                }
                case "withdraw-frozen":
                {
                    if(!FullStationAttr(station,row,"wFrom")||!TerminalIs(station,(string)row.Attribute("terminalBefore"))){detail="The station changed after its frozen image was proven; the withdrawal is held (the owner can abandon it).";return false;}
                    // The guards must exist before the setter write-ahead is saved.
                    try{RebirthRemoteResourcePlacedStationGuards.Install();}catch{detail="The station guards are not available; the original is retained.";return false;}
                    if(!RebirthRemoteResourcePlacedStationGuards.IsInstalled){detail="The station guards are not available; the original is retained.";return false;}
                    var next=Withdraw(row,"withdraw-entered");next.SetAttributeValue("wSession",WorldSession(GameManager.Instance.World));
                    if(!Save(id,row,next)){detail="The withdrawal setter write-ahead is settling.";return false;}
                    row=next;justEntered=true;continue;
                }
                case "withdraw-entered":
                {
                    string queueTo=(string)row.Attribute("wQueueTo");
                    if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(queueTo,out var target)){detail="The withdrawn queue image is unreadable.";return false;}
                    if(FullStationAttr(station,row,"wTo")&&TerminalIs(station,(string)row.Attribute("terminalBefore"))&&QueueWire(station.Queue)==QueueWire(target))
                    {
                        // Exact readback: the station is the captured image minus the admitted entry (in memory; its publication follows).
                        var next=Withdraw(row,"withdrawn");
                        if(!Save(id,row,next)){detail="The withdrawal readback is settling.";return false;}
                        row=next;continue;
                    }
                    if(!FullStationAttr(station,row,"wFrom"))
                    {
                        // Neither proven image. A later session cannot have a live setter call; if the admitted entry is still intact the whole withdrawal is
                        // simply redone from a fresh capture (no refund exists before wToProof, so this cannot duplicate anything).
                        if(!justEntered&&!SameWithdrawSession(row,GameManager.Instance.World)&&AdmittedImage(station,row,out _,out _)&&
                            TerminalIs(station,(string)row.Attribute("terminalBefore")))
                        {
                            var redo=Withdraw(WithoutWithdrawAttributes(row,"wPrior"),"withdraw-attempted");
                            if(!Save(id,row,redo)){detail="The withdrawal recovery is settling.";return false;}
                            row=redo;continue;
                        }
                        detail="The station is neither the captured image nor its withdrawn image; the original is retained.";return false;
                    }
                    if(!justEntered)
                    {
                        if(SameWithdrawSession(row,GameManager.Instance.World)){detail="An entered queue withdrawal is not replayed within the same session.";return false;}
                        // A later session: the in-memory setter call cannot be alive and the loaded station is still the exact proven frozen image.
                        var back=Withdraw(row,"withdraw-frozen");back.Attribute("wSession")?.Remove();
                        if(!Save(id,row,back)){detail="The withdrawal recovery is settling.";return false;}
                        row=back;continue;
                    }
                    justEntered=false;
                    var entered=row;
                    bool ok=Set(station,"set_Queue",target,()=>WithdrawEnvOf(owner,id,entered,true,out var live,out _)&&ReferenceEquals(live.Station,station)&&
                        FullStationAttr(station,entered,"wFrom"),()=>station.Queue=target,out bool used);
                    if(!ok&&!used&&FullStationAttr(station,entered,"wFrom"))
                    {
                        // The one-use permit was never consumed: the native setter provably did not run, so the write-ahead can be taken back in this session.
                        var back=Withdraw(entered,"withdraw-frozen");back.Attribute("wSession")?.Remove();
                        if(Save(id,entered,back)){detail="The station setter was not permitted; nothing changed and the withdrawal can be retried.";return false;}
                    }
                    continue;
                }
                case "withdrawn":
                {
                    Func<bool> emptied=()=>FullStationAttr(station,snap,"wTo")&&TerminalIs(station,(string)snap.Attribute("terminalBefore"));
                    if(emptied())
                    {
                        if(!PublishHeld(op,station,"to",emptied,out string proof,out detail))return false;
                        var next=Withdraw(row,"withdraw-published");next.SetAttributeValue("wToProof",proof);
                        if(!Save(id,row,next)){detail="The withdrawn station proof is settling.";return false;}
                        row=next;continue;
                    }
                    if(FullStationAttr(station,row,"wFrom")&&!SameWithdrawSession(row,GameManager.Instance.World))
                    {
                        // The unpublished emptied queue did not survive the restart: the disk still has the proven frozen image. No refund exists yet.
                        var back=Withdraw(row,"withdraw-frozen");back.Attribute("wSession")?.Remove();
                        if(!Save(id,row,back)){detail="The withdrawal recovery is settling.";return false;}
                        row=back;continue;
                    }
                    detail="The station is not the withdrawn image; the original is retained.";return false;
                }
                case "withdraw-published":
                {
                    if(!FullStationAttr(station,row,"wTo")){detail="The station changed after its withdrawn image was proven; the original is retained.";return false;}
                    var attempted=Withdraw(row,"refund-attempted");attempted.SetAttributeValue("refundTicks",DateTime.UtcNow.Ticks);
                    if(!Save(id,row,attempted)){detail="The refund write-ahead is settling.";return false;}
                    return ContinueRefund(id,attempted,out detail);
                }
                default:detail="This craft is not in a withdrawable phase.";return false;
            }
        }
        return false;
    }
    // After the refund released the hold: the station's tick clock is brought up to date (it was frozen for the whole hold) and the now useless
    // returned-call stamp is removed from the chunk. Both are best effort; neither is part of any proof.
    private static void FinishWithdrawalCleanup(XElement row)
    {
        try
        {
            var world=GameManager.Instance?.World;if(world==null||world.IsRemote()||!ThreadManager.IsMainThread())return;
            if(world.GetTileEntity(Position(row)) is TileEntityWorkstation station&&!(station is RebirthCamperStationState)&&!BlocksStation(station)&&!station.IsUserAccessing())
                station.ResetTickTime();
            ClearStamp(row);
        }
        catch{}
    }
    private static void ClearStamp(XElement row)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(world==null||!RebirthRemoteResourceBlockHost.Ready(world,Position(row),out var chunk,out _,out _)||chunk?.ChunkCustomData?.dict==null)return;
            if(chunk.ChunkCustomData.dict.Remove(StampKey(row)))chunk.isModified=true;
        }
        catch{}
    }
    // The owner keeps the craft: only up to withdraw-frozen (the setter was never entered), and only while the station still holds the admitted entry.
    // Needs only the station (not the worker): a dead or despawned worker must never leave the station frozen. The job was already ended by Advance
    // (cancel phases are Terminal), so nothing would observe the cooking any more: the row ends as `unresolved` (the entry keeps cooking and its output
    // stays in the station for the players; no pickup, outcome or award exists), which also lets P6 release the assignment.
    internal static bool TryAbortWithdrawal(EntityPlayer owner,RebirthNpcStableId npc,ulong assignmentId,out string detail)
    {
        detail="The withdrawal cannot be abandoned.";
        try
        {
            if(!TryRead(npc,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignmentId),out var row)){detail="No saved craft original exists for this assignment.";return false;}
            string phase=(string)row.Attribute("phase"),prior=(string)row.Attribute("wPrior");
            if(phase!="withdraw-attempted"&&phase!="withdraw-captured"&&phase!="withdraw-frozen"){detail="The queue entry was already withdrawn or is being withdrawn.";return false;}
            if(prior!="queue-attempted"&&prior!="queue-published"&&prior!="cooking"){detail="The withdrawal has no recorded prior phase.";return false;}
            if(!WithdrawEnvOf(owner,npc,row,false,out var env,out detail))return false;
            if(!AdmittedImage(env.Station,row,out _,out _)||!TerminalIs(env.Station,(string)row.Attribute("terminalBefore"))){detail="The station no longer holds exactly the admitted entry.";return false;}
            var station=env.Station;var held=row;
            // The tick clock was frozen for the hold; bring it up to date under the one-use permit so the entry does not complete in one go.
            Set(station,nameof(TileEntityWorkstation.ResetTickTime),null,()=>WithdrawEnvOf(owner,npc,held,false,out var live,out _)&&ReferenceEquals(live.Station,station)&&
                AdmittedImage(station,held,out _,out _),station.ResetTickTime);
            var back=WithoutWithdrawAttributes(row);back.SetAttributeValue("phase","unresolved");
            back.SetAttributeValue("note","The owner abandoned the withdrawal; the entry keeps cooking and its output stays in the station.");
            if(!Save(npc,row,back)){detail="Abandoning the withdrawal is settling.";return false;}
            ReleaseWatch(Guid.ParseExact((string)row.Attribute("operation"),"N"));
            ClearStamp(back);
            detail="The withdrawal was abandoned; the entry keeps cooking and its output stays in the station.";return true;
        }
        catch{return false;}
    }
}
