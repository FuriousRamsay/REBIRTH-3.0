using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal sealed partial class SavedPendingToolsObservation
    {
        private MobileTerminal mobileTerminal;
        internal bool IsMobile=>mobileTerminal!=null;
        internal IDisposable EnterNativeCallbacks()=>IsMobile?mobileTerminal.Memory?.TryEnterCallbacks():RebirthProductiveEarnedObservationBudget.TryEnterDecoder();
        private bool MobileRetainedContext()
            =>memory!=null&&Enabled&&ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&world?.IsRemote()==false&&
                retained.TryGetValue(original.Station,out var active)&&ReferenceEquals(active,original)&&
                ReferenceEquals(original.MobileTerminal,mobileTerminal)&&ReferenceEquals(mobileTerminal.Body,originalBody)&&
                mobileTerminal.Positive&&!mobileTerminal.Clearing&&original.OutputTimerObserved&&!original.PendingTimerAmbiguous&&
                original.PendingTimerObserved&&original.MobileSuccessor==null&&original.PendingTimerIndex==Index&&
                original.PendingTimerBefore==BeforeTimer&&original.PendingTimerTarget==TargetTimer&&
                original.PendingRecipeBinding==Binding&&original.PendingExtras==Extras;
        private bool MobileMatchesBeforeTools(byte[] tools,RebirthProductiveEarnedObservationBudget.Decoder decoder)
        {
            try{return decoder?.IsCurrent==true&&CurrentContext()&&mobileTerminal.Current()&&
                RebirthProductiveEarnedArtifact.SameTools(RebirthCamperStationSnapshot.Restore(Birth,mobileTerminal.Installed,nativeBefore).Tools,tools);}
            catch{return false;}
        }
        internal bool MatchesNativeAfter(byte[] body,int offset)
        {
            if(!IsMobile)return RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,body,offset);
            try{return offset==0&&CurrentContext()&&artifact.MatchesMarker(mobileTerminal.Station.CopyProductiveEarnedMarker())&&
                RebirthProductiveEarnedMobileOrigin.Canonical(mobileTerminal.Station,mobileTerminal.Installed).SequenceEqual(body);}
            catch{return false;}
        }
        internal bool TryProjectMobileTools(RecipeQueueItem[] queue,ItemStack[] input,ItemStack[] output,ItemStack[] fuel,ItemStack[] tools,
            out byte[] before,out byte[] after)
        {
            before=after=null;
            try
            {
                if(!IsMobile||!MatchesNativeAfter(nativeBefore,0))return false;
                var state=RebirthCamperStationSnapshot.Restore(Birth,mobileTerminal.Installed,nativeBefore);
                if(queue==null||queue.Length!=state.Queue.Length||input!=null&&!SameMobileGrid(input,state.Input)||output!=null&&!SameMobileGrid(output,state.Output)||
                    fuel!=null&&!SameMobileGrid(fuel,state.Fuel)||tools==null||tools.Length!=state.Tools.Length)return false;
                for(int i=0;i<queue.Length;i++)
                    if(!RebirthProductiveEarnedRowProof.CaptureRow(queue[i]).SequenceEqual(RebirthProductiveEarnedRowProof.CaptureRow(state.Queue[i])))return false;
                state.Tools=ItemStack.Clone(tools);before=(byte[])nativeBefore.Clone();
                after=RebirthProductiveEarnedMobileOrigin.Canonical(state,mobileTerminal.Installed);return true;
            }
            catch{before=after=null;return false;}
        }
        private bool TryPublishMobileToolRebase(RebirthProductiveEarnedPendingToolGestures.SavedToolPublication publication)
        {
            if(publication?.Current()!=true||!ReferenceEquals(publication.Scope.Source,this)||!CurrentContext())return false;
            try
            {
                if(publication.Record==null)
                {
                    if(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(artifact.OriginalPath,out var journal))return false;
                    using(journal)
                    {
                        if(journal.PendingInvocation||journal.StationPending||journal.Returned<1||journal.Returned!=publication.Sync.Steps||
                            journal.OwnerStep!=journal.Returned||journal.StationStep!=journal.Returned||journal.Offset!=0||
                            !journal.OriginalDigest.SequenceEqual(originalDigest)||!journal.NativeDigest.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(nativeBefore))||
                            !journal.StationAfterDigest.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(publication.Sync.After)))return false;
                        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                        {
                            writer.Write(0x31424d52);writer.Write(originalDigest);writer.Write(journal.Operation.ToByteArray());
                            writer.Write(journal.RootDigest);writer.Write(journal.HeadDigest);writer.Write(journal.OwnerAfterDigest);
                            writer.Write(nativeBefore.Length);writer.Write(nativeBefore);writer.Write(publication.Sync.After.Length);writer.Write(publication.Sync.After);
                            writer.Flush();publication.Record=stream.ToArray();
                        }
                    }
                }
                if(publication.Retention==null&&!RebirthProductiveEarnedObservationBudget.TryRetainDetached(
                    checked(4L*publication.Record.Length+65536),out publication.Retention))return false;
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(artifact.OriginalPath+".mobile-tools-plan",publication.Record,
                    RebirthProductiveEarnedMobileOrigin.MaximumBytes))return false;
                if(!publication.Current()||mobileTerminal.PendingLocalTools!=null)return false;
                // The original returned gesture, saved PDS and returned native
                // sync are now immutable. Transfer the native save observation
                // to the station original so payer detach cannot strand it.
                mobileTerminal.PendingLocalTools=new MobileLocalTools{Record=publication.Record,
                    After=(byte[])publication.Sync.After.Clone(),Retention=publication.Retention};
                publication.Retention=null;publication.Published=true;return true;
            }
            catch{return false;}
        }
    }
    private static bool SameMobileGrid(ItemStack[] a,ItemStack[] b)
        =>a!=null&&b!=null&&RebirthCamperInventorySnapshot.Capture(a).SequenceEqual(RebirthCamperInventorySnapshot.Capture(b));
    private static bool TryObserveMobilePendingTools(TileEntityWorkstation station,RebirthProductiveEarnedObservationBudget.Decoder decoder,
        out SavedPendingToolsObservation observation)
    {
        observation=null;
        if(!Enabled||decoder?.IsCurrent!=true||!retained.TryGetValue(station,out var original)||
            original.MobileTerminal?.Positive!=true||original.MobileTerminal.Clearing||original.MobileSuccessor!=null||
            original.PendingTimerAmbiguous||!original.PendingTimerObserved||original.Artifact?.Current()!=true||
            original.MobileTerminal.Current()!=true||original.PendingTimerIndex!=station.Queue.Length-1||
            !MatchesPendingDefinition(original,station.Queue[original.PendingTimerIndex]))return false;
        foreach(string suffix in new[]{".mobile-successor",".mobile-successor.invoking",".mobile-tools-plan",".mobile-tools-rebased"})
            if(File.Exists(original.Artifact.OriginalPath+suffix)||File.Exists(original.Artifact.OriginalPath+suffix+".tmp"))return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*original.MobileTerminal.Body.Length+
            4L*original.Artifact.ImageLength+65536),out var memory))return false;
        observation=new SavedPendingToolsObservation(original,memory);return true;
    }
}
