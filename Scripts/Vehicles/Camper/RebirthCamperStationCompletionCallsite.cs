using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Installed only by the explicit mobile-module installer. Its bootstrap remains
// inactive until the complete mobile feature is ready for qualification.
[HarmonyPatch(typeof(TileEntityWorkstation),"HandleRecipeQueue")]
internal static class RebirthCamperStationCompletionCallsite
{
    private sealed class Iteration
    {
        internal RebirthCamperStationState Station;
        internal RebirthCamperStationCompletionCapture.Observation Observation;
        internal bool Intermediate;
    }
    private sealed class Retained
    {
        internal bool Uncertain;
        internal readonly Dictionary<string,RebirthCamperStationCompletionCapture.SuccessfulOutput> Events
            =new Dictionary<string,RebirthCamperStationCompletionCapture.SuccessfulOutput>(StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<RebirthCamperStationState,Retained> retained
        =new ConditionalWeakTable<RebirthCamperStationState,Retained>();
    [ThreadStatic] private static Iteration current;
    private static readonly ConditionalWeakTable<RebirthCamperStationState,RebirthCamperStationTrainingRecovery> recoveryHolds
        =new ConditionalWeakTable<RebirthCamperStationState,RebirthCamperStationTrainingRecovery>();
    internal static bool TryGetRecoveryHold(RebirthCamperStationState station,out RebirthCamperStationTrainingRecovery recovery)
        =>recoveryHolds.TryGetValue(station,out recovery);
    internal static bool HasRecoveryHold(RebirthCamperStationState station,RebirthCamperStationTrainingRecovery recovery)
        =>recoveryHolds.TryGetValue(station,out var held)&&ReferenceEquals(held,recovery);
    internal static bool TryHoldRecovery(RebirthCamperStationState station,RebirthCamperStationTrainingRecovery recovery)
    {
        if(!ThreadManager.IsMainThread()||station==null||recovery==null)return false;
        if(recoveryHolds.TryGetValue(station,out var held))return ReferenceEquals(held,recovery);
        recoveryHolds.Add(station,recovery);return true;
    }
    internal static bool TryReleaseRecovery(RebirthCamperStationState station,RebirthCamperStationTrainingRecovery recovery)
    {
        if(!ThreadManager.IsMainThread()||!HasRecoveryHold(station,recovery)||!recovery.IsRetirementSavedFor(station))return false;
        recoveryHolds.Remove(station);station.lastTickTime=0;return true;
    }
    internal static bool IsHeld(RebirthCamperStationState station)
    {
        if(station==null)return false;
        try
        {
            // Native receipts survive reload, while this weak event table does
            // not. Never let a cold station advance or consume reserved effects.
            return recoveryHolds.TryGetValue(station,out _)||!RebirthStationCompletionReceipt.CanUseNativeRewards(station.CraftCompleteList)||
                retained.TryGetValue(station,out var state)&&(state.Uncertain||state.Events.Count!=0);
        }
        catch{return true;}
    }
    private static void MarkUncertain(RebirthCamperStationState station)
        =>retained.GetValue(station,_=>new Retained()).Uncertain=true;
    internal static bool TryReleaseRetired(RebirthCamperStationCompletionCapture.SuccessfulOutput completed,
        RebirthCamperStationReceiptRetirement retirement)
    {
        if(!ThreadManager.IsMainThread()||completed==null||retirement==null||!retirement.IsSavedFor(completed)||
            !retained.TryGetValue(completed.Binding.Station,out var state)||state.Uncertain||
            !state.Events.TryGetValue(completed.Binding.Admission.JobId,out var original)||!ReferenceEquals(original,completed))return false;
        state.Events.Remove(completed.Binding.Admission.JobId);
        completed.Binding.Station.lastTickTime=0;return true;
    }
    internal static int Insert(ItemStack[] cells,ItemStack incoming,int maxItemCount,TileEntityWorkstation tile)
    {
        if(!(tile is RebirthCamperStationState station))return ItemStack.AddToItemStackArray(cells,incoming,maxItemCount);
        if(IsHeld(station)||current!=null)
        {
            if(current!=null)MarkUncertain(current.Station);
            throw new InvalidOperationException("Mobile completion is held for save or reconciliation.");
        }
        var active=station.Queue!=null&&station.Queue.Length>0?station.Queue[station.Queue.Length-1]:null;
        if(!RebirthStationGridQueue.IsMarked(active?.Recipe))return ItemStack.AddToItemStackArray(cells,incoming,maxItemCount);
        if(!ReferenceEquals(cells,station.Output)||
            !RebirthCamperStationCompletionCapture.TryObserveBefore(station,incoming,maxItemCount,out var observation))
            throw new InvalidOperationException("Mobile paid output lacks its original observation.");
        current=new Iteration{Station=station,Observation=observation,
            Intermediate=observation.Binding.Admission.IsPerBatch&&active.Multiplier>1};
        try
        {
            int result=ItemStack.AddToItemStackArray(cells,incoming,maxItemCount);
            if(result==-1)current=null;
            return result;
        }
        catch{current=null;MarkUncertain(station);throw;}
    }
    internal static void Complete(TileEntityWorkstation tile,int actor,ItemValue output,string recipe,string scrapped,int xp,int count)
    {
        if(!(tile is RebirthCamperStationState station)){tile.AddCraftComplete(actor,output,recipe,scrapped,xp,count);return;}
        var iteration=current;current=null;
        if(iteration==null)
        {
            var active=station.Queue!=null&&station.Queue.Length>0?station.Queue[station.Queue.Length-1]:null;
            if(RebirthStationGridQueue.IsMarked(active?.Recipe)){MarkUncertain(station);throw new InvalidOperationException("Missing mobile original output.");}
            station.AddCraftComplete(actor,output,recipe,scrapped,xp,count);return;
        }
        try
        {
            if(iteration.Intermediate)
            {
                if(!ReferenceEquals(iteration.Station,station)||IsHeld(station)||
                    !RebirthCamperStationCompletionCapture.TryAppendIntermediate(iteration.Observation,actor,output,recipe,scrapped,xp,count))
                    throw new InvalidOperationException("Unmatched mobile batch output.");
                return;
            }
            if(!ReferenceEquals(iteration.Station,station)||IsHeld(station)||
                !RebirthCamperStationCompletionCapture.TryAppend(iteration.Observation,actor,output,recipe,scrapped,xp,count,out var completed))
                throw new InvalidOperationException("Unmatched mobile successful output.");
            var state=retained.GetValue(station,_=>new Retained());
            var job=completed.Binding.Admission.JobId;
            if(state.Events.Count>=64||state.Events.ContainsKey(job))throw new InvalidOperationException("Duplicate mobile completion event.");
            state.Events.Add(job,completed);
        }
        catch{MarkUncertain(station);throw;}
    }
    [HarmonyFinalizer]
    internal static Exception FinalizeIteration(TileEntityWorkstation __instance,Exception __exception)
    {
        if(!(__instance is RebirthCamperStationState station))return __exception;
        if(current!=null&&ReferenceEquals(current.Station,station)){current=null;MarkUncertain(station);}
        if(__exception!=null&&retained.TryGetValue(station,out var state)&&state.Events.Count!=0)state.Uncertain=true;
        if(__exception==null&&retained.TryGetValue(station,out var completedState)&&!completedState.Uncertain)
            foreach(var completed in completedState.Events.Values)
                RebirthCamperStationCompletionWatch.TryWatch(completed);
        return __exception;
    }
    internal static bool TryGetSuccessfulOutput(RebirthCamperStationState station,string job,
        out RebirthCamperStationCompletionCapture.SuccessfulOutput completed)
    {
        completed=null;
        if(!ThreadManager.IsMainThread()||station==null||job==null||
            !retained.TryGetValue(station,out var state)||state.Uncertain||!state.Events.TryGetValue(job,out var original)||
            !original.MatchesLiveOutput()||station.Queue==null)return false;
        foreach(var entry in station.Queue)
            if(entry?.Recipe!=null&&RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var queuedJob)&&queuedJob==job)return false;
        completed=original;return true;
    }
    internal static IEnumerable<CodeInstruction> Transform(IEnumerable<CodeInstruction> instructions,MethodBase original)
    {
        // Reuse the exact MVID/body/instruction checks and stack-safe insertion.
        var source=RebirthStationPaidCompletionCallsite.Transform(instructions,original);
        var originalInsert=typeof(RebirthStationPaidCompletionCallsite).GetMethod("Insert",BindingFlags.Static|BindingFlags.NonPublic);
        var originalComplete=typeof(RebirthStationPaidCompletionCallsite).GetMethod("Complete",BindingFlags.Static|BindingFlags.NonPublic);
        var result=new List<CodeInstruction>();int inserts=0,completions=0;
        foreach(var instruction in source)
        {
            var copy=new CodeInstruction(instruction);
            if(Equals(copy.operand,originalInsert)){copy.operand=typeof(RebirthCamperStationCompletionCallsite).GetMethod(nameof(Insert),BindingFlags.Static|BindingFlags.NonPublic);inserts++;}
            if(Equals(copy.operand,originalComplete)){copy.operand=typeof(RebirthCamperStationCompletionCallsite).GetMethod(nameof(Complete),BindingFlags.Static|BindingFlags.NonPublic);completions++;}
            result.Add(copy);
        }
        if(inserts!=1||completions!=1)throw new InvalidOperationException("Missing mobile output call sites.");
        return result;
    }
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
        =>Transform(instructions,__originalMethod);
}
