using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Actual production retry/publication linked by project. Native/world/packet IO
// are explicit doubles; this fixture is not the native consumption codec probe.
static class Program
{
    static int checks;
    static void Check(bool condition,string label)
    { if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label); }
    static ItemStack Stack(int count,string metadata="original")
        =>count==0?ItemStack.Empty.Clone():new ItemStack(new ItemValue{type=1,Seed=42,Metadata=metadata},count);
    static World Server()
    {var world=new World();world.Player=new EntityPlayerLocal{entityId=7,world=world,Creation="creation-a"};return world;}
    static World Owner()
    {var world=Server();world.Remote=true;world.Player.bag.ItemGrid.items=new[]{Stack(2)};GameManager.Instance.World=world;return world;}
    static NetPackageRebirthMetabolismConsumeResult Packet(Guid id,int before=2,int after=1)
        =>new NetPackageRebirthMetabolismConsumeResult{Id=id,Owner=7,Before=Stack(before),After=Stack(after)};
    static byte[] Wire(NetPackageRebirthMetabolismConsumeResult packet)
    {using var stream=new MemoryStream();using var writer=new PooledBinaryWriter();writer.SetBaseStream(stream);packet.write(writer);writer.Flush();return stream.ToArray();}
    static bool Ack(World world,Guid id)=>RebirthConsumptionPublicationRetry.AcknowledgeAuthenticated(world,world.Player,id,world.Player.Creation);
    static void Finish(World world,params Guid[] ids)
    {foreach(var id in ids)Check(Ack(world,id),"cleanup acknowledges retained "+id);Check(RebirthConsumptionPublicationRetry.ClearAcknowledgedWorld(world),"explicit acknowledged-world clear");}
    static void Main()
    {
        var server=Server();var id=Guid.NewGuid();var original=Packet(id);byte[] expected=Wire(original);
        Check(RebirthConsumptionPublicationRetry.Retain(server,server.Player,original),"retains original operation");
        Check(RebirthConsumptionPublicationRetry.Retain(server,server.Player,Packet(id)),"same immutable operation deduplicated");
        original.After.count=9;original.Before.itemValue.Metadata="mutated";
        Check(!RebirthConsumptionPublicationRetry.Retain(server,server.Player,original),"same operation different bytes refused");
        Check(RebirthConsumptionPublicationRetry.TryGetRetry(server,server.Player,id,out var retry),"materializes first retained retry");
        Check(Wire(retry).SequenceEqual(expected),"original immutable wire retained after caller mutation");
        retry.After.itemValue.Metadata="retry-mutated";retry.After.count=10;
        Check(RebirthConsumptionPublicationRetry.TryGetRetry(server,server.Player,id,out var retry2)&&Wire(retry2).SequenceEqual(expected),"retry mutation cannot alter later retry");
        Check((int)RebirthConsumptionPublicationRetry.Observe()["records"].Count()==1,"operation dedup keeps one record");
        Check((int)RebirthConsumptionPublicationRetry.Observe()["records"][0]["retryMaterializations"]==2,"observation records materializations");

        var wrongWorld=Server();
        Check(!RebirthConsumptionPublicationRetry.TryGetRetry(wrongWorld,wrongWorld.Player,id,out _),"different world refused");
        Check(!RebirthConsumptionPublicationRetry.Retain(wrongWorld,wrongWorld.Player,Packet(Guid.NewGuid())),"unresolved world cannot be silently replaced");
        var owner=server.Player;var replacement=new EntityPlayerLocal{entityId=7,world=server,Creation=owner.Creation};server.Player=replacement;
        Check(!RebirthConsumptionPublicationRetry.TryGetRetry(server,replacement,id,out _),"same entity ID replacement owner refused");
        Check(!RebirthConsumptionPublicationRetry.AcknowledgeAuthenticated(server,replacement,id,replacement.Creation),"replacement owner ACK refused");server.Player=owner;
        owner.Creation="creation-b";
        Check(!RebirthConsumptionPublicationRetry.TryGetRetry(server,owner,id,out _),"changed character creation refused");
        Check(!RebirthConsumptionPublicationRetry.AcknowledgeAuthenticated(server,owner,id,"creation-a"),"changed creation cannot ACK old operation");owner.Creation="creation-a";
        Check(!RebirthConsumptionPublicationRetry.AcknowledgeAuthenticated(server,owner,id,"forged-scope"),"wrong ACK creation refused");
        Check(!RebirthConsumptionPublicationRetry.AcknowledgeAuthenticated(server,owner,Guid.NewGuid(),owner.Creation),"unknown ACK refused");
        Check(!RebirthConsumptionPublicationRetry.ClearAcknowledgedWorld(server),"unresolved record blocks clear");
        ThreadManager.Main=false;
        Check(!RebirthConsumptionPublicationRetry.TryGetRetry(server,owner,id,out _),"off-thread retry refused");ThreadManager.Main=true;
        Check(Ack(server,id)&&Ack(server,id),"duplicate authentic ACK idempotent");
        Check(!RebirthConsumptionPublicationRetry.TryGetRetry(server,owner,id,out _),"acknowledged record never retries");
        Check(RebirthConsumptionPublicationRetry.ClearAcknowledgedWorld(server),"acknowledged clear allowed explicitly");

        var client=Owner();var first=Packet(Guid.NewGuid());
        Check(RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(client,first,"creation-a"),"owner applies exact before once");
        Check(client.Player.bag.Writes==1&&client.Player.bag.ItemGrid.items[0].count==1,"owner source debit once");
        client.Player.bag.ItemGrid.items[0]=Stack(9,"different-item");
        Check(RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(client,first,"creation-a"),"receipt duplicate acknowledges after source movement");
        Check(client.Player.bag.Writes==1&&client.Player.bag.ItemGrid.items[0].count==9,"receipt duplicate does not write moved item");
        Check(!RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(client,Packet(Guid.NewGuid()),"creation-a"),"unreceipted changed source refuses overwrite");
        Check(client.Player.bag.Writes==1,"changed source refusal performs no setter");
        Check(!RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(client,first,"other-creation"),"owner ACK application rejects wrong character");
        var differentOwner=Packet(Guid.NewGuid());differentOwner.Owner=8;
        Check(!RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(client,differentOwner,"creation-a"),"wrong owner publication refused");
        var already=Owner();already.Player.bag.ItemGrid.items[0]=Stack(1);var alreadyPacket=Packet(Guid.NewGuid());
        Check(RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(already,alreadyPacket,"creation-a")&&already.Player.bag.Writes==0,"native already-after observation acknowledges without setter");
        var emptyOwner=Owner();emptyOwner.Player.bag.ItemGrid.items[0]=Stack(1);var empty=Packet(Guid.NewGuid(),1,0);
        Check(RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(emptyOwner,empty,"creation-a")&&emptyOwner.Player.bag.ItemGrid.items[0].IsEmpty(),"final serving owner postimage applied");
        Check(RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(emptyOwner,empty,"creation-a")&&emptyOwner.Player.bag.Writes==1,"empty receipt duplicate idempotent");

        var capacity=Server();var ids=new List<Guid>();
        for(int i=0;i<256;i++){var operation=Guid.NewGuid();ids.Add(operation);if(!RebirthConsumptionPublicationRetry.Retain(capacity,capacity.Player,Packet(operation)))throw new Exception("capacity setup "+i);}
        Check(!RebirthConsumptionPublicationRetry.Retain(capacity,capacity.Player,Packet(Guid.NewGuid())),"outstanding capacity refuses new operation");
        Check(RebirthConsumptionPublicationRetry.Observe()["records"].Count()==256,"capacity refuses without removing unresolved records");
        Check(RebirthConsumptionPublicationRetry.TryGetRetry(capacity,capacity.Player,ids[0],out _)&&RebirthConsumptionPublicationRetry.TryGetRetry(capacity,capacity.Player,ids[255],out _),"oldest and newest unresolved replies remain recoverable");
        Check(!RebirthConsumptionPublicationRetry.ClearAcknowledgedWorld(capacity),"capacity clear cannot discard outstanding custody");
        foreach(var operation in ids)if(!Ack(capacity,operation))throw new Exception("capacity cleanup");
        Check(RebirthConsumptionPublicationRetry.ClearAcknowledgedWorld(capacity),"capacity cleanup requires all ACKs");
        Check(NetPackageRebirthMetabolismConsumeResult.ProcessCalls==0&&Effects.Calls==0,"no packet processing or effect replay path invoked");
        Console.WriteLine("PASS "+checks+" SIMULATION ONLY: actual retry/owner source; native/world/character/packet/codec/transport doubles; no authentic sender, sockets, saves, game or benefits validated.");
    }
}

static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
public enum RebirthMetabolismSourceKind{Backpack,HeldToolbelt}
public sealed class World{public bool Remote;public EntityPlayerLocal Player;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public EntityPlayer GetEntity(int id)=>Player?.entityId==id?Player:null;}
public class EntityPlayer{public bool IsDead()=>false;public int entityId;public World world;public string Creation;public Bag bag=new Bag();public Inventory inventory=new Inventory();}
public sealed class EntityPlayerLocal:EntityPlayer{}
public sealed class Grid{public ItemStack[] items=new[]{ItemStack.Empty.Clone()};}
public sealed class Bag{public Grid ItemGrid=new Grid();public int Writes;public void SetSlot(int slot,ItemStack stack){Writes++;ItemGrid.items[slot]=stack;}}
public sealed class Inventory{public Grid ItemGrid=new Grid();public int Writes;public void SetItem(int slot,ItemStack stack){Writes++;ItemGrid.items[slot]=stack;}}
public sealed class ItemValue{public int type;public ushort Seed;public string Metadata="";public ItemValue Clone()=>new ItemValue{type=type,Seed=Seed,Metadata=Metadata};public override bool Equals(object value)=>value is ItemValue other&&type==other.type&&Seed==other.Seed&&Metadata==other.Metadata;public override int GetHashCode()=>HashCode.Combine(type,Seed,Metadata);}
public sealed class ItemStack{public static readonly ItemStack Empty=new ItemStack(new ItemValue(),0);public ItemValue itemValue;public int count;public ItemStack(ItemValue value,int amount){itemValue=value;count=amount;}public bool IsEmpty()=>count<=0||itemValue.type==0;public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);}
sealed class RebirthWorldCharacterRecord{public bool IsComplete=true;public Origin Origin=new Origin();}
sealed class Origin{public string CreationId;}
static class RebirthWorldCharacterService{public static bool TryGet(EntityPlayer player,out RebirthWorldCharacterRecord record){record=new RebirthWorldCharacterRecord();record.Origin.CreationId=player.Creation;return true;}}
static class RebirthSurvivorClientState{public static string GetProjectedCreationId(EntityPlayer player)=>player.Creation;}
static class RebirthSurvivorRequestScope{public static bool Matches(string offered,string current)=>!string.IsNullOrEmpty(offered)&&offered==current;public static bool TryNormalize(string value,out string normalized){normalized=null;if(!Guid.TryParse(value,out var id)||id==Guid.Empty)return false;normalized=id.ToString("N");return true;}}
static class Effects{public static int Calls;}
sealed class SwitchStream:Stream
{
    public Stream Target=new MemoryStream();public override bool CanRead=>Target.CanRead;public override bool CanSeek=>Target.CanSeek;public override bool CanWrite=>Target.CanWrite;public override long Length=>Target.Length;public override long Position{get=>Target.Position;set=>Target.Position=value;}
    public override void Flush()=>Target.Flush();public override int Read(byte[] b,int o,int c)=>Target.Read(b,o,c);public override long Seek(long o,SeekOrigin s)=>Target.Seek(o,s);public override void SetLength(long n)=>Target.SetLength(n);public override void Write(byte[] b,int o,int c)=>Target.Write(b,o,c);
}
public sealed class PooledBinaryWriter:BinaryWriter{public Encoding Encoding{get;set;}=System.Text.Encoding.UTF8;public PooledBinaryWriter():base(new SwitchStream()){}public void SetBaseStream(Stream stream)=>((SwitchStream)BaseStream).Target=stream;}
public sealed class PooledBinaryReader:BinaryReader{public Encoding Encoding{get;set;}=System.Text.Encoding.UTF8;public PooledBinaryReader():base(new SwitchStream()){}public void SetBaseStream(Stream stream)=>((SwitchStream)BaseStream).Target=stream;}
sealed class WriterPool{public PooledBinaryWriter AllocSync(bool sync)=>new PooledBinaryWriter();}
sealed class ReaderPool{public PooledBinaryReader AllocSync(bool sync)=>new PooledBinaryReader();}
static class MemoryPools{public static WriterPool poolBinaryWriter=new WriterPool();public static ReaderPool poolBinaryReader=new ReaderPool();}
// Packet/codec DOUBLE only; actual retry stores bytes and never calls ProcessPackage.
sealed class NetPackageRebirthMetabolismConsumeResult
{
    public static int ProcessCalls;public Guid Id;public int Owner;public ItemStack Before,After;
    public bool TryGetPublicationIdentity(out int owner,out Guid id){owner=Owner;id=Id;return id!=Guid.Empty&&Before!=null&&After!=null;}
    public bool ApplyPublicationForAcknowledgement(World world)=>RebirthConsumptionOwnerPublication.WasApplied(world,Owner,Id)||RebirthConsumptionOwnerPublication.Apply(world,Owner,Id,RebirthMetabolismSourceKind.Backpack,0,Before,After);
    public void write(PooledBinaryWriter writer){writer.Write((ushort)13);writer.Write(Id.ToByteArray());writer.Write(Owner);Write(writer,Before);Write(writer,After);}
    public void read(PooledBinaryReader reader){Id=new Guid(reader.ReadBytes(16));Owner=reader.ReadInt32();Before=Read(reader);After=Read(reader);}
    static void Write(BinaryWriter writer,ItemStack stack){writer.Write(stack.count);writer.Write(stack.itemValue.type);writer.Write(stack.itemValue.Seed);writer.Write(stack.itemValue.Metadata);}
    static ItemStack Read(BinaryReader reader){int count=reader.ReadInt32();return new ItemStack(new ItemValue{type=reader.ReadInt32(),Seed=reader.ReadUInt16(),Metadata=reader.ReadString()},count);}
    public void ProcessPackage(){ProcessCalls++;Effects.Calls++;}
}
// Character-source admission doubles; retry fixture stays vanilla for owner writes.
static class RebirthSurvivorMode{public static bool IsEnabledForCurrentWorld()=>false;}
static class RebirthWorldCharacterRepository{public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>r!=null;}

public sealed class GameManager{public static GameManager Instance=new GameManager();public World World;}
static class RebirthCharacterCreationHoldService{public static bool IsHeld(EntityPlayer p)=>false;}
