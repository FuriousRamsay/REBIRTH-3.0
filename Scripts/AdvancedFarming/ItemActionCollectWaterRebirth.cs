using UnityEngine;
using UnityEngine.Scripting;
using Audio;

#nullable disable

/// <summary>
/// CollectWater wrapper that preserves vanilla natural-water collection while adding
/// focused draw-from-storage support for Advanced Farming tanks and dew collectors.
/// </summary>
[Preserve]
public class ItemActionCollectWaterRebirth : ItemActionCollectWater
{
    public override void ExecuteAction(ItemActionData actionData, bool released)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
        {
            base.ExecuteAction(actionData, released);
            return;
        }

        if (!released || actionData == null || actionData.invData == null || actionData.lastUseTime > 0f)
            return;

        ItemInventoryData invData = actionData.invData;
        if (!AdvancedFarmingItemWaterExchange.TryGetAdvancedWaterStorage(invData, out Vector3i blockPos, out BlockValue blockValue, out TileEntityWaterTankRebirth tank))
        {
            base.ExecuteAction(actionData, released);
            return;
        }

        string itemName = invData.item != null ? invData.item.GetItemName() : string.Empty;
        if (!AdvancedFarmingItemWaterExchange.IsServerAuthority())
        {
            if (itemName != "bucketEmpty" && itemName != "drinkJarEmpty")
            {
                Manager.PlayInsidePlayerHead("ui_denied");
                return;
            }

            AdvancedFarmingItemWaterExchange.RequestServerWaterExchange(invData, blockPos, itemName);
            invData.holdingEntity.RightArmAnimationUse = true;
            if (soundStart != null)
                invData.holdingEntity.PlayOneShot(soundStart, false);
            return;
        }

        int delta = AdvancedFarmingItemWaterExchange.GetWaterDelta(itemName, invData.holdingEntity.inventory.holdingCount, tank.waterCount, AdvancedFarmingItemWaterExchange.ResolveMaxWater(blockValue, tank));
        if (delta >= 0)
        {
            Manager.PlayInsidePlayerHead("ui_denied");
            return;
        }

        AdvancedFarmingItemWaterExchange.ApplyWaterDelta(invData.world, blockPos, blockValue, tank, delta);
        AdvancedFarmingItemWaterExchange.ExchangeHeldItem(invData, changeItemToItem);

        invData.holdingEntity.RightArmAnimationUse = true;
        if (soundStart != null)
            invData.holdingEntity.PlayOneShot(soundStart, false);
    }
}
