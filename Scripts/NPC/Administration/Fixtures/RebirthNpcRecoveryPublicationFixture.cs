#if DEBUG
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

// PREPARED, NOT RUN. Isolated disposable filesystem fixture for the actual
// publication primitive. This does not certify coordinator/native convergence.
internal static class RebirthNpcRecoveryPublicationFixture
{
    internal static void Verify()
    {
        string root=Path.Combine(Path.GetTempPath(),"rebirth-recovery-publication-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string primary=Path.Combine(root,"state.xml"),staged=Path.Combine(root,"immutable.xml");
            byte[] before=Encoding.UTF8.GetBytes("<state generation='1' action='before'/>");
            byte[] after=Encoding.UTF8.GetBytes("<state generation='1' action='new-action-after-install' replay='retained'/>");
            File.WriteAllBytes(primary,before);File.WriteAllBytes(staged,after);
            Publish(staged,primary,Digest(after));
            Equal(File.ReadAllBytes(primary),after);Equal(File.ReadAllBytes(staged),after);
            Equal(File.ReadAllBytes(primary+".recovery-previous"),before);

            // A later legitimate same-generation save stays authoritative. A
            // stale exact candidate cannot overwrite it under ambiguous temp.
            File.WriteAllBytes(primary,before);File.WriteAllBytes(primary+".recovery-publish.tmp",after);
            Refuses(()=>Publish(staged,primary,Digest(after)));
            Equal(File.ReadAllBytes(primary),before);Equal(File.ReadAllBytes(staged),after);
            File.Delete(primary+".recovery-publish.tmp");

            Refuses(()=>Publish(staged,primary,Digest(before)));
            Equal(File.ReadAllBytes(primary),before);Equal(File.ReadAllBytes(staged),after);
            Equal(File.ReadAllBytes(primary+".recovery-publish.tmp"),after); // evidence retained
        }
        finally{Directory.Delete(root,true);}
    }
    private static void Publish(string source,string target,string digest)
    {
        try{typeof(RebirthNpcRecoveryPublications).GetMethod("PublishExact",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{source,target,digest});}
        catch(TargetInvocationException error){throw error.InnerException;}
    }
    private static string Digest(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static void Equal(byte[] actual,byte[] expected){Require(actual.Length==expected.Length);for(int i=0;i<actual.Length;i++)Require(actual[i]==expected[i]);}
    private static void Refuses(Action action){bool refused=false;try{action();}catch(Exception error)when(error is IOException||error is InvalidDataException){refused=true;}Require(refused);}
    private static void Require(bool value){if(!value)throw new Exception("NPC recovery publication invariant failed.");}
}

#endif
