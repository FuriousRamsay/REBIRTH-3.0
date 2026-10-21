using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

[HarmonyPatch(typeof(XUiC_CraftingQueue),nameof(XUiC_CraftingQueue.Update))]
internal static class RebirthProductiveEarnedUiQueueRotation
{
    private static void Prefix(XUiC_CraftingQueue __instance)=>RebirthProductiveEarnedUiCoordinator.BeginQueueHandoff(__instance);
    private static void Postfix(XUiC_CraftingQueue __instance,bool __runOriginal)=>RebirthProductiveEarnedUiCoordinator.EndQueueHandoff(__instance,__runOriginal);
    private static Exception Finalizer(Exception __exception,XUiC_CraftingQueue __instance)
    {if(__exception!=null)RebirthProductiveEarnedUiCoordinator.EndQueueHandoff(__instance,false);return __exception;}
    // Native base.Update still runs its actual children. Only the subsequent
    // CopyTo/SetRecipe rotation waits for this exact original's terminal save.
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,ILGenerator generator)
    {
        var native=AccessTools.Method(typeof(XUiController),nameof(XUiController.Update),new[]{typeof(float)});int count=0;
        foreach(var instruction in instructions)
        {
            yield return instruction;
            if(instruction.opcode!=OpCodes.Call||!Equals(instruction.operand,native))continue;
            count++;var next=generator.DefineLabel();
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(RebirthProductiveEarnedUiCoordinator),nameof(RebirthProductiveEarnedUiCoordinator.HoldQueueRotation)));
            yield return new CodeInstruction(OpCodes.Brfalse,next);
            yield return new CodeInstruction(OpCodes.Ret);
            var resume=new CodeInstruction(OpCodes.Nop);resume.labels.Add(next);yield return resume;
        }
        if(count!=1)throw new InvalidOperationException("UIO1 requires the actual one native queue base.Update boundary.");
    }
}
[HarmonyPatch(typeof(XUiC_CraftingQueue),nameof(XUiC_CraftingQueue.RefreshQueue))]
internal static class RebirthProductiveEarnedUiQueueRefresh
{private static bool Prefix(XUiC_CraftingQueue __instance)=>!RebirthProductiveEarnedUiCoordinator.HoldQueueRefresh(__instance);}
internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal static bool HoldQueueRefresh(XUiC_CraftingQueue queue)=>handoffs.ContainsKey(queue)||HoldQueueRotation(queue);
    internal static bool HoldQueueRotation(XUiC_CraftingQueue queue)
    {
        if(RebirthRecyclerPlacedSettlement.HoldsStation(queue?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity))return true;
        if(!RebirthCookingMillingPublication.CanRotate(queue)||!RebirthProductiveEarnedUiDeferredTransport.CanRotateAuthorityNative(queue))return true;
        if(!RebirthProductiveEarnedUiDeferredTransport.CanRotateKnownReceiptQueue(queue))return true;
        if(!RebirthWorkstationCompletionPipeline.CanRotateCachedPrimary(queue))return true;
        if(handoffs.TryGetValue(queue,out var pending)&&!ReferenceEquals(rotating,pending))return true;
        if(!RebirthProductiveEarnedUiOriginal.Enabled)return false;
        foreach(var original in originals.Values)
            if(ReferenceEquals(original.Binding.Queue,queue)&&original.CompletionAttempted&&!original.TerminalSaved)return true;
        return false;
    }
}

