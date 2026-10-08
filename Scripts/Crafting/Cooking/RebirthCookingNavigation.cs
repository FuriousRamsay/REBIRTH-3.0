using HarmonyLib;
using System.Linq;
using UnityEngine;

#nullable disable

// Remember only the station behind the current menu visit. Returning reacquires the
// normal workstation lock; no stale workstation UI or tile data is opened directly.
public static class RebirthCookingNavigation
{
    private static XUi context;
    private static World world;
    private static TileEntityWorkstation station;
    private static float requestedUntil;
    private static bool installed;
    public static void Attach(XUiC_RebirthCookingStation owner)
    {
        context=owner.xui;world=context.playerUI.entityPlayer.world;
        station=owner.WorkstationData?.TileEntity;requestedUntil=0;
        if(!installed)
        {
            RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.cooking.navigation"),typeof(RebirthCookingNavigationLifetimePatch));
            installed=true;
        }
    }
    public static bool Available(XUi xui)
    {
        if(context!=xui || station==null)return false;
        var player=xui.playerUI.entityPlayer;
        if(player==null || player.IsDead() || player.world!=world ||
            world.GetTileEntity(station.ToWorldPos())!=station ||
            (player.position-station.ToWorldPos().ToVector3()).sqrMagnitude>64)
        {Clear();return false;}
        return true;
    }
    public static bool Return(XUi xui)
    {
        if(!Available(xui))return false;
        if(XUiC_RebirthCookingWorkspace.ActiveInstance?.IsCookingOpen==true)return true;
        requestedUntil=Time.realtimeSinceStartup+5;
        xui.playerUI.windowManager.CloseAllOpenModalWindows();
        var position=station.ToWorldPos();
        var block=world.GetBlock(position);
        return block.Block.OnBlockActivated(world,position,block,xui.playerUI.entityPlayer);
    }
    public static void Tick(GUIWindowManager manager)
    {
        if(context==null || context.playerUI.windowManager!=manager)return;
        if(!Available(context))return;
        if(!manager.IsModalWindowOpen() && !manager.windowsToOpen.Any(w=>w.isModal) && Time.realtimeSinceStartup>=requestedUntil)Clear();
    }
    private static void Clear(){context=null;world=null;station=null;requestedUntil=0;}
}
[HarmonyPatch(typeof(GUIWindowManager),nameof(GUIWindowManager.Update))]
internal static class RebirthCookingNavigationLifetimePatch
{
    [HarmonyPostfix] private static void Postfix(GUIWindowManager __instance){RebirthCookingNavigation.Tick(__instance);}
}
