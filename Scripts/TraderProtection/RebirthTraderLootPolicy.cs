using System;
using System.Collections.Generic;
using Audio;
using HarmonyLib;

#nullable disable

public static class RebirthTraderLootPolicy
{
    public static bool Enabled = true;
    private const string SearchCommand = "TEFeatureStorage:Search";

    private static readonly HashSet<string> RestoredTraderContainerBlocks =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "traderShelvesWoodFullA",
            "traderShelvesFreeStandingFullA",
            "traderShelvesMetalFullA",
            "traderMilitaryMetalShelvesFull",
            "traderMilitaryGoodsA",
            "traderMilitaryGoodsB",
            "traderMilitaryGoodsC",
            "traderMilitaryGoodsD",
            "traderClothesRackRoundPantsMilitary",
            "traderClothesRackRoundShirtsMilitary",
            "traderClothesRackRectanglePantsMilitary",
            "traderClothesRackRectangleShirtsMilitary",
            "traderClothesRackWallPantsMilitary",
            "traderClothesRackWallLongSleeveMilitary",
            "traderClothesRackWallSweaterMilitary",
            "traderClothesRackWallTShirtsMilitary",
            "traderClothesShelfStackShirts01",
            "traderClothesShelfStackShirts02",
            "traderClothesShelfHalfShirts01",
            "traderClothesShelfHalfShirts02",
            "traderCashRegister"
        };

    public static void EnsureRestoredTraderTileEntity(
        BlockCompositeTileEntity block,
        WorldBase worldBase,
        BlockValue blockValue,
        Vector3i blockPos)
    {
        World world = worldBase as World;
        if (block == null || world == null || blockValue.ischild ||
            !RestoredTraderContainerBlocks.Contains(block.GetBlockName()) ||
            world.GetTileEntity(blockPos) is TileEntityComposite)
            return;

        Chunk chunk = world.GetChunkFromWorldPos(blockPos) as Chunk;
        if (chunk == null)
            return;

        TileEntityComposite tileEntity = new TileEntityComposite(chunk, blockValue);
        tileEntity.localChunkPos = World.toBlock(blockPos);
        tileEntity.OnBlockAdded(blockPos, blockValue, null);
        chunk.AddTileEntity(tileEntity);
    }

    public static bool TryGetProtectedTraderStorage(
        BlockCompositeTileEntity block,
        WorldBase worldBase,
        BlockValue blockValue,
        Vector3i blockPos,
        out TileEntityComposite tileEntity)
    {
        tileEntity = null;
        World world = worldBase as World;
        if (block == null || world == null)
            return false;

        if (block.isMultiBlock && blockValue.ischild && block.multiBlockPos != null)
        {
            blockPos = block.multiBlockPos.GetParentPos(blockPos, blockValue);
            blockValue = world.GetBlock(blockPos);
            if (blockValue.ischild)
                return false;
        }

        if (world.GetTraderAreaAt(blockPos) == null)
            return false;

        EnsureRestoredTraderTileEntity(block, worldBase, blockValue, blockPos);
        tileEntity = world.GetTileEntity(blockPos) as TileEntityComposite;
        if (tileEntity == null)
            return false;

        TEFeatureStorage storage = tileEntity.GetFeature<TEFeatureStorage>();
        return storage?.ItemGrid != null && !storage.ItemGrid.PlayerOwned;
    }

    public static bool ApplySearchCommandPolicy(BlockActivationCommand[] commands)
    {
        if (commands == null)
            return false;

        bool anyEnabled = false;
        for (int i = 0; i < commands.Length; i++)
        {
            if (string.Equals(commands[i].text, SearchCommand, StringComparison.OrdinalIgnoreCase))
                commands[i].enabled = Enabled;

            anyEnabled |= commands[i].enabled;
        }
        return anyEnabled;
    }

    public static bool IsSearchCommand(string commandName)
    {
        return string.Equals(commandName, SearchCommand, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(commandName, "Search", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProtectedTraderStorage(TileEntityComposite tileEntity, WorldBase worldBase, Vector3i blockPos)
    {
        World world = worldBase as World;
        if (tileEntity == null || world == null || world.GetTraderAreaAt(blockPos) == null)
            return false;

        TEFeatureStorage storage = tileEntity.GetFeature<TEFeatureStorage>();
        return storage?.ItemGrid != null && !storage.ItemGrid.PlayerOwned;
    }
}



/// <summary>
/// 3.1 resolves module command permission inside TileEntityComposite before the outer
/// BlockCompositeTileEntity returns its command array. Force the storage Search command
/// here for trader-area non-player storage so another feature cannot silently disable it.
/// </summary>
[HarmonyPatch(typeof(TileEntityComposite), nameof(TileEntityComposite.UpdateBlockActivationCommands))]
internal static class RebirthTraderCompositeCommandPolicyPatch
{
    private static void Postfix(
        TileEntityComposite __instance,
        BlockActivationCommand[] _commands,
        WorldBase _world,
        Vector3i _blockPos,
        ref bool __result)
    {
        if (!RebirthTraderLootPolicy.IsProtectedTraderStorage(__instance, _world, _blockPos))
            return;

        __result = RebirthTraderLootPolicy.ApplySearchCommandPolicy(_commands);
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.OnBlockLoaded))]
internal static class RebirthTraderRestoredStorageLoadPatch
{
    private static void Postfix(
        BlockCompositeTileEntity __instance,
        WorldBase _world,
        Vector3i _blockPos,
        BlockValue _blockValue)
    {
        RebirthTraderLootPolicy.EnsureRestoredTraderTileEntity(__instance, _world, _blockValue, _blockPos);
    }
}

/// <summary>
/// 2.6 enforced trader-loot policy at the block interaction layer. 3.1 routes
/// storage through BlockCompositeTileEntity, so patch that same player-facing layer.
/// This also covers the stocked trader-only props restored as composite storage by XML.
/// </summary>
[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.HasBlockActivationCommands))]
internal static class RebirthTraderBlockHasCommandsPatch
{
    private static void Postfix(
        BlockCompositeTileEntity __instance,
        WorldBase _world,
        BlockValue _blockValue,
        Vector3i _blockPos,
        ref bool __result)
    {
        TileEntityComposite tileEntity;
        if (!RebirthTraderLootPolicy.TryGetProtectedTraderStorage(__instance, _world, _blockValue, _blockPos, out tileEntity))
            return;

        __result = RebirthTraderLootPolicy.ApplySearchCommandPolicy(__instance.commands);
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.GetBlockActivationCommands))]
internal static class RebirthTraderBlockGetCommandsPatch
{
    private static void Postfix(
        BlockCompositeTileEntity __instance,
        WorldBase _world,
        BlockValue _blockValue,
        Vector3i _blockPos,
        ref BlockActivationCommand[] __result)
    {
        TileEntityComposite tileEntity;
        if (!RebirthTraderLootPolicy.TryGetProtectedTraderStorage(__instance, _world, _blockValue, _blockPos, out tileEntity))
            return;

        RebirthTraderLootPolicy.ApplySearchCommandPolicy(__result);
    }
}

[HarmonyPatch(typeof(BlockCompositeTileEntity), nameof(BlockCompositeTileEntity.OnBlockActivated))]
internal static class RebirthTraderBlockActivationPatch
{
    private static bool Prefix(
        BlockCompositeTileEntity __instance,
        string _commandName,
        WorldBase _world,
        Vector3i _blockPos,
        BlockValue _blockValue,
        EntityPlayerLocal _player,
        ref bool __result)
    {
        if (!RebirthTraderLootPolicy.IsSearchCommand(_commandName))
            return true;

        TileEntityComposite tileEntity;
        if (!RebirthTraderLootPolicy.TryGetProtectedTraderStorage(__instance, _world, _blockValue, _blockPos, out tileEntity))
            return true;

        if (RebirthTraderLootPolicy.Enabled)
            return true;

        Manager.PlayInsidePlayerHead("ui_denied");
        if (_player != null)
            GameManager.ShowTooltip(_player, Localization.Get("ttBelongsToTrader"), string.Empty, "ui_denied", null);

        __result = false;
        return false;
    }
}

public static class RebirthTraderLootPolicyInstaller
{
    private static bool installed;

    public static void Install()
    {
        if (installed)
            return;

        installed = true;
        Harmony harmony = new Harmony("rebirth.trader-loot-policy.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderCompositeCommandPolicyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderRestoredStorageLoadPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderBlockHasCommandsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderBlockGetCommandsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderBlockActivationPatch));
        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH] Trader loot policy installed at the 3.1 BlockCompositeTileEntity interaction layers."); }
    }
}
