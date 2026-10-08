using System;

// TOOLS ONLY. Immutable observations/decisions; no authority, native mutation, replay or refund.
public enum SeedPlacementOutcome { Unknown, RejectedBeforeMutation, Committed }
public enum SeedDebitState { Captured, ExactDebitObserved, HeldUncertain }
public enum SeedDebitDecision { PreserveCustody, RefundEligible, CommittedDebit, DuplicateWitness }

public sealed class SeedDebitReceipt
{
    private readonly byte[] canonicalSeed;
    public ulong Epoch { get; }
    public ulong Nonce { get; }
    public int OriginalSlot { get; }
    public int OriginalCount { get; }
    public object WorldIdentity { get; }
    public object PlayerIdentity { get; }
    public object ActionIdentity { get; }
    public object SourceIdentity { get; }
    public SeedDebitState State { get; }
    public byte[] CanonicalSeed => (byte[])canonicalSeed.Clone();

    public SeedDebitReceipt(ulong epoch, ulong nonce, int slot, int count, byte[] seed,
        object world, object player, object action, object source)
        : this(epoch,nonce,slot,count,seed,world,player,action,source,SeedDebitState.Captured)
    { }
    private SeedDebitReceipt(ulong epoch, ulong nonce, int slot, int count, byte[] seed,
        object world, object player, object action, object source, SeedDebitState state)
    {
        if(epoch==0 || nonce==0 || slot<0 || count<=0 || seed==null || seed.Length==0 || seed.Length>8192 ||
            world==null || player==null || action==null || source==null) throw new ArgumentException("Invalid original debit capture.");
        Epoch=epoch;Nonce=nonce;OriginalSlot=slot;OriginalCount=count;canonicalSeed=(byte[])seed.Clone();
        WorldIdentity=world;PlayerIdentity=player;ActionIdentity=action;SourceIdentity=source;State=state;
    }
    private SeedDebitReceipt With(SeedDebitState state) => new SeedDebitReceipt(Epoch,Nonce,OriginalSlot,OriginalCount,
        canonicalSeed,WorldIdentity,PlayerIdentity,ActionIdentity,SourceIdentity,state);
    private bool Scope(SeedDebitObservation observed) => observed!=null && observed.Epoch==Epoch && observed.Nonce==Nonce &&
        observed.Slot==OriginalSlot && ReferenceEquals(observed.World,WorldIdentity) && ReferenceEquals(observed.Player,PlayerIdentity) &&
        ReferenceEquals(observed.Action,ActionIdentity) && ReferenceEquals(observed.Source,SourceIdentity);
    private bool Equal(byte[] seed)
    {
        if(seed==null || seed.Length!=canonicalSeed.Length)return false;
        for(int i=0;i<seed.Length;i++)if(seed[i]!=canonicalSeed[i])return false;
        return true;
    }
    public bool MatchesOriginal(SeedDebitObservation current) => Scope(current) && !current.Reentrant &&
        current.Count==OriginalCount && Equal(current.Seed);

    // Original native decrement must be witnessed across its exact setter boundary.
    // Merely observing count-1 later does not establish who consumed the item.
    public SeedDebitEvaluation ObserveOriginalDebit(SeedDebitObservation before, SeedDebitObservation after,
        bool witnessedOriginalNativeSetter, SeedPlacementOutcome outcome, bool authoritativeNoWorldEffect=false, bool partialWorldMutation=false)
    {
        if(State==SeedDebitState.ExactDebitObserved)
            return new SeedDebitEvaluation(this,SeedDebitDecision.DuplicateWitness);
        if(State==SeedDebitState.HeldUncertain || !Enum.IsDefined(typeof(SeedPlacementOutcome),outcome) ||
            !witnessedOriginalNativeSetter || !MatchesOriginal(before) || !Scope(after) || after.Reentrant ||
            after.Count!=OriginalCount-1 || (after.Count>0 && !Equal(after.Seed)) ||
            (after.Count==0 && after.Seed!=null && after.Seed.Length>0 && !Equal(after.Seed)))
            return new SeedDebitEvaluation(With(SeedDebitState.HeldUncertain),SeedDebitDecision.PreserveCustody);
        var receipt=With(SeedDebitState.ExactDebitObserved);
        return receipt.Resolve(outcome,authoritativeNoWorldEffect,partialWorldMutation);
    }
    // Receipt only marks eligibility; owner must durably atomically settle once.
    public SeedDebitEvaluation Resolve(SeedPlacementOutcome outcome, bool authoritativeNoWorldEffect=false, bool partialWorldMutation=false)
    {
        if(State!=SeedDebitState.ExactDebitObserved || !Enum.IsDefined(typeof(SeedPlacementOutcome),outcome) || partialWorldMutation ||
            (authoritativeNoWorldEffect && outcome!=SeedPlacementOutcome.RejectedBeforeMutation))
            return new SeedDebitEvaluation(this,SeedDebitDecision.PreserveCustody);
        return new SeedDebitEvaluation(this,outcome==SeedPlacementOutcome.RejectedBeforeMutation && authoritativeNoWorldEffect ? SeedDebitDecision.RefundEligible :
            outcome==SeedPlacementOutcome.Committed ? SeedDebitDecision.CommittedDebit : SeedDebitDecision.PreserveCustody);
    }
}

public sealed class SeedDebitObservation
{
    private readonly byte[] seed;
    public ulong Epoch { get; } public ulong Nonce { get; } public int Slot { get; } public int Count { get; }
    public object World { get; } public object Player { get; } public object Action { get; } public object Source { get; }
    public bool Reentrant { get; } public byte[] Seed => seed==null?null:(byte[])seed.Clone();
    public SeedDebitObservation(ulong epoch,ulong nonce,int slot,int count,byte[] canonicalSeed,object world,object player,
        object action,object originalSource,bool reentrant=false)
    { Epoch=epoch;Nonce=nonce;Slot=slot;Count=count;seed=canonicalSeed==null?null:(byte[])canonicalSeed.Clone();
      World=world;Player=player;Action=action;Source=originalSource;Reentrant=reentrant; }
}
public sealed class SeedDebitEvaluation
{
    public SeedDebitReceipt Receipt { get; } public SeedDebitDecision Decision { get; }
    public SeedDebitEvaluation(SeedDebitReceipt receipt,SeedDebitDecision decision){Receipt=receipt;Decision=decision;}
}


