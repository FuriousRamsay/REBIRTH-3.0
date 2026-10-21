using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class CachedIdentitySuccessor
    {
        internal static CachedIdentitySuccessor FromNativeBefore(NativeBeforePublication proof,string path)
        {
            if(proof?.Current()!=true||proof.World.IsRemote()||proof.Connection!=null||proof.LocalSavedPath==null||proof.LocalSavedDigest==null||
                !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(proof.World,proof.Station,out var version)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(256*1024,out var allocation))return null;
            try
            {
                var owner=proof.EarlierOwner;var birth=owner.CurrentCustodyBirth;if(birth==Guid.Empty)return null;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",proof.Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(proof.World)),new XElement("cachedIdentityNativeSuccessor",
                        new XAttribute("historicalBirth",owner.Birth.ToString("N")),new XAttribute("custodyBirth",birth.ToString("N")),
                        new XAttribute("ownerPurpose",RebirthProductiveEarnedUiQueueOwner.Export(owner)),
                        new XAttribute("savedBefore",proof.LocalSavedPath),new XAttribute("savedBeforeDigest",proof.LocalSavedDigest)));
                if(RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(record)>24*1024||
                    !RebirthWorkstationCompletionPublication.Write(path,record)||!proof.Current())return null;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null||bytes.Length>24*1024)return null;
                var result=new CachedIdentitySuccessor(proof.World,proof.Station,birth,path,bytes,allocation,version);allocation=null;return result;
            }
            finally{allocation?.Dispose();}
        }
    }
    private static bool RetireSameOwnerCachedWithNativeBefore(NativeBeforePublication proof)
    {
        if(proof?.Current()!=true||proof.World.IsRemote())return false;
        var values=cachedCustodies.Values.Where(value=>value.Operation!=proof.Operation&&ReferenceEquals(value.World,proof.World)&&
            ReferenceEquals(value.Station,proof.Station)&&ReferenceEquals(value.Frame?.EarlierOwner,proof.EarlierOwner)&&
            value.Original.Element("cachedOriginal")?.Element("cachedOwnerEffect")!=null).ToArray();
        if(values.Length==0)return true;CachedIdentitySuccessor compact=null;
        try
        {
            if(values.Any(value=>!value.Physical||!value.Saved||
                !MatchesSavedCachedOwnerEffect(value.Original.Element("cachedOriginal").Element("cachedOwnerEffect").Elements().SingleOrDefault())))return false;
            compact=CachedIdentitySuccessor.FromNativeBefore(proof,values[0].Path+".identity."+proof.Operation.ToString("N"));if(compact==null)return false;
            foreach(var value in values)
            {
                if(!TryPublishCachedSettledNotice(value))return false;
                long length=RebirthWorkstationCompletionPublication.PublishedLength(value.Path+".physical");
                if(length<1||!value.ReserveRetirementWorkspace(checked(8*(length+65536))))return false;
                var physical=RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".physical");if(physical==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("world",value.WorldGuid),
                    new XElement(value.Original.Element("before")),new XElement("cachedIdentityRetired",new XAttribute("original",HistoricalHash(value.Image)),
                        new XAttribute("successor",compact.Path),new XElement("identity",Convert.ToBase64String(compact.Image)),new XElement("physical",physical)));
                if(!RebirthWorkstationCompletionPublication.Write(value.Path+".identity-retired",record)||
                    !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(value.Path+".identity-retired"))||!proof.Current())return false;
            }
            var owner=proof.EarlierOwner;var previous=owner.Retention;
            owner.CachedIdentity=compact;owner.CachedSource=null;owner.Path=compact.Path;owner.Image=compact.Image;owner.Retention=compact;compact=null;previous?.Dispose();
            foreach(var value in values){cachedCustodies.Remove(value.Operation);value.Frame?.CachedReentry?.Dispose();value.Dispose();}
            return true;
        }
        finally{compact?.Dispose();}
    }
}
