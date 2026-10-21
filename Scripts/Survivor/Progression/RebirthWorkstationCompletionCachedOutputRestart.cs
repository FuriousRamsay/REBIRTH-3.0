using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static void ReceiveCachedOutputRestart(EntityPlayer viewer,ClientInfo sender,string owner,string creation,Guid operation,XElement request)
    {
        CachedPhysicalHostAction candidate=null;
        RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CachedOwnerHandoff handoff=null;
        try
        {
            if((int?)request.Attribute("version")!=1||(bool?)request.Attribute("finalCallback") is not bool final||
                !cachedCustodies.TryGetValue(operation,out var cache)||cache.CreatorIssue is not CachedCreatorIssue issue||
                !ReferenceEquals(issue.Viewer,viewer)||!ReferenceEquals(issue.Sender,sender)||!issue.CurrentIdentity()||
                issue.Owner!=owner||issue.Creation!=creation||issue.RetainedAction==Guid.Empty||
                (string)request.Attribute("action")!=issue.RetainedAction.ToString("N")||(string)request.Attribute("ticket")!=issue.Ticket.ToString("N")||
                (string)request.Attribute("source")!=HistoricalHash(cache.Image)||
                !Guid.TryParseExact((string)request.Attribute("localOperation"),"N",out var local)||local==Guid.Empty||
                (int?)request.Attribute("count") is not int count||count<1||count>65||
                !RebirthWorkstationCompletionOriginal.ValidKey((string)request.Attribute("generation"))||
                request.Elements("cachedPhysicalPrepare").Count()!=1)return;
            var prepare=request.Element("cachedPhysicalPrepare");
            if(!issue.MatchesPrepareRequest(prepare))return;
            bool existing=cachedPhysicalActions.TryGetValue(operation,out var action);
            if(existing)
            {
                if(!ReferenceEquals(action.Issue,issue))
                {
                    if(executingCachedPhysical!=null||readingCachedUnused!=null)return;
                    var next=candidate=CachedPhysicalHostAction.FromRetainedIssue(issue,prepare);if(next==null)return;
                    handoff=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CachedOwnerHandoff.Begin(action,next);
                    if(handoff==null)return;action=next;
                }
                else if(action.Action!=issue.RetainedAction||!action.Current())return;
            }
            else
            {
                if(cachedPhysicalActions.Count>=4)return;
                action=candidate=CachedPhysicalHostAction.FromRetainedIssue(issue,prepare);if(action==null)return;
            }
            string generation=(string)request.Attribute("generation"),digest=(string)request.Attribute("prefixDigest"),original=(string)request.Attribute("original");
            if(final)
            {
                if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.RestoreRetainedCachedFinalOwner(action,generation,count,digest,local,original)||
                    !LoadedCachedFinalTransition.TryInstall(action))return;
            }
            else if(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.RestoreRetainedCachedPrefix(action,generation,count,digest,local,original))return;
            if(handoff!=null)
            {
                var selected=action;
                if(!cachedPhysicalActions.TryGetValue(operation,out var previous)||!ReferenceEquals(previous,handoff.Previous))return;
                if(!handoff.Commit(()=>cachedPhysicalActions[operation]=selected))return;
                candidate=null;
            }
            else if(!existing){cachedPhysicalActions.Add(operation,action);candidate=null;}
            Send(false,new XElement("ack",new XAttribute("world",cache.WorldGuid),new XAttribute("viewerCreation",creation),
                new XAttribute("challenge",(string)request.Attribute("challenge")??""),new XAttribute("stage","cachedOutputRestartReady"),
                new XAttribute("action",action.Action.ToString("N")),new XAttribute("ticket",issue.Ticket.ToString("N")),new XAttribute("source",action.SourceHash),
                new XAttribute("group",(string)prepare.Attribute("group")),new XAttribute("session",action.Session),
                new XAttribute("generation",generation),new XAttribute("count",count),new XAttribute("prefixDigest",digest),
                new XAttribute("localOperation",local.ToString("N")),new XAttribute("original",original),new XAttribute("finalCallback",final),
                new XAttribute("queueOnly",final)),operation,viewer.entityId);
        }
        catch{}
        finally{handoff?.Dispose();if(candidate!=null){RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.DiscardRestoredCachedAction(candidate);candidate.ClearEvidence();}}
    }
    internal static void RequestRetainedCachedOutputRestart(RebirthRecipeOutputIntegration.RemoteOutputColdClient source)
    {
        var binding=source?.RetainedCached;
        if(source?.ExternalCached!=true||source.BeforeBindingCurrent()!=true||binding?.Current()!=true||
            binding.HostPrefixRestored||UnityEngine.Time.realtimeSinceStartup<binding.Next)return;
        var prepare=binding.CopyPrepare();if(prepare==null)return;binding.Next=UnityEngine.Time.realtimeSinceStartup+1;
        var request=source.Request;
        Send(true,new XElement("cachedOutputRestart",new XAttribute("version",1),new XAttribute("finalCallback",source.FinalCallbackSaved),
            new XAttribute("action",binding.Action.ToString("N")),new XAttribute("ticket",binding.Grant.Ticket.ToString("N")),new XAttribute("source",binding.Grant.Original),
            new XAttribute("generation",(string)request.Attribute("generation")),new XAttribute("count",source.Prefix),
            new XAttribute("prefixDigest",RemoteOutputRequestHash(request)),new XAttribute("localOperation",source.LocalOperation.ToString("N")),
            new XAttribute("original",source.OriginalHash),prepare),source.Operation,source.Actor.entityId);
    }
    private static void AcceptRetainedCachedOutputRestart(EntityPlayer viewer,Guid operation,XElement ack)
    {
        // The owner discovery retains the exact candidate and its actual grant.
        // Outer AcceptAck has already authenticated World/creation/challenge.
        RebirthRecipeOutputIntegration.AcceptRetainedCachedOutputRestart(viewer,operation,ack);
    }
}
