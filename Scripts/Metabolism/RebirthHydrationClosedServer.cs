using System;

internal static class RebirthHydrationClosedServer
{
    internal static bool TrySend(World world,int playerId,ClientInfo peer,Guid operation,string digest)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
                manager?.IsServer!=true||peer==null||!peer.loginDone||peer.disconnecting||!ReferenceEquals(manager.Clients?.ForEntityId(playerId),peer))return false;
            var player=world.GetEntity(playerId) as EntityPlayer;
            if(player!=null&&RebirthHydrationTransferJournal.TryReadAcknowledged(player,operation,digest,out _)&&
                !RebirthHydrationServerSourceHold.RetireAcknowledgedHistory(player,operation,digest))return false;
            if(player==null||RebirthHydrationServerSourceHold.HoldsPlayer(player)||!RebirthHydrationTransferJournal.TryReadAcknowledged(player,operation,digest,out var original))return false;
            if(!RebirthHydrationProposalServer.TryRetireAcknowledged(player,operation,digest))return false;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationClosed));
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationClosed>().Setup(playerId,original);
            if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!ReferenceEquals(world,GameManager.Instance?.World)||
                !ReferenceEquals(manager.Clients?.ForEntityId(playerId),peer)||RebirthHydrationServerSourceHold.HoldsPlayer(player)||
                !RebirthHydrationTransferJournal.TryReadAcknowledged(player,operation,digest,out _))return false;
            manager.SendPackage(packet,_attachedToEntityId:playerId);return true;
        }
        catch{return false;}
    }
}
