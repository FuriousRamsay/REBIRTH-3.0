// Actual terminal Hit scope/decision methods. World/entity/context/positions are
// doubles. Does not execute Harmony or native attack/event graphs.
using System;using System.Collections.Generic;
enum MinEventTypes {onSelfDamagedBlock,onSelfDestroyedBlock}
struct Vector3i {public int x;public static bool operator==(Vector3i a,Vector3i b)=>a.x==b.x;public static bool operator!=(Vector3i a,Vector3i b)=>a.x!=b.x;public override bool Equals(object b)=>b is Vector3i v&&v==this;public override int GetHashCode()=>x;}
struct BlockValue {public uint rawData;}
class World {}
class Context {public BlockValue BlockValue;}
class EntityAlive {public Context MinEventContext=new Context();}
class EntityPlayerLocal:EntityAlive {public bool Dead;public bool IsDead()=>Dead;}
static class Time {public static int frameCount;}
class GameManager {public static GameManager Instance=new GameManager();public World World;}
class BlockValueRef {public Vector3i BlockPosition;}
class ItemActionAttack {public class AttackHitInfo {public BlockValueRef hitRef=new BlockValueRef();}}
static class AdvancedFarmingHarvestTerminalPlot {
 private sealed class Capsule {internal bool? UseInventory;}
 private sealed class Pending {internal Capsule Capsule=new Capsule();internal EntityPlayerLocal Owner;internal World World;internal bool Failed;internal int Frame;internal BlockValue PlotValue;internal Vector3i Plot;}
 private static readonly Dictionary<ItemActionAttack.AttackHitInfo,Pending> PendingActions=new Dictionary<ItemActionAttack.AttackHitInfo,Pending>();
 [ThreadStatic] private static ItemActionAttack.AttackHitInfo nativeHit;
// PRODUCTION_METHODS
 internal static void Run(){var w=new World();GameManager.Instance.World=w;var p=new EntityPlayerLocal();p.MinEventContext.BlockValue=new BlockValue{rawData=5};var hit=new ItemActionAttack.AttackHitInfo();hit.hitRef.BlockPosition=new Vector3i{x=4};PendingActions.Add(hit,new Pending{World=w,Owner=p,Frame=0,PlotValue=p.MinEventContext.BlockValue,Plot=hit.hitRef.BlockPosition});
  Check(!SuppressHeldDamage(p,MinEventTypes.onSelfDamagedBlock),"outside native Hit suppressed");var prior=EnterNativeHit(hit);Check(SuppressHeldDamage(p,MinEventTypes.onSelfDamagedBlock),"exact held terminal not suppressed");Check(!SuppressHeldDamage(p,MinEventTypes.onSelfDestroyedBlock),"native destroy suppressed");Check(!SuppressHeldDamage(new EntityPlayerLocal(),MinEventTypes.onSelfDamagedBlock),"other owner suppressed");
  var nested=EnterNativeHit(new ItemActionAttack.AttackHitInfo());Check(!SuppressHeldDamage(p,MinEventTypes.onSelfDamagedBlock),"nested unrelated hit suppressed");LeaveNativeHit(nested);Check(SuppressHeldDamage(p,MinEventTypes.onSelfDamagedBlock),"outer original scope lost");Time.frameCount++;Check(!SuppressHeldDamage(p,MinEventTypes.onSelfDamagedBlock),"stale action suppressed");Time.frameCount=0;p.Dead=true;Check(!SuppressHeldDamage(p,MinEventTypes.onSelfDamagedBlock),"dead owner suppressed");p.Dead=false;GameManager.Instance.World=new World();Check(!SuppressHeldDamage(p,MinEventTypes.onSelfDamagedBlock),"retired world suppressed");LeaveNativeHit(prior);
 }
 private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
static class Fixture {static void Main(){AdvancedFarmingHarvestTerminalPlot.Run();Console.WriteLine("PASS actual terminal Hit scope predicates: exact owner/plot/event/frame/world only, nested restoration and death refusal; native Harmony/events doubled");}}
