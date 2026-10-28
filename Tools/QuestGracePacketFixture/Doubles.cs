using System;
using System.IO;
// Explicit native transport/world/quest-manager boundaries. Actual packet
// classes are linked; no claim that these doubles prove native quest persistence.
public enum NetPackageDirection {ToServer}
public abstract class NetPackage {public ClientInfo Sender;public abstract NetPackageDirection PackageDirection{get;}public abstract void read(PooledBinaryReader r);public virtual void write(PooledBinaryWriter w){}public abstract void ProcessPackage(World world,GameManager callbacks);}
public sealed class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){}}
public sealed class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream s):base(s){}}
public sealed class ClientInfo {public int entityId;}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public sealed class ClientRegistry {public ClientInfo Current;public ClientInfo ForEntityId(int id)=>Current?.entityId==id?Current:null;}
public sealed class ConnectionManager {public bool IsServer=true;public ClientRegistry Clients=new ClientRegistry();}
public sealed class EntityPlayer {public World world;public bool Dead,Eligible=true,FinalPhase=true;}
public sealed class World {public bool Remote;public EntityPlayer Player;public bool IsRemote()=>Remote;public object GetEntity(int id)=>id==1?Player:null;}
public sealed class GameManager {public static GameManager Instance;public World World;}
public enum RebirthTraderQuestGraceReason:byte {LeftArea,Death,Disconnect}
public static class RebirthTraderQuestGraceManager {
 public static int Parties,Updates;public static EntityPlayer LastPlayer;
 public static bool IsAuthorizedPartyCompletion(EntityPlayer p,int code,string id)=>p?.Eligible==true&&p.FinalPhase;
 public static void MarkOfflinePartySiteCompleted(int code,string id){Parties++;}
 public static void ApplyServerGraceUpdate(ClientInfo sender,EntityPlayer p,int code,string id,string key,long deadline,RebirthTraderQuestGraceReason reason,bool clear){Updates++;LastPlayer=p;}
}
