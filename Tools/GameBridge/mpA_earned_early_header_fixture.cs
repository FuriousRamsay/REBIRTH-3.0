using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
// ACTUAL_PURE_CODEC
// ACTUAL_NATIVE_COMPARISON
// ACTUAL_EARLY_MANIFEST
internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal static bool FixtureRead(byte[] bytes,out Guid birth,out int offset,out int length,out byte[] comparison,out string digest)
        =>Manifest(bytes,out birth,out offset,out length,out comparison,out digest);
}
internal static class Fixture
{
    private static void Need(bool value,string label){if(!value)throw new Exception(label);}
    private static void Refuses(Action action,string label){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Need(rejected,label);}
    public static void Main()
    {
        var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=Guid.NewGuid(),Operation=Guid.NewGuid(),World=Convert.ToBase64String(new byte[32]),
            RowIndex=0,Multiplier=1,WorkedSeconds=2,BeforeRow=new byte[]{1},AfterRow=new byte[]{2},BeforeTools=new byte[]{3},AfterTools=new byte[]{4},
            BeforeNative=new byte[128],AfterNative=new byte[128],BeforeElapsedOffset=40,AfterElapsedOffset=40};
        // Opaque native bytes are comparison inputs only, NOT native TE proof.
        var image=RebirthProductiveEarnedRowProof.Encode(frame);
        var text=RebirthProductiveEarnedRemoteHost.EarlyManifest(frame,image);var bytes=Convert.FromBase64String(text);
        Need(text.Length==120&&bytes.Length==89,"bounded one-packet manifest");
        Need(RebirthProductiveEarnedRemoteHost.FixtureRead(bytes,out var birth,out var offset,out var length,out var comparison,out var digest)&&
            birth==frame.Birth&&offset==40&&length==128&&digest.Length==64,"actual manifest parser roundtrip");
        Need(!RebirthProductiveEarnedRemoteHost.FixtureRead(bytes.Take(88).ToArray(),out _,out _,out _,out _,out _),"truncated manifest refuses");
        Need(!RebirthProductiveEarnedRemoteHost.FixtureRead(bytes.Concat(new byte[]{0}).ToArray(),out _,out _,out _,out _,out _),"trailing manifest refuses");
        var bad=(byte[])bytes.Clone();bad[0]=2;
        Need(!RebirthProductiveEarnedRemoteHost.FixtureRead(bad,out _,out _,out _,out _,out _),"unknown version refuses");
        bad=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(int.MaxValue),0,bad,21,4);
        Need(!RebirthProductiveEarnedRemoteHost.FixtureRead(bad,out _,out _,out _,out _,out _),"body length bounded before allocation");
        bad=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(-1),0,bad,17,4);
        Need(!RebirthProductiveEarnedRemoteHost.FixtureRead(bad,out _,out _,out _,out _,out _),"negative elapsed offset refuses");
        var elapsed=(byte[])frame.BeforeNative.Clone();for(int i=40;i<48;i++)elapsed[i]=255;
        Need(RebirthProductiveEarnedNativeBody.ComparisonDigest(elapsed,40).SequenceEqual(comparison),"only exact elapsed span normalized");
        elapsed[39]=1;Need(!RebirthProductiveEarnedNativeBody.ComparisonDigest(elapsed,40).SequenceEqual(comparison),"pre-span native change refuses");
        elapsed=(byte[])frame.BeforeNative.Clone();elapsed[48]=1;
        Need(!RebirthProductiveEarnedNativeBody.ComparisonDigest(elapsed,40).SequenceEqual(comparison),"post-span native change refuses");
        Refuses(()=>RebirthProductiveEarnedNativeBody.ComparisonDigest(elapsed,127),"invalid comparison offset");
        Console.WriteLine("PASS actual pure early manifest/comparison methods; no authority, native upload, work or publication exercised");
    }
}
