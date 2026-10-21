using System;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    internal static bool HasRetainedCachedPendingOutput(RemoteOutputColdClient source)=>source?.Card!=null&&
        pendingUiOutputs.TryGetValue(source.Card,out var pending)&&ReferenceEquals(pending.Source?.RemoteCold,source);
    internal static void AcceptRetainedCachedOutputRestart(EntityPlayer viewer,Guid operation,XElement ack)
    {
        var loaded=loadedRemoteOutputOwner;if(loaded?.EpisodeCurrent()!=true||!ReferenceEquals(loaded.Actor,viewer))return;
        var values=loaded.Scan?.Candidates.Where(value=>value.ExternalCached&&!value.Attached&&value.Operation==operation&&value.RetainedCached?.Current()==true).ToArray();
        if(values?.Length==1)values[0].RetainedCached.AcceptHostPrefix(ack);
    }
    internal sealed partial class RemoteOutputColdClient
    {
        internal RebirthWorkstationCompletionPipeline.RetainedCachedClientBinding RetainedCached;
        internal bool TryBindRetainedCachedGrant(RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant grant)
        {
            if(RetainedCached!=null)
            {
                if(RetainedCached.Current())return ReferenceEquals(RetainedCached.Grant,grant);
                RetainedCached.Dispose();RetainedCached=null;
            }
            RetainedCached=RebirthWorkstationCompletionPipeline.RetainedCachedClientBinding.TryCapture(this,grant);
            return RetainedCached?.Current()==true;
        }
    }
}
