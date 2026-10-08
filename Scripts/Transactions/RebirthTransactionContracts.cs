using System;

#nullable disable

/// <summary>
/// Minimal shared vocabulary for cross-domain REBIRTH mutations. Domain services retain
/// their own reservation/commit logic; this contract exists so failure and replay are not
/// collapsed into a success-like boolean.
/// </summary>
public enum RebirthTransactionState : byte
{
    Prepared = 0,
    Committed = 1,
    RolledBack = 2,
    Failed = 3,
    Indeterminate = 4
}

public sealed class RebirthTransactionReceipt
{
    public Guid TransactionId { get; private set; }
    public string IntentFingerprint { get; private set; }
    public RebirthTransactionState State { get; private set; }
    public string Detail { get; private set; }
    public long Revision { get; private set; }

    public bool IsTerminal
    {
        get { return State == RebirthTransactionState.Committed || State == RebirthTransactionState.RolledBack || State == RebirthTransactionState.Failed; }
    }

    public RebirthTransactionReceipt(Guid transactionId, string intentFingerprint,
        RebirthTransactionState state, string detail, long revision)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("Transaction id is required.", nameof(transactionId));
        TransactionId = transactionId;
        IntentFingerprint = intentFingerprint ?? string.Empty;
        State = state;
        Detail = detail ?? string.Empty;
        Revision = revision;
    }

    public bool Matches(string fingerprint)
    {
        return string.Equals(IntentFingerprint, fingerprint ?? string.Empty, StringComparison.Ordinal);
    }
}
