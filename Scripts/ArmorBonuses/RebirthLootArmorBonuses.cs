using System;
using HarmonyLib;
using UnityEngine.Scripting;
using Random = System.Random;

#nullable disable

/// <summary>
/// 2.6 looted armor: armor found in a container that is opened for the first time can already
/// carry bonuses. Harder biomes give more of them, at a higher level.
/// </summary>
public static class RebirthLootArmorBonuses
{
    private static readonly RebirthArmorRolledBonus[] NoBonuses = new RebirthArmorRolledBonus[0];
    private static readonly Random random = new Random();

    public static int RollCount(string biome, double roll, double threePlusRoll)
    {
        double none, one, two;
        switch ((biome ?? string.Empty).ToLowerInvariant())
        {
            case "burnt_forest": none = 0.30; one = 0.35; two = 0.80; break;
            case "wasteland": none = 0.30; one = 0.50; two = 0.95; break;
            case "snow": none = 0.50; one = 0.65; two = 1.00; break;
            case "desert": none = 0.50; one = 0.85; two = 1.00; break;
            default: none = 0.50; one = 1.00; two = 1.00; break;
        }
        if (roll < none) return 0;
        if (roll < one) return 1;
        if (roll < two) return 2;
        return threePlusRoll < 0.75 ? 3 : 4;
    }

    public static int BonusLevel(string biome)
    {
        switch ((biome ?? string.Empty).ToLowerInvariant())
        {
            case "burnt_forest": return 3;
            case "snow":
            case "wasteland": return 2;
            default: return 1;
        }
    }

    /// <summary>Returns true when the item received bonuses. Slots with no bonus pool yet stay plain.</summary>
    public static bool TryApply(ItemValue item, string biome, Random rng)
    {
        EquipmentSlots slot;
        if (item == null || item.IsEmpty() || !RebirthArmorBonusKits.TryGetSlot(item, out slot)) return false;
        if (RebirthArmorBonusStorage.Read(item).Length > 0) return false;
        int count = RollCount(biome, rng.NextDouble(), rng.NextDouble());
        if (count <= 0) return false;
        var rolled = RebirthArmorBonusRoller.Roll(slot, NoBonuses, count, BonusLevel(biome), item.Quality, rng);
        if (rolled.Count == 0) return false;
        RebirthArmorBonusStorage.Write(item, rolled);
        return true;
    }

    internal static void ApplyToContainer(TEFeatureStorage storage, World world)
    {
        var grid = storage.ItemGrid;
        var position = storage.ToWorldPos();
        string biome = world.GetBiome(position.x, position.z)?.m_sBiomeName;
        for (int i = 0; i < grid.Length; i++)
        {
            var stack = grid[i];
            if (stack == null || stack.IsEmpty() || !(stack.itemValue.ItemClass is ItemClassArmor)) continue;
            var copy = stack.Clone();
            if (TryApply(copy.itemValue, biome, random)) grid[i] = copy;
        }
    }
}

[HarmonyPatch(typeof(LootManager), nameof(LootManager.LootContainerOpened))]
internal static class RebirthLootArmorBonusPatch
{
    private static void Prefix(TEFeatureStorage _tileEntity, out bool __state)
    {
        __state = _tileEntity != null && _tileEntity.ItemGrid.Touched;
    }

    private static void Postfix(LootManager __instance, TEFeatureStorage _tileEntity, bool __state)
    {
        if (__state || _tileEntity == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        var world = GameManager.Instance?.World;
        if (world != null) RebirthLootArmorBonuses.ApplyToContainer(_tileEntity, world);
    }
}

[Preserve]
public sealed class RebirthLootArmorBonusModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.armorbonuses.loot"), typeof(RebirthLootArmorBonusPatch));
    }
}
