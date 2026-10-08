using System;

// Accept only the actual canonical runtime identity returned by registration.
internal static class RebirthNpcSpawnIdentity
{
    internal static bool TryBind(int entityId,string profile,out RebirthNpcStableId stableId,out string reason)
    {
        stableId=default(RebirthNpcStableId);reason="Spawn identity could not be bound.";
        if(entityId<=0||string.IsNullOrWhiteSpace(profile))return false;
        try
        {
            var runtime=RebirthNpcRuntimeRegistry.Register(entityId,profile,RebirthNpcStableId.NewId());
            if(runtime==null||runtime.StableId.IsEmpty||!string.Equals(runtime.ProfileId,profile,StringComparison.OrdinalIgnoreCase)||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out var canonical)||canonical!=entityId||
                !RebirthNpcRuntimeRegistry.TryGet(entityId,out var current)||!ReferenceEquals(current,runtime))return false;
            stableId=runtime.StableId;reason=string.Empty;return true;
        }
        catch(Exception ex){reason="Spawn identity binding failed: "+ex.GetType().Name;return false;}
    }
}