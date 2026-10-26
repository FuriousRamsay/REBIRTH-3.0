using System;
using System.Linq;
using System.Xml.Linq;

#nullable disable

// B14 saved tool wear, bound to the exact native strike. Same custody discipline as P9's
// RebirthNpcStructureRepairOriginal.TryWearNativeTool (whose helpers are private and repair-specific), on public stores:
//   TryPlan     pure: validates the held tool's toolbelt binding + saved native-stack record against the live stack and
//               computes the after-frame. The resulting row text is staged by RebirthNpcB14NativeStrike.TryStrike and
//               saved in the SAME write-ahead save as the pre-journaled Damaged deliverable, i.e. wear exists in the
//               journal only for a strike that is actually about to be issued (a protected/no-damage refusal never
//               reaches it).
//   TryComplete applied only once the strike is known to have landed (phase Damaged or later; recovery reverts a
//               strike that did not land to Armed, which discards the staged wear). Replay-safe on EVERY entry:
//               slot, binding id and saved tool record are revalidated, including for an already "applied" row.
// Row text: "operation|phase|slot|toolId|beforeFrame|afterFrame", phase = attempted | applied.
internal sealed class RebirthNpcB14WearPlan
{
    internal string RowText;
    internal float AfterUseTimes;
}

internal static class RebirthNpcB14ToolWear
{
    internal static bool TryPlan(EntityRebirthHumanoidNPC npc, ItemValue tool, Guid operation, out RebirthNpcB14WearPlan plan)
    {
        plan = null;
        try
        {
            if (npc == null || npc.inventory == null || npc.RebirthRuntimeState == null || tool == null || tool.IsEmpty()) return false;
            string op = operation.ToString("N");
            if (tool.MaxUseTimes <= 0)
            {
                plan = new RebirthNpcB14WearPlan { RowText = op + "|applied|-1|0||", AfterUseTimes = tool.UseTimes };
                return true;
            }
            int slot = npc.inventory.SelectedSlot;
            ItemStack held = npc.inventory.GetItem(slot);
            if (!ReferenceEquals(tool, npc.inventory.holdingItemItemValue) || held == null || held.IsEmpty() ||
                !ReferenceEquals(tool, held.itemValue)) return false;
            Guid toolId;
            if (!TryBinding(npc, slot, out toolId)) return false;
            string beforeFrame = RebirthHumanEquipment.Frame(held);
            ItemStack before = RebirthHumanEquipment.Decode(beforeFrame);
            if (!SavedRecordMatches(npc.RebirthRuntimeState.StableId, toolId, before)) return false;
            float wear = EffectManager.GetValue(PassiveEffects.DegradationPerUse, tool, 1f, npc, null,
                tool.ItemClass.ItemTags) * ItemAction.ItemDegradationModifier;
            if (float.IsNaN(wear) || float.IsInfinity(wear) || wear < 0f) return false;
            ItemStack after = before.Clone();
            after.itemValue.UseTimes += wear;
            if (float.IsNaN(after.itemValue.UseTimes) || float.IsInfinity(after.itemValue.UseTimes)) return false;
            plan = new RebirthNpcB14WearPlan
            {
                AfterUseTimes = after.itemValue.UseTimes,
                RowText = op + "|attempted|" + slot + "|" + toolId.ToString("N") + "|" + beforeFrame + "|" + RebirthHumanEquipment.Frame(after)
            };
            return true;
        }
        catch (Exception) { return false; }
    }

    // True when the wear of this operation is applied AND still matches the live tool, binding and saved record.
    internal static bool TryComplete(EntityRebirthHumanoidNPC npc, ItemValue tool, RebirthNpcB14Store.Row row, Func<bool> persistRow)
    {
        try
        {
            if (npc == null || npc.inventory == null || npc.RebirthRuntimeState == null || row == null ||
                row.Original == null || persistRow == null) return false;
            string[] f = string.IsNullOrEmpty(row.ToolWear) ? new string[0] : row.ToolWear.Split('|');
            if (f.Length != 6 || f[0] != row.Original.OperationId.ToString("N"))
                return tool != null && tool.MaxUseTimes <= 0; // no staged wear: only a tool that cannot wear may proceed
            int slot;
            if (!int.TryParse(f[2], out slot)) return false;
            if (slot < 0) return f[1] == "applied"; // nothing wears
            Guid toolId;
            if (!Guid.TryParseExact(f[3], "N", out toolId) || toolId == Guid.Empty) return false;
            // Revalidate the exact saved slot/toolId against the live binding on EVERY replay.
            Guid liveId;
            if (!TryBinding(npc, slot, out liveId) || liveId != toolId) return false;
            ItemStack held = npc.inventory.GetItem(slot);
            if (held == null || held.IsEmpty()) return false;
            string live = RebirthHumanEquipment.Frame(held);
            ItemStack after = RebirthHumanEquipment.Decode(f[5]);
            if (f[1] == "applied")
                return live == f[5] && SavedRecordMatches(npc.RebirthRuntimeState.StableId, toolId, after);
            if (f[1] != "attempted" || (live != f[4] && live != f[5])) return false;
            if (live == f[4])
            {
                // Mutation still owed: the strike's tool must be this exact held stack.
                if (tool == null || slot != npc.inventory.SelectedSlot || !ReferenceEquals(tool, held.itemValue) ||
                    !SavedRecordMatches(npc.RebirthRuntimeState.StableId, toolId, RebirthHumanEquipment.Decode(f[4]))) return false;
                tool.UseTimes = after.itemValue.UseTimes;
                if (RebirthHumanEquipment.Frame(npc.inventory.GetItem(slot)) != f[5]) return false;
            }
            RebirthHumanEquipment.Save(npc, RebirthHumanEquipment.Read(npc)); // re-observes custody; throws when unavailable
            if (!SavedRecordMatches(npc.RebirthRuntimeState.StableId, toolId, after)) return false;
            string previous = row.ToolWear;
            row.ToolWear = f[0] + "|applied|" + f[2] + "|" + f[3] + "|" + f[4] + "|" + f[5];
            if (persistRow()) return true;
            row.ToolWear = previous;
            return false;
        }
        catch (Exception) { return false; }
    }

    private static bool TryBinding(EntityRebirthHumanoidNPC npc, int slot, out Guid toolId)
    {
        toolId = Guid.Empty;
        XElement equipment = RebirthHumanEquipment.Read(npc);
        XElement bindings = equipment.Element("bindings");
        XElement binding = bindings == null ? null : bindings.Elements("binding")
            .SingleOrDefault(x => (string)x.Attribute("area") == "toolbelt" && (int)x.Attribute("index") == slot);
        return binding != null && Guid.TryParseExact((string)binding.Attribute("id"), "N", out toolId) && toolId != Guid.Empty;
    }

    private static bool SavedRecordMatches(RebirthNpcStableId npc, Guid toolId, ItemStack expected)
    {
        try
        {
            RebirthNpcInventoryPersistentRecord inventory;
            if (!RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(npc, out inventory) ||
                inventory.NativeStacks == null) return false;
            RebirthNpcNativeStackRecord record = inventory.NativeStacks.Write().Elements()
                .Select(x => RebirthNpcNativeStackRecord.TryRead(x, out var item) ? item : null)
                .SingleOrDefault(x => x != null && x.StackId == toolId);
            return record != null && record.Count == expected.count &&
                record.Payload == RebirthNativeItemCodec.Encode(expected.itemValue);
        }
        catch (Exception) { return false; }
    }
}
