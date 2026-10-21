using System;

internal static class RebirthHydrationIntentClient
{
    private static bool sending;
    internal static bool TrySend(RebirthHydrationProposalClient.Intent intent)
    {
        if(!ThreadManager.IsMainThread()||sending)return false;sending=true;
        try
        {
            if(intent==null||!intent.Current())return false;
            var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationIntent));
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationIntent>().Setup(intent);
            if(!intent.Current()||!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
            game.SaveLocalPlayerData();
            if(!intent.Current())return false;
            game.TriggerSendOfLocalPlayerDataFile(0f);
            if(!intent.Current()||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return false;
            manager.SendToServer(packet);return intent.Current();
        }
        catch{return false;}
        finally{sending=false;}
    }
}
