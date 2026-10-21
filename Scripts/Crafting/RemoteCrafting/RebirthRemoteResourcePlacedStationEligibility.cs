using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

// Readonly UI eligibility only. Uninstalled until the entire placed producer
// qualifies. Native cost/effect calculation and native eligibility loop run.
internal static class RebirthRemoteResourcePlacedStationEligibility
{
    private sealed class Scope
    {internal ItemActionEntryCraft Entry;internal ItemStack[] Input;internal bool Completed;}
    [ThreadStatic]private static Scope active;
    private static bool qualified,installed;
    internal static bool IsInstalled=>installed;
    internal static void Install()
    {
        if(installed)return;
        var harmony=new Harmony("rebirth.remote.resources.placed.eligibility.v1");
        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(NativeEligibility));
            if(!qualified)throw new NotSupportedException("Native placed eligibility callsites are unsupported.");
            installed=true;
        }
        catch{harmony.UnpatchSelf();throw;}
    }
    private static List<ItemStack> Pool(XUiM_PlayerInventory inventory)
    {
        var original=inventory.GetAllItemStacks();var scope=active;
        if(scope==null||original==null||!ReferenceEquals(scope.Entry.ItemController.xui.PlayerInventory,inventory))return original;
        // Native all-stacks remains detached; never mutate its cached list or
        // any UI/native item. These are eligibility candidates, not payment.
        var combined=new List<ItemStack>(original.Count+scope.Input.Length);combined.AddRange(original);
        foreach(var item in scope.Input)if(item!=null&&!item.IsEmpty())combined.Add(item.Clone());
        return combined;
    }
    private static bool FinalNativeInventoryCheck(XUiM_PlayerInventory inventory,IList<ItemStack> requirements,int count)
    {
        var scope=active;
        if(scope!=null&&ReferenceEquals(scope.Entry.ItemController.xui.PlayerInventory,inventory)&&
            ReferenceEquals(scope.Entry.tempIngredientList,requirements))scope.Completed=true;
        return inventory.HasItems(requirements,count);
    }
    [HarmonyPatch(typeof(ItemActionEntryCraft),nameof(ItemActionEntryCraft.hasItems))]
    private static class NativeEligibility
    {
        private static void Prefix(ItemActionEntryCraft __instance,out Scope __state)
        {
            __state=active;active=null;
            if(RebirthRemoteResourcePlacedStationOriginal.ProtocolActive&&RebirthRemoteResourcePlacedStationProjection.TryActualInputPool(__instance,out _,out var input))
                active=new Scope{Entry=__instance,Input=input};
        }
        private static void Postfix(ItemActionEntryCraft __instance,ref bool __result)
        {
            // Reaching the actual final HasItems call proves the native
            // eligibility loop accepted EVERY modified ingredient. A partial
            // early-return list can never be promoted by a count comparison.
            if(active?.Entry==__instance&&active.Completed&&
                RebirthRemoteResourcePlacedStationProjection.TryCurrentShortfall(__instance,out _,out _))__result=true;
        }
        private static Exception Finalizer(Exception __exception,Scope __state)
        {active=__state;return __exception;}
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();int pools=0,checks=0;
            var pool=AccessTools.Method(typeof(XUiM_PlayerInventory),nameof(XUiM_PlayerInventory.GetAllItemStacks),Type.EmptyTypes);
            var check=AccessTools.Method(typeof(XUiM_PlayerInventory),nameof(XUiM_PlayerInventory.HasItems),new[]{typeof(IList<ItemStack>),typeof(int)});
            foreach(var instruction in code)
            {
                if(instruction.Calls(pool)){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(RebirthRemoteResourcePlacedStationEligibility),nameof(Pool));pools++;}
                else if(instruction.Calls(check)){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(RebirthRemoteResourcePlacedStationEligibility),nameof(FinalNativeInventoryCheck));checks++;}
            }
            if(pools!=1||checks!=1)throw new NotSupportedException("Native placed eligibility requires exactly one pool and final inventory check.");
            qualified=true;return code;
        }
    }
}
