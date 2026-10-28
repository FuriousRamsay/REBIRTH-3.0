using System;
using System.IO;
using System.Reflection;
using System.Linq;

public sealed class World { }
public sealed class GameManager { public static GameManager Instance=new GameManager(); public World World; }
public static class GameIO { public static string Root; public static string GetSaveGameDir()=>Root; }
public static class RebirthWorldCharacterRepository
{
    public static bool IsServerAuthority=true, Retained;
    public static int Checks;
    public static bool HasRetainedRemoteResourceRefunds(){Checks++;return Retained;}
}
public static class RemoteResourceRefundRecord
{
    public static bool ValidStorageKey(string key)=>key!=null&&key.Length==64&&key.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
}
internal static class Program
{
    static int checks;
    static void Check(bool condition,string title){if(!condition)throw new Exception(title);checks++;Console.WriteLine("PASS "+title);}
    static string PathFor(string root)=>Path.Combine(root,"RebirthData","RemoteResources","worldIdentity.xml");
    static void Ready(string root, World world){GameIO.Root=root;GameManager.Instance.World=world;}
    static void RetryNow()=>typeof(RemoteResourceWorldIdentity).GetField("retryAfter",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,0L);
    static int Main()
    {
        string dir=Path.Combine(Path.GetTempPath(),"RebirthRemoteIdentityIo_"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);var world=new World();Ready(Path.Combine(dir,"blocked"),world);
            RebirthWorldCharacterRepository.Retained=true;
            Check(!RemoteResourceWorldIdentity.TryGet(world,out _),"retained custody refuses new identity");
            int scans=RebirthWorldCharacterRepository.Checks;
            for(int i=0;i<1000;i++)if(RemoteResourceWorldIdentity.TryGet(world,out _))throw new Exception("retry gate admitted");
            Check(RebirthWorldCharacterRepository.Checks==scans,"1000 repeated failures cause no extra custody scans");
            RetryNow();Check(!RemoteResourceWorldIdentity.TryGet(world,out _)&&RebirthWorldCharacterRepository.Checks==scans+1,"expired failure retries validation");
            Ready(Path.Combine(dir,"other"),world);RebirthWorldCharacterRepository.Retained=false;
            Check(RemoteResourceWorldIdentity.TryGet(world,out var key),"save-path change bypasses old refusal and initializes");
            Check(File.Exists(PathFor(GameIO.Root)),"identity published at current save path");
            Check(RemoteResourceWorldIdentity.TryGet(world,out var second)&&key==second,"valid key remains stable");
            File.WriteAllText(PathFor(GameIO.Root),"broken");
            Check(!RemoteResourceWorldIdentity.TryGet(world,out _),"valid prior read does not hide later corruption");
            File.WriteAllText(PathFor(GameIO.Root),"<remoteResourceWorld version=\"1\" key=\""+key+"\"/>");
            Check(!RemoteResourceWorldIdentity.TryGet(world,out _),"repair remains refused until bounded retry");
            var replacementWorld=new World();Ready(GameIO.Root,replacementWorld);
            Check(RemoteResourceWorldIdentity.TryGet(replacementWorld,out var recovered)&&recovered==key,"new live world bypasses old refusal without new key");
            Check(!RemoteResourceWorldIdentity.TryGet(world,out _),"stale world cannot use current save identity");
            RebirthWorldCharacterRepository.IsServerAuthority=false;
            Check(!RemoteResourceWorldIdentity.TryGet(replacementWorld,out _),"client authority remains refused");
            RebirthWorldCharacterRepository.IsServerAuthority=true;
            Ready(Path.Combine(dir,"malformed-player"),world);
            var players=Path.Combine(GameIO.Root,"RebirthData","Survivor","Players");Directory.CreateDirectory(players);
            File.WriteAllText(Path.Combine(players,"bad.xml"),"malformed");
            Check(!RemoteResourceWorldIdentity.TryGet(world,out _),"unreadable player custody refuses initialization");
            scans=RebirthWorldCharacterRepository.Checks;
            for(int i=0;i<1000;i++)RemoteResourceWorldIdentity.TryGet(world,out _);
            Check(RebirthWorldCharacterRepository.Checks==scans,"malformed-player retry burst is bounded");
            File.WriteAllText(Path.Combine(players,"bad.xml"),"<rebirthWorldCharacter/>");RetryNow();
            Check(RemoteResourceWorldIdentity.TryGet(world,out _),"repaired player custody permits next retry");
            Ready(Path.Combine(dir,"backup-only"),world);Directory.CreateDirectory(Path.GetDirectoryName(PathFor(GameIO.Root)));
            File.WriteAllText(PathFor(GameIO.Root)+".bak","existing");
            Check(!RemoteResourceWorldIdentity.TryGet(world,out _)&&!File.Exists(PathFor(GameIO.Root)),"backup evidence never mints a replacement identity");
            Console.WriteLine("PASS total="+checks);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        finally {try{Directory.Delete(dir,true);}catch{}}
    }
}
