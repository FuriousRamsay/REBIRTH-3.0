using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        internal static bool ValidateLoadedDeferredFinal(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,
            RebirthWorkstationCompletionPipeline.RemoteOutputHostSource loaded,out XElement physical)
        {
            physical=null;
            try
            {
                if(loaded?.FinalCallbackSaved!=true||loaded.DeferredMembership==null||loaded.DeferredAddBody==null||
                    !original.IsRemoteOutputRestart(loaded)||!loaded.Current()||loaded.Native?.Current()!=true||
                    !sources.TryGetValue(Key(original,(string)loaded.Request.Attribute("generation")),out var source)||
                    !ReferenceEquals(source.Original,original)||!source.Current()||source.DirectEffect!=null||
                    source.Before?.Current()!=true||source.After?.Current()!=true||!source.BeforeInventoryCurrent()||!source.InventoryCurrent()||
                    !XNode.DeepEquals(source.After.Request,loaded.Request)||
                    !ProjectionCounters(source.Before.Frame,original.Recipe.GetName(),out int total,out float crafted)||
                    !ProjectionCounters(source.After.Frame,original.Recipe.GetName(),out int nextTotal,out float nextCrafted)||
                    total!=nextTotal||crafted!=nextCrafted)return false;
                var contribution=new XElement(loaded.DeferredMembership);
                contribution.Add(new XAttribute("phase",2),new XElement("activeBefore",new XElement(source.Active)),
                    new XElement("physicalDelivery",new XElement(source.Delivery)));
                return ValidateRemoteNativePhysical(original,contribution,loaded.DeferredAddBody,loaded.DeferredAddOffset,
                    loaded.DeferredAddBody,loaded.DeferredAddOffset,2,out physical);
            }
            catch{return false;}
        }
    }
}
