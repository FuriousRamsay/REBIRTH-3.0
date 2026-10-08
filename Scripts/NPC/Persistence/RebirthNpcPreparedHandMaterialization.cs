using System;using System.Linq;
// Verified original candidate only. No native creation, publication or identity store.
internal static class RebirthNpcPreparedHandMaterialization
{
    private sealed class Lease:IDisposable
    {
        internal EntityRebirthHumanoidNPC Npc;internal RebirthNpcPersistentRecord Person;internal ItemValue QuestValue;
        public void Dispose(){if(ReferenceEquals(active,this))active=null;}
    }
    private sealed class ReadyStamp{internal World World;internal Hand Hand;internal int NativeId;internal uint Generation,Revision;internal string Checksum;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityRebirthHumanoidNPC,ReadyStamp> Ready=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityRebirthHumanoidNPC,ReadyStamp>();
    [ThreadStatic] private static Lease active;
    internal static bool Allows(Hand hand)=>active!=null&&ReferenceEquals(hand,active.Npc.Hand)&&Current(active);
    internal static bool SuppressesQuest(ItemValue value)=>active!=null&&value!=null&&ReferenceEquals(active.QuestValue,value);
    internal static bool AllowsEquip(Hand hand,ItemInventoryData data)
    {
        if(!Allows(hand)||data==null)return false;active.QuestValue=data.itemValue;return true;
    }
    private static bool Current(Lease lease)
    {
        var npc=lease.Npc;var person=lease.Person;var world=GameManager.Instance?.World;
        if(npc==null||person?.Identity==null||person.Profile==null||person.Presence==null)return false;
        return world!=null&&!world.IsRemote()&&ReferenceEquals(world,npc.world)&&world.GetEntity(npc.entityId)==null&&npc.IsPreparedRestorationPending&&
            RebirthNpcPreparedPhysicalHold.IsOriginalBindingCurrent(npc,person.Presence.EmbodimentGeneration)&&
            npc.RebirthRuntimeState?.StableId==person.Identity.StableNpcId&&npc.RebirthRuntimeState.ProfileId==person.Profile.ProfileId&&
            RebirthNpcAggregatePersistenceStore.TryGet(person.Identity.StableNpcId,out var saved)&&saved.AggregateRevision==person.AggregateRevision&&saved.AggregateChecksum==person.AggregateChecksum;
    }
    internal static bool Qualified(ItemValue value)=>RebirthNpcIdleHandActionQualification.Qualified(value);
    internal static bool TryMaterialize(EntityRebirthHumanoidNPC npc,RebirthNpcPersistentRecord person)
    {
        if(active!=null||npc==null||person?.NativeReconstruction?.Hand==null||!RebirthNpcPreparedEventGateInstaller.IsReady)return false;
        var lease=new Lease{Npc=npc,Person=person};if(!Current(lease)||!RebirthNpcPreparedPhysicalHold.TryHold(npc,person))return false;
        var evidence=person.NativeReconstruction.Hand;
        if(npc.Hand==null||!ReferenceEquals(npc.Hand.entity,npc)||npc.Hand.IsSwitching||npc.Hand.switchingCoroutine!=null||npc.Hand.holstering!=null||
            npc.inventory.SelectedSlot!=evidence.Selected||!RebirthNativeItemCodec.TryDecode(evidence.BarePayload,out var bare)||!Qualified(bare))return false;
        for(int index=0;index<npc.inventory.ItemGrid.Length;index++)
            if(npc.inventory.ItemGrid[index]!=null&&!npc.inventory.ItemGrid[index].IsEmpty()&&!Qualified(npc.inventory.ItemGrid[index].itemValue))return false;
        if(IsReady(npc,person))return true;
        if(npc.Hand.bareHandData==null||npc.Hand.transientData==null||!ReferenceEquals(npc.Hand.bareHandData.holdingEntity,npc)||!ReferenceEquals(npc.Hand.transientData.holdingEntity,npc)||npc.Hand.transientData.HasItem||!Qualified(npc.Hand.BareHandItemValue)||npc.emodel?.avatarController?.GetRightHandTransform()==null)return false;
        if(npc.Hand.slotDatas!=null)foreach(var data in npc.Hand.slotDatas)if(data!=null&&(!ReferenceEquals(data.holdingEntity,npc)||data.item!=null&&!Qualified(data.itemValue)))return false;
        try
        {
            active=lease;
            using(lease)
            {
                Ready.Remove(npc);
                var models=new System.Collections.Generic.List<UnityEngine.Transform>();
                models.Add(npc.Hand.bareHandData.model);models.Add(npc.Hand.transientData.model);
                if(npc.Hand.slotDatas!=null)foreach(var data in npc.Hand.slotDatas)if(data!=null)models.Add(data.model);
                if(!RebirthNpcPreparedPhysicalHold.TryBeginDerivedHandRetirement(npc,person.Presence.EmbodimentGeneration,models,out var retirement))return false;
                try{npc.Hand.Cleanup();}
                finally{if(!RebirthNpcPreparedPhysicalHold.TryFinishDerivedHandRetirement(npc,retirement))throw new InvalidOperationException("Original derived hand cleanup did not preserve physical suspension.");}
                npc.Hand=new Hand(npc);
                npc.Hand.SetBareHandItem(bare);npc.Hand.BindToolbelt(npc.inventory);npc.Hand.mode=(Hand.HoldingMode)evidence.Mode;
                npc.Hand.Reconcile();
                // A native model may add physical participants. Rebind only this original
                // suspension before proving readiness; no component is allowed to run loose.
                if(!RebirthNpcPreparedPhysicalHold.TryHold(npc,person))return false;
                if(!StructuralReady(npc,person)||!Current(lease))return false;
                Ready.Add(npc,new ReadyStamp{World=npc.world,Hand=npc.Hand,NativeId=npc.entityId,Generation=person.Presence.EmbodimentGeneration,Revision=person.AggregateRevision,Checksum=person.AggregateChecksum});
                return true;
            }
        }
        catch(Exception){RebirthNpcPreparedPhysicalHold.TrySuspendAgain(npc);return false;}
    }
    internal static bool IsReady(EntityRebirthHumanoidNPC npc,RebirthNpcPersistentRecord person)
    {
        return npc!=null&&person?.Presence!=null&&Ready.TryGetValue(npc,out var stamp)&&ReferenceEquals(stamp.World,npc.world)&&ReferenceEquals(stamp.World,GameManager.Instance?.World)&&ReferenceEquals(stamp.Hand,npc.Hand)&&stamp.NativeId==npc.entityId&&stamp.Generation==person.Presence.EmbodimentGeneration&&stamp.Revision==person.AggregateRevision&&stamp.Checksum==person.AggregateChecksum&&StructuralReady(npc,person);
    }
    private static bool StructuralReady(EntityRebirthHumanoidNPC npc,RebirthNpcPersistentRecord person)
    {
        try
        {
            var evidence=person?.NativeReconstruction?.Hand;var hand=npc?.Hand;
            if(evidence==null||hand==null||hand.IsSwitching||hand.switchingCoroutine!=null||hand.holstering!=null||hand.mode!=(Hand.HoldingMode)evidence.Mode||
                !ReferenceEquals(hand.toolbelt,npc.inventory)||npc.inventory.SelectedSlot!=evidence.Selected||hand.equipped==null||
                !ReferenceEquals(hand.equipped,hand.Held)||!ReferenceEquals(hand.Held.holdingEntity,npc)||!hand.equipped.IsEquipped||hand.equipped.NeedsRebuild||!RebirthNpcIdleHandActionQualification.IsIdle(npc)||
                RebirthNativeItemCodec.Encode(hand.BareHandItemValue.Clone())!=evidence.BarePayload||
                RebirthNativeItemCodec.Encode(hand.equipped.PhysicalItemValue.Clone())!=RebirthNativeItemCodec.Encode(hand.Held.itemValue.Clone()))return false;
            var held=hand.Held;
            if(hand.mode==Hand.HoldingMode.Current&&npc.inventory.ItemGrid[evidence.Selected]!=null&&!npc.inventory.ItemGrid[evidence.Selected].IsEmpty())
            {if(!ReferenceEquals(held.itemStack,npc.inventory.ItemGrid[evidence.Selected]))return false;}
            else if(!ReferenceEquals(held,hand.bareHandData))return false;
            if(held.item.CanHold()&&(held.model==null||!ReferenceEquals(held.model.parent,npc.emodel?.avatarController?.GetRightHandTransform())))return false;
            return true;
        }
        catch(Exception){return false;}
    }
}
