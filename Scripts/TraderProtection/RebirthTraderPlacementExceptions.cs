using System;
using HarmonyLib;

#nullable disable

/// <summary>
/// Preserves the REBIRTH 2.6 block-placement exceptions inside protected trader areas
/// while leaving the native 3.0 TraderProtection option authoritative.
/// </summary>
public static class RebirthTraderPlacementExceptions
{
    public static bool IsAllowedPlacementBlock(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return false;

        return blockName.IndexOf("furiousramsayspawncube", StringComparison.OrdinalIgnoreCase) >= 0
            || string.Equals(blockName, "walltorchlightplayer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(blockName, "meleetooltorch", StringComparison.OrdinalIgnoreCase)
            || string.Equals(blockName, "gunbott1junksledge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(blockName, "gunbott2junkturret", StringComparison.OrdinalIgnoreCase)
            || string.Equals(blockName, "gunbott3junkdrone", StringComparison.OrdinalIgnoreCase)
            || string.Equals(blockName, "workbenchtoolbox001_fr", StringComparison.OrdinalIgnoreCase)
            || blockName.IndexOf("frameshapes", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsTraderProtectionEnabled()
    {
        EnumGamePrefs traderProtection;
        if (!Enum.TryParse("TraderProtection", true, out traderProtection))
            return true;

        return GamePrefs.GetBool(traderProtection);
    }

    public static void ApplyTraderAllowance(
        World world,
        Vector3i blockPos,
        PersistentPlayerData playerData,
        ref bool traderAllowed)
    {
        if (world == null || playerData == null || traderAllowed)
            return;

        if (!IsTraderProtectionEnabled())
            return;

        if (world.GetTraderAreaAt(blockPos) == null)
            return;

        EntityPlayer player = world.GetEntity(playerData.EntityId) as EntityPlayer;
        if (player == null || player.inventory == null || player.inventory.holdingItem == null)
            return;

        string blockName = player.inventory.holdingItem.GetItemName();
        if (!IsAllowedPlacementBlock(blockName))
            return;

        // This is the only native predicate input REBIRTH owns. The original
        // World.CanPlaceBlockAt method still runs and therefore retains collision,
        // claim, authority and every other placement check.
        traderAllowed = true;
    }

}

public static class RebirthTraderPlacementExceptionsInstaller
{
    private static bool s_installed;

    public static void Install()
    {
        if (s_installed)
            return;

        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.trader-placement-exceptions.3.1"), typeof(RebirthTraderPlacementPatch));
        s_installed = true;
        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH] Trader placement exceptions installed."); }
    }
}

[HarmonyPatch(typeof(World), nameof(World.CanPlaceBlockAt), new[]
{
    typeof(Vector3i), typeof(PersistentPlayerData), typeof(bool)
})]
internal static class RebirthTraderPlacementPatch
{
    private static void Prefix(
        World __instance,
        Vector3i blockPos,
        PersistentPlayerData lpRelative,
        ref bool traderAllowed)
    {
        RebirthTraderPlacementExceptions.ApplyTraderAllowance(
            __instance, blockPos, lpRelative, ref traderAllowed);
    }
}
