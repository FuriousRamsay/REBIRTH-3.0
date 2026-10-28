using System;
using System.Linq;
// AUTHORED_NOT_RUN. Actual local journal retirement; native/parser adapters.
internal static class ThreadManager {internal static bool IsMainThread()=>true;}
internal sealed class World {internal State worldState=new State();internal sealed class State {internal string Guid="world";}internal EntityPlayerLocal Primary,Registered;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;internal object GetEntity(int id)=>Registered;}
internal sealed class EntityPlayerLocal {internal World world;internal string Creation="creation",Owner="owner";internal bool Dead;internal int entityId=1,Died=2;internal Buffs Buffs=new Buffs();internal bool IsDead()=>Dead;internal bool IsSpawned()=>true;}
internal static class GameIO {internal static string GetSaveGameDir()=>"fixture-scope";}
internal static class Log {internal static void Warning(string message){}}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayerLocal p)=>p.Creation;}
internal static class RebirthCookingRemoteAdmission {internal static string Receipt(Guid op)=>"rbCookingAdmission_"+op.ToString("N");}
internal sealed class Buffs {internal float Phase=4;internal float GetCustomVar(string key)=>Phase;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal sealed class RebirthStablePlayerIdentity {internal string CanonicalId;internal static bool TryResolveServerEntity(EntityPlayerLocal p,out RebirthStablePlayerIdentity i){i=new RebirthStablePlayerIdentity{CanonicalId=p.Owner};return true;}}
internal sealed class NativeFile {internal int deaths=2;}
internal static class RebirthGearNativePlayerFile {internal static bool Available=true;internal static NativeFile File=new NativeFile();internal static bool TryRead(RebirthStablePlayerIdentity i,out NativeFile saved){saved=File;return Available;}}
internal static class RebirthCookingRemoteAdmissionBackend {internal static bool Available=true,Present=true;internal static float Phase=4;internal static Action OnRead;internal static bool NativePhase(NativeFile f,string receipt,out float phase,out bool present){OnRead?.Invoke();phase=Phase;present=Present;return Available;}}
internal static class RebirthCamperTransferJournal {
 internal enum Phase {Prepared,Uncertain,Applied,Rejected}
 internal sealed class Record {internal Guid Operation=Guid.NewGuid(),Vehicle=Guid.NewGuid();internal string OwnerCreation="creation";internal long Revision;internal byte[] Before=new byte[]{1},After=new byte[]{1};internal Phase State;}
}
internal sealed class Journal {
 internal RebirthCamperTransferJournal.Record Record;internal int Reads,MissingAt,Publications;internal bool Suppress;internal Action OnAdvance;
 internal RebirthCamperTransferJournal.Record Read(Guid id){Reads++;return Reads==MissingAt?null:Record;}
 internal RebirthCamperTransferJournal.Record Advance(Guid id,RebirthCamperTransferJournal.Phase before,RebirthCamperTransferJournal.Phase after){Publications++;if(!Suppress)Record.State=after;OnAdvance?.Invoke();return Record;}
}
internal static class RebirthCookingGridCustody {
 private sealed class Intent {internal string NativeReceipt="receipt",Action="external",World="world",Creation="creation";internal int Deaths=2;}
 private static Intent intent;private static Journal journal;private static Journal Journal()=>journal;
 private static Intent Parse(RebirthCamperTransferJournal.Record r,object workspace,EntityPlayerLocal p)=>intent;
// ACTUAL_REJECT
// ACTUAL_COMPLETE
 private static void CompleteCases(){
  for(int variant=0;variant<22;variant++){
   var world=new World();var player=new EntityPlayerLocal{world=world};world.Primary=world.Registered=player;GameManager.Instance.World=world;
   intent=new Intent();journal=new Journal{Record=new RebirthCamperTransferJournal.Record()};var operation=journal.Record.Operation;intent.NativeReceipt="rbCookingGridReturn_"+operation.ToString("N");
   RebirthGearNativePlayerFile.Available=RebirthCookingRemoteAdmissionBackend.Available=RebirthCookingRemoteAdmissionBackend.Present=true;RebirthGearNativePlayerFile.File=new NativeFile();RebirthCookingRemoteAdmissionBackend.Phase=2;RebirthCookingRemoteAdmissionBackend.OnRead=null;
   switch(variant){
    case 1:RebirthCookingRemoteAdmissionBackend.Phase=3;break;case 2:RebirthCookingRemoteAdmissionBackend.Present=false;break;case 3:RebirthGearNativePlayerFile.Available=false;break;
    case 4:RebirthGearNativePlayerFile.File.deaths=1;break;case 5:intent.Deaths=1;break;case 6:RebirthCookingRemoteAdmissionBackend.Phase=1;break;case 7:RebirthCookingRemoteAdmissionBackend.Phase=4;break;
    case 8:journal.Suppress=true;break;case 9:journal.MissingAt=2;break;case 10:journal.Record.State=RebirthCamperTransferJournal.Phase.Rejected;break;
    case 11:intent.NativeReceipt="foreign";break;case 12:journal.Record.After=new byte[]{2};break;case 13:journal.Record.State=RebirthCamperTransferJournal.Phase.Applied;break;
    case 14:RebirthCookingRemoteAdmissionBackend.OnRead=()=>player.Died++;break;case 15:journal.Record.State=RebirthCamperTransferJournal.Phase.Uncertain;break;
    case 16:journal.OnAdvance=()=>player.Died++;break;case 17:RebirthCookingRemoteAdmissionBackend.OnRead=()=>world.worldState.Guid="replacement";break;
    case 18:RebirthCookingRemoteAdmissionBackend.OnRead=()=>player.Creation="replacement";break;case 19:RebirthCookingRemoteAdmissionBackend.OnRead=()=>player.Owner="replacement";break;
    case 20:intent.Action="foreign";break;case 21:world.Registered=new EntityPlayerLocal();break;
   }
   bool result=CompleteExternal(operation),expected=variant==0||variant==1||variant==13||variant==15;
   if(result!=expected)throw new Exception("unqualified grid completion accepted or valid completion lost "+variant);
   if(result&&(!CompleteExternal(operation)||journal.Record.State!=RebirthCamperTransferJournal.Phase.Applied))throw new Exception("grid completion terminal not idempotent");
   if(variant==9&&!CompleteExternal(operation))throw new Exception("post-publication grid completion observation could not resume");
   if(variant==16&&journal.Record.State!=RebirthCamperTransferJournal.Phase.Uncertain)throw new Exception("scope expiry did not retain intermediate grid metadata");
  }
 }
 internal static void Exercise(){
  for(int variant=0;variant<11;variant++){
   var w=new World();var p=new EntityPlayerLocal{world=w};w.Primary=w.Registered=p;GameManager.Instance.World=w;
   intent=new Intent();journal=new Journal{Record=new RebirthCamperTransferJournal.Record()};var operation=journal.Record.Operation;
   RebirthGearNativePlayerFile.Available=RebirthCookingRemoteAdmissionBackend.Available=RebirthCookingRemoteAdmissionBackend.Present=true;RebirthGearNativePlayerFile.File=new NativeFile();RebirthCookingRemoteAdmissionBackend.Phase=4;
   switch(variant){case 1:journal.Record.State=RebirthCamperTransferJournal.Phase.Uncertain;break;case 2:p.Buffs.Phase=1;break;case 3:RebirthGearNativePlayerFile.Available=false;break;
    case 4:RebirthGearNativePlayerFile.File.deaths=1;break;case 5:RebirthCookingRemoteAdmissionBackend.Available=false;break;case 6:RebirthCookingRemoteAdmissionBackend.Present=false;break;
    case 7:RebirthCookingRemoteAdmissionBackend.Phase=1;break;case 8:journal.Suppress=true;break;case 9:journal.MissingAt=2;break;case 10:p.Died=3;break;}
   bool result=RejectUnappliedExternal(operation,p);if(result!=(variant==0))throw new Exception("unqualified grid rejection completed "+variant);
   if(variant==0){RebirthGearNativePlayerFile.Available=false;if(!RejectUnappliedExternal(operation,p)||journal.Publications!=1)throw new Exception("terminal rejection replay lost");}
   if(variant==9&&!RejectUnappliedExternal(operation,p))throw new Exception("interrupted post-publication observation could not resume metadata");
   if(variant>0&&variant<8&&journal.Publications!=0)throw new Exception("unqualified rejection published metadata");
  }
  CompleteCases();
  Console.WriteLine("PASS actual local grid rejection/completion save/phase/post-publication checks; native parser/files/journal NOT RUN");
 }
}
internal static class Program {public static void Main()=>RebirthCookingGridCustody.Exercise();}
