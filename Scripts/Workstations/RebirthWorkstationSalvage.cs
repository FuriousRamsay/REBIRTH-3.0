using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

#nullable disable

internal static class RebirthWorkstationSalvage
{
    [ThreadStatic] private static int damageDepth;
    internal sealed class DamageState
    {
        internal bool Root;
        internal WorldBase World;
        internal Vector3i Position;
        internal int BlockType;
        internal int Actor;
        internal string Shell;
    }

    internal static void BeforeDamage(WorldBase __0, BlockValueRef __1, BlockValue __2,
        ref int __3, int __4, bool __6, out DamageState __state)
    {
        __state = null;
        if (!(__2.Block is BlockWorkstation)) return;
        __state = new DamageState { Root = damageDepth++ == 0 };
        if (!__state.Root || __0 == null || __0.IsRemote() || __3 <= 0 || !__6) return;
        Vector3i position;
        if (!__1.TryGetBlockPos(out position)) return;
        BlockValue value = __2;
        if (value.ischild && value.Block?.multiBlockPos != null)
        {
            position = value.Block.multiBlockPos.GetParentPos(position, value);
            value = __0.GetBlock(position);
        }
        var station = __0.GetTileEntity(position) as TileEntityWorkstation;
        PlatformUserIdentifierAbs ignored;
        if (station == null || station.IsPlayerPlaced ||
            RebirthWorkstationSecurityService.TryGetOwner(position, out ignored)) return;
        var player = __0.GetEntity(__4) as EntityPlayer;
        var held = player?.inventory?.holdingItem;
        if (held == null || !IsSalvageTool(held.GetItemName())) return;
        string shell = value.Block.Properties.GetString("RebirthSalvageShell");
        if (string.IsNullOrEmpty(shell) || ItemClass.GetItem(shell).type == 0) return;
        __state.World = __0; __state.Position = position; __state.BlockType = value.type;
        __state.Actor = __4; __state.Shell = shell;
    }

    internal static Exception AfterDamage(Exception __exception, DamageState __state)
    {
        if (__state == null) return __exception;
        damageDepth = Math.Max(0, damageDepth - 1);
        if (__exception != null || !__state.Root || __state.World == null) return __exception;
        // Award only after the actual authoritative block mutation. Overrides which veto destruction
        // or downgrade to another live station cannot manufacture a repairable shell.
        var remaining = __state.World.GetBlock(__state.Position);
        if (!remaining.isair) return __exception;
        GameManager.Instance.ItemDropServer(new ItemStack(ItemClass.GetItem(__state.Shell), 1),
            new Vector3(__state.Position.x + .5f, __state.Position.y + .5f, __state.Position.z + .5f),
            Vector3.zero, __state.Actor, 120f, false);
        return __exception;
    }

    internal static bool IsSalvageTool(string name)
    {
        return name == "meleeToolSalvageT1Wrench" || name == "meleeToolSalvageT2Ratchet" ||
            name == "meleeToolSalvageT3ImpactDriver";
    }
}
