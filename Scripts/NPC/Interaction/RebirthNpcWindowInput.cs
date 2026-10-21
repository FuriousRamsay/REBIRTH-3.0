using HarmonyLib;

internal static class RebirthNpcWindowInput
{
    internal static readonly string[] Groups={"rebirthCompanions","rebirthCompanionInteraction"};
    internal static ulong OpenRevision;
}

// One native entry point covers every Open overload, including Creative and
// NPC-to-Companions navigation. HUD-only windows do not dismiss an NPC screen.
[HarmonyPatch(typeof(GUIWindowManager),"openInternal")]
internal static class RebirthNpcWindowExclusivity
{
    internal static void Install()=>RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.npc.window.exclusivity"),typeof(RebirthNpcWindowExclusivity));
    private static void Prefix(GUIWindowManager __instance,GUIWindow _w,bool _bModal)
    {
        if(_w==null||__instance.IsWindowOpen(_w)||__instance.IsFullHUDDisabled())return;
        bool npc=System.Array.IndexOf(RebirthNpcWindowInput.Groups,_w.Id)>=0;
        if(!npc&&!_bModal&&!_w.alwaysUsesMouseCursor&&!_w.HasActionSet())return;
        RebirthNpcWindowInput.OpenRevision++;
        foreach(string group in RebirthNpcWindowInput.Groups)
            if(group!=_w.Id&&__instance.IsWindowOpen(group))__instance.Close(group);
        if(npc)_w.alwaysUsesMouseCursor=true;
    }
}
