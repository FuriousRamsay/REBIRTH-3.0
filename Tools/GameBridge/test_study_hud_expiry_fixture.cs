using System;
using UnityEngine;
namespace UnityEngine {
 public static class Time {public static float unscaledTime;}
 public static class Mathf {public static float Clamp01(float v){return Math.Max(0,Math.Min(1,v));}public static float Max(float a,float b){return Math.Max(a,b);}}
}
public enum RebirthStudyHudMode:byte {None,Reading,Audiobook}
// CLIENT_STATE
public static class StudyExpiryChecks {
 static bool Visible(){RebirthStudyHudMode m;string id;float p,r;bool slow;return RebirthStudyHudClientState.TryGet(out m,out id,out p,out r,out slow);}
 public static void Run(){
  RebirthStudyHudClientState.Reset();Time.unscaledTime=10;
  RebirthStudyHudClientState.Receive(RebirthStudyHudMode.Reading,"book",.5f,100,false,1);
  Time.unscaledTime=14.99f;if(!Visible())throw new Exception("early expiry");
  Time.unscaledTime=15;if(Visible())throw new Exception("lost clear stays visible");
  RebirthStudyHudClientState.Receive(RebirthStudyHudMode.Reading,"book",.5f,100,false,1);
  if(Visible())throw new Exception("duplicate revives expired display");
  RebirthStudyHudClientState.Receive(RebirthStudyHudMode.Audiobook,"audio",.6f,80,false,2);
  if(!Visible())throw new Exception("new snapshot does not restore display");
  RebirthStudyHudClientState.Receive(RebirthStudyHudMode.None,"",0,0,false,3);
  if(Visible())throw new Exception("explicit clear ignored");
  RebirthStudyHudClientState.Receive(RebirthStudyHudMode.Reading,"book",.5f,100,false,4);
  Time.unscaledTime=1;if(Visible())throw new Exception("clock rewind retained stale display");
 }
}