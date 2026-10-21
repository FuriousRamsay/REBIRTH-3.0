using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

// Exact actual native cancellation -> actual native UI-to-TE commit. It grants
// no refund/item setter permission. Only the native input-grid refund path that
// fits in full is supported; external drop/owner-bag paths retain the original.
internal static class RebirthProductiveEarnedCancellation
{
    private sealed class Original
    {
        internal XUiC_RecipeStack Card;internal XUiC_WorkstationWindowGroup Window;
        internal EntityPlayerLocal Actor;internal World World;internal string Creation;internal int Deaths;
        internal TileEntityWorkstation Station;internal RebirthProductiveEarnedArtifact.Prepared Artifact;
        internal RebirthProductiveEarnedRowProof.Frame Frame;internal ItemStack[] ExpectedInput;
        internal bool Cancelled;
        internal bool Current()
        {
            return Actor!=null&&!Actor.IsDead()&&Actor.Died==Deaths&&ReferenceEquals(Actor.world,World)&&
                ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&
                RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&ReferenceEquals(Window?.WorkstationData?.TileEntity,Station)&&
                ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Actor)&&Artifact?.Current()==true;
        }
    }
    private static readonly Dictionary<XUiC_WorkstationWindowGroup,Original> pending=new Dictionary<XUiC_WorkstationWindowGroup,Original>();
    [ThreadStatic] private static Original syncing;
    private static bool installed;
    private static bool beforeFlushInstalled;
    internal static void InstallBeforeFlushObserver()
    {
        if(beforeFlushInstalled)return;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.earned.cancel"),typeof(Sync));
        beforeFlushInstalled=true;
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.productive.earned.cancel");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Press));InstallBeforeFlushObserver();
        installed=true;
    }
    internal static bool IsSyncedNativeCancellation(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
        =>syncing!=null&&syncing.Cancelled&&ReferenceEquals(syncing.Station,station)&&ReferenceEquals(syncing.Artifact,artifact)&&syncing.Current();
    private static bool Same(ItemStack[] a,ItemStack[] b)
        =>a!=null&&b!=null&&a.Length==b.Length&&!a.Where((item,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(item,b[index])).Any();
    [HarmonyPatch(typeof(XUiC_RecipeStack),"HandleOnPress")]
    private static class Press
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(XUiC_RecipeStack __instance,bool __runOriginal,out Original __state)
        {
            __state=null;if(!__runOriginal)return true;
            if(RebirthProductiveEarnedUiCoordinator.BlocksCancellation(__instance))return false;
            if(!RebirthProductiveEarnedCoordinator.Enabled)return true;
            try
            {
            var window=__instance.GetParentByType<XUiC_WorkstationWindowGroup>();var station=window?.WorkstationData?.TileEntity;
            if(station==null)return true;
            if(RebirthProductiveEarnedRemoteClient.HasPending(station))return false; // Canonical remote cancellation must settle this same original; no client-only native refund.
            if(!RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out _))return true;
            var actor=__instance.xui?.playerUI?.entityPlayer;
            if(actor==null||actor.world?.IsRemote()!=false||actor.IsDead()||window.windowGroup?.isShowing!=true||
                !ReferenceEquals(window.xui.playerUI.entityPlayer,actor)||LockManager.Instance?.IsLockedByLocalPlayer(station,0)!=true||
                !RebirthProductiveEarnedCoordinator.TryCancellationOriginal(station,__instance.GetRecipe(),__instance.recipeCount,out var artifact,out var frame))return false;
            var input=window.GetChildByType<XUiC_WorkstationInputGrid>();
            if(input==null||window.GetChildByType<XUiC_WorkstationMaterialInputGrid>()!=null||__instance.amountToRepair!=0||
                !Same(input.GetSlots(),station.Input)||pending.ContainsKey(window)||pending.Count>=8)return false;
            var expected=ItemStack.Clone(station.Input);var recipe=__instance.GetRecipe();
            if(recipe?.ingredients==null||recipe.ingredients.Count>8192)return false;
            foreach(var ingredient in recipe.ingredients)
            {
                if(ingredient==null||ingredient.count<0||ingredient.itemValue==null)return false;
                long count=(long)ingredient.count*__instance.recipeCount;if(count==0)continue;
                if(count<1||count>ushort.MaxValue)return false;
                var refund=new ItemStack(ingredient.itemValue.Clone(),(int)count);
                // Real native replacement/merge algorithm, detached target only.
                if(ItemStack.AddToItemStackArray(expected,refund)==-1)return false;
            }
            if(expected.Any(stack=>stack==null||stack.count<0||stack.count>ushort.MaxValue)||
                !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(expected,262144,349528,out _))return false;
            var original=new Original{Card=__instance,Window=window,Actor=actor,World=actor.world,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),
                Deaths=actor.Died,Station=station,Artifact=artifact,Frame=frame,ExpectedInput=expected};
            if(!original.Current())return false;
            pending.Add(window,original);__state=original;return true;
            }
            catch{return false;} // No native refund was invoked; retain original.
        }
        private static void Postfix(XUiC_RecipeStack __instance,bool __runOriginal,Original __state)
        {
            if(!__runOriginal||__state==null)return;
            try
            {
            if(!__state.Current()||__instance.GetRecipe()!=null||
                !Same(__state.Window.GetChildByType<XUiC_WorkstationInputGrid>()?.GetSlots(),__state.ExpectedInput))return;
            __state.Cancelled=true; // Actual native callback, not queue-absence inference.
            }
            catch{/* Native callback happened; keep unresolved original. */}
        }
    }
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),"syncTEfromUI")]
    private static class Sync
    {
        private sealed class Scopes {internal object Earned,Fetched,UiTerminal,Handoff,RemoteInsertion,RemoteClose,Cooking,NativeTools;internal RebirthWorkstationCompletionPipeline.NativeSync Completion;}
        // One dispatch point for deliberately distinct native owner
        // Before purposes. Neither descriptor is convertible to the other's
        // authority, and a skipped native call is observed by neither purpose.
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal,out Scopes __state)
        {
            __state=null;
            if(__instance is XUiC_RebirthCookingStation nativeCookingView&&!nativeCookingView.IsMilling)return true;
            bool receipt=!RebirthRecyclerPlacedSettlement.HoldsStation(__instance.WorkstationData?.TileEntity)&&RebirthCookingMillingPublication.CanSync(__instance)&&RebirthProductiveEarnedUiDeferredTransport.CanSyncAuthorityNative(__instance)&&RebirthWorkstationCompletionPipeline.CanSyncCachedPrimary(__instance)&&RebirthProductiveEarnedUiCoordinator.CanSyncOwnedOriginal(__instance)&&(RebirthProductiveEarnedUiCoordinator.IsExecutingEmptyLocalQueueSync(__instance)||RebirthProductiveEarnedUiDeferredTransport.CanSyncKnownNativeReceipt(__instance))&&RebirthProductiveEarnedUiCoordinator.CanSyncQueueHandoff(__instance);
            receipt=RebirthProductiveEarnedRemoteClient.EnterRemoteToolsSync(__instance,__runOriginal&&receipt,out var nativeTools)&&receipt;
            bool earned=RebirthProductiveEarnedUiOriginal.EnterBeforeFlush(__instance,__runOriginal&&receipt,out var earnedState);
            bool fetched=RebirthRemoteResourcePlacedStationOriginal.EnterBeforeFlush(__instance,__runOriginal&&earned,out var fetchedState);
            bool terminal=RebirthProductiveEarnedUiCoordinator.EnterTerminalSync(__instance,__runOriginal&&earned&&fetched,out var terminalState);
            bool handoff=RebirthProductiveEarnedUiCoordinator.EnterQueueHandoffSync(__instance,__runOriginal&&earned&&fetched&&terminal,out var handoffState);
            RebirthProductiveEarnedUiQueueOwner.BeforeRemoteSync(__instance,__runOriginal&&receipt&&earned&&fetched&&terminal&&handoff,out var remoteInsertion);
            RebirthProductiveEarnedUiDeferredTransport.BeforeRemoteRecipientCloseSync(__instance,__runOriginal&&receipt&&earned&&fetched&&terminal&&handoff,out var remoteClose);
            RebirthWorkstationCompletionPipeline.NativeSync completion=null;
            if(__runOriginal&&receipt&&earned&&fetched&&terminal&&handoff)RebirthWorkstationCompletionPipeline.BeforeNativeSync(__instance,out completion);
            var cooking=RebirthCookingMillingPublication.BeginNativeSync(__instance,__runOriginal&&receipt&&earned&&fetched&&terminal&&handoff);
            if(earnedState!=null||fetchedState!=null||completion!=null||terminalState!=null||handoffState!=null||remoteInsertion!=null||remoteClose!=null||cooking!=null||nativeTools!=null)__state=new Scopes{Earned=earnedState,Fetched=fetchedState,Completion=completion,UiTerminal=terminalState,Handoff=handoffState,RemoteInsertion=remoteInsertion,RemoteClose=remoteClose,Cooking=cooking,NativeTools=nativeTools};
            return receipt&&earned&&fetched&&terminal&&handoff;
        }
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(XUiC_WorkstationWindowGroup __instance,bool __runOriginal,Scopes __state)
        {
            if(__instance is XUiC_RebirthCookingStation nativeCookingView&&!nativeCookingView.IsMilling)return;
            try{RebirthProductiveEarnedRemoteClient.ObserveRemoteToolsSync(__instance,__runOriginal,__state?.NativeTools);}catch{}
            RebirthCookingMillingPublication.ObserveNativeSync(__instance,__runOriginal);
            RebirthWorkstationCompletionPipeline.AfterNativeSync(__instance,__runOriginal,__state?.Completion);
            RebirthProductiveEarnedUiDeferredTransport.AfterRemoteRecipientCloseSync(__instance,__runOriginal,__state?.RemoteClose);
            RebirthProductiveEarnedUiQueueOwner.AfterRemoteSync(__instance,__runOriginal,__state?.RemoteInsertion);
            RebirthProductiveEarnedUiQueueOwner.AfterNativeSync(__instance,__runOriginal);
            RebirthProductiveEarnedUiOriginal.ObserveBeforeFlush(__instance,__runOriginal,__state?.Earned);
            RebirthRemoteResourcePlacedStationOriginal.ObserveBeforeFlush(__instance,__runOriginal,__state?.Fetched);
            RebirthProductiveEarnedUiCoordinator.ObserveTerminalSync(__instance,__runOriginal,__state?.UiTerminal);
            RebirthProductiveEarnedUiCoordinator.ObserveQueueHandoffSync(__instance,__runOriginal,__state?.Handoff);
            try
            {
            if(!__runOriginal||!pending.TryGetValue(__instance,out var original)||!original.Cancelled||!original.Current()||
                !Same(original.Station.Input,original.ExpectedInput)||original.Station.Queue==null)return;
            // The exact previously cancelled native row must no longer exist;
            // this is corroborated by BOTH actual native callbacks above.
            if(original.Station.Queue.Any(row=>row?.Recipe!=null&&row.Multiplier>0&&
                RebirthProductiveEarnedRowProof.CaptureRow(row).SequenceEqual(original.Frame.AfterRow)))return;
            var previous=syncing;syncing=original;
            try
            {
                if(RebirthProductiveEarnedCoordinator.RetireNativeCancellation(original.Station,original.Artifact))pending.Remove(__instance);
            }
            finally{syncing=previous;}
            }
            catch{/* Metadata remains original; no refund/terminal inference. */}
        }
        private static Exception Finalizer(Exception __exception,Scopes __state,XUiC_WorkstationWindowGroup __instance,bool __runOriginal)
        {
            if(__instance is XUiC_RebirthCookingStation nativeCookingView&&!nativeCookingView.IsMilling)return __exception;
            try{RebirthRecipeOutputIntegration.ObserveStationOutputBeforeSync(__instance,__runOriginal,__exception);}
            catch{/* The purpose-owned source stays unqualified; preserve native error and other observers. */}
            RebirthCookingMillingPublication.EndNativeSync(__instance,__exception,__state?.Cooking);RebirthProductiveEarnedUiQueueOwner.EndRemoteSync(__state?.RemoteInsertion);RebirthWorkstationCompletionPipeline.EndRemoteNativeSync(__state?.Completion);return __exception;
        }
    }
}

