// AUTHORED_NOT_RUN. Actual native-scope capture/positive-ended predicate and
// complete bundle retirement helper; native connection and typed domains adapt.
using System;using System.Collections.Generic;
class World{internal bool Remote=true;internal bool IsRemote()=>Remote;}
static class RebirthNpcClientNativeReentry{internal sealed class Source{internal World World;internal string Directory;internal uint Epoch,Connection;internal bool Current()=>true;internal bool ScopeCurrent()=>true;}}
static class RebirthNpcSaveQuarantineScope{internal static bool SameDirectory(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
interface INetConnection{bool IsDisconnected();}
class NativeConnection:INetConnection{internal bool Ended;public bool IsDisconnected()=>Ended;}
class ConnectionManager{internal INetConnection[] Channels;internal bool Throws;internal INetConnection[] GetConnectionToServer(){if(Throws)throw new Exception();return Channels;}}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class RebirthNpcSaveQuarantineDomain{internal string Name;internal bool IsDetached=true;}
// Explicit observation cleanup adapter; native owner-load disposal has its own
// source cohort. Bundle retirement must retain custody when cleanup refuses.
static class RebirthNpcTradeOwnerLoad{internal sealed class Retirement{internal bool Ready=true;internal bool ReleaseDetachedObservation()=>Ready;}}
public static partial class RebirthNpcNetworkEpoch
{
 private static readonly object Sync=new object();private static World clientWorld,serverWorld;
 private static string clientDirectory;private static ConnectionManager clientManager,serverManager;private static INetConnection clientTransport;
 private static uint clientEpoch,clientConnectionEpoch,activeServerEpoch;private static Dictionary<int,uint> ServerConnectionEpochs=new Dictionary<int,uint>();
 // ACTUAL_CLIENT_PROOF
 // ACTUAL_RETAINED_OBSERVATION
 internal static ClientProjectionRetirement Fresh(out World world,out ConnectionManager manager,out NativeConnection transport)
 {
  world=clientWorld=new World();transport=new NativeConnection();manager=clientManager=new ConnectionManager{Channels=new INetConnection[]{transport}};
  clientTransport=transport;clientDirectory="original-client";clientEpoch=3;clientConnectionEpoch=4;
  return ClientProjectionRetirement.Capture(world,clientDirectory,clientEpoch,clientConnectionEpoch);
 }
 internal static bool WrongTupleRefused(World world)=>ClientProjectionRetirement.Capture(world,"foreign",3,4)==null&&ClientProjectionRetirement.Capture(world,"original-client",3,5)==null;
}
internal static class RebirthNpcSaveQuarantineBundle
{
 private sealed class Retained
 {internal World Original;internal string Directory;internal RebirthNpcSaveQuarantineDomain[] Domains;internal bool Detached=true,RollbackIncomplete,ClientFinancialCustody;internal object Fence,Capture;internal RebirthNpcNetworkEpoch.ClientProjectionRetirement ClientRetirement;internal RebirthNpcTradeOwnerLoad.Retirement TradeObservationRetirement;}
 private static readonly List<Retained> retained=new List<Retained>();private static bool transferring;
 // ACTUAL_CLIENT_RETIREMENT
 private static int assertions;
 private static void Need(bool result,string message){if(!result)throw new Exception(message);assertions++;}
 private static Retained Fresh(out ConnectionManager manager,out NativeConnection channel)
 {
  var proof=RebirthNpcNetworkEpoch.Fresh(out var world,out manager,out channel);
  var values=new List<RebirthNpcSaveQuarantineDomain>();foreach(string name in ClientProjectionMembers)values.Add(new RebirthNpcSaveQuarantineDomain{Name=name});
  var original=new Retained{Original=world,Directory="original-client",ClientRetirement=proof,Domains=values.ToArray(),TradeObservationRetirement=new RebirthNpcTradeOwnerLoad.Retirement()};retained.Clear();retained.Add(original);return original;
 }
 internal static void Run()
 {
  var original=Fresh(out var manager,out var channel);RetireEndedClientProjections();Need(retained.Contains(original),"live original channel retired");
  Need(RebirthNpcNetworkEpoch.WrongTupleRefused(original.Original),"foreign tuple captured retirement proof");
  channel.Ended=true;original.ClientFinancialCustody=true;RetireEndedClientProjections();Need(retained.Contains(original),"financial trade original retired with disconnected projections");
  original.ClientFinancialCustody=false;RetireEndedClientProjections();Need(!retained.Contains(original),"positive disconnected projection retained");
  original=Fresh(out manager,out channel);manager.Channels=new INetConnection[]{new NativeConnection()};RetireEndedClientProjections();Need(!retained.Contains(original),"positive channel replacement retained old projection");
  original=Fresh(out manager,out channel);manager.Throws=true;RetireEndedClientProjections();Need(retained.Contains(original),"unknown connection observation retired projection");
  original=Fresh(out manager,out channel);channel.Ended=true;original.TradeObservationRetirement.Ready=false;RetireEndedClientProjections();Need(retained.Contains(original),"unreleased native observation lost custody");
  original=Fresh(out manager,out channel);channel.Ended=true;original.TradeObservationRetirement=null;RetireEndedClientProjections();Need(retained.Contains(original),"missing native observation cleanup lost custody");
  for(int variant=0;variant<8;variant++)
  {
   original=Fresh(out manager,out channel);channel.Ended=true;
   if(variant==0)original.RollbackIncomplete=true;
   if(variant==1)original.Detached=false;
   if(variant==2)original.Domains[0].IsDetached=false;
   if(variant==3)original.Domains[0].Name="inventory-service";
   if(variant==4)original.Fence=new object();
   if(variant==5)original.Capture=new object();
   if(variant==6)original.Directory="foreign";
   if(variant==7)original.Original.Remote=false;
   RetireEndedClientProjections();Need(retained.Contains(original),"unqualified/server group retired: "+variant);
  }
  original=Fresh(out manager,out channel);channel.Ended=true;ThreadManager.Main=false;RetireEndedClientProjections();Need(retained.Contains(original),"worker retired projection");ThreadManager.Main=true;
  transferring=true;RetireEndedClientProjections();Need(retained.Contains(original),"mid-transfer original retired");transferring=false;
  for(int index=0;index<32;index++){original=Fresh(out manager,out channel);channel.Ended=true;RetireEndedClientProjections();if(retained.Contains(original))throw new Exception("ended tuple retained");}
  Need(retained.Count==0,"ended client projection tuples exhausted retained capacity");
  Console.WriteLine("MP049 client projection retirement: "+assertions+" authored assertions; native/domain adapters explicit, runtime NOT_RUN.");
 }
}
class Program{static void Main()=>RebirthNpcSaveQuarantineBundle.Run();}
