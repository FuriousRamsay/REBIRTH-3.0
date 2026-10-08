using System;

#nullable disable

// Explicit game-thread reconciliation entry point. No ambient hooks, scheduler registration or fresh native creation.
internal static class RebirthNpcPreparedPopulationDispatcher
{
    internal static bool TryReconcile(string replayId,out RebirthNpcStableId stable,out string reason)
    {
        stable=default(RebirthNpcStableId);reason="Original prepared request is unavailable.";
        var world=GameManager.Instance?.World;
        if(world==null||world.IsRemote())return false;
        if(!RebirthNpcWorldIntegrationService.TryGetPreparedRequest(replayId,out var request))
        {
            if(!RebirthNpcWorldIntegrationService.TryGetCompletedBinding(replayId,out var completion))return false;
            stable=completion.Stable;
            var originalCandidate=world.GetEntity(completion.NativeId);
            return RebirthNpcWorldIntegrationService.TryCompletePublishedExistingPerson(replayId,world,originalCandidate,out reason);
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
        return RebirthNpcWorldIntegrationService.TryCompletePublishedExistingPerson(replayId,world,candidate,out reason);
    }
}