using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#nullable disable

// B09 exact container-cell selection. Server-side, volatile and NON-MUTATING: it only reads native
// containers and never touches an inventory slot, so a UI gesture (click on a source cell, click on an
// empty destination cell) can be recorded without the inventory action the native item-stack handler
// would perform first. The only mutating step is TryCommit, which forwards to the existing
// authenticated RebirthNpcStorageOrganizationWork.Assign (which revalidates everything itself).
public enum RebirthNpcB09SelectionCode : byte
{
    Ok = 0, NoSession, NotAuthorized, NotServer, InvalidCell, NotNamedOwnedContainer, ContainerBusy,
    SlotLocked, SourceEmpty, SourceNotMovable, DestinationOccupied, SameContainer, TooFarFromOwner,
    ContainersTooFarApart, CellReserved, SourceNotSelected, DestinationNotSelected, AlreadyWorking, AssignFailed
}

public struct RebirthNpcB09SelectionResult
{
    public bool Ok;
    public RebirthNpcB09SelectionCode Code;
    public string Detail;
    internal static RebirthNpcB09SelectionResult Fail(RebirthNpcB09SelectionCode code, string detail)=>
        new RebirthNpcB09SelectionResult{Ok=false,Code=code,Detail=detail};
    internal static RebirthNpcB09SelectionResult Success(string detail)=>
        new RebirthNpcB09SelectionResult{Ok=true,Code=RebirthNpcB09SelectionCode.Ok,Detail=detail};
}

public struct RebirthNpcB09SelectionSnapshot
{
    public bool HasSource, HasDestination;
    public Vector3i SourcePosition, DestinationPosition;
    public int SourceSlot, DestinationSlot;
}

public static class RebirthNpcB09SelectionAdapter
{
    private sealed class Entry
    {
        public bool HasSource, HasDestination;
        public Vector3i Source, Destination;
        public int SourceSlot, DestinationSlot;
        public string SourceIncarnation, DestinationIncarnation;
        public long ExpiresTicks;
    }
    private const int MaxSessions = 64;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private static readonly object Sync = new object();
    private static object lastWorld;
    private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

    public static void Clear(string session)
    {
        if (string.IsNullOrWhiteSpace(session)) return;
        lock (Sync) Entries.Remove(session.Trim());
    }
    public static void ResetForWorldChange(){lock (Sync) Entries.Clear();}

    public static bool TryGetSelection(string session, out RebirthNpcB09SelectionSnapshot snapshot)
    {
        snapshot = default(RebirthNpcB09SelectionSnapshot);
        if (string.IsNullOrWhiteSpace(session)) return false;
        lock (Sync)
        {
            if (!Entries.TryGetValue(session.Trim(), out var e) || e.ExpiresTicks <= DateTime.UtcNow.Ticks) return false;
            snapshot = new RebirthNpcB09SelectionSnapshot{HasSource=e.HasSource,HasDestination=e.HasDestination,
                SourcePosition=e.Source,DestinationPosition=e.Destination,SourceSlot=e.SourceSlot,DestinationSlot=e.DestinationSlot};
            return true;
        }
    }

    public static RebirthNpcB09SelectionResult TrySelectSourceCell(string session, Vector3i position, int slot)
    {
        if (!Begin(session, RebirthNpcInteractionAction.AssignWork, out var context, out var owner, out var failure)) return failure;
        var check = ValidateCell(owner, position, slot, true, out string incarnation);
        if (!check.Ok) return check;
        lock (Sync)
        {
            var e = Get(session.Trim());
            // Re-selecting a different source container drops a destination that is no longer a neighbour.
            e.Source = position; e.SourceSlot = slot; e.SourceIncarnation = incarnation; e.HasSource = true;
            if (e.HasDestination && !Neighbours(e.Source, e.Destination) && !WithinFar(e.Source, e.Destination)) e.HasDestination = false;
            if (e.HasDestination && e.Source == e.Destination) e.HasDestination = false;
        }
        return RebirthNpcB09SelectionResult.Success("Source cell selected.");
    }

    // Empty cells are valid here by design; the native item-stack selection path ignores them.
    public static RebirthNpcB09SelectionResult TrySelectDestinationCell(string session, Vector3i position, int slot)
    {
        if (!Begin(session, RebirthNpcInteractionAction.AssignWork, out var context, out var owner, out var failure)) return failure;
        RebirthNpcB09SelectionSnapshot snapshot;
        if (!TryGetSelection(session, out snapshot) || !snapshot.HasSource)
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.SourceNotSelected, "Select the source cell first.");
        if (position == snapshot.SourcePosition)
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.SameContainer, "Source and destination must be different containers.");
        if (!Neighbours(snapshot.SourcePosition, position) && !WithinFar(snapshot.SourcePosition, position))
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.ContainersTooFarApart,
                "Containers are too far apart for one hauling trip.");
        var check = ValidateCell(owner, position, slot, false, out string incarnation);
        if (!check.Ok) return check;
        lock (Sync)
        {
            var e = Get(session.Trim());
            e.Destination = position; e.DestinationSlot = slot; e.DestinationIncarnation = incarnation; e.HasDestination = true;
        }
        return RebirthNpcB09SelectionResult.Success("Destination cell selected.");
    }

    // Revalidates both endpoints against the live world, then creates the one saved assignment.
    public static RebirthNpcB09SelectionResult TryCommit(string session, out RebirthNpcWorkAssignment assignment)
    {
        assignment = null;
        if (!Begin(session, RebirthNpcInteractionAction.AssignWork, out var context, out var owner, out var failure)) return failure;
        Entry copy;
        lock (Sync)
        {
            if (!Entries.TryGetValue(session.Trim(), out var e) || e.ExpiresTicks <= DateTime.UtcNow.Ticks) copy = null;
            else copy = new Entry{HasSource=e.HasSource,HasDestination=e.HasDestination,Source=e.Source,Destination=e.Destination,
                SourceSlot=e.SourceSlot,DestinationSlot=e.DestinationSlot,SourceIncarnation=e.SourceIncarnation,DestinationIncarnation=e.DestinationIncarnation};
        }
        if (copy == null || !copy.HasSource) return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.SourceNotSelected, "Select the source cell first.");
        if (!copy.HasDestination) return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.DestinationNotSelected, "Select an empty destination cell.");
        if (HasActiveStorageAssignment(context.NpcId))
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.AlreadyWorking, "This worker already has a storage move.");
        var src = ValidateCell(owner, copy.Source, copy.SourceSlot, true, out string srcInc, true);
        if (!src.Ok) return src;
        var dst = ValidateCell(owner, copy.Destination, copy.DestinationSlot, false, out string dstInc, true);
        if (!dst.Ok) return dst;
        // The player's selection was made against these incarnations; a replaced container must be reselected.
        if (srcInc != copy.SourceIncarnation || dstInc != copy.DestinationIncarnation)
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.NotNamedOwnedContainer, "A selected container was replaced; select again.");
        var result = RebirthNpcStorageOrganizationWork.Assign(session.Trim(), copy.Source, copy.SourceSlot, copy.Destination, copy.DestinationSlot,
            out assignment, out string detail, !Neighbours(copy.Source, copy.Destination));
        if (result != RebirthNpcWorkAssignmentResult.Succeeded)
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.AssignFailed, string.IsNullOrEmpty(detail) ? result.ToString() : detail);
        Clear(session);
        return RebirthNpcB09SelectionResult.Success("Storage move assigned.");
    }

    // Cells claimed by every live (non-terminal, incl. Executing) storage assignment of any NPC ("x,y,z#slot").
    // Near, far and deposit targets are all parsed; a deposit has no source cell.
    internal static HashSet<string> ClaimedCells()
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var list = RebirthNpcWorkAssignmentService.GetAllActiveSnapshot();
        for (int i = 0; i < list.Length; i++)
        {
            var a = list[i];
            if (!RebirthNpcStorageOrganizationWork.Parse(a.DefinitionId, a.TargetKey, a.HasTargetPosition, a.TargetPosition, out var t)) continue;
            if (t.Source != null) claimed.Add(CellKey(t.Source.Position, t.SourceSlot));
            claimed.Add(CellKey(t.Destination.Position, t.DestinationSlot));
        }
        return claimed;
    }
    internal static string CellKey(Vector3 p, int slot)=>
        Mathf.FloorToInt(p.x).ToString(CultureInfo.InvariantCulture)+","+Mathf.FloorToInt(p.y).ToString(CultureInfo.InvariantCulture)+","+
        Mathf.FloorToInt(p.z).ToString(CultureInfo.InvariantCulture)+"#"+slot.ToString(CultureInfo.InvariantCulture);
    internal static string CellKey(Vector3i p, int slot)=>CellKey(p.ToVector3(), slot);

    internal static bool HasActiveStorageAssignment(RebirthNpcStableId npc)
    {
        var list = RebirthNpcWorkAssignmentService.GetForNpc(npc, false);
        for (int i = 0; i < list.Length; i++) if (RebirthNpcStorageOrganizationWork.IsDefinition(list[i].DefinitionId)) return true;
        return false;
    }

    // Finished-work deposit: the bag stack comes from a saved P7 pickup receipt (phase completed); the
    // player only chooses an empty cell of an owned named container. Mutation is the P6 credit chain.
    public static RebirthNpcB09SelectionResult TryCommitDeposit(string session, Guid pickupOperation, Vector3i position, int slot,
        out RebirthNpcWorkAssignment assignment)
    {
        assignment = null;
        if (!Begin(session, RebirthNpcInteractionAction.AssignWork, out var context, out var owner, out var failure)) return failure;
        var check = ValidateCell(owner, position, slot, false, out _, true);
        if (!check.Ok) return check;
        var result = RebirthNpcStorageOrganizationWork.AssignDeposit(session.Trim(), pickupOperation, position, slot, out assignment, out string detail);
        return result == RebirthNpcWorkAssignmentResult.Succeeded ? RebirthNpcB09SelectionResult.Success("Deposit assigned.") :
            RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.AssignFailed, string.IsNullOrEmpty(detail) ? result.ToString() : detail);
    }

    internal static bool WithinFar(Vector3i a, Vector3i b)=>
        (a.ToVector3() - b.ToVector3()).sqrMagnitude <= RebirthNpcStorageOrganizationWork.FarMaxSeparation * RebirthNpcStorageOrganizationWork.FarMaxSeparation;

    private static bool Neighbours(Vector3i a, Vector3i b)
    {
        float separation = RebirthCompanionService.StorageDistance - RebirthNpcWorkRuntime.WorkRadius;
        return separation > 0f && (a.ToVector3() - b.ToVector3()).sqrMagnitude <= separation * separation;
    }

    private static Entry Get(string key)
    {
        long now = DateTime.UtcNow.Ticks;
        if (Entries.Count >= MaxSessions)
        {
            var dead = new List<string>();
            foreach (var kv in Entries) if (kv.Value.ExpiresTicks <= now) dead.Add(kv.Key);
            for (int i = 0; i < dead.Count; i++) Entries.Remove(dead[i]);
            if (Entries.Count >= MaxSessions && !Entries.ContainsKey(key))
            {
                string oldest = null; long best = long.MaxValue;
                foreach (var kv in Entries) if (kv.Value.ExpiresTicks < best) { best = kv.Value.ExpiresTicks; oldest = kv.Key; }
                if (oldest != null) Entries.Remove(oldest);
            }
        }
        if (!Entries.TryGetValue(key, out var e)) { e = new Entry(); Entries[key] = e; }
        e.ExpiresTicks = now + Lifetime.Ticks;
        return e;
    }

    private static bool Begin(string session, RebirthNpcInteractionAction action, out RebirthNpcInteractionContext context,
        out EntityPlayer owner, out RebirthNpcB09SelectionResult failure)
    {
        context = null; owner = null; failure = default(RebirthNpcB09SelectionResult);
        if (string.IsNullOrWhiteSpace(session))
        { failure = RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.NoSession, "No NPC interaction session."); return false; }
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote() || !ThreadManager.IsMainThread())
        { failure = RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.NotServer, "Selection is validated on the server main thread."); return false; }
        // Selections belong to one loaded world; drop them when the world instance changes (no external hook needed).
        lock (Sync) { if (!ReferenceEquals(lastWorld, world)) { Entries.Clear(); lastWorld = world; } }
        if (RebirthNpcInteractionSessionService.Authorize(session.Trim(), action, out context) != RebirthNpcInteractionResult.Allowed || context == null)
        { failure = RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.NotAuthorized, "Session is not allowed to assign Work."); return false; }
        owner = RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId);
        if (owner == null || owner.IsDead())
        { failure = RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.NotAuthorized, "Owner is unavailable."); return false; }
        return true;
    }

    // Read-only validation of one cell. isSource: occupied + movable; otherwise: empty + unlocked.
    internal static RebirthNpcB09SelectionResult ValidateCell(EntityPlayer owner, Vector3i position, int slot, bool isSource, out string incarnation, bool commit = false)
    {
        incarnation = null;
        var world = GameManager.Instance?.World;
        var tile = world?.GetTileEntity(position) as TileEntityComposite;
        var feature = tile?.GetFeature<TEFeatureRebirthContainerName>();
        if (feature == null || string.IsNullOrWhiteSpace(feature.CustomName))
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.NotNamedOwnedContainer, "Only named containers you own can be used.");
        // Selection may happen in the native window the owner has open (that makes the container "busy");
        // commit requires it closed because the worker will write it.
        if (!RebirthNpcStorageOrganizationWork.Named(position, owner, feature.Incarnation, !commit))
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.NotNamedOwnedContainer, "Container is not yours, not loaded, or excluded.");
        if (commit && !RebirthNpcStorageOrganizationWork.Named(position, owner, feature.Incarnation))
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.ContainerBusy, "Close the container (or wait until it is free) before sending the worker.");
        float reach = RebirthCompanionService.StorageDistance;
        if ((owner.position - position.ToVector3()).sqrMagnitude > reach * reach)
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.TooFarFromOwner, "Container is too far from you.");
        var source = new StaticContainerResourceSource(world, position);
        var slots = source.Slots; var locks = source.SlotLocks;
        if (slots == null || locks == null || slot < 0 || slot >= slots.Length || slot >= locks.Length || slot > 255)
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.InvalidCell, "That cell does not exist.");
        if (locks[slot])
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.SlotLocked, "That cell is locked.");
        var stack = slots[slot];
        bool empty = stack == null || stack.IsEmpty();
        if (isSource)
        {
            if (empty) return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.SourceEmpty, "The source cell is empty.");
            if (stack.itemValue?.ItemClass == null || stack.count < 1 || !stack.CanMoveTo(XUiC_ItemStack.StackLocationTypes.LootContainer) ||
                !stack.itemValue.ItemClass.CanPlaceInContainer())
                return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.SourceNotMovable, "That item cannot be moved by a worker.");
        }
        else if (!empty)
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.DestinationOccupied, "The destination cell must be empty.");
        var claimed = ClaimedCells();
        if (claimed.Contains(CellKey(position, slot)))
            return RebirthNpcB09SelectionResult.Fail(RebirthNpcB09SelectionCode.CellReserved, "Another storage move already uses that cell.");
        incarnation = feature.Incarnation;
        return RebirthNpcB09SelectionResult.Success("Cell valid.");
    }

}
