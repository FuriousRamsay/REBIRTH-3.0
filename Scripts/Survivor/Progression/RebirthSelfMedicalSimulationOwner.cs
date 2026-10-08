using System;

// Follows native !isEntityRemote simulation ownership. Replica receipt never grants execution.
internal static class RebirthSelfMedicalSimulationOwner
{
    internal static bool Current(EntityPlayer player)
    {
        World world = player?.world;
        GameManager game = GameManager.Instance;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (world == null || game == null || connection == null || player.isEntityRemote || player.IsDead()
            || !ReferenceEquals(game.World, world) || !ReferenceEquals(world.GetEntity(player.entityId), player)
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return false;

        if (!world.IsRemote())
            return connection.IsServer;

        if (!(player is EntityPlayerLocal) || connection.IsServer
            || !ReferenceEquals(world.GetPrimaryPlayer(), player)
            || connection.connectionToServer == null || connection.connectionToServer.Length == 0)
            return false;

        var peer = connection.connectionToServer[0];
        RebirthSurvivorOwnerScalars scalars;
        return peer != null && !peer.IsDisconnected()
            && RebirthSurvivorClientState.TryGetOwnerScalars(player, out scalars) && scalars != null
            && ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance, connection)
            && ReferenceEquals(GameManager.Instance, game) && ReferenceEquals(game.World, world)
            && ReferenceEquals(world.GetPrimaryPlayer(), player)
            && ReferenceEquals(world.GetEntity(player.entityId), player)
            && !player.isEntityRemote && !connection.IsServer
            && connection.connectionToServer != null && connection.connectionToServer.Length > 0
            && ReferenceEquals(connection.connectionToServer[0], peer) && !peer.IsDisconnected();
    }

    internal static void Set(EntityBuffs buffs, string name, float value, bool netSync = true)
    {
        // Reliable native SET packets deliver zero retirement without relying on unreliable deltas.
        // The normal authenticated full-player save owns persisted buff state; no save receipt is invented.
        if (buffs.parent.world.IsRemote())
        {
            buffs.SetCustomVar(name, value, _netSync: true, _operation: CVarOperation.set,
                _forceSendToClients: true);
            GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        }
        else
        {
            buffs.SetCustomVar(name, value, netSync);
        }
    }
}
