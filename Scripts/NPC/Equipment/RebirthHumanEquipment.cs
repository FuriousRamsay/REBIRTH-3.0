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
    // Slot 10 is already the first saved cargo cell. Keep all existing meanings;
    // the separately addressed Lockpicks cell uses the final native toolbelt cell.
    internal const int LockpicksSlot=179, LockpicksToolbeltIndex=7;
    // Toolbelt index 6 is outside the existing public gear slots and is never
    // overwritten when a legacy native stack occupies it.
    internal const int ReadingToolbeltIndex=6;
    internal static bool Human(EntityRebirthNPC npc)=>RebirthHumanAiPolicy.IsHuman(npc);
    private static bool SupportedSlot(EntityRebirthHumanoidNPC npc,int slot)=>
        slot==LockpicksSlot||slot>=0&&slot<Names.Length+(npc?.bag?.SlotCount??0);
    internal static int EquippedLockpickCount(EntityRebirthHumanoidNPC npc)
    {
        var stack=Get(npc,LockpicksSlot);
        return !stack.IsEmpty()&&Accept(LockpicksSlot,stack)?stack.count:0;
    }
    internal static ItemStack Get(EntityRebirthHumanoidNPC npc,int slot)
    {
        if(npc==null||slot<0||slot>LockpicksSlot)return ItemStack.Empty.Clone();
        if(slot==LockpicksSlot)return npc.inventory!=null&&npc.inventory.SlotCount>LockpicksToolbeltIndex?
            npc.inventory.GetItem(LockpicksToolbeltIndex)?.Clone()??ItemStack.Empty.Clone():ItemStack.Empty.Clone();
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
        if(slot<0||slot>LockpicksSlot)return false;if(stack==null||stack.IsEmpty())return true;
        if(slot==LockpicksSlot)return stack.itemValue?.ItemClass!=null&&
            string.Equals(stack.itemValue.ItemClass.GetItemName(),"resourceLockPick",StringComparison.OrdinalIgnoreCase)&&
            stack.count<=Math.Max(1,stack.itemValue.ItemClass.Stacknumber.Value);
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
    internal static int Capacity(EntityRebirthHumanoidNPC npc,ItemStack pack,ItemStack chest,int baseSlots)
    {
        int bonus=0;
        if(pack!=null&&!pack.IsEmpty())
        {
            if(RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(pack.itemValue.ItemClass.GetItemName(),out var support))bonus=support.GearBagSlotBonus;
            else bonus=Math.Max(0,(int)EffectManager.GetItemValue(PassiveEffects.BagSize,pack.itemValue));
        }
        // The migrated carrying-capacity modifiers install on chest armor.
        // Read their native item effect from the candidate worn item, never
        // from cargo or from the owner's equipment.
        if(chest!=null&&!chest.IsEmpty())
            bonus+=Math.Max(0,(int)EffectManager.GetItemValue(PassiveEffects.BagSize,chest.itemValue));
        bonus+=RebirthG01NpcBackgroundEffects.BagSlotBonus(npc); // G01: NPC's own background storage bonus (Scavenger), cached read-only
        return Math.Max(1,Math.Min(169,baseSlots+bonus));
    }
    internal static bool TailEmpty(EntityRebirthHumanoidNPC npc,int size)=>npc.bag!=null&&npc.bag.ItemGrid.items.Skip(size).All(s=>s==null||s.IsEmpty());
    internal static void Put(EntityRebirthHumanoidNPC npc,int slot,ItemStack stack,bool birth=false)
    {
        if(!SupportedSlot(npc,slot))throw new ArgumentOutOfRangeException(nameof(slot));
        EnsureToolbelt(npc);
        if(slot==LockpicksSlot)npc.inventory.SetItem(LockpicksToolbeltIndex,stack.Clone());
        else if(slot>=10)npc.bag.SetSlot(slot-10,stack.Clone());
        else if(slot<4||slot==9)npc.equipment.SetSlotItem(slot==9?(int)EquipmentSlots.Backpack:slot,stack.IsEmpty()?ItemValue.None:stack.itemValue.Clone());
        else npc.inventory.SetItem(slot-4,stack.Clone());
        if(Frame(Get(npc,slot))!=Frame(stack))
        {
            // Birth gear may adopt the native Meta initialisation; the live cell (not the pre-write frame) is what custody observes.
            bool adopt=birth&&RebirthP2NativeWriteDiagnostics.IsMetaOnly(stack,Get(npc,slot));
            RebirthP2NativeWriteDiagnostics.Report(npc,slot,stack,Get(npc,slot),adopt?"adopted-meta":"rejected");
            if(!adopt)throw new IOException("Native equipment write was retained by another owner");
        }
        // A write into the currently held toolbelt cell must refresh the native held item,
        // otherwise the old weapon's effects/model persist after a swap or removal.
        if((slot>=4&&slot<9||slot==LockpicksSlot)&&npc.inventory.SelectedSlot==(slot==LockpicksSlot?LockpicksToolbeltIndex:slot-4)){npc.inventory.ReconcileHeldItem();RebirthHumanWeaponPassives.SyncNow(npc);}
        // SetSlotItem only RAISES slotsSetFlags/slotsChangedFlags (Equipment.cs BuildSlot 531-536); onSelfEquipStart/Changed fire from
        // FireEventsForSetSlots/ChangedSlots (624-686), which nothing on the NPC path calls. Fire them once now (both clear their flags: idempotent).
        if(slot<4||slot==9){npc.equipment.FireEventsForSetSlots();npc.equipment.FireEventsForChangedSlots();}
        if(slot<4)RebirthG04NpcWornEffects.SyncNow(npc); // G04: project worn armor-mod CVars immediately (read-only on gear)
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
    internal static XElement Read(EntityRebirthHumanoidNPC npc)
    {
        if(RebirthNpcAggregatePersistenceStore.TryGet(npc.RebirthRuntimeState.StableId,out var person))
            foreach(string text in person.UnknownOptionalElements??Array.Empty<string>())
            {var x=XElement.Parse(text);if(x.Name=="humanEquipment")return x;}
        return new XElement("humanEquipment",new XAttribute("version",1),new XAttribute("base",npc.bag?.SlotCount??16));
    }
    internal static void Save(EntityRebirthHumanoidNPC npc,XElement x)
    {
        if(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(npc,x))throw new IOException("Equipment native custody unavailable");
        string image=x.ToString(SaveOptions.DisableFormatting);var id=npc.RebirthRuntimeState.StableId;
        if(!RebirthNpcAggregatePersistenceStore.Mutate(id,p=>p.UnknownOptionalElements=(p.UnknownOptionalElements??Array.Empty<string>()).Where(t=>XElement.Parse(t).Name!="humanEquipment").Concat(new[]{image}).ToArray()))throw new IOException("Equipment checkpoint unavailable");
        RebirthNpcInventoryPersistenceStore.Save();
        RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
        RebirthNpcAggregatePersistenceStore.Save();
        if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(id,out var saved)||!(saved.UnknownOptionalElements??Array.Empty<string>()).Contains(image)||saved.NativeReconstruction==null)throw new IOException("Equipment checkpoint readback or original native snapshot unavailable");
    }
    internal static bool TryHoldBagLiterature(EntityRebirthHumanoidNPC npc,int bagIndex,out string reason)
    {
        reason="unavailable";
        if(!ThreadManager.IsMainThread()||npc?.world?.IsRemote()!=false||npc.IsDead()||
            npc.IsPreparedRestorationPending||npc.Hand==null||npc.Hand.IsBusy||npc.bag==null||npc.inventory==null||npc.GetAttackTarget()!=null||
            bagIndex<0||bagIndex>=npc.bag.SlotCount||!ReferenceEquals(npc.world.GetEntity(npc.entityId),npc))return false;
        try
        {
            EnsureToolbelt(npc);
            var x=Read(npc);
            if(RebirthNpcWorkNativeTransferOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                RebirthNpcStructureRepairOriginal.HasNativePending(npc.RebirthRuntimeState.StableId)||
                RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                x.Element("swap") is XElement swap&&(string)swap.Attribute("done")!="1")return false;
            if(!RecoverBookMove(npc,x)||x.Element("bookMove")!=null)return false;
            var book=npc.bag.ItemGrid[bagIndex];
            if(book==null||book.IsEmpty()||book.itemValue?.ItemClass==null||book.count!=1||
                !RebirthProgressionRuntimeConfig.TryGetLiterature(book.itemValue.ItemClass.GetItemName(),out var definition)||
                definition==null||!string.Equals(definition.Kind,"discovery",StringComparison.OrdinalIgnoreCase))return false;
            var occupied=npc.inventory.GetItem(ReadingToolbeltIndex);
            if(occupied!=null&&!occupied.IsEmpty())return false;
            if(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(npc,x))return false;
            int oldSelected=npc.inventory.SelectedSlot;
            var source=Binding(x,bagIndex+10);
            string identity=(string)source.Attribute("id");
            var move=new XElement("bookMove",new XAttribute("phase","prepared"),new XAttribute("bag",bagIndex),
                new XAttribute("selected",oldSelected),new XAttribute("stack",Frame(book)),new XAttribute("id",identity));
            x.Add(move);Save(npc,x);
            npc.bag.SetSlot(bagIndex,ItemStack.Empty.Clone());
            npc.inventory.SetItem(ReadingToolbeltIndex,book.Clone());
            source.SetAttributeValue("area","toolbelt");source.SetAttributeValue("index",ReadingToolbeltIndex);
            npc.inventory.SetSelectedSlot(ReadingToolbeltIndex);npc.inventory.ReconcileHeldItem();
            move.SetAttributeValue("phase","held");Save(npc,x);
            reason="held";return true;
        }
        catch(Exception error)
        {
            Log.Warning("[REBIRTH NPC Reading] book transfer retained: "+error.Message);
            // The prepared intent was saved before either native cell changed.
            // Reconcile the live actor now too; recovery must not wait for reload.
            try{RecoverBookMove(npc,Read(npc));}
            catch(Exception recovery){Log.Warning("[REBIRTH NPC Reading] book transfer recovery pending: "+recovery.Message);}
            return false;
        }
    }
    internal static bool TryReturnHeldLiterature(EntityRebirthHumanoidNPC npc)
    {
        if(!ThreadManager.IsMainThread()||npc?.world?.IsRemote()!=false||npc.Hand==null||npc.Hand.IsBusy)return false;
        try
        {
            var x=Read(npc);var move=x.Element("bookMove");
            if(move==null)return true;
            if(!RecoverBookMove(npc,x))return false;
            move=x.Element("bookMove");if(move==null)return true;
            if((string)move.Attribute("phase")!="held")return false;
            int bag=(int)move.Attribute("bag");
            if(bag<0||bag>=npc.bag.SlotCount||npc.bag.ItemGrid[bag]!=null&&!npc.bag.ItemGrid[bag].IsEmpty()||
                Frame(npc.inventory.GetItem(ReadingToolbeltIndex))!=(string)move.Attribute("stack"))return false;
            move.SetAttributeValue("phase","returning");Save(npc,x);
            var book=npc.inventory.GetItem(ReadingToolbeltIndex).Clone();
            npc.inventory.SetItem(ReadingToolbeltIndex,ItemStack.Empty.Clone());npc.bag.SetSlot(bag,book);
            var binding=(x.Element("bindings")?.Elements("binding")??Enumerable.Empty<XElement>())
                .Single(b=>(string)b.Attribute("id")==""+(string)move.Attribute("id"));
            binding.SetAttributeValue("area","bag");binding.SetAttributeValue("index",bag);
            int previous=(int)move.Attribute("selected");
            if(npc.inventory.SelectedSlot==ReadingToolbeltIndex&&previous>=0&&previous<npc.inventory.SlotCount)
                npc.inventory.SetSelectedSlot(previous);
            npc.inventory.ReconcileHeldItem();move.Remove();Save(npc,x);return true;
        }
        catch(Exception error)
        {
            Log.Warning("[REBIRTH NPC Reading] book return retained: "+error.Message);
            try{RecoverBookMove(npc,Read(npc));}
            catch(Exception recovery){Log.Warning("[REBIRTH NPC Reading] book return recovery pending: "+recovery.Message);}
            return false;
        }
    }
    internal static bool MatchesHeldLiteratureMove(EntityRebirthHumanoidNPC npc)
    {
        if(npc?.inventory==null||npc.bag==null||npc.inventory.SelectedSlot!=ReadingToolbeltIndex)return false;
        try
        {
            var move=Read(npc).Element("bookMove");
            if(move==null||(string)move.Attribute("phase")!="held")return false;
            int bag=(int)move.Attribute("bag");
            if(bag<0||bag>=npc.bag.SlotCount||npc.bag.ItemGrid[bag]!=null&&!npc.bag.ItemGrid[bag].IsEmpty())return false;
            return Frame(npc.inventory.GetItem(ReadingToolbeltIndex))==(string)move.Attribute("stack");
        }
        catch{return false;}
    }
    // Called by the reload restore after every other saved cell is live (its custody save then observes a whole body).
    internal static bool RecoverBookMoveAfterRestore(EntityRebirthHumanoidNPC npc,XElement x)=>RecoverBookMove(npc,x);
    private static bool RecoverBookMove(EntityRebirthHumanoidNPC npc,XElement x)
    {
        var move=x.Element("bookMove");if(move==null)return true;
        int bag=(int)move.Attribute("bag");if(bag<0||bag>=npc.bag.SlotCount||npc.inventory.SlotCount<=ReadingToolbeltIndex)return false;
        string expected=(string)move.Attribute("stack");
        bool inBag=Frame(npc.bag.ItemGrid[bag])==expected;
        bool inHand=Frame(npc.inventory.GetItem(ReadingToolbeltIndex))==expected;
        if(inBag&&inHand)return false; // duplicate physical owners: retain for audit
        if(!inBag&&!inHand)
        {
            // A process may stop between the two native SetSlot calls. The
            // saved pre-move intent contains the exact stack and its custody ID.
            // Restore only when no live cell already holds that same payload.
            if(npc.bag.ItemGrid[bag]!=null&&!npc.bag.ItemGrid[bag].IsEmpty()||
                npc.inventory.GetItem(ReadingToolbeltIndex)!=null&&
                !npc.inventory.GetItem(ReadingToolbeltIndex).IsEmpty()||
                npc.bag.ItemGrid.items.Any(s=>Frame(s)==expected)||
                npc.inventory.ItemGrid.items.Any(s=>Frame(s)==expected)||
                npc.equipment.ItemGrid.items.Any(s=>Frame(s)==expected))return false;
            npc.bag.SetSlot(bag,Decode(expected));
            inBag=true;
        }
        var binding=(x.Element("bindings")?.Elements("binding")??Enumerable.Empty<XElement>())
            .SingleOrDefault(b=>(string)b.Attribute("id")==""+(string)move.Attribute("id"));
        if(binding==null)return false;
        if(inBag)
        {
            binding.SetAttributeValue("area","bag");binding.SetAttributeValue("index",bag);
            move.Remove();Save(npc,x);return true;
        }
        binding.SetAttributeValue("area","toolbelt");binding.SetAttributeValue("index",ReadingToolbeltIndex);
        move.SetAttributeValue("phase","held");
        npc.inventory.SetSelectedSlot(ReadingToolbeltIndex);npc.inventory.ReconcileHeldItem();
        Save(npc,x);return true;
    }
    internal static void CheckpointNativeOwners()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false)return;
        bool any=false;
        foreach(var state in RebirthNpcRuntimeRegistry.GetSnapshot())
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out int id)||world.GetEntity(id) is not EntityRebirthHumanoidNPC npc||npc.IsDead()||
                npc.IsPreparedRestorationPending||!RebirthP2NpcEquipmentRestore.IsSettled(npc)||!RebirthP2NpcEquipmentRestore.ShouldCheckpoint(npc))continue;
            any=true;var x=Read(npc);
            RebirthP2NpcEquipmentRestore.ApplyBirthAttributes(npc,x); // CVars the engine does not persist (C2C-105/109): birth marker, failed-birth intent
            if(!RebirthNpcInventoryTransactionService.ObserveHumanEquipment(npc,x))throw new IOException("Human native equipment checkpoint custody conflict");
            string image=x.ToString(SaveOptions.DisableFormatting);
            RebirthNpcAggregatePersistenceStore.Mutate(state.StableId,p=>p.UnknownOptionalElements=(p.UnknownOptionalElements??Array.Empty<string>()).Where(t=>XElement.Parse(t).Name!="humanEquipment").Concat(new[]{image}).ToArray());
        }
        if(any)RebirthNpcInventoryPersistenceStore.Save();
    }
    private static XElement Binding(XElement x,int slot)
    {
        string area=slot==LockpicksSlot?"toolbelt":slot>=10?"bag":slot<4||slot==9?"equipment":"toolbelt";
        int index=slot==LockpicksSlot?LockpicksToolbeltIndex:slot>=10?slot-10:slot==9?12:slot<4?slot:slot-4;
        var bindings=x.Element("bindings");if(bindings==null){bindings=new XElement("bindings");x.Add(bindings);}
        var binding=bindings.Elements("binding").SingleOrDefault(b=>(string)b.Attribute("area")==area&&(int)b.Attribute("index")==index);
        if(binding==null){binding=new XElement("binding",new XAttribute("area",area),new XAttribute("index",index),new XAttribute("id",Guid.NewGuid().ToString("N")));bindings.Add(binding);}
        return binding;
    }
    private static bool Owned(EntityPlayer player,EntityRebirthHumanoidNPC npc)=>player!=null&&npc!=null&&Human(npc)&&!npc.IsDead()&&!player.IsDead()&&
        npc.RebirthRuntimeState?.OwnershipKind==RebirthNpcOwnershipKind.Player&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&
        string.Equals(owner.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase);
    internal static XElement InspectHire(EntityPlayer player,EntityRebirthHumanoidNPC npc)
    {
        if(!ThreadManager.IsMainThread()||player==null||npc?.world==null||npc.world.IsRemote()||
            !ReferenceEquals(player.world,npc.world)||!ReferenceEquals(npc.world,GameManager.Instance?.World)||
            !ReferenceEquals(npc.world.GetEntity(npc.entityId),npc)||!Human(npc)||npc.IsDead()||player.IsDead()||
            (player.position-npc.position).sqrMagnitude>RebirthCompanionService.StorageDistance*RebirthCompanionService.StorageDistance||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc))
            return Error("xuiRbNpcEquipmentUnavailable");
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var actor))return Error("xuiRbNpcEquipmentUnavailable");
        var runtime=npc.RebirthRuntimeState;
        var permissions=new RebirthNpcDefaultInteractionPermissionProvider().Resolve(actor.CanonicalId,runtime.StableId,runtime);
        if((permissions&RebirthNpcInteractionPermissions.Hire)==0)return Error("xuiRbNpcEquipmentUnavailable");
        try
        {
            var result=Snapshot(npc,Read(npc));
            result.SetAttributeValue("readonly",1);
            result.Element("swap")?.Remove();
            return result;
        }
        catch(Exception e){Log.Warning("[REBIRTH NPC Hire Equipment] "+e.GetType().Name);return Error("xuiRbNpcEquipmentUnavailable");}
    }
    internal static XElement Handle(EntityPlayer player,EntityRebirthHumanoidNPC npc,int action,int target,int source,string operation)
    {
        if(!ThreadManager.IsMainThread()||npc?.world==null||npc.world.IsRemote()||!ReferenceEquals(npc.world,GameManager.Instance?.World)||
            !ReferenceEquals(npc.world.GetEntity(npc.entityId),npc)||!Owned(player,npc)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||
            RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc))return Error("xuiRbNpcEquipmentUnavailable");
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryGet(npc.RebirthRuntimeState.StableId,out _))RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
            if(!RebirthNpcAggregatePersistenceStore.TryGet(npc.RebirthRuntimeState.StableId,out _))return Error("xuiRbNpcEquipmentBusy");
            // Optional compare-and-swap suffix: "<sourceFrame>|<expectedTargetFrame>".
            string expectedTarget=null;int guard=action==1&&operation!=null?operation.IndexOf('|'):-1;
            if(guard>=0){expectedTarget=operation.Substring(guard+1);operation=operation.Substring(0,guard);}
            if(action==0){RebirthP2NpcEquipmentRestore.TryReconcileOnce(npc);RebirthP2NpcBagCapacityMigration.TryReconcile(npc);} // retry for loads that preceded the aggregate store
            // A reloaded body whose saved cells are not restored yet must not save/exchange: observing its empty cells would erase the records.
            if(action!=0&&RebirthP2NpcEquipmentRestore.IsRestorePending(npc))return Error("xuiRbNpcEquipmentBusy");
            var x=Read(npc);var swap=x.Element("swap");
            if(action==1&&(RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                RebirthNpcWorkNativeCreditOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                x.Element("bookMove")!=null||x.Elements("lockpickAttempt").Any(a=>(int?)a.Attribute("phase")!=2)||x.Elements("workMove").Any(work=>
                (int?)work.Attribute("phase")!=4)))return Error("xuiRbNpcEquipmentBusy");
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
                int capacity=slot==1||slot==9?Capacity(npc,slot==9?Decode(after):Get(npc,9),
                    slot==1?Decode(after):Get(npc,1),(int)x.Attribute("base")):npc.bag.SlotCount;
                if(!TailEmpty(npc,capacity))return Error("xuiRbNpcEquipmentOverflow");
                Binding(x,slot).SetAttributeValue("id",operation);
                Put(npc,slot,Decode(after));if(slot==1||slot==9)npc.bag.Resize(new Vector2i(capacity,1));
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
                if(source<0||source>=169||!SupportedSlot(npc,target)||!RebirthHumanAiPolicy.Ready(npc)||npc.GetAttackTarget()!=null||npc.Hand.IsBusy)return Error("xuiRbNpcEquipmentBusy");
                if(!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||saved.bDead||saved.id!=player.entityId||saved.deaths!=player.Died)return Error("xuiRbNpcEquipmentWaitSave");
                var cells=RebirthGearPlayerDataInventory.ReadSlots(saved,true);
                if(cells==null||source>=cells.Length||Frame(cells[source])!=operation)return Error("xuiRbNpcEquipmentWaitSave");
                if(!Accept(target,cells[source]))return Error("xuiRbNpcEquipmentWrongItem");
                var old=Get(npc,target);if(!old.IsEmpty()&&!old.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack,source))return Error("xuiRbNpcEquipmentWrongItem");if(Frame(old)==Frame(cells[source]))return Snapshot(npc,x);if(expectedTarget!=null&&expectedTarget!=Frame(old))return Error("xuiRbNpcEquipmentStale");
                if((target==1||target==9)&&!TailEmpty(npc,Capacity(npc,target==9?cells[source]:Get(npc,9),
                    target==1?cells[source]:Get(npc,1),(int)x.Attribute("base"))))return Error("xuiRbNpcEquipmentOverflow");
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
        root.Add(new XElement("slot",new XAttribute("id",LockpicksSlot),Frame(Get(npc,LockpicksSlot))));
        root.SetAttributeValue("gear",RebirthP2NpcEquipmentReadModel.GearToken(root));
        var inventory=RebirthNpcInventoryTransactionService.GetSnapshot(npc.RebirthRuntimeState.StableId);RebirthNpcInventoryTransactionService.TryGetExistingNativeCustody(npc.RebirthRuntimeState.StableId,out var custody,out _);
        root.SetAttributeValue("legacy",inventory.Quantities.Any(p=>p.Value>(custody?.GetQuantity(p.Key)??0))?1:0);
        root.Add(new XElement("storage",npc.bag.ItemGrid.items.Select((s,i)=>new XElement("cell",new XAttribute("id",i),Frame(s)))));
        var swap=state.Element("swap");if(swap!=null){if((string)swap.Attribute("done")!="1")root.Add(new XElement(swap));else root.SetAttributeValue("settled",(string)swap.Attribute("id"));}return root;
    }
}





