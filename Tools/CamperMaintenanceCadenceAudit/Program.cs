using System;
using System.Linq;
using System.Collections.Generic;
class ModEvents { public struct SGameUpdateData {} }
static class Time { public static float realtimeSinceStartup; }
class EntityVehicle {}
class EntityPlayer {}
class TileEntityPowered {}
class Node { public object TileEntity=new TileEntityPowered(); public int Position=1; }
class Entities { public List<object> list=new(){new EntityVehicle()}; }
class World { public Entities Entities=new(); public bool IsRemote()=>false; }
class GameManager { public static GameManager Instance=new(); public World World=new(); }
static class ThreadManager { public static bool IsMainThread()=>true; }
static class RebirthCamperRadial { public static bool IsCamper(EntityVehicle v)=>true; }
enum RebirthCamperEnergyMode { Camp,Travel }
static class RebirthCamperEnergyRuntime {
 public static RebirthCamperEnergyMode Mode=RebirthCamperEnergyMode.Camp;
 public static bool TryObserveMode(EntityVehicle v,out RebirthCamperEnergyMode m,out long r,out float range){m=Mode;r=0;range=10;return true;}
 public static bool IsHeld(EntityVehicle v)=>false;
}
static class RebirthCamperRemoteAdmission { public static int NativeInteractionOwner(EntityVehicle v)=>-1; }
class PowerManager { public static bool HasInstance=true;public static PowerManager Instance=new();public Dictionary<int,Node> PowerItemDictionary=new(){{1,new Node()}}; }
partial class Audit {
 const float IntervalSeconds=60;
 static bool installed=true;
 class Run { public object Record; public float Next,NextOwnerProbe;public Queue<int> Targets=new(); }
 static Dictionary<EntityVehicle,Run> runs=new();
 static bool eligible;static int owners,ready,progress,prepared;
 static void Ready(EntityVehicle v){ready++;if(!runs.ContainsKey(v))runs[v]=new Run{Next=Time.realtimeSinceStartup+60};}
 static void Progress(Run r){progress++;}
 static EntityPlayer Owner(EntityVehicle v,out string creation,out string key){owners++;creation=key="owner";return eligible?new EntityPlayer():null;}
 static bool PoweredAdmission(EntityVehicle v,TileEntityPowered t,EntityPlayer p)=>true;
 static bool Prepare(Run r,EntityPlayer p,string c,string k,int pos){prepared++;return false;}
 static void Warn(Run r,string why){throw new Exception(why);}
 static Run Reset(){runs.Clear();owners=ready=progress=prepared=0;eligible=false;RebirthCamperEnergyRuntime.Mode=RebirthCamperEnergyMode.Camp;Tick(0);return runs.Values.Single();}
 static void Tick(float now){Time.realtimeSinceStartup=now;var d=new ModEvents.SGameUpdateData();Update(ref d);}
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){
  var r=Reset();for(int i=1;i<3600;i++)Tick(i/60f);Check(owners==0,"no owner probe before first scan");
  for(int i=3600;i<7200;i++)Tick(i/60f);Check(owners==60&&ready==7200,"steady ineligible owner: 60 probes over 3600 frames; Ready every frame");
  r=Reset();Tick(60);Tick(60.5f);Check(owners==1,"failed probe bounded");eligible=true;Tick(60.99f);Check(prepared==0,"new eligibility waits at most one second");Tick(61);Check(prepared==1&&r.Next==121,"new eligibility scans on retry without changing 60-second interval");Tick(120.99f);Check(owners==2,"idle after scan skips owner lookup");Tick(121);Check(prepared==2,"next scan exact deadline");
  r=Reset();Tick(60);r.Targets.Enqueue(1);eligible=true;Tick(60.1f);Check(prepared==1,"active queue bypasses owner retry deadline");
  r=Reset();Tick(60);r.Record=new object();Tick(60.1f);Check(progress==1&&owners==1,"pending receipt advances before owner retry");
  r=Reset();Tick(60);RebirthCamperEnergyRuntime.Mode=RebirthCamperEnergyMode.Travel;r.Targets.Enqueue(1);Tick(60.1f);Check(r.Targets.Count==0&&r.Next==120.1f,"travel clears queue and resets existing deadline");
  Console.WriteLine("All cadence boundaries passed; actual Update source extracted, external dependencies stubbed.");
 }
}
