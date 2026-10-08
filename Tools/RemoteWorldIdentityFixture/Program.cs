using System;using System.IO;using System.Linq;
public class World {}
public class GameManager {public static GameManager Instance=new();public World World;}
public static class GameIO {public static string Root;public static Action ReadHook;public static string GetSaveGameDir(){ReadHook?.Invoke();return Root;}}
public static class RebirthWorldCharacterRepository {public static bool IsServerAuthority=true,RetainedRefund;public static bool HasRetainedRemoteResourceRefunds()=>RetainedRefund;}
public static class RemoteResourceRefundRecord {public static bool ValidStorageKey(string s)=>s!=null&&s.Length==64&&s.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');}
public static class Program {
 static int tests;static void A(bool ok,string label){if(!ok)throw new Exception(label);tests++;}
 public static void Main(){
  string temp=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"rebirth-world-identity-"+Guid.NewGuid().ToString("N")));
  Directory.CreateDirectory(temp);
  try{
   var world=new World();GameManager.Instance.World=world;GameIO.Root=Path.Combine(temp,"original");
   A(RemoteResourceWorldIdentity.TryGet(world,out var first)&&first.Length==64,"create durable world identity");
   A(RemoteResourceWorldIdentity.TryGet(world,out var second)&&first==second,"repeat reads same identity");
   A(!RemoteResourceWorldIdentity.TryGet(new World(),out _),"foreign world refused");
   RebirthWorldCharacterRepository.IsServerAuthority=false;A(!RemoteResourceWorldIdentity.TryGet(world,out _),"client cannot create or read authoritative identity");RebirthWorldCharacterRepository.IsServerAuthority=true;
   string original=Path.Combine(GameIO.Root,"RebirthData","RemoteResources","worldIdentity.xml");
   GameIO.Root=Path.Combine(temp,"moved");string moved=Path.Combine(GameIO.Root,"RebirthData","RemoteResources","worldIdentity.xml");Directory.CreateDirectory(Path.GetDirectoryName(moved));File.Copy(original,moved);
   A(RemoteResourceWorldIdentity.TryGet(world,out second)&&first==second,"relocated save retains world identity");
   File.WriteAllText(moved,"broken");A(!RemoteResourceWorldIdentity.TryGet(world,out _)&&File.ReadAllText(moved)=="broken","corrupt final not replaced");
   File.Delete(moved);File.WriteAllText(moved+".bak","retained");
   A(!RemoteResourceWorldIdentity.TryGet(world,out _)&&!File.Exists(moved),"backup-only identity not regenerated");File.Delete(moved+".bak");
   File.WriteAllText(moved+".tmp","uncertain");A(!RemoteResourceWorldIdentity.TryGet(world,out _)&&!File.Exists(moved),"temp-only identity not regenerated");File.Delete(moved+".tmp");
   File.WriteAllText(moved,new string('x',4097));A(!RemoteResourceWorldIdentity.TryGet(world,out _),"oversized identity refused");
   File.WriteAllText(moved,"<!DOCTYPE x [<!ENTITY y 'bad'>]><remoteResourceWorld version='1' key='"+first+"'/>");A(!RemoteResourceWorldIdentity.TryGet(world,out _),"DTD identity refused");
   File.WriteAllText(moved,"<remoteResourceWorld version='1' key='"+first+"' extra='1'/>");A(!RemoteResourceWorldIdentity.TryGet(world,out _),"unexpected identity shape refused");
   GameIO.Root=Path.Combine(temp,"switching");string switchingFinal=Path.Combine(GameIO.Root,"RebirthData","RemoteResources","worldIdentity.xml");
   int reads=0;GameIO.ReadHook=()=>{if(++reads==3)GameIO.Root=Path.Combine(temp,"retired");};
   A(!RemoteResourceWorldIdentity.TryGet(world,out var refusedKey)&&refusedKey==null&&!File.Exists(switchingFinal),"changed save root cannot install or return old world key");
   GameIO.ReadHook=null;
   GameIO.Root=Path.Combine(temp,"custody");string custodyFinal=Path.Combine(GameIO.Root,"RebirthData","RemoteResources","worldIdentity.xml");
   string players=Path.Combine(GameIO.Root,"RebirthData","Survivor","Players");Directory.CreateDirectory(players);string playerFile=Path.Combine(players,"owner.xml");
   File.WriteAllText(playerFile,"<rebirthWorldCharacter><support><remoteResourceRefunds /></support></rebirthWorldCharacter>");
   A(!RemoteResourceWorldIdentity.TryGet(world,out _)&&!File.Exists(custodyFinal),"retained refund section prevents replacement world identity");
   File.Delete(playerFile);File.WriteAllText(playerFile+".bak","<rebirthWorldCharacter><support><remoteResourceRefunds /></support></rebirthWorldCharacter>");
   A(!RemoteResourceWorldIdentity.TryGet(world,out _)&&!File.Exists(custodyFinal),"backup refund section prevents replacement world identity");File.Delete(playerFile+".bak");
   File.WriteAllText(playerFile+".tmp","<rebirthWorldCharacter><support><remoteResourceRefunds /></support></rebirthWorldCharacter>");
   A(!RemoteResourceWorldIdentity.TryGet(world,out _)&&!File.Exists(custodyFinal),"temporary refund section prevents replacement world identity");File.Delete(playerFile+".tmp");
   File.WriteAllText(playerFile,"<rebirthWorldCharacter><support>");
   A(!RemoteResourceWorldIdentity.TryGet(world,out _)&&!File.Exists(custodyFinal),"unreadable character cannot establish absent refund custody");
   File.WriteAllText(playerFile,"<!DOCTYPE x><rebirthWorldCharacter/>");
   A(!RemoteResourceWorldIdentity.TryGet(world,out _),"DTD character cannot establish absent refund custody");
   File.WriteAllText(playerFile,"<rebirthWorldCharacter><support /></rebirthWorldCharacter>");
   RebirthWorldCharacterRepository.RetainedRefund=true;
   A(!RemoteResourceWorldIdentity.TryGet(world,out _)&&!File.Exists(custodyFinal),"unsaved cached refund prevents replacement world identity");RebirthWorldCharacterRepository.RetainedRefund=false;
   A(RemoteResourceWorldIdentity.TryGet(world,out var legacyKey)&&legacyKey.Length==64,"legacy character without refund permits first identity");
   File.WriteAllText(playerFile,"<rebirthWorldCharacter><support><remoteResourceRefunds /></support></rebirthWorldCharacter>");
   A(RemoteResourceWorldIdentity.TryGet(world,out var retainedKey)&&retainedKey==legacyKey,"existing valid world identity remains stable with refund journal");
   Console.WriteLine("PASS "+tests+" actual world identity / atomic file checks; native world, save-root and key-validator adapters doubled. Only private synthetic temp files used.");
  }finally{
   string resolved=Path.GetFullPath(temp);if(resolved!=temp||!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture cleanup");
   foreach(var file in Directory.GetFiles(resolved,"*",SearchOption.AllDirectories)){if(!Path.GetFullPath(file).StartsWith(resolved+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture file");File.Delete(file);}
   foreach(var directory in Directory.GetDirectories(resolved,"*",SearchOption.AllDirectories).OrderByDescending(d=>d.Length))Directory.Delete(directory);
   Directory.Delete(resolved);
  }
 }
}