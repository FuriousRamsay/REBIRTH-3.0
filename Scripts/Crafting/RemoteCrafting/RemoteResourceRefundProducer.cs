using System;
using System.IO;

// Preparation only, deliberately not called by live packets until the saved-unused
// verifier and exact native owner credit/save protocol are implemented.
internal static class RemoteResourceRefundProducer
{
    internal static bool TryPrepare(EntityPlayer player, ClientInfo sender, string creation,
        ulong epoch, ulong request, RemoteResourceClientOperation operation,
        Func<RemoteResourceRefundRecord, bool> verifySavedUnused,
        out RemoteResourceRefundRecord refund, out bool pending)
    {
        refund = null; pending = false;
        if (verifySavedUnused == null) return false;
        try
        {
            if (!RebirthRemoteGearInventorySource.TryResolve(player, sender, creation, out var record) ||
                !RebirthWorldCharacterService.TryGetIdentity(player, out var identity) || identity == null ||
                sender.InternalId == null || identity.CanonicalId != (sender.InternalId.CombinedString ?? string.Empty).Trim() ||
                identity.StorageKey != record.StablePlayerKey || identity.CanonicalId != record.StablePlayerId ||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)) return false;
            World world = player.world;
            string rawRoot = GameIO.GetSaveGameDir();
            if (string.IsNullOrEmpty(rawRoot)) return false;
            string saveRoot = Path.GetFullPath(rawRoot);
            var nativeUser = sender.InternalId;
            if (!RemoteResourceTransactionOutcomeJournal.TryGetRetained(world, nativeUser, epoch, request, operation,
                out var removal, out var handle)) return false;
            Func<bool> current = () =>
            {
                if (!RebirthRemoteGearInventorySource.TryResolve(player, sender, creation, out var live) ||
                    !ReferenceEquals(live, record) || !ReferenceEquals(player.world, world) ||
                    !ReferenceEquals(sender.InternalId, nativeUser) ||
                    !RebirthWorldCharacterService.TryGetIdentity(player, out var liveIdentity) || liveIdentity == null ||
                    liveIdentity.StorageKey != identity.StorageKey || liveIdentity.CanonicalId != identity.CanonicalId ||
                    !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)) return false;
                string liveRoot = GameIO.GetSaveGameDir();
                return !string.IsNullOrEmpty(liveRoot) && Path.GetFullPath(liveRoot) == saveRoot &&
                    RemoteResourceTransactionOutcomeJournal.MatchesRetained(world, nativeUser, epoch, request, operation, handle);
            };
            // No caller-provided stacks or quantities: full unused grants only.
            if (!current() || !RemoteResourceWorldIdentity.TryGet(world, out var worldKey) || !current() ||
                !RemoteResourceRefundRecord.TryCreate(worldKey, identity.StorageKey, record.Origin.CreationId,
                    epoch, request, operation, removal.Removed, out var candidate) ||
                !verifySavedUnused(candidate) || !current()) return false;
            if (!RemoteResourceRefundSaveCheckpoint.TryPersist(record, identity, worldKey, candidate, current, out var retainedPending) ||
                !current() || !RemoteResourceWorldIdentity.TryGet(world, out var finalWorldKey) || finalWorldKey != worldKey ||
                !current()) return false;
            refund = candidate; pending = retainedPending;
            return true;
        }
        catch { refund = null; pending = false; return false; }
    }
}