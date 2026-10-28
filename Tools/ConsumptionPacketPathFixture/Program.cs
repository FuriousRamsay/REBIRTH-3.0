using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

static class Program
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static ItemStack Stack(int type,int count,ushort seed=42)=>new ItemStack(new ItemValue{type=type,Seed=seed,Metadata="prepared-item"},count);
    static World World(bool remote,int type,int count,bool belt=false)
    {var world=new World{Remote=remote};world.Player=new EntityPlayerLocal{entityId=7,world=world};if(belt)world.Player.inventory.ItemGrid.items[0]=Stack(type,count);else world.Player.bag.ItemGrid.items[0]=Stack(type,count);return world;}
    static void Reset(){RebirthMetabolismService.Entries=0;RebirthMetabolismService.Attempts=0;RebirthMetabolismUiFeedback.Count=0;RebirthBackpackLibraryReservation.Blocked=false;}
    static void Route(int type,int count,bool belt)
    {
        Reset();var host=World(false,type,count,belt);var owner=World(true,type,count,belt);
        host.Player.RemoteEffectsTarget=owner.Player;
        var server=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=server;
        var source=belt?host.Player.inventory.ItemGrid.items[0]:host.Player.bag.ItemGrid.items[0];
        var request=new NetPackageRebirthMetabolismConsumeRequest().Setup(7,source.itemValue,false);request.Sender=new ClientInfo{entityId=7};
        GameManager.Instance.World=host;server.Clients.Current=request.Sender;request.ProcessPackage(host,new GameManager());
        Check(RebirthMetabolismService.Entries==1,"server admission double once type="+type+" count="+count);
        Check((belt?host.Player.inventory.ItemGrid.items[0]:host.Player.bag.ItemGrid.items[0]).count==count-1,"actual source writeback debit");
        Check(!host.Player.MinEventContext.IsLocal,"actual native-effect scope restored");
        var received=Transport(server.Sent);SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
        GameManager.Instance.World=owner;received.ProcessPackage(owner,new GameManager());
        var after=belt?owner.Player.inventory.ItemGrid.items[0]:owner.Player.bag.ItemGrid.items[0];
        Check(after.count==count-1,"actual result ProcessPackage owner debit");
        Check(count==1?after.IsEmpty():after.itemValue.Metadata=="prepared-item"&&after.itemValue.Seed==42,"final/remaining native-image double preserved");
        int writes=owner.Player.bag.Writes+owner.Player.inventory.Writes;GameManager.Instance.World=owner;received.ProcessPackage(owner,new GameManager());
        Check(owner.Player.bag.Writes+owner.Player.inventory.Writes==writes&&RebirthMetabolismUiFeedback.Count==1,"duplicate reply neither writes nor repeats feedback");
        Check(RebirthMetabolismService.Entries==1,"duplicate reply cannot rerun server admission");
        if(type==2)Check(owner.Player.Buffs.GetCustomVar("$buffInfectionAddCurePerc")==5&&owner.Player.Buffs.HasBuff("buffInfectionAddCure"),"actual preserved honey wrapper promotes native cure sync double");
        Check(host.Player.Stats.Food.Value==10&&host.Player.Buffs.GetCustomVar("$foodAmountAdd")==0,"preserved native food payload cannot bypass digestion");
        if(count==1)
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance=server;GameManager.Instance.World=host;server.Clients.Current=request.Sender;request.ProcessPackage(host,new GameManager());
            Check(RebirthMetabolismService.Entries==1,"repeated request against empty authoritative source adds no admission");
        }
    }
    static void Main()
    {
        foreach(bool belt in new[]{false,true})foreach(int count in new[]{1,2})foreach(int type in new[]{1,2})Route(type,count,belt);
        Reset();var host=World(false,1,2);var owner=World(true,1,2);var server=new ConnectionManager{IsServer=true};SingletonMonoBehaviour<ConnectionManager>.Instance=server;
        var request=new NetPackageRebirthMetabolismConsumeRequest().Setup(7,host.Player.bag.ItemGrid.items[0].itemValue,false);request.Sender=new ClientInfo{entityId=8};
        GameManager.Instance.World=host;server.Clients.Current=request.Sender;request.ProcessPackage(host,new GameManager());Check(server.Sent==null&&RebirthMetabolismService.Attempts==0,"foreign sender refused before admission");
        request.Sender=new ClientInfo{entityId=7};request.Setup(7,new ItemValue{type=1,Seed=99},false);GameManager.Instance.World=host;server.Clients.Current=request.Sender;request.ProcessPackage(host,new GameManager());
        Check(RebirthMetabolismService.Entries==0&&host.Player.bag.ItemGrid.items[0].count==2,"wrong seed refuses benefits and debit");
        request.Setup(7,host.Player.bag.ItemGrid.items[0].itemValue,false);host.Player.bag.IgnoreWrites=true;GameManager.Instance.World=host;server.Clients.Current=request.Sender;request.ProcessPackage(host,new GameManager());
        Check(RebirthMetabolismService.Entries==0&&host.Player.bag.ItemGrid.items[0].count==2,"native setter refusal blocks admission double");host.Player.bag.IgnoreWrites=false;
        GameManager.Instance.World=host;server.Clients.Current=request.Sender;request.ProcessPackage(host,new GameManager());var received=Transport(server.Sent);
        owner.Player.bag.ItemGrid.items[0]=Stack(9,5);SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();GameManager.Instance.World=owner;received.ProcessPackage(owner,new GameManager());
        Check(owner.Player.bag.Writes==0&&owner.Player.bag.ItemGrid.items[0].itemValue.type==9,"moved owner source refuses overwrite");
        Check(RebirthMetabolismService.Entries==1,"owner refusal does not rollback already-awarded server admission double");
        Console.WriteLine("PASS "+checks+" SIMULATION ONLY: actual ConsumeRequest/Result methods, source identity/writeback, owner publication and preserved effect wrapper. Native codec/min-events/transport/world/stomach admission are explicit doubles. No native infection ticking, sockets or gameplay proof.");
    }
    static NetPackageRebirthMetabolismConsumeResult Transport(NetPackage packet)
    {
        using var bytes=new MemoryStream();using var writer=new PooledBinaryWriter();writer.SetBaseStream(bytes);packet.write(writer);writer.Flush();bytes.Position=0;
        using var reader=new PooledBinaryReader();reader.SetBaseStream(bytes);reader.ReadUInt16();var result=new NetPackageRebirthMetabolismConsumeResult();result.read(reader);return result;
    }
}
namespace UnityEngine.Scripting{[AttributeUsage(AttributeTargets.Class)]public sealed class PreserveAttribute:Attribute{}}
public enum NetPackageDirection{ToServer,ToClient}
public abstract class NetPackage{public ClientInfo Sender;public abstract NetPackageDirection PackageDirection{get;}public abstract void read(PooledBinaryReader reader);public virtual void write(PooledBinaryWriter writer)=>writer.Write((ushort)13);public abstract void ProcessPackage(World world,GameManager callbacks);public bool ValidEntityIdForSender(int owner)=>Sender?.entityId==owner;}
public sealed class ClientInfo{public int entityId;}
public sealed class GameManager{public static readonly GameManager Instance=new GameManager();public World World;}
public sealed class World{public bool Remote;public EntityPlayerLocal Player;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public EntityPlayer GetEntity(int owner)=>Player?.entityId==owner?Player:null;}
public class EntityPlayer
{
    public bool Dead;public bool IsDead()=>Dead;public int entityId;public World world;public Bag bag=new Bag();public Inventory inventory=new Inventory();public Stats Stats=new Stats();public Buffs Buffs=new Buffs();public Context MinEventContext=new Context();public EntityPlayer RemoteEffectsTarget;
    public void FireEvent(int evt){Buffs.SetCustomVar("$foodAmountAdd",8);Stats.Food.Value+=8;if(MinEventContext.ItemValue.type==2){Buffs.SetCustomVar("$buffInfectionAddCurePerc",5);Buffs.AddBuff("buffInfectionAddCure");if(MinEventContext.IsLocal&&RemoteEffectsTarget!=null){RemoteEffectsTarget.Buffs.SetCustomVar("$buffInfectionAddCurePerc",5);RemoteEffectsTarget.Buffs.AddBuff("buffInfectionAddCure");}}}
}
public sealed class EntityPlayerLocal:EntityPlayer{public Stealth Stealth=new Stealth();}
public sealed class Stealth{public void SetSmellEat(float amount){}}
public sealed class Stats{public Stat Food=new Stat{Value=10},Water=new Stat{Value=20};}
public sealed class Stat{public float Value;}
public sealed class Context{public bool IsLocal;public ItemValue ItemValue;}
public sealed class Buffs{readonly Dictionary<string,float> values=new Dictionary<string,float>();readonly HashSet<string> active=new HashSet<string>();public float GetCustomVar(string name)=>values.TryGetValue(name,out var value)?value:0;public void SetCustomVar(string name,float value)=>values[name]=value;public void AddBuff(string name)=>active.Add(name);public bool HasBuff(string name)=>active.Contains(name);}
public sealed class Grid{public ItemStack[] items=new[]{ItemStack.Empty.Clone()};}
public sealed class Bag{public Grid ItemGrid=new Grid();public bool IgnoreWrites;public int Writes;public void SetSlot(int slot,ItemStack value){if(!IgnoreWrites){Writes++;ItemGrid.items[slot]=value.Clone();}}}
public sealed class Inventory{public Grid ItemGrid=new Grid();public int holdingItemIdx;public int Writes;public void SetItem(int slot,ItemStack value){Writes++;ItemGrid.items[slot]=value.Clone();}}
public sealed class ItemClass{public object[] Actions=Array.Empty<object>();}
public sealed class ItemActionEat{public float smellUse;}
public sealed class ItemValue{public int type;public ushort Seed;public string Metadata="";public ItemClass ItemClass=new ItemClass();public ItemValue Clone()=>new ItemValue{type=type,Seed=Seed,Metadata=Metadata};public override bool Equals(object obj)=>obj is ItemValue other&&type==other.type&&Seed==other.Seed&&Metadata==other.Metadata;public override int GetHashCode()=>HashCode.Combine(type,Seed,Metadata);}
public sealed class ItemStack{public static readonly ItemStack Empty=new ItemStack(new ItemValue(),0);public ItemValue itemValue;public int count;public ItemStack(ItemValue value,int amount){itemValue=value;count=amount;}public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);public bool IsEmpty()=>count<=0||itemValue.type==0;}
public enum RebirthMetabolismSourceKind:byte{Backpack,HeldToolbelt,HydrationSlot}
public enum RebirthMetabolismConsumeFailure:byte{None,ItemChanged}
public struct RebirthConsumeResult{public bool Success,CreatedEmptyItem;public RebirthMetabolismConsumeFailure Failure;public float ConsumedMl,RemainingMl;public int AddedEntryId;public string Message;}
public sealed class RebirthMetabolismState{public ItemStack HydrationSlotItem;}
static class RebirthMetabolismService
{
    public static int Entries,Attempts;
    public static bool TryFindMatchingStack(EntityPlayer p,int type,ushort seed,out RebirthMetabolismSourceKind kind,out int slot,out ItemStack stack)
    {kind=RebirthMetabolismSourceKind.HeldToolbelt;slot=0;stack=p.inventory.ItemGrid.items[0];if(RebirthConsumptionSourceIdentity.Matches(stack,type,seed))return true;kind=RebirthMetabolismSourceKind.Backpack;stack=p.bag.ItemGrid.items[0];if(RebirthConsumptionSourceIdentity.Matches(stack,type,seed))return true;stack=null;slot=-1;return false;}
    public static RebirthConsumeResult ConsumeMatchingInventoryItem(EntityPlayer p,int type,ushort seed,bool auto)
    {Attempts++;if(!TryFindMatchingStack(p,type,seed,out var kind,out int slot,out var stack))return new RebirthConsumeResult{Failure=RebirthMetabolismConsumeFailure.ItemChanged};var before=stack.Clone();var after=stack.count==1?ItemStack.Empty.Clone():new ItemStack(stack.itemValue.Clone(),stack.count-1);if(!RebirthConsumptionSourceWriteback.TryCommit(p,kind,slot,before,after,null))return new RebirthConsumeResult{Failure=RebirthMetabolismConsumeFailure.ItemChanged};Entries++;RebirthPreservedConsumableEffects.Apply(p,before.itemValue,true,false);return new RebirthConsumeResult{Success=true,AddedEntryId=Entries,Message="entered digestion"};}
}
static class RebirthMetabolismUiFeedback{public static int Count;public static void Receive(RebirthConsumeResult result){Count++;}}
static class RebirthBackpackLibraryReservation{public static bool Blocked;public static bool BlocksResourceUse(EntityPlayer player)=>Blocked;}
static class RebirthToolbeltCapacity{public static int GetOwnedSlotCount(EntityPlayer player,int length)=>length;}
static class ThreadManager{public static bool IsMainThread()=>true;}
static class RebirthConsumptionPublicationRetry{public static bool AcknowledgementProtocolEnabled=false;}
static class RebirthSurvivorClientState{public static string GetProjectedCreationId(EntityPlayer player)=>"fixture";}
static class RebirthConsumptionAcknowledgement{public static bool TrySend(World world,NetPackageRebirthMetabolismConsumeResult packet,string creation)=>false;}
// Explicit native current-connection registry boundary, not socket/authentication proof.
public sealed class Clients{public ClientInfo Current;public ClientInfo ForEntityId(int id)=>Current?.entityId==id?Current:null;}
public sealed class ConnectionManager{public Clients Clients=new Clients();public bool IsServer;public NetPackage Sent;public void SendPackage(NetPackage packet,int _attachedToEntityId){Sent=packet;}}
static class SingletonMonoBehaviour<T>{public static T Instance;}
static class NetPackageManager{public static T GetPackage<T>()where T:new()=>new T();}
static class QuestEventManager{public static QuestEvents Current=new QuestEvents();}
sealed class QuestEvents{public void UsedItem(ItemValue value){}}
static class MinEvent{public static int[] End={1};}
static class Log{public static void Warning(string message){}public static void Out(string message){}}
static class RebirthNativeItemCodec
{
    public static string Encode(ItemValue item){using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(item.type);writer.Write(item.Seed);writer.Write(item.Metadata);writer.Flush();return Convert.ToBase64String(stream.ToArray());}
    public static bool TryDecode(string value,out ItemValue item){item=null;try{using var stream=new MemoryStream(Convert.FromBase64String(value));using var reader=new BinaryReader(stream);var next=new ItemValue{type=reader.ReadInt32(),Seed=reader.ReadUInt16(),Metadata=reader.ReadString()};if(next.type<=0||stream.Position!=stream.Length)return false;item=next;return true;}catch{return false;}}
}
sealed class SwitchStream:Stream{public Stream Target=new MemoryStream();public override bool CanRead=>true;public override bool CanSeek=>true;public override bool CanWrite=>true;public override long Length=>Target.Length;public override long Position{get=>Target.Position;set=>Target.Position=value;}public override int Read(byte[] b,int o,int c)=>Target.Read(b,o,c);public override void Write(byte[] b,int o,int c)=>Target.Write(b,o,c);public override void Flush()=>Target.Flush();public override long Seek(long o,SeekOrigin origin)=>Target.Seek(o,origin);public override void SetLength(long length)=>Target.SetLength(length);}
public sealed class PooledBinaryReader:BinaryReader{public PooledBinaryReader():base(new SwitchStream()){}public void SetBaseStream(Stream value)=>((SwitchStream)BaseStream).Target=value;}
public sealed class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter():base(new SwitchStream()){}public void SetBaseStream(Stream value)=>((SwitchStream)BaseStream).Target=value;}


// Explicit character-scope boundaries: vanilla unless a fixture enables Survivor.
static class RebirthSurvivorMode{public static bool Enabled;public static bool IsEnabledForCurrentWorld()=>Enabled;}
sealed class RebirthWorldCharacterRecord{public bool IsComplete;public ScopeOrigin Origin;}
sealed class ScopeOrigin{public string CreationId;}
static class RebirthWorldCharacterService{public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=null;return false;}}
static class RebirthWorldCharacterRepository{public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>false;}
static class RebirthSurvivorRequestScope{public static bool TryNormalize(string s,out string n){n=null;if(!Guid.TryParse(s,out var g)||g==Guid.Empty)return false;n=g.ToString("N");return true;}}
static class RebirthSurvivorNetworkCodec{public static string ReadBoundedString(BinaryReader r,int max){var s=r.ReadString();if(s.Length>max)throw new InvalidDataException();return s;}public static void WriteString(BinaryWriter w,string s,int max){if(s.Length>max)throw new InvalidDataException();w.Write(s);}}
static class RebirthCharacterCreationHoldService{public static bool IsHeld(EntityPlayer p)=>false;}
