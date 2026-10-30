using System;
using UnityEngine;
using UnityEngine.Scripting;
using Audio;

#nullable disable

// Filled water jars keep their tank deposit action when full. Partial jars use
// right-click to top up at a focused source; inventory stacking never invokes this.
[Preserve]
public abstract class ItemActionWaterRefillRebirth : ItemActionCollectWater
{
    private sealed class RefillData:CollectWaterActionData
    {
        internal bool Refilling;internal string Before;internal ItemValue Original;internal int Count,Slot;internal World World;internal EntityPlayerLocal Owner;
        internal RefillData(ItemInventoryData data,int index):base(data,index){}
    }
    public override ItemActionData CreateModifierData(ItemInventoryData data,int index)=>new RefillData(data,index);
    public override void ExecuteAction(ItemActionData actionData,bool released)
    {
        if(!ThreadManager.IsMainThread()||!released||actionData is not RefillData data||data.invData==null||data.lastUseTime>0f)return;
        var inv=data.invData;
        // Natural water is the native renewable jar source (ReduceWater=false).
        // Do not invoke the old filled-jar empty-to-air exchange on any miss.
        if(inv.holdingEntity is not EntityPlayerLocal player||inv.world is not World world||
            !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)||
            player.IsDead()||!player.IsSpawned()||!ReferenceEquals(player.world,world)||!AdvancedFarmingWaterExchangeOriginal.CanUseNaturalRefill(player))return;
        int slot=inv.slotIdx;
        var cells=player.inventory?.ItemGrid?.items;
        if(cells==null||slot<0||slot>=RebirthToolbeltCapacity.GetOwnedSlotCount(player,cells.Length)||slot!=player.inventory.holdingItemIdx)return;
        var before=player.inventory.GetItem(slot);
        if(!RebirthWaterRefillPlan.IsFillableJar(before)||isReduceWater||
            !RebirthWaterRefillPlan.TryCreate(before,RebirthWaterRefillPlan.Grade.Murky,double.PositiveInfinity,out _))return;
        data.Refilling=true;
        data.Original=before.itemValue;data.Before=RebirthNativeItemCodec.Encode(before.itemValue);data.Count=before.count;data.Slot=slot;data.World=world;data.Owner=player;
        base.ExecuteAction(data,released);
        if(data.lastUseTime==0f){data.Refilling=false;data.Before=null;data.Original=null;data.Owner=null;data.World=null;}
    }
    protected void ExecuteNativeCollect(ItemActionData data,bool released)
    {if(data is RefillData refill)refill.Refilling=false;base.ExecuteAction(data,released);}
    public override void StopHolding(ItemActionData actionData)
    {
        if(actionData is RefillData data){data.Refilling=false;data.Before=null;data.Original=null;data.Owner=null;data.World=null;}
        base.StopHolding(actionData);
    }
    public override void OnHoldingUpdate(ItemActionData actionData)
    {
        if(!ThreadManager.IsMainThread()||actionData is not RefillData data)return;
        if(!data.Refilling){base.OnHoldingUpdate(actionData);return;}
        if(data.lastUseTime==0f||IsActionRunning(data))return;
        data.Refilling=false;
        data.lastUseTime=0f;var player=data.Owner;var world=data.World;string expected=data.Before;var original=data.Original;
        data.Owner=null;data.World=null;data.Before=null;data.Original=null;
        if(player==null||world==null||player.IsDead()||!player.IsSpawned()||!ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)||
            data.invData?.holdingEntity!=player||data.Slot!=player.inventory.holdingItemIdx||
            !AdvancedFarmingWaterExchangeOriginal.CanUseNaturalRefill(player)||world.ChunkCache==null)return;
        var cells=player.inventory?.ItemGrid?.items;
        if(cells==null||data.Slot<0||data.Slot>=RebirthToolbeltCapacity.GetOwnedSlotCount(player,cells.Length))return;
        var current=player.inventory.GetItem(data.Slot);
        if(current==null||current.IsEmpty()||!ReferenceEquals(current.itemValue,original)||current.count!=data.Count||RebirthNativeItemCodec.Encode(current.itemValue)!=expected||
            !RebirthWaterRefillPlan.IsFillableJar(current)||!RebirthWaterRefillPlan.TryCreate(current,RebirthWaterRefillPlan.Grade.Murky,double.PositiveInfinity,out var plan))return;
        // Revalidate the source and current reach at completion, as well as the
        // held owner image. A delayed action cannot fill from a removed/distant source.
        var ray=player.GetLookRay();
        if(!Voxel.Raycast(world,ray,Constants.cDigAndBuildDistance,16,4095,0f)||!Voxel.voxelRayHitInfo.bHitValid||
            Voxel.voxelRayHitInfo.hit.blockPos!=data.targetPosition||!Voxel.voxelRayHitInfo.hit.voxelData.WaterValue.HasMass())return;
        waterPoints.Clear();
        try
        {
            int mass=CollectWaterUtils.CollectWater(world.ChunkCache,data.targetMass,data.targetPosition,2,waterPoints);
            if(mass>195)current.Set(plan.After.itemValue,plan.After.count);
        }
        finally{waterPoints.Clear();data.Before=null;data.Original=null;data.Owner=null;data.World=null;}
    }
}

[Preserve]
public class ItemActionExchangeItemRebirth : ItemActionWaterRefillRebirth
{
    public override void ExecuteAction(ItemActionData actionData,bool released)
    {
        if(!ThreadManager.IsMainThread()||!released||actionData?.invData==null||actionData.lastUseTime>0f)return;
        var inv=actionData.invData;
        if(AdvancedFarmingRuntimePolicy.Enabled&&AdvancedFarmingItemWaterExchange.TryGetAdvancedWaterStorage(inv,out var position,out _,out _))
        {
            if(!RebirthWaterRefillPlan.WaterName(inv.item?.GetItemName())||!AdvancedFarmingWaterExchangeOriginal.Begin(inv,position))
            {Manager.PlayInsidePlayerHead("ui_denied");return;}
            inv.holdingEntity.RightArmAnimationUse=true;
            if(!string.IsNullOrEmpty(soundStart))inv.holdingEntity.PlayOneShot(soundStart,false);
            return;
        }
        base.ExecuteAction(actionData,released);
    }
}
