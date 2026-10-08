// TOOLS ONLY. No registration, debit, prediction repair, queue release or session issue transport.
using System;
internal interface ISeedPlacementTerminalBoundaryReview
{
    void ObserveTerminal(ClientInfo sender,World world,ulong epoch,ulong nonce,
        SeedPlacementIntentCodecReview.Intent? parsedIntent,NetPackageSetBlock native,
        SeedPlacementHostOutcomeReview outcome,bool nativeExecutionMayHaveStarted,
        SeedPlacementHostOwnerReview.TerminalReceipt receipt);
}
internal interface ISeedPlacementHostPolicyReview
{
    bool Validate(ClientInfo sender,World world,GameManager callbacks,
        SeedPlacementSessionAuthority.Reservation reservation,SeedPlacementIntentCodecReview.Intent intent,NetPackageSetBlock native);
}
internal sealed class SeedPlacementHostOwnerReview : ISeedPlacementReviewOwner
{
    private readonly ISeedPlacementTerminalBoundaryReview terminal;
    private readonly ISeedPlacementHostPolicyReview policy;
    internal SeedPlacementHostOwnerReview(ISeedPlacementTerminalBoundaryReview terminal,ISeedPlacementHostPolicyReview policy)
    {this.terminal=terminal??throw new ArgumentNullException(nameof(terminal));this.policy=policy??throw new ArgumentNullException(nameof(policy));}
    public bool TryAdmit(ClientInfo sender,World world,GameManager callbacks,ulong epoch,ulong nonce,
        SeedPlacementIntentCodecReview.Intent intent,NetPackageSetBlock original,out ISeedPlacementReviewAdmission admission)
    {
        admission=null;
        if(callbacks==null||!ReferenceEquals(GameManager.Instance,callbacks))return false;
        SeedPlacementSessionAuthority.Reservation reservation;
        if(!SeedPlacementSessionAuthority.TryReserve(sender,world,epoch,nonce,out reservation))return false;
        SeedPlacementOriginalCommandReview command;
        try{command=SeedPlacementOriginalCommandReview.Capture(epoch,nonce,sender.entityId,intent.OriginalCount,intent);}
        catch(System.IO.InvalidDataException){return false;}
        // Keep the genuine reservation/image even when subsequent validation rejects the command.
        var owned=new Admission(this,reservation,command,world);admission=owned;
        if(!SeedPlacementIntentCodecReview.ValidateSenderSnapshot(sender,world,callbacks,intent,original)||
            !policy.Validate(sender,world,callbacks,reservation,intent,original))return false;
        var scope=AdvancedFarmingRemotePlantingAdmissionReview.Begin(reservation,command,world,reservation.Actor);
        if(scope==null)return false;
        owned.SetScope(scope);
        if(!scope.BeforeNativeExecution())return false;
        owned.MarkExecutionMayStart();return true; // Envelope alone invokes the native executor next.
    }
    public void ReleaseOrRetainNativeAndReconcile(ClientInfo sender,World world,ulong epoch,ulong nonce,
        SeedPlacementIntentCodecReview.Intent? intent,NetPackageSetBlock original,ISeedPlacementReviewAdmission admission)
    {
        TerminalReceipt receipt;
        TerminalReceipt.TryMint(admission,this,sender,world,epoch,nonce,out receipt);
        // Outcome/bool are diagnostics. Only the sealed same-reservation receipt permits authenticated ACK export.
        terminal.ObserveTerminal(sender,world,epoch,nonce,intent,original,receipt?.Outcome,
            receipt!=null&&receipt.NativeExecutionMayHaveStarted,receipt);
    }
    internal sealed class TerminalReceipt
    {
        private readonly Admission source;
        private readonly ClientInfo sender;
        private readonly World world;
        private readonly int thread;
        private bool consumed;
        internal readonly SeedPlacementHostOutcomeReview Outcome;
        internal readonly bool NativeExecutionMayHaveStarted;
        private TerminalReceipt(Admission source,ClientInfo sender,World world)
        {this.source=source;this.sender=sender;this.world=world;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
         Outcome=source.SnapshotOutcome();NativeExecutionMayHaveStarted=source.NativeExecutionMayHaveStarted;}
        internal static bool TryMint(object opaqueAdmission,SeedPlacementHostOwnerReview issuer,ClientInfo sender,
            World world,ulong epoch,ulong nonce,out TerminalReceipt receipt)
        {
            receipt=null;var source=opaqueAdmission as Admission;
            if(source==null||!source.TryTakeTerminal(issuer,sender,world,epoch,nonce))return false;
            receipt=new TerminalReceipt(source,sender,world);return true;
        }
        internal bool TryConsumeCurrent(ClientInfo currentSender,World currentWorld,out SeedPlacementHostOutcomeReview outcome)
        {
            outcome=null;
            if(consumed||thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||
                !ReferenceEquals(sender,currentSender)||!ReferenceEquals(world,currentWorld)||
                !source.BoundCurrent(currentSender,currentWorld))return false;
            consumed=true;outcome=Outcome;return true; // Never reserves/releases a second nonce.
        }
    }
    private sealed class Admission : ISeedPlacementReviewAdmission
    {
        private readonly SeedPlacementHostOwnerReview issuer;
        private readonly SeedPlacementSessionAuthority.Reservation reservation;
        private readonly SeedPlacementOriginalCommandReview command;
        private readonly World world;
        private readonly int thread;
        private AdvancedFarmingRemotePlantingAdmissionReview.RemoteScope scope;
        private TileEntityPlantGrowingRebirth committed;
        private ulong incarnation,revision;
        private bool completed,disposed,terminalTaken;
        internal bool NativeExecutionMayHaveStarted {get;private set;}
        internal Admission(SeedPlacementHostOwnerReview issuer,SeedPlacementSessionAuthority.Reservation reservation,
            SeedPlacementOriginalCommandReview command,World world)
        {this.issuer=issuer;this.reservation=reservation;this.command=command;this.world=world;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        internal void SetScope(AdvancedFarmingRemotePlantingAdmissionReview.RemoteScope scope){this.scope=scope;}
        internal void MarkExecutionMayStart(){NativeExecutionMayHaveStarted=true;}
        internal bool BoundCurrent(ClientInfo sender,World suppliedWorld)
        {return ReferenceEquals(world,suppliedWorld)&&SeedPlacementSessionAuthority.IsBoundToSender(reservation,sender,world,reservation.Actor);}
        internal bool TryTakeTerminal(SeedPlacementHostOwnerReview owner,ClientInfo sender,World suppliedWorld,ulong epoch,ulong nonce)
        {
            if(terminalTaken||!disposed||thread!=System.Threading.Thread.CurrentThread.ManagedThreadId||
                !ReferenceEquals(owner,issuer)||epoch!=command.Epoch||nonce!=command.Nonce||!BoundCurrent(sender,suppliedWorld))return false;
            terminalTaken=true;return true;
        }
        public void CompleteObservedCommit()
        {
            if(disposed||completed||!NativeExecutionMayHaveStarted||scope==null||!scope.Complete())return;
            var te=world.GetTileEntity(command.Position) as TileEntityPlantGrowingRebirth;var observed=world.GetBlock(command.Position);
            if(!SeedPlacementSessionAuthority.IsCurrent(reservation)||te==null||te.PlantOrigin!=AdvancedFarmingPlantOrigin.Player||
                te.PlantIncarnation==0||te.StateRevision==0||observed.rawData!=command.Target.rawData||observed.damage!=command.Target.damage)return;
            committed=te;incarnation=te.PlantIncarnation;revision=te.StateRevision;completed=true;
        }
        internal SeedPlacementHostOutcomeReview SnapshotOutcome()
        {
            var observed=world.GetBlock(command.Position);
            bool exact=completed&&SeedPlacementSessionAuthority.IsCurrent(reservation)&&ReferenceEquals(world.GetTileEntity(command.Position),committed)&&
                committed.PlantOrigin==AdvancedFarmingPlantOrigin.Player&&committed.PlantIncarnation==incarnation&&committed.StateRevision==revision&&
                observed.rawData==command.Target.rawData&&observed.damage==command.Target.damage;
            var status=exact?SeedPlacementHostDispositionReview.Committed:NativeExecutionMayHaveStarted?
                SeedPlacementHostDispositionReview.NativeExecutionUncertain:SeedPlacementHostDispositionReview.RejectedBeforeExecution;
            return new SeedPlacementHostOutcomeReview(command,status,observed,exact?incarnation:0,exact?revision:0);
        }
        public void Dispose(){if(disposed)return;try{scope?.Dispose();}finally{disposed=true;}}
    }
}