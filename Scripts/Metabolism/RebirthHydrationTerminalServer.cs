using System;

internal static class RebirthHydrationTerminalServer
{
    internal static bool TrySend(RebirthHydrationRemoteGrant grant)
    {
        try
        {
            if(grant==null||!grant.Current()||!RebirthHydrationRemoteCompletionServer.TryGetReady(grant,out var terminal))return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var player=grant.Lease.Player;var world=player.world;
            var peer=manager?.Clients?.ForEntityId(player.entityId);
            if(manager?.IsServer!=true||peer==null||!peer.loginDone||peer.disconnecting)return false;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationTerminal));
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationTerminal>().Setup(terminal);
            if(!grant.Current()||!terminal.CurrentFor(grant.Lease)||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(peer,manager.Clients?.ForEntityId(player.entityId)))return false;
            manager.SendPackage(packet,_attachedToEntityId:player.entityId);
            // Retain source/grant/outbox until independent owner acknowledgment.
            return grant.Current();
        }
        catch{return false;}
    }
}
