using System;
using System.IO;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
static class Program {
 static int checks;
 static void Check(bool pass,string reason){checks++;if(!pass)throw new Exception(reason);}
 static ItemValue Mod(string name)=>new ItemValue{ItemClass=new ItemClass{Name=name}};
 static void Main(string[] args){try{Run(args);}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(string[] args){
  var xml=XElement.Load(args[0]);
  var rules=new RebirthPurgeSupplyPolicy.Rules((int)xml.Attribute("baseKills"),(int)xml.Attribute("increment"),(int)xml.Attribute("maximumBonus"),(int)xml.Attribute("flightRadius"));
  Check(rules.BaseKills==75&&rules.Increment==5&&rules.MaximumBonus==150,"Actual installed defaults match required75+5/cap150");
  var p=new EntityPlayer{equipment=new Equipment{Hands=Mod("armorButcherGloves")}};
  for(int tier=0;tier<=5;tier++){
   p.equipment.Hands.modifications=tier==0?Array.Empty<ItemValue>():new[]{Mod("ModPurgeTier"+tier)};
   Check(RebirthPurgeSupplyEquipment.Tier(p)==tier,"Equipped native hands tier"+tier);
   int target=75-10*tier;
   Check(rules.Target(0,tier)==target,"Initial target for tier"+tier);
   Check(rules.Target(1,tier)==target+5,"Escalation added after discount tier"+tier);
   Check(rules.Target(30,tier)==target+150&&rules.Target(long.MaxValue,tier)==target+150,"Only added cost capped tier"+tier);
   Check(rules.TryRedeem(target-1,0,2,out var below,tier)&&below.Drops==0&&below.Credits==target-1,"Below modified quota retained tier"+tier);
   Check(rules.TryRedeem(target,0,2,out var exact,tier)&&exact.Drops==1&&exact.Credits==0&&exact.EarnedDrops==1,"Exact modified quota once tier"+tier);
   Check(rules.TryRedeem(target*2+5,0,2,out var twice,tier)&&twice.Drops==2&&twice.Credits==0,"Two modified/escalating quotas tier"+tier);
  }
  p.equipment.Hands.modifications=new[]{Mod("ModPurgeTier1"),Mod("ModPurgeTier5"),Mod("ModPurgeTier3")};
  Check(RebirthPurgeSupplyEquipment.Tier(p)==5,"Highest installed tier wins without stacking");
  p.equipment.Hands.modifications=new[]{Mod("ModPurgeTier6"),Mod("ModPurgeTier0"),Mod("ModPurgeTier01"),Mod("xModPurgeTier5"),new ItemValue(),null};
  Check(RebirthPurgeSupplyEquipment.Tier(p)==0,"Malformed/unresolved modifier names have no discount");
  p.Backpack=new[]{Mod("ModPurgeTier5")};p.equipment.Hands.modifications=Array.Empty<ItemValue>();
  Check(RebirthPurgeSupplyEquipment.Tier(p)==0,"Backpack modifier does not discount");
  p.equipment.Hands=null;Check(RebirthPurgeSupplyEquipment.Tier(p)==0,"Unequipped gloves do not leave stale discount");
  var world=new World();p.world=world;p.equipment.Hands=Mod("armorButcherGloves");p.equipment.Hands.modifications=new[]{Mod("ModPurgeTier2")};
  world.aiDirector.Players.trackedPlayers.list.Add(new PlayerEntry{Player=p});GameManager.Instance.World=world;UnityEngine.Time.realtimeSinceStartup=10;
  Check(RebirthPurgeSupplyEquipment.TryTier(p.Key,out var cached)&&cached==2,"Tracked spawned native owner resolves");
  p.equipment.Hands.modifications=new[]{Mod("ModPurgeTier4")};
  Check(RebirthPurgeSupplyEquipment.TryTier(p.Key,out cached)&&cached==4,"Cached player reads live equipment without stale tier");
  p.Spawned=false;Check(!RebirthPurgeSupplyEquipment.TryTier(p.Key,out _),"Unspawned native owner refused");p.Spawned=true;
  world.Remote=true;Check(!RebirthPurgeSupplyEquipment.TryTier(p.Key,out _),"Remote world cannot authorize local discount");world.Remote=false;
  RebirthPurgeSupplyEquipment.Reset();GameManager.Instance.World=null;world=null;p=null;
  var weak=CacheWorld();ForceCollection();Check(weak.IsAlive,"Cached world is strongly retained before lifecycle reset");
  RebirthPurgeSupplyEquipment.Reset();ForceCollection();Check(!weak.IsAlive,"Lifecycle reset releases actual world/player cache references");
  Check(!RebirthPurgeSupplyEquipment.TryTier("player",out _),"No owner survives reset with no current world");
  var unavailable=CacheWorld();Check(!RebirthPurgeSupplyEquipment.TryTier("player",out _),"Lost native world refuses resolution");ForceCollection();
  Check(!unavailable.IsAlive,"Unavailable-world lookup also releases old cache");
  Console.WriteLine($"PASS {checks} linked production modifier/policy/lifecycle checks; native equipment/identity boundary doubled");
 }
 [MethodImpl(MethodImplOptions.NoInlining)] static WeakReference CacheWorld(){
  var world=new World();var player=new EntityPlayer{world=world};world.aiDirector.Players.trackedPlayers.list.Add(new PlayerEntry{Player=player});GameManager.Instance.World=world;
  UnityEngine.Time.realtimeSinceStartup+=10;
  if(!RebirthPurgeSupplyEquipment.TryTier(player.Key,out _))throw new Exception("Fixture native player index not admitted");
  var weak=new WeakReference(world);GameManager.Instance.World=null;return weak;
 }
 static void ForceCollection(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
}
