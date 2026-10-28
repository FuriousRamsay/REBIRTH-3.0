using System;
using System.IO;
using System.Linq;
using System.Text;
// ACTUAL_ARTIFACT_FILE_COMPARE
internal static class Fixture
{
    // Explicit native-current/Frame DOUBLE. Only actual file/stage methods are
    // exercised; this is NOT native publication, actor ownership or payment proof.
    private sealed class Frame {internal Guid Operation;}
    private sealed class Original {internal string Path,Digest;internal Frame Frame;internal bool CurrentDouble=true,IdentityDouble=true;internal bool Current()=>CurrentDouble;internal bool Identity()=>IdentityDouble;}
    // ACTUAL_STAGE_METHODS
    private static void Need(bool value,string label){if(!value)throw new Exception(label);}
    private static void Refuses(Action action,string label){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Need(rejected,label);}
    public static void Main(string[] args)
    {
        if(args.Length!=1||!Directory.Exists(args[0]))throw new Exception("Generated disposable directory required");
        string fixtureRoot=System.IO.Path.GetFullPath(args[0]);
        if(!fixtureRoot.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
            !System.IO.Path.GetFileName(fixtureRoot).StartsWith("rebirth-earned-stage-",StringComparison.Ordinal)||!Safe(fixtureRoot))throw new Exception("Refuse non-generated fixture directory");
        string path=System.IO.Path.Combine(args[0],"original.rew");var original=new Original{Path=path,Digest=new string('a',64),Frame=new Frame{Operation=Guid.NewGuid()}};
        Guid nonce=Guid.NewGuid();var expected=StageBytes(original,"attempt",nonce);
        Need(WriteExact(path+".attempt",expected,2048),"first actual durable file publication");
        Need(WriteExact(path+".attempt",expected,2048),"same original exact retry");
        Need(MatchesStage(original,"attempt",nonce),"actual exact stage match");
        Need(!MatchesStage(original,"attempt",Guid.NewGuid()),"different nonce cannot reuse actual primary");
        Need(!WriteExact(path+".attempt",StageBytes(original,"attempt",Guid.NewGuid()),2048),"conflicting same operation cannot overwrite");
        Need(Read(path+".attempt",2048).SequenceEqual(expected),"conflict preserves primary");
        File.WriteAllBytes(path+".attempt.tmp",new byte[]{9});
        Need(!MatchesStage(original,"attempt",nonce),"cold unknown tmp prevents positive match");
        Need(!WriteExact(path+".attempt",expected,2048),"tmp retained, no silent repair");
        Need(File.ReadAllBytes(path+".attempt.tmp").SequenceEqual(new byte[]{9}),"unknown tmp unchanged");
        File.Delete(path+".attempt.tmp"); // Generated fixture path ONLY, never live save.
        original.CurrentDouble=false;Need(!MatchesStage(original,"attempt",nonce),"scope/current double false refuses unchanged old primary");
        original.CurrentDouble=true;Need(MatchesStage(original,"attempt",nonce),"same-process exact scope retry");
        original.IdentityDouble=false;Need(!MatchesStage(original,"attempt",nonce),"identity fence refuses exact saved stage");original.IdentityDouble=true;
        string failed=path+".afterUpload";File.WriteAllBytes(failed+".tmp",new byte[]{7,8});
        Need(!WriteExact(failed,StageBytes(original,"afterUpload",nonce),2048)&&!File.Exists(failed),"failed publication does not invent final");
        Need(File.ReadAllBytes(failed+".tmp").SequenceEqual(new byte[]{7,8}),"failed candidate retained");
        string large=path+".large";File.WriteAllBytes(large,new byte[2049]);
        Refuses(()=>Read(large,2048),"file length before allocation");
        string artifact=path+".compare";var full=new byte[4113];for(int i=0;i<full.Length;i++)full[i]=(byte)(i%251);File.WriteAllBytes(artifact,full);
        Need(RebirthProductiveEarnedArtifact.FileMatches(artifact,full),"actual 4KiB streaming artifact equality");
        var different=(byte[])full.Clone();different[4096]^=1;
        Need(!RebirthProductiveEarnedArtifact.FileMatches(artifact,different),"actual boundary-spanning mismatch refused");
        File.WriteAllBytes(artifact+".tmp",new byte[]{5});
        Need(!RebirthProductiveEarnedArtifact.FileMatches(artifact,full)&&File.Exists(artifact+".tmp"),"streaming current rejects retained unknown tmp");
        File.Delete(artifact+".tmp");
        using(var stream=new FileStream(artifact,FileMode.Append,FileAccess.Write))stream.WriteByte(0);
        Need(!RebirthProductiveEarnedArtifact.FileMatches(artifact,full),"streaming exact length refuses trailing file bytes");
        Console.WriteLine("PASS actual file/stage/stream comparison methods only; Current/Frame explicit doubles, no native ownership/payment/publication authority");
    }
}
