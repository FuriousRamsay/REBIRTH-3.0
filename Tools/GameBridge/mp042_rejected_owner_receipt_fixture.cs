// Explicit persistence adapters for the actual rejected-original observation.
internal static class ThreadManager {internal static bool IsMainThread()=>true;}
internal sealed class World {internal State worldState=new State();internal sealed class State {internal string Guid;}internal bool IsRemote()=>false;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class GameIO {internal static string GetSaveGameDir()=>System.IO.Path.GetTempPath();}
internal static class Log {internal static void Warning(string message){}}
internal sealed class RebirthCamperTransferJournal {
 internal enum Phase {Prepared,Uncertain,Applied,Rejected}
 internal sealed class Record {internal Guid Operation,Vehicle;internal string OwnerCreation;internal long Revision;internal byte[] Before,After;internal Phase State;}
 internal static readonly System.Collections.Generic.Dictionary<Guid,Record> Records=new System.Collections.Generic.Dictionary<Guid,Record>();
 internal static Phase? FailNext;internal static int Publications;
 internal RebirthCamperTransferJournal(string path){}
 internal static Record ReadExisting(string path,Guid operation)=>Records.TryGetValue(operation,out var r)?r:null;
 internal Record Prepare(Guid op,Guid vehicle,string creation,long revision,byte[] before,byte[] after){Publications++;var r=new Record{Operation=op,Vehicle=vehicle,OwnerCreation=creation,Revision=revision,Before=(byte[])before.Clone(),After=(byte[])after.Clone(),State=Phase.Prepared};Records.Add(op,r);return r;}
 internal Record Advance(Guid op,Phase expected,Phase next){if(FailNext==next)throw new System.IO.IOException("injected publication failure");var r=Records[op];if(r.State!=expected)throw new Exception("wrong phase");Publications++;r.State=next;return r;}
}
internal static partial class RebirthCookingOwnerDelta {
 private static void RejectedCases(){
  for(int variant=0;variant<7;variant++){
   var plan=new Plan{Operation=Guid.NewGuid(),World=Guid.NewGuid().ToString("N"),Owner="owner",Creation="creation",Deaths=2,SourceHash=new string('a',64),BeforeBag=new ItemStack[0],AfterBag=new ItemStack[0],BeforeBelt=new ItemStack[0],AfterBelt=new ItemStack[0]};
   GameManager.Instance.World=new World();GameManager.Instance.World.worldState.Guid=plan.World;
   var image=System.Text.Encoding.UTF8.GetBytes(Encode(plan));var original=new RebirthCamperTransferJournal.Record{Operation=plan.Operation,Vehicle=Guid.NewGuid(),OwnerCreation=plan.Creation,Before=image,After=image,State=RebirthCamperTransferJournal.Phase.Rejected};
   RebirthCamperTransferJournal.Records.Clear();RebirthCamperTransferJournal.FailNext=null;RebirthCamperTransferJournal.Publications=0;
   RebirthGearNativePlayerFile.Available=true;RebirthGearNativePlayerFile.File=new NativeFile{deaths=2};RebirthCookingRemoteAdmissionBackend.Available=RebirthCookingRemoteAdmissionBackend.Present=true;RebirthCookingRemoteAdmissionBackend.Phase=4;
   if(variant==1)RebirthCookingRemoteAdmissionBackend.Phase=1;
   if(variant==2)RebirthCamperTransferJournal.FailNext=RebirthCamperTransferJournal.Phase.Applied;
   if(variant==3)original.After=new byte[]{1};
   if(variant==4){plan.Deaths=-1;original.Before=original.After=System.Text.Encoding.UTF8.GetBytes(Encode(plan));}
   if(variant==5)RebirthGearNativePlayerFile.File.deaths=3;
   if(variant==6)RebirthCookingRemoteAdmissionBackend.Available=false;
   bool result=ObserveRejected(new RebirthStablePlayerIdentity(),original);Need(result==(variant==0),"rejected acknowledgement qualification incorrect "+variant);
   if(variant==0){
    Need(RebirthCamperTransferJournal.Records[plan.Operation].State==RebirthCamperTransferJournal.Phase.Applied,"positive rejection observation not durable");
    RebirthGearNativePlayerFile.Available=false;RebirthGearNativePlayerFile.File.deaths=3;
    Need(ObserveRejected(new RebirthStablePlayerIdentity(),original),"recorded original acknowledgement forgotten after life changed");
    var ack=RebirthCamperTransferJournal.Records[plan.Operation];ack.Vehicle=Guid.NewGuid();Need(!ObserveRejected(new RebirthStablePlayerIdentity(),original),"foreign receipt accepted");
   }else if(variant==2){
    Need(RebirthCamperTransferJournal.Records[plan.Operation].State==RebirthCamperTransferJournal.Phase.Uncertain,"interrupted metadata receipt lost");
    RebirthCamperTransferJournal.FailNext=null;Need(ObserveRejected(new RebirthStablePlayerIdentity(),original),"metadata receipt could not resume without credit");
   }else Need(RebirthCamperTransferJournal.Publications==0,"unknown rejection published authority");
  }
 }
}
