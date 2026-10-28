using System;
using HarmonyLib;
using UnityEngine;

// The compact Energy window replaces native powersource presentation. Reuse
// that window's existing open cue and network-aware local-player broadcast.
// Crafting stations already reach native workstation OnOpen; never play twice.
[HarmonyPatch(typeof(RebirthCamperEnergyAccess), "Open")]
internal static class RebirthCamperCompactBankOpenSound
{
    internal const string NativeOpenSound = "open_vending";

    private static void Prefix(EntityPlayerLocal __1, out bool __state)
    {
        __state = false;
        if (!ThreadManager.IsMainThread() || __1 == null) return;
        var ui = LocalPlayerUI.GetUIForPlayer(__1);
        __state = ui?.windowManager != null && !ui.windowManager.IsWindowOpen(RebirthCamperRadial.Energy);
    }

    private static void Postfix(EntityVehicle __0, EntityPlayerLocal __1, bool __state)
    {
        // Harmony does not run this postfix when opening throws. The prefix
        // also excludes attempts to reopen an already-visible Energy window.
        if (!__state || !ThreadManager.IsMainThread() || __0 == null || __1 == null
            || __0.world == null || !ReferenceEquals(__0.world, __1.world)) return;
        try
        {
            if (!RebirthCamperModuleInteraction.Current(__0, __1)) return;
            var ui = LocalPlayerUI.GetUIForPlayer(__1);
            if (ui?.windowManager?.IsWindowOpen(RebirthCamperRadial.Energy) != true) return;
            // The normal native power window broadcasts from the tile centre.
            // A compact source has no world tile; use its camper's tile centre.
            var tile = new Vector3i(Mathf.FloorToInt(__0.position.x),
                Mathf.FloorToInt(__0.position.y), Mathf.FloorToInt(__0.position.z));
            Audio.Manager.BroadcastPlayByLocalPlayer(tile.ToVector3() + Vector3.one * .5f, NativeOpenSound);
        }
        catch (Exception error)
        {
            // Audio presentation must not roll back an admitted inventory UI.
            Log.Warning("[CamperOpenSound] Native bank open cue unavailable: " + error.Message);
        }
    }
}
