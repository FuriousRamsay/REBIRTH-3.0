using System;
using Newtonsoft.Json.Linq;
sealed class World { }
sealed class GameManager { internal static GameManager Instance=new(); internal World World=new(); }
static class RebirthGameBridge { internal static bool IsRunning=true; }
enum EnumGamePrefs { GameName }
static class GamePrefs { internal static string Save="CodexTestPurgeFresh20261102"; internal static string GetString(EnumGamePrefs p)=>Save; }
namespace UnityEngine { static class Time { internal static float realtimeSinceStartup; } }
static class Program
{
 static int checks;
 static void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;}
 static long Total()=>RebirthPurgeFeedbackTrace.Observe()["total"].Value<long>();
 static void Main()
 {
  Check(Total()==0,"empty observation");
  RebirthPurgeFeedbackTrace.Record("discovery-tooltip","cabin_01","pine_forest","colored report");
  Check(Total()==1,"current fixture accepted");
  var observed=RebirthPurgeFeedbackTrace.Observe();
  observed["events"][0]["value"]="mutated";
  Check(RebirthPurgeFeedbackTrace.Observe()["events"][0]["value"].Value<string>()=="colored report","returned snapshot isolated");
  for(int i=0;i<200;i++)RebirthPurgeFeedbackTrace.Record("clear-audio","cabin_01","pine_forest","purge_discovered");
  observed=RebirthPurgeFeedbackTrace.Observe();
  Check(Total()==201&&((JArray)observed["events"]).Count==128,"bounded events preserve total");
  Check(observed["events"][0]["sequence"].Value<long>()==74,"oldest events evicted");
  RebirthPurgeFeedbackTrace.Record("clear-tooltip","cabin_01","pine_forest",new string('x',5000));
  observed=RebirthPurgeFeedbackTrace.Observe();
  Check(observed["events"][127]["value"].Value<string>().Length==4096&&observed["events"][127]["valueTruncated"].Value<bool>(),"bounded payload disclosed");
  GamePrefs.Save="PurgeTest";RebirthPurgeFeedbackTrace.Record("must ignore",null,null,null);
  Check(Total()==202,"owner save ignored");
  GamePrefs.Save="CodexTestPurgeFresh20261102";RebirthGameBridge.IsRunning=false;
  RebirthPurgeFeedbackTrace.Record("must ignore",null,null,null);Check(Total()==202,"bridge disabled ignored");
  RebirthGameBridge.IsRunning=true;GameManager.Instance.World=new World();
  Check(Total()==0,"different world does not expose old trace");
  RebirthPurgeFeedbackTrace.Record("discovery-audio","new","snow",null);
  Check(Total()==1&&RebirthPurgeFeedbackTrace.Observe()["events"][0]["value"].Value<string>()==null,"new world trace and null payload");
  GameManager.Instance.World=null;RebirthPurgeFeedbackTrace.Record("must ignore",null,null,null);
  Check(Total()==0,"menu does not expose trace");
  var field=typeof(RebirthPurgeFeedbackTrace).GetField("owner",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
  Check(field.FieldType==typeof(WeakReference<World>),"world custody is weak");
  Console.WriteLine($"PASS {checks} actual production feedback trace invariants");
 }
}

