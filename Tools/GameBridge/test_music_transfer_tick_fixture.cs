using System;
using System.Collections.Generic;
static class Time {public static float realtimeSinceStartup;}
class EntityPlayerLocal {public int entityId=7;public bool IsSpawned(){return true;}}
class World {public EntityPlayerLocal Player=new EntityPlayerLocal();public EntityPlayerLocal GetPrimaryPlayer(){return Player;}public bool IsRemote(){return true;}}
class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
class RebirthMusicTransferState {public string CreationId="owner";}
static class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld(){return true;}}
enum RebirthMusicOwnerTransferResult {Pending,Applied,Rejected}
static class RebirthMusicOwnerTransfer {public static int Calls;public static RebirthMusicOwnerTransferResult Result=RebirthMusicOwnerTransferResult.Applied;public static RebirthMusicOwnerTransferResult Apply(EntityPlayerLocal p,string c,RebirthMusicTransferState o){Calls++;return Result;}}
static class RebirthMusicLibraryClient {public static string CreationId="owner";public static long Revision=1;public static bool PendingTransfer;public static bool Connected=true;public static bool EnsureCurrent(EntityPlayerLocal p){return true;}public static void Dispatch(EntityPlayerLocal p,int op){}public static bool CanSend(ConnectionManager c,object p){return Connected&&p!=null;}}
class NetPackagePlayerData {public NetPackagePlayerData Setup(EntityPlayerLocal p){if(NetPackageManager.FailSetup==1)throw new Exception();return this;}}
class NetPackageRebirthMusicTransferAck {public NetPackageRebirthMusicTransferAck Setup(int id,RebirthMusicTransferState offer,bool applied){if(NetPackageManager.FailSetup==2)throw new Exception();return this;}}
static class NetPackageManager {public static int Factories,FailFactory,FailSetup;public static T GetPackage<T>() where T:new(){if(++Factories==FailFactory)throw new Exception();return new T();}}
class ConnectionManager {public bool IsClient=true,IsServer;public int FailSend;public List<string> Sends=new List<string>();public void SendToServer(object packet){Sends.Add(packet is NetPackagePlayerData?"checkpoint":"ack");if(Sends.Count==FailSend)throw new Exception();}}
class Check {
 static World offerWorld;static int ownerId;static RebirthMusicTransferState pending;static float nextAttempt;
 static void Reset(){pending=null;}
 // SOURCE
 static void Setup(){Time.realtimeSinceStartup=0;nextAttempt=0;offerWorld=GameManager.Instance.World;ownerId=7;pending=new RebirthMusicTransferState();RebirthMusicOwnerTransfer.Calls=0;RebirthMusicOwnerTransfer.Result=RebirthMusicOwnerTransferResult.Applied;NetPackageManager.Factories=0;NetPackageManager.FailFactory=0;NetPackageManager.FailSetup=0;RebirthMusicLibraryClient.Connected=true;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();}
 static void A(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Main(){for(int mode=0;mode<7;mode++){
 Setup();var c=SingletonMonoBehaviour<ConnectionManager>.Instance;
 if(mode<2)NetPackageManager.FailFactory=mode+1;
 if(mode==2)RebirthMusicLibraryClient.Connected=false;
 if(mode==3)c.FailSend=1;if(mode==4)c.FailSend=2;
 if(mode==5)NetPackageManager.FailSetup=1;if(mode==6)NetPackageManager.FailSetup=2;
 Tick();A(pending!=null,"failure lost offer");
 A(RebirthMusicOwnerTransfer.Calls==(mode<3?0:1),"preflight did not precede apply");
 int sends=mode<3||mode==5?0:mode==4?2:1;A(c.Sends.Count==sends,"wrong send boundary");
 if(sends>0)A(c.Sends[0]=="checkpoint","ack before checkpoint");
 c.FailSend=0;c.Sends.Clear();NetPackageManager.FailSetup=0;NetPackageManager.FailFactory=0;RebirthMusicLibraryClient.Connected=true;Time.realtimeSinceStartup=3;Tick();A(c.Sends.Count==2&&c.Sends[1]=="ack","retry failed");
 }
 Setup();RebirthMusicOwnerTransfer.Result=RebirthMusicOwnerTransferResult.Pending;Tick();A(SingletonMonoBehaviour<ConnectionManager>.Instance.Sends.Count==0,"uncertain receipt sent ACK");
 Console.WriteLine("PASS actual music Tick: factory/channel preflight, both setup/send failures, checkpoint-before-ACK retry, Pending sends nothing; owner/network adapters doubled");}
}