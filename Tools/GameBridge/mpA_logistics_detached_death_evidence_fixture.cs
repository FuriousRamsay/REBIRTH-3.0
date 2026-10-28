using System;
using System.IO;
using System.Reflection;

// Prepared source fixture only. Not executed. The production decoder grants
// no player-file or inventory mutation and is deliberately invoked detached.
internal static class mpA_logistics_detached_death_evidence_fixture
{
    internal static void Verify(Assembly production)
    {
        var method=production.GetType("RebirthLogisticsOriginalTransport",true).GetMethod("TryDecodePhysicalEvidence",BindingFlags.Static|BindingFlags.NonPublic);
        Func<byte[],bool> decode=bytes=>{object[] args={bytes,null};return (bool)method.Invoke(null,args);};
        byte[] Body(int length,bool trailing=false)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {writer.Write((byte)1);writer.Write(7);writer.Write(2);writer.Write(true);for(int i=0;i<4;i++){writer.Write(i==0?length:1);writer.Write((byte)0);}if(trailing)writer.Write((byte)0);writer.Flush();return stream.ToArray();}
        }
        if(!decode(Body(1)))throw new Exception("Bounded detached envelope refused.");
        if(decode(Body(int.MaxValue))||decode(Body(-1))||decode(Body(0))||decode(Body(1,true))||decode(null))throw new Exception("Malformed detached evidence accepted.");
        var dead=Body(1);dead[9]=0;if(decode(dead))throw new Exception("Living image accepted as actual native fatal observation.");
        var version=Body(1);version[0]=2;if(decode(version))throw new Exception("Unknown codec version accepted.");
        // Envelope acceptance does NOT imply native blob validity: domain
        // native marker/slot decoders still reject these synthetic zero blobs.
    }
}
