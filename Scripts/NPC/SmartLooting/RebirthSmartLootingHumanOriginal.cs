using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Immutable preimage for a future recoverable Take All transfer. This does not
// mutate a container or grant a transfer permit; B11 stays gated until a source
// lease, destination credit witness, and restart recovery consume this original.
internal sealed class RebirthSmartLootingHumanOriginal
{
    internal Guid Operation;
    internal RebirthNpcStableId Npc;
    internal string Owner, Creation, World, SourceFrame, SourceIncarnation, Binding, SourceProof, CancellationProof;
    internal Vector3i SourcePosition;
    internal int SourceSlot, DestinationSlot, Quantity, Deaths;
    internal int Phase, Attempted, Published;
    internal bool Cancelled;
    // NPC-led Follow activity: no player lock exists, so the source is admitted by the
    // absence of any native lock instead of OwnsLootLock. Written only when true so
    // existing rows keep their 21-attribute shape and binding digest.
    internal bool NpcActivity;
    internal ItemStack[] SourceBefore, SourceAfter, DestinationBefore, DestinationAfter;
    internal bool[] SourceLocks, DestinationLocks;

    private static bool Same(ItemStack a, ItemStack b) =>
        RebirthStationGridIngredients.IsSameStackSnapshot(a, b);

    private static string Image(ItemStack[] stacks) =>
        Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(stacks));

    private string Immutable() => string.Join("|", new[] {
        Operation.ToString("N"), Npc.ToString(), Owner, Creation, World,
        SourcePosition.x.ToString(System.Globalization.CultureInfo.InvariantCulture),
        SourcePosition.y.ToString(System.Globalization.CultureInfo.InvariantCulture),
        SourcePosition.z.ToString(System.Globalization.CultureInfo.InvariantCulture),
        SourceSlot.ToString(System.Globalization.CultureInfo.InvariantCulture),
        DestinationSlot.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture), SourceFrame, SourceIncarnation,
        Image(SourceBefore), Image(SourceAfter), Image(DestinationBefore), Image(DestinationAfter),
        new string(SourceLocks.Select(x => x ? '1' : '0').ToArray()),
        new string(DestinationLocks.Select(x => x ? '1' : '0').ToArray())
    }.Concat(NpcActivity ? new[] { "npcActivity" } : Array.Empty<string>()));

    internal bool Valid()
    {
        try
        {
            if (Operation == Guid.Empty || Npc.IsEmpty || Quantity <= 0 ||
                !RebirthOriginalActionScope.Validate(Owner, Creation) || World?.Length != 32 ||
                !Guid.TryParseExact(SourceIncarnation, "N", out var sourceToken) || sourceToken == Guid.Empty ||
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
                !string.IsNullOrEmpty(SourceProof) &&
                    (SourceProof.Length != 64 || SourceProof.Any(x => !Uri.IsHexDigit(x)) ||
                     SourceProof != SourceProof.ToLowerInvariant()) ||
                Published == 1 && SourceProof?.Length != 64 ||
                !string.IsNullOrEmpty(CancellationProof) &&
                    (CancellationProof.Length != 64 || CancellationProof.Any(x => !Uri.IsHexDigit(x)) ||
                     CancellationProof != CancellationProof.ToLowerInvariant())) return false;

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

    internal static bool TryPlan(EntityPlayer owner, TEFeatureStorage source, int sourceSlot,
        EntityRebirthHumanoidNPC npc, int destinationSlot, Guid operation,
        out RebirthSmartLootingHumanOriginal original, bool npcActivity = false)
    {
        original = null;
        try
        {
            World world = GameManager.Instance?.World;
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false || operation == Guid.Empty ||
                owner == null || source == null || npc?.RebirthRuntimeState == null ||
                owner.IsDead() || npc.IsDead() || npc.IsPreparedRestorationPending ||
                !ReferenceEquals(owner.world, world) || !ReferenceEquals(npc.world, world) ||
                !ReferenceEquals(world.GetEntity(owner.entityId), owner) ||
                !ReferenceEquals(world.GetEntity(npc.entityId), npc) ||
                npc.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity) ||
                !string.Equals(identity.CanonicalId, npc.RebirthRuntimeState.OwnerId, StringComparison.OrdinalIgnoreCase) ||
                !(npcActivity ? RebirthP11NpcLootAccess.NoNativeLock(source) : OwnsLootLock(source, owner.entityId)) ||
                (owner.position - source.ToWorldCenterPos()).sqrMagnitude >
                    (Constants.cCollectItemDistance + 30f) * (Constants.cCollectItemDistance + 30f) ||
                (npc.position - source.ToWorldCenterPos()).sqrMagnitude >
                    RebirthCompanionService.StorageDistance * RebirthCompanionService.StorageDistance ||
                npc.bag?.ItemGrid?.items == null ||
                source.ItemGrid?.items == null || sourceSlot < 0 || sourceSlot >= source.ItemGrid.items.Length ||
                destinationSlot < 0 || destinationSlot >= npc.bag.SlotCount ||
                source.ItemGrid.SlotLocks == null || source.ItemGrid.SlotLocks.Length != source.ItemGrid.items.Length ||
                npc.bag.ItemGrid.SlotLocks == null || npc.bag.ItemGrid.SlotLocks.Length != npc.bag.SlotCount ||
                source.ItemGrid.SlotLocks[sourceSlot] || npc.bag.ItemGrid.SlotLocks[destinationSlot]) return false;

            ItemStack incoming = source.ItemGrid.items[sourceSlot];
            ItemStack target = npc.bag.ItemGrid[destinationSlot];
            // Generated storage gets its marker only after every other check passes.
            bool freshToken = !RebirthP11StorageIncarnation.TryResolve(world, source, false, out string incarnation);
            if (freshToken) incarnation = Guid.NewGuid().ToString("N");
            if (!Guid.TryParseExact(incarnation, "N", out var sourceToken) || sourceToken == Guid.Empty)
                return false;
            if (incoming == null || incoming.IsEmpty() || incoming.count <= 0 ||
                incoming.itemValue?.ItemClass == null ||
                !incoming.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack, destinationSlot) ||
                !RebirthHumanEquipment.Accept(10 + destinationSlot, incoming) ||
                target != null && !target.IsEmpty() && !target.CanStackWith(incoming)) return false;
            int maximum = Math.Max(1, incoming.itemValue.ItemClass.Stacknumber.Value);
            int room = maximum - (target == null || target.IsEmpty() ? 0 : target.count);
            int quantity = Math.Min(incoming.count, room);
            if (quantity <= 0) return false;

            var sourceBefore = ItemStack.Clone((IList<ItemStack>)source.ItemGrid.items);
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
            var candidate = new RebirthSmartLootingHumanOriginal {
                Operation = operation, Npc = npc.RebirthRuntimeState.StableId,
                Owner = identity.StorageKey, Creation = RebirthCamperCharacterAccess.CurrentCreation(owner),
                World = RebirthLogisticsTransferJournal.WorldKey(world), Deaths = owner.Died,
                SourcePosition = source.ToWorldPos(), SourceSlot = sourceSlot,
                DestinationSlot = destinationSlot, Quantity = quantity,
                SourceFrame = RebirthHumanEquipment.Frame(incoming), SourceIncarnation = incarnation,
                NpcActivity = npcActivity,
                SourceBefore = sourceBefore, SourceAfter = sourceAfter,
                DestinationBefore = destinationBefore, DestinationAfter = destinationAfter,
                SourceLocks = Enumerable.Range(0, sourceBefore.Length).Select(i => source.ItemGrid.SlotLocks[i]).ToArray(),
                DestinationLocks = Enumerable.Range(0, destinationBefore.Length).Select(i => npc.bag.ItemGrid.SlotLocks[i]).ToArray()
            };
            candidate.Binding = RebirthRemoteResourceDurableJournal.Digest(candidate.Immutable());
            if (!candidate.Valid()) return false;
            if (freshToken && !RebirthP11StorageIncarnation.TryMint(world, source, incarnation)) return false;
            original = candidate;
            return true;
        }
        catch { return false; }
    }

    private static bool OwnsLootLock(ILockTarget source, int playerId)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (source == null || connection == null || !connection.IsServer) return false;
        var entry = new LockManager.LockEntry(source, 0);
        if (source.IsSharedLock(0))
        {
            if (!LockManager.Instance.sharedLocks.TryGetByValue(entry, out IReadOnlyCollection<int> owners) || owners == null)
                return false;
            return owners.Contains(playerId);
        }
        return LockManager.Instance.singleLocks.TryGetByValue(entry, out int owner) && owner == playerId;
    }

    internal XElement Write()
    {
        if (!Valid()) throw new System.IO.InvalidDataException("Invalid Smart Looting human original.");
        var element = new XElement("smartLootMove", new XAttribute("version", 1),
            new XAttribute("operation", Operation.ToString("N")), new XAttribute("npc", Npc.ToString()),
            new XAttribute("owner", Owner), new XAttribute("creation", Creation),
            new XAttribute("world", World), new XAttribute("incarnation", SourceIncarnation),
            new XAttribute("sourceX", SourcePosition.x),
            new XAttribute("sourceY", SourcePosition.y),
            new XAttribute("sourceZ", SourcePosition.z),
            new XAttribute("sourceSlot", SourceSlot), new XAttribute("destinationSlot", DestinationSlot),
            new XAttribute("quantity", Quantity), new XAttribute("deaths", Deaths),
            new XAttribute("binding", Binding), new XAttribute("phase", Phase),
            new XAttribute("attempted", Attempted), new XAttribute("published", Published),
            new XAttribute("sourceProof", SourceProof ?? string.Empty),
            new XAttribute("cancelled", Cancelled ? 1 : 0),
            new XAttribute("cancellationProof", CancellationProof ?? string.Empty),
            new XElement("stack", SourceFrame),
            new XElement("sourceBefore", Image(SourceBefore)),
            new XElement("sourceAfter", Image(SourceAfter)),
            new XElement("destinationBefore", Image(DestinationBefore)),
            new XElement("destinationAfter", Image(DestinationAfter)),
            new XElement("sourceLocks", new string(SourceLocks.Select(x => x ? '1' : '0').ToArray())),
            new XElement("destinationLocks", new string(DestinationLocks.Select(x => x ? '1' : '0').ToArray())));
        if (NpcActivity) element.SetAttributeValue("npcActivity", 1);
        return element;
    }

    internal static bool TryRead(XElement node, out RebirthSmartLootingHumanOriginal original)
    {
        original = null;
        try
        {
            if (node?.Name != "smartLootMove" || (string)node.Attribute("version") != "1" ||
                node.Attributes().Count() != (node.Attribute("npcActivity") != null ? 22 : 21) ||
                node.Attribute("npcActivity") != null && (string)node.Attribute("npcActivity") != "1" ||
                !node.Elements().Select(x => x.Name.LocalName).SequenceEqual(new[] {
                    "stack", "sourceBefore", "sourceAfter", "destinationBefore", "destinationAfter",
                    "sourceLocks", "destinationLocks"
                }) || !RebirthNpcStableId.TryParse((string)node.Attribute("npc"), out var npc)) return false;
            var candidate = new RebirthSmartLootingHumanOriginal {
                Operation = Guid.ParseExact((string)node.Attribute("operation"), "N"), Npc = npc,
                Owner = (string)node.Attribute("owner"), Creation = (string)node.Attribute("creation"),
                World = (string)node.Attribute("world"), SourceIncarnation = (string)node.Attribute("incarnation"),
                SourcePosition = new Vector3i((int)node.Attribute("sourceX"),
                    (int)node.Attribute("sourceY"), (int)node.Attribute("sourceZ")),
                SourceSlot = (int)node.Attribute("sourceSlot"), DestinationSlot = (int)node.Attribute("destinationSlot"),
                Quantity = (int)node.Attribute("quantity"), Deaths = (int)node.Attribute("deaths"),
                Binding = (string)node.Attribute("binding"), Phase = (int)node.Attribute("phase"),
                Attempted = (int)node.Attribute("attempted"), Published = (int)node.Attribute("published"),
                SourceProof = (string)node.Attribute("sourceProof"),
                Cancelled = (string)node.Attribute("cancelled") == "1",
                NpcActivity = node.Attribute("npcActivity") != null,
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
                sourceLocks.Any(x => x != '0' && x != '1') ||
                destinationLocks.Any(x => x != '0' && x != '1')) return false;
            candidate.SourceLocks = sourceLocks.Select(x => x == '1').ToArray();
            candidate.DestinationLocks = destinationLocks.Select(x => x == '1').ToArray();
            if (((string)node.Attribute("cancelled") != "0" &&
                 (string)node.Attribute("cancelled") != "1") ||
                !candidate.Valid() || !XNode.DeepEquals(candidate.Write(), node)) return false;
            original = candidate;
            return true;
        }
        catch { return false; }
    }

    internal static bool TryReadSaved(RebirthNpcStableId npc, Guid operation,
        out RebirthSmartLootingHumanOriginal original)
    {
        original = null;
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var person)) return false;
            var moves = (person.UnknownOptionalElements ?? Array.Empty<string>())
                .Select(XElement.Parse).Where(x => x.Name == "humanEquipment")
                .SelectMany(x => x.Elements("smartLootMove"))
                .Where(x => (string)x.Attribute("operation") == operation.ToString("N")).ToArray();
            if (moves.Length != 1 || !TryRead(moves[0], out var found) ||
                found.Npc != npc || found.Operation != operation) return false;
            original = found;
            return true;
        }
        catch { return false; }
    }

    internal static bool TryPublish(EntityRebirthHumanoidNPC npc,
        RebirthSmartLootingHumanOriginal next)
    {
        try
        {
            if (!ThreadManager.IsMainThread() || npc?.world?.IsRemote() != false ||
                next == null || npc.RebirthRuntimeState.StableId != next.Npc || !next.Valid()) return false;
            XElement equipment = RebirthHumanEquipment.Read(npc);
            XElement[] matches = equipment.Elements("smartLootMove")
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
                    equipment.Elements("smartLootMove").Count() >= 128 ||
                    equipment.Elements("smartLootMove").Any(x =>
                        !TryRead(x, out var existing) || existing.Phase < 4)) return false;
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
            if (RebirthP11CorpseOriginal.HasPending(npc)) return true;
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var person))
            {
                if (!RebirthNpcAggregatePersistenceStore.TryGet(npc, out person)) return true;
            }
            foreach (var equipment in (person.UnknownOptionalElements ?? Array.Empty<string>())
                .Select(XElement.Parse).Where(x => x.Name == "humanEquipment"))
            foreach (var move in equipment.Elements("smartLootMove"))
                if (!TryRead(move, out var row) || row.Phase < 4) return true;
            return false;
        }
        catch { return true; }
    }

    internal static bool TryRead(string owner, Guid operation, string sourceId,
        out RebirthSmartLootingHumanOriginal original)
    {
        original = null;
        try
        {
            foreach (var person in RebirthNpcAggregatePersistenceStore.Snapshot())
            {
                if (!TryReadSaved(person.Identity.StableNpcId, operation, out var candidate)) continue;
                if (candidate.Owner != owner || RemoteResourceIdentity.Static(candidate.SourcePosition) != sourceId)
                    continue;
                if (original != null) return false;
                original = candidate;
            }
            return original != null;
        }
        catch { original = null; return false; }
    }

    internal static bool TryReadPending(World world, out RebirthSmartLootingHumanOriginal[] originals)
    {
        originals = null;
        try
        {
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false ||
                !ReferenceEquals(GameManager.Instance?.World, world)) return false;
            var found = new List<RebirthSmartLootingHumanOriginal>();
            var unique = new HashSet<Guid>();
            foreach (var person in RebirthNpcAggregatePersistenceStore.Snapshot())
            foreach (var equipment in (person.UnknownOptionalElements ?? Array.Empty<string>())
                .Select(XElement.Parse).Where(x => x.Name == "humanEquipment"))
            foreach (var move in equipment.Elements("smartLootMove"))
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
            if (!Valid() || world?.IsRemote() != false || owner == null ||
                !ReferenceEquals(owner.world, world) ||
                !RebirthOriginalActionScope.Matches(owner, Owner, Creation) ||
                World != RebirthLogisticsTransferJournal.WorldKey(world)) return false;
            TEFeatureStorage source = world.GetTileEntity(SourcePosition)?.GetSelfOrFeature<TEFeatureStorage>();
            if (source?.ItemGrid?.items == null || source.ToWorldPos() != SourcePosition ||
                !RebirthP11StorageIncarnation.TryResolve(world, source, false, out string currentIncarnation) ||
                currentIncarnation != SourceIncarnation ||
                !after && !(NpcActivity ? RebirthP11NpcLootAccess.NoNativeLock(source) : OwnsLootLock(source, owner.entityId)) ||
                source.ItemGrid.items.Length != SourceBefore.Length ||
                source.ItemGrid.SlotLocks == null || source.ItemGrid.SlotLocks.Length != SourceLocks.Length)
                return false;
            ItemStack[] expected = after ? SourceAfter : SourceBefore;
            for (int i = 0; i < expected.Length; i++)
                if (source.ItemGrid.SlotLocks[i] != SourceLocks[i] ||
                    !Same(source.ItemGrid.items[i], expected[i])) return false;
            return true;
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
