using System;
using System.Collections.Generic;
using System.Threading;

#nullable disable

public enum RebirthNpcInventoryDispositionReason : byte
{
    Death = 0,
    Dismissal = 1,
    PermanentRemoval = 2,
    Migration = 3
}

public enum RebirthNpcInventoryDispositionMode : byte
{
    Preserve = 0,
    TransferToEndpoint = 1,
    DestroyUnreserved = 2
}

public sealed class RebirthNpcInventoryDispositionPolicy
{
    public RebirthNpcInventoryDispositionMode Mode { get; }
    public string EndpointId { get; }

    public RebirthNpcInventoryDispositionPolicy(RebirthNpcInventoryDispositionMode mode,
        string endpointId = null)
    {
        Mode = mode;
        EndpointId = (endpointId ?? string.Empty).Trim();
        if (mode == RebirthNpcInventoryDispositionMode.TransferToEndpoint && EndpointId.Length == 0)
            throw new ArgumentException("Transfer disposition requires an endpoint ID.", nameof(endpointId));
    }
}

public sealed class RebirthNpcInventoryDispositionResult
{
    public int ItemKindsProcessed { get; internal set; }
    public int QuantityProcessed { get; internal set; }
    public int Failures { get; internal set; }
    public uint FinalRevision { get; internal set; }
    public bool Preserved { get; internal set; }
}

public static class RebirthNpcInventoryDispositionService
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcInventoryDispositionReason, RebirthNpcInventoryDispositionPolicy> Policies =
        new Dictionary<RebirthNpcInventoryDispositionReason, RebirthNpcInventoryDispositionPolicy>();
    private static long executions;
    private static long failures;

    static RebirthNpcInventoryDispositionService()
    {
        Policies[RebirthNpcInventoryDispositionReason.Death] =
            new RebirthNpcInventoryDispositionPolicy(RebirthNpcInventoryDispositionMode.Preserve);
        Policies[RebirthNpcInventoryDispositionReason.Dismissal] =
            new RebirthNpcInventoryDispositionPolicy(RebirthNpcInventoryDispositionMode.Preserve);
        Policies[RebirthNpcInventoryDispositionReason.PermanentRemoval] =
            new RebirthNpcInventoryDispositionPolicy(RebirthNpcInventoryDispositionMode.DestroyUnreserved);
        Policies[RebirthNpcInventoryDispositionReason.Migration] =
            new RebirthNpcInventoryDispositionPolicy(RebirthNpcInventoryDispositionMode.Preserve);
    }

    public static void SetPolicy(RebirthNpcInventoryDispositionReason reason,
        RebirthNpcInventoryDispositionPolicy policy)
    {
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        lock (Sync) Policies[reason] = policy;
    }

    public static RebirthNpcInventoryDispositionResult Execute(RebirthNpcStableId npcId,
        RebirthNpcInventoryDispositionReason reason, string authorityKey)
    {
        if (npcId.IsEmpty) throw new ArgumentException("Stable NPC identity is required.", nameof(npcId));
        RebirthNpcInventoryDispositionPolicy policy;
        lock (Sync) policy = Policies[reason];

        RebirthNpcInventoryDispositionResult result = new RebirthNpcInventoryDispositionResult();
        if (policy.Mode == RebirthNpcInventoryDispositionMode.Preserve)
        {
            result.Preserved = true;
            result.FinalRevision = RebirthNpcInventoryTransactionService.GetRevision(npcId);
            Interlocked.Increment(ref executions);
            return result;
        }

        RebirthNpcEquipmentService.ReleaseAll(npcId);
        RebirthNpcInventorySnapshot snapshot = RebirthNpcInventoryTransactionService.GetSnapshot(npcId);
        uint revision = snapshot.Revision;
        foreach (KeyValuePair<string, int> pair in snapshot.Quantities)
        {
            if (pair.Value <= 0) continue;
            bool succeeded = false;
            if (policy.Mode == RebirthNpcInventoryDispositionMode.TransferToEndpoint)
            {
                string error;
                RebirthNpcExternalTransferRequest request = new RebirthNpcExternalTransferRequest(
                    Guid.NewGuid(), npcId, revision, policy.EndpointId, pair.Key, pair.Value,
                    RebirthNpcExternalTransferDirection.NpcToExternal, authorityKey);
                RebirthNpcExternalTransferResult transfer =
                    RebirthNpcExternalInventoryTransferCoordinator.Apply(request, out revision, out error);
                succeeded = transfer == RebirthNpcExternalTransferResult.Applied ||
                    transfer == RebirthNpcExternalTransferResult.Replayed;
            }
            else
            {
                RebirthNpcInventoryTransaction transaction = new RebirthNpcInventoryTransaction(
                    Guid.NewGuid(), npcId, revision, authorityKey,
                    new[] { new RebirthNpcInventoryMutation(pair.Key, -pair.Value) });
                RebirthNpcInventoryTransactionResult mutation =
                    RebirthNpcInventoryTransactionService.Apply(transaction, out revision);
                succeeded = mutation == RebirthNpcInventoryTransactionResult.Applied ||
                    mutation == RebirthNpcInventoryTransactionResult.Replayed;
            }

            result.ItemKindsProcessed++;
            if (succeeded) result.QuantityProcessed += pair.Value;
            else
            {
                result.Failures++;
                Interlocked.Increment(ref failures);
            }
        }
        result.FinalRevision = revision;
        Interlocked.Increment(ref executions);
        return result;
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            return "[REBIRTH NPC Inventory Disposition] policies=" + Policies.Count +
                " executions=" + Interlocked.Read(ref executions) +
                " failures=" + Interlocked.Read(ref failures);
        }
    }
}
