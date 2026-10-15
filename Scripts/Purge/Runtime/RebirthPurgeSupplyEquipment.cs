using System;
using System.Collections.Generic;
using UnityEngine;

// Read native equipped gloves, not a client-supplied price or an inventory item.
internal static class RebirthPurgeSupplyEquipment
{
    private static World owner;
    private static double next;
    private static readonly Dictionary<string, EntityPlayer> players = new Dictionary<string, EntityPlayer>(StringComparer.Ordinal);

    internal static int Tier(EntityPlayer player)
    {
        var gloves = player?.equipment?.GetSlotItem((int)EquipmentSlots.Hands);
        var mods = gloves?.modifications;
        int tier = 0;
        if (gloves == null || gloves.IsEmpty() || mods == null) return tier;
        foreach (var mod in mods)
        {
            if (mod == null || mod.IsEmpty()) continue;
            string name = mod.ItemClass?.GetItemName();
            if (name != null && name.Length == 13 && name.StartsWith("ModPurgeTier", StringComparison.Ordinal))
            {
                int value = name[name.Length - 1] - '0';
                if (value >= 1 && value <= 5) tier = Math.Max(tier, value);
            }
        }
        return tier;
    }

    internal static bool TryTier(string identity, out int tier)
    {
        tier = 0;
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote()) return false;
        if (!ReferenceEquals(owner, world)) { owner = world; next = 0; players.Clear(); }
        if (Time.realtimeSinceStartup >= next)
        {
            next = Time.realtimeSinceStartup + 1;
            players.Clear();
            var tracked = world.aiDirector?.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
            if (tracked != null) foreach (var entry in tracked)
            {
                var player = entry.Player;
                if (player == null) continue;
                string key = RebirthPurgeKillContributor.Identify(player, world);
                if (key != null) players[key] = player;
            }
        }
        EntityPlayer online;
        if (identity == null || !players.TryGetValue(identity, out online) || !online.IsSpawned() ||
            !string.Equals(RebirthPurgeKillContributor.Identify(online,world),identity,StringComparison.Ordinal)) return false;
        tier = Tier(online);
        return true;
    }
}
