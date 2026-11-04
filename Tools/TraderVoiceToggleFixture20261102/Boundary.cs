namespace UnityEngine { public class AudioClip {} public class GameObject {} }
namespace UnityEngine.Scripting { public class PreserveAttribute:System.Attribute {} }
namespace HarmonyLib { public class Harmony { public Harmony(string s){} } }
public class Mod { public string Path; }
public interface IModApi { void InitMod(Mod m); }
public static class RebirthHarmonyBootstrap { public static void PatchClassOnce(object h,System.Type t){} }
public static class Log { public static void Out(string s){} }
public static class GameManager { public static bool IsDedicatedServer; }
public static class TraderInfo { public static bool TraderDialog; }
public class NPCInfo { public string Id; }
public class EntityNPC { public NPCInfo NPCInfo; }
public class EntityTrader:EntityNPC {}
public class LocalPlayerUI {
 public object entityPlayer=new(); public static bool Ready=true;
 public static LocalPlayerUI GetUIForPrimaryPlayer()=>Ready?new LocalPlayerUI():null;
}
public static class DataLoader { public static int Loads; public static T LoadAsset<T>(string p) where T:new(){Loads++;return new T();} }
public static class RebirthTraderVoicePreferences {
 public static string[] Traders={"Rekt","Hugh","Jen","Joel","Bob"};
 public static string[] Presets={"ORIGINAL","REKT","HUGH","JEN","JOEL","BOB","POPPY"};
 public static int Selection;
 public static bool ValidTrader(string t)=>System.Array.IndexOf(Traders,t)>=0;
 public static int Get(string t)=>ValidTrader(t)?Selection:0;
}
namespace Audio {
 public class ClipSourceMap {public string clipName,audioSourceName,subtitleID;public bool hasSubtitle;}
 public class XmlData {
  public string soundGroupName; public int maxVoices,channel,priority;
  public float maxRepeatRate,localCrouchVolumeScale,runningVolumeScale,noiseScale,crouchNoiseScale,maxVolume,lowestPitch,highestPitch;
  public object noiseData;public bool vibratesController; public List<ClipSourceMap> audioClipMap=new();
 }
 public class SubtitleData {public string name,speakerLocId,contentLocId;}
 public static class Manager {
  public static Dictionary<string,XmlData> audioData=new();
  public static Dictionary<string,UnityEngine.AudioClip> audioClipAssetCache=new();
  public static Dictionary<string,SubtitleData> subtitleCache=new();
  public static int Plays;
  public static void PlayInsidePlayerHead(string s,int i,float f,bool a,bool b){Plays++;}
 }
}
public class RebirthTraderVoiceHeadPatch {} public class RebirthTraderVoicePositionPatch {} public class RebirthTraderVoiceEntityPatch {}
public class RebirthTraderVoiceBuyPatch {} public class RebirthTraderVoiceSellPatch {} public class RebirthTraderVoiceBrowsePatch {}
public class RebirthTraderVoiceQuestCompletePatch {} public class RebirthTraderVoiceQuestAcceptPatch {} public class RebirthTraderVoicePrematurePatch {} public class RebirthTraderVoiceDialogPatch {}
