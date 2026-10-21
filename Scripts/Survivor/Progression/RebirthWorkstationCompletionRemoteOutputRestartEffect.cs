using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed partial class NativeViewerEffectSource
    {
        private RebirthRecipeOutputIntegration.RemoteOutputColdClient remoteOutputRestart;
        private bool ViewerOwnsNativeOutputOriginal()
            =>EarlierOwner.StartingEntityId==Viewer.entityId||remoteOutputRestart?.FinalCallbackSaved==true&&remoteOutputRestart.QueueOnlyAuthorized&&
                ReferenceEquals(remoteOutputRestart.Actor,Viewer)&&ReferenceEquals(remoteOutputRestart.OwnerSource,EarlierOwner)&&
                EarlierOwner.NativeHydration!=null&&remoteOutputRestart.BindingCurrent();
        internal static NativeViewerEffectSource FromRemoteOutputRestart(Frame frame,RebirthRecipeOutputIntegration.RemoteOutputColdClient authority)
        {
            NativeViewerEffectSource result=null;
            try
            {
                var evidence=authority?.Effect;
                if(authority?.BindingCurrent()!=true||frame?.EarlierOwner?.SourcePublished()!=true||evidence==null||
                    frame.Observation.Operation!=authority.Operation||!ReferenceEquals(frame.Observation.Card,authority.Card)||
                    !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(frame.Observation.Window,out var session))return null;
                result=new NativeViewerEffectSource(frame,session){remoteOutputRestart=authority,returned=true,eventImage=new XElement(evidence)};
                long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(evidence);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measured+65536),out result.memory)||
                    (result.ownerRetention=frame.EarlierOwner.RetainPublishedSource())==null)return null;
                result.savedIntent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(result.path+".intent");
                result.savedEvent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(result.path+".observed");
                var saved=RebirthWorkstationCompletionPublication.ReadPrimary(result.path+".observed");
                var entered=RebirthWorkstationCompletionPublication.ReadPrimary(result.path+".intent");
                if(result.savedIntent==null||result.savedEvent==null||(string)saved?.Attribute("operation")!=authority.Operation.ToString("N")||
                    !XNode.DeepEquals(saved.Element("before")?.Element("nativeViewerEffect"),evidence)||
                    (string)entered?.Attribute("operation")!=authority.Operation.ToString("N"))return null;
                result.intent=entered.Element("before").Element("nativeViewerEffect");result.before=evidence.Element("ownerBefore").Elements().Single();
                result.after=evidence.Element("ownerAfterGiveExp").Elements().Single();result.primary=Convert.FromBase64String(evidence.Element("primaryNative").Value);
                result.recipe=Convert.FromBase64String(evidence.Element("recipeNative").Value);result.initialViewerBefore=new XElement(evidence.Element("initialViewerBefore").Elements().Single());
                // No live item/definition or actual giveExp ThreadStatic token is
                // assigned. This can export only the saved entered effect.
                var accepted=result;result=null;return accepted;
            }
            finally{if(result!=null){result.ownerRetention?.Dispose();result.memory?.Dispose();}}
        }
    }
}
