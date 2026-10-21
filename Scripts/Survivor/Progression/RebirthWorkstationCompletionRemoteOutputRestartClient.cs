using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static string RemoteOutputRequestHash(XElement request)=>RemoteOutputRequestDigest(request);
    private sealed class RemoteOutputRestartClient
    {
        internal RebirthRecipeOutputIntegration.RemoteOutputColdClient Source;internal XUiC_RecipeStack Card;internal XElement Ack;internal double Next;
    }
    private static readonly Dictionary<Guid,RemoteOutputRestartClient> remoteOutputRestartClients=new Dictionary<Guid,RemoteOutputRestartClient>();
    private static void PruneRemoteOutputRestartQueries()
    {
        foreach(var old in remoteOutputRestartClients.Where(value=>value.Value.Source?.EpisodeCurrent!=true||
            value.Value.Source.Attached||value.Value.Source.QueueTerminalSaved).Select(value=>value.Key).ToArray())remoteOutputRestartClients.Remove(old);
    }
    internal static void QueryRemoteOutputFinalTerminal(RebirthRecipeOutputIntegration.RemoteOutputColdClient source)
    {
        PruneRemoteOutputRestartQueries();
        if(source?.FinalCallbackSaved!=true||source.Attached||source.QueueTerminalSaved||!source.BeforeBindingCurrent())return;
        if(!remoteOutputRestartClients.TryGetValue(source.Operation,out var pending))
        {if(remoteOutputRestartClients.Count>=4)return;pending=new RemoteOutputRestartClient{Source=source};remoteOutputRestartClients.Add(source.Operation,pending);}
        if(!ReferenceEquals(pending.Source,source)||UnityEngine.Time.realtimeSinceStartup<pending.Next)return;
        SendRemoteOutputRestartRequest(pending);
    }
    private static void SendRemoteOutputRestartRequest(RemoteOutputRestartClient pending)
    {
        var source=pending.Source;var original=source.Source;pending.Next=UnityEngine.Time.realtimeSinceStartup+1;
        if(source.ExternalCached)
        {
            if(!source.FinalCallbackSaved)return;
            Send(true,new XElement("cachedOutputFinalQuery",new XAttribute("query",source.LocalOperation.ToString("N")),
                new XAttribute("nonce",(string)original.Attribute("nonce")),new XAttribute("cachedOriginal",(string)original.Attribute("source")),
                new XAttribute("generation",(string)source.Request.Attribute("generation")),new XAttribute("localOperation",source.LocalOperation.ToString("N")),
                new XAttribute("original",source.OriginalHash),new XAttribute("count",source.Prefix),new XAttribute("prefixDigest",RemoteOutputRequestHash(source.Request)),
                new XAttribute("finalCallback",true)),source.Operation,source.Actor.entityId);
            return;
        }
        Send(true,new XElement("remoteOutputRestart",new XAttribute("nonce",(string)original.Attribute("nonce")),new XAttribute("source",(string)original.Attribute("source")),
            new XAttribute("localOperation",source.LocalOperation.ToString("N")),new XAttribute("original",source.OriginalHash),new XAttribute("count",source.Prefix),
            new XAttribute("prefixDigest",RemoteOutputRequestHash(source.Request)),new XAttribute("finalCallback",source.FinalCallbackSaved)),source.Operation,source.Actor.entityId);
    }
    internal static bool TryResumeRemoteOutputCold(XUiC_RecipeStack card,RebirthRecipeOutputIntegration.RemoteOutputColdClient source)
    {
        PruneRemoteOutputRestartQueries();
        if(source?.ExternalCached==true)return false;
        if(source?.Attached==true)return ReferenceEquals(source.Card,card);
        if(source?.BeforeBindingCurrent()!=true||!source.MatchesCard(card)||frames.ContainsKey(source.Operation))return false;
        if(!remoteOutputRestartClients.TryGetValue(source.Operation,out var pending))
        {
            if(remoteOutputRestartClients.Count>=4)return false;
            pending=new RemoteOutputRestartClient{Source=source,Card=card};remoteOutputRestartClients.Add(source.Operation,pending);
        }
        if(!ReferenceEquals(pending.Source,source))return false;
        if(pending.Card==null&&source.FinalCallbackSaved)pending.Card=card;
        if(!ReferenceEquals(pending.Card,card))return false;
        if(pending.Ack!=null&&AttachRemoteOutputRestart(pending)){remoteOutputRestartClients.Remove(source.Operation);return true;}
        if(UnityEngine.Time.realtimeSinceStartup<pending.Next)return false;SendRemoteOutputRestartRequest(pending);return false;
    }
    private static void AcceptRemoteOutputRestartAck(EntityPlayer viewer,Guid operation,XElement ack)
    {
        if(!remoteOutputRestartClients.TryGetValue(operation,out var pending)||!ReferenceEquals(pending.Source.Actor,viewer)||pending.Source.BeforeBindingCurrent()!=true)return;
        var source=pending.Source;var original=source.Source;var request=source.Request;
        if((string)ack.Attribute("stage")!=(source.ExternalCached?"cachedOutputFinalSettled":"remoteOutputRestartReady"))return;
        if((string)ack.Attribute("nativeNonce")!=(string)original.Attribute("nonce")||(string)ack.Attribute("nativeSource")!=(string)original.Attribute("source")||
            (string)ack.Attribute("localOperation")!=source.LocalOperation.ToString("N")||(string)ack.Attribute("original")!=source.OriginalHash||
            (int?)ack.Attribute("count")!=source.Prefix||(string)ack.Attribute("generation")!=(string)request.Attribute("generation")||
            (string)ack.Attribute("prefixDigest")!=RemoteOutputRequestHash(request)||((bool?)ack.Attribute("finalCallback")??false)!=source.FinalCallbackSaved)return;
        if((bool?)ack.Attribute("queueTerminal")==true)
        {if(source.AcceptQueueTerminal(ack))remoteOutputRestartClients.Remove(operation);return;}
        if(source.FinalCallbackSaved&&(bool?)ack.Attribute("queueOnly")!=true)return;
        pending.Ack=new XElement(ack);
    }
    private static bool AttachRemoteOutputRestart(RemoteOutputRestartClient pending)
    {
        var source=pending.Source;var card=pending.Card;var ack=pending.Ack;Frame frame=null;NativeViewerClient viewer=null;
        RebirthRecipeOutputIntegration.OwningObservation observation=null,observedScope=null;IDisposable memory=null;bool registered=false;
        try
        {
            var window=card.GetParentByType<XUiC_WorkstationWindowGroup>();var station=window?.WorkstationData?.TileEntity;var active=source.Source.Element("activeBefore")?.Elements().SingleOrDefault();
            if(source.BeforeBindingCurrent()!=true||!source.MatchesCard(card)||active==null||station?.Queue==null||
                frames.ContainsKey(source.Operation)||cards.TryGetValue(card,out _)||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var session))return false;
            int index=(int)active.Attribute("slot");
            if(index<0||index>=station.Queue.Length||!RebirthProductiveEarnedUiQueueOwner.TryObserveRemoteNativeSource(card,station,index,station.Queue[index],(string)ack.Attribute("queueOwnerPurpose"),out var owner)||
                !owner.SourcePublished()||owner.NativeHydration==null||owner.OriginalOwnerKey!=(string)active.Attribute("startingOwnerKey")||
                owner.Creation!=(string)active.Attribute("startingOwnerCreation")||owner.Deaths!=(int)active.Attribute("startingOwnerDeaths"))return false;
            if(source.FinalCallbackSaved)
            {if((bool?)ack.Attribute("queueOnly")!=true)return false;source.QueueOnlyAuthorized=true;}
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                byte[] body=Convert.FromBase64String(active.Element("nativeBefore").Value);int offset=(int)active.Attribute("nativeElapsedOffset");
                if(decoder==null)return false;
                if(source.IsDeferred&&active.Element("cachedPredecessor")==null&&!RebirthProductiveEarnedUiDeferredTransport.TryDescribeRemoteNativeAddBody(card.GetRecipe(),
                    owner.StartingEntityId,body,offset,source.DeferredContribution,out body,out offset))return false;
                if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,body,offset))return false;
            }
            observation=RebirthRecipeOutputIntegration.BeginOwningObservation(card,false,source.Operation);observedScope=observation;if(observation==null)return false;
            if(!observation.Current()||!observation.RecipeMatches()||observation.BeforeCount!=(int)active.Attribute("beforeCount")||
                observation.RecipeFull!=(string)active.Element("recipeFull")||!source.BindNativeCard(card,owner)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out memory))return false;
            observation.BagBefore=Convert.FromBase64String(active.Element("bagBefore").Value);observation.BeltBefore=Convert.FromBase64String(active.Element("beltBefore").Value);
            observation.EquipmentBefore=Convert.FromBase64String(active.Element("equipmentBefore").Value);
            observation.ExtrasNative=RebirthRecipeOutputIntegration.OwningGrid(source.Outputs());observation.ExtrasBound=true;
            observation.RemoteOutputRestart=source;
            var intent=RebirthWorkstationCompletionPublication.ReadPrimary(source.FramePath);
            if((string)intent?.Attribute("operation")!=source.Operation.ToString("N")||!XNode.DeepEquals(ActiveBefore(intent),active))return false;
            frame=new Frame{Observation=observation,Intent=new XElement(intent),Path=source.FramePath,Owner=owner.OriginalOwnerKey,Creation=owner.Creation,
                EarlierOwner=owner,Prepared=true,Attempted=true,Saved=true,RemoteNativeNonce=Guid.ParseExact((string)ack.Attribute("nativeNonce"),"N"),RemoteNativeSourceHash=(string)ack.Attribute("nativeSource")};
            viewer=new NativeViewerClient{Frame=frame,Generation=(string)ack.Attribute("generation"),Session=session,Connection=CurrentNativeServerConnection(),Memory=memory,
                BagLocks=(string)source.Request.Attribute("bagLocks"),BeltLocks=(string)source.Request.Attribute("beltLocks"),BeltCount=(int)source.Request.Attribute("beltCount"),
                BeltPreferred=(string)source.Request.Attribute("beltPreferred"),EquipmentPreferred=(string)source.Request.Attribute("equipmentPreferred")};memory=null;
            var beforeRoot=RebirthWorkstationCompletionPublication.ReadPrimary(frame.Path+".viewer."+viewer.Generation+".before");var before=beforeRoot?.Element("before")?.Element("nativeViewer");
            if(before==null||!XNode.DeepEquals(before.Element("activeBefore")?.Elements().SingleOrDefault(),active))return false;
            viewer.Before=new ViewerClientStage{Request=new XElement(before),Frame=new XElement(before.Element("nativeOwnerFrame")),Digest=RemoteOutputRequestHash(before),
                NativeDigest=(string)before.Attribute("pdsDigest"),NativeLength=(int)before.Attribute("pdsLength"),Sent=true,Captured=true,Armed=true,Saved=true};
            if(!source.IsDeferred)
            {frame.NativeViewerEffect=NativeViewerEffectSource.FromRemoteOutputRestart(frame,source);if(frame.NativeViewerEffect==null)return false;}
            authenticatedNativeBeforeAck=frame;
            try{frame.NativeBefore=NativeBeforePublication.FromAcceptedAck(frame);}finally{authenticatedNativeBeforeAck=null;}
            if(frame.NativeBefore==null||!SaveFrame(frame))return false;
            var request=source.Request;
            var stage=new ViewerClientStage{Request=request,Frame=new XElement(request.Element("nativeOwnerFrame")),
                Digest=RemoteOutputRequestHash(request),NativeDigest=(string)request.Attribute("pdsDigest"),NativeLength=(int)request.Attribute("pdsLength"),Sent=true,Captured=true,Armed=true,Saved=true};
            if(source.FinalCallbackSaved)viewer.After=stage;
            var prefix=source.AttachOriginal(out bool finalAttached);if(prefix==null&&!finalAttached)return false;
            if(prefix!=null)viewer.Prefix=new ViewerOutputPrefix{Prefix=prefix,HeaderSent=true,Stage=stage};
            cards.Add(card,frame);frames.Add(source.Operation,frame);nativeViewerClients.Add(frame,viewer);registered=true;
            if(!viewer.Current())return false;
            RememberRemoteOutputPrefixAudit(viewer);
            observation=null;frame=null;viewer=null;return true;
        }
        catch{return false;}
        finally
        {
            RebirthRecipeOutputIntegration.EndOwningObservation(observedScope);
            if(!registered){viewer?.Dispose();frame?.NativeViewerEffect?.ReleaseFrame();memory?.Dispose();observation?.Dispose();source.UndoUnenteredBinding();}
        }
    }
}
