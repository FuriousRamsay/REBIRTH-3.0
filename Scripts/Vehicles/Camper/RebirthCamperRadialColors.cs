using System;
using HarmonyLib;
using UnityEngine;

// Native entity entries are created white on every build. Retain native labels
// and highlights, applying the requested icon colors only to this camper.
[HarmonyPatch(typeof(XUiC_Radial),nameof(XUiC_Radial.SetCurrentEntityData))]
internal static class RebirthCamperRadialColors
{
    private static readonly Color Management=new Color32(255,220,80,255);
    private static readonly Color Action=new Color32(181,140,255,255);
    private static void Postfix(XUiC_Radial __instance,Entity _entity)
    {
        if(!RebirthCamperRadial.IsCamper(_entity as EntityVehicle)||
            !(__instance.context is XUiC_Radial.RadialContextEntity context)||
            !ReferenceEquals(context.EntityFocused,_entity)||__instance.menuItem==null)return;
        foreach(var entry in __instance.menuItem)
        {
            if(entry==null||entry.CommandIndex<0||entry.CommandIndex>=context.Commands.Length)continue;
            var command=context.Commands[entry.CommandIndex];
            var id=command.commandId.AsSpan();
            bool management=id.SequenceEqual(RebirthCamperRadial.Storage.AsSpan())||
                id.SequenceEqual(RebirthCamperRadial.Stations.AsSpan())||id.SequenceEqual(RebirthCamperRadial.Energy.AsSpan());
            bool action=id.SequenceEqual("service".AsSpan())||id.SequenceEqual("storage".AsSpan())||
                id.SequenceEqual(RebirthCamperRadial.Craft.AsSpan())||RebirthCamperRadial.IsPoiCommand(id);
            if(management||action)entry.SetSprite("ui_game_symbol_"+command.icon,management?Management:Action);
        }
    }
}
