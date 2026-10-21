using System;

internal static class RebirthHydrationGrantServer
{
    internal static bool TrySend(RebirthHydrationRemoteGrant grant)
    {
        try
        {
            if(grant==null||!grant.Current())return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var player=grant.Lease.Player;var world=player.world;
            var peer=manager?.Clients?.ForEntityId(player.entityId);
            if(manager?.IsServer!=true||peer==null||!peer.loginDone||peer.disconnecting)return false;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationGrant));
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationGrant>().Setup(grant);
            if(!grant.Current()||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(peer,manager.Clients?.ForEntityId(player.entityId)))return false;
            manager.SendPackage(packet,_attachedToEntityId:player.entityId);
            // A successful send call is not a financial acknowledgment.
            return grant.Current();
        }
        catch{return false;}
    }
}
