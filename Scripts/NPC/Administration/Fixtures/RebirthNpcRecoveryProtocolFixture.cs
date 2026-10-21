#if DEBUG
using System;
using System.IO;
using System.Text;

// PREPARED, NOT RUN. Uses actual production codec methods. No Unity/world,
// fake ClientInfo, fabricated ACK, store mutation or transport certification.
internal static class RebirthNpcRecoveryProtocolFixture
{
    internal static void Verify()
    {
        var original=new RebirthNpcRecoveryProjectionNetwork.Probe{Operation=Guid.NewGuid(),Challenge=Guid.NewGuid(),Generation=7,Sequence=11,NameSequence=12,
            Entity=17,Stable=new RebirthNpcStableId(3,9),Epoch=5,Connection=6,Revision=2,Digest=new string('a',64),Name=new string('\u754c',256)};
        byte[] complete=Encode(original,true);Require(complete.Length<2048&&complete.Length<13*1024);
        var round=Read(complete,true);Require(round.Operation==original.Operation&&round.Challenge==original.Challenge&&round.Generation==7&&round.Sequence==11&&
            round.Stable==original.Stable&&round.Entity==17&&round.Epoch==5&&round.Connection==6&&round.Revision==2&&round.Name==original.Name&&round.Digest==original.Digest);
        for(int length=0;length<complete.Length;length++)
        {var truncated=new byte[length];Buffer.BlockCopy(complete,0,truncated,0,length);Refuses(()=>Read(truncated,true));}
        byte[] ack=Encode(original,false);Require(ack.Length<256);var observed=Read(ack,false);
        Require(observed.Operation==original.Operation&&observed.Challenge==original.Challenge&&observed.Generation==original.Generation&&observed.Digest==original.Digest);
        var wrong=(byte[])complete.Clone();wrong[0]=9;Refuses(()=>Read(wrong,true));
        // Digest length starts after the fixed identity fields (78 bytes).
        var oversized=(byte[])complete.Clone();for(int i=0;i<4;i++)oversized[78+i]=255;oversized[82]=7;
        Refuses(()=>Read(oversized,true));
        original.Name=new string('x',257);Refuses(()=>Encode(original,true));
        original.Name="valid";original.Generation=0;Refuses(()=>Encode(original,true));
    }
    private static byte[] Encode(RebirthNpcRecoveryProjectionNetwork.Probe image,bool name)
    {using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))NetPackageRebirthNpcRecoveryProjectionProbe.Write(writer,image,name);return stream.ToArray();}}
    private static RebirthNpcRecoveryProjectionNetwork.Probe Read(byte[] bytes,bool name)
    {using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream,Encoding.UTF8)){var value=NetPackageRebirthNpcRecoveryProjectionProbe.Read(reader,name);Require(stream.Position==stream.Length);return value;}}
    private static void Refuses(Action action)
    {bool rejected=false;try{action();}catch(Exception ex)when(ex is IOException||ex is InvalidDataException||ex is ArgumentException){rejected=true;}Require(rejected);}
    private static void Require(bool value){if(!value)throw new InvalidOperationException("Recovery projection production codec invariant failed");}
}

#endif
