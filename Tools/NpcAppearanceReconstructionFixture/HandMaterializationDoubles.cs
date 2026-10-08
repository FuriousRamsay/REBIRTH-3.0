using System;
public class ItemAction{} public class ItemActionData{} public class ItemActionMelee:ItemAction{} public enum ItemActionFiringState{Off,On} public class ItemActionRanged:ItemAction{public class ItemActionDataRanged:ItemActionData{public ItemActionFiringState state;public bool isReloading,isWeaponReloading,isChangingAmmoType,burstShotStarted,burstShotFinished;}} public class ItemActionZoom:ItemAction{public class ItemActionDataZoom:ItemActionData{public bool bZoomInProgress,aimingValue;public object aimingCoroutine;}}
public class FixtureAvatar{public UnityEngine.Transform RightHand;public UnityEngine.Transform GetRightHandTransform()=>RightHand;}
public static class RebirthNpcPreparedEventGateInstaller{internal static bool IsReady=true;}
public static class RebirthNpcAggregatePersistenceStore{internal static RebirthNpcPersistentRecord Saved;internal static bool TryGet(RebirthNpcStableId id,out RebirthNpcPersistentRecord value){value=Saved;return value?.Identity?.StableNpcId==id;}}
public class ItemInventoryData
{
 public System.Collections.Generic.List<ItemActionData> actionData=new System.Collections.Generic.List<ItemActionData>(); public Entity holdingEntity;public ItemStack Stack=new ItemStack(new ItemValue(),0);public ItemValue PhysicalItemValue=new ItemValue();public bool IsEquipped,NeedsRebuild;public UnityEngine.Transform model;
 public ItemStack itemStack=>Stack;public ItemValue itemValue=>Stack.itemValue;public ItemClass item=>Stack.itemValue.ItemClass;public bool HasItem=>!Stack.IsEmpty();
 public void Rebuild(){PhysicalItemValue=itemValue.Clone();NeedsRebuild=false;}
}
public class Hand
{
 public enum HoldingMode{Current=0,Bare=1,Transient=2}public Entity entity;public HoldingMode mode;public bool IsSwitching,ActionRunning;public object switchingCoroutine;public ItemInventoryData holstering,equipped;public ItemInventoryData[] slotDatas;public NativeState toolbelt;
 public ItemInventoryData bareHandData=new ItemInventoryData(),transientData=new ItemInventoryData();
 public ItemValue BareHandItemValue=>bareHandData.itemValue;public bool IsActionRunning()=>ActionRunning;
 public Hand(){}public Hand(Entity owner){entity=owner;bareHandData.holdingEntity=transientData.holdingEntity=owner;}
 public ItemInventoryData Held=>mode==HoldingMode.Current&&toolbelt!=null&&toolbelt.ItemGrid[toolbelt.SelectedSlot]!=null&&!toolbelt.ItemGrid[toolbelt.SelectedSlot].IsEmpty()?slotDatas[toolbelt.SelectedSlot]:bareHandData;
 public ItemValue ItemValue=>Held.itemValue;public bool ModelFixturePresent()=>equipped?.model!=null;
 public static int CleanupCalls,EquipCalls,QuestCalls;public static bool ThrowEquipOnce,ThrowCleanupOnce,ChangeSavedDuringEquip;public static ItemValue OtherQuestValue;
 public void Cleanup(){CleanupCalls++;if(ThrowCleanupOnce){ThrowCleanupOnce=false;throw new InvalidOperationException("native cleanup fixture failure");}if(equipped?.model!=null){var m=equipped.model;if(m.Parent!=null)m.Parent.Children.Remove(m);m.Parent=null;m.gameObject.SetActive(false);equipped.model=null;}equipped=null;slotDatas=null;toolbelt=null;}
 public void SetBareHandItem(ItemValue value){bareHandData.Stack=new ItemStack(value,0);bareHandData.Rebuild();}
 public void BindToolbelt(NativeState belt){toolbelt=belt;slotDatas=new ItemInventoryData[belt.ItemGrid.Length];for(int i=0;i<slotDatas.Length;i++){slotDatas[i]=new ItemInventoryData{holdingEntity=entity,Stack=belt.ItemGrid[i]??new ItemStack(new ItemValue(),0)};slotDatas[i].Rebuild();}}
 public void Reconcile(){if(!ActualHandReconcileGate.Prefix(this))return;if(equipped==null)Equip(Held);}
 public void Equip(ItemInventoryData data){if(!ActualHandEquipGate.Prefix(this,data))return;equipped=data;data.IsEquipped=true;EquipCalls++;if(ChangeSavedDuringEquip){ChangeSavedDuringEquip=false;RebirthNpcAggregatePersistenceStore.Saved=null;}if(ActualHeldQuestGate.Prefix(data.itemValue))QuestCalls++;if(OtherQuestValue!=null&&ActualHeldQuestGate.Prefix(OtherQuestValue))QuestCalls++;if(ThrowEquipOnce){ThrowEquipOnce=false;throw new InvalidOperationException("native equip fixture failure");}if(data.item.CanHold()){var parent=((EntityRebirthHumanoidNPC)entity).emodel.avatarController.RightHand;data.model=new UnityEngine.Transform{Parent=parent};parent.Children.Add(data.model);data.model.Components.Add(new UnityEngine.SphereCollider{transform=data.model});}}
}
