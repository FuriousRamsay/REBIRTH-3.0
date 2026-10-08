// TOOLS candidate only. One pending-operation owner; root authority/ACK transport and effects remain separate.
using System;using System.Collections.Generic;
public interface ISeedConnectedWitnessPort
{
    // Full native affected fields incl old/current density, textures, TE identity; raw block equality is insufficient.
    bool TryCaptureComplete(SeedNativeCapture capture,out FarmingPredictionRecoveryReducer.TargetWitness witness);
    bool CurrentSessionAndOriginal(SeedNativeCapture capture,SeedPlacementOriginalCommandReview original);
}
internal sealed class ConnectedSeedRecoveryOwner : ISeedNativeRejectionCancellationOwner,ISeedNativePreparedCaptureOwner,
    ISeedPlacementClientReviewOwner,ISeedRecoveryOwnerAdmission
{
    internal sealed class Pending
    {
        internal SeedNativeCapture Capture;internal SeedDebitReceipt Receipt;internal SeedPlacementOriginalCommandReview Original;
        internal SeedPlacementClientScopeReview Scope;internal SeedNativeDeferredDebit Deferred;
        internal object WorldState;internal Guid WorldId,CharacterId;
        internal bool Sent,Queued,Settled,Authenticated,Handled,PlacementReturned,InvocationStarted,Conflicted,NominalSendReturned;
        internal object PredictionTile,PredictionChunk;internal bool Claiming;internal SeedMatchedOutcomeEnrollment Matched;internal SeedPlacementHostOutcomeReview Outcome;internal SeedPlacementOutcomeFrameReview Frame;internal SeedOutgoingEnrollment Outgoing;internal byte[] IntentBytes;
        internal FarmingPredictionRecoveryReducer.TargetWitness Old,Prediction;
        internal FarmingPredictionRecoveryReducer.RecoveryDecision LastDecision;
    }
    readonly Dictionary<string,Pending> pending=new Dictionary<string,Pending>();
    readonly SeedRecoveryJournal journal;readonly ISeedConnectedWitnessPort witnesses;
    Pending enrolling; // synchronous clientScope enrollment reuse; not a second session registry.
    internal ConnectedSeedRecoveryOwner(SeedRecoveryJournal journal,ISeedConnectedWitnessPort witnesses)
    {this.journal=journal??throw new ArgumentNullException(nameof(journal));this.witnesses=witnesses;}
    static string Key(ulong e,ulong n)=>e+":"+n;
    internal Pending Find(ulong e,ulong n)=>pending.TryGetValue(Key(e,n),out var p)?p:null;
        ulong allocatingEpoch,allocatingNonce;
    SeedNativeCapture preparing;
    sealed class Retained : ISeedPlacementRetainedCommandReview
    {
        readonly ConnectedSeedRecoveryOwner owner;readonly Pending pending;
        internal Retained(ConnectedSeedRecoveryOwner o,Pending p){owner=o;pending=p;}
        public void MarkNativeInvocationStarted(){if(pending.InvocationStarted)throw new InvalidOperationException("Native invocation duplicated.");pending.InvocationStarted=true;}
        public bool TryBindConsumedEnvelope(SeedPlacementConsumedEnvelopeReview capture)
        {
            if(capture==null||!ReferenceEquals(capture.Original,pending.Original)||pending.Outgoing!=null||!pending.InvocationStarted||!owner.MatchesRetainedOriginal(pending.Original))return false;
            var intent=capture.CopyIntentBytes();var native=capture.CopyNativeBytes();
            if(!SeedPlacementIntentCodecReview.TryRead(intent,out var parsed)||!SeedPlacementOutcomeWireReview.SameCommand(pending.Original,SeedPlacementOriginalCommandReview.Capture(pending.Original.Epoch,pending.Original.Nonce,pending.Original.Actor,parsed.OriginalCount,parsed))||native.Length<2||native.Length>65535)return false;
            pending.IntentBytes=intent;owner.CapturePrediction(pending);pending.Outgoing=new SeedOutgoingEnrollment(owner,pending,pending.Original,intent,native);return true;
        }
        public void ObserveNativeSendReturn(SeedPlacementConsumedEnvelopeReview capture){if(capture!=null&&ReferenceEquals(capture.Original,pending.Original))pending.NominalSendReturned=true;}
        public void Retire(bool consumed,bool predictionMayHaveStarted)
        {pending.InvocationStarted|=predictionMayHaveStarted; // consumed is envelope selection, never proof of send or ACK.
        }
    }
    // Binding prefix ATTACHES Third-created pre-native capture; it never starts another clientScope.
    public bool TryEnroll(SeedNativeCapture capture,out SeedDebitReceipt receipt){receipt=null;throw new InvalidOperationException("Third producer must retain before native helper.");}
    public bool IsAuthoredHumanSeedCommand(ItemInventoryData data,BlockPlacement.Result placement,BlockValue actual)
    {
        var world=GameManager.Instance?.World;var player=data?.holdingEntity as EntityPlayerLocal;
        if(witnesses==null||world==null||!world.IsRemote()||GameManager.Instance.IsEditMode()||player==null||!data.IsBlock||placement.placement!=BlockPlacement.EnumPlacement.Voxel||!(actual.Block is BlockPlantGrowingRebirth crop)||!crop.IsSeedStage(actual)||preparing!=null)return false;
        foreach(var p in pending.Values)if(ReferenceEquals(p.Capture.Data,data)&&(!p.Settled||p.Outcome==null||p.Outcome.Disposition==SeedPlacementHostDispositionReview.NativeExecutionUncertain||p.Conflicted))throw new InvalidOperationException("Prior original seed custody unresolved.");
        byte[] seed;var slot=player.inventory.SelectedSlot;var stack=player.inventory.GetItem(slot);
        if(stack==null||stack.count<=0||!SeedPlacementIntentCodecReview.TrySeedIdentity(stack.itemValue,out seed))return false;
        preparing=new SeedNativeCapture(world,player,data,slot,stack.count,placement.blockPos,actual,world.GetBlock(placement.blockPos),seed);
        return preparing.ContextCurrent();
    }
    public bool TryGetBoundSessionCommand(World world,EntityPlayerLocal player,out ulong epoch,out ulong nonce)
    {
        epoch=nonce=0;if(preparing==null||!ReferenceEquals(preparing.World,world)||!ReferenceEquals(preparing.Player,player)||!preparing.ContextCurrent())return false;
        if(!FarmingSessionTransport.TryNext(out epoch,out nonce)){preparing=null;return false;}
        allocatingEpoch=epoch;allocatingNonce=nonce;return true;
    }
    public bool TryRetainOriginalCommand(World world,EntityPlayerLocal player,SeedPlacementOriginalCommandReview original,out ISeedPlacementRetainedCommandReview retained)
    {
        retained=null;var capture=preparing;preparing=null;
        if(capture==null||original==null||pending.Count>=256||original.Epoch!=allocatingEpoch||original.Nonce!=allocatingNonce||
            !ReferenceEquals(world,capture.World)||!ReferenceEquals(player,capture.Player)||original.Slot!=capture.OriginalSlot||original.OriginalCount!=capture.Count||
            !Same(original.Position,capture.Position)||original.Target.rawData!=capture.Target.rawData||original.Target.damage!=capture.Target.damage||
            original.ExpectedOld.rawData!=capture.Old.rawData||original.ExpectedOld.damage!=capture.Old.damage||!original.MatchesSeedIdentity(capture.Seed))throw new InvalidOperationException("Third private original command changed before retention.");
        if(!Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var worldId)||worldId==Guid.Empty||
            !Guid.TryParseExact(RebirthSurvivorClientState.GetProjectedCreationId(player),"N",out var character)||character==Guid.Empty)throw new InvalidOperationException("Unbound retention identity.");
        var receipt=new SeedDebitReceipt(original.Epoch,original.Nonce,capture.OriginalSlot,capture.Count,capture.Seed,world,player,capture.Data,capture.Source);
        var p=new Pending{Capture=capture,Receipt=receipt,Original=original,WorldState=world.worldState,WorldId=worldId,CharacterId=character};
        witnesses.TryCaptureComplete(capture,out p.Old);
        if(!journal.Retain(SeedRecoveryOriginalCommandAdapter.Capture(worldId,character,original,receipt),receipt))throw new InvalidOperationException("Durable original custody failed before native invocation.");
        pending.Add(Key(original.Epoch,original.Nonce),p);retained=new Retained(this,p);return true;
    }
    public bool TryAttachPrepared(ItemInventoryData data,BlockPlacement.Result placement,BlockValue actual,out SeedNativeCapture capture,out SeedDebitReceipt receipt)
    {
        capture=null;receipt=null;foreach(var p in pending.Values)if(ReferenceEquals(p.Capture.Data,data)&&p.InvocationStarted&&!p.PlacementReturned&&Same(placement.blockPos,p.Capture.Position)&&p.Capture.Target.rawData==actual.rawData&&p.Capture.Target.damage==actual.damage)
        {if(capture!=null)throw new InvalidOperationException("Ambiguous native original invocation.");capture=p.Capture;receipt=p.Receipt;}return capture!=null;
    }
    public void PlacementReturned(SeedNativeCapture capture,SeedDebitReceipt receipt,bool returned,Exception error)
    {var p=Find(receipt.Epoch,receipt.Nonce);if(p==null||!ReferenceEquals(capture,p.Capture))return;p.PlacementReturned=true;if(error!=null)p.LastDecision=null;}
public bool TryClaimQueuedDebit(ItemInventoryData data,int nativeQueuedIndex,out SeedNativeCapture capture,out SeedDebitReceipt receipt)
    {
        capture=null;receipt=null;Pending selected=null;
        foreach(var p in pending.Values)if(ReferenceEquals(p.Capture.Data,data)&&p.PlacementReturned&&!p.Queued){if(selected!=null)throw new InvalidOperationException("Ambiguous original debit queue.");selected=p;}
        if(selected==null)return false;selected.Queued=true;capture=selected.Capture;receipt=selected.Receipt;return true;
    }
    public bool MatchedEnvelopeAndContextCurrent(SeedNativeCapture capture,SeedDebitReceipt receipt)
    {var p=Find(receipt.Epoch,receipt.Nonce);return p!=null&&ReferenceEquals(p.Capture,capture)&&p.Sent&&p.Matched!=null&&p.Matched.PeerLink.IsCurrent()&&capture.ContextCurrent()&&witnesses!=null&&witnesses.CurrentSessionAndOriginal(capture,p.Original)&&(!p.Authenticated||p.Outcome.Disposition!=SeedPlacementHostDispositionReview.RejectedBeforeExecution);}
    public SeedPlacementOutcome ProvenOutcome(SeedNativeCapture capture,SeedDebitReceipt receipt)
    {var p=Find(receipt.Epoch,receipt.Nonce);return p?.Authenticated==true&&!p.Conflicted&&p.Outcome.Disposition==SeedPlacementHostDispositionReview.Committed?SeedPlacementOutcome.Committed:SeedPlacementOutcome.Unknown;}
    public bool CanCancelOriginalDebit(SeedNativeCapture capture,SeedDebitReceipt receipt)
    {var p=Find(receipt.Epoch,receipt.Nonce);return p!=null&&ReferenceEquals(p.Capture,capture)&&p.Authenticated&&!p.Conflicted&&p.Matched!=null&&p.Matched.PeerLink.IsCurrent()&&p.Outcome.Disposition==SeedPlacementHostDispositionReview.RejectedBeforeExecution&&MatchesRetainedOriginal(p.Outcome.Original)&&capture.ContextCurrent()&&receipt.MatchesOriginal(capture.Observe(receipt));}
    public void KeepHeld(SeedNativeDeferredDebit continuation,SeedDebitReceipt receipt,string reason)
    {var p=Find(receipt.Epoch,receipt.Nonce);if(p==null)throw new InvalidOperationException("Unknown original debit custody.");p.Deferred=continuation;p.Receipt=receipt;p.Settled=continuation.NativeDebitSettled;Persist(p);Evaluate(p);}
    public void RecordOriginalDebit(SeedNativeCapture capture,SeedDebitEvaluation evaluation)
    {var p=Find(evaluation.Receipt.Epoch,evaluation.Receipt.Nonce);if(p==null||!ReferenceEquals(p.Capture,capture))throw new InvalidOperationException("Unknown original native setter.");p.Receipt=evaluation.Receipt;p.Settled=true;Persist(p);Evaluate(p);}
    public bool MatchesRetainedOriginal(SeedPlacementOriginalCommandReview o)
    {
        if(o==null)return false;var p=Find(o.Epoch,o.Nonce);if(p==null||p.Handled||p.Conflicted||!p.Capture.StableContext()||!ReferenceEquals(p.WorldState,p.Capture.World.worldState)||witnesses==null||!witnesses.CurrentSessionAndOriginal(p.Capture,p.Original))return false;
        var a=p.Original;return a.Actor==o.Actor&&a.Slot==o.Slot&&a.OriginalCount==o.OriginalCount&&a.Flags==o.Flags&&a.Density==o.Density&&a.Texture==o.Texture&&Same(a.Position,o.Position)&&
            a.Target.rawData==o.Target.rawData&&a.Target.damage==o.Target.damage&&a.ExpectedOld.rawData==o.ExpectedOld.rawData&&a.ExpectedOld.damage==o.ExpectedOld.damage&&a.MatchesSeedIdentity(o.CopySeedIdentity());
    }
    private void ReceiveMatchedCore(World world,EntityPlayerLocal player,object state,SeedPlacementOutcomeFrameReview frame,SeedOutgoingEnrollment registered)
    {
        // Called only by actual Outcome packet AFTER root authenticated receive seam. Direct DTO calls are not authority.
        var outcome=frame?.Outcome;if(outcome==null||!MatchesRetainedOriginal(outcome.Original))return;var p=Find(outcome.Original.Epoch,outcome.Original.Nonce);
        if(registered==null || !ReferenceEquals(p.Outgoing,registered) || !registered.MatchesFrame(frame))return;
        if(!ReferenceEquals(world,p.Capture.World)||!ReferenceEquals(player,p.Capture.Player)||!ReferenceEquals(state,p.WorldState))return;
        if(p.Authenticated){if(!SameFrame(p.Frame,frame)){p.Conflicted=true;p.LastDecision=null;}return;}
        p.Authenticated=true;p.Sent=true;p.Outcome=outcome;p.Frame=frame; // Authenticated original ACK proves host received this command.
        if(p.Deferred!=null&&outcome.Disposition==SeedPlacementHostDispositionReview.RejectedBeforeExecution)p.Deferred.CancelBeforeOriginalOnce();
        Persist(p);Evaluate(p);
    }
    void Persist(Pending p)
    {
        bool rejected=p.Authenticated&&!p.Conflicted&&p.Outcome.Disposition==SeedPlacementHostDispositionReview.RejectedBeforeExecution;
        var result=!p.Authenticated||p.Outcome.Disposition==SeedPlacementHostDispositionReview.NativeExecutionUncertain?SeedPlacementOutcome.Unknown:rejected?SeedPlacementOutcome.RejectedBeforeMutation:SeedPlacementOutcome.Committed;
        // No unknown->refund inference: owner Current/AcceptEvidence requires authenticated full command and settled setter.
        journal.RecordEvidence(p.WorldId,p.CharacterId,p.Receipt.Epoch,p.Receipt.Nonce,p.Receipt,result,rejected,p.Authenticated&&!p.Conflicted&&p.Outcome.Disposition==SeedPlacementHostDispositionReview.NativeExecutionUncertain,this);
    }
    void Evaluate(Pending p)
    {
        if(p.Conflicted||!p.Authenticated||p.Old==null||p.Prediction==null||witnesses==null||!witnesses.TryCaptureComplete(p.Capture,out var current)){p.LastDecision=null;return;}
        var o=p.Outcome;var result=o.Disposition==SeedPlacementHostDispositionReview.Committed?FarmingPredictionRecoveryReducer.Outcome.Committed:o.Disposition==SeedPlacementHostDispositionReview.RejectedBeforeExecution?FarmingPredictionRecoveryReducer.Outcome.RejectedBeforeMutation:FarmingPredictionRecoveryReducer.Outcome.Indeterminate;
        // Root full affected-field snapshot is required. Rejected old is original complete snapshot, never rawdamage reconstruction.
        var authoritative=new FarmingPredictionRecoveryReducer.TargetWitness(o.Original.Position.x,o.Original.Position.y,o.Original.Position.z,p.Old.WorldGeneration,o.PlantIncarnation,o.StateRevision,
            SeedNativeAffectedImage.Encode(o.ObservedBlock,p.Frame.AuthoritativeDensity,p.Frame.AuthoritativeTexture,p.Frame.AuthoritativeTileAbsent,o.PlantIncarnation,o.StateRevision));
        var context=new FarmingPredictionRecoveryReducer.Context(p.Capture.World,p.Capture.Player,p.Capture.Data);
        p.LastDecision=FarmingPredictionRecoveryReducer.Decide(new FarmingPredictionRecoveryReducer.RecoveryObservation(result,
            new FarmingPredictionRecoveryReducer.Correlation(p.Receipt.Epoch,p.Receipt.Nonce),new FarmingPredictionRecoveryReducer.Correlation(o.Original.Epoch,o.Original.Nonce),context,context,p.Old,p.Prediction,current,authoritative,
            true,p.Sent,p.Authenticated,result==FarmingPredictionRecoveryReducer.Outcome.RejectedBeforeMutation,p.Handled,result==FarmingPredictionRecoveryReducer.Outcome.Committed,
            Convert.ToBase64String(p.Receipt.CanonicalSeed),p.Receipt.OriginalSlot,new FarmingPredictionRecoveryReducer.DebitReceipt(p.Receipt,p.Capture.Observe(p.Receipt,false,true),p.Settled,p.Handled)));
        // Decisions only. NO raw-block correction, refund, placement or automatic effect executor.
    }
        void CapturePrediction(Pending p)
    {
        var tile=p.Capture.World.GetTileEntity(p.Original.Position);
        if(witnesses.TryCaptureComplete(p.Capture,out var snapshot)&&ReferenceEquals(tile,p.Capture.World.GetTileEntity(p.Original.Position)))
        {p.Prediction=snapshot;p.PredictionTile=tile;p.PredictionChunk=p.Capture.World.ChunkCache.GetChunkFromWorldPos(new BlockValueRef(p.Original.Position));}
    }
    internal bool TryClaimOutcome(SeedPlacementOutcomeFrameReview frame,FarmingOutcomePeerReceipt peer,out SeedMatchedOutcomeEnrollment matched)
    {
        matched=null;if(frame?.Outcome==null||peer==null||peer.IsConsumed||!peer.IsAuthenticatedOutcomeCurrent())return false;
        var p=Find(frame.Outcome.Original.Epoch,frame.Outcome.Original.Nonce);
        if(p==null||p.Claiming||p.Handled||p.Outgoing==null||!p.Outgoing.MatchesFrame(frame)||!p.Outgoing.IsRegisteredCurrent()||
            !ReferenceEquals(peer.World,p.Capture.World)||!ReferenceEquals(peer.Player,p.Capture.Player)||!ReferenceEquals(peer.WorldState,p.WorldState)||
            peer.Epoch!=p.Original.Epoch||peer.Nonce!=p.Original.Nonce||peer.Actor!=p.Original.Actor||peer.Creation!=p.CharacterId||peer.ServerWorld!=p.WorldId)return false;
        p.Claiming=true;
        try
        {
            if(!peer.TryConsumeForEnrollment(p.Outgoing,out var link)||!p.Outgoing.IsRegisteredCurrent())return false;
            if(p.Authenticated)
            {
                if(!SameFrame(p.Frame,frame)){p.Conflicted=true;p.LastDecision=null;journal.RecordConflict(p.WorldId,p.CharacterId,p.Receipt.Epoch,p.Receipt.Nonce,p.Receipt,this);}
                return false;
            }
            matched=new SeedMatchedOutcomeEnrollment(link,p.Outgoing,frame);
            if(!link.IsCurrent())return false;p.Matched=matched;return true;
        }
        finally {p.Claiming=false;}
    }
    internal void ReceiveMatchedOutcome(SeedMatchedOutcomeEnrollment matched)
    {
        if(matched==null||matched.Delivered||matched.PeerLink==null||!matched.PeerLink.IsCurrent()||!ReferenceEquals(matched.Registered.Owner,this)||!ReferenceEquals(matched.Registered.Pending.Matched,matched))return;
        matched.Delivered=true; // Consume before callbacks/reentrant native getter.
        ReceiveMatchedCore(matched.Registered.World,matched.Registered.Player,matched.Registered.WorldState,matched.Frame,matched.Registered);
    }
    internal bool TryReserveMatchedCorrection(SeedMatchedOutcomeEnrollment matched,out Guid reservation)
    {
        reservation=Guid.Empty;if(matched==null||!matched.PeerLink.IsCurrent()||!ReferenceEquals(matched.Registered.Owner,this))return false;var p=matched.Registered.Pending;
        Evaluate(p);Persist(p);return journal.TryReserveCorrection(p.WorldId,p.CharacterId,p.Receipt.Epoch,p.Receipt.Nonce,p.Receipt,this,out reservation);
    }
    internal bool CompleteMatchedCorrection(SeedMatchedOutcomeEnrollment matched,Guid reservation)
    {
        if(matched==null||!matched.PeerLink.IsCurrent()||!ReferenceEquals(matched.Registered.Owner,this))return false;var p=matched.Registered.Pending;
        return journal.CompleteReservation(p.WorldId,p.CharacterId,p.Receipt.Epoch,p.Receipt.Nonce,reservation,SeedRecoveryEffect.PredictionCorrection);
    }
    public bool AcceptConflict(SeedRecoveryRecord retained,SeedDebitReceipt receipt)
    {var p=Find(receipt.Epoch,receipt.Nonce);return p!=null&&p.Conflicted&&p.Authenticated&&p.Matched!=null&&ReferenceEquals(p.Receipt,receipt)&&p.Capture.StableContext()&&p.Outgoing!=null&&ReferenceEquals(p.Outgoing.Original,p.Original);}
public bool Current(SeedRecoveryRecord retained,SeedDebitReceipt receipt)
    {var p=Find(receipt.Epoch,receipt.Nonce);return p!=null&&!p.Conflicted&&ReferenceEquals(p.Receipt,receipt)&&p.Settled&&FreshDebitMatches(p)&&retained.WorldId==p.WorldId&&retained.CharacterId==p.CharacterId&&MatchesRetainedOriginal(p.Original);}
    public bool AcceptEvidence(SeedRecoveryRecord retained,SeedDebitReceipt receipt,SeedPlacementOutcome outcome,bool noWorldEffect,bool partial)
    {var p=Find(receipt.Epoch,receipt.Nonce);if(p==null)return false;if(outcome==SeedPlacementOutcome.Unknown)return !noWorldEffect;
      return p.Authenticated&&p.Settled&&MatchesRetainedOriginal(p.Outcome.Original)&&
          (outcome==SeedPlacementOutcome.RejectedBeforeMutation?p.Outcome.Disposition==SeedPlacementHostDispositionReview.RejectedBeforeExecution&&noWorldEffect&&!partial:p.Outcome.Disposition==SeedPlacementHostDispositionReview.Committed&&!noWorldEffect&&!partial);}
        public bool CanCorrectPrediction(SeedRecoveryRecord retained,SeedDebitReceipt receipt)
    {var p=Find(receipt.Epoch,receipt.Nonce);if(p==null||!Current(retained,receipt))return false;Evaluate(p);return p.LastDecision!=null&&
        (p.LastDecision.Decisions&FarmingPredictionRecoveryReducer.Decision.CorrectExactPrediction)!=0;}
    internal bool TryGetEnrolledOriginal(World world,EntityPlayerLocal player,ItemInventoryData data,out SeedPlacementOriginalCommandReview original)
    {original=null;foreach(var p in pending.Values)if(ReferenceEquals(p.Capture.World,world)&&ReferenceEquals(p.Capture.Player,player)&&ReferenceEquals(p.Capture.Data,data)&&p.Scope!=null&&!p.PlacementReturned){original=p.Original;return true;}return false;}
    internal SeedOutgoingEnrollment RegisterOutgoing(SeedPlacementOriginalCommandReview original,byte[] intentBytes,byte[] nativeBytes,NetPackageSetBlock native)
    {
        if(original==null||intentBytes==null||nativeBytes==null||nativeBytes.Length==0||nativeBytes.Length>65535||native==null)return null;
        var p=Find(original.Epoch,original.Nonce);
        if(p==null||!ReferenceEquals(p.Original,original)||p.Outgoing!=null||!MatchesRetainedOriginal(original)||!Equal(p.IntentBytes,intentBytes))return null;
        if(!SeedPlacementIntentCodecReview.TryRead(intentBytes,out var intent)||native.blockChanges==null||native.blockChanges.Count!=1||
            native.localPlayerThatChanged!=original.Actor||native.blockChanges[0].changedByEntityId!=original.Actor||!SeedPlacementIntentCodecReview.MatchesChange(intent,native.blockChanges[0])||
            !native.blockChanges[0].blockValueRef.TryGetBlockPos(out var position)||!Same(position,original.Position))return null;
        using(var blob=StreamUtils.ToBlob(native.write))if(!Equal(blob.ToArray(),nativeBytes))return null;
        p.Outgoing=new SeedOutgoingEnrollment(this,p,original,intentBytes,nativeBytes);return p.Outgoing;
    }
    internal bool TryMatchRegisteredOutcome(SeedPlacementOutcomeFrameReview frame,out SeedOutgoingEnrollment registered)
    {registered=null;if(frame?.Outcome==null||!MatchesRetainedOriginal(frame.Outcome.Original))return false;var p=Find(frame.Outcome.Original.Epoch,frame.Outcome.Original.Nonce);if(p.Outgoing==null||!p.Outgoing.MatchesFrame(frame))return false;registered=p.Outgoing;return true;}
    static bool Equal(byte[] a,byte[] b){if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    bool FreshDebitMatches(Pending p)
    {
        var observed=p.Capture.Observe(p.Receipt,false,true);if(observed==null||observed.Reentrant)return false;
        if(p.Receipt.State==SeedDebitState.Captured)return p.Receipt.MatchesOriginal(observed);
        if(p.Receipt.State!=SeedDebitState.ExactDebitObserved||observed.Count!=p.Receipt.OriginalCount-1)return false;
        return observed.Count==0&&(observed.Seed==null||observed.Seed.Length==0)||Equal(observed.Seed,p.Receipt.CanonicalSeed);
    }
    static bool SameFrame(SeedPlacementOutcomeFrameReview a,SeedPlacementOutcomeFrameReview b)=>a!=null&&b!=null&&a.Creation==b.Creation&&a.ServerWorld==b.ServerWorld&&
        SeedPlacementOutcomeWireReview.SameCommand(a.Outcome.Original,b.Outcome.Original)&&SameOutcome(a.Outcome,b.Outcome)&&a.AuthoritativeDensity==b.AuthoritativeDensity&&a.AuthoritativeTexture==b.AuthoritativeTexture&&a.AuthoritativeTileAbsent==b.AuthoritativeTileAbsent;
static bool Same(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;
    static bool SameOutcome(SeedPlacementHostOutcomeReview a,SeedPlacementHostOutcomeReview b)=>a.Disposition==b.Disposition&&a.ObservedBlock.rawData==b.ObservedBlock.rawData&&a.ObservedBlock.damage==b.ObservedBlock.damage&&a.PlantIncarnation==b.PlantIncarnation&&a.StateRevision==b.StateRevision;
}



