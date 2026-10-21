using System.Collections.Generic;
using UnityEngine;

internal sealed partial class RebirthCamperTurretRuntime
{
    internal float FocusDistance(int corner)
    {
        if(corner<0||corner>=4||guns[corner]?.Muzzle==null)return -1;
        var g=guns[corner];var state=Observe();float range=RebirthCamperTurretProfiles.Range(state.Slots[corner]);
        if(range<=0)return -1;
        var origin=g.Muzzle.position+Origin.position;float bestAngle=6,bestDistance=-1;
        var candidates=vehicle.world.GetEntitiesInBounds(typeof(EntityAlive),new Bounds(origin,Vector3.one*range*2),new List<Entity>());
        foreach(var entity in candidates)
        {
            if(!(entity is EntityAlive alive)||!Eligible(alive))continue;
            var offset=alive.getChestPosition()-origin;float distance=offset.magnitude;
            if(distance>range||distance<.1f)continue;
            float angle=Vector3.Angle(g.Muzzle.forward,offset);
            if(angle>=bestAngle||!Sight(g,alive,range))continue;
            bestAngle=angle;bestDistance=distance;
        }
        return bestDistance;
    }
}
