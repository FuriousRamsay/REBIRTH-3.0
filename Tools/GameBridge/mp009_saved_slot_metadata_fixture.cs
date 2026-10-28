using System;using System.Linq;
// Disclosed adapters: native checkpoint authority, owner identity, journal I/O.
// This fixture cannot establish an actual native setter/save event.
class PlayerDataFile{}
class RebirthCamperTransferJournal
{internal enum Phase{Prepared,Uncertain,Applied,Rejected}internal sealed class Record{internal Guid Operation,Vehicle;internal string OwnerCreation;internal int Revision;internal byte[] Before,After;internal Phase State;}}
static class RebirthRemoteResourcePlacedStationOriginal
{
 internal sealed class CancellationOwnerProgress{internal Guid Source;internal string Creation;internal bool Checkpoint;}
 internal sealed class CancellationOwnerStep{internal Guid Key;internal byte[] Command;}
 internal sealed class Journal
 {
  internal RebirthCamperTransferJournal.Record Value;internal int Advances;
  internal RebirthCamperTransferJournal.Record Read(Guid key)=>Value;
  internal RebirthCamperTransferJournal.Record Advance(Guid key,RebirthCamperTransferJournal.Phase before,RebirthCamperTransferJournal.Phase after)
  {if(Value.Operation!=key||Value.State!=before)throw new Exception("Wrong metadata attempt");Advances++;Value.State=after;return Value;}
 }
 internal static Journal Store=new Journal();
 private static bool OwnerProgressExact(CancellationOwnerProgress p,RebirthCamperTransferJournal.Record r,Guid key,byte[] bytes,RebirthCamperTransferJournal.Phase state)
 =>r!=null&&r.Operation==key&&r.Vehicle==p.Source&&r.OwnerCreation==p.Creation&&r.Revision==0&&r.State==state&&r.Before.SequenceEqual(bytes)&&r.After.SequenceEqual(bytes);
 private static Journal OwnerProgressJournal(CancellationOwnerProgress p,Guid key,string purpose,int size)
 {if(purpose!="OwnerSlot")throw new Exception("Wrong journal purpose");return Store;}
 private static bool ReadRestartCheckpoint(CancellationOwnerProgress p,CancellationOwnerStep step,out PlayerDataFile data)
 {data=p.Checkpoint?new PlayerDataFile():null;return data!=null;}
 // ACTUAL_METHOD
 internal static bool Observe(CancellationOwnerProgress p,CancellationOwnerStep step,RebirthCamperTransferJournal.Record r)=>ReadSavedRestartOwnerStep(p,step,r,out _);
}
static class Program
{
 static int assertions;static RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerProgress progress;static RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerStep step;
 static RebirthCamperTransferJournal.Record Record(RebirthCamperTransferJournal.Phase state)=>new RebirthCamperTransferJournal.Record{Operation=step.Key,Vehicle=progress.Source,OwnerCreation=progress.Creation,Before=step.Command.ToArray(),After=step.Command.ToArray(),State=state};
 static void Reset(RebirthCamperTransferJournal.Phase state,bool checkpoint)
 {progress=new RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerProgress{Source=Guid.NewGuid(),Creation="original",Checkpoint=checkpoint};step=new RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerStep{Key=Guid.NewGuid(),Command=new byte[]{1,2,3}};RebirthRemoteResourcePlacedStationOriginal.Store=new RebirthRemoteResourcePlacedStationOriginal.Journal{Value=Record(state)};}
 static void Require(bool b,string message){assertions++;if(!b)throw new Exception(message);}
 static bool Observe()=>RebirthRemoteResourcePlacedStationOriginal.Observe(progress,step,RebirthRemoteResourcePlacedStationOriginal.Store.Value);
 static void Main()
 {
  Reset(RebirthCamperTransferJournal.Phase.Uncertain,true);Require(Observe()&&RebirthRemoteResourcePlacedStationOriginal.Store.Advances==1,"Positive actual-native checkpoint completes only original slot metadata");Require(Observe()&&RebirthRemoteResourcePlacedStationOriginal.Store.Advances==1,"Repeated metadata observation has no new transition");
  Reset(RebirthCamperTransferJournal.Phase.Uncertain,false);Require(!Observe()&&RebirthRemoteResourcePlacedStationOriginal.Store.Advances==0,"Unknown native checkpoint cannot complete attempted original");
  Reset(RebirthCamperTransferJournal.Phase.Prepared,true);Require(!Observe()&&RebirthRemoteResourcePlacedStationOriginal.Store.Advances==0,"Checkpoint cannot manufacture an entered slot");
  Reset(RebirthCamperTransferJournal.Phase.Applied,true);Require(Observe()&&RebirthRemoteResourcePlacedStationOriginal.Store.Advances==0,"Existing applied original is read-only");
  Reset(RebirthCamperTransferJournal.Phase.Applied,false);Require(!Observe(),"Even applied metadata still requires full native checkpoint for cold prefix");
  Reset(RebirthCamperTransferJournal.Phase.Uncertain,true);RebirthRemoteResourcePlacedStationOriginal.Store.Value.Vehicle=Guid.NewGuid();Require(!Observe(),"Foreign source cannot reuse checkpoint");
  Reset(RebirthCamperTransferJournal.Phase.Uncertain,true);RebirthRemoteResourcePlacedStationOriginal.Store.Value.After[0]=99;Require(!Observe(),"Changed full original command cannot advance");
  Reset(RebirthCamperTransferJournal.Phase.Uncertain,true);var prior=Record(RebirthCamperTransferJournal.Phase.Uncertain);RebirthRemoteResourcePlacedStationOriginal.Store.Value.State=RebirthCamperTransferJournal.Phase.Rejected;Require(!RebirthRemoteResourcePlacedStationOriginal.Observe(progress,step,prior),"Changed original phase refuses before transition");
  Console.WriteLine("Saved original slot metadata: "+assertions+" assertions passed; native checkpoints remain separate evidence.");
 }
}
