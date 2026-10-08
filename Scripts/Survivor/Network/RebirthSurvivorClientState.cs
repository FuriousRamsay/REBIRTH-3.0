using System;
using System.Collections.Generic;
using System.Threading;

#nullable disable

public static class RebirthSurvivorClientState
{
    private static readonly object Sync = new object();
    private static RebirthSurvivorOwnerStateSnapshot ownerState;
    private static RebirthSurvivorOwnerScalars ownerScalars;
    private static object ownerWorld;
    private static EntityPlayer ownerPlayer;
    private static long projectionGeneration;
    private static bool ownerNotificationPending;
    private static readonly Queue<RebirthSurvivorCreationNetworkResponse> PendingCreationNotifications = new Queue<RebirthSurvivorCreationNetworkResponse>();
    private static readonly Dictionary<ulong, RebirthSurvivorCreationNetworkResponse> CreationResults = new Dictionary<ulong, RebirthSurvivorCreationNetworkResponse>();
    private static RebirthSurvivorCreationNetworkResponse lastCreationResult;
    private static long requestCounter = DateTime.UtcNow.Ticks & long.MaxValue;

    public static event Action<RebirthSurvivorOwnerStateSnapshot> OwnerStateChanged;
    public static event Action OwnerProjectionChanged;
    public static event Action<RebirthSurvivorCreationNetworkResponse> CreationResultReceived;

    public static void Reset()
    {
        RebirthLearningChallenges.Reset();
        lock (Sync)
        {
            ownerState = null;
            ownerScalars = null; ownerWorld = null; ownerPlayer = null;
            unchecked { projectionGeneration++; }
            ownerNotificationPending = true;
            PendingCreationNotifications.Clear();
            CreationResults.Clear();
            lastCreationResult = null;
        }
    }

    public static ulong NextRequestId()
    {
        long next = Interlocked.Increment(ref requestCounter);
        if (next <= 0L)
        {
            Interlocked.Exchange(ref requestCounter, DateTime.UtcNow.Ticks & long.MaxValue);
            next = Interlocked.Increment(ref requestCounter);
        }
        return (ulong)next;
    }

    public static RebirthSurvivorOwnerStateSnapshot GetOwnerStateSnapshot()
    {
        lock (Sync) return ownerState != null ? ownerState.Clone() : null;
    }

    // Creation/pre-spawn has no EntityPlayer yet and deliberately uses the separate unscoped
    // creation-state API. Gameplay and HUD projections must match the current local owner.
    private static bool ScopeMatches(World world, EntityPlayer player)
    {
        if (ownerState == null || world == null || player == null || !ReferenceEquals(player.world, world)) return false;
        if (ownerWorld != null && !ReferenceEquals(ownerWorld, world)) return false;
        if (ownerPlayer != null && !ReferenceEquals(ownerPlayer, player)) return false;
        if (ownerWorld == null) ownerWorld = world;
        if (ownerPlayer == null) ownerPlayer = player;
        return true;
    }

    public static RebirthSurvivorOwnerHeader GetOwnerHeader()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        lock (Sync) return new RebirthSurvivorOwnerHeader(projectionGeneration,
            ScopeMatches(world, player) ? ownerState : null, world, player);
    }

    public static RebirthSurvivorOwnerStateSnapshot GetOwnerStateSnapshot(out RebirthSurvivorOwnerHeader header)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        lock (Sync)
        {
            bool valid = ScopeMatches(world, player);
            header = new RebirthSurvivorOwnerHeader(projectionGeneration, valid ? ownerState : null, world, player);
            return valid ? ownerState.Clone() : null;
        }
    }

    public static bool TryGetOwnerScalars(EntityPlayer player, out RebirthSurvivorOwnerScalars scalars)
    {
        scalars = null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || player == null || !ReferenceEquals(world.GetPrimaryPlayer(), player)) return false;
        lock (Sync)
        {
            if (!ScopeMatches(world, player) || !ownerState.RebirthModeEnabled || !ownerState.HasCharacter
                || !ownerState.DefinitionsCompatible) return false;
            scalars = ownerScalars;
            return scalars != null;
        }
    }

    /// <summary>
    /// Called by the main-thread lifecycle pump, not a network callback. Multiple packets in a
    /// tick collapse to the latest owner projection. Each legacy listener gets its own detached
    /// value and cannot corrupt the next listener's input. Creation responses are not coalesced.
    /// </summary>
    public static void PumpNotifications()
    {
        RebirthLearningChallenges.Tick();
        RebirthSurvivorOwnerStateSnapshot accepted = null;
        RebirthSurvivorCreationNetworkResponse[] responses = null;
        bool notifyOwner;
        lock (Sync)
        {
            notifyOwner = ownerNotificationPending;
            if (!notifyOwner && PendingCreationNotifications.Count == 0) return;
            ownerNotificationPending = false;
            if (notifyOwner && ownerState != null) accepted = ownerState.Clone();
            if (PendingCreationNotifications.Count > 0)
            {
                responses = PendingCreationNotifications.ToArray();
                PendingCreationNotifications.Clear();
            }
        }
        // Creation acceptance must be consumed before Ready listeners clear pending UI state.
        if (responses != null)
        {
            Action<RebirthSurvivorCreationNetworkResponse> handler = CreationResultReceived;
            if (handler != null) foreach (RebirthSurvivorCreationNetworkResponse response in responses)
                foreach (Action<RebirthSurvivorCreationNetworkResponse> listener in handler.GetInvocationList())
                    try { listener(response.Clone()); }
                    catch (Exception ex) { Log.Warning("[REBIRTH Survivor] creation-result listener failed: " + ex.Message); }
        }
        if (notifyOwner)
        {
            try
            {
                World world = GameManager.Instance != null ? GameManager.Instance.World : null;
                EntityPlayerLocal local = world != null ? world.GetPrimaryPlayer() : null;
                RebirthSurvivorOwnerScalars ignored;
                if (local != null && accepted != null && TryGetOwnerScalars(local, out ignored))
                {
                    RebirthSurvivorGearService.ReconcilePhysicalBagCapacity(local, accepted.PhysicalBagSlots, false);
                    RebirthLearningChallenges.Observe(accepted);
                }
            }
            catch (Exception ex) { Log.Warning("[REBIRTH Survivor] owner-state backpack reconciliation failed: " + ex.Message); }
            Action<RebirthSurvivorOwnerStateSnapshot> handler = OwnerStateChanged;
            if (handler != null) foreach (Action<RebirthSurvivorOwnerStateSnapshot> listener in handler.GetInvocationList())
                try { listener(accepted != null ? accepted.Clone() : null); }
                catch (Exception ex) { Log.Warning("[REBIRTH Survivor] owner-state listener failed: " + ex.Message); }
            Action projectionHandler = OwnerProjectionChanged;
            if (projectionHandler != null) foreach (Action listener in projectionHandler.GetInvocationList())
                try { listener(); }
                catch (Exception ex) { Log.Warning("[REBIRTH Survivor] projection listener failed: " + ex.Message); }
        }
    }

    /// <summary>
    /// Allocation-free hot-path projection used by the creation hold. UI callers that need
    /// display data should continue to request the cloned owner snapshot.
    /// </summary>
    public static bool TryGetOwnerCreationState(out bool rebirthModeEnabled, out RebirthSurvivorOwnerCreationState creationState)
    {
        lock (Sync)
        {
            if (ownerState == null)
            {
                rebirthModeEnabled = false;
                creationState = RebirthSurvivorOwnerCreationState.NotApplicable;
                return false;
            }
            rebirthModeEnabled = ownerState.RebirthModeEnabled;
            creationState = ownerState.CreationState;
            return true;
        }
    }

    public static bool TryGetProjectedPhysicalBagSlots(out int slots)
    {
        lock (Sync)
        {
            if (ownerState == null)
            {
                slots = 0;
                return false;
            }
            if (!ownerState.RebirthModeEnabled || !ownerState.HasCharacter)
            {
                slots = RebirthSurvivorGearService.BasePhysicalBagSlots;
                return true;
            }
            int value = ownerState.PhysicalBagSlots;
            slots = value >= RebirthSurvivorGearService.BasePhysicalBagSlots && value <= RebirthSurvivorGearService.MaxPhysicalBagSlots
                ? value
                : RebirthSurvivorGearService.BasePhysicalBagSlots;
            return true;
        }
    }

    public static bool TryGetProjectedPhysicalBagSlots(EntityPlayer player, out int slots)
    {
        slots = 0;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || player == null || !ReferenceEquals(world.GetPrimaryPlayer(), player)) return false;
        lock (Sync)
        {
            if (!ScopeMatches(world, player) || !ownerState.DefinitionsCompatible) return false;
            return TryGetProjectedPhysicalBagSlots(out slots);
        }
    }

    public static int GetProjectedToolbeltBonus(EntityPlayer player)
    {
        lock (Sync)
        {
            if (player == null || player.world == null || !ReferenceEquals(player.world.GetPrimaryPlayer(), player)
                || ownerState == null || !ScopeMatches(player.world, player) || !ownerState.DefinitionsCompatible
                || !ownerState.RebirthModeEnabled || !ownerState.HasCharacter) return 0;
            foreach (var gear in ownerState.GearSlots)
                if (gear.SlotId == RebirthSurvivorGearService.BeltSlotId)
                    return RebirthSurvivorGearService.ToolbeltBonusForItem(gear.ItemId);
            return 0;
        }
    }

    public static bool HasProjectedGear(EntityPlayer player, string slotId, string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && GetProjectedGearItem(player, slotId) == itemId;
    }

    public static string GetProjectedCreationId(EntityPlayer player)
    {
        lock (Sync)
        {
            if (player == null || player.world == null || !player.world.IsRemote()
                || !ReferenceEquals(player.world.GetPrimaryPlayer(), player)
                || ownerState == null || !ScopeMatches(player.world, player) || !ownerState.DefinitionsCompatible
                || !ownerState.RebirthModeEnabled || !ownerState.HasCharacter) return string.Empty;
            return ownerState.CreationId ?? string.Empty;
        }
    }

    public static bool HasProjectedDiscipline(EntityPlayer player, string disciplineId)
    {
        lock (Sync)
        {
            if (string.IsNullOrEmpty(disciplineId) || player == null || player.world == null
                || !player.world.IsRemote() || !ReferenceEquals(player.world.GetPrimaryPlayer(), player)
                || ownerState == null || !ScopeMatches(player.world, player) || !ownerState.DefinitionsCompatible
                || !ownerState.RebirthModeEnabled || !ownerState.HasCharacter) return false;
            foreach (string acquired in ownerState.AcquiredDisciplineIds)
                if (string.Equals(acquired, disciplineId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public static string GetProjectedGearItem(EntityPlayer player, string slotId)
    {
        lock (Sync)
        {
            if (player == null || player.world == null || !ReferenceEquals(player.world.GetPrimaryPlayer(), player)
                || ownerState == null || !ScopeMatches(player.world, player) || !ownerState.DefinitionsCompatible
                || !ownerState.RebirthModeEnabled || !ownerState.HasCharacter) return string.Empty;
            foreach (var gear in ownerState.GearSlots)
                if (gear.SlotId == slotId) return gear.ItemId ?? string.Empty;
            return string.Empty;
        }
    }

    public static int GetProjectedPhysicalBagSlots()
    {
        int slots;
        return TryGetProjectedPhysicalBagSlots(out slots) ? slots : RebirthSurvivorGearService.BasePhysicalBagSlots;
    }


    public static RebirthSurvivorCreationNetworkResponse GetLastCreationResult()
    {
        lock (Sync) return lastCreationResult != null ? lastCreationResult.Clone() : null;
    }

    public static bool TryGetCreationResult(ulong requestId, out RebirthSurvivorCreationNetworkResponse response)
    {
        lock (Sync)
        {
            RebirthSurvivorCreationNetworkResponse found;
            if (CreationResults.TryGetValue(requestId, out found) && found != null)
            {
                response = found.Clone();
                return true;
            }
        }
        response = null;
        return false;
    }

    internal static void ReceiveOwnerState(RebirthSurvivorOwnerStateSnapshot incoming)
    {
        if (incoming == null || incoming.ProtocolVersion != RebirthSurvivorNetworkProtocol.Version)
            return;
        if (incoming.HasCharacter && (incoming.GearRevision < 0 || !RebirthSurvivorRequestScope.Matches(incoming.CreationId, incoming.CreationId)))
            return;

        lock (Sync)
        {
            if (ownerState != null)
            {
                // Base Game mode is an authoritative clear. Otherwise a no-character/stale
                // packet must never erase a committed character projection.
                if (incoming.RebirthModeEnabled && ownerState.HasCharacter && !incoming.HasCharacter)
                    return;
                if (ownerState.RebirthModeEnabled == incoming.RebirthModeEnabled &&
                    ownerState.HasCharacter == incoming.HasCharacter &&
                    string.Equals(ownerState.CreationId, incoming.CreationId, StringComparison.OrdinalIgnoreCase) &&
                    (incoming.CharacterRevision < ownerState.CharacterRevision || incoming.GearRevision < ownerState.GearRevision))
                    return;
            }
            ownerState = incoming.Clone();
            ownerScalars = new RebirthSurvivorOwnerScalars(ownerState);
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            ownerWorld = world;
            ownerPlayer = world != null ? world.GetPrimaryPlayer() : null;
            unchecked { projectionGeneration++; }
            ownerNotificationPending = true;
        }
    }

    internal static void ReceiveCreationResult(RebirthSurvivorCreationNetworkResponse incoming)
    {
        if (incoming == null || incoming.ProtocolVersion != RebirthSurvivorNetworkProtocol.Version || incoming.RequestId == 0UL)
            return;
        RebirthSurvivorCreationNetworkResponse accepted = incoming.Clone();
        lock (Sync)
        {
            CreationResults[incoming.RequestId] = accepted.Clone();
            lastCreationResult = accepted.Clone();
            // Keep this UI-facing cache bounded. IDs are monotonic within a client process;
            // oldest removal is intentionally simple because this is not authoritative state.
            if (CreationResults.Count > 64)
            {
                ulong smallest = ulong.MaxValue;
                foreach (ulong id in CreationResults.Keys) if (id < smallest) smallest = id;
                if (smallest != ulong.MaxValue) CreationResults.Remove(smallest);
            }
        }
        lock (Sync)
        {
            // Matches the bounded request-result cache. Querying by RequestId remains reliable
            // even when an inactive UI did not consume a notification.
            if (PendingCreationNotifications.Count >= 64) PendingCreationNotifications.Dequeue();
            PendingCreationNotifications.Enqueue(accepted);
        }
    }
}
