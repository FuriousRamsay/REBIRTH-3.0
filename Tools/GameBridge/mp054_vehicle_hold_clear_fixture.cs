// AUTHORED_NOT_RUN. Actual runtime clear method, binary journal and live index.
// Native ItemValue metadata/sync, station clocks and World producers are adapters.
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class World{internal bool Remote;internal bool IsRemote()=>Remote;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class GameIO{internal static string Save;internal static string GetSaveGameDir()=>Save;}
class ItemValue{internal Dictionary<string,string> Data=new Dictionary<string,string>();internal bool TryGetMetadata(string key,out string value)=>Data.TryGetValue(key,out value);internal void SetMetadata(string key,string value){Data[key]=value;}}
class NativeVehicle{internal ItemValue Item=new ItemValue();internal ItemValue GetUpdatedItemValue()=>Item;}
class EntityVehicle{internal NativeVehicle vehicle=new NativeVehicle();internal int Syncs;internal void SendSyncData(int flags){Syncs++;}}
static class RebirthCamperCraftingStation{internal const int ModuleSlots=2;}
class Stations{internal class State{internal ulong lastTickTime=77;}internal class Slot{internal State State=new State();}internal Slot[] Slots={new Slot(),new Slot()};internal Slot GetSlot(int slot)=>Slots[slot];}
class RebirthCamperModuleRuntime{internal Guid Identity=Guid.NewGuid();internal bool TransferPending=true;internal Stations Stations=new Stations();internal EntityVehicle vehicle=new EntityVehicle();
 // ACTUAL_HOLD_CLEAR
}
// ACTUAL_BINARY_JOURNAL
// ACTUAL_HISTORY_SCOPE
// ACTUAL_PENDING_INDEX
static class Program{
 static List<string> roots=new List<string>();static RebirthCamperModuleRuntime runtime;static RebirthCamperTransferJournal journal;static string root,directory;static Guid operation;static int checks;
 static void Seed(){RebirthCamperTransferHistoryScope.Clear();GameManager.Instance.World=null;ThreadManager.Main=true;root=Path.Combine(Path.GetTempPath(),"rebirth-hold-clear-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);roots.Add(root);GameIO.Save=root;directory=Path.Combine(root,"REBIRTH","CamperTransfers");runtime=new RebirthCamperModuleRuntime();journal=null;operation=Guid.NewGuid();}
 static void Prepare(Guid? vin=null){journal=new RebirthCamperTransferJournal(directory);journal.Prepare(operation,vin??runtime.Identity,"owner",1,new byte[]{1},new byte[]{2});}
 static void Uncertain()=>journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
 static void Applied(){Uncertain();journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);}
 static bool Refuses(){try{runtime.SetTransferPending(false);return false;}catch(InvalidOperationException){return true;}catch(InvalidDataException){return true;}catch(IOException){return true;}catch(ArgumentException){return true;}}
 static void Need(bool condition){if(!condition)throw new Exception("vehicle hold clear "+checks);checks++;}
 public static void Main(){try{
  Seed();runtime.TransferPending=false;runtime.SetTransferPending(false);Need(!runtime.TransferPending&&!Directory.Exists(directory)&&runtime.vehicle.Syncs==1);
  Seed();Need(Refuses()&&runtime.TransferPending&&runtime.vehicle.Syncs==0&&runtime.Stations.Slots.All(value=>value.State.lastTickTime==77)&&!Directory.Exists(directory));
  Seed();Prepare();Need(Refuses()&&runtime.TransferPending&&runtime.vehicle.Syncs==0);Uncertain();Need(Refuses()&&runtime.TransferPending);
  AppliedAfterUncertain();runtime.SetTransferPending(false);Need(!runtime.TransferPending&&runtime.vehicle.Syncs==1&&runtime.Stations.Slots.All(value=>value.State.lastTickTime==0));
  Seed();Prepare();journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);runtime.SetTransferPending(false);Need(!runtime.TransferPending);
  Seed();Prepare(Guid.NewGuid());runtime.SetTransferPending(false);Need(!runtime.TransferPending);
  Seed();Prepare();Applied();journal.Prepare(Guid.NewGuid(),runtime.Identity,"other",2,new byte[]{3},new byte[]{4});Need(Refuses()&&runtime.TransferPending&&runtime.vehicle.Syncs==0);
  Seed();Prepare();Uncertain();bool injected=false;Action<string,RebirthCamperTransferJournal.Record,bool> callback=(path,record,published)=>{if(published&&!injected&&record.Operation==operation&&record.State==RebirthCamperTransferJournal.Phase.Applied){injected=true;new RebirthCamperTransferJournal(path).Prepare(Guid.NewGuid(),runtime.Identity,"other",2,new byte[]{3},new byte[]{4});}};RebirthCamperTransferJournal.PublicationFinished+=callback;try{AppliedAfterUncertain();Need(injected&&Refuses()&&runtime.TransferPending&&runtime.vehicle.Syncs==0);}finally{RebirthCamperTransferJournal.PublicationFinished-=callback;}
  Seed();Prepare();Uncertain();injected=false;callback=(path,record,published)=>{if(published&&!injected&&record.Operation==operation&&record.State==RebirthCamperTransferJournal.Phase.Applied){injected=true;new RebirthCamperTransferJournal(path).Prepare(Guid.NewGuid(),Guid.NewGuid(),"other",2,new byte[]{3},new byte[]{4});}};RebirthCamperTransferJournal.PublicationFinished+=callback;try{AppliedAfterUncertain();runtime.SetTransferPending(false);Need(injected&&!runtime.TransferPending);}finally{RebirthCamperTransferJournal.PublicationFinished-=callback;}
  Seed();Prepare();Applied();ThreadManager.Main=false;Need(Refuses()&&runtime.TransferPending&&runtime.vehicle.Syncs==0);
  Seed();GameIO.Save="relative-save";Need(Refuses()&&runtime.TransferPending);Seed();Prepare();Applied();runtime.Identity=Guid.Empty;Need(Refuses()&&runtime.TransferPending);
  Seed();Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"unknown.rcj"),"foreign");Need(Refuses()&&runtime.TransferPending);
  Seed();runtime.TransferPending=false;runtime.SetTransferPending(true);Need(runtime.TransferPending&&!Directory.Exists(directory)&&runtime.Stations.Slots.All(value=>value.State.lastTickTime==77));
  Seed();GameManager.Instance.World=new World();Prepare();Need(Refuses()&&runtime.TransferPending);Applied();runtime.SetTransferPending(false);Need(!runtime.TransferPending);
  Console.WriteLine("PASS "+checks+" actual runtime/journal/index hold-clear assertions; native producers adapted");
 }finally{RebirthCamperTransferHistoryScope.Clear();foreach(string path in roots){if(Path.GetDirectoryName(path)!=Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)||!Path.GetFileName(path).StartsWith("rebirth-hold-clear-"))throw new InvalidDataException();Directory.Delete(path,true);}}}
 static void AppliedAfterUncertain()=>journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
}
