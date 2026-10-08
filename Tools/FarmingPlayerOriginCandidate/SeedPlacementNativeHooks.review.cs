// TOOLS ONLY. Explicit installer; no Harmony attributes or automatic PatchAll discovery.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
internal static class SeedPlacementNativeHooksReview
{
    private static readonly MethodInfo NativePlace=AccessTools.Method(typeof(BlockToolSelection),"PlaceBlock",
        new[]{typeof(ItemInventoryData),typeof(BlockPlacement.Result),typeof(ItemInventoryData),typeof(Block),typeof(BlockValue)});
    private static readonly MethodInfo NativeSend=AccessTools.Method(typeof(ConnectionManager),"SendToServer",new[]{typeof(NetPackage),typeof(bool)});
    private static readonly Func<BlockToolSelection,ItemInventoryData,BlockPlacement.Result,ItemInventoryData,Block,BlockValue,bool> Place=
        AccessTools.MethodDelegate<Func<BlockToolSelection,ItemInventoryData,BlockPlacement.Result,ItemInventoryData,Block,BlockValue,bool>>(NativePlace);
    private static readonly Action<ConnectionManager,NetPackage,bool> Send=AccessTools.MethodDelegate<Action<ConnectionManager,NetPackage,bool>>(NativeSend);
    internal static bool PlaceWithOrigin(BlockToolSelection tool,ItemInventoryData data,BlockPlacement.Result result,
        ItemInventoryData placementData,Block block,BlockValue nativeValue)
    {
        BlockValue actual=placementData.itemValue.TextureFullArray.IsDefault?result.blockValue:nativeValue;
        using(var local=AdvancedFarmingLocalPlantingAdmission.Begin(data,result,actual))
        using(var remote=SeedPlacementClientScopeReview.Begin(data,result,actual))
        {
            if(remote!=null&&!remote.BeforeNativeInvocation())return false;
            bool placed=Place(tool,data,result,placementData,block,nativeValue);
            if(placed)local?.Complete();
            return placed;
        }
    }
    internal static void SendWithSeedIntent(ConnectionManager connection,NetPackage package,bool flush)
    {
        NetPackageRebirthSeedPlacementReview envelope;
        var native=package as NetPackageSetBlock;
        if(native!=null&&SeedPlacementClientScopeReview.TryConsumeMatchingSend(native,out envelope))
        {
            Send(connection,envelope,flush);
            SeedPlacementClientScopeReview.ObserveNativeSendReturned(envelope); // Normal return only; null connection also returns normally.
        }
        else Send(connection,package,flush);
    }
    private static IEnumerable<CodeInstruction> ReplaceExactlyOne(IEnumerable<CodeInstruction> instructions,MethodInfo original,MethodInfo replacement)
    {
        var code=instructions.ToList();
        int matches=code.Count(i=>(i.opcode==OpCodes.Call||i.opcode==OpCodes.Callvirt)&&Equals(i.operand,original));
        if(original==null||replacement==null||matches!=1)throw new InvalidOperationException("Installed farming callsite shape changed.");
        foreach(var instruction in code)
            if((instruction.opcode==OpCodes.Call||instruction.opcode==OpCodes.Callvirt)&&Equals(instruction.operand,original))
            {instruction.opcode=OpCodes.Call;instruction.operand=replacement;}
        return code; // Labels/exception blocks stay on their original instructions.
    }
    internal static IEnumerable<CodeInstruction> VoxelTranspiler(IEnumerable<CodeInstruction> instructions)
    {return ReplaceExactlyOne(instructions,NativePlace,AccessTools.Method(typeof(SeedPlacementNativeHooksReview),nameof(PlaceWithOrigin)));}
    internal static IEnumerable<CodeInstruction> SendTranspiler(IEnumerable<CodeInstruction> instructions)
    {return ReplaceExactlyOne(instructions,NativeSend,AccessTools.Method(typeof(SeedPlacementNativeHooksReview),nameof(SendWithSeedIntent)));}
    internal static void InstallForReview(Harmony harmony)
    {
        if(harmony==null||NetPackageRebirthSeedPlacementReview.Owner==null||SeedPlacementClientScopeReview.ClientOwner==null)
            throw new InvalidOperationException("Session and terminal owners must be composed before explicit native installation.");
        var voxel=AccessTools.Method(typeof(BlockToolSelection),"ExecuteUseAction",new[]{typeof(ItemInventoryData),typeof(bool),typeof(PlayerActionsLocal)});
        var rpc=AccessTools.Method(typeof(GameManager),"SetBlocksRPC",new[]{typeof(List<BlockChangeInfo>),typeof(PlatformUserIdentifierAbs)});
        var voxelPatch=AccessTools.Method(typeof(SeedPlacementNativeHooksReview),nameof(VoxelTranspiler));
        var sendPatch=AccessTools.Method(typeof(SeedPlacementNativeHooksReview),nameof(SendTranspiler));
        if(voxel==null||rpc==null)throw new InvalidOperationException("Installed native farming methods missing.");
        try
        {
            harmony.Patch(voxel,transpiler:new HarmonyMethod(voxelPatch));
            harmony.Patch(rpc,transpiler:new HarmonyMethod(sendPatch));
        }
        catch
        {
            harmony.Unpatch(voxel,voxelPatch);harmony.Unpatch(rpc,sendPatch);throw;
        }
    }
}