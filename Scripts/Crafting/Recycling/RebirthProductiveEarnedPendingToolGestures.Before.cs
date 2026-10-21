using System;

internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    // Preparation at an actual native window-open boundary. This performs no
    // tool action and cannot retroactively qualify an already entered gesture.
    internal static void ObserveWindowOpened(XUiC_WorkstationWindowGroup window)
    {
        if(!RebirthProductiveEarnedCoordinator.Enabled||!ThreadManager.IsMainThread()||executing!=null)return;
        Observation created=null;IDisposable allocation=null;
        RebirthProductiveEarnedCoordinator.SavedPendingToolsObservation source=null;
        try
        {
            var actor=window?.xui?.playerUI?.entityPlayer;var station=window?.WorkstationData?.TileEntity;
            var tools=window?.GetChildByType<XUiC_WorkstationToolGrid>();
            if(actor==null||station==null||tools==null||actor.world?.IsRemote()!=false||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var session))return;
            if(observations.TryGetValue(station,out var prior))
            {
                if(prior.WindowCurrent())return;
                prior.Dispose();
            }
            if(observations.Count>=4)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                    !RebirthProductiveEarnedCoordinator.TryObserveSavedPendingTools(station,decoder,out source)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out allocation))return;
                created=new Observation(allocation){Actor=actor,Station=station,Window=window,Tools=tools,World=actor.world,
                    Owner=owner.StorageKey,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,Session=session,Source=source};
                allocation=null;source=null;created.Before=Frame.Capture(actor,tools);
                if(created.Before==null||!created.Reserve(created.Before)||!created.WindowCurrent()||
                    !created.Source.MatchesBeforeTools(Convert.FromBase64String(created.Before.Cells[2]),decoder)||
                    !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{ItemStack.Empty},262144,349528,out var empty)||created.Before.Cells[3]!=empty)return;
                created.After=created.Before;observations.Add(station,created);created=null;
            }
        }
        catch{/* An unavailable before snapshot cannot authorize a later gesture. */}
        finally{created?.Dispose();allocation?.Dispose();source?.Dispose();}
    }
    private static void PumpOwnerBefore()
    {
        if(!RebirthProductiveEarnedCoordinator.Enabled||executing!=null)return;
        foreach(var scope in System.Linq.Enumerable.ToArray(observations.Values))
        {
            if(scope.BeforeSaveRequested||scope.Steps!=0||scope.Unknown)continue;
            try
            {
                if(!scope.BeforeContext())continue;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null)continue;
                    var live=Frame.Capture(scope.Actor,scope.Tools);
                    if(live==null||!scope.Before.Same(live)){scope.Unknown=true;continue;}
                }
                // One native save request per actual opening, never per frame.
                // Its real FromPlayer/Save callbacks must positively return.
                scope.BeforeSaveRequested=true;GameManager.Instance.SaveLocalPlayerData();
            }
            catch{/* No retries or action replay are inferred from a failed request. */}
        }
    }
    internal static bool TryObserveSavedGestureBefore(EntityPlayerLocal actor,TileEntityWorkstation station,out OwnerCapture capture)
    {
        capture=null;
        try
        {
            if(!ThreadManager.IsMainThread()||executing!=null||station==null||!observations.TryGetValue(station,out var scope)||
                !ReferenceEquals(scope.Actor,actor)||!scope.BeforeContext()||scope.OwnerBefore?.Saved(scope)!=true)return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;var live=Frame.Capture(actor,scope.Tools);
                if(live==null||!scope.Before.Same(live)||!scope.Source.MatchesBeforeTools(Convert.FromBase64String(scope.Before.Cells[2]),decoder))return false;
                capture=scope.OwnerBefore;return true;
            }
        }
        catch{return false;}
    }
}
