// AUTHORED_NOT_RUN. Actual packages/frame/server sender; native buffers,
// roster/platform and saved enrollment/projection services are explicit adapters.
// ACTUAL_PLAN_SOURCES
namespace UnityEngine.Scripting {public sealed class PreserveAttribute:Attribute {}}
public enum NetPackageDirection {ToServer,ToClient}
public class PooledBinaryReader:BinaryReader {internal PooledBinaryReader(Stream stream):base(stream){}}
public class PooledBinaryWriter:BinaryWriter {internal PooledBinaryWriter(Stream stream):base(stream){}}
public abstract class NetPackage
{
    internal ClientInfo Sender;
    public abstract NetPackageDirection PackageDirection{get;}
    public abstract void read(PooledBinaryReader reader);
    public virtual void write(PooledBinaryWriter writer)=>writer.Write((ushort)19);
    public abstract void ProcessPackage(World world,GameManager game);
    internal bool ValidEntityIdForSender(int id)=>Sender!=null&&Sender.EntityId==id;
}
public class EntityPlayer
{
    internal World world;internal int entityId=1,Died=2;internal bool Dead,Spawned=true;
    internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;
}
public sealed class EntityPlayerLocal:EntityPlayer {}
public sealed class World
{
    internal string Guid=System.Guid.NewGuid().ToString("N");internal bool Remote;internal EntityPlayer Actor;
    internal bool IsRemote()=>Remote;internal EntityPlayer GetEntity(int id)=>Actor?.entityId==id?Actor:null;
}
public sealed class GameManager {internal static GameManager Instance;internal World World;}
public sealed class ClientInfo {internal int EntityId=1;internal bool loginDone=true,disconnecting;}
internal sealed class Clients {internal ClientInfo Peer=new ClientInfo();internal ClientInfo ForEntityId(int id)=>Peer.EntityId==id?Peer:null;}
internal sealed class ConnectionManager
{
    internal bool IsServer=true;internal Clients Clients=new Clients();internal int Sends;internal Action DuringSend;
    internal NetPackageRebirthHydrationGeneration Sent;
    internal void SendPackage(NetPackageRebirthHydrationGeneration packet,int _attachedToEntityId){Sends++;Sent=packet;DuringSend?.Invoke();}
}
internal static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal static class RebirthSurvivorMode {internal static bool Enabled;internal static bool IsEnabledForCurrentWorld()=>Enabled;}
internal sealed class RebirthStablePlayerIdentity
{
    internal string StorageKey=new string('a',64);
    internal static bool TryResolveServerEntity(EntityPlayer player,out RebirthStablePlayerIdentity owner){owner=new RebirthStablePlayerIdentity();return true;}
}
internal static class RebirthMetabolismStateRepository
{
    internal static int Enrollments;internal static string Generation;internal static bool Saved=true;
    internal static bool TryEnsureHydrationGeneration(EntityPlayer player,out string generation)
    {Enrollments++;generation=Generation;return Saved&&ThreadManager.Main&&!RebirthSurvivorMode.Enabled&&player?.world!=null&&ReferenceEquals(player.world,GameManager.Instance.World)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&!player.Dead&&player.Spawned&&SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;}
}
internal static class RebirthHydrationGenerationClient
{
    internal sealed class RequestScope {internal int PlayerId,Deaths;internal Guid WorldId;internal bool Valid=true;internal bool Current()=>Valid;}
    internal static RebirthHydrationGenerationFrame Received;
    internal static bool Receive(World world,RebirthHydrationGenerationFrame frame){Received=frame;return frame!=null;}
}
internal static class NetPackageManager
{
    internal static bool Missing;internal static Action DuringMap;
    internal static int GetPackageId(Type type){DuringMap?.Invoke();if(Missing)throw new InvalidOperationException("fixture missing response mapping");return 19;}
    internal static T GetPackage<T>() where T:new()=>new T();
}
internal static class Program
{
    private static EntityPlayer player;private static World world;private static ConnectionManager manager;
    private static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
    private static void Fresh()
    {
        world=new World();player=new EntityPlayer{world=world};world.Actor=player;GameManager.Instance=new GameManager{World=world};
        manager=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=manager;
        ThreadManager.Main=true;RebirthSurvivorMode.Enabled=false;RebirthMetabolismStateRepository.Generation=Guid.NewGuid().ToString("N");
        RebirthMetabolismStateRepository.Enrollments=0;RebirthMetabolismStateRepository.Saved=true;
        NetPackageManager.Missing=false;NetPackageManager.DuringMap=null;RebirthHydrationGenerationClient.Received=null;
    }
    private static byte[] Encode(NetPackage packet)
    {using(var stream=new MemoryStream()){using(var writer=new PooledBinaryWriter(stream)){packet.write(writer);writer.Flush();return stream.ToArray();}}}
    private static NetPackageRebirthHydrationGenerationRequest Request()
    {
        var outgoing=new NetPackageRebirthHydrationGenerationRequest().Setup(new RebirthHydrationGenerationClient.RequestScope{PlayerId=player.entityId,Deaths=player.Died,WorldId=Guid.Parse(world.Guid)});
        byte[] bytes=Encode(outgoing);Need(bytes.Length==outgoing.GetLength()&&bytes.Length==27,"generation request native envelope length drifted");
        var incoming=new NetPackageRebirthHydrationGenerationRequest();using(var reader=new PooledBinaryReader(new MemoryStream(bytes.Skip(2).ToArray())))incoming.read(reader);
        incoming.Sender=manager.Clients.Peer;return incoming;
    }
    public static void Main()
    {
        Fresh();var request=Request();request.ProcessPackage(world,GameManager.Instance);
        Need(manager.Sends==1&&manager.Sent!=null,"authenticated native generation request did not produce response");
        byte[] response=Encode(manager.Sent);Need(response.Length==manager.Sent.GetLength()&&response.Length==115,"generation response native envelope length drifted");
        var received=new NetPackageRebirthHydrationGeneration();using(var reader=new PooledBinaryReader(new MemoryStream(response.Skip(2).ToArray())))received.read(reader);
        received.ProcessPackage(world,GameManager.Instance);Need(RebirthHydrationGenerationClient.Received.Generation==RebirthMetabolismStateRepository.Generation,"response package lost saved generation");
        foreach(int variant in Enumerable.Range(0,10))
        {
            Fresh();request=Request();switch(variant){case 0:request.Sender=new ClientInfo();break;case 1:request.Sender.EntityId=2;break;case 2:request.Sender.loginDone=false;break;case 3:request.Sender.disconnecting=true;break;case 4:world.Guid=Guid.NewGuid().ToString("N");break;case 5:player.Died++;break;case 6:player.Dead=true;break;case 7:player.Spawned=false;break;case 8:RebirthSurvivorMode.Enabled=true;break;case 9:ThreadManager.Main=false;break;}
            request.ProcessPackage(world,GameManager.Instance);Need(manager.Sends==0&&RebirthMetabolismStateRepository.Enrollments==0,"unqualified request enrolled/sent generation "+variant);
        }
        Fresh();NetPackageManager.Missing=true;Need(!RebirthHydrationGenerationServer.TrySend(player)&&RebirthMetabolismStateRepository.Enrollments==0&&manager.Sends==0,"missing response mapping enrolled generation");
        Fresh();NetPackageManager.DuringMap=()=>manager.Clients.Peer=new ClientInfo();Need(!RebirthHydrationGenerationServer.TrySend(player)&&manager.Sends==0&&RebirthMetabolismStateRepository.Enrollments==0,"peer replacement during mapping enrolled/sent stale generation");
        Fresh();manager.DuringSend=()=>manager.Clients.Peer.disconnecting=true;Need(!RebirthHydrationGenerationServer.TrySend(player)&&manager.Sends==1,"disconnect during send reported current response");
        Fresh();RebirthMetabolismStateRepository.Saved=false;Need(!RebirthHydrationGenerationServer.TrySend(player)&&manager.Sends==0,"unsaved generation sent");
        Fresh();byte[] exactRequest=Encode(new NetPackageRebirthHydrationGenerationRequest().Setup(new RebirthHydrationGenerationClient.RequestScope{PlayerId=1,Deaths=2,WorldId=Guid.Parse(world.Guid)})).Skip(2).ToArray();
        for(int length=0;length<exactRequest.Length;length++){bool refused=false;try{using(var reader=new PooledBinaryReader(new MemoryStream(exactRequest.Take(length).ToArray())))new NetPackageRebirthHydrationGenerationRequest().read(reader);}catch(IOException){refused=true;}catch(InvalidDataException){refused=true;}Need(refused,"truncated generation request accepted");}
        exactRequest[0]=2;bool badVersion=false;try{using(var reader=new PooledBinaryReader(new MemoryStream(exactRequest)))new NetPackageRebirthHydrationGenerationRequest().read(reader);}catch(InvalidDataException){badVersion=true;}Need(badVersion,"unknown generation request version accepted");
        Console.WriteLine("PASS actual generation packages/frame/server sender; native buffers/roster/enrollment/projection adapters, real topology NOT RUN");
    }
}
