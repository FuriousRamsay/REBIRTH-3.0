using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static bool TryAttachRetainedCachedOutput(RebirthRecipeOutputIntegration.RemoteOutputColdClient source)
    {
        var binding=source?.RetainedCached;
        if(binding?.HostPrefixRestored!=true||source.FinalCallbackSaved&&(!binding.HostFinalOwnerRestored||!source.QueueOnlyAuthorized)||source.BeforeBindingCurrent()!=true)return false;
        var card=binding.Grant.Card;var window=binding.Grant.Window;var station=binding.Grant.Station;
        Frame frame=null;NativeViewerClient viewer=null;CachedPhysicalSource physical=null;IDisposable memory=null;
        RebirthRecipeOutputIntegration.OwningObservation observation=null,scope=null;bool installed=false,registered=false;
        try
        {
            if(frames.ContainsKey(source.Operation)||cards.TryGetValue(card,out _)||cachedPhysicalSources.ContainsKey(source.Operation)||cachedPhysicalSources.Count>=4||
                !source.MatchesCard(card)||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var session))return false;
            var active=source.Source.Element("activeBefore")?.Elements().SingleOrDefault();int index=(int)active.Attribute("slot");
            if(index<0||index>=station.Queue.Length||!RebirthProductiveEarnedUiQueueOwner.TryObserveRemoteNativeSource(card,station,index,station.Queue[index],binding.Grant.OwnerPurpose,out var owner)||
                owner?.SourcePublished()!=true||owner.NativeHydration==null||owner.OriginalOwnerKey!=(string)active.Attribute("startingOwnerKey")||
                owner.Creation!=(string)active.Attribute("startingOwnerCreation")||owner.Deaths!=(int)active.Attribute("startingOwnerDeaths"))return false;
            physical=CachedPhysicalSource.FromRetainedPrefix(binding,source);if(physical==null||!source.BindNativeCard(card,owner))return false;
            cachedPhysicalSources.Add(source.Operation,physical);installed=true;
            if(!physical.RestoreRetainedCardMetadata(source))return false;
            observation=RebirthRecipeOutputIntegration.BeginOwningObservation(card,true,source.Operation);scope=observation;
            if(observation==null||!observation.Current()||!observation.RecipeMatches()||observation.BeforeCount!=(int)active.Attribute("beforeCount")||
                observation.RecipeFull!=(string)active.Element("recipeFull")||observation.UiQueueFull!=(string)active.Element("uiQueueFull")||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out memory))return false;
            observation.BagBefore=Convert.FromBase64String(active.Element("bagBefore").Value);observation.BeltBefore=Convert.FromBase64String(active.Element("beltBefore").Value);
            observation.EquipmentBefore=Convert.FromBase64String(active.Element("equipmentBefore").Value);
            observation.CachedPredecessor=new XElement(active.Element("cachedPredecessor").Elements().Single());
            observation.ExtrasNative=RebirthRecipeOutputIntegration.OwningGrid(source.Outputs());observation.ExtrasBound=true;observation.RemoteOutputRestart=source;
            var intent=RebirthWorkstationCompletionPublication.ReadPrimary(source.FramePath);
            if(!XNode.DeepEquals(ActiveBefore(intent),active)||(bool?)intent.Attribute("prepared")!=true||(bool?)intent.Attribute("attempted")!=true||
                (string)intent.Attribute("nativeNonce")!=binding.Action.ToString("N")||(string)intent.Attribute("nativeSource")!=binding.Grant.Original)return false;
            frame=new Frame{Observation=observation,Intent=new XElement(intent),Path=source.FramePath,Owner=owner.OriginalOwnerKey,Creation=owner.Creation,
                EarlierOwner=owner,Prepared=true,Attempted=true,Saved=true,RemoteNativeNonce=binding.Action,RemoteNativeSourceHash=binding.Grant.Original,CachedPhysical=physical};
            physical.Frame=frame;
            viewer=new NativeViewerClient{Frame=frame,Generation=(string)source.Request.Attribute("generation"),Session=session,Connection=CurrentNativeServerConnection(),Memory=memory,
                BagLocks=(string)source.Request.Attribute("bagLocks"),BeltLocks=(string)source.Request.Attribute("beltLocks"),BeltCount=(int)source.Request.Attribute("beltCount"),
                BeltPreferred=(string)source.Request.Attribute("beltPreferred"),EquipmentPreferred=(string)source.Request.Attribute("equipmentPreferred")};memory=null;
            var before=RebirthWorkstationCompletionPublication.ReadPrimary(source.FramePath+".viewer."+viewer.Generation+".before")?.Element("before")?.Element("cachedViewer");
            if(before==null||!XNode.DeepEquals(before.Element("activeBefore")?.Elements().SingleOrDefault(),active))return false;
            viewer.Before=new ViewerClientStage{Request=new XElement(before),Frame=new XElement(before.Element("nativeOwnerFrame")),Digest=RemoteOutputRequestHash(before),
                NativeDigest=(string)before.Attribute("pdsDigest"),NativeLength=(int)before.Attribute("pdsLength"),Sent=true,Captured=true,Armed=true,Saved=true};
            var request=source.Request;var stage=new ViewerClientStage{Request=request,Frame=new XElement(request.Element("nativeOwnerFrame")),Digest=RemoteOutputRequestHash(request),
                NativeDigest=(string)request.Attribute("pdsDigest"),NativeLength=(int)request.Attribute("pdsLength"),Sent=true,Captured=true,Armed=true,Saved=true};
            var prefix=source.AttachOriginal(out var final);
            if(source.FinalCallbackSaved)
            {if(prefix!=null||!final||!source.QueueOnlyAuthorized)return false;viewer.After=stage;}
            else
            {if(prefix==null||final)return false;viewer.Prefix=new ViewerOutputPrefix{Prefix=prefix,Stage=stage,HeaderSent=true};}
            cards.Add(card,frame);frames.Add(source.Operation,frame);nativeViewerClients.Add(frame,viewer);registered=true;
            if(!viewer.Current())return false;
            RememberRemoteOutputPrefixAudit(viewer);physical=null;frame=null;viewer=null;observation=null;return true;
        }
        catch{return false;}
        finally
        {
            RebirthRecipeOutputIntegration.EndOwningObservation(scope);
            if(!registered)
            {
                // If AttachOriginal retained its immutable pending output, its
                // source must keep the matching frame even on a late refusal.
                if(frame!=null&&RebirthRecipeOutputIntegration.HasRetainedCachedPendingOutput(source))
                {
                    if(!cards.TryGetValue(card,out _))cards.Add(card,frame);
                    if(!frames.ContainsKey(source.Operation))frames.Add(source.Operation,frame);
                    if(viewer!=null&&!nativeViewerClients.ContainsKey(frame))nativeViewerClients.Add(frame,viewer);
                }
                else
                {
                    if(installed&&cachedPhysicalSources.TryGetValue(source.Operation,out var own)&&ReferenceEquals(own,physical))cachedPhysicalSources.Remove(source.Operation);
                    viewer?.Dispose();memory?.Dispose();observation?.Dispose();physical?.Dispose();source.UndoUnenteredBinding();
                }
            }
        }
    }
}
