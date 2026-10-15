using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

// On-demand read-only observation. Does not resolve/create metabolism state or consume.
internal static class RebirthGameBridgeConsumption
{
    internal static JObject Observe(EntityPlayerLocal player)
    {
        if (!ThreadManager.IsMainThread() || player?.world == null)
            throw new InvalidOperationException("Consumption observation requires a local player on the main thread.");
        var result = new JObject { ["schema"] = 1, ["ownerEntityId"] = player.entityId,
            ["remote"] = player.world.IsRemote(), ["utc"] = DateTime.UtcNow.ToString("o"),
            ["food"] = player.Stats == null ? JValue.CreateNull() : new JValue(player.Stats.Food.Value),
            ["water"] = player.Stats == null ? JValue.CreateNull() : new JValue(player.Stats.Water.Value),
            ["consumptionReceiptAvailable"] = false,
            ["consumptionReceiptReason"] = "Owner publication IDs are private; metabolism snapshot sequence is not a consumption receipt.",
            ["toolbelt"] = Slots(player.inventory?.ItemGrid.items),
            ["backpack"] = Slots(player.bag?.ItemGrid.items) };
        var cvars = new JObject();
        foreach (string name in new[] { "infectionCounter", "$infectionMaxDuration", "$infectionCureCounter",
            "$buffInfectionAddCurePerc", ".infectionDisplayPerc", ".infectionCureDisplayPerc",
            "$foodAmountAdd", "$waterAmountAdd" })
            cvars[name] = player.Buffs == null ? JValue.CreateNull() : new JValue(player.Buffs.GetCustomVar(name));
        result["cvars"] = cvars;
        var buffs = new JArray();
        if (player.Buffs != null)
            foreach (BuffValue buff in player.Buffs.ActiveBuffs)
                if (buff != null && !buff.Invalid && !buff.Remove) buffs.Add(buff.BuffName);
        result["buffs"] = buffs;
        RebirthMetabolismSnapshot snapshot;
        result["metabolism"] = RebirthMetabolismClientState.TryGet(out snapshot)
            ? new JObject { ["ownerEntityId"] = snapshot.OwnerEntityId, ["creationId"] = snapshot.CreationId,
                ["sequence"] = snapshot.Sequence, ["revision"] = snapshot.Revision, ["food"] = snapshot.Food,
                ["pendingFood"] = snapshot.PendingFood, ["projectedFood"] = snapshot.ProjectedFood,
                ["fullnessMl"] = snapshot.FullnessMl } : null;
        return result;
    }

    private static JArray Slots(ItemStack[] slots)
    {
        if (slots == null) return null;
        var result = new JArray();
        using (var hash = SHA256.Create())
            for (int i = 0; i < slots.Length; i++)
            {
                var stack = slots[i];
                bool empty = stack == null || stack.IsEmpty();
                result.Add(new JObject { ["slot"] = i, ["count"] = empty ? 0 : stack.count,
                    ["name"] = empty ? null : stack.itemValue.ItemClass.GetItemName(),
                    ["type"] = empty ? 0 : stack.itemValue.type, ["seed"] = empty ? 0 : stack.itemValue.Seed,
                    ["nativeHash"] = empty ? null : Convert.ToBase64String(hash.ComputeHash(
                        Encoding.UTF8.GetBytes(RebirthNativeItemCodec.Encode(stack.itemValue.Clone())))) });
            }
        return result;
    }
}
