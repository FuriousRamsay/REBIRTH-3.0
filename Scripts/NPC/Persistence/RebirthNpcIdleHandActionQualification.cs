using System;
// Only stable idle native action state can be rebuilt from the original ItemValue.
// Active aiming/reload/burst transitions require their own restoration contract.
internal static class RebirthNpcIdleHandActionQualification
{
    internal static bool Qualified(ItemValue value)
    {
        if(value==null||value.IsEmpty()||value.ItemClass?.GetType()!=typeof(ItemClass))return false;
        foreach(var action in value.ItemClass.Actions)
            if(action!=null&&action.GetType()!=typeof(ItemAction)&&action.GetType()!=typeof(ItemActionMelee)&&action.GetType()!=typeof(ItemActionRanged)&&action.GetType()!=typeof(ItemActionZoom))return false;
        return true;
    }
    internal static bool IsIdle(EntityRebirthHumanoidNPC npc)
    {
        try
        {
            if(npc?.Hand==null||npc.AimingGun||npc.Hand.IsSwitching||npc.Hand.switchingCoroutine!=null||npc.Hand.holstering!=null||npc.Hand.mode==Hand.HoldingMode.Transient)return false;
            var held=npc.Hand.Held;
            if(held==null||!Qualified(held.itemValue)||npc.Hand.IsActionRunning())return false;
            foreach(var data in held.actionData)
            {
                if(data is ItemActionZoom.ItemActionDataZoom zoom&&(zoom.bZoomInProgress||zoom.aimingCoroutine!=null||zoom.aimingValue))return false;
                if(data is ItemActionRanged.ItemActionDataRanged ranged&&(ranged.state!=ItemActionFiringState.Off||ranged.isReloading||ranged.isWeaponReloading||ranged.isChangingAmmoType||ranged.burstShotStarted&&!ranged.burstShotFinished))return false;
            }
            return true;
        }
        catch(Exception){return false;}
    }
}
