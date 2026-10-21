using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class LoadedNativeMember
    {
        internal bool SaveLocalTerminal(RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication proof,XElement group)
        {
            if(!Current()||aggregate.AuthorityLoadedOriginal==null||proof?.IncludesLoadedAuthorityOriginal(aggregate.AuthorityLoadedOriginal)!=true||
                group?.Element("membership")?.Elements().SelectMany(value=>value.Element("members").Elements("member")).Count(value=>XNode.DeepEquals(value,Member))!=1)return false;
            if(TerminalSaved)return true;
            long bound=checked(MeasurePublicationElement(group)+MeasurePublicationElement(Before)+MeasurePublicationElement(Member)+
                (physical==null?0:MeasurePublicationElement(physical))+65536);
            using(var workspace=proof.Scope.EnterPublication(bound))
            {
                if(workspace==null)return false;
                var saved=authority?new XElement("nativeOwnerRemoteTerminal",new XElement(group),new XElement("delivery",new XElement(physical))):
                    new XElement("remoteNativeOwnerTerminal",new XAttribute("nonce",Nonce.ToString("N")),new XAttribute("original",Hash),new XElement(group));
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                    new XElement("before",new XElement(Before)),saved);
                if(!RebirthWorkstationCompletionPublication.Write(OwnerTerminalPath,record)||!Current()||!proof.Current())return false;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(OwnerTerminalPath);if(bytes==null)return false;
                var history=authority?new XElement("loadedAuthorityNativeOwnerTerminal",new XAttribute("original",Hash),
                    new XAttribute("physical",RebirthWorkstationCompletionPipeline.CachedFileHash(inputs[authorityPath+".saved"])),
                    new XAttribute("terminal",RebirthWorkstationCompletionPipeline.CachedFileHash(bytes)),new XElement(group)):
                    new XElement("remoteNativeHistoricalTerminal",new XAttribute("version",1),new XAttribute("nonce",Nonce.ToString("N")),
                        new XAttribute("original",Hash),new XAttribute("terminal",RebirthWorkstationCompletionPipeline.CachedFileHash(bytes)),
                        new XAttribute("viewer",Viewer),new XAttribute("creation",Creation),new XAttribute("owner",creator.OriginalOwnerKey),new XAttribute("ownerCreation",creator.Creation));
                var queryRecord=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                    new XElement("before",new XElement(Before)),history);
                if(!RebirthWorkstationCompletionPublication.Write(QueryTerminalPath,queryRecord)||!Current()||!proof.Current())return false;
                var queryBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(QueryTerminalPath);if(queryBytes==null||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(32L*inputs.Values.Sum(value=>(long)value.Length)+
                        16L*(bytes.Length+queryBytes.Length+bound)+65536)))return false;
                terminal=bytes;query=queryBytes;return TerminalSaved;
            }
        }
    }
    internal sealed partial class LoadedUiMember
    {
        internal bool SaveLocalTerminal(RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication proof,XElement evidence)
        {
            if(!Current()||aggregate.AuthorityLoadedOriginal==null||proof?.IncludesLoadedAuthorityOriginal(aggregate.AuthorityLoadedOriginal)!=true||
                evidence?.Element("membership")?.Elements().SelectMany(value=>value.Element("members").Elements("member")).Count(value=>XNode.DeepEquals(value,Member))!=1)return false;
            if(TerminalSaved)return true;
            long bound=checked(MeasurePublicationElement(evidence)+MeasurePublicationElement(Member)+65536);
            using(var workspace=proof.Scope.EnterPublication(bound))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(aggregate.World)),new XElement("before",
                        new XElement("loadedUiOwnerTerminal",new XAttribute("uio",Hash),new XAttribute("nonce",Nonce.ToString("N")),
                            new XAttribute("birth",Birth.ToString("N")),new XAttribute("owner",Owner),new XAttribute("creation",Creation),new XAttribute("deaths",Deaths),
                            new XElement(Member),new XElement(evidence))));
                string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",Operation.ToString("N")+".loaded-uio-owner.xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!Current()||!proof.Current())return false;
                var saved=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(saved==null)return false;
                var receipt=new XElement("uiNativeDeferredTerminalSaved",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("nonce",Nonce.ToString("N")),new XAttribute("birth",Birth.ToString("N")),new XAttribute("uio",Hash));
                var bytes=Encoding.UTF8.GetBytes(receipt.ToString(SaveOptions.DisableFormatting));
                if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(Path+".TerminalSaved",bytes)||!Current()||!proof.Current()||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(32L*inputs.Values.Sum(value=>(long)value.Length)+
                        16L*(bytes.Length+saved.Length+bound)+65536)))return false;
                terminal=bytes;group=saved;groupPath=path;return TerminalSaved;
            }
        }
    }
}
