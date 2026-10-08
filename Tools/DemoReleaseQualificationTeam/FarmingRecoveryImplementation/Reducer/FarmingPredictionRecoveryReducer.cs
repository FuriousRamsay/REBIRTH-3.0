using System;

// TOOLS ONLY: root-authorized proposed pure contract, pending Third binding.
public static class FarmingPredictionRecoveryReducer
{
    public enum Outcome { RejectedBeforeMutation, Committed, Indeterminate }
    [Flags] public enum Decision { Hold = 1, RequestAuthoritativeState = 2, AcceptObservedCommit = 4, CorrectExactPrediction = 8, ConsiderExactDebitCompensation = 16 }
    public sealed class Correlation
    {
        public readonly ulong Epoch, Nonce;
        public Correlation(ulong epoch, ulong nonce) { Epoch = epoch; Nonce = nonce; }
        internal bool Valid { get { return Epoch != 0 && Nonce != 0; } }
        internal bool Same(Correlation other) { return other != null && Valid && other.Valid && Epoch == other.Epoch && Nonce == other.Nonce; }
    }
    public sealed class Context
    {
        public readonly object World, Player, Action;
        public Context(object world, object player, object action) { World = world; Player = player; Action = action; }
        internal bool Valid { get { return World != null && Player != null && Action != null; } }
        internal bool Same(Context other) { return other != null && Valid && other.Valid && ReferenceEquals(World, other.World) && ReferenceEquals(Player, other.Player) && ReferenceEquals(Action, other.Action); }
    }
    public sealed class TargetWitness
    {
        public readonly int X, Y, Z;
        public readonly ulong WorldGeneration, Incarnation, Revision;
        public readonly string CanonicalBlock;
        // Incarnation/revision zero permitted for exact old nonplant tile; generation cannot be zero.
        public TargetWitness(int x, int y, int z, ulong worldGeneration, ulong incarnation, ulong revision, string canonicalBlock)
        { X = x; Y = y; Z = z; WorldGeneration = worldGeneration; Incarnation = incarnation; Revision = revision; CanonicalBlock = canonicalBlock; }
        internal bool Valid { get { return WorldGeneration != 0 && !string.IsNullOrEmpty(CanonicalBlock) && ((Incarnation == 0) == (Revision == 0)); } }
        internal bool SamePosition(TargetWitness other) { return other != null && X == other.X && Y == other.Y && Z == other.Z && WorldGeneration == other.WorldGeneration; }
        internal bool Same(TargetWitness other) { return Valid && other != null && other.Valid && SamePosition(other) && Incarnation == other.Incarnation && Revision == other.Revision && string.Equals(CanonicalBlock, other.CanonicalBlock, StringComparison.Ordinal); }
    }
    // Wrap actual gear receipt; reducer never rediscovers a native debit from a count delta.
    public sealed class DebitReceipt
    {
        public readonly SeedDebitReceipt Receipt;
        public readonly SeedDebitObservation Current;
        public readonly bool NativeDebitSettledProven, ReceiptAlreadyConsumed;
        public DebitReceipt(SeedDebitReceipt receipt, SeedDebitObservation current, bool nativeDebitSettledProven, bool receiptAlreadyConsumed)
        { Receipt = receipt; Current = current; NativeDebitSettledProven = nativeDebitSettledProven; ReceiptAlreadyConsumed = receiptAlreadyConsumed; }
        internal bool Scope(RecoveryObservation o)
        { return Receipt != null && Current != null && !Current.Reentrant && NativeDebitSettledProven && !ReceiptAlreadyConsumed &&
            Receipt.Epoch == o.ExpectedCorrelation.Epoch && Receipt.Nonce == o.ExpectedCorrelation.Nonce && Receipt.OriginalSlot == o.IntendedSlot &&
            ReferenceEquals(Receipt.WorldIdentity, o.ExpectedContext.World) && ReferenceEquals(Receipt.PlayerIdentity, o.ExpectedContext.Player) && ReferenceEquals(Receipt.ActionIdentity, o.ExpectedContext.Action) &&
            Current.Epoch == Receipt.Epoch && Current.Nonce == Receipt.Nonce && Current.Slot == Receipt.OriginalSlot &&
            ReferenceEquals(Current.World, Receipt.WorldIdentity) && ReferenceEquals(Current.Player, Receipt.PlayerIdentity) && ReferenceEquals(Current.Action, Receipt.ActionIdentity) && ReferenceEquals(Current.Source, Receipt.SourceIdentity) &&
            string.Equals(Convert.ToBase64String(Receipt.CanonicalSeed), o.IntendedSeed, StringComparison.Ordinal); }
        internal bool NoDebit(RecoveryObservation o)
        { return Scope(o) && Receipt.State == SeedDebitState.Captured && Receipt.MatchesOriginal(Current); }
        internal bool ExactDebit(RecoveryObservation o)
        {
            if (!Scope(o) || Receipt.State != SeedDebitState.ExactDebitObserved || Current.Count != Receipt.OriginalCount - 1) return false;
            byte[] seed = Current.Seed, original = Receipt.CanonicalSeed;
            if (Current.Count == 0 && (seed == null || seed.Length == 0)) return true;
            if (seed == null || seed.Length != original.Length) return false;
            for (int i = 0; i < seed.Length; i++) if (seed[i] != original[i]) return false;
            return Receipt.Resolve(SeedPlacementOutcome.RejectedBeforeMutation, true, false).Decision == SeedDebitDecision.RefundEligible;
        }
    }
    public sealed class RecoveryObservation
    {
        public readonly Outcome Result;
        public readonly Correlation ExpectedCorrelation, ReplyCorrelation;
        public readonly Context ExpectedContext, CurrentContext;
        public readonly TargetWitness ExpectedOld, Predicted, Current, Authoritative;
        public readonly bool PredictionObserved, Sent, AuthoritativeOutcomeProven, NativeMutationNeverStartedProven, AlreadyHandled, CommitCompleteProven;
        public readonly string IntendedSeed;
        public readonly int IntendedSlot;
        public readonly DebitReceipt Debit;
        public RecoveryObservation(Outcome result, Correlation expectedCorrelation, Correlation replyCorrelation, Context expectedContext, Context currentContext,
            TargetWitness expectedOld, TargetWitness predicted, TargetWitness current, TargetWitness authoritative,
            bool predictionObserved, bool sent, bool authoritativeOutcomeProven, bool nativeMutationNeverStartedProven, bool alreadyHandled, bool commitCompleteProven,
            string intendedSeed, int intendedSlot, DebitReceipt debit)
        { Result = result; ExpectedCorrelation = expectedCorrelation; ReplyCorrelation = replyCorrelation; ExpectedContext = expectedContext; CurrentContext = currentContext;
          ExpectedOld = expectedOld; Predicted = predicted; Current = current; Authoritative = authoritative;
          PredictionObserved = predictionObserved; Sent = sent; AuthoritativeOutcomeProven = authoritativeOutcomeProven; NativeMutationNeverStartedProven = nativeMutationNeverStartedProven;
          AlreadyHandled = alreadyHandled; CommitCompleteProven = commitCompleteProven; IntendedSeed = intendedSeed; IntendedSlot = intendedSlot; Debit = debit; }
    }
    public sealed class RecoveryDecision
    {
        public readonly Decision Decisions;
        public readonly string Reason;
        internal RecoveryDecision(Decision decisions, string reason) { Decisions = decisions; Reason = reason; }
    }
    private static RecoveryDecision Hold(string reason, bool request = false) { return new RecoveryDecision(Decision.Hold | (request ? Decision.RequestAuthoritativeState : 0), reason); }
    public static RecoveryDecision Decide(RecoveryObservation o)
    {
        if (o == null) return Hold("Missing observation.");
        if (o.ExpectedCorrelation == null || !o.ExpectedCorrelation.Same(o.ReplyCorrelation)) return Hold("Uncorrelated or stale epoch/nonce.");
        if (o.ExpectedContext == null || !o.ExpectedContext.Same(o.CurrentContext)) return Hold("World/player/action changed or missing.");
        if (o.AlreadyHandled) return Hold("Previously handled reply.");
        if (o.ExpectedOld == null || !o.ExpectedOld.Valid || o.Current == null || !o.Current.Valid || !o.ExpectedOld.SamePosition(o.Current)) return Hold("Missing or displaced current target.", true);
        if (o.IntendedSlot < 0 || string.IsNullOrEmpty(o.IntendedSeed)) return Hold("Missing full seed/slot custody identity.", true);
        if (o.PredictionObserved && (o.Predicted == null || !o.Predicted.Valid || !o.ExpectedOld.SamePosition(o.Predicted))) return Hold("Missing exact prediction witness.", true);
        if (o.Result != Outcome.RejectedBeforeMutation && o.Result != Outcome.Committed && o.Result != Outcome.Indeterminate) return Hold("Unknown outcome.", true);
        if (o.Result == Outcome.Indeterminate) return Hold("Unknown/partial outcome retains custody.", true);
        if (!o.AuthoritativeOutcomeProven || o.Authoritative == null || !o.Authoritative.Valid || !o.ExpectedOld.SamePosition(o.Authoritative)) return Hold("Missing correlated authoritative final evidence.", true);
        if (o.Result == Outcome.Committed)
        {
            if (!o.Sent || !o.CommitCompleteProven || o.NativeMutationNeverStartedProven) return Hold("Contradictory or incomplete commit evidence.", true);
            if (!o.Current.Same(o.Authoritative)) return Hold("Authoritative commit postimage replaced or newer.", true);
            return new RecoveryDecision(Decision.AcceptObservedCommit, "Exact correlated authoritative commit observed; no compensation.");
        }
        if (!o.NativeMutationNeverStartedProven || o.CommitCompleteProven || !o.Authoritative.Same(o.ExpectedOld)) return Hold("Rejected reply lacks definitive no-mutation/old-target proof.", true);
        if (!o.PredictionObserved)
        {
            // Native debit without a captured prediction is contradictory; never compensate.
            if (!o.Current.Same(o.ExpectedOld) || (o.Debit == null || !o.Debit.NoDebit(o))) return Hold("Unexpected target or debit without observed prediction.", true);
            return Hold("Definitive rejection with no observed prediction; no effects.");
        }
        if (!o.Current.Same(o.Predicted)) return Hold("Prediction replaced or newer; preserve it.", true);
        // A declared debit must have exact unchanged one-use custody; otherwise do not correct anything.
        if (o.Debit == null || (!o.Debit.NoDebit(o) && !o.Debit.ExactDebit(o))) return Hold("Debit custody changed, incomplete or consumed.", true);
        Decision decision = Decision.CorrectExactPrediction;
        if (o.Debit == null || !o.Debit.NoDebit(o)) decision |= Decision.ConsiderExactDebitCompensation;
        return new RecoveryDecision(decision, "Only exact prediction correction may be considered; effect owner must revalidate all witnesses.");
    }
}

