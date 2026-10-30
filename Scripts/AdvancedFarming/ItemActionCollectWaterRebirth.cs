using UnityEngine;
using UnityEngine.Scripting;
using Audio;

#nullable disable

/// <summary>
/// CollectWater wrapper that preserves vanilla natural-water collection while adding
/// focused draw-from-storage support for Advanced Farming tanks and dew collectors.
/// </summary>
[Preserve]
public class ItemActionCollectWaterRebirth : ItemActionWaterRefillRebirth
{
    public override void ExecuteAction(ItemActionData actionData, bool released)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
        {
            if(actionData?.invData?.item?.GetItemName()=="drinkJarEmpty")base.ExecuteAction(actionData,released);
            else ExecuteNativeCollect(actionData,released);
            return;
        }

        if (!released || actionData == null || actionData.invData == null || actionData.lastUseTime > 0f)
            return;

        ItemInventoryData invData = actionData.invData;
        if (!AdvancedFarmingItemWaterExchange.TryGetAdvancedWaterStorage(invData, out Vector3i blockPos, out BlockValue blockValue, out TileEntityWaterTankRebirth tank))
        {
            if(actionData?.invData?.item?.GetItemName()=="drinkJarEmpty")base.ExecuteAction(actionData,released);
            else ExecuteNativeCollect(actionData,released);
            return;
        }

        string itemName = invData.item != null ? invData.item.GetItemName() : string.Empty;
        if ((itemName != "bucketEmpty" && itemName != "drinkJarEmpty") || !AdvancedFarmingWaterExchangeOriginal.Begin(invData, blockPos))
        {
            Manager.PlayInsidePlayerHead("ui_denied");
            return;
        }

        invData.holdingEntity.RightArmAnimationUse = true;
        if (soundStart != null)
            invData.holdingEntity.PlayOneShot(soundStart, false);
    }
}
