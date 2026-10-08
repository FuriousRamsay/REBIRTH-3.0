using System;using System.Xml.Linq;
static class HandMaterializationFixture
{
 internal static EntityRebirthHumanoidNPC Actor(out RebirthNpcPersistentRecord person)
 {
  var npc=new EntityRebirthHumanoidNPC{entityId=7,RebirthRuntimeState=new RebirthNpcRuntimeState{StableId=new RebirthNpcStableId(1,1),ProfileId="specialist.medic"}};GameManager.Instance.World=npc.world;
  var root=Program.HeaderMigrationRoot();root.SetAttributeValue("version",4);root.Element("slots").RemoveNodes();RebirthNpcNativeHeaderProjection.TryCapture(npc,out var header);RebirthNpcNativeGeometryProjection.TryCapture(npc,out var geometry);RebirthNpcNativeHand.TryCapture(npc,root.Element("slots"),out var hand);root.Add(header.Write(),geometry.Write(),hand.Write());
  if(!RebirthNpcNativeReconstruction.TryRead(root,out var reconstruction))throw new Exception("hand source fixture shape");
  person=new RebirthNpcPersistentRecord{Identity=new RebirthNpcIdentityRecord{StableNpcId=npc.RebirthRuntimeState.StableId},Profile=new RebirthNpcProfileBindingRecord{ProfileId=npc.RebirthRuntimeState.ProfileId},Presence=new RebirthNpcPresenceRecord{EmbodimentGeneration=1},NativeReconstruction=reconstruction};RebirthNpcAggregatePersistenceStore.Saved=person;
  Hand.CleanupCalls=Hand.EquipCalls=Hand.QuestCalls=0;Hand.ThrowEquipOnce=Hand.ThrowCleanupOnce=Hand.ChangeSavedDuringEquip=false;Hand.OtherQuestValue=null;
  if(!RebirthNpcPreparedPhysicalHold.TryHold(npc,person))throw new Exception("hand source fixture original suspension");return npc;
 }
 internal static int Run()
 {
  int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};var npc=Actor(out var person);
  check(ActualHandReconcileGate.Prefix(null),"null native hand unheld");check(!ActualHandReconcileGate.Prefix(npc.Hand)&&!ActualHandEquipGate.Prefix(npc.Hand,npc.Hand.bareHandData)&&!ActualHandModeGate.Prefix(npc.Hand)&&!ActualHandSlotGate.Prefix(npc.Hand),"all held entry points blocked without original materialization lease");
  check(!RebirthNpcPreparedHandMaterialization.IsReady(npc,person),"factory hand cannot certify original readiness");
  var firstResult=RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person);check(firstResult,"actual linked original hand owner materialization");
  check(Hand.CleanupCalls==1&&Hand.EquipCalls==1&&Hand.QuestCalls==0,"derived rebuild complete without direct original quest publication");check(RebirthNpcPreparedHandMaterialization.IsReady(npc,person)&&RebirthNpcPreparedPhysicalHold.IsPhysicallyHeld(npc,1),"readiness witness and original physical hold");
  check(!RebirthNpcPreparedHandMaterialization.Allows(npc.Hand)&&!RebirthNpcPreparedHandMaterialization.SuppressesQuest(npc.Hand.ItemValue),"materialization lease disposed after successful callbacks");
  check(RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.CleanupCalls==1&&Hand.EquipCalls==1,"already verified original hand does not replay callbacks");
  var originalHand=npc.Hand;npc.Hand=new Hand(npc);check(!RebirthNpcPreparedHandMaterialization.IsReady(npc,person),"replacement native hand cannot borrow callback witness");npc.Hand=originalHand;
  person.AggregateRevision++;check(!RebirthNpcPreparedHandMaterialization.IsReady(npc,person),"original saved revision invalidates witness");
  npc=Actor(out person);Hand.ThrowEquipOnce=true;check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&!RebirthNpcPreparedHandMaterialization.IsReady(npc,person),"half-completed native callback cannot grant readiness");check(RebirthNpcPreparedPhysicalHold.IsPhysicallyHeld(npc,1)&&Hand.QuestCalls==0&&!RebirthNpcPreparedHandMaterialization.Allows(npc.Hand),"failed callback retained exact suspension and closed lease");
  check(RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.CleanupCalls==2&&Hand.EquipCalls==2&&Hand.QuestCalls==0,"same original actor cleanup/retry recovers known callback failure");
  npc=Actor(out person);Hand.ThrowCleanupOnce=true;check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.EquipCalls==0,"cleanup failure cannot publish or certify hand");check(RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.QuestCalls==0,"same original cleanup failure retry");
  npc=Actor(out person);Hand.ChangeSavedDuringEquip=true;check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.QuestCalls==0,"original quest suppressed even if saved context changes mid-callback");check(!RebirthNpcPreparedHandMaterialization.IsReady(npc,person)&&!RebirthNpcPreparedHandMaterialization.Allows(npc.Hand),"stale callback context cannot retain witness/lease");RebirthNpcAggregatePersistenceStore.Saved=person;check(RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person),"confirmed original saved context permits exact retry");
  npc=Actor(out person);Hand.OtherQuestValue=new ItemValue();check(RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.QuestCalls==1,"unrelated item quest publication is not suppressed");Hand.OtherQuestValue=null;
  npc=Actor(out person);npc.Hand.BareHandItemValue.ItemClass.Actions=new ItemAction[]{new UnknownAction()};check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.CleanupCalls==0,"unknown native action refused before cleanup");
  npc=Actor(out person);npc.world.Remote=true;check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.CleanupCalls==0,"client cannot obtain original callback lease");
  npc=Actor(out person);person.Presence.EmbodimentGeneration=2;check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.CleanupCalls==0,"wrong original generation refused before effects");
  npc=Actor(out person);RebirthNpcPreparedEventGateInstaller.IsReady=false;check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.CleanupCalls==0,"missing installed event guards refuses materialization");RebirthNpcPreparedEventGateInstaller.IsReady=true;
  npc=Actor(out person);npc.IsPreparedRestorationPending=false;check(ActualHandReconcileGate.Prefix(npc.Hand)&&ActualHandEquipGate.Prefix(npc.Hand,npc.Hand.bareHandData),"ordinary unheld human hand entry points preserved");
  npc=Actor(out person);npc.Hand.BareHandItemValue.ItemClass.Holdable=true;RebirthNativeItemCodec.DecodedClass=npc.Hand.BareHandItemValue.ItemClass;
  check(RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&npc.Hand.ModelFixturePresent(),"actual owner seats a derived native model and suspends its new collider");
  person.AggregateRevision++;check(RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&RebirthNpcPreparedPhysicalHold.IsPhysicallyHeld(npc,1)&&Hand.CleanupCalls==2,"original derived model retirement permits exact rebuild retry");
  RebirthNativeItemCodec.DecodedClass=null;
  npc=Actor(out person);npc.Hand.entity=new Entity();check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.CleanupCalls==0,"foreign hand owner refused before cleanup");
  npc=Actor(out person);npc.Hand.bareHandData.holdingEntity=new Entity();check(!RebirthNpcPreparedHandMaterialization.TryMaterialize(npc,person)&&Hand.CleanupCalls==0,"foreign item-data owner refused before cleanup");
  return count;
 }
 private class UnknownAction:ItemAction{}
}
