#nullable disable

public static class Harmony_RebirthBlockPickupPatches
{
    public static void Prefix_BlocksFromXml_CreateBlocks(XmlFile __0)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthBlockPickupEmptyVariantGenerator.Prepare(__0);
    }

    public static void Prefix_BlockCompositeTileEntity_Init(
        BlockCompositeTileEntity __instance)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthBlockPickupSecurityInjector.Prepare(__instance);
    }


    public static void Postfix_BlockWorkstation_PlaceBlock(
        WorldBase __0,
        BlockPlacement.Result __1,
        EntityAlive __2)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthWorkstationSecurityService.RegisterPlaced(__0, __1.blockPos, __2);
    }

    public static void Postfix_BlockWorkstation_OnBlockRemoved(
        Vector3i __2)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthWorkstationSecurityService.Remove(__2);
    }

    public static void Postfix_BlockWorkstation_GetActivationText(
        WorldBase __0,
        BlockValue __1,
        Vector3i __2,
        EntityAlive __3,
        ref string __result)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthBlockPickupService.ApplyPickupActivationText(
            __1.Block, __0, __1, __2, __3, ref __result);
        RebirthWorkstationSecurityService.ApplyActivationText(
            __0, __2, __1, __3, ref __result);
    }


    public static void Postfix_TileEntity_CanLockOnServer(
        TileEntity __instance,
        int __0,
        ref bool __result)
    {
        if (!RebirthBlockPickupPatchInstaller.Active || !__result ||
            !(__instance is TileEntityWorkstation))
            return;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        __result = RebirthWorkstationSecurityService.CanLockOnServer(
            world, __instance.ToWorldPos(), __0);
    }

    public static void Postfix_GameManager_SaveWorld()
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthWorkstationSecurityService.Save();
    }

    public static bool Prefix_GameManager_PickupBlockClient(
        GameManager __instance,
        Vector3i __0,
        BlockValue __1,
        int __2)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return true;

        return !RebirthBlockPickupService.TryHandlePickupClient(
            __instance, __0, __1, __2);
    }

    public static bool Prefix_TEFeatureStorage_ShouldDestroyOnClose(
        TEFeatureStorage __instance,
        ref bool __result)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return true;

        if (__instance == null ||
            (!__instance.ItemGrid.PlayerOwned &&
             !RebirthBlockPickupService.ShouldPreserveAfterLoot(__instance)))
            return true;

        __result = false;
        return false;
    }

    public static void Postfix_TEFeatureStorage_OnAdded(
        TEFeatureStorage __instance,
        Vector3i __0,
        BlockValue __1)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthContainerSizeService.ApplyToNewPlayerStorage(__instance, __1);
    }

    public static void Postfix_BlockCompositeTileEntity_GetActivationText(
        WorldBase __0,
        BlockValue __1,
        Vector3i __2,
        EntityAlive __3,
        ref string __result)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthBlockPickupService.ApplyPickupActivationText(
            __1.Block, __0, __1, __2, __3, ref __result);
        RebirthContainerRenameService.ApplyCustomNameToActivationText(
            __0, __2, __1, ref __result);
    }

    public static void Postfix_Block_GetActivationText(
        Block __instance,
        WorldBase __0,
        BlockValue __1,
        Vector3i __2,
        EntityAlive __3,
        ref string __result)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthBlockPickupService.ApplyPickupActivationText(
            __instance, __0, __1, __2, __3, ref __result);
    }

    public static void Postfix_XUiC_ItemStack_GetBindingValueInternal(
        XUiC_ItemStack __instance,
        ref string __0,
        string __1,
        ref bool __result)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthSecureContainerItem.ApplyItemStackBinding(
            __instance, ref __0, __1, ref __result);
    }

    public static void Postfix_ItemStack_CanStackWith(
        ItemStack __instance,
        ItemStack __0,
        ref bool __result)
    {
        if (!RebirthBlockPickupPatchInstaller.Active || !__result ||
            __instance == null || __0 == null)
            return;

        // The base game's block stacking check ignores ItemValue metadata. Keep
        // recovered secure containers distinct so the lock marker cannot be lost
        // by merging into an ordinary POI/container block stack.
        if (RebirthSecureContainerItem.IsMarked(__instance.itemValue) !=
            RebirthSecureContainerItem.IsMarked(__0.itemValue))
            __result = false;
    }

    public static void Postfix_HasBlockActivationCommands(
        Block __instance,
        ref bool __result)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        if (!__result && RebirthBlockPickupService.ShouldAddPickupCommand(__instance))
            __result = true;
    }

    // Positional Harmony arguments are intentional because Block subclasses use
    // different source parameter names for the same virtual signatures.
    public static void Postfix_GetBlockActivationCommands(
        Block __instance,
        WorldBase __0,
        BlockValue __1,
        Vector3i __2,
        EntityAlive __3,
        ref BlockActivationCommand[] __result)
    {
        // Quick Stack and Remote Resources share this already-installed activation hook,
        // but their commands must not disappear when the independent Block Pickup option
        // is disabled at runtime.
        if (RebirthBlockPickupPatchInstaller.Active)
        {
            __result = RebirthBlockPickupService.AppendPickupCommandIfNeeded(__instance, __result);
            if (__instance is BlockWorkstation)
            {
                __result = RebirthWorkstationSecurityService.ConfigureCommands(
                    __result, __0, __2, __1, __3);
            }
            else
            {
                RebirthBlockPickupService.ConfigurePickupCommandAvailability(
                    __result, __instance, __0, __2, __1, __3);
                __result = RebirthContainerRenameService.AppendCommandIfNeeded(
                    __result, __0, __2, __1, __3);
                RebirthContainerRenameService.ConfigureCommands(__result, __0, __2, __1, __3);
            }
        }

        if (__instance is BlockWorkstation)
        {
            __result = RemoteResourceToggleCommand.Append(__result, __0, __2, __3);
        }
        else
        {
            __result = QuickStackContainerCategoryCommand.Append(__result, __0, __2, __3);
            __result = RebirthContainerPackUp.Append(__result, __0, __2, __3);
            // Static containers use the 2.6-style Wi-Fi button in windowLooting.
            // Workstations retain the radial command because they do not use that window.
        }
    }

    public static bool Prefix_OnBlockActivated(
        Block __instance,
        string __0,
        WorldBase __1,
        Vector3i __2,
        BlockValue __3,
        EntityPlayerLocal __4,
        ref bool __result)
    {
        // Handle the independent Quick Stack / Remote Resource commands even when the
        // Block Pickup runtime option is disabled.
        if (RebirthContainerPackUp.TryHandle(__0, __1, __2, __4))
        { __result = true; return false; }

        if (QuickStackContainerCategoryCommand.TryHandle(__0, __1, __2, __4))
        {
            __result = true;
            return false;
        }

        if (RemoteResourceToggleCommand.TryHandle(__0, __1, __2, __4))
        {
            __result = true;
            return false;
        }

        if (!RebirthBlockPickupPatchInstaller.Active)
            return true;

        if (__instance is BlockWorkstation &&
            RebirthWorkstationSecurityService.TryHandleActivation(
                __0, __1, __2, __3, __4))
        {
            __result = true;
            return false;
        }

        if (RebirthContainerRenameService.TryHandleActivation(
                __0, __1, __2, __3, __4))
        {
            __result = true;
            return false;
        }

        if (!RebirthBlockPickupService.TryHandleActivation(
                __instance, __0, __1, __2, __3, __4))
            return true;

        __result = true;
        return false;
    }

    public static void Prefix_OnBlockDamaged(
        Block __instance,
        WorldBase __0,
        BlockValueRef __1,
        BlockValue __2,
        int __4)
    {
        if (!RebirthBlockPickupPatchInstaller.Active)
            return;

        RebirthBlockPickupService.LogEmptyHandBlockHit(
            __instance, __0, __1, __2, __4);
    }
}
