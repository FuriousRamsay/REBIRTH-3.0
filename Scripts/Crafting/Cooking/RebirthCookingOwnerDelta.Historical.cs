using System;

internal static partial class RebirthCookingOwnerDelta
{
    // Called only after an authenticated terminal cancellation reply proved
    // saved owner credit AND saved station removal. Retires old warm metadata;
    // never applies a delta, rebases life, saves a player or edits a receipt.
    internal static bool RetireHistoricalMetadata(EntityPlayerLocal player,Plan plan,Func<bool> authenticatedTerminalCurrent)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||plan==null||authenticatedTerminalCurrent==null||permit!=null||
                plan.Deaths<0||plan.Deaths>=player.Died)return false;
            int deaths=player.Died;var world=player.world;string directory=GameIO.GetSaveGameDir();
            bool Current()=>authenticatedTerminalCurrent()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(world?.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead()&&player.Died==deaths&&
                plan.World==world.worldState.Guid&&plan.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&directory==GameIO.GetSaveGameDir()&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.CanonicalId==plan.Owner;
            if(!Current())return false;
            if(Attempts.TryGetValue(plan.Operation,out var attempt))
            {
                if(attempt.Image!=Encode(plan)||attempt.Plan.Operation!=plan.Operation||attempt.Deaths!=plan.Deaths||attempt.Directory!=directory||!Current())return false;
                Attempts.Remove(plan.Operation);
            }
            return Current();
        }
        catch{return false;}
    }
}
