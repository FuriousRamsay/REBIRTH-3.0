using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcPhysicalResourceEndpoint : IRebirthNpcExternalInventoryEndpoint,
    IRebirthNpcExternalInventoryQuantityEndpoint
{
    private sealed class Reservation : IRebirthNpcExternalInventoryReservation, IRebirthNpcExternalInventoryReservationOutcome
    {
        private enum ReservationState : byte { Prepared = 0, Committed = 1, Failed = 2, RolledBack = 3, Indeterminate = 4 }
        private readonly RebirthNpcPhysicalResourceEndpoint owner;
        private readonly string itemKey;
        private readonly int quantity;
        private readonly bool debit;
        private readonly int expectedRevision;
        private ReservationState state;
        private bool released;
        private string terminalError = string.Empty;

        public string EndpointId { get { return owner.EndpointId; } }
        public Guid TransactionId { get; private set; }
        RebirthTransactionState IRebirthNpcExternalInventoryReservationOutcome.State
        {
            get
            {
                switch (state)
                {
                    case ReservationState.Committed: return RebirthTransactionState.Committed;
                    case ReservationState.RolledBack: return RebirthTransactionState.RolledBack;
                    case ReservationState.Failed: return RebirthTransactionState.Failed;
                    case ReservationState.Indeterminate: return RebirthTransactionState.Indeterminate;
                    default: return RebirthTransactionState.Prepared;
                }
            }
        }
        string IRebirthNpcExternalInventoryReservationOutcome.Detail { get { return terminalError ?? string.Empty; } }


        public Reservation(RebirthNpcPhysicalResourceEndpoint owner, Guid transactionId,
            string itemKey, int quantity, bool debit, int expectedRevision)
        {
            this.owner = owner; TransactionId = transactionId; this.itemKey = itemKey;
            this.quantity = quantity; this.debit = debit; this.expectedRevision = expectedRevision;
        }

        public bool Commit(out string error)
        {
            lock (this)
            {
                if (state == ReservationState.Committed) { error = string.Empty; return true; }
                if (state != ReservationState.Prepared) { error = terminalError.Length > 0 ? terminalError : "Reservation is no longer commit-capable."; return false; }
                try
                {
                    bool success = owner.Commit(TransactionId, itemKey, quantity, debit, expectedRevision, out error);
                    terminalError = error ?? string.Empty;
                    state = success ? ReservationState.Committed : (IsIndeterminate(error) ? ReservationState.Indeterminate : ReservationState.Failed);
                    return success;
                }
                catch (Exception ex)
                {
                    terminalError = ex.GetType().Name + ": " + ex.Message;
                    error = terminalError;
                    state = ReservationState.Indeterminate;
                    return false;
                }
                finally { ReleaseOnce(); }
            }
        }

        public bool Rollback(out string error)
        {
            lock (this)
            {
                if (state == ReservationState.RolledBack) { error = string.Empty; return true; }
                if (state == ReservationState.Committed || state == ReservationState.Indeterminate) { error = "Committed or indeterminate reservation requires an explicit compensation receipt."; return false; }
                ReleaseOnce(); state = ReservationState.RolledBack; terminalError = string.Empty; error = string.Empty;
                Interlocked.Increment(ref owner.rollbacks); return true;
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                if (state != ReservationState.Prepared) return;
                ReleaseOnce(); state = ReservationState.RolledBack; Interlocked.Increment(ref owner.rollbacks);
            }
        }

        private void ReleaseOnce()
        {
            if (released) return;
            owner.Release(TransactionId, itemKey, quantity, debit); released = true;
        }
        private static bool IsIndeterminate(string error)
        {
            return !string.IsNullOrEmpty(error) &&
                error.IndexOf("indeterminate", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    private readonly object sync = new object();
    private readonly Dictionary<Guid, int> debitReservations = new Dictionary<Guid, int>();
    private readonly Dictionary<Guid, int> creditReservations = new Dictionary<Guid, int>();
    private readonly Dictionary<string, int> debitByItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> creditByItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private int totalReservedCredit;
    private long reservations;
    private long commits;
    private long rollbacks;
    private long rejections;

    public string EndpointId { get; private set; }
    public RemoteResourceSourceKind Kind { get; private set; }
    public Vector3i Position { get; private set; }

    public RebirthNpcPhysicalResourceEndpoint(string endpointId, RemoteResourceSourceKind kind, Vector3i position)
    {
        EndpointId = (endpointId ?? string.Empty).Trim();
        Kind = kind;
        Position = position;
        if (EndpointId.Length == 0) throw new ArgumentException("Endpoint id is required.", nameof(endpointId));
        if (kind != RemoteResourceSourceKind.StaticContainer && kind != RemoteResourceSourceKind.WorkstationOutput)
            throw new ArgumentOutOfRangeException(nameof(kind), "Only static containers and workstation outputs are supported.");
    }

    public int GetAvailableDebitQuantity(string itemKey)
    {
        IRemoteResourceSource source;
        string error;
        if (!TryResolveSource(out source, out error)) return 0;
        int itemType;
        if (!TryResolveItemType(itemKey, out itemType, out error)) return 0;
        int total = CountFungible(source.Slots, source.SlotLocks, itemType);
        lock (sync)
        {
            int reserved;
            debitByItem.TryGetValue(itemKey, out reserved);
            return Math.Max(0, total - reserved);
        }
    }

    public int GetAvailableCreditQuantity(string itemKey)
    {
        if (Kind == RemoteResourceSourceKind.WorkstationOutput) return 0;
        IRemoteResourceSource source;
        string error;
        if (!TryResolveSource(out source, out error)) return 0;
        ItemValue itemValue = ItemClass.GetItem(itemKey, false);
        if (itemValue.IsEmpty()) return 0;
        int capacity = ComputeCreditCapacity(source.Slots, source.SlotLocks, itemValue);
        lock (sync)
        {
            int reserved;
            creditByItem.TryGetValue(itemKey, out reserved);
            return Math.Max(0, capacity - Math.Max(reserved, totalReservedCredit));
        }
    }

    public bool TryReserveDebit(Guid transactionId, string itemKey, int quantity,
        out IRebirthNpcExternalInventoryReservation reservation, out string error)
    {
        reservation = null;
        itemKey = (itemKey ?? string.Empty).Trim();
        if (!ValidateReservation(transactionId, itemKey, quantity, out error)) return false;
        IRemoteResourceSource source;
        if (!TryResolveSource(out source, out error)) return Reject();
        int itemType;
        if (!TryResolveItemType(itemKey, out itemType, out error)) return Reject();
        lock (sync)
        {
            if (debitReservations.ContainsKey(transactionId) || creditReservations.ContainsKey(transactionId))
            { error = "Transaction already owns a physical-resource reservation."; return Reject(); }
            int available = CountFungible(source.Slots, source.SlotLocks, itemType);
            int existing;
            debitByItem.TryGetValue(itemKey, out existing);
            if (available - existing < quantity)
            { error = "Physical source has insufficient unreserved quantity."; return Reject(); }
            debitReservations[transactionId] = quantity;
            debitByItem[itemKey] = existing + quantity;
        }
        Interlocked.Increment(ref reservations);
        reservation = new Reservation(this, transactionId, itemKey, quantity, true, source.Revision);
        return true;
    }

    public bool TryReserveCredit(Guid transactionId, string itemKey, int quantity,
        out IRebirthNpcExternalInventoryReservation reservation, out string error)
    {
        reservation = null;
        itemKey = (itemKey ?? string.Empty).Trim();
        if (!ValidateReservation(transactionId, itemKey, quantity, out error)) return false;
        if (Kind == RemoteResourceSourceKind.WorkstationOutput)
        { error = "Workstation output endpoints are debit-only."; return Reject(); }
        IRemoteResourceSource source;
        if (!TryResolveSource(out source, out error)) return Reject();
        ItemValue itemValue = ItemClass.GetItem(itemKey, false);
        if (itemValue.IsEmpty())
        { error = "Unknown item key: " + itemKey; return Reject(); }
        lock (sync)
        {
            if (debitReservations.ContainsKey(transactionId) || creditReservations.ContainsKey(transactionId))
            { error = "Transaction already owns a physical-resource reservation."; return Reject(); }
            int capacity = ComputeCreditCapacity(source.Slots, source.SlotLocks, itemValue);
            int existing;
            creditByItem.TryGetValue(itemKey, out existing);
            if (capacity - totalReservedCredit < quantity)
            { error = "Physical destination has insufficient unreserved capacity."; return Reject(); }
            creditReservations[transactionId] = quantity;
            creditByItem[itemKey] = existing + quantity;
            totalReservedCredit += quantity;
        }
        Interlocked.Increment(ref reservations);
        reservation = new Reservation(this, transactionId, itemKey, quantity, false, source.Revision);
        return true;
    }

    private bool Commit(Guid transactionId, string itemKey, int quantity, bool debit,
        int expectedRevision, out string error)
    {
        error = string.Empty;
        IRemoteResourceSource source;
        if (!TryResolveSource(out source, out error)) return false;
        if (source.Revision != expectedRevision)
        { error = "Physical endpoint revision changed before commit."; return false; }
        ItemValue itemValue = ItemClass.GetItem(itemKey, false);
        if (itemValue.IsEmpty())
        { error = "Unknown item key: " + itemKey; return false; }
        ItemStack[] slots = source.Slots;
        if (slots == null) { error = "Physical endpoint has no live slots."; return false; }
        ItemStack[] original = ItemStack.Clone((IList<ItemStack>)slots);
        ItemStack[] proposed = ItemStack.Clone((IList<ItemStack>)slots);
        bool planned = debit
            ? TryPlanDebit(proposed, source.SlotLocks, itemValue.type, quantity)
            : TryPlanCredit(proposed, source.SlotLocks, itemValue, quantity);
        if (!planned)
        { error = debit ? "Physical debit plan no longer fits live inventory." : "Physical credit plan no longer fits live inventory."; return false; }

        try
        {
            for (int i = 0; i < proposed.Length; i++)
            {
                if (StacksEqual(slots[i], proposed[i])) continue;
                source.SetSlot(i, proposed[i] ?? ItemStack.Empty);
            }
            source.MarkModified();
            Interlocked.Increment(ref commits);
            return true;
        }
        catch (Exception ex)
        {
            bool restored = true;
            bool indeterminate = false;
            string restoreDetail = string.Empty;
            ItemStack[] live = source.Slots;
            for (int i = 0; i < original.Length; i++)
            {
                // Restore only a slot that still contains the exact value this transaction
                // proposed. If another actor changed it after our partial write, ownership of
                // that slot is indeterminate and overwriting it would destroy unrelated state.
                if (StacksEqual(original[i], proposed[i])) continue;
                if (live == null || i >= live.Length || !StacksEqual(live[i], proposed[i]))
                {
                    restored = false; indeterminate = true;
                    if (restoreDetail.Length == 0) restoreDetail = "slot " + i + " no longer matches owned mutation";
                    continue;
                }
                try { source.SetSlot(i, original[i] ?? ItemStack.Empty); }
                catch (Exception restoreEx)
                {
                    restored = false; indeterminate = true;
                    if (restoreDetail.Length == 0) restoreDetail = restoreEx.GetType().Name + ": " + restoreEx.Message;
                }
            }
            try { source.MarkModified(); } catch { restored = false; indeterminate = true; }
            error = restored && !indeterminate
                ? "Physical endpoint mutation failed and was restored: " + ex.GetType().Name + ": " + ex.Message
                : "Physical endpoint mutation failed and rollback is indeterminate: " + ex.GetType().Name + ": " + ex.Message + "; rollback=" + restoreDetail;
            return false;
        }
    }

    private bool TryResolveSource(out IRemoteResourceSource source, out string error)
    {
        source = null;
        error = string.Empty;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) { error = "World is unavailable."; return false; }
        source = Kind == RemoteResourceSourceKind.WorkstationOutput
            ? (IRemoteResourceSource)new WorkstationOutputResourceSource(world, Position)
            : new StaticContainerResourceSource(world, Position);
        if (!source.IsLoaded) { error = "Physical endpoint is not loaded."; return false; }
        if (source.IsBusy) { error = "Physical endpoint is open, reserved, or being edited."; return false; }
        if (source.IsExcluded) { error = "Physical endpoint is excluded from resource automation."; return false; }
        if (!source.IsActivated) { error = "Physical endpoint has not been activated by player deposit."; return false; }
        return true;
    }

    private static bool ValidateReservation(Guid transactionId, string itemKey, int quantity, out string error)
    {
        error = string.Empty;
        if (transactionId == Guid.Empty || itemKey.Length == 0 || quantity <= 0)
        { error = "Invalid physical-resource reservation."; return false; }
        return true;
    }

    private bool Reject() { Interlocked.Increment(ref rejections); return false; }

    private void Release(Guid transactionId, string itemKey, int quantity, bool debit)
    {
        lock (sync)
        {
            Dictionary<Guid, int> transactions = debit ? debitReservations : creditReservations;
            Dictionary<string, int> byItem = debit ? debitByItem : creditByItem;
            if (!transactions.Remove(transactionId)) return;
            if (!debit) totalReservedCredit = Math.Max(0, totalReservedCredit - quantity);
            int value;
            if (byItem.TryGetValue(itemKey, out value))
            {
                value -= quantity;
                if (value <= 0) byItem.Remove(itemKey); else byItem[itemKey] = value;
            }
        }
    }

    public string GetReport()
    {
        lock (sync)
            return "endpoint=" + EndpointId + " kind=" + Kind + " pos=" + Position +
                " debitActive=" + debitReservations.Count + " creditActive=" + creditReservations.Count +
                " reservations=" + Interlocked.Read(ref reservations) + " commits=" + Interlocked.Read(ref commits) +
                " rollbacks=" + Interlocked.Read(ref rollbacks) + " rejected=" + Interlocked.Read(ref rejections);
    }

    private static bool TryResolveItemType(string itemKey, out int itemType, out string error)
    {
        itemType = 0;
        error = string.Empty;
        ItemValue value = ItemClass.GetItem(itemKey, false);
        if (value.IsEmpty()) { error = "Unknown item key: " + itemKey; return false; }
        itemType = value.type;
        return true;
    }

    private static int CountFungible(ItemStack[] slots, PackedBoolArray locks, int itemType)
    {
        int total = 0;
        if (slots == null) return 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            ItemStack stack = slots[i];
            if (stack != null && !stack.IsEmpty() && stack.itemValue.type == itemType &&
                IsFungible(stack.itemValue))
                total = total > int.MaxValue - stack.count ? int.MaxValue : total + stack.count;
        }
        return total;
    }

    private static bool IsFungible(ItemValue value)
    {
        if (value == null || value.IsEmpty()) return false;
        if (value.Metadata != null && value.Metadata.Count > 0) return false;
        if (value.modifications != null)
            for (int i = 0; i < value.modifications.Length; i++)
                if (value.modifications[i] != null && !value.modifications[i].IsEmpty()) return false;
        if (value.cosmeticMods != null)
            for (int i = 0; i < value.cosmeticMods.Length; i++)
                if (value.cosmeticMods[i] != null && !value.cosmeticMods[i].IsEmpty()) return false;
        if (value.Quality > 0 || Math.Abs(value.UseTimes) > 0.0001f) return false;
        return true;
    }

    private static int Count(ItemStack[] slots, PackedBoolArray locks, int itemType)
    {
        int total = 0;
        if (slots == null) return 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            ItemStack stack = slots[i];
            if (stack != null && !stack.IsEmpty() && stack.itemValue.type == itemType)
                total = total > int.MaxValue - stack.count ? int.MaxValue : total + stack.count;
        }
        return total;
    }

    private static int ComputeCreditCapacity(ItemStack[] slots, PackedBoolArray locks, ItemValue value)
    {
        if (slots == null || value == null || value.IsEmpty()) return 0;
        int max = ItemClass.GetForId(value.type).Stacknumber.Value;
        long capacity = 0;
        ItemStack probe = new ItemStack(value.Clone(), 1);
        for (int i = 0; i < slots.Length; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) capacity += max;
            else if (stack.CanStackPartlyWith(probe, out int ignored)) capacity += Math.Max(0, max - stack.count);
            if (capacity >= int.MaxValue) return int.MaxValue;
        }
        return (int)capacity;
    }

    private static bool TryPlanDebit(ItemStack[] slots, PackedBoolArray locks, int itemType, int quantity)
    {
        int remaining = quantity;
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue.type != itemType ||
                !IsFungible(stack.itemValue)) continue;
            int take = Math.Min(remaining, stack.count);
            remaining -= take;
            int left = stack.count - take;
            slots[i] = left > 0 ? new ItemStack(stack.itemValue.Clone(), left) : ItemStack.Empty;
        }
        return remaining == 0;
    }

    private static bool TryPlanCredit(ItemStack[] slots, PackedBoolArray locks, ItemValue value, int quantity)
    {
        int remaining = quantity;
        ItemStack probe = new ItemStack(value.Clone(), 1);
        int max = ItemClass.GetForId(value.type).Stacknumber.Value;
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            if (!stack.CanStackPartlyWith(probe, out int ignored)) continue;
            int add = Math.Min(remaining, Math.Max(0, max - stack.count));
            if (add <= 0) continue;
            ItemStack next = stack.Clone(); next.count += add; slots[i] = next; remaining -= add;
        }
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (locks != null && i < locks.Length && locks[i]) continue;
            if (slots[i] != null && !slots[i].IsEmpty()) continue;
            int add = Math.Min(remaining, max);
            slots[i] = new ItemStack(value.Clone(), add); remaining -= add;
        }
        return remaining == 0;
    }

    private static bool StacksEqual(ItemStack a, ItemStack b)
    {
        bool ae = a == null || a.IsEmpty();
        bool be = b == null || b.IsEmpty();
        if (ae || be) return ae == be;
        return a.count == b.count && a.itemValue != null && b.itemValue != null &&
            a.itemValue.Equals(b.itemValue);
    }
}

public static class RebirthNpcPhysicalResourceEndpointRegistry
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthNpcPhysicalResourceEndpoint> Endpoints =
        new Dictionary<string, RebirthNpcPhysicalResourceEndpoint>(StringComparer.OrdinalIgnoreCase);

    public static RebirthNpcPhysicalResourceEndpoint Ensure(string token, out string error)
    {
        error = string.Empty;
        RemoteResourceSourceKind kind;
        Vector3i position;
        string endpointId;
        if (!TryParseToken(token, out kind, out position, out endpointId, out error)) return null;
        lock (Sync)
        {
            RebirthNpcPhysicalResourceEndpoint endpoint;
            if (!Endpoints.TryGetValue(endpointId, out endpoint))
            {
                endpoint = new RebirthNpcPhysicalResourceEndpoint(endpointId, kind, position);
                Endpoints.Add(endpointId, endpoint);
                RebirthNpcExternalInventoryEndpointRegistry.Register(endpoint);
            }
            return endpoint;
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC Physical Resource Endpoints] registered=").Append(Endpoints.Count);
            foreach (RebirthNpcPhysicalResourceEndpoint endpoint in Endpoints.Values)
                b.AppendLine().Append("  ").Append(endpoint.GetReport());
            return b.ToString();
        }
    }

    private static bool TryParseToken(string token, out RemoteResourceSourceKind kind,
        out Vector3i position, out string endpointId, out string error)
    {
        kind = 0; position = Vector3i.zero; endpointId = string.Empty; error = string.Empty;
        token = (token ?? string.Empty).Trim();
        int split = token.IndexOf('@');
        if (split <= 0 || split >= token.Length - 1)
        { error = "Physical endpoint must use S@x,y,z or W@x,y,z."; return false; }
        string prefix = token.Substring(0, split);
        kind = string.Equals(prefix, "S", StringComparison.OrdinalIgnoreCase)
            ? RemoteResourceSourceKind.StaticContainer
            : string.Equals(prefix, "W", StringComparison.OrdinalIgnoreCase)
                ? RemoteResourceSourceKind.WorkstationOutput : 0;
        if (kind == 0) { error = "Physical endpoint kind must be S or W."; return false; }
        string[] xyz = token.Substring(split + 1).Split(',');
        int x, y, z;
        if (xyz.Length != 3 || !int.TryParse(xyz[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
            !int.TryParse(xyz[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
            !int.TryParse(xyz[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out z))
        { error = "Physical endpoint coordinates are invalid."; return false; }
        position = new Vector3i(x, y, z);
        endpointId = "resource:" + char.ToUpperInvariant(prefix[0]) + "@" + x + "," + y + "," + z;
        return true;
    }
}

public sealed class RebirthNpcPhysicalResourceHaulingPlanProvider : IRebirthNpcHaulingPlanProvider
{
    public int Priority { get { return 600; } }

    public bool TryPlan(RebirthNpcConcreteWorkContext context, out RebirthNpcHaulingPlan plan, out string detail)
    {
        plan = null; detail = string.Empty;
        string key = context?.Assignment?.TargetKey ?? string.Empty;
        if (!key.StartsWith("haul:resource:", StringComparison.OrdinalIgnoreCase)) return false;
        string[] p = key.Split(':');
        if (p.Length < 7)
        { detail = "Physical hauling target must be haul:resource:<S@x,y,z|W@x,y,z>:<S@x,y,z>:<item>:<quantity>:<partial|exact>."; return false; }
        string error;
        RebirthNpcPhysicalResourceEndpoint source = RebirthNpcPhysicalResourceEndpointRegistry.Ensure(p[2], out error);
        if (source == null) { detail = "Source endpoint invalid: " + error; return false; }
        RebirthNpcPhysicalResourceEndpoint destination = RebirthNpcPhysicalResourceEndpointRegistry.Ensure(p[3], out error);
        if (destination == null) { detail = "Destination endpoint invalid: " + error; return false; }
        if (destination.Kind == RemoteResourceSourceKind.WorkstationOutput)
        { detail = "Workstation output endpoints cannot be hauling destinations."; return false; }
        int quantity;
        if (string.IsNullOrWhiteSpace(p[4]) || !int.TryParse(p[5], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
        { detail = "Physical hauling item or quantity is invalid."; return false; }
        bool partial = string.Equals(p[6], "partial", StringComparison.OrdinalIgnoreCase);
        if (!partial && !string.Equals(p[6], "exact", StringComparison.OrdinalIgnoreCase))
        { detail = "Physical hauling mode must be partial or exact."; return false; }
        plan = new RebirthNpcHaulingPlan
        {
            SourceEndpointId = source.EndpointId,
            DestinationEndpointId = destination.EndpointId,
            ItemKey = p[4].Trim(),
            Quantity = quantity,
            AllowPartial = partial
        };
        detail = "Physical resource hauling plan resolved.";
        return true;
    }
}
