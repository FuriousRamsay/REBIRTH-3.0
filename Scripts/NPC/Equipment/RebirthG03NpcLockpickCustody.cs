using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// G03: NPC lockpick consumption through the guarded equipment custody (logical slot 179 =
// native toolbelt cell 7). DORMANT: every entry point returns false/Blocked while
// RebirthP11SmartLootGate.NpcLockpickQualified is false, and no production code calls it.
// Never reads or writes a player inventory.
//
// Persistent ledger: <lockpickAttempt id pre post phase broke x y z roll time breakChance skill> inside the NPC's
// saved humanEquipment element (same Read/Save custody as npcLootOpen/bookMove).
//   phase 0 intent    : saved BEFORE any roll is applied; pre = exact Frame of slot 179; nothing consumed.
//   phase 1 decided   : outcome (broke 0|1) and post frame saved BEFORE the native cell is touched.
//   phase 2 consumed  : native cell read back == post (or broke=0: nothing to consume). Terminal.
//   phase 3 conflict  : live cell is neither pre nor post (player/other owner moved it). Retained for audit; the
//                       lockpick is NOT consumed again and the pending row blocks new attempts for the NPC.
// Exactly-once: the decrement is the only transition pre -> post and runs only when live == pre; recovery
// re-applies it only in that state and only finishes (no write) when live == post. A crash anywhere between
// the phase-1 save and the native write replays once; after the write it only settles.
internal enum RebirthG03LockpickHold : byte
{
    None, GateClosed, NotServerMainThread, NpcUnavailable, NoLockpicks, AttemptPending, EquipmentBusy,
    SkillUnavailable, CustodyUnavailable, Conflict
}

internal struct RebirthG03LockpickPlan
{
    internal Guid Attempt;
    internal float PickSeconds;     // native base LockPickTime scaled by the NPC's own skill.lockpicking
    internal float BreakChance;     // 0..1, native base break chance scaled likewise
    internal float SkillValue;      // the NPC's saved skill.lockpicking value
}

internal static class RebirthG03NpcLockpickCustody
{
    // Reason keys (see _Documentation/Claude/localization/G03_LOCKPICK.csv).
    internal static string ReasonKey(RebirthG03LockpickHold hold)
    {
        switch (hold)
        {
            case RebirthG03LockpickHold.NoLockpicks: return "xuiRbNpcLockpickNoLockpicks";
            case RebirthG03LockpickHold.AttemptPending: return "xuiRbNpcLockpickAttemptPending";
            case RebirthG03LockpickHold.EquipmentBusy: return "xuiRbNpcLockpickEquipmentBusy";
            case RebirthG03LockpickHold.SkillUnavailable: return "xuiRbNpcLockpickSkillUnavailable";
            case RebirthG03LockpickHold.CustodyUnavailable: return "xuiRbNpcLockpickCustodyUnavailable";
            case RebirthG03LockpickHold.Conflict: return "xuiRbNpcLockpickConflict";
            case RebirthG03LockpickHold.None: return "";
            default: return "xuiRbNpcLockpickUnavailable";
        }
    }
    internal static string ReasonText(RebirthG03LockpickHold hold) =>
        hold == RebirthG03LockpickHold.NoLockpicks ? "NoLockpicks" : hold.ToString();

    // The NPC's own saved skill.lockpicking, read-only (no create/merge/save).
    internal static bool TryReadSkill(EntityRebirthHumanoidNPC npc, out float value)
    {
        value = 0f;
        try
        {
            if (npc?.RebirthRuntimeState == null ||
                !RebirthHumanProgression.TryPeek(npc.RebirthRuntimeState.StableId, out var record) ||
                !record.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillLockpicking, out var skill) ||
                skill == null || float.IsNaN(skill.Value)) return false;
            value = Mathf.Clamp(skill.Value, -50f, 100f);
            return true;
        }
        catch { return false; }
    }

    // The player's formula (RebirthSkillWaveAService.SyncNativePassiveEffects): LockPickTime and
    // LockPickBreakChance are perc_add of SignedEndpoint(skill, negativeAtMinus50, positiveAt100).
    internal static float ScaleTime(float nativeBaseSeconds, float skill) => Mathf.Max(0f, nativeBaseSeconds *
        (1f + RebirthSkillWaveAService.SignedEndpoint(skill, RebirthProgressionRuntimeConfig.LockpickNegativeTime, RebirthProgressionRuntimeConfig.LockpickPositiveTime)));
    internal static float ScaleBreak(float nativeBaseChance, float skill) => Mathf.Clamp01(nativeBaseChance *
        (1f + RebirthSkillWaveAService.SignedEndpoint(skill, RebirthProgressionRuntimeConfig.LockpickNegativeBreak, RebirthProgressionRuntimeConfig.LockpickPositiveBreak)));

    private static XElement Pending(XElement equipment) =>
        equipment.Elements("lockpickAttempt").FirstOrDefault(x => (int?)x.Attribute("phase") != 2);

    internal static bool HasPending(EntityRebirthHumanoidNPC npc)
    {
        try { return npc != null && Pending(RebirthHumanEquipment.Read(npc)) != null; }
        catch { return true; } // unreadable ledger fails closed
    }

    // Availability + write-ahead intent. nativeBaseSeconds / nativeBaseBreakChance are the target
    // lock's native values (TEFeatureLockPickable.lockPickTime and the block's break chance).
    internal static RebirthG03LockpickHold TryBegin(EntityRebirthHumanoidNPC npc, Vector3i position,
        float nativeBaseSeconds, float nativeBaseBreakChance, out RebirthG03LockpickPlan plan)
    {
        plan = default(RebirthG03LockpickPlan);
        try
        {
            if (!RebirthP11SmartLootGate.NpcLockpickQualified) return RebirthG03LockpickHold.GateClosed;
            if (!ThreadManager.IsMainThread() || npc?.world?.IsRemote() != false) return RebirthG03LockpickHold.NotServerMainThread;
            if (npc.RebirthRuntimeState == null || npc.IsDead() || !RebirthHumanEquipment.Human(npc) ||
                !ReferenceEquals(npc.world.GetEntity(npc.entityId), npc)) return RebirthG03LockpickHold.NpcUnavailable;
            if (RebirthHumanEquipment.EquippedLockpickCount(npc) < 1) return RebirthG03LockpickHold.NoLockpicks;
            if (!TryReadSkill(npc, out float skill)) return RebirthG03LockpickHold.SkillUnavailable;
            var x = RebirthHumanEquipment.Read(npc);
            if (Pending(x) != null) return RebirthG03LockpickHold.AttemptPending;
            var swap = x.Element("swap");
            if (swap != null && (string)swap.Attribute("done") != "1" || x.Element("bookMove") != null ||
                x.Elements("workMove").Any(w => (int?)w.Attribute("phase") != 4)) return RebirthG03LockpickHold.EquipmentBusy;
            var id = Guid.NewGuid();
            plan = new RebirthG03LockpickPlan { Attempt = id, SkillValue = skill,
                PickSeconds = ScaleTime(nativeBaseSeconds, skill), BreakChance = ScaleBreak(nativeBaseBreakChance, skill) };
            x.Add(new XElement("lockpickAttempt", new XAttribute("id", id.ToString("N")),
                new XAttribute("pre", RebirthHumanEquipment.Frame(RebirthHumanEquipment.Get(npc, RebirthHumanEquipment.LockpicksSlot))),
                new XAttribute("post", ""), new XAttribute("phase", 0), new XAttribute("broke", 0),
                new XAttribute("x", position.x), new XAttribute("y", position.y), new XAttribute("z", position.z),
                new XAttribute("breakChance", plan.BreakChance.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("skill", skill.ToString("R", CultureInfo.InvariantCulture))));
            // Trim settled rows to a bounded tail.
            foreach (var old in x.Elements("lockpickAttempt").Where(r => (int?)r.Attribute("phase") == 2).Reverse().Skip(8).ToArray()) old.Remove();
            RebirthHumanEquipment.Save(npc, x);
            return RebirthG03LockpickHold.None;
        }
        catch { return RebirthG03LockpickHold.CustodyUnavailable; }
    }

    // Records the attempt's outcome (roll01 in [0,1) from the server RNG, broke = roll01 < BreakChance) and applies
    // exactly one decrement when broke. Safe to call again for the same attempt (idempotent).
    internal static RebirthG03LockpickHold TryResolve(EntityRebirthHumanoidNPC npc, Guid attempt, float roll01)
    {
        try
        {
            if (!RebirthP11SmartLootGate.NpcLockpickQualified) return RebirthG03LockpickHold.GateClosed;
            if (!ThreadManager.IsMainThread() || npc?.world?.IsRemote() != false || npc.RebirthRuntimeState == null)
                return RebirthG03LockpickHold.NotServerMainThread;
            var x = RebirthHumanEquipment.Read(npc);
            var row = x.Elements("lockpickAttempt").SingleOrDefault(r => (string)r.Attribute("id") == attempt.ToString("N"));
            if (row == null) return RebirthG03LockpickHold.CustodyUnavailable;
            int phase = (int)row.Attribute("phase");
            if (phase == 2) return RebirthG03LockpickHold.None;
            if (phase == 3) return RebirthG03LockpickHold.Conflict;
            if (phase == 0)
            {
                // Decide once, persist before the native cell is touched.
                string pre = (string)row.Attribute("pre");
                if (RebirthHumanEquipment.Frame(RebirthHumanEquipment.Get(npc, RebirthHumanEquipment.LockpicksSlot)) != pre)
                { row.SetAttributeValue("phase", 3); RebirthHumanEquipment.Save(npc, x); return RebirthG03LockpickHold.Conflict; }
                float chance = float.Parse((string)row.Attribute("breakChance"), CultureInfo.InvariantCulture);
                bool broke = !float.IsNaN(roll01) && roll01 < chance;
                string post = pre;
                if (broke)
                {
                    var stack = RebirthHumanEquipment.Decode(pre);
                    if (stack.count < 1) return RebirthG03LockpickHold.NoLockpicks;
                    stack.count -= 1;
                    post = stack.count < 1 ? "" : RebirthHumanEquipment.Frame(stack);
                }
                row.SetAttributeValue("broke", broke ? 1 : 0);
                row.SetAttributeValue("post", post);
                row.SetAttributeValue("roll", roll01.ToString("R", CultureInfo.InvariantCulture));
                row.SetAttributeValue("phase", 1);
                RebirthHumanEquipment.Save(npc, x);
            }
            return Apply(npc, x, row);
        }
        catch { return RebirthG03LockpickHold.CustodyUnavailable; }
    }

    // Phase 1 -> 2. live == pre: write post once; live == post: already applied; anything else: conflict.
    private static RebirthG03LockpickHold Apply(EntityRebirthHumanoidNPC npc, XElement x, XElement row)
    {
        string pre = (string)row.Attribute("pre"), post = (string)row.Attribute("post");
        bool broke = (int)row.Attribute("broke") == 1;
        string live = RebirthHumanEquipment.Frame(RebirthHumanEquipment.Get(npc, RebirthHumanEquipment.LockpicksSlot));
        if (broke && live == pre)
        {
            RebirthHumanEquipment.Put(npc, RebirthHumanEquipment.LockpicksSlot, RebirthHumanEquipment.Decode(post));
            live = RebirthHumanEquipment.Frame(RebirthHumanEquipment.Get(npc, RebirthHumanEquipment.LockpicksSlot));
        }
        if (live != post)
        { row.SetAttributeValue("phase", 3); RebirthHumanEquipment.Save(npc, x); return RebirthG03LockpickHold.Conflict; }
        row.SetAttributeValue("phase", 2);
        RebirthHumanEquipment.Save(npc, x);
        return RebirthG03LockpickHold.None;
    }

    // Crash/reload recovery. A phase-0 row had no outcome and consumed nothing: it is abandoned (phase 2, post == pre).
    // A phase-1 row is replayed by Apply (decrement at most once).
    internal static void Recover(EntityRebirthHumanoidNPC npc)
    {
        try
        {
            if (!RebirthP11SmartLootGate.NpcLockpickQualified || !ThreadManager.IsMainThread() ||
                npc?.world?.IsRemote() != false || npc.RebirthRuntimeState == null) return;
            var x = RebirthHumanEquipment.Read(npc);
            foreach (var row in x.Elements("lockpickAttempt").ToArray())
            {
                int phase = (int?)row.Attribute("phase") ?? -1;
                if (phase == 0) { row.SetAttributeValue("post", (string)row.Attribute("pre")); row.SetAttributeValue("phase", 2); RebirthHumanEquipment.Save(npc, x); }
                else if (phase == 1) Apply(npc, x, row);
            }
        }
        catch { }
    }
}
