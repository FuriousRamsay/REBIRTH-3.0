using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostContribution
    {internal readonly List<LoadedUiMember> LoadedUiMembers=new List<LoadedUiMember>();}
    internal sealed partial class LoadedUiMember:IDisposable
    {
        private HostContribution aggregate;private RebirthProductiveEarnedUiQueueOwner.Witness creator;private IDisposable memory,creatorRetention;private int references=1;
        private readonly Dictionary<string,byte[]> inputs=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        private byte[] terminal,group;private string groupPath;
        internal Guid Operation,Nonce,Birth;internal string Path,Hash,Owner,Creation;internal int Deaths;
        internal XElement Member;
        internal HostContribution Aggregate=>aggregate;
        private LoadedUiMember(){}
        internal bool Current()=>references>0&&aggregate?.LoadedIdentityCurrent()==true&&
            creator?.SourceCurrent()==true&&inputs.All(pair=>RebirthProductiveEarnedArtifact.FileMatches(pair.Key,pair.Value));
        internal bool Matches(XElement member)=>Current()&&XNode.DeepEquals(Member,member);
        internal IDisposable Retain(){if(!Current())return null;references++;return new Lease(this);}
        private sealed class Lease:IDisposable
        {private LoadedUiMember value;internal Lease(LoadedUiMember source){value=source;}public void Dispose(){var source=System.Threading.Interlocked.Exchange(ref value,null);source?.Dispose();}}
        public void Dispose(){if(references<=0||--references!=0)return;inputs.Clear();terminal=group=null;Member=null;aggregate=null;creator=null;creatorRetention?.Dispose();creatorRetention=null;memory?.Dispose();memory=null;}
        internal bool HistoricalTerminalSaved=>references>0&&terminal!=null&&group!=null&&
            inputs.All(pair=>RebirthProductiveEarnedArtifact.FileMatches(pair.Key,pair.Value))&&
            RebirthProductiveEarnedArtifact.FileMatches(Path+".TerminalSaved",terminal)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(groupPath,group);
        internal bool TerminalSaved=>Current()&&HistoricalTerminalSaved;
        internal bool SaveTerminal(HostTerminalPublication proof)
        {
            if(!Current()||proof?.IncludesLoadedUiMember(this)!=true)return false;if(TerminalSaved)return true;
            long bound=checked(proof.GroupEvidenceBound+MeasurePublicationElement(Member)+65536);
            using(var workspace=proof.EnterGroupPublication(bound))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(aggregate.World)),new XElement("before",
                        new XElement("loadedUiOwnerTerminal",new XAttribute("uio",Hash),new XAttribute("nonce",Nonce.ToString("N")),
                            new XAttribute("birth",Birth.ToString("N")),new XAttribute("owner",Owner),new XAttribute("creation",Creation),new XAttribute("deaths",Deaths),
                            new XElement(Member),proof.CopyGroupEvidence())));
                string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",Operation.ToString("N")+".loaded-uio-owner.xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!proof.Current()||!Current())return false;
                var saved=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(saved==null)return false;
                var receipt=new XElement("uiNativeDeferredTerminalSaved",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("nonce",Nonce.ToString("N")),new XAttribute("birth",Birth.ToString("N")),new XAttribute("uio",Hash));
                var bytes=Encoding.UTF8.GetBytes(receipt.ToString(SaveOptions.DisableFormatting));
                if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(Path+".TerminalSaved",bytes)||!proof.Current()||!Current()||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(inputs.Sum(pair=>16L*pair.Value.Length)+8L*(bytes.Length+saved.Length)+8L*MeasurePublicationElement(Member)+65536)))return false;
                terminal=bytes;group=saved;groupPath=path;return TerminalSaved;
            }
        }
        // UIO originals are binary and live outside the completion XML tree.
        // Read only the exact bounded primary, rejecting staged/reparse paths.
        internal static byte[] ReadExact(string path,long expected)
        {
            if(expected<1||expected>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return null;
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length!=expected)return null;var bytes=new byte[(int)expected];int read=0;
                while(read<bytes.Length){int count=file.Read(bytes,read,bytes.Length-read);if(count<1)return null;read+=count;}
                return file.ReadByte()==-1&&file.Length==expected&&RebirthProductiveEarnedArtifact.SafePath(path)&&!File.Exists(path+".tmp")?bytes:null;
            }
        }
        internal static bool TryFindOriginal(string save,Guid operation,out string path)
        {
            path=null;string root=System.IO.Path.Combine(save,"RebirthProductiveEarnedUi","remote");
            if(!RebirthProductiveEarnedArtifact.SafePath(root)||!Directory.Exists(root))return false;
            foreach(string owner in Directory.EnumerateDirectories(root))
            {
                if(!RebirthWorkstationCompletionOriginal.ValidKey(System.IO.Path.GetFileName(owner)))continue;
                string candidate=System.IO.Path.Combine(owner,operation.ToString("N"),"original.uio");
                if(!File.Exists(candidate))continue;
                if(!RebirthProductiveEarnedArtifact.SafePath(candidate)||path!=null)return false;path=candidate;
            }
            return path!=null;
        }
        internal static bool TryCapture(HostContribution aggregate,XElement member,RebirthProductiveEarnedUiQueueOwner.Witness creator,out LoadedUiMember result)
        {
            result=null;LoadedUiMember value=null;IDisposable memory=null;
            try
            {
                var loaded=aggregate?.LoadedMemberSource;
                if(loaded?.Current()!=true||loaded.Native?.Current()!=true||loaded.Native.IdentityCurrent()!=true||creator?.SourceCurrent()!=true||
                    (string)member.Attribute("kind")!="uio"||(string)member.Attribute("source")!=RebirthProductiveEarnedUiQueueOwner.Export(creator)||
                    aggregate.LoadedUiMembers.Count>=(aggregate.AuthorityLoadedOriginal==null?3:4))return false;
                Guid operation=ReadGuid(member,"operation");
                var membership=loaded.DeferredMembership??loaded.Request.Element("deferredNativeContribution");
                if(membership?.Element("members")?.Elements("member").Count(node=>XNode.DeepEquals(node,member))!=1||
                    !TryFindOriginal(loaded.Save,operation,out var path))return false;
                var lengths=new Dictionary<string,long>(StringComparer.Ordinal);long size=MeasurePublicationElement(member);
                foreach(string suffix in new[]{"",".TerminalOriginal",".StationViewerSaved"})
                {string file=path+suffix;if(!RebirthProductiveEarnedArtifact.SafePath(file)||File.Exists(file+".tmp"))return false;long length=new FileInfo(file).Length;
                    if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;lengths.Add(suffix,length);size=checked(size+length);}
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32*size+65536),out memory))return false;
                value=new LoadedUiMember{aggregate=aggregate,creator=creator,Operation=operation,Path=path,Member=new XElement(member),memory=memory};memory=null;
                value.creatorRetention=creator.RetainPublishedSource();if(value.creatorRetention==null)return false;
                foreach(var pair in lengths){var bytes=ReadExact(path+pair.Key,pair.Value);if(bytes==null)return false;value.inputs.Add(path+pair.Key,bytes);}
                var image=value.inputs[path];var frame=RebirthProductiveEarnedUiOriginal.ReadUiArtifact(image);value.Hash=HydrationHash(image);
                var terminal=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(value.inputs[path+".TerminalOriginal"]),RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                var seal=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(value.inputs[path+".StationViewerSaved"]),RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(frame.Operation!=operation||frame.Birth!=loaded.Birth||frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(loaded.World)||
                    frame.OwnerPurpose!=(string)member.Attribute("source")||value.Hash!=(string)member.Attribute("uio")||
                    !RebirthOriginalActionScope.Validate(frame.Owner,frame.Creation)||System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path)))!=frame.Owner||
                    !Guid.TryParseExact((string)member.Attribute("nonce"),"N",out value.Nonce)||value.Nonce==Guid.Empty||
                    terminal.Name!="uiTerminalOriginal"||(string)terminal.Attribute("operation")!=operation.ToString("N")||(string)terminal.Attribute("uio")!=value.Hash||
                    seal.Name!="uiNativeStationViewerSaved"||seal.HasElements||seal.Attributes().Count()!=11||
                    (string)seal.Attribute("operation")!=operation.ToString("N")||(string)seal.Attribute("uio")!=value.Hash||
                    (string)seal.Attribute("nonce")!=value.Nonce.ToString("N")||(string)seal.Attribute("birth")!=frame.Birth.ToString("N")||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)seal.Attribute("viewerNative"))||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)seal.Attribute("payload"))||!RebirthWorkstationCompletionOriginal.ValidKey((string)seal.Attribute("tile"))||
                    ((long?)seal.Attribute("offset")??-1)<8||((int?)seal.Attribute("length")??0)<1||
                    HydrationHash(Convert.FromBase64String((string)terminal.Element("afterNative")))!=(string)seal.Attribute("native")||
                    terminal.Element("deferredContribution")?.Element("members")?.Elements("member").Count(node=>XNode.DeepEquals(node,member))!=1||
                    !RebirthProductiveEarnedNativeBody.TryRead(frame.Earned.AfterNative,frame.Earned.AfterElapsedOffset,out var body,out _))return false;
                RebirthWorkstationCompletionPublication.RequireNativeSaveFile((string)seal.Attribute("nativePath"));
                int index=frame.Earned.RowIndex;
                if(index<0||index>=body.Queue.Length||body.Queue[index]?.Recipe==null||body.Queue[index].StartingEntityId!=creator.StartingEntityId||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(member,"nativeAddArgument"),out var argument)||
                    !NativeArgumentMatches(argument,body.Queue[index].Recipe,creator.StartingEntityId)||
                    !MemberDelta(member,body.Completions,body.Queue[index].Recipe,creator.StartingEntityId,argument.CraftedItemStack))return false;
                value.Birth=frame.Birth;value.Owner=frame.Owner;value.Creation=frame.Creation;value.Deaths=frame.Deaths;
                if(!value.Current())return false;result=value;value=null;return true;
            }
            catch{return false;}
            finally{value?.Dispose();memory?.Dispose();}
        }
    }
}
