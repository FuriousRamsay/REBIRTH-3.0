#if DEBUG
using System;
using System.IO;

// Actual production codec and tuple policy only. No invented World, ClientInfo,
// baseline ACK, actor, connection authorization or native packet is dispatched.
// Header ACK uses this same exact production Frame codec; it has no row/native
// readback fields and cannot substitute a recovery projection acknowledgment.
internal static class RebirthNpcBaselineV2Fixture
{
    internal static void Verify()
    {
        var original=new RebirthNpcBaselineFraming.Frame{Epoch=4,Connection=5,Generation=9,Sequence=17,First=RebirthNpcNetworkProtocol.MaxBaselineActors,Second=100};byte[] encoded=Encode(original);Require(encoded.Length==34&&encoded.Length<13*1024);var decoded=Read(encoded);Require(decoded.Epoch==4&&decoded.Connection==5&&decoded.Generation==9&&decoded.Sequence==17&&decoded.First==original.First&&decoded.Second==100);
        Require(RebirthNpcBaselineFraming.SameHeader(original,decoded));
        var changed=decoded;changed.Epoch++;Require(!RebirthNpcBaselineFraming.SameHeader(original,changed));
        changed=decoded;changed.Connection++;Require(!RebirthNpcBaselineFraming.SameHeader(original,changed));
        changed=decoded;changed.Generation++;Require(!RebirthNpcBaselineFraming.SameHeader(original,changed));
        changed=decoded;changed.Sequence++;Require(!RebirthNpcBaselineFraming.SameHeader(original,changed));
        changed=decoded;changed.First--;Require(!RebirthNpcBaselineFraming.SameHeader(original,changed));
        changed=decoded;changed.Second++;Require(!RebirthNpcBaselineFraming.SameHeader(original,changed));
        for(int length=0;length<encoded.Length;length++){var bytes=new byte[length];Array.Copy(encoded,bytes,length);Reject(()=>Read(bytes));}
        var wrong=(byte[])encoded.Clone();wrong[0]=2;Reject(()=>Read(wrong));original.Sequence=0;Reject(()=>Encode(original));original.Sequence=1;original.Epoch=0;Reject(()=>Encode(original));original.Epoch=1;original.Connection=0;Reject(()=>Encode(original));original.Connection=1;original.First=RebirthNpcNetworkProtocol.MaxBaselineActors+1;Reject(()=>Encode(original));original.First=1;original.Second=int.MaxValue;Reject(()=>Encode(original));
        Require(RebirthNpcBaselineClientHeader.HeaderCompatible(9,10,false,32,4,9,11,false,16,0));Require(!RebirthNpcBaselineClientHeader.HeaderCompatible(9,10,false,32,4,8,11,false,32,4));Require(!RebirthNpcBaselineClientHeader.HeaderCompatible(9,10,false,32,4,10,9,false,32,4));Require(!RebirthNpcBaselineClientHeader.HeaderCompatible(9,10,false,32,4,9,10,false,31,4));Require(!RebirthNpcBaselineClientHeader.HeaderCompatible(9,10,false,32,4,9,10,true,32,4));Require(RebirthNpcBaselineClientHeader.HeaderCompatible(9,10,false,32,4,9,10,false,32,4));
        Require(RebirthNpcBaselineClientHeader.CompletionCompatible(10,100,5,105,false));Require(!RebirthNpcBaselineClientHeader.CompletionCompatible(10,100,5,104,false));Require(!RebirthNpcBaselineClientHeader.CompletionCompatible(10,0,11,0,false));Require(!RebirthNpcBaselineClientHeader.CompletionCompatible(0,0,0,-1,false));
    }
    private static byte[] Encode(RebirthNpcBaselineFraming.Frame frame){using(var bytes=new MemoryStream()){using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))RebirthNpcBaselineFraming.Write(writer,frame);return bytes.ToArray();}}
    private static RebirthNpcBaselineFraming.Frame Read(byte[] bytes){using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream)){var frame=RebirthNpcBaselineFraming.Read(reader);Require(stream.Position==stream.Length);return frame;}}
    private static void Reject(Action action){bool rejected=false;try{action();}catch(Exception error)when(error is InvalidDataException||error is EndOfStreamException){rejected=true;}Require(rejected);}
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Actual baseline V2 codec/policy fixture failed");}
}
#endif
