using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool TryApplyStagedNativePartial(Attempt original)
    {
        var terminal=original?.Terminal;NativePartialSuccessor value=null;
        RebirthProductiveEarnedSavedNativeSource.Evidence plan=null;
        try
        {
            if(terminal?.SavedOrigin==null||!terminal.Current()||original.PartialSuccessor!=null||
                original.PendingTimerAmbiguous||!original.PendingTimerObserved||
                !RebirthRecyclerNativeLoop.IsCurrentInvocation(original.Station)||
                !RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativePartial(terminal.SavedOrigin,terminal.Memory.TryEnterCallbacks,out plan))return false;
            using(var callback=terminal.Memory.TryEnterCallbacks())
            {
                if(callback==null||!RebirthProductiveEarnedNativeBody.TryMeasure(plan.Native,plan.NativeOffset,out long graph,out long scratch)||
                    !terminal.Memory.TryReserveAllocation(checked(8L*(plan.Native.Length+original.Artifact.ImageLength+262144)+3*graph+scratch+65536),out var workspace))return false;
                using(workspace)
                {
                    var station=original.Station;int index=original.PendingTimerIndex;
                    if(index<0||index!=station.Queue.Length-1||plan.StagedPartialRecord==null||
                        !RebirthProductiveEarnedNativeBody.TryRead(plan.Native,plan.NativeOffset,out var after,out _))return false;
                    var row=station.Queue[index];
                    if(row?.Recipe==null||row.Multiplier<=0||row.RepairItem!=null||row.AmountToRepair!=0||
                        !MatchesPendingDefinition(original,row)||plan.PendingRecipeBinding!=original.PendingRecipeBinding||
                        plan.PendingExtras!=original.PendingExtras||row.CraftingTimeLeft!=original.PendingTimerBefore||
                        RebirthRecyclerRuntime.IsMarked(row.Recipe)||RebirthCookingHeat.Managed(row.Recipe)||
                        !TryPartialNativeWork(original.PendingTimerBefore,original.PendingTimerTarget,row.OneItemCraftTime,out float work,out float target)||
                        RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,work)!=work||
                        !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(after.Tools,262144,349528,out var encodedTools)||
                        !RebirthProductiveEarnedArtifact.SameTools(RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,work),Convert.FromBase64String(encodedTools)))return false;
                    value=new NativePartialSuccessor{Original=original,Row=row,Recipe=row.Recipe,
                        Binding=plan.PendingRecipeBinding,Extras=plan.PendingExtras,Before=terminal.Body,BeforeOffset=terminal.ElapsedOffset,
                        After=plan.Native,AfterOffset=plan.NativeOffset,Record=plan.StagedPartialRecord,
                        BeforeTimer=original.PendingTimerBefore,AfterTimer=target,Tools=after.Tools};
                    value.Retention=plan.RetainNativeLoad();
                    value.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(original.Artifact,original.Frame);
                    string path=original.Artifact.OriginalPath+".partial-successor",invoking=path+".invoking";
                    if(value.Retention==null||value.Memory==null||!plan.PrimaryCurrent()||!terminal.Current()||
                        !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path,value.Record)||File.Exists(invoking)||File.Exists(invoking+".tmp"))return false;
                    // Preserve this exact plan before entering its first native
                    // application. An uncertain WAL or setter is never retried.
                    original.PartialSuccessor=value;value=null;var entered=original.PartialSuccessor;entered.Entered=true;
                    if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(invoking,entered.Record)||!plan.PrimaryCurrent()||!terminal.Current())return false;
                    station.Tools=entered.Tools;entered.Row.CraftingTimeLeft=entered.AfterTimer;station.setModified();entered.Returned=true;
                    if(!entered.Current())return false;
                    terminal.Watch?.Dispose();terminal.Watch=null;terminal.Allocation?.Dispose();terminal.Allocation=null;
                }
            }
            return TryFinishPartialNativeSuccessor(original);
        }
        catch{return false;}
        finally{value?.Dispose();plan?.Dispose();}
    }
}
