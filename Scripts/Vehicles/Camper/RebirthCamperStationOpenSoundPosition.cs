using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

// Keep the native workstation's selected sound and single broadcast. Detached
// stations have no chunk, so resolve only their admitted audio position here,
// without depending on a generic ToWorldPos detour at an inlinable call site.
[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), nameof(XUiC_WorkstationWindowGroup.OnOpen))]
internal static class RebirthCamperStationOpenSoundPosition
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var broadcast = AccessTools.DeclaredMethod(typeof(Audio.Manager), nameof(Audio.Manager.BroadcastPlayByLocalPlayer),
            new[] { typeof(Vector3), typeof(string) });
        var openSound = AccessTools.Field(typeof(WorkstationData), nameof(WorkstationData.OpenSound));
        var replacement = AccessTools.DeclaredMethod(typeof(RebirthCamperStationOpenSoundPosition), nameof(Broadcast));
        if (broadcast == null || broadcast.ReturnType != typeof(void) || openSound == null || replacement == null)
            throw new InvalidOperationException("Native workstation open sound signature changed.");
        var codes = instructions.Select(code => new CodeInstruction(code)).ToList();
        int calls = 0, fields = 0, at = -1;
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldfld && Equals(codes[i].operand, openSound)) fields++;
            if (codes[i].opcode != OpCodes.Call || !Equals(codes[i].operand, broadcast)) continue;
            calls++; at = i;
        }
        // Qualify the exact installed 3.4 call site, not another audio call or
        // a guessed method shape. A changed body must be reviewed before use.
        if (calls != 1 || fields != 1 || at < 1 || codes[at - 1].opcode != OpCodes.Ldfld
            || !Equals(codes[at - 1].operand, openSound))
            throw new InvalidOperationException("Expected one native workstation OpenSound broadcast call.");
        var window = new CodeInstruction(OpCodes.Ldarg_0);
        window.labels.AddRange(codes[at].labels); codes[at].labels.Clear();
        window.blocks.AddRange(codes[at].blocks); codes[at].blocks.Clear();
        codes[at].operand = replacement;
        codes.Insert(at, window);
        return codes;
    }

    internal static Vector3 Position(Vector3 incoming, XUiC_WorkstationWindowGroup window)
    {
        try
        {
            if (!ThreadManager.IsMainThread() || window == null
                || !(window.WorkstationData?.TileEntity is RebirthCamperStationState station)) return incoming;
            var vehicle = station.BoundVehicle;
            var world = vehicle?.world;
            var player = window.xui?.playerUI?.entityPlayer;
            if (world == null || player == null || !ReferenceEquals(GameManager.Instance?.World, world)
                || !ReferenceEquals(player.world, world) || player.IsDead()
                || !ReferenceEquals(world.GetEntity(vehicle.entityId), vehicle)
                || !RebirthCamperModuleInteraction.Current(vehicle, player)
                || !RebirthCamperStationWindowSession.Admitted(window, station)
                || !RebirthCamperStationWindowSession.TryGetCurrent(player, out var currentStation)
                || !ReferenceEquals(currentStation, station)) return incoming;
            var tile = new Vector3i(Mathf.FloorToInt(vehicle.position.x),
                Mathf.FloorToInt(vehicle.position.y), Mathf.FloorToInt(vehicle.position.z));
            return tile.ToVector3() + Vector3.one * .5f;
        }
        catch { return incoming; } // Unknown/stale presentation retains native position.
    }

    internal static void Broadcast(Vector3 incoming, string sound, XUiC_WorkstationWindowGroup window)
        => Audio.Manager.BroadcastPlayByLocalPlayer(Position(incoming, window), sound);
}
