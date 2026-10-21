using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool TryRestoreStagedNativeChild(Attempt parent)
    {
        NativeEarnedChild value=null;RebirthProductiveEarnedSavedNativeSource.Evidence plan=null;
        try
        {
            var terminal=parent?.Terminal;
            if(terminal?.SavedOrigin==null||!terminal.Current()||parent.EarnedChild!=null||parent.PartialSuccessor!=null||
                parent.PendingTimerAmbiguous||!parent.PendingTimerObserved||!RebirthRecyclerNativeLoop.IsCurrentInvocation(parent.Station)||
                !RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativeChild(terminal.SavedOrigin,terminal.Memory.TryEnterCallbacks,out plan))return false;
            using(var callbacks=terminal.Memory.TryEnterCallbacks())
            {
                var frame=plan.Frame;
                if(callbacks==null||!RebirthProductiveEarnedNativeBody.TryMeasure(frame.AfterNative,frame.AfterElapsedOffset,out long graph,out long scratch)||
                    !terminal.Memory.TryReserveAllocation(checked(8L*plan.Image.Length+3*graph+scratch+65536),out var workspace))return false;
                using(workspace)
                {
                    var station=parent.Station;int index=parent.PendingTimerIndex;
                    if(index<0||index!=station.Queue.Length-1||frame.RowIndex!=index||plan.StagedChildLink==null||
                        !RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out var before,out var after,out _,out var afterTools))return false;
                    var row=station.Queue[index];
                    if(row?.Recipe==null||row.Multiplier<=0||row.RepairItem!=null||row.AmountToRepair!=0||!MatchesPendingDefinition(parent,row)||
                        frame.FullRecipeBinding!=parent.PendingRecipeBinding||frame.ExtrasDefinition!=parent.PendingExtras||
                        row.CraftingTimeLeft!=parent.PendingTimerBefore||RebirthRecyclerRuntime.IsMarked(row.Recipe)||RebirthCookingHeat.Managed(row.Recipe)||
                        !TryEarnedNativeChildWork(parent.PendingTimerBefore,parent.PendingTimerTarget,before.OneItemCraftTime,out float work,out float target)||
                        frame.WorkedSeconds!=work||after.CraftingTimeLeft!=target||
                        RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,work)!=work||
                        !RebirthProductiveEarnedArtifact.SameTools(RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,work),frame.AfterTools)||
                        !RebirthProductiveEarnedArtifact.TryBindStagedChild(station,parent.Artifact,plan,out var artifact))return false;
                    value=new NativeEarnedChild{Parent=parent,Row=row,Recipe=row.Recipe,Frame=frame,Tools=afterTools,
                        Image=plan.Image,Link=plan.StagedChildLink,Artifact=artifact,Retention=plan.RetainNativeLoad()};
                    if(value.Retention==null||!value.CurrentBefore()||!plan.PrimaryCurrent())return false;
                    parent.EarnedChild=value;value=null;return true;
                }
            }
        }
        catch{return false;}
        finally{value?.Dispose();plan?.Dispose();}
    }
}
