using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static bool TryReadMobileToolsSource(Attempt original,ItemStack installed,byte[] terminalBefore,byte[] loadedBody,
        out byte[] after,out byte[] record,out MobileLocalTools pending)
    {
        after=terminalBefore;record=null;pending=null;
        string path=original.Artifact.OriginalPath+".mobile-tools-plan";
        if(!File.Exists(path))
        {
            if(File.Exists(path+".tmp")||File.Exists(original.Artifact.OriginalPath+".mobile-tools-rebased")||
                File.Exists(original.Artifact.OriginalPath+".mobile-tools-rebased.tmp"))return false;
            if(!File.Exists(original.Artifact.OriginalPath+".tool-gesture"))return !File.Exists(original.Artifact.OriginalPath+".tool-gesture.tmp");
            if(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(original.Artifact.OriginalPath,out var observed))return false;
            using(observed)
            {
                if(observed.PendingInvocation||observed.StationPending||observed.OwnerStep!=observed.Returned||observed.Returned<1||
                    observed.Birth!=original.Artifact.Birth||observed.SourceOperation!=original.Artifact.Operation||observed.Offset!=0||
                    !observed.OriginalDigest.SequenceEqual(original.Artifact.OriginalDigest())||observed.WorldKey!=original.Frame.World||
                    observed.SaveRoot!=Path.GetFullPath(GameIO.GetSaveGameDir())||
                    !observed.NativeDigest.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(terminalBefore))||
                    !RebirthProductiveEarnedPendingToolGestures.TryCopyMobileStationReturned(original.Artifact.OriginalPath,observed,out var actualAfter)||
                    !actualAfter.SequenceEqual(loadedBody)&&!terminalBefore.SequenceEqual(loadedBody)||
                    !ValidMobileToolsOnly(original.Artifact.Birth,installed,terminalBefore,actualAfter))return false;
                byte[] recovered;
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    writer.Write(0x31424d52);writer.Write(original.Artifact.OriginalDigest());writer.Write(observed.Operation.ToByteArray());
                    writer.Write(observed.RootDigest);writer.Write(observed.HeadDigest);writer.Write(observed.OwnerAfterDigest);
                    writer.Write(terminalBefore.Length);writer.Write(terminalBefore);writer.Write(actualAfter.Length);writer.Write(actualAfter);
                    writer.Flush();recovered=stream.ToArray();
                }
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,recovered,RebirthProductiveEarnedMobileOrigin.MaximumBytes))return false;
            }
        }
        if(!ReadMobileRecord(path,out var bytes))return false;
        try
        {
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=0x31424d52||!reader.ReadBytes(32).SequenceEqual(original.Artifact.OriginalDigest()))return false;
                Guid gesture=new Guid(reader.ReadBytes(16));byte[] root=reader.ReadBytes(32),head=reader.ReadBytes(32),owner=reader.ReadBytes(32);
                byte[] before=ReadMobileBody(reader),body=ReadMobileBody(reader);
                if(stream.Position!=stream.Length||!before.SequenceEqual(terminalBefore)||
                    !ValidMobileToolsOnly(original.Artifact.Birth,installed,before,body)||
                    !RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(original.Artifact.OriginalPath,out var journal))return false;
                using(journal)
                {
                    if(journal.Operation!=gesture||journal.Birth!=original.Artifact.Birth||journal.SourceOperation!=original.Artifact.Operation||
                        journal.WorldKey!=original.Frame.World||journal.SaveRoot!=Path.GetFullPath(GameIO.GetSaveGameDir())||
                        !journal.OriginalDigest.SequenceEqual(original.Artifact.OriginalDigest())||journal.Offset!=0||
                        journal.Index!=original.PendingTimerIndex||journal.BeforeTimer!=original.PendingTimerBefore||journal.TargetTimer!=original.PendingTimerTarget||
                        journal.Binding!=original.PendingRecipeBinding||journal.Extras!=original.PendingExtras||journal.PendingInvocation||journal.StationPending||
                        journal.Returned<1||journal.OwnerStep!=journal.Returned||journal.StationStep!=journal.Returned||
                        !journal.RootDigest.SequenceEqual(root)||!journal.HeadDigest.SequenceEqual(head)||!journal.OwnerAfterDigest.SequenceEqual(owner)||
                        !journal.NativeDigest.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(before))||
                        !journal.StationAfterDigest.SequenceEqual(RebirthProductiveEarnedMobileOrigin.Digest(body)))return false;
                }
                string saved=original.Artifact.OriginalPath+".mobile-tools-rebased";
                byte[] digest=RebirthProductiveEarnedMobileOrigin.Digest(bytes);
                if(ReadMobileRecord(saved,out var receipt))
                {if(!receipt.SequenceEqual(digest))return false;}
                else
                {
                    // Exact actual native installed full After qualifies the
                    // station save which a prior process had not published as
                    // compact metadata. No native gesture/PDS/write is replayed.
                    if(loadedBody.SequenceEqual(before))
                    {
                        pending=new MobileLocalTools{Record=bytes,Before=before,After=body,Reentry=true};
                        return true;
                    }
                    if(!loadedBody.SequenceEqual(body)||!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(saved,digest))return false;
                }
                after=body;record=bytes;return true;
            }
        }
        catch{return false;}
    }
    private static bool ValidMobileToolsOnly(Guid installation,ItemStack installed,byte[] before,byte[] after)
    {
        var old=RebirthCamperStationSnapshot.Restore(installation,installed,before);
        var next=RebirthCamperStationSnapshot.Restore(installation,installed,after);
        old.Tools=ItemStack.Clone(next.Tools);
        return RebirthProductiveEarnedMobileOrigin.Canonical(old,installed).SequenceEqual(after);
    }
}
