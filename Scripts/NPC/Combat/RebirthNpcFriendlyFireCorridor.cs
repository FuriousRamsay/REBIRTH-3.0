using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public static class RebirthNpcFriendlyFireCorridor
{
    public static bool TrySlope(float horizontal,float vertical,float accuracy,float offset,out float slope)
    {
        slope=0f;
        if(!Finite(horizontal)||!Finite(vertical)||!Finite(accuracy)||!Finite(offset)||accuracy<0f)return false;
        float degrees=(Math.Abs(horizontal)+Math.Abs(vertical))*accuracy+Math.Abs(offset);
        if(!Finite(degrees)||degrees>=80f)return false;
        slope=(float)Math.Tan(degrees*Math.PI/180d);
        return Finite(slope)&&slope>=0f;
    }
    private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    private static bool Finite(Vector3 value){return Finite(value.x)&&Finite(value.y)&&Finite(value.z);}
    public static bool IsClear(EntityRebirthNPC actor,EntityAlive target,ItemActionRanged action,ItemActionRanged.ItemActionDataRanged data,List<Entity> scratch)
    {
        if(actor==null||target==null||actor.world==null||actor.world.IsRemote()||target.world!=actor.world||FactionManager.Instance==null)return false;
        float range=action.GetIdealRangeForAI(data);
        float horizontal=EffectManager.GetValue(PassiveEffects.SpreadDegreesHorizontal,data.invData.itemValue,45f,actor);
        float vertical=EffectManager.GetValue(PassiveEffects.SpreadDegreesVertical,data.invData.itemValue,45f,actor);
        float incremental=EffectManager.GetValue(PassiveEffects.IncrementalSpreadMultiplier,data.invData.itemValue,1f,actor);
        float slope;
        if(!Finite(range)||range<=0f||range>512f||!Finite(incremental)||incremental<0f
            ||!TrySlope(horizontal,vertical,data.lastAccuracy*Math.Max(1f,incremental),action.spreadVerticalOffset,out slope))return false;
        Ray ray=actor.GetLookRay();
        if(!Finite(ray.origin)||!Finite(ray.direction)||ray.direction.sqrMagnitude<0.99f||ray.direction.sqrMagnitude>1.01f)return false;
        ray.direction=ray.direction.normalized;
        Vector3 end=ray.origin+ray.direction*range;
        float radius=slope*range+0.25f;
        Bounds query=new Bounds((ray.origin+end)*0.5f,new Vector3(Math.Abs(end.x-ray.origin.x),Math.Abs(end.y-ray.origin.y),Math.Abs(end.z-ray.origin.z)));
        query.Expand(radius*2f);
        scratch.Clear();actor.world.GetEntitiesInBounds(typeof(EntityAlive),query,scratch);
        foreach(var entity in scratch)
        {
            var other=entity as EntityAlive;
            if(other==null||other==actor||other==target||other.IsDead()||other.world!=actor.world)continue;
            bool protect=RebirthNpcFactionCombatResolver.Classify(FactionManager.Instance.GetRelationshipValue(actor,other))!=RebirthNpcCombatDisposition.Hostile;
            var npc=other as EntityRebirthNPC;
            if(npc!=null&&RebirthNpcFriendlyFirePolicy.AreAllied(actor.RebirthRuntimeState,npc.RebirthRuntimeState))protect=true;
            var player=other as EntityPlayer;
            if(player!=null&&actor.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.Player)
            {
                var identity=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
                if(identity==null||string.IsNullOrWhiteSpace(actor.RebirthRuntimeState.OwnerId)
                    ||string.Equals(actor.RebirthRuntimeState.OwnerId,identity.ToString(),StringComparison.OrdinalIgnoreCase))protect=true;
            }
            if(!protect)continue;
            Bounds body=other.boundingBox;
            if(!Finite(body.center)||!Finite(body.extents)||body.extents.x<0f||body.extents.y<0f||body.extents.z<0f) {scratch.Clear();return false;}
            float extent=body.extents.magnitude;
            float depth=Vector3.Dot(body.center-ray.origin,ray.direction)+extent;
            if(!Finite(extent)||!Finite(depth)){scratch.Clear();return false;}
            if(depth<0f)continue;
            body.Expand(2f*(slope*Math.Min(range,depth)+0.25f));
            float distance;
            if(body.Contains(ray.origin)||(body.IntersectRay(ray,out distance)&&distance<=range)){scratch.Clear();return false;}
        }
        scratch.Clear();return true;
    }
}