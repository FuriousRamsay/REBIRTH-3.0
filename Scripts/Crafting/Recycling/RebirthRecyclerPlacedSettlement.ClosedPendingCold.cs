using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static bool HasClosedSourceProvenance(XElement saved,XElement native)
    {
        if(saved==null||native==null)return false;
        var viewer=native.Element("nativeViewer");
        if((bool?)saved.Attribute("actualRuntimeRow")==true)
            return (bool?)saved.Attribute("actualNativeUi")!=true&&viewer==null;
        return (bool?)saved.Attribute("actualRuntimeRow")==false&&(bool?)saved.Attribute("actualNativeUi")==true&&
            native.Elements("nativeViewer").Count()==1&&viewer!=null&&!viewer.Elements().Any()&&viewer.Attributes().Count()==3&&
            int.TryParse((string)viewer.Attribute("entity"),out int entity)&&entity>0&&
            int.TryParse((string)viewer.Attribute("deaths"),out int deaths)&&deaths>=0&&
            !string.IsNullOrWhiteSpace((string)viewer.Attribute("creation"))&&((string)viewer.Attribute("creation")).Length<=71;
    }
    private static bool MatchesClosedPendingMarker(XElement header,XElement original)
    {
        if(header.Name!="recyclerClosedAuthorityPending"||(int?)original.Attribute("version")!=1||
            !Guid.TryParseExact((string)header.Attribute("ticket"),"N",out var ticket)||ticket==Guid.Empty)return false;
        if(original.Name=="recyclerClosedAuthorityOriginal")return (string)header.Attribute("original")==
            RebirthWorkstationCompletionOriginal.Hash(original.ToString(SaveOptions.DisableFormatting));
        var claim=original.Element("closedAuthority");
        if((string)claim?.Attribute("ticket")!=ticket.ToString("N"))return false;
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),(string)original.Attribute("operation")+".closed-authority."+ticket.ToString("N")+".xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>12L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out var charge))return false;
        using(charge)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var saved=record?.Element("recyclerClosedAuthoritySource");
            var native=saved?.Element("recyclerClosedAuthorityOriginal");if(record?.Name!="workstationCompletionIntent"||native==null||
                (string)record.Attribute("operation")!=(string)original.Attribute("operation")||(string)record.Attribute("world")!=(string)original.Attribute("world")||
                saved.Elements().Count()!=1||(int?)native.Attribute("version")!=1||
                !HasClosedSourceProvenance(saved,native)||(string)saved.Attribute("ticket")!=ticket.ToString("N"))return false;
            string nativeText=native.ToString(SaveOptions.DisableFormatting);
            if((string)header.Attribute("original")!=RebirthWorkstationCompletionOriginal.Hash(nativeText)||
                (string)claim.Attribute("nativeOriginal")!=RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(nativeText)))return false;
            foreach(string name in new[]{"operation","owner","account","creation","deaths","world","x","y","z","block","beforeOffset","afterOffset","workSeconds"})
                if((string)native.Attribute(name)!=(string)original.Attribute(name))return false;
            return (string)claim.Attribute("group")== (string)native.Attribute("group")&&
                XNode.DeepEquals(native.Element("beforeNative"),original.Element("beforeNative"))&&
                XNode.DeepEquals(native.Element("afterNative"),original.Element("afterNative"))&&XNode.DeepEquals(native.Element("returns"),original.Element("returns"));
        }
    }
    private static ColdStationCandidate ReadClosedPendingCandidate(string root,Guid operation,World world,int x,int z,Stream stream)
    {
        ColdStationCandidate selected=null;
        foreach(string path in Directory.EnumerateFiles(root,operation.ToString("N")+".closed-pending.*.xml"))
        {
            var candidate=ReadColdStationCandidate(path,world,x,z);if(candidate==null)continue;
            if(!HasUniqueNativeSpan(stream,candidate.MarkerSpan)){candidate.Dispose();continue;}
            if(selected!=null){candidate.Dispose();selected.Dispose();return null;}selected=candidate;
        }
        return selected;
    }
    private static bool HasArchivedClosedNativeOriginal(Guid operation,Guid ticket,XElement original)
    {
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".closed-authority."+ticket.ToString("N")+".xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>12L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out var charge))return false;
        using(charge)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var saved=record?.Element("recyclerClosedAuthoritySource");
            return record?.Name=="workstationCompletionIntent"&&(string)record.Attribute("operation")==operation.ToString("N")&&
                (string)record.Attribute("world")== (string)original.Attribute("world")&&(string)saved?.Attribute("ticket")==ticket.ToString("N")&&
                HasClosedSourceProvenance(saved,original)&&saved.Elements().Count()==1&&XNode.DeepEquals(saved.Elements().Single(),original);
        }
    }
    internal sealed partial class ClosedAuthorityCompletion
    {
        internal static ClosedAuthorityCompletion RestorePending(LoadedStation loaded,RemoteLoadedOwner owner)
        {
            var peer=owner?.CurrentPeer();
            if(peer==null||loaded?.Current()!=true||loaded.Stage!="before"||!ReferenceEquals(owner.File.world,loaded.World)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||closedAuthorityCompletions.Count>=4||
                closedAuthorityCompletions.Any(value=>value.Operation==loaded.Operation)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(
                    checked(24L*loaded.MeasureOriginal()+65536),out var memory))return null;
            ClosedAuthorityCompletion token=null;bool retained=false;
            try
            {
                using var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)return null;
                byte[] image=loaded.CopyOriginal();var original=XElement.Parse(Encoding.UTF8.GetString(image));
                if(original.Name!="recyclerClosedAuthorityOriginal"||(string)original.Attribute("owner")!=identity.StorageKey||
                    (string)original.Attribute("account")!=identity.CanonicalId||(string)original.Attribute("creation")!=owner.Creation||
                    (int?)original.Attribute("deaths")!=owner.Deaths||!PhaseBytes(owner.File.buffs,"rbRecyclerPlaced_"+loaded.Operation.ToString("N"),out float phase)||phase!=0||
                    !PhaseBytes(owner.File.buffs,"rbRecyclerSlot_"+loaded.Operation.ToString("N"),out float count)||count!=0)return null;
                var header=RebirthCookingRemoteAdmission.ReadProtocolXml(Encoding.UTF8.GetString(loaded.MarkerImage),65536);
                if(!MatchesClosedPendingMarker(header,original)||!Guid.TryParseExact((string)header.Attribute("ticket"),"N",out var ticket)||
                    !Guid.TryParseExact((string)original.Attribute("group"),"N",out var group)||group==Guid.Empty||
                    !HasArchivedClosedNativeOriginal(loaded.Operation,ticket,original))return null;
                byte[] before=ColdBytes(original.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),after=ColdBytes(original.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                int beforeOffset=(int)original.Attribute("beforeOffset"),afterOffset=(int)original.Attribute("afterOffset");
                if(!RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var prior,out _)||!RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var projected,out _)||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(loaded.Station,before,beforeOffset)||prior.Queue.Length==0||loaded.Station.Queue?.Length!=prior.Queue.Length)return null;
                var queue=prior.Queue;queue[queue.Length-1]=new RecipeQueueItem{StartingEntityId=-1};
                var expected=ProjectNativeCycle(queue);if(expected==null||expected.Length!=projected.Queue.Length||expected.Where((row,index)=>
                    !RebirthProductiveEarnedRowProof.CaptureRow(row).SequenceEqual(RebirthProductiveEarnedRowProof.CaptureRow(projected.Queue[index]))).Any())return null;
                var returns=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("returns"),4*1024*1024));if(returns==null)return null;
                token=new ClosedAuthorityCompletion(owner.Actor,peer,identity,loaded.Station,loaded.Station.Queue.Last(),loaded.Operation,group,(float)original.Attribute("workSeconds"),
                    projected.Input,queue,projected.Output,projected.Tools,returns,before,after,beforeOffset,afterOffset,image,memory,ticket){ViewedOrigin=original.Element("nativeViewer")!=null};memory=null;
                token.OwnerGeneration=owner.CaptureReconnectGeneration(loaded.Operation);if(token.OwnerGeneration==Guid.Empty||!token.Current())return null;
                token.Access=AccessHold.CapturePending(token);if(token.Access==null)return null;
                token.Pending=ClosedPendingPublication.FromActualPendingLoad(token,loaded);if(token.Pending==null)return null;
                closedAuthorityCompletions.Add(token);loaded.Adopted=true;retained=true;return token;
            }
            catch{return null;}
            finally{if(!retained)token?.Dispose();memory?.Dispose();}
        }
    }
    private static bool RecoverClosedPendingSource(LoadedStation station)
    {
        foreach(var owner in remoteLoadedRecyclerOwners.ToArray())if(ClosedAuthorityCompletion.RestorePending(station,owner)!=null)return true;
        return false;
    }
}
