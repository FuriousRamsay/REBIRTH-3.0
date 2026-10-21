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
        // Entity sinks have no world water block. Consume both edges before
        // the farming/native dump fallback can place water on the cabin floor.
        if(actionData?.invData?.holdingEntity is EntityPlayerLocal player &&
            !RebirthCamperSink.Primary(player.inventory,1,released))return;
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
        if ((itemName != "bucketRiverWater") || !AdvancedFarmingWaterExchangeOriginal.Begin(invData, blockPos))
        {
            Manager.PlayInsidePlayerHead("ui_denied");
            return;
        }

        invData.holdingEntity.RightArmAnimationUse = true;
        if (soundStart != null)
            invData.holdingEntity.PlayOneShot(soundStart, false);
    }
}
