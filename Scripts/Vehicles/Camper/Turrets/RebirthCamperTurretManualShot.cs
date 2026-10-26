using System;
using HarmonyLib;
using UnityEngine;

// Scope follows the actual synchronous native Fire call, never general player/ammo state.
internal static class RebirthCamperTurretManualShot
{
    [ThreadStatic] internal static AutoTurretFireController Active;

    internal static void Fire(AutoTurretFireController fire,bool manual)
    {
        if(!manual)
        {
            var previous=Active;
            try{Active=fire;fire.Fire();}
            finally{Active=previous;}
            return;
        }
        var prior=Active;string flash=fire.muzzleFireParticle,smoke=fire.muzzleSmokeParticle;
        int before=fire.TileEntity?.PowerItem is PowerRangedTrap power&&power.Stacks?.Length>0?power.Stacks[0].count:0;
        try
        {
            Active=fire;
            // The native muzzle-fire prefab is a composite flash/cloud, and carries its sound.
            // Omit this near-camera composite; retain the same native networked sound separately.
            fire.muzzleFireParticle=string.Empty;fire.muzzleSmokeParticle=string.Empty;
            fire.Fire();
            if(fire.TileEntity?.PowerItem is PowerRangedTrap after&&after.Stacks?.Length>0&&after.Stacks[0].count<before&&!string.IsNullOrEmpty(fire.fireSound))
                Audio.Manager.BroadcastPlay(fire.Muzzle.position+Origin.position,fire.fireSound);
        }
        finally{fire.muzzleFireParticle=flash;fire.muzzleSmokeParticle=smoke;Active=prior;}
    }

    internal static bool Precise(AutoTurretFireController fire,ItemValue ammo)
    {
        if(!ReferenceEquals(Active,fire)||fire==null)return false;
        int rays=ammo==null?fire.rayCount:(int)EffectManager.GetValue(PassiveEffects.RoundRayCount,ammo,fire.rayCount);
        return rays==1;
    }
}

[HarmonyPatch(typeof(AutoTurretFireController),nameof(AutoTurretFireController.getSpread))]
internal static class RebirthCamperTurretManualSpread
{
    static void Postfix(AutoTurretFireController __instance,ItemValue _itemValue,ref Vector2 __result)
    {if(RebirthCamperTurretManualShot.Precise(__instance,_itemValue))__result=Vector2.zero;}
}
