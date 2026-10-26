using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Immutable preimage plus phase journal for ONE exact stack leg from a corpse / entity loot container
// into one NPC bag cell. Same shape and phase meaning as RebirthSmartLootingHumanOriginal (the tile
// source), but the source is a saved ROW of the entity (id + WorldTimeBorn + class key) and the loot
// grid image, stored as <smartLootCorpse> under the NPC's humanEquipment.
//   0 written (nothing mutated)   1 attempted (debit may exist)   2 debit saved (SourceProof)
//   3 NPC credit applied          4 terminal (credit saved) or cancelled before any debit.
// The remainder (a stack larger than the NPC cell's room) is whatever SourceAfter keeps in the corpse.
internal sealed class RebirthP11CorpseOriginal
{
    internal Guid Operation;
    internal RebirthNpcStableId Npc;
    internal string Owner, Creation, World, SourceFrame, SourceKey, Binding, SourceProof, CancellationProof;
    internal int SourceSlot, DestinationSlot, Quantity, Deaths, Phase, Attempted, Published;
    internal bool Cancelled, NpcActivity;
    internal ItemStack[] SourceBefore, SourceAfter, DestinationBefore, DestinationAfter;
    internal bool[] SourceLocks, DestinationLocks;

    private static bool Same(ItemStack a, ItemStack b) => RebirthStationGridIngredients.IsSameStackSnapshot(a, b);
    private static string Image(ItemStack[] stacks) =>
        Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(stacks));
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static bool Hex64(string value) =>
        value?.Length == 64 && value.All(x => Uri.IsHexDigit(x)) && value == value.ToLowerInvariant();

    private string Immutable() => string.Join("|", new[] {
        "corpse", Operation.ToString("N"), Npc.ToString(), Owner, Creation, World, SourceKey,
        N(SourceSlot), N(DestinationSlot), N(Quantity), N(Deaths), SourceFrame,
        Image(SourceBefore), Image(SourceAfter), Image(DestinationBefore), Image(DestinationAfter),
        new string(SourceLocks.Select(x => x ? '1' : '0').ToArray()),
        new string(DestinationLocks.Select(x => x ? '1' : '0').ToArray()),
        NpcActivity ? "npcActivity" : "player"
    });

    internal bool Valid()
    {
        try
        {
            if (Operation == Guid.Empty || Npc.IsEmpty || Quantity <= 0 ||
                !RebirthOriginalActionScope.Validate(Owner, Creation) || World?.Length != 32 ||
                !RebirthP11CorpseSource.TryParseKey(SourceKey, out _, out _, out _) ||
                SourceBefore == null || SourceAfter == null || DestinationBefore == null || DestinationAfter == null ||
                SourceBefore.Length != SourceAfter.Length || DestinationBefore.Length != DestinationAfter.Length ||
                SourceLocks == null || DestinationLocks == null ||
                SourceLocks.Length != SourceBefore.Length || DestinationLocks.Length != DestinationBefore.Length ||
                SourceSlot < 0 || SourceSlot >= SourceBefore.Length ||
                DestinationSlot < 0 || DestinationSlot >= DestinationBefore.Length ||
                SourceLocks[SourceSlot] || DestinationLocks[DestinationSlot] ||
                SourceBefore.Any(x => x == null) || SourceAfter.Any(x => x == null) ||
                DestinationBefore.Any(x => x == null) || DestinationAfter.Any(x => x == null) ||
                SourceBefore[SourceSlot].IsEmpty() || SourceBefore[SourceSlot].count < Quantity ||
                !SourceBefore[SourceSlot].CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack, DestinationSlot) ||
                !RebirthHumanEquipment.Accept(10 + DestinationSlot, SourceBefore[SourceSlot]) ||
                SourceFrame != RebirthHumanEquipment.Frame(SourceBefore[SourceSlot]) ||
                Binding != RebirthRemoteResourceDurableJournal.Digest(Immutable()) ||
                Deaths < 0 || Phase < 0 || Phase > 4 || Attempted < 0 || Attempted > 1 ||
                Published < 0 || Published > 1 || Published > Attempted ||
                Phase == 0 && (Attempted != 0 || Published != 0) ||
                Phase == 1 && Attempted != 1 ||
                !Cancelled && Phase >= 2 && Published != 1 ||
                Cancelled && (Phase != 4 || Attempted != 1 || Published != 0 ||
                    !string.IsNullOrEmpty(SourceProof) || CancellationProof?.Length != 64) ||
                !Cancelled && !string.IsNullOrEmpty(CancellationProof) ||
                !string.IsNullOrEmpty(SourceProof) && !Hex64(SourceProof) ||
                Published == 1 && SourceProof?.Length != 64 ||
                !string.IsNullOrEmpty(CancellationProof) && !Hex64(CancellationProof)) return false;

            var source = SourceBefore[SourceSlot].Clone();
            source.count -= Quantity;
            if (source.count == 0) source = ItemStack.Empty.Clone();
            if (!Same(source, SourceAfter[SourceSlot])) return false;
            var destination = DestinationBefore[DestinationSlot].Clone();
            if (!destination.IsEmpty() && !destination.CanStackWith(SourceBefore[SourceSlot])) return false;
            if (destination.IsEmpty())
            {
                destination = SourceBefore[SourceSlot].Clone();
                destination.count = Quantity;
            }
            else destination.count += Quantity;
            int max = Math.Max(1, destination.itemValue.ItemClass.Stacknumber.Value);
            if (destination.count > max || !Same(destination, DestinationAfter[DestinationSlot])) return false;
            for (int i = 0; i < SourceBefore.Length; i++)
                if (i != SourceSlot && !Same(SourceBefore[i], SourceAfter[i])) return false;
            for (int i = 0; i < DestinationBefore.Length; i++)
                if (i != DestinationSlot && !Same(DestinationBefore[i], DestinationAfter[i])) return false;
            return true;
        }
        catch { return false; }
    }

    internal static bool TryPlan(EntityPlayer owner, EntityLootContainer source, int sourceSlot,
        EntityRebirthHumanoidNPC npc, int destinationSlot, Guid operation,
        out RebirthP11CorpseOriginal original, bool npcActivity)
    {
        original = null;
        try
        {
            World world = GameManager.Instance?.World;
            if (!RebirthP11SmartLootGate.CorpseSourceQualified) return false;
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false || operation == Guid.Empty ||
                owner == null || source == null || npc?.RebirthRuntimeState == null ||
                owner.IsDead() || npc.IsDead() || npc.IsPreparedRestorationPending ||
                !ReferenceEquals(owner.world, world) || !ReferenceEquals(npc.world, world) ||
                !ReferenceEquals(world.GetEntity(owner.entityId), owner) ||
                !ReferenceEquals(world.GetEntity(npc.entityId), npc) ||
                npc.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity) ||
                !string.Equals(identity.CanonicalId, npc.RebirthRuntimeState.OwnerId, StringComparison.OrdinalIgnoreCase) ||
                !RebirthP11CorpseSource.TryResolve(world, RebirthP11CorpseSource.Key(source), out var live) ||
                !ReferenceEquals(live, source) ||
                !(npcActivity ? RebirthP11CorpseSource.NoNativeLock(source) : RebirthP11CorpseSource.OwnsLock(source, owner.entityId)) ||
                (owner.position - source.position).sqrMagnitude >
                    (Constants.cCollectItemDistance + 30f) * (Constants.cCollectItemDistance + 30f) ||
                (npc.position - source.position).sqrMagnitude >
                    RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance ||
                npc.bag?.ItemGrid?.items == null || sourceSlot < 0 || sourceSlot >= source.bag.ItemGrid.items.Length ||
                destinationSlot < 0 || destinationSlot >= npc.bag.SlotCount ||
                npc.bag.ItemGrid.SlotLocks == null || npc.bag.ItemGrid.SlotLocks.Length != npc.bag.SlotCount ||
                source.bag.ItemGrid.SlotLocks[sourceSlot] || npc.bag.ItemGrid.SlotLocks[destinationSlot]) return false;

            ItemStack incoming = source.bag.ItemGrid.items[sourceSlot];
            ItemStack target = npc.bag.ItemGrid[destinationSlot];
            if (incoming == null || incoming.IsEmpty() || incoming.count <= 0 || incoming.itemValue?.ItemClass == null ||
                !incoming.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack, destinationSlot) ||
                !RebirthHumanEquipment.Accept(10 + destinationSlot, incoming) ||
                target != null && !target.IsEmpty() && !target.CanStackWith(incoming)) return false;
            int maximum = Math.Max(1, incoming.itemValue.ItemClass.Stacknumber.Value);
            int room = maximum - (target == null || target.IsEmpty() ? 0 : target.count);
            // Exact remainder: whatever does not fit stays in the corpse cell; there is no fallback.
            int quantity = Math.Min(incoming.count, room);
            if (quantity <= 0) return false;

            var sourceBefore = ItemStack.Clone((IList<ItemStack>)source.bag.ItemGrid.items);
            var destinationBefore = ItemStack.Clone((IList<ItemStack>)npc.bag.ItemGrid.items);
            var sourceAfter = ItemStack.Clone((IList<ItemStack>)sourceBefore);
            var destinationAfter = ItemStack.Clone((IList<ItemStack>)destinationBefore);
            sourceAfter[sourceSlot].count -= quantity;
            if (sourceAfter[sourceSlot].count == 0) sourceAfter[sourceSlot] = ItemStack.Empty.Clone();
            if (destinationAfter[destinationSlot] == null || destinationAfter[destinationSlot].IsEmpty())
            {
                destinationAfter[destinationSlot] = incoming.Clone();
                destinationAfter[destinationSlot].count = quantity;
            }
            else destinationAfter[destinationSlot].count += quantity;
            var candidate = new RebirthP11CorpseOriginal {
                Operation = operation, Npc = npc.RebirthRuntimeState.StableId,
                Owner = identity.StorageKey, Creation = RebirthCamperCharacterAccess.CurrentCreation(owner),
                World = RebirthLogisticsTransferJournal.WorldKey(world), Deaths = owner.Died,
                SourceKey = RebirthP11CorpseSource.Key(source), SourceSlot = sourceSlot,
                DestinationSlot = destinationSlot, Quantity = quantity,
                SourceFrame = RebirthHumanEquipment.Frame(incoming), NpcActivity = npcActivity,
                SourceBefore = sourceBefore, SourceAfter = sourceAfter,
                DestinationBefore = destinationBefore, DestinationAfter = destinationAfter,
                SourceLocks = Enumerable.Range(0, sourceBefore.Length).Select(i => source.bag.ItemGrid.SlotLocks[i]).ToArray(),
                DestinationLocks = Enumerable.Range(0, destinationBefore.Length).Select(i => npc.bag.ItemGrid.SlotLocks[i]).ToArray()
            };
            candidate.Binding = RebirthRemoteResourceDurableJournal.Digest(candidate.Immutable());
            if (!candidate.Valid()) return false;
            original = candidate;
            return true;
        }
        catch { return false; }
    }

    internal XElement Write()
    {
        if (!Valid()) throw new System.IO.InvalidDataException("Invalid Smart Looting corpse original.");
        return new XElement("smartLootCorpse", new XAttribute("version", 1),
            new XAttribute("operation", Operation.ToString("N")), new XAttribute("npc", Npc.ToString()),
            new XAttribute("owner", Owner), new XAttribute("creation", Creation),
            new XAttribute("world", World), new XAttribute("key", SourceKey),
            new XAttribute("sourceSlot", SourceSlot), new XAttribute("destinationSlot", DestinationSlot),
            new XAttribute("quantity", Quantity), new XAttribute("deaths", Deaths),
            new XAttribute("binding", Binding), new XAttribute("phase", Phase),
            new XAttribute("attempted", Attempted), new XAttribute("published", Published),
            new XAttribute("sourceProof", SourceProof ?? string.Empty),
            new XAttribute("cancelled", Cancelled ? 1 : 0),
            new XAttribute("cancellationProof", CancellationProof ?? string.Empty),
            new XAttribute("npcActivity", NpcActivity ? 1 : 0),
            new XElement("stack", SourceFrame),
            new XElement("sourceBefore", Image(SourceBefore)),
            new XElement("sourceAfter", Image(SourceAfter)),
            new XElement("destinationBefore", Image(DestinationBefore)),
            new XElement("destinationAfter", Image(DestinationAfter)),
            new XElement("sourceLocks", new string(SourceLocks.Select(x => x ? '1' : '0').ToArray())),
            new XElement("destinationLocks", new string(DestinationLocks.Select(x => x ? '1' : '0').ToArray())));
    }

    internal static bool TryRead(XElement node, out RebirthP11CorpseOriginal original)
    {
        original = null;
        try
        {
            if (node?.Name != "smartLootCorpse" || (string)node.Attribute("version") != "1" ||
                node.Attributes().Count() != 19 ||
                ((string)node.Attribute("cancelled") != "0" && (string)node.Attribute("cancelled") != "1") ||
                ((string)node.Attribute("npcActivity") != "0" && (string)node.Attribute("npcActivity") != "1") ||
                !node.Elements().Select(x => x.Name.LocalName).SequenceEqual(new[] {
                    "stack", "sourceBefore", "sourceAfter", "destinationBefore", "destinationAfter",
                    "sourceLocks", "destinationLocks"
                }) || !RebirthNpcStableId.TryParse((string)node.Attribute("npc"), out var npc)) return false;
            var candidate = new RebirthP11CorpseOriginal {
                Operation = Guid.ParseExact((string)node.Attribute("operation"), "N"), Npc = npc,
                Owner = (string)node.Attribute("owner"), Creation = (string)node.Attribute("creation"),
                World = (string)node.Attribute("world"), SourceKey = (string)node.Attribute("key"),
                SourceSlot = (int)node.Attribute("sourceSlot"), DestinationSlot = (int)node.Attribute("destinationSlot"),
                Quantity = (int)node.Attribute("quantity"), Deaths = (int)node.Attribute("deaths"),
                Binding = (string)node.Attribute("binding"), Phase = (int)node.Attribute("phase"),
                Attempted = (int)node.Attribute("attempted"), Published = (int)node.Attribute("published"),
                SourceProof = (string)node.Attribute("sourceProof"),
                Cancelled = (string)node.Attribute("cancelled") == "1",
                NpcActivity = (string)node.Attribute("npcActivity") == "1",
                CancellationProof = (string)node.Attribute("cancellationProof"),
                SourceFrame = node.Element("stack")?.Value,
                SourceBefore = RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("sourceBefore")?.Value ?? "")),
                SourceAfter = RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("sourceAfter")?.Value ?? "")),
                DestinationBefore = RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("destinationBefore")?.Value ?? "")),
                DestinationAfter = RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("destinationAfter")?.Value ?? ""))
            };
            string sourceLocks = node.Element("sourceLocks")?.Value;
            string destinationLocks = node.Element("destinationLocks")?.Value;
            if (sourceLocks == null || destinationLocks == null ||
                sourceLocks.Any(x => x != '0' && x != '1') || destinationLocks.Any(x => x != '0' && x != '1')) return false;
            candidate.SourceLocks = sourceLocks.Select(x => x == '1').ToArray();
            candidate.DestinationLocks = destinationLocks.Select(x => x == '1').ToArray();
            if (!candidate.Valid() || !XNode.DeepEquals(candidate.Write(), node)) return false;
            original = candidate;
            return true;
        }
        catch { return false; }
    }

    internal static bool TryReadSaved(RebirthNpcStableId npc, Guid operation, out RebirthP11CorpseOriginal original)
    {
        original = null;
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var person)) return false;
            var moves = (person.UnknownOptionalElements ?? Array.Empty<string>())
                .Select(XElement.Parse).Where(x => x.Name == "humanEquipment")
                .SelectMany(x => x.Elements("smartLootCorpse"))
                .Where(x => (string)x.Attribute("operation") == operation.ToString("N")).ToArray();
            if (moves.Length != 1 || !TryRead(moves[0], out var found) ||
                found.Npc != npc || found.Operation != operation) return false;
            original = found;
            return true;
        }
        catch { return false; }
    }

    // Phases advance one at a time; a pre-debit attempt may be retired directly to a cancelled terminal row.
    // Terminal rows beyond a 16-row tail are pruned (oldest first) so the 128-row cap can never wedge an NPC.
    internal static bool TryPublish(EntityRebirthHumanoidNPC npc, RebirthP11CorpseOriginal next)
    {
        try
        {
            if (!ThreadManager.IsMainThread() || npc?.world?.IsRemote() != false ||
                next == null || npc.RebirthRuntimeState.StableId != next.Npc || !next.Valid()) return false;
            XElement equipment = RebirthHumanEquipment.Read(npc);
            XElement[] matches = equipment.Elements("smartLootCorpse")
                .Where(x => (string)x.Attribute("operation") == next.Operation.ToString("N")).ToArray();
            if (matches.Length > 1) return false;
            if (matches.Length == 1)
            {
                if (!TryRead(matches[0], out var previous) || previous.Binding != next.Binding ||
                    next.Phase != previous.Phase + 1 &&
                        !(previous.Phase == 1 && next.Phase == 4 && next.Cancelled) ||
                    next.Attempted < previous.Attempted || next.Attempted > previous.Attempted + 1 ||
                    next.Published < previous.Published || next.Published > previous.Published + 1) return false;
                matches[0].ReplaceWith(next.Write());
            }
            else
            {
                if (next.Phase != 0 || next.Attempted != 0 || next.Published != 0 ||
                    equipment.Elements("smartLootCorpse").Any(x => !TryRead(x, out var existing) || existing.Phase < 4))
                    return false;
                foreach (var old in equipment.Elements("smartLootCorpse").Reverse().Skip(15).ToArray()) old.Remove();
                equipment.Add(next.Write());
            }
            RebirthHumanEquipment.Save(npc, equipment);
            return TryReadSaved(next.Npc, next.Operation, out var saved) &&
                XNode.DeepEquals(saved.Write(), next.Write());
        }
        catch { return false; }
    }

    internal static bool HasPending(RebirthNpcStableId npc)
    {
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var person) &&
                !RebirthNpcAggregatePersistenceStore.TryGet(npc, out person)) return true;
            foreach (var equipment in (person.UnknownOptionalElements ?? Array.Empty<string>())
                .Select(XElement.Parse).Where(x => x.Name == "humanEquipment"))
            foreach (var move in equipment.Elements("smartLootCorpse"))
                if (!TryRead(move, out var row) || row.Phase < 4) return true;
            return false;
        }
        catch { return true; }
    }

    internal static bool TryReadPending(World world, out RebirthP11CorpseOriginal[] originals)
    {
        originals = null;
        try
        {
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false ||
                !ReferenceEquals(GameManager.Instance?.World, world)) return false;
            var found = new List<RebirthP11CorpseOriginal>();
            var unique = new HashSet<Guid>();
            foreach (var person in RebirthNpcAggregatePersistenceStore.Snapshot())
            foreach (var equipment in (person.UnknownOptionalElements ?? Array.Empty<string>())
                .Select(XElement.Parse).Where(x => x.Name == "humanEquipment"))
            foreach (var move in equipment.Elements("smartLootCorpse"))
            {
                if (!TryRead(move, out var row) || !unique.Add(row.Operation) ||
                    row.Npc != person.Identity.StableNpcId ||
                    row.World != RebirthLogisticsTransferJournal.WorldKey(world)) return false;
                if (row.Phase < 4) found.Add(row);
            }
            if (found.Count > 4096) return false;
            originals = found.ToArray();
            return true;
        }
        catch { return false; }
    }

    internal bool SourceMatches(World world, EntityPlayer owner, bool after)
    {
        try
        {
            if (!Valid() || world?.IsRemote() != false || owner == null || !ReferenceEquals(owner.world, world) ||
                !RebirthOriginalActionScope.Matches(owner, Owner, Creation) ||
                World != RebirthLogisticsTransferJournal.WorldKey(world) ||
                !RebirthP11CorpseSource.TryResolve(world, SourceKey, out var source) ||
                !after && !(NpcActivity ? RebirthP11CorpseSource.NoNativeLock(source)
                    : RebirthP11CorpseSource.OwnsLock(source, owner.entityId)) ||
                source.bag.ItemGrid.items.Length != SourceBefore.Length ||
                source.bag.ItemGrid.SlotLocks.Length != SourceLocks.Length) return false;
            for (int i = 0; i < SourceLocks.Length; i++)
                if (source.bag.ItemGrid.SlotLocks[i] != SourceLocks[i]) return false;
            return RebirthP11CorpseWitness.GridIs(source, after ? SourceAfter : SourceBefore);
        }
        catch { return false; }
    }

    internal bool DestinationMatches(EntityRebirthHumanoidNPC npc, bool after)
    {
        try
        {
            if (!Valid() || npc?.RebirthRuntimeState?.StableId != Npc ||
                npc.bag?.ItemGrid?.items == null || npc.bag.ItemGrid.items.Length != DestinationBefore.Length ||
                npc.bag.ItemGrid.SlotLocks == null || npc.bag.ItemGrid.SlotLocks.Length != DestinationLocks.Length)
                return false;
            ItemStack[] expected = after ? DestinationAfter : DestinationBefore;
            for (int i = 0; i < expected.Length; i++)
                if (npc.bag.ItemGrid.SlotLocks[i] != DestinationLocks[i] ||
                    !Same(npc.bag.ItemGrid.items[i], expected[i])) return false;
            return true;
        }
        catch { return false; }
    }
}
