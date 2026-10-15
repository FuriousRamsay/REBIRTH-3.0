using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// The native attached-player Activate path detaches on press. For camper
// passengers defer that tap until release, allowing its native radial on hold.
[HarmonyPatch(typeof(PlayerMoveController),"HandleInteraction")]
internal static class RebirthCamperPassengerRadial
{
    private sealed class Press {internal EntityVehicle Vehicle;internal float Started;internal bool Pending,Opened;}
    private static readonly ConditionalWeakTable<PlayerMoveController,Press> presses=new ConditionalWeakTable<PlayerMoveController,Press>();
    private static void Prefix(PlayerMoveController __instance,ref bool _activate)
    {
        var player=__instance.entityPlayerLocal;
        var vehicle=player?.AttachedToEntity as EntityVehicle;
        var state=presses.GetValue(__instance,_=>new Press());
        if(!RebirthCamperRadial.IsCamper(vehicle)||RebirthCamperRadial.IsDriver(vehicle,player)
            ||player.IsDead()||player.PlayerUI?.windowManager?.IsInputActive()!=false)
        {state.Pending=false;state.Opened=false;state.Vehicle=null;return;}
        if(state.Vehicle!=vehicle){state.Pending=false;state.Opened=false;state.Vehicle=vehicle;}
        var actions=player.playerInput;
        bool held=actions.Activate.IsPressed||actions.VehicleActions.Activate.IsPressed||actions.PermanentActions.Activate.IsPressed;
        if(_activate){state.Pending=true;state.Opened=false;state.Started=Time.realtimeSinceStartup;_activate=false;}
        if(!state.Pending)return;
        _activate=false;
        if(held&&Time.realtimeSinceStartup-state.Started>=.35f&&!state.Opened)
        {
            state.Opened=true;
            var radial=player.PlayerUI.xui.RadialWindow;
            radial.Open();radial.SetCurrentEntityData(vehicle,player);
        }
        if(!held)
        {
            bool detach=!state.Opened;state.Pending=false;state.Opened=false;
            if(detach)player.SendDetach();
        }
    }
}
