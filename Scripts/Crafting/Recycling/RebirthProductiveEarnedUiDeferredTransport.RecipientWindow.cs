using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class WindowVersion{internal long Value;}
    private static readonly ConditionalWeakTable<XUiC_WorkstationWindowGroup,WindowVersion> windowVersions=
        new ConditionalWeakTable<XUiC_WorkstationWindowGroup,WindowVersion>();
    private static bool recipientWindowInstalled;
    [ThreadStatic]private static XUiC_WorkstationWindowGroup cachedOpeningWindow;
    [ThreadStatic]private static long cachedOpeningSession;
    internal static bool IsActualCachedWindowOpen(XUiC_WorkstationWindowGroup group,long session)=>session>0&&
        ReferenceEquals(cachedOpeningWindow,group)&&cachedOpeningSession==session;
    internal static bool TryGetCurrentNativeUiSession(XUiC_WorkstationWindowGroup group,out long session)
    {
        session=0;
        if(!ThreadManager.IsMainThread()||group==null||group.WindowGroup?.isShowing!=true||
            !windowVersions.TryGetValue(group,out var version)||version.Value<1)return false;
        session=version.Value;return true;
    }
    internal static bool TryGetObservedNativeUiSession(XUiC_WorkstationWindowGroup group,out long session)
    {
        session=0;if(!ThreadManager.IsMainThread()||group==null||!windowVersions.TryGetValue(group,out var version)||version.Value<1)return false;
        session=version.Value;return true;
    }
    internal static void InstallRecipientWindowObserver()
    {
        if(recipientWindowInstalled)return;var harmony=new Harmony("rebirth.deferred.recipient.window");
        foreach(var type in new[]{typeof(RecipientOpen),typeof(RecipientClose),typeof(RecipientRetarget)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        recipientWindowInstalled=true;
    }
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),nameof(XUiC_WorkstationWindowGroup.OnOpen))]
    private static class RecipientOpen
    {
        private static void Prefix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal,out object __state)
        {__state=RebirthProductiveEarnedUiOwnerFile.BeginLocalOwnerWindowOpen(__instance,__runOriginal);}
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal)
        {
            if(!__runOriginal||!ThreadManager.IsMainThread())return;
            var version=windowVersions.GetValue(__instance,_=>new WindowVersion());version.Value=checked(version.Value+1);
            cachedOpeningWindow=__instance;cachedOpeningSession=version.Value;
            try{RebirthWorkstationCompletionPipeline.ObserveCachedPrimaryWindowOpened(__instance,version.Value);RebirthRecyclerPlacedSettlement.ObserveActualRecyclerHydrationOpen(__instance,version.Value);}
            finally{cachedOpeningWindow=null;cachedOpeningSession=0;}
            RebirthProductiveEarnedUiOwnerFile.ObserveLocalOwnerWindowOpened(__instance,version.Value);
            try{RebirthProductiveEarnedPendingToolGestures.ObserveWindowOpened(__instance);}catch{}
            try{RebirthProductiveEarnedRemoteClient.ObserveRemoteToolsWindow(__instance,version.Value);}catch{}
            var original=hydrating;
            // Only the OnOpen caused inside this exact native Process can bind
            // the recipient window. A later equal reopened window cannot.
            if(original?.ReadCompleted!=true||!original.CurrentSource()||
                !ReferenceEquals(original.Station,__instance.WorkstationData?.TileEntity)||
                !ReferenceEquals(original.Actor,__instance.xui?.playerUI?.entityPlayer))return;
            original.Window=__instance;original.WindowVersion=version.Value;
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {RebirthProductiveEarnedUiOwnerFile.EndLocalOwnerWindowOpen(__state);return __exception;}
    }
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),nameof(XUiC_WorkstationWindowGroup.OnClose))]
    private static class RecipientClose
    {
        private sealed class CloseState{internal object Remote,Local,Cooking;}
        private static void Prefix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal,out CloseState __state)
        {__state=new CloseState{Remote=BeginRemoteRecipientClose(__instance,__runOriginal),Local=RebirthWorkstationCompletionPipeline.BeginLocalCompletionClose(__instance,__runOriginal),Cooking=RebirthCookingMillingPublication.BeginWindowClose(__instance,__runOriginal)};}
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal,CloseState __state)
        {ObserveRemoteRecipientClose(__instance,__runOriginal,__state?.Remote);RebirthWorkstationCompletionPipeline.ObserveLocalCompletionClose(__instance,__runOriginal,__state?.Local);RebirthCookingMillingPublication.ObserveWindowClosed(__instance,__runOriginal,__state?.Cooking);RebirthRecyclerPlacedSettlement.ObserveRecyclerWindowClosed(__instance,__runOriginal);if(__runOriginal&&ThreadManager.IsMainThread()){var version=windowVersions.GetValue(__instance,_=>new WindowVersion());long previous=version.Value;version.Value=checked(version.Value+1);
            RebirthProductiveEarnedUiOwnerFile.ObserveLocalOwnerWindowClosed(__instance,previous);}}
        private static Exception Finalizer(Exception __exception,CloseState __state){RebirthCookingMillingPublication.EndWindowClose(__state?.Cooking);EndRemoteRecipientClose(__state?.Remote);RebirthWorkstationCompletionPipeline.EndLocalCompletionClose(__state?.Local);return __exception;}
    }
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),nameof(XUiC_WorkstationWindowGroup.SetTileEntity))]
    private static class RecipientRetarget
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal)
        {if(__runOriginal&&ThreadManager.IsMainThread()){var version=windowVersions.GetValue(__instance,_=>new WindowVersion());version.Value=checked(version.Value+1);}}
    }
}
