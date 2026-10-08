using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// Native partial merges only compare item types. Supply the incoming value while they run.
[HarmonyPatch]
public static class RebirthCookingPartialMergeScope
{
    [ThreadStatic] internal static ItemValue Incoming;
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return RequireTarget(typeof(Bag), "TryStackItem", typeof(int), typeof(ItemStack));
        yield return RequireTarget(typeof(Inventory), "TryStackItem", typeof(int), typeof(ItemStack));
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
}
[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanStackPartly))]
public static class RebirthCookingPartialMergeCheck
{
    static bool Prefix(ItemStack __instance, ref bool __result)
    {
        RebirthLiquidStackCapacityPatch.Apply(__instance?.itemValue?.ItemClass);
        var incoming=RebirthCookingPartialMergeScope.Incoming;
        if(incoming==null || RebirthCookingItemStats.Compatible(__instance.itemValue,incoming))return true;
        __result=false;return false;
    }
}
[HarmonyPatch(typeof(ItemStack), nameof(ItemStack.StackTransferCount))]
public static class RebirthCookingTransferCountCheck
{
    static void Prefix(ItemStack __instance) { RebirthLiquidStackCapacityPatch.Apply(__instance?.itemValue?.ItemClass); }
    static void Postfix(ItemStack __instance, ItemStack __0, ref int __result)
    { if(__result>0&&!RebirthCookingItemStats.Compatible(__instance.itemValue,__0.itemValue))__result=0; }
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
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleItemInspect))]
public static class RebirthCookingClickPickup
{
    static bool Prefix(XUiC_ItemStack __instance)
    {
        var owner=XUiC_RebirthCookingWorkspace.ActiveInstance;
        if(owner?.IsCookingOpen!=true||owner.xui!=__instance.xui)return true;
        if(__instance.StackLock||__instance.ItemStack.IsEmpty()||!__instance.xui.DragAndDropWindow.IsEmpty())return true;
        if(!__instance.xui.playerUI.CursorController.GetMouseButtonUp(UICamera.MouseButton.LeftButton))return true;
        __instance.SwapItem();
        __instance.HandleClickComplete();
        return false;
    }
}
[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleStackSwap))]
public static class RebirthCookingMouseMergeCheck
{
    static bool Prefix(XUiC_ItemStack __instance)
    {
        var incoming=__instance.xui.DragAndDropWindow.CurrentStack;
        var target=__instance.ItemStack;
        if(incoming.IsEmpty()||target.IsEmpty()||RebirthCookingItemStats.Compatible(incoming.itemValue,target.itemValue))return true;
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
        return incoming.IsEmpty() || __instance.CanSwap(incoming) && (target.IsEmpty()||RebirthCookingItemStats.Compatible(incoming.itemValue,target.itemValue));
    }
}

