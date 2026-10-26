using System;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

// B14 integration seams: Follow policy, Desk volume access (read-only, for U4 and the planner) and the
// credit sink (worker bag; owned storage HELD). Decisions: P12_B14_PROVISIONAL_DECISIONS_20261022.md.

internal static class RebirthNpcB14Policy
{
    // Mining/Logging are base jobs: they never start a strike under Follow. The same saved assignment
    // is resumed by the Work EAI when the order returns to a non-Follow state (no new assignment).
    internal static bool MayStrike(EntityRebirthNPC npc, out string detail)
    {
        detail = string.Empty;
        if (npc == null || npc.IsDead()) { detail = "Worker is unavailable."; return false; }
        if (RebirthP6WorkFollowState.IsFollowing(npc))
        { detail = "Mining/logging is suspended while the worker follows."; return false; }
        return true;
    }
}

internal static class RebirthNpcB14VolumeAccess
{
    // Exact (camper-aware) containment of a block cell in the owner's saved Mining or Logging volume.
    internal static Func<Vector3i, bool> CreateContains(World world, PlatformUserIdentifierAbs owner,
        string targetId, RebirthNpcB14Kind kind)
    {
        string desk = kind == RebirthNpcB14Kind.Logging ? "Logging" : "Mining";
        return cell =>
        {
            long revision;
            return RebirthCompanionDeskStore.TryContainsTargetPoint(world, owner, targetId, desk,
                new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f), out revision);
        };
    }
}

// Credits a witnessed stack into ONE empty worker-bag cell (the owned-storage tier is HELD: no destination
// selection is designed, so it is not implemented rather than claimed). Idempotent per (operation, index)
// through a WRITE-AHEAD journal in the saved humanEquipment element: the intended slot and exact stack frame
// are saved BEFORE the item is written, the final state after (bag-prepared -> bag-bound). A re-entry completes
// the reserved cell, never a second one. A stack the bag cannot hold is refused so the strike bridge retains it
// at its physical source with the no-loss overflow re-drop.
internal sealed class RebirthNpcB14BagCreditSink : IRebirthNpcB14CreditSink
{
    private const int JournalLimit = 64;
    private readonly EntityRebirthHumanoidNPC npc;

    internal RebirthNpcB14BagCreditSink(EntityRebirthHumanoidNPC npc)
    { this.npc = npc; }

    internal static Guid CreditId(Guid operation, int index)
    {
        byte[] seed = new byte[20];
        Buffer.BlockCopy(operation.ToByteArray(), 0, seed, 0, 16);
        Buffer.BlockCopy(BitConverter.GetBytes(index), 0, seed, 16, 4);
        using (MD5 md5 = MD5.Create()) return new Guid(md5.ComputeHash(seed));
    }

    private bool Ready()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        return ThreadManager.IsMainThread() && world != null && !world.IsRemote() && npc != null && !npc.IsDead() &&
            !npc.IsPreparedRestorationPending && npc.bag != null && npc.bag.ItemGrid != null &&
            npc.RebirthRuntimeState != null && ReferenceEquals(world.GetEntity(npc.entityId), npc) &&
            !RebirthNpcInteractionSessionService.IsInventoryAccessing(npc.RebirthRuntimeState.StableId);
    }

    // The `bag-bound` marker is only ever written AFTER the stack, the binding and the saved native-stack record were
    // verified, so a bound marker whose proof later fails means custody MOVED (consumed, stored, moved): that is
    // Ambiguous, never a reason to place another copy.
    private bool BoundVerified(XElement entry, XElement bindings, string id, string frame)
    {
        int slot = (int)entry.Attribute("slot");
        return slot >= 0 && slot < npc.bag.SlotCount && (string)entry.Attribute("frame") == frame &&
            RebirthHumanEquipment.Frame(npc.bag.ItemGrid[slot]) == frame &&
            bindings.Elements("binding").Any(b => (string)b.Attribute("id") == id && (string)b.Attribute("area") == "bag" &&
                (int)b.Attribute("index") == slot) &&
            SavedCredit(npc.RebirthRuntimeState.StableId, id, slot, frame);
    }

    public bool HasCredit(Guid operationId, int witnessIndex, ItemStack stack)
    {
        try
        {
            if (!Ready() || stack == null || stack.IsEmpty()) return false;
            string id = CreditId(operationId, witnessIndex).ToString("N");
            XElement equipment = RebirthHumanEquipment.Read(npc);
            XElement journal = equipment.Element("b14credits");
            XElement bindings = equipment.Element("bindings");
            XElement entry = journal == null ? null : journal.Elements("c").FirstOrDefault(x => (string)x.Attribute("id") == id);
            if (entry == null || bindings == null) return false;
            string entryState = (string)entry.Attribute("state");
            if ((entryState != "bag-bound" && entryState != "bag-prepared") ||
                !BoundVerified(entry, bindings, id, RebirthHumanEquipment.Frame(stack))) return false;
            // A prepared journal whose live cell + binding + saved record prove OUR placement is a valid credit
            // (crash between the verified placement and the marker promotion): advance the marker, best effort.
            if (entryState == "bag-prepared") TryPromote(entry, equipment);
            return true;
        }
        catch (Exception) { return false; }
    }

    private void TryPromote(XElement entry, XElement equipment)
    {
        try
        {
            entry.SetAttributeValue("state", "bag-bound");
            RebirthHumanEquipment.Save(npc, equipment);
        }
        catch (Exception) { }
    }

    public RebirthNpcB14CreditResult TryCredit(Guid operationId, int witnessIndex, ItemStack stack)
    {
        try
        {
            if (!Ready() || stack == null || stack.IsEmpty()) return RebirthNpcB14CreditResult.Retry;
            string id = CreditId(operationId, witnessIndex).ToString("N");
            string frame = RebirthHumanEquipment.Frame(stack);
            XElement equipment = RebirthHumanEquipment.Read(npc);
            XElement journal = equipment.Element("b14credits");
            if (journal == null) { journal = new XElement("b14credits"); equipment.Add(journal); }
            XElement bindings = equipment.Element("bindings");
            if (bindings == null) { bindings = new XElement("bindings"); equipment.Add(bindings); }
            XElement entry = journal.Elements("c").FirstOrDefault(x => (string)x.Attribute("id") == id);
            string state = (string)entry?.Attribute("state");
            if (state == "bag-bound")
                return BoundVerified(entry, bindings, id, frame) ? RebirthNpcB14CreditResult.Credited : RebirthNpcB14CreditResult.Ambiguous;

            if (entry == null)
            {
                int slot = -1;
                for (int i = 0; i < npc.bag.SlotCount; i++)
                {
                    ItemStack cell = npc.bag.ItemGrid[i];
                    if ((cell == null || cell.IsEmpty()) && RebirthHumanEquipment.Accept(10 + i, stack) &&
                        !bindings.Elements("binding").Any(b => (string)b.Attribute("area") == "bag" && (int)b.Attribute("index") == i) &&
                        !journal.Elements("c").Any(c => (string)c.Attribute("state") == "bag-prepared" && (int)c.Attribute("slot") == i))
                    { slot = i; break; }
                }
                if (slot < 0) return RebirthNpcB14CreditResult.Full;
                entry = new XElement("c", new XAttribute("id", id), new XAttribute("state", "bag-prepared"),
                    new XAttribute("slot", slot), new XAttribute("frame", frame));
                journal.Add(entry);
                while (journal.Elements("c").Count() > JournalLimit) journal.Elements("c").First().Remove();
                RebirthHumanEquipment.Save(npc, equipment); // write-ahead: intent is saved before any item moves
                state = "bag-prepared";
            }

            if (state != "bag-prepared") return RebirthNpcB14CreditResult.Retry;
            int target = (int)entry.Attribute("slot");
            if (target < 0 || target >= npc.bag.SlotCount || (string)entry.Attribute("frame") != frame) return RebirthNpcB14CreditResult.Retry;
            ItemStack current = npc.bag.ItemGrid[target];
            if (current != null && !current.IsEmpty() && RebirthHumanEquipment.Frame(current) != frame)
            { entry.Remove(); RebirthHumanEquipment.Save(npc, equipment); return RebirthNpcB14CreditResult.Retry; } // reserved cell was taken
            if (current != null && !current.IsEmpty())
            {
                // An identical stack is already in the reserved cell. It is adopted ONLY when persisted proof says it
                // is OUR placement (binding with this credit id on this cell AND the saved native-stack record for
                // the frame); otherwise it may be another actor's item: Ambiguous, never adopted.
                if (BoundVerified(entry, bindings, id, frame))
                {
                    TryPromote(entry, equipment);
                    return RebirthNpcB14CreditResult.Credited;
                }
                return RebirthNpcB14CreditResult.Ambiguous;
            }
            bool placed = true;
            npc.bag.SetSlot(target, stack);
            bool verified = false;
            try
            {
                if (!bindings.Elements("binding").Any(b => (string)b.Attribute("id") == id))
                    bindings.Add(new XElement("binding", new XAttribute("area", "bag"), new XAttribute("index", target), new XAttribute("id", id)));
                RebirthHumanEquipment.Save(npc, equipment); // state is still bag-prepared; throws when custody/readback fails
                verified = SavedCredit(npc.RebirthRuntimeState.StableId, id, target, frame);
            }
            catch (Exception) { }
            if (!verified)
            {
                // Not provably saved: take our own placement back (the journal still says bag-prepared, so a retry
                // re-places into the same reserved cell and nothing was ever marked bound).
                if (placed) npc.bag.SetSlot(target, ItemStack.Empty.Clone());
                try
                {
                    foreach (XElement stale in bindings.Elements("binding").Where(x => (string)x.Attribute("id") == id).ToArray()) stale.Remove();
                    RebirthHumanEquipment.Save(npc, equipment);
                }
                catch (Exception) { }
                return RebirthNpcB14CreditResult.Retry;
            }
            // Verified: only now is the marker promoted. If this save fails the stack is still verifiably bound and
            // the next call re-enters the prepared branch idempotently.
            try
            {
                entry.SetAttributeValue("state", "bag-bound");
                RebirthHumanEquipment.Save(npc, equipment);
            }
            catch (Exception) { }
            return RebirthNpcB14CreditResult.Credited;
        }
        catch (Exception) { return RebirthNpcB14CreditResult.Retry; }
    }

    private static bool SavedCredit(RebirthNpcStableId npcId, string id, int slot, string frame)
    {
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npcId, out var person) ||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(npcId, out var inventory) ||
                inventory.NativeStacks == null) return false;
            XElement equipment = (person.UnknownOptionalElements ?? Array.Empty<string>()).Select(XElement.Parse)
                .SingleOrDefault(x => x.Name == "humanEquipment");
            XElement binding = equipment?.Element("bindings")?.Elements("binding").SingleOrDefault(x =>
                (string)x.Attribute("area") == "bag" && (int)x.Attribute("index") == slot);
            if ((string)binding?.Attribute("id") != id) return false;
            ItemStack stack = RebirthHumanEquipment.Decode(frame);
            Guid credit = Guid.ParseExact(id, "N");
            var record = inventory.NativeStacks.Write().Elements()
                .Select(x => RebirthNpcNativeStackRecord.TryRead(x, out var item) ? item : null)
                .SingleOrDefault(x => x != null && x.StackId == credit);
            return record != null && record.Count == stack.count &&
                record.Payload == RebirthNativeItemCodec.Encode(stack.itemValue);
        }
        catch { return false; }
    }
}
