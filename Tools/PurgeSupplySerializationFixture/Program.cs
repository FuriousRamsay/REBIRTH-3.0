using System;using System.Collections.Generic;using System.IO;using System.Text;
class Program
{
 static int checks;static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static byte[] Save(EntitySupplyCrate crate,StreamModeWrite mode=StreamModeWrite.Persistency)
 {
  using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream)){writer.Write(123456);RebirthPurgeSupplyCrateSerialization.AfterWrite(crate,writer,mode,true);return stream.ToArray();}}
 }
 static EntitySupplyCrate Load(World world,byte[] bytes,StreamModeRead mode=StreamModeRead.Persistency,bool ran=true)
 {
  var crate=new EntitySupplyCrate{world=world,entityId=10,WorldTimeBorn=42};
  using(var stream=new MemoryStream(bytes))using(var reader=new PooledBinaryReader(stream)){reader.ReadInt32();RebirthPurgeSupplyCrateSerialization.AfterRead(crate,reader,mode,ran);}world.Entities.dict[crate.entityId]=crate;return crate;
 }
 static void Main()
 {
  var guid=Guid.NewGuid();var world=new World{worldState=new WorldState{Guid=guid.ToString("N").ToUpperInvariant()}};GameManager.Instance=new GameManager{World=world};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true};
  var player=new string('a',64);var account=new RebirthPurgeSupplyAccount(player,75,75,1,0,1);RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{player,account}};
  var crate=new EntitySupplyCrate{world=world,entityId=10,WorldTimeBorn=42};world.Entities.dict[10]=crate;var stamp=new RebirthPurgeSupplyCrateStamp(guid,player,1,account.Token(guid));
  Check(RebirthPurgeSupplyCrateSerialization.TryAttach(crate,stamp,new object(),()=>true),"original registered crate binds exact durable account delivery");
  Check(!RebirthPurgeSupplyCrateSerialization.TryReadSaved(crate,guid,out _),"attached live crate alone cannot prove a completed native save");
  var bytes=Save(crate);Check(bytes.Length==4+77,"actual serialization hook appends only owned fixed-size trailer");
  var cold=Load(world,bytes);Check(RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(cold,guid,out var restored)&&restored.Token==stamp.Token,"native persistency origin restores original authoritative ownership");
  Check(RebirthPurgeSupplyCrateSerialization.TryReadSaved(cold,guid,out _),"original registered native persistency read supplies cold recovery evidence");
  world.Entities.dict[10]=crate;Check(!RebirthPurgeSupplyCrateSerialization.TryReadSaved(cold,guid,out _),"reused runtime ID cannot authorize a stale saved instance");world.Entities.dict[10]=cold;
  Check(Save(cold).Length==bytes.Length,"reloaded crate carries stamp into subsequent native serialization");
  Check(!RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(Load(world,bytes,StreamModeRead.FromClient),guid,out _),"client-origin data cannot mint server crate ownership");
  Check(!RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(Load(world,bytes,StreamModeRead.FromServer),guid,out _),"network projection is not authoritative saved-crate evidence");
  Check(!RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(Load(world,bytes,StreamModeRead.Persistency,false),guid,out _),"suppressed native read cannot establish saved-crate proof");
  Check(Save(crate,StreamModeWrite.ToServer).Length==4,"outbound client stream does not transmit authoritative stamp");
  Check(!RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(Load(world,new byte[]{1,2,3,4}),guid,out _),"legacy native-only crate remains readable without ownership assertion");
  crate.WorldTimeBorn=43;Check(Save(crate).Length==4,"changed native entity birth cannot inherit old ownership");crate.WorldTimeBorn=42;
  world.worldState.Guid=Guid.NewGuid().ToString("N");Check(Save(crate).Length==4,"replaced world cannot serialize old crate ownership");world.worldState.Guid=guid.ToString("N").ToUpperInvariant();
  ThreadManager.Main=false;Check(Save(crate).Length==bytes.Length,"native save worker retains immutable original stamp");ThreadManager.Main=true;
  RebirthPurgeSupplyService.Published=null;Check(Save(crate).Length==bytes.Length,"shutdown account teardown does not erase final crate save stamp");
  RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>();var other=new EntitySupplyCrate{world=world,entityId=11,WorldTimeBorn=42};world.Entities.dict[11]=other;
  Check(!RebirthPurgeSupplyCrateSerialization.TryAttach(other,stamp,new object(),()=>true),"missing durable entitlement cannot bind native crate");
  RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{player,account}};
  var beforeSpawn=new EntitySupplyCrate{world=world,entityId=12,WorldTimeBorn=45};
  Check(RebirthPurgeSupplyCrateSerialization.TryAttachBeforeSpawn(beforeSpawn,stamp,new object(),()=>true),"original scoped crate binds stamp before native callbacks and broadcast");
  Check(!RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(beforeSpawn,guid,out _),"unregistered pre-spawn tag alone cannot establish native authority");
  world.Entities.dict[12]=beforeSpawn;
  Check(RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(beforeSpawn,guid,out _),"actual registration activates exact pre-spawn owned stamp");
  Check(!RebirthPurgeSupplyCrateSerialization.TryAttachBeforeSpawn(beforeSpawn,stamp,new object(),()=>true),"existing registered entity cannot be retagged as a newly scoped crate");
  var projection=Load(world,bytes,StreamModeRead.FromServer);
  Check(RebirthPurgeSupplyCrateSerialization.TryReadProjection(projection,out var projected)&&projected.Player==player&&!RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(projection,guid,out _),"server-origin projection displays original owner without acquiring reward authority");
  var forged=Load(world,bytes,StreamModeRead.FromClient);
  Check(!RebirthPurgeSupplyCrateSerialization.TryReadProjection(forged,out _),"client-origin trailer cannot supply even owned-crate projection");
  var current=new RebirthPurgeSupplyAccount(player,75,75,1);current.TryReserve(new RebirthPurgeSupplyDeliveryPlan(14,90,-993),out current);
  RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{player,current}};
  var qualified=new EntitySupplyCrate{world=world,entityId=13,WorldTimeBorn=55};world.Entities.dict[13]=qualified;
  Check(!RebirthPurgeSupplyCrateSerialization.TryAttach(qualified,stamp,new object(),()=>false),"inactive native reservation cannot bind an owned crate");
  current.TryLaunched(guid,current.Token(guid),out current);RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{player,current}};
  Check(RebirthPurgeSupplyCrateSerialization.TryAttach(qualified,stamp,new object(),()=>true),"accepted native launch binds its crate after account consumption");
  RebirthSandboxOptionManager.Current.IsPurge=false;Check(!RebirthPurgeSupplyCrateSerialization.TryReadSaved(cold,guid,out _),"None QoL cannot use saved Purge crate as authoritative delivery evidence");Check(Save(crate).Length==4,"None QoL does not append Purge crate metadata");
  Console.WriteLine("RESULT "+checks+" PASS; production stamp and serialization helpers; binary roundtrip real; native caller/Harmony/engine doubles; no native save commit or flight proof.");
 }
}
enum StreamModeRead {Persistency,FromServer,FromClient} enum StreamModeWrite {Persistency,ToServer,ToClient}
class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream stream):base(stream,Encoding.UTF8,true){}}
class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream stream):base(stream,Encoding.UTF8,true){}}
class Entity {public World world;public int entityId;public ulong WorldTimeBorn;}
class EntitySupplyCrate:Entity {public void Write(PooledBinaryWriter writer,StreamModeWrite mode){}public void Read(byte version,PooledBinaryReader reader,StreamModeRead mode){}}
class WorldState {public string Guid;} class EntityList {public Dictionary<int,Entity> dict=new Dictionary<int,Entity>();}
class World {public WorldState worldState;public EntityList Entities=new EntityList();public bool IsRemote()=>false;}
class GameManager {public static GameManager Instance;public World World;} class ConnectionManager {public bool IsServer;}
class SingletonMonoBehaviour<T> {public static T Instance;} class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
class Log {public static void Warning(string value){}}
internal static class RebirthPurgeReleasePolicy {internal static bool Enabled=true;}
internal sealed class RebirthSandboxOptionManager {internal static readonly RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool IsPurge=true;}
internal static class RebirthPurgeSupplyService {internal static IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> Published;}
public interface IModApi {void InitMod(Mod mod);} public class Mod {}
namespace UnityEngine.Scripting {class PreserveAttribute:Attribute {}}
namespace HarmonyLib
{
 class Harmony {public Harmony(string id){}public void Patch(object method,object postfix){}}
 class HarmonyMethod {public HarmonyMethod(Type type,string name){}}
 static class AccessTools {public static object Method(Type type,string name,Type[] args)=>new object();}
}