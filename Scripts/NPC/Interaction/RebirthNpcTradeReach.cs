using System;
using System.Collections.Generic;
using UnityEngine;

// Fresh trade admission only. The existing UI searches this native bounds;
// already-permitted original completion deliberately does not call this gate.
internal static class RebirthNpcTradeReach
{
    internal const float SearchRadius=5f;
    internal static bool CanBegin(string actorId,RebirthNpcStableId npc)
    {
        if(!ThreadManager.IsMainThread())return false;
        if(!RebirthNpcPlayerInventoryEndpointService.TryGet(actorId,out var endpoint)||endpoint==null||
            !endpoint.StartsWith("player:",StringComparison.Ordinal)||!int.TryParse(endpoint.Substring(7),out int player))return false;
        return CanBegin(GameManager.Instance?.World?.GetEntity(player) as EntityPlayer,npc);
    }
    internal static bool CanBegin(EntityPlayer actor,RebirthNpcStableId npc)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
                actor==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||actor.IsDead()||!actor.IsSpawned()||
                npc.IsEmpty||RebirthNpcSaveQuarantineBundle.BlocksRetainedTrade(npc)||!RebirthNpcRuntimeRegistry.TryGetEntityId(npc,out int id)||world.GetEntity(id) is not EntityRebirthNPC target||
                !ReferenceEquals(target.world,world)||target.IsDead()||!target.IsSpawned()||target.RebirthRuntimeState?.StableId!=npc||
                RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(target)||!Finite(actor.position)||!Finite(target.position))return false;
            var candidates=new List<Entity>();
            world.GetEntitiesInBounds(typeof(EntityRebirthNPC),new Bounds(actor.position,new Vector3(SearchRadius*2f,SearchRadius*2f,SearchRadius*2f)),candidates);
            return candidates.Exists(value=>ReferenceEquals(value,target))&&ReferenceEquals(world,GameManager.Instance?.World)&&
                ReferenceEquals(world.GetEntity(actor.entityId),actor)&&ReferenceEquals(world.GetEntity(id),target);
        }
        catch{return false;}
    }
    private static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&
        !float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);
}
