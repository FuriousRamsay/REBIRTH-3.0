using Platform;
using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public static class RebirthBlockPickupService
{
    public const string CommandName = "take";
    public const string PropertyPreserveAfterLoot = "RebirthPreserveAfterLoot";
    public const string PropertyInstantPickup = "RebirthInstantPickup";
    private const float MaximumPickupDistance = 6f;
    private static int lastLoggedFrame = -1;
    private static int lastLoggedEntityId = -1;
    private static Vector3i lastLoggedPosition = Vector3i.zero;
    private static PickupCommandCacheEntry[] commandCache;

    private sealed class PickupCommandCacheEntry
    {
        public BlockActivationCommand[] Source;
        public BlockActivationCommand[] TakeResult;
        public BlockActivationCommand[] RadialResult;
        public int TakeIndex;
        public int CancelIndex;
        public int RadialTakeIndex;
        public int RadialCancelIndex;
    }

    public static void ClearCommandCache()
    {
        commandCache = null;
    }

    public static void ClearRuntimeCaches()
    {
        ClearCommandCache();
        RebirthBlockPickupTargetResolver.ClearCache();
        lastLoggedFrame = -1;
        lastLoggedEntityId = -1;
        lastLoggedPosition = Vector3i.zero;
    }

    public static bool ShouldAddPickupCommand(Block block)
    {
        return ShouldUseRebirthPickup(block);
    }

    public static bool ShouldUseRebirthPickup(Block block)
    {
        if (block == null || !RebirthBlockPickupClassifier.Evaluate(block).Allowed)
            return false;

        // While Block Pickup is active, the XML-loading prefix converts the base
        // CanPickup property into REBIRTH metadata and removes the vanilla prompt.
        // Every eligible block therefore uses this one validated pickup path.
        return RebirthBlockPickupTargetResolver.Resolve(block).IsValid;
    }

    public static BlockActivationCommand[] AppendPickupCommandIfNeeded(
        Block block,
        BlockActivationCommand[] original)
    {
        if (!ShouldAddPickupCommand(block))
            return original ?? BlockActivationCommand.Empty;

        BlockActivationCommand[] source = original ?? BlockActivationCommand.Empty;
        PickupCommandCacheEntry[] local = commandCache;
        if (local == null || Block.list == null || local.Length != Block.list.Length)
        {
            local = new PickupCommandCacheEntry[Block.list != null ? Block.list.Length : 0];
            commandCache = local;
        }

        PickupCommandCacheEntry entry = block.blockID >= 0 && block.blockID < local.Length
            ? local[block.blockID]
            : null;
        if (entry == null || !ReferenceEquals(entry.Source, source))
        {
            entry = BuildCommandCacheEntry(source);
            if (block.blockID >= 0 && block.blockID < local.Length)
                local[block.blockID] = entry;
        }

        CopyAndConfigureTakeResult(entry);
        bool forceRadial = ShouldForceRadialMenu(block);
        int enabledCommandCount = CountEnabled(entry.TakeResult) + CountEnabled(block.CustomCmds);
        if (!forceRadial || enabledCommandCount >= 2)
            return entry.TakeResult;

        CopyAndConfigureRadialResult(entry);
        return entry.RadialResult;
    }

    public static void ConfigurePickupCommandAvailability(
        BlockActivationCommand[] commands,
        Block block,
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        EntityAlive focusingEntity)
    {
        if (commands == null || block == null || world == null ||
            !(focusingEntity is EntityPlayerLocal) ||
            !RebirthBlockPickupClassifier.IsStorageContainer(block))
            return;

        ResolveParentBlock(world, ref blockPos, ref blockValue);
        bool enabled = true;
        TileEntity tileEntity = world.GetTileEntity(blockPos);
        if (tileEntity == null || tileEntity.IsUserAccessing())
        {
            enabled = false;
        }
        else
        {
            TileEntityComposite composite = tileEntity as TileEntityComposite;
            if (IsLockedStorage(composite))
            {
                enabled = false;
            }
            else
            {
                TEFeatureStorage storage;
                if (!tileEntity.TryGetSelfOrFeature<TEFeatureStorage>(out storage) ||
                    storage == null || !storage.IsEmpty() ||
                    IsDestroyOnCloseStorage(storage) ||
                    (!storage.ItemGrid.Touched && (composite == null || !composite.PlayerPlaced)))
                {
                    enabled = false;
                }
                else if (composite != null && composite.Owner != null)
                {
                    EntityPlayer player = focusingEntity as EntityPlayer;
                    PersistentPlayerData persistent = GameManager.Instance != null
                        ? GameManager.Instance.GetPersistentLocalPlayer()
                        : null;
                    enabled = persistent != null && persistent.PrimaryId != null &&
                        RebirthBlockPickupAccessService.CanAccessOwnedComposite(
                            composite, persistent.PrimaryId, world, player);
                }
            }
        }

        for (int i = 0; i < commands.Length; i++)
        {
            if (string.Equals(commands[i].text, CommandName, StringComparison.OrdinalIgnoreCase))
                commands[i].enabled = enabled;
        }
    }

    public static bool ShouldForceRadialMenu(Block block)
    {
        if (block == null || RebirthBlockPickupClassifier.IsStorageContainer(block) ||
            block is BlockWorkstation)
            return true;

        // Inherited XML exception for lightweight objects such as rock01.
        // Storage and workstations always retain their validated radial flow.
        if (HasTrueProperty(block, PropertyInstantPickup))
            return false;

        // Only the three explicitly requested native-style pickup families keep a
        // direct press-E interaction.  Other recoverable blocks continue through
        // REBIRTH's normal radial/option rules even if vanilla originally exposed
        // CanPickup, so this compatibility path does not turn every pickup-capable
        // prop into an instant interaction.
        if (IsDirectPressPickupTarget(block))
            return false;

        switch (RebirthSandboxOptionManager.Current.InstantBlockPickup)
        {
            case RebirthInstantBlockPickupMode.Always:
                return false;
            case RebirthInstantBlockPickupMode.Default:
                return !block.SellableToTrader || GetEstimatedSellValue(block) < 15;
            default:
                return true;
        }
    }

    public static void ApplyPickupActivationText(
        Block block,
        WorldBase world,
        BlockValue blockValue,
        Vector3i blockPos,
        EntityAlive focusingEntity,
        ref string activationText)
    {
        if (!string.IsNullOrEmpty(activationText) || block == null || world == null ||
            !(focusingEntity is EntityPlayerLocal) || !ShouldUseRebirthPickup(block) ||
            !IsDirectPressPickupTarget(block))
            return;

        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        if (persistent == null || !world.CanPickupBlockAt(blockPos, persistent))
            return;

        RebirthBlockPickupTargetDecision target =
            RebirthBlockPickupTargetResolver.Resolve(block);
        if (!target.IsValid || target.TargetItem.ItemClass == null)
            return;

        string localizedName = target.TargetItem.ItemClass.GetLocalizedItemName();
        if (string.IsNullOrEmpty(localizedName))
            localizedName = Localization.Get(target.TargetName ?? block.GetBlockName());

        activationText = string.Format(
            Localization.Get("pickupPrompt"), localizedName);
    }

    /// <summary>
    /// Native-style direct pickup is intentionally restricted to the exact families
    /// requested for this behavior: the base Frame Shapes block, small loose rocks,
    /// and the player-placeable wall torch that returns meleeToolTorch.
    /// </summary>
    public static bool IsDirectPressPickupTarget(Block block)
    {
        if (block == null)
            return false;

        string name = block.GetBlockName() ?? string.Empty;
        if (string.Equals(name, "frameShapes", StringComparison.OrdinalIgnoreCase))
            return true;

        // 3.1 does not place the family root itself.  The shape system materializes
        // concrete blocks such as frameShapes:cube / frameShapes:rampFrame and
        // records their originating family in autoShapeBaseName.  Treat ONLY
        // generated shapes from the frameShapes family as the requested native
        // tap-E pickup target.  Other shape families (wood/concrete/steel/etc.)
        // continue to use their normal interaction rules.
        if (block.GetAutoShapeType() == EAutoShapeType.Shape &&
            string.Equals(block.GetAutoShapeBlockName(), "frameShapes", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!HasTrueProperty(block, RebirthBlockPickupClassifier.PropertyVanillaCanPickup))
            return false;

        RebirthBlockPickupTargetDecision target =
            RebirthBlockPickupTargetResolver.Resolve(block);
        if (!target.IsValid || target.TargetItem.ItemClass == null)
            return false;

        string targetItemName = target.TargetItem.ItemClass.GetItemName() ?? string.Empty;
        return string.Equals(targetItemName, "resourceRockSmall", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetItemName, "meleeToolTorch", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasPreserveAfterLoot(Block block)
    {
        return HasTrueProperty(block, PropertyPreserveAfterLoot);
    }

    public static bool ShouldPreserveAfterLoot(TEFeatureStorage storage)
    {
        return storage != null && storage.Parent != null &&
            storage.Parent.TeData != null &&
            HasPreserveAfterLoot(storage.Parent.TeData.Block);
    }

    public static bool HasTrueProperty(Block block, string propertyName)
    {
        if (block == null || block.Properties == null ||
            string.IsNullOrEmpty(propertyName))
            return false;

        string raw;
        if (!block.Properties.Values.TryGetValue(propertyName, out raw) ||
            string.IsNullOrEmpty(raw))
            return false;

        bool value;
        return bool.TryParse(raw, out value) && value;
    }

    public static int GetEstimatedSellValue(Block block)
    {
        if (block == null)
            return 0;

        RebirthBlockPickupTargetDecision target =
            RebirthBlockPickupTargetResolver.Resolve(block);
        if (!target.IsValid || target.TargetItem.ItemClass == null)
            return 0;

        ItemClass itemClass = target.TargetItem.ItemClass;
        if (itemClass.IsBlock() && target.IsBlockTarget)
        {
            Block targetBlock = target.Target.Block;
            return targetBlock.SellableToTrader
                ? CalculateEstimatedSellValue(targetBlock.EconomicValue, targetBlock.EconomicSellScale)
                : 0;
        }

        return itemClass.SellableToTrader
            ? CalculateEstimatedSellValue(itemClass.EconomicValue, itemClass.EconomicSellScale)
            : 0;
    }

    private static int CalculateEstimatedSellValue(float economicValue, float economicSellScale)
    {
        return (int)((economicValue * economicSellScale) / 5f);
    }

    private static PickupCommandCacheEntry BuildCommandCacheEntry(BlockActivationCommand[] source)
    {
        int sourceTakeIndex = FindCommand(source, CommandName);
        int sourceCancelIndex = FindCommand(source, "cancel");
        bool hasTake = sourceTakeIndex >= 0;
        bool hasCancel = sourceCancelIndex >= 0;

        int takeIndex = hasTake ? sourceTakeIndex : source.Length;
        int takeLength = source.Length + (hasTake ? 0 : 1);

        int radialCancelIndex = hasCancel ? sourceCancelIndex : source.Length;
        int radialTakeIndex = hasTake
            ? sourceTakeIndex
            : source.Length + (hasCancel ? 0 : 1);
        int radialLength = source.Length
            + (hasCancel ? 0 : 1)
            + (hasTake ? 0 : 1);

        return new PickupCommandCacheEntry
        {
            Source = source,
            TakeResult = new BlockActivationCommand[takeLength],
            RadialResult = new BlockActivationCommand[radialLength],
            TakeIndex = takeIndex,
            CancelIndex = sourceCancelIndex,
            RadialTakeIndex = radialTakeIndex,
            RadialCancelIndex = radialCancelIndex
        };
    }

    private static void CopyAndConfigureTakeResult(PickupCommandCacheEntry entry)
    {
        if (entry.Source.Length > 0)
            Array.Copy(entry.Source, entry.TakeResult, entry.Source.Length);

        if (entry.TakeIndex >= entry.Source.Length)
            entry.TakeResult[entry.TakeIndex] = new BlockActivationCommand(CommandName, "hand", true);
        else
            entry.TakeResult[entry.TakeIndex].enabled = true;

        if (entry.CancelIndex >= 0)
            entry.TakeResult[entry.CancelIndex].enabled = false;
    }

    private static void CopyAndConfigureRadialResult(PickupCommandCacheEntry entry)
    {
        if (entry.Source.Length > 0)
            Array.Copy(entry.Source, entry.RadialResult, entry.Source.Length);

        if (entry.RadialTakeIndex >= entry.Source.Length)
            entry.RadialResult[entry.RadialTakeIndex] = new BlockActivationCommand(CommandName, "hand", true);
        else
            entry.RadialResult[entry.RadialTakeIndex].enabled = true;

        if (entry.RadialCancelIndex >= entry.Source.Length)
            entry.RadialResult[entry.RadialCancelIndex] = new BlockActivationCommand("cancel", "x", true);
        else
            entry.RadialResult[entry.RadialCancelIndex].enabled = true;
    }

    private static int FindCommand(BlockActivationCommand[] commands, string commandName)
    {
        for (int i = 0; i < commands.Length; i++)
        {
            if (string.Equals(commands[i].text, commandName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static int CountEnabled(BlockActivationCommand[] commands)
    {
        if (commands == null)
            return 0;

        int count = 0;
        for (int i = 0; i < commands.Length; i++)
        {
            if (commands[i].enabled)
                count++;
        }
        return count;
    }

    public static bool TryHandleActivation(
        Block block,
        string commandName,
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        EntityPlayerLocal player)
    {
        if (block == null || player == null || world == null)
            return false;

        bool isCancel = string.Equals(commandName, "cancel", StringComparison.OrdinalIgnoreCase);
        bool isTake = string.Equals(commandName, CommandName, StringComparison.OrdinalIgnoreCase);
        if (!isCancel && !isTake)
            return false;

        ResolveParentBlock(world, ref blockPos, ref blockValue);
        block = blockValue.Block;
        if (!ShouldUseRebirthPickup(block))
            return false;

        // "cancel" is a synthetic radial-only command added by REBIRTH. It must
        // never fall through to the block's original command array.
        if (isCancel)
            return true;

        RebirthBlockPickupDecision decision = RebirthBlockPickupClassifier.Evaluate(block);
        if (!decision.Allowed)
            return false;

        string denial;
        if (!ValidateLocalStart(world, blockPos, blockValue, player, out denial))
        {
            ShowDenied(player, denial);
            return true;
        }

        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        if (persistent == null || persistent.PrimaryId == null)
        {
            ShowDenied(player, "Player identity is not available for pickup.");
            return true;
        }

        player.AimingGun = false;
        World authoritativeWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (authoritativeWorld == null)
            return true;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null)
        {
            ShowDenied(player, "Network connection is not available for pickup.");
            return true;
        }

        if (connection.IsServer)
        {
            ProcessServerRequest(authoritativeWorld, blockPos, blockValue, player.entityId, persistent.PrimaryId);
            return true;
        }

        NetPackageRebirthBlockPickupRequest package = NetPackageManager
            .GetPackage<NetPackageRebirthBlockPickupRequest>()
            .Setup(blockPos, blockValue, player.entityId, persistent);
        connection.SendToServer(package);
        return true;
    }

    public static void ProcessServerRequest(
        World world,
        Vector3i blockPos,
        BlockValue expected,
        int playerId,
        PlatformUserIdentifierAbs persistentPlayerId)
    {
        if (world == null || GameManager.Instance == null)
            return;

        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerId);
        if (player == null || persistent == null || persistent.PrimaryId == null || persistentPlayerId == null ||
            !persistent.PrimaryId.Equals(persistentPlayerId))
            return;

        BlockValue current = world.GetBlock(blockPos);
        if (!SamePickupSource(current, expected))
            return;

        Block block = current.Block;
        RebirthBlockPickupDecision decision = RebirthBlockPickupClassifier.Evaluate(block);
        RebirthBlockPickupTargetDecision targetDecision =
            RebirthBlockPickupTargetResolver.Resolve(block);
        if (!decision.Allowed || !targetDecision.IsValid || current.damage > 0)
            return;

        Vector3 center = blockPos.ToVector3() + Vector3.one * 0.5f;
        if ((player.position - center).sqrMagnitude > MaximumPickupDistance * MaximumPickupDistance)
            return;

        TileEntity tileEntity = world.GetTileEntity(blockPos);
        if (tileEntity != null && tileEntity.IsUserAccessing())
            return;

        TileEntityComposite composite = tileEntity as TileEntityComposite;
        bool authorizedOwnedComposite = composite != null && composite.Owner != null &&
            RebirthBlockPickupAccessService.CanAccessOwnedComposite(
                composite, persistent.PrimaryId, world, player);
        if (composite != null && composite.Owner != null && !authorizedOwnedComposite)
            return;

        bool workstationBlock = block is BlockWorkstation;
        if (!workstationBlock && !authorizedOwnedComposite &&
            !world.CanPickupBlockAt(blockPos, persistent))
            return;

        if (IsLockedStorage(composite))
            return;

        TileEntityWorkstation workstation = tileEntity as TileEntityWorkstation;
        if (block is BlockWorkstation)
        {
            string workstationDenial;
            if (!RebirthWorkstationSecurityService.CanPickup(
                    world, blockPos, workstation, persistent, player, true,
                    out workstationDenial))
                return;
        }

        TEFeatureStorage storage;
        if (tileEntity != null && tileEntity.TryGetSelfOrFeature<TEFeatureStorage>(out storage))
        {
            if (!storage.IsEmpty())
                return;

            if (!storage.ItemGrid.Touched && (composite == null || !composite.PlayerPlaced))
                return;

            if (IsDestroyOnCloseStorage(storage))
                return;
        }

        RebirthPlacedWorkmanshipPickupBridge.SendBeforePickup(world, blockPos, playerId);
        RebirthElectricalPickupBridge.SendBeforePickup(world, blockPos, playerId);
        world.GetGameManager().PickupBlockServer(
            blockPos,
            current,
            playerId,
            persistent.PrimaryId);
        RebirthPlacedWorkmanshipService.Remove(blockPos);
        RebirthInfrastructureWorkService.RemoveRecord(blockPos);
    }

    /// <summary>
    /// Replaces the base client item grant for every block handled by REBIRTH so
    /// replacement block/item, quantity, secure-container metadata and preferred
    /// active-slot placement are all applied consistently. Block removal remains
    /// in the authoritative base PickupBlockServer path.
    /// </summary>
    public static bool TryHandlePickupClient(
        GameManager gameManager,
        Vector3i blockPos,
        BlockValue sourceValue,
        int playerId)
    {
        if (gameManager == null || sourceValue.isair || sourceValue.Block == null ||
            !ShouldUseRebirthPickup(sourceValue.Block))
            return false;

        RebirthBlockPickupTargetDecision targetDecision =
            RebirthBlockPickupTargetResolver.Resolve(sourceValue.Block);
        if (!targetDecision.IsValid)
            return false;

        World world = gameManager.World;
        if (world == null)
            return true;

        if (world.GetBlock(blockPos).type != sourceValue.type)
            return true;

        ItemValue grantedValue = targetDecision.CreateItemValue();
        if (targetDecision.IsStorageContainer)
            RebirthSecureContainerItem.Mark(grantedValue);
        RebirthPlacedWorkmanshipPickupBridge.ApplyPending(blockPos, grantedValue);
        RebirthElectricalPickupBridge.ApplyPending(blockPos, grantedValue);

        ItemStack itemStack = new ItemStack(grantedValue, targetDecision.Count);
        QuestEventManager.Current.BlockPickedUp(sourceValue.Block.GetBlockName(), blockPos);
        QuestEventManager.Current.ItemAdded(itemStack);

        foreach (EntityPlayerLocal localPlayer in world.GetLocalPlayers())
        {
            if (localPlayer.entityId != playerId)
                continue;

            // Preserve normal inventory stacking first. If a compatible partial stack
            // already exists anywhere in the backpack/toolbelt, a recovered stackable
            // block belongs there rather than being forced into the empty active hand.
            if (TryStackIntoExistingPlayerStack(localPlayer, itemStack))
                return true;

            // 2.6 behavior applies when there is no partial matching stack: with bare
            // hands and an empty active slot, put the recovered result directly in hand.
            // Natural pickups such as loose stones resolve to crafting resources,
            // not placeable blocks. Keep those in the normal backpack-first path.
            if (grantedValue.ItemClass.IsBlock() && TryPlaceInActiveEmptyHandSlot(localPlayer, itemStack))
                return true;

            if (localPlayer.PlayerUI.xui.PlayerInventory.AddItem(itemStack, true))
                return true;
        }

        gameManager.ItemDropServer(
            itemStack,
            blockPos.ToVector3() + Vector3.one * 0.5f,
            Vector3.zero,
            playerId,
            60f,
            false);
        return true;
    }

    private static bool TryStackIntoExistingPlayerStack(
        EntityPlayerLocal player,
        ItemStack itemStack)
    {
        if (player == null || player.PlayerUI == null || player.PlayerUI.xui == null ||
            itemStack == null || itemStack.IsEmpty() || itemStack.itemValue.ItemClass == null ||
            itemStack.itemValue.ItemClass.MaxCount <= 1)
            return false;

        XUiM_PlayerInventory inventory = player.PlayerUI.xui.PlayerInventory;
        if (inventory == null)
            return false;

        bool hasPartialStack =
            inventory.Backpack.CanStackNoEmpty(itemStack) ||
            inventory.Toolbelt.CanStackNoEmpty(itemStack);
        if (!hasPartialStack)
            return false;

        return inventory.AddItem(itemStack, true);
    }

    private static bool TryPlaceInActiveEmptyHandSlot(
        EntityPlayerLocal player,
        ItemStack itemStack)
    {
        if (player == null || player.inventory == null || itemStack == null ||
            !player.inventory.Hand.UsingBareHand())
            return false;

        int slot = player.inventory.holdingItemIdx;
        if (slot < 0 || slot >= RebirthToolbeltCapacity.GetOwnedSlotCount(player, player.inventory.Length) ||
            !player.inventory.GetItemStack(slot).IsEmpty())
            return false;

        int maxStack = itemStack.itemValue.ItemClass.Stacknumber.Value;
        if (itemStack.count > maxStack)
            return false;

        if (!player.PlayerUI.xui.PlayerInventory.AddItemToPreferredToolbeltSlot(itemStack.Clone(), slot))
            return false;

        player.inventory.SetHoldingItemIdxNoHolsterTime(slot);
        if (player.PlayerUI.xui.CollectedItemList != null)
            player.PlayerUI.xui.CollectedItemList.AddItemStack(itemStack.Clone());
        Audio.Manager.PlayInsidePlayerHead("item_pickup");
        return true;
    }

    public static void LogEmptyHandBlockHit(
        Block block,
        WorldBase world,
        BlockValueRef blockValueRef,
        BlockValue blockValue,
        int entityIdThatDamaged)
    {
        if (!RebirthDiagnosticPolicy.MayPrepare(RebirthLogSettings.BlockPickupLoggingEnabled))
            return;
        if (block == null || world == null || GameManager.IsDedicatedServer)
            return;

        EntityPlayerLocal player = world.GetEntity(entityIdThatDamaged) as EntityPlayerLocal;
        if (player == null || player.inventory == null || player.inventory.holdingItem == null)
            return;

        if (!string.Equals(player.inventory.holdingItem.GetItemName(), "meleeHandPlayer", StringComparison.Ordinal))
            return;

        Vector3i pos = blockValueRef.Type == BlockValueRefType.Block
            ? blockValueRef.BlockPosition
            : Vector3i.zero;
        if (blockValueRef.Type == BlockValueRefType.Block)
        {
            ResolveParentBlock(world, ref pos, ref blockValue);
            block = blockValue.Block;
            if (block == null)
                return;
        }

        int frame = Time.frameCount;
        if (lastLoggedFrame == frame && lastLoggedEntityId == entityIdThatDamaged && lastLoggedPosition == pos)
            return;

        lastLoggedFrame = frame;
        lastLoggedEntityId = entityIdThatDamaged;
        lastLoggedPosition = pos;

        RebirthBlockPickupDecision decision = RebirthBlockPickupClassifier.Evaluate(block);
        RebirthBlockPickupTargetDecision targetDecision =
            RebirthBlockPickupTargetResolver.Resolve(block);
        Log.Out("[REBIRTH BlockPickup] empty-hand hit block=" + block.GetBlockName()
            + " id=" + block.blockID
            + " pos=" + pos
            + " damage=" + blockValue.damage
            + " pickup=" + decision
            + " target=" + (targetDecision.IsValid
                ? targetDecision.TargetName
                : "invalid")
            + " targetCount=" + targetDecision.Count
            + " targetReason=" + targetDecision.Reason);
    }

    public static string DescribeBlock(BlockValue blockValue, Vector3i blockPos, World world)
    {
        if (blockValue.isair || blockValue.Block == null)
            return "[REBIRTH BlockPickup] No block selected.";

        Block block = blockValue.Block;
        RebirthBlockPickupDecision decision = RebirthBlockPickupClassifier.Evaluate(block);
        RebirthBlockPickupTargetDecision targetDecision =
            RebirthBlockPickupTargetResolver.Resolve(block);
        TileEntity tileEntity = world != null ? world.GetTileEntity(blockPos) : null;
        TEFeatureStorage storage = null;
        bool hasStorage = tileEntity != null && tileEntity.TryGetSelfOrFeature<TEFeatureStorage>(out storage);
        bool empty = !hasStorage || storage == null || storage.IsEmpty();
        TileEntityComposite composite = tileEntity as TileEntityComposite;

        return "[REBIRTH BlockPickup] block=" + block.GetBlockName()
            + " id=" + block.blockID
            + " pos=" + blockPos
            + " class=" + block.GetType().FullName
            + " shape=" + (block.shape != null ? block.shape.GetType().Name : "null")
            + " isProp=" + block.IsProp
            + " canPickup=" + block.CanPickup
            + " instantMode=" + RebirthSandboxOptionManager.Current.InstantBlockPickup
            + " estimatedSellValue=" + GetEstimatedSellValue(block)
            + " radial=" + ShouldForceRadialMenu(block)
            + " instantOverride=" + HasTrueProperty(block, PropertyInstantPickup)
            + " preserveAfterLoot=" + HasPreserveAfterLoot(block)
            + " downgrade=" + (!block.DowngradeBlock.isair && block.DowngradeBlock.Block != null
                ? block.DowngradeBlock.Block.GetBlockName()
                : "none")
            + " target=" + (targetDecision.IsValid
                ? targetDecision.TargetName
                : "invalid")
            + " targetCount=" + targetDecision.Count
            + " targetReason=" + targetDecision.Reason
            + " targetLockable=" + (targetDecision.IsStorageContainer &&
                ((BlockCompositeTileEntity)targetDecision.Target.Block).CompositeData.HasFeature<TEFeatureLockable>())
            + " filterTags=" + (block.FilterTags == null ? "" : string.Join(",", block.FilterTags))
            + " storage=" + hasStorage
            + " empty=" + empty
            + " owner=" + (composite != null && composite.Owner != null ? composite.Owner.CombinedString : "none")
            + " decision={" + decision + "}";
    }

    private static bool ValidateLocalStart(
        WorldBase world,
        Vector3i blockPos,
        BlockValue expected,
        EntityPlayerLocal player,
        out string denial)
    {
        denial = string.Empty;
        BlockValue current = world.GetBlock(blockPos);
        if (!SamePickupSource(current, expected))
        {
            denial = "The block changed before pickup completed.";
            return false;
        }

        if (current.damage > 0)
        {
            denial = Localization.Get("ttRepairBeforePickup");
            return false;
        }

        if (!RebirthBlockPickupClassifier.Evaluate(current.Block).Allowed)
        {
            denial = "This block is not eligible for pickup.";
            return false;
        }

        if (!RebirthBlockPickupTargetResolver.Resolve(current.Block).IsValid)
        {
            denial = "This block has no valid pickup result.";
            return false;
        }

        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        if (persistent == null)
        {
            denial = "Player identity is not available for pickup.";
            return false;
        }

        Vector3 center = blockPos.ToVector3() + Vector3.one * 0.5f;
        if ((player.position - center).sqrMagnitude > MaximumPickupDistance * MaximumPickupDistance)
        {
            denial = "You are too far away to pick up this block.";
            return false;
        }

        TileEntity tileEntity = world.GetTileEntity(blockPos);
        if (tileEntity != null && tileEntity.IsUserAccessing())
        {
            denial = Localization.Get("ttCantPickupInUse");
            return false;
        }

        TEFeatureStorage storage;
        if (tileEntity != null && tileEntity.TryGetSelfOrFeature<TEFeatureStorage>(out storage))
        {
            if (!storage.IsEmpty())
            {
                denial = "The container must be empty before it can be picked up.";
                return false;
            }

            if (IsDestroyOnCloseStorage(storage))
            {
                denial = "Temporary loot containers cannot be picked up.";
                return false;
            }

            TileEntityComposite storageComposite = tileEntity as TileEntityComposite;
            if (!storage.ItemGrid.Touched && (storageComposite == null || !storageComposite.PlayerPlaced))
            {
                denial = "The container must be opened before it can be picked up.";
                return false;
            }
        }

        TileEntityComposite composite = tileEntity as TileEntityComposite;
        bool authorizedOwnedComposite = composite != null && composite.Owner != null &&
            RebirthBlockPickupAccessService.CanAccessOwnedComposite(
                composite, persistent.PrimaryId, world, player);
        if (composite != null && composite.Owner != null && !authorizedOwnedComposite)
        {
            denial = Localization.Get("xuiRebirthPickupOwnerDenied");
            return false;
        }

        if (!(current.Block is BlockWorkstation) && !authorizedOwnedComposite &&
            !world.CanPickupBlockAt(blockPos, persistent))
        {
            denial = "This block cannot be picked up here.";
            return false;
        }

        if (IsLockedStorage(composite))
        {
            denial = Localization.Get("xuiRebirthPickupUnlockFirst");
            return false;
        }

        if (current.Block is BlockWorkstation)
        {
            string workstationDenial;
            if (!RebirthWorkstationSecurityService.CanPickup(
                    world,
                    blockPos,
                    tileEntity as TileEntityWorkstation,
                    persistent,
                    player,
                    false,
                    out workstationDenial))
            {
                denial = workstationDenial;
                return false;
            }
        }

        return true;
    }


    private static bool IsLockedStorage(TileEntityComposite composite)
    {
        if (composite == null)
            return false;

        TEFeatureLockPickable lockPickable = composite.GetFeature<TEFeatureLockPickable>();
        if (lockPickable != null && lockPickable.NeedsLockpicking())
            return true;

        TEFeatureLockable lockable = composite.GetFeature<TEFeatureLockable>();
        return lockable != null && lockable.IsLocked();
    }

    private static bool IsDestroyOnCloseStorage(TEFeatureStorage storage)
    {
        if (storage == null || storage.ItemGrid.PlayerOwned ||
            string.IsNullOrEmpty(storage.lootListName))
            return false;

        TEFeatureStorage featureStorage = storage as TEFeatureStorage;
        if (featureStorage != null && ShouldPreserveAfterLoot(featureStorage))
            return false;

        LootContainer lootContainer = LootContainer.GetLootContainer(storage.lootListName);
        return lootContainer != null &&
            lootContainer.destroyOnClose != LootContainer.DestroyOnClose.False;
    }

    private static bool SamePickupSource(BlockValue current, BlockValue expected)
    {
        return !current.isair && current.rawData == expected.rawData;
    }

    private static void ResolveParentBlock(
        WorldBase world,
        ref Vector3i blockPos,
        ref BlockValue blockValue)
    {
        if (world == null || !blockValue.ischild || blockValue.Block == null || blockValue.Block.multiBlockPos == null)
            return;

        Vector3i parentPos = blockValue.Block.multiBlockPos.GetParentPos(blockPos, blockValue);
        BlockValue parentValue = world.GetBlock(parentPos);
        if (parentValue.isair || parentValue.ischild)
            return;

        blockPos = parentPos;
        blockValue = parentValue;
    }

    private static void ShowDenied(EntityPlayerLocal player, string message)
    {
        if (player != null)
            GameManager.ShowTooltip(player, string.IsNullOrEmpty(message) ? "Pickup denied." : message, string.Empty, "ui_denied");
    }
}
