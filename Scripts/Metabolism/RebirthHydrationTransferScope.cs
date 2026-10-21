using System;

// Hydration scope only. Consumption and Survivor Origin keep their own contracts.
// Native server generations must be durably enrolled; a remote owner can only
// observe the generation projected by its current native server session.
internal static class RebirthHydrationTransferScope
{
    internal static bool TryObserve(EntityPlayer player,out string generation)
    {
        generation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||player?.world==null||
                !ReferenceEquals(player.world,GameManager.Instance?.World)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player))return false;
            if(RebirthSurvivorMode.IsEnabledForCurrentWorld())
                return RebirthSurvivorRequestScope.TryNormalize(RebirthCamperCharacterAccess.CurrentCreation(player),out generation);
            if(player.world.IsRemote())
                return player is EntityPlayerLocal local&&RebirthHydrationGenerationClient.TryGet(local,out generation);
            if(SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
                !RebirthMetabolismStateRepository.TryGet(player,out var state)||!RebirthHydrationGeneration.IsCanonical(state.HydrationGenerationId))return false;
            generation=state.HydrationGenerationId;return true;
        }
        catch{generation=null;return false;}
    }
    internal static bool TryCapture(EntityPlayer player,out string generation)
    {
        generation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||player?.world==null||
                !ReferenceEquals(player.world,GameManager.Instance?.World)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.IsDead()||!player.IsSpawned())return false;
            if(RebirthSurvivorMode.IsEnabledForCurrentWorld())
            {
                if(!RebirthConsumptionOwnerPublication.TryCaptureCreation(player,out var creation)||
                    !RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized))return false;
                generation=normalized;return true;
            }
            if(player.world.IsRemote())
                return player is EntityPlayerLocal local&&RebirthHydrationGenerationClient.TryGet(local,out generation);
            return RebirthMetabolismStateRepository.TryEnsureHydrationGeneration(player,out generation);
        }
        catch{generation=null;return false;}
    }

    // Does not create/migrate a state row or turn an empty legacy identity into
    // authority. Callers must also retain their actual native row/scope witness.
    internal static bool MatchesState(string generation,RebirthMetabolismState state)
    {
        if(!ThreadManager.IsMainThread()||state==null)return false;
        return RebirthSurvivorMode.IsEnabledForCurrentWorld()
            ?RebirthSurvivorRequestScope.Matches(generation,state.SurvivorCreationId)
            :RebirthHydrationGeneration.IsCanonical(generation)&&generation==state.HydrationGenerationId;
    }
}
