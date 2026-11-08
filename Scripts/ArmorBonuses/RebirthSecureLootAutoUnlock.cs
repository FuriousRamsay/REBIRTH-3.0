using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 glove bonus: picking a locked container can succeed at once. It uses one lockpick and opens
/// the container. Copies do not stack; the best chance on the gloves applies.
/// </summary>
internal static class RebirthSecureLootAutoUnlock
{
    internal static float Chance(EntityPlayer player)
    {
        if (player?.equipment == null) return 0f;
        float best = 0f;
        foreach (var bonus in RebirthArmorBonusStorage.Read(player.equipment.GetSlotItem((int)EquipmentSlots.Hands)))
            if (bonus.Definition.Special == "secure_unlock" && bonus.Value > best) best = bonus.Value;
        return best;
    }

    internal static bool TryUnlock(TEFeatureLockPickable feature, Vector3i position, EntityPlayerLocal player)
    {
        float chance = Chance(player);
        if (chance <= 0f || UnityEngine.Random.value > chance) return false;
        var inventory = player.PlayerUI?.xui?.PlayerInventory;
        var pick = ItemClass.GetItem(feature.lockPickItem);
        if (inventory == null || pick == null || pick.IsEmpty() || inventory.GetItemCount(pick) <= 0) return false;

        var used = new ItemStack(pick, 1);
        inventory.RemoveItem(used);
        player.PlayerUI.xui.CollectedItemList.RemoveItemStack(used);
        feature.unlockCompletion = 1f;
        feature.lockPicksUsed++;
        feature.SetModified();
        feature.DowngradeToUnlockedVariant(position);
        Audio.Manager.BroadcastPlayByLocalPlayer(position.ToVector3() + Vector3.one * 0.5f, "Misc/unlocking");
        GameManager.ShowTooltip(player, Localization.Get("containerUnlocked"), string.Empty, null);
        ThreadManager.StartCoroutine(OpenAfterChange(position, player));
        return true;
    }

    // The unlocked variant replaces the block, so its storage exists only after the change lands.
    private static IEnumerator OpenAfterChange(Vector3i position, EntityPlayerLocal player)
    {
        yield return new WaitForSeconds(0.25f);
        var storage = (player.world?.GetTileEntity(position) as TileEntityComposite)?.GetFeature<TEFeatureStorage>();
        if (storage != null) LockManager.Instance.LockRequestLocal(storage, 0);
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.OnBlockActivated), new[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
internal static class RebirthSecureLootAutoUnlockPatch
{
    private static bool Prefix(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player, ref bool __result)
    {
        if (_commandName == null || !_commandName.EndsWith(":pick", StringComparison.OrdinalIgnoreCase)) return true;
        if (_blockValue.ischild) _blockPos = _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue);
        var feature = (_world.GetTileEntity(_blockPos) as TileEntityComposite)?.GetFeature<TEFeatureLockPickable>();
        if (feature == null || !RebirthSecureLootAutoUnlock.TryUnlock(feature, _blockPos, _player)) return true;
        __result = true;
        return false;
    }
}

[Preserve]
public sealed class RebirthSecureLootAutoUnlockModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.armorbonuses.secureunlock"), typeof(RebirthSecureLootAutoUnlockPatch));
    }
}
