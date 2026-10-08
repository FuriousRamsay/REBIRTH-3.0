using UnityEngine;

#nullable disable

/// <summary>Resolves live native factions without inventing faction identity from NPC category.</summary>
public static class RebirthNpcFactionCombatResolver
{
    public static RebirthNpcCombatDisposition Classify(float relationship)
    {
        if(float.IsNaN(relationship)||float.IsInfinity(relationship))return RebirthNpcCombatDisposition.Unknown;
        // Installed native tier boundaries: Hate<200, Dislike<400, Neutral<600, Like>=600.
        if(relationship<400f)return RebirthNpcCombatDisposition.Hostile;
        if(relationship<600f)return RebirthNpcCombatDisposition.Neutral;
        return RebirthNpcCombatDisposition.Allied;
    }
    public static bool TryResolve(RebirthNpcRuntimeState actor,RebirthNpcRuntimeState target,out RebirthNpcCombatDisposition disposition)
    {
        disposition=RebirthNpcCombatDisposition.Unknown;
        EntityAlive left,right;
        if(!TryBind(actor,out left)||!TryBind(target,out right)||left.world!=right.world||FactionManager.Instance==null)return false;
        disposition=Classify(FactionManager.Instance.GetRelationshipValue(left,right));
        // A live native relationship was sampled, even when its value is invalid.
        // Keep Unknown authoritative so callers cannot fall back to a hostile hint.
        return true;
    }
    private static bool TryBind(RebirthNpcRuntimeState state,out EntityAlive entity)
    {
        entity=null;int id;
        var world=GameManager.Instance?.World;
        if(state==null||world==null||state.Presence!=RebirthNpcPresenceState.Active||!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out id))return false;
        var npc=world.GetEntity(id) as EntityRebirthNPC;
        if(npc==null||npc.IsDead()||npc.world!=world||npc.RebirthRuntimeState==null||npc.RebirthRuntimeState.StableId!=state.StableId)return false;
        entity=npc;return true;
    }
}