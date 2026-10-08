using UnityEngine;
using UnityEngine.Scripting;
using Audio;

#nullable disable

/// <summary>
/// DumpWater wrapper that preserves vanilla water dumping while adding focused fill-storage
/// support for Advanced Farming tanks and dew collectors.
/// </summary>
[Preserve]
public class ItemActionDumpWaterRebirth : ItemActionDumpWater
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
            if (itemName != "bucketRiverWater")
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
        if (delta <= 0)
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
