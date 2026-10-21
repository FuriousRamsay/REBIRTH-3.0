using System;
using System.Collections.Generic;
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class)]public sealed class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}
    public static class Storage<T,F>{public static F Value;}
    public static class AccessTools{
        public delegate ref F FieldRef<T,F>(T owner);
        public static FieldRef<T,F> FieldRefAccess<T,F>(string name)=>(T owner)=>ref Storage<T,F>.Value;
    }
}
public struct Position {public float X;public float sqrMagnitude=>X*X;public static Position operator -(Position a,Position b)=>new Position{X=a.X-b.X};}
public class Entity {public World world;public int entityId;public Position position;}
public class EntityPlayer:Entity {public bool Dead;public bool IsDead()=>Dead;}
public class EntityPlayerLocal:EntityPlayer{}
public sealed class World {public bool Remote;public Dictionary<int,Entity> Entities=new();public bool IsRemote()=>Remote;public Entity GetEntity(int id)=>Entities.GetValueOrDefault(id);}
public sealed class ItemValue {public Dictionary<string,string> Metadata=new();public bool TryGetMetadata(string key,out string value)=>Metadata.TryGetValue(key,out value);}
public sealed class Vehicle {public string Name="rebirthCamper80s";public object OwnerId;public ItemValue Item=new();public string GetName()=>Name;public ItemValue GetUpdatedItemValue()=>Item;}
public sealed class EntityVehicle:Entity {
    public Vehicle vehicle=new();public Entity[] Seats=new Entity[6];public bool Locked,AllowedUser;
    public Entity GetAttached(int slot)=>Seats[slot];public bool AllowActivationCommand(ReadOnlySpan<char> command,EntityPlayerLocal player)=>true;
    public bool IsLocked()=>Locked;public bool hasLock()=>true;public bool isAllowedUser(object user)=>AllowedUser;
    public void InitLocalActivationCommands(Action<EntityActivationCommand> add){}public void OnEntityActivated(EntityActivationCommand command,EntityPlayerLocal player){}
}
public sealed class EntityActivationCommand{public string commandId,commandText;public EntityActivationCommand(string id,string icon,object a,object b){commandId=id;}}
public sealed class OneToManyDictionary<K,V>{public Dictionary<V,K> Values=new();public bool TryGetByValue(V value,out K key)=>Values.TryGetValue(value,out key);}
public sealed class LockManager {
    public static LockManager Instance=new();public int LocalPlayer;
    public readonly record struct LockEntry(EntityVehicle Vehicle,ushort Channel);
    public OneToManyDictionary<int,LockEntry> Single=new();
    public bool IsLockedByLocalPlayer(EntityVehicle vehicle,int channel)=>Single.TryGetByValue(new LockEntry(vehicle,(ushort)channel),out int owner)&&owner==LocalPlayer;
}
public sealed class ClientInfo {public int entityId;public object InternalId=new();}
public static class ThreadManager{public static bool IsMainThread()=>true;}
public static class Localization{public static string Get(string key)=>key;}
public static class RebirthCamperRemoteTransport{public static bool IsRegistered=true;}
public static class RebirthCamperRemoteStationTransport{public static bool IsRegistered=true;}
public static class RebirthCamperRemoteStationClient{public static bool IsReady=true;}
public static class RebirthCamperModuleInteraction{public static EntityVehicle Leased;public static EntityPlayerLocal Owner;public static bool Current(EntityVehicle vehicle,EntityPlayerLocal player)=>vehicle==Leased&&player==Owner;public static void Request(EntityVehicle vehicle,EntityPlayerLocal player,string command){}}
public static class RebirthCamperInteractionVisibility{public const string OwnerKey="RebirthCamperInteractionOwner";}
internal static class Program {
    static int passed;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;}
    static void Main(){
        RebirthCamperRadial.OpenSession=(v,p,c)=>{};
        foreach(string mode in new[]{"peer host/client","dedicated server/client"}){
            var world=new World();var passenger=new EntityPlayerLocal{world=world,entityId=11};var driver=new EntityPlayerLocal{world=world,entityId=12};
            var camper=new EntityVehicle{world=world,entityId=20};world.Entities[11]=passenger;world.Entities[12]=driver;world.Entities[20]=camper;
            var locks=LockManager.Instance=new LockManager{LocalPlayer=11};HarmonyLib.Storage<LockManager,OneToManyDictionary<int,LockManager.LockEntry>>.Value=locks.Single;
            camper.Seats[0]=driver;camper.Seats[1]=passenger;var key=new LockManager.LockEntry(camper,0);
            Check(RebirthCamperRadial.Allowed(camper,passenger),mode+" passenger allowed while another player drives");
            Check(!RebirthCamperRadial.Allowed(camper,driver),mode+" driver rejected");
            locks.Single.Values[key]=12;Check(!RebirthCamperRadial.Allowed(camper,passenger),mode+" other user's cargo/use excludes management");
            locks.Single.Values[key]=11;Check(!RebirthCamperRadial.Allowed(camper,passenger),mode+" own native cargo excludes management");
            RebirthCamperModuleInteraction.Leased=camper;RebirthCamperModuleInteraction.Owner=passenger;
            Check(RebirthCamperRadial.Allowed(camper,passenger),mode+" admitted lease remains valid");
            var sender=new ClientInfo{entityId=11};Check(RebirthCamperRemoteAdmission.CanAccess(camper,passenger,sender,()=>true),mode+" server admits passenger only with exact lock owner");
            locks.Single.Values[key]=12;Check(!RebirthCamperRemoteAdmission.CanAccess(camper,passenger,sender,()=>true),mode+" server rejects other lock owner");
            locks.Single.Values[key]=11;Check(!RebirthCamperRemoteAdmission.CanAccess(camper,passenger,sender,()=>false),mode+" expired character rejected");
            camper.Locked=true;Check(!RebirthCamperRemoteAdmission.CanAccess(camper,passenger,sender,()=>true),mode+" locked unauthorized passenger rejected");
            camper.AllowedUser=true;Check(RebirthCamperRemoteAdmission.CanAccess(camper,passenger,sender,()=>true),mode+" permitted passenger admitted");
            RebirthCamperModuleInteraction.Leased=null;RebirthCamperModuleInteraction.Owner=null;world.Remote=true;
            camper.vehicle.Item.Metadata[RebirthCamperInteractionVisibility.OwnerKey]="12";
            Check(!RebirthCamperRadial.Allowed(camper,passenger),mode+" replicated busy hint hides management");
            locks.Single.Values.Clear();camper.vehicle.Item.Metadata[RebirthCamperInteractionVisibility.OwnerKey]="-1";
            Check(RebirthCamperRadial.Allowed(camper,passenger),mode+" unlock restores management visibility");
        }
        Console.WriteLine($"PASS {passed} production admission assertions. Native input, transport and Harmony are doubled; no game launched.");
    }
}
