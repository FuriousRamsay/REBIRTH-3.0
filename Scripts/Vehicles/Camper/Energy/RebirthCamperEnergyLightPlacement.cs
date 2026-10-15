using System;
using HarmonyLib;

// Exact native BlockPoweredLight omits tile creation in OnBlockAdded. Restore
// the native powered-block placement lifecycle only for real player additions;
// this is not a scan/reload repair and never adopts an existing native circuit.
[HarmonyPatch(typeof(Block),nameof(Block.OnBlockAdded))]
internal static class RebirthCamperEnergyLightPlacement
{
    private static int diagnostics;
    private static void Postfix(Block __instance,WorldBase __0,Chunk __1,Vector3i __2,BlockValue __3,PlatformUserIdentifierAbs __4)
    {
        if(!(__instance is BlockPoweredLight light)||light.GetType()!=typeof(BlockPoweredLight)||__4==null||__3.ischild||__1==null
            ||!ThreadManager.IsMainThread()||!(__0 is World world)||world.IsRemote()
            ||!ReferenceEquals(GameManager.Instance?.World,world)
            ||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!PowerManager.HasInstance)return;
        var current=world.GetBlock(__2);
        if(current.ischild||current.type!=__3.type||!ReferenceEquals(current.Block,light)
            ||!ReferenceEquals(world.ChunkCache?.GetChunkFromWorldPos(__2),__1)
            ||world.GetTileEntity(__2)!=null||__1.GetTileEntity(World.toBlock(__2))!=null
            ||PowerManager.Instance.PowerItemDictionary.ContainsKey(__2))return;
        TileEntityPowered tile=null;
        try
        {
            tile=light.CreateTileEntity(__1);
            if(!(tile is TileEntityPoweredBlock))throw new InvalidOperationException("Native light factory returned an unsupported tile.");
            tile.SetDisableModifiedCheck(true);
            tile.localChunkPos=World.toBlock(__2);
            // Generic light tiles have no owner field. This native flag is
            // justified only by the callback's actual persistent placing ID.
            // Camper supply still requires its existing native claim owner gate.
            tile.IsPlayerPlaced=true;
            // Same ordering as current BlockPoweredTrap/BlockPowerSource:
            // initialization binds the native node to this tile before AddTileEntity.
            tile.InitializePowerData();
            __1.AddTileEntity(tile);
            tile.SetDisableModifiedCheck(false);
            tile.SetModified();
            Trace("created",__2,null);
        }
        catch(Exception error)
        {
            // Never delete/adopt a node here or manufacture a completion after
            // partial native failure. Keep explicit evidence for qualification.
            if(tile!=null)tile.SetDisableModifiedCheck(false);
            Trace("failed",__2,error.GetType().Name+": "+error.Message);
        }
    }
    private static void Trace(string stage,Vector3i position,string error)
    {
        if(diagnostics>=16)return;diagnostics++;
        Log.Out("[REBIRTH CamperEnergyLightPlacement] stage="+stage+" position="+position+(error==null?"":" error="+error));
    }
}
