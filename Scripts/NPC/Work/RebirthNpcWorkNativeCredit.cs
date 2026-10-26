using System;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// Second physical leg of an organization move. The first leg's B05 saved
// native bag receipt is its only source. Destination writes go exclusively
// through the existing one-use native source-custody lease.
internal static class RebirthNpcWorkNativeCredit
{
    private static World recoveryWorld;
    private static float nextRecovery;
    private static int recoveryCursor;
    internal static bool TryStart(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        Guid sourceOperation,RebirthNpcPhysicalResourceEndpoint destination,
        int bagSlot,int destinationSlot,Guid creditOperation,bool merge=false,bool sameContainer=false)
    {
        try
        {
            if(owner==null||npc?.RebirthRuntimeState==null||destination==null||
                creditOperation==Guid.Empty||sourceOperation==Guid.Empty||
                RebirthNpcWorkNativeCreditOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(npc.RebirthRuntimeState.StableId,
                    sourceOperation,out var source)||source.Phase!=4||source.BagSlot!=bagSlot||
                !RebirthNpcWorkNativeTransfer.TryAdvance(owner,npc,sourceOperation)||
                !destination.TryObserveExactCreditForNpc(owner,npc,bagSlot,destinationSlot,
                    source.Frame,out var seen,merge)||(seen.DestinationId==source.Source&&!sameContainer)||
                !RebirthNpcWorkNativeCreditOriginal.TryPrepare(owner,npc,source,seen,
                    creditOperation,out var record,merge,sameContainer))return false;
            return RebirthNpcWorkNativeCreditOriginal.TryPublish(npc,record);
        }
        catch{return false;}
    }
    private static bool Current(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        RebirthNpcWorkNativeCreditOriginal.Record row)
    {
        var world=GameManager.Instance?.World;
        return ThreadManager.IsMainThread()&&world?.IsRemote()==false&&
            owner!=null&&npc!=null&&!owner.IsDead()&&!npc.IsDead()&&
            ReferenceEquals(owner.world,world)&&ReferenceEquals(npc.world,world)&&
            ReferenceEquals(world.GetEntity(owner.entityId),owner)&&
            ReferenceEquals(world.GetEntity(npc.entityId),npc)&&
            npc.RebirthRuntimeState?.StableId==row.Npc&&
            npc.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.Player&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)&&
            string.Equals(identity.CanonicalId,npc.RebirthRuntimeState.OwnerId,
                StringComparison.OrdinalIgnoreCase)&&
            RebirthOriginalActionScope.Matches(owner,row.Owner,row.Creation)&&
            owner.Died==row.Deaths&&
            RebirthLogisticsTransferJournal.WorldKey(world)==row.World&&
            !npc.IsPreparedRestorationPending&&npc.bag?.ItemGrid!=null&&
            row.BagSlot<npc.bag.SlotCount&&
            !RebirthNpcInteractionSessionService.IsInventoryAccessing(row.Npc)&&
            (RebirthNpcWorkNativeCreditOriginal.TryParseBagSource(row.Source,out _)||
            RebirthNpcWorkNativeTransferOriginal.TryReadSaved(row.Npc,row.SourceOperation,
                out var source)&&source.Phase==4&&source.Owner==row.Owner&&
            source.Source==row.Source&&source.Frame==row.Frame&&source.BagSlot==row.BagSlot);
    }
    // Bag-source rows: the exact stack must still be bound and saved before any destination write.
    private static bool BagSourceExact(EntityRebirthHumanoidNPC npc,RebirthNpcWorkNativeCreditOriginal.Record row)=>
        !RebirthNpcWorkNativeCreditOriginal.TryParseBagSource(row.Source,out var id)||row.Phase>=5||
        RebirthHumanEquipment.Frame(npc.bag.ItemGrid[row.BagSlot])==row.Frame&&SavedBoundBagStack(npc.RebirthRuntimeState.StableId,row.BagSlot,id,row.Frame);
    private static bool SavedBoundBagStack(RebirthNpcStableId npcId,int bagSlot,Guid boundId,string frame)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npcId,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(npcId,out var inventory)||
                inventory.NativeStacks==null)return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .SingleOrDefault(x=>x.Name=="humanEquipment");
            var binding=equipment?.Element("bindings")?.Elements("binding").SingleOrDefault(x=>
                (string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==bagSlot);
            if((string)binding?.Attribute("id")!=boundId.ToString("N"))return false;
            var stack=RebirthHumanEquipment.Decode(frame);
            var saved=inventory.NativeStacks.Write().Elements().Select(x=>
                RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null)
                .SingleOrDefault(x=>x!=null&&x.StackId==boundId);
            return saved!=null&&saved.Count==stack.count&&saved.Payload==RebirthNativeItemCodec.Encode(stack.itemValue);
        }
        catch{return false;}
    }
    // Bag-source start: any exact bound NPC bag stack (saved binding + saved native-stack record + live frame)
    // is credited whole into one empty cell of an authorized owned container. Driven to completion by the
    // unchanged TryAdvance/Tick chain (write-ahead lease, saved proof, saved bag clear).
    internal static bool TryStartBag(EntityPlayer owner,EntityRebirthHumanoidNPC npc,Guid sourceOperation,
        int bagSlot,Guid boundStackId,RebirthNpcPhysicalResourceEndpoint destination,int destinationSlot,
        Guid creditOperation)
    {
        try
        {
            if(owner==null||npc?.RebirthRuntimeState==null||destination==null||npc.bag?.ItemGrid==null||
                creditOperation==Guid.Empty||sourceOperation==Guid.Empty||boundStackId==Guid.Empty||
                bagSlot<0||bagSlot>=npc.bag.SlotCount||
                RebirthNpcWorkNativeCreditOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId))return false;
            // Same owner proof the advance chain requires (Current); refuse before any Phase-0 row is written,
            // because a row the owner can never advance would block every credit for this worker.
            if(npc.RebirthRuntimeState.OwnershipKind!=RebirthNpcOwnershipKind.Player||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var ownerIdentity)||
                !string.Equals(ownerIdentity.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase)||
                !RebirthOriginalActionScope.Matches(owner,ownerIdentity.StorageKey,RebirthCamperCharacterAccess.CurrentCreation(owner)))return false;
            string frame=RebirthHumanEquipment.Frame(npc.bag.ItemGrid[bagSlot]);
            if(string.IsNullOrEmpty(frame)||!SavedBoundBagStack(npc.RebirthRuntimeState.StableId,bagSlot,boundStackId,frame)||
                !destination.TryObserveExactCreditForNpc(owner,npc,bagSlot,destinationSlot,frame,out var seen)||
                !RebirthNpcWorkNativeCreditOriginal.TryPrepareBag(owner,npc,sourceOperation,boundStackId,seen,
                    creditOperation,out var record))return false;
            return RebirthNpcWorkNativeCreditOriginal.TryPublish(npc,record);
        }
        catch{return false;}
    }
    // P7 pickup receipt as the source: the saved npcStationQueueOriginal row of the finished craft/cook
    // operation (phase completed) names the bag slot, bound stack id and exact output frame.
    internal static bool TryStartFromPickupReceipt(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        Guid pickupOperation,RebirthNpcPhysicalResourceEndpoint destination,int destinationSlot,Guid creditOperation)
    {
        try
        {
            if(npc?.RebirthRuntimeState==null||
                !RebirthNpcStationQueueOriginal.TryRead(npc.RebirthRuntimeState.StableId,pickupOperation,out var row)||
                (string)row.Attribute("phase")!="completed"||
                !Guid.TryParseExact((string)row.Attribute("pickupCredit"),"N",out var bound)||
                (int?)row.Attribute("pickupBag") is not int bag||bag<0||bag>=npc.bag.SlotCount||
                RebirthHumanEquipment.Frame(npc.bag.ItemGrid[bag])!=(string)row.Attribute("outFrame"))return false;
            return TryStartBag(owner,npc,pickupOperation,bag,bound,destination,destinationSlot,creditOperation);
        }
        catch{return false;}
    }
    private static bool SavedBagClear(RebirthNpcWorkNativeCreditOriginal.Record row)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(row.Npc,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(row.Npc,out var snapshot)||
                snapshot.NativeStacks==null)return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .SingleOrDefault(x=>x.Name=="humanEquipment");
            if(equipment?.Element("bindings")?.Elements("binding").Any(x=>
                (string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==row.BagSlot)==true)return false;
            return !snapshot.NativeStacks.Write().Elements().Select(x=>
                RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null)
                .Any(x=>x!=null&&x.StackId==SourceCreditId(row));
        }
        catch{return false;}
    }
    private static Guid SourceCreditId(RebirthNpcWorkNativeCreditOriginal.Record row)=>
        RebirthNpcWorkNativeCreditOriginal.TryParseBagSource(row.Source,out var bagCredit)?bagCredit:
        RebirthNpcWorkNativeTransferOriginal.TryReadSaved(row.Npc,row.SourceOperation,out var source)
            ?source.CreditId:Guid.Empty;
    internal static bool HasSavedCompletion(RebirthNpcWorkNativeCreditOriginal.Record row)=>row!=null&&row.Phase==6&&
        RebirthNpcWorkNativeCreditOriginal.TryReadSaved(row.Npc,row.Operation,out var saved)&&saved.Binding==row.Binding&&
        saved.Phase==6&&saved.CreditProof==row.CreditProof&&SavedBagClear(saved);
    private static bool ClearBag(EntityRebirthHumanoidNPC npc,RebirthNpcWorkNativeCreditOriginal.Record row)
    {
        try
        {
            var equipment=RebirthHumanEquipment.Read(npc);
            var saved=equipment.Elements("workCredit").SingleOrDefault(x=>
                (string)x.Attribute("operation")==row.Operation.ToString("N"));
            if(saved==null||!RebirthNpcWorkNativeCreditOriginal.TryReadSaved(row.Npc,row.Operation,
                out var original)||original.Binding!=row.Binding||original.Phase!=5)return false;
            var bag=npc.bag.ItemGrid[row.BagSlot];
            if(bag!=null&&!bag.IsEmpty()&&RebirthHumanEquipment.Frame(bag)!=row.Frame)return false;
            var binding=equipment.Element("bindings")?.Elements("binding").SingleOrDefault(x=>
                (string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==row.BagSlot);
            if(bag!=null&&!bag.IsEmpty()&&binding==null)return false;
            if(binding!=null&&(string)binding.Attribute("id")!=SourceCreditId(row).ToString("N"))
                return false;
            if(bag!=null&&!bag.IsEmpty())npc.bag.SetSlot(row.BagSlot,ItemStack.Empty.Clone());
            binding?.Remove();
            RebirthHumanEquipment.Save(npc,equipment);
            return SavedBagClear(row);
        }
        catch{return false;}
    }
    internal static bool TryAdvance(EntityPlayer owner,EntityRebirthHumanoidNPC npc,Guid operation)
    {
        try
        {
            if(npc?.RebirthRuntimeState==null||
                !RebirthNpcWorkNativeCreditOriginal.TryReadSaved(npc.RebirthRuntimeState.StableId,
                    operation,out var row)||!Current(owner,npc,row))return false;
            if(row.Phase==6)return SavedBagClear(row);
            if(!BagSourceExact(npc,row))return false;
            bool OriginalCurrent()=>Current(owner,npc,row)&&
                RebirthNpcWorkNativeCreditOriginal.TryReadSaved(row.Npc,row.Operation,out var saved)&&
                saved.Binding==row.Binding;
            if(!RebirthRemoteResourceSourceCustody.TryReserveNpcWorkCredit(owner,row,
                OriginalCurrent,out var lease))return false;
            if(row.Phase==0)
            {
                if(!lease.TryObserveSaved(0))return false;
                row.Phase=1;row.Attempted=1;
                return RebirthNpcWorkNativeCreditOriginal.TryPublish(npc,row);
            }
            if(row.Phase==1)
            {
                if(!lease.TryObserveSaved(1,out _)&&
                    (!lease.TryObserveSaved(0)||!lease.ApplyStage(1)||
                     !lease.TryObserveSaved(1,out _)))return false;
                row.Phase=2;row.Published=1;
                return RebirthNpcWorkNativeCreditOriginal.TryPublish(npc,row);
            }
            if(!lease.TryObserveSaved(1,out _)&&row.Phase==2)return false;
            if(row.Phase==2)
            {
                row.Phase=3;row.Attempted=2;
                return RebirthNpcWorkNativeCreditOriginal.TryPublish(npc,row);
            }
            if(row.Phase==3)
            {
                if(!lease.TryObserveSaved(2,out var proof)&&
                    !(lease.ApplyStage(2)&&
                      lease.TryObserveSaved(2,out proof)))return false;
                row.CreditProof=proof;row.Phase=4;row.Published=2;
                return RebirthNpcWorkNativeCreditOriginal.TryPublish(npc,row);
            }
            if(!lease.TryObserveSaved(2,out var creditProof)||creditProof!=row.CreditProof)return false;
            if(row.Phase==4)
            {
                row.Phase=5;
                return RebirthNpcWorkNativeCreditOriginal.TryPublish(npc,row);
            }
            if(row.Phase==5)
            {
                if(!ClearBag(npc,row))return false;
                row.Phase=6;
                return RebirthNpcWorkNativeCreditOriginal.TryPublish(npc,row);
            }
            return row.Phase==6&&SavedBagClear(row);
        }
        catch(Exception error)
        {
            Log.Warning("[REBIRTH NPC Work] exact credit retained: "+error.Message);
            return false;
        }
    }
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(world,recoveryWorld)){recoveryWorld=world;nextRecovery=0f;recoveryCursor=0;}
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||
            Time.unscaledTime<nextRecovery)return;
        nextRecovery=Time.unscaledTime+5f;
        if(!RebirthNpcWorkNativeCreditOriginal.TryReadPending(world,out var pending))return;
        if(pending.Length==0){nextRecovery=Time.unscaledTime+20f;return;}
        int count=Math.Min(4,pending.Length);
        for(int i=0;i<count;i++)
        {
            var row=pending[(recoveryCursor+i)%pending.Length];
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(row.Npc,out int entity)||
                world.GetEntity(entity) is not EntityRebirthHumanoidNPC npc||npc.IsDead())continue;
            var owner=world.Players?.list?.FirstOrDefault(player=>player!=null&&
                !player.IsDead()&&RebirthOriginalActionScope.Matches(player,row.Owner,row.Creation));
            if(owner!=null)TryAdvance(owner,npc,row.Operation);
        }
        recoveryCursor=(recoveryCursor+count)%pending.Length;
    }
}
