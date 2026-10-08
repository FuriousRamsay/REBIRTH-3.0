using System;
using UnityEngine;

// Read-only resolution. Permissions, lesson timing and durable completion remain the caller's responsibility.
public static class RebirthTheorySpecialistResolver
{
    public static bool TryResolve(EntityPlayer student,int instructorEntityId,string skillId,
        out string instructorKey,out float theory)
    {
        instructorKey=null;theory=0f;
        EntityRebirthNPC npc;
        if(string.IsNullOrEmpty(skillId)||!TryResolveNpc(student,instructorEntityId,out npc)||
            !RebirthTheorySpecialistRegistry.TryGetTheory(npc.RebirthRuntimeState.ProfileId,skillId,out theory))return false;
        instructorKey=npc.RebirthRuntimeState.StableId.ToString();return true;
    }
    public static bool TryResolveSubjects(EntityPlayer student,int instructorEntityId,out string[] subjects)
    {
        subjects=new string[0];EntityRebirthNPC npc;
        if(!TryResolveNpc(student,instructorEntityId,out npc))return false;
        subjects=RebirthTheorySpecialistRegistry.GetSubjects(npc.RebirthRuntimeState.ProfileId);
        return subjects.Length>0;
    }
    private static bool TryResolveNpc(EntityPlayer student,int instructorEntityId,out EntityRebirthNPC npc)
    {
        npc=null;var world=GameManager.Instance?.World;
        if(!RebirthWorldCharacterRepository.IsServerAuthority||world==null||world.IsRemote()||
            student==null||student.IsDead()||!ReferenceEquals(student.world,world)||
            !ReferenceEquals(world.GetEntity(student.entityId),student)||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        var candidate=world.GetEntity(instructorEntityId) as EntityRebirthNPC;
        if(candidate==null||candidate.IsDead()||!ReferenceEquals(candidate.world,world)||
            !((candidate.position-student.position).sqrMagnitude<=25f))return false;
        var factions=FactionManager.Instance;
        if(factions==null||candidate.GetAttackTarget()!=null||
            !RebirthTheorySpecialistEligibility.CanInstruct(
                factions.GetRelationshipValue(candidate,student),factions.GetRelationshipValue(student,candidate)))return false;
        var state=candidate.RebirthRuntimeState;
        if(state==null||state.StableId==default(RebirthNpcStableId)||
            !string.Equals(state.ProfileId,candidate.RebirthProfileId,StringComparison.OrdinalIgnoreCase)||
            !RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out var currentId)||currentId!=instructorEntityId)return false;
        npc=candidate;return true;
    }
}
