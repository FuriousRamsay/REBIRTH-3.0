using System.Collections.Generic;
public class ClientInfo{public int ClientNumber,entityId;public bool loginDone=true,bAttachedToEntity=true,disconnecting;}
public class ClientInfoCollection{public Dictionary<int,ClientInfo> Entries=new Dictionary<int,ClientInfo>();public ClientInfo ForClientNumber(int n)=>Entries.TryGetValue(n,out var c)?c:null;}
public class ConnectionManager{public bool IsServer=true;public ClientInfoCollection Clients=new ClientInfoCollection();}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public class GameManager{public static GameManager Instance=new GameManager();public World World;}
public class World{public bool Remote;public Dictionary<int,EntityPlayer> Actors=new Dictionary<int,EntityPlayer>();public bool IsRemote()=>Remote;public object GetEntity(int id)=>Actors.TryGetValue(id,out var p)?p:null;}
public class EntityPlayer{public int entityId;public World world;public bool Dead;public bool IsDead()=>Dead;}
