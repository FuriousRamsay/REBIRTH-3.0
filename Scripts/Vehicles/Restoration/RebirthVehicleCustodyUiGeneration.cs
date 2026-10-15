using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static class RebirthVehicleCustodyUiGeneration
{
    private sealed class Stamp { internal long Value; }
    private static readonly ConditionalWeakTable<XUiWindowGroup,Stamp> stamps=new ConditionalWeakTable<XUiWindowGroup,Stamp>();
    internal static long Read(XUiWindowGroup group)=>group==null?-1:stamps.GetValue(group,_=>new Stamp()).Value;
    internal static void Install(Harmony harmony)
    {
        foreach(var name in new[]{"OnOpen","OnClose","Cleanup"})
            if(AccessTools.DeclaredMethod(typeof(XUiWindowGroup),name,Type.EmptyTypes)==null)throw new MissingMethodException("Native window lifecycle unavailable: "+name);
        foreach(var name in new[]{"OnOpen","OnClose","Cleanup"})
            harmony.Patch(AccessTools.DeclaredMethod(typeof(XUiWindowGroup),name,Type.EmptyTypes),prefix:new HarmonyMethod(typeof(RebirthVehicleCustodyUiGeneration),nameof(Change)));
    }
    private static void Change(XUiWindowGroup __instance)
    {if(__instance!=null)stamps.GetValue(__instance,_=>new Stamp()).Value++;}
}
