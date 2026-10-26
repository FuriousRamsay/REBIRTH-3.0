using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

#nullable disable

// B14 design types: policy, saved harvest original, drop witness and conservation rules.
// Nothing here mutates the world or an inventory; RebirthNpcB14NativeStrike drives it.
// Release stays held (RebirthNpcBlockMiningAdapter.NativeHarvestQualified, P12_B14_20261022.md).

public enum RebirthNpcB14Kind { Mining = 0, Logging = 1 }

// Provisional lead decisions (C2C-20261022-007; P12_B14_PROVISIONAL_DECISIONS_20261022.md).
public enum RebirthNpcB14Destination
{
    // Worker bag first, then eligible owner-controlled storage when the bag cannot hold it;
    // overflow stays at its physical source (never destroyed, never teleported).
    BagThenOwnedStorage = 0
}

public enum RebirthNpcB14FollowPolicy
{
    // Base job at a player volume: suspends while the worker is following, like other base work.
    SuspendUnderFollow = 0
}

public enum RebirthNpcB14Phase
{
    Planned = 0,     // target + tool + pre-image saved, nothing mutated
    Armed = 1,       // native hit about to be issued (saved BEFORE DamageBlock)
    Damaged = 2,     // native DamageBlock observed; witness window open
    Witnessed = 3,   // window closed, all emitted stacks recorded
    Credited = 4,    // every witnessed stack moved to bag/storage exactly once
    Aborted = 5,     // fail closed, nothing credited
    // Unattributable destruction after a crash: the saved deliverable is held UN-credited and un-discarded until
    // RebirthNpcB14NativeStrike.TryResolveEscrow(award) decides (manual or automatic reconcile).
    Escrowed = 6
}

public sealed class RebirthNpcB14DropWitness
{
    public int ItemType;
    public int Count;
    public int Quality;
    public Vector3 Position;
    public long Tick;
    // One-use marker: set when this witnessed stack has been credited to the destination.
    public bool Credited;
    // World EntityItem id this stack was read from (0 = emitted by the harvest calculation, never
    // an entity). The entity is removed before any credit so it cannot also be picked up.
    public int EntityId;
    // Saved BEFORE the world entity is removed: Claimed && entity vanished => we removed it (credit);
    // !Claimed && vanished => someone else took it (settle without credit).
    public bool Claimed;
    // True only for a world drop captured INSIDE the strike's own synchronous DamageBlock call stack
    // (World.EntityLoadedDelegates). A world-drop witness without it is ambiguous: never credited, never removed.
    public bool CallBound;
    // Saved BEFORE the overflow drop ("attempted"). On re-entry the drop is proven by the unique metadata marker
    // stamped into the dropped ItemValue (RebirthNpcB14NativeStrike.OverflowMarker) or the owed stack is ESCROWED
    // (never dropped twice, never credited).
    public bool OverflowDropped;
    // DEPRECATED: kept for row compatibility only. A predicted EntityFactory.nextEntityID is not reserved, so it is
    // no longer written or trusted; cleared by TryReleaseOwed.
    public int DropIntentId;
    // Exact native stack (RebirthHumanEquipment.Frame, base64 so the row separators stay unambiguous), so
    // quality, mods and metadata survive credit and re-drop. Empty only for rows saved before this field.
    public string Frame = string.Empty;
}

public sealed class RebirthNpcB14HarvestOriginal
{
    public Guid OperationId;
    public ulong AssignmentId;
    public RebirthNpcB14Kind Kind;
    public RebirthNpcB14Phase Phase;
    public Vector3i Position;
    // Pre-image of the strike target.
    public int PreBlockType;
    public int PreBlockRotation;
    public int PreBlockDamage;
    // Post-image, set once Damaged.
    public int PostBlockType;
    public bool HasPostImage;
    // Block damage the native DamageBlock is predicted to return for this strike (-1 = the block is destroyed).
    // Saved with the pre-journaled deliverable BEFORE the hit; recovery uses it to confirm the strike landed.
    public int PredictedDamage;
    // Tool and stamina captured before the hit.
    public int ToolItemType;
    public int ToolDurabilityBefore;
    public float StaminaBefore;
    // Management damage bonus (percent) applied to block damage only; never touches yield.
    public int ManagementDamageBonusPercent;
    public long DamagedTick;
    // Sum of positive witnessed counts, fixed when the window closes (Witnessed phase).
    public int WitnessedTotal;
    public readonly List<RebirthNpcB14DropWitness> Witnesses = new List<RebirthNpcB14DropWitness>();

    public bool IsTerminal { get { return Phase == RebirthNpcB14Phase.Credited || Phase == RebirthNpcB14Phase.Aborted; } }

    public string Serialize()
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        StringBuilder b = new StringBuilder("v2|");
        b.Append(OperationId.ToString("N")).Append('|').Append(AssignmentId.ToString(c)).Append('|')
            .Append((int)Kind).Append('|').Append((int)Phase).Append('|')
            .Append(Position.x.ToString(c)).Append(',').Append(Position.y.ToString(c)).Append(',').Append(Position.z.ToString(c)).Append('|')
            .Append(PreBlockType.ToString(c)).Append(',').Append(PreBlockRotation.ToString(c)).Append(',').Append(PreBlockDamage.ToString(c)).Append('|')
            .Append(HasPostImage ? "1" : "0").Append(',').Append(PostBlockType.ToString(c)).Append(',').Append(PredictedDamage.ToString(c)).Append('|')
            .Append(ToolItemType.ToString(c)).Append(',').Append(ToolDurabilityBefore.ToString(c)).Append(',')
            .Append(StaminaBefore.ToString("R", c)).Append('|')
            .Append(ManagementDamageBonusPercent.ToString(c)).Append('|').Append(DamagedTick.ToString(c)).Append('|').Append(WitnessedTotal.ToString(c)).Append('|');
        for (int i = 0; i < Witnesses.Count; i++)
        {
            RebirthNpcB14DropWitness w = Witnesses[i];
            if (i > 0) b.Append(';');
            b.Append(w.ItemType.ToString(c)).Append(',').Append(w.Count.ToString(c)).Append(',').Append(w.Quality.ToString(c)).Append(',')
                .Append(w.Position.x.ToString("R", c)).Append(',').Append(w.Position.y.ToString("R", c)).Append(',').Append(w.Position.z.ToString("R", c)).Append(',')
                .Append(w.Tick.ToString(c)).Append(',').Append(w.Credited ? "1" : "0").Append(',').Append(w.EntityId.ToString(c))
                .Append(',').Append(((w.Claimed ? 1 : 0) | (w.OverflowDropped ? 2 : 0) | (w.CallBound ? 4 : 0)).ToString(c))
                .Append(',').Append(w.Frame ?? string.Empty)
                .Append(',').Append(w.DropIntentId.ToString(c));
        }
        return b.ToString();
    }

    public static bool TryParse(string text, out RebirthNpcB14HarvestOriginal original)
    {
        original = null;
        if (string.IsNullOrEmpty(text)) return false;
        string[] p = text.Split('|');
        bool v2 = p.Length == 13 && p[0] == "v2";
        if (!v2 && !(p.Length == 12 && p[0] == "v1")) return false;
        CultureInfo c = CultureInfo.InvariantCulture;
        try
        {
            RebirthNpcB14HarvestOriginal o = new RebirthNpcB14HarvestOriginal();
            o.OperationId = Guid.ParseExact(p[1], "N");
            o.AssignmentId = ulong.Parse(p[2], c);
            int kind = int.Parse(p[3], c), phase = int.Parse(p[4], c);
            if (kind < 0 || kind > 1 || phase < 0 || phase > 6) return false;
            o.Kind = (RebirthNpcB14Kind)kind; o.Phase = (RebirthNpcB14Phase)phase;
            string[] pos = p[5].Split(','); if (pos.Length != 3) return false;
            o.Position = new Vector3i(int.Parse(pos[0], c), int.Parse(pos[1], c), int.Parse(pos[2], c));
            string[] pre = p[6].Split(','); if (pre.Length != 3) return false;
            o.PreBlockType = int.Parse(pre[0], c); o.PreBlockRotation = int.Parse(pre[1], c); o.PreBlockDamage = int.Parse(pre[2], c);
            string[] post = p[7].Split(','); if (post.Length != 2 && post.Length != 3) return false;
            o.HasPostImage = post[0] == "1"; o.PostBlockType = int.Parse(post[1], c);
            o.PredictedDamage = post.Length == 3 ? int.Parse(post[2], c) : 0;
            string[] tool = p[8].Split(','); if (tool.Length != 3) return false;
            o.ToolItemType = int.Parse(tool[0], c); o.ToolDurabilityBefore = int.Parse(tool[1], c);
            o.StaminaBefore = float.Parse(tool[2], NumberStyles.Float, c);
            o.ManagementDamageBonusPercent = int.Parse(p[9], c);
            o.DamagedTick = long.Parse(p[10].Length == 0 ? "0" : p[10], c);
            if (!TryParseWitnesses(p[v2 ? 12 : 11], o.Witnesses)) return false;
            int sum = 0;
            for (int i = 0; i < o.Witnesses.Count; i++)
            {
                if (o.Witnesses[i].Count <= 0) return false;
                sum += o.Witnesses[i].Count;
            }
            if (v2) { o.WitnessedTotal = int.Parse(p[11], c); if (o.WitnessedTotal < 0) return false; }
            else o.WitnessedTotal = sum;
            if (o.Phase >= RebirthNpcB14Phase.Witnessed && o.Phase != RebirthNpcB14Phase.Aborted &&
                o.Phase != RebirthNpcB14Phase.Escrowed &&
                o.WitnessedTotal != sum) return false;
            original = o;
            return true;
        }
        catch (Exception) { return false; }
    }

    public static bool TryParseWitnesses(string field, List<RebirthNpcB14DropWitness> into)
    {
        if (into == null) return false;
        if (string.IsNullOrEmpty(field)) return true;
        CultureInfo c = CultureInfo.InvariantCulture;
        string[] rows = field.Split(';');
        for (int i = 0; i < rows.Length; i++)
        {
            string[] f = rows[i].Split(',');
            if (f.Length < 8 || f.Length > 12) return false;
            try
            {
                into.Add(new RebirthNpcB14DropWitness
                {
                    ItemType = int.Parse(f[0], c), Count = int.Parse(f[1], c), Quality = int.Parse(f[2], c),
                    Position = new Vector3(float.Parse(f[3], c), float.Parse(f[4], c), float.Parse(f[5], c)),
                    Tick = long.Parse(f[6], c), Credited = f[7] == "1",
                    EntityId = f.Length >= 9 ? int.Parse(f[8], c) : 0,
                    Claimed = f.Length >= 10 && (int.Parse(f[9], c) & 1) != 0,
                    OverflowDropped = f.Length >= 10 && (int.Parse(f[9], c) & 2) != 0,
                    CallBound = f.Length >= 10 && (int.Parse(f[9], c) & 4) != 0,
                    Frame = f.Length >= 11 ? f[10] : string.Empty,
                    DropIntentId = f.Length == 12 ? int.Parse(f[11], c) : 0
                });
            }
            catch (Exception) { return false; }
        }
        return true;
    }
}

public static class RebirthNpcB14Rules
{
    // A drop entity belongs to this destroy only if it appeared at the block (within radius) inside
    // the witness window after the saved Damaged tick. Everything else is somebody else's item.
    // Tunables, deliberately constants. Rationale: harvest yield here is a virtual calculation (no entity), so
    // these only bound the OPTIONAL capture of stray world items from the struck block. SAFETY DOES NOT DEPEND
    // ON THEM: only items witnessed (recorded and saved) inside the window are ever credited, items spawned
    // later or farther simply stay in the world, and nothing is regenerated. A too-short window can lose
    // credit for a late drop (it stays in the world); a too-long one can only attribute an unrelated item
    // appearing in the same radius after the strike (the pre-strike entity snapshot is excluded).
    // Units are World.worldTime ticks; the values (settle 2, window 40) are estimates, unverified.
    public const float WitnessRadius = 3.5f;
    public const long WitnessSettleTicks = 2;
    public const long WitnessWindowTicks = 40;

    public static bool IsWitnessCandidate(RebirthNpcB14HarvestOriginal o, Vector3 entityPosition, long nowTick)
    {
        return IsWitnessCandidate(o, entityPosition, nowTick, WitnessRadius, WitnessWindowTicks);
    }

    // Tree felling widens both the radius (fall footprint) and the window (settling time).
    public static bool IsWitnessCandidate(RebirthNpcB14HarvestOriginal o, Vector3 entityPosition, long nowTick,
        float radius, long windowTicks)
    {
        if (o == null || o.Phase != RebirthNpcB14Phase.Damaged) return false;
        if (nowTick < o.DamagedTick || nowTick - o.DamagedTick > windowTicks) return false;
        Vector3 center = new Vector3(o.Position.x + 0.5f, o.Position.y + 0.5f, o.Position.z + 0.5f);
        return (entityPosition - center).sqrMagnitude <= radius * radius;
    }

    // Uncredited counts per item type. Credit marks are one-use, so calling this after a
    // reload yields only what is still owed, never a regenerated yield.
    public static Dictionary<int, int> Owed(RebirthNpcB14HarvestOriginal o)
    {
        Dictionary<int, int> owed = new Dictionary<int, int>();
        if (o == null) return owed;
        for (int i = 0; i < o.Witnesses.Count; i++)
        {
            RebirthNpcB14DropWitness w = o.Witnesses[i];
            if (w.Credited || w.Count <= 0) continue;
            int have; owed.TryGetValue(w.ItemType, out have); owed[w.ItemType] = have + w.Count;
        }
        return owed;
    }

    // Conservation: sum(witnessed) == sum(credited) + sum(owed). Credit is only legal from
    // Witnessed, and only marks a stack Credited together with the destination add (caller's
    // responsibility: add first, then mark, then save; on crash the stack is re-offered only if
    // the destination journal does not already hold the operation id).
    public static bool CheckConservation(RebirthNpcB14HarvestOriginal o, int destinationReceived)
    {
        if (o == null) return false;
        int credited = 0, owed = 0;
        for (int i = 0; i < o.Witnesses.Count; i++)
        {
            int n = o.Witnesses[i].Count;
            if (n <= 0) continue; // same rule as Owed
            if (o.Witnesses[i].Credited) credited += n; else owed += n;
        }
        return credited + owed == o.WitnessedTotal && destinationReceived == credited;
    }

    // ONE tier policy: the shared RebirthCompanionLeadership.ManagementBlockDamage table (1.10/1.20/1.30/1.40
    // at 20/40/60/80). The directing player is resolved from the worker at arm time and the resulting
    // percent is saved in the original, so recovery never re-reads the owner and the factor is applied
    // exactly once, to resource-block damage only (RebirthNpcB14NativeStrike.TryStrike).
    public static int ManagementDamageBonusPercent(RebirthNpcStableId npc)
    {
        float multiplier = RebirthCompanionLeadership.ManagementBlockDamageMultiplier(npc);
        return multiplier <= 1f ? 0 : (int)Math.Round((multiplier - 1f) * 100f);
    }

    // B13 action-derived effort of one Credited strike: the tool's attack delay in seconds (the real swing
    // time), independent of assignment wall time and of how many items dropped. The executor sums these
    // per completed block and passes the total to RebirthP1SharedWorkAward.SupplyOwnerMetric.
    public static bool TryStrikeSeconds(RebirthNpcB14HarvestOriginal o, out float seconds)
    {
        seconds = 0f;
        if (o == null || o.Phase != RebirthNpcB14Phase.Credited) return false;
        ItemClass tool = ItemClass.GetForId(o.ToolItemType);
        ItemAction action = tool != null && tool.Actions != null && tool.Actions.Length > 0 ? tool.Actions[0] : null;
        if (!(action is ItemActionAttack) || float.IsNaN(action.Delay) || float.IsInfinity(action.Delay) || action.Delay <= 0.05f)
            return false;
        seconds = Math.Min(120f, action.Delay);
        return true;
    }

    // Tool/stamina precondition so a hit is never issued with a broken tool or an empty worker.
    public static bool CanSwing(int toolDurability, float stamina, float minStamina, out string detail)
    {
        if (toolDurability <= 0) { detail = "Equipped tool is broken."; return false; }
        if (stamina < minStamina) { detail = "Worker stamina is too low."; return false; }
        detail = string.Empty;
        return true;
    }
}
