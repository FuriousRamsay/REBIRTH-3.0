using System;

#nullable disable

/// <summary>
/// Route adapter for the Rebirth-owned Personal Crafting top tabs.
///
/// The public native paging routes remain authoritative for Map, Quests,
/// Challenges and Players. Character deliberately prefers the already-approved
/// Rebirth Survivor Character surface when a committed Rebirth character exists;
/// Skills opens its Progression page;
/// otherwise it falls back to the native Character route.
/// </summary>
public static class RebirthCraftingNavigationService
{
    public enum Destination
    {
        Inventory = 0,
        Crafting = 1,
        Character = 2,
        Map = 3,
        Skills = 4,
        Quests = 5,
        Challenges = 6,
        Players = 7,
        Journal = 8,
        PersonalCrafting = 9
    }

    public static bool Navigate(XUiController source, Destination destination)
    {
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
            return false;
        if (source == null || source.xui == null || source.xui.playerUI == null)
            return false;
        var character = XUiC_RebirthSurvivorCharacter.ActiveInstance;
        if (character != null && character.xui == source.xui
            && (character.GetChildByType<XUiC_RebirthHudTrackingManager>()?.IsManagerOpen ?? false))
            return false;

        EntityPlayerLocal player = source.xui.playerUI.entityPlayer;
        GUIWindowManager windowManager = source.xui.playerUI.windowManager;
        if (player == null || windowManager == null || player.IsDead())
            return false;
        if(source.windowGroup?.Controller is XUiC_RebirthTraderWorkspace)
            windowManager.Close("trader");

        if (destination == Destination.Crafting && RebirthContextNavigationService.IsActiveFor(source.xui))
        {
            bool changedPage = RebirthContextNavigationService.IsSuspendedFor(source.xui);
            if (!RebirthContextNavigationService.ReturnToContext(source.xui)) return false;
            // Other routes open the native paging selector; return-to-container does not.
            // Reuse that selector's configured sound rather than guessing a sound asset name.
            if (changedPage)
            {
                var selector = source.xui.FindWindowGroupByName("windowpaging")?.GetChildByType<XUiC_WindowSelector>();
                if (!string.IsNullOrEmpty(selector?.SoundOnOpen))
                    Audio.Manager.PlayInsidePlayerHead(selector.SoundOnOpen);
            }
            return true;
        }

        if(destination==Destination.Crafting && RebirthCookingNavigation.Available(source.xui))
            return RebirthCookingNavigation.Return(source.xui);

        switch (destination)
        {
            case Destination.Inventory:
            {
                XUiC_RebirthPersonalCrafting owner = source.GetParentByType<XUiC_RebirthPersonalCrafting>();
                if (owner == null) owner = source as XUiC_RebirthPersonalCrafting;
                if (owner == null) return OpenPersonalSurface(source, windowManager, false);
                owner.ShowInventorySurface();
                return true;
            }

            case Destination.Crafting:
            {
                XUiC_RebirthPersonalCrafting owner = source.GetParentByType<XUiC_RebirthPersonalCrafting>();
                if (owner == null) owner = source as XUiC_RebirthPersonalCrafting;
                if (owner == null) return OpenPersonalSurface(source, windowManager, true);
                owner.ShowCraftingSurface();
                return true;
            }

            case Destination.PersonalCrafting:
                // Explicit shortcut opens recipes/backpack even during a container lease.
                // The ordinary Crafting tab still returns to its current station/container.
                PrepareExternalRoute(source);
                return OpenPersonalSurface(source, windowManager, true);
            case Destination.Character:
                if (source.GetParentByType<XUiC_RebirthSurvivorCharacter>() != null || source is XUiC_RebirthSurvivorCharacter) return true;
                PrepareExternalRoute(source);
                RebirthContextNavigationService.SetOverlayDestination(Destination.Character);
                return OpenCharacter(source, player, windowManager);

            case Destination.Map:
                PrepareExternalRoute(source);
                RebirthContextNavigationService.SetOverlayDestination(Destination.Map);
                return OpenNativePagingRoute(player, "map");

            case Destination.Skills:
                PrepareExternalRoute(source);
                RebirthContextNavigationService.SetOverlayDestination(Destination.Skills);
                return OpenSkills(source, player, windowManager);

            case Destination.Quests:
                PrepareExternalRoute(source);
                RebirthContextNavigationService.SetOverlayDestination(Destination.Quests);
                return OpenNativePagingRoute(player, "quests");

            case Destination.Challenges:
                PrepareExternalRoute(source);
                RebirthContextNavigationService.SetOverlayDestination(Destination.Challenges);
                return OpenNativePagingRoute(player, "challenges");

            case Destination.Players:
                PrepareExternalRoute(source);
                RebirthContextNavigationService.SetOverlayDestination(Destination.Players);
                return OpenNativePagingRoute(player, "players");

            case Destination.Journal:
                if (windowManager.IsWindowOpen("rebirthJournal")) return true;
                PrepareExternalRoute(source);
                RebirthContextNavigationService.SetOverlayDestination(Destination.Journal);
                if (windowManager.IsWindowOpen("crafting")) windowManager.Close("crafting");
                if (windowManager.IsWindowOpen("windowpaging")) windowManager.Close("windowpaging");
                windowManager.Open("rebirthJournal", true);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Explicit item cross-link. The top-row Crafting button means "return to Loot/Storage"
    /// during a container lease, so Recipes must not call Navigate(Crafting). Resolve the
    /// visible personal catalogue in this XUi, not the first hidden native RecipeList.
    /// </summary>
    public static bool OpenRecipesForItem(XUiController source, ItemStack selectedItem)
    {
        if (source?.xui?.playerUI == null || selectedItem == null || selectedItem.IsEmpty() ||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld() || RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
            return false;
        var ui = source.xui;
        var manager = ui.playerUI.windowManager;
        if (manager == null || ui.playerUI.entityPlayer == null || ui.playerUI.entityPlayer.IsDead()) return false;
        var group = ui.FindWindowGroupByName("crafting");
        var owner = group as XUiC_RebirthPersonalCrafting ?? group?.GetChildByType<XUiC_RebirthPersonalCrafting>();
        var catalogue = owner?.GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>();
        if (catalogue == null) return false;

        // Snapshot BEFORE a source window can retire its slot controls. Queue AFTER focus
        // release/OnOpen housekeeping so a search-field Submit cannot erase this new request.
        // If Open is queued, the pending request survives until the owner's OnOpen completes.
        ItemStack snapshot = selectedItem.Clone();
        PrepareExternalRoute(source);
        if (!manager.IsWindowOpen("crafting")) manager.Open("crafting", true);
        owner.ShowCraftingSurface();
        RebirthContextNavigationService.SetOverlayDestination(Destination.Crafting);
        catalogue.RequestRecipesUsingItem(snapshot);
        catalogue.ApplyPendingItemRecipes();
        return true;
    }

    private static void PrepareExternalRoute(XUiController source)
    {
        if (source != null && source.xui != null && RebirthContextNavigationService.IsActiveFor(source.xui))
            RebirthContextNavigationService.SuspendForExternalRoute(source.xui);
        XUiC_RebirthPersonalCrafting owner = source != null
            ? source.GetParentByType<XUiC_RebirthPersonalCrafting>()
            : null;
        owner?.PrepareForExternalRoute();
        GUIWindowManager manager = source?.xui?.playerUI?.windowManager;
        if (manager != null && manager.IsWindowOpen("rebirthJournal")) manager.Close("rebirthJournal");
        if (manager != null && manager.IsWindowOpen(XUiC_RebirthSurvivorCharacter.WindowGroupId))
            manager.Close(XUiC_RebirthSurvivorCharacter.WindowGroupId);
    }

    private static bool OpenPersonalSurface(XUiController source, GUIWindowManager manager, bool crafting)
    {
        XUiController group = source.xui.FindWindowGroupByName("crafting");
        XUiC_RebirthPersonalCrafting owner = group as XUiC_RebirthPersonalCrafting;
        if (owner == null) owner = group?.GetChildByType<XUiC_RebirthPersonalCrafting>();
        if (owner == null) return false;
        if (manager.IsWindowOpen(XUiC_RebirthSurvivorCharacter.WindowGroupId))
            manager.Close(XUiC_RebirthSurvivorCharacter.WindowGroupId);
        if (manager.IsWindowOpen("rebirthJournal")) manager.Close("rebirthJournal");
        // Native Open runs OnOpen before returning. Apply the requested surface afterwards:
        // the standard Tab-open remains Inventory, while the Crafting tab explicitly selects Crafting.
        manager.Open("crafting", true);
        if (crafting) owner.ShowCraftingSurface(); else owner.ShowInventorySurface();
        return true;
    }

    private static bool OpenCharacter(XUiController source, EntityPlayerLocal player, GUIWindowManager windowManager)
    {
        try
        {
            RebirthProgressionWindowRouting.Witness witness;
            var admission = RebirthProgressionWindowRouting.Resolve(source, out witness);
            if (admission == RebirthProgressionWindowRouting.Admission.Blocked) return false;
            bool hasRebirthCharacter = admission == RebirthProgressionWindowRouting.Admission.Rebirth;
            if (hasRebirthCharacter && (!ReferenceEquals(source?.xui?.playerUI?.entityPlayer, player) ||
                !ReferenceEquals(source.xui.playerUI.windowManager, windowManager))) return false;

            if (hasRebirthCharacter)
            {
                // The dedicated Survivor Character surface is not a native windowpaging page.
                // Close the current Crafting route first so its OnClose can restore/close the
                // hidden paging host, then open the already-approved Rebirth Character group.
                if (windowManager.IsWindowOpen("crafting"))
                {
                    windowManager.Close("crafting");
                    if (!RebirthProgressionWindowRouting.StillCurrent(source, witness)) return false;
                }
                if (windowManager.IsWindowOpen("windowpaging"))
                {
                    windowManager.Close("windowpaging");
                    if (!RebirthProgressionWindowRouting.StillCurrent(source, witness)) return false;
                }
                if (!RebirthProgressionWindowRouting.StillCurrent(source, witness)) return false;
                windowManager.Open(XUiC_RebirthSurvivorCharacter.WindowGroupId, true);
                return true;
            }
        }
        catch (Exception e)
        {
            Log.Warning("[REBIRTH Crafting] Character tab Rebirth route probe failed: " + e.Message);
            // Native character routing is intercepted back here; retrying it would recurse.
            return false;
        }

        return OpenNativePagingRoute(player, "character");
    }

    private static bool OpenSkills(XUiController source, EntityPlayerLocal player, GUIWindowManager manager)
    {
        RebirthProgressionWindowRouting.Witness witness;
        var admission = RebirthProgressionWindowRouting.Resolve(source, out witness);
        if (admission == RebirthProgressionWindowRouting.Admission.Native)
            return OpenNativePagingRoute(player, "skills");
        if (admission == RebirthProgressionWindowRouting.Admission.Blocked) return false;
        if (!ReferenceEquals(source?.xui?.playerUI?.entityPlayer, player) ||
            !ReferenceEquals(source.xui.playerUI.windowManager, manager)) return false;
        if (!OpenCharacter(source, player, manager)) return false;
        var character = player.PlayerUI.xui.FindWindowGroupByName(XUiC_RebirthSurvivorCharacter.WindowGroupId)
            ?.GetChildByType<XUiC_RebirthSurvivorCharacter>();
        if (character == null || !RebirthProgressionWindowRouting.StillCurrent(source, witness)) return false;
        character.ShowSkillsPage();
        return true;
    }
    private static bool OpenNativePagingRoute(EntityPlayerLocal player, string route)
    {
        if (player == null || string.IsNullOrEmpty(route))
            return false;

        XUiC_WindowSelector.OpenSelectorAndWindow(player, route);
        return true;
    }
}
