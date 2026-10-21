using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        internal sealed class SavedCachedFinalOwner
        {
            private readonly ViewerSource source;
            private SavedCachedFinalOwner(ViewerSource source){this.source=source;}
            internal bool Current()
            {
                lock(gate)return sources.TryGetValue(CachedKey(source.Cached,source.Generation),out var own)&&ReferenceEquals(own,source)&&
                    source.RestoredCachedInputs!=null&&source.Current()&&source.Before?.Current()==true&&source.After?.Current()==true&&
                    source.BeforeInventoryCurrent()&&source.InventoryCurrent()&&
                    RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(source.SourceSender.latestPlayerData,source.After.Frame);
            }
            internal long MeasureImage()=>MeasurePublicationElement(source.Active)+MeasurePublicationElement(source.Delivery)+MeasurePublicationElement(source.After.Request);
            internal bool Matches(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active,XElement delivery)=>
                ReferenceEquals(source.Cached,action)&&Current()&&XNode.DeepEquals(source.Active,active)&&XNode.DeepEquals(source.Delivery,delivery);
            internal static SavedCachedFinalOwner Capture(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action)
            {
                if(action?.Current()!=true||action.Issue.RetainedAction!=action.Action||action.BeforeGeneration==null)return null;
                lock(gate)
                {
                    if(!sources.TryGetValue(CachedKey(action,action.BeforeGeneration),out var source)||!ReferenceEquals(source.Cached,action)||
                        source.After==null||source.Delivery==null||source.RestoredCachedInputs==null)return null;
                    var result=new SavedCachedFinalOwner(source);return result.Current()?result:null;
                }
            }
        }
    }
}
