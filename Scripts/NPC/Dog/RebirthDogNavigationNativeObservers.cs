using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;

public static partial class RebirthDogNavigationMarkerService
{
    private const string MarkerObserverOwner="rebirth.npc.dog.marker.original-observation.v1";
    private static bool installingMarkerObservers;
    private sealed class MarkerRemovalState
    {internal World World;internal NavObjectManager Manager;internal EntityPlayerLocal Owner;internal Dictionary<string,NavObject> Rows;}
    private static void InstallMarkerObservers()
    {
        if(nativeMarkerObserverReady||installingMarkerObservers||!ThreadManager.IsMainThread())return;
        installingMarkerObservers=true;
        try
        {
            var harmony=new Harmony(MarkerObserverOwner);
            PatchMarkerObserver(harmony,AccessTools.Method(typeof(NavObjectManager),nameof(NavObjectManager.UnRegisterNavObject),new[]{typeof(NavObject)}),nameof(BeforeNativeMarkerUnregister),nameof(AfterNativeMarkerRemoval));
            PatchMarkerObserver(harmony,AccessTools.Method(typeof(NavObjectManager),nameof(NavObjectManager.Cleanup),Type.EmptyTypes),nameof(BeforeNativeMarkerCleanup),nameof(AfterNativeMarkerRemoval));
            PatchMarkerObserver(harmony,AccessTools.Method(typeof(NavObject),nameof(NavObject.Reset),new[]{typeof(string)}),nameof(BeforeNativeMarkerReset),null);
            nativeMarkerObserverReady=true;
        }
        catch(Exception ex){Log.Warning("[REBIRTH Dog Marker] original native observer install incomplete: "+ex.GetType().Name);}
        finally{installingMarkerObservers=false;}
    }
    private static void PatchMarkerObserver(Harmony harmony,MethodInfo original,string prefix,string postfix)
    {
        if(original==null)throw new MissingMethodException("Native marker observer target missing");
        var info=Harmony.GetPatchInfo(original);var pre=AccessTools.Method(typeof(RebirthDogNavigationMarkerService),prefix);var post=postfix==null?null:AccessTools.Method(typeof(RebirthDogNavigationMarkerService),postfix);
        bool hasPre=info?.Prefixes.Any(p=>p.owner==MarkerObserverOwner&&p.PatchMethod==pre)==true,hasPost=post==null||info?.Postfixes.Any(p=>p.owner==MarkerObserverOwner&&p.PatchMethod==post)==true;
        if(!hasPre||!hasPost)harmony.Patch(original,hasPre?null:new HarmonyMethod(pre),hasPost?null:new HarmonyMethod(post));
        info=Harmony.GetPatchInfo(original);
        if(info?.Prefixes.Any(p=>p.owner==MarkerObserverOwner&&p.PatchMethod==pre)!=true||(post!=null&&info?.Postfixes.Any(p=>p.owner==MarkerObserverOwner&&p.PatchMethod==post)!=true))throw new InvalidOperationException("Native marker observer installation unproved");
    }
    private static void BeforeNativeMarkerUnregister(NavObjectManager __instance,NavObject __0,out MarkerRemovalState __state)
    {__state=null;try{__state=CaptureMarkerRemoval(__instance,__0,false);}catch{UnknownMarkerObservation(__instance);}}
    private static void BeforeNativeMarkerCleanup(NavObjectManager __instance,out MarkerRemovalState __state)
    {__state=null;try{__state=CaptureMarkerRemoval(__instance,null,true);}catch{UnknownMarkerObservation(__instance);}}
    private static void UnknownMarkerObservation(NavObjectManager manager)
    {lock(Sync)if(ReferenceEquals(manager,markerManager))markerPublicationUncertain=true;}
    private static MarkerRemovalState CaptureMarkerRemoval(NavObjectManager manager,NavObject marker,bool all)
    {
        if(!ThreadManager.IsMainThread()){UnknownMarkerObservation(manager);return null;}
        lock(Sync)
        {
            if(!ReferenceEquals(manager,markerManager)||markerWorld==null||markerOwner==null)return null;
            var rows=new Dictionary<string,NavObject>(StringComparer.OrdinalIgnoreCase);
            foreach(var pair in Markers)if(all||ReferenceEquals(pair.Value,marker))
            {if(!OwnMarker(pair.Value)){markerPublicationUncertain=true;return null;}rows.Add(pair.Key,pair.Value);}
            return rows.Count==0?null:new MarkerRemovalState{World=markerWorld,Manager=manager,Owner=markerOwner,Rows=rows};
        }
    }
    private static void AfterNativeMarkerRemoval(NavObjectManager __instance,MarkerRemovalState __state,bool __runOriginal)
    {
        if(!__runOriginal||!ThreadManager.IsMainThread()||__state==null||!ReferenceEquals(__instance,__state.Manager))return;
        try
        {
        lock(Sync)
        {
            if(!ReferenceEquals(markerWorld,__state.World)||!ReferenceEquals(markerManager,__state.Manager)||!ReferenceEquals(markerOwner,__state.Owner))return;
            foreach(var row in __state.Rows)if(__instance.NavObjectList.Contains(row.Value))return;
            foreach(var row in __state.Rows)if(Markers.TryGetValue(row.Key,out var current)&&ReferenceEquals(current,row.Value))Markers.Remove(row.Key);
            // Only this service's confirmed removed primitive handles retire.
            // No native unregister, financial release or replacement is invoked.
        }
        }
        catch{UnknownMarkerObservation(__instance);}
    }
    private static void BeforeNativeMarkerReset(NavObject __instance)
    {
        lock(Sync)foreach(var marker in Markers.Values)if(ReferenceEquals(marker,__instance)){markerPublicationUncertain=true;return;}
    }
}
