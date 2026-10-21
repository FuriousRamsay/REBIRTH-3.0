using System;

internal static class RebirthHydrationDeclineServer
{
    internal static bool Receive(World world,int playerId,ClientInfo peer,Guid operation,string digest)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
                manager?.IsServer!=true||peer==null||!peer.loginDone||peer.disconnecting||!ReferenceEquals(manager.Clients?.ForEntityId(playerId),peer))return false;
            var player=world.GetEntity(playerId) as EntityPlayer;if(player==null)return false;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationPreparedRejection));
            if(RebirthHydrationProposalServer.TryGet(player,peer,operation,digest,out var proposal))
            {
                if(!RebirthHydrationTransferJournal.PreparedRejection.TryPublish(proposal.Lease,out var rejection)||
                    !RebirthHydrationServerSourceHold.TryReleasePreparedRejection(rejection))return false;
            }
            else if(RebirthHydrationServerSourceHold.HoldsPlayer(player))return false;
            if(!RebirthHydrationProposalServer.TryRetireRejected(player,operation,digest))return false;
            return RebirthHydrationRejectedServer.TrySend(world,playerId,peer,operation,digest);
        }
        catch{return false;}
    }
}
