#if DEBUG
using System;
using System.IO;
using System.Reflection;

// PREPARED, NOT RUN. Actual bounded staging helper, disposable temp files only.
internal static class RebirthNpcRecoveryStagingFixture
{
    internal static void Verify()
    {
        string root=Path.Combine(Path.GetTempPath(),"rebirth-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string source=Path.Combine(root,"backup"),target=Path.Combine(root,"staged");
            var bytes=new byte[131079];for(int i=0;i<bytes.Length;i++)bytes[i]=(byte)(i%251);
            File.WriteAllBytes(source,bytes);Stage(source,target);
            var actual=File.ReadAllBytes(target);Require(actual.Length==bytes.Length);
            for(int i=0;i<actual.Length;i++)Require(actual[i]==bytes[i]);
            Require(File.ReadAllBytes(source).Length==bytes.Length);
            Refuses(()=>Stage(source,target)); // immutable prior evidence
            File.WriteAllBytes(source,new byte[0]);string empty=Path.Combine(root,"empty");
            Refuses(()=>Stage(source,empty));Require(!File.Exists(empty));
            using(var file=new FileStream(source,FileMode.Create))file.SetLength(67108865);
            string oversized=Path.Combine(root,"oversized");Refuses(()=>Stage(source,oversized));Require(!File.Exists(oversized));
        }
        finally{Directory.Delete(root,true);}
    }
    private static void Stage(string source,string target)
    {
        try{typeof(RebirthNpcLiveRecoverySession).GetMethod("FreezeBackup",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{source,target});}
        catch(TargetInvocationException error){throw error.InnerException;}
    }
    private static void Refuses(Action action){bool rejected=false;try{action();}catch(Exception error)when(error is IOException||error is InvalidDataException){rejected=true;}Require(rejected);}
    private static void Require(bool value){if(!value)throw new Exception("Recovery staging invariant failed.");}
}

#endif
