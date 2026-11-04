using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
namespace Platform { public static class PlatformManager { public static object InternalLocalUserIdentifier=new object(); } }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
public class EntityPlayerLocal { }
public class WorldState { public string Guid; }
public class World { public WorldState worldState=new WorldState(); public EntityPlayerLocal Player=new EntityPlayerLocal(); public bool IsRemote()=>false; public EntityPlayerLocal GetPrimaryPlayer()=>Player; }
public class GameManager { public static GameManager Instance=new GameManager();public World World; }
public static class GameIO { public static string Save,User;public static string GetSaveGameDir()=>Save;public static string GetUserGameDataDir()=>User; }
public enum EnumGamePrefs { GameGuidClient }
public static class GamePrefs { public static string GetString(EnumGamePrefs pref)=>""; }
public class RebirthStablePlayerIdentity {
 public string StorageKey=new string('a',64);
 public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}
 public static string ComputeStorageKey(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
public static class Log { public static void Warning(string text){} }
// Native world/platform and atomic IO transport are doubled. Production store,
// queue/context lifecycle and preference models are linked without extraction.
public static class RebirthAtomicXmlFile {
 public static bool Fail; public static readonly List<string> Writes=new List<string>();
 public static bool TryWrite(string path,XDocument doc,out string error){Writes.Add(path);error=Fail?"injected IO failure":"";if(Fail)return false;Directory.CreateDirectory(Path.GetDirectoryName(path));doc.Save(path);return true;}
 public static bool TryLoad(string path,out XDocument doc,out string error){error="";try{doc=XDocument.Load(path);return true;}catch(Exception e){doc=null;error=e.Message;return false;}}
 public static bool TryLoadFinalThenBackup(string path,out XDocument doc,out bool backup,out string error){backup=!File.Exists(path);return TryLoad(backup?path+".bak":path,out doc,out error);}
}
