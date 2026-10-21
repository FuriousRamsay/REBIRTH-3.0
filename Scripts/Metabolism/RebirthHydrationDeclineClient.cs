using System;

internal static class RebirthHydrationDeclineClient
{
    private static bool sending;
    internal static bool TrySend(RebirthHydrationProposalClient.Intent intent)
    {
        if(!ThreadManager.IsMainThread()||sending)return false;sending=true;
        try
        {
            if(intent?.Original==null||!intent.Current()||RebirthHydrationRemoteClientGrant.HasIssued(intent.Operation))return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationDecline));
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationDecline>().Setup(intent);
            if(!intent.Current()||RebirthHydrationRemoteClientGrant.HasIssued(intent.Operation)||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
            manager.SendToServer(packet);return intent.Current();
        }
        catch{return false;}
        finally{sending=false;}
    }
}
