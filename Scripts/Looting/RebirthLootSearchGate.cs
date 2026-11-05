using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native marks a container touched when the search starts, so cancelling the timer made
/// the next open skip it. A search now only counts once its timer has finished.
/// Supply crates also keep 2.6's $WasOpened value, which survives reloads and other players.
/// </summary>
internal static class RebirthLootSearchGate
{
    internal const string CrateOpenedCVar = "$WasOpened";
    private static readonly HashSet<Vector3i> blocks = new HashSet<Vector3i>();
    private static readonly HashSet<int> entities = new HashSet<int>();
    private static World boundWorld;

    private static void Bind()
    {
        var world = GameManager.Instance?.World;
        if (ReferenceEquals(world, boundWorld)) return;
        boundWorld = world;
        blocks.Clear();
        entities.Clear();
    }

    internal static bool CrateSearchFinished(EntitySupplyCrate crate)
    {
        return crate != null && crate.Buffs != null && crate.Buffs.GetCustomVar(CrateOpenedCVar) > 0f;
    }

    internal static bool CrateLanded(EntitySupplyCrate crate)
    {
        return crate != null && (crate.onGround || crate.IsInWater());
    }

    internal static void BeforeOpenLooting(TEFeatureStorage _te, ref bool _firstTimeTouched)
    {
        if (_te == null || _te.ItemGrid == null || _te.ItemGrid.PlayerOwned) return;
        Bind();
        Vector3i key = _te.ToWorldPos();
        if (_firstTimeTouched) blocks.Add(key);
        else if (blocks.Contains(key)) _firstTimeTouched = true;
    }

    internal static void AfterOpenContainer(TEFeatureStorage _te)
    {
        if (_te == null) return;
        Bind();
        blocks.Remove(_te.ToWorldPos());
    }

    internal static void BeforeBagOpen(Entity _entity, LootContainer _lootContainer, ref bool _runOpenTimer)
    {
        if (_entity == null || _lootContainer == null) return;
        Bind();
        var crate = _entity as EntitySupplyCrate;
        if (crate != null)
        {
            if (!CrateSearchFinished(crate)) _runOpenTimer = true;
            return;
        }
        if (_runOpenTimer) entities.Add(_entity.entityId);
        else if (entities.Contains(_entity.entityId)) _runOpenTimer = true;
    }

    internal static void AfterOpenStorageWindow(XUiC_BagStorageWindowGroup __instance)
    {
        var entity = __instance?.Entity;
        if (entity == null) return;
        Bind();
        entities.Remove(entity.entityId);
        var crate = entity as EntitySupplyCrate;
        if (crate != null && crate.Buffs != null) crate.Buffs.SetCustomVar(CrateOpenedCVar, 1f);
    }

    // 2.6: a crate cannot be searched until it has landed. Water counts as landed because
    // native closes the parachute there and a floating crate would otherwise never open.
    internal static void AfterAllowActivation(EntitySupplyCrate __instance, ref bool __result)
    {
        if (__result && !CrateLanded(__instance)) __result = false;
    }

    internal static void AfterActivationText(EntitySupplyCrate __instance, ref string __result)
    {
        if (!CrateLanded(__instance)) __result = string.Empty;
    }

    internal static bool BeforeCrateLock(EntityAlive __instance, ref bool __result)
    {
        var crate = __instance as EntitySupplyCrate;
        if (crate == null || CrateLanded(crate)) return true;
        __result = false;
        return false;
    }
}

[Preserve]
public sealed class RebirthLootSearchGateModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        var gate = typeof(RebirthLootSearchGate);
        var harmony = new HarmonyLib.Harmony("rebirth.looting.searchgate");
        Patch(harmony, typeof(XUiC_LootWindowGroup), nameof(XUiC_LootWindowGroup.OpenLooting),
            new[] { typeof(string), typeof(TEFeatureStorage), typeof(bool) }, gate, nameof(RebirthLootSearchGate.BeforeOpenLooting), false);
        Patch(harmony, typeof(XUiC_LootWindowGroup), "openContainer",
            new[] { typeof(string), typeof(TEFeatureStorage), typeof(bool), typeof(bool) }, gate, nameof(RebirthLootSearchGate.AfterOpenContainer), true);
        var bagOpen = new HarmonyMethod(AccessTools.Method(gate, nameof(RebirthLootSearchGate.BeforeBagOpen))) { priority = Priority.First };
        harmony.Patch(Require(typeof(XUiC_BagStorageWindowGroup), nameof(XUiC_BagStorageWindowGroup.Open),
            new[] { typeof(XUi), typeof(Entity), typeof(Bag), typeof(LootContainer), typeof(string), typeof(Action), typeof(Action), typeof(Func<bool>), typeof(bool) }), prefix: bagOpen);
        Patch(harmony, typeof(XUiC_BagStorageWindowGroup), nameof(XUiC_BagStorageWindowGroup.OpenStorageWindow),
            Type.EmptyTypes, gate, nameof(RebirthLootSearchGate.AfterOpenStorageWindow), true);
        Patch(harmony, typeof(EntitySupplyCrate), nameof(EntitySupplyCrate.AllowActivationCommand),
            new[] { typeof(ReadOnlySpan<char>), typeof(EntityPlayerLocal) }, gate, nameof(RebirthLootSearchGate.AfterAllowActivation), true);
        Patch(harmony, typeof(EntitySupplyCrate), nameof(EntitySupplyCrate.GetActivationText),
            Type.EmptyTypes, gate, nameof(RebirthLootSearchGate.AfterActivationText), true);
        var crateLock = new HarmonyMethod(AccessTools.Method(gate, nameof(RebirthLootSearchGate.BeforeCrateLock))) { priority = Priority.First };
        harmony.Patch(Require(typeof(EntityAlive), nameof(EntityAlive.OnLockRequestServer),
            new[] { typeof(int), typeof(PooledBinaryReader), typeof(ushort) }), prefix: crateLock);
    }

    private static void Patch(HarmonyLib.Harmony harmony, Type target, string name, Type[] arguments, Type handler, string callback, bool postfix)
    {
        var patch = new HarmonyMethod(AccessTools.Method(handler, callback));
        harmony.Patch(Require(target, name, arguments), prefix: postfix ? null : patch, postfix: postfix ? patch : null);
    }

    private static System.Reflection.MethodInfo Require(Type target, string name, Type[] arguments)
    {
        return AccessTools.Method(target, name, arguments) ?? throw new MissingMethodException(target.Name, name);
    }
}
