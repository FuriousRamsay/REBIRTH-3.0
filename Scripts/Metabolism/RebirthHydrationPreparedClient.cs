using System;

internal static class RebirthHydrationPreparedClient
{
    private static bool sending;
    internal static bool TrySend(RebirthHydrationProposalClient.Intent intent)
    {
        if(!ThreadManager.IsMainThread()||sending)return false;sending=true;
        try
        {
            if(intent?.Original==null||!intent.Current()||!RebirthHydrationOwnerHold.Matches(intent.Player,intent.Original))return false;
            var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationPreparedRequest));
            if(!intent.Current()||
                !RebirthHydrationOwnerCheckpoint.HasSaved(intent.Player,intent.Original,RebirthGearOwnerReceipt.Applied)&&
                !RebirthHydrationOwnerCheckpoint.TryPersist(intent.Player,intent.Original,RebirthGearOwnerReceipt.Applying)||!intent.Current())return false;
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationPreparedRequest>().Setup(intent);
            if(!intent.Current()||!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
            game.TriggerSendOfLocalPlayerDataFile(0f);
            if(!intent.Current()||!RebirthHydrationOwnerHold.Matches(intent.Player,intent.Original)||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
            manager.SendToServer(packet);return intent.Current();
        }
        catch{return false;}
        finally{sending=false;}
    }
}
