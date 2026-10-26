using System;
using System.Linq;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// GET /npcgear?entity=ID  (or no entity: every loaded human NPC). READ-ONLY: no Put, no Save, no input.
/// Per NPC: ids, birth-equipment marker CVars, native toolbelt 0-9, equipment slots, bag use/capacity, lockpicks and,
/// for slots 4/5, the live native frame next to the saved humanEquipment binding image.
/// </summary>
internal static class RebirthGameBridgeNpcGear
{
    internal static void Handle(BridgeRequest req)
    {
        var world = GameManager.Instance?.World;
        if (req.Method != "GET") { req.Fail("npcgear is GET only.", 405); return; }
        if (world == null || !ThreadManager.IsMainThread()) { req.Fail("In-game world on the main thread required.", 409); return; }
        int id = req.QueryInt("entity", -1);
        var rows = new JArray();
        if (id >= 0)
        {
            var npc = world.GetEntity(id) as EntityRebirthHumanoidNPC;
            if (npc == null) { req.Fail("Entity " + id + " is not a loaded human NPC.", 404); return; }
            rows.Add(Describe(npc));
        }
        else
            foreach (Entity e in world.Entities.list.ToArray())
                if (e is EntityRebirthHumanoidNPC human && !human.IsDead()) rows.Add(Describe(human));
        req.Complete(new JObject { ["schema"] = 1, ["observationOnly"] = true, ["count"] = rows.Count, ["npcs"] = rows });
    }

    private static JObject Stack(ItemStack s)
    {
        if (s == null || s.IsEmpty()) return null;
        var v = s.itemValue;
        return new JObject { ["name"] = v.ItemClass?.GetItemName(), ["count"] = s.count, ["quality"] = (int)v.Quality,
            ["useTimes"] = v.UseTimes, ["meta"] = v.Meta, ["frame"] = RebirthHumanEquipment.Frame(s) };
    }

    private static JObject Describe(EntityRebirthHumanoidNPC npc)
    {
        var o = new JObject { ["entityId"] = npc.entityId, ["class"] = npc.EntityClass?.entityClassName ?? npc.GetType().Name };
        try { o["stableId"] = npc.RebirthRuntimeState?.StableId.ToString(); } catch (Exception ex) { o["stableIdError"] = ex.Message; }
        var buffs = npc.Buffs;
        o["rbNpcBirthEquipment"] = buffs == null ? JValue.CreateNull() : new JValue(buffs.GetCustomVar("rbNpcBirthEquipment"));
        o["rbNpcBirthTries"] = buffs == null ? JValue.CreateNull() : new JValue(buffs.GetCustomVar("rbNpcBirthTries"));
        o["rbNpcBirthPending"] = buffs == null ? JValue.CreateNull() : new JValue(buffs.GetCustomVar("rbNpcBirthPending"));
        o["nativeEquipmentMode"] = buffs == null ? JValue.CreateNull() : new JValue(buffs.GetCustomVar(RebirthHumanEquipment.NativeMode));
        var toolbelt = new JArray();
        int belt = npc.inventory == null ? 0 : npc.inventory.SlotCount;
        for (int i = 0; i < 10 && i < belt; i++)
        { var j = Stack(npc.inventory.GetItem(i)); toolbelt.Add(new JObject { ["slot"] = i, ["item"] = j }); }
        o["toolbeltSlotCount"] = belt; o["heldSlot"] = npc.inventory?.SelectedSlot ?? -1; o["toolbelt"] = toolbelt;
        var equipment = new JArray();
        foreach (int slot in new[] { 0, 1, 2, 3, 9 })
            equipment.Add(new JObject { ["slot"] = slot, ["name"] = RebirthHumanEquipment.Names[slot], ["item"] = Stack(RebirthHumanEquipment.Get(npc, slot)) });
        o["equipment"] = equipment;
        int used = 0; var items = npc.bag?.ItemGrid?.items;
        if (items != null) foreach (var s in items) if (s != null && !s.IsEmpty()) used++;
        o["bagUsedSlots"] = used; o["bagCapacity"] = npc.bag?.SlotCount ?? 0;
        o["lockpickCount"] = RebirthHumanEquipment.EquippedLockpickCount(npc);
        var custody = new JArray();
        System.Xml.Linq.XElement saved = null; string savedError = null;
        try { saved = RebirthHumanEquipment.Read(npc); } catch (Exception ex) { savedError = ex.Message; }
        foreach (int slot in new[] { 4, 5 })
        {
            // Saved image: the humanEquipment binding for toolbelt index slot-4 (read only; never created here).
            var binding = saved?.Element("bindings")?.Elements("binding")
                .FirstOrDefault(b => (string)b.Attribute("area") == "toolbelt" && (string)b.Attribute("index") == (slot - 4).ToString());
            custody.Add(new JObject { ["slot"] = slot, ["liveFrame"] = RebirthHumanEquipment.Frame(RebirthHumanEquipment.Get(npc, slot)),
                ["savedBinding"] = binding?.ToString(System.Xml.Linq.SaveOptions.DisableFormatting) });
        }
        o["custody"] = custody; if (savedError != null) o["savedReadError"] = savedError;
        o["savedHumanEquipment"] = saved?.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        return o;
    }
}
