// REGISTRY
public struct Vector3i { public int x,y,z; public Vector3i(int a,int b,int c){x=a;y=b;z=c;} }
public class Block { public string GetBlockName(){return "crate";} }
public struct BlockValue {public bool isair; public Block Block;}
public class WorldBase {public BlockValue GetBlock(Vector3i p){return new BlockValue{Block=new Block()};}}
public class World:WorldBase {}
public class TileEntity {public Vector3i ToWorldPos(){return default(Vector3i);}}
public static class QuickStackAcceptedCategoryRegistry {public static TileEntity ResolveTileEntity(WorldBase w,Vector3i p){return null;}}
public static class RebirthContainerRenameService {public static string NormalizeName(string n){return (n??"").Trim();}}
public class ConnectionManager {public bool IsServer;public void SendPackage(object p){}}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public class ClientInfo {public void SendPackage(object p){}}
public static class NetPackageManager {public static T GetPackage<T>() where T:new(){return new T();}}
public class NetPackageRebirthContainerNameSync {public object Setup(bool r,RebirthContainerNameRegistry.Entry[] e){return this;}}
public static class GameIO {public static string GetSaveGameDir(){throw new Exception("Client must not read server saves");}}
public static class Log {public static void Warning(string s){}}
public static class RebirthDurableFileCommit {public static bool TryPublish(string a,string b,out string e){e="";return true;}}
public static class Checks {
 static Vector3i p=new Vector3i(1,2,3);static World world=new World();
 static RebirthContainerNameRegistry.Entry[] Entry(string name){return new[]{new RebirthContainerNameRegistry.Entry{Position=p,BlockName="crate",Name=name}};}
 static void Equal(string expected){if(RebirthContainerNameRegistry.Get(world,p)!=expected)throw new Exception("Expected "+expected);}
 public static void Main(){
 RebirthContainerNameRegistry.Reset(false);
 RebirthContainerNameRegistry.ApplyNetworkSnapshot(false,Entry("First delta"));Equal("First delta");
 if(RebirthContainerNameRegistry.Snapshot().Length!=1)throw new Exception("Snapshot lost delta");
 RebirthContainerNameRegistry.ApplyNetworkSnapshot(true,Entry("Full snapshot"));Equal("Full snapshot");
 RebirthContainerNameRegistry.ApplyNetworkSnapshot(false,Entry("Renamed"));Equal("Renamed");
 RebirthContainerNameRegistry.ApplyNetworkSnapshot(false,Entry(""));Equal("");
 RebirthContainerNameRegistry.ApplyNetworkSnapshot(false,Entry("Old world"));
 RebirthContainerNameRegistry.Reset(false);Equal("");
 RebirthContainerNameRegistry.ApplyNetworkSnapshot(false,Entry("New world"));Equal("New world");
 RebirthContainerNameRegistry.ApplyNetworkSnapshot(true,null);Equal("");
 RebirthContainerNameRegistry.Reset(true);
 RebirthContainerNameRegistry.ApplyNetworkSnapshot(true,Entry("Must ignore"));
 var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
 var names=(System.Collections.IDictionary)typeof(RebirthContainerNameRegistry).GetField("names",flags).GetValue(null);
 if(names.Count!=0 || (bool)typeof(RebirthContainerNameRegistry).GetField("loaded",flags).GetValue(null))throw new Exception("Network changed server registry");
 System.Console.WriteLine("PASS: delta before first lookup, snapshot/delta/delete/reset, server isolation");
 }
}
