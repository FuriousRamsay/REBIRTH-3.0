using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

// Explicit native/context doubles. None of the production ledger is replaced.
static class NativeEvidence
{
    internal static int Reads,Encodes,Saves,Setters;
    internal static bool ThrowReads;
    internal static void Read(){Reads++;if(ThrowReads)throw new InvalidOperationException("context fault");}
    internal static void Mutation(string kind){if(kind=="encode")Encodes++;else if(kind=="save")Saves++;else Setters++;throw new Exception("Forbidden native effect: "+kind);}
}
public class EntityPlayer
{
    public int entityId;public World world;public Buffs Buffs=new();public string Creation="creation";
    public readonly object[] Inventory={new object(),new object()};public int Count=17;
    public void SetSlot(int index,object value)=>NativeEvidence.Mutation("setter");
}
public class EntityPlayerLocal:EntityPlayer{}
public class Buffs
{
    public readonly Dictionary<string,float> Values=new();
    public float GetCustomVar(string name){NativeEvidence.Read();return Values.TryGetValue(name,out var value)?value:0;}
    public void SetCustomVar(string name,float value,bool sync)=>NativeEvidence.Mutation("setter");
}
public class WorldState{public string Guid="world-original";}
public class World
{
    public WorldState worldState=new();public bool Remote=true;public EntityPlayerLocal Primary;
    public readonly Dictionary<int,EntityPlayer> Entities=new();
    public bool IsRemote()=>Remote;
    public EntityPlayerLocal GetPrimaryPlayer()=>Primary;
    public EntityPlayer GetEntity(int id)=>Entities.TryGetValue(id,out var player)?player:null;
}
public class GameManager
{
    public static GameManager Instance;public World World;
    public void SaveLocalPlayerData()=>NativeEvidence.Mutation("save");
}
public class Connection{}
public class ClientInfo{public string StorageKey;}
public class Clients
{
    public readonly Dictionary<int,ClientInfo> Actors=new();
    public ClientInfo ForEntityId(int id)=>Actors.TryGetValue(id,out var info)?info:null;
}
public class ConnectionManager{public Connection[] connectionToServer={new()};public Clients Clients=new();}
public static class SingletonMonoBehaviour<T>{public static T Instance;}
public class RebirthStablePlayerIdentity
{
    public string StorageKey;
    public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity)
    {NativeEvidence.Read();identity=new(){StorageKey="local-owner"};return true;}
    public static bool TryFromClientInfo(ClientInfo sender,out RebirthStablePlayerIdentity identity)
    {NativeEvidence.Read();identity=new(){StorageKey=sender.StorageKey};return true;}
}
public static class RebirthSurvivorClientState
{public static string GetProjectedCreationId(EntityPlayer player){NativeEvidence.Read();return player.Creation;}}
public enum EnumGamePrefs{GameGuidClient}
public static class GamePrefs{public static string GetString(EnumGamePrefs key)=>"saved-world-original";}
public static class RebirthGearOwnerReservation
{public static readonly HashSet<EntityPlayerLocal> Holds=new();public static bool IsHeld(EntityPlayerLocal player)=>player!=null&&Holds.Contains(player);}
public static class RebirthBackpackLibraryReservation
{public static bool IsHeld(EntityPlayerLocal player)=>false;}
public static class RemoteResourceClientTransactionCoordinator
{public static bool HasPendingInventoryOperation(EntityPlayerLocal player)=>false;}
public class RebirthGearTransferState{public string TransactionId,CreationId;public string SecretBlob="ITEM_BLOB_SENTINEL";}
public static class RebirthNativeItemCodec
{public static string Encode(object item){NativeEvidence.Mutation("encode");return null;}}

static class Program
{
    const string Tx="11111111111111111111111111111111";
    static int checks;static EntityPlayerLocal owner;static object[] originalItems;
    static void Check(bool result,string message){if(!result)throw new Exception(message);checks++;}
    static JArray Events()=> (JArray)RebirthGearTrace.Snapshot()["events"];
    static JObject Last()=> (JObject)Events().Last;
    static void Setup()
    {
        owner=new(){entityId=7};var world=new World{Primary=owner};owner.world=world;world.Entities[7]=owner;
        owner.Buffs.Values["rbGear_"+Tx]=2;originalItems=(object[])owner.Inventory.Clone();
        GameManager.Instance=new(){World=world};SingletonMonoBehaviour<ConnectionManager>.Instance=new();
        RebirthGearOwnerReservation.Holds.Clear();RebirthGearOwnerReservation.Holds.Add(owner);
    }
    static void NoNativeEffects()
    {
        Check(NativeEvidence.Encodes==0&&NativeEvidence.Saves==0&&NativeEvidence.Setters==0,"trace invoked forbidden native effect");
        Check(owner.Count==17&&owner.Inventory.SequenceEqual(originalItems)&&owner.Buffs.Values["rbGear_"+Tx]==2&&
              RebirthGearOwnerReservation.IsHeld(owner),"trace changed native inventory/receipt/custody");
    }
    static void Main()
    {
        Setup();
        Check((bool)RebirthGearTrace.Snapshot()["enabled"]==false&&Events().Count==0,"production trace not default off");
        int reads=NativeEvidence.Reads;RebirthGearTrace.Note(owner,Tx,"creation","disabled");
        Check(NativeEvidence.Reads==reads&&Events().Count==0,"disabled trace inspected native context");
        RebirthGearTrace.Configure(true,true);RebirthGearTrace.Note(owner,Tx,"creation","enabled",false);
        var first=Last();long sequence=(long)first["sequence"];
        Check((int)first["ownerEntityId"]==7&&(string)first["ownerKey"]=="local-owner"&&
              (bool)first["currentOwner"]&&(bool)first["currentEntity"]&&(bool)first["gearHeld"]&&
              (float)first["receipt"]==2f&&!(bool)first["success"],"local owner/receipt/hold attribution lost");
        Check((int)first["worldScope"]==RuntimeHelpers.GetHashCode(owner.world)&&
              (int)first["sessionScope"]==RuntimeHelpers.GetHashCode(SingletonMonoBehaviour<ConnectionManager>.Instance.connectionToServer[0]),"captured exact reference scope differs");
        RebirthGearTrace.Configure(false,false);reads=NativeEvidence.Reads;
        RebirthGearTrace.Note(owner,Tx,"creation","disabled-again");
        Check(NativeEvidence.Reads==reads&&Events().Count==1&&(long)RebirthGearTrace.Snapshot()["lastSequence"]==sequence,"off transition recorded/inspected context");
        RebirthGearTrace.Configure(true,true);
        Check(Events().Count==0&&(long)RebirthGearTrace.Snapshot()["lastSequence"]==sequence,"clear reset monotonic sequence");
        for(int i=0;i<250;i++)RebirthGearTrace.Note(owner,Tx,"creation","bounded."+i);
        var bounded=Events();
        Check(bounded.Count==192&&(string)bounded[0]["stage"]=="bounded.58"&&
              (long)bounded[0]["sequence"]==sequence+59&&(long)bounded.Last["sequence"]==sequence+250,"event bound/order/sequence incorrect");
        var snapshot=RebirthGearTrace.Snapshot();((JObject)((JArray)snapshot["events"])[0])["stage"]="tampered";
        ((JArray)snapshot["events"]).Clear();snapshot["lastSequence"]=-1;
        Check(Events().Count==192&&(string)Events()[0]["stage"]=="bounded.58"&&
              (long)RebirthGearTrace.Snapshot()["lastSequence"]==sequence+250,"snapshot aliases actual ledger");
        var foreign=new EntityPlayerLocal{entityId=7,world=owner.world,Creation="foreign-creation"};
        RebirthGearTrace.Note(foreign,Tx,"foreign-creation","foreign-owner");
        Check(!(bool)Last()["currentOwner"]&&!(bool)Last()["currentEntity"]&&!(bool)Last()["gearHeld"],"foreign same-ID object claimed current owner/custody");
        var serverPlayer=new EntityPlayer{entityId=44,world=owner.world,Creation="remote-creation"};
        owner.world.Remote=false;owner.world.Entities[44]=serverPlayer;
        var sender=new ClientInfo{StorageKey="remote-owner"};SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Actors[44]=sender;
        RebirthGearTrace.Note(serverPlayer,Tx,"remote-creation","server-actor",true,sender);
        var actor=Last();
        Check((int)actor["ownerEntityId"]==44&&(string)actor["ownerKey"]=="remote-owner"&&
              (bool)actor["authenticatedSender"]&&(bool)actor["currentEntity"]&&!(bool)actor["currentOwner"]&&
              !(bool)actor["gearHeld"]&&(int)actor["sessionScope"]==RuntimeHelpers.GetHashCode(sender),"server actor was attributed to host primary");
        RebirthGearTrace.Note(serverPlayer,Tx,"remote-creation","foreign-sender",false,new ClientInfo{StorageKey="foreign-owner"});
        Check(!(bool)Last()["authenticatedSender"]&&(string)Last()["ownerKey"]=="foreign-owner", "foreign sender identity was silently authenticated/replaced");
        long beforeFault=(long)RebirthGearTrace.Snapshot()["lastSequence"];NativeEvidence.ThrowReads=true;
        RebirthGearTrace.Note(owner,Tx,"creation","context-fault");NativeEvidence.ThrowReads=false;
        Check((long)RebirthGearTrace.Snapshot()["lastSequence"]==beforeFault,"context exception escaped or produced incomplete event");
        var offer=new RebirthGearTransferState{TransactionId=Tx,CreationId="creation"};
        RebirthGearTrace.Note(offer,"offer-overload");
        Check((string)Last()["transactionId"]==Tx&&(string)Last()["creationId"]=="creation"&&!RebirthGearTrace.Snapshot().ToString().Contains(offer.SecretBlob),"offer correlation/blob privacy failed");
        NoNativeEffects();RebirthGearTrace.Configure(false,true);
        Console.WriteLine("PASS "+checks+" actual GearTrace ledger checks; native/context doubled, no native game execution.");
    }
}
