using System;

internal static class RebirthHydrationPreparedServer
{
    internal static bool Receive(World world,int playerId,ClientInfo peer,Guid operation,string digest)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
                manager?.IsServer!=true||peer==null||!peer.loginDone||peer.disconnecting||!ReferenceEquals(manager.Clients?.ForEntityId(playerId),peer))return false;
            var player=world.GetEntity(playerId) as EntityPlayer;
            if(!RebirthHydrationProposalServer.TryGet(player,peer,operation,digest,out var proposal))return false;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationGrant));
            if(!proposal.Current()||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !ReferenceEquals(manager.Clients?.ForEntityId(playerId),peer)||!RebirthHydrationRemoteGrant.TryIssue(proposal.Lease,out var grant))return false;
            return RebirthHydrationGrantServer.TrySend(grant);
        }
        catch{return false;}
    }
}
