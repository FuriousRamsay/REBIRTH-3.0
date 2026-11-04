using System;using System.Reflection;using System.Xml.Linq;using System.Linq;
static class Program {
static int checks; static void Assert(bool b,string message){if(!b)throw new Exception(message);checks++;}
static void Near(float a,float b,string message,float epsilon=.0001f)=>Assert(Math.Abs(a-b)<epsilon,message+" actual="+a+" expected="+b);
static EntityPlayer player;static EntityAlive target;static ItemValue weapon;static DamageSource source;
static void Setup(string name="ItemsWeaponsCleaver001_FR",string tags="WeaponTier1Knives,CurrentWeaponKnives",int type=1){
var world=new World();player=new(){entityId=7,world=world};world.Player=player;target=new(){entityId=8,world=world};weapon=new(){type=type,ItemClass=new(){Name=name,Tags=tags}};source=new(){AttackingItem=weapon};
RebirthSkillAwardService.Record=new();foreach(var id in new[]{"skill.knives","skill.long_range_rifles","skill.archery"})RebirthSkillAwardService.Record.Progression.Skills[id]=new();RebirthSkillAwardService.Receipts.Clear();RebirthSkillAwardService.Save=true;RebirthSkillAwardService.Eligible=true;RebirthWeaponSustainedDpsService.ProfileAvailable=true;RebirthWeaponSustainedDpsService.ActiveProfile=new();RebirthStatisticsService.Calls=0;RebirthStatisticsService.Damage=0;
}
static object Invoke(Type type,string method,params object[] args)=>type.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
// Native b9 call graph separately audited from the exact installed binary.
static void Hit(bool received,int damage=24,bool ran=true,bool alreadyDead=false){
target.Dead=alreadyDead;var response=new DamageResponse{Source=source,Strength=damage};
if(received){var args=new object[]{target,response,null};Invoke(typeof(RebirthSurvivorReceivedCombatSkillPatch),"Prefix",args);if(ran)target.Health=Math.Max(0,target.Health-damage);Invoke(typeof(RebirthSurvivorReceivedCombatSkillPatch),"Postfix",target,response,args[2],ran);}
else {var args=new object[]{target,source,damage,null};Invoke(typeof(RebirthSurvivorCombatSkillPatch),"Prefix",args);if(ran)target.Health=Math.Max(0,target.Health-damage);Invoke(typeof(RebirthSurvivorCombatSkillPatch),"Postfix",target,response,args[3],ran);}
RebirthWeaponSustainedDpsService.FlushQueuedCombatProgress(player);
}
static float Progress(string skill)=>RebirthSkillAwardService.Record.Progression.Skills[skill].Progress;
static void Combat(string inputs){
var xml=XDocument.Load(inputs+"/skill_sources.xml");foreach(var row in xml.Root.Element("combat").Elements("map"))RebirthProgressionRuntimeConfig.LoadMap(row);
var cleaver=XDocument.Load(inputs+"/starter_legacy_items.xml").Descendants("item").Single(x=>(string)x.Attribute("name")=="ItemsWeaponsCleaver001_FR");string knifeTags=(string)cleaver.Elements("property").Single(x=>(string)x.Attribute("name")=="Tags").Attribute("value");
var cases=new[]{("ItemsWeaponsCleaver001_FR",knifeTags,"skill.knives",1),("gunRifleT1HuntingRifle","","skill.long_range_rifles",2),("gunBowT0PrimitiveBow","","skill.archery",3)};
foreach(var (name,tags,skill,type) in cases){foreach(bool received in new[]{false,true}){Setup(name,tags,type);Assert(RebirthProgressionRuntimeConfig.ClassifyCombat(weapon)==skill,name+" existing classifier");Hit(received);Near(Progress(skill),.005f,name+" "+(received?"received":"local")+" commits one normalized award");Assert(RebirthStatisticsService.Calls==1,"one statistics observation");RebirthWeaponSustainedDpsService.FlushQueuedCombatProgress(player);Near(Progress(skill),.005f,"flush cannot replay");}}
Setup();Hit(true,1000);Near(Progress("skill.knives"),100f/24*.005f,"overkill capped to actual prehit health");
Setup();Hit(true,0);Near(Progress("skill.knives"),0,"zero/negated health loss no XP");
Setup();Hit(true,24,false);Near(Progress("skill.knives"),0,"suppressed received original no XP");Assert(RebirthStatisticsService.Calls==0,"suppressed no stats");
Setup();Hit(true,24,true,true);Near(Progress("skill.knives"),0,"already dead is no combat practice");
Setup();target.world.Remote=true;Hit(true);Near(Progress("skill.knives"),0,"remote client refuses authority");
Setup();source.Actor=99;Hit(true);Near(Progress("skill.knives"),0,"unresolved attacker refuses");
Setup();source.AttackingItem=null;Hit(true);Near(Progress("skill.knives"),0,"missing weapon refuses");
Setup();RebirthSkillAwardService.Eligible=false;Hit(true);Near(Progress("skill.knives"),0,"ineligible character refuses");
Setup();RebirthWeaponSustainedDpsService.ProfileAvailable=false;Hit(true);Near(Progress("skill.knives"),0,"unresolved DPS fails closed");
Setup();RebirthSkillAwardService.Save=false;Hit(true);Near(Progress("skill.knives"),.005f,"uncertain save retains original applied award");RebirthSkillAwardService.Save=true;RebirthWeaponSustainedDpsService.FlushQueuedCombatProgress(player);Near(Progress("skill.knives"),.005f,"durable receipt retry does not replay");
Console.WriteLine("combat routes/classification/normalization/queue/receipt guards PASS");}
static RebirthMetabolismState State(float energy=10,float hydration=100,float food=80){Setup();player.Stats.Water.Value=hydration;player.Stats.Food.Value=food;var state=new RebirthMetabolismState{Energy=energy,LastStamina=100};RebirthMetabolismStateRepository.State=state;RebirthMetabolismModifierResolver.Mods=new();return state;}
static void Energy(){
foreach(var (water,rate) in new[]{(100f,4f),(75f,4f),(50f,3.4f),(25f,2.4f),(0f,1f)}){var s=State(0,water);RebirthMetabolismService.Step(player,s,new(),1);Near(s.Energy,rate/60,"hydration curve "+water);Near(RebirthMetabolismService.BuildSnapshot(player).EnergyRecoveryPerRealMinute,rate,"published completed recovery "+water);}
var state=State();var mods=new RebirthMetabolismModifiers{EnergyRecovery=1.025f};RebirthMetabolismModifierResolver.Mods=mods;RebirthMetabolismService.Step(player,state,mods,1);Near(RebirthMetabolismService.BuildSnapshot(player).EnergyRecoveryPerRealMinute,4.1f,"4.1 is authored4 times mood/trait modifier1.025");
state=State(80);player.Stats.Stamina.Value=0;RebirthMetabolismService.Step(player,state,new(),1);Near(state.Energy,74.8f,"100 spent stamina=5 Energy plus one second extreme tier=.2");var snap=RebirthMetabolismService.BuildSnapshot(player);Near(snap.EnergyUsePerRealMinute,312,"publish direct stamina plus tier cost, not12");Near(snap.EnergyRecoveryPerRealMinute,0,"extreme activity no recovery");
state=State(80);player.CurrentMovementTag=FastTags<TagGroup.Global>.Parse("swimmingRun");player.Stats.Stamina.Value=0;RebirthMetabolismService.Step(player,state,new(),60);Near(state.Energy,63,"one minute extreme plus100 stamina costs17");
state=State(10,100,.001f);RebirthMetabolismService.Step(player,state,new(),1);snap=RebirthMetabolismService.BuildSnapshot(player);Near(snap.EnergyRecoveryPerRealMinute,0,"ceiling-limited recovery publishes actual zero");
state=State(10,100,.01f);state.Energy=0;RebirthMetabolismService.Step(player,state,new(),1);snap=RebirthMetabolismService.BuildSnapshot(player);Near(snap.EnergyRecoveryPerRealMinute,3,"Food-limited recovery actual3 not theoretical4");Near(snap.FoodUsePerReal60Minutes,66,"publish basal30 plus recovery food36");
state=State(77,100,10);RebirthMetabolismService.Step(player,state,new(),1);Near(state.Energy,10,"nutrition cap can clamp77 to10; mechanism only, no reported-play cause claimed");
state=State(70);player.Stats.Stamina.Value=0;RebirthMetabolismService.Step(player,state,new(),1);player.IsGodMode.Value=true;snap=RebirthMetabolismService.BuildSnapshot(player);Near(state.Energy,100,"native god mode Energy restored");Near(snap.EnergyUsePerRealMinute,0,"god mode clears stale use sample");Near(snap.EnergyRecoveryPerRealMinute,0,"god mode clears stale recovery sample");
Console.WriteLine("Energy units/hydration/cap/activity/Food-limited recovery and owner rates PASS");}
static int Main(string[] args){try{if(args.Contains("energy"))Energy();else if(args.Contains("combat"))Combat(args[0]);else{Combat(args[0]);Energy();}Console.WriteLine(checks+" checks PASS (exact-source methods; explicit native/storage/world adapters)");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
