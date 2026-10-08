using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable disable

/// <summary>REBIRTH equipment-driven toolbelt: 4..18 usable slots, 20 real storage slots for recovery.
/// Installed Inventory uses ItemStackGrid; Hand owns transient/bare-hand state separately.</summary>
public static class RebirthToolbeltCapacity
{
    public const int StartingSlots = 4;
    public const int MaximumSlots = 18;
    public const int BackingPublicSlots = 20;
    public const int PhysicalInventorySlots = BackingPublicSlots;
    private static bool loggedBackingFailure;
    public static bool Enabled => RebirthSurvivorMode.ConfiguredMode == RebirthPlayerProgressionMode.Rebirth;
    public static int GetSlotsForLevel(int playerLevel)
    {
        return StartingSlots; // Capacity comes from equipment and background, never player level.
    }

    // Every installed ItemStackGrid element is owned storage; there is no dummy slot.
    public static int GetOwnedSlotCount(EntityPlayer player, int backingSlotCount)
    {
        return Math.Max(0, Math.Min(GetSlotsForPlayer(player), backingSlotCount));
    }

    public static int GetSlotsForPlayer(EntityPlayer player)
    {
        if (!Enabled) return player?.inventory?.Length ?? ToolbeltConfig.CurrentConfig.TotalSlots;
        int level = 1;
        try
        {
            if (player != null && player.Progression != null)
                level = player.Progression.Level;
        }
        catch
        {
            level = 1;
        }

        return Math.Min(MaximumSlots, GetSlotsForLevel(level) + RebirthSurvivorGearService.GetToolbeltBonus(player) + RebirthBackgroundStorageService.ToolbeltBonus(player));
    }

    public static EntityAlive ResolveOwner(Inventory inventory) => inventory?.entity;
    public static bool IsPlayerInventory(Inventory inventory) => ResolveOwner(inventory) is EntityPlayer;

    // Capacity must be supplied BEFORE native construction binds Hand and creates preferences.
    // ReadInto restores saved grids and resizes them to the constructor's SlotCount itself.
    // Never resize just ItemGrid after Hand binding: the action-data array would become stale.
    public static void EnsurePhysicalCapacity(Inventory inventory)
    {
        if (!Enabled || !IsPlayerInventory(inventory) || inventory.Length >= BackingPublicSlots) return;
        if (!loggedBackingFailure)
        {
            loggedBackingFailure = true;
            Log.Warning("[REBIRTH Toolbelt] Player inventory was constructed before capacity integration; retaining existing storage until native reconstruction.");
        }
    }
    public static bool IsRejectedIncoming(ItemStack stack) => Enabled && stack != null && !stack.IsEmpty()
        && string.Equals(stack.itemValue?.ItemClass?.GetItemName(), "noteDuke01", StringComparison.OrdinalIgnoreCase);

    public static bool IsLockedPlayerSlot(Inventory inventory, int slot)
    {
        EntityPlayer player = ResolveOwner(inventory) as EntityPlayer;
        if (!Enabled || player == null || slot < 0 || slot >= inventory.Length)
            return false;
        return slot >= GetSlotsForPlayer(player);
    }

    public static void RecoverLockedSlots(EntityPlayer player)
    {
        if (!Enabled || player == null || player.inventory == null) return;
        EnsurePhysicalCapacity(player.inventory);
        int unlocked = GetSlotsForPlayer(player);
        ItemStack[] slots = player.inventory.ItemGrid.CloneItems();
        int limit = Math.Min(BackingPublicSlots, slots != null ? slots.Length : 0);
        for (int i = unlocked; i < limit; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            ItemStack moving = stack.Clone();
            // Establish a destination before clearing the locked source slot. If delivery
            // throws or no destination exists, the original stack remains authoritative.
            if (AddToBackpack(player, moving))
            {
                player.inventory.SetItem(i, ItemStack.Empty.Clone());
                continue;
            }
            if (player.world != null && player.world.gameManager != null)
            {
                try
                {
                    player.world.gameManager.ItemDropServer(moving, player.GetPosition(), Vector3.zero);
                    player.inventory.SetItem(i, ItemStack.Empty.Clone());
                }
                catch (Exception ex)
                {
                    Log.Warning("[REBIRTH Toolbelt] Locked-slot recovery retained source after failed drop: " + ex.Message);
                }
            }
        }
    }

    public static void SanitizePlayerStartup(EntityPlayer player, RebirthMetabolismState state, bool definitelyNewSpawn)
    {
        if (!Enabled || player == null || player.inventory == null || state == null) return;
        EnsurePhysicalCapacity(player.inventory);

        // The 3.x Duke note is a startup gate, not gameplay equipment. Remove the gate buff
        // (which also clears its pinned toolbelt message), then leave the read CVar satisfied
        // so any later startup check treats the introduction as already completed.
        if (player.Buffs != null)
        {
            player.Buffs.RemoveBuff("buffDukeNote");
            player.Buffs.SetCustomVar(".dukeNoteRead", 1f);
        }

        ItemStack[] slots = player.inventory.ItemGrid.CloneItems();
        int publicLimit = Math.Min(BackingPublicSlots, slots != null ? slots.Length : 0);
        bool startupOnly = true;
        bool hasAny = false;
        for (int i = 0; i < publicLimit; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            hasAny = true;
            string name = stack.itemValue != null && stack.itemValue.ItemClass != null ? stack.itemValue.ItemClass.GetItemName() : string.Empty;
            if (!IsStartupArtifact(name)) startupOnly = false;
        }

        bool clearAll = !state.StartupSanitized && (definitelyNewSpawn || (hasAny && startupOnly));
        for (int i = 0; i < publicLimit; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) continue;
            string name = stack.itemValue != null && stack.itemValue.ItemClass != null ? stack.itemValue.ItemClass.GetItemName() : string.Empty;
            if (clearAll || string.Equals(name, "noteDuke01", StringComparison.OrdinalIgnoreCase))
                player.inventory.SetItem(i, ItemStack.Empty.Clone());
        }

        RemoveStartupNoteFromBackpack(player);
        RecoverLockedSlots(player);
        if (!state.StartupSanitized)
        {
            state.StartupSanitized = true;
            state.Touch();
        }
    }

    private static bool IsStartupArtifact(string name)
    {
        return string.Equals(name, "noteDuke01", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "questMaster", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "q_basicSurvival", StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveStartupNoteFromBackpack(EntityPlayer player)
    {
        if (player == null || player.bag == null) return;
        ItemStack[] bag = player.bag.ItemGrid.CloneItems();
        for (int i = 0; i < bag.Length; i++)
        {
            ItemStack stack = bag[i];
            if (stack == null || stack.IsEmpty()) continue;
            string name = stack.itemValue != null && stack.itemValue.ItemClass != null ? stack.itemValue.ItemClass.GetItemName() : string.Empty;
            if (string.Equals(name, "noteDuke01", StringComparison.OrdinalIgnoreCase))
                player.bag.SetSlot(i, ItemStack.Empty.Clone());
        }
    }

    private static bool AddToBackpack(EntityPlayer player, ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return true;
        if (player == null || player.bag == null) return false;
        ItemStack[] bag = player.bag.ItemGrid.CloneItems();
        if (bag == null) return false;
        for (int i = 0; i < bag.Length; i++)
        {
            if (bag[i] == null || bag[i].IsEmpty())
            {
                player.bag.SetSlot(i, stack);
                return true;
            }
        }
        return false;
    }

}

// Allocate storage and Hand action data together. Native mode/NPC constructors retain their count.
[HarmonyPatch(typeof(Inventory), MethodType.Constructor, new Type[] { typeof(EntityAlive), typeof(int) })]
public static class RebirthToolbeltInventoryConstructorPatch
{
    public static void Prefix(EntityAlive __0, ref int __1)
    {
        if (RebirthToolbeltCapacity.Enabled && __0 is EntityPlayer)
            __1 = Math.Max(__1, RebirthToolbeltCapacity.BackingPublicSlots);
    }
}

// SetStackAt is the common native Inventory write boundary for both SetItem overloads/indexer.
[HarmonyPatch(typeof(Inventory), nameof(Inventory.SetStackAt), new Type[] { typeof(int), typeof(ItemStack) })]
public static class RebirthToolbeltSetItemAuthorityPatch
{
    public static bool Prefix(Inventory __instance, int __0, ItemStack __1)
    {
        if (!RebirthToolbeltCapacity.Enabled || !RebirthToolbeltCapacity.IsPlayerInventory(__instance)) return true;
        if (__1 == null || __1.IsEmpty()) return true; // Recovery may clear retained locked storage.
        if (RebirthToolbeltCapacity.IsRejectedIncoming(__1)) return false;
        return !RebirthToolbeltCapacity.IsLockedPlayerSlot(__instance, __0);
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.CanMoveToSlot), new Type[] { typeof(ItemStack), typeof(int) })]
public static class RebirthToolbeltCanMoveToSlotPatch
{
    public static bool Prefix(Inventory __instance, ItemStack __0, int __1, ref bool __result)
    {
        if (!RebirthToolbeltCapacity.IsLockedPlayerSlot(__instance, __1) &&
            !(RebirthToolbeltCapacity.IsPlayerInventory(__instance) && RebirthToolbeltCapacity.IsRejectedIncoming(__0))) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItemAtSlot), new Type[] { typeof(ItemStack), typeof(int) })]
public static class RebirthToolbeltAddItemAtSlotAuthorityPatch
{
    public static bool Prefix(Inventory __instance, ItemStack __0, int __1, ref bool __result)
    {
        if (!RebirthToolbeltCapacity.IsLockedPlayerSlot(__instance, __1) &&
            !(RebirthToolbeltCapacity.IsPlayerInventory(__instance) && RebirthToolbeltCapacity.IsRejectedIncoming(__0))) return true;
        __result = false;
        return false;
    }
}

// Every AddItem overload delegates here. Constrain native stacking AND empty-slot search
// before mutation so native callers cannot report success for a rejected hidden-slot write.
[HarmonyPatch]
public static class RebirthToolbeltAddItemAuthorityPatch
{
    public static MethodBase TargetMethod() => AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem),
        new[] { typeof(ItemStack), typeof(int), typeof(int), typeof(int).MakeByRefType() });
    public static bool Prefix(Inventory __instance, ItemStack __0, ref int __1, ref int __2, ref int __3, ref bool __result)
    {
        if (!RebirthToolbeltCapacity.Enabled || !(RebirthToolbeltCapacity.ResolveOwner(__instance) is EntityPlayer player)) return true;
        if (RebirthToolbeltCapacity.IsRejectedIncoming(__0))
        { __3 = -1; __result = false; return false; }
        int usable = RebirthToolbeltCapacity.GetOwnedSlotCount(player, __instance.Length);
        __1 = Math.Max(0, Math.Min(__1, usable));
        __2 = Math.Max(0, Math.Min(__2, usable - __1));
        return true;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.SetSelectedSlot), new Type[] { typeof(int) })]
public static class RebirthToolbeltSelectedSlotPatch
{
    public static void Prefix(Inventory __instance, ref int __0)
    {
        if (!RebirthToolbeltCapacity.Enabled || !(RebirthToolbeltCapacity.ResolveOwner(__instance) is EntityPlayer player)) return;
        int count = RebirthToolbeltCapacity.GetOwnedSlotCount(player, __instance.Length);
        if (__0 < 0 || __0 >= count) __0 = 0;
    }
}

[HarmonyPatch(typeof(Hand), nameof(Hand.SelectSlot), new Type[] { typeof(int), typeof(bool) })]
public static class RebirthToolbeltHandSelectPatch
{
    public static bool Prefix(Hand __instance, int __0)
    {
        if (!RebirthToolbeltCapacity.Enabled || !(__instance.entity is EntityPlayer player)) return true;
        Inventory toolbelt = __instance.toolbelt;
        return toolbelt == null || (__0 >= 0 && __0 < RebirthToolbeltCapacity.GetOwnedSlotCount(player, toolbelt.Length));
    }
}

// REBIRTH renders one physical page with two number-key banks. Do not change the
// shared native ToolbeltConfig: native-mode worlds keep their own pages/bars.
[HarmonyPatch(typeof(XUiC_Toolbelt), nameof(XUiC_Toolbelt.ResolveShortcutSlot))]
public static class RebirthToolbeltShortcutPatch
{
    public static void Postfix(int __0, bool __1, ref int __result)
    {
        if (RebirthToolbeltCapacity.Enabled && __0 >= 0)
            __result = __0 + (__1 ? 10 : 0);
    }
}

[HarmonyPatch(typeof(XUiC_Toolbelt), nameof(XUiC_Toolbelt.WrapFocusWithinPage))]
public static class RebirthToolbeltFocusWrapPatch
{
    public static bool Prefix(XUiC_Toolbelt __instance, int __0, int __1, ref int __result)
    {
        if (!RebirthToolbeltCapacity.Enabled) return true;
        EntityPlayer player = __instance.xui?.playerUI?.entityPlayer;
        if (player?.inventory == null) return true;
        int count = RebirthToolbeltCapacity.GetOwnedSlotCount(player, player.inventory.Length);
        if (count <= 0) { __result = __0; return false; }
        long next = (long)__0 + __1;
        __result = (int)((next % count + count) % count);
        return false;
    }
}
public static class RebirthToolbeltCapacityInstaller
{
    private static readonly Harmony Harmony = new Harmony("rebirth.toolbelt.capacity.3.1");
    private static bool installed;
    public static string Install()
    {
        if (installed) return "[REBIRTH Toolbelt] capacity patch already installed.";
        foreach (Type patch in new[] {
            typeof(RebirthToolbeltInventoryConstructorPatch), typeof(RebirthToolbeltSetItemAuthorityPatch),
            typeof(RebirthToolbeltCanMoveToSlotPatch), typeof(RebirthToolbeltAddItemAtSlotAuthorityPatch),
            typeof(RebirthToolbeltAddItemAuthorityPatch), typeof(RebirthToolbeltSelectedSlotPatch),
            typeof(RebirthToolbeltHandSelectPatch), typeof(RebirthToolbeltShortcutPatch),
            typeof(RebirthToolbeltFocusWrapPatch) })
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, patch);
        installed = true;
        return "[REBIRTH Toolbelt] installed native ItemGrid capacity: 4 base + equipment/background, max18 usable; 20 real recovery slots; native Hand transient use retained.";
    }
}