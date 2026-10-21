using System;
using System.IO;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool TryRestorePendingNativeTerminal(TileEntityWorkstation station)
    {
        if(!Enabled||!(RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station)))return false;
        ObserveWorld();if(retained.ContainsKey(station)||retained.Count>=4)return false;
        Attempt value=null;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedSavedNativeSource.TryObserved(station,out var source)||
                    !source.Terminal||!source.PendingNextWork||source.PendingTimerAmbiguous||
                    source.PendingTimerIndex<0||source.PendingTimerIndex!=station.Queue.Length-1||
                    !RebirthProductiveEarnedArtifact.TryLoadObservedPending(station,out var artifact))return false;
                string path=artifact.OriginalPath;
                // Existing successor custody requires its own typed recovery;
                // an old saved prefix cannot grant another native application.
                foreach(string suffix in source.ToolsRebased?new[]{".partial-successor.invoking",".earned-child.invoking"}:new[]{".partial-successor.invoking",".earned-child.invoking",".tool-gesture"})
                    if(File.Exists(path+suffix)||File.Exists(path+suffix+".tmp"))return false;
                if(File.Exists(path+".partial-successor.tmp")||File.Exists(path+".earned-child.tmp"))return false;
                var row=station.Queue[source.PendingTimerIndex];var frame=artifact.ReadFrame();
                string binding=source.PendingRecipeBinding??frame.FullRecipeBinding,extras=source.PendingExtras??frame.ExtrasDefinition;
                if(row?.Recipe==null||row.Multiplier<=0||row.RepairItem!=null||row.AmountToRepair!=0||
                    row.CraftingTimeLeft!=source.PendingTimerBefore||RebirthRecyclerRuntime.IsMarked(row.Recipe)||
                    RebirthCookingHeat.Managed(row.Recipe)||!frame.HasRecipeDefinition||
                    RebirthProductiveEarnedUiQueueOwner.Binding(row)!=binding||
                    RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)!=extras)return false;
                // Legacy REL2 has only the original recipe's full definition.
                // A different cycled recipe needs its own saved definition,
                // never a current XML/getter substitute.
                value=new Attempt{Station=station,Row=row,Recipe=row.Recipe,Multiplier=frame.Multiplier,Frame=frame,Artifact=artifact,
                    NativeTickCustody=RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station),
                    OutputTimerObserved=true,PendingTimerObserved=true,PendingTimerIndex=source.PendingTimerIndex,
                    PendingTimerBefore=source.PendingTimerBefore,PendingTimerTarget=source.PendingTimerTarget,
                    PendingRecipeBinding=binding,PendingExtras=extras};
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(2L*(binding.Length+extras.Length)+128),out value.PendingDefinitionRetention))return false;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*artifact.ImageLength),out value.Retention))return false;
                value.Terminal=new NativeTerminal{Original=value,World=GameManager.Instance.World,Body=source.Native,
                    ElapsedOffset=source.NativeOffset,PendingNextWork=true,SavedOrigin=source};
                value.Terminal.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(artifact,frame);
                if(value.Terminal.Memory==null)return false;
            }
            value.Terminal.Allocation=value.Terminal.Memory.TryObserveNative(value.Terminal.Body,value.Terminal.ElapsedOffset);
            if(value.Terminal.Allocation==null||!value.Terminal.Current())return false;
            ParkSavedPendingTerminal(value.Terminal);
            retained.Add(station,value);value=null;return true;
        }
        catch{return false;}
        finally{if(value!=null){value.Terminal?.Dispose();value.Retention?.Dispose();value.PendingDefinitionRetention?.Dispose();}}
    }
}
