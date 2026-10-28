using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
// ACTUAL_PURE_CODEC
internal static class Fixture
{
    private static void Need(bool ok,string label){if(!ok)throw new Exception(label);}
    private static void Refuses(Action action,string label)
    {bool refused=false;try{action();}catch(InvalidDataException){refused=true;}catch(EndOfStreamException){refused=true;}Need(refused,label);}
    public static void Main()
    {
        var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=Guid.NewGuid(),Operation=Guid.NewGuid(),World=Convert.ToBase64String(new byte[32]),RowIndex=15,Multiplier=2,WorkedSeconds=4,
            BeforeRow=new byte[]{1,2},AfterRow=new byte[]{3,4},BeforeTools=new byte[]{5},AfterTools=new byte[]{6}};
        var bytes=RebirthProductiveEarnedRowProof.Encode(frame);
        var copy=RebirthProductiveEarnedRowProof.Decode(bytes);
        Need(copy.Birth==frame.Birth&&copy.Operation==frame.Operation&&copy.Multiplier==2,"exact identity roundtrip");
        var marker=RebirthProductiveEarnedRowProof.Marker(frame,bytes);
        Need(marker.Length<=512&&RebirthProductiveEarnedRowProof.MatchesMarker(marker,bytes),"bounded marker match");
        var changed=(byte[])bytes.Clone();changed[changed.Length-1]^=1;
        Need(!RebirthProductiveEarnedRowProof.MatchesMarker(marker,changed),"changed full tools refused");
        Refuses(()=>RebirthProductiveEarnedRowProof.Decode(bytes.Concat(new byte[]{0}).ToArray()),"trailing bytes");
        Refuses(()=>RebirthProductiveEarnedRowProof.Decode(bytes.Take(bytes.Length-1).ToArray()),"truncated exact frame");
        // Fixed envelope: 4 magic +16 birth +16 op +32 world +4 row +4 multiplier +4 work =80.
        var malformed=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(int.MaxValue),0,malformed,80,4);
        Refuses(()=>RebirthProductiveEarnedRowProof.Decode(malformed),"oversized nested frame before allocation");
        frame.WorkedSeconds=0;Refuses(()=>RebirthProductiveEarnedRowProof.Encode(frame),"zero work");
        frame.WorkedSeconds=float.NaN;Refuses(()=>RebirthProductiveEarnedRowProof.Encode(frame),"NaN work");
        frame.WorkedSeconds=4;frame.BeforeTools=new byte[RebirthProductiveEarnedRowProof.MaximumFrameBytes+1];
        Refuses(()=>RebirthProductiveEarnedRowProof.Encode(frame),"rich individual overlimit");
        frame.BeforeTools=new byte[RebirthProductiveEarnedRowProof.MaximumFrameBytes];
        Need(RebirthProductiveEarnedRowProof.Decode(RebirthProductiveEarnedRowProof.Encode(frame)).BeforeTools.Length==frame.BeforeTools.Length,"exact rich boundary");
        frame.BeforeTools=new byte[]{5};
        // REW2 framing only: opaque bodies are deliberately NOT native TE evidence.
        frame.BeforeNative=new byte[2097088];frame.AfterNative=new byte[2097088];
        frame.BeforeElapsedOffset=13;frame.AfterElapsedOffset=13;
        var rew2=RebirthProductiveEarnedRowProof.Encode(frame);
        Need(RebirthProductiveEarnedRowProof.Decode(rew2).HasNativeBodies,"REW2 full-body framing boundary");
        Need(rew2.Length<RebirthProductiveEarnedRowProof.MaximumArtifactBytes,"scoped 6MiB artifact bound");
        Need(rew2.Length==112+frame.BeforeRow.Length+frame.AfterRow.Length+frame.BeforeTools.Length+frame.AfterTools.Length+frame.BeforeNative.Length+frame.AfterNative.Length,"exact allocation length includes native offsets and lengths");
        var wrongCanonical=(byte[])rew2.Clone();wrongCanonical[wrongCanonical.Length-1]^=1;
        Refuses(()=>RebirthProductiveEarnedRowProof.Marker(frame,wrongCanonical),"streaming canonical comparison rejects changed final body byte");
        var richMarker=RebirthProductiveEarnedRowProof.Marker(frame,rew2);
        Need(RebirthProductiveEarnedRowProof.MatchesMarker(richMarker,rew2),"streaming REW2 canonical marker comparison");
        frame.FullRecipeBinding="<full-authored-recipe tool='1' output='2' count='3'/>";frame.ExtrasDefinition="none";
        var rew3=RebirthProductiveEarnedRowProof.Encode(frame);var recipeCopy=RebirthProductiveEarnedRowProof.Decode(rew3);
        Need(recipeCopy.HasRecipeDefinition&&recipeCopy.FullRecipeBinding==frame.FullRecipeBinding&&recipeCopy.ExtrasDefinition=="none","REW3 full original definition framing");
        var definitionMarker=RebirthProductiveEarnedRowProof.Marker(frame,rew3);frame.FullRecipeBinding+="changed";
        Refuses(()=>RebirthProductiveEarnedRowProof.Marker(frame,rew3),"changed authored recipe cannot share native-only marker");
        Need(RebirthProductiveEarnedRowProof.MatchesMarker(definitionMarker,rew3),"definition original marker remains canonical");
        frame.FullRecipeBinding=new string('x',RebirthProductiveEarnedRowProof.MaximumFrameBytes+1);
        Refuses(()=>RebirthProductiveEarnedRowProof.Encode(frame),"full definition bounded before encoded allocation");
        frame.FullRecipeBinding=null;frame.ExtrasDefinition=null;
        int nativeLength=80+16+frame.BeforeRow.Length+frame.AfterRow.Length+frame.BeforeTools.Length+frame.AfterTools.Length+8;
        var malformedBody=(byte[])rew2.Clone();Array.Copy(BitConverter.GetBytes(int.MaxValue),0,malformedBody,nativeLength,4);
        Refuses(()=>RebirthProductiveEarnedRowProof.Decode(malformedBody),"native body length before allocation");
        frame.BeforeElapsedOffset=-1;Refuses(()=>RebirthProductiveEarnedRowProof.Encode(frame),"invalid independent elapsed span");
        frame.BeforeElapsedOffset=13;frame.BeforeNative=new byte[2097089];
        Refuses(()=>RebirthProductiveEarnedRowProof.Encode(frame),"native body above exact frame allowance");
        frame.BeforeNative=null;frame.AfterNative=null;frame.BeforeElapsedOffset=frame.AfterElapsedOffset=-1;
        frame.World="unknown";Refuses(()=>RebirthProductiveEarnedRowProof.Encode(frame),"invalid world");
        Console.WriteLine("PASS pure artifact codec only; no native work, publication, lease or output authority exercised");
    }
}
