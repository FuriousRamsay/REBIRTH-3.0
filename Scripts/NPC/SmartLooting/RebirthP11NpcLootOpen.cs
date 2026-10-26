using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// B10: NPC-owned first-touch loot generation. Mirrors LootManager.LootContainerOpened
// (b259 IL): server only, not editor, grid not Touched, loot list resolves, container
// empty; stage = owner party stage (or unmodified stage when the list demands it);
// LootContainer.Spawn(random, grid length, stage, 0f, player, tags, UniqueItems,
// IgnoreLootProb, false); each result cloned into the grid. Differences, by design:
//  - the opener is NOT cast to EntityPlayer; Spawn receives player=null, so no player
//    attribution, no player buff actions and no player MinEvents fire;
//  - NPC-worn effects are evaluated ONCE per open with the NPC as the entity:
//    LootStage on the owner-derived base stage (boots), ScavengingTime on the base search
//    time (gloves), and LootProb / LootQuantity inside the roll through the actor-context
//    prefixes of RebirthP11NpcLootEffects. The owner's own effects are never added.
//  - ItemStackGrid.Touch() (sole writer of worldTimeTouched, which get_Touched tests) is
//    called after the grid is written, so a generated crate can never be rolled twice.
// The rolled stacks are persisted in the receipt BEFORE the grid is written, and phase 1 is
// published only after the live grid reads back EXACTLY as the saved roll (full length,
// cell contents and frames, trailing cells empty).
//
// Receipt phases: 0 rolled (saved image retained), 1 exact readback done, 2 CONFLICT
// (grid diverged from the roll with foreign content; saved roll AND the observed grid image
// are retained until the container is cleared or gone), 3 reconciled (images dropped,
// digests and disposition kept). A mismatch never overwrites the grid, never re-rolls, and
// never silently settles the operation. Every transfer out of the container is still its
// own exact smartLootMove original (full before/after grid images), so divergent items can
// never become an untracked transfer. Gate: NpcAttributedOpenQualified.
internal sealed class RebirthP11NpcLootOpenReceipt
{
    internal Guid Operation;
    internal string Npc, Owner, Incarnation, ImageDigest, Rolled, Observed, ObservedDigest, Disposition;
    internal Vector3i Position;
    internal int Stage, Count, Phase, GridLength;
    internal float SearchSeconds;
    internal bool EffectsApplied;

    internal bool KeepsRoll => Phase == 0 || Phase == 2;

    internal XElement Write()
    {
        var node = new XElement("npcLootOpen", new XAttribute("version", 3),
            new XAttribute("operation", Operation.ToString("N")), new XAttribute("npc", Npc ?? ""),
            new XAttribute("owner", Owner ?? ""), new XAttribute("incarnation", Incarnation ?? ""),
            new XAttribute("x", Position.x), new XAttribute("y", Position.y), new XAttribute("z", Position.z),
            new XAttribute("stage", Stage), new XAttribute("count", Count), new XAttribute("grid", GridLength),
            new XAttribute("phase", Phase), new XAttribute("effects", EffectsApplied ? 1 : 0),
            new XAttribute("search", SearchSeconds.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("image", ImageDigest ?? ""), new XAttribute("disp", Disposition ?? ""),
            new XAttribute("observed", ObservedDigest ?? ""),
            new XElement("rolled", KeepsRoll ? Rolled ?? "" : ""));
        node.Add(new XElement("observedGrid", Phase == 2 ? Observed ?? "" : ""));
        return node;
    }

    internal ItemStack[] Restore() => RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(Rolled ?? ""));
    internal ItemStack[] RestoreObserved() => RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(Observed ?? ""));

    internal static bool TryRead(XElement node, out RebirthP11NpcLootOpenReceipt receipt)
    {
        receipt = null;
        try
        {
            if (node?.Name != "npcLootOpen" || (string)node.Attribute("version") != "3" ||
                !Guid.TryParseExact((string)node.Attribute("operation"), "N", out var op)) return false;
            var candidate = new RebirthP11NpcLootOpenReceipt {
                Operation = op, Npc = (string)node.Attribute("npc"), Owner = (string)node.Attribute("owner"),
                Incarnation = (string)node.Attribute("incarnation"),
                Position = new Vector3i((int)node.Attribute("x"), (int)node.Attribute("y"), (int)node.Attribute("z")),
                Stage = (int)node.Attribute("stage"), Count = (int)node.Attribute("count"),
                GridLength = (int)node.Attribute("grid"), Phase = (int)node.Attribute("phase"),
                EffectsApplied = (string)node.Attribute("effects") == "1",
                SearchSeconds = float.Parse((string)node.Attribute("search"), CultureInfo.InvariantCulture),
                ImageDigest = (string)node.Attribute("image"), Disposition = (string)node.Attribute("disp"),
                ObservedDigest = (string)node.Attribute("observed"),
                Rolled = node.Element("rolled")?.Value ?? "", Observed = node.Element("observedGrid")?.Value ?? ""
            };
            if (candidate.Phase < 0 || candidate.Phase > 3 || candidate.Count < 0 || candidate.Count > 256 ||
                candidate.GridLength < candidate.Count || candidate.GridLength > 256 ||
                float.IsNaN(candidate.SearchSeconds) || candidate.SearchSeconds < 0f ||
                candidate.ImageDigest?.Length != 64 || !XNode.DeepEquals(candidate.Write(), node)) return false;
            if (candidate.KeepsRoll)
            {
                var stacks = candidate.Restore();
                if (stacks.Length != candidate.Count || Image(stacks) != candidate.ImageDigest) return false;
            }
            if (candidate.Phase == 2)
            {
                var observed = candidate.RestoreObserved();
                if (candidate.Disposition != "external" || observed.Length != candidate.GridLength ||
                    candidate.ObservedDigest?.Length != 64 || Image(observed) != candidate.ObservedDigest) return false;
            }
            if (candidate.Phase == 3 && candidate.Disposition != "subset" && candidate.Disposition != "cleared" &&
                candidate.Disposition != "gone") return false;
            if ((candidate.Phase == 0 || candidate.Phase == 1) && !string.IsNullOrEmpty(candidate.Disposition)) return false;
            receipt = candidate;
            return true;
        }
        catch { return false; }
    }

    internal static string Image(IList<ItemStack> stacks) => RebirthRemoteResourceDurableJournal.Digest(
        Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(stacks.ToArray())));

    // Unreadable rows fail closed. includeConflict also protects a retained conflict record.
    internal static bool HasPendingAt(XElement equipment, Vector3i position, bool includeConflict = true) =>
        equipment.Elements("npcLootOpen").Any(x => !TryRead(x, out var row) ||
            row.Position == position && (row.Phase == 0 || includeConflict && row.Phase == 2));

    // Insert (phase 0), or settle 0 -> 1 | 2 | 3 and 2 -> 3. Terminal rows (1, 3) are pruned
    // to a bounded tail; unresolved rows (0, 2) are capped, never pruned.
    internal static bool TryPublish(EntityRebirthHumanoidNPC npc, RebirthP11NpcLootOpenReceipt next)
    {
        try
        {
            if (!ThreadManager.IsMainThread() || npc?.world?.IsRemote() != false || next == null) return false;
            XElement equipment = RebirthHumanEquipment.Read(npc);
            var match = equipment.Elements("npcLootOpen")
                .Where(x => (string)x.Attribute("operation") == next.Operation.ToString("N")).ToArray();
            if (match.Length > 1) return false;
            if (match.Length == 1)
            {
                if (!TryRead(match[0], out var previous) || previous.ImageDigest != next.ImageDigest) return false;
                bool allowed = previous.Phase == 0 && next.Phase >= 1 && next.Phase <= 3 ||
                    previous.Phase == 2 && next.Phase == 3;
                if (!allowed) return false;
                match[0].ReplaceWith(next.Write());
            }
            else
            {
                if (next.Phase != 0) return false;
                var rows = equipment.Elements("npcLootOpen").ToArray();
                if (rows.Count(x => !TryRead(x, out var r) || r.Phase == 0 || r.Phase == 2) >= 8) return false;
                foreach (var old in rows.Where(x => TryRead(x, out var r) && (r.Phase == 1 || r.Phase == 3))
                    .Reverse().Skip(16).ToArray()) old.Remove();
                equipment.Add(next.Write());
            }
            RebirthHumanEquipment.Save(npc, equipment);
            return true;
        }
        catch { return false; }
    }
}

internal static class RebirthP11NpcLootOpen
{
    internal enum OpenResult : byte { NotApplicable, AlreadyTouched, Generated, Blocked }

    private static void Write(TEFeatureStorage source, IList<ItemStack> stacks)
    {
        for (int i = 0; i < stacks.Count; i++) source.ItemGrid[i] = stacks[i].Clone();
        source.ItemGrid.Touch();
        source.SetModified();
    }

    // Marks the container touched/modified and forces the native chunk snapshot save that
    // carries the tile entity, observing Touched afterwards. A receipt may leave phase 0 only
    // when this returns true.
    private static bool SealTouched(TEFeatureStorage source)
    {
        if (!source.ItemGrid.Touched) source.ItemGrid.Touch();
        source.SetModified();
        return source.ItemGrid.Touched &&
            RebirthP11StorageIncarnation.TryPersistChunk(GameManager.Instance?.World, source);
    }

    // Exact readback: same length as the roll's grid, each rolled cell identical (item
    // frame and count), every trailing cell empty.
    internal static bool GridMatches(TEFeatureStorage source, IList<ItemStack> roll, int gridLength)
    {
        var items = source?.ItemGrid?.items;
        if (items == null || items.Length != gridLength || roll == null || roll.Count > gridLength) return false;
        for (int i = 0; i < items.Length; i++)
        {
            ItemStack expected = i < roll.Count ? roll[i] : ItemStack.Empty;
            if (!RebirthStationGridIngredients.IsSameStackSnapshot(items[i] ?? ItemStack.Empty, expected)) return false;
        }
        return true;
    }

    // Subset: nothing foreign. Every occupied cell equals the roll at the same index
    // (partial write, or the roll's own items later taken). Never proves what was lost.
    internal static bool GridIsRollSubset(TEFeatureStorage source, IList<ItemStack> roll, int gridLength)
    {
        var items = source?.ItemGrid?.items;
        if (items == null || items.Length != gridLength || roll == null || roll.Count > gridLength) return false;
        for (int i = 0; i < items.Length; i++)
        {
            ItemStack cell = items[i];
            if (cell == null || cell.IsEmpty()) continue;
            if (i >= roll.Count || !RebirthStationGridIngredients.IsSameStackSnapshot(cell, roll[i])) return false;
        }
        return true;
    }

    // Effective search time: native base time scaled by the NPC's ScavengingTime effects.
    internal static float SearchSeconds(EntityRebirthHumanoidNPC npc, LootContainer list)
    {
        float baseSeconds = Math.Max(0f, list.openTime * LootContainer.LootTimerModifier);
        float seconds = Math.Max(0f, RebirthP11NpcLootEffects.Eval(PassiveEffects.ScavengingTime, npc, baseSeconds));
        // B13 provisional: directing player's Management shortens the search (time only; no stage/quantity/quality).
        return npc?.RebirthRuntimeState == null ? seconds
            : Math.Max(0f, RebirthCompanionLeadership.ManagementAdjustedLootSearchSeconds(seconds, npc.RebirthRuntimeState.StableId));
    }

    internal static bool TrySearchSeconds(EntityRebirthHumanoidNPC npc, TEFeatureStorage source, out float seconds)
    {
        seconds = 0f;
        try
        {
            LootContainer list = LootContainer.GetLootContainer(source?.lootListName, true);
            if (list == null || npc == null || !RebirthP11NpcLootEffects.Installed) return false;
            seconds = SearchSeconds(npc, list);
            return true;
        }
        catch { return false; }
    }

    internal static OpenResult TryOpen(EntityPlayer owner, EntityRebirthHumanoidNPC npc,
        TEFeatureStorage source, out Guid operation)
    {
        operation = Guid.Empty;
        try
        {
            World world = GameManager.Instance?.World;
            var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (!RebirthP11SmartLootGate.NpcAttributedOpenQualified || !RebirthP11NpcLootEffects.Installed ||
                !ThreadManager.IsMainThread() || world?.IsRemote() != false || connection == null ||
                !connection.IsServer || world.IsEditor() || owner == null || npc?.RebirthRuntimeState == null ||
                source?.ItemGrid == null)
                return OpenResult.Blocked;
            if (RebirthP11NpcLootAccess.Classify(owner, source) != RebirthP11NpcLootAccessResult.Open)
                return OpenResult.Blocked;
            if (source.ItemGrid.Touched) return OpenResult.AlreadyTouched;
            // An unresolved row (rolled or conflict) for this position means recovery owns it.
            if (RebirthP11NpcLootOpenReceipt.HasPendingAt(RebirthHumanEquipment.Read(npc), source.ToWorldPos()))
                return OpenResult.Blocked;
            LootContainer list = LootContainer.GetLootContainer(source.lootListName, true);
            if (list == null || !source.IsEmpty()) return OpenResult.NotApplicable;
            LootManager manager = GameManager.Instance.lootManager;
            if (manager == null) return OpenResult.Blocked;

            // Generated storage needs a durable, saved-span-witnessed identity before its
            // first debit; mint it only after every other check above has passed. A fresh
            // marker is witnessed asynchronously: the open retries on a later step.
            bool fresh = !RebirthP11StorageIncarnation.TryResolve(world, source, false, out string incarnation);
            if (fresh)
            {
                incarnation = Guid.NewGuid().ToString("N");
                if (!RebirthP11StorageIncarnation.TryMint(world, source, incarnation)) return OpenResult.Blocked;
            }

            // One evaluation per open. Owner supplies the base stage; NPC boots scale it.
            float baseStage = list.useUnmodifiedLootstage ? owner.unModifiedGameStage
                : owner.GetHighestPartyLootStage(source.LootStageMod, source.LootStageBonus);
            float ownerStage = baseStage;
            // G04: the NPC's worn ModGamestageDecrease (native GameStage passive) lowers the stage it opens with, like a player's own.
            baseStage = RebirthP11NpcLootEffects.Eval(PassiveEffects.GameStage, npc, baseStage);
            int stage = list.useUnmodifiedLootstage ? (int)baseStage
                : (int)RebirthP11NpcLootEffects.Eval(PassiveEffects.LootStage, npc, baseStage);
            int length = source.ItemGrid.Length;
            List<ItemStack> generated;
            RebirthP11NpcLootEffects.Actor = npc;
            try
            {
                generated = list.Spawn(manager.Random, length, stage, 0f, null,
                    default(FastTags<TagGroup.Global>), list.UniqueItems, list.IgnoreLootProb, false);
            }
            finally { RebirthP11NpcLootEffects.Actor = null; }
            if (generated == null || generated.Count > length) return OpenResult.Blocked;

            if (!RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity)) return OpenResult.Blocked;
            var receipt = new RebirthP11NpcLootOpenReceipt {
                Operation = Guid.NewGuid(), Npc = npc.RebirthRuntimeState.StableId.ToString(),
                Owner = identity.StorageKey, Incarnation = incarnation, Position = source.ToWorldPos(),
                Stage = stage, Count = generated.Count, GridLength = length, Phase = 0,
                SearchSeconds = SearchSeconds(npc, list), EffectsApplied = true,
                ImageDigest = RebirthP11NpcLootOpenReceipt.Image(generated),
                Rolled = Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(generated.ToArray()))
            };
            // Rolled loot is durable before the grid is written.
            if (!RebirthP11NpcLootOpenReceipt.TryPublish(npc, receipt)) return OpenResult.Blocked;
            Write(source, generated);
            // Phase 1 requires the exact grid image, not merely a completed Write. A mismatch
            // leaves phase 0 so recovery classifies it (subset / external) with the roll kept.
            if (!GridMatches(source, generated, length) || !SealTouched(source)) return OpenResult.Blocked;
            receipt.Phase = 1;
            if (!RebirthP11NpcLootOpenReceipt.TryPublish(npc, receipt)) return OpenResult.Blocked;
            RebirthG04LootSearchAttribution.LogOpen(owner, npc, list, ownerStage, stage);
            operation = receipt.Operation;
            return OpenResult.Generated;
        }
        catch { RebirthP11NpcLootEffects.Actor = null; return OpenResult.Blocked; }
    }

    // Recovery for a phase-0 row, keeping identity, owner and source-location checks:
    //  - untouched empty grid: replay the exact saved roll, require exact readback -> 1;
    //  - exact readback of the saved roll -> 1;
    //  - subset of the roll (partial write, or the roll's own items taken; nothing foreign)
    //    -> 3 "subset": nothing is rewritten (no resurrection of taken items) and an
    //    untouched grid is Touch()ed so it can never be rolled; later transfers are tracked
    //    by their own exact smartLootMove originals;
    //  - any foreign content (another opener, player-placed items) -> 2 "external": the
    //    saved roll and the observed grid image are RETAINED, the grid is never overwritten
    //    or re-rolled, and the row keeps protecting the position until it is resolved.
    // An unloaded or replaced source leaves the row pending.
    internal static bool TryRecover(EntityRebirthHumanoidNPC npc, RebirthP11NpcLootOpenReceipt row)
    {
        try
        {
            World world = GameManager.Instance?.World;
            if (!RebirthP11SmartLootGate.NpcAttributedOpenQualified || !ThreadManager.IsMainThread() ||
                world?.IsRemote() != false || npc?.RebirthRuntimeState == null || row == null || row.Phase != 0 ||
                npc.RebirthRuntimeState.StableId.ToString() != row.Npc) return false;
            var source = world.GetTileEntity(row.Position)?.GetSelfOrFeature<TEFeatureStorage>();
            if (source?.ItemGrid == null || source.ToWorldPos() != row.Position ||
                source.ItemGrid.Length != row.GridLength ||
                !RebirthP11StorageIncarnation.TryResolve(world, source, false, out string current) ||
                current != row.Incarnation) return false;
            var owner = world.Players?.list?.FirstOrDefault(p => p != null && !p.IsDead() &&
                RebirthStablePlayerIdentity.TryResolveServerEntity(p, out var id) && id.StorageKey == row.Owner);
            if (owner == null) return false;
            var stacks = row.Restore();
            if (!source.ItemGrid.Touched && source.IsEmpty())
            {
                // Order: exact roll -> Touch + modified + native chunk save -> receipt phase.
                Write(source, stacks);
                if (!GridMatches(source, stacks, row.GridLength) || !SealTouched(source)) return false;
                row.Phase = 1;
            }
            else if (GridMatches(source, stacks, row.GridLength))
            {
                if (!SealTouched(source)) return false;
                row.Phase = 1;
            }
            else if (GridIsRollSubset(source, stacks, row.GridLength))
            {
                // Phase 0 is retained until the touch is durable, so a crash can never leave
                // an untouched container (native reroll eligible) behind a closed receipt.
                if (!SealTouched(source)) return false;
                row.Phase = 3; row.Disposition = "subset";
            }
            else
            {
                var observed = ItemStack.Clone((IList<ItemStack>)source.ItemGrid.items);
                if (!SealTouched(source)) return false;
                row.Phase = 2; row.Disposition = "external";
                row.Observed = Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(observed));
                row.ObservedDigest = RebirthP11NpcLootOpenReceipt.Image(observed);
            }
            return RebirthP11NpcLootOpenReceipt.TryPublish(npc, row);
        }
        catch { return false; }
    }

    // Conflict (phase 2) resolution without touching items: reconciled when the container
    // is cleared by whoever owns the foreign content, or when it no longer exists.
    internal static bool TryResolveConflict(EntityRebirthHumanoidNPC npc, RebirthP11NpcLootOpenReceipt row)
    {
        try
        {
            World world = GameManager.Instance?.World;
            if (!RebirthP11SmartLootGate.NpcAttributedOpenQualified || !ThreadManager.IsMainThread() ||
                world?.IsRemote() != false || npc?.RebirthRuntimeState == null || row == null || row.Phase != 2 ||
                npc.RebirthRuntimeState.StableId.ToString() != row.Npc) return false;
            TileEntity tile = world.GetTileEntity(row.Position);
            if (tile == null) return false;   // unloaded or absent: hold
            var source = tile.GetSelfOrFeature<TEFeatureStorage>();
            if (source?.ItemGrid == null || source.ToWorldPos() != row.Position) row.Disposition = "gone";
            else if (RebirthP11StorageIncarnation.TryResolve(world, source, false, out string current))
            {
                if (current != row.Incarnation) row.Disposition = "gone";
                else if (source.ItemGrid.Touched && source.IsEmpty()) row.Disposition = "cleared";
                else return false;
            }
            else return false;
            row.Phase = 3;
            return RebirthP11NpcLootOpenReceipt.TryPublish(npc, row);
        }
        catch { return false; }
    }

    private static int recoveryCursor;

    // Paced by RebirthSmartLootingHumanTransfer.Tick (5 s, at most 4 NPCs per pass).
    internal static void RecoverTick(World world)
    {
        if (!RebirthP11SmartLootGate.NpcAttributedOpenQualified || world?.IsRemote() != false) return;
        var persons = RebirthNpcAggregatePersistenceStore.Snapshot().ToArray();
        if (persons.Length == 0) return;
        int count = Math.Min(4, persons.Length);
        for (int i = 0; i < count; i++)
        {
            var person = persons[(recoveryCursor + i) % persons.Length];
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(person.Identity.StableNpcId, out int id) ||
                world.GetEntity(id) is not EntityRebirthHumanoidNPC npc || npc.IsDead()) continue;
            foreach (var node in RebirthHumanEquipment.Read(npc).Elements("npcLootOpen").ToArray())
            {
                if (!RebirthP11NpcLootOpenReceipt.TryRead(node, out var row)) continue;
                if (row.Phase == 0) TryRecover(npc, row);
                else if (row.Phase == 2) TryResolveConflict(npc, row);
            }
        }
        recoveryCursor = (recoveryCursor + count) % persons.Length;
    }
}
