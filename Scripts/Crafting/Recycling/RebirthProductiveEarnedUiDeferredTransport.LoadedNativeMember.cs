using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostContribution
    {
        internal readonly List<LoadedNativeMember> LoadedNativeMembers=new List<LoadedNativeMember>();
    }
    // A member of an actual native-loaded aggregate, not a recreated live Add
    // original. It has no peer, dispatch, callback or native-write permission.
    internal sealed partial class LoadedNativeMember:IDisposable
    {
        private HostContribution aggregate;
        private RebirthProductiveEarnedUiQueueOwner.Witness creator;
        private readonly Dictionary<string,byte[]> inputs=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        private IDisposable memory,creatorRetention;private int references=1;
        internal Guid Operation,Nonce;
        internal string Path,Hash,WorldGuid,Viewer,Creation;
        internal XElement Member,Before;
        private byte[] terminal,query;
        private LoadedNativeMember(){}
        internal HostContribution Aggregate=>aggregate;
        internal bool Current()=>references>0&&aggregate?.LoadedIdentityCurrent()==true&&
            creator?.SourceCurrent()==true&&
            inputs.All(pair=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(pair.Key,pair.Value));
        internal bool Matches(XElement member)=>Current()&&XNode.DeepEquals(Member,member);
        internal IDisposable Retain(){if(!Current())return null;references++;return new Lease(this);}
        private sealed class Lease:IDisposable
        {private LoadedNativeMember source;internal Lease(LoadedNativeMember value){source=value;}public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref source,null);value?.Dispose();}}
        public void Dispose()
        {
            if(references<=0||--references!=0)return;
            inputs.Clear();Member=Before=physical=null;terminal=query=null;creator=null;aggregate=null;authorityPath=null;creatorRetention?.Dispose();creatorRetention=null;memory?.Dispose();memory=null;
        }
        internal bool HistoricalTerminalSaved=>references>0&&terminal!=null&&query!=null&&
            inputs.All(pair=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(pair.Key,pair.Value))&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(OwnerTerminalPath,terminal)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(QueryTerminalPath,query);
        internal bool TerminalSaved=>Current()&&HistoricalTerminalSaved;
        internal bool SaveTerminal(HostTerminalPublication proof)
        {
            if(!Current()||proof?.IncludesLoadedNativeMember(this)!=true)return false;
            if(authority)return SaveAuthorityTerminal(proof);
            if(TerminalSaved)return true;
            long bound=checked(proof.GroupEvidenceBound+MeasurePublicationElement(Before)+65536);
            using(var workspace=proof.EnterGroupPublication(bound))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                    new XElement("before",new XElement(Before)),new XElement("remoteNativeOwnerTerminal",new XAttribute("nonce",Nonce.ToString("N")),
                        new XAttribute("original",Hash),proof.CopyGroupEvidence()));
                if(!RebirthWorkstationCompletionPublication.Write(Path+".owner-terminal",record)||!Current()||!proof.Current())return false;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(Path+".owner-terminal");if(bytes==null)return false;
                var historical=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                    new XElement("before",new XElement(Before)),new XElement("remoteNativeHistoricalTerminal",new XAttribute("version",1),
                        new XAttribute("nonce",Nonce.ToString("N")),new XAttribute("original",Hash),new XAttribute("terminal",RebirthWorkstationCompletionPipeline.CachedFileHash(bytes)),
                        new XAttribute("viewer",Viewer),new XAttribute("creation",Creation),new XAttribute("owner",creator.OriginalOwnerKey),new XAttribute("ownerCreation",creator.Creation)));
                if(!RebirthWorkstationCompletionPublication.Write(Path+".query-terminal",historical)||!Current()||!proof.Current())return false;
                var queryBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(Path+".query-terminal");if(queryBytes==null)return false;
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(inputs.Sum(pair=>8L*pair.Value.Length)+8L*(bytes.Length+queryBytes.Length)+8L*MeasurePublicationElement(Member)+65536)))return false;
                terminal=bytes;query=queryBytes;return TerminalSaved;
            }
        }
        internal static bool TryCapture(HostContribution aggregate,XElement member,RebirthProductiveEarnedUiQueueOwner.Witness creator,out LoadedNativeMember result)
        {
            result=null;LoadedNativeMember value=null;IDisposable memory=null;
            try
            {
                var loaded=aggregate?.LoadedMemberSource;
                if(loaded?.Current()!=true||loaded.Native?.IdentityCurrent()!=true||loaded.Native.Current()!=true||creator?.SourceCurrent()!=true||
                    (string)member.Attribute("kind")!="native"||ReadGuid(member,"operation")==aggregate.Operation&&aggregate.AuthorityLoadedOriginal==null||
                    (string)member.Attribute("source")!=RebirthProductiveEarnedUiQueueOwner.Export(creator)||
                    aggregate.LoadedNativeMembers.Count>=(aggregate.AuthorityLoadedOriginal==null?3:4))return false;
                var membership=loaded.DeferredMembership??loaded.Request.Element("deferredNativeContribution");
                if(membership?.Element("members")?.Elements("member").Count(node=>XNode.DeepEquals(node,member))!=1)return false;
                Guid operation=ReadGuid(member,"operation");
                string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",operation.ToString("N")+".xml");
                string local=AuthorityOriginalPath(operation);
                if(RebirthWorkstationCompletionPublication.PublishedLength(path)<1)return TryCaptureAuthority(aggregate,member,creator,out result);
                if(RebirthWorkstationCompletionPublication.PublishedLength(local)>0)return false;
                long size=MeasurePublicationElement(member);
                var lengths=new Dictionary<string,long>(StringComparer.Ordinal);
                foreach(string suffix in new[]{"",".saved",".contribution",".station-saved"})
                {
                    long length=RebirthWorkstationCompletionPublication.PublishedLength(path+suffix);
                    if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
                    lengths.Add(suffix,length);
                    size=checked(size+length);
                }
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32*size+65536),out memory))return false;
                value=new LoadedNativeMember{aggregate=aggregate,creator=creator,Operation=operation,Path=path,Member=new XElement(member),memory=memory};memory=null;
                value.creatorRetention=creator.RetainPublishedSource();if(value.creatorRetention==null)return false;
                foreach(string suffix in new[]{"",".saved",".contribution",".station-saved"})
                {var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+suffix);if(bytes==null||bytes.LongLength!=lengths[suffix])return false;value.inputs.Add(path+suffix,bytes);}
                var original=RebirthWorkstationCompletionPublication.ReadPrimary(path);var native=original?.Element("before")?.Element("remoteNativeOriginal");
                var before=native?.Element("nativeOwningBefore");
                var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path+".saved");var seal=saved?.Element("before")?.Element("remoteNativeBeforeSaved");
                var contribution=RebirthWorkstationCompletionPublication.ReadPrimary(path+".contribution");var own=contribution?.Element("before")?.Element("deferredNativeContribution");
                var physicalSaved=RebirthWorkstationCompletionPublication.ReadPrimary(path+".station-saved");
                var completed=physicalSaved?.Element("before")?.Element("remoteNativeStationSaved");
                value.Hash=RebirthWorkstationCompletionPipeline.CachedFileHash(value.inputs[path]);value.WorldGuid=RebirthWorkstationCompletionOutbox.CurrentGuid(loaded.World);
                if(before==null||native.Elements().Count()!=1||!Guid.TryParseExact((string)native.Attribute("nonce"),"N",out value.Nonce)||value.Nonce==Guid.Empty||
                    (string)native.Attribute("ownerPurpose")!=(string)member.Attribute("source")||
                    !XNode.DeepEquals(member.Element("nativeOriginal")?.Elements().SingleOrDefault(),before)||
                    (string)native.Attribute("birth")!=loaded.Birth.ToString("N"))return false;
                foreach(var record in new[]{original,saved,contribution,physicalSaved})
                    if((string)record?.Attribute("operation")!=operation.ToString("N")||(string)record.Attribute("world")!=value.WorldGuid)return false;
                if(seal==null||seal.HasElements||seal.Attributes().Count()!=7||
                    (string)seal.Attribute("nonce")!=value.Nonce.ToString("N")||(string)seal.Attribute("source")!=value.Hash||
                    ((long?)seal.Attribute("offset")??-1)<8||((int?)seal.Attribute("length")??0)<1||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)seal.Attribute("payload"))||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)seal.Attribute("tile"))||
                    (int?)completed?.Attribute("version")!=1||(int?)completed.Attribute("phase")!=3||
                    completed.HasElements||completed.Attributes().Count()!=11||
                    (string)completed.Attribute("nonce")!=value.Nonce.ToString("N")||(string)completed.Attribute("source")!=value.Hash||
                    (string)completed.Attribute("birth")!=loaded.Birth.ToString("N")||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)completed.Attribute("native"))||
                    ((long?)completed.Attribute("offset")??-1)<8||((int?)completed.Attribute("length")??0)<1||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)completed.Attribute("payload"))||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)completed.Attribute("tile"))||
                    (string)own?.Attribute("operation")!=operation.ToString("N")||(string)own.Attribute("nonce")!=value.Nonce.ToString("N")||
                    (string)own.Attribute("original")!=value.Hash||(string)own.Attribute("source")!=(string)member.Attribute("source")||
                    own.Element("members")?.Elements("member").Count(node=>XNode.DeepEquals(node,member))!=1||
                    !RebirthProductiveEarnedNativeBody.TryRead(Convert.FromBase64String((string)before.Element("nativeBefore")),(int)before.Attribute("nativeElapsedOffset"),out var body,out _))return false;
                RebirthWorkstationCompletionPublication.RequireNativeSaveFile((string)completed.Attribute("nativePath"));
                RebirthWorkstationCompletionPublication.RequireNativeSaveFile((string)seal.Attribute("nativePath"));
                int index=(int)before.Attribute("slot");
                if((string)before.Attribute("world")!=value.WorldGuid||
                    (int?)before.Attribute("x")!=loaded.Position.x||(int?)before.Attribute("y")!=loaded.Position.y||(int?)before.Attribute("z")!=loaded.Position.z||
                    (int?)before.Attribute("startingOwner")!=creator.StartingEntityId||
                    (string)before.Attribute("startingOwnerKey")!=creator.OriginalOwnerKey||(string)before.Attribute("startingOwnerCreation")!=creator.Creation||
                    (int?)before.Attribute("startingOwnerDeaths")!=creator.Deaths||
                    index<0||index>=body.Queue.Length||body.Queue[index]?.Recipe==null||body.Queue[index].StartingEntityId!=creator.StartingEntityId||
                    body.Queue[index].Multiplier!=(int?)before.Attribute("beforeCount")||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(member,"nativeAddArgument"),out var argument)||
                    !NativeArgumentMatches(argument,body.Queue[index].Recipe,creator.StartingEntityId)||
                    !MemberDelta(member,body.Completions,body.Queue[index].Recipe,creator.StartingEntityId,argument.CraftedItemStack)||
                    !TryDescribeRemoteNativeAddBody(body.Queue[index].Recipe,creator.StartingEntityId,
                        Convert.FromBase64String((string)before.Element("nativeBefore")),(int)before.Attribute("nativeElapsedOffset"),own,out _,out _)||
                    !ReadBytes(member,"nativeAddArgument").SequenceEqual(ReadBytes(own,"nativeAddArgument"))||
                    !ReadBytes(member,"beforeComplete").SequenceEqual(ReadBytes(own,"beforeComplete"))||
                    !ReadBytes(member,"afterComplete").SequenceEqual(ReadBytes(own,"afterComplete")))return false;
                value.Viewer=(string)native.Attribute("viewer");value.Creation=(string)native.Attribute("creation");value.Before=new XElement(before);
                if(!RebirthOriginalActionScope.Validate(value.Viewer,value.Creation)||!value.Current())return false;
                result=value;value=null;return true;
            }
            catch{return false;}
            finally{value?.Dispose();memory?.Dispose();}
        }
    }
}
