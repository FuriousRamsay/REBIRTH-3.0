using System;

internal static class RebirthHydrationAcknowledgmentClient
{
    private static bool sending;
    internal static bool TrySend(EntityPlayerLocal player,Guid operation,string digest)
    {
        if(!ThreadManager.IsMainThread()||sending)return false;
        sending=true;
        try
        {
            if(!RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,operation,digest,out var original))return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var game=GameManager.Instance;
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationAcknowledgment));
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationAcknowledgment>().Setup(player,operation,digest);
            if(!RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,operation,digest,out _))return false;
            game.TriggerSendOfLocalPlayerDataFile(0f);
            if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !ReferenceEquals(game,GameManager.Instance)||
                !RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,operation,digest,out var retained)||retained.Digest!=original.Digest)return false;
            manager.SendToServer(packet);
            // Keep terminal-seen custody for retries; queued is not received.
            return RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,operation,digest,out _);
        }
        catch{return false;}
        finally{sending=false;}
    }
}
