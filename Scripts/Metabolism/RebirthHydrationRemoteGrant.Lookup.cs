using System;

internal sealed partial class RebirthHydrationRemoteGrant
{
    internal static bool TryGet(EntityPlayer player,Guid operation,string digest,out RebirthHydrationRemoteGrant grant)
    {
        grant=null;
        if(player==null||!issued.TryGetValue(operation,out var retained)||!ReferenceEquals(retained.Lease.Player,player)||
            retained.Original.Digest!=digest||!retained.Current())return false;
        grant=retained;return true;
    }
}
