using System;
using UnityEngine;

// Captures location only from an exact currently published embodiment.
// A guard anchor describes an order, not the NPC's current world location.
internal static class RebirthNpcAggregateTransformCapture
{
    internal static bool TryCapture(World world,RebirthNpcRuntimeState state,RebirthNpcTransformRecord transform)
    {
        if(world==null||world.IsRemote()||state==null||transform==null||
            !ReferenceEquals(GameManager.Instance?.World,world)||
            !RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out var entityId))return false;
        var npc=world.GetEntity(entityId) as EntityRebirthNPC;
        var live=npc?.RebirthRuntimeState;
        if(npc==null||!ReferenceEquals(npc.world,world)||live==null||live.StableId!=state.StableId||
            live.Revision!=state.Revision||!string.Equals(live.ProfileId,state.ProfileId,StringComparison.OrdinalIgnoreCase))return false;
        var position=npc.position;float yaw=npc.rotation.y;
        if(!Finite(position.x)||!Finite(position.y)||!Finite(position.z)||!Finite(yaw)||
            !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetEntity(entityId),npc))return false;
        transform.WorldPosition=position;transform.RotationYaw=yaw;transform.TransformRevision=state.Revision;
        return true;
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}