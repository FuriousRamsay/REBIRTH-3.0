using System;
using System.Collections.Generic;
using System.Linq;

// Original owner half of the native mobile station handoff. No caller yet:
// the native authenticated payload matcher must precede these exact permits.
internal static partial class RebirthRemoteResourceOwnerAction
{
    internal static bool CanReleaseEndedFetchedNoEffect(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(player==null||original==null||player.Died<=original.Deaths||!RebirthRemoteResourceFetchedStationOriginal.EndedClientEffectsAbsent(original)||
            fetchedStationHandoffs.ContainsKey(original.Operation)||player.Buffs.GetCustomVar("rbCamperNativePayment_"+original.Operation.ToString("N"))!=0f)return false;
        float receipt=player.Buffs.GetCustomVar(Receipt(original.Operation));if(receipt!=0f&&receipt!=10f)return false;
        return !holds.TryGetValue(original.Operation,out var hold)||hold.Original.Digest==original.Digest&&hold.Original.World==original.World&&
            hold.Original.Creation==original.Creation&&hold.Original.Deaths==original.Deaths&&!hold.Refund&&!hold.BlockRefund&&hold.SourceOperation==Guid.Empty&&
            (hold.SourceBinding==null||hold.SourceBinding=="")&&hold.Before.Matches(hold.After);
    }
    internal static bool ReleaseEndedFetchedNoEffect(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            var terminal=new RebirthRemoteResourceClientOriginalJournal(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationEndedZeroTerminals")).Read(original.Operation);
            if(terminal?.State!=RebirthCamperTransferJournal.Phase.Applied||!terminal.Before.SequenceEqual(terminal.After)||
                !RebirthRemoteResourceFetchedStationOriginal.ValidateEndedClientTerminal(player,original,new System.Text.UTF8Encoding(false,true).GetString(terminal.After))||
                !CanReleaseEndedFetchedNoEffect(player,original))return false;
            holds.Remove(original.Operation);
            if(player.Buffs.GetCustomVar(Receipt(original.Operation))==10f){player.Buffs.SetCustomVar(Receipt(original.Operation),0f,true);player.Buffs.RemoveCustomVar(Receipt(original.Operation));}
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    private sealed class FetchedOwnerHandoff{internal Hold Hold;internal string Payload;internal Guid Source;internal string Binding;}
    private static readonly Dictionary<Guid,FetchedOwnerHandoff> fetchedStationHandoffs=new Dictionary<Guid,FetchedOwnerHandoff>();
    private sealed class FetchedSetter{internal FetchedOwnerHandoff Original;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Image;internal bool Used;}
    [ThreadStatic]private static FetchedSetter fetchedSetter;
    internal static bool HasFetchedStationHandoff(Guid job)=>fetchedStationHandoffs.TryGetValue(job,out var original)&&holds.TryGetValue(job,out var hold)&&ReferenceEquals(original.Hold,hold);
    internal static bool ReleaseFetchedStationTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof,byte[] payload,RebirthCamperStationAddress address)
    {
        if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!RebirthRemoteResourceFetchedStationOriginal.HasClientUsedTerminal(player,original.Operation,address,original.Creation,payload,out var actual,out var actualBinding,out var actualProof)||
            actual!=source||actualBinding!=binding||actualProof!=proof)return false;
        if(holds.TryGetValue(original.Operation,out var hold))
        {
            bool ended=!ReferenceEquals(hold.Player,player)&&!ReferenceEquals(hold.Player?.world?.GetEntity(hold.Player.entityId),hold.Player);
            if(!ReferenceEquals(hold.Player,player)&&!ended||hold.Original.Digest!=original.Digest||hold.SourceOperation!=source||hold.SourceBinding!=binding||hold.Refund||hold.BlockingOnly)return false;
            holds.Remove(original.Operation);fetchedStationHandoffs.Remove(original.Operation);
        }
        player.Buffs.SetCustomVar(Receipt(original.Operation),0f,true);player.Buffs.RemoveCustomVar(Receipt(original.Operation));
        GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
    }
    private static bool ForeignFetchedHold(EntityPlayerLocal player,Guid job)=>RebirthDogLifecycleService.HasHireInventoryHold(player)||player==null||RebirthLogisticsOwnerReceipt.IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||
        RebirthCookingOutputDelivery.HasOwnHold(player)||RebirthCookingOwnerDelta.HasOwnHold(player)||RebirthCookingGridCustody.IsHeld(player)||
        AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||
        RebirthBackpackLibraryReservation.IsHeld(player)||RebirthCamperEnergyOwnerHold.Blocks(player.bag)||
        holds.Any(pair=>pair.Key!=job&&ReferenceEquals(pair.Value.Player,player)&&Current(pair.Value));
    internal static bool BindFetchedStationHandoff(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,
        Guid source,string binding,string payload,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,Func<bool> authenticated)
    {
        if(authenticated?.Invoke()!=true||original==null||ForeignFetchedHold(player,original.Operation)||payload?.Length!=64||payload.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||!RebirthRemoteResourceFetchedStationOriginal.IsCandidate(original)||
            !holds.TryGetValue(original.Operation,out var hold)||!ReferenceEquals(hold.Player,player)||!Current(hold)||hold.BlockingOnly||hold.Refund||
            hold.Original.Digest!=original.Digest||player.Buffs.GetCustomVar(Receipt(original.Operation))!=10f||!before.Matches(hold.Before)||!DebitOnly(before.Bag,after.Bag)||!DebitOnly(before.Belt,after.Belt)||
            !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var paidSource,out string paidBinding,out var paid)||paidSource!=source||paidBinding!=binding)return false;
        var requirements=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
        if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,requirements,paid,player.inventory.Length)||
            !RebirthRemoteResourceCraftPlan.TryDebit(before,requirements,paid,player.inventory.Length,out var derived,out _)||!derived.Matches(after))return false;
        if(fetchedStationHandoffs.TryGetValue(original.Operation,out var retained))return ReferenceEquals(retained.Hold,hold)&&retained.Payload==payload&&retained.Source==source&&retained.Binding==binding&&after.Matches(hold.After);
        foreach(var stale in fetchedStationHandoffs.Where(pair=>!holds.TryGetValue(pair.Key,out var current)||!ReferenceEquals(current,pair.Value.Hold)).Select(pair=>pair.Key).ToArray())fetchedStationHandoffs.Remove(stale);
        if(fetchedStationHandoffs.Count>=32||!hold.Before.Matches(hold.After)||!before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
        hold.After=after.Copy();hold.SourceOperation=source;hold.SourceBinding=binding;
        fetchedStationHandoffs.Add(original.Operation,new FetchedOwnerHandoff{Hold=hold,Payload=payload,Source=source,Binding=binding});return true;
    }
    internal static bool BindRecoveredFetchedStationHandoff(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,
        Guid source,string binding,string payload,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,Func<bool> authenticated,Func<bool> positiveNativeProof)
    {
        if(authenticated?.Invoke()!=true||positiveNativeProof?.Invoke()!=true||!RebirthRemoteResourceClientOriginal.Current(player,original)||
            !RebirthRemoteResourceFetchedStationOriginal.IsCandidate(original)||ForeignFetchedHold(player,original.Operation)||permit!=null||fetchedSetter!=null||
            payload?.Length!=64||!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var paidSource,out var paidBinding,out var paid)||
            paidSource!=source||paidBinding!=binding||!RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(before))return false;
        var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
        if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,needs,paid,player.inventory.Length)||
            !RebirthRemoteResourceCraftPlan.TryDebit(before,needs,paid,player.inventory.Length,out var derived,out _)||!derived.Matches(after))return false;
        if(holds.TryGetValue(original.Operation,out var existing)&&(existing.Original.Digest!=original.Digest||existing.Refund||
            !existing.Before.Matches(before)||!existing.After.Matches(before)&&!existing.After.Matches(after)))return false;
        if(!holds.ContainsKey(original.Operation)&&holds.Count>=32||!fetchedStationHandoffs.ContainsKey(original.Operation)&&fetchedStationHandoffs.Count>=32)return false;
        // Native proof was checked before replacing transient actor/peer
        // bindings. Original immutable images and source payment never change.
        var hold=new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before.Copy(),After=after.Copy(),
            Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,SourceOperation=source,SourceBinding=binding,
            Authorized=()=>RebirthRemoteResourceClientOriginal.Current(player,original)&&authenticated?.Invoke()==true};
        holds[original.Operation]=hold;fetchedStationHandoffs[original.Operation]=new FetchedOwnerHandoff{Hold=hold,Payload=payload,Source=source,Binding=binding};return true;
    }
    internal static bool MatchesFetchedStationHandoff(EntityPlayerLocal player,Guid job,string payload,ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt)
    {
        if(ForeignFetchedHold(player,job)||!fetchedStationHandoffs.TryGetValue(job,out var original)||original.Payload!=payload||!ReferenceEquals(original.Hold.Player,player)||!Current(original.Hold)||original.Hold.BlockingOnly)return false;
        var hold=original.Hold;
        bool Area(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Select((value,index)=>Same(value,b[index])).All(value=>value);
        if(!Area(beforeBag,hold.Before.Bag)||!Area(beforeBelt,hold.Before.Belt)||!Area(afterBag,hold.After.Bag)||!Area(afterBelt,hold.After.Belt))return false;
        var actual=RebirthCamperEnergyActorImage.Capture(player);
        // Compare the full current actor with original noninventory state and
        // its actual current bag/belt. This ActorImage covers cursor and bag
        // locks; it is not a separate equipment-image witness.
        var unchanged=hold.Before.Copy();Array.Copy(actual.Bag,unchanged.Bag,actual.Bag.Length);Array.Copy(actual.Belt,unchanged.Belt,actual.Belt.Length);
        return unchanged.Matches(actual);
    }
    internal static bool WriteFetchedNativeSlot(EntityPlayerLocal player,Guid job,string payload,ItemStackGrid grid,int slot,ItemStack desired,Action exactNativeSetter)
    {
        if(ForeignFetchedHold(player,job)||fetchedSetter!=null||permit!=null||exactNativeSetter==null||!fetchedStationHandoffs.TryGetValue(job,out var original)||original.Payload!=payload||
            !ReferenceEquals(original.Hold.Player,player)||!Current(original.Hold)||original.Hold.BlockingOnly)return false;
        var hold=original.Hold;bool bag=ReferenceEquals(grid,hold.Bag);if(!bag&&!ReferenceEquals(grid,hold.Belt))return false;
        var before=bag?hold.Before.Bag:hold.Before.Belt;var after=bag?hold.After.Bag:hold.After.Belt;
        if(slot<0||slot>=after.Length||desired==null||Same(before[slot],after[slot])||!Same(desired,after[slot])||!Same(grid.items[slot],before[slot])&&!Same(grid.items[slot],after[slot]))return false;
        var once=new FetchedSetter{Original=original,Grid=grid,Slot=slot,Image=desired.Clone()};fetchedSetter=once;
        try{exactNativeSetter();}finally{fetchedSetter=null;}
        return once.Used&&Current(hold)&&Same(grid.items[slot],after[slot]);
    }
    private static bool ConsumeFetchedNativeSlot(ItemStackGrid grid,int slot,ItemStack value)
    {
        var once=fetchedSetter;
        if(once==null||once.Used||once.Slot!=slot||!ReferenceEquals(once.Grid,grid)||value==null||!Same(value,once.Image)||!Current(once.Original.Hold))return false;
        once.Used=true;return true;
    }
}
