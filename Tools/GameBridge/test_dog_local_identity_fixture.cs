using System;
class EntityPlayer{public int entityId;}
class PersistentPlayerData{public string PrimaryId;}
class PlayerList{public PersistentPlayerData data;public PersistentPlayerData GetPlayerDataFromEntityID(int id){return data;}}
class World{public bool remote;public EntityPlayer local;public bool IsRemote(){return remote;}public EntityPlayer GetPrimaryPlayer(){return local;}}
class GameManager{public static GameManager Instance;public World World;public PlayerList list;public PersistentPlayerData local;public PlayerList GetPersistentPlayerList(){return list;}public PersistentPlayerData GetPersistentLocalPlayer(){return local;}}
class RebirthStablePlayerIdentity{public string CanonicalId;public static RebirthStablePlayerIdentity fallback;public static bool TryResolveServerEntity(EntityPlayer p,out RebirthStablePlayerIdentity i){i=fallback;return i!=null;}}
class Check{
// METHOD
static void Need(bool b,string s){if(!b)throw new Exception(s);}
static int Main(){var p=new EntityPlayer{entityId=1};var other=new EntityPlayer{entityId=2};string id;
GameManager.Instance=new GameManager{World=new World{remote=true,local=p},list=new PlayerList(),local=new PersistentPlayerData{PrimaryId="local"}};
Need(TryResolveOwnerId(p,out id)&&id=="local","remote local fallback");Need(!TryResolveOwnerId(other,out id)&&id=="","do not claim other player");GameManager.Instance.World.remote=false;Need(!TryResolveOwnerId(p,out id),"no local identity on server");RebirthStablePlayerIdentity.fallback=new RebirthStablePlayerIdentity{CanonicalId="server"};Need(TryResolveOwnerId(p,out id)&&id=="server","server cache preserved");GameManager.Instance.World.remote=true;GameManager.Instance.list.data=new PersistentPlayerData{PrimaryId="indexed"};Need(TryResolveOwnerId(p,out id)&&id=="indexed","indexed preferred");GameManager.Instance.list=null;Need(TryResolveOwnerId(p,out id)&&id=="local","missing list fallback");GameManager.Instance.local=null;RebirthStablePlayerIdentity.fallback=null;Need(!TryResolveOwnerId(p,out id),"missing identity");Need(!TryResolveOwnerId(null,out id),"null player");GameManager.Instance=null;Need(!TryResolveOwnerId(p,out id),"null game");Console.WriteLine("PASS: exact local identity fallback, other-player isolation, indexed precedence and server fallback preserved");return 0;}}
