using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthWorkstationCompletionPipeline
{
    [ThreadStatic]private static Frame actualViewerGiveExpFrame;
    [ThreadStatic]private static NativeViewerEffectSource actualViewerGiveExpSource;
    // Actual direct native giveExp source. This is deliberately not an Add or
    // deferred CraftCompleteData receipt: vanilla selects these disjoint arms.
    internal sealed partial class NativeViewerEffectSource
    {
        internal readonly Frame Frame;internal readonly Guid Operation;internal readonly World World;
        internal readonly TileEntityWorkstation Station;internal readonly EntityPlayerLocal Viewer;
        internal readonly RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;
        private readonly string save,path;private readonly long session;private readonly INetConnection connection;
        private ItemValue item;private ItemClass itemClass;private XElement before,after,intent,eventImage,initialViewerBefore;
        private byte[] primary,recipe,savedIntent,savedEvent;private IDisposable memory,ownerRetention;
        private int references=1;private bool frameReleased,returned,ambiguous;
        private NativeViewerEffectSource(Frame frame,long session)
        {
            Frame=frame;Operation=frame.Observation.Operation;World=frame.Observation.World;Station=frame.Observation.Station;
            Viewer=frame.Observation.Viewer;EarlierOwner=frame.EarlierOwner;save=frame.Observation.SaveDirectory;this.session=session;
            path=frame.Path+".viewer-effect";connection=CurrentNativeServerConnection();
        }
        internal bool SourceCurrentWithoutRow()=>references>0&&(remoteOutputRestart==null||remoteOutputRestart.BindingCurrent())&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&
            !Frame.Closed&&ReferenceEquals(Frame.NativeViewerEffect,this)&&Frame.Observation.Operation==Operation&&connection!=null&&
            ReferenceEquals(CurrentNativeServerConnection(),connection)&&!connection.IsDisconnected()&&
            ReferenceEquals(Frame.Observation.World,World)&&ReferenceEquals(Frame.Observation.Station,Station)&&ReferenceEquals(Frame.Observation.Viewer,Viewer)&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&EarlierOwner.SourceCurrent()&&
            EarlierOwner.OriginalOwnerKey==Frame.Observation.ViewerOwnerKey&&EarlierOwner.Creation==Frame.Observation.ViewerCreation&&EarlierOwner.Deaths==Frame.Observation.ViewerDeaths&&
            ViewerOwnsNativeOutputOriginal()&&ReferenceEquals(World.GetPrimaryPlayer(),Viewer)&&!Viewer.IsDead()&&Viewer.Died==Frame.Observation.ViewerDeaths&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==EarlierOwner.OriginalOwnerKey&&
            RebirthCamperCharacterAccess.CurrentCreation(Viewer)==EarlierOwner.Creation&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Frame.Observation.Window,out var currentSession)&&currentSession==session;
        private bool AtCall(XUiC_RecipeStack card,ItemValue actual,ItemClass definition)=>!ambiguous&&ReferenceEquals(actualViewerGiveExpFrame,Frame)&&
            ReferenceEquals(actualViewerGiveExpSource,this)&&SourceCurrentWithoutRow()&&ReferenceEquals(current,Frame)&&
            ReferenceEquals(Frame.Observation.Card,card)&&!Frame.Observation.Retry&&Frame.Prepared&&Frame.Attempted&&Frame.Saved&&
            RebirthRecipeOutputIntegration.IsActualOwningUpdate(card)&&Frame.Observation.Current()&&Frame.Observation.RecipeMatches()&&
            ReferenceEquals(actual,item)&&ReferenceEquals(definition,itemClass)&&ReferenceEquals(card.outputItemValue,item)&&ReferenceEquals(item?.ItemClass,itemClass)&&
            item.type==Frame.Observation.Recipe.itemValueType&&item.Quality==(itemClass.HasQuality?Frame.Observation.OutputQuality:0);
        private bool EventCurrent()=>!ambiguous&&SourceCurrentWithoutRow()&&returned&&savedEvent!=null&&savedIntent!=null&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path+".intent",savedIntent)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path+".observed",savedEvent);
        internal long MeasureEvidence()=>EventCurrent()?RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(eventImage):0;
        internal XElement CopyEvidence()=>EventCurrent()?new XElement(eventImage):null;
        private bool SavedMetadataCurrent()=>references>0&&!ambiguous&&returned&&savedIntent!=null&&savedEvent!=null&&eventImage!=null&&
            ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(World)==(string)eventImage.Attribute("world")&&Frame.NativeViewerEffect==this&&
            Frame.Observation.Operation==Operation&&EarlierOwner.OriginalOwnerKey==(string)eventImage.Attribute("viewer")&&
            EarlierOwner.Creation==(string)eventImage.Attribute("creation")&&EarlierOwner.Deaths==(int?)eventImage.Attribute("deaths")&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path+".intent",savedIntent)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path+".observed",savedEvent);
        // Historical event export for an already physically confirmed terminal
        // query only. It cannot resume native output or issue action admission.
        internal long MeasureSavedEvidence()=>SavedMetadataCurrent()?RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(eventImage):0;
        internal XElement CopySavedEvidence()=>SavedMetadataCurrent()?new XElement(eventImage):null;
        internal IDisposable RetainForCompletion(){if(!SourceCurrentWithoutRow())return null;references++;return new EffectLease(this);}
        private sealed class EffectLease:IDisposable {private NativeViewerEffectSource source;internal EffectLease(NativeViewerEffectSource value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        internal void ReleaseFrame(){if(frameReleased)return;frameReleased=true;Release();}
        private void Release()
        {
            if(--references!=0)return;item=null;itemClass=null;before=after=intent=eventImage=initialViewerBefore=null;primary=recipe=savedIntent=savedEvent=null;
            ownerRetention?.Dispose();ownerRetention=null;memory?.Dispose();memory=null;
        }
        private bool CaptureIntent(ItemValue actual,ItemClass definition)
        {
            item=actual;itemClass=definition;var o=Frame.Observation;
            if(!AtCall(o.Card,actual,definition)||!nativeViewerClients.TryGetValue(Frame,out var viewer)||!viewer.Current()||viewer.Before?.Saved!=true||
                !RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(Viewer,new[]{o.Recipe.GetName()},out int bytes)||bytes>8*1024*1024||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*bytes+64L*1024*1024),out memory))return false;
            ownerRetention=EarlierOwner.RetainPublishedSource();if(ownerRetention==null)return false;
            before=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(Viewer,new[]{o.Recipe.GetName()});
            if(!XNode.DeepEquals(before,viewer.Before.Frame))return false;
            primary=RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(actual,o.Recipe.count)});recipe=(byte[])o.RecipeNative.Clone();
            initialViewerBefore=new XElement(viewer.Before.Request);
            intent=new XElement("nativeViewerEffect",new XAttribute("version",1),new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",o.WorldGuid),new XAttribute("viewer",o.ViewerOwnerKey),new XAttribute("creation",o.ViewerCreation),new XAttribute("deaths",o.ViewerDeaths),
                new XAttribute("startingOwner",o.StartingOwnerEntityId),new XAttribute("slot",o.Index),new XAttribute("beforeCount",o.BeforeCount),
                new XAttribute("nonce",Frame.RemoteNativeNonce.ToString("N")),new XAttribute("source",Frame.RemoteNativeSourceHash),
                new XAttribute("nativeVersionBefore",o.NativeVersionBefore),new XAttribute("window",session),
                new XAttribute("recipe",o.Recipe.GetName()),new XAttribute("experience",o.Recipe.craftExpGain),new XAttribute("count",o.Recipe.count),
                new XElement("recipeNative",Convert.ToBase64String(recipe)),new XElement("recipeFull",o.RecipeFull),
                new XElement("primaryNative",Convert.ToBase64String(primary)),new XElement("initialBefore",new XElement(ActiveBefore(Frame.Intent))),
                new XElement("initialViewerBefore",new XElement(initialViewerBefore)),new XElement("ownerBefore",new XElement(before)));
            if(!RebirthWorkstationCompletionPublication.Write(path+".intent",new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",o.WorldGuid),new XElement("before",new XElement(intent)))))return false;
            savedIntent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".intent");return savedIntent!=null;
        }
        internal void Observe(XUiC_RecipeStack card,ItemValue actual,ItemClass definition,bool run)
        {
            if(!run||returned||savedIntent==null||!AtCall(card,actual,definition)||
                !primary.SequenceEqual(RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(actual,Frame.Observation.Recipe.count)}))||
                !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path+".intent",savedIntent))return;
            after=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(Viewer,new[]{Frame.Observation.Recipe.GetName()});
            if((string)before.Element("bag")!=(string)after.Element("bag")||(string)before.Element("belt")!=(string)after.Element("belt"))return;
            returned=true;eventImage=new XElement(intent);eventImage.Add(new XAttribute("nativeOriginalReturned",true),new XElement("ownerAfterGiveExp",new XElement(after)));
            if(!RebirthWorkstationCompletionPublication.Write(path+".observed",new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",Frame.Observation.WorldGuid),new XElement("before",new XElement(eventImage)))))return;
            savedEvent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".observed");
        }
        internal static NativeViewerEffectSource Begin(Frame frame,ItemValue item,ItemClass definition)
        {
            if(!ReferenceEquals(actualViewerGiveExpFrame,frame))return null;
            if(frame.NativeViewerEffect!=null){frame.NativeViewerEffect.ambiguous=true;return frame.NativeViewerEffect;}
            if(!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(frame.Observation.Window,out var session))return null;
            // This private prefix tombstone classifies custody even if its rich
            // evidence cannot be charged/published. It never certifies effects.
            var source=new NativeViewerEffectSource(frame,session);frame.NativeViewerEffect=source;actualViewerGiveExpSource=source;
            try{source.CaptureIntent(item,definition);}catch{}return source;
        }
    }
    internal static bool TryGetNativeViewerEffect(Frame frame,out NativeViewerEffectSource source)
    {source=frame?.NativeViewerEffect;return source?.SourceCurrentWithoutRow()==true;}
    internal static void InstallNativeViewerEffectObserver(Harmony harmony)=>RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(NativeViewerGiveExp));
    [HarmonyPatch(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.giveExp),new[]{typeof(ItemValue),typeof(ItemClass)})]
    private static class NativeViewerGiveExp
    {
        private sealed class Call {internal Frame PreviousFrame;internal NativeViewerEffectSource PreviousSource,Source;}
        private static void Prefix(XUiC_RecipeStack __instance,ItemValue _iv,ItemClass _ic,bool __runOriginal,out Call __state)
        {
            __state=null;var frame=current;
            if(!__runOriginal||frame==null||frame.Closed||frame.RemoteNativeNonce==Guid.Empty||!frame.Observation.World.IsRemote()||
                !ReferenceEquals(frame.Observation.Card,__instance)||frame.Observation.StartingOwnerEntityId!=frame.Observation.Viewer.entityId||
                frame.EarlierOwner==null||!frame.EarlierOwner.SourcePublished()||!frame.Attempted||!frame.Saved||!frame.Prepared||
                !RebirthRecipeOutputIntegration.IsActualOwningUpdate(__instance))return;
            __state=new Call{PreviousFrame=actualViewerGiveExpFrame,PreviousSource=actualViewerGiveExpSource};
            actualViewerGiveExpFrame=frame;actualViewerGiveExpSource=null;
            __state.Source=NativeViewerEffectSource.Begin(frame,_iv,_ic);
        }
        private static void Postfix(XUiC_RecipeStack __instance,ItemValue _iv,ItemClass _ic,bool __runOriginal,Call __state)
        {try{__state?.Source?.Observe(__instance,_iv,_ic,__runOriginal);}catch{/* Unknown original effect remains held, never replayed. */}}
        private static Exception Finalizer(Exception __exception,Call __state)
        {if(__state!=null){actualViewerGiveExpFrame=__state.PreviousFrame;actualViewerGiveExpSource=__state.PreviousSource;}return __exception;}
    }
}
