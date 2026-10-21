using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static bool TryRestoreClosedAuthority(RemoteHostOriginal source,LoadedStation loaded)=>ClosedAuthorityCompletion.RestoreActualLoaded(source,loaded);
    internal sealed partial class ClosedAuthorityCompletion
    {
    internal bool RestoredActualLoad{get;private set;}
    internal static bool RestoreActualLoaded(RemoteHostOriginal source,LoadedStation loaded)
    {
        if(source==null||loaded?.Current()!=true||!ReferenceEquals(source.ColdStation,loaded)||
            !loaded.MatchesOriginal(source.Image)||!source.Current())return false;
        var original=XElement.Parse(Encoding.UTF8.GetString(source.Image));
        var claim=original.Element("closedAuthority");if(claim==null)return true;
        if(source.Purpose!="complete"||closedAuthorityCompletions.Count>=4||
            !Guid.TryParseExact((string)claim.Attribute("ticket"),"N",out var ticket)||ticket==Guid.Empty||
            !Guid.TryParseExact((string)claim.Attribute("group"),"N",out var group)||group==Guid.Empty||
            closedAuthorityCompletions.Any(value=>value.Operation==source.Operation)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(source.Peer,out var identity)||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return false;
        ClosedAuthorityCompletion token=null;bool retained=false;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".closed-authority."+ticket.ToString("N")+".xml");
                long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
                if(length<1||length>12L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(12L*length+65536)))return false;
                var receipt=RebirthWorkstationCompletionPublication.ReadPrimary(path);
                var saved=receipt?.Element("recyclerClosedAuthoritySource");
                var native=saved?.Elements("recyclerClosedAuthorityOriginal").SingleOrDefault();
                if(receipt?.Name!="workstationCompletionIntent"||(string)receipt.Attribute("operation")!=source.Operation.ToString("N")||
                    (string)receipt.Attribute("world")!=source.WorldKey||!HasClosedSourceProvenance(saved,native)||
                    (string)saved.Attribute("ticket")!=ticket.ToString("N")||native==null)return false;
                byte[] image=Encoding.UTF8.GetBytes(native.ToString(SaveOptions.DisableFormatting));
                if(image.Length>8*1024*1024||RebirthStationPublicationRecord.Digest(image)!=(string)claim.Attribute("nativeOriginal")||
                    (string)native.Attribute("operation")!=source.Operation.ToString("N")||(string)native.Attribute("group")!=group.ToString("N")||
                    !ColdBytes(native.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes).SequenceEqual(source.BeforeNative)||
                    !ColdBytes(native.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes).SequenceEqual(source.AfterNative)||
                    !RebirthProductiveEarnedNativeBody.TryRead(source.BeforeNative,source.BeforeOffset,out var before,out _)||
                    !RebirthProductiveEarnedNativeBody.TryRead(source.AfterNative,source.AfterOffset,out var after,out _)||before.Queue.Length==0)return false;
                var returns=RebirthCamperInventorySnapshot.Restore(ColdBytes(native.Element("returns"),4*1024*1024));
                if(returns==null||!SameOwnerGrid(returns,RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("returns"),4*1024*1024))))return false;
                var queue=(RecipeQueueItem[])before.Queue.Clone();var row=queue[queue.Length-1];
                queue[queue.Length-1]=new RecipeQueueItem{StartingEntityId=-1};
                var projected=ProjectNativeCycle(queue);
                if(row?.Recipe==null||projected==null||projected.Length!=after.Queue.Length||
                    projected.Where((entry,index)=>!RebirthProductiveEarnedRowProof.CaptureRow(entry).SequenceEqual(
                        RebirthProductiveEarnedRowProof.CaptureRow(after.Queue[index]))).Any()||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,
                    Math.Max(checked(12L*length+65536),checked(16L*(image.Length+source.BeforeNative.Length+source.AfterNative.Length)+65536))))return false;
                token=new ClosedAuthorityCompletion(source.Actor,source.Peer,identity,source.Station,row,source.Operation,group,
                    (float)original.Attribute("workSeconds"),after.Input,queue,after.Output,after.Tools,returns,
                    source.BeforeNative,source.AfterNative,source.BeforeOffset,source.AfterOffset,image,memory,ticket){ViewedOrigin=native.Element("nativeViewer")!=null};
                memory=null;
                // Only this genuine loaded original can attach a restored token.
                // No current-row equality is used to issue an earlier runtime event.
                token.Attached=source;source.ClosedAuthority=token;
                if(!token.MatchesOwnerOriginal(original)||!loaded.Current()||!loaded.MatchesOriginal(source.Image))return false;
                token.RestoredActualLoad=true;closedAuthorityCompletions.Add(token);retained=true;return true;
            }
        }
        catch{return false;}
        finally
        {
            if(!retained){if(ReferenceEquals(source.ClosedAuthority,token))source.ClosedAuthority=null;token?.Dispose();}
            memory?.Dispose();
        }
    }
}
}

