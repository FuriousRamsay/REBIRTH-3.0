using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;

// INACTIVE: no Harmony attribute or installer. Paid-job receipt semantics differ from native merging.
internal static class RebirthStationPaidCompletionCallsite
{
    private const string NativeMvid="1a9a4203-3d95-4c90-b094-8926dec1ee9c";
    private const string NativeBody="9D6DD060B6343C7D0F0A11E724C33C8CFAD4E5E802E7D878D99A99A834BBC1DD";
    private sealed class Fault { }
    private sealed class PendingCompletion
    {
        internal RebirthStationCompletionCapture.SuccessfulOutput Event;
        internal RebirthStationCompletionObservation Watch;
        internal bool Abandoned;
    }
    private static readonly ConditionalWeakTable<TileEntityWorkstation,Dictionary<string,PendingCompletion>> pending=new ConditionalWeakTable<TileEntityWorkstation,Dictionary<string,PendingCompletion>>();
    private sealed class Iteration
    {
        internal TileEntityWorkstation Station;
        internal RecipeQueueItem Active;
        internal ItemValue Output;
        internal int Count;
        internal bool Paid;
        internal RebirthStationCompletionCapture.OutputObservation Observation;
    }
    private static readonly ConditionalWeakTable<TileEntityWorkstation,Fault> uncertain=new ConditionalWeakTable<TileEntityWorkstation,Fault>();
    [ThreadStatic] private static Iteration current;
    internal static bool HasUncertainEffect(TileEntityWorkstation station)=>station!=null&&uncertain.TryGetValue(station,out _);
    // No clearing API: uncertain physical/receipt effects require separately proved reconciliation.
    private static void Retain(TileEntityWorkstation station){if(station!=null)uncertain.GetValue(station,_=>new Fault());}
    internal static int Insert(ItemStack[] cells,ItemStack stack,int start,TileEntityWorkstation station)
    {
        if(station==null||HasUncertainEffect(station))throw new InvalidOperationException("Station completion requires reconciliation.");
        if(current!=null){Retain(current.Station);Retain(station);throw new InvalidOperationException("Reentrant station completion.");}
        var active=station.Queue!=null&&station.Queue.Length>0?station.Queue[station.Queue.Length-1]:null;
        if(active?.Recipe==null||stack?.itemValue==null||!ReferenceEquals(cells,station.Output))throw new InvalidOperationException("Unknown station output context.");
        bool paid=RebirthStationGridQueue.IsMarked(active.Recipe);
        var iteration=new Iteration{Station=station,Active=active,Output=stack.itemValue,Count=stack.count,Paid=paid};
        if(paid)
        {
            if(!RebirthStationObservationDispatcher.TryGetVerifiedQueuedAdmission(station,active,out var admission)||admission==null||
                !HasPendingCapacity(station,admission.JobId)||!RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,admission.JobId)||
                !RebirthStationCompletionCapture.TryObserveBefore(station,active.StartingEntityId,stack.itemValue,active.Recipe.GetName(),
                    active.Recipe.IsScrap?active.Recipe.ingredients[0].itemValue.ItemClass.GetItemName():string.Empty,
                    active.Recipe.craftExpGain,stack.count,out iteration.Observation))
                throw new InvalidOperationException("Paid station completion lacks authenticated pre-output observation.");
            if(!RebirthStationCompletionCapture.TryBuild(station,active.StartingEntityId,stack.itemValue,active.Recipe.GetName(),
                string.Empty,active.Recipe.craftExpGain,stack.count,out var expected)||
                !RebirthStationPaidOutputPreflight.TryConform(cells,stack,start,expected))
                throw new InvalidOperationException("Paid station output cannot preserve the exact physical item.");
        }
        else if(station.CraftCompleteList!=null)
        {
            // Native merging ignores receipt metadata. Wait before any output until marked reward custody settles.
            foreach(var row in station.CraftCompleteList)
                if(RebirthStationCompletionReceipt.HasReservedMarker(row)&&row.CraftedItemStack?.itemValue?.GetItemId()==stack.itemValue.GetItemId())
                    throw new InvalidOperationException("Native completion would merge into retained paid receipt.");
        }
        current=iteration;
        try
        {
            int result=ItemStack.AddToItemStackArray(cells,stack,start);
            if(result==-1)current=null;
            return result;
        }
        catch{current=null;if(paid)Retain(station);throw;}
    }
    internal static void Complete(TileEntityWorkstation station,int actor,ItemValue output,string recipe,string scrapped,int xp,int count)
    {
        var iteration=current;current=null;
        if(HasUncertainEffect(station)||iteration==null||!ReferenceEquals(iteration.Station,station)||!ReferenceEquals(iteration.Output,output)||
            count!=iteration.Count||station.Queue==null||station.Queue.Length==0||!ReferenceEquals(iteration.Active,station.Queue[station.Queue.Length-1]))
        {Retain(station);throw new InvalidOperationException("Station completion lost its exact successful iteration.");}
        if(!iteration.Paid){station.AddCraftComplete(actor,output,recipe,scrapped,xp,count);return;}
        try
        {
            if(!RebirthStationCompletionCapture.TryAppend(station,actor,output,recipe,scrapped,xp,count,iteration.Observation,out var completed)||completed==null)
            {Retain(station);throw new InvalidOperationException("Paid station receipt has an uncertain or unmatched output effect.");}
            var retained=pending.GetValue(station,_=>new Dictionary<string,PendingCompletion>(StringComparer.Ordinal));
            string job=completed.Original.Admission.JobId;
            if(retained.Count>=64||retained.ContainsKey(job)){Retain(station);throw new InvalidOperationException("Paid event custody changed after output.");}
            retained.Add(job,new PendingCompletion{Event=completed});
        }
        catch{Retain(station);throw;}
    }
    // INACTIVE original HandleRecipeQueue finalizer: preserve original exception object.
    internal static Exception FinalizeIteration(TileEntityWorkstation __instance,Exception __exception)
    {
        var iteration=current;
        if(iteration!=null&&ReferenceEquals(iteration.Station,__instance))
        {current=null;if(iteration.Paid)Retain(__instance);}
        if(pending.TryGetValue(__instance,out var original))
        {
            if(__exception!=null){foreach(var value in original.Values)value.Abandoned=true;Retain(__instance);}
            else TrySuccessfulOriginalExit(__instance);
        }
        return __exception;
    }
    private static bool HasPendingCapacity(TileEntityWorkstation station,string job)
        =>job!=null&&(!pending.TryGetValue(station,out var retained)||retained.Count<64&&!retained.ContainsKey(job));
    // INACTIVE successful-original-exit seam. Detached originals preserve native multi-job while semantics.
    internal static bool TrySuccessfulOriginalExit(TileEntityWorkstation station)
    {
        if(station==null||HasUncertainEffect(station)||!pending.TryGetValue(station,out var originals))return false;
        var batch=new List<KeyValuePair<string,PendingCompletion>>(originals);
        foreach(var pair in batch)
        {
            var original=pair.Value;
            if(original.Abandoned||original.Event==null||!originals.TryGetValue(pair.Key,out var currentOriginal)||!ReferenceEquals(currentOriginal,original))continue;
            if(original.Watch==null)
            {
                if(!RebirthStationCompletionObservation.TryBegin(original.Event,out var watch))continue;
                original.Watch=watch;
            }
            if(!original.Watch.TrySavePublication(out _))continue;
            if(!originals.TryGetValue(pair.Key,out currentOriginal)||!ReferenceEquals(currentOriginal,original))continue;
            original.Watch.Dispose();originals.Remove(pair.Key);
        }
        if(originals.Count!=0)return false;
        pending.Remove(station);return true;
    }
    internal static IEnumerable<CodeInstruction> Transform(IEnumerable<CodeInstruction> instructions,MethodBase original)
    {
        if(original==null||original.DeclaringType!=typeof(TileEntityWorkstation)||original.Name!="HandleRecipeQueue"||
            original.Module.ModuleVersionId.ToString("D")!=NativeMvid)throw new InvalidOperationException("Unsupported native station version.");
        using(var sha=SHA256.Create())
            if(BitConverter.ToString(sha.ComputeHash(original.GetMethodBody().GetILAsByteArray())).Replace("-","")!=NativeBody)
                throw new InvalidOperationException("Unsupported native station body.");
        var source=new List<CodeInstruction>(instructions);
        var expected=PatchProcessor.GetOriginalInstructions(original);
        if(!SameInstructions(source,expected))throw new InvalidOperationException("Station completion IL was changed by another patch.");
        var insertion=typeof(ItemStack).GetMethod(nameof(ItemStack.AddToItemStackArray),new[]{typeof(ItemStack[]),typeof(ItemStack),typeof(int)});
        var completion=typeof(TileEntityWorkstation).GetMethod(nameof(TileEntityWorkstation.AddCraftComplete),new[]{typeof(int),typeof(ItemValue),typeof(string),typeof(string),typeof(int),typeof(int)});
        int inserted=0,completed=0;
        var result=new List<CodeInstruction>();
        foreach(var code in source)
        {
            var copy=new CodeInstruction(code);
            if(Equals(copy.operand,insertion)&&copy.opcode==OpCodes.Call)
            {
                var station=new CodeInstruction(OpCodes.Ldarg_0);station.labels.AddRange(copy.labels);station.blocks.AddRange(copy.blocks);
                copy.labels.Clear();copy.blocks.Clear();result.Add(station);
                copy.operand=typeof(RebirthStationPaidCompletionCallsite).GetMethod(nameof(Insert),BindingFlags.Static|BindingFlags.NonPublic);inserted++;
            }
            else if(Equals(copy.operand,completion)&&copy.opcode==OpCodes.Call)
            {copy.operand=typeof(RebirthStationPaidCompletionCallsite).GetMethod(nameof(Complete),BindingFlags.Static|BindingFlags.NonPublic);completed++;}
            result.Add(copy);
        }
        if(inserted!=1||completed!=1)throw new InvalidOperationException("Missing exact station success call sites.");
        return result;
    }
    private static Dictionary<Label,int> Targets(List<CodeInstruction> codes)
    {var targets=new Dictionary<Label,int>();for(int i=0;i<codes.Count;i++)foreach(var label in codes[i].labels)targets.Add(label,i);return targets;}
    private static bool SameInstructions(List<CodeInstruction> left,List<CodeInstruction> right)
    {
        if(left.Count!=right.Count)return false;
        var a=Targets(left);var b=Targets(right);
        for(int i=0;i<left.Count;i++)
        {
            if(left[i].opcode!=right[i].opcode||left[i].blocks.Count!=0||right[i].blocks.Count!=0)return false;
            var x=left[i].operand;var y=right[i].operand;
            if(x is Label lx&&y is Label ly){if(!a.TryGetValue(lx,out var ax)||!b.TryGetValue(ly,out var by)||ax!=by)return false;}
            else if(x is LocalBuilder localX&&y is LocalBuilder localY){if(localX.LocalIndex!=localY.LocalIndex||localX.LocalType!=localY.LocalType)return false;}
            else if(!Equals(x,y))return false;
        }
        return true;
    }
}