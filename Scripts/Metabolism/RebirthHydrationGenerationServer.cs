using System;

internal static class RebirthHydrationGenerationServer
{
    internal static bool TrySend(EntityPlayer player)
    {
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=player?.world;
            if(!ThreadManager.IsMainThread()||player==null||player is EntityPlayerLocal||world==null||world.IsRemote()||
                !ReferenceEquals(world,GameManager.Instance?.World)||manager?.IsServer!=true||RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
            var peer=manager.Clients?.ForEntityId(player.entityId);if(peer==null||!peer.loginDone||peer.disconnecting)return false;
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationGeneration));
            if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(player.world,world)||
                !ReferenceEquals(world.GetEntity(player.entityId),player)||player.IsDead()||!player.IsSpawned()||
                !ReferenceEquals(peer,manager.Clients?.ForEntityId(player.entityId))||!peer.loginDone||peer.disconnecting)return false;
            var packet=NetPackageManager.GetPackage<NetPackageRebirthHydrationGeneration>().Setup(player);
            if(!packet.CurrentFor(player)||!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(peer,manager.Clients?.ForEntityId(player.entityId)))return false;
            manager.SendPackage(packet,_attachedToEntityId:player.entityId);
            return packet.CurrentFor(player)&&ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&
                ReferenceEquals(world,GameManager.Instance?.World)&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(peer,manager.Clients?.ForEntityId(player.entityId))&&peer.loginDone&&!peer.disconnecting;
        }
        catch{return false;}
    }
}
