using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Exact personal native queue observation. It never treats a callback return,
// consumed flag, current postimage, or intent marker as proof of paid usage.
internal static partial class RebirthRemoteResourceOwnerAction
{
    private sealed class Hold
    {
        internal EntityPlayerLocal Player;internal World World;internal string Directory;internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal RebirthCamperEnergyActorImage Before,After;internal ItemStackGrid Bag,Belt;internal Func<bool> Authorized;
        internal Guid SourceOperation;internal string SourceBinding;internal bool Refund,BlockRefund,BlockingOnly;internal int RefundDeaths;
    }
    private sealed class Permit{internal Hold Hold;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used;}
    private static readonly System.Collections.Generic.Dictionary<Guid,Hold> holds=new System.Collections.Generic.Dictionary<Guid,Hold>();
    [ThreadStatic]private static Permit permit;
    internal static bool IsHeld(EntityPlayerLocal player)=>PlacedCancellationIsHeld(player)||player!=null&&player.IsSpawned()&&!player.IsDead()&&holds.Values.Any(hold=>
        ReferenceEquals(hold.Player,player)&&ReferenceEquals(hold.World,GameManager.Instance?.World)&&(hold.Refund?hold.RefundDeaths:hold.Original.Deaths)==player.Died&&
        hold.Directory==GameIO.GetSaveGameDir()&&hold.Original.World==player.world.worldState.Guid&&hold.Original.Creation==RebirthCamperCharacterAccess.CurrentCreation(player));
    internal static bool HoldsOriginalScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)=>
        original!=null&&IsHeld(player)&&holds.TryGetValue(original.Operation,out var hold)&&ReferenceEquals(hold.Player,player)&&
        ReferenceEquals(hold.World,player.world)&&hold.Directory==GameIO.GetSaveGameDir()&&hold.Original.Deaths==player.Died&&
        hold.Original.Digest==original.Digest&&!hold.Refund&&RebirthRemoteResourceClientOriginal.Current(player,original);
    internal static bool ReleaseRepairTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid sourceOperation,string binding,string proof)
    {
        if(!RebirthRemoteResourceRepairRefund.Current(player,original)||!RebirthRemoteResourceClientOriginal.HasRepairTerminal(original,sourceOperation,binding,proof)&&
            !RebirthRemoteResourceRepairRefund.HasTerminal(original,sourceOperation,binding,proof))return false;
        if(holds.TryGetValue(original.Operation,out var hold))
        {
            if(!ReferenceEquals(hold.Player,player)||hold.Original.Digest!=original.Digest||hold.SourceOperation!=sourceOperation||hold.SourceBinding!=binding)return false;
            holds.Remove(original.Operation);
        }
        // Host Root4 and the flushed local terminal mirror are authority now.
        // Do not accumulate one persisted CVar per completed lifetime repair.
        player.Buffs.SetCustomVar(Receipt(original.Operation),0f,true);player.Buffs.RemoveCustomVar(Receipt(original.Operation));
        GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
    }
    internal static bool ReleaseNoPaymentTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid sourceOperation,string binding,string proof)
    {
        if(!RebirthRemoteResourceRepairAdoption.QueryScope(player,original)||!RebirthRemoteResourceClientOriginal.HasNoPaymentTerminal(original,sourceOperation,binding,proof))return false;
        if(original.Deaths<player.Died)
        {
            if(holds.TryGetValue(original.Operation,out var historical))
            {if(historical.Original.Digest!=original.Digest||historical.SourceOperation!=Guid.Empty||historical.Refund)return false;holds.Remove(original.Operation);}
            return true; // Exact terminal metadata; never rewrite a new-life receipt.
        }
        if(holds.TryGetValue(original.Operation,out var hold))
        {
            if(!ReferenceEquals(hold.Player,player)||hold.Original.Digest!=original.Digest||hold.SourceOperation!=Guid.Empty)return false;
            holds.Remove(original.Operation);
        }
        player.Buffs.SetCustomVar(Receipt(original.Operation),0f,true);player.Buffs.RemoveCustomVar(Receipt(original.Operation));
        GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
    }
    internal static bool ReleaseHistoricalCraftTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        if(player==null||original?.Kind!=RemoteResourceClientOperation.Craft||original.Deaths>=player.Died||!RebirthRemoteResourceCraftAdoption.QueryScope(player,original)||
            !RebirthRemoteResourceClientOriginal.TryReadCraftTerminal(source,binding,out var terminal)||
            (string)terminal.Attribute("originalDigest")!=original.Digest||(string)terminal.Attribute("hostProof")!=proof)return false;
        if(holds.TryGetValue(original.Operation,out var hold))
        {
            if(hold.Original.Digest!=original.Digest||hold.Refund||hold.Original.Deaths>=player.Died||hold.SourceOperation!=source||hold.SourceBinding!=binding)return false;
            holds.Remove(original.Operation);
        }
        return true;
    }
    internal static bool ReleaseNeverAdmittedTerminal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!RebirthRemoteResourceClientOriginal.Current(player,original)||!RebirthRemoteResourcePreAdmissionRefusal.HasClientTerminal(original))return false;
        if(holds.TryGetValue(original.Operation,out var hold))
        {
            if(!ReferenceEquals(hold.Player,player)||hold.Original.Digest!=original.Digest||hold.SourceOperation!=Guid.Empty||hold.Refund)return false;
            holds.Remove(original.Operation);
        }
        player.Buffs.SetCustomVar(Receipt(original.Operation),0f,true);player.Buffs.RemoveCustomVar(Receipt(original.Operation));
        GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
    }
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {
        if(ConsumePlacedCancellation(grid,slot,value))return true;
        var current=permit;
        if(current==null)
        {
            if(fetchedSetter!=null)return ConsumeFetchedNativeSlot(grid,slot,value);
            if(craftScope!=null)return ConsumeCraftOriginal(grid,slot,value);
            if(blockScope!=null)return ConsumeBlockOriginal(grid,slot,value);
            var scope=repairScope;
            if(scope?.Entered!=true||!holds.TryGetValue(scope.Original.Operation,out var original)||!Current(original)||scope.Authorized?.Invoke()!=true)return false;
            bool bag=ReferenceEquals(grid,original.Bag);if(!bag&&!ReferenceEquals(grid,original.Belt))return false;
            var before=bag?original.Before.Bag:original.Before.Belt;var after=bag?original.After.Bag:original.After.Belt;
            return slot>=0&&slot<after.Length&&value!=null&&Same(value,after[slot])&&
                (Same(grid.items[slot],before[slot])||Same(grid.items[slot],after[slot]));
        }
        if(current.Used||!ReferenceEquals(current.Grid,grid)||current.Slot!=slot||!ReferenceEquals(current.Value,value)||!Current(current.Hold))return false;
        current.Used=true;return true;
    }
    private static bool Current(Hold hold)=>hold!=null&&(hold.Refund?hold.RefundDeaths==hold.Player.Died&&(hold.BlockRefund?RebirthRemoteResourceBlockRefund.Current(hold.Player,hold.Original):RebirthRemoteResourceRepairRefund.Current(hold.Player,hold.Original)):RebirthRemoteResourceClientOriginal.Current(hold.Player,hold.Original))&&
        ReferenceEquals(hold.World,hold.Player.world)&&GameIO.GetSaveGameDir()==hold.Directory&&ReferenceEquals(hold.Bag,hold.Player.bag?.ItemGrid)&&
        ReferenceEquals(hold.Belt,hold.Player.inventory?.ItemGrid)&&hold.Authorized?.Invoke()==true;
    private static bool Same(ItemStack left,ItemStack right)=>RebirthStationGridIngredients.IsSameStackSnapshot(left,right);
    private static bool ValidSource(int kind,int slot)=>slot>=0&&(kind==1&&slot<169||kind==2&&slot<20||kind==3&&slot<13);
    internal static bool TrySource(ItemActionEntryRepair entry,out int kind,out int slot,out string nativeInput)
    {
        kind=0;slot=-1;nativeInput=null;
        try
        {
            var controller=entry?.ItemController;var player=controller?.xui?.playerUI?.entityPlayer;ItemStack stack;
            if(controller is XUiC_EquipmentStack equipment){kind=3;slot=equipment.SlotNumber;stack=equipment.ItemStack;}
            else if(controller is XUiC_ItemStack grid)
            {kind=grid.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack?1:grid.StackLocation==XUiC_ItemStack.StackLocationTypes.ToolBelt?2:0;slot=grid.SlotNumber;stack=grid.ItemStack;}
            else return false;
            if(player==null||!ValidSource(kind,slot)||stack==null||stack.count!=1||stack.IsEmpty())return false;
            var slots=kind==1?player.bag?.ItemGrid.items:kind==2?player.inventory?.ItemGrid.items:player.equipment?.itemGrid?.items;
            if(slots==null||slot>=slots.Length||!Same(slots[slot],stack))return false;
            nativeInput=RebirthNativeItemCodec.Encode(stack.itemValue);return true;
        }
        catch{kind=0;slot=-1;nativeInput=null;return false;}
    }
    private static bool SavedSource(PaymentIntent payment,RebirthCamperEnergyActorImage actor,PlayerDataFile saved,bool removed)
    {
        if(payment==null||!ValidSource(payment.SourceKind,payment.SourceSlot)||actor==null||saved==null)return false;
        var slots=payment.SourceKind==1?actor.Bag:payment.SourceKind==2?actor.Belt:RebirthRepairOriginalCorrection.ReadSavedEquipment(saved);
        if(slots==null||payment.SourceSlot>=slots.Length||slots[payment.SourceSlot]==null)return false;
        var source=slots[payment.SourceSlot];return removed?source.IsEmpty():source.count==1&&!source.IsEmpty()&&RebirthRemoteResourceDurableJournal.Digest(RebirthNativeItemCodec.Encode(source.itemValue))==payment.InputHash;
    }
    internal static bool CurrentRepairSource(EntityPlayerLocal player,PaymentIntent payment)
    {
        try
        {
            if(player==null||payment==null||!ValidSource(payment.SourceKind,payment.SourceSlot)||player.IsDead()||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player))return false;
            var slots=payment.SourceKind==1?player.bag?.ItemGrid.items:payment.SourceKind==2?player.inventory?.ItemGrid.items:player.equipment?.itemGrid?.items;
            return slots!=null&&payment.SourceSlot<slots.Length&&slots[payment.SourceSlot]?.count==1&&!slots[payment.SourceSlot].IsEmpty()&&
                RebirthRemoteResourceDurableJournal.Digest(RebirthNativeItemCodec.Encode(slots[payment.SourceSlot].itemValue))==payment.InputHash;
        }
        catch{return false;}
    }
    private static bool DebitOnly(ItemStack[] before,ItemStack[] after)=>before!=null&&after!=null&&before.Length==after.Length&&
        before.Select((item,index)=>item!=null&&after[index]!=null&&after[index].count>=0&&after[index].count<=item.count&&
            (after[index].IsEmpty()||!item.IsEmpty()&&RebirthNativeItemCodec.Encode(after[index].itemValue)==RebirthNativeItemCodec.Encode(item.itemValue))).All(value=>value);
    internal static bool TryHoldPreparedOriginal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||holds.ContainsKey(original.Operation)||!CanPrepareNativeOriginal(player))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value);
            if(!before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before.Copy(),After=before.Copy(),
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Authorized=()=>RebirthRemoteResourceClientOriginal.Current(player,original),SourceOperation=Guid.Empty,SourceBinding=string.Empty});
            return true;
        }
        catch{return false;}
    }
    internal static bool CanPrepareNativeOriginal(EntityPlayerLocal player)=>CanPrepareNativeOriginalCore(player,false);
    internal static bool CanReattachNativeTrade(EntityPlayerLocal player,RebirthNpcTradeOwnerLoad.Source source)=>
        source?.Current(player)==true&&CanPrepareNativeOriginalCore(player,true);
    internal static bool CanReattachNativeLocalTrade(EntityPlayerLocal player,RebirthNpcTradeLocalOwnerLoad.Source source)=>
        source?.Current(player)==true&&CanPrepareNativeOriginalCore(player,true);
    private static bool CanPrepareNativeOriginalCore(EntityPlayerLocal player,bool ownTrade)=>player!=null&&player.IsSpawned()&&!player.IsDead()&&!IsHeld(player)&&(ownTrade||!RebirthNpcTradeOwnerExchange.IsHeld(player))&&!RebirthConsumptionDurableDelivery.HoldsInventoryOwner(player)&&
        !RebirthHydrationOwnerHold.IsHeld(player)&&!RebirthCamperStationContentsDelivery.HoldsOwner(player)&&
        !RebirthLogisticsOwnerReceipt.IsHeld(player)&&!RebirthPlacedOriginal.IsHeld(player)&&!RebirthCookingOutputDelivery.HasOwnHold(player)&&!RebirthCookingOwnerDelta.HasOwnHold(player)&&!RebirthCookingGridCustody.IsHeld(player)&&
        !AdvancedFarmingHarvestOwnerDelta.IsHeld(player)&&!AdvancedFarmingWaterExchangeOriginal.IsHeld(player)&&!RebirthGearOwnerReservation.IsHeld(player)&&
        !RebirthBackpackLibraryReservation.IsHeld(player)&&!RebirthCamperEnergyOwnerHold.Blocks(player.bag)&&!RebirthCamperStationNativeActorPayment.Blocks(player.bag);
    internal static bool TryHoldRestoredRepairUsage(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Func<bool> authenticatedOriginal)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||authenticatedOriginal?.Invoke()!=true||
                !RebirthRemoteResourceClientOriginal.TryReadRepairAction(original,out var action)||!TryDecodeRepairPaymentIntent(action.Element("payment").Value,out var payment)||
                !RebirthRepairOriginalCorrection.TryObserveRestoredRemoteOriginal(player,payment.Encode(),original.Copy().Element("action")?.Element("nativeInput")?.Value,
                    action.Element("queuedInput").Value,(int)action.Element("amount"),out _))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value);
            if(!after.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            if(holds.TryGetValue(original.Operation,out var retained))
            {
                if(!ReferenceEquals(retained.Player,player)||retained.Original.Digest!=original.Digest||!retained.Before.Matches(before)||
                    retained.SourceOperation!=Guid.Empty&&(retained.SourceOperation!=payment.SourceOperation||retained.SourceBinding!=payment.Binding))return false;
                retained.After=after.Copy();retained.SourceOperation=payment.SourceOperation;retained.SourceBinding=payment.Binding;retained.Authorized=authenticatedOriginal;return Current(retained);
            }
            if(RebirthDogLifecycleService.HasHireInventoryHold(player)||IsHeld(player)||RebirthLogisticsOwnerReceipt.IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||RebirthCookingOutputDelivery.HasOwnHold(player)||RebirthCookingOwnerDelta.HasOwnHold(player)||RebirthCookingGridCustody.IsHeld(player)||
                AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||
                RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.bag))return false;
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before,After=after,
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Authorized=authenticatedOriginal,SourceOperation=payment.SourceOperation,SourceBinding=payment.Binding});return true;
        }
        catch{return false;}
    }
    // Predict only the local effects of the original repair callback. The
    // remote fraction stays virtual and never needs an empty backpack slot.
    // Unsupported source controllers retain their original intent; this pure
    // planner is not a fallback payment or permission to mutate another TE.
    internal static bool TryPlanRepairOwner(ItemActionEntryRepair entry,PaymentIntent payment,string paidInput,
        out RebirthCamperEnergyActorImage before,out RebirthCamperEnergyActorImage after)
    {
        before=null;after=null;
        try
        {
            var controller=entry?.ItemController;var player=controller?.xui?.playerUI?.entityPlayer;
            if(player==null||payment==null||!TryDecodeRepairPaymentIntent(payment.Encode(),out _)||player.Died!=payment.Deaths||
                !TrySource(entry,out int sourceKind,out int sourceSlot,out string actualInput)||sourceKind!=payment.SourceKind||sourceSlot!=payment.SourceSlot||actualInput!=paidInput||
                player.world.worldState.Guid!=payment.World||RebirthCamperCharacterAccess.CurrentCreation(player)!=payment.Creation||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.CanonicalId!=payment.CanonicalOwner||
                RebirthRemoteResourceDurableJournal.Digest(paidInput)!=payment.InputHash)return false;
            var source=controller is XUiC_EquipmentStack equipment?equipment.ItemStack:(controller as IItemStackController)?.ItemStack;
            if(source==null||source.count!=1||RebirthNativeItemCodec.Encode(source.itemValue)!=paidInput)return false;
            before=RebirthCamperEnergyActorImage.Capture(player);after=before.Copy();
            if(controller is XUiC_ItemStack slot&&!(controller is XUiC_EquipmentStack))
            {
                ItemStack[] native=slot.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack?after.Bag:
                    slot.StackLocation==XUiC_ItemStack.StackLocationTypes.ToolBelt?after.Belt:null;
                if(native==null||slot.SlotNumber<0||slot.SlotNumber>=native.Length||!Same(native[slot.SlotNumber],source))return false;
                native[slot.SlotNumber]=ItemStack.Empty.Clone();
            }
            else if(!(controller is XUiC_EquipmentStack))return false;
            int left=payment.LocalKits;
            foreach(var slots in new[]{after.Bag,after.Belt})
            for(int index=0;index<slots.Length&&left>0;index++)
            {
                var item=slots[index];if(item.IsEmpty()||item.itemValue.type!=payment.KitType||item.itemValue.HasModSlots&&item.itemValue.HasMods())continue;
                int take=Math.Min(left,item.count);var remaining=item.Clone();remaining.count-=take;
                slots[index]=remaining.count==0?ItemStack.Empty.Clone():remaining;left-=take;
            }
            if(left!=0||!before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            return true;
        }
        catch{before=null;after=null;return false;}
    }
    internal static bool TryHold(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,
        Guid sourceOperation,string sourceBinding,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,Func<bool> authenticatedOriginal)
    {
        try
        {
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||sourceOperation==Guid.Empty||sourceBinding?.Length!=64||
                sourceBinding.Any(value=>!(value>='0'&&value<='9'||value>='a'&&value<='f'))||before==null||after==null||authenticatedOriginal?.Invoke()!=true||
                !DebitOnly(before.Bag,after.Bag)||!DebitOnly(before.Belt,after.Belt)||!Same(before.Cursor,after.Cursor)||
                before.BagLocks==null||after.BagLocks==null||!before.BagLocks.SequenceEqual(after.BagLocks))return false;
            if(holds.TryGetValue(original.Operation,out var retained))
            {
                if(!ReferenceEquals(retained.Player,player)||retained.Original.Digest!=original.Digest||!retained.Before.Matches(before)||!Current(retained))return false;
                if(retained.SourceOperation==Guid.Empty)
                {
                    if(!retained.Before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
                    retained.SourceOperation=sourceOperation;retained.SourceBinding=sourceBinding;retained.After=after.Copy();retained.Authorized=authenticatedOriginal;
                }
                return retained.SourceOperation==sourceOperation&&retained.SourceBinding==sourceBinding&&retained.After.Matches(after)&&Current(retained);
            }
            if(RebirthDogLifecycleService.HasHireInventoryHold(player)||IsHeld(player)||RebirthLogisticsOwnerReceipt.IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||RebirthCookingOutputDelivery.HasOwnHold(player)||RebirthCookingOwnerDelta.HasOwnHold(player)||RebirthCookingGridCustody.IsHeld(player)||
                AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||
                RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.bag)||
                !before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before.Copy(),After=after.Copy(),
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Authorized=authenticatedOriginal,SourceOperation=sourceOperation,SourceBinding=sourceBinding});return true;
        }
        catch{return false;}
    }
    internal static bool BlocksPersonalQueue(XUiC_RecipeStack card)
    {
        if(card?.GetParentByType<XUiC_WorkstationWindowGroup>()!=null)return false;
        // An unrelated native queue can also deliver or refund into the held
        // actor, sometimes before a rejected slot write. Pause its original
        // update/cancel rather than moving a result into a transient outbox.
        bool held=IsHeld(card?.xui?.playerUI?.entityPlayer);
        var recipe=card?.recipe;if(recipe?.ingredients?.Any(ingredient=>ingredient?.itemValue?.HasMetadata(Marker)==true)!=true)return held;
        try
        {
            var marked=recipe.ingredients.Where(ingredient=>ingredient?.itemValue?.HasMetadata(Marker)==true).ToArray();if(marked.Length!=1)return true;
            var item=marked[0].itemValue;
            if(!item.TryGetMetadata(Marker,out string operationText)||!Guid.TryParseExact(operationText,"N",out var operation)||
                !item.TryGetMetadata(BindingMarker,out string binding)||!RebirthRemoteResourceClientOriginal.TryReadCraftTerminal(operation,binding,out var terminal)||
                !Guid.TryParseExact((string)terminal.Attribute("original"),"N",out var originalId)||!RebirthRemoteResourceClientOriginal.TryRead(originalId,out var original))return true;
            var player=card.xui?.playerUI?.entityPlayer;
            if(!RebirthRemoteResourceClientOriginal.Current(player,original)||RecipeImage(recipe)!=terminal.Element("markedRecipe").Value||
                card.GetRecipeCount()!=(int)terminal.Attribute("count")||card.OutputQuality!=(int)terminal.Attribute("quality")||
                !RebirthRemoteResourceClientOriginal.ReadActor(terminal.Element("afterActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))return true;
            if(holds.TryGetValue(originalId,out var retained)&&(retained.Original.Digest!=original.Digest||retained.SourceOperation!=operation||retained.SourceBinding!=binding))return true;
            // Positive host-terminal mirror was flushed before cleanup. Remove
            // only our marker so native cancellation returns original item
            // metadata and normal native output/cost behavior remains intact.
            if(!RebirthRemoteResourceClientOriginal.CompleteCraftTerminal(original,operation,binding))return true;
            var clone=item.Clone();clone.Metadata.Remove(Marker);clone.Metadata.Remove(BindingMarker);marked[0].itemValue=clone;
            holds.Remove(originalId);player.Buffs.SetCustomVar(Receipt(originalId),0f,true);player.Buffs.RemoveCustomVar(Receipt(originalId));
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return IsHeld(player);
        }
        catch{return true;}
    }
    // Only the retained original gesture may request its exact planned setter.
    // This is not a capability to restore an incoming snapshot or a whole bag.
    internal static bool WriteOriginalSlot(EntityPlayerLocal player,Guid original,ItemStackGrid grid,int slot,ItemStack desired)
    {
        if(!holds.TryGetValue(original,out var hold)||hold.BlockingOnly||!ReferenceEquals(hold.Player,player)||!Current(hold)||permit!=null)return false;
        bool bag=ReferenceEquals(grid,hold.Bag);if(!bag&&!ReferenceEquals(grid,hold.Belt))return false;
        var before=bag?hold.Before.Bag:hold.Before.Belt;var after=bag?hold.After.Bag:hold.After.Belt;
        if(slot<0||slot>=after.Length||desired==null||!Same(desired,after[slot])||!Same(grid.items[slot],before[slot])&&!Same(grid.items[slot],after[slot]))return false;
        if(Same(grid.items[slot],after[slot]))return true;
        var value=desired.Clone();var once=new Permit{Hold=hold,Grid=grid,Slot=slot,Value=value};permit=once;
        try{grid.SetItem(slot,value);}finally{permit=null;}
        return once.Used&&Current(hold)&&Same(grid.items[slot],after[slot]);
    }
    internal static bool ReleaseRepairNoUse(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        if(!RebirthRemoteResourceRepairRefund.Current(player,original)||!RebirthRemoteResourceRepairRefund.HasNoUse(original,source,binding,proof))return false;
        if(!holds.TryGetValue(original.Operation,out var held))return true;
        if(held.Original.Digest==original.Digest&&(held.Refund?held.RefundDeaths:held.Original.Deaths)<player.Died)
        {holds.Remove(original.Operation);return true;} // Owed proof releases only this obsolete original's hold.
        if(!ReferenceEquals(held.Player,player)||held.Original.Digest!=original.Digest||held.SourceOperation!=Guid.Empty&&(held.SourceOperation!=source||held.SourceBinding!=binding))return false;
        holds.Remove(original.Operation);return true;
    }
    internal static bool TryHoldRefund(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,
        RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,Func<bool> authenticatedOriginal)
    {
        try
        {
            if(!RebirthRemoteResourceRepairRefund.Current(player,original)||authenticatedOriginal?.Invoke()!=true||
                !RebirthRemoteResourceRepairRefund.IsPrepared(original,source,binding,before,after))return false;
            if(holds.TryGetValue(original.Operation,out var oldLife)&&oldLife.Original.Digest==original.Digest&&
                (oldLife.Refund?oldLife.RefundDeaths:oldLife.Original.Deaths)!=player.Died)holds.Remove(original.Operation);
            if(holds.TryGetValue(original.Operation,out var retained))return ReferenceEquals(retained.Player,player)&&retained.SourceOperation==source&&retained.SourceBinding==binding&&
                retained.Before.Matches(before)&&retained.After.Matches(after)&&Current(retained);
            if(RebirthDogLifecycleService.HasHireInventoryHold(player)||IsHeld(player)||RebirthLogisticsOwnerReceipt.IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||RebirthCookingOutputDelivery.HasOwnHold(player)||RebirthCookingOwnerDelta.HasOwnHold(player)||RebirthCookingGridCustody.IsHeld(player)||
                AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||
                RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.bag)||
                !before.Matches(RebirthCamperEnergyActorImage.Capture(player))&&!after.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            holds.Add(original.Operation,new Hold{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Original=original,Before=before.Copy(),After=after.Copy(),
                Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,SourceOperation=source,SourceBinding=binding,Authorized=authenticatedOriginal,Refund=true,RefundDeaths=player.Died});return true;
        }
        catch{return false;}
    }
    internal const string Marker="rebirth.fetch.owner.action.v1";
    internal const string BindingMarker="rebirth.fetch.owner.binding.v1";
    internal static string Receipt(Guid operation)=>"rbRemoteResourceOwner_"+operation.ToString("N");
    internal sealed class PaymentIntent
    {
        internal readonly Guid SourceOperation,ClientOriginal,OwnerAction;
        internal readonly string Binding,World,SourceWorld,Owner,CanonicalOwner,Creation,InputHash;
        internal readonly int Deaths,KitType,TotalKits,LocalKits,RemoteKits,SourceKind,SourceSlot;
        internal readonly ulong Epoch,Request;
        internal PaymentIntent(Guid source,Guid original,Guid action,string binding,string world,string sourceWorld,string owner,string canonical,string creation,
            int deaths,int kit,int total,int local,int remote,string input,ulong epoch,ulong request,int sourceKind=0,int sourceSlot=-1)
        {SourceOperation=source;ClientOriginal=original;OwnerAction=action;Binding=binding;World=world;SourceWorld=sourceWorld;Owner=owner;CanonicalOwner=canonical;Creation=creation;
            Deaths=deaths;KitType=kit;TotalKits=total;LocalKits=local;RemoteKits=remote;InputHash=input;Epoch=epoch;Request=request;SourceKind=sourceKind;SourceSlot=sourceSlot;}
        internal string Encode()=>new XElement("fetchRepairPayment",new XAttribute("version",SourceKind==0?1:2),new XAttribute("source",SourceOperation.ToString("N")),new XAttribute("original",ClientOriginal.ToString("N")),
            new XAttribute("action",OwnerAction.ToString("N")),new XAttribute("binding",Binding),new XAttribute("world",World),new XAttribute("sourceWorld",SourceWorld),new XAttribute("owner",Owner),new XAttribute("canonicalOwner",CanonicalOwner),
            new XAttribute("creation",Creation),new XAttribute("deaths",Deaths),new XAttribute("kitType",KitType),new XAttribute("total",TotalKits),new XAttribute("local",LocalKits),new XAttribute("remote",RemoteKits),
            new XAttribute("input",InputHash),new XAttribute("epoch",Epoch),new XAttribute("request",Request),SourceKind==0?null:new XAttribute("sourceKind",SourceKind),SourceKind==0?null:new XAttribute("sourceSlot",SourceSlot)).ToString(SaveOptions.DisableFormatting);
    }
    private sealed class RepairScope:IDisposable
    {
        internal EntityPlayerLocal Player;internal ItemActionEntryRepair Entry;internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal PaymentIntent Payment;internal Func<bool> Authorized;internal bool Entered;
        public void Dispose(){if(ReferenceEquals(repairScope,this))repairScope=null;}
    }
    [ThreadStatic]private static RepairScope repairScope;
    // Detached planning after the authenticated source response. This creates
    // no execution scope, hold, permission, saved receipt or inventory effect.
    internal static bool TryCreateRepairPaymentIntent(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,
        string sourceWorld,Guid sourceOperation,string binding,ItemStack[] paid,int kitType,int totalKits,string exactNativeInput,out PaymentIntent payment)
    {
        payment=null;
        try
        {
            if(original?.Kind!=RemoteResourceClientOperation.ItemRepair||!RebirthRemoteResourceClientOriginal.Current(player,original)||
                sourceWorld?.Length!=64||binding?.Length!=64||paid==null||paid.Length<1||totalKits<1||
                paid.Any(item=>item==null||item.IsEmpty()||item.count<1||item.itemValue.type!=kitType||item.itemValue.HasModSlots&&item.itemValue.HasMods())||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||
                !RebirthNativeItemCodec.TryDecode(exactNativeInput,out var value)||RebirthNativeItemCodec.Encode(value)!=exactNativeInput||
                original.Copy().Element("action")?.Element("nativeInput")?.Value!=exactNativeInput)return false;
            long remote=paid.Sum(item=>(long)item.count);if(remote<1||remote>totalKits)return false;
            var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            if(needs.Any(item=>item.IsEmpty()||item.itemValue.type!=kitType)||needs.Sum(item=>(long)item.count)!=totalKits||
                RebirthRemoteResourceDurableJournal.Operation(sourceWorld,identity.StorageKey,original.Creation,original.Epoch,original.Request,RemoteResourceClientOperation.ItemRepair)!=sourceOperation)return false;
            var action=original.Copy().Element("action");int sourceKind=(int?)action.Attribute("sourceKind")??0,sourceSlot=(int?)action.Attribute("sourceSlot")??-1;
            if(!ValidSource(sourceKind,sourceSlot))return false;
            var candidate=new PaymentIntent(sourceOperation,original.Operation,original.Operation,binding,original.World,sourceWorld,identity.StorageKey,identity.CanonicalId,
                original.Creation,original.Deaths,kitType,totalKits,totalKits-(int)remote,(int)remote,RebirthRemoteResourceDurableJournal.Digest(exactNativeInput),original.Epoch,original.Request,sourceKind,sourceSlot);
            if(!TryDecodeRepairPaymentIntent(candidate.Encode(),out payment))return false;return true;
        }
        catch{payment=null;return false;}
    }
    // Only the retained native entry may enter, once, while its exact saved
    // host permission and original BEFORE still hold. Other actions remain
    // fenced; nested entry calls cannot reuse this permission.
    internal static bool AllowOriginalEntry(BaseItemActionEntry entry)
    {
        try
        {
            if(craftScope!=null)return EnterCraftOriginal(entry);
            var scope=repairScope;
            if(scope==null||scope.Entered||!ReferenceEquals(scope.Entry,entry)||
                !holds.TryGetValue(scope.Original.Operation,out var hold)||!Current(hold)||scope.Authorized?.Invoke()!=true||
                !hold.Before.Matches(RebirthCamperEnergyActorImage.Capture(scope.Player)))return false;
            scope.Entered=true;return true;
        }
        catch{return false;}
    }
    // The actual owner coordinator enters only around its original native
    // repair callback, after the host saved original application permission.
    internal static IDisposable BeginRepairOriginalScope(EntityPlayerLocal player,ItemActionEntryRepair entry,RebirthRemoteResourceClientOriginal.Intent original,
        string sourceWorld,Guid sourceOperation,string binding,ItemStack[] actualPaid,int kitType,int totalKits,string exactNativeInput,Func<bool> authenticatedPermission)
    {
        try
        {
            if(repairScope!=null||craftScope!=null||entry==null||original?.Kind!=RemoteResourceClientOperation.ItemRepair||!RebirthRemoteResourceClientOriginal.Current(player,original)||
                !holds.TryGetValue(original.Operation,out var held)||!Current(held)||held.SourceOperation!=sourceOperation||held.SourceBinding!=binding||
                authenticatedPermission?.Invoke()!=true||sourceOperation==Guid.Empty||sourceWorld?.Length!=64||binding?.Length!=64||actualPaid==null||actualPaid.Length<1||
                totalKits<1||actualPaid.Any(item=>item==null||item.IsEmpty()||item.itemValue.type!=kitType||item.count<1||item.itemValue.HasModSlots&&item.itemValue.HasMods())||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||!RebirthNativeItemCodec.TryDecode(exactNativeInput,out var input)||RebirthNativeItemCodec.Encode(input)!=exactNativeInput)return null;
            long remote=actualPaid.Sum(item=>(long)item.count);if(remote<1||remote>totalKits)return null;
            var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            if(needs.Any(item=>item.IsEmpty()||item.itemValue.type!=kitType)||needs.Sum(item=>(long)item.count)!=totalKits)return null;
            if(RebirthRemoteResourceDurableJournal.Operation(sourceWorld,identity.StorageKey,original.Creation,original.Epoch,original.Request,RemoteResourceClientOperation.ItemRepair)!=sourceOperation)return null;
            if(!TryCreateRepairPaymentIntent(player,original,sourceWorld,sourceOperation,binding,actualPaid,kitType,totalKits,exactNativeInput,out var expected)||
                !TrySource(entry,out int sourceKind,out int sourceSlot,out _)||sourceKind!=expected.SourceKind||sourceSlot!=expected.SourceSlot)return null;
            var scope=new RepairScope{Player=player,Entry=entry,Original=original,Authorized=authenticatedPermission,Payment=expected};
            repairScope=scope;return scope;
        }
        catch{return null;}
    }
    internal static bool TryCaptureRepairPaymentIntent(ItemActionEntryRepair entry,string nativeInput,int expectedKits,out PaymentIntent payment)
    {
        payment=null;var scope=repairScope;
        if(scope==null||!ReferenceEquals(scope.Entry,entry)||!RebirthRemoteResourceClientOriginal.Current(scope.Player,scope.Original)||scope.Authorized?.Invoke()!=true||
            expectedKits!=scope.Payment.TotalKits||RebirthRemoteResourceDurableJournal.Digest(nativeInput)!=scope.Payment.InputHash)return false;
        payment=scope.Payment;return true;
    }
    internal static bool TryStampRepairQueueOriginal(ItemValue original,out ItemValue marked)
    {
        marked=null;var scope=repairScope;
        if(scope==null||!RebirthRemoteResourceClientOriginal.Current(scope.Player,scope.Original)||scope.Authorized?.Invoke()!=true||original==null||original.IsEmpty())return false;
        try
        {
            var value=original.Clone();if(value.HasMetadata(Marker)||value.HasMetadata(BindingMarker)||(value.Metadata?.Count??0)>byte.MaxValue-2)return false;
            value.SetMetadata(Marker,scope.Payment.SourceOperation.ToString("N"));value.SetMetadata(BindingMarker,scope.Payment.Binding);marked=value;return true;
        }
        catch{return false;}
    }
    internal static bool TryDecodeRepairPaymentIntent(string text,out PaymentIntent payment)
    {
        payment=null;
        try
        {
            var root=RebirthCookingRemoteAdmission.ReadProtocolXml(text,16384);
            bool Hex(string value)=>value?.Length==64&&value.All(character=>character>='0'&&character<='9'||character>='a'&&character<='f');
            Guid Id(string name){if(!Guid.TryParseExact((string)root.Attribute(name),"N",out var id)||id==Guid.Empty)throw new InvalidDataException();return id;}
            int version=(int?)root.Attribute("version")??0;
            if(root.Name!="fetchRepairPayment"||root.Attributes().Count()!=(version==1?18:20)||root.Nodes().Any()||(version!=1&&version!=2)||
                !Guid.TryParse((string)root.Attribute("world"),out _)||!Hex((string)root.Attribute("sourceWorld"))||!Hex((string)root.Attribute("binding"))||
                !Hex((string)root.Attribute("input"))||!RebirthOriginalActionScope.Validate((string)root.Attribute("owner"),(string)root.Attribute("creation"))||
                string.IsNullOrEmpty((string)root.Attribute("canonicalOwner"))||((string)root.Attribute("canonicalOwner")).Length>512)return false;
            int deaths=(int)root.Attribute("deaths"),kit=(int)root.Attribute("kitType"),total=(int)root.Attribute("total"),local=(int)root.Attribute("local"),remote=(int)root.Attribute("remote");
            ulong epoch=(ulong)root.Attribute("epoch"),request=(ulong)root.Attribute("request");Guid source=Id("source"),original=Id("original"),action=Id("action");
            int sourceKind=version==1?0:(int)root.Attribute("sourceKind"),sourceSlot=version==1?-1:(int)root.Attribute("sourceSlot");
            if(version==2&&!ValidSource(sourceKind,sourceSlot))return false;
            if(deaths<0||kit<=0||ItemClass.GetForId(kit)==null||total<1||local<0||remote<1||(long)local+remote!=total||epoch==0||request==0||action!=original||
                original!=RebirthRemoteResourceClientOriginal.OperationFor((string)root.Attribute("world"),(string)root.Attribute("canonicalOwner"),(string)root.Attribute("creation"),epoch,request,RemoteResourceClientOperation.ItemRepair)||
                RebirthRemoteResourceDurableJournal.Operation((string)root.Attribute("sourceWorld"),(string)root.Attribute("owner"),(string)root.Attribute("creation"),epoch,request,RemoteResourceClientOperation.ItemRepair)!=source)return false;
            payment=new PaymentIntent(source,original,action,(string)root.Attribute("binding"),(string)root.Attribute("world"),(string)root.Attribute("sourceWorld"),(string)root.Attribute("owner"),
                (string)root.Attribute("canonicalOwner"),(string)root.Attribute("creation"),deaths,kit,total,local,remote,(string)root.Attribute("input"),epoch,request,sourceKind,sourceSlot);
            return payment.Encode()==text;
        }
        catch{payment=null;return false;}
    }
    // This publishes permission only. It observes the original owner BEFORE
    // receipt in the final native file and never changes any inventory.
    internal static bool TryPrepareRepairOwnerAction(EntityPlayer actor,string encodedPayment,string exactBQueuedInput,int amount,
        RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||before==null||after==null||amount<1||amount>ushort.MaxValue||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.IsDead()||
                !TryDecodeRepairPaymentIntent(encodedPayment,out var payment)||!ValidSource(payment.SourceKind,payment.SourceSlot)||actor.Died!=payment.Deaths||actor.world.worldState.Guid!=payment.World||
                !RebirthOriginalActionScope.Matches(actor,payment.Owner,payment.Creation)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=payment.Owner||identity.CanonicalId!=payment.CanonicalOwner||
                !RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||world!=payment.SourceWorld||
                !RebirthRemoteResourceDurableJournal.TryRead(payment.Owner,payment.SourceOperation,out var original,out _)||original?.Phase!=2||original.Binding!=payment.Binding||
                !DebitOnly(before.Bag,after.Bag)||!DebitOnly(before.Belt,after.Belt)||!Same(before.Cursor,after.Cursor)||
                before.BagLocks==null||after.BagLocks==null||!before.BagLocks.SequenceEqual(after.BagLocks)||
                !RebirthNativeItemCodec.TryDecode(exactBQueuedInput,out var queued)||RebirthNativeItemCodec.Encode(queued)!=exactBQueuedInput||
                !queued.TryGetMetadata(Marker,out string source)||source!=payment.SourceOperation.ToString("N")||
                !queued.TryGetMetadata(BindingMarker,out string binding)||binding!=payment.Binding||
                !queued.TryGetMetadata("RebirthRepairOriginalV1",out string repair)||!Guid.TryParseExact(repair,"N",out var repairId)||repairId==Guid.Empty)return false;
            var root=original.Copy();
            if((int)root.Attribute("kind")!=(int)RemoteResourceClientOperation.ItemRepair||((int?)root.Attribute("ownerRejecting")??0)!=0)return false;
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("removed").Value));
            long Count(RebirthCamperEnergyActorImage image)=>image.Bag.Concat(image.Belt).Where(item=>!item.IsEmpty()&&item.itemValue.type==payment.KitType).Sum(item=>(long)item.count);
            if(paid.Any(item=>item.IsEmpty()||item.itemValue.type!=payment.KitType)||paid.Sum(item=>(long)item.count)!=payment.RemoteKits||
                Count(before)-Count(after)!=payment.LocalKits||
                !RebirthCamperEnergySavedOwner.TryRead(identity,out var savedActor,out var saved)||!before.Matches(savedActor)||saved.deaths!=payment.Deaths||!SavedSource(payment,savedActor,saved,false)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,Receipt(payment.OwnerAction),out float phase,out bool present)||!present||phase!=1f)return false;
            string beforeProof=RebirthRemoteResourceDurableJournal.Digest(payment.Owner+"|"+payment.OwnerAction.ToString("N")+"|1|"+RebirthRemoteResourceClientOriginal.ActorImage(savedActor)+"|"+payment.SourceKind+":"+payment.SourceSlot+"|"+payment.InputHash);
            var action=new XElement("ownerAction",new XAttribute("domain","native-repair"),new XAttribute("child",payment.OwnerAction.ToString("N")),new XAttribute("nativeBeforeProof",beforeProof),
                new XElement("payment",encodedPayment),new XElement("repairInput",exactBQueuedInput),new XElement("repairAmount",amount),
                new XElement("beforeActor",RebirthRemoteResourceClientOriginal.ActorImage(before)),new XElement("afterActor",RebirthRemoteResourceClientOriginal.ActorImage(after)));
            if(root.Element("ownerAction")!=null)return XNode.DeepEquals(root.Element("ownerAction"),action);
            bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
                !actor.IsDead()&&actor.Died==payment.Deaths&&actor.world.worldState.Guid==payment.World&&RebirthOriginalActionScope.Matches(actor,payment.Owner,payment.Creation);
            return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>value.Add(new XElement(action)),Current,out var retained)&&
                retained?.Phase==2&&XNode.DeepEquals(retained.Copy().Element("ownerAction"),action);
        }
        catch{return false;}
    }
    // Native custom transport authenticates its exact sender before this
    // domain edge. Unresolved source/save evidence is a retained query, never
    // a failed outcome or owner refund. Stage 0 requests original source work;
    // stages 1/2 operate only that already retained canonical original.
    internal static bool ResolveRepairOriginal(EntityPlayer actor,ulong epoch,ulong request,int stage,System.Collections.Generic.List<ItemStack> remoteNeeds,
        string originalAction,out RebirthRemoteResourceDurableJournal.Record result,out bool ownerPermission)
    {
        result=null;ownerPermission=false;
        try
        {
            if(stage<0||stage>4||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||
                !RebirthRemoteResourceDurableDispatch.Resolve(actor,epoch,request,RemoteResourceClientOperation.ItemRepair,remoteNeeds,stage!=0,out var source)||source==null||source.Phase<2)return false;
            result=source;if(source.Phase>=3)return true;
            if(stage==0)return true;
            var action=RebirthCookingRemoteAdmission.ReadProtocolXml(originalAction,24*1024*1024);
            if(action.Name!="fetchRepairOwnerOriginal"||action.Attributes().Count()!=3||action.Elements().Count()!=5||(int?)action.Attribute("version")!=1||
                action.Elements().GroupBy(node=>node.Name).Any(group=>group.Count()!=1)||
                action.Elements().Any(node=>!new[]{"payment","queuedInput","amount","beforeActor","afterActor"}.Contains(node.Name.LocalName)||node.Name.NamespaceName.Length!=0)||
                !TryDecodeRepairPaymentIntent(action.Element("payment")?.Value,out var payment)||payment.SourceOperation!=source.Operation||payment.Binding!=source.Binding||
                payment.Owner!=identity.StorageKey||payment.Epoch!=epoch||payment.Request!=request||
                (string)action.Attribute("original")!=payment.ClientOriginal.ToString("N")||
                !Guid.TryParseExact((string)action.Attribute("original"),"N",out _)||
                ((string)action.Attribute("originalDigest"))?.Length!=64||((string)action.Attribute("originalDigest")).Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
            string input=action.Element("queuedInput").Value;int amount=(int)action.Element("amount");
            if(stage==1)ownerPermission=TryPrepareRepairOwnerAction(actor,payment.Encode(),input,amount,
                RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value),RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value));
            else if(stage==2)TryCompleteSavedRepairPayment(actor,payment.Encode(),input,amount,out _,out _);
            else if(stage==3)TryBeginRepairRejection(actor,payment.Encode(),input,amount);
            else TryObserveRejectedRepairOwner(actor,payment.Encode(),input,amount,out _);
            if(!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,source.Operation,out result,out _)||result==null||result.Binding!=source.Binding)return false;
            return true;
        }
        catch{result=null;ownerPermission=false;return false;}
    }
    // Rejection is durable before an owner is allowed to freeze phase five.
    // This revokes future permission; it is not evidence that an earlier
    // permission was unused. An already accepted queue may still win until
    // the separate positive no-use witness has been published.
    internal static bool TryBeginRepairRejection(EntityPlayer actor,string encodedPayment,string exactQueuedInput,int amount)
    {
        try
        {
            if(!RepairRejectionCurrent(actor,encodedPayment,exactQueuedInput,amount,out var payment,out var original))return false;
            var root=original.Copy();
            if(((int?)root.Attribute("ownerRejecting")??0)==1)return true;
            return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>value.SetAttributeValue("ownerRejecting",1),
                ()=>RepairRejectionCurrent(actor,encodedPayment,exactQueuedInput,amount,out _,out _),out var saved)&&
                saved?.Phase==2&&((int?)saved.Copy().Attribute("ownerRejecting")??0)==1;
        }
        catch{return false;}
    }
    private static bool RepairRejectionCurrent(EntityPlayer actor,string encodedPayment,string exactQueuedInput,int amount,
        out PaymentIntent payment,out RebirthRemoteResourceDurableJournal.Record original)
    {
        payment=null;original=null;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
            !TryDecodeRepairPaymentIntent(encodedPayment,out payment)||!ValidSource(payment.SourceKind,payment.SourceSlot)||
            actor.Died!=payment.Deaths||actor.world.worldState.Guid!=payment.World||!RebirthOriginalActionScope.Matches(actor,payment.Owner,payment.Creation)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=payment.Owner||identity.CanonicalId!=payment.CanonicalOwner||
            !RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||world!=payment.SourceWorld||
            !RebirthRemoteResourceDurableJournal.TryRead(payment.Owner,payment.SourceOperation,out original,out _)||original?.Phase!=2||original.Binding!=payment.Binding)return false;
        var root=original.Copy();var action=root.Element("ownerAction");
        return (int)root.Attribute("kind")== (int)RemoteResourceClientOperation.ItemRepair&&(int)root.Attribute("outcome")==1&&
            (string)action?.Attribute("domain")=="native-repair"&&(string)action.Attribute("child")==payment.OwnerAction.ToString("N")&&
            action.Element("payment")?.Value==encodedPayment&&action.Element("repairInput")?.Value==exactQueuedInput&&
            (int?)action.Element("repairAmount")==amount;
    }
    internal static bool TryObserveRejectedRepairOwner(EntityPlayer actor,string encodedPayment,string exactQueuedInput,int amount,out string proof)
    {
        proof=null;
        try
        {
            if(!RepairRejectionCurrent(actor,encodedPayment,exactQueuedInput,amount,out var payment,out var original))return false;
            var root=original.Copy();if(((int?)root.Attribute("ownerRejecting")??0)!=1)return false;
            if(!RebirthRepairOriginalCorrection.TryReadSavedRejectedRepairOwner(actor,exactQueuedInput,amount,out var observed,out var saved)||
                saved.deaths!=payment.Deaths||!SavedSource(payment,observed,saved,false)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,Receipt(payment.OwnerAction),out float phase,out bool present)||!present||phase!=5f)return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(root.Element("ownerAction").Element("beforeActor").Value);
            if(!before.Matches(observed))return false;
            string witness=RebirthRemoteResourceDurableJournal.Digest("repair-unused|"+encodedPayment+"|"+exactQueuedInput+"|"+amount+"|"+
                RebirthRemoteResourceClientOriginal.ActorImage(observed)+"|"+payment.SourceKind+":"+payment.SourceSlot+"|"+payment.InputHash);
            string retained=(string)root.Attribute("ownerNoUseProof");
            if(retained!=null){if(retained!=witness)return false;proof=witness;return true;}
            if(!RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>value.SetAttributeValue("ownerNoUseProof",witness),
                ()=>RepairRejectionCurrent(actor,encodedPayment,exactQueuedInput,amount,out _,out var current)&&
                    ((int?)current.Copy().Attribute("ownerRejecting")??0)==1,out var published)||
                (string)published?.Copy().Attribute("ownerNoUseProof")!=witness)return false;
            proof=witness;return true;
        }
        catch{proof=null;return false;}
    }
    internal static bool TryVerifySavedRepairPayment(EntityPlayer actor,string paymentIntent,string exactBQueuedInput,int amount,
        out Guid repairOriginal,out string proof)
    {
        repairOriginal=Guid.Empty;proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
                !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.IsDead()||!TryDecodeRepairPaymentIntent(paymentIntent,out var payment)||actor.Died!=payment.Deaths||
                actor.world.worldState.Guid!=payment.World||!RebirthOriginalActionScope.Matches(actor,payment.Owner,payment.Creation)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=payment.Owner||owner.CanonicalId!=payment.CanonicalOwner||
                !RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||world!=payment.SourceWorld||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,payment.SourceOperation,out var original,out _)||original==null||original.Phase<2||original.Binding!=payment.Binding)return false;
            var root=original.Copy();var action=root.Element("ownerAction");
            if(root.Attribute("ownerNoUseProof")!=null||root.Element("ownerRefund")!=null||
                (int)root.Attribute("kind")!=(int)RemoteResourceClientOperation.ItemRepair||(string)action?.Attribute("domain")!="native-repair"||
                ((string)action.Attribute("nativeBeforeProof"))?.Length!=64||
                (string)action.Attribute("child")!=payment.OwnerAction.ToString("N")||action.Element("payment")?.Value!=paymentIntent||
                action.Element("repairInput")?.Value!=exactBQueuedInput||(int?)action.Element("repairAmount")!=amount)return false;
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("removed").Value));
            if(paid.Any(item=>item.IsEmpty()||item.itemValue.type!=payment.KitType||item.count<1)||paid.Sum(item=>(long)item.count)!=payment.RemoteKits)return false;
            if(!RebirthRepairOriginalCorrection.TryReadSavedOriginalQueue(actor,exactBQueuedInput,amount,out repairOriginal,out var savedActor,out var saved)||saved.deaths!=payment.Deaths||
                payment.SourceKind!=0&&!SavedSource(payment,savedActor,saved,true)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,Receipt(payment.OwnerAction),out float phase,out bool present)||!present||phase!=2f)return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor")?.Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor")?.Value);
            long Count(RebirthCamperEnergyActorImage image)=>image.Bag.Concat(image.Belt).Where(item=>!item.IsEmpty()&&item.itemValue.type==payment.KitType).Sum(item=>(long)item.count);
            if(!after.Matches(savedActor)||Count(before)-Count(after)!=payment.LocalKits||!RebirthNativeItemCodec.TryDecode(exactBQueuedInput,out var queued)||
                !queued.TryGetMetadata(Marker,out string source)||source!=payment.SourceOperation.ToString("N")||
                !queued.TryGetMetadata(BindingMarker,out string binding)||binding!=payment.Binding)return false;
            proof=RebirthRemoteResourceDurableJournal.Digest(paymentIntent+"|"+repairOriginal.ToString("N")+"|"+exactBQueuedInput+"|"+amount+"|"+RebirthRemoteResourceClientOriginal.ActorImage(after));return true;
        }
        catch{repairOriginal=Guid.Empty;proof=null;return false;}
    }
    // The repair domain calls this while its exact queue is still present.
    // Persist the source terminal before acknowledging usage or releasing its
    // marker. A later missing queue is never reconstructed as paid usage.
    internal static bool TryCompleteSavedRepairPayment(EntityPlayer actor,string encodedPayment,string exactQueuedInput,int amount,
        out Guid repairOriginal,out string proof)
    {
        repairOriginal=Guid.Empty;proof=null;
        try
        {
            if(TryReadSavedRepairTerminal(actor,encodedPayment,exactQueuedInput,amount,out repairOriginal,out proof))return true;
            if(!TryVerifySavedRepairPayment(actor,encodedPayment,exactQueuedInput,amount,out var repair,out var observed)||
                !TryDecodeRepairPaymentIntent(encodedPayment,out var payment)||
                !RebirthRemoteResourceDurableJournal.TryRead(payment.Owner,payment.SourceOperation,out var original,out _)||original==null)return false;
            if(original.Phase==4)
            {
                var terminal=original.Copy();
                if((string)terminal.Attribute("disposition")!="used"||(string)terminal.Attribute("ownerProof")!=observed)return false;
                repairOriginal=repair;proof=observed;return true;
            }
            if(original.Phase!=2)return false;
            long revision=original.Revision;string binding=original.Binding;
            bool Current()=>ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&
                ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
                !actor.IsDead()&&actor.Died==payment.Deaths&&actor.world.worldState.Guid==payment.World&&
                RebirthOriginalActionScope.Matches(actor,payment.Owner,payment.Creation)&&
                RebirthRemoteResourceDurableJournal.TryRead(payment.Owner,payment.SourceOperation,out var current,out _)&&current!=null&&
                current.Binding==binding&&(current.Revision==revision&&current.Phase==2||
                    current.Revision==revision+1&&current.Phase==4&&(string)current.Copy().Attribute("disposition")=="used"&&
                    (string)current.Copy().Attribute("ownerProof")==observed);
            if(!RebirthRemoteResourceDurableJournal.TryAdvance(original,root=>
                {root.SetAttributeValue("phase",4);root.SetAttributeValue("disposition","used");root.SetAttributeValue("ownerProof",observed);},Current,out var saved)||saved?.Phase!=4)return false;
            repairOriginal=repair;proof=observed;return true;
        }
        catch{repairOriginal=Guid.Empty;proof=null;return false;}
    }
    // A positively saved terminal already proves the original queued usage.
    // Requiring the live queue/phase2 again would strand B admission if its
    // own save failed after root4, or the owner had already acknowledged3.
    private static bool TryReadSavedRepairTerminal(EntityPlayer actor,string encodedPayment,string exactQueuedInput,int amount,out Guid repairOriginal,out string proof)
    {
        repairOriginal=Guid.Empty;proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !TryDecodeRepairPaymentIntent(encodedPayment,out var payment)||actor.Died!=payment.Deaths||actor.world.worldState.Guid!=payment.World||
                !RebirthOriginalActionScope.Matches(actor,payment.Owner,payment.Creation)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||
                identity.StorageKey!=payment.Owner||identity.CanonicalId!=payment.CanonicalOwner||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||world!=payment.SourceWorld||
                !RebirthRemoteResourceDurableJournal.TryRead(payment.Owner,payment.SourceOperation,out var original,out _)||original?.Phase!=4||original.Binding!=payment.Binding)return false;
            var root=original.Copy();var action=root.Element("ownerAction");
            if((string)root.Attribute("disposition")!="used"||(int)root.Attribute("outcome")!=1||(int)root.Attribute("kind")!=(int)RemoteResourceClientOperation.ItemRepair||
                (string)action?.Attribute("domain")!="native-repair"||(string)action.Attribute("child")!=payment.OwnerAction.ToString("N")||
                action.Element("payment")?.Value!=encodedPayment||action.Element("repairInput")?.Value!=exactQueuedInput||(int?)action.Element("repairAmount")!=amount||
                !RebirthNativeItemCodec.TryDecode(exactQueuedInput,out var queued)||!queued.TryGetMetadata("RebirthRepairOriginalV1",out string repair)||
                !Guid.TryParseExact(repair,"N",out var id)||id==Guid.Empty||!queued.TryGetMetadata(Marker,out string source)||source!=payment.SourceOperation.ToString("N")||
                !queued.TryGetMetadata(BindingMarker,out string binding)||binding!=payment.Binding)return false;
            var after=RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor")?.Value);
            string expected=RebirthRemoteResourceDurableJournal.Digest(encodedPayment+"|"+id.ToString("N")+"|"+exactQueuedInput+"|"+amount+"|"+RebirthRemoteResourceClientOriginal.ActorImage(after));
            if((string)root.Attribute("ownerProof")!=expected)return false;repairOriginal=id;proof=expected;return true;
        }
        catch{repairOriginal=Guid.Empty;proof=null;return false;}
    }
    // Called by the repair domain only after its exact paid queue witness was
    // saved. A marker on an old item is never permission to replace it during
    // another repair; retire only this terminal original's two owned fields.
    internal static bool TryRetireRepairMarkers(EntityPlayer actor,ItemValue value,string encodedPayment,string savedPaymentProof,out ItemValue clean)
    {
        clean=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||value==null||value.IsEmpty()||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !TryDecodeRepairPaymentIntent(encodedPayment,out var payment)||savedPaymentProof?.Length!=64||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=payment.Owner||identity.CanonicalId!=payment.CanonicalOwner||
                actor.world.worldState.Guid!=payment.World||!RebirthOriginalActionScope.Matches(actor,payment.Owner,payment.Creation)||
                !RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||world!=payment.SourceWorld||
                !value.TryGetMetadata(Marker,out string operation)||operation!=payment.SourceOperation.ToString("N")||
                !value.TryGetMetadata(BindingMarker,out string binding)||binding!=payment.Binding||
                !RebirthRemoteResourceDurableJournal.TryRead(payment.Owner,payment.SourceOperation,out var original,out _)||original==null||original.Phase!=4||original.Binding!=payment.Binding)return false;
            var root=original.Copy();var action=root.Element("ownerAction");
            if((string)root.Attribute("disposition")!="used"||(string)root.Attribute("ownerProof")!=savedPaymentProof||
                (string)action?.Attribute("domain")!="native-repair"||(string)action.Attribute("child")!=payment.OwnerAction.ToString("N")||
                action.Element("payment")?.Value!=encodedPayment)return false;
            var detached=value.Clone();detached.Metadata.Remove(Marker);detached.Metadata.Remove(BindingMarker);clean=detached;return true;
        }
        catch{clean=null;return false;}
    }
    internal static string RecipeImage(Recipe recipe)
    {
        if(recipe==null||recipe.ingredients==null||recipe.ingredients.Count<1||recipe.ingredients.Count>4096)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);recipe.Write(writer);writer.Flush();if(stream.Length>4*1024*1024)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());}
    }
    private static string SavedRecipeImage(Recipe recipe,System.Collections.Generic.IReadOnlyDictionary<ItemStack,ItemStack> captured)
    {
        // Exact installed Recipe.Write v1 serializes these fields and its real
        // ingredients. Canonical raw values belong to this SAME file read.
        if(recipe?.Version!=1||captured==null)throw new InvalidDataException();
        var detached=new Recipe{Version=recipe.Version,itemValueType=recipe.itemValueType,count=recipe.count,IsScrap=recipe.IsScrap,
            craftingTime=recipe.craftingTime,craftExpGain=recipe.craftExpGain,craftingArea=recipe.craftingArea};
        foreach(var ingredient in recipe.ingredients)
        {
            if(!captured.TryGetValue(ingredient,out var native)||native.count!=ingredient.count)throw new InvalidDataException();
            detached.ingredients.Add(native.Clone());
        }
        return RecipeImage(detached);
    }
    // Invoke only on the actual native accepted recipe object inside the
    // original action scope. Never insert a fake ingredient or change costs.
    internal static bool StampActualRecipe(Recipe recipe,Guid operation,string binding)
    {
        if(!ThreadManager.IsMainThread()||operation==Guid.Empty||binding?.Length!=64||binding.Any(value=>!(value>='0'&&value<='9'||value>='a'&&value<='f'))||
            recipe?.ingredients==null)return false;
        var ingredient=recipe.ingredients.FirstOrDefault(stack=>stack!=null&&!stack.IsEmpty()&&stack.count>0);if(ingredient==null)return false;
        string operationText=operation.ToString("N");
        if(ingredient.itemValue.HasMetadata(Marker))return ingredient.itemValue.TryGetMetadata(Marker,out string existing)&&existing==operationText&&
            ingredient.itemValue.TryGetMetadata(BindingMarker,out string retained)&&retained==binding;
        if(ingredient.itemValue.HasMetadata(BindingMarker)||(ingredient.itemValue.Metadata?.Count??0)>byte.MaxValue-2)return false;
        var clone=ingredient.itemValue.Clone();clone.SetMetadata(Marker,operationText);clone.SetMetadata(BindingMarker,binding);ingredient.itemValue=clone;return true;
    }
    internal static bool IsMarked(Recipe recipe,Guid operation,string binding)
    {
        int count=0;
        foreach(var ingredient in recipe?.ingredients??new System.Collections.Generic.List<ItemStack>())
        {
            if(ingredient?.itemValue?.HasMetadata(Marker)!=true)continue;
            if(!ingredient.itemValue.TryGetMetadata(Marker,out string found)||found!=operation.ToString("N")||
                !ingredient.itemValue.TryGetMetadata(BindingMarker,out string digest)||digest!=binding)return false;count++;
        }
        return count==1;
    }
    internal static bool TrySavedCraftUsage(EntityPlayer actor,RebirthStablePlayerIdentity owner,RebirthRemoteResourceClientOriginal.Intent original,
        Guid sourceOperation,string sourceBinding,string exactMarkedRecipe,int count,int quality,RebirthCamperEnergyActorImage after,out string proof)
    {
        proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||owner==null||original==null||original.Kind!=RemoteResourceClientOperation.Craft||owner.CanonicalId!=original.Owner||sourceOperation==Guid.Empty||sourceBinding?.Length!=64||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.world.worldState.Guid!=original.World||actor.Died!=original.Deaths||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var actualOwner)||actualOwner.StorageKey!=owner.StorageKey||!RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation)||quality<0||quality>byte.MaxValue||
                count<1||count>short.MaxValue||after==null||!RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved,out var captured)||saved.deaths!=original.Deaths||
                saved.id!=actor.entityId||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,Receipt(original.Operation),out float phase,out bool present)||!present||phase!=2f||
                saved.craftingData?.RecipeQueueItems==null||saved.craftingData.RecipeQueueItems.Length>255)return false;
            var rows=saved.craftingData.RecipeQueueItems.Where(row=>row?.Recipe!=null&&IsMarked(row.Recipe,sourceOperation,sourceBinding)).ToArray();
            if(rows.Length!=1||rows[0].RepairItem!=null&&rows[0].RepairItem.type!=0||rows[0].AmountToRepair!=0||rows[0].Multiplier!=count||rows[0].Quality!=quality||SavedRecipeImage(rows[0].Recipe,captured)!=exactMarkedRecipe)return false;
            // The same final native file must contain both accepted queue and
            // local debit. No second file/cache read can supply either half.
            if(!after.Matches(actual))return false;
            proof=RebirthRemoteResourceDurableJournal.Digest(sourceOperation.ToString("N")+"|"+sourceBinding+"|"+original.Digest+"|"+exactMarkedRecipe+"|"+count+"|"+quality+"|"+RebirthRemoteResourceClientOriginal.ActorImage(after));return true;
        }
        catch{proof=null;return false;}
    }
}
