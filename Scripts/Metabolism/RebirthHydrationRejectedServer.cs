using System;

internal static class RebirthHydrationRejectedServer
{
    internal static bool TrySend(World world,int playerId,ClientInfo peer,Guid operation,string digest)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
                manager?.IsServer!=true||peer==null||!peer.loginDone||peer.disconnecting||!ReferenceEquals(manager.Clients?.ForEntityId(playerId),peer))return false;
            var player=world.GetEntity(playerId) as EntityPlayer;
            if(!RebirthHydrationTransferJournal.SavedRejection.TryRead(player,operation,digest,out var rejection))return false;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationPreparedRejection));
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationPreparedRejection>().Setup(rejection);
            if(!rejection.Current()||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(manager.Clients?.ForEntityId(playerId),peer))return false;
            manager.SendPackage(packet,_attachedToEntityId:playerId);return rejection.Current();
        }
        catch{return false;}
    }
}
