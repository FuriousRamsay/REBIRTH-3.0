using System;
class SdPlayerPrefs {
 public static int Value=75;public static bool Fail;public static int GetInt(string k,int d){return Value;}
 public static void SetInt(string k,int v){Value=v;}public static void Save(){if(Fail)throw new Exception("disk");}
}
class Mathf {
 public static float Max(float a,float b){return Math.Max(a,b);}
 public static float MoveTowards(float a,float b,float d){return a<b?Math.Min(a+d,b):Math.Max(a-d,b);}
}
class Time {public static float unscaledDeltaTime=.125f;}
class AudioSource {public float volume;public bool isPlaying=true;public void Pause(){isPlaying=false;}}
// PREFERENCES
class Check {
 static AudioSource audioSource=new AudioSource();static int currentCassetteIndex=1;
 static float fadeGain,volume=.8f;static bool paused;
 // SOURCE
 static void Main(){
 if(RebirthMusicPreferences.LoadVolume()!=.75f)throw new Exception("default");
 if(!RebirthMusicPreferences.TrySaveVolume(.4f)||RebirthMusicPreferences.LoadVolume()!=.4f)throw new Exception("persist");
 if(RebirthMusicPreferences.TrySaveVolume(float.NaN)||RebirthMusicPreferences.TrySaveVolume(float.PositiveInfinity))throw new Exception("nonfinite");
 if(!RebirthMusicPreferences.TrySaveVolume(2f)||SdPlayerPrefs.Value!=100)throw new Exception("clamp");
 SdPlayerPrefs.Fail=true;if(RebirthMusicPreferences.TrySaveVolume(.5f))throw new Exception("failure reported success");if(SdPlayerPrefs.Value!=100)throw new Exception("failed save changed cached preference");SdPlayerPrefs.Fail=false;
 UpdateFade();if(fadeGain!=.5f||audioSource.volume!=.4f)throw new Exception("half fade");
 UpdateFade();if(fadeGain!=1f||audioSource.volume!=.8f)throw new Exception("full fade");
 paused=true;UpdateFade();if(!audioSource.isPlaying||fadeGain!=.5f)throw new Exception("early pause");
 UpdateFade();if(audioSource.isPlaying||audioSource.volume!=0f)throw new Exception("pause at zero");
 paused=false;audioSource.isPlaying=true;UpdateFade();if(fadeGain!=.5f)throw new Exception("resume");
 paused=true;UpdateFade();if(fadeGain!=0f)throw new Exception("reversal");
 Console.WriteLine("PASS actual music preference/fade: persisted level, clamp/nonfinite/failure; fade in/out/pause/reversal");
 }
}