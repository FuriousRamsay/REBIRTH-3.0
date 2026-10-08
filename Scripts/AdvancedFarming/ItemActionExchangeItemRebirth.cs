using UnityEngine;
using UnityEngine.Scripting;
using Audio;

#nullable disable

/// <summary>
/// Focused filled-jar exchange action for Advanced Farming water storage.
/// Non-storage use intentionally does nothing so jars do not empty into air.
/// Empty jars/buckets and filled buckets use CollectWaterRebirth/DumpWaterRebirth to preserve vanilla natural-water behavior.
/// </summary>
[Preserve]
public class ItemActionExchangeItemRebirth : ItemActionExchangeItem
{
    public override void ExecuteAction(ItemActionData actionData, bool released)
    {
        // Filled-jar Action1 is added only for Advanced Farming. When the feature is
        // disabled, do nothing so 3.0 retains its original no-dump Action1 behavior.
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (!released || actionData == null || actionData.invData == null || actionData.lastUseTime > 0f)
            return;

        ItemInventoryData invData = actionData.invData;
        if (!AdvancedFarmingItemWaterExchange.TryGetAdvancedWaterStorage(invData, out Vector3i blockPos, out BlockValue blockValue, out TileEntityWaterTankRebirth tank))
            return;

        string itemName = invData.item != null ? invData.item.GetItemName() : string.Empty;
        if (!AdvancedFarmingItemWaterExchange.IsServerAuthority())
        {
            if (itemName != "drinkJarRiverWater" && itemName != "drinkJarBoiledWater" && itemName != "drinkJarPureMineralWater")
            {
                Manager.PlayInsidePlayerHead("ui_denied");
                return;
            }

            AdvancedFarmingItemWaterExchange.RequestServerWaterExchange(invData, blockPos, itemName);
            invData.holdingEntity.RightArmAnimationUse = true;
            if (!string.IsNullOrEmpty(soundStart))
                invData.holdingEntity.PlayOneShot(soundStart, false);
            return;
        }

        int delta = AdvancedFarmingItemWaterExchange.GetWaterDelta(itemName, invData.holdingEntity.inventory.holdingCount, tank.waterCount, AdvancedFarmingItemWaterExchange.ResolveMaxWater(blockValue, tank));
        if (delta <= 0)
        {
            Manager.PlayInsidePlayerHead("ui_denied");
            return;
        }

        AdvancedFarmingItemWaterExchange.ApplyWaterDelta(invData.world, blockPos, blockValue, tank, delta);
        AdvancedFarmingItemWaterExchange.ExchangeHeldItem(invData, changeItemToItem);

        invData.holdingEntity.RightArmAnimationUse = true;

        // Filled jars use XML Sound_start just like the 2.6 implementation.
        // Missing XML sound config should remain visible during testing.
        if (!string.IsNullOrEmpty(soundStart))
            invData.holdingEntity.PlayOneShot(soundStart, false);
    }
}
