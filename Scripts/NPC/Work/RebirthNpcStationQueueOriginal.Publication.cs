using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthNpcStationQueueOriginal
{
    private static RebirthLogisticsEndpointIdentity.Admission nativeLease;
    private static RebirthStationSnapshotEvidence.Request queueWatch;
    private static Guid watchedOperation;
    private static World watchedWorld;
    // One token per World instance (a reload or restart creates a new World): it tells whether the in-memory native call that a saved
    // queue-attempted row names can still be alive. Never persisted except as the row's attemptSession.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World,string> worldSessions=
        new System.Runtime.CompilerServices.ConditionalWeakTable<World,string>();
    private static string WorldSession(World world)=>world==null?null:worldSessions.GetValue(world,_=>Guid.NewGuid().ToString("N"));
    private static bool SameSession(XElement row,World world)=>world!=null&&(string)row.Attribute("attemptSession")==WorldSession(world);
    // queue-attempted without a returned-call stamp, found by a LATER session than the one that entered set_Queue (crash/restart between the
    // native setter and the chunk stamp). The in-memory call is gone, so the loaded station image is the truth: still the exact pre-image means the
    // insertion never persisted (the saved debit stands, the row goes back to debit-attempted and the insertion is redone once); exactly the
    // post-image means it persisted (the idempotent tick reset and the stamp are completed). Any other image, or the same session, stays held.
    private static RebirthNpcWorkMutationResult RecoverUnreturned(RebirthNpcConcreteWorkContext c,XElement row,EntityRebirthHumanoidNPC npc,TileEntityWorkstation station)
    {
        const string held="Entered queue insertion has no returned-call witness; it is not replayed.";
        var a=c.Assignment;var world=npc.world;
        if(SameSession(row,world)||!SavedDebit(a.NpcId,row)||!Physical(npc,row,true))return Hold(held);
        if(!RebirthRemoteResourceBlockHost.Ready(world,Position(row),out var chunk,out _,out var providerCurrent)||
            chunk?.ChunkCustomData?.dict==null||chunk.ChunkCustomData.dict.ContainsKey(StampKey(row)))return Hold(held);
        if(FullStation(station,row,"before")&&!QueueCurrent(station,row))
        {
            var back=new XElement(row);back.SetAttributeValue("phase","debit-attempted");back.Attribute("debitProof")?.Remove();back.Attribute("attemptSession")?.Remove();
            return Save(a.NpcId,row,back)?Pending("Queue insertion did not persist before the restart; it is redone once from the saved debit."):
                Hold("Queue insertion recovery is settling.");
        }
        if(FullStation(station,row,"after")&&QueueCurrent(station,row))
            return FinishEntered(c,row,station,chunk,providerCurrent)?Pending("Queue insertion had persisted before the restart; its witness is completed."):
                Hold("Persisted queue insertion could not be completed; original retained.");
        return Hold(held);
    }
    // Compared through the native wire (what survives a chunk/TE reload); the sidecar-only runtime Recipe fields are reset by Recipe.Read.
    private static bool QueueCurrent(TileEntityWorkstation station,XElement row)=>
        RebirthRemoteResourcePlacedStationQueue.TryDecode(row.Element("queueAfter")?.Value??string.Empty,out var expected)&&station?.Queue!=null&&
        QueueWire(station.Queue)==QueueWire(expected);
    private static bool BoundAssignment(RebirthNpcWorkAssignment a,XElement row)=>
        RebirthNpcRecipeQueueWork.Allows(a)&&(string)row.Attribute("assignment")==a.AssignmentId.ToString()&&
        (long?)row.Attribute("startedTicks")==a.CreatedUtcTicks&&(string)row.Attribute("world")==GameManager.Instance?.World?.worldState?.Guid&&
        (string)row.Attribute("owner")==RebirthStablePlayerIdentity.ComputeStorageKey(a.ActorId)&&
        RebirthNpcRecipeQueueWork.MatchesBoundSource(a,(ulong)row.Attribute("sourceAssignment"),Position(row).ToVector3())&&UnitsMatchKey(a,row);
    internal static bool CanObserveExistingAssignment(RebirthNpcWorkAssignment a,EntityRebirthHumanoidNPC npc,XElement row)
    {
        try
        {
            if(!BoundAssignment(a,row))return false;
            string phase=(string)row.Attribute("phase");
            if(IsCancelPhase(phase))return false;
            if(phase=="queue-published"||phase=="completed"||IsOutputPhase(phase))return
                (string)row.Attribute("debitProof")==DebitProof(row)&&((string)row.Attribute("leaseProof"))?.Length==64&&
                ((string)row.Attribute("queueProof"))?.Length==64;
            var world=GameManager.Instance?.World;var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
            if(npc==null||npc.IsDead()||npc.IsPreparedRestorationPending||npc.Hand==null||npc.Hand.IsBusy||npc.GetAttackTarget()!=null||!ReferenceEquals(npc.world,world)||
                !ReferenceEquals(world?.GetEntity(npc.entityId),npc)||npc.RebirthRuntimeState?.StableId!=a.NpcId||
                !string.Equals(npc.RebirthRuntimeState.OwnerId,a.ActorId,StringComparison.Ordinal)||owner==null||owner.IsDead()||
                owner.Died!=(int)row.Attribute("deaths")||!RebirthOriginalActionScope.Matches(owner,(string)row.Attribute("owner"),(string)row.Attribute("creation"))||
                !RebirthNpcAggregatePersistenceStore.TryGetView(a.NpcId,out var person)||person.Presence==null||
                !RebirthNpcRecipeQueueWork.ResolveSet(a,out var set)||!UnitsCurrent(npc,a,row,set)||
                !RebirthNpcRecipeStagingWork.StationCurrent(set[0].Target,owner))return false;
            // A stale embodiment before the debit is dispatched so Advance can abort it safely (nothing consumed); after the
            // debit started the stable NPC continues under its new embodiment (same rule as Current).
            if((uint?)row.Attribute("embodiment")!=person.Presence.EmbodimentGeneration)
            {
                if(phase=="prepared")return true;
                if(phase!="debit-attempted"&&phase!="queue-attempted")return false;
            }
            var station=world.GetTileEntity(Position(row)) as TileEntityWorkstation;if(station==null)return false;
            if(phase=="prepared")return Physical(npc,row,false)&&FullStation(station,row,"before");
            if(!SavedDebit(a.NpcId,row)||!Physical(npc,row,true))return false;
            if(phase=="debit-attempted")return FullStation(station,row,"before");
            if(phase!="queue-attempted")return false;
            var chunk=station.GetChunk();
            if(chunk?.ChunkCustomData?.dict==null)return false;
            bool after=FullStation(station,row,"after")&&QueueCurrent(station,row);
            if(chunk.ChunkCustomData.dict.TryGetValue(StampKey(row),out var stamp))
                return after&&stamp?.data!=null&&stamp.data.SequenceEqual(Stamp(row));
            // No returned-call stamp: dispatched only when the setter was entered by an earlier session (RecoverUnreturned classifies it).
            return !SameSession(row,world)&&(after||FullStation(station,row,"before"));
        }
        catch{return false;}
    }
    private static bool AdmitNativeLease(RebirthNpcConcreteWorkContext c,XElement row)
    {
        if(!Current(c,row,out var npc,out _,out var station)||!Physical(npc,row,false)||!FullStation(station,row,"before"))return false;
        if(row.Attribute("leaseProof")!=null)return true;
        bool Authorized()=>Current(c,row,out var actual,out _,out var exact)&&ReferenceEquals(exact,station)&&
            Physical(actual,row,false)&&FullStation(station,row,"before");
        if(nativeLease!=null&&(!nativeLease.Current()||!ReferenceEquals(nativeLease.Tile,station)))
        {nativeLease.Dispose();nativeLease=null;}
        RebirthLogisticsEndpointIdentity.Install();
        if(nativeLease==null)nativeLease=RebirthLogisticsEndpointIdentity.Begin(npc.world,station,Authorized);
        if(nativeLease==null||nativeLease.Id!=(string)row.Attribute("incarnation")||!nativeLease.Saved()||
            !RebirthStationSnapshotEvidence.TryCopy(nativeLease.Request,out var bytes))return false;
        string proof;
        using(var sha=System.Security.Cryptography.SHA256.Create())proof=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        var next=new XElement(row);next.SetAttributeValue("leaseProof",proof);
        if(!Save(c.Assignment.NpcId,row,next))return false;
        nativeLease.Dispose();nativeLease=null;return true;
    }
    private static bool PublishQueue(RebirthNpcConcreteWorkContext c,XElement row)
    {
        if((string)row.Attribute("phase")!="queue-attempted"||!SavedDebit(c.Assignment.NpcId,row)||
            !Current(c,row,out var npc,out _,out var station)||!Physical(npc,row,true)||!FullStation(station,row,"after")||!QueueCurrent(station,row)||
            !RebirthRemoteResourceBlockHost.Ready(npc.world,Position(row),out var chunk,out var provider,out var providerCurrent)||
            !RebirthLogisticsEndpointIdentity.TryGetNativeMarker(npc.world,station,out var birthKey,out var birthMarker))return false;
        var world=npc.world;byte[] birth=(byte[])birthMarker.data.Clone(),stamp=Stamp(row);string key=StampKey(row);
        bool CurrentImage()=>providerCurrent()&&Current(c,row,out var live,out _,out var exact)&&ReferenceEquals(exact,station)&&
            Physical(live,row,true)&&FullStation(station,row,"after")&&QueueCurrent(station,row)&&
            RebirthLogisticsEndpointIdentity.TryGetNativeMarker(world,station,out var currentKey,out var currentBirth)&&
            currentKey==birthKey&&currentBirth.data.SequenceEqual(birth)&&
            chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)&&marker?.data!=null&&marker.data.SequenceEqual(stamp)&&
            marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null;
        if(!CurrentImage())return false;
        if(queueWatch!=null&&(!ReferenceEquals(watchedWorld,world)||watchedOperation!=c.OperationId||queueWatch.Observe?.Invoke()!=true))
        {queueWatch.Dispose();queueWatch=null;}
        if(queueWatch!=null&&RebirthStationSnapshotEvidence.TryGetPublished(queueWatch,out var publication)&&
            RebirthStationSnapshotEvidence.TryCopy(queueWatch,out var bytes)&&
            RebirthPlacedOriginalPublication.TrySnapshot(bytes,chunk.X,chunk.Z,Position(row),(uint)row.Attribute("blockRaw"),key,stamp,out _,birthKey,birth))
        {
            var next=new XElement(row);next.SetAttributeValue("phase","queue-published");next.SetAttributeValue("queueProof",publication.PayloadDigest);
            if(!Save(c.Assignment.NpcId,row,next))return false;
            queueWatch.Dispose();queueWatch=null;return true;
        }
        if(queueWatch!=null)return false;
        if(!RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var expected))return false;
        queueWatch=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,CurrentImage,station,
            (inputs,queue)=>expected.MatchesSerializedQueue(queue));
        if(queueWatch==null)return false;watchedWorld=world;watchedOperation=c.OperationId;
        lock(chunk)
        {
            if(!CurrentImage()){queueWatch.Dispose();queueWatch=null;return false;}
            if(ChunkBusy(chunk)){queueWatch.Dispose();queueWatch=null;return false;}
            chunk.InProgressSaving=true;
        }
        try{provider.m_RegionFileManager.SaveChunkSnapshot(chunk,true);}
        finally{lock(chunk)chunk.InProgressSaving=false;}
        return false;
    }
    private static RebirthNpcWorkOutcomeRecord Outcome(RebirthNpcWorkAssignment a,XElement row)=>new RebirthNpcWorkOutcomeRecord{
        OutcomeId=Guid.ParseExact((string)row.Attribute("operation"),"N"),AssignmentId=a.AssignmentId,NpcId=a.NpcId,DefinitionId=RebirthNpcRecipeQueueWork.DefinitionId,
        TargetId=RebirthNpcRecipeQueueWork.TargetId(a),StartedUtcTicks=a.CreatedUtcTicks,CompletedUtcTicks=(long)row.Attribute("completedTicks"),DurableCompletion=true,
        DurableWitness="npc-native-queue:"+(string)row.Attribute("binding")+"|debit:"+(string)row.Attribute("debitProof")+"|queue:"+(string)row.Attribute("queueProof")+
            (V2(row)?"|output:"+(string)row.Attribute("outputProof")+"|pickup:"+(string)row.Attribute("pickupProof"):string.Empty),
        State=RebirthNpcWorkOutcomeState.Completed,FailureCategory=RebirthNpcWorkFailureCategory.None,
        Detail=V2(row)?"One known recipe cooked by the native station queue; its exact finished output moved into the NPC bag.":
            "One known recipe admitted to the native station queue; cooking and output collection remain pending.",
        Produced=V2(row)?new Dictionary<string,int>{{RebirthHumanEquipment.Decode((string)row.Attribute("outFrame")).itemValue.ItemClass.GetItemName(),
            RebirthHumanEquipment.Decode((string)row.Attribute("outFrame")).count}}:null,
        Consumed=ConsumedMap(row)};
    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)
    {
        try
        {
            if(outcome==null||!RebirthNpcRecipeQueueWork.IsDefinition(outcome.DefinitionId)||!outcome.DurableCompletion||outcome.State!=RebirthNpcWorkOutcomeState.Completed||
                !RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId,out var a)||!RebirthNpcRecipeQueueWork.Allows(a)||a.NpcId!=outcome.NpcId||
                outcome.OutcomeId!=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId)||!TryRead(a.NpcId,outcome.OutcomeId,out var row)||
                (V2(row)?(string)row.Attribute("phase")!="pickup-settled"&&(string)row.Attribute("phase")!="completed":
                    (string)row.Attribute("phase")!="queue-published"&&(string)row.Attribute("phase")!="completed")||
                !BoundAssignment(a,row)||
                (string)row.Attribute("debitProof")!=DebitProof(row)||((string)row.Attribute("queueProof"))?.Length!=64||
                ((string)row.Attribute("leaseProof"))?.Length!=64||((long?)row.Attribute("completedTicks")??0)<=0)return false;
            var expected=Outcome(a,row);
            return outcome.TargetId==expected.TargetId&&outcome.StartedUtcTicks==expected.StartedUtcTicks&&outcome.CompletedUtcTicks==expected.CompletedUtcTicks&&
                outcome.DurableWitness==expected.DurableWitness&&outcome.Detail==expected.Detail&&
                (V2(row)?outcome.Produced?.Count==1&&outcome.Produced.TryGetValue(expected.Produced.Keys.Single(),out int made)&&made==expected.Produced.Values.Single():outcome.Produced==null)&&
                SameMap(outcome.Consumed,expected.Consumed);
        }
        catch{return false;}
    }
    private static RebirthNpcWorkMutationResult Pending(string detail)=>RebirthNpcWorkMutationResult.Continue(0f,detail);
    private static RebirthNpcWorkMutationResult Hold(string detail)=>new RebirthNpcWorkMutationResult{
        Disposition=RebirthNpcWorkExecutionDisposition.Suspend,FailureCategory=RebirthNpcWorkFailureCategory.PersistenceRecovery,Detail=detail};
    private static RebirthNpcWorkMutationResult FinishSavedAdmission(RebirthNpcWorkAssignment a,XElement row)
    {
        // Positive saved queue and debit proofs already own this disposition.
        // No EAI/native setter/source mutation or old embodiment is adopted.
        if(!BoundAssignment(a,row)||(string)row.Attribute("debitProof")!=DebitProof(row)||
            ((string)row.Attribute("leaseProof"))?.Length!=64||((string)row.Attribute("queueProof"))?.Length!=64)
            return Hold("Saved native admission does not match this exact assignment original.");
        if(row.Attribute("completedTicks")==null)
        {var next=new XElement(row);next.SetAttributeValue("completedTicks",DateTime.UtcNow.Ticks);if(!Save(a.NpcId,row,next))return Hold("Native queue admission outcome timestamp is settling.");row=next;}
        if(!RebirthNpcWorkOutcomeService.TryRecordCommitted(Outcome(a,row)))return Hold("Positive native queue admission awaits saved outcome publication.");
        if((string)row.Attribute("phase")!="completed")
        {var next=new XElement(row);next.SetAttributeValue("phase","completed");if(!Save(a.NpcId,row,next))return Hold("Saved queue admission is awaiting terminal metadata publication.");row=next;}
        return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,
            Detail=Outcome(a,row).Detail,Consumed=Outcome(a,row).Consumed};
    }
    internal static RebirthNpcWorkMutationResult Advance(RebirthNpcConcreteWorkContext c)
    {
        try
        {
            ReapWatches();RetryPendingAwards();
            var a=c?.Assignment;if(!RebirthNpcRecipeQueueWork.Allows(a))return Hold("Exact saved NPC queue assignment is unavailable.");
            if(!TryRead(a.NpcId,c.OperationId,out var row))return Prepare(c)?Pending("Saved NPC queue original prepared; native station lease publication is pending."):
                Hold("Known one-unit recipe, paid staged source or eligible empty station is unavailable.");
            string phase=(string)row.Attribute("phase");
            if(IsCancelPhase(phase))return Terminal("This craft was cancelled; its withdrawal and refund are finished only by an owner-confirmed cancel call.");
            if(phase=="queue-published"||phase=="completed"||IsOutputPhase(phase))return Finish(c,a,row);
            // Re-embodied (restart) before any debit: nothing was consumed (the debit starts only after debit-attempted is saved), so the
            // original can be released instead of pinning station, bag and assignment forever. The staged ingredient stays in the bag.
            if(phase=="prepared"&&RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is { } stale&&(uint?)row.Attribute("embodiment")!=stale.Generation)
                return Unresolved(a,row,"The NPC was re-embodied before any ingredient was debited; nothing was consumed.");
            if(!Current(c,row,out var npc,out _,out var station))return Hold("Original EAI, NPC/source custody or station authority changed.");
            if(phase=="prepared")
            {
                if(!Physical(npc,row,false)||!FullStation(station,row,"before"))return Hold("Saved unentered NPC/station preimage changed; no debit is authorized.");
                if(!AdmitNativeLease(c,row))return Pending("Actual native station identity publication is pending; no ingredient consumed.");
                if(!TryRead(a.NpcId,c.OperationId,out row)||!DebitNative(c,row))return Hold("Native ingredient debit is retained; an uncertain setter is not repeated.");
                return Pending("Exact NPC ingredient debit saved; native queue insertion remains owed.");
            }
            if(phase=="debit-attempted")
            {
                // No saved debit: the live bag and the saved inventory must still equal the pre-image (DebitNative verifies both) and the
                // all-N debit is retried; otherwise the original stays retained.
                if(!SavedDebit(a.NpcId,row))
                    return DebitNative(c,row)?Pending("Debit retried from the intact pre-image and saved; native queue insertion remains owed."):
                        Hold("Debit pre-image is not intact or the retry did not complete; original retained.");
                return ApplyQueue(c,row)?
                    Pending("Genuine native queue insertion returned; exact saved queue publication is pending."):
                    Hold("Saved positive native debit or unentered exact station queue is unavailable; original retained.");
            }
            if(phase=="queue-attempted")
            {
                var chunk=station.GetChunk();
                if(chunk?.ChunkCustomData?.dict==null)return Hold("Entered queue insertion has no returned-call witness; it is not replayed.");
                if(!chunk.ChunkCustomData.dict.TryGetValue(StampKey(row),out var stamp))return RecoverUnreturned(c,row,npc,station);
                if(stamp?.data==null||!stamp.data.SequenceEqual(Stamp(row)))return Hold("Entered queue insertion has no returned-call witness; it is not replayed.");
                if(!Physical(npc,row,true)||!FullStation(station,row,"after")||!QueueCurrent(station,row))
                    return Hold("Returned native queue or paid NPC image changed; publication authority is held.");
                if(!PublishQueue(c,row))return Pending("Native returned queue is awaiting positive saved readback.");
                if(!TryRead(a.NpcId,c.OperationId,out row))return Hold("Saved queue proof readback is unavailable.");phase=(string)row.Attribute("phase");
            }
            if(phase!="queue-published"&&phase!="completed")return Hold("NPC queue original remains retained.");
            return Finish(c,a,row);
        }
        catch{return Hold("NPC native queue original retained after an indeterminate source or station operation.");}
    }
}
