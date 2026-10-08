using System;

/// <summary>
/// Keyboard/controller shortcuts for native block-shape operations. The gameplay
/// behavior mirrors the 3.1 BlockShape radial command; only the trigger is REBIRTH.
/// </summary>
public static class RebirthBuildingHotkeyService
{
    public static bool TryCopyShapeAndRotation(EntityPlayerLocal player)
    {
        if (player == null || player.inventory == null || player.world == null)
            return false;
        if (player.PlayerUI == null || player.PlayerUI.windowManager == null)
            return false;

        Inventory inventory = player.inventory;
        ItemValue holdingValue = inventory.holdingItemItemValue;
        if (holdingValue == null || !(holdingValue.ItemClass is ItemClassBlock) ||
            holdingValue.ItemClass.GetItemName().IndexOf("shapes:", StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        Block blockHolding = inventory.GetHoldingBlock().GetBlock();
        ItemInventoryData ibid = inventory.holdingItemData;
        if (blockHolding == null || ibid == null || !blockHolding.SelectAlternates)
            return false;
        if (!player.HitInfo.bHitValid || player.HitInfo.hit.blockValue.isair)
            return false;

        Block blockSelectedShape = blockHolding.GetAltBlock(ibid.itemValue.Meta);
        if (blockSelectedShape == null)
            return false;

        BlockValue targetValue = player.HitInfo.hit.blockValue;
        if (targetValue.ischild)
        {
            Vector3i parentPos = targetValue.Block.multiBlockPos.GetParentPos(
                player.HitInfo.hit.blockPos, targetValue);
            targetValue = player.world.GetBlock(parentPos);
        }
        if (targetValue.isair || targetValue.Block == null)
            return false;

        // Match the native 3.1 Copy Shape + Rotation radial eligibility.
        bool onlySimpleRotations =
            (blockSelectedShape.AllowedRotations & EBlockRotationClasses.Advanced) == EBlockRotationClasses.None;
        bool hasCopyRotation = blockSelectedShape.SupportsRotation(targetValue.rotation);
        if (onlySimpleRotations || !hasCopyRotation)
            return false;

        Block targetedBlock = targetValue.Block;
        bool hasCopyAutoShape = false;
        bool hasCopyShapeLegacy = false;

        if (targetedBlock.GetAutoShapeType() != EAutoShapeType.None &&
            blockHolding.AutoShapeSupportsShapeName(targetedBlock.GetAutoShapeShapeName()))
        {
            string shapeMenu;
            hasCopyAutoShape = !targetedBlock.Properties.TryGetValue("ShapeMenu", out shapeMenu) ||
                !shapeMenu.EqualsCaseInsensitive("false") ||
                GameManager.Instance.IsEditMode() || player.IsGodMode.Value;
        }
        else if (blockHolding.ContainsAlternateBlock(targetedBlock.GetBlockName()))
        {
            hasCopyShapeLegacy = true;
        }

        if (!hasCopyAutoShape && !hasCopyShapeLegacy)
            return false;

        int alternateIndex = hasCopyAutoShape
            ? blockHolding.AutoShapeAlternateShapeNameIndex(targetedBlock.GetAutoShapeShapeName())
            : blockHolding.GetAlternateBlockIndex(targetedBlock.GetBlockName());
        if (alternateIndex < 0)
            return false;

        ibid.itemValue.Meta = alternateIndex;
        ibid.rotation = targetValue.rotation;
        ibid.mode = BlockPlacement.EnumRotationMode.Advanced;

        // The native radial refreshes the held toolbelt stack immediately after changing
        // Meta. Do the same so shape ghost/icon state changes without waiting a frame.
        XUiWindowGroup toolbeltWindow =
            player.PlayerUI.windowManager.GetWindow("toolbelt") as XUiWindowGroup;
        XUiC_Toolbelt toolbelt = toolbeltWindow != null
            ? toolbeltWindow.Controller.GetChildByType<XUiC_Toolbelt>()
            : null;
        if (toolbelt != null)
        {
            int viewIndex;
            XUiC_ItemStack slot = toolbelt.TryAbsoluteToView(inventory.holdingItemIdx, out viewIndex)
                ? toolbelt.itemControllers[viewIndex] : null;
            if (slot != null)
            {
                slot.ItemStack = new ItemStack(ibid.itemValue, ibid.itemStack.count);
                slot.ForceRefreshItemStack();
            }
        }

        return true;
    }
}
