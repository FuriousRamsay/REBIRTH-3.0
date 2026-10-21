using System;

internal static partial class RebirthHydrationRemoteClientDispatch
{
    private static bool confirming;
    internal static bool TryRequestConfirmation(EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread()||confirming)return false;
        confirming=true;
        try
        {
            if(!RebirthHydrationRemoteClientGrant.TryGet(player,out var grant)||!grant.Current())return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationAppliedRequest));
            if(!grant.Current()||!TryApplyAndUpload(player)||!grant.Current()||
                !ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationAppliedRequest>().Setup(grant);
            if(!grant.Current()||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
            manager.SendToServer(packet);
            return grant.Current(); // Queued only; owner custody remains held.
        }
        catch{return false;}
        finally{confirming=false;}
    }
}
