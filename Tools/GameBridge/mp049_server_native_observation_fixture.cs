// AUTHORED_NOT_RUN. Complete production Retirement; native Load and Release adapters.
using System;using System.Linq;
class World{internal class State{internal string Guid=System.Guid.NewGuid().ToString();}internal State worldState=new State();internal bool Remote;internal bool IsRemote()=>Remote;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcTradeOwnerExchange{
 internal sealed class Release{internal bool Valid=true;internal Guid Operation;internal string Account,Creation,World;internal int Deaths;
 internal bool Current()=>Valid;internal bool MatchesNative(Guid operation,string account,string creation,string world,int deaths)=>Operation==operation&&Account==account&&Creation==creation&&World==world&&Deaths==deaths;}
}
static class RebirthNpcTradeLocalOwnerLoad{
 private sealed class Load:IDisposable{internal World World;internal Guid Operation=Guid.NewGuid();internal string Account="owner",Creation="creation";internal int Deaths,Disposals;internal bool Closed,Hydrating,ReadReturned=true,Loaded=true;public void Dispose(){if(Closed)return;Closed=true;World=null;Disposals++;}}
 private static Load loading,pending,loaded;
 // ACTUAL_RETIREMENT
 static int checks;static void Need(bool value){if(!value)throw new Exception("native observation "+checks);checks++;}
 static void Refuses(Action action){bool refused=false;try{action();}catch(InvalidOperationException){refused=true;}Need(refused);}
 static World world;static Load first,second;
 static void Seed(){ThreadManager.Main=true;world=new World();first=new Load{World=world};second=new Load{World=world};pending=first;loaded=second;loading=null;}
 static RebirthNpcTradeOwnerExchange.Release Proof(Load source)=>new RebirthNpcTradeOwnerExchange.Release{Operation=source.Operation,Account=source.Account,Creation=source.Creation,World=world.worldState.Guid,Deaths=source.Deaths};
 public static void Main(){
  Seed();var retirement=Retirement.Capture(world);var proof=Proof(first);Need(!retirement.Complete);retirement.Observe(proof);Need(first.Disposals==0);
  pending=loaded=null;proof.Valid=false;retirement.Observe(proof);Need(first.Disposals==0);proof.Valid=true;proof.Creation="foreign";retirement.Observe(proof);Need(first.Disposals==0);
  proof=Proof(first);ThreadManager.Main=false;retirement.Observe(proof);Need(first.Disposals==0);ThreadManager.Main=true;retirement.Observe(proof);Need(first.Disposals==1&&!retirement.Complete);
  retirement.Observe(proof);Need(first.Disposals==1);retirement.Observe(Proof(second));Need(second.Disposals==1&&retirement.Complete);
  Seed();loaded=pending;retirement=Retirement.Capture(world);proof=Proof(first);pending=loaded=null;retirement.Observe(proof);Need(retirement.Complete&&first.Disposals==1);
  Seed();loading=first;Refuses(()=>Retirement.Capture(world));Seed();Refuses(()=>Retirement.Capture(new World()));Seed();first.Hydrating=true;Refuses(()=>Retirement.Capture(world));
  Seed();first.Operation=Guid.Empty;Refuses(()=>Retirement.Capture(world));Seed();world.worldState=null;Refuses(()=>Retirement.Capture(world));
  Seed();pending=loaded=null;Need(Retirement.Capture(world).Complete);
  Console.WriteLine("PASS "+checks+" actual detached native observation assertions; native release provenance adapted");
 }
}
