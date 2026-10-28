using System;
using HarmonyLib;

// Entity caches the whole radial command array. Permission checks cannot add
// commands missing from that array after the body modifier is installed.
internal static class RebirthCamperRadialCache
{
    internal static bool NeedsRebuild(EntityActivationCommand[] commands, bool hasBody, bool energyRegistered)
    {
        if (commands == null) return false; // Native code will build it.
        int found = 0;
        foreach (var command in commands)
        {
            // Retire the old camper-only radial Refuel identity once; native
            // vehicle-window refueling and its permission checks are unchanged.
            if (command.commandId == "refuel") return true;
            if (command.commandId == RebirthCamperRadial.Storage) found |= 1;
            else if (command.commandId == RebirthCamperRadial.Stations) found |= 2;
            else if (command.commandId == RebirthCamperRadial.Turrets) found |= 4;
            else if (command.commandId == RebirthCamperRadial.Craft) found |= 8;
            else if (command.commandId == RebirthCamperRadial.Energy) found |= 16;
            else if (command.commandId == RebirthCamperRadial.PoiOn) found |= 32;
            else if (command.commandId == RebirthCamperRadial.PoiOff) found |= 64;
        }
        // Identities are registered even before the body is restored. Current
        // body/access eligibility is evaluated by native UpdateActivationCommands.
        // POI actions were retired from the radial; expecting them here rebuilt
        // the evaluated array on SetCurrentEntityData's second Get call and reset
        // every native command's enabled flag to its constructor default (true).
        int expected = energyRegistered ? 31 : 15;
        return found != expected;
    }
}

[HarmonyPatch(typeof(Entity), nameof(Entity.GetActivationCommands))]
internal static class RebirthCamperRadialRefresh
{
    private static void Prefix(Entity __instance)
    {
        var vehicle = __instance as EntityVehicle;
        if (!RebirthCamperRadial.IsCamper(vehicle)) return;
        if (!RebirthCamperRadialCache.NeedsRebuild(vehicle.activationCommands,
            RebirthCamperBodyAttachment.HasBody(vehicle), RebirthCamperEnergyAccess.Registered)) return;
        vehicle.activationCommands = null;
        // Also expire the native per-frame permission result for the old array.
        vehicle.lastUpdateFrameOfActivationCommands = -1;
    }
}
