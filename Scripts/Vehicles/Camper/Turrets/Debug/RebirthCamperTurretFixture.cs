using System;using System.IO;using System.Linq;using System.Collections.Generic;
public sealed class ConsoleCmdRebirthCamperTurretFixture:ConsoleCmdAbstract
{
 public override string[] getCommands()=>new[]{"rbturretfixture"};
 public override string getDescription()=>"Verify camper turret native profile, inventory conservation and metadata round trips in CodexTest.";
 public override void Execute(List<string> args,CommandSenderInfo sender)
 {
  if(GameManager.Instance?.World?.GetPrimaryPlayer()==null||!GameIO.GetSaveGameDir().EndsWith("CodexTest",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("CodexTest only");
  var report=new List<string>();
  foreach(var name in new[]{"ammo9mmBulletBall","ammo9mmBulletHP","ammo9mmBulletAP","ammo44MagnumBulletBall","ammo44MagnumBulletHP","ammo44MagnumBulletAP","ammo762mmBulletBall","ammo762mmBulletHP","ammo762mmBulletAP","ammoShotgunShell","ammoShotgunSlug","ammoShotgunBreachingSlug"})
   Require(RebirthCamperTurretProfiles.IsAmmo(ItemClass.GetItem(name,false)),"accept firearm ammunition "+name);
  foreach(var name in new[]{"ammoJunkTurretRegular","ammoJunkTurretShell","ammoJunkTurretAP"})
   Require(RebirthCamperTurretProfiles.IsAmmo(ItemClass.GetItem(name,false)),"accept junk turret ammunition "+name);
  foreach(var name in new[]{"ammoArrowStone","ammoRocketHE"})
   Require(!RebirthCamperTurretProfiles.IsAmmo(ItemClass.GetItem(name,false)),"reject non-firearm ammunition "+name);
  report.Add("PASS firearm rounds, HP/AP variants, shotgun shells/slugs/breaching and junk turret ammunition accepted; arrows and rockets rejected.");
  for(int type=0;type<3;type++)
  {
   var state=new RebirthCamperTurretState();var bag=ItemStack.CreateArray(20);bag[0]=new ItemStack(ItemClass.GetItem(RebirthCamperTurretProfiles.ItemName,false),1);
   for(int corner=0;corner<4;corner++)
   {
    bag[0]=new ItemStack(ItemClass.GetItem(RebirthCamperTurretProfiles.ItemName,false),1);
    Require(RebirthCamperTurretTransfer.Apply(state,bag,RebirthCamperTurretAction.Install,corner,type)==null,"install");
    Require(!state.Slots[corner].Enabled&&bag[0].IsEmpty(),"installation consumes item and starts off");
    var profile=RebirthCamperTurretProfiles.Block(state.Slots[corner]);var ammo=new[]{ItemClass.GetItem(new[]{"ammo9mmBulletBall","ammo762mmBulletAP","ammoShotgunShell"}[type],false).ItemClass};Require(ammo.Length>0,"native ammo list");
    bag[1]=new ItemStack(new ItemValue(ammo[0].Id),50);
    int total=Count(bag,ammo[0].Id);
    Require(RebirthCamperTurretTransfer.Apply(state,bag,RebirthCamperTurretAction.Load,corner,4)==null,"load");
    Require(Count(bag,ammo[0].Id)+state.Slots[corner].Ammo[0].count==total,"load conserves ammunition");
    state=RebirthCamperTurretState.Decode(state.Encode());Require(state.Slots[corner].HasTurret&&state.Slots[corner].Ammo[0].count==50,"metadata round trip preserves item and ammo");
    Require(RebirthCamperTurretTransfer.Apply(state,bag,RebirthCamperTurretAction.Toggle,corner,0)==null&&state.Slots[corner].Enabled,"enable independently");
    Require(RebirthCamperTurretTransfer.Apply(state,bag,RebirthCamperTurretAction.Remove,corner,0)==null,"remove");
    Require(!state.Slots[corner].HasTurret&&Count(bag,ammo[0].Id)==total,"remove returns ammo and hides slot");
    report.Add("PASS "+profile.GetBlockName()+" corner="+corner+" nativeAmmo="+ammo.Length+" install/load/enable/serialize/remove conservation");
    bag=ItemStack.CreateArray(20);
   }
  }
  // Production callers work on copies and discard a rejected transaction.
  var protectedState=new RebirthCamperTurretState();var setup=ItemStack.CreateArray(1);
  setup[0]=new ItemStack(ItemClass.GetItem(RebirthCamperTurretProfiles.ItemName,false),1);
  Require(RebirthCamperTurretTransfer.Apply(protectedState,setup,RebirthCamperTurretAction.Install,0,0)==null,"full bag fixture install");
  var full=ItemStack.CreateArray(1);var filler=ItemClass.GetItem("ammo9mmBulletBall",false);full[0]=new ItemStack(filler,filler.ItemClass.Stacknumber.Value);
  var original=protectedState.Encode();var originalBag=RebirthCamperInventorySnapshot.Capture(full);
  var rejected=protectedState.Copy();var candidateBag=RebirthCamperInventorySnapshot.Restore(originalBag);
  Require(RebirthCamperTurretTransfer.Apply(rejected,candidateBag,RebirthCamperTurretAction.Remove,0,0)=="xuiCamperTurretBagFull","full backpack refuses removal");
  Require(original.SequenceEqual(protectedState.Encode())&&originalBag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(full)),"rejected candidate does not mutate committed state");
  report.Add("PASS full backpack removal refusal preserves committed item and bag.");
  // The equipment fits in the sole empty cell, but its remaining ammo does not.
  // Production discards both candidate grids on refusal, including this late failure.
  var loadedState=protectedState.Copy();
  loadedState.Slots[0].Ammo[0]=new ItemStack(ItemClass.GetItem("ammoShotgunSlug",false),7);
  var oneEmpty=ItemStack.CreateArray(1);
  var loadedBefore=loadedState.Encode();var emptyBefore=RebirthCamperInventorySnapshot.Capture(oneEmpty);
  var loadedCandidate=loadedState.Copy();var bagCandidate=RebirthCamperInventorySnapshot.Restore(emptyBefore);
  Require(RebirthCamperTurretTransfer.Apply(loadedCandidate,bagCandidate,RebirthCamperTurretAction.Remove,0,0)=="xuiCamperTurretBagFull","turret fits but ammunition does not");
  Require(loadedBefore.SequenceEqual(loadedState.Encode())&&emptyBefore.SequenceEqual(RebirthCamperInventorySnapshot.Capture(oneEmpty)),"late removal refusal retains committed turret and ammunition");
  report.Add("PASS late ammo-capacity failure preserves committed turret, seven slugs and backpack.");
  var selection=protectedState.Copy();var selected=selection.Slots[0];
  selected.Ammo[0]=new ItemStack(ItemClass.GetItem("ammo9mmBulletHP",false),3);
  selected.Ammo[1]=new ItemStack(ItemClass.GetItem("ammo762mmBulletAP",false),4);
  selected.Ammo[2]=new ItemStack(ItemClass.GetItem("ammoShotgunSlug",false),5);
  Require(RebirthCamperTurretTransfer.Apply(selection,setup,RebirthCamperTurretAction.SelectAmmo,0,2)==null&&selected.LoadedAmmo==2,"select third mixed stack");
  Require(selection.Copy().Slots[0].LoadedAmmo==2,"selection persists");
  var legacy=Legacy(selection,1);
  Require(RebirthCamperTurretState.Decode(legacy).Slots[0].LoadedAmmo==0,"V1 migrates to first nonempty stack without losing ammo");
  var v2=RebirthCamperTurretState.Decode(Legacy(selection,2));Require(v2.Slots[0].LoadedAmmo==2&&v2.Slots.All(s=>s.Silencer.IsEmpty()),"V2 preserves selected ammo and defaults silencers empty");
  selected.Ammo[2]=ItemStack.Empty.Clone();selected.ResolveAmmo();Require(selected.LoadedAmmo==0,"third empties then first");
  selected.Ammo[2]=new ItemStack(ItemClass.GetItem("ammoShotgunSlug",false),5);selected.ResolveAmmo();Require(selected.LoadedAmmo==0,"refill does not steal selection");
  selected.Ammo[0]=ItemStack.Empty.Clone();selected.ResolveAmmo();Require(selected.LoadedAmmo==1,"first empties then second");
  selected.Ammo[1]=ItemStack.Empty.Clone();selected.ResolveAmmo();Require(selected.LoadedAmmo==2,"second empties then third");
  selected.Ammo[2]=ItemStack.Empty.Clone();selected.ResolveAmmo();Require(selected.LoadedAmmo== -1,"no ammunition");
  Require(RebirthCamperTurretTransfer.Apply(selection,setup,RebirthCamperTurretAction.SelectAmmo,0,0)!=null,"empty selection rejected");
  report.Add("PASS selected mixed stack persists; depletion falls back first-to-last; refill preserves loaded slot; V1 migration.");
  var attachment=protectedState.Copy();var mods=ItemStack.CreateArray(2);mods[0]=new ItemStack(ItemClass.GetItem("modGunSoundSuppressorSilencer",false),2);
  Require(RebirthCamperTurretTransfer.Apply(attachment,mods,RebirthCamperTurretAction.InstallSilencer,0,0)==null&&mods[0].count==1,"silencer install consumes exactly one");
  Require(attachment.Copy().Slots[0].Silencer.count==1,"V3 persists silencer");
  Require(RebirthCamperTurretTransfer.Apply(attachment,mods,RebirthCamperTurretAction.RemoveSilencer,0,0)==null&&Count(mods,ItemClass.GetItem("modGunSoundSuppressorSilencer",false).type)==2,"silencer removal conserves modifier");
  foreach(var test in new[]{new[]{"ammo9mmBulletBall","gunHandgunT3SMG5"},new[]{"ammoShotgunShell","gunShotgunT2PumpShotgun"},new[]{"ammo762mmBulletBall","gunMGT2TacticalAR"}})
  {
   var ammo=ItemClass.GetItem(test[0],false);var plain=RebirthCamperTurretFiringProfiles.Resolve(ammo,ItemStack.Empty);
   var quiet=RebirthCamperTurretFiringProfiles.Resolve(ammo,new ItemStack(ItemClass.GetItem("modGunSoundSuppressorSilencer",false),1));
   Require(plain.Weapon==test[1]&&plain.Interval>0&&quiet.Interval>0&&plain.Sound!=quiet.Sound,"native firing/suppressed profile "+test[0]);
   report.Add("PROFILE "+test[0]+" weapon="+plain.Weapon+" normal="+plain.Sound+" suppressed="+quiet.Sound+" interval="+plain.Interval);
  }
  report.Add("PASS silencer conservation/V3 and native weapon firing profiles. Detached fixtures; actual audio/cadence/gameplay not proven.");
  var rifleAmmo=ItemClass.GetItem("ammo762mmBulletBall",false);
  var assault=RebirthCamperTurretFiringProfiles.Resolve(rifleAmmo,ItemStack.Empty,false);
  var sniper=RebirthCamperTurretFiringProfiles.Resolve(rifleAmmo,ItemStack.Empty,true);
  Require(sniper.Weapon=="gunRifleT3SniperRifle"&&sniper.Interval>assault.Interval,"sniper uses native slower rifle cadence");
  Require(sniper.Range>assault.Range&&assault.Range>0,"native sniper/assault ranges differ");
  var rangeState=protectedState.Copy();rangeState.Slots[0].Ammo[0]=new ItemStack(rifleAmmo.Clone(),1);
  Require(Math.Abs(RebirthCamperTurretProfiles.Range(rangeState.Slots[0])-assault.Range)<.001f,"ammo without range inherits native assault baseline");
  rangeState.Slots[0].SniperMode=true;Require(Math.Abs(RebirthCamperTurretProfiles.Range(rangeState.Slots[0])-sniper.Range)<.001f,"sniper baseline reaches UI and targeting range");
  Require(RebirthCamperTurretTransfer.Apply(attachment,mods,RebirthCamperTurretAction.ToggleSniper,0,0)==null&&attachment.Copy().Slots[0].SniperMode,"sniper preference persists in V4");
  var v3=Legacy(attachment,3);
  Require(!RebirthCamperTurretState.Decode(v3).Slots[0].SniperMode,"V3 defaults to assault");
  Require(RebirthCamperTurretFiringProfiles.Zoom(rifleAmmo,true)==8&&RebirthCamperTurretFiringProfiles.Zoom(rifleAmmo,false)==4,"rifle mode zoom");
  foreach(var zoomAmmo in new[]{"ammoShotgunShell","ammoJunkTurretRegular","ammoJunkTurretShell","ammoJunkTurretAP"})
   Require(RebirthCamperTurretFiringProfiles.Zoom(ItemClass.GetItem(zoomAmmo,false),true)==2,"2x zoom "+zoomAmmo);
  Require(RebirthCamperTurretFiringProfiles.Resolve(ItemClass.GetItem("ammo9mmBulletBall",false),ItemStack.Empty,true).Weapon=="gunHandgunT3SMG5","sniper preference does not change non-762 firing");
  report.Add("PASS V4 sniper preference/V3 migration, native sniper cadence and ammo-dependent magnifications. Actual CCTV input remains a gameplay gate.");
  var optics=protectedState.Copy();var scopeBag=ItemStack.CreateArray(12);
  optics.Slots[0].Ammo[0]=new ItemStack(rifleAmmo.Clone(),10);
  for(int i=0;i<3;i++)
  {
   var scope=ItemClass.GetItem(RebirthCamperTurretOptics.Items[i],false);
   scopeBag[i]=new ItemStack(scope.Clone(),1);scopeBag[i+3]=new ItemStack(scope.Clone(),1);
   Require(RebirthCamperTurretTransfer.Apply(optics,scopeBag,RebirthCamperTurretAction.InstallScope,0,(i<<2)|i)==null&&Count(scopeBag,scope.type)==1,"scope debit "+i);
  }
  Require(RebirthCamperTurretOptics.Available(optics.Slots[0]).SequenceEqual(new[]{1,2,4}),"assault cap excludes installed 8x");
  optics.Slots[0].SniperMode=true;
  Require(RebirthCamperTurretOptics.Available(optics.Copy().Slots[0]).SequenceEqual(new[]{1,2,4,8}),"V5 scopes persist; sniper unlocks supplied 8x");
  Require(RebirthCamperTurretOptics.Step(optics.Slots[0],4,1)==8&&RebirthCamperTurretOptics.Step(optics.Slots[0],8,1)==8,"wheel step and cap");
  Require(RebirthCamperTurretOptics.Step(optics.Slots[0],4,-1)==2&&RebirthCamperTurretOptics.Step(optics.Slots[0],1,-1)==1,"wheel zoom out and lower bound");
  optics.Slots[0].Ammo[0]=new ItemStack(ItemClass.GetItem("ammoShotgunShell",false),10);
  Require(RebirthCamperTurretOptics.Available(optics.Slots[0]).SequenceEqual(new[]{1,2}),"shell cap applies despite all scopes installed");
  Require(RebirthCamperTurretTransfer.Apply(optics,scopeBag,RebirthCamperTurretAction.RemoveScope,0,0)==null&&Count(scopeBag,ItemClass.GetItem(RebirthCamperTurretOptics.Items[0],false).type)==2,"scope removal conservation");
  Require(RebirthCamperTurretOptics.Available(optics.Slots[0]).SequenceEqual(new[]{1}),"removed 2x cannot zoom shotgun");
  Require(RebirthCamperTurretOptics.Step(optics.Slots[0],1,1)==1,"wheel cannot select an absent or capped scope");
  Require(Legacy(optics,4)[0]==4&&!RebirthCamperTurretState.Decode(Legacy(optics,4)).Slots[0].Scopes.Any(s=>!s.IsEmpty()),"V4 defaults scopes empty");
  Require(RebirthCamperTurretTransfer.Apply(optics,scopeBag,RebirthCamperTurretAction.Remove,0,0)==null&&Count(scopeBag,ItemClass.GetItem(RebirthCamperTurretOptics.Items[1],false).type)==2&&Count(scopeBag,ItemClass.GetItem(RebirthCamperTurretOptics.Items[2],false).type)==2,"turret removal returns remaining scopes");
  report.Add("PASS supplied scope conservation, V5 persistence/V4 migration, manual levels and ammo/mode hard caps. Auto focus and wheel still require gameplay.");
  var heldState=new RebirthCamperTurretState();var heldActor=ItemStack.CreateArray(5);
  heldActor[4]=new ItemStack(ItemClass.GetItem(RebirthCamperTurretProfiles.ItemName,false),1);
  Require(RebirthCamperTurretHeldTransfer.Apply(heldState,heldActor,RebirthCamperTurretAction.InstallHeld,1,0)==null&&heldActor[4].IsEmpty(),"held installation consumes one");
  heldActor[4]=new ItemStack(ItemClass.GetItem("ammo762mmBulletAP",false),37);
  int ammoCap=heldActor[4].itemValue.ItemClass.Stacknumber.Value;
  heldState.Slots[1].Ammo[2]=new ItemStack(heldActor[4].itemValue.Clone(),ammoCap-10);
  Require(RebirthCamperTurretHeldTransfer.Apply(heldState,heldActor,RebirthCamperTurretAction.LoadHeld,1,2)==null&&heldActor[4].count==27&&heldState.Slots[1].Ammo[2].count==ammoCap,"held ammunition fills chosen slot and preserves remainder");
  heldActor[4]=new ItemStack(ItemClass.GetItem("ammoArrowStone",false),9);
  var heldBefore=heldState.Encode();var actorBefore=RebirthCamperInventorySnapshot.Capture(heldActor);
  Require(RebirthCamperTurretHeldTransfer.Apply(heldState,heldActor,RebirthCamperTurretAction.LoadHeld,1,0)!=null&&heldBefore.SequenceEqual(heldState.Encode())&&actorBefore.SequenceEqual(RebirthCamperInventorySnapshot.Capture(heldActor)),"unsupported held ammo preserves exact actor and turret");
  heldActor[4]=new ItemStack(ItemClass.GetItem("modGunScopeLarge",false),1);
  Require(RebirthCamperTurretHeldTransfer.Apply(heldState,heldActor,RebirthCamperTurretAction.ScopeHeld,1,0)!=null&&!heldActor[4].IsEmpty(),"wrong scope slot retains held scope");
  Require(RebirthCamperTurretHeldTransfer.Apply(heldState,heldActor,RebirthCamperTurretAction.ScopeHeld,1,2)==null&&heldActor[4].IsEmpty()&&!heldState.Slots[1].Scopes[2].IsEmpty(),"held scope installation");
  Require(RebirthCamperTurretOptics.Auto(heldState.Slots[1],100,1)==1,"assault cannot auto use only installed 8x");
  heldState.Slots[1].SniperMode=true;
  Require(RebirthCamperTurretOptics.Auto(heldState.Slots[1],100,1)==8&&RebirthCamperTurretOptics.Auto(heldState.Slots[1],-1,8)==1,"auto obeys supplied scope and loses focus safely");
  report.Add("PASS detached held transfer consumption/remainder, exact rejection preservation, scope slot identity and Auto limits. Native pointer and remote acknowledgement remain live gates.");
  var empty=new RebirthCamperTurretState();Require(empty.Slots.All(s=>!s.HasTurret&&!s.Enabled),"legacy vehicles default empty/off");
  bool refused=false;try{RebirthCamperTurretState.Decode(new byte[]{255,0,0});}catch{refused=true;}Require(refused,"unsupported state rejected");
  report.Add("PASS legacy-empty defaults and invalid state rejection. Detached fixture only; gameplay acceptance requires real radial/UI and firing tests.");
  string folder=Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location),"Tools","GameBridge","out");Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"camper_turret_fixture.txt"),report);
  foreach(var line in report)SdtdConsole.Instance.Output(line);
  Log.Out("[CamperTurretFixture] PASS all profile, conservation, migration and supplied-optics assertions");
 }
 static int Count(ItemStack[] cells,int kind)=>cells.Where(s=>!s.IsEmpty()&&s.itemValue.type==kind).Sum(s=>s.count);
 static byte[] Legacy(RebirthCamperTurretState state,int version)
 {
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
  {
   writer.Write((byte)version);writer.Write(state.Revision);
   foreach(var slot in state.Slots){writer.Write(slot.Enabled);writer.Write(slot.AmmoType);var data=RebirthCamperInventorySnapshot.Capture(new[]{slot.Installed}.Concat(slot.Ammo).ToArray());writer.Write(data.Length);writer.Write(data);}
   if(version>=2)foreach(var slot in state.Slots)writer.Write(slot.SelectedAmmo);
   if(version>=3){var attachments=RebirthCamperInventorySnapshot.Capture(state.Slots.Select(slot=>slot.Silencer).ToArray());writer.Write(attachments.Length);writer.Write(attachments);}
   if(version>=4)foreach(var slot in state.Slots)writer.Write(slot.SniperMode);
   return stream.ToArray();
  }
 }
 static void Require(bool passed,string name){if(!passed)throw new InvalidOperationException("FAIL turret fixture: "+name);}
}
public sealed class ConsoleCmdRebirthCamperTurretInspect:ConsoleCmdAbstract
{
 public override string[] getCommands()=>new[]{"rbturretstate"};
 public override string getDescription()=>"Observe the closest camper's four turret slots in the disposable test save.";
 public override void Execute(List<string> args,CommandSenderInfo sender)
 {
  var world=GameManager.Instance?.World;var p=world?.GetPrimaryPlayer();if(p==null||!GameIO.GetSaveGameDir().EndsWith("CodexTest",StringComparison.OrdinalIgnoreCase))return;
  var vehicle=args.Count>0&&int.TryParse(args[0],out int id)?world.GetEntity(id) as EntityVehicle:world.GetEntitiesInBounds(typeof(EntityVehicle),new UnityEngine.Bounds(p.position,UnityEngine.Vector3.one*60),new List<Entity>()).OfType<EntityVehicle>().FirstOrDefault(RebirthCamperRadial.IsCamper);
  if(vehicle==null)throw new InvalidOperationException("Camper not found");var state=RebirthCamperTurretState.Read(vehicle);
  SdtdConsole.Instance.Output("Camper "+vehicle.entityId+" revision="+state.Revision);
  for(int i=0;i<4;i++){var s=state.Slots[i];var model=vehicle.RootTransform.GetComponentsInChildren<UnityEngine.Transform>(true).FirstOrDefault(t=>t.name=="REBIRTHCamperTurretSlot"+i);SdtdConsole.Instance.Output("Corner "+i+" installed="+s.HasTurret+" enabled="+s.Enabled+" ammo="+string.Join(",",s.Ammo.Select(a=>a.count))+" model="+(model?model.gameObject.activeInHierarchy.ToString():"MISSING"));}
 }
}
