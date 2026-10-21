using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Combines the real returned native grant with the independently loaded
    // owner prefix and its original client action. This owns no native effect.
    internal sealed class RetainedCachedClientBinding:IDisposable
    {
        internal readonly RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant Grant;
        internal readonly Guid Action,OriginalGroup;
        internal readonly long OriginalSession;
        private readonly RebirthRecipeOutputIntegration.RemoteOutputColdClient owner;
        private readonly string actionPath,framePath;
        private byte[] actionImage,frameImage;
        private XElement prepare;
        private XElement frameRecord;
        private IDisposable memory,retention;
        internal double Next;
        internal string ActionPath=>actionPath;
        internal byte[] CopyActionImage()=>Current()?(byte[])actionImage.Clone():null;
        private XElement hostPrefixAck;
        internal bool HostPrefixRestored=>Current()&&hostPrefixAck!=null;
        internal bool HostFinalOwnerRestored=>HostPrefixRestored&&(bool?)hostPrefixAck.Attribute("finalCallback")==true&&(bool?)hostPrefixAck.Attribute("queueOnly")==true;
        internal bool AcceptHostPrefix(XElement ack)
        {
            if(!Current()||owner.Attached||!owner.BeforeBindingCurrent()||ack?.Name!="ack"||ack.HasElements||
                (string)ack.Attribute("stage")!="cachedOutputRestartReady"||ack.Attributes().Any(attribute=>!
                    new[]{"world","viewerCreation","challenge","stage","action","ticket","source","group","session","generation","count","prefixDigest","localOperation","original","finalCallback","queueOnly"}.Contains(attribute.Name.ToString())))return false;
            var request=owner.Request;
            if((string)ack?.Attribute("action")!=Action.ToString("N")||(string)ack.Attribute("ticket")!=Grant.Ticket.ToString("N")||
                (string)ack.Attribute("source")!=Grant.Original||(string)ack.Attribute("group")!=OriginalGroup.ToString("N")||
                (long?)ack.Attribute("session")!=OriginalSession||(string)ack.Attribute("generation")!=(string)request.Attribute("generation")||
                (int?)ack.Attribute("count")!=owner.Prefix||(string)ack.Attribute("prefixDigest")!=RemoteOutputRequestHash(request)||
                (string)ack.Attribute("localOperation")!=owner.LocalOperation.ToString("N")||(string)ack.Attribute("original")!=owner.OriginalHash||
                ((bool?)ack.Attribute("finalCallback")??false)!=owner.FinalCallbackSaved||((bool?)ack.Attribute("queueOnly")??false)!=owner.FinalCallbackSaved)return false;
            var copy=new XElement(ack);copy.Attribute("challenge")?.Remove();hostPrefixAck=copy;
            if(owner.FinalCallbackSaved)owner.QueueOnlyAuthorized=true;
            return true;
        }
        private RetainedCachedClientBinding(RebirthRecipeOutputIntegration.RemoteOutputColdClient owner,
            RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant grant,string actionPath,
            byte[] actionImage,byte[] frameImage,XElement frameRecord,XElement prepare,IDisposable memory,IDisposable retention)
        {
            this.owner=owner;Grant=grant;Action=grant.RetainedAction;OriginalGroup=Guid.ParseExact((string)prepare.Attribute("group"),"N");
            OriginalSession=(long)prepare.Attribute("session");this.actionPath=actionPath;framePath=owner.FramePath;
            this.actionImage=actionImage;this.frameImage=frameImage;this.frameRecord=new XElement(frameRecord);this.prepare=new XElement(prepare);this.memory=memory;this.retention=retention;
        }
        internal bool Current()=>memory!=null&&retention!=null&&owner.EpisodeCurrent&&Grant.CurrentWindowSource()&&
            ReferenceEquals(owner.Actor,Grant.Actor)&&Grant.RetainedAction==Action&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(actionPath,actionImage)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(framePath,frameImage);
        internal XElement CopyPrepare()=>Current()?new XElement(prepare):null;
        // Called only by SaveFrame after the same observation has yielded its
        // actual confirmed delivery. Preserve the complete original intent;
        // only that one confirmedOriginal child may be appended.
        internal bool ObserveConfirmedFramePublication(XElement confirmed,XElement actual)
        {
            try
            {
                if(memory==null||retention==null||hostPrefixAck==null||!owner.Attached||!owner.EpisodeCurrent||!Grant.CurrentWindowSource()||
                    !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(actionPath,actionImage)||confirmed==null||frameRecord==null)return false;
                long size=RebirthWorkstationCompletionPublication.PublishedLength(framePath);
                if(size<1||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(16*(actionImage.LongLength+size)+65536)))return false;
                var prior=frameRecord.Element("confirmedOriginal");
                if(prior!=null&&!XNode.DeepEquals(prior.Elements().SingleOrDefault(),confirmed))return false;
                var expected=new XElement(frameRecord);expected.Element("confirmedOriginal")?.Remove();expected.Add(new XElement("confirmedOriginal",new XElement(confirmed)));
                if(!XNode.DeepEquals(expected,actual)||!XNode.DeepEquals(RebirthWorkstationCompletionPublication.ReadPrimary(framePath),actual))return false;
                var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(framePath);
                if(bytes==null||bytes.LongLength!=size||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(framePath,bytes))return false;
                frameImage=bytes;frameRecord=expected;return Current();
            }
            catch{return false;}
        }
        internal static RetainedCachedClientBinding TryCapture(RebirthRecipeOutputIntegration.RemoteOutputColdClient owner,
            RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant grant)
        {
            IDisposable memory=null,retention=null;RetainedCachedClientBinding result=null;
            try
            {
                if(owner?.ExternalCached!=true||owner.Attached||!owner.BeforeBindingCurrent()||grant?.CurrentWindow()!=true||
                    !grant.NativeCurrent()||grant.RetainedAction==Guid.Empty||grant.Operation!=owner.Operation||
                    !ReferenceEquals(grant.Actor,owner.Actor)||!owner.MatchesCard(grant.Card))return null;
                var remote=owner.Source;var request=owner.Request;var active=remote.Element("activeBefore")?.Elements().SingleOrDefault();
                if((string)remote.Attribute("nonce")!=grant.RetainedAction.ToString("N")||(string)remote.Attribute("source")!=grant.Original||
                    !RebirthRecipeOutputIntegration.MatchesRemoteColdRequestSource(remote,request,true)||active==null||
                    !XNode.DeepEquals(active.Element("cachedPredecessor")?.Elements().SingleOrDefault(),grant.CopyFull()))return null;
                string actionPath=Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"cachedPhysical",
                    owner.Operation.ToString("N")+"."+grant.RetainedAction.ToString("N")+".xml");
                // Cached-space continuation can advance ActiveBefore inside
                // the same original frame file. Its filename retains the first
                // generation; the current request must bind the current body.
                string name=Path.GetFileName(owner.FramePath),prefix=Path.GetFileName(actionPath)+".intent.";
                if(!PathEquals(Path.GetDirectoryName(owner.FramePath),Path.GetDirectoryName(actionPath))||
                    !name.StartsWith(prefix,StringComparison.Ordinal)||!RebirthWorkstationCompletionOriginal.ValidKey(name.Substring(prefix.Length))||
                    (string)request.Attribute("generation")!=RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting)))return null;
                long first=RebirthWorkstationCompletionPublication.PublishedLength(actionPath),second=RebirthWorkstationCompletionPublication.PublishedLength(owner.FramePath);
                if(first<1||second<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*(first+second)+65536),out memory))return null;
                var originalBefore=grant.CopyBefore();if(originalBefore==null)return null;
                foreach(string field in new[]{"operation","world","viewer","creation","deaths","x","y","z","block","slot","beforeCount","startingOwner",
                    "startingOwnerKnown","startingOwnerKey","startingOwnerCreation","startingOwnerDeaths","extrasDefinition"})
                    if((string)active.Attribute(field)!=(string)originalBefore.Attribute(field))return null;
                if((string)active.Element("recipeFull")!=(string)originalBefore.Element("recipeFull"))return null;
                var action=RebirthWorkstationCompletionPublication.ReadPrimary(actionPath);
                var frame=RebirthWorkstationCompletionPublication.ReadPrimary(owner.FramePath);
                var source=frame?.Element("cachedPhysicalSource");var prepare=action?.Element("cachedPhysicalPrepare");
                if(prepare!=null){prepare=new XElement(prepare);prepare.Attribute("challenge")?.Remove();}
                if((string)action?.Attribute("operation")!=owner.Operation.ToString("N")||(string)frame?.Attribute("operation")!=owner.Operation.ToString("N")||
                    (string)action.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(grant.World)||
                    (string)frame.Attribute("world")!=(string)action.Attribute("world")||!XNode.DeepEquals(ActiveBefore(frame),active)||
                    source==null||!PathEquals((string)source.Attribute("path"),actionPath)||
                    (string)source.Attribute("ticket")!=grant.Ticket.ToString("N")||(string)source.Attribute("action")!=grant.RetainedAction.ToString("N")||
                    (string)source.Attribute("original")!=grant.Original||prepare==null||prepare.HasElements||prepare.Attributes().Count()!=5||
                    (string)prepare.Attribute("ticket")!=grant.Ticket.ToString("N")||(string)prepare.Attribute("action")!=grant.RetainedAction.ToString("N")||
                    (string)prepare.Attribute("original")!=grant.Original||
                    !Guid.TryParseExact((string)prepare.Attribute("group"),"N",out var oldGroup)||oldGroup==Guid.Empty||
                    (long?)prepare.Attribute("session") is not long oldSession||oldSession<1)return null;
                var actionImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(actionPath);
                var frameImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(owner.FramePath);
                if(actionImage==null||frameImage==null||actionImage.LongLength!=first||frameImage.LongLength!=second||
                    !owner.BeforeBindingCurrent()||!grant.CurrentWindow()||!grant.NativeCurrent()||(retention=grant.Retain())==null)return null;
                result=new RetainedCachedClientBinding(owner,grant,actionPath,actionImage,frameImage,frame,prepare,memory,retention);memory=retention=null;
                if(!result.Current())return null;var owned=result;result=null;return owned;
            }
            catch{return null;}
            finally{result?.Dispose();retention?.Dispose();memory?.Dispose();}
        }
        public void Dispose(){hostPrefixAck=prepare=frameRecord=null;actionImage=frameImage=null;retention?.Dispose();retention=null;memory?.Dispose();memory=null;}
    }
}
