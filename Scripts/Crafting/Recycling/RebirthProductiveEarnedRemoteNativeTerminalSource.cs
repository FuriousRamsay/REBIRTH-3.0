using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    // Source publication only. Called after the actual original native owner
    // upload returned and its full native station publication was observed.
    internal static bool PublishRemoteNativeTerminalSource(RebirthProductiveEarnedArtifact.Prepared artifact,
        RebirthProductiveEarnedRowProof.Frame frame,RebirthProductiveEarnedNativeOutputPlan.Description plan,
        RebirthProductiveEarnedObservationBudget memory,RebirthRemoteResourceNativePublication.Watch watch,Func<bool> current)
    {
        if(current?.Invoke()!=true||artifact?.Current()!=true||plan==null||memory==null||watch==null)return false;
        if(artifact.LoadedSource is RebirthProductiveEarnedSavedNativeSource.Evidence loaded&&loaded.Terminal&&loaded.PrimaryCurrent()&&
            loaded.Frame.Birth==artifact.Birth&&loaded.Frame.Operation==artifact.Operation&&loaded.PendingNextWork==(plan.PendingIndex>=0)&&
            loaded.PendingTimerIndex==plan.PendingIndex&&loaded.PendingTimerBefore==plan.PendingBefore&&loaded.PendingTimerTarget==plan.PendingTarget&&
            loaded.CurrentIdentity(GameManager.Instance?.World,watch.Tile as TileEntityWorkstation,artifact.Birth,artifact.Operation)&&
            RebirthProductiveEarnedNativeBody.SameExceptElapsed(loaded.Native,loaded.NativeOffset,plan.Native,plan.Offset))
            return current(); // Actual installed saved terminal already supplies this source; never rewrite its immutable descriptor.
        Attempt value=null;
        try
        {
            var station=watch.Tile as TileEntityWorkstation;if(station==null)return false;
            value=new Attempt{Station=station,Frame=frame,Artifact=artifact,OutputTimerObserved=true,PendingTimerObserved=plan.PendingIndex>=0,
                PendingTimerIndex=plan.PendingIndex,PendingTimerBefore=plan.PendingBefore,PendingTimerTarget=plan.PendingTarget,
                PendingRecipeBinding=plan.Binding,PendingExtras=plan.Extras};
            value.Terminal=new NativeTerminal{Original=value,World=GameManager.Instance.World,Body=plan.Native,ElapsedOffset=plan.Offset,
                Memory=memory,PendingNextWork=plan.PendingIndex>=0};
            if(plan.PendingIndex>=0&&!PublishPendingDefinition(value))return false;
            if(!current()||!watch.TryReadPublishedTileDescriptor(out var publication,out long offset,out int length,out string digest)||
                !RebirthProductiveEarnedSavedNativeSource.Publish(artifact,memory,watch,plan.Native,plan.Offset,true,current,
                    plan.PendingIndex>=0,plan.PendingIndex,plan.PendingBefore,plan.PendingTarget))return false;
            return current()&&artifact.TryPublishNativeTerminal(plan.Native,plan.Offset,publication,offset,length,digest)&&current();
        }
        finally{value?.PendingDefinitionRetention?.Dispose();}
    }
}
