using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

// The native wire controller detects a successful shock and applies its buffs.
// Only its wear target is changed: each end of the shocking wire receives half.
[HarmonyPatch(typeof(ElectricWireController), nameof(ElectricWireController.DamageSelf))]
internal static class RebirthElectricFenceSharedWear
{
    private sealed class Wear { internal float Pending; }
    private static readonly ConditionalWeakTable<TileEntityPoweredMeleeTrap, Wear> pending
        = new ConditionalWeakTable<TileEntityPoweredMeleeTrap, Wear>();

    private static bool Prefix(ElectricWireController __instance, float damage)
    {
        var world = GameManager.Instance?.World;
        var parent = __instance?.TileEntityParent;
        var child = __instance?.TileEntityChild;
        if (world == null || parent == null || child == null ||
            float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
            return true;

        var parentPos = parent.ToWorldPos();
        var childPos = child.ToWorldPos();
        if (!IsCurrentPost(world, parent, parentPos) || !IsCurrentPost(world, child, childPos))
            return true;

        if (ReferenceEquals(parent, child))
            DamagePost(world, parent, parentPos, damage);
        else
        {
            float half = damage * 0.5f;
            DamagePost(world, parent, parentPos, half);
            DamagePost(world, child, childPos, half);
        }
        return false;
    }

    private static bool IsCurrentPost(World world, TileEntityPoweredMeleeTrap tile, Vector3i pos)
        => ReferenceEquals(world.GetTileEntity(pos), tile) && world.GetBlock(pos).Block is BlockElectricWire;

    private static void DamagePost(World world, TileEntityPoweredMeleeTrap tile, Vector3i pos, float amount)
    {
        var wear = pending.GetValue(tile, _ => new Wear());
        wear.Pending += amount;
        int whole = (int)wear.Pending;
        if (whole < 1) return;
        wear.Pending -= whole;

        var block = world.GetBlock(pos);
        int maximum = block.Block.MaxDamage;
        if (maximum <= 0 || block.damage >= maximum) return;
        block.damage = Math.Min(maximum, block.damage + whole);
        world.SetBlockRPC((BlockValueRef)pos, block);
    }
}
