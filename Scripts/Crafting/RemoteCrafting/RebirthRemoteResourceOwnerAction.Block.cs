using System;
using System.Collections.Generic;
using System.Linq;

// Purpose-specific Block callback scope. Prepared/mixed records do not grant it.
internal static partial class RebirthRemoteResourceOwnerAction
{
    internal static bool HasRetainedBlockHold(Guid operation)=>holds.ContainsKey(operation);
    internal static bool ReleaseBlockUsedTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        try
        {
            if(!RebirthRemoteResourceBlockOriginal.TerminalScope(player,original)||!RebirthRemoteResourceBlockOriginal.HasUsedTerminal(original,source,binding,proof))return false;
            if(holds.TryGetValue(original.Operation,out var hold))
            {
                if(hold.Original.Digest!=original.Digest||hold.SourceOperation!=source||hold.SourceBinding!=binding||hold.Refund)return false;
            }
            if(!RebirthRemoteResourceClientOriginal.CompleteBlockTerminal(player,original,source,binding,proof))return false;
            holds.Remove(original.Operation);
            // Metadata only. No bag/belt/cursor/world write or native callback.
            // Receipt keys are per immutable operation; never clear native1,
            // native5, native10 or an unrecognized newer receipt value.
            string receipt=Receipt(original.Operation);
            if(player.Buffs.GetCustomVar(receipt)==2f)
            {player.Buffs.SetCustomVar(receipt,0f,true);player.Buffs.RemoveCustomVar(receipt);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
            return true;
        }
        catch{return false;}
    }
    private sealed class BlockScope:IDisposable
    {
        internal Hold Hold;internal Func<bool> Permission;internal readonly HashSet<string> Written=new HashSet<string>();
        public void Dispose(){if(ReferenceEquals(blockScope,this))blockScope=null;}
    }
    [ThreadStatic]private static BlockScope blockScope;
    internal static bool TryHoldMixedBlock(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> originalScope)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||originalScope?.Invoke()!=true||
                !RebirthRemoteResourceBlockOriginal.TryRead(original,out var action,out var state)||state!=RebirthCamperTransferJournal.Phase.Uncertain||
                !Guid.TryParseExact((string)action.Attribute("source"),"N",out var source)||holds.ContainsKey(original.Operation)||!CanPrepareNativeOriginal(player))return false;
            float phase=player.Buffs.GetCustomVar(Receipt(original.Operation));if(phase!=1f&&phase!=2f&&phase!=5f)return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value);
            var observed=RebirthCamperEnergyActorImage.Capture(player);
            if(!Same(before.Cursor,after.Cursor)||!Same(observed.Cursor,before.Cursor)||before.BagLocks==null||after.BagLocks==null||observed.BagLocks==null||
                !before.BagLocks.SequenceEqual(after.BagLocks)||!before.BagLocks.SequenceEqual(observed.BagLocks))return false;
            foreach(var area in new[]{new[]{before.Bag,after.Bag,observed.Bag},new[]{before.Belt,after.Belt,observed.Belt}})
            {
                if(area[0].Length!=area[1].Length||area[0].Length!=area[2].Length)return false;
                for(int slot=0;slot<area[0].Length;slot++)if(!Same(area[2][slot],area[0][slot])&&!Same(area[2][slot],area[1][slot]))return false;
            }
            // Canonical Block plan separately proves exact materials/tool wear.
            // This retains custody only; no callback or setter is permitted.
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before,After=after,
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Authorized=originalScope,SourceOperation=source,SourceBinding=(string)action.Attribute("binding"),BlockingOnly=true});return true;
        }
        catch{return false;}
    }
    internal static bool ReleaseBlockOwed(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding)
    {
        if(!RebirthRemoteResourceBlockAdoption.QueryScope(player,original)||!RebirthRemoteResourceBlockRefund.HasClientOwed(original,source,binding,out _,out _))return false;
        if(!holds.TryGetValue(original.Operation,out var hold))return true;
        if(!ReferenceEquals(hold.Player,player)||hold.Original.Digest!=original.Digest||hold.SourceOperation!=Guid.Empty&&(hold.SourceOperation!=source||hold.SourceBinding!=binding)||hold.Refund)return false;
        holds.Remove(original.Operation);return true; // Positive owed claim remains, allowing the owner to make room.
    }
    internal static bool ReleaseBlockRefundTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        if(!RebirthRemoteResourceBlockAdoption.QueryScope(player,original)||!RebirthRemoteResourceBlockRefund.HasClientTerminal(original,source,binding,proof))return false;
        if(holds.TryGetValue(original.Operation,out var hold)&&(!ReferenceEquals(hold.Player,player)||!hold.BlockRefund||hold.Original.Digest!=original.Digest||hold.SourceOperation!=source||hold.SourceBinding!=binding))return false;
        if(!RebirthRemoteResourceClientOriginal.CompleteBlockRefundMetadata(original,source,binding,proof))return false;
        holds.Remove(original.Operation);
        foreach(var id in new[]{original.Operation,RebirthRemoteResourceBlockRefund.Child(original)})
        {
            float phase=player.Buffs.GetCustomVar(Receipt(id));
            if(phase==2f||id==original.Operation&&phase==5f){player.Buffs.SetCustomVar(Receipt(id),0f,true);player.Buffs.RemoveCustomVar(Receipt(id));}
        }
        GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
    }
    internal static bool TryHoldBlockRefund(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,
        RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,Func<bool> authenticated)
    {
        try
        {
            if(!RebirthRemoteResourceBlockRefund.Current(player,original)||authenticated?.Invoke()!=true||
                !RebirthRemoteResourceBlockRefund.IsClientPrepared(original,source,binding,before,after))return false;
            if(holds.TryGetValue(original.Operation,out var retained))
            {
                if(!retained.Refund||!retained.BlockRefund||!ReferenceEquals(retained.Player,player)||retained.Original.Digest!=original.Digest||retained.SourceOperation!=source||retained.SourceBinding!=binding||
                    !retained.Before.Matches(before)||!retained.After.Matches(after)||!Current(retained))return false;
                retained.Authorized=authenticated;return Current(retained);
            }
            if(!CanPrepareNativeOriginal(player)||!before.Matches(RebirthCamperEnergyActorImage.Capture(player))&&!after.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before.Copy(),After=after.Copy(),
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,SourceOperation=source,SourceBinding=binding,Authorized=authenticated,Refund=true,BlockRefund=true,RefundDeaths=RebirthRemoteResourceBlockRefund.DeliveryDeaths(original)});return true;
        }
        catch{return false;}
    }
    internal static bool CanRestorePreparedBlock(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> actualSavedHostPermission)
    {
        try
        {
            return blockScope==null&&craftScope==null&&repairScope==null&&actualSavedHostPermission?.Invoke()==true&&
                RebirthRemoteResourceClientOriginal.Current(player,original)&&!RebirthRemoteResourceBlockOriginal.RejectedOrUnknown(original)&&!RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original)&&
                player.Buffs.GetCustomVar(Receipt(original.Operation))==1f&&RebirthRemoteResourceBlockOriginal.NeverEntered(original)&&
                holds.TryGetValue(original.Operation,out var hold)&&ReferenceEquals(hold.Player,player)&&Current(hold)&&!hold.BlockingOnly&&!hold.Refund&&
                hold.Before.Matches(RebirthCamperEnergyActorImage.Capture(player))&&
                !RebirthLogisticsOwnerReceipt.IsHeld(player)&&!RebirthPlacedOriginal.IsHeld(player)&&!RebirthCookingOutputDelivery.HasOwnHold(player)&&
                !RebirthCookingOwnerDelta.HasOwnHold(player)&&!RebirthCookingGridCustody.IsHeld(player)&&!AdvancedFarmingHarvestOwnerDelta.IsHeld(player)&&
                !AdvancedFarmingWaterExchangeOriginal.IsHeld(player)&&!RebirthGearOwnerReservation.IsHeld(player)&&!RebirthBackpackLibraryReservation.IsHeld(player)&&
                !RebirthCamperEnergyOwnerHold.Blocks(player.bag)&&!RebirthCamperStationNativeActorPayment.Blocks(player.bag);
        }
        catch{return false;}
    }
    internal static bool HasExecutingBlock(RebirthRemoteResourceClientOriginal.Intent original)
    {
        var scope=blockScope;return original!=null&&scope?.Hold?.Original.Digest==original.Digest&&scope.Permission?.Invoke()==true&&Current(scope.Hold)&&
            !scope.Hold.BlockingOnly&&!scope.Hold.Refund;
    }
    internal static bool SaveBlockPaidOwner(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockNativeCapture.Scope actualScope)
    {
        if(!HasExecutingBlock(original)||!holds.TryGetValue(original.Operation,out var hold)||!ReferenceEquals(hold.Player,player)||
            !hold.After.Matches(RebirthCamperEnergyActorImage.Capture(player))||!RebirthRemoteResourceBlockOriginal.SaveCapturedCommand(player,original,actualScope))return false;
        player.Buffs.SetCustomVar(Receipt(original.Operation),2f,true);
        GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        return true; // Host must independently observe SAME final native owner2/fullAfter.
    }
    internal static bool SaveRejectedBlockOwner(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> authenticatedMonotoneRejecting)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||authenticatedMonotoneRejecting?.Invoke()!=true||
                !RebirthRemoteResourceBlockOriginal.CanRejectUnentered(original)||
                !holds.TryGetValue(original.Operation,out var hold)||!ReferenceEquals(hold.Player,player)||hold.Refund||!Current(hold)||
                !hold.Before.Matches(RebirthCamperEnergyActorImage.Capture(player))||!RebirthRemoteResourceBlockOriginal.SaveRejected(player,original,authenticatedMonotoneRejecting))return false;
            player.Buffs.SetCustomVar(Receipt(original.Operation),5f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool ReleaseOriginalBlockInput(EntityPlayerLocal player,ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,
        RebirthRemoteResourceClientOriginal.Intent original,int releasedActionIndex)
    {
        try
        {
            // Exact installed base released branch only clears native started
            // state and repairType before every cost/event/block path. It is
            // not an allowance through the general held-use action guard.
            if(action?.GetType()!=typeof(ItemActionRepair)||blockScope!=null||!RebirthRemoteResourceClientOriginal.Current(player,original)||
                !RebirthRemoteResourceBlockPlan.Validate(original)||
                !ReferenceEquals(data?.invData?.holdingEntity,player)||!ReferenceEquals(player.inventory.holdingItemData,data.invData)||
                releasedActionIndex!=data.indexInEntityOfAction||releasedActionIndex<0||data.invData.item?.Actions==null||releasedActionIndex>=data.invData.item.Actions.Length||
                !ReferenceEquals(data.invData.item.Actions[releasedActionIndex],action)||data.invData.actionData==null||releasedActionIndex>=data.invData.actionData.Count||
                !ReferenceEquals(data.invData.actionData[releasedActionIndex],data))return false;
            bool retained=holds.TryGetValue(original.Operation,out var hold)&&Current(hold)&&!hold.Refund;
            bool owed=RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var source,out var binding,out _)&&
                RebirthRemoteResourceBlockRefund.HasClientOwed(original,source,binding,out _,out _);
            if(!retained&&!owed)return false;
            var body=original.Copy().Element("action");string tool=RebirthNativeItemCodec.Encode(data.invData.itemValue);
            if(releasedActionIndex!=(int)body.Attribute("actionIndex")||data.invData.slotIdx!=(int)body.Attribute("toolSlot")||player.inventory.holdingItemIdx!=data.invData.slotIdx||
                tool!=body.Element("toolInput").Value&&tool!=body.Element("toolAfter").Value)return false;
            action.ExecuteAction(data,true);
            return !data.bUseStarted&&data.repairType==ItemActionRepair.EnumRepairType.None;
        }
        catch{return false;}
    }
    internal static bool TryHoldBlockOriginal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> authenticatedOriginal)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||authenticatedOriginal?.Invoke()!=true||
                !RebirthRemoteResourceBlockOriginal.TryRead(original,out var action,out var originalPhase)||
                !Guid.TryParseExact((string)action.Attribute("source"),"N",out var source)||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var paidSource,out var binding,out var paid)||paidSource!=source||
                !RebirthRemoteResourceBlockPlan.Expected(original,paid,out var before,out var after,out _))return false;
            if(holds.TryGetValue(original.Operation,out var retained))
            {
                if(!ReferenceEquals(retained.Player,player)||retained.Original.Digest!=original.Digest||!Current(retained)||retained.Refund||retained.BlockingOnly||!retained.Before.Matches(before))return false;
                if(retained.SourceOperation==Guid.Empty)
                {
                    if(!before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
                    retained.SourceOperation=source;retained.SourceBinding=binding;retained.After=after.Copy();retained.Authorized=authenticatedOriginal;
                }
                return retained.SourceOperation==source&&retained.SourceBinding==binding&&retained.After.Matches(after);
            }
            var observed=RebirthCamperEnergyActorImage.Capture(player);
            bool alreadyPaid=originalPhase==RebirthCamperTransferJournal.Phase.Uncertain&&player.Buffs.GetCustomVar(Receipt(original.Operation))==2f&&
                after.Matches(observed)&&RebirthRemoteResourceBlockOriginal.TryReadCapturedCommand(original,out _,out _);
            if(!CanPrepareNativeOriginal(player)||!before.Matches(observed)&&!alreadyPaid)return false;
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before,After=after,
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Authorized=authenticatedOriginal,SourceOperation=source,SourceBinding=binding});return true;
        }
        catch{return false;}
    }
    internal static IDisposable BeginBlockOriginalScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> actualSavedHostPermission)
    {
        if(blockScope!=null||craftScope!=null||repairScope!=null||actualSavedHostPermission?.Invoke()!=true||
            !RebirthRemoteResourceClientOriginal.Current(player,original)||RebirthRemoteResourceBlockOriginal.RejectedOrUnknown(original)||RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(original)||player.Buffs.GetCustomVar(Receipt(original.Operation))!=1f||
            !RebirthRemoteResourceBlockOriginal.TryRead(original,out _,out var state)||state!=RebirthCamperTransferJournal.Phase.Uncertain||
            !holds.TryGetValue(original.Operation,out var hold)||!ReferenceEquals(hold.Player,player)||!Current(hold)||hold.BlockingOnly||hold.Refund||
            !hold.Before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return null;
        var scope=new BlockScope{Hold=hold,Permission=actualSavedHostPermission};blockScope=scope;return scope;
    }
    private static bool ConsumeBlockOriginal(ItemStackGrid grid,int slot,ItemStack value)
    {
        var scope=blockScope;var hold=scope?.Hold;
        if(hold==null||hold.BlockingOnly||scope.Permission?.Invoke()!=true||!Current(hold))return false;
        bool bag=ReferenceEquals(grid,hold.Bag);if(!bag&&!ReferenceEquals(grid,hold.Belt))return false;
        var before=bag?hold.Before.Bag:hold.Before.Belt;var after=bag?hold.After.Bag:hold.After.Belt;
        if(slot<0||slot>=after.Length||value==null||Same(before[slot],after[slot]))return false;
        // Do not permit a material consumer to write the separately native-worn
        // held tool. Its one native UseTimes change is verified in fullAfter.
        int tool=(int)hold.Original.Copy().Element("action").Attribute("toolSlot");
        if(!bag&&slot==tool)return false;
        var current=grid.items[slot];var initial=before[slot];var final=after[slot];
        if(current==null||initial==null||initial.IsEmpty()||current.IsEmpty()||value.count>=current.count||value.count<Math.Max(0,final.count)||current.count>initial.count)return false;
        var expectedCurrent=initial.Clone();expectedCurrent.count=current.count;
        if(!Same(current,expectedCurrent))return false;
        if(value.IsEmpty())
        {if(!final.IsEmpty()||!Same(value,final))return false;}
        else
        {var expectedValue=initial.Clone();expectedValue.count=value.count;if(!Same(value,expectedValue))return false;}
        // Native authored lists may charge the same material more than once.
        // Permit each actual monotone fragment once, within the frozen total;
        // never permit a credit, metadata replacement or excess source debit.
        return scope.Written.Add((bag?"bag:":"belt:")+slot+":"+current.count+":"+value.count);
    }
}
