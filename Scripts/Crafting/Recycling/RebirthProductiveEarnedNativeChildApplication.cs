using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool TryApplyEarnedNativeChild(Attempt parent)
    {
        var child=parent?.EarnedChild;
        if(child==null||child.Entered||child.Artifact==null||!IsNativeSettlementInvocation(parent))return false;
        try
        {
            using(var callbacks=parent.Terminal.Memory.TryEnterCallbacks())
            {
                if(callbacks==null||!child.CurrentBefore()||!child.Artifact.Current()||
                    !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(parent.Artifact.OriginalPath+".earned-child",child.Link))return false;
                if(!RebirthProductiveEarnedNativeBody.TryMeasure(child.Frame.AfterNative,child.Frame.AfterElapsedOffset,out long graph,out long scratch)||
                    !parent.Terminal.Memory.TryReserveAllocation(checked(8L*child.Image.Length+2*graph+scratch+65536),out var workspace))return false;
                using(workspace)
                {
                string invoking=parent.Artifact.OriginalPath+".earned-child.invoking";
                if(File.Exists(invoking)||File.Exists(invoking+".tmp")){child.Entered=true;return false;}
                byte[] beforeMarker=parent.Artifact.Marker(parent.Frame),afterMarker=child.Artifact.Marker(child.Frame);
                child.Next=new Attempt{Station=parent.Station,Row=child.Row,Recipe=child.Recipe,Multiplier=child.Frame.Multiplier,
                    Tools=child.Tools,Frame=child.Frame,Artifact=child.Artifact,NativeTickCustody=parent.NativeTickCustody,
                    NativeAdoptionReleaseReady=parent.NativeAdoptionReleaseReady};
                // The target is derived from the frozen full native child plan,
                // not recalculated from elapsed time at this later invocation.
                if(!RebirthProductiveEarnedRowProof.TryDecodeNative(child.Frame,out _,out var after,out _,out _))return false;
                child.Next.Target=after.CraftingTimeLeft;
                child.Entered=true;
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(invoking,child.Link)||!child.CurrentBefore())return false;
                parent.Station.Tools=child.Tools;child.Row.CraftingTimeLeft=child.Next.Target;parent.Station.setModified();
                if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(parent.Station,child.Frame.AfterNative,child.Frame.AfterElapsedOffset)||
                    !ReplaceExactNativeMarker(parent.Station,beforeMarker,afterMarker))return false;
                child.Returned=true;
                // The predecessor cannot observe native After once its marker
                // changes. Release its graph phase before creating the child's.
                parent.Terminal.Watch?.Dispose();parent.Terminal.Watch=null;
                parent.Terminal.Allocation?.Dispose();parent.Terminal.Allocation=null;
                RebirthProductiveEarnedSavedNativeSource.ReleaseRegisteredTerminal(parent.Artifact.Birth,parent.Artifact.Operation);
                }
            }
            return TryFinishEarnedNativeChild(parent);
        }
        catch{return false;} // Entered/unknown never obtains another setter permit.
    }
    private static bool TryFinishEarnedNativeChild(Attempt parent)
    {
        var child=parent?.EarnedChild;
        if(child?.Returned!=true||child.Next==null)return false;
        try
        {
            // Resource cleanup may itself have been interrupted after the
            // returned setters. Retry cleanup, never those setters.
            if(parent.Terminal!=null)
            {
                parent.Terminal.Watch?.Dispose();parent.Terminal.Watch=null;
                parent.Terminal.Allocation?.Dispose();parent.Terminal.Allocation=null;
            }
            RebirthProductiveEarnedSavedNativeSource.ReleaseRegisteredTerminal(parent.Artifact.Birth,parent.Artifact.Operation);
            var next=child.Next;
            if(next.Observation!=null)
            {
                using(var callbacks=next.Observation.Memory.TryEnterCallbacks())
                    if(callbacks==null||!next.Observation.Current()||!EarnedChildAfter(child))return false;
            }
            else
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder==null||!EarnedChildAfter(child))return false;
            if(next.Observation==null)
            {
                if(!IsNativeSettlementInvocation(parent))return false;
                if(!RebirthProductiveEarnedSavedNativeSource.RegisterInvokedChild(parent.Artifact.OriginalPath+(parent.Terminal.ToolRebaseCurrent()?".tools-load":".terminal-load"),
                    child.Artifact.OriginalPath,GameIO.GetSaveGameDir(),child.Image))return false;
                next.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(parent.Station,child.Artifact);
                if(next.Observation==null)return false;
            }
            if(!next.Observation.Saved()||!RebirthProductiveEarnedSavedNativeSource.Publish(child.Artifact,
                next.Observation.Memory,next.Observation.Placed,child.Frame.AfterNative,child.Frame.AfterElapsedOffset,false,
                next.Observation.Current))return false;
            // No native output executes here. The ordinary AllowsCompletion
            // gate must observe this saved child under its own real invocation.
            next.Retention=child.Retention;child.Retention=null;
            retained[parent.Station]=next;child.Next=null;parent.EarnedChild=null;
            parent.Terminal?.Dispose();parent.Terminal=null;parent.Observation?.Dispose();parent.Observation=null;
            RebirthProductiveEarnedSavedNativeSource.Release(parent.Artifact.LoadedSource);
            parent.Tools=null;parent.Frame=null;parent.PendingDefinitionRetention?.Dispose();parent.Retention?.Dispose();parent.Retention=null;child.Dispose();return true;
        }
        catch{return false;}
    }
    private static bool EarnedChildAfter(NativeEarnedChild child)
    {
        var parent=child.Parent;
        return !child.Closed&&child.Returned&&child.Artifact.Current()&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(parent.Artifact.OriginalPath+".earned-child",child.Link)&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(parent.Artifact.OriginalPath+".earned-child.invoking",child.Link)&&
            ReferenceEquals(parent.Station.Queue?.LastOrDefault(),child.Row)&&ReferenceEquals(child.Row.Recipe,child.Recipe)&&
            RebirthProductiveEarnedUiQueueOwner.Binding(child.Row)==child.Frame.FullRecipeBinding&&
            RebirthRecipeOutputs.DefinitionIdentity(child.Recipe)==child.Frame.ExtrasDefinition&&
            TryNativeMarker(parent.Station,out var marker)&&child.Artifact.MatchesMarker(marker)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(parent.Station,child.Frame.AfterNative,child.Frame.AfterElapsedOffset);
    }
    private static bool ReplaceExactNativeMarker(TileEntityWorkstation station,byte[] before,byte[] after)
    {
        if(station==null||station is RebirthCamperStationState||before==null||after==null||
            !TryNativeMarker(station,out var current)||!current.SequenceEqual(before))return false;
        var chunk=station.GetChunk();var p=station.localChunkPos;string key="RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z;
        if(chunk==null||!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)||marker.TriggerWriteDataDelegate!=null||
            marker.isSavedToNetwork||marker.expiresInWorldTime!=ulong.MaxValue||marker.data==null||!marker.data.SequenceEqual(before))return false;
        marker.data=(byte[])after.Clone();chunk.isModified=true;station.setModified();
        return ReferenceEquals(chunk.ChunkCustomData.dict[key],marker)&&marker.data.SequenceEqual(after);
    }
}
