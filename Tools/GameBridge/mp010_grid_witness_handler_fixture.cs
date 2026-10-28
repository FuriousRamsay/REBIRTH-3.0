// AUTHORED_NOT_RUN. Shares explicit native peer adapters with no-payment fixture.
internal static class NetPackageManager {internal static T GetPackage<T>() where T:new()=>new T();}
internal sealed class NetPackageRebirthCookingGridOwnerWitnessResult:NetPackage {
 private Guid operation,nonce;private int phase;private string hash;private bool accepted;
 internal Guid Operation=>operation;internal Guid Nonce=>nonce;internal int Phase=>phase;internal string Hash=>hash;internal bool Accepted=>accepted;
 internal NetPackageRebirthCookingGridOwnerWitnessResult Setup(Guid op,Guid correlation,int stage,string digest,bool saved){operation=op;nonce=correlation;phase=stage;hash=digest;accepted=saved;return this;}
 // ACTUAL_RESULT_HANDLER
}
internal static class RebirthCookingGridCustody {
 internal static int Calls;internal static EntityPlayer Actor;internal static bool Accept;
 internal static int Replies;internal static void ReceiveWitness(World world,Guid op,Guid nonce,int phase,string hash,bool accepted){Replies++;if(phase!=2||hash!="digest:rich-owner"||!accepted)throw new Exception("result handler changed correlation");}
 internal static string Digest(string expected)=>"digest:"+expected;
 internal static bool VerifyWitness(EntityPlayer actor,Guid op,int phase,string world,string owner,string creation,int deaths,string expected,string handoff){
  Calls++;Actor=actor;if(op==Guid.Empty||phase!=2||world!="world"||owner!="owner"||creation!="creation"||deaths!=2||expected!="rich-owner"||handoff!="original-handoff")throw new Exception("handler changed witness evidence");return Accept;
 }
}
internal sealed class ActualWitnessHandler:NetPackage {
 internal int playerId=1,phase=2,deaths=2;internal object user=new object();internal Guid operation=Guid.NewGuid(),nonce=Guid.NewGuid();internal string worldId="world",owner="owner",creation="creation",expected="rich-owner",handoff="original-handoff";
// ACTUAL_HANDLER
}
internal static class Program {
 static void Need(bool b,string text){if(!b)throw new Exception(text);}
 public static void Main(){
  for(int variant=0;variant<18;variant++){
   ThreadManager.Main=true;
   var w=new World();var p=new EntityPlayer{world=w};w.Actor=p;GameManager.Instance.World=w;
   var peer=new Peer();var connection=new ConnectionManager();connection.Clients.Registered=peer;SingletonMonoBehaviour<ConnectionManager>.Instance=connection;
   var h=new ActualWitnessHandler{Sender=peer,Received=peer.netConnection[0]};
   foreach(bool accepted in new[]{false,true}){
    RebirthCookingGridCustody.Accept=accepted;RebirthCookingGridCustody.Calls=0;connection.Sent=0;h.ProcessPackage(w,GameManager.Instance);
    var result=connection.LastPacket as NetPackageRebirthCookingGridOwnerWitnessResult;
    Need(RebirthCookingGridCustody.Calls==1&&ReferenceEquals(RebirthCookingGridCustody.Actor,p)&&connection.Sent==1&&connection.Recipient==1&&result!=null&&result.Operation==h.operation&&result.Nonce==h.nonce&&result.Phase==h.phase&&result.Hash=="digest:rich-owner"&&result.Accepted==accepted,"witness reply lost exact correlation or refusal");
   }
   switch(variant){
    case 0:w.Remote=true;break;case 1:GameManager.Instance.World=new World();break;case 2:h.EntityValid=false;break;case 3:h.UserValid=false;break;
    case 4:h.Sender=null;break;case 5:connection.Clients.Registered=new Peer();break;case 6:connection.Clients=null;break;case 7:w.Actor=null;break;
    case 8:p.world=new World();break;case 9:p.Supported=false;break;case 10:peer.Owner="foreign";break;case 11:h.owner="foreign";break;
    case 12:ThreadManager.Main=false;break;case 13:h.Received=null;break;case 14:h.Received=new NativeConnection();break;case 15:peer.loginDone=false;break;case 16:peer.disconnecting=true;break;case 17:connection.IsServer=false;break;
   }
   RebirthCookingGridCustody.Calls=0;connection.Sent=0;h.ProcessPackage(w,GameManager.Instance);
   Need(RebirthCookingGridCustody.Calls==0&&connection.Sent==0,"invalid witness reached service or received reply "+variant);
  }
  for(int variant=0;variant<6;variant++){
   ThreadManager.Main=true;var world=new World{Remote=true};GameManager.Instance.World=world;var host=new NativeConnection();var manager=new ConnectionManager{IsServer=false,connectionToServer=new INetConnection[]{host}};SingletonMonoBehaviour<ConnectionManager>.Instance=manager;
   var result=new NetPackageRebirthCookingGridOwnerWitnessResult{Received=host}.Setup(Guid.NewGuid(),Guid.NewGuid(),2,"digest:rich-owner",true);
   RebirthCookingGridCustody.Replies=0;result.ProcessPackage(world,GameManager.Instance);Need(RebirthCookingGridCustody.Replies==1,"valid native witness response refused");
   if(variant==0)result.Received=null;if(variant==1)result.Received=new NativeConnection();if(variant==2)manager.IsConnected=false;if(variant==3)manager.IsServer=true;if(variant==4)ThreadManager.Main=false;
   RebirthCookingGridCustody.Replies=0;result.ProcessPackage(world,variant==5?new GameManager():GameManager.Instance);Need(RebirthCookingGridCustody.Replies==0,"foreign witness response reached receipt service");
  }
  Console.WriteLine("PASS actual cooking grid witness request/result peer/correlation; native saved owner/service/topology NOT RUN");
 }
}
