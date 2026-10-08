using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Typed integration points for player inventory queries and consumption. Server/listen
/// players use authoritative source snapshots and transactions; dedicated clients receive
/// replicated availability and use a server preflight grant before queue-style mutations.
/// Remote storage is never mutated authoritatively by the client.
/// </summary>
public static class RemoteResourceConsumerPatchInstaller
{
    private static bool installed;
    private static readonly Dictionary<XUiC_BagStorageWindowGroup, MobileOpenSnapshot> mobileSnapshots =
        new Dictionary<XUiC_BagStorageWindowGroup, MobileOpenSnapshot>();

    private sealed class MobileOpenSnapshot
    {
        public int EntityId;
        public Dictionary<int, int> Counts;
    }

    public static void Install()
    {
        if (installed) return;
        installed = true;
        Harmony harmony = new Harmony("rebirth.remote.resource.consumers.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceHasItemsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceRemoveItemsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceGetItemCountPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceGetAllItemStacksPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceRecipeListAvailabilityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceMobileStorageOpenPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceMobileStorageClosePatch));
    }

    internal static void AfterHasItems(XUiM_PlayerInventory __instance, IList<ItemStack> _itemStacks, int _multiplier, ref bool __result)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __result || __instance == null || __instance.localPlayer == null) return;
        if (IsAuthoritativeServer())
        {
            __result = RemoteResourceTransactions.HasItems(__instance.localPlayer, _itemStacks, _multiplier, true);
            return;
        }
        EntityPlayerLocal local = __instance.localPlayer as EntityPlayerLocal;
        if (local != null)
        {
            __result = RemoteResourceClientGrantContext.Active
                ? RemoteResourceClientGrantContext.HasItems(local, _itemStacks, _multiplier)
                : RemoteResourceClientAvailability.HasItems(local, _itemStacks, _multiplier);
        }
    }

    internal static bool BeforeRemoveItems(
        XUiM_PlayerInventory __instance,
        IList<ItemStack> _itemStacks,
        int _multiplier,
        IList<ItemStack> _removedItems)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __instance == null || __instance.localPlayer == null || _itemStacks == null)
            return true;

        EntityPlayerLocal dedicatedLocal = __instance.localPlayer as EntityPlayerLocal;
        if (!IsAuthoritativeServer())
        {
            if (dedicatedLocal != null && RemoteResourceClientGrantContext.Active)
            {
                bool consumed = RemoteResourceClientGrantContext.ConsumeLocalRemainder(
                    dedicatedLocal, _itemStacks, _multiplier, _removedItems);
                if (!consumed)
                    RemoteResourceDiagnostics.Write("client grant local remainder failed");
                return false;
            }
            return true;
        }

        if (RemoteResourceTransactions.HasLocalItems(__instance.localPlayer, _itemStacks, _multiplier))
            return true;

        // Mutation paths never trust the availability snapshot. Build and validate one
        // complete authoritative transaction plan immediately before removing anything.
        RemoteResourceTransactions.ConsumptionResult result = RemoteResourceTransactions.TryConsume(
            __instance.localPlayer, _itemStacks, _multiplier, _removedItems, true);
        if (!result.Success)
        {
            RemoteResourceDiagnostics.Write("consumer transaction aborted: " + result.FailureReason);
            RemoteResourceSnapshotCache.InvalidatePlayer(__instance.localPlayer.entityId);
            __instance.dispatchBackpackItemsChanged();
            __instance.dispatchToolbeltItemsChanged();
            return false;
        }
        __instance.dispatchBackpackItemsChanged();
        __instance.dispatchToolbeltItemsChanged();
        RemoteResourceDiagnostics.Write(
            "consumer transaction local=" + result.LocalItemsConsumed +
            " remote=" + result.RemoteItemsConsumed +
            " sources=" + result.SourcesChanged);
        return false;
    }

    internal static void AfterGetItemCount(XUiM_PlayerInventory __instance, ItemValue _itemValue, ref int __result)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __instance == null || __instance.localPlayer == null || _itemValue == null) return;
        if (IsAuthoritativeServer())
        {
            __result += RemoteResourceTransactions.CountRemote(__instance.localPlayer, _itemValue);
            return;
        }
        EntityPlayerLocal local = __instance.localPlayer as EntityPlayerLocal;
        if (local != null)
        {
            // During a server preflight grant, the already-consumed remote amount is the
            // only remote inventory the resumed vanilla action may see. Do not combine it
            // with the asynchronously replicated availability snapshot.
            __result += RemoteResourceClientGrantContext.Active
                ? RemoteResourceClientGrantContext.GetCount(_itemValue)
                : RemoteResourceClientAvailability.GetCount(local, _itemValue);
        }
    }

    internal static void AfterGetAllItemStacks(XUiM_PlayerInventory __instance, ref List<ItemStack> __result)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __instance == null || __instance.localPlayer == null) return;
        if (__result == null) __result = new List<ItemStack>();
        if (IsAuthoritativeServer())
        {
            RemoteResourceSnapshotCache.Get(__instance.localPlayer).AppendClonedStacks(__result);
            return;
        }
        EntityPlayerLocal local = __instance.localPlayer as EntityPlayerLocal;
        if (local != null)
        {
            if (RemoteResourceClientGrantContext.Active)
                RemoteResourceClientGrantContext.AppendStacks(__result);
            else
                RemoteResourceClientAvailability.AppendStacks(local, __result);
        }
    }

    /// <summary>
    /// XUiC_RecipeList does not use XUiM_PlayerInventory.GetAllItemStacks when it decides
    /// whether a recipe row is craftable. Vanilla builds that list from backpack/toolbelt
    /// directly (or workstation input slots), so Remote Resources can make the selected
    /// recipe craftable while the recipe-list row remains gray. Re-evaluate only rows that
    /// vanilla marked unavailable using the same local + replicated/authoritative remote
    /// inventory projection already used by the craft action. This runs once per recipe-list
    /// rebuild, not per frame, and the remote projection comes from the event-invalidated
    /// short-lived snapshot cache.
    /// </summary>
    internal static void AfterBuildRecipeInfosList(XUiC_RecipeList __instance)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __instance == null || __instance.xui == null ||
            __instance.xui.PlayerInventory == null || __instance.xui.playerUI == null ||
            __instance.recipeInfos == null || __instance.recipeInfos.Count == 0) return;

        EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
        if (player == null) return;

        // GetAllItemStacks is already patched by RemoteResourceGetAllItemStacksPatch, so this
        // is the exact player-local + eligible-remote projection used by ItemActionEntryCraft.
        // At a workstation, vanilla BuildRecipeInfosList tests workstation input separately;
        // adding this second player/remote test mirrors ItemActionEntryCraft.hasItems, which ORs
        // workstation input with PlayerInventory.HasItems rather than combining the two pools.
        List<ItemStack> playerAndRemote = __instance.xui.PlayerInventory.GetAllItemStacks();
        if (playerAndRemote == null || playerAndRemote.Count == 0) return;

        int promoted = 0;
        for (int i = 0; i < __instance.recipeInfos.Count; i++)
        {
            XUiC_RecipeList.RecipeInfo info = __instance.recipeInfos[i];
            if (info.recipe == null || info.hasIngredients) continue;

            // Preserve every non-resource crafting gate used by vanilla BuildRecipeInfosList.
            if (__instance.craftingWindow != null && !__instance.craftingWindow.CraftingRequirementsValid(info.recipe))
                continue;

            if (!XUiM_Recipes.HasIngredientsForRecipe(playerAndRemote, info.recipe, player))
                continue;

            info.hasIngredients = true;
            __instance.recipeInfos[i] = info;
            promoted++;
        }

        if (promoted > 0)
            RemoteResourceDiagnostics.Write("recipe list remote availability promoted=" + promoted);
    }

    internal static void AfterMobileStorageOpen(XUiC_BagStorageWindowGroup __instance)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || __instance == null || __instance.Bag == null ||
            !(__instance.Entity is EntityVehicle) && !(__instance.Entity is EntityDrone)) return;
        mobileSnapshots[__instance] = new MobileOpenSnapshot
        {
            EntityId = __instance.Entity.entityId,
            Counts = CaptureCounts(__instance.Bag.ItemGrid.items)
        };
    }

    internal static void BeforeMobileStorageClose(XUiC_BagStorageWindowGroup __instance)
    {
        MobileOpenSnapshot snapshot;
        if (!RemoteResourcesRuntimePolicy.Enabled || __instance == null || !mobileSnapshots.TryGetValue(__instance, out snapshot)) return;
        mobileSnapshots.Remove(__instance);
        ItemStack[] finalSlots = __instance.Bag != null ? __instance.Bag.ItemGrid.items : null;
        if (!HasAnyIncrease(snapshot.Counts, finalSlots)) return;
        RequestMobileActivation(snapshot.EntityId);
    }

    private static void RequestMobileActivation(int entityId)
    {
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (player == null || persistent == null || persistent.PrimaryId == null) return;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection.IsServer)
            ProcessMobileActivation(GameManager.Instance.World, player.entityId, persistent.PrimaryId, entityId);
        else
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRemoteResourceMobileActivate>()
                .Setup(player.entityId, persistent, entityId));
    }

    public static void ProcessMobileActivation(
        World world, int playerId, PlatformUserIdentifierAbs userId, int sourceEntityId)
    {
        EntityPlayer player = world != null ? world.GetEntity(playerId) as EntityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId)
            : null;
        Entity entity = world != null ? world.GetEntity(sourceEntityId) : null;
        if (player == null || persistent == null || persistent.PrimaryId == null ||
            userId == null || !persistent.PrimaryId.Equals(userId) || entity == null ||
            (entity.position - player.position).sqrMagnitude > RemoteResourcesRuntimePolicy.Radius * RemoteResourcesRuntimePolicy.Radius)
            return;

        IRemoteResourceSource source = entity is EntityVehicle
            ? (IRemoteResourceSource)new VehicleResourceSource((EntityVehicle)entity)
            : entity is EntityDrone ? new DroneResourceSource((EntityDrone)entity) : null;
        string reason;
        if (source == null || !source.IsLoaded || !source.IsAuthorized(player, false, out reason) ||
            !RemoteResourceAccess.HasUsableItem(source.Slots)) return;
        RemoteResourceStateStore.Activate(source.StableId);
        RemoteResourceLiveSync.NotifySourceChanged(source.StableId, source.Position, "mobile storage activated");
    }

    private static bool IsAuthoritativeServer()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return connection != null && connection.IsServer;
    }

    private static Dictionary<int, int> CaptureCounts(ItemStack[] slots)
    {
        Dictionary<int, int> result = new Dictionary<int, int>();
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            int current;
            result.TryGetValue(stack.itemValue.type, out current);
            result[stack.itemValue.type] = current + stack.count;
        }
        return result;
    }

    private static bool HasAnyIncrease(Dictionary<int, int> before, ItemStack[] afterSlots)
    {
        Dictionary<int, int> after = CaptureCounts(afterSlots);
        foreach (KeyValuePair<int, int> pair in after)
        {
            int oldCount;
            if (!before.TryGetValue(pair.Key, out oldCount) || pair.Value > oldCount) return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.HasItems))]
internal static class RemoteResourceHasItemsPatch
{
    private static void Postfix(XUiM_PlayerInventory __instance, IList<ItemStack> _itemStacks, int _multiplier, ref bool __result)
    {
        RemoteResourceConsumerPatchInstaller.AfterHasItems(__instance, _itemStacks, _multiplier, ref __result);
    }
}

[HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.RemoveItems))]
internal static class RemoteResourceRemoveItemsPatch
{
    private static bool Prefix(XUiM_PlayerInventory __instance, IList<ItemStack> _itemStacks, int _multiplier, IList<ItemStack> _removedItems)
    {
        return RemoteResourceConsumerPatchInstaller.BeforeRemoveItems(__instance, _itemStacks, _multiplier, _removedItems);
    }
}

[HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetItemCount), new[] { typeof(ItemValue) })]
internal static class RemoteResourceGetItemCountPatch
{
    private static void Postfix(XUiM_PlayerInventory __instance, ItemValue _itemValue, ref int __result)
    {
        RemoteResourceConsumerPatchInstaller.AfterGetItemCount(__instance, _itemValue, ref __result);
    }
}

[HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetAllItemStacks))]
internal static class RemoteResourceGetAllItemStacksPatch
{
    private static void Postfix(XUiM_PlayerInventory __instance, ref List<ItemStack> __result)
    {
        RemoteResourceConsumerPatchInstaller.AfterGetAllItemStacks(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(XUiC_RecipeList), nameof(XUiC_RecipeList.BuildRecipeInfosList))]
internal static class RemoteResourceRecipeListAvailabilityPatch
{
    private static void Postfix(XUiC_RecipeList __instance)
    {
        RemoteResourceConsumerPatchInstaller.AfterBuildRecipeInfosList(__instance);
    }
}

[HarmonyPatch(typeof(XUiC_BagStorageWindowGroup), nameof(XUiC_BagStorageWindowGroup.OnOpen))]
internal static class RemoteResourceMobileStorageOpenPatch
{
    private static void Postfix(XUiC_BagStorageWindowGroup __instance)
    {
        RemoteResourceConsumerPatchInstaller.AfterMobileStorageOpen(__instance);
    }
}

[HarmonyPatch(typeof(XUiC_BagStorageWindowGroup), nameof(XUiC_BagStorageWindowGroup.OnClose))]
internal static class RemoteResourceMobileStorageClosePatch
{
    private static void Prefix(XUiC_BagStorageWindowGroup __instance)
    {
        RemoteResourceConsumerPatchInstaller.BeforeMobileStorageClose(__instance);
    }
}

[Preserve]
public sealed class NetPackageRemoteResourceMobileActivate : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private int sourceEntityId;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRemoteResourceMobileActivate Setup(int id, PersistentPlayerData persistent, int sourceId)
    {
        playerId = id;
        userId = persistent.PrimaryId;
        sourceEntityId = sourceId;
        return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        sourceEntityId = binary.ReadInt32();
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId);
        userId.ToStream(binary);
        binary.Write(sourceEntityId);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || world == null || world.IsRemote() || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;
        RemoteResourceConsumerPatchInstaller.ProcessMobileActivation(world, playerId, userId, sourceEntityId);
    }
    public int GetLength() { return 36; }
}
