using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    [ThreadStatic] private static RebirthCamperStationState mobileSettlementStation;
    internal sealed class MobileSuccessor:IDisposable
    {
        internal Attempt Parent,Next;
        internal RecipeQueueItem Row;
        internal ItemStack[] Tools;
        internal byte[] Before,After,Record,ChildOrigin;
        internal float Target;
        internal bool Entered,Returned,ProjectionReady;
        internal IDisposable Retention;
        public void Dispose(){Next?.Observation?.Dispose();Retention?.Dispose();Retention=null;Before=After=Record=ChildOrigin=null;Tools=null;Next=null;Parent=null;Row=null;}
    }
    internal static bool IsMobileSettlementSource(TileEntityWorkstation station)
        =>ReferenceEquals(mobileSettlementStation,station)&&station is RebirthCamperStationState&&
            retained.TryGetValue(station,out var original)&&original.MobileTerminal?.Positive==true;
    internal static bool IsMobileChildPreparation(TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame frame)
        =>IsMobileSettlementSource(station)&&retained.TryGetValue(station,out var original)&&
            ReferenceEquals(original.MobileSuccessor?.Next?.Frame,frame)&&original.MobileTerminal.Current();
    private static bool ContinueMobilePending(Attempt parent)
    {
        var terminal=parent?.MobileTerminal;
        if(terminal?.Positive!=true||parent.PendingTimerAmbiguous)return false;
        if(parent.MobileSuccessor?.Returned==true)return FinishMobileSuccessor(parent);
        if(!IsMobileSettlementSource(parent.Station)||terminal.Current()!=true)return false;
        try
        {
            var station=terminal.Station;
            var value=parent.MobileSuccessor;
            if(value==null)
            {
                int index=parent.PendingTimerIndex;
                if(index<0||index!=station.Queue.Length-1)return false;
                var row=station.Queue[index];
                if(row?.Recipe==null||row.Multiplier<=0||row.RepairItem!=null||row.AmountToRepair!=0||
                    !MatchesPendingDefinition(parent,row)||row.CraftingTimeLeft!=parent.PendingTimerBefore||
                    RebirthRecyclerRuntime.IsMarked(row.Recipe)||RebirthCookingHeat.Managed(row.Recipe))return false;
                bool child=parent.PendingTimerTarget<=0;
                float work,target;
                if(!(child?TryEarnedNativeChildWork(parent.PendingTimerBefore,parent.PendingTimerTarget,row.OneItemCraftTime,out work,out target):
                    TryPartialNativeWork(parent.PendingTimerBefore,parent.PendingTimerTarget,row.OneItemCraftTime,out work,out target))||
                    RebirthProductiveToolWork.Limit(station.Tools,row.Recipe,station.block,work)!=work)return false;
                var tools=RebirthProductiveToolWork.Wear(station.Tools,row.Recipe,station.block,work);
                value=new MobileSuccessor{Parent=parent,Row=row,Tools=tools,Target=target,Before=(byte[])terminal.Body.Clone()};
                var detached=RebirthCamperStationSnapshot.Restore(station.InstallationId,terminal.Installed,value.Before);
                detached.Tools=tools;detached.Queue[index].CraftingTimeLeft=target;
                value.After=RebirthProductiveEarnedMobileOrigin.Canonical(detached,terminal.Installed);
                if(child)
                {
                    if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(station.Tools,262144,349528,out var beforeTools)||
                        !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(tools,262144,349528,out var afterTools))return false;
                    var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=station.InstallationId,Operation=Guid.NewGuid(),World=parent.Frame.World,
                        RowIndex=index,Multiplier=row.Multiplier,WorkedSeconds=work,BeforeRow=RebirthProductiveEarnedRowProof.CaptureRow(row),
                        AfterRow=RebirthProductiveEarnedRowProof.CaptureRow(detached.Queue[index]),BeforeTools=Convert.FromBase64String(beforeTools),
                        AfterTools=Convert.FromBase64String(afterTools),FullRecipeBinding=RebirthProductiveEarnedUiQueueOwner.Binding(row),
                        ExtrasDefinition=RebirthRecipeOutputs.DefinitionIdentity(row.Recipe)};
                    if(!RebirthProductiveEarnedMobileOrigin.TryCapture(station,frame,tools,target,out value.ChildOrigin))return false;
                    frame.MobileOriginDigest=RebirthProductiveEarnedMobileOrigin.Digest(value.ChildOrigin);
                    value.Next=new Attempt{Station=station,Row=row,Recipe=row.Recipe,Multiplier=row.Multiplier,Frame=frame,Target=target,
                        Tools=tools,MobileOriginal=value.ChildOrigin};
                }
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*(value.Before.Length+value.After.Length)+
                    4L*(value.ChildOrigin?.Length??0)+1048576+(value.Next==null?0:
                        4L*RebirthStationSnapshotEvidence.MaximumSnapshotBytes+2L*(RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace/3))),out value.Retention))return false;
                // Keep exact child identity before any immutable publication.
                parent.MobileSuccessor=value;
            }
            if(value.Entered)return false;
            if(value.Next!=null)
            {
                if(value.Next.Artifact==null)
                {
                    if(!RebirthProductiveEarnedArtifact.TryPrepare(station,value.Next.Frame,out var childArtifact))return false;
                    value.Next.Artifact=childArtifact;
                }
                // Preparation retains the exact child even if its companion
                // write fails. Every retry must publish/read back that SAME
                // origin before entering any successor timer/tool work.
                if(!RebirthProductiveEarnedMobileOrigin.Publish(value.Next.Artifact,value.ChildOrigin))return false;
            }
            if(value.Record==null)
            {
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    writer.Write(0x31534d52);writer.Write(parent.Artifact.OriginalDigest());
                    writer.Write(value.Next!=null);writer.Write(value.Next?.Artifact.Operation.ToByteArray()??Guid.Empty.ToByteArray());
                    writer.Write(value.Next?.Artifact.OriginalDigest()??new byte[32]);
                    writer.Write(value.Target);writer.Write(parent.PendingRecipeBinding);writer.Write(parent.PendingExtras);
                    writer.Write(value.Before.Length);writer.Write(value.Before);writer.Write(value.After.Length);writer.Write(value.After);
                    writer.Flush();value.Record=stream.ToArray();
                }
            }
            string path=parent.Artifact.OriginalPath+".mobile-successor";
            if(File.Exists(path+".invoking")||File.Exists(path+".invoking.tmp")||
                !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,value.Record,RebirthProductiveEarnedMobileOrigin.MaximumBytes)||
                !terminal.Current())return false;
            if(value.Next!=null)
            {
                byte[] link;
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    writer.Write(0x31504d52);writer.Write(parent.Artifact.Operation.ToByteArray());writer.Write(parent.Artifact.OriginalDigest());
                    writer.Write(RebirthProductiveEarnedMobileOrigin.Digest(value.Record));writer.Flush();link=stream.ToArray();
                }
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(value.Next.Artifact.OriginalPath+".mobile-parent",link))return false;
            }
            value.Entered=true;
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path+".invoking",value.Record,RebirthProductiveEarnedMobileOrigin.MaximumBytes)||
                !terminal.Current())return false;
            station.Tools=value.Tools;value.Row.CraftingTimeLeft=value.Target;
            if(value.Next!=null)station.RestoreProductiveEarnedMarker(true,value.Next.Artifact.Marker(value.Next.Frame));
            station.setModified();value.Returned=true;
            value.ProjectionReady=RebirthCamperStationOpenQueueProcessing.RefreshAfterOriginal(station);
            terminal.Save?.Dispose();terminal.Save=null;terminal.Saved?.Dispose();terminal.Saved=null;
            terminal.Body=value.After;
            return FinishMobileSuccessor(parent);
        }
        catch{return false;}
    }
    private static bool FinishMobileSuccessor(Attempt parent)
    {
        var value=parent.MobileSuccessor;var terminal=parent.MobileTerminal;
        if(value?.Returned!=true||terminal==null)return false;
        try
        {
            var station=terminal.Station;
            if(!value.ProjectionReady)
            {if(!RebirthCamperStationOpenQueueProcessing.RefreshAfterOriginal(station))return false;value.ProjectionReady=true;}
            bool current=parent.Artifact.Current()&&RebirthProductiveEarnedMobileOrigin.Canonical(station,terminal.Installed).SequenceEqual(value.After)&&
                (value.Next==null?parent.Artifact.MatchesMarker(station.CopyProductiveEarnedMarker()):value.Next.Artifact.MatchesAfter(station)&&
                    value.Next.Artifact.MatchesMarker(station.CopyProductiveEarnedMarker()));
            if(!current)return false;
            Func<bool> exact=()=>parent.Artifact.Current()&&
                RebirthProductiveEarnedMobileOrigin.Canonical(station,terminal.Installed).SequenceEqual(value.After)&&
                (value.Next==null?parent.Artifact.MatchesMarker(station.CopyProductiveEarnedMarker()):
                    value.Next.Artifact.MatchesMarker(station.CopyProductiveEarnedMarker()));
            if(terminal.Save==null&&terminal.Saved==null)
            {
                var runtime=RebirthCamperModuleRuntime.For(station.BoundVehicle);
                terminal.Save=RebirthCamperStationSaveEvidence.BeginProcessingRecovery(station,
                    new RebirthCamperStationAddress(runtime.Identity,station.InstallationId),exact,terminal.Memory);
            }
            if(terminal.Saved==null)
            {if(!RebirthCamperStationSaveEvidence.TryObserve(terminal.Save,out var publication))return false;terminal.Saved=publication;}
            using(var callback=terminal.Memory.TryEnterCallbacks())
                if(callback==null||!terminal.Saved.ReadStationSnapshot(saved=>
                    RebirthProductiveEarnedMobileOrigin.Canonical(saved,terminal.Installed).SequenceEqual(value.After)&&
                    (value.Next==null?parent.Artifact.MatchesMarker(saved.CopyProductiveEarnedMarker()):
                        value.Next.Artifact.MatchesMarker(saved.CopyProductiveEarnedMarker())))||!exact()||
                    !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(parent.Artifact.OriginalPath+".mobile-successor-saved",
                        RebirthProductiveEarnedMobileOrigin.Digest(value.Record)))return false;
            if(value.Next==null)
            {
                parent.PendingTimerObserved=false;return BeginMobileClear(parent);
            }
            // The independently saved full child source takes over the native
            // marker; the immutable parent output is metadata-only thereafter.
            var next=value.Next;
            if(!RebirthProductiveEarnedMobileOrigin.PublishInitialReturned(station,next.Artifact,next.Frame))return false;
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(parent.Artifact.OriginalPath+".mobile-released",
                RebirthProductiveEarnedMobileOrigin.Digest(terminal.Record)))return false;
            RecordMobileReleasedStatus(parent);
            terminal.Dispose();parent.MobileTerminal=null;value.Next=null;
            next.Retention=value.Retention??parent.Retention;
            if(value.Retention==null)parent.Retention=null;
            value.Retention=null;parent.MobileSuccessor=null;
            parent.PendingDefinitionRetention?.Dispose();parent.Retention?.Dispose();
            retained[station]=next;
            next.MobileInitialSaved=true;
            // A fresh primary native readback is still required before output.
            return true;
        }
        catch{return false;}
    }
}
