using System;
using System.Linq;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    // Positive original local native sync only; not a saved region publication
    // or a replacement/rebase permit. No native sync or setter is invoked here.
    internal sealed class NativeToolSync:IDisposable
    {
        internal Observation Scope;internal byte[] Before,After,EntryDigest;
        internal int BeforeOffset,AfterOffset,Steps;internal bool Observed,Finished;
        private IDisposable allocation;
        internal NativeToolSync(IDisposable retained){allocation=retained;}
        internal bool Current()
        {
            try{using(var decoder=Scope?.Source?.EnterNativeCallbacks())return decoder!=null&&allocation!=null&&Observed&&Scope?.ReturnedContext()==true&&Scope.Steps==Steps&&ReferenceEquals(Scope.StationSync,this)&&
                Scope.Source.MatchesNativeAfter(After,AfterOffset);}
            catch{return false;}
        }
        public void Dispose(){var held=System.Threading.Interlocked.Exchange(ref allocation,null);Scope=null;Before=After=EntryDigest=null;held?.Dispose();}
    }
    [ThreadStatic]private static NativeToolSync syncingTools;
    private static RecipeQueueItem ToolSyncRow(XUiC_RecipeStack card)=>new RecipeQueueItem{
        Recipe=card.GetRecipe(),Multiplier=checked((short)card.GetRecipeCount()),CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),
        IsCrafting=card.IsCrafting,Quality=checked((byte)card.OutputQuality),StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()};
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),"syncTEfromUI")]
    private static class StationSyncObserver
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal,out NativeToolSync __state)
        {
            __state=null;if(!__runOriginal||syncingTools!=null||executing!=null||!ThreadManager.IsMainThread()||!RebirthProductiveEarnedCoordinator.Enabled)return;
            NativeToolSync created=null;IDisposable allocation=null;
            try
            {
                var actor=__instance?.xui?.playerUI?.entityPlayer;var station=__instance?.WorkstationData?.TileEntity;
                if(actor==null||station==null||!TryObserveReturnedGesture(actor,station,out var scope)||scope.Journal==null||
                    !ReferenceEquals(scope.Window,__instance)||scope.StationSync!=null||__instance.craftingQueue==null||
                    !ReferenceEquals(__instance.toolWindow,scope.Tools))return;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*1024*1024+65536,out allocation))return;
                    var cards=__instance.craftingQueue.GetRecipesToCraft();if(cards==null||cards.Length<1||cards.Length>255||cards.Any(card=>card==null))return;
                    var source=scope.Source.CopyNativeBefore();
                    if(scope.Source.IsMobile)
                    {
                        if(!scope.Source.TryProjectMobileTools(cards.Select(ToolSyncRow).ToArray(),__instance.inputWindow?.GetSlots(),
                            __instance.outputWindow?.GetSlots(),__instance.fuelWindow?.GetSlots(),scope.Tools.GetSlots(),out var mobileBefore,out var mobileAfter)||
                            !RebirthProductiveEarnedArtifact.SameTools(scope.Tools.GetSlots(),Convert.FromBase64String(scope.After.Cells[2])))return;
                        created=new NativeToolSync(allocation){Scope=scope,Before=mobileBefore,After=mobileAfter,BeforeOffset=0,AfterOffset=0,Steps=scope.Steps};allocation=null;
                        if(!scope.Journal.EnterStationSync(scope,mobileBefore,0,mobileAfter,0,out created.EntryDigest))return;
                        __state=syncingTools=created;created=null;return;
                    }
                    if(source==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source,scope.Source.Offset)||
                        !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(station,cards.Select(ToolSyncRow).ToArray(),
                            __instance.inputWindow?.GetSlots(),__instance.outputWindow?.GetSlots(),__instance.fuelWindow?.GetSlots(),scope.Tools.GetSlots(),
                            out var before,out var after,out int beforeOffset,out int afterOffset)||
                        !RebirthProductiveEarnedNativeBody.ValidPendingToolReplacement(source,scope.Source.Offset,after,afterOffset,scope.Source.Index,scope.Source.BeforeTimer)||
                        !RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var projected,out _)||
                        !RebirthProductiveEarnedArtifact.SameTools(projected.Tools,Convert.FromBase64String(scope.After.Cells[2])))return;
                    created=new NativeToolSync(allocation){Scope=scope,Before=before,After=after,BeforeOffset=beforeOffset,AfterOffset=afterOffset,Steps=scope.Steps};allocation=null;
                    if(!scope.Journal.EnterStationSync(scope,before,beforeOffset,after,afterOffset,out created.EntryDigest))return;
                    __state=syncingTools=created;created=null;
                }
            }
            catch{/* Native sync admission remains owned by existing native/receipt guards. */}
            finally{created?.Dispose();allocation?.Dispose();}
        }
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal,NativeToolSync __state)
        {
            if(__state==null||!ReferenceEquals(syncingTools,__state))return;
            try
            {
                var scope=__state.Scope;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(!__runOriginal||decoder==null||scope?.ReturnedContext()!=true||scope.Steps!=__state.Steps||
                        !ReferenceEquals(scope.Window,__instance)||!scope.After.Same(Frame.Capture(scope.Actor,scope.Tools))||
                        !scope.Source.MatchesNativeAfter(__state.After,__state.AfterOffset)||
                        !scope.Journal.ReturnStationSync(scope,__state.EntryDigest,__state.After,__state.AfterOffset))return;
                    __state.Observed=true;scope.StationSync=__state;
                }
            }
            catch{/* An unobserved original sync stays unresolved on disk. */}
        }
        private static Exception Finalizer(Exception __exception,NativeToolSync __state)
        {
            if(__state!=null&&ReferenceEquals(syncingTools,__state))
            {
                syncingTools=null;__state.Finished=true;
                if(__exception!=null||!__state.Observed)
                {if(__state.Scope!=null)__state.Scope.Unknown=true;__state.Dispose();}
            }
            return __exception;
        }
    }
}
