using System;
using System.Runtime.CompilerServices;

// Native transport lifetime only. A captured connection grants no payment,
// inventory delta, original receipt, saved publication or retry permission.
internal sealed class RebirthRemoteResourceNetworkScope
{
    private readonly World world;private readonly EntityPlayer actor;private readonly int entity;
    private readonly ConnectionManager manager;private readonly ClientInfo peer;private readonly INetConnection connection;private readonly bool local;
    private RebirthRemoteResourceNetworkScope(EntityPlayer value,ConnectionManager current,ClientInfo sender,INetConnection native,bool primary)
    {actor=value;world=value.world;entity=value.entityId;manager=current;peer=sender;connection=native;local=primary;}
    internal static bool IsPacket(NetPackage packet)=>packet is NetPackageRebirthRemoteRepairOriginal||packet is NetPackageRebirthRemoteRepairResult;
    private sealed class Session{internal readonly string Value=Guid.NewGuid().ToString("N");}
    private static readonly ConditionalWeakTable<INetConnection,Session> sessions=new ConditionalWeakTable<INetConnection,Session>();
    internal static string SessionKey(INetConnection connection)=>sessions.GetValue(connection,_=>new Session()).Value;
    internal static RebirthRemoteResourceNetworkScope Capture(EntityPlayer actor)
    {
        var world=GameManager.Instance?.World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||manager?.IsServer!=true||actor==null||
            !ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor))return null;
        if(actor is EntityPlayerLocal&&ReferenceEquals(world.GetPrimaryPlayer(),actor))return new RebirthRemoteResourceNetworkScope(actor,manager,null,null,true);
        var peer=manager.Clients?.ForEntityId(actor.entityId);
        if(peer?.loginDone!=true||peer.disconnecting||peer.netConnection==null||peer.netConnection.Length==0||peer.netConnection[0]==null||peer.netConnection[0].IsDisconnected())return null;
        return new RebirthRemoteResourceNetworkScope(actor,manager,peer,peer.netConnection[0],false);
    }
    internal bool Current=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&world.IsRemote()==false&&
        ReferenceEquals(actor.world,world)&&actor.entityId==entity&&ReferenceEquals(world.GetEntity(entity),actor)&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&manager.IsServer&&
        (local?ReferenceEquals(world.GetPrimaryPlayer(),actor):ReferenceEquals(manager.Clients?.ForEntityId(entity),peer)&&peer.loginDone&&!peer.disconnecting&&
            peer.netConnection?.Length>0&&ReferenceEquals(peer.netConnection[0],connection)&&!connection.IsDisconnected());
    internal static bool Receive(NetPackage packet,World world,GameManager callbacks,bool server,out INetConnection connection)
    {
        connection=null;
        if(!ThreadManager.IsMainThread()||packet==null||packet.Channel!=0||world==null||world.IsRemote()==server||
            !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(GameManager.Instance,callbacks)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var received)||received.IsDisconnected())return false;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(server)
        {
            var peer=packet.Sender;
            if(manager?.IsServer!=true||peer?.loginDone!=true||peer.disconnecting||!ReferenceEquals(manager.Clients?.ForEntityId(peer.entityId),peer)||
                peer.netConnection==null||peer.netConnection.Length==0||!ReferenceEquals(peer.netConnection[0],received))return false;
        }
        else if(manager?.IsServer!=false||!manager.IsConnected||manager.connectionToServer==null||manager.connectionToServer.Length==0||!ReferenceEquals(manager.connectionToServer[0],received))return false;
        connection=received;return true;
    }
}
