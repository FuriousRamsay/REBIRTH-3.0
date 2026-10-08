using System;using System.IO;
namespace UnityEngine.Scripting {public class PreserveAttribute:Attribute{}}
public enum NetPackageDirection{ToServer,ToClient}
public class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s,System.Text.Encoding.UTF8,true){}}
public class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s,System.Text.Encoding.UTF8,true){}}
public abstract class NetPackage{
 public ClientInfo Sender;public abstract NetPackageDirection PackageDirection{get;}public abstract void read(PooledBinaryReader r);public virtual void write(PooledBinaryWriter w){w.Write((ushort)0);}public abstract void ProcessPackage(World w,GameManager g);
 public bool ValidEntityIdForSender(int id)=>Sender!=null&&Sender.EntityId==id;
}
public class ClientInfo{public int EntityId=7;}
public class EntityPlayer{public int entityId=7;}
public class World{public bool Remote;public EntityPlayer Player=new();public bool IsRemote()=>Remote;public object GetEntity(int id)=>id==Player.entityId?Player:null;}
public class GameManager{public static GameManager Instance;public World World;}
public class SingletonMonoBehaviour<T>{public static T Instance;}
public class ConnectionManager{public bool IsServer=true;public int Sends,Attached;public NetPackageRebirthGearSettled Last;public void SendPackage(NetPackageRebirthGearSettled p,int _attachedToEntityId){Sends++;Attached=_attachedToEntityId;Last=p;}}
public static class NetPackageManager{public static bool Missing;public static int GetPackageId(Type t){if(Missing)throw new InvalidOperationException("mapping");return 1;}public static T GetPackage<T>()where T:new()=>new();}
public class RebirthGearSettlement{public bool Applied;public string CreationId,TransactionId;public long GearRevision=6;}
public class NetPackageRebirthGearSettled{public int Id;public RebirthGearSettlement Value;public NetPackageRebirthGearSettled Setup(int id,RebirthGearSettlement v){Id=id;Value=v;return this;}}
public static class RebirthRemoteGearAppliedConfirmation{
 public static bool Ready,CancelAllowed=true,Applied;public static int Cancels,Queries;public static Action OnCancel;
 public static string Creation,Transaction;public static ClientInfo OriginalSender;
 public static bool TryGetSettlement(EntityPlayer p,ClientInfo sender,string c,string t,out RebirthGearSettlement result){Queries++;result=null;if(!Ready||p==null||sender!=OriginalSender||c!=Creation||t!=Transaction)return false;result=new(){Applied=Applied,CreationId=c,TransactionId=t};return true;}
 public static bool TryCancelRejected(EntityPlayer p,ClientInfo sender,string c,string t){Cancels++;OnCancel?.Invoke();if(!CancelAllowed||p==null||sender!=OriginalSender||c!=Creation||t!=Transaction)return false;Ready=true;return true;}
}
class Program{
 static int checks;static World world;static ConnectionManager manager;static ClientInfo sender;static string creation;static Guid tx;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
 static void Throws(Action action,string label){bool thrown=false;try{action();}catch(ArgumentException){thrown=true;}catch(InvalidDataException){thrown=true;}catch(EndOfStreamException){thrown=true;}catch(InvalidOperationException){thrown=true;}Check(thrown,label);}
 static void Reset(){world=new();GameManager.Instance=new(){World=world};manager=new();SingletonMonoBehaviour<ConnectionManager>.Instance=manager;sender=new();creation=Guid.NewGuid().ToString("N");tx=Guid.NewGuid();NetPackageManager.Missing=false;RebirthRemoteGearAppliedConfirmation.Ready=false;RebirthRemoteGearAppliedConfirmation.CancelAllowed=true;RebirthRemoteGearAppliedConfirmation.Applied=false;RebirthRemoteGearAppliedConfirmation.Cancels=RebirthRemoteGearAppliedConfirmation.Queries=0;RebirthRemoteGearAppliedConfirmation.OnCancel=null;RebirthRemoteGearAppliedConfirmation.Creation=creation;RebirthRemoteGearAppliedConfirmation.Transaction=tx.ToString("N");RebirthRemoteGearAppliedConfirmation.OriginalSender=sender;}
 static NetPackageRebirthGearRejectedRequest Request(){var p=new NetPackageRebirthGearRejectedRequest().Setup(7,creation,tx);p.Sender=sender;return p;}
 static byte[] Encode(NetPackageRebirthGearRejectedRequest p){using var s=new MemoryStream();using var w=new PooledBinaryWriter(s);p.write(w);return s.ToArray();}
 static NetPackageRebirthGearRejectedRequest Decode(byte[] bytes){using var s=new MemoryStream(bytes);using var r=new PooledBinaryReader(s);r.ReadUInt16();var p=new NetPackageRebirthGearRejectedRequest();p.read(r);Check(s.Position==s.Length,"exact wire consumed");p.Sender=sender;return p;}
 static void Main(){
 Reset();var p=Request();var wire=Encode(p);Check(p.PackageDirection==NetPackageDirection.ToServer&&wire.Length==p.GetLength(),"direction and exact native frame length");Decode(wire).ProcessPackage(world,GameManager.Instance);Check(manager.Sends==1&&manager.Attached==7&&!manager.Last.Value.Applied&&manager.Last.Value.TransactionId==tx.ToString("N")&&RebirthRemoteGearAppliedConfirmation.Cancels==1,"roundtrip authentic rejected request settles exact identity");
 Request().ProcessPackage(world,GameManager.Instance);Check(manager.Sends==2&&RebirthRemoteGearAppliedConfirmation.Cancels==1,"terminal replay does not cancel twice");
 Reset();creation="legacy-"+new string('a',64);RebirthRemoteGearAppliedConfirmation.Creation=creation;p=Request();wire=Encode(p);Check(wire.Length==p.GetLength(),"migrated full identity frame length");Decode(wire).ProcessPackage(world,GameManager.Instance);Check(manager.Sends==1&&manager.Last.Value.CreationId==creation,"migrated identity not truncated");
 Reset();Throws(()=>new NetPackageRebirthGearRejectedRequest().Setup(7,creation,Guid.Empty),"empty transaction refused");Throws(()=>new NetPackageRebirthGearRejectedRequest().Setup(7,"bad",tx),"bad creation refused");Throws(()=>Encode(new()),"uninitialized package cannot write");
 p=Request();wire=Encode(p);Array.Resize(ref wire,wire.Length-1);Throws(()=>Decode(wire),"truncated transaction refused");
 Reset();p=Request();p.Sender=null;p.ProcessPackage(world,GameManager.Instance);Check(manager.Sends==0&&RebirthRemoteGearAppliedConfirmation.Cancels==0,"missing sender refused before cancellation");
 Reset();p=Request();sender.EntityId=8;p.ProcessPackage(world,GameManager.Instance);Check(manager.Sends==0&&RebirthRemoteGearAppliedConfirmation.Cancels==0,"foreign entity refused before cancellation");
 Reset();p=Request();world.Remote=true;p.ProcessPackage(world,GameManager.Instance);Check(manager.Sends==0&&RebirthRemoteGearAppliedConfirmation.Cancels==0,"remote world refused");
 Reset();p=Request();GameManager.Instance.World=new();p.ProcessPackage(world,GameManager.Instance);Check(manager.Sends==0&&RebirthRemoteGearAppliedConfirmation.Cancels==0,"retired world refused");
 Reset();p=Request();manager.IsServer=false;p.ProcessPackage(world,GameManager.Instance);Check(manager.Sends==0&&RebirthRemoteGearAppliedConfirmation.Cancels==0,"non-authority manager refused");
 Reset();p=Request();RebirthRemoteGearAppliedConfirmation.CancelAllowed=false;p.ProcessPackage(world,GameManager.Instance);Check(manager.Sends==0&&RebirthRemoteGearAppliedConfirmation.Cancels==1,"unverified rejection sends no terminal");
 Reset();p=Request();RebirthRemoteGearAppliedConfirmation.Ready=true;RebirthRemoteGearAppliedConfirmation.Applied=true;p.ProcessPackage(world,GameManager.Instance);Check(manager.Sends==0&&RebirthRemoteGearAppliedConfirmation.Cancels==0,"applied terminal cannot answer rejected request");
 Reset();p=Request();RebirthRemoteGearAppliedConfirmation.OnCancel=()=>SingletonMonoBehaviour<ConnectionManager>.Instance=new();p.ProcessPackage(world,GameManager.Instance);Check(manager.Sends==0,"manager replacement while saving prevents stale reply");
 Reset();p=Request();NetPackageManager.Missing=true;Throws(()=>p.ProcessPackage(world,GameManager.Instance),"missing terminal mapping refuses");Check(RebirthRemoteGearAppliedConfirmation.Cancels==0&&manager.Sends==0,"mapping refused before cancellation effect");
 Console.WriteLine("PASS "+checks+" actual rejected request/creation wire checks; native transport, authentication and saved-terminal service are doubles. No native network execution.");
 }
}