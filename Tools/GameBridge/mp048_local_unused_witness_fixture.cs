// AUTHORED_NOT_RUN. Complete production privately issued local-unused witness.
// Native policy, saved PDS checkpoint, receipt and marker-clear are adapters.
using System;using System.Collections.Generic;
class World{internal EntityPlayerLocal Player;internal State worldState=new State();internal class State{internal string Guid="world";}internal bool IsRemote()=>false;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal object GetEntity(int id)=>Player;}
class EntityPlayerLocal{internal int entityId=7,Died=2;internal bool IsDead()=>false;internal bool IsSpawned()=>true;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class RebirthNpcTradeOriginal{internal Guid Operation=Guid.NewGuid();internal string World="world",Image="original",Digest="digest";internal int Deaths=2;}
static class RebirthNpcTradeOwnerCheckpoint{internal static bool Saved=true;internal static int Step,Phase=1;internal static bool TryReadRetainedStage(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out int step,out int phase){step=Step;phase=Phase;return Saved;}}
static class RebirthNpcTradeOwnerOriginalStore{internal static bool Durable=true;internal static bool TryRetainReceipt(EntityPlayerLocal p,RebirthNpcTradeOriginal o,string purpose,string image)=>Durable;}
static class RebirthNpcTradeOwnerReceipt{internal static string WriteUnused(Guid op,string original,string unused)=>"unused receipt";}
static class RebirthNpcTradeService{internal class HostUnused{internal RebirthNpcTradeOriginal Original;internal string Digest="unused";internal bool Valid=true;internal bool ReadRetained()=>Valid;}}
static class RebirthNpcTradeOwnerExchange{
 private class Hold{internal EntityPlayerLocal Player;internal World World;internal RebirthNpcTradeOriginal Original;internal int Progress;internal bool Entered,NeedsSave;internal object Grant,LocalGrant;}
 static Dictionary<Guid,Hold> holds=new Dictionary<Guid,Hold>();static object permit,releasing;static bool Metadata=true,Before=true,Clear=true;
 static bool MetadataCurrent(Hold hold)=>Metadata;static bool Prefix(Hold hold,int progress)=>Before&&progress==0;static bool ClearHeldStamp(Hold hold)=>Clear;
 // ACTUAL_LOCAL_UNUSED_WITNESS
 static Hold hold;static EntityPlayerLocal player;static RebirthNpcTradeOriginal original;static int checks;
 static void Fresh(){holds.Clear();Metadata=Before=Clear=RebirthNpcTradeOwnerCheckpoint.Saved=RebirthNpcTradeOwnerOriginalStore.Durable=true;RebirthNpcTradeOwnerCheckpoint.Step=0;RebirthNpcTradeOwnerCheckpoint.Phase=1;permit=releasing=null;
  player=new EntityPlayerLocal();original=new RebirthNpcTradeOriginal();var world=new World{Player=player};GameManager.Instance.World=world;hold=new Hold{Player=player,World=world,Original=original};holds.Add(original.Operation,hold);}
 static bool Capture(out LocalUnusedBefore source)=>LocalUnusedBefore.TryCapture(player,original,out source);
 static void Need(bool value){if(!value)throw new Exception("Local unused witness "+checks);checks++;}
 static RebirthNpcTradeService.HostUnused Unused()=>new RebirthNpcTradeService.HostUnused{Original=original};
 static void Main(){
  Fresh();Need(Capture(out var source)&&source.Current());holds[original.Operation]=new Hold();Need(!source.Current());
  Fresh();hold.Entered=true;Need(!Capture(out _));
  Fresh();hold.Grant=new object();Need(!Capture(out _));
  Fresh();hold.LocalGrant=new object();Need(!Capture(out _));
  Fresh();hold.Progress=1;Need(!Capture(out _));
  Fresh();hold.NeedsSave=true;Need(!Capture(out _));
  Fresh();RebirthNpcTradeOwnerCheckpoint.Saved=false;Need(!Capture(out _));
  Fresh();Need(!LocalUnusedBefore.TryCapture(player,new RebirthNpcTradeOriginal{Operation=original.Operation},out _));
  Fresh();Before=false;Need(!Capture(out _));
  Fresh();GameManager.Instance.World=new World{Player=player};Need(!Capture(out _));
  Fresh();Capture(out source);RebirthNpcTradeOwnerOriginalStore.Durable=false;Need(!source.TryRelease(Unused())&&holds.Count==1);
  Fresh();Capture(out source);Clear=false;Need(!source.TryRelease(Unused())&&holds.Count==1);Clear=true;Need(source.TryRelease(Unused())&&holds.Count==0&&!source.Current());
  Fresh();Capture(out source);Need(!source.TryRelease(new RebirthNpcTradeService.HostUnused{Original=new RebirthNpcTradeOriginal{Image="foreign"}})&&holds.Count==1);
  Fresh();RebirthNpcTradeOwnerCheckpoint.Phase=2;Need(!Capture(out _));
  Console.WriteLine("PASS "+checks+" actual local unused witness assertions; native producer adapters excluded");}
}
