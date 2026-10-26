using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Read-only native policy observation. This is deliberately not a tool-wear,
// fractional-credit or native repair-effect receipt and cannot release B08.
internal static class RebirthNpcSingleRepairPolicy
{
    private static string R(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
    internal static bool TryObserve(EntityRebirthHumanoidNPC npc,Vector3i position,BlockValue block,bool visible,out XElement policy,bool requireReady=true)
    {
        policy=null;
        try
        {
            if(!ThreadManager.IsMainThread()||npc?.world?.IsRemote()!=false||npc.IsDead()||npc.IsPreparedRestorationPending||
                !ReferenceEquals(npc.world,GameManager.Instance?.World)||!ReferenceEquals(npc.world.GetEntity(npc.entityId),npc)||
                block.isair||block.ischild||block.Block.GetType()!=typeof(Block)||block.Block.isMultiBlock||block.Block.shape.IsTerrain()||
                block.damage<=0||block.damage>=block.Block.MaxDamage||!block.Block.CanRepair(block)||
                block.Block.RepairItems?.Count!=1||block.Block.RepairItemsMeshDamage!=null&&block.Block.shape.UseRepairDamageState(block)||
                !ReferenceEquals(npc.inventory?.Hand,npc.Hand)||npc.Hand==null||npc.Hand.IsBusy||npc.GetAttackTarget()!=null)return false;
            var tool=npc.inventory.GetItem(npc.inventory.SelectedSlot);
            var held=npc.inventory.holdingItemItemValue;var invData=npc.inventory.holdingItemData;
            var actions=held?.ItemClass?.Actions;
            if(tool==null||tool.IsEmpty()||tool.count!=1||!ReferenceEquals(tool.itemValue,held)||
                invData==null||!ReferenceEquals(invData.itemValue,held)||!ReferenceEquals(invData.holdingEntity,npc)||!ReferenceEquals(invData.world,npc.world)||actions==null||
                held.MaxUseTimes<=0||float.IsNaN(held.UseTimes)||float.IsInfinity(held.UseTimes)||held.UseTimes<0f||requireReady&&held.UseTimes>=held.MaxUseTimes||
                RebirthCompanionInventoryLockService.IsNpcItemLocked(npc.RebirthRuntimeState.StableId,held.ItemClass.GetItemName()))return false;
            int[] repair=Enumerable.Range(0,actions.Length).Where(i=>actions[i]?.GetType()==typeof(ItemActionRepair)).ToArray();
            if(repair.Length!=1||invData.actionData==null||repair[0]>=invData.actionData.Count||
                invData.actionData[repair[0]] is not ItemActionRepair.InventoryDataRepair data)return false;
            if(!ReferenceEquals(data.invData,invData))return false;
            var action=(ItemActionRepair)actions[repair[0]];
            if(float.IsNaN(action.repairAmount)||float.IsInfinity(action.repairAmount)||action.repairAmount<block.damage||action.repairAmount>=int.MaxValue||
                float.IsNaN(action.Delay)||float.IsInfinity(action.Delay)||action.Delay<0f||
                float.IsNaN(data.lastUseTime)||float.IsInfinity(data.lastUseTime)||requireReady&&Time.time-data.lastUseTime<action.Delay||
                EffectManager.GetValue(PassiveEffects.DisableItem,held,0f,npc,null,held.ItemClass.ItemTags)>0f)return false;
            var material=block.Block.RepairItems[0];
            float cost=material.Count*((float)block.damage/block.Block.MaxDamage)*block.Block.ResourceScale;
            // An integral one-stroke cost leaves zero carried fraction. No
            // approximation/tolerance is used to invent a whole-cost subset.
            if(material.Count<=0||float.IsNaN(cost)||float.IsInfinity(cost)||cost<1f||cost>=65536f||cost!=(float)Math.Floor(cost))return false;
            bool same=data.lastHitPosition==position&&data.lastHitBlockValue.type==block.type&&ReferenceEquals(data.lastRepairItems,block.Block.RepairItems);
            if(data.lastRepairItems!=null&&(!same||data.lastRepairItemsPercents==null||data.lastRepairItemsPercents.Length!=1||data.lastRepairItemsPercents[0]!=0f))return false;
            if(data.lastRepairItems==null&&data.lastRepairItemsPercents!=null&&data.lastRepairItemsPercents.Any(value=>value!=0f))return false;
            if(visible)
            {
                var ray=npc.GetLookRay();var delta=position.ToVector3()+new Vector3(.5f,.5f,.5f)-ray.origin;
                float distance=delta.magnitude;
                if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<.5f||distance>Constants.cDigAndBuildDistance||
                    (npc.position-position.ToVector3()).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)return false;
                ray.direction=delta/distance;ray.origin+=ray.direction*.5f;
                if(!Voxel.Raycast(npc.world,ray,Constants.cDigAndBuildDistance,-538480645,4095,0f)||
                    !Voxel.voxelRayHitInfo.bHitValid||Voxel.voxelRayHitInfo.hit.blockPos!=position)return false;
            }
            policy=new XElement("nativeRepairPolicy",new XAttribute("version",1),new XAttribute("slot",npc.inventory.SelectedSlot),
                new XAttribute("tool",RebirthHumanEquipment.Frame(tool)),new XAttribute("action",repair[0]),new XAttribute("amount",R(action.repairAmount)),
                new XAttribute("delay",R(action.Delay)),new XAttribute("lastUse",R(data.lastUseTime)),new XAttribute("maximum",held.MaxUseTimes),
                new XAttribute("cost",(int)cost),new XAttribute("fraction",0),new XAttribute("requiredWear",1),
                new XAttribute("repairType",(int)data.repairType),new XAttribute("useStarted",data.bUseStarted),
                new XAttribute("damagePercent",R(data.blockDamagePerc)),new XAttribute("upgradePercent",R(data.upgradePerc)),
                new XAttribute("hitX",data.lastHitPosition.x),new XAttribute("hitY",data.lastHitPosition.y),new XAttribute("hitZ",data.lastHitPosition.z),
                new XAttribute("hitRaw",data.lastHitBlockValue.rawData),new XAttribute("hitDamage",data.lastHitBlockValue.damage),
                new XAttribute("hasRepairItems",data.lastRepairItems!=null));
            return true;
        }
        catch{return false;}
    }
}
