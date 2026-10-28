using System;using System.IO;using System.Linq;
// Actual native Read return / incoming qualifier / EnterJoin isolation members.
// Native packet authentication, marker codec, lifecycle transaction and native
// ProcessPackage/ToPlayer return remain explicit adapters or outside this cohort.
class World{internal bool Remote=true;internal Player Player;internal bool IsRemote()=>Remote;internal Player GetPrimaryPlayer()=>Player;internal Player GetEntity(int id)=>Player;}
class Player{internal World world;internal int Died;internal bool IsDead()=>false;internal bool IsSpawned()=>true;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
interface INetConnection{bool IsDisconnected();}
class Channel:INetConnection{internal bool Ended;public bool IsDisconnected()=>Ended;}
class ConnectionManager{internal INetConnection[] connectionToServer=new INetConnection[]{new Channel()};}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class PlayerDataFile{internal byte[] bagData=new byte[]{1},inventoryData=new byte[]{2},buffData=new byte[]{3};internal int deaths;internal bool bDead;}
class NetPackagePlayerId{internal PlayerDataFile playerDataFile;}
static class RebirthProductiveEarnedUiDeferredTransport{internal static bool Authenticated=true;internal static bool TryGetReceivedConnection(NetPackagePlayerId packet,out INetConnection connection){connection=SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0];return Authenticated;}}
static class RebirthNpcLifecycle{internal static Func<bool> Boundary=()=>true;internal static bool PrepareNativeEpochBoundary()=>Boundary();}
static class RebirthNpcTradeClientFlow{internal static bool Resume;internal static bool ResumeLoaded(object source)=>Resume;}
static class RebirthNpcTradeLocalOwnerLoad{internal static bool IsState(object state)=>false;internal static void ObserveRead(bool ran,object state){}}
static class RebirthNpcTradeOwnerCheckpoint{internal const string Held="held",HeldLife="life";}
static class RebirthRemoteResourcePlacedStationSavedReceipt{internal static bool TryRead(PlayerDataFile data,string key,out float value,out bool present){value=key=="held"?1:data.deaths;present=true;return true;}}
static class RebirthNpcTradeOwnerLoad
{
 private sealed class Load{internal PlayerDataFile Data;internal byte[] Bag,Belt,Buffs;internal int Deaths,Disposals,Entity;internal long Next;internal Guid Operation;internal bool ReadReturned=true,Closed,Joined,Hydrating,Hydrated,Consumed,Valid;internal World World;internal Player Player;internal NetPackagePlayerId Packet;internal INetConnection Connection;internal bool Current(Player player)=>Valid;internal void Dispose(){Closed=true;Disposals++;}}
 private static class Source{internal static object CaptureCurrent()=>loaded;}
 private static object gate=new object();private static Load next,loaded,joining,incomingBoundary;private static bool installed=true;
 private static bool TryOperation(byte[] data,out Guid operation){operation=Guid.NewGuid();return true;}
 // ACTUAL_INCOMING_JOIN
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Incoming join assertion "+checks);}
  var world=new World();var oldWorld=new World();GameManager.Instance.World=world;var packet=new NetPackagePlayerId();
  Load Seed()
  {
   var data=new PlayerDataFile();packet.playerDataFile=data;var source=new Load{Data=data,Bag=data.bagData.ToArray(),Belt=data.inventoryData.ToArray(),Buffs=data.buffData.ToArray()};
   next=source;loaded=new Load{World=oldWorld};joining=incomingBoundary=null;RebirthProductiveEarnedUiDeferredTransport.Authenticated=true;RebirthNpcLifecycle.Boundary=()=>true;GameManager.Instance.World=world;ThreadManager.Main=true;return source;
  }
  var source=Seed();RebirthProductiveEarnedUiDeferredTransport.Authenticated=false;Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source)&&incomingBoundary==null);
  source=Seed();ThreadManager.Main=false;Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source));ThreadManager.Main=true;
  source=Seed();Check(EnterJoin(packet,world,GameManager.Instance,false)==null&&ReferenceEquals(next,source));
  source=Seed();var old=loaded;RebirthNpcLifecycle.Boundary=()=>false;Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source)&&ReferenceEquals(loaded,old)&&source.Packet==null&&source.World==null&&source.Connection==null&&!source.Closed);
  source=Seed();RebirthNpcLifecycle.Boundary=()=>{throw new Exception();};Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source)&&incomingBoundary==null);
  source=Seed();Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source));
  source=Seed();loaded.World=world;var sameWorldOriginal=loaded;Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source)&&ReferenceEquals(loaded,sameWorldOriginal)&&!sameWorldOriginal.Closed);
  source=Seed();old=loaded;
  RebirthNpcLifecycle.Boundary=()=>
  {
   Check(ReferenceEquals(incomingBoundary,source)&&IncomingBoundaryCurrent()&&next==null&&joining==null&&ReferenceEquals(loaded,old));
   var later=new Load{Data=new PlayerDataFile()};ObserveRead(true,later);Check(later.Closed&&later.Disposals==1&&next==null&&!source.Closed);
   Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(incomingBoundary,source));
   loaded=null;return true;
  };
  var result=EnterJoin(packet,world,GameManager.Instance,true);Check(ReferenceEquals(result,source)&&ReferenceEquals(joining,source)&&incomingBoundary==null&&next==null&&!source.Closed);
  Check(ReferenceEquals(source.Packet,packet)&&ReferenceEquals(source.World,world)&&ReferenceEquals(source.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0]));
  source=Seed();RebirthNpcLifecycle.Boundary=()=>{loaded=null;GameManager.Instance.World=new World();return true;};Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source)&&incomingBoundary==null);
  source=Seed();source.Bag[0]=9;Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source));
  source=Seed();packet.playerDataFile=new PlayerDataFile();Check(EnterJoin(packet,world,GameManager.Instance,true)==null&&ReferenceEquals(next,source));
  source=Seed();old=loaded;old.Next=123;Update();Check(ReferenceEquals(loaded,old)&&old.Next==123&&!old.Closed);
  old.World=world;ThreadManager.Main=false;Update();Check(ReferenceEquals(loaded,old)&&old.Next==123&&!old.Closed);ThreadManager.Main=true;
  old.Next=0;old.Valid=true;RebirthNpcTradeClientFlow.Resume=false;Update();Check(ReferenceEquals(loaded,old)&&!old.Closed&&!old.Consumed);
  old.Next=0;RebirthNpcTradeClientFlow.Resume=true;Update();Check(loaded==null&&old.Closed&&old.Consumed&&old.Disposals==1);
  Console.WriteLine(checks+" actual incoming native join isolation assertions passed; native packet/lifecycle/ToPlayer adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcTradeOwnerLoad.Run();}
