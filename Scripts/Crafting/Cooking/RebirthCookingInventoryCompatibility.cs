using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using HarmonyLib;

internal static class RebirthCookingMergeScope
{
    // Native stacking is sufficient when neither item carries mod state or
    // represents a measured liquid. Keep every metadata-bearing legacy item on
    // the compatibility path, even if it predates the current cooking keys.
    internal static bool NeedsCheck(ItemValue item)
        =>item!=null&&(item.Metadata!=null||RebirthCookingItemStats.IsLiquid(item)||RebirthContainerPackUp.IsPacked(item));
}

// Native partial merges only compare item types. Supply the incoming value while they run.
[HarmonyPatch]
public static class RebirthCookingPartialMergeScope
{
    [ThreadStatic] internal static ItemValue Incoming;
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return RequireTarget(typeof(Bag), "TryStackItem", typeof(int), typeof(ItemStack));
        yield return RequireTarget(typeof(Inventory), "TryStackItem", typeof(int), typeof(ItemStack));
        yield return RequireTarget(typeof(TEFeatureStorage), "TryStackItem", typeof(int), typeof(ItemStack));
        // Current native capacity query calls CanStackPartlyWith -> patched CanStackWith.
        yield return RequireTarget(typeof(Inventory), "CanTakeItem", typeof(ItemStack));
        yield return RequireTarget(typeof(XUiM_PlayerInventory), "TryStackItem", typeof(int), typeof(ItemStack), typeof(ItemStack[]));
        yield return RequireTarget(typeof(XUiC_WorkstationGrid), "TryStackItem", typeof(int), typeof(ItemStack));
    }
    private static MethodInfo RequireTarget(Type owner, string name, params Type[] signature)
    {
        MethodInfo method = AccessTools.Method(owner, name, signature);
        if (method == null)
            throw new MissingMethodException(owner.FullName, name);
        bool incomingFound = false;
        foreach (ParameterInfo parameter in method.GetParameters())
            if (parameter.Name == "_itemStack" && parameter.ParameterType == typeof(ItemStack))
                incomingFound = true;
        if (!incomingFound)
            throw new InvalidOperationException("Cooking merge target lacks ItemStack _itemStack: " + method);
        return method;
    }
    static void Prefix(ItemStack _itemStack, out ItemValue __state) { __state=Incoming; Incoming=_itemStack?.itemValue; }
    static void Finalizer(ItemValue __state) { Incoming=__state; }

    // Storage must carry the incoming value at the merge call itself. Its native
    // capacity-only call can otherwise bypass the separately detoured leaf method.
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code=instructions.Select(instruction=>new CodeInstruction(instruction)).ToList();
        if(__originalMethod.DeclaringType!=typeof(TEFeatureStorage))return code;
        var capacity=AccessTools.Method(typeof(ItemStack),nameof(ItemStack.CanStackPartly),new[]{typeof(int).MakeByRefType()});
        var checks=code.Select((instruction,index)=>new{instruction,index})
            .Where(entry=>entry.instruction.Calls(capacity)).ToArray();
        if(checks.Length!=1 || __originalMethod.GetMethodBody().ExceptionHandlingClauses.Count!=0)
            throw new InvalidOperationException("Storage liquid merge call shape changed.");
        int at=checks[0].index;
        var incoming=new CodeInstruction(OpCodes.Ldarg_2);
        incoming.labels.AddRange(code[at].labels);code[at].labels.Clear();
        code[at].opcode=OpCodes.Call;
        code[at].operand=AccessTools.Method(typeof(RebirthCookingPartialMergeScope),nameof(StorageCanStackPartly));
        code.Insert(at,incoming);
        return code;
    }
    private static bool StorageCanStackPartly(ItemStack target,ref int count,ItemStack incoming)
    {
        if((RebirthCookingMergeScope.NeedsCheck(target?.itemValue)||RebirthCookingMergeScope.NeedsCheck(incoming?.itemValue)) &&
            !RebirthCookingItemStats.Compatible(target?.itemValue,incoming?.itemValue))return false;
        RebirthLiquidStackCapacityPatch.Apply(target?.itemValue?.ItemClass);
        return target.CanStackPartly(ref count);
    }
}
[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanStackPartly))]
public static class RebirthCookingPartialMergeCheck
{
    static bool Prefix(ItemStack __instance, ref bool __result)
    {
        var incoming=RebirthCookingPartialMergeScope.Incoming;
        if(incoming==null || !RebirthCookingMergeScope.NeedsCheck(__instance?.itemValue)
            && !RebirthCookingMergeScope.NeedsCheck(incoming))return true;
        RebirthLiquidStackCapacityPatch.Apply(__instance?.itemValue?.ItemClass);
        if(RebirthCookingItemStats.Compatible(__instance.itemValue,incoming))return true;
        __result=false;return false;
    }
}
[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.StackTransferCount))]
public static class RebirthCookingTransferCountCheck
{
    static void Prefix(ItemStack __instance)
    { if(RebirthCookingMergeScope.NeedsCheck(__instance?.itemValue))RebirthLiquidStackCapacityPatch.Apply(__instance.itemValue.ItemClass); }
    static void Postfix(ItemStack __instance, ItemStack __0, ref int __result)
    { if(__result>0&&(RebirthCookingMergeScope.NeedsCheck(__instance?.itemValue)
        ||RebirthCookingMergeScope.NeedsCheck(__0?.itemValue))
        &&!RebirthCookingItemStats.Compatible(__instance.itemValue,__0.itemValue))__result=0; }
}
[HarmonyPatch(typeof(StackSortUtil), nameof(StackSortUtil.CombineAndSortStacks))]
public static class RebirthCookingSortCompatibility
{
    static bool Prefix(ItemStack[] _stacks, int _ignoreSlots, PackedBoolArray _ignoredSlots, ref ItemStack[] __result)
    {
        bool cooked=false;
        foreach(var stack in _stacks)
            if (RebirthCookingItemStats.NeedsStateAwareStacking(stack?.itemValue)) { cooked = true; break; }
        if(!cooked)return true;
        // Native CombineAndSortStacks clones its input before any count writes.
        // The metadata-aware path must preserve the same caller ownership.
        var detached=new ItemStack[_stacks.Length];
        for(int index=0;index<_stacks.Length;index++)detached[index]=_stacks[index]?.Clone()??ItemStack.Empty.Clone();
        _stacks=detached;
        for(int i=_ignoreSlots;i<_stacks.Length;i++)
        {
            var target=_stacks[i];
            if(target == null || target.IsEmpty() || StackSortUtil.IsIgnoredSlot(_ignoreSlots,_ignoredSlots,i) ||
                target.itemValue.ItemClass.HasQuality && !RebirthCookingItemStats.IsLiquid(target.itemValue)) continue;
            for(int j=i+1;j<_stacks.Length;j++)
            {
                var source=_stacks[j];
                if(source == null || source.IsEmpty() || StackSortUtil.IsIgnoredSlot(_ignoreSlots,_ignoredSlots,j) ||
                    !target.CanStackWith(source,true) || !RebirthCookingItemStats.Compatible(target.itemValue,source.itemValue)) continue;
                int limit = RebirthCookingItemStats.IsLiquid(target.itemValue)
                    ? Math.Min(10, Math.Max(1, target.itemValue.ItemClass.Stacknumber.Value))
                    : target.itemValue.ItemClass.MaxCount;
                int count=Math.Min(source.count,Math.Max(0,limit-target.count));
                target.count+=count;source.count-=count;
                if(source.count==0)_stacks[j]=ItemStack.Empty.Clone();
            }
        }
        __result=StackSortUtil.SortStacks(_stacks,_ignoreSlots,_ignoredSlots);return false;
    }
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.CompactSlots))]
public static class RebirthCookingGridCompactionCompatibility
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
    {
        var code=instructions.Select(instruction=>new CodeInstruction(instruction)).ToList();
        var body=__originalMethod.GetMethodBody();
        var stacks=body.LocalVariables.Where(local=>local.LocalType==typeof(ItemStack)).ToArray();
        var minimum=AccessTools.Method(typeof(Utils),nameof(Utils.FastMin),new[]{typeof(int),typeof(int)});
        var calls=code.Select((instruction,index)=>new{instruction,index}).Where(entry=>entry.instruction.Calls(minimum)).ToArray();
        if(stacks.Length!=2 || calls.Length!=1 || body.ExceptionHandlingClauses.Count!=0)
            throw new InvalidOperationException("Native storage compaction shape changed.");
        int at=calls[0].index;
        // Keep native compaction, locks, sorting and notifications. Only refuse
        // the proposed count transfer when it would erase item state.
        var target=CodeInstruction.LoadLocal(stacks[0].LocalIndex);
        target.labels.AddRange(code[at].labels);code[at].labels.Clear();
        code[at].operand=AccessTools.Method(typeof(RebirthCookingGridCompactionCompatibility),nameof(CompatibleCount));
        code.InsertRange(at,new[]{target,CodeInstruction.LoadLocal(stacks[1].LocalIndex)});
        return code;
    }
    private static int CompatibleCount(int incomingCount,int capacity,ItemStack target,ItemStack incoming)
    {
        if((RebirthCookingMergeScope.NeedsCheck(target?.itemValue)||RebirthCookingMergeScope.NeedsCheck(incoming?.itemValue)) &&
            !RebirthCookingItemStats.Compatible(target?.itemValue,incoming?.itemValue))return 0;
        return Utils.FastMin(incomingCount,capacity);
    }
}
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleStackSwap))]
public static class RebirthCookingMouseMergeCheck
{
    static bool Prefix(XUiC_ItemStack __instance)
    {
        var incoming=__instance.xui.DragAndDropWindow.CurrentStack;
        var target=__instance.ItemStack;
        if(incoming.IsEmpty()||target.IsEmpty()||!RebirthCookingMergeScope.NeedsCheck(incoming.itemValue)
            &&!RebirthCookingMergeScope.NeedsCheck(target.itemValue)
            ||RebirthCookingItemStats.Compatible(incoming.itemValue,target.itemValue))return true;
        // Different cooked values behave like different items: swap instead of combining.
        __instance.SwapItem();__instance.HandleClickComplete();return false;
    }
}
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleDropOne))]
public static class RebirthCookingSingleMergeCheck
{
    static bool Prefix(XUiC_ItemStack __instance)
    {
        var incoming=__instance.xui.DragAndDropWindow.CurrentStack;
        var target=__instance.ItemStack;
        return incoming.IsEmpty()||target.IsEmpty()
            ||!RebirthCookingMergeScope.NeedsCheck(incoming.itemValue)
                &&!RebirthCookingMergeScope.NeedsCheck(target.itemValue)
            ||__instance.CanSwap(incoming)&&RebirthCookingItemStats.Compatible(incoming.itemValue,target.itemValue);
    }
}

