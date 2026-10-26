using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#nullable disable

// B09 general organization. The planner is a pure function of the live native containers and the
// player's rules: it never mutates a slot and keeps no state, so it is restart safe (every dispatch
// re-plans from the world; a completed move is simply no longer a misplaced item). It emits ONE step
// at a time, executed by the existing exact-cell executor (RebirthNpcStorageOrganizationWork.Assign).
public enum RebirthNpcB09RuleSource : byte { PlayerItemRule = 0, PlayerContainerTag = 1, ContainerNameMatch = 2, DefaultContainer = 3 }
public enum RebirthNpcB09SortOrder : byte { None = 0, ByName = 1, ByGroupThenName = 2 }
public enum RebirthNpcB09HoldReason : byte
{
    None = 0, NoDestinationRule, DestinationFull, ContainersTooFarApart, CellReserved, MergeNeedsNativeStackOriginal,SortCustodyHeld,NoFreeCellToRotate,
    PartialNeedsNativeStackOriginal, ContainerUnavailable, Preserved, SortTooLarge
}

public sealed class RebirthNpcB09ContainerSpec
{
    public Vector3i Position;
    public string[] PlayerTags;          // player-assigned tags (highest precedence after exact item rules)
    public string Incarnation;           // saved incarnation; a mismatch (replaced container) drops the spec
    public bool AcceptsDeposits = true;  // player can exclude a container from receiving
    public bool AllowsWithdrawals = true;// ...or from being emptied
}

public sealed class RebirthNpcB09Policy
{
    public RebirthNpcB09ContainerSpec[] Containers = new RebirthNpcB09ContainerSpec[0];
    // exact item name -> destination container position (player intent; beats every tag and default)
    public Dictionary<string, Vector3i> ItemRules = new Dictionary<string, Vector3i>(StringComparer.OrdinalIgnoreCase);
    // item name/group -> tag a destination container must carry
    public Dictionary<string, string> TagRules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public bool UseContainerNameMatch = true;
    public bool HasDefaultContainer;
    public Vector3i DefaultContainer;
    public HashSet<string> PreserveItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool SortWithinContainers;    // single-item moves between cells of one container (held until SortCustodyReady)
    public bool AllowDistant = true;     // loaded owned containers only; false holds far moves
    public bool Merge = true;            // prefer a destination that already holds the item
    public RebirthNpcB09SortOrder Sort = RebirthNpcB09SortOrder.ByGroupThenName;
}

public sealed class RebirthNpcB09Step
{
    public Vector3i SourcePosition, DestinationPosition;
    public int SourceSlot, DestinationSlot;
    public string ItemName;
    public RebirthNpcB09RuleSource Rule;
    public bool Merge;                   // credit into an identical-payload partial stack (MergeCustodyReady)
    public bool InContainer;             // same-container sort step (SourcePosition == DestinationPosition)
    public bool Far;                     // leg-aware distant hauling (storage-far)
    public bool AlreadyHoldsItem;        // destination also holds this item elsewhere (merge unavailable, see doc)
}

public sealed class RebirthNpcB09Held
{
    public Vector3i SourcePosition; public int SourceSlot; public string ItemName; public RebirthNpcB09HoldReason Reason;
}

public sealed class RebirthNpcB09Plan
{
    public List<RebirthNpcB09Step> Steps = new List<RebirthNpcB09Step>();
    public List<RebirthNpcB09Held> Held = new List<RebirthNpcB09Held>();
}

public static class RebirthNpcB09OrganizationPlanner
{
    private sealed class Box
    {
        public RebirthNpcB09ContainerSpec Spec; public string Name; public string Incarnation;
        public ItemStack[] Slots; public PackedBoolArray Locks; public HashSet<string> Items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    // Plans from the live world. owner must be the exact owner; every container must pass the same
    // named/owned/loaded/unbusy test the executor uses. Distant containers (beyond the executor's
    // one-arrival reach) are reported Held, never silently skipped.
    public static RebirthNpcB09Plan Plan(EntityPlayer owner, RebirthNpcB09Policy policy, ISet<string> claimedCells, int maxSteps)
    {
        var plan = new RebirthNpcB09Plan();
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote() || owner == null || policy?.Containers == null || maxSteps <= 0) return plan;
        claimedCells = claimedCells ?? new HashSet<string>(StringComparer.Ordinal);
        var boxes = new List<Box>();
        for (int i = 0; i < policy.Containers.Length; i++)
        {
            var spec = policy.Containers[i];
            if (spec == null) continue;
            var feature = (world.GetTileEntity(spec.Position) as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>();
            if (feature == null || (!string.IsNullOrEmpty(spec.Incarnation) && spec.Incarnation != feature.Incarnation) ||
                !RebirthNpcStorageOrganizationWork.Named(spec.Position, owner, feature.Incarnation)) continue;
            var source = new StaticContainerResourceSource(world, spec.Position);
            if (source.Slots == null || source.SlotLocks == null) continue;
            var box = new Box{Spec=spec,Name=feature.CustomName,Incarnation=feature.Incarnation,Slots=source.Slots,Locks=source.SlotLocks};
            for (int s = 0; s < box.Slots.Length; s++)
                if (box.Slots[s] != null && !box.Slots[s].IsEmpty()) { var n = ItemName(box.Slots[s]); if (n != null) box.Items.Add(n); }
            boxes.Add(box);
        }
        boxes.Sort((a, b) => Compare(a.Spec.Position, b.Spec.Position));
        var usedDestination = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<KeyValuePair<Box, int>>();
        foreach (var box in boxes)
        {
            if (!box.Spec.AllowsWithdrawals) continue;
            for (int s = 0; s < box.Slots.Length && s < box.Locks.Length && s <= 255; s++)
            {
                var stack = box.Slots[s];
                if (box.Locks[s] || stack == null || stack.IsEmpty()) continue;
                candidates.Add(new KeyValuePair<Box, int>(box, s));
            }
        }
        candidates.Sort((a, b) =>
        {
            int c = CompareItems(policy.Sort, a.Key.Slots[a.Value], b.Key.Slots[b.Value]);
            return c != 0 ? c : CompareCell(a, b);
        });
        foreach (var cand in candidates)
        {
            if (plan.Steps.Count >= maxSteps) break;
            var from = cand.Key; int slot = cand.Value; var stack = from.Slots[slot];
            string name = ItemName(stack);
            if (name == null) continue;
            if (policy.PreserveItems.Contains(name)) { plan.Held.Add(Hold(from, slot, name, RebirthNpcB09HoldReason.Preserved)); continue; }
            if (claimedCells.Contains(RebirthNpcB09SelectionAdapter.CellKey(from.Spec.Position, slot)))
            { plan.Held.Add(Hold(from, slot, name, RebirthNpcB09HoldReason.CellReserved)); continue; }
            var ranked = Rank(policy, boxes, stack, name, out var rule);
            if (ranked.Count == 0) { plan.Held.Add(Hold(from, slot, name, RebirthNpcB09HoldReason.NoDestinationRule)); continue; }
            bool alreadyThere = false;
            foreach (var r in ranked) if (r == from) { alreadyThere = true; break; }
            if (alreadyThere) continue; // already in an eligible container: player intent satisfied
            // The executor moves ONE item per assignment; without the merge credit each item would claim a fresh empty cell,
            // so a multi-item stack is held until MergeCustodyReady (items 2..n then merge into the first cell).
            if (stack.count > 1 && !RebirthNpcStorageOrganizationWork.MergeCustodyReady)
            { plan.Held.Add(Hold(from, slot, name, RebirthNpcB09HoldReason.MergeNeedsNativeStackOriginal)); continue; }
            RebirthNpcB09HoldReason why = RebirthNpcB09HoldReason.DestinationFull;
            bool placed = false;
            foreach (var to in ranked)
            {
                if (!to.Spec.AcceptsDeposits) continue;
                if (!Reaches(from, to, policy.AllowDistant, out bool far)) { why = RebirthNpcB09HoldReason.ContainersTooFarApart; continue; }
                int mergeCell = RebirthNpcStorageOrganizationWork.MergeCustodyReady ? MergeCell(to, stack, claimedCells, usedDestination) : -1;
                int empty = mergeCell >= 0 ? mergeCell : FreeCell(to, claimedCells, usedDestination);
                if (empty < 0) { if (why != RebirthNpcB09HoldReason.ContainersTooFarApart) why = RebirthNpcB09HoldReason.DestinationFull; continue; }
                usedDestination.Add(RebirthNpcB09SelectionAdapter.CellKey(to.Spec.Position, empty));
                plan.Steps.Add(new RebirthNpcB09Step{SourcePosition=from.Spec.Position,SourceSlot=slot,DestinationPosition=to.Spec.Position,
                    DestinationSlot=empty,ItemName=name,Rule=rule,Far=far,Merge=mergeCell >= 0,AlreadyHoldsItem=to.Items.Contains(name)});
                placed = true; break; // inventory-full fallback = next ranked eligible container
            }
            if (!placed) plan.Held.Add(Hold(from, slot, name, why));
        }
        // In-container sort only after nothing is misplaced across containers; planned (not dispatched) while custody is held.
        if (plan.Steps.Count == 0 && policy.SortWithinContainers && policy.Sort != RebirthNpcB09SortOrder.None)
            foreach (var sortBox in boxes)
            {
                var step = SortStep(sortBox, policy, claimedCells, plan.Held);
                if (step == null) continue;
                if (!RebirthNpcStorageOrganizationWork.SortCustodyReady || (step.Merge && !RebirthNpcStorageOrganizationWork.MergeCustodyReady))
                { plan.Held.Add(Hold(sortBox, step.SourceSlot, step.ItemName, RebirthNpcB09HoldReason.SortCustodyHeld)); continue; }
                plan.Steps.Add(step); break;
            }
        return plan;
    }

    // Unit-level sort (C2C-087 option b). The executor moves ONE unit per assignment, so a stack of n is moved as n unit
    // steps: unit 1 into an EMPTY cell (plain credit), units 2..n into that cell with the merge credit (After.count ==
    // Before.count + 1). The planner is stateless and works on a model of (payload key, count) per cell:
    //  * Target layout (restart-stable, independent of partial states): units of each exact payload are coalesced into
    //    ceil(total/max) stacks (full stacks first, remainder last), groups ordered by the sort policy, packed to the
    //    front of the unlocked cells, the rest empty. A crash mid-stack leaves two partial stacks of one payload; the next
    //    pass sees them as an intermediate state of the same target and merges them (the deficit/surplus rule below).
    //  * Every call re-simulates the entire remaining unit job on a copy; nothing is emitted unless the simulation ends
    //    exactly on the target layout (so a completed sort can only be reported when the final layout equals the target),
    //    and the live container is re-planned and re-verified after every real move.
    //  * Multi-unit stacks need the Merge policy (coalescing); with Merge off they stay fixed in place and are Held.
    //  * If any unlocked cell of the container is claimed by another storage assignment the container is deferred.
    private sealed class UnitCell { internal string Key; internal int Count; internal ItemStack Rep; }
    private const int MaxSortUnits = 512, MaxSortCells = 128;

    private static RebirthNpcB09Step SortStep(Box box, RebirthNpcB09Policy policy, ISet<string> claimed, List<RebirthNpcB09Held> held)
    {
        if (!box.Spec.AllowsWithdrawals || !box.Spec.AcceptsDeposits) return null;
        var cells = new List<int>();
        var state = new List<UnitCell>();
        int units = 0;
        for (int i = 0; i < box.Slots.Length && i < box.Locks.Length && i <= 255; i++)
        {
            if (box.Locks[i]) continue;
            if (claimed.Contains(RebirthNpcB09SelectionAdapter.CellKey(box.Spec.Position, i)))
            { held.Add(Hold(box, i, null, RebirthNpcB09HoldReason.CellReserved)); return null; }
            var stack = box.Slots[i];
            bool filled = stack != null && !stack.IsEmpty();
            if (filled && stack.count > 1 && !policy.Merge)
            { held.Add(Hold(box, i, ItemName(stack), RebirthNpcB09HoldReason.PartialNeedsNativeStackOriginal)); continue; }
            cells.Add(i);
            state.Add(filled ? new UnitCell{Key=RebirthNativeItemCodec.Encode(stack.itemValue),Count=stack.count,Rep=stack} : new UnitCell());
            if (filled) units += stack.count;
        }
        if (units == 0) return null;
        if (state.Count > MaxSortCells || units > MaxSortUnits)
        { held.Add(Hold(box, cells[0], null, RebirthNpcB09HoldReason.SortTooLarge)); return null; }
        var target = BuildSortTarget(state, policy.Sort);
        int from, to;
        if (!NextUnitMove(state, target, out from, out to)) return null; // live layout already equals the target
        var model = state.Select(c => new UnitCell{Key=c.Key,Count=c.Count,Rep=c.Rep}).ToList();
        bool reached = false;
        for (int guard = 0; guard < 4 * units + 8; guard++)
        {
            int f, t;
            if (!NextUnitMove(model, target, out f, out t)) { reached = true; break; }
            if (t < 0) break;
            ApplyUnitMove(model, f, t);
        }
        if (!reached || to < 0)
        {
            held.Add(Hold(box, cells[from], state[from].Rep != null ? ItemName(state[from].Rep) : null, RebirthNpcB09HoldReason.NoFreeCellToRotate));
            return null;
        }
        return new RebirthNpcB09Step{SourcePosition=box.Spec.Position,DestinationPosition=box.Spec.Position,SourceSlot=cells[from],
            DestinationSlot=cells[to],ItemName=ItemName(state[from].Rep),Rule=RebirthNpcB09RuleSource.DefaultContainer,InContainer=true,
            Merge=state[to].Key != null};
    }

    private static void ApplyUnitMove(List<UnitCell> st, int from, int to)
    {
        var src = st[from];
        if (st[to].Key == null) { st[to].Key = src.Key; st[to].Rep = src.Rep; st[to].Count = 1; }
        else st[to].Count++;
        src.Count--;
        if (src.Count <= 0) { src.Key = null; src.Rep = null; src.Count = 0; }
    }

    private static List<UnitCell> BuildSortTarget(List<UnitCell> state, RebirthNpcB09SortOrder order)
    {
        var groups = new Dictionary<string, UnitCell>(StringComparer.Ordinal);
        foreach (var c in state)
        {
            if (c.Key == null) continue;
            if (!groups.TryGetValue(c.Key, out var g)) { g = new UnitCell{Key=c.Key,Rep=c.Rep}; groups[c.Key] = g; }
            g.Count += c.Count;
        }
        var ordered = groups.Values.ToList();
        ordered.Sort((a, b) =>
        {
            int r = CompareItems(order, a.Rep, b.Rep);
            return r != 0 ? r : string.CompareOrdinal(a.Key, b.Key);
        });
        var target = new List<UnitCell>();
        foreach (var g in ordered)
        {
            int max = Math.Max(1, g.Rep.itemValue.ItemClass.Stacknumber.Value);
            for (int left = g.Count; left > 0; left -= max) target.Add(new UnitCell{Key=g.Key,Rep=g.Rep,Count=Math.Min(max,left)});
        }
        while (target.Count < state.Count) target.Add(new UnitCell());
        return target;
    }

    // Next single UNIT move toward the target. false = layout equals target. to < 0 = stuck (nowhere to put a unit).
    // Deficit at position k: pull one unit of tgt[k].Key from a cell holding surplus of that key (merge credit when k is
    // occupied, plain credit when k is empty). A wrong occupant at k is evacuated one unit at a time, preferably to a
    // position that needs that key, else to an empty cell outside the target prefix, else to any empty cell.
    private static bool NextUnitMove(List<UnitCell> st, List<UnitCell> tgt, out int from, out int to)
    {
        int m = tgt.Count(c => c.Key != null);
        Func<int, int> surplus = j => st[j].Key == null ? 0 : st[j].Count - (j < m && tgt[j].Key == st[j].Key ? Math.Min(st[j].Count, tgt[j].Count) : 0);
        for (int k = 0; k < m; k++)
        {
            bool sameKey = st[k].Key == tgt[k].Key;
            if (sameKey && st[k].Count >= tgt[k].Count) continue;
            if (st[k].Key != null && !sameKey)
            {
                string key = st[k].Key;
                for (int p = 0; p < m; p++)
                    if (p != k && tgt[p].Key == key && (st[p].Key == null || st[p].Key == key) &&
                        (st[p].Key == null ? 0 : st[p].Count) < tgt[p].Count) { from = k; to = p; return true; }
                // Staging cell outside the target prefix already holding the identical payload with room: merge credit
                // (After.count == Before.count + 1), so a second unit never needs another empty cell.
                int max = Math.Max(1, st[k].Rep.itemValue.ItemClass.Stacknumber.Value);
                for (int j = m; j < st.Count; j++)
                    if (j != k && st[j].Key == key && st[j].Count < max) { from = k; to = j; return true; }
                for (int e = st.Count - 1; e >= m; e--)
                    if (st[e].Key == null) { from = k; to = e; return true; }
                for (int e = st.Count - 1; e >= 0; e--)
                    if (e != k && st[e].Key == null) { from = k; to = e; return true; }
                from = k; to = -1; return true;
            }
            for (int j = 0; j < st.Count; j++)
                if (j != k && st[j].Key == tgt[k].Key && surplus(j) > 0) { from = j; to = k; return true; }
            from = k; to = -1; return true;
        }
        from = -1; to = -1;
        return false;
    }

    // Destination candidates for one item, in strict precedence order. Merge preference only reorders
    // within the same precedence tier, never across tiers (player intent is not overridden by "merge").
    private static List<Box> Rank(RebirthNpcB09Policy policy, List<Box> boxes, ItemStack stack, string name, out RebirthNpcB09RuleSource rule)
    {
        var result = new List<Box>();
        rule = RebirthNpcB09RuleSource.DefaultContainer;
        if (policy.ItemRules.TryGetValue(name, out var pos))
        {
            rule = RebirthNpcB09RuleSource.PlayerItemRule;
            foreach (var b in boxes) if (b.Spec.Position == pos) result.Add(b);
            return result; // an explicit item rule that cannot be honoured is held, not rerouted
        }
        var keys = new List<string>{name};
        var groups = stack.itemValue.ItemClass.Groups;
        if (groups != null) foreach (var g in groups) if (!string.IsNullOrEmpty(g)) keys.Add(g);
        foreach (var key in keys)
        {
            if (!policy.TagRules.TryGetValue(key, out var tag)) continue;
            foreach (var b in boxes)
                if (b.Spec.PlayerTags != null && Array.Exists(b.Spec.PlayerTags, t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))) result.Add(b);
            if (result.Count > 0) { rule = RebirthNpcB09RuleSource.PlayerContainerTag; Merge(policy, result, name); return result; }
        }
        if (policy.UseContainerNameMatch)
        {
            // Whole-word match between the container's custom name and the item's localized name / group names
            // (groups split on '/' and other separators, e.g. "Decor/Miscellaneous"). No substring matching.
            var words = Words(stack.itemValue.ItemClass.GetLocalizedItemName());
            if (groups != null) foreach (var g in groups) foreach (var w in Words(g)) words.Add(w);
            foreach (var b in boxes)
            {
                if (b.Name == null || result.Contains(b)) continue;
                foreach (var w in Words(b.Name)) if (words.Contains(w)) { result.Add(b); break; }
            }
            if (result.Count > 0) { rule = RebirthNpcB09RuleSource.ContainerNameMatch; Merge(policy, result, name); return result; }
        }
        if (policy.HasDefaultContainer)
            foreach (var b in boxes) if (b.Spec.Position == policy.DefaultContainer) result.Add(b);
        return result;
    }

    private static HashSet<string> Words(string text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(text)) return set;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && char.IsLetterOrDigit(text[i])) { sb.Append(char.ToLowerInvariant(text[i])); continue; }
            if (sb.Length >= 3) set.Add(sb.ToString());
            sb.Length = 0;
        }
        return set;
    }

    private static void Merge(RebirthNpcB09Policy policy, List<Box> list, string name)
    {
        if (!policy.Merge) return;
        var holders = list.FindAll(b => b.Items.Contains(name));
        if (holders.Count == 0 || holders.Count == list.Count) return;
        list.RemoveAll(b => holders.Contains(b)); list.InsertRange(0, holders);
    }

    // Near = one trip reaches both from the source; Far = leg-aware hauling (source leg, then destination leg).
    private static bool Reaches(Box a, Box b, bool allowFar, out bool far)
    {
        far = false;
        if (a == b) return false;
        float near = RebirthCompanionService.StorageDistance - RebirthNpcWorkRuntime.WorkRadius;
        float d2 = (a.Spec.Position.ToVector3() - b.Spec.Position.ToVector3()).sqrMagnitude;
        if (near > 0f && d2 <= near * near) return true;
        float max = RebirthNpcStorageOrganizationWork.FarMaxSeparation;
        far = allowFar && d2 <= max * max;
        return far;
    }
    // A partially filled cell of the destination holding the identical exact payload with room for one more item.
    private static int MergeCell(Box to, ItemStack stack, ISet<string> claimed, ISet<string> used)
    {
        var one = stack.Clone(); one.count = 1;
        string frame = RebirthHumanEquipment.Frame(one);
        for (int i = 0; i < to.Slots.Length && i < to.Locks.Length && i <= 255; i++)
        {
            if (to.Locks[i] || !RebirthNpcPhysicalResourceEndpoint.MergeTarget(to.Slots[i], frame)) continue;
            string key = RebirthNpcB09SelectionAdapter.CellKey(to.Spec.Position, i);
            if (claimed.Contains(key) || used.Contains(key)) continue;
            return i;
        }
        return -1;
    }
    private static int FreeCell(Box to, ISet<string> claimed, ISet<string> used)
    {
        for (int i = 0; i < to.Slots.Length && i < to.Locks.Length && i <= 255; i++)
        {
            if (to.Locks[i] || (to.Slots[i] != null && !to.Slots[i].IsEmpty())) continue;
            string key = RebirthNpcB09SelectionAdapter.CellKey(to.Spec.Position, i);
            if (claimed.Contains(key) || used.Contains(key)) continue;
            return i;
        }
        return -1;
    }

    private static string ItemName(ItemStack s)=>s?.itemValue?.ItemClass?.GetItemName();
    private static RebirthNpcB09Held Hold(Box b, int slot, string name, RebirthNpcB09HoldReason r)=>
        new RebirthNpcB09Held{SourcePosition=b.Spec.Position,SourceSlot=slot,ItemName=name,Reason=r};
    private static int Compare(Vector3i a, Vector3i b){int c=a.x.CompareTo(b.x);if(c!=0)return c;c=a.y.CompareTo(b.y);return c!=0?c:a.z.CompareTo(b.z);}
    private static int CompareCell(KeyValuePair<Box, int> a, KeyValuePair<Box, int> b){int c=Compare(a.Key.Spec.Position,b.Key.Spec.Position);return c!=0?c:a.Value.CompareTo(b.Value);}
    private static int CompareItems(RebirthNpcB09SortOrder order, ItemStack a, ItemStack b)
    {
        if (order == RebirthNpcB09SortOrder.None) return 0;
        if (order == RebirthNpcB09SortOrder.ByGroupThenName)
        {
            int g = string.Compare(FirstGroup(a), FirstGroup(b), StringComparison.OrdinalIgnoreCase);
            if (g != 0) return g;
        }
        int n = string.Compare(ItemName(a), ItemName(b), StringComparison.OrdinalIgnoreCase);
        if (n != 0) return n;
        // Tie-break on content so equal-comparing stacks are interchangeable: higher quality, then larger count, first.
        int q = b.itemValue.Quality.CompareTo(a.itemValue.Quality);
        return q;
    }
    private static string FirstGroup(ItemStack s){var g=s?.itemValue?.ItemClass?.Groups;return g!=null&&g.Length>0?g[0]??string.Empty:string.Empty;}
}

// One move per call: re-plans from live state, skips anything another worker has claimed, and creates
// at most one saved assignment through the existing authenticated, revalidating Assign path.
public static class RebirthNpcB09OrganizationDriver
{
    // Executes the NPC's saved policy (RebirthNpcB09PolicyStore); disabled / absent policy never moves anything.
    public static bool TryDispatchSaved(string session, out RebirthNpcB09Plan plan, out string detail)
    {
        plan = null; detail = "Organization is unavailable.";
        if (RebirthNpcInteractionSessionService.Authorize(session, RebirthNpcInteractionAction.AssignWork, out var context) != RebirthNpcInteractionResult.Allowed || context == null)
        { detail = "Session is not allowed to assign Work."; return false; }
        if (!RebirthNpcB09PolicyStore.TryRead(context.NpcId, out var policy, out bool enabled) || !enabled)
        { detail = "No enabled Organize policy is saved for this worker."; return false; }
        return TryDispatchNext(session, policy, out plan, out detail);
    }

    public static bool TryDispatchNext(string session, RebirthNpcB09Policy policy, out RebirthNpcB09Plan plan, out string detail)
    {
        plan = null; detail = "Organization is unavailable.";
        if (RebirthNpcInteractionSessionService.Authorize(session, RebirthNpcInteractionAction.AssignWork, out var context) != RebirthNpcInteractionResult.Allowed || context == null)
        { detail = "Session is not allowed to assign Work."; return false; }
        var owner = RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId);
        if (owner == null) return false;
        if (RebirthNpcB09SelectionAdapter.HasActiveStorageAssignment(context.NpcId))
        { detail = "The worker is still completing a storage move."; return false; }
        var claimed = RebirthNpcB09SelectionAdapter.ClaimedCells();
        plan = RebirthNpcB09OrganizationPlanner.Plan(owner, policy, claimed, 1);
        if (plan.Steps.Count == 0) { detail = plan.Held.Count > 0 ? "Nothing movable; " + plan.Held.Count + " item(s) held." : "Everything is organized."; return false; }
        var step = plan.Steps[0];
        var result = step.InContainer ?
            RebirthNpcStorageOrganizationWork.AssignSort(session, step.SourcePosition, step.SourceSlot, step.DestinationSlot, out _, out detail, step.Merge) :
            RebirthNpcStorageOrganizationWork.Assign(session, step.SourcePosition, step.SourceSlot, step.DestinationPosition, step.DestinationSlot,
                out _, out detail, step.Far, step.Merge);
        return result == RebirthNpcWorkAssignmentResult.Succeeded;
    }
}
