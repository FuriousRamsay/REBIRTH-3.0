using System;
using System.Collections.Generic;

public static partial class RebirthNpcRuntimeRegistry
{
    internal static RebirthNpcRuntimeNativeAdoption.Outgoing[] CaptureOutgoingNativeAdoptionMembership(World originalWorld)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=false||
                registryWorld!=null&&!ReferenceEquals(registryWorld,originalWorld)||ByEntityId.Count!=EntityByStableId.Count||ByEntityId.Count>8192)
                throw new InvalidOperationException("Exact outgoing native runtime membership required");
            var result=new List<RebirthNpcRuntimeNativeAdoption.Outgoing>();
            foreach(var pair in ByEntityId)
            {
                var actor=originalWorld.GetEntity(pair.Key) as EntityRebirthNPC;
                if(actor==null||!ReferenceEquals(actor.RebirthRuntimeState,pair.Value)||
                    !EntityByStableId.TryGetValue(pair.Value.StableId,out int id)||id!=pair.Key)
                    throw new InvalidOperationException("Outgoing native runtime actor changed");
                var source=RebirthNpcRuntimeNativeAdoption.CaptureOutgoing(actor,pair.Value);
                if(source==null)throw new InvalidOperationException("Actual unchanged native saved runtime source unavailable");
                result.Add(source);
            }
            return result.ToArray();
        }
    }
}
