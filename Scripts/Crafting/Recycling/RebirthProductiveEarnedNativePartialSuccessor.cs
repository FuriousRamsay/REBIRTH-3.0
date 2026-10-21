using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal sealed class NativePartialSuccessor:IDisposable
    {
        internal Attempt Original;
        internal RecipeQueueItem Row;
        internal Recipe Recipe;
        internal string Binding,Extras;
        internal byte[] Before,After,Record;
        internal int BeforeOffset,AfterOffset;
        internal float BeforeTimer,AfterTimer;
        internal ItemStack[] Tools;
        internal bool Entered,Returned,Closed;
        internal IDisposable Retention;
        internal RebirthProductiveEarnedObservationBudget Memory;
        internal RebirthProductiveEarnedObservationBudget.Observation Allocation;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal bool Current()
        {
            try
            {
                using(var callback=Memory?.TryEnterCallbacks())
                    return callback!=null&&!Closed&&Returned&&Original?.Artifact?.Current()==true&&
                        RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Original.Artifact.OriginalPath+".partial-successor",Record)&&
                        RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Original.Artifact.OriginalPath+".partial-successor.invoking",Record)&&
                        ReferenceEquals(Original.Station.Queue?.LastOrDefault(),Row)&&ReferenceEquals(Row.Recipe,Recipe)&&
                        RebirthProductiveEarnedUiQueueOwner.Binding(Row)==Binding&&RebirthRecipeOutputs.DefinitionIdentity(Recipe)==Extras&&
                        TryNativeMarker(Original.Station,out var marker)&&Original.Artifact.MatchesMarker(marker)&&
                        RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,After,AfterOffset);
            }
            catch{return false;}
        }
        public void Dispose()
        {
            if(Closed)return;Closed=true;var watch=Watch;Watch=null;var allocation=Allocation;Allocation=null;
            var memory=Memory;Memory=null;var retention=Retention;Retention=null;
            Original=null;Row=null;Recipe=null;Tools=null;Before=After=Record=null;
            try{watch?.Dispose();}finally{try{allocation?.Dispose();}finally{try{memory?.Dispose();}finally{retention?.Dispose();}}}
        }
    }
    // Only a positively saved predecessor output/queue/completion terminal can
    // call this. This performs productive work, never another output attempt.
    private static bool TryApplyPartialNativeSuccessor(Attempt original)
    {
        var terminal=original?.Terminal;
        if(terminal?.Current()!=true||original.PendingTimerAmbiguous||!original.PendingTimerObserved||
            !IsNativeSettlementInvocation(original)||original.PartialSuccessor!=null)return false;
        if(terminal.SavedOrigin!=null&&(File.Exists(original.Artifact.OriginalPath+".partial-successor")||
            File.Exists(original.Artifact.OriginalPath+".partial-successor.tmp")))return TryApplyStagedNativePartial(original);
        NativePartialSuccessor value=null;
        try
        {
            var station=original.Station;int index=original.PendingTimerIndex;
            if(index!=station.Queue.Length-1||index<0)return false;
            var row=station.Queue[index];
            if(row?.Recipe==null||row.Multiplier<=0||row.RepairItem!=null||row.AmountToRepair!=0||
                !MatchesPendingDefinition(original,row)||
                RebirthRecyclerRuntime.IsMarked(row.Recipe)||RebirthCookingHeat.Managed(row.Recipe)||
                row.CraftingTimeLeft!=original.PendingTimerBefore||
                !TryPartialNativeWork(original.PendingTimerBefore,original.PendingTimerTarget,row.OneItemCraftTime,
                    out float work,out float afterTimer)||
                RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,work)!=work)return false;
            using(var callback=terminal.Memory.TryEnterCallbacks())
            {
                if(callback==null||!RebirthProductiveEarnedNativeBody.TryMeasure(terminal.Body,terminal.ElapsedOffset,out long sourceGraph,out long sourceScratch)||
                    !terminal.Memory.TryReserveAllocation(checked(8L*(original.Artifact.ImageLength+terminal.Body.Length+262144)+
                        2*sourceGraph+sourceScratch),out var workspace))return false;
                using(workspace)
                {
                    var tools=RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,work);
                    if(!RebirthProductiveEarnedNativeBody.TryCapturePlan(station,tools,index,afterTimer,
                        out var before,out var after,out int beforeOffset,out int afterOffset)||
                        !RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,beforeOffset,terminal.Body,terminal.ElapsedOffset)||
                        !terminal.Current())return false;
                    value=new NativePartialSuccessor{Original=original,Row=row,Recipe=row.Recipe,
                        Binding=RebirthProductiveEarnedUiQueueOwner.Binding(row),Extras=RebirthRecipeOutputs.DefinitionIdentity(row.Recipe),
                        Before=before,After=after,BeforeOffset=beforeOffset,AfterOffset=afterOffset,
                        BeforeTimer=row.CraftingTimeLeft,AfterTimer=afterTimer,Tools=tools};
                    value.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(original.Artifact,original.Frame);
                    if(value.Memory==null)return false;
                    if(string.IsNullOrEmpty(value.Binding)||string.IsNullOrEmpty(value.Extras)||
                        Encoding.UTF8.GetByteCount(value.Binding)>RebirthProductiveEarnedRowProof.MaximumFrameBytes||
                        Encoding.UTF8.GetByteCount(value.Extras)>RebirthProductiveEarnedRowProof.MaximumFrameBytes)return false;
                    using(var buffer=new MemoryStream())using(var writer=new BinaryWriter(buffer,new UTF8Encoding(false,true)))
                    {
                        writer.Write(0x31535052u); // RPS1: exact saved-predecessor partial productive successor.
                        writer.Write(original.Artifact.Birth.ToByteArray());writer.Write(original.Artifact.Operation.ToByteArray());
                        writer.Write(index);writer.Write(work);writer.Write(value.BeforeTimer);writer.Write(afterTimer);
                        writer.Write(value.Binding);writer.Write(value.Extras);
                        writer.Write(beforeOffset);writer.Write(before.Length);writer.Write(before);
                        writer.Write(afterOffset);writer.Write(after.Length);writer.Write(after);writer.Flush();
                        if(buffer.Length>RebirthProductiveEarnedNativeSuccessorRecord.MaximumBytes)return false;value.Record=buffer.ToArray();
                    }
                    if(!RebirthProductiveEarnedNativeBody.TryMeasure(after,afterOffset,out long graph,out _)||
                        !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(before.Length+(long)after.Length+value.Record.Length+
                            graph+2L*(value.Binding.Length+value.Extras.Length)+65536),out value.Retention))return false;
                    string path=original.Artifact.OriginalPath+".partial-successor";
                    if(File.Exists(path+".invoking")||File.Exists(path+".invoking.tmp")||
                        !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,value.Record)||!terminal.Current())return false;
                    // Retain the exact entered attempt before publishing its WAL.
                    // A failed/throwing setter cannot obtain another warm permit.
                    original.PartialSuccessor=value;value=null;
                    var entered=original.PartialSuccessor;entered.Entered=true;
                    if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path+".invoking",entered.Record)||!terminal.Current())return false;
                    station.Tools=entered.Tools;entered.Row.CraftingTimeLeft=entered.AfterTimer;station.setModified();
                    entered.Returned=true;
                    if(!entered.Current())return false;
                    // Hand the one graph-bearing observation phase to the
                    // successor only after all predecessor callbacks retire.
                    terminal.Watch?.Dispose();terminal.Watch=null;
                    terminal.Allocation?.Dispose();terminal.Allocation=null;
                }
            }
            return TryFinishPartialNativeSuccessor(original);
        }
        catch{return false;}
        finally{value?.Dispose();}
    }
    internal static bool TryPartialNativeWork(float before,float target,float unit,out float work,out float after)
    {
        work=0;after=target;
        if(float.IsNaN(before)||float.IsInfinity(before)||float.IsNaN(target)||float.IsInfinity(target)||
            float.IsNaN(unit)||float.IsInfinity(unit)||unit<=0||target<=0)return false;
        // Same-row native += unit includes work already budgeted by the
        // original negative carryover; a cycled row instead subtracts it.
        if(before<0)
        {if(target!=before+unit)return false;work=Math.Min(unit,-before);}
        else
        {if(target>=before)return false;work=before-target;}
        return work>0&&!float.IsNaN(work)&&!float.IsInfinity(work);
    }
    private static bool TryFinishPartialNativeSuccessor(Attempt original)
    {
        if(original?.ClearedNativeMarker==true)return TryFinishClearedNativeMetadata(original);
        var value=original?.PartialSuccessor;
        try
        {
            if(value?.Current()!=true)return false;
            if(original.Terminal!=null)
            {
                original.Terminal.Watch?.Dispose();original.Terminal.Watch=null;
                original.Terminal.Allocation?.Dispose();original.Terminal.Allocation=null;
            }
            if(value.Allocation==null)
            {
                if(!RebirthProductiveEarnedSavedNativeSource.RegisterInvokedPartial(original.Artifact.OriginalPath+(original.Terminal.ToolRebaseCurrent()?".tools-load":".terminal-load"),
                    GameIO.GetSaveGameDir(),original.Artifact.LoadedSource))return false;
                value.Allocation=value.Memory.TryObserveNative(value.After,value.AfterOffset);if(value.Allocation==null)return false;
            }
            if(value.Watch==null)
            {
                var station=original.Station;var world=GameManager.Instance?.World;
                if(!RebirthRemoteResourceNativeBinding.TryResolve(world,RemoteResourceIdentity.Workstation(station.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,original.Artifact.CustodyBirth.ToString("N"),out var binding))return false;
                var p=station.localChunkPos;string key="RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z;
                value.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,value.Current,value.Memory,key);
                if(value.Watch==null)return false;
            }
            if(!RebirthProductiveEarnedSavedNativeSource.Publish(original.Artifact,value.Memory,value.Watch,
                value.After,value.AfterOffset,true,value.Current,partialSuccessor:true)||!value.Current())return false;
            byte[] completedMarker=original.Artifact.Marker(original.Frame);
            var retention=original.Artifact.CaptureCompletionRetention(completedMarker);
            if(retention==null||!ClearExactMarker(original.Station,completedMarker))return false;
            original.CompletionRetention=retention;original.ClearedNativeMarker=true;
            return TryFinishClearedNativeMetadata(original);
        }
        catch{return false;}
    }
}
