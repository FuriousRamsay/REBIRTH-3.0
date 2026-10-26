using System;
using System.Collections.Generic;
using HarmonyLib;

public static class RebirthCamperRadial
{
    public const string Storage = "rebirthCamperStorageManagement";
    public const string Stations = "rebirthCamperStationManagement";
    public const string Craft = "rebirthCamperUseStation";
    public const string Turrets = "rebirthCamperTurrets";
    public const string Energy = "rebirthCamperEnergyManagement";
    public const string PoiOn = "rebirthCamperPoiLightsOn";
    public const string PoiOff = "rebirthCamperPoiLightsOff";
    internal static bool IsPoiCommand(ReadOnlySpan<char> command)=>Matches(command,PoiOn)||Matches(command,PoiOff);
    // Registered once by the completed management/session coordinator.
    internal static Action<EntityVehicle, EntityPlayerLocal, string> OpenSession { get; set; }
    internal static bool IsCamper(EntityVehicle vehicle) => vehicle?.vehicle != null
        && vehicle.vehicle.GetName().Equals("rebirthCamper80s", StringComparison.OrdinalIgnoreCase);
    internal static bool IsDriver(EntityVehicle vehicle,EntityPlayer player)
        =>vehicle!=null&&player!=null&&ReferenceEquals(vehicle.GetAttached(0),player);
    internal static bool IsCommand(ReadOnlySpan<char> command) => Matches(command, Storage)
        || Matches(command, Stations) || Matches(command, Turrets) || Matches(command, Craft) || Matches(command, Energy)||IsPoiCommand(command);
    private static bool Matches(ReadOnlySpan<char> command, string name)
    {
        if (command.Length != name.Length) return false;
        for (int index = 0; index < command.Length; index++) if (command[index] != name[index]) return false;
        return true;
    }
    internal static bool Allowed(EntityVehicle vehicle, EntityPlayerLocal player) => OpenSession != null
        && IsCamper(vehicle) && RebirthCamperBodyAttachment.HasBody(vehicle) && player != null && !player.IsDead() && ReferenceEquals(vehicle.world, player.world)
        && vehicle.world != null && (!vehicle.world.IsRemote() || RebirthCamperRemoteTransport.IsRegistered) 
        && (RebirthCamperModuleInteraction.Current(vehicle,player)
            ||RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle)<0
                &&!LockManager.Instance.IsLockedByLocalPlayer(vehicle,0))
        && (player.position - vehicle.position).sqrMagnitude <= 64f
        && vehicle.AllowActivationCommand("service", player);
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.InitLocalActivationCommands))]
public static class RebirthCamperRadialCommands
{
    private static void Prefix(EntityVehicle __instance, ref Action<EntityActivationCommand> __0)
    {
        if(!RebirthCamperRadial.IsCamper(__instance)||!RebirthCamperBodyAttachment.HasBody(__instance))return;
        var add=__0;
        __0=command=>{if(command.commandId!="repair"&&command.commandId!="rebirthCamperNativeVehicleEditor")add(command);};
    }

    private static void Postfix(EntityVehicle __instance, Action<EntityActivationCommand> __0)
    {
        if (!RebirthCamperRadial.IsCamper(__instance)||!RebirthCamperBodyAttachment.HasBody(__instance)) return;
        __0(Command(RebirthCamperRadial.Storage, "purgesupplies", "xuiRebirthCamperStorageManagement"));
        __0(Command(RebirthCamperRadial.Stations, "workbench", "xuiRebirthCamperStationManagement"));
        __0(Command(RebirthCamperRadial.Craft, "hammer", "xuiRebirthCamperUseStation"));
        if(RebirthCamperEnergyAccess.Registered)
        {
            __0(Command(RebirthCamperRadial.PoiOn,"lightbulb","xuiRebirthCamperEnergyPoiOn"));
            __0(Command(RebirthCamperRadial.PoiOff,"lightbulb","xuiRebirthCamperEnergyPoiOff"));
            __0(Command(RebirthCamperRadial.Energy,"electric_switch","xuiRebirthCamperEnergyManagement"));
        }
        __0(Command(RebirthCamperRadial.Turrets,"junk_turret","xuiCamperTurretManagement"));
    }

    private static EntityActivationCommand Command(string id, string icon, string localizationKey)
    {
        // Native constructor prefixes its custom text with entitycommand_. Assign the
        // resolved XUi label afterwards; these keys are shared with the native windows.
        return new EntityActivationCommand(id, icon, null, null)
        { commandText = Localization.Get(localizationKey) };
    }
}

[HarmonyPatch(typeof(EntityVehicle), "ReorderActivationCommands")]
internal static class RebirthCamperRadialStorageOrder
{
    private static void Postfix(EntityVehicle __instance, List<EntityActivationCommand> __0)
    {
        if (!RebirthCamperRadial.IsCamper(__instance) || __0 == null) return;
        // service is the native Open action for installing parts. Only repair
        // is the redundant wrench; also filter it at initial registration.
        __0.RemoveAll(command => command.commandId == "repair" || command.commandId == "rebirthCamperNativeVehicleEditor");
        int storage = __0.FindIndex(command => command.commandId == "storage");
        int management = __0.FindIndex(command => command.commandId == RebirthCamperRadial.Storage);
        // Swap complete actions after the native owner-specific reorder, retaining
        // each action's icon, permission and destination. Other vehicles are untouched.
        if (storage >= 0 && management >= 0)
        {
            var action = __0[storage];
            __0[storage] = __0[management];
            __0[management] = action;
        }
        // The native radial starts at Drive and lays subsequent commands
        // clockwise. Keep the custom left-side run at the end of the list:
        // Stations, Craft, Energy, Turrets, then wrap back to Drive.
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.AllowActivationCommand))]
public static class RebirthCamperRadialPermission
{
    private static bool Prefix(EntityVehicle __instance, ReadOnlySpan<char> __0, EntityPlayerLocal __1, ref bool __result)
    {
        if(RebirthCamperRadial.IsCamper(__instance)&&__0.SequenceEqual("storage".AsSpan())
            &&(RebirthCamperModuleInteraction.Current(__instance,__1)
                ||RebirthCamperRemoteAdmission.NativeInteractionOwner(__instance)>=0))
        {__result=false;return false;}
        if (!RebirthCamperRadial.IsCommand(__0)) return true;
        if(__0.SequenceEqual(RebirthCamperRadial.Turrets.AsSpan()))
        {__result=RebirthCamperTurretAccess.CanUse(__instance,__1);return false;}
        if(RebirthCamperRadial.IsPoiCommand(__0))
        {
            __result=RebirthCamperEnergyPoiAccess.Allowed(__instance,__1)&&
                RebirthCamperEnergyDeviceAdapter.TryObservePoiOn(__instance,out bool on)&&
                __0.SequenceEqual((on?RebirthCamperRadial.PoiOff:RebirthCamperRadial.PoiOn).AsSpan())&&
                (on||RebirthCamperEnergyDeviceAdapter.CanTurnPoiLightsOn(__instance));
            return false;
        }
        if(__0.SequenceEqual(RebirthCamperRadial.Energy.AsSpan())&&!RebirthCamperEnergyAccess.Registered){__result=false;return false;}
        if(RebirthCamperModuleInteraction.Current(__instance,__1))
        {__result=false;return false;}
        if (__instance.world != null && __instance.world.IsRemote() && MatchesCraft(__0))
        { __result = RebirthCamperRemoteStationClient.IsReady && RebirthCamperRemoteStationTransport.IsRegistered
            && RebirthCamperRadial.Allowed(__instance,__1); return false; }
        __result = RebirthCamperRadial.Allowed(__instance, __1);
        return false;
    }
    private static bool MatchesCraft(ReadOnlySpan<char> command) => command.SequenceEqual(RebirthCamperRadial.Craft.AsSpan());
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.OnEntityActivated))]
public static class RebirthCamperRadialActivate
{
    private static bool Prefix(EntityVehicle __instance, EntityActivationCommand __0, EntityPlayerLocal __1)
    {
        if(RebirthCamperRadial.IsCamper(__instance)&&__0.commandId=="storage"&&!__instance.world.IsRemote())
        {
            try { RebirthCamperModuleRuntime.For(__instance).InitializeEmptyCargo(); }
            catch(Exception error)
            {
                Log.Warning("[CamperStorage] Native storage opening refused until cargo is reconciled: "+error.Message);
                GameManager.ShowTooltip(__1,Localization.Get("xuiRebirthCamperTransferRecover"));
                return false;
            }
        }
        if (!RebirthCamperRadial.IsCommand(__0.commandId)) return true;
        if(__0.commandId==RebirthCamperRadial.Turrets)
        {
            if(RebirthCamperTurretAccess.CanUse(__instance,__1))RebirthCamperTurretAccess.Open(__instance,__1);
            return false;
        }
        if(RebirthCamperRadial.IsPoiCommand(__0.commandId))
        {
            RebirthCamperEnergyPoiAccess.Request(__instance,__1,__0.commandId==RebirthCamperRadial.PoiOn);
            return false;
        }
        if (RebirthCamperRadial.Allowed(__instance, __1)) RebirthCamperModuleInteraction.Request(__instance, __1, __0.commandId);
        return false;
    }
}
