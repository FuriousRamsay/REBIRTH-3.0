using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

#nullable disable

// G04: who is credited for each loot/search effect of an NPC-owned container open (docs: _Documentation/NPC/G04_ARMOR_MOD_NPC_EFFECTS_20261023.md).
// Read-only. Never rolls loot, never writes gear, containers, receipts or CVars. The credit rule it reports and logs:
//  - NPC credit: ScavengingTime (gloves ModLootTimeDecrease), LootQuantity (same gloves), LootStage (boots ModLootStageIncrease),
//    GameStage (chest ModGamestageDecrease), LootProb: evaluated once with the NPC as the EffectManager entity.
//  - Owner credit: only the base stage (owner.GetHighestPartyLootStage / unModifiedGameStage) and the Management search-time tier.
//    The owner's own worn LootStage/ScavengingTime mods are not meant to count. Native GetHighestPartyLootStage may fold the
//    owner's LootStage passive into the base; OwnerLootStageLeak measures exactly that so the batch can prove or refute it.
// Mods are attributed by family name (same convention as RebirthG04NpcWornEffects); the numbers come from the engine evaluation.
internal static class RebirthG04LootSearchAttribution
{
    private static readonly string[] Slots = { "Head", "Chest", "Hands", "Feet" };
    private const string ScavengingFamily = "ModLootTimeDecreaseTier", LootStageFamily = "ModLootStageIncreaseTier",
        GameStageFamily = "ModGamestageDecreaseTier";
    private static int logged;

    private static JArray Worn(EntityRebirthHumanoidNPC npc, string family)
    {
        var list = new JArray();
        try
        {
            for(int slot = 0; slot < Slots.Length; slot++)
            {
                var mods = npc?.equipment?.ItemGrid[slot]?.itemValue?.modifications;
                if(mods == null) continue;
                for(int m = 0; m < mods.Length; m++)
                {
                    string name = mods[m] != null && !mods[m].IsEmpty() ? mods[m].ItemClass?.Name : null;
                    if(name != null && name.StartsWith(family, StringComparison.OrdinalIgnoreCase)) list.Add(Slots[slot] + ":" + name);
                }
            }
        }
        catch(Exception) { }
        return list;
    }

    private static JObject Channel(EntityRebirthHumanoidNPC npc, PassiveEffects type, float baseValue, string family)
    {
        float effective = RebirthP11NpcLootEffects.Eval(type, npc, baseValue);
        return new JObject { ["base"] = baseValue, ["npcEffective"] = effective, ["npcDelta"] = effective - baseValue,
            ["credit"] = "npc", ["mods"] = Worn(npc, family) };
    }

    // list/ownerStage may be null/0 for the idle diagnostic; the real open passes both.
    internal static JObject Describe(EntityRebirthHumanoidNPC npc, EntityPlayer owner, LootContainer list, float ownerStage)
    {
        var o = new JObject();
        try
        {
            if(ownerStage <= 0f) ownerStage = 100f;
            float baseSeconds = list != null ? Math.Max(0f, list.openTime * LootContainer.LootTimerModifier) : 100f;
            var search = Channel(npc, PassiveEffects.ScavengingTime, baseSeconds, ScavengingFamily);
            if(npc?.RebirthRuntimeState != null)
                search["afterManagementOwnerCredit"] = RebirthCompanionLeadership.ManagementAdjustedLootSearchSeconds(
                    (float)search["npcEffective"], npc.RebirthRuntimeState.StableId);
            o["search"] = search;
            o["lootQuantity@100"] = Channel(npc, PassiveEffects.LootQuantity, 100f, ScavengingFamily);
            o["gameStage"] = Channel(npc, PassiveEffects.GameStage, ownerStage, GameStageFamily);
            float afterGame = (float)o["gameStage"]["npcEffective"];
            o["lootStage"] = Channel(npc, PassiveEffects.LootStage, afterGame, LootStageFamily);
            var ownerNode = new JObject { ["credit"] = "base stage and Management search-time only", ["baseStage"] = ownerStage };
            if(owner != null)
            {
                float own = RebirthP11NpcLootEffects.Eval(PassiveEffects.LootStage, owner, ownerStage);
                ownerNode["ownerLootStageLeak"] = own - ownerStage;
                ownerNode["ownerScavengingTimeEffect"] = RebirthP11NpcLootEffects.Eval(PassiveEffects.ScavengingTime, owner, baseSeconds) - baseSeconds;
                if(RebirthStablePlayerIdentity.TryResolveServerEntity(owner, out var identity)) ownerNode["owner"] = identity.StorageKey;
            }
            o["owner"] = ownerNode;
        }
        catch(Exception ex) { o["error"] = ex.Message; }
        return o;
    }

    // One bounded log line per generated NPC open (first 200 per session): the batch's evidence for credit and any owner leak.
    internal static void LogOpen(EntityPlayer owner, EntityRebirthHumanoidNPC npc, LootContainer list, float ownerStage, int effectiveStage)
    {
        if(logged >= 200) return;
        logged++;
        try
        {
            var d = Describe(npc, owner, list, ownerStage);
            Log.Out("[REBIRTH G04 loot attribution] npc=" + npc?.RebirthRuntimeState?.StableId + " effectiveStage=" + effectiveStage +
                " ownerStage=" + ownerStage + " " + d.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch(Exception) { }
    }
}
