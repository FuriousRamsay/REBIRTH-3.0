using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine.Scripting;

[Preserve]
public sealed class RebirthPoiNativeResetWitnessModApi:IModApi
{
    private static bool actorPulseRegistered;
    public void InitMod(Mod mod){ if(!RebirthPurgeReleasePolicy.Enabled) return; RebirthPoiNativeResetWitnesses.Install();RebirthPoiInheritedActorOutcomeHooks.Install();if(!actorPulseRegistered){ModEvents.GameUpdate.RegisterHandler(ActorUpdate);actorPulseRegistered=true;}}
    private static void ActorUpdate(ref ModEvents.SGameUpdateData data){ if(!RebirthPurgeReleasePolicy.Enabled) return; RebirthPoiInheritedActorOutcomes.Instance.Pulse();}
}

// Passive receipts exist only inside the original, guarded native iterator.
// This class never skips, repeats or changes any native method argument/result.
internal static class RebirthPoiNativeResetWitnesses
{
    private const string Owner="rebirth.purge.original-reset-witness.v1";
    private static readonly Harmony Patcher=new Harmony(Owner);
    private static readonly List<MethodInfo> methods=new List<MethodInfo>();
    private static bool installed;
    [ThreadStatic] private static HelperFrame activeHelper;
    internal static bool IsReady
    {
        get
        {
            if(!installed||methods.Count!=6||!RebirthPoiNativeCopyRegistrationWitnesses.IsReady)return false;
            try{return methods.All(m=>{var p=Harmony.GetPatchInfo(m);return p!=null&&p.Owners.Contains(Owner)&&!p.Transpilers.Any()&&!p.Postfixes.Any(x=>x.owner!=Owner&&!(m.DeclaringType==typeof(SleeperVolume)&&m.Name=="DespawnAndReset"&&x.owner=="rebirth.purge.passive-evidence.v1"));});}
            catch{return false;}
        }
    }
    internal static bool TryQualifyChunkHelper(string suffix,out MethodInfo helper,out FieldInfo world,out FieldInfo owner,out FieldInfo cluster)
    {
        helper=null;world=null;owner=null;cluster=null;
        try
        {
            foreach(var candidate in typeof(PrefabInstance).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                if(!candidate.Name.StartsWith("<ResetBlocksAndRebuild>g__"+suffix+"|",StringComparison.Ordinal))continue;
                var args=candidate.GetParameters();
                if(helper!=null||candidate.ReturnType!=typeof(bool)||args.Length!=2||args[0].ParameterType!=typeof(long)||!args[1].ParameterType.IsByRef)return false;
                var closure=args[1].ParameterType.GetElementType();var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
                var w=closure.GetField("_world",flags);var p=closure.GetField("<>4__this",flags);var c=closure.GetField("cc",flags);
                if(w==null||w.FieldType!=typeof(World)||p==null||p.FieldType!=typeof(PrefabInstance)||c==null||c.FieldType!=typeof(ChunkCluster))return false;
                helper=candidate;world=w;owner=p;cluster=c;
            }
            return helper!=null;
        }
        catch{helper=null;world=null;owner=null;cluster=null;return false;}
    }
    internal static void Install()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        if(installed)return;
        RebirthPoiNativeCopyRegistrationWitnesses.Install();
        try
        {
            MethodInfo copy,regen;FieldInfo w,p,c;
            if(!TryQualifyChunkHelper("CopyChunk",out copy,out w,out p,out c)||!TryQualifyChunkHelper("RegenerateChunk",out regen,out w,out p,out c))throw new MissingMethodException("Original native reset helpers unavailable.");
            var selected=new[]{copy,regen,
                AccessTools.Method(typeof(PrefabInstance),"CopyIntoChunk",new[]{typeof(World),typeof(Chunk),typeof(bool),typeof(FastTags<TagGroup.Global>)}),
                AccessTools.Method(typeof(SleeperVolume),"DespawnAndReset",new[]{typeof(World)}),
                AccessTools.Method(typeof(TriggerVolume),"Reset",Type.EmptyTypes),
                AccessTools.Method(typeof(TriggerManager),"RefreshTriggers",new[]{typeof(PrefabInstance),typeof(FastTags<TagGroup.Global>)})};
            if(selected.Any(m=>m==null)||selected.Skip(2).Any(m=>m.ReturnType!=typeof(void))||selected.Any(m=>{var info=Harmony.GetPatchInfo(m);return info!=null&&info.Transpilers.Any();}))throw new MissingMethodException("Native reset witness methods are missing or transpiled.");
            Patch(copy,"HelperBefore","HelperAfter","HelperFailed");Patch(regen,"HelperBefore","HelperAfter","HelperFailed");
            Patch(selected[2],"CopyBefore","CopyAfter","EffectFailed");Patch(selected[3],"VolumeBefore","VolumeAfter","EffectFailed");
            Patch(selected[4],"TriggerBefore","TriggerAfter","EffectFailed");Patch(selected[5],"RefreshBefore","RefreshAfter","EffectFailed");
            methods.Clear();methods.AddRange(selected);installed=true;
        }
        catch(Exception error){installed=false;Log.Warning("[REBIRTH Purge] Original reset admission closed: "+error.Message);}
    }
    private static void Patch(MethodInfo original,string before,string after,string finalizer)
    {
        var info=Harmony.GetPatchInfo(original);if(info!=null&&info.Owners.Contains(Owner))return;
        Patcher.Patch(original,new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiNativeResetWitnesses),before)),new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiNativeResetWitnesses),after)),null,new HarmonyMethod(AccessTools.Method(typeof(RebirthPoiNativeResetWitnesses),finalizer)),null);
    }
    private sealed class HelperFrame
    {
        internal RebirthPoiNativeResetExecution Execution;internal RebirthPoiNativeResetEnvelope.Target Target;
        internal World World;internal ChunkCluster Cluster;internal long Key;internal bool Copy;
        internal object Closure;internal FieldInfo WorldField,OwnerField,ClusterField;
        internal HelperFrame Previous;internal Chunk Copied;internal bool CopyOriginal;internal RebirthPoiNativeCopyReceiptScope Registration;
        internal bool Current(object[] args)
        {
            try{return Execution.IsOriginalCurrent&&args!=null&&args.Length==2&&(long)args[0]==Key&&ReferenceEquals(WorldField.GetValue(args[1]),World)&&ReferenceEquals(OwnerField.GetValue(args[1]),Target.Prefab)&&ReferenceEquals(ClusterField.GetValue(args[1]),Cluster)&&ReferenceEquals(World.ChunkCache,Cluster);}
            catch{return false;}
        }
    }
    private static void HelperBefore(PrefabInstance __instance,MethodBase __originalMethod,object[] __args,out HelperFrame __state)
    {
        __state=null;var execution=RebirthPoiNativeResetExecution.Active;if(execution==null)return;
        try
        {
            var target=execution.TargetFor(__instance);if(target==null||!IsReady||activeHelper!=null||__args==null||__args.Length!=2){execution.Batch.AbortUncertain();return;}
            MethodInfo method;FieldInfo w,p,c;bool copy=__originalMethod.Name.Contains("g__CopyChunk|");
            if(!TryQualifyChunkHelper(copy?"CopyChunk":"RegenerateChunk",out method,out w,out p,out c)||!Equals(method,__originalMethod)){execution.Batch.AbortUncertain();return;}
            var frame=new HelperFrame{Execution=execution,Target=target,World=execution.Envelope.World,Cluster=c.GetValue(__args[1]) as ChunkCluster,Key=(long)__args[0],Copy=copy,Closure=__args[1],WorldField=w,OwnerField=p,ClusterField=c,Previous=activeHelper};
            if(!frame.Current(__args)||!target.Entry.Plan.Chunks.Contains(frame.Key)){execution.Batch.AbortUncertain();return;}
            __state=frame;activeHelper=frame;
        }
        catch(Exception error){execution.Batch.AbortUncertain(error);}
    }
    private static void HelperAfter(object[] __args,bool __result,bool __runOriginal,HelperFrame __state)
    {
        if(__state==null)return;
        try
        {
            if(!__state.Current(__args)||!__runOriginal){__state.Execution.Batch.AbortUncertain();return;}
            // Native false is a retryable absent chunk, never a positive receipt.
            if(!__result)return;
            var chunk=__state.Cluster.GetChunkSync(__state.Key);
            bool verified=chunk!=null&&chunk.Key==__state.Key&&(__state.Copy?__state.CopyOriginal&&ReferenceEquals(chunk,__state.Copied):chunk.NeedsDecoration&&chunk.NeedsLightDecoration&&chunk.NeedsLightCalculation);
            if(verified&&__state.Copy)verified=__state.Execution.CompleteCopyReceipts(__state.Target,__state.Registration);
            __state.Execution.ChunkResult(__state.Target,__state.Key,__state.Copy,verified);
        }
        catch(Exception error){__state.Execution.Batch.AbortUncertain(error);}
        finally{__state.Registration?.Fail();if(ReferenceEquals(activeHelper,__state))activeHelper=__state.Previous;}
    }
    private static Exception HelperFailed(Exception __exception,HelperFrame __state)
    {
        if(__state!=null){if(__exception!=null)__state.Execution.Batch.AbortUncertain(__exception);if(ReferenceEquals(activeHelper,__state))activeHelper=__state.Previous;}
        return __exception;
    }
    private sealed class EffectFrame
    {internal RebirthPoiNativeResetExecution Execution;internal object Subject;internal World World;internal Chunk Chunk;internal HelperFrame Helper;internal RebirthPoiNativeCopyReceiptScope Registration;}
    private static void CopyBefore(PrefabInstance __instance,World __0,Chunk __1,bool __2,out EffectFrame __state)
    {
        __state=null;var helper=activeHelper;if(helper==null)return;
        if(!helper.Copy||!ReferenceEquals(helper.Target.Prefab,__instance)||!ReferenceEquals(helper.World,__0)||__1==null||__1.Key!=helper.Key||!__2){helper.Execution.Batch.AbortUncertain();return;}
        __state=new EffectFrame{Execution=helper.Execution,Subject=__instance,World=__0,Chunk=__1,Helper=helper};
        __state.Registration=helper.Execution.BeginCopyReceipts(helper.Target,__state);helper.Registration=__state.Registration;
        if(!RebirthPoiNativeCopyRegistrationWitnesses.Enter(__state.Registration))helper.Execution.Batch.AbortUncertain();
    }
    private static void CopyAfter(bool __runOriginal,EffectFrame __state)
    {
        if(__state==null)return;var helper=__state.Helper;
        RebirthPoiNativeCopyRegistrationWitnesses.Leave(__state.Registration);
        if(!__runOriginal||!__state.Execution.IsOriginalCurrent||!ReferenceEquals(activeHelper,helper)||helper.CopyOriginal){__state.Execution.Batch.AbortUncertain();return;}
        helper.Copied=__state.Chunk;helper.CopyOriginal=true;
    }
    private static void VolumeBefore(SleeperVolume __instance,World __0,out EffectFrame __state)
    {var execution=RebirthPoiNativeResetExecution.Active;__state=execution==null?null:new EffectFrame{Execution=execution,Subject=__instance,World=__0};}
    private static void VolumeAfter(bool __runOriginal,EffectFrame __state)
    {
        if(__state==null)return;var volume=(SleeperVolume)__state.Subject;
        try { bool reset=__runOriginal&&volume.respawnTime==ulong.MaxValue&&!volume.isSpawning&&!volume.isSpawned&&!volume.wasCleared&&volume.groupCountList==null&&volume.numSpawned==0&&volume.respawnMap.Count==0&&volume.respawnList==null&&volume.pendingSpawnMap.Count==0&&volume.pendingSpawnOps.Count==0&&volume.playerTouchedToUpdate==null&&volume.playerTouchedTrigger==null;
        __state.Execution.VolumeResult(volume,__state.World,reset); } catch(Exception error){__state.Execution.Batch.AbortUncertain(error);}
    }
    private static void TriggerBefore(TriggerVolume __instance,out EffectFrame __state)
    {var execution=RebirthPoiNativeResetExecution.Active;__state=execution==null?null:new EffectFrame{Execution=execution,Subject=__instance};}
    private static void TriggerAfter(bool __runOriginal,EffectFrame __state)
    {if(__state!=null){var volume=(TriggerVolume)__state.Subject;__state.Execution.TriggerResult(volume,__runOriginal&&!volume.isTriggered);}}
    private static void RefreshBefore(TriggerManager __instance,PrefabInstance __0,out EffectFrame __state)
    {var execution=RebirthPoiNativeResetExecution.Active;__state=execution==null?null:new EffectFrame{Execution=execution,Subject=__0,World=execution.Envelope.World};if(__state!=null&&!ReferenceEquals(__instance,__state.World.triggerManager))execution.Batch.AbortUncertain();}
    private static void RefreshAfter(TriggerManager __instance,bool __runOriginal,EffectFrame __state)
    {
        if(__state==null)return;var prefab=(PrefabInstance)__state.Subject;
        try { PrefabTriggerData data;bool verified=__runOriginal&&ReferenceEquals(__instance,__state.World.triggerManager)&&__instance.PrefabDataDict.TryGetValue(prefab,out data)&&data!=null&&ReferenceEquals(data.Owner,__instance)&&ReferenceEquals(data.PrefabInstance,prefab)&&ReferenceEquals(data.world,__state.World);
        __state.Execution.RefreshResult(prefab,verified); } catch(Exception error){__state.Execution.Batch.AbortUncertain(error);}
    }
    private static Exception EffectFailed(Exception __exception,EffectFrame __state)
    {if(__exception!=null&&__state!=null){__state.Registration?.Fail();if(__state.Registration!=null)RebirthPoiNativeCopyRegistrationWitnesses.Leave(__state.Registration);__state.Execution.Batch.AbortUncertain(__exception);}return __exception;}
}