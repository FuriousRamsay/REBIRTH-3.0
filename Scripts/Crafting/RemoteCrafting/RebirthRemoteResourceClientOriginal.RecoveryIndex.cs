using System;
using System.Collections.Generic;
using System.IO;

internal static partial class RebirthRemoteResourceClientOriginal
{
    private const long MaximumIndexedOriginalBytes = 8 * 1024 * 1024;
    private sealed class RecoveryIndex
    {
        internal EntityPlayerLocal Player;
        internal World World;
        internal ConnectionManager Manager;
        internal INetConnection Peer;
        internal string NativeWorld, Save, Owner, Creation;
        internal int Deaths;
        internal IReadOnlyList<Intent> Originals;

        internal bool Matches(EntityPlayerLocal player, World world, ConnectionManager manager, INetConnection peer,
            string nativeWorld, string save, string owner, string creation) =>
            ReferenceEquals(Player, player) && ReferenceEquals(World, world) && ReferenceEquals(Manager, manager) &&
            ReferenceEquals(Peer, peer) && Deaths == player.Died && NativeWorld == nativeWorld &&
            Save == save && Owner == owner && Creation == creation;
    }

    private static RecoveryIndex pendingRecovery;
    internal static void ClearPendingRecovery() => pendingRecovery = null;

    // One read for periodic per-kind recovery. Fresh admission and native
    // action proofs continue to use the journal, never this index.
    internal static bool TryReadPendingForRecovery(EntityPlayerLocal player, out IReadOnlyList<Intent> originals)
    {
        originals = null;
        var world = player?.world;
        var manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || manager == null ||
            !ReferenceEquals(GameManager.Instance?.World, world) || !ReferenceEquals(world.GetPrimaryPlayer(), player) ||
            !ReferenceEquals(world.GetEntity(player.entityId), player) || player.IsDead() || !player.IsSpawned())
            return false;

        try
        {
            bool remote = world.IsRemote();
            INetConnection peer = remote && manager.connectionToServer?.Length > 0 ? manager.connectionToServer[0] : null;
            string nativeWorld = RebirthRemoteResourceNetworkScope.ProtocolWorld(world);
            string creation = RebirthCamperCharacterAccess.CurrentCreation(player);
            if (remote && (peer == null || peer.IsDisconnected()) || string.IsNullOrEmpty(nativeWorld) ||
                string.IsNullOrEmpty(creation) || !RebirthStablePlayerIdentity.TryResolveServerEntity(player, out var identity))
                return false;
            string save = Path.GetFullPath(GameIO.GetSaveGameDir());

            if (pendingRecovery?.Matches(player, world, manager, peer, nativeWorld, save, identity.CanonicalId, creation) == true)
            {
                originals = pendingRecovery.Originals;
                return true;
            }

            ClearPendingRecovery();
            if (!ReadPending(out var rows, out long bytes)) return false;
            originals = Array.AsReadOnly(rows);
            RebirthDebug.Log("Crafting", $"Remote resource recovery catalogue read ({rows.Length} pending).");
            if (bytes <= MaximumIndexedOriginalBytes)
                pendingRecovery = new RecoveryIndex
                {
                    Player = player, World = world, Manager = manager, Peer = peer, Deaths = player.Died,
                    NativeWorld = nativeWorld, Save = save, Owner = identity.CanonicalId, Creation = creation,
                    Originals = originals
                };
            return true;
        }
        catch
        {
            ClearPendingRecovery();
            originals = null;
            return false;
        }
    }
}
