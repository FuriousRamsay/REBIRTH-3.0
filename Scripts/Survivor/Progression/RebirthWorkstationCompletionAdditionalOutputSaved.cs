using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Observation from the existing authenticated native viewer Saved stage.
    // This capability sends no upload and authorizes no physical insertion.
    internal sealed class AdditionalOutputOwnerSaved
    {
        private NativeViewerClient source;private ViewerClientStage stage;private XElement acknowledgment;
        private AdditionalOutputOwnerSaved(){}
        internal RebirthRecipeOutputIntegration.OwningObservation Observation=>source.Frame.Observation;
        internal XElement Request=>new XElement(stage.Request);
        internal XElement Acknowledgment=>new XElement(acknowledgment);
        internal bool Current()=>source!=null&&stage!=null&&source.Current()&&ReferenceEquals(source.After,stage)&&
            stage.Saved&&stage.Sent&&stage.Captured&&(string)stage.Request?.Attribute("stage")=="after"&&
            ((string)acknowledgment?.Attribute("stage")=="nativeViewerAfterSaved"||(string)acknowledgment?.Attribute("stage")=="cachedViewerAfterSaved")&&
            (string)acknowledgment.Attribute("digest")==stage.Digest&&
            (string)acknowledgment.Attribute("nativeNonce")==source.Frame.RemoteNativeNonce.ToString("N")&&
            (string)acknowledgment.Attribute("nativeSource")==source.Frame.RemoteNativeSourceHash;
        internal static AdditionalOutputOwnerSaved Capture(object original,object saved,XElement ack)
        {
            if(original is not NativeViewerClient source||saved is not ViewerClientStage stage||ack==null)return null;
            var result=new AdditionalOutputOwnerSaved{source=source,stage=stage,acknowledgment=new XElement(ack)};
            return result.Current()?result:null;
        }
    }
}
