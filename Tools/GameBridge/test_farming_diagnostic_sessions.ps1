$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/AdvancedFarming/Debug/AdvancedFarmingCropGrowthDiagnosticService.cs')
$a=$source.IndexOf('    public static string Start(');$b=$source.IndexOf('    public static void Pump(World world)',$a)
$k=$source.IndexOf('    private static string MakeSessionKey(');$e=$source.IndexOf("`n    }",$k)
if($a -lt 0 -or $b -le $a -or $k -lt 0 -or $e -le $k){throw 'Production session methods missing'}
$methods=$source.Substring($a,$b-$a)+$source.Substring($k,$e-$k+6)
$fixture=@'
using System;using System.Collections.Generic;
public class EntityPlayer{public bool Dead;public bool IsDead()=>Dead;}
public class World{public readonly Dictionary<int,EntityPlayer> Players=new Dictionary<int,EntityPlayer>();public object GetEntity(int id){if(!Players.ContainsKey(id))Players[id]=new EntityPlayer();return Players[id];}public bool IsRemote()=>false;}
public struct Vector3i{}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
class GameManager{public static GameManager Instance=new GameManager();public World World=new World();}
static class Time{public static float realtimeSinceStartup=10;}
static class Log{public static void Out(string text){}}
static class AdvancedFarmingRuntimePolicy{public static bool Enabled=true;public static int MinimumNaturalSunlight=1,PolicyVersion=1;public static int GetEffectiveGrowthSeconds()=>1;}
public static class DiagnosticSessionChecks{
class Session{public World World;public EntityPlayer Requester;public string Id,Role;public int RequestingEntityId,Radius,DurationSeconds,IntervalSeconds,SampleIndex;public float StartedRealtime,NextSampleRealtime;public Vector3i Center;public Vector3i[] CropPositions;}
static readonly Dictionary<string,Session> s_sessions=new Dictionary<string,Session>();
static readonly Dictionary<string,float> s_lastStarts=new Dictionary<string,float>();
static readonly List<string> s_finishedSessionIds=new List<string>();
public static long AdmissionRevision{get;private set;}
static int ClampRadius(int v)=>v;static int ClampDurationSeconds(int v)=>v;static int ClampIntervalSeconds(int v)=>v;
static Vector3i[] SanitizePositions(Vector3i p,int radius,Vector3i[] values)=>values??new Vector3i[0];
static string BuildSessionId(int owner)=>"generated-"+owner;
static void LogSample(World world,Session session,float elapsed,bool final){}
static bool ShouldNotifyLocalPlayer(string role)=>false;static void NotifyToolbelt(string text){}
// METHODS
static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
static void Begin(int owner,string label="same",string role="SERVER"){Start(GameManager.Instance.World,label,role,owner,new Vector3i(),1,20,5,new Vector3i[0]);}
public static void Run(){
 Begin(1);Begin(2);Check(IsActive("same","SERVER",1)&&IsActive("same","SERVER",2)&&s_sessions.Count==2,"cross-owner label collision");
 long revision=AdmissionRevision;Begin(1);Check(AdmissionRevision==revision&&s_sessions.Count==2,"cooldown admitted");
 Begin(1,"same","HOST");Check(IsActive("same","HOST",1)&&IsActive("same","SERVER",1),"paired role collision");
 revision=AdmissionRevision;Begin(3,"unsafe|label");Begin(3,new string('x',129));Check(AdmissionRevision==revision,"invalid label admitted");
 ThreadManager.Main=false;Begin(4);ThreadManager.Main=true;Check(AdmissionRevision==revision,"worker admission");
 Time.realtimeSinceStartup+=2;Begin(1,"new");Check(IsActive("new","SERVER",1)&&!IsActive("same","SERVER",1)&&IsActive("same","SERVER",2),"replacement removed foreign session");
 GameManager.Instance.World.Players[1]=new EntityPlayer();Check(!IsActive("new","SERVER",1),"replacement requester inherited session");
 GameManager.Instance.World.Players[2].Dead=true;Check(!IsActive("same","SERVER",2),"dead requester retained session");
 Console.WriteLine("PASS actual diagnostic admission: requester/role keys, cooldown, labels, thread, replacement. Native sampling/packet NOT covered.");}
}
'@
Add-Type -TypeDefinition $fixture.Replace('// METHODS',$methods)
[DiagnosticSessionChecks]::Run()
