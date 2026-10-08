using System;
using HarmonyLib;
using UnityEngine;

// Use native RewardItem grants, inventory overflow, quest persistence and reward UI.
// Tier-completion quests receive one automatic vehicle; normal jobs do not.
public static class RebirthTierVehicleRewards
{
    public static string VehicleForBiome(string biome)
    {
        switch (biome)
        {
            case "pine_forest": return "vehicleBicyclePlaceable";
            case "desert": return "vehicleMinibikePlaceable";
            case "snow": return "vehicleMotorcyclePlaceable";
            case "wasteland": return "vehicleTruck4x4Placeable";
            case "burnt_forest": return "vehicleGyrocopterPlaceable";
            default: return null;
        }
    }

    public static void Resolve(RewardItem reward)
    {
        Quest quest = reward.OwnerQuest;
        if (quest == null || quest.ID == null || !quest.ID.StartsWith("quest_tier", StringComparison.Ordinal)
            || !quest.ID.EndsWith("complete", StringComparison.Ordinal) || reward.isChosenReward
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        if (reward.ID != "vehicleBicyclePlaceable" && reward.ID != "vehicleMinibikePlaceable"
            && reward.ID != "vehicleMotorcyclePlaceable" && reward.ID != "vehicleTruck4x4Placeable"
            && reward.ID != "vehicleGyrocopterPlaceable") return;
        EntityPlayerLocal player = quest.OwnerJournal != null ? quest.OwnerJournal.OwnerPlayer : null;
        if (player == null || player.world == null) return;
        Vector3 at = quest.GetQuestGiverLocation();
        if (at == Vector3.zero) at = player.position;
        BiomeDefinition biome = player.world.GetBiomeInWorld((int)at.x, (int)at.z);
        string item = biome == null ? null : VehicleForBiome(biome.m_sBiomeName);
        if (item != null) reward.ID = item;
    }
}

[HarmonyPatch(typeof(RewardItem), nameof(RewardItem.SetupReward))]
public static class RebirthTierVehicleDisplayPatch
{
    public static void Prefix(RewardItem __instance) { RebirthTierVehicleRewards.Resolve(__instance); }
}

[HarmonyPatch(typeof(RewardItem), "SetupItem")]
public static class RebirthTierVehicleItemPatch
{
    public static void Prefix(RewardItem __instance) { RebirthTierVehicleRewards.Resolve(__instance); }
}
