using System;

// PREPARED, NOT RUN. All scopes/files/fatal observations must be created by
// real native flow. No fixture replaces a native image, receipt, or permission.
internal static class mpA_logistics_historical_zero_fixture
{
    internal static void MissingActualOwnPrimaryCannotCapture(EntityPlayerLocal player,RebirthLogisticsNativeBodyIntent.Command command,string acceptedNonce)
    {
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||RebirthGearNativePlayerFile.TryRead(owner,out _))throw new Exception("Actual unavailable own native primary required.");
        if(RebirthLogisticsHistoricalBeforeCapsule.TryPrepareLocal(player,command,acceptedNonce))throw new Exception("Missing native publication fabricated historical Before.");
    }
    internal static void ChangedNativeStageCannotRecapture(EntityPlayerLocal player,RebirthLogisticsNativeBodyIntent.Command command,string acceptedNonce)
    {
        if(!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var op,out int phase)||op!=command.Operation||phase==0)throw new Exception("Actual later original native stage required.");
        if(RebirthLogisticsHistoricalBeforeCapsule.TryPrepareLocal(player,command,acceptedNonce))throw new Exception("Later native life/stage replaced original historical0.");
    }
    internal static void ObserveOriginalAdoption(EntityPlayer actor,Guid operation,bool expectFrozenOriginal)
    {
        if(actor?.world?.IsRemote()!=false||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))throw new Exception("Actual server original actor required.");
        var bag=ItemStack.Clone(actor.bag.ItemGrid.items);var belt=ItemStack.Clone(actor.inventory.ItemGrid.items);
        RebirthLogisticsHistoricalBeforeCapsule.TryAdoptPending(actor,operation);
        bool exists=RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original);
        if(exists!=expectFrozenOriginal||!RebirthLogisticsDeathPartition.Same(bag,actor.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(belt,actor.inventory.ItemGrid.items))throw new Exception("Historical attestation changed live native inventory or violated actual fatal/never-admitted gate.");
        if(exists&&(original.Legs.Count!=0||original.Npcs.Count!=0||Array.Exists(original.BeforeOwner,x=>!x.IsEmpty())||Array.Exists(original.AfterOwner,x=>!x.IsEmpty())||Array.Exists(original.DebitedOwner,x=>!x.IsEmpty())))throw new Exception("Historical attestation authorized a source/owner effect.");
    }
    internal static void AdmittingOriginalCannotUseHistoricalFreeze(EntityPlayer actor,Guid operation)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))throw new Exception("Actual server actor required.");
        string Before()=>RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var value)?RebirthLogisticsTransferJournal.Encode(value):null;
        string prior=Before();if(RebirthLogisticsHeaderAdmission.TryHistoricalNonce(actor,operation,out _))throw new Exception("Harness must pause actual Admitting header.");
        RebirthLogisticsHistoricalBeforeCapsule.TryAdoptPending(actor,operation);if(Before()!=prior)throw new Exception("Admitted history was replaced by historical no-effect.");
    }
    internal static void HistoricalEvidenceNeverSubstitutesNativeSavedZero(EntityPlayer actor,Guid operation)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||RebirthLogisticsOwnerReceipt.TrySaved(owner,operation,0,out _))throw new Exception("Requires actual absent host native0 publication.");
        RebirthLogisticsHistoricalBeforeCapsule.TryAdoptPending(actor,operation);
        if(RebirthLogisticsOwnerReceipt.TrySaved(owner,operation,0,out _))throw new Exception("Historical attestation became current native saved0.");
    }
    // Additional actual event sequences: capsule before fatal/no original;
    // fatal before fragments; lost normal native0 but real own primary0;
    // duplicate fragments/ACK loss/reconnect same capsule; interrupted body or
    // metadata moves; malformed conflicting candidate retained; terminal4 before
    // ACK never re-Begin/delta; native final5 and body full publication still
    // required before any zero-effect settlement/hold release.
}
