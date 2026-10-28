using System;using System.IO;using System.Collections.Generic;
// Actual CurrentCore; native saved prefix/policy, NPC state and session adapters.
class World{internal EntityPlayer Actor;internal State worldState=new State();internal EntityPlayer GetEntity(int id)=>Actor;internal class State{internal string Guid="world";}}
class EntityPlayer{internal World world;internal int entityId=7,Died;internal bool IsDead()=>false;internal bool IsSpawned()=>true;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class PlayerDataFile{}
interface INetConnection{bool IsDisconnected();}
class Channel:INetConnection{public bool IsDisconnected()=>false;}
class ClientInfo{internal int entityId=7;internal bool disconnecting,loginDone=true,bAttachedToEntity=true;internal INetConnection[] netConnection={new Channel()};internal PlayerDataFile latestPlayerData=new PlayerDataFile();}
class ConnectionManager{internal bool IsServer=true;internal ClientsValue Clients=new ClientsValue();internal class ClientsValue{internal ClientInfo Peer=new ClientInfo();internal ClientInfo ForEntityId(int id)=>Peer;}}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class GameIO{internal static string GetSaveGameDir()=>Path.GetTempPath();}
class Identity{internal string CanonicalId="owner";}
static class RebirthStablePlayerIdentity{internal static bool TryFromClientInfo(ClientInfo peer,out Identity owner){owner=new Identity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer actor)=>"creation";}
static class RebirthNpcNetworkEpoch{internal static bool Valid=true;internal static bool ValidateConnectionEpoch(int id,uint connection)=>Valid;internal static uint GetServerEpoch()=>1;}
enum RebirthNpcInteractionAction{Trade}
enum RebirthNpcInteractionResult{Allowed,Rejected}
class Context{internal Guid NpcId;}
static class RebirthNpcInteractionSessionService{internal static bool Allowed=true;internal static int Checks;internal static Context Context=new Context();internal static RebirthNpcInteractionResult Authorize(string session,string actor,RebirthNpcInteractionAction action,out Context context){Checks++;context=Context;return Allowed?RebirthNpcInteractionResult.Allowed:RebirthNpcInteractionResult.Rejected;}}
class Quote{internal Guid NpcId=Guid.NewGuid();internal long ExpiresClockTicks=DateTime.UtcNow.AddMinutes(1).Ticks;}
class RebirthNpcTradeOriginal{internal Guid Operation=Guid.NewGuid();internal int Deaths;internal string World="world",Creation="creation",Account="owner";internal Quote Quote=new Quote();internal Quote CopyQuote()=>Quote;internal bool MatchesQuote(Quote quote)=>ReferenceEquals(quote,Quote);}
static class RebirthGearNativePlayerFile{internal static bool Saved=true;internal static bool TryRead(Identity owner,out PlayerDataFile saved){saved=Saved?new PlayerDataFile():null;return saved!=null;}}
static class RebirthNpcInventoryPersistenceStore{internal static bool Saved=true;internal static string CaptureTradeSaved(RebirthNpcTradeOriginal original,object plan,bool after)=>Saved?"npc-before":null;}
public static partial class RebirthNpcTradeService
{
 private static object Sync=new object();private static Dictionary<Guid,Record> Quotes=new Dictionary<Guid,Record>();
 private class Record{internal Quote Quote;internal bool Consumed;internal object OwnerPreparation;internal int Resolution;}
 internal class HostPrepared{}
 internal class OwnerAdmission
 {
  internal RebirthNpcTradeOriginal Original=new RebirthNpcTradeOriginal();private World world;private EntityPlayer actor;private ConnectionManager manager;private ClientInfo peer;private INetConnection transport;private PlayerDataFile accepted;
  private string directory=Path.GetFullPath(GameIO.GetSaveGameDir()),session="session",actorKey="owner";private uint epoch=1,connection=1;private int nativeId=7;private bool reattached,local;private Record quote;private object NpcPlan=new object();
  internal static bool BeforeValid=true,AfterValid=true,PrefixValid=true;
  private static bool Before(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayer actor)=>BeforeValid;
  private static bool RetainedPrefix(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayer actor)=>PrefixValid;
  private static bool SameRetainedStage(PlayerDataFile a,PlayerDataFile b,RebirthNpcTradeOriginal original)=>PrefixValid;
  private static bool After(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayer actor,float phase)=>AfterValid;
  private bool CurrentLocal(HostPrepared prepared,bool completed,PlayerDataFile head,float phase)=>false;
  // ACTUAL_REMOTE_CURRENT
  internal static void Run()
  {
   int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Remote admission assertion "+checks);}
   var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=new World();world.Actor=new EntityPlayer{world=world};GameManager.Instance.World=world;
   var a=new OwnerAdmission{world=world,actor=world.Actor,manager=manager,peer=manager.Clients.Peer,transport=manager.Clients.Peer.netConnection[0],accepted=manager.Clients.Peer.latestPlayerData};
   a.quote=new Record{Quote=a.Original.Quote};Quotes[a.Original.Operation]=a.quote;RebirthNpcInteractionSessionService.Context.NpcId=a.Original.Quote.NpcId;
   Check(a.CurrentCore(null));RebirthNpcInteractionSessionService.Allowed=false;Check(!a.CurrentCore(null));RebirthNpcInteractionSessionService.Allowed=true;
   a.Original.Quote.ExpiresClockTicks=0;Check(!a.CurrentCore(null));a.Original.Quote.ExpiresClockTicks=DateTime.UtcNow.AddMinutes(1).Ticks;
   var prepared=new HostPrepared();a.quote.Consumed=true;a.quote.OwnerPreparation=prepared;
   int sessions=RebirthNpcInteractionSessionService.Checks;RebirthNpcInteractionSessionService.Allowed=false;a.Original.Quote.ExpiresClockTicks=0;
   Check(a.CurrentCore(prepared)&&RebirthNpcInteractionSessionService.Checks==sessions);
   Check(!a.CurrentCore(new HostPrepared()));a.quote.Resolution=1;Check(!a.CurrentCore(prepared));a.quote.Resolution=0;
   BeforeValid=false;Check(!a.CurrentCore(prepared));BeforeValid=true;
   RebirthGearNativePlayerFile.Saved=false;Check(!a.CurrentCore(prepared));RebirthGearNativePlayerFile.Saved=true;
   RebirthNpcNetworkEpoch.Valid=false;Check(!a.CurrentCore(prepared));RebirthNpcNetworkEpoch.Valid=true;
   var head=new PlayerDataFile();a.peer.latestPlayerData=head;Check(a.CurrentCore(prepared,true,head));AfterValid=false;Check(!a.CurrentCore(prepared,true,head));AfterValid=true;
   a.peer.latestPlayerData=a.accepted;a.reattached=true;Check(a.CurrentCore(prepared));PrefixValid=false;Check(!a.CurrentCore(prepared));PrefixValid=true;
   ThreadManager.Main=false;Check(!a.CurrentCore(prepared));ThreadManager.Main=true;
   GameManager.Instance.World=new World();Check(!a.CurrentCore(prepared));
   Console.WriteLine(checks+" actual remote admission assertions passed; native policy/receipt producers NOT certified");
  }
 }
 internal static void Run()=>OwnerAdmission.Run();
}
class Program{static void Main()=>RebirthNpcTradeService.Run();}
