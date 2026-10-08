using System;
static class IdleHandQualificationFixture
{
 internal static int Run()
 {
  int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};var npc=new EntityRebirthHumanoidNPC();var held=npc.Hand.Held;
  check(RebirthNpcIdleHandActionQualification.IsIdle(npc),"idle original bare hand qualified");npc.AimingGun=true;check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"stable aiming is not silently discarded");npc.AimingGun=false;
  var zoom=new ItemActionZoom.ItemActionDataZoom();held.actionData.Add(zoom);check(RebirthNpcIdleHandActionQualification.IsIdle(npc),"idle native zoom data qualified");zoom.bZoomInProgress=true;check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"zoom transition refused");zoom.bZoomInProgress=false;zoom.aimingValue=true;check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"zoom aiming value refused");zoom.aimingValue=false;zoom.aimingCoroutine=new object();check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"zoom coroutine refused");zoom.aimingCoroutine=null;
  var ranged=new ItemActionRanged.ItemActionDataRanged();held.actionData.Add(ranged);check(RebirthNpcIdleHandActionQualification.IsIdle(npc),"idle ranged data qualified");ranged.state=ItemActionFiringState.On;check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"firing state refused");ranged.state=ItemActionFiringState.Off;ranged.isReloading=true;check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"reload refused");ranged.isReloading=false;ranged.isWeaponReloading=true;check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"weapon reload refused");ranged.isWeaponReloading=false;ranged.isChangingAmmoType=true;check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"ammo transition refused");ranged.isChangingAmmoType=false;ranged.burstShotStarted=true;check(!RebirthNpcIdleHandActionQualification.IsIdle(npc),"unfinished burst refused");ranged.burstShotFinished=true;check(RebirthNpcIdleHandActionQualification.IsIdle(npc),"completed burst no longer active");return count;
 }
}
