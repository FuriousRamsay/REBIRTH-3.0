using System;

#nullable disable

// Explicit game-thread reconciliation entry point. No ambient hooks, scheduler registration or fresh native creation.
internal static class RebirthNpcPreparedPopulationDispatcher
{
    internal static bool TryReconcile(string replayId,out RebirthNpcStableId stable,out string reason)
    {
        stable=default(RebirthNpcStableId);reason="Original prepared request is unavailable.";
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote())return false;
        if(!RebirthNpcWorldIntegrationService.TryGetPreparedRequest(replayId,out var request))
        {
            if(!RebirthNpcWorldIntegrationService.TryGetCompletedBinding(replayId,out var completion))return false;
            stable=completion.Stable;
            var originalCandidate=world.GetEntity(completion.NativeId);
            return CompleteAndRelease(replayId,world,originalCandidate,out reason);
        }
        if(!RebirthNpcStableId.TryParse(request.StableId,out stable))return false;        if(!RebirthNpcAggregatePersistenceStore.TryGet(stable,out var person)||
            !RebirthNpcPreparedPersonAdmission.TryValidate(person,out var profile,out reason,true)||request.Profile!=profile)return false;
        if(!request.IsAttempted)
        {reason="Fresh native reconstruction is not admitted: AI, vitals, item/controller restoration must be qualified first.";return false;}
        if(!request.IsConstructed)
        {reason="Native construction outcome is uncertain; original identity retained, no retry construction.";return false;}
        var candidate=world.GetEntity(request.NativeEntityId);
        if(candidate==null)
        {reason="Original constructed/publishing candidate is not observed; no recreate or new identity.";return false;}
        return CompleteAndRelease(replayId,world,candidate,out reason);
    }
    private static bool CompleteAndRelease(string replayId,World world,Entity candidate,out string reason)
    {
        reason="Original published embodiment requires reconciliation.";
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||
            !ReferenceEquals(GameManager.Instance?.World,world)||!(candidate is EntityRebirthNPC npc)||
            !ReferenceEquals(candidate.world,world)||!ReferenceEquals(world.GetEntity(candidate.entityId),candidate)||
            npc.IsDead()||npc.IsMarkedForUnload())return false;
        if(!RebirthNpcWorldIntegrationService.TryCompletePublishedExistingPerson(replayId,world,candidate,out reason))return false;
        // Completion is a saved publication witness; the actor separately checks
        // native reconstruction, current binding and physical custody for release.
        if(!npc.TryReleasePreparedRestoration(replayId))
        {reason="Original publication is saved; native release remains retained for reconciliation.";return false;}
        reason=string.Empty;return true;
    }
}
