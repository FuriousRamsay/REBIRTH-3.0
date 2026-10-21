using System;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using UnityEngine;

// Full native items stay in the NPC's native equipment/toolbelt. The optional
// aggregate extension holds only the original one-cell exchange until its owner save proves it.
internal static class RebirthHumanEquipment
{
    internal const string NativeMode="rbNpcNativeEquipment", Hold="rbNpcEquipPending";
    internal static readonly string[] Names={"Head","Chest","Hands","Feet","Melee","Ranged","Ammo1","Ammo2","Ammo3","Backpack"};
    internal static bool Human(EntityRebirthNPC npc)=>RebirthHumanAiPolicy.IsHuman(npc);
    internal static ItemStack Get(EntityRebirthHumanoidNPC npc,int slot)
    {
        if(npc==null||slot<0||slot>=Names.Length+169)return ItemStack.Empty.Clone();
        if(slot>=10)return npc.bag!=null&&slot-10<npc.bag.SlotCount?npc.bag.ItemGrid[slot-10].Clone():ItemStack.Empty.Clone();
        if(slot>=4&&slot<9&&(npc.inventory==null||npc.inventory.SlotCount<=slot-4))return ItemStack.Empty.Clone();
        return (slot<4?npc.equipment.ItemGrid[slot]:slot==9?npc.equipment.ItemGrid[(int)EquipmentSlots.Backpack]:npc.inventory.GetItem(slot-4))?.Clone()??ItemStack.Empty.Clone();
    }
    internal static string Frame(ItemStack stack)=>stack==null||stack.IsEmpty()?"":stack.count+":"+RebirthNativeItemCodec.Encode(stack.itemValue);
    internal static ItemStack Decode(string frame)
    {
        if(string.IsNullOrEmpty(frame))return ItemStack.Empty.Clone();
        int p=frame.IndexOf(':');if(p<1||!int.TryParse(frame.Substring(0,p),out int count)||count<1||count>65535||frame.Length>262144||
            !RebirthNativeItemCodec.TryDecode(frame.Substring(p+1),out var value)||value==null||value.IsEmpty())throw new InvalidDataException("Equipment item frame");
        return new ItemStack(value,count);
    }
    internal static bool Accept(int slot,ItemStack stack)
    {
        if(slot<0||slot>=Names.Length+169)return false;if(stack==null||stack.IsEmpty())return true;
        if(slot>=10)return stack.CanMoveTo(XUiC_ItemStack.StackLocationTypes.LootContainer)&&stack.itemValue.ItemClass.CanPlaceInContainer();
        var value=stack.itemValue;var item=value?.ItemClass;if(item==null||stack.count>Math.Max(1,item.Stacknumber.Value))return false;
        if(slot<4)return stack.count==1&&item is ItemClassArmor armor&&(int)armor.EquipSlot==slot;
        if(slot==9)return stack.count==1&&(item is ItemClassArmor pack&&pack.EquipSlot==EquipmentSlots.Backpack||
            RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(item.GetItemName(),out var support)&&support.GearSlotId==RebirthSurvivorGearService.BackpackSlotId);
        var action=item.Actions!=null&&item.Actions.Length>0?item.Actions[0]:null;
        if(slot==4)return stack.count==1&&(action is ItemActionMelee||action is ItemActionDynamicMelee);
        if(slot==5)return stack.count==1&&action is ItemActionRanged&&!(action is ItemActionLauncher)&&!(action is ItemActionCatapult);
        return item.ItemTags.Test_AnySet(FastTags<TagGroup.Global>.Parse("ammo"));
    }
    internal static int Capacity(EntityRebirthHumanoidNPC npc,ItemStack pack,int baseSlots)
    {
        int bonus=0;
        if(pack!=null&&!pack.IsEmpty())
        {
            if(RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(pack.itemValue.ItemClass.GetItemName(),out var support))bonus=support.GearBagSlotBonus;
            else bonus=Math.Max(0,(int)EffectManager.GetItemValue(PassiveEffects.BagSize,pack.itemValue));
        }
        return Math.Max(1,Math.Min(169,baseSlots+bonus));
    }
    internal static bool TailEmpty(EntityRebirthHumanoidNPC npc,int size)=>npc.bag!=null&&npc.bag.ItemGrid.items.Skip(size).All(s=>s==null||s.IsEmpty());
    internal static void Put(EntityRebirthHumanoidNPC npc,int slot,ItemStack stack)
    {
        EnsureToolbelt(npc);
        if(slot>=10)npc.bag.SetSlot(slot-10,stack.Clone());
        else if(slot<4||slot==9)npc.equipment.SetSlotItem(slot==9?(int)EquipmentSlots.Backpack:slot,stack.IsEmpty()?ItemValue.None:stack.itemValue.Clone());
        else npc.inventory.SetItem(slot-4,stack.Clone());
        if(Frame(Get(npc,slot))!=Frame(stack))throw new IOException("Native equipment write was retained by another owner");
        npc.Buffs.SetCustomVar(NativeMode,1f,true);npc.RebirthRuntimeState.MarkChanged();npc.RefreshRebirthModelEquipment();
    }
    // Vanilla NPCs have one slot. Grow only on an authorized equipment edit;
    // preserve every exact stack and the selected native index.
    private static void EnsureToolbelt(EntityRebirthHumanoidNPC npc)
    {
        if(npc.inventory.SlotCount>=8)return;
        var original=npc.inventory;var items=original.ItemGrid.CloneItems();int selected=original.SelectedSlot;
        npc.inventory=new Inventory(npc,8);npc.inventory.isReading=true;
        try{for(int i=0;i<items.Length;i++)npc.inventory.SetItem(i,items[i]);npc.inventory.SetSelectedSlot(selected);}
        finally{npc.inventory.isReading=false;}
        npc.inventory.ReconcileHeldItem();
    }
    private static XElement Read(EntityRebirthHumanoidNPC npc)
    {
        if(RebirthNpcAggregatePersistenceStore.TryGet(npc.RebirthRuntimeState.StableId,out var person))
            foreach(string text in person.UnknownOptionalElements??Array.Empty<string>())
            {var x=XElement.Parse(text);if(x.Name=="humanEquipment")return x;}
        return new XElement("humanEquipment",new XAttribute("version",1),new XAttribute("base",npc.bag?.SlotCount??16));
    }
    private static void Save(EntityRebirthHumanoidNPC npc,XElement x)
    {
        if(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(npc,x))throw new IOException("Equipment native custody unavailable");
        string image=x.ToString(SaveOptions.DisableFormatting);var id=npc.RebirthRuntimeState.StableId;
        if(!RebirthNpcAggregatePersistenceStore.Mutate(id,p=>p.UnknownOptionalElements=(p.UnknownOptionalElements??Array.Empty<string>()).Where(t=>XElement.Parse(t).Name!="humanEquipment").Concat(new[]{image}).ToArray()))throw new IOException("Equipment checkpoint unavailable");
        RebirthNpcInventoryPersistenceStore.Save();
        RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
        RebirthNpcAggregatePersistenceStore.Save();
        if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var saved)||!(saved.UnknownOptionalElements??Array.Empty<string>()).Contains(image)||saved.NativeReconstruction==null)throw new IOException("Equipment checkpoint readback or original native snapshot unavailable");
    }
    internal static void CheckpointNativeOwners()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false)return;
        bool any=false;
        foreach(var state in RebirthNpcRuntimeRegistry.GetSnapshot())
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out int id)||world.GetEntity(id) is not EntityRebirthHumanoidNPC npc||npc.IsDead()||
                npc.IsPreparedRestorationPending||npc.Buffs.GetCustomVar(NativeMode)!=1f)continue;
            any=true;var x=Read(npc);if(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(npc,x))throw new IOException("Human native equipment checkpoint custody conflict");
            string image=x.ToString(SaveOptions.DisableFormatting);
            RebirthNpcAggregatePersistenceStore.Mutate(state.StableId,p=>p.UnknownOptionalElements=(p.UnknownOptionalElements??Array.Empty<string>()).Where(t=>XElement.Parse(t).Name!="humanEquipment").Concat(new[]{image}).ToArray());
        }
        if(any)RebirthNpcInventoryPersistenceStore.Save();
    }
    private static XElement Binding(XElement x,int slot)
    {
        string area=slot>=10?"bag":slot<4||slot==9?"equipment":"toolbelt";int index=slot>=10?slot-10:slot==9?12:slot<4?slot:slot-4;
        var bindings=x.Element("bindings");if(bindings==null){bindings=new XElement("bindings");x.Add(bindings);}
        var binding=bindings.Elements("binding").SingleOrDefault(b=>(string)b.Attribute("area")==area&&(int)b.Attribute("index")==index);
        if(binding==null){binding=new XElement("binding",new XAttribute("area",area),new XAttribute("index",index),new XAttribute("id",Guid.NewGuid().ToString("N")));bindings.Add(binding);}
        return binding;
    }
    private static bool Owned(EntityPlayer player,EntityRebirthHumanoidNPC npc)=>player!=null&&npc!=null&&Human(npc)&&!npc.IsDead()&&!player.IsDead()&&
        npc.RebirthRuntimeState?.OwnershipKind==RebirthNpcOwnershipKind.Player&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&
        string.Equals(owner.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase);
    internal static XElement Handle(EntityPlayer player,EntityRebirthHumanoidNPC npc,int action,int target,int source,string operation)
    {
        if(!ThreadManager.IsMainThread()||npc?.world==null||npc.world.IsRemote()||!ReferenceEquals(npc.world,GameManager.Instance?.World)||
            !ReferenceEquals(npc.world.GetEntity(npc.entityId),npc)||!Owned(player,npc)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||
            RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc))return Error("xuiRbNpcEquipmentUnavailable");
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryGet(npc.RebirthRuntimeState.StableId,out _))RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
            if(!RebirthNpcAggregatePersistenceStore.TryGet(npc.RebirthRuntimeState.StableId,out _))return Error("xuiRbNpcEquipmentBusy");
            var x=Read(npc);var swap=x.Element("swap");
            RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner);
            if(swap!=null&&(string)swap.Attribute("done")!="1"&&(string)swap.Attribute("owner")!=owner.CanonicalId)return Error("xuiRbNpcEquipmentBusy");
            if(action==2)
            {
                if(swap==null||operation!=(string)swap.Attribute("id"))return Error("xuiRbNpcEquipmentUnavailable");
                if((string)swap.Attribute("done")=="1"){Save(npc,x);swap.SetAttributeValue("published",1);Save(npc,x);return Snapshot(npc,x);}
                if(RebirthCamperCharacterAccess.CurrentCreation(player)!=(string)swap.Attribute("creation"))return Error("xuiRbNpcEquipmentConflict");
                if(!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||saved.deaths<(int)swap.Attribute("deaths")||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,"rbNpcEquip_"+operation,out float phase,out bool present)||!present||phase!=2f)
                    return Snapshot(npc,x);
                // The saved terminal receipt proves the original one-cell exchange.
                // Its returned item may legitimately move after closing or after death.
                int slot=(int)swap.Attribute("slot");string before=(string)swap.Element("after"),after=(string)swap.Element("before");
                string actual=Frame(Get(npc,slot));if(actual!=""&&actual!=after)return Error("xuiRbNpcEquipmentConflict");
                int capacity=slot==9?Capacity(npc,Decode(after),(int)x.Attribute("base")):npc.bag.SlotCount;
                if(!TailEmpty(npc,capacity))return Error("xuiRbNpcEquipmentOverflow");
                Binding(x,slot).SetAttributeValue("id",operation);
                Put(npc,slot,Decode(after));if(slot==9)npc.bag.Resize(new Vector2i(capacity,1));
                npc.Buffs.SetCustomVar(Hold,0f,true);swap.SetAttributeValue("done",1);Save(npc,x);swap.SetAttributeValue("published",1);Save(npc,x);
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcAppearanceEquipmentV2>().Setup(npc));
                return Snapshot(npc,x);
            }
            if(action==1)
            {
                if(RebirthNpcTradeService.BlocksOwnerPreparedNpc(npc.RebirthRuntimeState.StableId)||RebirthNpcMetabolismCustody.Blocks(npc.RebirthRuntimeState.StableId)||
                    RemoteResourceTransactions.ActionBlocksNpc(npc.RebirthRuntimeState.StableId)||RebirthNpcSaveQuarantineBundle.BlocksRetainedTrade(npc.RebirthRuntimeState.StableId))return Error("xuiRbNpcEquipmentBusy");
                if((player.position-npc.position).sqrMagnitude>RebirthCompanionService.StorageDistance*RebirthCompanionService.StorageDistance)return Error("xuiRebirthStorageDistanceRequirement");
                if(swap!=null&&(string)swap.Attribute("done")!="1")return Snapshot(npc,x);
                if(source<0||source>=169||target<0||target>=Names.Length+npc.bag.SlotCount||!RebirthHumanAiPolicy.Ready(npc)||npc.GetAttackTarget()!=null||npc.Hand.IsBusy)return Error("xuiRbNpcEquipmentBusy");
                if(!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||saved.bDead||saved.id!=player.entityId||saved.deaths!=player.Died)return Error("xuiRbNpcEquipmentWaitSave");
                var cells=RebirthGearPlayerDataInventory.ReadSlots(saved,true);
                if(cells==null||source>=cells.Length||Frame(cells[source])!=operation)return Error("xuiRbNpcEquipmentWaitSave");
                if(!Accept(target,cells[source]))return Error("xuiRbNpcEquipmentWrongItem");
                var old=Get(npc,target);if(!old.IsEmpty()&&!old.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack,source))return Error("xuiRbNpcEquipmentWrongItem");if(Frame(old)==Frame(cells[source]))return Snapshot(npc,x);
                if(target==9&&!TailEmpty(npc,Capacity(npc,cells[source],(int)x.Attribute("base"))))return Error("xuiRbNpcEquipmentOverflow");
                if(npc.Buffs.GetCustomVar(NativeMode)!=1f)
                {npc.Buffs.SetCustomVar(NativeMode,1f,true);RebirthNpcEquipmentService.ReleaseAll(npc.RebirthRuntimeState.StableId);}
                if(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(npc,x))return Error("xuiRbNpcEquipmentConflict");
                x.Element("swap")?.Remove();
                swap=new XElement("swap",new XAttribute("id",Guid.NewGuid().ToString("N")),new XAttribute("owner",owner.CanonicalId),new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(player)),new XAttribute("deaths",player.Died),
                    new XAttribute("slot",target),new XAttribute("source",source),new XAttribute("oldStack",(string)Binding(x,target).Attribute("id")),new XAttribute("done",0),new XElement("before",Frame(cells[source])),new XElement("after",Frame(old)));
                // Publish the original before touching its native projection. A
                // failed checkpoint must never strand a displaced actual item.
                x.Add(swap);npc.Buffs.SetCustomVar(Hold,1f,true);Save(npc,x);
                Put(npc,target,ItemStack.Empty.Clone());Save(npc,x);
            }
            if(x.Element("swap")!=null&&(string)x.Element("swap").Attribute("done")!="1")
            {
                swap=x.Element("swap");int targetSlot=(int)swap.Attribute("slot");string current=Frame(Get(npc,targetSlot));
                if(current!="")
                {
                    if(current!=(string)swap.Element("after"))return Error("xuiRbNpcEquipmentConflict");
                    Put(npc,targetSlot,ItemStack.Empty.Clone());
                }
                Save(npc,x);
            }
            else if(swap!=null&&(string)swap.Attribute("published")!="1")
            {Save(npc,x);swap.SetAttributeValue("published",1);Save(npc,x);}
            return Snapshot(npc,x);
        }
        catch(Exception e){Log.Warning("[REBIRTH NPC Equipment] "+e.GetType().Name+": "+e.Message);return Error("xuiRbNpcEquipmentConflict");}
    }
    private static XElement Error(string key)=>new XElement("equipment",new XAttribute("error",key));
    private static XElement Snapshot(EntityRebirthHumanoidNPC npc,XElement state)
    {
        var root=new XElement("equipment",new XAttribute("entity",npc.entityId),new XAttribute("stable",npc.RebirthRuntimeState.StableId.ToString()),
            new XAttribute("capacity",npc.bag?.SlotCount??0),new XAttribute("holding",npc.inventory.SelectedSlot),new XAttribute("native",npc.Buffs.GetCustomVar(NativeMode)==1f?1:0),Names.Select((n,i)=>new XElement("slot",new XAttribute("id",i),Frame(Get(npc,i)))));
        var inventory=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);RebirthNpcInventoryTransactionService.TryGetExistingNativeCustody(npc.RebirthRuntimeState.StableId,out var custody,out _);
        root.SetAttributeValue("legacy",inventory.Quantities.Any(p=>p.Value>(custody?.GetQuantity(p.Key)??0))?1:0);
        root.Add(new XElement("storage",npc.bag.ItemGrid.items.Select((s,i)=>new XElement("cell",new XAttribute("id",i),Frame(s)))));
        var swap=state.Element("swap");if(swap!=null){if((string)swap.Attribute("done")!="1")root.Add(new XElement(swap));else root.SetAttributeValue("settled",(string)swap.Attribute("id"));}return root;
    }
}





