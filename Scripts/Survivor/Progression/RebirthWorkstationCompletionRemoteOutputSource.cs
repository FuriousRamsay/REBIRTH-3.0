using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal static bool TryCaptureRemoteOutputSource(XUiC_RecipeStack card,out XElement image)
    {
        image=null;var frame=current;
        if(frame==null)
        {
            var candidates=nativeViewerClients.Values.Where(value=>!value.Frame.Closed&&ReferenceEquals(value.Frame.Observation.Card,card)&&value.Current()).ToArray();
            if(candidates.Length!=1)return false;frame=candidates[0].Frame;
        }
        if(!ReferenceEquals(frame.Observation.Card,card)||!frame.Observation.World.IsRemote()||frame.RemoteNativeNonce==Guid.Empty||
            !nativeViewerClients.TryGetValue(frame,out var source)||!source.Current()||source.Before?.Saved!=true)return false;
        image=new XElement("remoteOutputSource",new XAttribute("version",1),new XAttribute("operation",frame.Observation.Operation.ToString("N")),
            new XAttribute("nonce",frame.RemoteNativeNonce.ToString("N")),new XAttribute("source",frame.RemoteNativeSourceHash),
            new XAttribute("generation",source.Generation),new XAttribute("cached",frame.CachedPhysical!=null),new XAttribute("intentPath",frame.Path),
            new XElement("activeBefore",new XElement(ActiveBefore(frame.Intent))));return true;
    }
}
