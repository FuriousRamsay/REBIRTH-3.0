using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// These native overrides create valid consumer tiles but omit IsPlayerPlaced.
// Record only an actual player callback after native initialization completes.
[HarmonyPatch]
internal static class RebirthCamperEnergyDevicePlacement
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var signature=new[]{typeof(WorldBase),typeof(Chunk),typeof(Vector3i),typeof(BlockValue),typeof(PlatformUserIdentifierAbs)};
        foreach(var type in new[]{typeof(BlockRanged),typeof(BlockPoweredTrap),typeof(BlockLauncher)})
        {
            var method=AccessTools.DeclaredMethod(type,nameof(Block.OnBlockAdded),signature);
            if(method!=null)yield return method;
        }
    }
    private static void Postfix(Block __instance,WorldBase __0,Chunk __1,Vector3i __2,BlockValue __3,PlatformUserIdentifierAbs __4)
    {
        if(__4==null||__3.ischild||__1==null||RebirthCamperWorldLighting.Admission!=null||!ThreadManager.IsMainThread()||!(__0 is World world)||world.IsRemote()||world.IsEditor()||
            !ReferenceEquals(GameManager.Instance?.World,world)||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)return;
        var value=world.GetBlock(__2);
        if(value.ischild||value.type!=__3.type||!ReferenceEquals(value.Block,__instance)||
            !ReferenceEquals(world.ChunkCache?.GetChunkFromWorldPos(__2),__1)||
            !(world.GetTileEntity(__2) is TileEntityPoweredBlock tile)||!ReferenceEquals(tile.chunk,__1))return;
        if(!tile.IsPlayerPlaced)tile.IsPlayerPlaced=true;
    }
}
