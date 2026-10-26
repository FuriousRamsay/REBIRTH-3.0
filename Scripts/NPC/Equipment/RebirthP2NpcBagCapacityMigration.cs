using System;
using System.Linq;
using System.Xml.Linq;

// P2/G03+G04: saved post-admission bag-capacity reconciliation for already-equipped
// chest/backpack capacity items. The intent lives in the humanEquipment aggregate element
// (<capacityMigration>), is checkpointed with exact before/after bag images BEFORE the
// native bag changes, and is verified against the live image before and after the resize.
// A crash anywhere replays safely: live==before re-applies, live==after finishes.
internal static class RebirthP2NpcBagCapacityMigration
{
    private const string Element = "capacityMigration";

    private static string Image(Bag bag, int count)
    {
        var items = bag.ItemGrid.items;
        return string.Join(";", Enumerable.Range(0, count).Select(i => RebirthHumanEquipment.Frame(i < items.Length ? items[i] : null)));
    }

    // Grow appends empty cells; shrink drops an already-empty tail. Existing cells never change.
    private static string Adjust(string before, int from, int to)
    {
        var cells = before.Split(';');
        if (cells.Length != from) throw new InvalidOperationException("bag image shape");
        return string.Join(";", Enumerable.Range(0, to).Select(i => i < from ? cells[i] : ""));
    }

    // True when no migration is pending (or it completed); false when it must be retried later.
    internal static bool TryReconcile(EntityRebirthHumanoidNPC npc)
    {
        if (!ThreadManager.IsMainThread() || npc?.world?.IsRemote() != false || !ReferenceEquals(npc.world, GameManager.Instance?.World) ||
            npc.IsDead() || npc.IsPreparedRestorationPending || npc.bag == null || npc.inventory == null || npc.equipment == null ||
            !RebirthHumanEquipment.Human(npc) || npc.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode) != 1f ||
            npc.Buffs.GetCustomVar(RebirthHumanEquipment.Hold) == 1f || RebirthNpcPersistenceCoordinator.IsRecoveryInstalling ||
            RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc) || !ReferenceEquals(npc.world.GetEntity(npc.entityId), npc))
            return false;
        var id = npc.RebirthRuntimeState.StableId;
        try
        {
            if (!RebirthNpcAggregatePersistenceStore.TryGet(id, out _)) return false;
            var x = RebirthHumanEquipment.Read(npc);
            // Let a pending gear exchange / book move settle first; they own the bag and bindings.
            if (x.Element("bookMove") != null || x.Element("swap") is XElement swap && (string)swap.Attribute("done") != "1") return false;
            var m = x.Element(Element);
            if (m == null)
            {
                int baseSlots = (int?)x.Attribute("base") ?? 0;
                if (baseSlots < 1) return false;
                int current = npc.bag.SlotCount;
                int target = RebirthHumanEquipment.Capacity(npc, RebirthHumanEquipment.Get(npc, 9), RebirthHumanEquipment.Get(npc, 1), baseSlots);
                if (target == current) return true;
                if (target < current && !RebirthHumanEquipment.TailEmpty(npc, target)) return false;
                if (!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(npc, x)) return false;
                string before = Image(npc.bag, current);
                m = new XElement(Element, new XAttribute("id", Guid.NewGuid().ToString("N")), new XAttribute("from", current), new XAttribute("to", target),
                    new XAttribute("custodyRevision", RebirthNpcInventoryTransactionService.GetSnapshot(id).Revision),
                    new XElement("before", before), new XElement("after", Adjust(before, current, target)));
                x.Add(m);
                RebirthHumanEquipment.Save(npc, x); // intent + exact images durable before any native change
            }
            return Apply(npc, x, m);
        }
        catch (Exception e)
        {
            Log.Warning("[REBIRTH NPC Capacity] migration retained: " + e.GetType().Name + ": " + e.Message);
            return false;
        }
    }

    private static bool Apply(EntityRebirthHumanoidNPC npc, XElement x, XElement m)
    {
        int from = (int)m.Attribute("from"), to = (int)m.Attribute("to");
        string before = (string)m.Element("before"), after = (string)m.Element("after");
        if (from < 1 || to < 1 || to > 169 || from > 169 || before == null || after == null) return false;
        string live = Image(npc.bag, npc.bag.SlotCount);
        if (!(npc.bag.SlotCount == to && live == after))
        {
            if (npc.bag.SlotCount != from || live != before) return false; // unknown owner changed the bag: retain intent for audit
            if (to < from && !RebirthHumanEquipment.TailEmpty(npc, to)) return false;
            npc.bag.Resize(new Vector2i(to, 1));
            if (npc.bag.SlotCount != to || Image(npc.bag, to) != after) throw new InvalidOperationException("bag resize did not verify");
        }
        m.Remove();
        RebirthHumanEquipment.Save(npc, x); // custody revision advances through ObserveHumanEquipment
        return true;
    }
}
