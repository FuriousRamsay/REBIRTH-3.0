using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    // Serialization of an already privately issued actual Add event. Neither
    // this exporter nor an owner-source attestation can construct that event.
    internal static bool TryCopyNativeContribution(DeferredReceipt token,out XElement evidence,out IDisposable retention)
        =>TryCopyNativeContributionCore(token,false,false,out evidence,out retention);
    internal static bool TryCopyNativeOriginalLedger(DeferredReceipt token,out XElement evidence,out IDisposable retention)
        =>TryCopyNativeContributionCore(token,true,false,out evidence,out retention);
    internal static bool TryCopyCachedNativeLedger(DeferredReceipt token,out XElement evidence,out IDisposable retention)
        =>TryCopyNativeContributionCore(token,false,true,out evidence,out retention);
    internal static bool TryCopyAuthorityNativeLedger(DeferredReceipt token,out XElement evidence,out IDisposable retention)
        =>TryCopyNativeContributionCore(token,false,false,out evidence,out retention,true);
    private static bool TryCopyNativeContributionCore(DeferredReceipt token,bool originalLedger,bool cached,out XElement evidence,out IDisposable retention,bool authority=false)
    {
        evidence=null;retention=null;IDisposable workspace=null;XElement node=null;
        if(token?.NativeOriginal==null||authority&&(token.NativeOriginal.World.IsRemote()||!token.NativeOriginal.AuthoritySavedBeforeCurrent())||!cached&&!authority&&(token.NativeOriginal.RemoteNativeNonce==Guid.Empty||
            string.IsNullOrEmpty(token.NativeOriginal.RemoteNativeSourceHash))||
            (originalLedger?token.CurrentSourceWithoutRow()!=true||token.AllContributions.Any(value=>!value.CurrentSourceWithoutRow()):token.CurrentAggregateMembership()!=true)||
            token.NativeArgument==null||token.AllContributions.Any(value=>value.Imported!=null||value.HostLocal!=null||value.NativeArgument==null))return false;
        try
        {
            var original=token.NativeOriginal;
            long bound=65536L+2L*(token.BeforeComplete.Length+token.AfterComplete.Length+token.aggregate.Image.Length+token.NativeArgument.Length);
            foreach(var member in token.AllContributions)
            {
                bound=checked(bound+65536L+2L*(member.BeforeComplete.Length+member.AfterComplete.Length+member.NativeArgument.Length)+
                    (member.NativeOriginal?.MeasureBefore()??0));
                if(bound>32L*1024*1024)return false;
            }
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*bound),out workspace))return false;
                node=new XElement(authority?"authorityNativeContribution":"deferredNativeContribution",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),
                    cached?new XAttribute("cachedOriginal",true):null,
                    new XAttribute("nonce",original.RemoteNativeNonce.ToString("N")),new XAttribute("birth",original.EarlierOwner.Birth.ToString("N")),
                    new XAttribute("original",authority?original.AuthoritySourceHash:original.RemoteNativeSourceHash),new XAttribute("source",Export(original.EarlierOwner)),
                    new XAttribute("recipient",Export(token.aggregate.Recipient)),new XAttribute("contribution",token.ContributionCount),
                    new XElement("beforeComplete",Convert.ToBase64String(token.BeforeComplete)),new XElement("afterComplete",Convert.ToBase64String(token.AfterComplete)),
                    new XElement("aggregate",Convert.ToBase64String(token.aggregate.Image)),new XElement("nativeAddArgument",Convert.ToBase64String(token.NativeArgument)),
                    new XElement("members",token.AllContributions.Select(MemberEvidence)));
                long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(node);
                if(measured>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(workspace,checked(4*measured+65536)))return false;
                evidence=node;retention=workspace;node=null;workspace=null;return true;
        }
        catch{return false;}
        finally{node=null;workspace?.Dispose();}
    }
}
