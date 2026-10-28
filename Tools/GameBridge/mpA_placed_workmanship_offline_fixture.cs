using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;

// SERVICE_SOURCE
// COMMIT_SOURCE
// ASSERTION_SOURCE

// DISCLOSED DOUBLES: native world/connection/events/main-thread/time only;
// synthetic detached record admission; publication-failure boundary. Actual
// service lifecycle/parser/save/recovery bodies and durable success helper above.
public class WorldBase {public bool Remote;public bool IsRemote()=>Remote;}
public class World:WorldBase {}
public sealed class GameManager {public static GameManager Instance=new GameManager();public World World;}
public static class GameIO {public static string Save;public static string GetSaveGameDir()=>Save;}
public static class ThreadManager {public static bool IsMainThread()=>true;}
public sealed class ConnectionManager {public bool IsServer=true;}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public static class Log {public static void Warning(string s){Console.WriteLine("OBSERVED "+s);}}
namespace UnityEngine
{
 public static class Time {public static float realtimeSinceStartup;}
 public struct Vector3i {public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}public static bool operator==(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator!=(Vector3i a,Vector3i b)=>!(a==b);public override bool Equals(object o)=>o is Vector3i&&this==(Vector3i)o;public override int GetHashCode()=>x^y^z;}
}
namespace HarmonyLib {public sealed class Harmony {}}
public static class RebirthPlacedWorkmanshipIdentity {public static void Install(Harmony h){}}
public static class RebirthPlacedWorkmanshipPatchInstaller {public static int Install(Harmony h)=>0;}
public static class RebirthPlacedOriginal {public static void Install(Harmony h){}}
public static class ModEvents
{
 public struct SGameStartingData{} public struct SGameStartDoneData{} public struct SGameUpdateData{} public struct SWorldShuttingDownData{} public struct SGameShutdownData{}
 public delegate void ModEventHandlerDelegate<T>(ref T value);
 public sealed class Event<T>{public int Calls;public bool FailOnce;public void RegisterHandler(ModEventHandlerDelegate<T> h){if(FailOnce){FailOnce=false;throw new IOException("fixture registration refusal before effect");}Calls++;}}
 public static Event<SGameStartingData> GameStarting=new Event<SGameStartingData>();public static Event<SGameStartDoneData> GameStartDone=new Event<SGameStartDoneData>();public static Event<SGameUpdateData> GameUpdate=new Event<SGameUpdateData>();public static Event<SWorldShuttingDownData> WorldShuttingDown=new Event<SWorldShuttingDownData>();public static Event<SGameShutdownData> GameShutdown=new Event<SGameShutdownData>();
}
public static class RebirthDurableFileCommit
{
 public static bool Fail;
 public static bool TryPublish(string tmp,string path,out string error){if(Fail){error="fixture explicit pre-effect publication refusal";return false;}return ActualRebirthDurableFileCommit.TryPublish(tmp,path,out error);}
}
internal static class Program
{
 private static readonly Type S=typeof(RebirthPlacedWorkmanshipService);
 private static object Field(string name)=>S.GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
 private static void Event(string name,object data)=>S.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{data});
 private static void Need(bool b,string s){if(!b)throw new Exception(s);}
 private static string PathFor(string save)=>Path.Combine(save,"RebirthData","Survivor","PlacedWorkmanship.dat");
 private static string Hash(string path){using(var f=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","");}
 private static World Start(string save)
 {
  GameIO.Save=save;GameManager.Instance.World=null;Event("OnGameStarting",new ModEvents.SGameStartingData());Need(!(bool)Field("loaded"),"GameStarting bound before actual world");
  var world=new World();GameManager.Instance.World=world;Event("OnGameStartDone",new ModEvents.SGameStartDoneData());return world;
 }
 private static void Add(int x)
 {
  // Detached admission DOUBLE; no claim that gameplay placement/identity ran.
  var records=(Dictionary<string,RebirthPlacedWorkmanshipRecord>)Field("records");records.Add("0|"+x+",1,2",new RebirthPlacedWorkmanshipRecord{Position=new Vector3i(x,1,2),BlockName="fixtureBlock",CreatorStableId="fixtureCreator",Kind=RebirthPlacedProvenanceKind.ConstructionPlacement,SkillId="skill.construction",Workmanship=.5f,OriginalBaseMaxDurability=100});
  S.GetMethod("MarkDirty",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
 }
 private static void CleanReset(){Need(RebirthPlacedWorkmanshipService.TryResetAfterCoordinatedSave(),"Clean reset refused");}
 public static int Main(string[] args)
 {
  try
  {
   if(args.Length!=2||!Path.GetFileName(Path.GetFullPath(args[0])).StartsWith("rebirth-mp036-",StringComparison.Ordinal)||!File.Exists(Path.Combine(args[0],".fixture-only")))throw new Exception("Isolated generated fixture directory required");
   if(args[1]=="installer")
   {
    ModEvents.GameUpdate.FailOnce=true;bool failed=false;try{RebirthPlacedWorkmanshipService.Install(new Harmony());}catch(IOException){failed=true;}
    Need(failed&&ModEvents.GameStartDone.Calls==1&&ModEvents.GameStarting.Calls==1,"Fixture failed registration boundary not reached");
    RebirthPlacedWorkmanshipService.Install(new Harmony());RebirthPlacedWorkmanshipService.Install(new Harmony());
    Need(ModEvents.GameStartDone.Calls==1&&ModEvents.GameStarting.Calls==1&&ModEvents.GameUpdate.Calls==1&&ModEvents.WorldShuttingDown.Calls==1&&ModEvents.GameShutdown.Calls==1,"Retry duplicated an already registered handler");
    Console.WriteLine("PASS installer actual staged registration; event failure/native dependencies doubled");return 0;
   }
   string save=Path.Combine(args[0],args[1],"A");Directory.CreateDirectory(save);var world=Start(save);mpA_placed_workmanship_lifecycle_fixture.RequireReady(world);string path=PathFor(save);
   if(args[1]=="parser")
   {
    Add(1);RebirthPlacedWorkmanshipService.Save();mpA_placed_workmanship_lifecycle_fixture.RequireParser(path,true);
    byte[] bytes=File.ReadAllBytes(path);string malformed=Path.Combine(save,"parser-malformed.dat");
    int kindOffset,skillOffset;
    using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream)){stream.Position=26;reader.ReadString();kindOffset=(int)stream.Position;reader.ReadByte();for(int i=0;i<4;i++)reader.ReadString();skillOffset=(int)stream.Position;}
    foreach(var vector in new[]{"length","truncated","trailing","version","duplicate","kind","nan"})
    {
     byte[] b=(byte[])bytes.Clone();if(vector=="length"){for(int i=26;i<31;i++)b[i]=255;}else if(vector=="truncated")Array.Resize(ref b,27);else if(vector=="trailing")Array.Resize(ref b,b.Length+1);else if(vector=="version")b[4]=255;else if(vector=="kind")b[kindOffset]=255;else if(vector=="nan")Buffer.BlockCopy(BitConverter.GetBytes(float.NaN),0,b,skillOffset,4);else {b=new byte[bytes.Length+(bytes.Length-10)];Buffer.BlockCopy(bytes,0,b,0,bytes.Length);Buffer.BlockCopy(bytes,10,b,bytes.Length,bytes.Length-10);Buffer.BlockCopy(BitConverter.GetBytes(2),0,b,6,4);}
     File.WriteAllBytes(malformed,b);mpA_placed_workmanship_lifecycle_fixture.RequireParser(malformed,false);
    }
   }
   else if(args[1]=="cadence-shutdown")
   {
    Add(1);UnityEngine.Time.realtimeSinceStartup=19;Event("OnGameUpdate",new ModEvents.SGameUpdateData());Need(!File.Exists(path)&&(bool)Field("dirty"),"Periodic save ignored cadence");
    UnityEngine.Time.realtimeSinceStartup=21;Event("OnGameUpdate",new ModEvents.SGameUpdateData());Need(File.Exists(path)&&!(bool)Field("dirty"),"Periodic save did not publish");
    Add(2);Event("OnWorldShuttingDown",new ModEvents.SWorldShuttingDownData());Need(!(bool)Field("loaded")&&!(bool)Field("dirty"),"Shutdown failed to flush/reset clean store");string final=Hash(path);
    Event("OnGameShutdown",new ModEvents.SGameShutdownData());Need(Hash(path)==final,"Duplicate shutdown rewrote original");
   }
   else if(args[1]=="cold-tmp")
   {
    Add(1);RebirthPlacedWorkmanshipService.Save();string primary=Hash(path);File.WriteAllBytes(path+".tmp",new byte[]{1,2,3});string tmp=Hash(path+".tmp");CleanReset();world=Start(save);
    Need((bool)Field("loadFailed"),"Unknown cold tmp became writable");mpA_placed_workmanship_lifecycle_fixture.RequireUnknownRetained(world,path,primary,null,tmp);
    GameIO.Save=Path.Combine(args[0],args[1],"B");GameManager.Instance.World=new World();Event("OnGameStarting",new ModEvents.SGameStartingData());Event("OnGameStartDone",new ModEvents.SGameStartDoneData());
    mpA_placed_workmanship_lifecycle_fixture.RequireUnknownRetained(world,path,primary,null,tmp);Need(!File.Exists(PathFor(GameIO.Save)),"Unknown A wrote B");
   }
   else if(args[1]=="backup")
   {
    Add(1);RebirthPlacedWorkmanshipService.Save();Add(2);RebirthPlacedWorkmanshipService.Save();string backup=Hash(path+".bak");File.WriteAllBytes(path,new byte[]{7,8,9});string bad=Hash(path);CleanReset();world=Start(save);mpA_placed_workmanship_lifecycle_fixture.RequireReady(world);
    Need(Hash(path)==bad&&Hash(path+".bak")==backup,"Fallback replaced original inputs");Need(Hash(path+".retained."+bad.ToLowerInvariant())==bad&&Hash(path+".bak.retained."+backup.ToLowerInvariant())==backup,"Fallback did not preserve BOTH originals");
    Add(3);RebirthPlacedWorkmanshipService.Save();Need(Hash(path+".retained."+bad.ToLowerInvariant())==bad&&Hash(path+".bak.retained."+backup.ToLowerInvariant())==backup,"Later rotation destroyed retained originals");
   }
   else if(args[1]=="retry"||args[1]=="changed-retry"||args[1]=="cross-world")
   {
    Add(1);RebirthPlacedWorkmanshipService.Save();string primary=Hash(path);Add(2);RebirthDurableFileCommit.Fail=true;RebirthPlacedWorkmanshipService.Save();Need((bool)Field("dirty")&&Hash(path)==primary&&File.Exists(path+".tmp"),"Failed publish lost dirty/original");string tmp=Hash(path+".tmp");
    if(args[1]=="changed-retry")Add(3);
    if(args[1]=="cross-world")
    {
     string b=Path.Combine(args[0],args[1],"B");GameIO.Save=b;GameManager.Instance.World=new World();Event("OnGameStarting",new ModEvents.SGameStartingData());Event("OnGameStartDone",new ModEvents.SGameStartDoneData());mpA_placed_workmanship_lifecycle_fixture.RequireUnknownRetained(world,path,primary,null,tmp);Need(!File.Exists(PathFor(b)),"A dirty data wrote B");
    }
    RebirthDurableFileCommit.Fail=false;RebirthPlacedWorkmanshipService.Save();Need(!(bool)Field("dirty")&&!File.Exists(path+".tmp"),"Exact process retry failed");mpA_placed_workmanship_lifecycle_fixture.RequireParser(path,true);
    if(args[1]=="cross-world")
    {
     string settled=Hash(path);Directory.CreateDirectory(GameIO.Save);Event("OnGameStarting",new ModEvents.SGameStartingData());Event("OnGameStartDone",new ModEvents.SGameStartDoneData());mpA_placed_workmanship_lifecycle_fixture.RequireReady(GameManager.Instance.World);
     Need(((IDictionary)Field("records")).Count==0&&Hash(path)==settled,"Clean B inherited A records or rewrote A");
    }
    if(args[1]=="changed-retry")Need(Directory.GetFiles(Path.GetDirectoryName(path),"PlacedWorkmanship.dat.tmp.retained.*").Length==1,"Exact obsolete own temp not retained");
   }
   else throw new Exception("Unknown fixture case");
   Console.WriteLine("PASS "+args[1]+" actual extracted service methods; native/admission/failure boundary doubles disclosed");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
