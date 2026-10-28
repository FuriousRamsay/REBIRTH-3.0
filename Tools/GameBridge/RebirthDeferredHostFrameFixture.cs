// Actual host fragment assembly/parser; explicit resource-scope/packet doubles.
// NOT_RUN. No native sender, effect or saved publication qualification.
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
class NetPackageRebirthUiDeferredOwnerV1
{
    internal const int Chunk=9000,Maximum=16*1024*1024;
    internal int Total,Index;internal string Digest;internal byte[] Part;
}
class RecipientScope
{
    internal bool Allowed=true;internal int Calls;
    internal bool ReserveFrameInput(long total,bool after){Calls++;return Allowed;}
}
static class RebirthDeferredHostFrameFixture
{
    private sealed class HostOwnerSession
    {internal RecipientScope Scope=new RecipientScope();internal HostOwnerFrame BeforeInput,AfterInput;}
    // PRODUCTION_HOST_FRAME_TYPE
    // PRODUCTION_HYDRATION_HASH
    // PRODUCTION_HOST_FRAME_PART
    static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    static NetPackageRebirthUiDeferredOwnerV1 Packet(byte[] data,int index)
    {
        int length=Math.Min(NetPackageRebirthUiDeferredOwnerV1.Chunk,data.Length-index*NetPackageRebirthUiDeferredOwnerV1.Chunk);
        var part=new byte[length];Buffer.BlockCopy(data,index*NetPackageRebirthUiDeferredOwnerV1.Chunk,part,0,length);
        return new NetPackageRebirthUiDeferredOwnerV1{Total=data.Length,Index=index,Digest=HydrationHash(data),Part=part};
    }
    public static void Main()
    {
        var data=Encoding.UTF8.GetBytes("<frame><value>"+new string('a',20000)+"</value></frame>");
        var session=new HostOwnerSession();
        Need(!HostFramePart(session,Packet(data,2),false,out _),"Partial tail became complete");
        Need(!HostFramePart(session,Packet(data,0),false,out _),"Missing middle became complete");
        var altered=Packet(data,0);altered.Part[0]^=1;
        Need(!HostFramePart(session,altered,false,out _),"Changed duplicate was accepted");
        Need(HostFramePart(session,Packet(data,1),false,out var parsed)&&parsed.Name=="frame"&&parsed.Element("value").Value.Length==20000,"Exact reordered frame failed");
        Need(session.Scope.Calls==4,"Reservation not checked before all copies");
        var refused=new HostOwnerSession();refused.Scope.Allowed=false;
        Need(!HostFramePart(refused,Packet(data,0),false,out _)&&refused.BeforeInput==null,"Failed reservation allocated original buffer");
        var excessive=Packet(data,0);excessive.Total=NetPackageRebirthUiDeferredOwnerV1.Maximum+1;
        Need(!HostFramePart(new HostOwnerSession(),excessive,false,out _),"Oversized frame accepted");
        var dtd=Encoding.UTF8.GetBytes("<!DOCTYPE frame [<!ENTITY x 'value'>]><frame>&x;</frame>");
        try{HostFramePart(new HostOwnerSession(),Packet(dtd,0),false,out _);throw new Exception("DTD accepted");}catch(XmlException){}
        string nested="value";for(int index=0;index<70;index++)nested="<node>"+nested+"</node>";
        var depth=Encoding.UTF8.GetBytes(nested);
        try{HostFramePart(new HostOwnerSession(),Packet(depth,0),false,out _);throw new Exception("Excessive topology accepted");}catch(InvalidDataException){}
        var wrong=Packet(Encoding.UTF8.GetBytes("<frame/>"),0);wrong.Digest=new string('0',64);
        Need(!HostFramePart(new HostOwnerSession(),wrong,false,out _),"Incorrect full digest accepted");
    }
}
