using System;
using System.Linq;

// Native kill attribution supplies the player; no network payload or cached
// runtime entity ID is accepted as a durable reward identity.
internal static class RebirthPurgeKillContributor
{
    internal static string Capture(EntityAlive victim)
    {
        try
        {
            if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!ThreadManager.IsMainThread()||victim==null)return null;
            var world=victim.world as World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(world==null||world.IsRemote()||manager==null||!manager.IsServer||!ReferenceEquals(GameManager.Instance?.World,world))return null;
            return Identify(victim.entityThatKilledMe as EntityPlayer,world);
        }
        catch{return null;}
    }
    internal static string Identify(EntityPlayer player,World expectedWorld)
    {
        try
        {
            if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!ThreadManager.IsMainThread()||player==null)return null;
            var world=player.world as World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(world==null||!ReferenceEquals(world,expectedWorld)||world.IsRemote()||manager==null||!manager.IsServer||!ReferenceEquals(GameManager.Instance?.World,world))return null;
            Entity registered;
            if(!world.Entities.dict.TryGetValue(player.entityId,out registered)||!ReferenceEquals(registered,player))return null;
            RebirthStablePlayerIdentity identity;
            var client=manager.Clients.ForEntityId(player.entityId);
            if(client!=null)
            {
                if(!client.loginDone||client.entityId!=player.entityId||!RebirthStablePlayerIdentity.TryFromClientInfo(client,out identity))return null;
                return identity.StorageKey;
            }
            if(GameManager.IsDedicatedServer||!(player is EntityPlayerLocal)||!world.GetLocalPlayers().Contains(player)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity))return null;
            return identity.StorageKey;
        }
        catch{return null;}
    }
}