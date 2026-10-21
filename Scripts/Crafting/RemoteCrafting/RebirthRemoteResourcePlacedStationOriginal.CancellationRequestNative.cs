using System;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class CancellationNativeRequest
    {
        internal NetPackageRebirthRemoteRepairOriginal Packet;internal World World;internal EntityPlayer Actor;internal ClientInfo Peer;internal string Text;internal INetConnection Connection;internal ConnectionManager Manager;
        internal bool Current=>ReferenceEquals(cancellationNativeRequest,this)&&ThreadManager.IsMainThread()&&Packet?.Sender!=null&&ReferenceEquals(Packet.Sender,Peer)&&
            ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()==false&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,Manager)&&Manager.IsServer&&ReferenceEquals(Manager.Clients.ForEntityId(Actor.entityId),Peer)&&
            Peer.loginDone&&!Peer.disconnecting&&Peer.netConnection?.Length>0&&ReferenceEquals(Peer.netConnection[0],Connection)&&!Connection.IsDisconnected();
    }
    [ThreadStatic]private static CancellationNativeRequest cancellationNativeRequest;
    internal static void NativeCancellationRequest(NetPackageRebirthRemoteRepairOriginal packet,World world,EntityPlayer actor,string text)
    {
        if(cancellationNativeRequest!=null||packet==null||actor==null||!RebirthRemoteResourceNetworkScope.Receive(packet,world,GameManager.Instance,true,out var connection))return;
        var scope=new CancellationNativeRequest{Packet=packet,World=world,Actor=actor,Peer=packet.Sender,Text=text,Connection=connection,Manager=SingletonMonoBehaviour<ConnectionManager>.Instance};cancellationNativeRequest=scope;
        try{if(scope.Current)CancellationRequest(actor,text);}finally{cancellationNativeRequest=null;scope.Packet=null;scope.World=null;scope.Actor=null;scope.Peer=null;scope.Connection=null;scope.Manager=null;}
    }
}
