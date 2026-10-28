using System.Text;
using System.Xml;
using System.Xml.Linq;
// AUTHORED_NOT_RUN. Actual complete receiver AND real fragment assembler.
// ACTUAL_FRAGMENTS
internal sealed class EntityPlayer {internal World world;internal string Creation="creation";}
internal sealed class World {internal bool Remote;internal EntityPlayer Actor,Primary;internal bool IsRemote()=>Remote;internal EntityPlayer GetEntity(int id)=>id==1?Actor:null;internal EntityPlayer GetPrimaryPlayer()=>Primary;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class ClientInfo {internal bool loginDone=true,disconnecting;internal string Owner="account";}
internal sealed class Clients {internal ClientInfo Registered;internal ClientInfo ForEntityId(int id)=>id==1?Registered:null;}
internal sealed class ConnectionManager {internal bool IsServer;internal Clients Clients=new Clients();internal object[] Transports=new[]{new object()};internal object[] GetConnectionToServer()=>Transports;}
internal static class SingletonMonoBehaviour<T> where T:new() {internal static T Instance=new T();}
internal sealed class NetPackage {internal ClientInfo Sender;internal bool EntityValid=true;internal bool ValidEntityIdForSender(int id)=>EntityValid&&id==1;}
internal sealed class RebirthStablePlayerIdentity {
 internal string StorageKey;internal static bool Allow=true;internal static string Local="account";
 internal static bool TryFromClientInfo(ClientInfo peer,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{StorageKey=peer?.Owner};return Allow&&peer?.Owner!=null;}
 internal static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity{StorageKey=Local};return Allow&&Local!=null;}
}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer player)=>player?.Creation;}
internal static class RebirthSurvivorRequestScope {internal static bool TryNormalize(string value,out string creation){creation=value;return !string.IsNullOrEmpty(value);}}
namespace UnityEngine {internal static class Time {internal static float realtimeSinceStartup=10;}}
internal static class RebirthWorkstationCompletionPipeline {
 private static readonly Dictionary<object,string> Tokens=new Dictionary<object,string>();
 private static string NativeScope(object native){if(!Tokens.TryGetValue(native,out var token)){token=(Tokens.Count+1).ToString();Tokens.Add(native,token);}return token;}
 internal static int Calls;internal static string Kind;internal static EntityPlayer Recipient;internal static Guid Received;
 private static void Routed(EntityPlayer player,Guid operation,XElement body){Calls++;Kind=body.Name.ToString();Recipient=player;Received=operation;}
 private static void PrepareServer(EntityPlayer p,ClientInfo c,string account,string creation,Guid op,XElement body)=>Routed(p,op,body);
 private static void CompleteServer(EntityPlayer p,string account,string creation,Guid op,XElement body)=>Routed(p,op,body);
 private static void ReceiveNativeViewer(EntityPlayer p,ClientInfo c,string account,string creation,Guid op,XElement body)=>Routed(p,op,body);
 private static void QueryRemoteNativeOwner(EntityPlayer p,ClientInfo c,string account,string creation,Guid op,XElement body)=>Routed(p,op,body);
 private static void QuerySameOwnerTerminal(EntityPlayer p,ClientInfo c,string account,string creation,Guid op,XElement body)=>Routed(p,op,body);
 private static void QueryCachedPrimarySettled(EntityPlayer p,Guid op,XElement body)=>Routed(p,op,body);
 private static void PrepareCachedPhysical(EntityPlayer p,ClientInfo c,Guid op,XElement body)=>Routed(p,op,body);
 private static void ReceiveCachedUnusedClose(EntityPlayer p,ClientInfo c,Guid op,XElement body)=>Routed(p,op,body);
 private static void ReceiveCachedViewer(EntityPlayer p,ClientInfo c,string account,string creation,Guid op,XElement body)=>Routed(p,op,body);
 private static void ReceiveCachedOutputRestart(EntityPlayer p,ClientInfo c,string account,string creation,Guid op,XElement body)=>Routed(p,op,body);
 private static void ReceiveRemoteOutputRestart(EntityPlayer p,ClientInfo c,string account,string creation,Guid op,XElement body)=>Routed(p,op,body);
 private static void AcceptAck(EntityPlayer p,Guid op,XElement body)=>Routed(p,op,body);
// ACTUAL_RECEIVE
}
internal static class Program {
 private static World world;private static NetPackage packet;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static void Fresh(bool server){
  world=new World{Remote=!server};world.Actor=world.Primary=new EntityPlayer{world=world};GameManager.Instance.World=world;ThreadManager.Main=true;
  var peer=new ClientInfo();packet=new NetPackage{Sender=peer};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=server};SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Registered=peer;
  RebirthStablePlayerIdentity.Allow=true;RebirthStablePlayerIdentity.Local="account";RebirthRemoteResourceFragments.ClearTransient();RebirthWorkstationCompletionPipeline.Calls=0;
 }
 private static void Part(bool server,Guid operation,RebirthRemoteResourceFragments.Body body,int index)=>RebirthWorkstationCompletionPipeline.Receive(server,packet,world,1,operation,body.Hash,body.Length,body.Count,index,body.Part(index));
 private static Guid Send(bool server,string text){var body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes(text));var operation=Guid.NewGuid();for(int i=0;i<body.Count;i++)Part(server,operation,body,i);return operation;}
 public static void Main(){
  foreach(string kind in new[]{"prepare","complete","nativeViewer","nativeOwnerQuery","nativeSameOwnerQuery","cachedPrimaryQuery","cachedPhysicalPrepare","cachedUnusedClose","cachedViewer","cachedOutputFinalQuery","cachedOutputRestart","remoteOutputRestart"}){
   Fresh(true);var operation=Send(true,"<"+kind+" />");Need(RebirthWorkstationCompletionPipeline.Calls==1&&RebirthWorkstationCompletionPipeline.Kind==kind&&ReferenceEquals(RebirthWorkstationCompletionPipeline.Recipient,world.Actor)&&RebirthWorkstationCompletionPipeline.Received==operation,"server route changed original "+kind);
  }
  Fresh(false);Send(false,"<ack />");Need(RebirthWorkstationCompletionPipeline.Calls==1,"authenticated ACK route missing");
  for(int variant=0;variant<16;variant++){
   Fresh(true);var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
   switch(variant){case 0:ThreadManager.Main=false;break;case 1:GameManager.Instance.World=new World();break;case 2:world.Remote=true;break;case 3:SingletonMonoBehaviour<ConnectionManager>.Instance=null;break;case 4:connection.IsServer=false;break;case 5:packet.Sender=null;break;case 6:packet.EntityValid=false;break;case 7:connection.Clients.Registered=new ClientInfo();break;case 8:packet.Sender.loginDone=false;break;case 9:packet.Sender.disconnecting=true;break;case 10:world.Actor=null;break;case 11:world.Actor.world=new World();break;case 12:RebirthStablePlayerIdentity.Allow=false;break;case 13:world.Actor.Creation=null;break;case 14:packet=null;break;case 15:connection.Clients=null;break;}
   Send(true,"<complete />");Need(RebirthWorkstationCompletionPipeline.Calls==0,"unauthenticated server route "+variant);
  }
  for(int variant=0;variant<12;variant++){
   Fresh(false);var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
   switch(variant){case 0:ThreadManager.Main=false;break;case 1:GameManager.Instance.World=new World();break;case 2:world.Remote=false;break;case 3:SingletonMonoBehaviour<ConnectionManager>.Instance=null;break;case 4:connection.IsServer=true;break;case 5:connection.Transports=null;break;case 6:connection.Transports=new object[0];break;case 7:connection.Transports=new object[]{null};break;case 8:world.Primary=new EntityPlayer{world=world};break;case 9:world.Actor.world=new World();break;case 10:RebirthStablePlayerIdentity.Allow=false;break;case 11:world.Actor.Creation=null;break;}
   Send(false,"<ack />");Need(RebirthWorkstationCompletionPipeline.Calls==0,"unauthenticated ACK route "+variant);
  }
  foreach(string malformed in new[]{"<complete>","<!DOCTYPE complete [<!ENTITY denied 'x'>]><complete>&denied;</complete>","<unknown />","<ack />"}){Fresh(true);Send(true,malformed);Need(RebirthWorkstationCompletionPipeline.Calls==0,"malformed/foreign route accepted");}
  Fresh(false);Send(false,"<complete />");Need(RebirthWorkstationCompletionPipeline.Calls==0,"server command routed on client");
  Fresh(true);var body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes("<complete>"+new string('x',10000)+"</complete>"));var original=Guid.NewGuid();
  Part(true,original,body,0);Need(RebirthWorkstationCompletionPipeline.Calls==0,"partial original routed");
  var first=packet.Sender;var second=new ClientInfo();packet.Sender=second;SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Registered=second;Part(true,original,body,1);Need(RebirthWorkstationCompletionPipeline.Calls==0,"different native peer completed old fragments");
  packet.Sender=first;SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Registered=first;Part(true,original,body,1);Need(RebirthWorkstationCompletionPipeline.Calls==1,"exact native peer could not resume original fragments");
  Fresh(true);original=Guid.NewGuid();Part(true,original,body,0);packet.Sender.Owner="other-account";Part(true,original,body,1);Need(RebirthWorkstationCompletionPipeline.Calls==0,"account replacement completed old fragments");packet.Sender.Owner="account";Part(true,original,body,1);Need(RebirthWorkstationCompletionPipeline.Calls==1,"same account/native peer original did not resume");
  Console.WriteLine("PASS actual MP015 receive + actual fragments; native peers/domain handlers/topology remain explicit adapters, NOT RUN");
 }
}
