using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

// One exact physical source stack enters one NPC native bag cell. Work callers
// may request this only after travel/activity arbitration; no key-based item
// reconstruction or direct source setter is used here.
internal static class RebirthNpcWorkNativeTransfer
{
    private static World recoveryWorld;
    private static float nextRecovery;
    private static int recoveryCursor;
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(world,recoveryWorld)){recoveryWorld=world;nextRecovery=0f;recoveryCursor=0;}
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||Time.unscaledTime<nextRecovery)return;
        nextRecovery=Time.unscaledTime+5f;
        if(!RebirthNpcWorkNativeTransferOriginal.TryReadPending(world,out var pending))return;
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
    private static bool Current(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        RebirthNpcWorkNativeTransferOriginal.Record row)
    {
        var world=GameManager.Instance?.World;
        return ThreadManager.IsMainThread()&&owner?.world?.IsRemote()==false&&npc?.world==world&&
            ReferenceEquals(owner.world,world)&&ReferenceEquals(world.GetEntity(owner.entityId),owner)&&
            ReferenceEquals(world.GetEntity(npc.entityId),npc)&&!owner.IsDead()&&!npc.IsDead()&&
            npc.RebirthRuntimeState?.StableId==row.Npc&&npc.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.Player&&
            RebirthOriginalActionScope.Matches(owner,row.Owner,row.Creation)&&owner.Died==row.Deaths&&
            RebirthLogisticsTransferJournal.WorldKey(world)==row.World&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)&&
            string.Equals(identity.CanonicalId,npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase)&&
            !npc.IsPreparedRestorationPending&&npc.bag?.ItemGrid!=null&&row.BagSlot<npc.bag.SlotCount;
    }
    internal static bool TryStart(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        RebirthNpcPhysicalResourceEndpoint source,int sourceSlot,int bagSlot,out Guid operation)
        =>TryStart(owner,npc,source,sourceSlot,bagSlot,Guid.NewGuid(),out operation);
    internal static bool TryStart(EntityPlayer owner,EntityRebirthHumanoidNPC npc,
        RebirthNpcPhysicalResourceEndpoint source,int sourceSlot,int bagSlot,Guid requestedOperation,out Guid operation)
    {
        operation=Guid.Empty;
        try
        {
            if(owner==null||npc?.RebirthRuntimeState==null||source==null||requestedOperation==Guid.Empty||
                npc.GetAttackTarget()!=null||npc.Hand==null||npc.Hand.IsBusy||
                RebirthNpcInteractionSessionService.IsInventoryAccessing(npc.RebirthRuntimeState.StableId)||
                !source.TryObserveExactStackForNpc(owner,npc,sourceSlot,bagSlot,out var seen)||
                !RebirthNpcWorkNativeTransferOriginal.TryPrepare(owner,npc,seen,requestedOperation,out var row)||!Current(owner,npc,row))return false;
            var equipment=RebirthHumanEquipment.Read(npc);
            if(RebirthNpcWorkNativeTransferOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                RebirthSmartLootingHumanOriginal.HasPending(npc.RebirthRuntimeState.StableId)||
                equipment.Element("bookMove")!=null||equipment.Element("swap")!=null)return false;
            if(!RebirthNpcWorkNativeTransferOriginal.TryPublish(npc,row))return false;
            operation=row.Operation;return true;
        }
        catch{return false;}
    }
    private static bool SavedCredit(RebirthNpcWorkNativeTransferOriginal.Record row)
    {
        try
        {
            if(!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(row.Npc,out var person)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(row.Npc,out var inventory)||
                inventory.NativeStacks==null)return false;
            var equipment=(person.UnknownOptionalElements??Array.Empty<string>()).Select(XElement.Parse)
                .SingleOrDefault(x=>x.Name=="humanEquipment");
            var binding=equipment?.Element("bindings")?.Elements("binding").SingleOrDefault(x=>
                (string)x.Attribute("area")=="bag"&&(int)x.Attribute("index")==row.BagSlot);
            if((string)binding?.Attribute("id")!=row.CreditId.ToString("N"))return false;
            var native=inventory.NativeStacks.Write().Elements().Select(x=>
                RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null).ToArray();
            var credited=native.SingleOrDefault(x=>x?.StackId==row.CreditId);
            var stack=RebirthHumanEquipment.Decode(row.Frame);
            return credited!=null&&credited.Count==stack.count&&
                credited.Payload==RebirthNativeItemCodec.Encode(stack.itemValue);
        }
        catch{return false;}
    }
    private static bool CreditBag(EntityRebirthHumanoidNPC npc,RebirthNpcWorkNativeTransferOriginal.Record row)
    {
        try
        {
            var equipment=RebirthHumanEquipment.Read(npc);
            var saved=equipment.Elements("workMove").SingleOrDefault(x=>(string)x.Attribute("operation")==row.Operation.ToString("N"));
            if(saved==null||!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(row.Npc,row.Operation,out var original)||
                original.Binding!=row.Binding||original.Phase!=2)return false;
            var bag=npc.bag.ItemGrid[row.BagSlot];
            var bindings=equipment.Element("bindings");
            if(bindings==null){bindings=new XElement("bindings");equipment.Add(bindings);}
            var binding=bindings.Elements("binding").SingleOrDefault(x=>(string)x.Attribute("area")=="bag"&&
                (int)x.Attribute("index")==row.BagSlot);
            string credit=row.CreditId.ToString("N");
            if(binding!=null&&(string)binding.Attribute("id")!=credit||
                bindings.Elements("binding").Any(x=>!ReferenceEquals(x,binding)&&(string)x.Attribute("id")==credit))return false;
            // A Save may fail after the native cell changed. Rebind that exact
            // physical item on retry, but reject any differently identified
            // saved native owner that would make ObserveHumanEquipment count it
            // twice. The immutable work original is the pre-credit intent.
            if(!RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(row.Npc,out var inventory)||
                inventory.NativeStacks==null)return false;
            var intended=RebirthHumanEquipment.Decode(row.Frame);
            var matching=inventory.NativeStacks.Write().Elements().Select(x=>
                RebirthNpcNativeStackRecord.TryRead(x,out var item)?item:null)
                .Where(x=>x!=null&&x.Count==intended.count&&x.Payload==RebirthNativeItemCodec.Encode(intended.itemValue))
                .ToArray();
            if(matching.Any(x=>x.StackId!=row.CreditId&&
                !bindings.Elements("binding").Any(b=>(string)b.Attribute("id")==x.StackId.ToString("N")&&
                    !((string)b.Attribute("area")=="bag"&&(int)b.Attribute("index")==row.BagSlot))))return false;
            if(bag!=null&&!bag.IsEmpty())
            {
                if(RebirthHumanEquipment.Frame(bag)!=row.Frame)return false;
            }
            else
            {
                npc.bag.SetSlot(row.BagSlot,intended);
            }
            if(binding==null){binding=new XElement("binding",new XAttribute("area","bag"),
                new XAttribute("index",row.BagSlot));bindings.Add(binding);}
            binding.SetAttributeValue("id",credit);
            RebirthHumanEquipment.Save(npc,equipment);
            return SavedCredit(row);
        }
        catch{return false;}
    }
    internal static bool TryAdvance(EntityPlayer owner,EntityRebirthHumanoidNPC npc,Guid operation)
    {
        try
        {
            if(npc?.RebirthRuntimeState==null||!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(npc.RebirthRuntimeState.StableId,
                operation,out var row)||!Current(owner,npc,row))return false;
            if(row.Phase==4)return SavedCredit(row);
            bool CurrentOriginal()=>Current(owner,npc,row)&&
                RebirthNpcWorkNativeTransferOriginal.TryReadSaved(row.Npc,row.Operation,out var saved)&&saved.Binding==row.Binding;
            if(!RebirthNpcOriginalEffectAdmission.TryReserveNpcWork(owner,row,out var npcAdmission)||
                !npcAdmission.Current()||
                !RebirthRemoteResourceSourceCustody.TryReserveNpcWork(owner,row,CurrentOriginal,out var lease))return false;
            if(row.Phase==0)
            {
                if(!lease.TryObserveSaved(0))return false;
                row.Phase=1;row.Attempted=1;
                return RebirthNpcWorkNativeTransferOriginal.TryPublish(npc,row);
            }
            if(row.Phase==1)
            {
                if(!lease.TryObserveSaved(1,out var proof))
                {
                    if(!lease.TryObserveSaved(0)||!lease.ApplyStage(1)||!lease.TryObserveSaved(1,out proof))return false;
                }
                row.SourceProof=proof;row.Phase=2;row.Published=1;
                return RebirthNpcWorkNativeTransferOriginal.TryPublish(npc,row);
            }
            if(!lease.TryObserveSaved(1,out var sourceProof)||sourceProof!=row.SourceProof)return false;
            if(row.Phase==2)
            {
                if(!CreditBag(npc,row))return false;
                row.Phase=3;return RebirthNpcWorkNativeTransferOriginal.TryPublish(npc,row);
            }
            if(row.Phase!=3||!SavedCredit(row))return false;
            row.Phase=4;
            if(!RebirthNpcWorkNativeTransferOriginal.TryPublish(npc,row))return false;
            lease.Dispose();npcAdmission.Dispose();return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH NPC Work] native transfer retained: "+error.Message);return false;}
    }
}
