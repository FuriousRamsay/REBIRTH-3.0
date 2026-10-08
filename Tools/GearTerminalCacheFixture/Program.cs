using System;using System.Xml.Linq;using System.Security.Cryptography;using System.Text;
public class EntityPlayerLocal {public World world;public int entityId=7;public bool Dead;public bool IsSpawned()=>true;public bool IsDead()=>Dead;}
public class World {public EntityPlayerLocal Player;public bool Remote=true;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public object GetEntity(int id)=>id==7?Player:null;}
public class GameManager {public static GameManager Instance;public World World;public Identity Identity=new();public Identity getPersistentPlayerID(object o)=>Identity;}
public class Identity {public string CombinedString="owner";}
public class Connection {public bool Disconnected;public bool IsDisconnected()=>Disconnected;}
public class ConnectionManager {public bool IsServer;public Connection[] connectionToServer={new()};}
public class SingletonMonoBehaviour<T> {public static T Instance;}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public enum EnumGamePrefs {GameGuidClient}
public static class GamePrefs {public static string SavedWorld;public static string GetString(EnumGamePrefs p)=>SavedWorld;}
public static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld()=>Enabled;}
public static class RebirthSurvivorClientState {public static string Creation;public static string GetProjectedCreationId(EntityPlayerLocal p)=>Creation;}
public class RebirthStablePlayerIdentity {public string CanonicalId="owner";public static bool Available=true;public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity i){i=new();return Available;}}
public class RebirthGearPreparationIntent {public string CreationId;public Guid TransactionId;public long ExpectedRevision=2;}
public class RebirthGearInventorySnapshot {}
public static class RebirthGearPreparationMarker {public static string Marker="original";public static Guid Transaction;public static bool TryRead(string key,float value,out Guid world,out int slots,out RebirthGearPreparationIntent intent){world=Guid.Parse(GamePrefs.SavedWorld);slots=4;intent=new(){CreationId=RebirthSurvivorClientState.Creation,TransactionId=Transaction};return key==Marker&&value==1;}}
public static class RebirthGearPreparationPlayerFileWitness {public static bool Saved=true;public static int Reads;public static Action OnRead;public static bool TryRead(RebirthStablePlayerIdentity owner,Guid world,out string marker,out RebirthGearPreparationIntent intent,out RebirthGearInventorySnapshot snapshot){Reads++;OnRead?.Invoke();marker=RebirthGearPreparationMarker.Marker;intent=new(){CreationId=RebirthSurvivorClientState.Creation};snapshot=new();return Saved;}}
public class RebirthGearTransferState {public string CreationId,TransactionId,PreparationRequestDigest;public long ExpectedRevision=2;}
class Program {
 static int checks;static World world;static RebirthGearSettlement terminal;static ConnectionManager manager;
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);checks++;}
 static void Reset(){RebirthGearTerminalClient.Reset();world=new();world.Player=new(){world=world};GameManager.Instance=new(){World=world};manager=new();SingletonMonoBehaviour<ConnectionManager>.Instance=manager;GamePrefs.SavedWorld=Guid.NewGuid().ToString("N");RebirthSurvivorClientState.Creation=Guid.NewGuid().ToString("N");RebirthGearPreparationMarker.Transaction=Guid.NewGuid();RebirthGearPreparationPlayerFileWitness.Saved=true;RebirthGearPreparationPlayerFileWitness.Reads=0;RebirthGearPreparationPlayerFileWitness.OnRead=null;ThreadManager.Main=true;RebirthStablePlayerIdentity.Available=true;
 var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RebirthGearPreparationMarker.Marker))).ToLowerInvariant();terminal=RebirthGearSettlement.Create(new(){CreationId=RebirthSurvivorClientState.Creation,TransactionId=RebirthGearPreparationMarker.Transaction.ToString("N"),PreparationRequestDigest=digest},true);}
 static bool Receive()=>RebirthGearTerminalClient.Receive(world,7,terminal);
 static void Main(){
 Reset();Check(Receive()&&RebirthGearPreparationPlayerFileWitness.Reads==1,"original saved marker admits immutable cache");RebirthGearPreparationPlayerFileWitness.Saved=false;Check(Receive()&&RebirthGearPreparationPlayerFileWitness.Reads==1,"exact duplicate after retirement retains original admission without missing marker reread");
 Check(RebirthGearTerminalClient.TryGetCurrent(world.Player,out var marker,out var retained)&&marker=="original"&&retained.Applied,"original cached proof retained");
 var node=new XElement("support",terminal.Write());node.Element("gearSettlement").SetAttributeValue("applied",false);RebirthGearSettlement.TryRead(node,3,out var altered);Check(!RebirthGearTerminalClient.Receive(world,7,altered)&&RebirthGearTerminalClient.TryGetCurrent(world.Player,out _,out retained)&&retained.Applied,"altered same original terminal cannot replace cached outcome");
 manager.connectionToServer[0]=new();Check(!Receive()&&!RebirthGearTerminalClient.TryGetCurrent(world.Player,out _,out _),"replacement native connection cannot use retired marker cache");
 Reset();RebirthGearPreparationPlayerFileWitness.Saved=false;Check(!Receive(),"new admission cannot bypass exact saved original marker");
 Reset();RebirthGearPreparationPlayerFileWitness.OnRead=()=>manager.connectionToServer[0]=new();Check(!Receive(),"session changed during file lookup cannot admit");
 Reset();Receive();GamePrefs.SavedWorld=Guid.NewGuid().ToString("N");RebirthGearPreparationPlayerFileWitness.Saved=false;Check(!Receive()&&!RebirthGearTerminalClient.TryGetCurrent(world.Player,out _,out _),"changed server saved world cannot use cache");
 Reset();Receive();GameManager.Instance.Identity.CombinedString="foreign";Check(!Receive(),"foreign platform owner cannot retry terminal");
 Reset();Receive();world.Player.Dead=true;Check(!Receive(),"dead actor cannot retry");
 Reset();Receive();ThreadManager.Main=false;Check(!Receive()&&!RebirthGearTerminalClient.TryGetCurrent(world.Player,out _,out _),"nonmain cache access refuses");
 Reset();Receive();RebirthGearTerminalClient.Reset();RebirthGearPreparationPlayerFileWitness.Saved=false;Check(!Receive(),"reset does not restore cache authority");
 Check(!RebirthGearTerminalClient.TryGetCurrent(null,out _,out _),"null cache owner refuses");
 Console.WriteLine("PASS "+checks+" actual terminal cache/settlement/wire checks; native world/session/identity and marker-file boundaries doubled. No native cleanup or hold release.");
 }
}