using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

static class AckProgram
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static bool Acknowledged()=> (bool)RebirthConsumptionPublicationRetry.Observe()["records"][0]["acknowledged"];
    static void Main()
    {
        string creation=Guid.NewGuid().ToString("N");var id=Guid.NewGuid();
        var world=new World();var owner=new EntityPlayerLocal{entityId=7,world=world,Creation=creation};world.Player=owner;
        var sender=new ClientInfo{entityId=7};var manager=new ConnectionManager{IsServer=true};manager.Clients.Values[7]=sender;
        SingletonMonoBehaviour<ConnectionManager>.Instance=manager;GameManager.Instance=new GameManager{World=world};
        var result=new NetPackageRebirthMetabolismConsumeResult{Owner=7,Id=id,Before=new ItemStack(new ItemValue{type=1,Seed=42},2),After=new ItemStack(new ItemValue{type=1,Seed=42},1)};
        Check(RebirthConsumptionPublicationRetry.Retain(world,owner,result),"prepares actual retained record");
        var ack=new NetPackageRebirthConsumptionAcknowledgement().Setup(7,creation,id);ack.Sender=sender;
        ack.ProcessPackage(world,GameManager.Instance);Check(!Acknowledged(),"default-off protocol refuses ACK");
        RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled=true;
        ack.Sender=new ClientInfo{entityId=8};ack.ProcessPackage(world,GameManager.Instance);Check(!Acknowledged(),"foreign sender refused");
        ack.Sender=new ClientInfo{entityId=7};ack.ProcessPackage(world,GameManager.Instance);Check(!Acknowledged(),"stale sender reference refused");ack.Sender=sender;
        GameManager.Instance.World=new World();ack.ProcessPackage(world,GameManager.Instance);Check(!Acknowledged(),"noncurrent world refused");GameManager.Instance.World=world;
        manager.IsServer=false;ack.ProcessPackage(world,GameManager.Instance);Check(!Acknowledged(),"nonserver manager refused");manager.IsServer=true;
        ThreadManager.Main=false;ack.ProcessPackage(world,GameManager.Instance);ThreadManager.Main=true;Check(!Acknowledged(),"offthread ACK refused");
        var wrong=new NetPackageRebirthConsumptionAcknowledgement().Setup(7,Guid.NewGuid().ToString("N"),id);wrong.Sender=sender;wrong.ProcessPackage(world,GameManager.Instance);Check(!Acknowledged(),"foreign character refused");
        var unknown=new NetPackageRebirthConsumptionAcknowledgement().Setup(7,creation,Guid.NewGuid());unknown.Sender=sender;unknown.ProcessPackage(world,GameManager.Instance);Check(!Acknowledged(),"unknown publication refused");
        world.Player=new EntityPlayerLocal{entityId=7,world=world,Creation=creation};ack.ProcessPackage(world,GameManager.Instance);Check(!Acknowledged(),"same ID replacement player refused");world.Player=owner;
        byte[] wire=Write(ack);var decoded=Read(wire);decoded.Sender=sender;decoded.ProcessPackage(world,GameManager.Instance);
        Check(Acknowledged(),"actual ACK wire handler confirms retained owner");
        decoded.ProcessPackage(world,GameManager.Instance);Check(Acknowledged(),"duplicate authenticated ACK idempotent");
        Check(!RebirthConsumptionPublicationRetry.TryGetRetry(world,owner,id,out _),"ACK disables retry only");
        Rejected(wire.TakePrefix(wire.Length-1),"truncated operation GUID rejected");
        var empty=(byte[])wire.Clone();Array.Clear(empty,empty.Length-16,16);Rejected(empty,"empty operation GUID rejected");
        Check(RebirthConsumptionPublicationRetry.ClearAcknowledgedWorld(world),"explicit acknowledged cleanup");

        var client=new World{Remote=true};var player=new EntityPlayerLocal{entityId=7,world=client,Creation=creation};client.Player=player;
        player.bag.ItemGrid.items=new[]{result.Before.Clone()};GameManager.Instance.World=client;manager.IsServer=false;
        RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled=false;
        Check(!RebirthConsumptionAcknowledgement.TrySend(client,result,creation)&&player.bag.Writes==0,"default-off owner helper writes nothing");
        RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled=true;
        Check(RebirthConsumptionAcknowledgement.TrySend(client,result,creation)&&manager.Sent==1&&player.bag.Writes==1,"owner helper applies once and sends ACK");
        player.bag.ItemGrid.items[0]=new ItemStack(new ItemValue{type=9},5);
        Check(RebirthConsumptionAcknowledgement.TrySend(client,result,creation)&&manager.Sent==2&&player.bag.Writes==1,"known receipt duplicate ACK after movement does not write");
        var moved=new NetPackageRebirthMetabolismConsumeResult{Owner=7,Id=Guid.NewGuid(),Before=result.Before.Clone(),After=result.After.Clone()};
        Check(!RebirthConsumptionAcknowledgement.TrySend(client,moved,creation)&&manager.Sent==2&&player.bag.Writes==1,"unknown moved source refuses setter and ACK");
        Check(NetPackageRebirthMetabolismConsumeResult.ProcessCalls==0&&Effects.Calls==0,"ACK never replays processing/effects");
        RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled=false;
        Console.WriteLine("PASS "+checks+" SIMULATION ONLY: actual ACK/retry/owner/creation-wire sources, native authority/world/transport/result-codec doubles. No sockets, native save or gameplay qualification.");
    }
    static byte[] Write(NetPackageRebirthConsumptionAcknowledgement packet){using var stream=new MemoryStream();using var writer=new PooledBinaryWriter();writer.SetBaseStream(stream);packet.write(writer);writer.Flush();return stream.ToArray();}
    static NetPackageRebirthConsumptionAcknowledgement Read(byte[] wire){using var stream=new MemoryStream(wire);using var reader=new PooledBinaryReader();reader.SetBaseStream(stream);reader.ReadUInt16();var packet=new NetPackageRebirthConsumptionAcknowledgement();packet.read(reader);return packet;}
    static void Rejected(byte[] wire,string label){try{Read(wire);}catch(InvalidDataException){Check(true,label);return;}throw new Exception(label);}
}
static class Bytes{public static byte[] TakePrefix(this byte[] source,int count){var result=new byte[count];Array.Copy(source,result,count);return result;}}
namespace UnityEngine.Scripting{[AttributeUsage(AttributeTargets.Class)]public sealed class PreserveAttribute:Attribute{}}
public enum NetPackageDirection{ToServer,ToClient}
public abstract class NetPackage
{
    public ClientInfo Sender;public abstract NetPackageDirection PackageDirection{get;}public abstract void read(PooledBinaryReader reader);public virtual void write(PooledBinaryWriter writer)=>writer.Write((ushort)99);public abstract void ProcessPackage(World world,GameManager callbacks);public bool ValidEntityIdForSender(int owner)=>Sender?.entityId==owner;
}
public sealed class ClientInfo{public int entityId;}
sealed class Clients{public Dictionary<int,ClientInfo> Values=new Dictionary<int,ClientInfo>();public ClientInfo ForEntityId(int owner)=>Values.TryGetValue(owner,out var sender)?sender:null;}
sealed class ConnectionManager{public bool IsServer;public Clients Clients=new Clients();public int Sent;public void SendToServer(NetPackage packet){Sent++;}}
static class SingletonMonoBehaviour<T>{public static T Instance;}
// GameManager boundary is shared from ExplicitRetryDoubles.cs.
static class NetPackageManager{public static T GetPackage<T>() where T:new()=>new T();}

