using System;

// Source admission for remote gear custody. Does not equip, debit or save anything;
// the caller must persist a conserved plan and obtain the owner's durable receipt.
public static class RebirthRemoteGearInventorySource
{
    public static bool TryCapture(EntityPlayer player, ClientInfo sender, string creationId,
        out RebirthGearInventorySnapshot snapshot)
    {
        snapshot = null;
        if (!TryResolve(player, sender, creationId, out var record)) return false;

        // latestPlayerData is native per-connection custody evidence. EntityPlayer.bag
        // can be absent or stale for a client; never substitute that mirror on failure.
        var uploaded = sender.latestPlayerData;
        ItemStack[] bag = RebirthPlayerDataInventory.ReadSlots(uploaded, true);
        ItemStack[] belt = RebirthPlayerDataInventory.ReadSlots(uploaded, false);
        if (bag == null || belt == null ||
            bag.Length != RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(record)) return false;
        int owned = RebirthToolbeltCapacity.GetOwnedSlotCount(player, belt.Length);
        if (!RebirthGearInventorySnapshot.TryCapture(bag, belt, owned, out var captured) ||
            !ReferenceEquals(sender.latestPlayerData, uploaded) ||
            !TryResolve(player, sender, creationId, out var current) || !ReferenceEquals(current, record)) return false;
        snapshot = captured;
        return true;
    }
    // Authentication is independent of a newly uploaded inventory. A saved offer
    // must remain replayable after its owner has already applied its postimage.
    internal static bool TryResolve(EntityPlayer player, ClientInfo sender, string creationId,
        out RebirthWorldCharacterRecord record)
    {
        record = null;
        World world = GameManager.Instance?.World;
        if (player == null || sender?.InternalId == null || sender.entityId != player.entityId ||
            world == null || world.IsRemote() || !ReferenceEquals(world, player.world) ||
            !ReferenceEquals(world.GetEntity(player.entityId), player) || !player.isEntityRemote ||
            player.IsDead() || !RebirthWorldCharacterRepository.IsServerAuthority ||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld() ||
            RebirthCharacterCreationHoldService.IsHeld(player)) return false;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record?.Support == null ||
            !record.IsComplete || record.Origin == null ||
            !RebirthSurvivorRequestScope.Matches(creationId, record.Origin.CreationId)) return false;

        return true;
    }
}