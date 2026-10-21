using System;

internal static partial class RebirthHydrationRemoteClientDispatch
{
    private static bool running;
    // Explicit owning action, never invoked merely by receiving grant bytes.
    // Upload scheduling is not server settlement or permission to release.
    internal static bool TryApplyAndUpload(EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread()||running)return false;
        running=true;
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthHydrationRemoteClientGrant.TryGet(player,out var grant)||!grant.Current())return false;
            var game=GameManager.Instance;
            // Resolve native upload mapping before the first financial write.
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));
            if(!grant.Current()||!ReferenceEquals(game,GameManager.Instance)||
                RebirthHydrationOwnerApply.Apply(grant)!=RebirthGearOwnerApplySequence.Result.Applied||!grant.Current()||
                !RebirthHydrationOwnerCheckpoint.TryPersist(player,grant.Original,RebirthGearOwnerReceipt.Applied)||
                !grant.Current()||!ReferenceEquals(game,GameManager.Instance))return false;
            game.TriggerSendOfLocalPlayerDataFile(0f);
            return grant.Current()&&ReferenceEquals(game,GameManager.Instance);
        }
        catch{return false;}
        finally{running=false;}
    }
}
