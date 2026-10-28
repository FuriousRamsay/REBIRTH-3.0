// AUTHORED_NOT_RUN. Production loaded-receipt finisher, receipt identity and
// complete binary journal; native loaded After/owner and runtime are adapters.
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
static class RebirthCamperTransferHistoryScope{
 internal sealed class Index{internal RebirthCamperTransferJournal.Record FindPending(Guid id)=>null;internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid id,string owner)=>new RebirthCamperTransferJournal.Record[0];internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid id,string owner)=>new RebirthCamperTransferJournal.Record[0];}
 internal static bool TryGet(RebirthCamperTransferJournal journal,out Index index){index=null;return false;}
}
static class GameIO{internal static string Root;internal static string GetSaveGameDir()=>Root;}
class ItemGrid{internal object[] items=new object[0];}class Bag{internal ItemGrid ItemGrid=new ItemGrid();}class Vehicle{internal Bag bag=new Bag();}class Station{internal Vehicle BoundVehicle=new Vehicle();}
class RebirthCamperModuleRuntime{internal Guid Identity;internal object Storage=new object(),Stations=new object();internal bool TransferPending=true;internal int Clears,Saves;internal void SetTransferPending(bool value){TransferPending=value;if(!value)Clears++;}internal void Persist(bool forced,int reason){Saves++;}}
static class RebirthCamperTransferSnapshot{internal class Snapshot{internal object Cursor=new object();}internal static Snapshot Restore(Guid vehicle,byte[] image)=>new Snapshot();}
static class RebirthCamperTransferRecovery{internal static bool Allowed=true;internal static bool CanFinish(RebirthCamperTransferJournal.Record receipt,object storage,object stations,object[] items,object cursor,bool ownerSaved,out string error){error="";return Allowed;}}
static class RebirthProductiveEarnedMobileOrigin{internal static byte[] Digest(byte[] bytes){using(var sha=SHA256.Create())return sha.ComputeHash(bytes);}}
internal static partial class RebirthProductiveEarnedCoordinator{
 internal class MobileRemoteTools{internal Guid Operation;internal string Account="owner",Creation="creation";internal byte[] TransferBefore,TransferAfter;internal bool Live=true;internal bool Current()=>Live;}
 internal class Terminal{internal bool Live=true;internal Station Station=new Station();internal List<MobileRemoteTools> RemoteTools=new List<MobileRemoteTools>();internal bool Current()=>Live;}
 internal class Attempt{internal Terminal MobileTerminal=new Terminal();}
 static bool Observed=true;static bool EnsureMobileTerminalObservation(Terminal terminal)=>Observed;
 static Attempt original;static MobileRemoteTools source;static RebirthCamperModuleRuntime runtime;static RebirthCamperTransferJournal journal;static string root;static int checks;
 static void Seed(){
  root=Path.Combine(Path.GetTempPath(),"rebirth-mobile-loaded-receipt-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);GameIO.Root=root;roots.Add(root);Observed=RebirthCamperTransferRecovery.Allowed=true;
  runtime=new RebirthCamperModuleRuntime{Identity=Guid.NewGuid()};original=new Attempt();source=new MobileRemoteTools{Operation=Guid.NewGuid(),TransferBefore=RebirthProductiveEarnedMobileOrigin.Digest(new byte[]{1}),TransferAfter=RebirthProductiveEarnedMobileOrigin.Digest(new byte[]{2})};original.MobileTerminal.RemoteTools.Add(source);
  journal=new RebirthCamperTransferJournal(Path.Combine(root,"REBIRTH","CamperTransfers"));journal.Prepare(source.Operation,runtime.Identity,"remote:owner:creation",1,new byte[]{1},new byte[]{2});journal.Advance(source.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
 }
 static List<string> roots=new List<string>();static bool Finish()=>FinishLoadedMobileToolReceipt(original,runtime);static void Need(bool value){if(!value)throw new Exception("mobile loaded receipt "+checks);checks++;}
 static bool Blocked(){try{Finish();return false;}catch(InvalidOperationException){return true;}}
 public static void Main(){try{
  Seed();Need(Finish()&&journal.Read(source.Operation).State==RebirthCamperTransferJournal.Phase.Applied&&!runtime.TransferPending&&runtime.Clears==1&&runtime.Saves==1);Need(!Finish()&&runtime.Clears==1&&runtime.Saves==1);
  Seed();Observed=false;Need(!Finish()&&runtime.TransferPending&&runtime.Clears==0);
  Seed();original.MobileTerminal.Live=false;Need(!Finish()&&runtime.TransferPending);
  Seed();source.Live=false;Need(!Finish()&&runtime.TransferPending&&journal.Read(source.Operation).State==RebirthCamperTransferJournal.Phase.Uncertain);
  Seed();runtime.Identity=Guid.NewGuid();Need(!Finish()&&runtime.TransferPending);
  Seed();source.Account="foreign";Need(!Finish()&&runtime.TransferPending);
  Seed();source.TransferBefore=new byte[]{3};Need(!Finish()&&runtime.TransferPending);
  Seed();RebirthCamperTransferRecovery.Allowed=false;Need(!Finish()&&runtime.TransferPending);
  Seed();journal.Prepare(Guid.NewGuid(),runtime.Identity,"another-owner",2,new byte[]{3},new byte[]{4});Need(Blocked()&&runtime.TransferPending&&runtime.Clears==0&&runtime.Saves==0);Need(journal.Read(source.Operation).State==RebirthCamperTransferJournal.Phase.Applied);
  Seed();bool injected=false;Guid other=Guid.NewGuid();Action<string,RebirthCamperTransferJournal.Record,bool> callback=(directory,record,published)=>{if(published&&!injected&&record.Operation==source.Operation&&record.State==RebirthCamperTransferJournal.Phase.Applied){injected=true;new RebirthCamperTransferJournal(directory).Prepare(other,runtime.Identity,"another-owner",2,new byte[]{3},new byte[]{4});}};RebirthCamperTransferJournal.PublicationFinished+=callback;try{Need(Blocked()&&injected&&runtime.TransferPending&&runtime.Clears==0&&journal.Read(other).State==RebirthCamperTransferJournal.Phase.Prepared);}finally{RebirthCamperTransferJournal.PublicationFinished-=callback;}
  Seed();injected=false;other=Guid.NewGuid();callback=(directory,record,published)=>{if(published&&!injected&&record.Operation==source.Operation&&record.State==RebirthCamperTransferJournal.Phase.Applied){injected=true;new RebirthCamperTransferJournal(directory).Prepare(other,Guid.NewGuid(),"another-owner",2,new byte[]{3},new byte[]{4});}};RebirthCamperTransferJournal.PublicationFinished+=callback;try{Need(Finish()&&injected&&!runtime.TransferPending&&runtime.Clears==1);}finally{RebirthCamperTransferJournal.PublicationFinished-=callback;}
  Seed();original.MobileTerminal.RemoteTools.Clear();Need(!Finish()&&runtime.TransferPending&&runtime.Clears==0);
  Seed();source.Operation=Guid.NewGuid();Need(!Finish()&&runtime.TransferPending&&runtime.Clears==0);
  Console.WriteLine("PASS "+checks+" production mobile loaded receipt/journal assertions; native owner/vehicle adapters excluded");
 }finally{foreach(string directory in roots){if(Path.GetDirectoryName(directory)!=Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)||!Path.GetFileName(directory).StartsWith("rebirth-mobile-loaded-receipt-"))throw new InvalidDataException();Directory.Delete(directory,true);}}}
 // ACTUAL_LOADED_RECEIPT_FINISHER
}
// ACTUAL_MOBILE_RECEIPT_IDENTITY
// ACTUAL_BINARY_JOURNAL
