using System;
using System.Collections.Generic;

// In-process exclusion only. Unknown native effects cannot release a claim.
internal sealed class RebirthNpcSpawnAttemptGate
{
    private readonly object sync=new object();
    private readonly Dictionary<string,Guid> claims=new Dictionary<string,Guid>(StringComparer.Ordinal);
    internal bool TryBegin(string key,out Guid lease)
    {
        lease=Guid.Empty;if(!RebirthNpcSpawnReplayCodec.ValidKey(key))return false;
        lock(sync)
        {
            if(claims.Count>=1024||claims.ContainsKey(key))return false;
            lease=Guid.NewGuid();claims.Add(key,lease);return true;
        }
    }
    internal bool ReleaseKnown(string key,Guid lease)
    {
        lock(sync)
        {
            if(lease==Guid.Empty||!claims.TryGetValue(key,out var current)||current!=lease)return false;
            return claims.Remove(key);
        }
    }
    internal void Reset(){lock(sync)claims.Clear();}
}