using System;
// SOURCE
namespace UnityEngine {public static class Time {public static float unscaledDeltaTime;} public static class Mathf {public static float Clamp(float v,float min,float max){return Math.Max(min,Math.Min(max,v));}}}
public class GameManager {public static GameManager Instance;public bool Paused;public bool IsPaused(){return Paused;}}
class Test {
 static void Main(){
  UnityEngine.Time.unscaledDeltaTime=0.1f;
  if(RebirthStudyClock.ElapsedThisFrame()!=0)throw new Exception("missing game advanced");
  GameManager.Instance=new GameManager{Paused=true};
  if(RebirthStudyClock.ElapsedThisFrame()!=0)throw new Exception("paused study advanced");
  GameManager.Instance.Paused=false;
  if(RebirthStudyClock.ElapsedThisFrame()!=0.1f)throw new Exception("normal real-time study changed");
  UnityEngine.Time.unscaledDeltaTime=2f;if(RebirthStudyClock.ElapsedThisFrame()!=0.25f)throw new Exception("hitch cap changed");
  UnityEngine.Time.unscaledDeltaTime=-1f;if(RebirthStudyClock.ElapsedThisFrame()!=0f)throw new Exception("negative time");
  Console.WriteLine("PASS actual shared study clock: paused/missing game yields zero, active real seconds retained, hitch and negative bounds retained. Native time/pause substituted.");
 }
}
