using System;using System.Collections.Generic;
class World{public bool Remote=true;public bool IsRemote(){return Remote;}}
class EntityPlayerLocal{public World world;public int entityId=7;}
class ItemStack{}
class PersistentPlayerData{public object PrimaryId=new object();}
class GameManager{public static GameManager Instance;public World World;public PersistentPlayerData Persistent=new PersistentPlayerData();public PersistentPlayerData GetPersistentLocalPlayer(){return Persistent;}}
class Channel{public bool Disconnected;public bool IsDisconnected(){return Disconnected;}}
class ConnectionManager{public bool IsServer,IsConnected=true,ThrowOnSend;public Channel[] Channels=new[]{new Channel()};public int Sends;public Channel[] GetConnectionToServer(){return Channels;}public void SendToServer(NetPackageRemoteResourceConsumeRequest p){Sends++;if(ThrowOnSend)throw new Exception("ambiguous");}}
class SingletonMonoBehaviour<T>{public static T Instance;}
static class RemoteResourceDiagnostics{public static void Write(string s){}}
enum RemoteResourceClientOperation{Craft=1,ItemRepair,BlockRepair,BlockUpgrade}
enum RemoteResourceTransactionRequestMode{ExecuteOrReplay=1,QueryOnly}
class NetPackageRemoteResourceConsumeRequest{public int Channel;public static ulong Epoch,Id;public static RemoteResourceTransactionRequestMode Mode;public void Setup(int player,object uid,ulong epoch,ulong id,RemoteResourceClientOperation op,RemoteResourceTransactionRequestMode mode,List<ItemStack> items){Epoch=epoch;Id=id;Mode=mode;}}
class NetPackageManager{public static T GetPackage<T>()where T:new(){return new T();}}
class Subject{static ulong clientSessionEpoch=123;
// PRODUCTION_METHOD
public static bool Send(EntityPlayerLocal p,bool query){return SendRequest(p,8,RemoteResourceClientOperation.Craft,new List<ItemStack>(),query?RemoteResourceTransactionRequestMode.QueryOnly:RemoteResourceTransactionRequestMode.ExecuteOrReplay,query?456UL:0);}
public static ulong Epoch(){return CreateSessionEpoch();}}
class Check{static void Main(){foreach(bool query in new[]{false,true})for(int mode=0;mode<10;mode++){var w=new World();var p=new EntityPlayerLocal{world=w};var g=new GameManager{World=w};GameManager.Instance=g;var c=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=c;if(mode==1)c.IsConnected=false;if(mode==2)c.Channels=null;if(mode==3)c.Channels=new Channel[0];if(mode==4)c.Channels[0]=null;if(mode==5)c.Channels[0].Disconnected=true;if(mode==6)g.Persistent=null;if(mode==7)g.World=new World();if(mode==8)c.IsServer=true;if(mode==9)c.ThrowOnSend=true;try{bool sent=Subject.Send(p,query);if(sent!=(mode==0)||c.Sends!=(mode==0?1:0))throw new Exception("readiness "+mode);if(sent&&(NetPackageRemoteResourceConsumeRequest.Epoch!=(query?456UL:123UL)||NetPackageRemoteResourceConsumeRequest.Id!=8||NetPackageRemoteResourceConsumeRequest.Mode!=(query?RemoteResourceTransactionRequestMode.QueryOnly:RemoteResourceTransactionRequestMode.ExecuteOrReplay)))throw new Exception("wire identity");}catch(Exception){if(mode!=9||c.Sends!=1)throw;}}
var epochs=new HashSet<ulong>();for(int i=0;i<1000;i++){ulong e=Subject.Epoch();if(e==0||!epochs.Add(e))throw new Exception("epoch sample");}Console.WriteLine("PASS: execute/query readiness, wire epoch/id/mode preserved, ambiguous send propagated; 1000 fresh nonzero epoch samples");}}
