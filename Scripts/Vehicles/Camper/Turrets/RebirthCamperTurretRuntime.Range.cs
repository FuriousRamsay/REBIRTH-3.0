using UnityEngine;

internal sealed partial class RebirthCamperTurretRuntime
{
    private bool ManualRangeBlocked(Gun gun,ControlLease lease,int corner,RebirthCamperTurretSlot slot)
    {
        float range=RebirthCamperTurretProfiles.Range(slot);if(range<=0)return false;
        if(Time.unscaledTime<lease.NextRangeProbe)return true;
        // Probe the actual barrel's first visible impact, beyond the ammo's reach.
        // Empty sky has no target to reject; native free firing still works there.
        if(!Voxel.Raycast(vehicle.world,new Ray(gun.Muzzle.position+Origin.position,gun.Muzzle.forward),
            Mathf.Max(1000,range),-538751005,8,0))return false;
        float distance=Mathf.Sqrt(Voxel.voxelRayHitInfo.hit.distanceSq);
        if(distance<=range+.02f)return false;
        lease.NextRangeProbe=Time.unscaledTime+.1f;
        if(Time.unscaledTime>=lease.NextRangeWarning)
        {
            lease.NextRangeWarning=Time.unscaledTime+1;
            RebirthCamperTurretAccess.RangeWarning(vehicle,lease.Player,corner,lease.Token,range,distance);
        }
        return true;
    }
}
