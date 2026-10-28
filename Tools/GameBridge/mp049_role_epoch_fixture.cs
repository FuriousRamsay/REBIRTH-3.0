using System;using System.Collections.Generic;
// Actual epoch entry members. Lifecycle transition and native transport/world
// are explicit adapters; no actual original detach or network join is certified.
class World{internal bool Remote;internal bool IsRemote()=>Remote;}
interface INetConnection{bool IsDisconnected();}
class Channel:INetConnection{internal bool Ended;public bool IsDisconnected()=>Ended;}
class ConnectionManager{internal bool IsServer=true;internal INetConnection[] Channels=new INetConnection[]{new Channel()};internal INetConnection[] GetConnectionToServer()=>Channels;}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class GameIO{internal static string GetSaveGameDir()=>"save";}
static class RebirthNpcLifecycle{internal static Func<bool> Boundary=()=>true;internal static int Calls;internal static bool PrepareNativeEpochBoundary(){Calls++;return Boundary();}}
static class RebirthNpcPersistenceCoordinator{internal static bool IsRecoveryInstalling;}
static class RebirthNpcNetworkEpoch
{
 private static object Sync=new object();private static World serverWorld,clientWorld;private static ConnectionManager serverManager,clientManager;
 private static INetConnection clientTransport;private static string clientDirectory;private static uint activeServerEpoch,clientEpoch,clientConnectionEpoch,nextServerEpoch=1;
 private static Dictionary<int,uint> ServerConnectionEpochs=new Dictionary<int,uint>();
 private static uint TakeEpoch(ref uint next){uint value=next;if(next!=0)next=next==uint.MaxValue?0:next+1;return value;}
 // ACTUAL_ROLE_EPOCHS
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Role epoch assertion "+checks);}
  void Empty(){serverWorld=clientWorld=null;serverManager=clientManager=null;clientTransport=null;clientDirectory=null;activeServerEpoch=clientEpoch=clientConnectionEpoch=0;ServerConnectionEpochs.Clear();nextServerEpoch=1;RebirthNpcLifecycle.Calls=0;RebirthNpcLifecycle.Boundary=()=>true;ThreadManager.Main=true;}
  var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var server=new World();var remote=new World{Remote=true};Empty();GameManager.Instance.World=server;
  ThreadManager.Main=false;Check(GetServerEpoch()==0&&RebirthNpcLifecycle.Calls==0);ThreadManager.Main=true;
  RebirthNpcLifecycle.Boundary=()=>false;Check(GetServerEpoch()==0&&serverWorld==null&&nextServerEpoch==1);
  RebirthNpcLifecycle.Boundary=()=>true;uint issued=GetServerEpoch();Check(issued==1&&ReferenceEquals(serverWorld,server)&&nextServerEpoch==2);
  Check(GetServerEpoch()==issued&&nextServerEpoch==2);
  manager.IsServer=false;GameManager.Instance.World=remote;BeginClientEpoch(3,4);Check(clientWorld==null&&ReferenceEquals(serverWorld,server));
  RebirthNpcLifecycle.Boundary=()=>{serverWorld=null;serverManager=null;activeServerEpoch=0;return true;};BeginClientEpoch(3,4);
  Check(ReferenceEquals(clientWorld,remote)&&clientEpoch==3&&clientConnectionEpoch==4&&serverWorld==null);
  RebirthNpcLifecycle.Boundary=()=>true;manager.IsServer=true;GameManager.Instance.World=server;Check(GetServerEpoch()==0&&serverWorld==null&&ReferenceEquals(clientWorld,remote));
  RebirthNpcLifecycle.Boundary=()=>{clientWorld=null;clientManager=null;clientTransport=null;clientDirectory=null;clientEpoch=clientConnectionEpoch=0;return true;};
  Check(GetServerEpoch()==2&&ReferenceEquals(serverWorld,server)&&clientWorld==null);
  Empty();GameManager.Instance.World=remote;manager.IsServer=false;RebirthNpcLifecycle.Boundary=()=>false;BeginClientEpoch(3,4);Check(clientWorld==null);
  RebirthNpcLifecycle.Boundary=()=>true;BeginClientEpoch(3,4);var originalTransport=clientTransport;Check(ReferenceEquals(clientWorld,remote)&&clientEpoch==3);
  BeginClientEpoch(8,9);Check(clientEpoch==3&&clientConnectionEpoch==4&&ReferenceEquals(clientTransport,originalTransport));
  Empty();GameManager.Instance.World=remote;manager.IsServer=false;((Channel)manager.Channels[0]).Ended=true;BeginClientEpoch(3,4);Check(clientWorld==null);((Channel)manager.Channels[0]).Ended=false;
  BeginClientEpoch(0,4);Check(clientWorld==null);
  RebirthNpcLifecycle.Boundary=()=>{GameManager.Instance.World=new World{Remote=true};return true;};BeginClientEpoch(3,4);Check(clientWorld==null);
  GameManager.Instance.World=remote;RebirthNpcLifecycle.Boundary=()=>{manager.Channels=new INetConnection[]{new Channel()};return true;};BeginClientEpoch(3,4);Check(clientWorld==null);
  Empty();GameManager.Instance.World=server;manager.IsServer=true;RebirthNpcLifecycle.Boundary=()=>{GameManager.Instance.World=new World();return true;};Check(GetServerEpoch()==0&&serverWorld==null);
  Console.WriteLine(checks+" actual role epoch assertions passed; lifecycle/native transport adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcNetworkEpoch.Run();}
