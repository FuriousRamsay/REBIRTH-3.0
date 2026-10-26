using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

// P11 wave 4 (dog side): immutable preimage of ONE recoverable "native source slot -> dog inventory"
// Smart Looting leg. It lives as one known optional element on the DOG's own aggregate row
// (UnknownOptionalElements), so the saved row, the dog inventory it credits and the revision bump
// are written by the same aggregate publication. Exact before/after images of the source grid and of
// the whole dog inventory are bound by a digest; recovery settles only through
// RebirthDogSmartLootTransfer.TryAdvance on this same row (operation id), never a fresh request.
//
// Phases: 0 prepared, 1 source debit attempted, 2 source debit proven saved (SourceProof),
// 3 dog credited (written atomically with the inventory by CommitSmartLootCredit), 4 terminal.
// Member names deliberately mirror RebirthSmartLootingHumanOriginal so the source-custody owner can
// add a dog branch next to each SmartLooting branch with the same field names (see P11 doc).
internal sealed class RebirthDogSmartLootOriginal
{
    internal const string ElementName = "rebirthDogSmartLootRow";
    internal Guid Operation;
    internal RebirthNpcStableId Dog;
    internal string Owner, Creation, World, SourceIncarnation, Binding, SourceProof, CancellationProof;
    internal Vector3i SourcePosition;
    internal int SourceSlot, Quantity, Deaths, Phase, Attempted, Published;
    internal uint DogRevision; // inventory revision of DogBefore; DogAfter is published at DogRevision + 1
    internal bool Cancelled, NpcActivity;
    internal ItemStack[] SourceBefore, SourceAfter, DogBefore, DogAfter;
    internal bool[] SourceLocks, DogLocks;

    // Same member name the human row exposes for the source-custody switch sites.
    internal int DestinationSlot => -1;

    private static bool Same(ItemStack a, ItemStack b) => RebirthStationGridIngredients.IsSameStackSnapshot(a, b);
    private static string Image(ItemStack[] stacks) => Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(stacks));
    private static string Bits(bool[] bits) => new string(bits.Select(x => x ? '1' : '0').ToArray());
    private static string Num(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string Immutable() => string.Join("|", new[] {
        Operation.ToString("N"), Dog.ToString(), Owner, Creation, World,
        Num(SourcePosition.x), Num(SourcePosition.y), Num(SourcePosition.z),
        Num(SourceSlot), Num(Quantity), Num(Deaths), Num(DogRevision), SourceIncarnation,
        Image(SourceBefore), Image(SourceAfter), Image(DogBefore), Image(DogAfter),
        Bits(SourceLocks), Bits(DogLocks)
    }.Concat(NpcActivity ? new[] { "npcActivity" } : Array.Empty<string>()));

    // Deterministic whole-stack add, identical in order to RebirthDogInventoryService.TryAdd:
    // matching stacks first (lock only protects automatic removal), then empty cells.
    internal static bool TryPlanAdd(ItemStack[] before, ItemStack incoming, out ItemStack[] after)
    {
        after = null;
        if (before == null || before.Length != RebirthDogInventoryService.Capacity || incoming == null ||
            incoming.IsEmpty() || incoming.count <= 0 || incoming.itemValue?.ItemClass == null) return false;
        var result = ItemStack.Clone((IList<ItemStack>)before);
        var remaining = incoming.Clone();
        for (int i = 0; i < result.Length && remaining.count > 0; i++)
        {
            var target = result[i];
            if (target == null || target.IsEmpty() || !target.CanStackWith(remaining)) continue;
            int room = Math.Max(0, target.itemValue.ItemClass.Stacknumber.Value - target.count);
            int move = Math.Min(room, remaining.count);
            if (move <= 0) continue;
            target.count += move; remaining.count -= move;
        }
        for (int i = 0; i < result.Length && remaining.count > 0; i++)
        {
            var target = result[i];
            if (target != null && !target.IsEmpty()) continue;
            int move = Math.Min(Math.Max(1, remaining.itemValue.ItemClass.Stacknumber.Value), remaining.count);
            var placed = remaining.Clone(); placed.count = move;
            result[i] = placed; remaining.count -= move;
        }
        if (remaining.count > 0) return false;
        after = result;
        return true;
    }

    internal bool Valid()
    {
        try
        {
            if (Operation == Guid.Empty || Dog.IsEmpty || Quantity <= 0 || DogRevision == 0 || DogRevision >= uint.MaxValue - 2 ||
                !RebirthOriginalActionScope.Validate(Owner, Creation) || World?.Length != 32 ||
                !Guid.TryParseExact(SourceIncarnation, "N", out var token) || token == Guid.Empty ||
                SourceBefore == null || SourceAfter == null || DogBefore == null || DogAfter == null ||
                SourceBefore.Length != SourceAfter.Length || SourceLocks == null || DogLocks == null ||
                SourceLocks.Length != SourceBefore.Length ||
                DogBefore.Length != RebirthDogInventoryService.Capacity || DogAfter.Length != DogBefore.Length ||
                DogLocks.Length != DogBefore.Length ||
                SourceSlot < 0 || SourceSlot >= SourceBefore.Length || SourceLocks[SourceSlot] ||
                SourceBefore.Any(x => x == null) || SourceAfter.Any(x => x == null) ||
                DogBefore.Any(x => x == null) || DogAfter.Any(x => x == null) ||
                SourceBefore[SourceSlot].IsEmpty() || SourceBefore[SourceSlot].count != Quantity ||
                !SourceAfter[SourceSlot].IsEmpty() ||
                !RebirthDogInventoryPolicy.IsDogAutoLootItem(SourceBefore[SourceSlot].itemValue) ||
                Binding != RebirthRemoteResourceDurableJournal.Digest(Immutable()) ||
                Deaths < 0 || Phase < 0 || Phase > 4 || Attempted < 0 || Attempted > 1 ||
                Published < 0 || Published > 1 || Published > Attempted ||
                Phase == 0 && (Attempted != 0 || Published != 0) ||
                Phase == 1 && Attempted != 1 ||
                !Cancelled && Phase >= 2 && Published != 1 ||
                Cancelled && (Phase != 4 || Published != 0 ||
                    !string.IsNullOrEmpty(SourceProof) || CancellationProof?.Length != 64) ||
                !Cancelled && !string.IsNullOrEmpty(CancellationProof) ||
                !string.IsNullOrEmpty(SourceProof) && !Hex64(SourceProof) ||
                Published == 1 && SourceProof?.Length != 64 ||
                !string.IsNullOrEmpty(CancellationProof) && !Hex64(CancellationProof)) return false;
            for (int i = 0; i < SourceBefore.Length; i++)
                if (i != SourceSlot && !Same(SourceBefore[i], SourceAfter[i])) return false;
            if (!TryPlanAdd(DogBefore, SourceBefore[SourceSlot], out var planned)) return false;
            for (int i = 0; i < planned.Length; i++) if (!Same(planned[i], DogAfter[i])) return false;
            return true;
        }
        catch { return false; }
    }

    private static bool Hex64(string value) =>
        value.Length == 64 && value.All(x => Uri.IsHexDigit(x)) && value == value.ToLowerInvariant();

    internal static bool TryPlan(RebirthDogSmartLootInputs inputs, out RebirthDogSmartLootOriginal original)
    {
        original = null;
        try
        {
            var candidate = new RebirthDogSmartLootOriginal {
                Operation = inputs.Operation, Dog = inputs.Dog, Owner = inputs.Owner, Creation = inputs.Creation,
                World = inputs.World, Deaths = inputs.Deaths, SourcePosition = inputs.SourcePosition,
                SourceSlot = inputs.SourceSlot, Quantity = inputs.SourceBefore[inputs.SourceSlot].count,
                SourceIncarnation = inputs.SourceIncarnation, DogRevision = inputs.DogRevision,
                NpcActivity = inputs.NpcActivity, SourceBefore = inputs.SourceBefore,
                SourceAfter = ItemStack.Clone((IList<ItemStack>)inputs.SourceBefore),
                DogBefore = inputs.DogBefore, DogLocks = inputs.DogLocks, SourceLocks = inputs.SourceLocks
            };
            candidate.SourceAfter[candidate.SourceSlot] = ItemStack.Empty.Clone();
            if (!TryPlanAdd(candidate.DogBefore, candidate.SourceBefore[candidate.SourceSlot], out candidate.DogAfter))
                return false;
            candidate.Binding = RebirthRemoteResourceDurableJournal.Digest(candidate.Immutable());
            if (!candidate.Valid()) return false;
            original = candidate;
            return true;
        }
        catch { return false; }
    }

    internal XElement Write()
    {
        if (!Valid()) throw new System.IO.InvalidDataException("Invalid Smart Looting dog original.");
        var element = new XElement(ElementName, new XAttribute("version", 1),
            new XAttribute("operation", Operation.ToString("N")), new XAttribute("dog", Dog.ToString()),
            new XAttribute("owner", Owner), new XAttribute("creation", Creation),
            new XAttribute("world", World), new XAttribute("incarnation", SourceIncarnation),
            new XAttribute("sourceX", SourcePosition.x), new XAttribute("sourceY", SourcePosition.y),
            new XAttribute("sourceZ", SourcePosition.z), new XAttribute("sourceSlot", SourceSlot),
            new XAttribute("quantity", Quantity), new XAttribute("deaths", Deaths),
            new XAttribute("dogRevision", DogRevision),
            new XAttribute("binding", Binding), new XAttribute("phase", Phase),
            new XAttribute("attempted", Attempted), new XAttribute("published", Published),
            new XAttribute("sourceProof", SourceProof ?? string.Empty),
            new XAttribute("cancelled", Cancelled ? 1 : 0),
            new XAttribute("cancellationProof", CancellationProof ?? string.Empty),
            new XElement("sourceBefore", Image(SourceBefore)), new XElement("sourceAfter", Image(SourceAfter)),
            new XElement("dogBefore", Image(DogBefore)), new XElement("dogAfter", Image(DogAfter)),
            new XElement("sourceLocks", Bits(SourceLocks)), new XElement("dogLocks", Bits(DogLocks)));
        if (NpcActivity) element.SetAttributeValue("npcActivity", 1);
        return element;
    }

    internal static bool TryRead(XElement node, out RebirthDogSmartLootOriginal original)
    {
        original = null;
        try
        {
            if (node?.Name != ElementName || (string)node.Attribute("version") != "1" ||
                node.Attributes().Count() != (node.Attribute("npcActivity") != null ? 22 : 21) ||
                node.Attribute("npcActivity") != null && (string)node.Attribute("npcActivity") != "1" ||
                !node.Elements().Select(x => x.Name.LocalName).SequenceEqual(new[] {
                    "sourceBefore", "sourceAfter", "dogBefore", "dogAfter", "sourceLocks", "dogLocks"
                }) || !RebirthNpcStableId.TryParse((string)node.Attribute("dog"), out var dog)) return false;
            string sourceLocks = node.Element("sourceLocks")?.Value, dogLocks = node.Element("dogLocks")?.Value;
            if (sourceLocks == null || dogLocks == null || sourceLocks.Any(x => x != '0' && x != '1') ||
                dogLocks.Any(x => x != '0' && x != '1')) return false;
            ItemStack[] Img(string name) => RebirthCamperInventorySnapshot.Restore(
                Convert.FromBase64String(node.Element(name)?.Value ?? ""));
            var candidate = new RebirthDogSmartLootOriginal {
                Operation = Guid.ParseExact((string)node.Attribute("operation"), "N"), Dog = dog,
                Owner = (string)node.Attribute("owner"), Creation = (string)node.Attribute("creation"),
                World = (string)node.Attribute("world"), SourceIncarnation = (string)node.Attribute("incarnation"),
                SourcePosition = new Vector3i((int)node.Attribute("sourceX"), (int)node.Attribute("sourceY"),
                    (int)node.Attribute("sourceZ")),
                SourceSlot = (int)node.Attribute("sourceSlot"), Quantity = (int)node.Attribute("quantity"),
                Deaths = (int)node.Attribute("deaths"), DogRevision = (uint)node.Attribute("dogRevision"),
                Binding = (string)node.Attribute("binding"), Phase = (int)node.Attribute("phase"),
                Attempted = (int)node.Attribute("attempted"), Published = (int)node.Attribute("published"),
                SourceProof = (string)node.Attribute("sourceProof"),
                Cancelled = (string)node.Attribute("cancelled") == "1",
                NpcActivity = node.Attribute("npcActivity") != null,
                CancellationProof = (string)node.Attribute("cancellationProof"),
                SourceBefore = Img("sourceBefore"), SourceAfter = Img("sourceAfter"),
                DogBefore = Img("dogBefore"), DogAfter = Img("dogAfter"),
                SourceLocks = sourceLocks.Select(x => x == '1').ToArray(),
                DogLocks = dogLocks.Select(x => x == '1').ToArray()
            };
            if (((string)node.Attribute("cancelled") != "0" && (string)node.Attribute("cancelled") != "1") ||
                !candidate.Valid() || !XNode.DeepEquals(candidate.Write(), node)) return false;
            original = candidate;
            return true;
        }
        catch { return false; }
    }

    // The single row element on a dog record. 'foreign' is false when no row exists. A malformed
    // or duplicated element yields false so nothing is ever settled from an ambiguous record.
    internal static bool TryFind(RebirthNpcPersistentRecord record, out RebirthDogSmartLootOriginal row)
    {
        row = null;
        try
        {
            var found = new List<XElement>();
            foreach (string text in record?.UnknownOptionalElements ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith("<" + ElementName, StringComparison.Ordinal))
                    continue;
                found.Add(XElement.Parse(text));
            }
            if (found.Count != 1) return false;
            return TryRead(found[0], out row);
        }
        catch { row = null; return false; }
    }

    // True when the record carries a row element that is not a valid terminal row.
    internal static bool HasUnsettledElement(RebirthNpcPersistentRecord record)
    {
        try
        {
            int count = 0;
            foreach (string text in record?.UnknownOptionalElements ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("<" + ElementName, StringComparison.Ordinal))
                    count++;
            if (count == 0) return false;
            return count != 1 || !TryFind(record, out var row) || row.Phase < 4;
        }
        catch { return true; }
    }

    // Replaces (or appends) the row element, preserving every unrelated optional element.
    internal static void Set(RebirthNpcPersistentRecord record, RebirthDogSmartLootOriginal next)
    {
        var values = new List<string>();
        foreach (string text in record.UnknownOptionalElements ?? Array.Empty<string>())
            if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith("<" + ElementName, StringComparison.Ordinal))
                values.Add(text);
        values.Add(next.Write().ToString(SaveOptions.DisableFormatting));
        record.UnknownOptionalElements = values.ToArray();
    }

    internal static bool TryReadSaved(RebirthNpcStableId dog, Guid operation, out RebirthDogSmartLootOriginal original)
    {
        original = null;
        try
        {
            return RebirthNpcAggregatePersistenceStore.TryReadSavedLogisticsDog(dog, out var saved) &&
                TryFind(saved, out var row) && row.Dog == dog && row.Operation == operation && (original = row) != null;
        }
        catch { original = null; return false; }
    }

    // Same lookup shape as RebirthSmartLootingHumanOriginal.TryRead(owner, operation, sourceId), but the
    // dog is located from the in-memory records first: TryReadSaved parses the whole saved file, and the
    // source custody calls this from every Current()/MayApply()/AttemptedStage() check.
    internal static bool TryRead(string owner, Guid operation, string sourceId, out RebirthDogSmartLootOriginal original)
    {
        original = null;
        try
        {
            RebirthNpcStableId? found = null;
            foreach (var record in RebirthNpcAggregatePersistenceStore.Snapshot())
            {
                if (!RebirthDogStateService.IsDog(record) || !TryFind(record, out var live) || live.Operation != operation)
                    continue;
                if (found != null) return false;
                found = record.Identity.StableNpcId;
            }
            if (found == null || !TryReadSaved(found.Value, operation, out var candidate) ||
                candidate.Owner != owner || RemoteResourceIdentity.Static(candidate.SourcePosition) != sourceId) return false;
            original = candidate;
            return true;
        }
        catch { original = null; return false; }
    }

    // True only when NO dog record carries the operation id in any optional element (a malformed or
    // unparseable row still counts as present). The aggregate store must be loaded first.
    internal static bool OperationAbsent(Guid operation)
    {
        try
        {
            if (!ThreadManager.IsMainThread() || GameManager.Instance?.World?.IsRemote() != false) return false;
            RebirthNpcAggregatePersistenceStore.EnsureLoaded();
            string key = operation.ToString("N");
            foreach (var record in RebirthNpcAggregatePersistenceStore.Snapshot())
                foreach (string text in record?.UnknownOptionalElements ?? Array.Empty<string>())
                    if (text != null && text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }
        catch { return false; }
    }

    // In-memory scan of every non-terminal row. False on any malformed element (fail closed).
    internal static bool TryReadPending(World world, out RebirthDogSmartLootOriginal[] originals)
    {
        originals = null;
        try
        {
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false ||
                !ReferenceEquals(GameManager.Instance?.World, world)) return false;
            var found = new List<RebirthDogSmartLootOriginal>();
            var unique = new HashSet<Guid>();
            foreach (var record in RebirthNpcAggregatePersistenceStore.Snapshot())
            {
                if (!RebirthDogStateService.IsDog(record)) continue;
                if (!HasUnsettledElement(record)) continue;
                if (!TryFind(record, out var row) || row.Dog != record.Identity.StableNpcId ||
                    !unique.Add(row.Operation) || row.World != RebirthLogisticsTransferJournal.WorldKey(world)) return false;
                found.Add(row);
            }
            originals = found.ToArray();
            return true;
        }
        catch { return false; }
    }

    internal static bool HasPending(RebirthNpcStableId dog)
    {
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryGet(dog, out var record)) return true;
            return HasUnsettledElement(record);
        }
        catch { return true; }
    }

    internal bool SourceMatches(World world, bool after)
    {
        try
        {
            if (!Valid() || world?.IsRemote() != false || World != RebirthLogisticsTransferJournal.WorldKey(world)) return false;
            TEFeatureStorage source = world.GetTileEntity(SourcePosition)?.GetSelfOrFeature<TEFeatureStorage>();
            if (source?.ItemGrid?.items == null || source.ToWorldPos() != SourcePosition ||
                !RebirthP11StorageIncarnation.TryResolve(world, source, false, out string current) ||
                current != SourceIncarnation || source.ItemGrid.items.Length != SourceBefore.Length ||
                source.ItemGrid.SlotLocks == null || source.ItemGrid.SlotLocks.Length != SourceLocks.Length) return false;
            ItemStack[] expected = after ? SourceAfter : SourceBefore;
            for (int i = 0; i < expected.Length; i++)
                if (source.ItemGrid.SlotLocks[i] != SourceLocks[i] || !Same(source.ItemGrid.items[i], expected[i])) return false;
            return true;
        }
        catch { return false; }
    }

    // Positive evidence that the source is no longer the planned one: the chunk is loaded and the storage
    // is gone, a different incarnation, or its grid/locks differ from the before image. An unloaded chunk
    // or an unresolvable token is NOT divergence (the leg just waits). Only meaningful at phase 0, where
    // Attempted == 0 proves no debit was ever applied.
    internal bool SourceDiverged(World world)
    {
        try
        {
            if (!Valid() || world?.IsRemote() != false || World != RebirthLogisticsTransferJournal.WorldKey(world)) return false;
            var chunk = world.ChunkCache?.GetChunkFromWorldPos(SourcePosition.x, SourcePosition.y, SourcePosition.z) as Chunk;
            if (chunk == null || chunk.InProgressUnloading) return false;
            TEFeatureStorage source = world.GetTileEntity(SourcePosition)?.GetSelfOrFeature<TEFeatureStorage>();
            if (source?.ItemGrid?.items == null) return true;
            if (!RebirthP11StorageIncarnation.TryResolve(world, source, false, out string current)) return false;
            return current != SourceIncarnation || !SourceMatches(world, false);
        }
        catch { return false; }
    }

    // The saved dog record exists and its live inventory is not the before image.
    internal bool DogDiverged()
    {
        try
        {
            return RebirthNpcAggregatePersistenceStore.TryGet(Dog, out var record) &&
                RebirthDogStateService.IsDog(record) && !DogMatches(false);
        }
        catch { return false; }
    }

    // Live (in-memory) dog inventory equals the before or after image exactly, locks included.
    internal bool DogMatches(bool after)
    {
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryGet(Dog, out var record) || !RebirthDogStateService.IsDog(record))
                return false;
            return RebirthRemoteResourceDogTransfer.Matches(record, after ? DogAfter : DogBefore, DogLocks,
                after ? DogRevision + 1 : DogRevision);
        }
        catch { return false; }
    }
}

internal struct RebirthDogSmartLootInputs
{
    internal Guid Operation;
    internal RebirthNpcStableId Dog;
    internal string Owner, Creation, World, SourceIncarnation;
    internal int Deaths, SourceSlot;
    internal uint DogRevision;
    internal Vector3i SourcePosition;
    internal bool NpcActivity;
    internal ItemStack[] SourceBefore, DogBefore;
    internal bool[] SourceLocks, DogLocks;
}
