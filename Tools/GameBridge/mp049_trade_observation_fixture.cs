using System;
// Actual native-observation retirement; native ended-connection proof, world,
// Load.Dispose and observation handles are explicit adapters.
class World{internal bool Remote=true;internal bool IsRemote()=>Remote;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcNetworkEpoch
{
 internal sealed class ClientProjectionRetirement
 {internal World World;internal bool Ended;internal string Directory="original";internal bool Matches(World w,string d)=>ReferenceEquals(w,World)&&d==Directory;internal bool HasEnded=>Ended;}
}
static class RebirthNpcTradeOwnerLoad
{
 private sealed class Load{internal World World;internal int Disposals;private bool disposed;internal void Dispose(){if(disposed)return;disposed=true;Disposals++;}}
 private static object gate=new object();private static Load loaded,next,joining,incomingBoundary;
 private static bool IncomingBoundaryCurrent()=>false;
 // ACTUAL_OBSERVATION_RETIREMENT
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Observation retirement assertion "+checks);}
  var world=new World();var proof=new RebirthNpcNetworkEpoch.ClientProjectionRetirement{World=world};
  var empty=Retirement.Capture(world,"original",proof);Check(!empty.ReleaseDetachedObservation());proof.Ended=true;Check(empty.ReleaseDetachedObservation());
  loaded=new Load{World=world};var source=loaded;proof.Ended=false;var retirement=Retirement.Capture(world,"original",proof);
  Check(!retirement.ReleaseDetachedObservation()&&source.Disposals==0);proof.Ended=true;Check(!retirement.ReleaseDetachedObservation());
  loaded=null;next=source;Check(!retirement.ReleaseDetachedObservation());next=null;joining=source;Check(!retirement.ReleaseDetachedObservation());
  joining=null;incomingBoundary=source;Check(!retirement.ReleaseDetachedObservation()&&source.Disposals==0);incomingBoundary=null;
  Check(retirement.ReleaseDetachedObservation()&&source.Disposals==1);Check(retirement.ReleaseDetachedObservation()&&source.Disposals==1);
  proof.Directory="foreign";Check(!retirement.ReleaseDetachedObservation());proof.Directory="original";
  bool refused=false;try{Retirement.Capture(new World(),"original",proof);}catch(InvalidOperationException){refused=true;}Check(refused);
  loaded=new Load{World=new World()};refused=false;try{Retirement.Capture(world,"original",proof);}catch(InvalidOperationException){refused=true;}Check(refused);loaded=null;
  ThreadManager.Main=false;Check(!retirement.ReleaseDetachedObservation());ThreadManager.Main=true;
  next=new Load{World=world};refused=false;try{Retirement.Capture(world,"original",proof);}catch(InvalidOperationException){refused=true;}Check(refused);next=null;
  Console.WriteLine(checks+" actual observation retirement assertions passed; native proof/disposal adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcTradeOwnerLoad.Run();}
