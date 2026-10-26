// B10: looting, salvaging and gathering assignments are Follow-conditional. They are
// admitted and progressed only while the NPC is in the Follow order; leaving Follow
// suspends them without consuming a reservation, and re-entering Follow resumes.
// Uses the P6 predicate (the same source as EAIRebirthWork.CanExecute).
internal static class RebirthP11FollowCondition
{
    internal static bool Satisfied(RebirthNpcWorkAssignment assignment)
    {
        if (assignment == null) return false;
        return RebirthNpcWorkNavigationService.TryResolveNpc(assignment.NpcId, out EntityRebirthNPC npc) &&
            RebirthP6WorkFollowState.IsFollowing(npc);
    }
}
