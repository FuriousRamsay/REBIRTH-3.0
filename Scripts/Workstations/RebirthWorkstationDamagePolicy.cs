using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>Protects placed stations before native damage, including multiblock children.</summary>
[Preserve]
public sealed class RebirthWorkstationDamageModApi : IModApi
{
    public void InitMod(Mod modInstance) { RebirthWorkstationDamagePolicy.Install(); }
}

internal static class RebirthWorkstationDamagePolicy
{
    private static bool installed;
    internal static void Install()
    {
        if (installed) return;
        var harmony = new HarmonyLib.Harmony("rebirth.workstations.damage");
        var prefix = new HarmonyMethod(typeof(RebirthWorkstationDamagePolicy), nameof(BeforeDamage));
        prefix.priority = Priority.First;
        var seen = new HashSet<MethodBase>();
        try
        {
            foreach (var assembly in new[] { typeof(Block).Assembly, typeof(RebirthWorkstationDamagePolicy).Assembly })
            foreach (var type in assembly.GetTypes())
            {
                if (!typeof(Block).IsAssignableFrom(type)) continue;
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.Name != "DamageBlock" && method.Name != "OnBlockDamaged") continue;
                    var p = method.GetParameters();
                    if (method.ReturnType != typeof(int) || p.Length < 5 || p[0].ParameterType != typeof(WorldBase) ||
                        p[1].ParameterType != typeof(BlockValueRef) || p[2].ParameterType != typeof(BlockValue) ||
                        p[3].ParameterType != typeof(int) || p[4].ParameterType != typeof(int)) continue;
                    if (seen.Add(method))
                    {
                        harmony.Patch(method, prefix: prefix);
                        if (method.Name == "OnBlockDamaged")
                            harmony.Patch(method,
                                prefix: new HarmonyMethod(typeof(RebirthWorkstationSalvage), nameof(RebirthWorkstationSalvage.BeforeDamage)),
                                finalizer: new HarmonyMethod(typeof(RebirthWorkstationSalvage), nameof(RebirthWorkstationSalvage.AfterDamage)));
                    }
                }
            }
            if (seen.Count == 0) throw new InvalidOperationException("Native station damage targets unavailable.");
            harmony.Patch(AccessTools.Method(typeof(BlockWorkstation), nameof(BlockWorkstation.PlaceBlock)),
                postfix: new HarmonyMethod(typeof(RebirthWorkstationDamagePolicy), nameof(Placed)));
            harmony.Patch(AccessTools.Method(typeof(BlockWorkstation), nameof(BlockWorkstation.OnBlockRemoved)),
                postfix: new HarmonyMethod(typeof(RebirthWorkstationDamagePolicy), nameof(Removed)));
            RebirthWorldStationMigration.Install(harmony);
            RebirthWorkstationExplosionProtection.Install(harmony);
            installed = true;
        }
        catch { harmony.UnpatchSelf(); throw; }
    }

    private static void Placed(WorldBase __0,BlockPlacement.Result __1,EntityAlive __2)
    {
        // Normal placement is already registered by BlockPickup; its diagnostic-off mode
        // must not turn ownership protection into a permanent unknown-owner lockout.
        if(!RebirthBlockPickupPatchInstaller.Active)RebirthWorkstationSecurityService.RegisterPlaced(__0,__1.blockPos,__2);
    }
    private static void Removed(Vector3i __2)
    {if(!RebirthBlockPickupPatchInstaller.Active)RebirthWorkstationSecurityService.Remove(__2);}
    internal static bool BeforeDamage(WorldBase __0, BlockValueRef __1, BlockValue __2,
        int __3, int __4, ref int __result)
    {
        if (__3 <= 0 || __0 == null) return true; // Native repairs are negative damage.
        Vector3i position = __1.BlockPosition;
        BlockValue value = __2;
        if (value.ischild && value.Block?.multiBlockPos != null)
        {
            position = value.Block.multiBlockPos.GetParentPos(position, value);
            value = __0.GetBlock(position);
        }
        if (!(value.Block is BlockWorkstation)) return true;
        var station = __0.GetTileEntity(position) as TileEntityWorkstation;
        // An existing ownership record also protects a station with an unavailable tile entity.
        PlatformUserIdentifierAbs owner;
        bool recorded = RebirthWorkstationSecurityService.TryGetOwner(position, out owner);
        if (!recorded && station != null && !station.IsPlayerPlaced) return true;
        var actor = __0.GetEntity(__4);
        var player = actor as EntityPlayer ?? (actor as EntityVehicle)?.GetFirstAttached() as EntityPlayer;
        if (player == null) return true; // Environmental/zombie damage retains native behavior.
        if (player.IsGodMode.Value) return true;
        var user = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
        if (recorded && RebirthWorkstationSecurityService.IsOwner(position, user)) return true;
        __result = 0;
        return false; // Unknown ownership fails closed; access/admin status never implies ownership.
    }
}
