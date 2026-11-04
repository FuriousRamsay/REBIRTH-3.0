using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Clear-time offline provenance is part of the original clear publication.
// It is never inferred from a later login or from a displayed player name.
internal sealed class RebirthPurgeSupplyOrigin
{
    internal readonly int X,Y,Z;
    internal RebirthPurgeSupplyOrigin(int x,int y,int z)
    {if(Math.Abs((long)x)>10000000||Math.Abs((long)y)>10000000||Math.Abs((long)z)>10000000)throw new ArgumentException("Supply origin outside world bounds.");X=x;Y=y;Z=z;}
    internal XElement Write(string name)=>new XElement(name,new XAttribute("x",X),new XAttribute("y",Y),new XAttribute("z",Z));
    internal static RebirthPurgeSupplyOrigin Read(XElement n)=>new RebirthPurgeSupplyOrigin(RebirthPoiClearanceCodec.Int(n,"x"),RebirthPoiClearanceCodec.Int(n,"y"),RebirthPoiClearanceCodec.Int(n,"z"));
}
internal sealed class RebirthPurgeOfflineCredit
{
    internal readonly long Count;
    internal readonly ulong Time;
    internal readonly RebirthPurgeSupplyOrigin Origin;
    internal RebirthPurgeOfflineCredit(long count,ulong time,RebirthPurgeSupplyOrigin origin)
    {if(count<1||origin==null)throw new ArgumentException("Invalid offline supply credit.");Count=count;Time=time;Origin=origin;}
}
internal sealed class RebirthPurgeSupplyOriginSource
{
    internal readonly string Key;
    internal readonly RebirthPurgeOfflineCredit Credit;
    internal RebirthPurgeSupplyOriginSource(string key,RebirthPurgeOfflineCredit credit){Key=key;Credit=credit;}
}

