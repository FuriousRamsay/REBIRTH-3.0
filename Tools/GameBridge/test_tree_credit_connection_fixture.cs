using System;
// Native world, RNG, connection and ledger doubles; production method extracted by runner.
public class World {public bool Remote=true;public bool IsRemote(){return Remote;}}
public class EntityPlayerLocal {public World world=new World();public int entityId=42;}
public class ItemValue {public int type=3;public bool Wood=true;}
public struct Vector3i {}
public class Block {public string GetBlockName(){return "treeOak";}}
public struct BlockValue {public Block Block;public int type;}
public enum BlockValueRefType {Block,Prop}
public class HitRef {public BlockValueRefType Type;}
public class Details {public HitRef hitRef=new HitRef();public BlockValue blockBeingDamaged=new BlockValue{Block=new Block(),type=9};public int damageMax=100;public float damageTotalOfTarget=50,damageGiven=10;public Vector3i raycastHitPosition;}
public class InvData {public object holdingEntity=new EntityPlayerLocal();public ItemValue itemValue=new ItemValue();}
public class ItemActionData {public InvData invData=new InvData();public Details attackDetails=new Details();}
public static class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld(){return true;}}
public class GameRandom {public int Reads;public float Value;public float RandomFloat{get{Reads++;return Value;}}public void SetSeed(int n){}}
public class GameRandomManager {public static GameRandomManager Instance=new GameRandomManager();public GameRandom CreateGameRandom(){return new GameRandom();}}
public static class Utils {public static float FastMin(float a,float b){return Math.Min(a,b);}}
public static class Mathf {public static int Min(int a,int b){return Math.Min(a,b);}}
public class Channel {public bool Down;public bool IsDisconnected(){return Down;}}
public class ConnectionManager {public bool IsServer,IsConnected=true;public Channel[] Channels=new[]{new Channel()};public int Sends;public NetPackageRebirthTreeWoodCreditRequest Sent;public Channel[] GetConnectionToServer(){return Channels;}public void SendToServer(NetPackageRebirthTreeWoodCreditRequest p){Sends++;Sent=p;}}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public class NetPackageRebirthTreeWoodCreditRequest {public int Channel,Count,Owner;public NetPackageRebirthTreeWoodCreditRequest Setup(int p,Vector3i v,string n,int b,int t,int c){Owner=p;Count=c;return this;}}
public static class NetPackageManager {public static int Channel;public static bool MissingMapping,FactoryFailure;public static int GetPackageId(Type type){if(MissingMapping)throw new Exception("mapping");return 1;}public static T GetPackage<T>() where T:new(){if(FactoryFailure)throw new Exception("factory");return (T)(object)new NetPackageRebirthTreeWoodCreditRequest{Channel=Channel};}}
public static class Subject {
 public static int HostCredits;
 public static bool IsWood(ItemValue v){return v!=null&&v.Wood;}
 public static bool IsTree(BlockValue v){return v.Block!=null;}
 public static void SubmitTreeWoodCredit(EntityPlayerLocal p,Vector3i v,string n,int b,int t,int c){HostCredits+=c;}
 // PRODUCTION_CLASS
}
public static class Test {
 static void Check(bool v,string n){if(!v)throw new Exception(n);}
 static bool Call(ItemActionData a,ref GameRandom r,int amount=4,float probability=1){return Subject.TryWithholdTreeWood(a,new ItemValue(),amount,probability,false,ref r);}
 static void Refuses(ConnectionManager c,int channel,string name){SingletonMonoBehaviour<ConnectionManager>.Instance=c;NetPackageManager.Channel=channel;var a=new ItemActionData();GameRandom r=null;Check(!Call(a,ref r)&&r==null,name+" untouched RNG");r=new GameRandom();Check(!Call(a,ref r)&&r.Reads==0,name+" existing RNG");if(c!=null)Check(c.Sends==0,name+" no send");}
 public static void Main(){
  Refuses(null,0,"missing manager");Refuses(new ConnectionManager{IsConnected=false},0,"disconnected");Refuses(new ConnectionManager{IsServer=true},0,"wrong role");Refuses(new ConnectionManager{Channels=null},0,"missing channels");Refuses(new ConnectionManager(),-1,"negative channel");Refuses(new ConnectionManager(),1,"channel bounds");Refuses(new ConnectionManager{Channels=new Channel[]{null}},0,"missing channel");Refuses(new ConnectionManager{Channels=new[]{new Channel{Down=true}}},0,"closed channel");
  NetPackageManager.MissingMapping=true;Refuses(new ConnectionManager(),0,"missing packet mapping");NetPackageManager.MissingMapping=false;NetPackageManager.FactoryFailure=true;Refuses(new ConnectionManager(),0,"packet factory failure");NetPackageManager.FactoryFailure=false;
  var zeroConnection=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=zeroConnection;NetPackageManager.Channel=0;var zeroAction=new ItemActionData();var zeroRandom=new GameRandom();Check(Call(zeroAction,ref zeroRandom,0)&&zeroRandom.Reads==1&&zeroConnection.Sends==0,"zero-count slice preserves native probability draw without credit");
  var c=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=c;NetPackageManager.Channel=0;var a=new ItemActionData();var r=new GameRandom();Check(Call(a,ref r)&&r.Reads==1&&c.Sends==1&&c.Sent.Count==4&&c.Sent.Owner==42,"connected exact credit");
  a.attackDetails.hitRef.Type=BlockValueRefType.Prop;r=new GameRandom();Check(!Call(a,ref r)&&r.Reads==0&&c.Sends==1,"prop native fallback");
  a.attackDetails.hitRef.Type=BlockValueRefType.Block;((EntityPlayerLocal)a.invData.holdingEntity).world.Remote=false;SingletonMonoBehaviour<ConnectionManager>.Instance=null;r=new GameRandom();Check(Call(a,ref r)&&Subject.HostCredits==4,"host no client transport");
  Console.WriteLine("PASS: production tree withholding method; unavailable transport preserves RNG/native path, connected exact credit, prop fallback and host path (native doubles)");
 }
}
