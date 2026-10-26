using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

// B14 executor: drives RebirthNpcB14NativeStrike through its saved phases for ONE block, persisting the
// original in the NPC aggregate before every mutation (write-ahead), recovering after a restart, and
// reporting the committed result. It is not wired into the Work adapter registry and
// RebirthNpcBlockMiningAdapter.NativeHarvestQualified stays false (see P12_B14_20261022.md).

internal enum RebirthNpcB14StepState { Continue, Completed, Failed }

internal sealed class RebirthNpcB14StepResult
{
    internal RebirthNpcB14StepState State;
    internal string Detail = string.Empty;
    // Completed only: items credited (settled at source excluded) and the summed saved strike seconds.
    internal Dictionary<string, int> Produced = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    internal float EffortSeconds;
    // Sum of block damage the credited strikes dealt (saved prediction minus pre-image, per strike).
    internal float DamageDealt;
    // The saved completion time; the outcome must be built with this exact value on every retry.
    internal long CompletedUtcTicks;
    // Logging: worker is inside the fall footprint; the caller navigates here before the next step.
    internal bool HasEscape;
    internal Vector3 EscapeTo;
}

// Saved original row, one per NPC, inside the aggregate's unknown optional elements.
internal static class RebirthNpcB14Store
{
    internal const string Element = "npcB14Original";

    internal sealed class Row
    {
        internal RebirthNpcB14HarvestOriginal Original;
        internal float Effort;
        internal float Damage; // summed block damage dealt for this block (NPC shared-skill award credited work)
        internal string Produced = string.Empty; // "type:count;..."
        internal string Tree = string.Empty;     // reserved (b259 trees are single blocks; no affected set to save)
        internal string ToolWear = string.Empty; // RebirthNpcB14ToolWear write-ahead row for the current operation
        // ONE saved completion time, minted once when the block is finished and reused by every retry of
        // the outcome (P6 SameCompletionWitness compares it). 0 = not completed yet.
        internal long CompletedUtcTicks;
    }

    private static XElement Find(RebirthNpcPersistentRecord person)
    {
        XElement result = null;
        foreach (string raw in person.UnknownOptionalElements ?? Array.Empty<string>())
        {
            XElement row = RebirthCompanionDeskStore.Parse(raw);
            if (row.Name != Element) continue;
            if (result != null) throw new System.IO.InvalidDataException("Duplicate B14 original.");
            result = row;
        }
        return result;
    }

    internal static bool TryRead(RebirthNpcStableId npc, out Row row)
    {
        row = null;
        try
        {
            XElement x;
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var person) || (x = Find(person)) == null ||
                (string)x.Attribute("version") != "1" || (string)x.Attribute("npc") != npc.ToString()) return false;
            RebirthNpcB14HarvestOriginal o;
            if (!RebirthNpcB14HarvestOriginal.TryParse((string)x.Attribute("data"), out o)) return false;
            row = new Row
            {
                Original = o, Produced = (string)x.Attribute("produced") ?? string.Empty,
                Tree = (string)x.Attribute("tree") ?? string.Empty,
                ToolWear = (string)x.Attribute("toolwear") ?? string.Empty,
                CompletedUtcTicks = long.Parse((string)x.Attribute("completed") ?? "0", CultureInfo.InvariantCulture),
                Damage = float.Parse((string)x.Attribute("damage") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture),
                Effort = float.Parse((string)x.Attribute("effort") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture)
            };
            return true;
        }
        catch { return false; }
    }

    // Write-ahead publication: replace the single row, then read the saved aggregate back.
    internal static bool TryWrite(RebirthNpcStableId npc, Row row)
    {
        try
        {
            XElement image = new XElement(Element, new XAttribute("version", "1"), new XAttribute("npc", npc.ToString()),
                new XAttribute("operation", row.Original.OperationId.ToString("N")),
                new XAttribute("data", row.Original.Serialize()),
                new XAttribute("effort", row.Effort.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("damage", row.Damage.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("produced", row.Produced ?? string.Empty), new XAttribute("tree", row.Tree ?? string.Empty),
                new XAttribute("toolwear", row.ToolWear ?? string.Empty),
                new XAttribute("completed", row.CompletedUtcTicks.ToString(CultureInfo.InvariantCulture)));
            string raw = image.ToString(SaveOptions.DisableFormatting);
            if (!RebirthNpcAggregatePersistenceStore.Mutate(npc, record =>
            {
                string[] rows = record.UnknownOptionalElements ?? Array.Empty<string>();
                record.UnknownOptionalElements = rows.Where(r => RebirthCompanionDeskStore.Parse(r).Name != Element)
                    .Concat(new[] { raw }).ToArray();
            })) return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var saved) && XNode.DeepEquals(Find(saved), image);
        }
        catch { return false; }
    }

    internal static bool TryRetire(RebirthNpcStableId npc)
    {
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.Mutate(npc, record =>
                record.UnknownOptionalElements = (record.UnknownOptionalElements ?? Array.Empty<string>())
                    .Where(r => RebirthCompanionDeskStore.Parse(r).Name != Element).ToArray())) return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var saved) && Find(saved) == null;
        }
        catch { return false; }
    }
}

// Tree felling in b259 (decompile BlockModelTree.cs / EntityFallingTree.cs, blocks.xml treeMaster Class="ModelTree"):
// a tree is ONE multi-block (BlockModelTree). Killing it makes OnBlockDestroyedBy -> startToFall return Keep: the
// block stays with damage == MaxDamage and an EntityFallingTree is spawned whose direction is
// (tree position - damager position), i.e. AWAY from the striking worker; the entity sets the block to Air about
// 3 s later. The tree itself drops no items (<drop event="Destroy" count="0"/>); wood comes from the Harvest drop
// list, which RebirthNpcB14HarvestCalculator already computes. So there is no affected-block set to save and no
// escape point: the worker is on the safe side by construction. What must be handled is the falling window, in
// which the block is still present and must NOT be struck again (it would spawn a second falling entity).
internal static class RebirthNpcB14Tree
{
    internal static bool IsTree(BlockValue value)
    {
        return !value.isair && value.Block is BlockModelTree;
    }

    // Present with full damage: EntityFallingTree is still on its way to removing the block.
    internal static bool IsFalling(BlockValue value)
    {
        return IsTree(value) && value.damage >= value.Block.MaxDamage;
    }
}

internal static class RebirthNpcB14HarvestExecutor
{
    // Operations whose escrow was already logged this session (memory only, log noise control).
    private static readonly HashSet<Guid> EscrowAnnounced = new HashSet<Guid>();

    private static RebirthNpcB14StepResult Result(RebirthNpcB14StepState state, string detail)
    {
        return new RebirthNpcB14StepResult { State = state, Detail = detail };
    }

    // One bounded step. tool is the worker's live equipped attack stack. Safe to call every tick.
    internal static RebirthNpcB14StepResult Step(EntityRebirthHumanoidNPC npc, ulong assignmentId, RebirthNpcB14Kind kind,
        Vector3i target, ItemValue tool)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || npc == null || npc.RebirthRuntimeState == null ||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)
            return Result(RebirthNpcB14StepState.Continue, "Authoritative world or worker is not ready.");
        RebirthNpcStableId id = npc.RebirthRuntimeState.StableId;
        RebirthNpcStableId npcId = id;
        RebirthNpcB14Store.Row row;
        bool has = RebirthNpcB14Store.TryRead(id, out row);
#if DEBUG
        // Fixture halt (CodexTest bridge only): the phase is already saved when the next Step reads it, so holding here
        // stops the executor right after that phase's save without mutating anything further.
        if (has && row.Original != null && DebugHaltHeld(id, row.Original.Phase))
            return Result(RebirthNpcB14StepState.Continue, "Debug halt after phase " + row.Original.Phase + ".");
#endif

        if (!has)
        {
            string follow;
            if (!RebirthNpcB14Policy.MayStrike(npc, out follow)) return Result(RebirthNpcB14StepState.Continue, follow);
            if (world.GetBlock(target).isair) return Result(RebirthNpcB14StepState.Failed, "Target block is gone.");
            row = new RebirthNpcB14Store.Row();
            return Arm(npc, id, assignmentId, kind, target, tool, row, world);
        }
        RebirthNpcB14HarvestOriginal original = row.Original;
        if (original.Position != target || original.AssignmentId != assignmentId)
        {
            // A cancelled/finished earlier assignment left a row. Rows that hold NO custody (nothing struck yet, or
            // aborted) are retired so the worker is not blocked forever; anything from Damaged on holds real
            // custody and stays for reconciliation (credit, escrow command).
            RebirthNpcB14Phase stale = original.Phase;
            if (stale == RebirthNpcB14Phase.Planned || stale == RebirthNpcB14Phase.Armed || stale == RebirthNpcB14Phase.Aborted)
            {
                RebirthNpcB14Store.TryRetire(id);
                return Result(RebirthNpcB14StepState.Continue, "A stale B14 row without custody was retired; retrying.");
            }
            return Result(RebirthNpcB14StepState.Failed, "A saved B14 original for another target holds custody and awaits reconciliation.");
        }
        PersistentPlayerData ppd = null;
        EntityPlayer owner = RebirthCompanionLeadership.DirectingPlayer(npcId);
        if (owner != null && GameManager.Instance.GetPersistentPlayerList() != null)
            ppd = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(owner.entityId);
        Func<string, bool> save = text => Persist(id, row, original);

        switch (original.Phase)
        {
            case RebirthNpcB14Phase.Planned:
            {
                // Accumulators survive in the row; a fresh operation is armed for the next strike, unless the
                // block is gone (finish) or a felled tree is still falling (never strike it again).
                BlockValue plannedBlock = world.GetBlock(target);
                if (plannedBlock.isair || plannedBlock.type != original.PreBlockType) return Complete(id, row);
                if (RebirthNpcB14Tree.IsFalling(plannedBlock)) return Result(RebirthNpcB14StepState.Continue, "The felled tree is still falling.");
                string followPlanned;
                if (!RebirthNpcB14Policy.MayStrike(npc, out followPlanned)) return Result(RebirthNpcB14StepState.Continue, followPlanned);
                return Arm(npc, id, assignmentId, kind, target, tool, row, world);
            }
            case RebirthNpcB14Phase.Armed:
            {
                if (!RebirthNpcB14NativeStrike.HasSnapshot(original.OperationId))
                {
                    // Restart: refuse to guess. An untouched block is re-armed under a new operation.
                    RebirthNpcB14NativeStrike.Recover(original, save);
                    if (original.Phase == RebirthNpcB14Phase.Armed)
                    {
                        row.Original = null;
                        return Arm(npc, id, assignmentId, kind, target, tool, row, world);
                    }
                    return Result(RebirthNpcB14StepState.Continue, "Recovered an interrupted strike.");
                }
                string follow;
                if (!RebirthNpcB14Policy.MayStrike(npc, out follow)) return Result(RebirthNpcB14StepState.Continue, follow);
                if (ppd == null) return Result(RebirthNpcB14StepState.Continue, "Directing player is offline.");
                // The strike stages its wear row inside its own write-ahead save (see TryStrike).
                string detail;
                bool ok = RebirthNpcB14NativeStrike.TryStrike(npc, tool, original, ppd, text => row.ToolWear = text, save, out detail);
                if (ok || original.Phase != RebirthNpcB14Phase.Armed) save(null);
                return Result(RebirthNpcB14StepState.Continue, detail);
            }
            case RebirthNpcB14Phase.Damaged:
            {
                // In this session the hit has landed (TryStrike confirmed it): apply the staged wear now. After a
                // restart the hit is first confirmed/reverted by Recover inside TryCloseWitnessWindow.
                if (RebirthNpcB14NativeStrike.HasSnapshot(original.OperationId) &&
                    !RebirthNpcB14ToolWear.TryComplete(npc, tool, row, () => Persist(id, row, original)))
                    return Result(RebirthNpcB14StepState.Continue, "Tool wear receipt is settling.");
                string detail;
                RebirthNpcB14NativeStrike.TryCloseWitnessWindow(original, save, out detail);
                return Result(RebirthNpcB14StepState.Continue, detail);
            }
            case RebirthNpcB14Phase.Witnessed:
            {
                if (!RebirthNpcB14ToolWear.TryComplete(npc, tool, row, () => Persist(id, row, original)))
                    return Result(RebirthNpcB14StepState.Continue, "Tool wear receipt is settling.");
                string detail;
                // Owned-storage fallback is HELD (no destination selection is designed): a stack the bag cannot
                // hold is retained at its physical source by the overflow re-drop, never claimed as stored.
                RebirthNpcB14NativeStrike.TryCredit(original, new RebirthNpcB14BagCreditSink(npc), save, out detail);
                return Result(RebirthNpcB14StepState.Continue, detail);
            }
            case RebirthNpcB14Phase.Credited:
                // Once the completion stamp is saved the wear was long settled; do not re-gate a finished receipt.
                if (row.CompletedUtcTicks == 0 &&
                    !RebirthNpcB14ToolWear.TryComplete(npc, tool, row, () => Persist(id, row, original)))
                    return Result(RebirthNpcB14StepState.Continue, "Tool wear receipt is settling.");
                return Finish(id, target, row, world);
            case RebirthNpcB14Phase.Escrowed:
                // Held, never discarded: the saved deliverable waits for RebirthNpcB14NativeStrike.TryResolveEscrow.
                // There is deliberately no automatic resolver: after a crash nothing observable says WHO destroyed the
                // block, so any rule would credit a guess or discard a real harvest. Make the wait visible once.
                if (EscrowAnnounced.Add(original.OperationId))
                    Log.Warning("[RBNPC] b14 escrow npc=" + id + " assignment=" + original.AssignmentId + " pos=" + original.Position +
                        " witnesses=" + original.Witnesses.Count + ": resolve with 'rbnpcescrow award|abort <npc> confirm=OwnerEscrow'.");
                return Result(RebirthNpcB14StepState.Continue,
                    "A destroyed block could not be attributed to this worker after a restart; its saved harvest is escrowed for reconciliation.");
            default: // Aborted
            {
                string reason = "B14 strike aborted; world items were left in place.";
                RebirthNpcB14Store.TryRetire(id);
                return Result(RebirthNpcB14StepState.Failed, reason);
            }
        }
    }

#if DEBUG
    // Test-only controls for the GameBridge fixture commands. Never persisted; absent from release builds.
    internal static string DebugHaltAfterPhase;
    internal static RebirthNpcStableId DebugHaltNpc;
    internal static bool DebugHaltTripped;

    internal static void DebugSetHalt(RebirthNpcStableId npc, string phase)
    {
        DebugHaltNpc = npc; DebugHaltAfterPhase = phase; DebugHaltTripped = false;
        DebugSetPlayerVar("rbnpc_b14_halted", 0f);
    }

    internal static void DebugClearHalt()
    {
        DebugHaltNpc = default(RebirthNpcStableId); DebugHaltAfterPhase = null; DebugHaltTripped = false;
        DebugSetPlayerVar("rbnpc_b14_halted", 0f);
    }

    private static bool DebugHaltHeld(RebirthNpcStableId id, RebirthNpcB14Phase phase)
    {
        if (string.IsNullOrEmpty(DebugHaltAfterPhase) || !(DebugHaltNpc == id)) return false;
        if (DebugHaltTripped) return true;
        if (!string.Equals(phase.ToString(), DebugHaltAfterPhase, StringComparison.OrdinalIgnoreCase)) return false;
        DebugHaltTripped = true;
        Log.Out("[RBNPC] b14 halted phase=" + phase);
        DebugSetPlayerVar("rbnpc_b14_halted", 1f);
        return true;
    }

    internal static void DebugSetPlayerVar(string name, float value)
    {
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player != null && player.Buffs != null) player.Buffs.SetCustomVar(name, value, true);
    }
#endif

    private static bool Persist(RebirthNpcStableId id, RebirthNpcB14Store.Row row, RebirthNpcB14HarvestOriginal o)
    {
        row.Original = o;
        return RebirthNpcB14Store.TryWrite(id, row);
    }

    private static RebirthNpcB14StepResult Arm(EntityRebirthHumanoidNPC npc, RebirthNpcStableId id, ulong assignmentId,
        RebirthNpcB14Kind kind, Vector3i target, ItemValue tool, RebirthNpcB14Store.Row row, World world)
    {
        RebirthNpcB14HarvestOriginal o;
        string detail;
        if (!RebirthNpcB14NativeStrike.TryArm(npc, target, kind, tool, id, assignmentId, out o, out detail))
            return Result(RebirthNpcB14StepState.Continue, detail);
        // Write-ahead: the Armed row is durable before TryStrike (which saves it again) issues the hit.
        if (!Persist(id, row, o)) return Result(RebirthNpcB14StepState.Continue, "Armed original could not be saved.");
        return Result(RebirthNpcB14StepState.Continue, "Strike armed.");
    }

    private static RebirthNpcB14StepResult Finish(RebirthNpcStableId id, Vector3i target,
        RebirthNpcB14Store.Row row, World world)
    {
        RebirthNpcB14HarvestOriginal o = row.Original;
        // Already finished (an earlier call saved the stamp): never re-accumulate, return the same receipt.
        if (row.CompletedUtcTicks != 0) return BuildCompleted(row);
        // Accumulate into LOCALS; the row is only touched together with its save, and every failure path
        // restores ALL mutable fields (Original, Effort, Produced, CompletedUtcTicks). A retry therefore always
        // starts from the same saved row and recomputes the same totals (idempotent).
        RebirthNpcB14HarvestOriginal savedOriginal = row.Original;
        float savedEffort = row.Effort;
        float savedDamage = row.Damage;
        string savedProduced = row.Produced;
        float seconds;
        float newEffort = row.Effort;
        if (RebirthNpcB14Rules.TryStrikeSeconds(o, out seconds)) newEffort = Math.Min(120f, row.Effort + seconds);
        // Block damage this credited strike dealt, from the SAVED prediction/pre-image (never from elapsed time).
        float dealt = 0f;
        if (o.PredictedDamage >= 0) dealt = Math.Max(0, o.PredictedDamage - o.PreBlockDamage);
        else
        {
            Block preBlock = o.PreBlockType >= 0 && o.PreBlockType < Block.list.Length ? Block.list[o.PreBlockType] : null;
            if (preBlock != null) dealt = Math.Max(0, preBlock.MaxDamage - o.PreBlockDamage);
        }
        float newDamage = row.Damage + dealt;
        Dictionary<int, int> produced = ParseProduced(row.Produced);
        for (int i = 0; i < o.Witnesses.Count; i++)
        {
            RebirthNpcB14DropWitness w = o.Witnesses[i];
            if (w.Credited && w.EntityId != -1 && w.EntityId != -2 && w.Count > 0)
            { int have; produced.TryGetValue(w.ItemType, out have); produced[w.ItemType] = have + w.Count; }
        }
        string newProduced = string.Join(";", produced.Select(p => p.Key.ToString(CultureInfo.InvariantCulture) + ":" +
            p.Value.ToString(CultureInfo.InvariantCulture)).ToArray());
        BlockValue now = world.GetBlock(target);
        bool gone = now.isair || now.type != o.PreBlockType;
        if (!gone)
        {
            // Block survived this strike: replace the credited original by a Planned one in the SAME write
            // that stores the accumulators, so a retry can never add this strike's effort/items twice.
            row.Original = new RebirthNpcB14HarvestOriginal
            {
                OperationId = Guid.NewGuid(), AssignmentId = o.AssignmentId, Kind = o.Kind, Phase = RebirthNpcB14Phase.Planned,
                Position = o.Position, PreBlockType = o.PreBlockType, PreBlockRotation = o.PreBlockRotation,
                PreBlockDamage = o.PreBlockDamage, ToolItemType = o.ToolItemType
            };
            row.Effort = newEffort; row.Produced = newProduced; row.Damage = newDamage;
            if (!RebirthNpcB14Store.TryWrite(id, row))
            {
                row.Original = savedOriginal; row.Effort = savedEffort; row.Produced = savedProduced; row.Damage = savedDamage;
                return Result(RebirthNpcB14StepState.Continue, "Accumulators could not be saved.");
            }
            return Result(RebirthNpcB14StepState.Continue, "Block damaged but standing; continuing.");
        }
        row.Effort = newEffort; row.Produced = newProduced; row.Damage = newDamage;
        RebirthNpcB14StepResult completed = Complete(id, row);
        if (completed.State != RebirthNpcB14StepState.Completed)
        {
            // Complete already reset the stamp; undo the accumulators with it.
            row.Original = savedOriginal; row.Effort = savedEffort; row.Produced = savedProduced; row.Damage = savedDamage;
            row.CompletedUtcTicks = 0;
        }
        return completed;
    }

    // Mint the ONE completion time, save it together with the accumulators, and only then report.
    private static RebirthNpcB14StepResult Complete(RebirthNpcStableId id, RebirthNpcB14Store.Row row)
    {
        if (row.CompletedUtcTicks != 0) return BuildCompleted(row);
        long minimum = 1;
        RebirthNpcWorkAssignment assignment;
        if (RebirthNpcWorkAssignmentService.TryGet(row.Original.AssignmentId, out assignment) && assignment != null)
            minimum = assignment.CreatedUtcTicks + 1;
        row.CompletedUtcTicks = Math.Max(DateTime.UtcNow.Ticks, minimum);
        if (!RebirthNpcB14Store.TryWrite(id, row))
        { row.CompletedUtcTicks = 0; return Result(RebirthNpcB14StepState.Continue, "Completion stamp could not be saved."); }
        return BuildCompleted(row);
    }

    private static RebirthNpcB14StepResult BuildCompleted(RebirthNpcB14Store.Row row)
    {
        RebirthNpcB14StepResult result = Result(RebirthNpcB14StepState.Completed, "Block harvested; custody settled.");
        foreach (KeyValuePair<int, int> p in ParseProduced(row.Produced))
        {
            ItemClass item = ItemClass.GetForId(p.Key);
            if (item != null) result.Produced[item.Name] = p.Value;
        }
        result.EffortSeconds = row.Effort;
        result.DamageDealt = row.Damage;
        result.CompletedUtcTicks = row.CompletedUtcTicks;
        return result;
    }

    // Call after the Work outcome (built with result.CompletedUtcTicks every time) is durably recorded.
    // Until then every Step returns the same Completed receipt, so a retry mints no new time.
    internal static bool Acknowledge(EntityRebirthHumanoidNPC npc)
    {
        bool retired = npc != null && npc.RebirthRuntimeState != null && RebirthNpcB14Store.TryRetire(npc.RebirthRuntimeState.StableId);
#if DEBUG
        if (retired) DebugSetPlayerVar("rbnpc_b14_completed", 1f); // outcome recorded + row acknowledged
#endif
        return retired;
    }

    private static Dictionary<int, int> ParseProduced(string text)
    {
        Dictionary<int, int> map = new Dictionary<int, int>();
        if (string.IsNullOrEmpty(text)) return map;
        foreach (string part in text.Split(';'))
        {
            string[] f = part.Split(':');
            int type, count;
            if (f.Length == 2 && int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out type) &&
                int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) && count > 0) map[type] = count;
        }
        return map;
    }

    // After the caller records the Work outcome for the completed block: supply the saved physical effort
    // (sum of this block's strike durations, capped at 120 s) to the B13 owner award. Retry-safe: the P1
    // metric is immutable once filled.
    // The worker's OWN shared Mining/Logging skill award for the completed block. Continuous evidence: credited
    // work = block damage dealt (saved), live work rate = damage / the saved strike seconds, so P1's equivalent
    // seconds equal the saved strike delays and nothing is invented. Receipt is stable per saved outcome, so a retry
    // reuses it (the P1 queue de-dupes by receipt). No truthful rate (no damage or no strike seconds) => no award.
    internal static RebirthP1AwardQueueResult SupplyNpcSkillAward(RebirthNpcWorkOutcomeRecord outcome,
        RebirthNpcB14StepResult result, RebirthNpcB14Kind kind)
    {
        if (outcome == null || result == null || result.State != RebirthNpcB14StepState.Completed || outcome.NpcId.IsEmpty)
            return RebirthP1AwardQueueResult.Invalid;
        float seconds = Math.Min(120f, result.EffortSeconds);
        if (result.DamageDealt <= 0f || seconds <= 0.05f) return RebirthP1AwardQueueResult.Invalid;
        string skill = kind == RebirthNpcB14Kind.Logging ? RebirthSurvivorIds.SkillLogging : RebirthSurvivorIds.SkillMining;
        return RebirthP1SharedWorkAward.QueueContinuousAward(outcome.NpcId, skill, "npc:b14:" + outcome.OutcomeId.ToString("N"),
            result.DamageDealt, result.DamageDealt / seconds, "b14-native-harvest",
            kind == RebirthNpcB14Kind.Logging ? "Felled a tree with native strikes" : "Mined a block with native strikes");
    }

    internal static RebirthP1AwardQueueResult SupplyEffort(RebirthNpcWorkOutcomeRecord outcome, RebirthNpcB14StepResult result)
    {
        if (outcome == null || result == null || result.State != RebirthNpcB14StepState.Completed)
            return RebirthP1AwardQueueResult.Invalid;
        // No measured strike time on the saved row (instant/legacy receipt): the completed block is still a saved one-shot completion, so
        // supply ONE discrete unit instead of leaving the bridge-created intent without evidence forever (Invalid supplies nothing).
        if (float.IsNaN(result.EffortSeconds) || float.IsInfinity(result.EffortSeconds) || result.EffortSeconds <= 0f)
            return RebirthP1SharedWorkAward.SupplyOwnerDiscrete(outcome);
        return RebirthP1SharedWorkAward.SupplyOwnerMetric(outcome, Math.Min(120f, result.EffortSeconds));
    }
}
