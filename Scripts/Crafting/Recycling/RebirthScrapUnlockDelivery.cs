using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 ScrapCompletion.OnLastOutput unlock delivery (owner 2026-11-08): when a scrap job ends, its owner gets the
/// scrapped item's cosmetic and recipe, with "Cosmetic Unlocked: name" / "Recipe Unlocked: name" toasts, wherever they
/// are. Single player and the host's own player are handled here; a client player is unlocked on the server and told
/// by package (their game applies it and shows the toasts); an owner who is offline gets it when they next spawn
/// (2.6 PendingOfflineUnlocks; kept for the session).
/// </summary>
public static class RebirthScrapUnlockDelivery
{
    private static readonly Dictionary<string, List<string>> Pending = new Dictionary<string, List<string>>(StringComparer.Ordinal);

    /// <summary>Server side. ownerIdentity/creation identify the character that queued the scrap job.</summary>
    internal static void Deliver(World world, string ownerIdentity, Func<EntityPlayer, bool> isOwner, string itemName)
    {
        if (world == null || world.IsRemote() || string.IsNullOrEmpty(itemName)) return;
        var player = world.Players?.list?.FirstOrDefault(p => p != null && p.IsSpawned() && isOwner(p));
        if (player == null)
        {
            if (string.IsNullOrEmpty(ownerIdentity)) return;
            lock (Pending)
            {
                if (!Pending.TryGetValue(ownerIdentity, out var list)) Pending[ownerIdentity] = list = new List<string>();
                if (!list.Contains(itemName)) list.Add(itemName);
            }
            return;
        }
        DeliverTo(player, itemName);
    }

    internal static void FlushFor(EntityPlayer player, string ownerIdentity)
    {
        if (player == null || string.IsNullOrEmpty(ownerIdentity)) return;
        List<string> items;
        lock (Pending) { if (!Pending.TryGetValue(ownerIdentity, out items)) return; Pending.Remove(ownerIdentity); }
        foreach (var item in items) DeliverTo(player, item);
    }

    private static void DeliverTo(EntityPlayer player, string itemName)
    {
        var item = ItemClass.GetItemClass(itemName);
        if (item == null) return;
        bool cosmetic = ApplyCosmetic(player, item);
        bool recipe = ApplyRecipe(player, itemName);
        // REBIRTH recipe knowledge is server-owned; granting it publishes the owner's state to their client.
        recipe |= GrantKnowledge(player, itemName);
        if (!cosmetic && !recipe) return;
        if (player is EntityPlayerLocal local) Toast(local, itemName, cosmetic, recipe);
        else SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(player.entityId)?
            .SendPackage(NetPackageManager.GetPackage<NetPackageRebirthScrapUnlock>().Setup(itemName, cosmetic, recipe));
    }

    internal static bool ApplyCosmetic(EntityPlayer player, ItemClass item)
    {
        if (player?.equipment == null || !Equipment.CosmeticMappingStringID.TryGetValue(item.GetItemName(), out int id)
            || player.equipment.m_unlockedCosmetics.Contains(id)) return false;
        player.equipment.UnlockCosmeticItem(item);
        return true;
    }

    internal static bool ApplyRecipe(EntityPlayer player, string itemName)
    {
        var recipe = CraftingManager.GetRecipe(itemName);
        if (recipe == null) return false;
        string name = recipe.GetName();
        if (player.GetCVar(name) > 0f || CraftingManager.UnlockedRecipeList.Contains(name) && player is EntityPlayerLocal) return false;
        CraftingManager.UnlockRecipe(name, player);
        return true;
    }

    private static bool GrantKnowledge(EntityPlayer player, string itemName)
    {
        var recipe = CraftingManager.GetRecipe(itemName);
        if (recipe == null) return false;
        string name = recipe.GetName(); bool granted = false;
        if (RebirthCraftingProgressionRegistry.TryGetRecipe(name, out var definition) && definition?.KnowledgeIds != null)
            foreach (var knowledge in definition.KnowledgeIds)
                if (!string.IsNullOrEmpty(knowledge) && RebirthKnowledgeService.Grant(player, knowledge, "scrap:" + itemName)) granted = true;
        if (RebirthProgressionRuntimeConfig.TryGetRecipeRule(name, out var rule) && !string.IsNullOrEmpty(rule?.KnowledgeId)
            && RebirthKnowledgeService.Grant(player, rule.KnowledgeId, "scrap:" + itemName)) granted = true;
        return granted;
    }

    internal static void Toast(EntityPlayerLocal player, string itemName, bool cosmetic, bool recipe)
    {
        string display = "[c5c791]" + (Localization.Exists(itemName) ? Localization.Get(itemName) : itemName) + "[-]";
        if (cosmetic) GameManager.ShowTooltip(player, string.Format(Localization.Get("xuiRebirthCosmeticUnlocked"), display), string.Empty, "ui_challenge_complete");
        if (recipe) GameManager.ShowTooltip(player, string.Format(Localization.Get("xuiRebirthScrapRecipeUnlocked"), display), string.Empty, "ui_challenge_complete");
    }
}

/// <summary>Server → owning client: apply a scrap unlock locally and show its toasts.</summary>
[Preserve]
public sealed class NetPackageRebirthScrapUnlock : NetPackage
{
    private string itemName; private bool cosmetic, recipe;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthScrapUnlock Setup(string item, bool unlockCosmetic, bool unlockRecipe)
    { itemName = item; cosmetic = unlockCosmetic; recipe = unlockRecipe; return this; }
    public override void read(PooledBinaryReader reader)
    {
        itemName = reader.ReadString(); cosmetic = reader.ReadBoolean(); recipe = reader.ReadBoolean();
        if (string.IsNullOrEmpty(itemName) || itemName.Length > 128) throw new InvalidDataException("Invalid scrap unlock");
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        ((BinaryWriter)writer).Write(itemName ?? string.Empty); ((BinaryWriter)writer).Write(cosmetic); ((BinaryWriter)writer).Write(recipe);
    }
    public int GetLength() => 8 + (itemName?.Length ?? 0) * 2;
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        var local = world?.GetPrimaryPlayer();
        var item = ItemClass.GetItemClass(itemName);
        if (local == null || item == null) return;
        if (cosmetic) RebirthScrapUnlockDelivery.ApplyCosmetic(local, item);
        if (recipe) RebirthScrapUnlockDelivery.ApplyRecipe(local, itemName);
        RebirthScrapUnlockDelivery.Toast(local, itemName, cosmetic, recipe);
    }
}

[Preserve]
public sealed class RebirthScrapUnlockDeliveryModApi : IModApi
{
    public void InitMod(Mod mod)
        => ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnSpawn));

    private static void OnSpawn(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote()) return;
        if (world.GetEntity(data.EntityId) is EntityPlayer player)
            RebirthScrapUnlockDelivery.FlushFor(player, RebirthRecyclerRuntime.IdentityOf(player));
    }
}
