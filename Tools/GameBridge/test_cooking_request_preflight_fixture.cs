using System;using System.Collections.Generic;
class World{public bool Remote=true;public bool IsRemote(){return Remote;}}
class EntityPlayerLocal{public World world;public int entityId=1;}
class ItemStack{}
class Persistent {public object PrimaryId=new object();}
class Players {public Persistent Data=new Persistent();public Persistent GetPlayerDataFromEntityID(int id){return Data;}}
class GameManager {public static GameManager Instance;public World World;public Players Players=new Players();public Players GetPersistentPlayerList(){return Players;}}
class Channel{public bool Disconnected;public bool IsDisconnected(){return Disconnected;}}
class ConnectionManager {public bool IsServer,IsConnected=true,ThrowOnSend;public Channel[] Channels=new[]{new Channel()};public int Sends;public Channel[] GetConnectionToServer(){return Channels;}public void SendToServer(NetPackageRebirthCookingPull p){Sends++;if(ThrowOnSend)throw new Exception("ambiguous send");}}
class SingletonMonoBehaviour<T>{public static T Instance;}
class NetPackageRebirthCookingPull{public int Channel;public void Setup(int p,object uid,long id,List<ItemStack> needs){}}
class NetPackageManager{public static T GetPackage<T>() where T:new(){return new T();}}
class Subject{
const int MaximumIngredientRequests=12;static long sequence;public static Dictionary<long,Action<List<ItemStack>,string>> waiting=new Dictionary<long,Action<List<ItemStack>,string>>();
// PRODUCTION_METHOD
}
class Check{static void Main(){for(int mode=0;mode<12;mode++){var world=new World();var player=new EntityPlayerLocal{world=world};var game=new GameManager{World=world};GameManager.Instance=game;var connection=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=connection;var needs=new List<ItemStack>{new ItemStack()};Action<List<ItemStack>,string> callback=(a,b)=>{};Subject.waiting.Clear();if(mode==1)connection.IsConnected=false;if(mode==2)connection.Channels=null;if(mode==3)connection.Channels=new Channel[0];if(mode==4)connection.Channels[0]=null;if(mode==5)connection.Channels[0].Disconnected=true;if(mode==6)game.Players.Data=null;if(mode==7)game.Players.Data.PrimaryId=null;if(mode==8)game.World=new World();if(mode==9)connection.IsServer=true;if(mode==10)needs.Clear();if(mode==11)connection.ThrowOnSend=true;try{bool sent=Subject.Request(player,needs,callback);if(sent!=(mode==0)||Subject.waiting.Count!=(mode==0?1:0)||connection.Sends!=(mode==0?1:0))throw new Exception("preflight case "+mode);}catch(Exception){if(mode!=11||Subject.waiting.Count!=1||connection.Sends!=1)throw;}}
Console.WriteLine("PASS: available request queued; disconnected/missing channel/identity/wrong world/empty request rejected without pending state; ambiguous send retains callback");}}
