using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal sealed partial class SavedPendingToolsObservation
    {
        internal bool TryPublishToolRebase(RebirthProductiveEarnedPendingToolGestures.SavedToolPublication publication)
        {
            if(mobileTerminal!=null)return TryPublishMobileToolRebase(publication);
            if(publication?.Current()!=true||!ReferenceEquals(publication.Scope.Source,this)||!CurrentContext())return false;
            if(publication.Memory==null)
            {
                publication.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(artifact,original.Frame);
                if(publication.Memory==null)return false;
            }
            if(publication.Watch==null)
            {
                if(!RebirthRemoteResourceNativeBinding.TryResolve(world,RemoteResourceIdentity.Workstation(original.Station.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,artifact.CustodyBirth.ToString("N"),out var binding))return false;
                var p=original.Station.localChunkPos;
                publication.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,publication.Current,publication.Memory,
                    "RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z);
            }
            if(publication.Watch==null||!publication.Watch.TryReadPublishedTileDescriptor(out _,out _,out _,out _)||!publication.Current())return false;
            if(publication.Record==null)
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(nativeBefore.Length+publication.Sync.After.Length)+65536),out var held))return false;
                try
                {
                    if(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(artifact.OriginalPath,out var journal))return false;
                    using(journal)
                    {
                        if(journal.PendingInvocation||journal.StationPending||journal.Returned!=publication.Sync.Steps||journal.OwnerStep!=journal.Returned||journal.StationStep!=journal.Returned)return false;
                        var value=new RebirthProductiveEarnedPendingToolRebaseRecord.Description{Birth=Birth,Source=Operation,Gesture=journal.Operation,
                            Index=Index,BeforeTimer=BeforeTimer,TargetTimer=TargetTimer,Step=journal.Returned,
                            Original=CopyOriginalDigest(),JournalRoot=(byte[])journal.RootDigest.Clone(),JournalHead=(byte[])journal.HeadDigest.Clone(),
                            OwnerAfter=(byte[])journal.OwnerAfterDigest.Clone(),Before=CopyNativeBefore(),BeforeOffset=Offset,
                            After=(byte[])publication.Sync.After.Clone(),AfterOffset=publication.Sync.AfterOffset};
                        var record=RebirthProductiveEarnedPendingToolRebaseRecord.Encode(value);
                        if(!publication.Current()||!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(artifact.OriginalPath+".tools-rebased",record)||
                            !RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(artifact.OriginalPath,artifact.CopyImage(),value.After,value.AfterOffset,out _,out _))return false;
                        publication.Record=record;publication.Retention=held;held=null;
                    }
                }
                finally{held?.Dispose();}
            }
            if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(artifact.OriginalPath+".tools-rebased",publication.Record)||
                !RebirthProductiveEarnedSavedNativeSource.Publish(artifact,publication.Memory,publication.Watch,publication.Sync.After,
                    publication.Sync.AfterOffset,true,publication.Current,true,Index,BeforeTimer,TargetTimer,false,false,true)||!publication.Current())return false;
            // No native transfer is invoked here. Adopt only the positively
            // published actual returned sync and retain its immutable WAL.
            var next=terminal.Memory.TryObserveNative(publication.Sync.After,publication.Sync.AfterOffset);
            if(next==null)return false;
            var prior=terminal.Allocation;terminal.Allocation=next;
            terminal.Body=(byte[])publication.Sync.After.Clone();terminal.ElapsedOffset=publication.Sync.AfterOffset;
            terminal.ToolRebaseRecord=publication.Record;terminal.ToolRebaseRetention=publication.Retention;publication.Retention=null;
            publication.Published=true;prior?.Dispose();return true;
        }
    }
}
