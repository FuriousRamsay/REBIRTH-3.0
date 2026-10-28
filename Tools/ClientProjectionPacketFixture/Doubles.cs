using System;
using System.IO;
// Explicit native world/transport/codec/manager doubles. Actual packet classes
// are linked; the fixture qualifies admission/delegation, not persistence/wire IO.
namespace UnityEngine.Scripting {public sealed class PreserveAttribute:Attribute{}}
public enum NetPackageDirection {Both,ToServer,ToClient}
public abstract class NetPackage {public virtual NetPackageDirection PackageDirection=>NetPackageDirection.Both;public abstract void read(PooledBinaryReader r);public virtual void write(PooledBinaryWriter w){}public abstract void ProcessPackage(World world,GameManager callbacks);}
public sealed class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){}}
public sealed class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream s):base(s){}}
public struct Vector3i {public int x,y,z;}
public static class StreamUtils {public static Vector3i ReadVector3i(BinaryReader r)=>new Vector3i{x=r.ReadInt32(),y=r.ReadInt32(),z=r.ReadInt32()};public static void Write(BinaryWriter w,Vector3i p){w.Write(p.x);w.Write(p.y);w.Write(p.z);}}
public static class RebirthSurvivorNetworkCodec {public static string ReadBoundedString(BinaryReader r,int n)=>r.ReadString();public static void WriteString(BinaryWriter w,string s,int n)=>w.Write(s??"");public static int EstimateString(string s,int n)=>(s??"").Length;}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public sealed class ConnectionManager {public bool IsServer;}
public sealed class World {public bool Remote=true;public bool IsRemote()=>Remote;}
public sealed class GameManager {public static GameManager Instance;public World World;}
public static class RebirthTraderQuestGraceManager {public static int Calls;public static void ApplyReconnectSync(int code,string id,long deadline,bool party){Calls++;}}
public static class RebirthWorkstationSecurityService {public static int Calls;public static void ReceiveOwnerSync(Vector3i p,string owner,bool locked,string metadata,string[] users){Calls++;}}
