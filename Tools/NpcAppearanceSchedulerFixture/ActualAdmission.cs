using System;partial class AdmissionActor {    internal bool TryAdmitPreparedAi(string replayId,uint generation)
    {
        if(rebirthPreparedRestorationPending||RebirthRuntimeState.PreparedRestorationPending||generation!=rebirthRestoredGeneration||
            world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(world.GetEntity(entityId),this)||IsDead()||
            RebirthRuntimeState.Presence!=PresenceState.Active||
            !RebirthNpcWorldIntegrationService.TryGetCompletedBinding(replayId,out var completion)||
            completion.Stable!=RebirthRuntimeState.StableId||completion.Profile!=RebirthRuntimeState.ProfileId||
            completion.NativeId!=entityId||completion.Generation!=generation||
            !RebirthNpcAggregatePersistenceStore.TryGet(RebirthRuntimeState.StableId,out var person)||
            person.Profile.ProfileId!=RebirthRuntimeState.ProfileId||person.Presence.EmbodimentGeneration!=generation)return false;
        if(!(this is EntityRebirthHumanoidNPC humanoid)||!RebirthNpcPreparedPhysicalHold.IsOriginalReleaseReady(humanoid,generation))return false;
        hasAI=rebirthPreparedOriginalAi;return true;
    }
}