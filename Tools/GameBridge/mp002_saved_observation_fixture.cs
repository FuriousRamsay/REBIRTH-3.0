// AUTHORED_NOT_RUN. Actual primary observer and actual production v2 parser.
using System;using System.Collections.Generic;using System.IO;using System.Linq;
struct Vector3i{internal int x,y,z;internal Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
class RebirthFireState{internal Vector3i Position;internal ulong IgnitedWorldTime,NextProcessWorldTime;internal int IgniterEntityId,SourceBlockType;internal uint ScheduleToken;internal byte Flags;}
static class RebirthFireDefaults{internal const int MaxSnapshotFires=512,MaxSnapshotCooldowns=4096;}
class World{}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
static class RebirthFirePersistenceScope{internal static bool Current=true;internal static bool IsCurrentWorld(World world)=>Current;}
static class Subject
{
 const uint Magic=0x52424652;const ushort Version=2;static int lastSavedRevision=7;static bool loadFailed;static string SavePath;
 // ACTUAL_OBSERVE
 // ACTUAL_READ
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"CodexTest-mp002-saved-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);SavePath=Path.Combine(root,"RebirthFireManager.dat");
  using(var w=new BinaryWriter(File.Create(SavePath)))
  {w.Write(Magic);w.Write(Version);w.Write(1);w.Write(1);w.Write(2);w.Write(3);w.Write(10UL);w.Write(20UL);w.Write(7);w.Write(9);w.Write(1U);w.Write((byte)0);w.Write(1);w.Write(4);w.Write(5);w.Write(6);w.Write(100UL);}
  byte[] before=File.ReadAllBytes(SavePath);
  Need(TryObserveSaved(out int revision,out var fires,out var cooling,out string digest)&&revision==7&&fires.Count==1&&fires[0].IgniterEntityId==7&&cooling[new Vector3i(4,5,6)]==100,"actual v2 primary read");
  using(var hash=System.Security.Cryptography.SHA256.Create())Need(digest==Convert.ToBase64String(hash.ComputeHash(before)),"digest covers same actual primary");
  Need(before.SequenceEqual(File.ReadAllBytes(SavePath)),"observation never saves primary");
  File.WriteAllText(SavePath+".tmp","unknown original");Need(!TryObserveSaved(out _,out _,out _,out _)&&File.ReadAllText(SavePath+".tmp")=="unknown original","unknown staged primary refused and retained");
  loadFailed=true;Need(!TryObserveSaved(out _,out _,out _,out _),"failed recovery remains unqualified");loadFailed=false;
  RebirthFirePersistenceScope.Current=false;Need(!TryObserveSaved(out _,out _,out _,out _),"foreign native save scope refused");RebirthFirePersistenceScope.Current=true;
  lastSavedRevision=-1;Need(!TryObserveSaved(out _,out _,out _,out _),"unobserved native save cannot qualify");
  // Independent corrupt primary; no deletion or repair of original staged evidence.
  SavePath=Path.Combine(root,"corrupt.dat");File.WriteAllText(SavePath,"not native data");lastSavedRevision=7;
  Need(!TryObserveSaved(out _,out _,out _,out _)&&File.ReadAllText(SavePath)=="not native data","corrupt primary neither repaired nor accepted");
  Console.WriteLine("PASS actual saved-primary observation/parser; native autosave/reload NOT_RUN. Evidence: "+root);
 }
}
