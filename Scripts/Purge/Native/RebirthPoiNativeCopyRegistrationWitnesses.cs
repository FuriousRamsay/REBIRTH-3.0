using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Passive registration callbacks. Never alters original arguments, result or control flow.
internal static class RebirthPoiNativeCopyRegistrationWitnesses
{
    private const string Owner="rebirth.purge.original-copy-registration.v1";
    private static readonly Harmony Patcher=new Harmony(Owner);
    private static readonly List<MethodInfo> methods=new List<MethodInfo>();
    [ThreadStatic] private static RebirthPoiNativeCopyReceiptScope current;
    internal static bool IsReady
    {get{try{return methods.Count==6&&methods.All(m=>{var p=Harmony.GetPatchInfo(m);return p!=null&&p.Owners.Contains(Owner);});}catch{return false;}}}
    internal static void Install()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        if(IsReady)return;
        try
        {
            var selected=new List<MethodInfo>();
            foreach(var name in new[]{"PrefabVolumes.PrefabSleeperVolumeList","PrefabVolumes.PrefabTriggerVolumeList"})
            {
                var type=AccessTools.TypeByName(name);if(type==null)throw new MissingMethodException(name);
                var find=AccessTools.Method(type,"FindWorldVolume",new[]{typeof(World),typeof(Vector3i),typeof(Vector3i)});
                var create=type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SingleOrDefault(m=>m.Name=="CreateWorldVolume"&&m.GetParameters().Length==8);
                if(find==null||create==null||find.ReturnType!=typeof(int)||create.ReturnType!=typeof(int))throw new MissingMethodException("Original native volume registration methods unavailable.");
                var args=create.GetParameters();if(args[0].ParameterType!=typeof(int)||args[1].ParameterType.Name!=(name.Contains("Sleeper")?"PrefabSleeperVolume":"PrefabTriggerVolume")||args[2].ParameterType!=typeof(Vector3i)||args[3].ParameterType!=typeof(Vector3i)||args[4].ParameterType!=typeof(Vector3i)||args[5].ParameterType!=typeof(World)||args[6].ParameterType!=typeof(Vector3i)||args[7].ParameterType!=typeof(Vector3i))throw new MissingMethodException("Original native creator ABI differs.");
                selected.Add(find);selected.Add(create);
            }
            selected.Add(AccessTools.Method(typeof(World),"AddSleeperVolume",new[]{typeof(SleeperVolume)}));selected.Add(AccessTools.Method(typeof(World),"AddTriggerVolume",new[]{typeof(TriggerVolume)}));
            if(selected.Any(m=>m==null||m.ReturnType!=typeof(int)))throw new MissingMethodException("Original native registration return differs.");
            foreach(var method in selected)
            {
                var info=Harmony.GetPatchInfo(method);if(info!=null&&info.Owners.Contains(Owner))continue;
                string prefix=method.Name=="FindWorldVolume"?"FindBefore":method.Name=="CreateWorldVolume"?"CreateBefore":"AddBefore";
                string postfix=method.Name=="FindWorldVolume"?"FindAfter":method.Name=="CreateWorldVolume"?"CreateAfter":"AddAfter";
                Patcher.Patch(method,new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiNativeCopyRegistrationWitnesses),prefix)),new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiNativeCopyRegistrationWitnesses),postfix)),null,new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiNativeCopyRegistrationWitnesses),"Failed")),null);
            }
            methods.Clear();methods.AddRange(selected);
        }
        catch(Exception error){methods.Clear();Log.Warning("[REBIRTH Purge] Original copy registration proof unavailable: "+error.Message);}
    }
    internal static bool Enter(RebirthPoiNativeCopyReceiptScope scope)
    {if(scope==null||current!=null||!IsReady||!scope.IsCurrent){scope?.Fail();current?.Fail();return false;}current=scope;return true;}
    internal static void Leave(RebirthPoiNativeCopyReceiptScope scope){if(ReferenceEquals(current,scope))current=null;else scope?.Fail();}
    private sealed class Frame
    {internal RebirthPoiNativeCopyReceiptScope Scope;internal RebirthPoiNativeCopyReceiptScope.Creation Creation;internal object Subject;internal object[] Args;}
    [ThreadStatic] private static Frame creator;
    private static void FindBefore(object __instance,object[] __args,out Frame __state)
    {__state=current==null?null:new Frame{Scope=current,Subject=__instance,Args=__args};}
    private static void FindAfter(int __result,bool __runOriginal,Frame __state)
    {if(__state==null)return;try{var a=__state.Args;if(a==null||a.Length!=3||!__state.Scope.FindCompleted(__state.Subject,(World)a[0],(Vector3i)a[1],(Vector3i)a[2],__result,__runOriginal))__state.Scope.Fail();}catch{__state.Scope.Fail();}}
    private static void CreateBefore(object __instance,object[] __args,out Frame __state)
    {
        __state=null;if(current==null)return;
        if(creator!=null){current.Fail();return;}
        __state=new Frame{Scope=current,Subject=__instance,Args=__args,Creation=current.BeginCreation(__instance,__args)};creator=__state;
    }
    private static void CreateAfter(object[] __args,int __result,bool __runOriginal,Frame __state)
    {if(__state==null)return;try{if(!ReferenceEquals(creator,__state)||!__state.Scope.CreationCompleted(__state.Creation,__state.Subject,__args,__result,__runOriginal))__state.Scope.Fail();}catch{__state.Scope.Fail();}finally{if(ReferenceEquals(creator,__state))creator=null;}}
    private static void AddBefore(World __instance,object[] __args,out Frame __state)
    {__state=current==null?null:new Frame{Scope=current,Subject=__instance,Args=__args,Creation=creator?.Creation};}
    private static void AddAfter(int __result,bool __runOriginal,Frame __state)
    {if(__state==null)return;try{if(__state.Creation==null||__state.Args==null||__state.Args.Length!=1||!__state.Scope.AddCompleted(__state.Creation,(World)__state.Subject,__state.Args[0],__result,__runOriginal))__state.Scope.Fail();}catch{__state.Scope.Fail();}}
    private static Exception Failed(Exception __exception,Frame __state)
    {if(__exception!=null&&__state!=null){__state.Scope.Fail();if(ReferenceEquals(creator,__state))creator=null;}return __exception;}
}