  for(int direction=0;direction<2;direction++)
  {
   bool server=direction==0;
   Reset(server);var original=server?(NetPackage)new NetPackageRebirthRemoteRepairOriginal():new NetPackageRebirthRemoteRepairResult();original.Sender=peer;Dequeue(original);
   Require(RebirthRemoteResourceNetworkScope.Receive(original,world,game,server,out var received)&&ReferenceEquals(received,connection),"Actual original resource dequeue connection");
   Reset(server);original=server?(NetPackage)new NetPackageRebirthRemoteRepairOriginal():new NetPackageRebirthRemoteRepairResult();original.Sender=peer;
   Require(!RebirthRemoteResourceNetworkScope.Receive(original,world,game,server,out _),"Unreceived resource packet cannot enter source/owner action");
   Dequeue(original);if(server)peer.netConnection[0]=new NetConnectionAbs();else manager.connectionToServer[0]=new NetConnectionAbs();
   Require(!RebirthRemoteResourceNetworkScope.Receive(original,world,game,server,out _),"Resource original cannot cross native session replacement");
   Reset(server);original=server?(NetPackage)new NetPackageRebirthRemoteRepairOriginal():new NetPackageRebirthRemoteRepairResult();original.Sender=peer;Dequeue(original);original.Channel=1;
   Require(!RebirthRemoteResourceNetworkScope.Receive(original,world,game,server,out _),"Resource channel mismatch refuses");
   Reset(server);original=server?(NetPackage)new NetPackageRebirthRemoteRepairOriginal():new NetPackageRebirthRemoteRepairResult();original.Sender=peer;Dequeue(original);connection.Disconnected=true;
   Require(!RebirthRemoteResourceNetworkScope.Receive(original,world,game,server,out _),"Disconnected resource packet refuses");
   Reset(server);original=server?(NetPackage)new NetPackageRebirthRemoteRepairOriginal():new NetPackageRebirthRemoteRepairResult();original.Sender=peer;Dequeue(original);
   Require(!RebirthRemoteResourceNetworkScope.Receive(original,world,new GameManager{World=world},server,out _),"Wrong native callback manager refuses");
  }
  Reset(true);world.Actor=new EntityPlayer{world=world};var resource=RebirthRemoteResourceNetworkScope.Capture(world.Actor);Require(resource!=null&&resource.Current,"Actual remote original source captures current network lineage");
  peer.netConnection[0]=new NetConnectionAbs();Require(!resource.Current,"Pending source original becomes ineligible after native connection replacement");
  Reset(true);resource=RebirthRemoteResourceNetworkScope.Capture(world.Actor);Require(resource!=null&&resource.Current,"Host-local cooking/resource original remains supported without a ClientInfo");
  world.Actor=new EntityPlayerLocal{world=world};Require(!resource.Current,"Local source original cannot follow a replacement native body");
  Reset(true);peer.netConnection=new INetConnection[0];world.Actor=new EntityPlayer{world=world};Require(RebirthRemoteResourceNetworkScope.Capture(world.Actor)==null,"Empty remote source connection refuses without indexing");
  Reset(true);resource=RebirthRemoteResourceNetworkScope.Capture(world.Actor);SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();Require(!resource.Current,"Source original cannot follow a replaced native manager");
  Reset(false);string session=RebirthRemoteResourceNetworkScope.SessionKey(connection);Require(session==RebirthRemoteResourceNetworkScope.SessionKey(connection),"Same native fragment session is stable");Require(session!=RebirthRemoteResourceNetworkScope.SessionKey(new NetConnectionAbs()),"Distinct native connections cannot share a fragment assembly namespace");
  Reset(true);var cancellation=new NetPackageRebirthRemoteRepairOriginal{Sender=peer};Dequeue(cancellation);
  RebirthRemoteResourcePlacedStationOriginal.Check=()=>true;
  RebirthRemoteResourcePlacedStationOriginal.NativeCancellationRequest(cancellation,world,world.Actor,"exact");
  Require(RebirthRemoteResourcePlacedStationOriginal.Calls==1&&RebirthRemoteResourcePlacedStationOriginal.Valid,"Actual cancellation request enters only its current dequeued scope");
  RebirthRemoteResourcePlacedStationOriginal.Check=()=>{SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true,Clients=manager.Clients};return true;};
  RebirthRemoteResourcePlacedStationOriginal.NativeCancellationRequest(cancellation,world,world.Actor,"exact");
  Require(!RebirthRemoteResourcePlacedStationOriginal.Valid,"Cancellation reentry cannot follow replaced manager with same peer");
  Reset(true);cancellation=new NetPackageRebirthRemoteRepairOriginal{Sender=peer};Dequeue(cancellation);
  RebirthRemoteResourcePlacedStationOriginal.Check=()=>{peer.netConnection=new INetConnection[0];return true;};
  RebirthRemoteResourcePlacedStationOriginal.NativeCancellationRequest(cancellation,world,world.Actor,"exact");
  Require(!RebirthRemoteResourcePlacedStationOriginal.Valid,"Cancellation reentry refuses emptied connection array without indexing");
// ADAPTER_TYPES
class NetPackageRebirthRemoteRepairOriginal:NetPackage{}
class NetPackageRebirthRemoteRepairResult:NetPackage{}
internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
 internal static Action Callback;internal static Func<bool> Check;internal static int Calls;internal static bool Valid;
 private static void CancellationRequest(EntityPlayer actor,string text)
 {Calls++;Check?.Invoke();Valid=cancellationNativeRequest?.Current==true;Callback?.Invoke();}
}
