using System;
// AUTHORED_NOT_RUN. Actual network entry point; native peer/service adapters.
internal sealed class World {internal bool Remote;internal WorldState worldState=new WorldState();internal EntityPlayer Actor;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Actor;}
internal sealed class WorldState {internal string Guid="world";}
internal sealed class EntityPlayer {internal World world;internal bool Dead,Supported=true;internal string Creation="creation",Owner="owner",CharacterOwner="owner";internal bool IsDead()=>Dead;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal interface INetConnection{bool IsDisconnected();}
internal sealed class NativeConnection:INetConnection{internal bool Disconnected;public bool IsDisconnected()=>Disconnected;}
internal static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class Peer {internal string Owner="owner";internal bool loginDone=true,disconnecting;internal INetConnection[] netConnection=new INetConnection[]{new NativeConnection()};}
internal sealed class Clients {internal Peer Registered;internal Peer ForEntityId(int id)=>Registered;}
internal sealed class ConnectionManager {internal bool IsServer=true,IsConnected=true;internal INetConnection[] connectionToServer;internal Clients Clients=new Clients();internal int Sent,Recipient;internal object LastPacket;internal void SendPackage(object packet,int _attachedToEntityId){Sent++;Recipient=_attachedToEntityId;LastPacket=packet;}}
internal static class SingletonMonoBehaviour<T> where T:new() {internal static T Instance=new T();}
internal static class RebirthRemoteResourceRepairProtocol {internal static bool SupportsServerActor(EntityPlayer p)=>p.Supported;}
internal static class RebirthOriginalActionScope {internal static bool TryNormalize(string s,out string value){value=s?.Trim();return !string.IsNullOrEmpty(value);}}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer p)=>p.Creation;}
internal sealed class Identity {internal string CanonicalId;}
internal static class RebirthWorldCharacterService {internal static bool TryGetIdentity(EntityPlayer p,out Identity i){i=new Identity{CanonicalId=p.CharacterOwner};return p.CharacterOwner!=null;}}
internal static class RebirthStablePlayerIdentity {
 internal static bool TryResolveServerEntity(EntityPlayer p,out Identity i){i=new Identity{CanonicalId=p.Owner};return p.Owner!=null;}
 internal static bool TryFromClientInfo(Peer p,out Identity i){i=new Identity{CanonicalId=p?.Owner};return p?.Owner!=null;}
}
internal static class RebirthRemoteResourceDurableOutcome {internal static int Calls;internal static EntityPlayer Actor;internal static Guid Operation;internal static string Binding;internal static bool AcknowledgeCookingNoPayment(EntityPlayer a,Guid op,string binding){Calls++;Actor=a;Operation=op;Binding=binding;return true;}}
internal class NetPackage {internal Peer Sender;internal INetConnection Received;internal int Channel;internal bool EntityValid=true,UserValid=true;protected bool ValidEntityIdForSender(int id)=>EntityValid;protected bool ValidUserIdForSender(object user)=>UserValid;public virtual void ProcessPackage(World world,GameManager callbacks){}}
// Exact-dequeue collector is composed separately in mp009_scope.
internal static class RebirthProductiveEarnedUiDeferredTransport{internal static bool TryGetReceivedConnection(NetPackage packet,out INetConnection connection){connection=packet.Received;return connection!=null;}}
internal sealed class ActualHandler:NetPackage {
 internal int playerId=1;internal object user=new object();internal Guid operation=Guid.NewGuid();internal string binding="binding",worldId="world",creation="creation";
// ACTUAL_HANDLER
}
internal static class Program {
 static void Need(bool b,string text){if(!b)throw new Exception(text);}
 public static void Main(){
  for(int variant=0;variant<26;variant++){
   ThreadManager.Main=true;
   var w=new World();var p=new EntityPlayer{world=w};w.Actor=p;GameManager.Instance.World=w;
   var peer=new Peer();SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Registered=peer;
   var h=new ActualHandler{Sender=peer,Received=peer.netConnection[0]};RebirthRemoteResourceDurableOutcome.Calls=0;
   h.ProcessPackage(w,GameManager.Instance);Need(RebirthRemoteResourceDurableOutcome.Calls==1&&ReferenceEquals(RebirthRemoteResourceDurableOutcome.Actor,p)&&RebirthRemoteResourceDurableOutcome.Operation==h.operation&&RebirthRemoteResourceDurableOutcome.Binding==h.binding,"valid receipt not forwarded exactly");
   switch(variant){
    case 0:w.Remote=true;break;case 1:GameManager.Instance.World=new World();break;case 2:h.worldId="other";break;case 3:h.operation=Guid.Empty;break;case 4:h.binding="";break;
    case 5:h.EntityValid=false;break;case 6:h.UserValid=false;break;case 7:h.Sender=null;break;case 8:SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Registered=new Peer();break;
    case 9:SingletonMonoBehaviour<ConnectionManager>.Instance.Clients=null;break;case 10:w.Actor=null;break;case 11:p.Dead=true;break;case 12:p.world=new World();break;
    case 13:p.Supported=false;break;case 14:h.creation=" creation ";break;case 15:p.Creation="other";break;case 16:p.CharacterOwner="foreign";break;case 17:peer.Owner="foreign";break;case 18:p.Owner=null;break;case 19:SingletonMonoBehaviour<ConnectionManager>.Instance=null;break;
    case 20:ThreadManager.Main=false;break;case 21:h.Received=null;break;case 22:h.Received=new NativeConnection();break;case 23:peer.loginDone=false;break;case 24:peer.disconnecting=true;break;case 25:SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;break;
   }
   RebirthRemoteResourceDurableOutcome.Calls=0;h.ProcessPackage(w,GameManager.Instance);Need(RebirthRemoteResourceDurableOutcome.Calls==0,"unauthenticated acknowledgement reached service "+variant);
  }
  Console.WriteLine("PASS actual no-payment handler authentication; service transitions and native peers remain separate evidence");
 }
}
