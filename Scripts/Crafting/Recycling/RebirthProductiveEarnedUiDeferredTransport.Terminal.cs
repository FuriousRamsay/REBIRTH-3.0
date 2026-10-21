using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostTerminalPublication
    {
        private readonly Removal removal;
        private readonly byte[] receipt;
        private readonly string path;
        private readonly HostContribution subject;
        private HostTerminalPublication(Removal source,byte[] image,string file)
        {removal=source;subject=source.Scope.Source;receipt=image;path=file;}
        private HostTerminalPublication(Removal source,HostContribution member,byte[] image,string file)
        {removal=source;subject=member;receipt=image;path=file;}
        internal HostContribution Original=>subject;
        internal Guid Operation=>subject.Operation;
        internal Guid Nonce=>subject.Nonce;
        internal Guid Birth=>subject.Birth;
        internal TileEntityWorkstation Station=>removal.Scope.Station;
        internal byte[] Body=>removal.Body;
        internal int Offset=>removal.Offset;
        internal string Path=>path;
        internal int ReceiptLength=>receipt.Length;
        internal byte[] Image=>(byte[])receipt.Clone();
        internal bool Current()
        {
            try
            {
                using(var workspace=removal.Scope.TryEnterPublicationWorkspace(checked(receipt.Length+65536L)))
                    return workspace!=null&&subject.SourceCurrentWithoutViewer()&&subject.TrySealStation()&&removal.Current()&&removal.Watch!=null&&RebirthWorkstationCompletionPublication.ReadPrimary(path)!=null&&
                        Encoding.UTF8.GetBytes(RebirthWorkstationCompletionPublication.ReadEvidence(path)).SequenceEqual(receipt);
            }
            catch{return false;}
        }

        // No public constructor or parsed-receipt factory. The live native
        // removal callback and saved owner-file issuer are both mandatory.
        internal static bool TryPublish(RecipientScope scope,out HostTerminalPublication token)
            =>TryPublishSubject(scope,scope?.Source,out token);
        internal static bool TryPublishSubject(RecipientScope scope,HostContribution member,out HostTerminalPublication token)
        {
            token=null;
            try
            {
                if(member==null||!member.SourceCurrentWithoutViewer()||!member.TrySealStation()||scope?.Current()!=true||!removals.TryGetValue(scope.Operation,out var source)||!ReferenceEquals(source.Scope,scope)||
                    !source.Current()||source.Watch==null||!OwnerPds.TryReadBefore(scope,out var before)||!before.Current()||
                    !OwnerPds.TryReadAfter(scope,out var after)||!ReferenceEquals(source.Owner,after)||!after.Current()||
                    !source.Watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string tile)||!source.Current())return false;
                long bound=checked(65536+scope.Hydration.Sources.Sum(value=>MeasurePublicationElement(value.ImmutableMembership)));
                using(var workspace=scope.TryEnterPublicationWorkspace(bound))
                {
                if(workspace==null)return false;
                var node=new XElement("uiDeferredNativeOwnerSaved",new XAttribute("version",1),
                    new XAttribute("operation",member.Operation.ToString("N")),new XAttribute("nonce",member.Nonce.ToString("N")),
                    new XAttribute("birth",member.Birth.ToString("N")),new XAttribute("owner",scope.Owner),new XAttribute("creation",scope.Creation),
                    new XAttribute("entity",scope.Actor.entityId),new XAttribute("deaths",scope.Deaths),
                    new XAttribute("beforePlayerFile",before.Path),new XAttribute("beforePlayerDigest",before.NativeDigest),
                    new XAttribute("afterPlayerFile",after.Path),new XAttribute("afterPlayerDigest",after.NativeDigest),
                    new XAttribute("nativePath",native.Path),new XAttribute("payload",native.PayloadDigest),
                    new XAttribute("offset",offset),new XAttribute("length",length),new XAttribute("tile",tile),
                    new XElement("membership",scope.Hydration.Sources.Select(value=>new XElement(value.ImmutableMembership))));
                string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",member.Operation.ToString("N")+".terminal.xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,new XElement("workstationCompletionIntent",new XAttribute("operation",member.Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(scope.World)),new XElement("before",node))))return false;
                // Retain the exact strict UTF8 primary, including its purpose
                // envelope, rather than treating the inner XML as the file.
                byte[] image=Encoding.UTF8.GetBytes(RebirthWorkstationCompletionPublication.ReadEvidence(path));
                token=new HostTerminalPublication(source,member,image,path);
                }
                if(!token.Current()){token=null;return false;}return true;
            }
            catch{return false;}
        }
    }
}
