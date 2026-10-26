using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed partial class NetPackageRebirthVehicleAssemblyRequest
{
    private static int recoveryAdmissionTraceBudget;
    private void AdvanceNativeQuery(World world,EntityPlayer player,ClientInfo sender)
    {
        bool journalAvailable=RebirthVehicleRequestJournal.IsAvailable;
        if(RebirthLogSettings.RuntimeInstallLoggingEnabled&&recoveryAdmissionTraceBudget++<24)Log.Warning("[REBIRTH Vehicle Recovery] server id="+requestId+" version="+readVersion+
            " action="+action+" journal="+journalAvailable+" proof="+(ownerCustodyProof!=null)+" admitting="+RebirthVehicleTransferCoordinator.IsAdmitting);
        if(readVersion<3||action==RebirthVehicleAssemblyAction.Read||!journalAvailable)return;
        string actor=userId?.ToString()??string.Empty,fingerprint=BuildFingerprint();
        if(RebirthVehicleRequestJournal.TryGet(requestId,actor,fingerprint,out var completed)&&completed.Outcome!=RebirthVehicleRequestOutcome.Indeterminate)return;
        bool retained=RebirthVehicleRequestJournal.HasRetainedNativeOriginal(requestId,actor,fingerprint);
        if(!retained&&!RebirthVehicleRequestJournal.TryBegin(requestId,actor,fingerprint,out _,out _,true))
        {if(RebirthLogSettings.RuntimeInstallLoggingEnabled&&recoveryAdmissionTraceBudget++<24)Log.Warning("[REBIRTH Vehicle Recovery] server id="+requestId+" no retained original and admission refused");return;}
        if(RebirthLogSettings.RuntimeInstallLoggingEnabled&&recoveryAdmissionTraceBudget++<24)Log.Warning("[REBIRTH Vehicle Recovery] server id="+requestId+" advancing retained="+retained);
        TryNativeTransfer(world,player,sender,out _,out _,out _);
    }
    // Actual authenticated request entry. Never read/debit a server inventory
    // mirror: the existing native reservation positively binds the PDS image.
    private bool TryNativeTransfer(World world,EntityPlayer player,ClientInfo sender,out RebirthVehicleAssembly snapshot,out string message,out RebirthVehicleRequestOutcome outcome)
    {
        snapshot=null;message=Localization.Get("xuiRebirthVehicleTransferPending");outcome=RebirthVehicleRequestOutcome.Indeterminate;
        if(action==RebirthVehicleAssemblyAction.Read)return false;
        // New siphons take the direct service path. An older siphon that already
        // retained a native original must still enter recovery to settle its
        // gas receipt and release the backpack/toolbelt reservation.
        if(action==RebirthVehicleAssemblyAction.Siphon&&
            !RebirthVehicleRequestJournal.HasRetainedNativeOriginal(requestId,userId?.ToString()??string.Empty,BuildFingerprint()))return false;
        if(readVersion<3)return DenyNative(23); // Retain legacy unknown originals; never migrate them into a fresh grant.
        try
        {
            var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;string save=GameIO.GetSaveGameDir();int deaths=player.Died;
            var transport=sender?.netConnection!=null&&sender.netConnection.Length>0?sender.netConnection[0]:null;
            bool Current()=>RebirthVehicleTransferCoordinator.IsAdmitting&&ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(world,game.World)&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(world.GetEntity(player.entityId),player)&&!world.IsRemote()&&!player.IsDead()&&player.IsSpawned()&&player.Died==deaths&&save==GameIO.GetSaveGameDir()&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&
                (sender==null?player is EntityPlayerLocal&&ReferenceEquals(world.GetPrimaryPlayer(),player):sender.loginDone&&!sender.disconnecting&&
                    ReferenceEquals(manager.Clients?.ForEntityId(player.entityId),sender)&&transport!=null&&sender.netConnection!=null&&sender.netConnection.Length>0&&ReferenceEquals(sender.netConnection[0],transport)&&!sender.netConnection[0].IsDisconnected());
            if(!Current()||requestId==Guid.Empty||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return DenyNative(33);
            string actor=identity.CanonicalId,fingerprint=BuildFingerprint();
            if(RebirthSurvivorMode.IsEnabledForCurrentWorld())
            {if(!RebirthConsumptionOwnerPublication.TryCaptureCreation(player,out var creation)||!RebirthSurvivorRequestScope.Matches(creation,expectedCreationId))return DenyNative(36);}
            else if(expectedCreationId!=string.Empty)return DenyNative(37);
            var store=new RebirthVehicleTransferPlanStore(save,GameIO.GetSaveGameDir,Current);
            var plan=store.Read(requestId);
            if(plan!=null)
            {
                if(plan.ActorId!=actor||plan.Fingerprint!=fingerprint||plan.NativeDeaths<0||plan.NativeDeaths>deaths||plan.CreationId!=expectedCreationId||plan.Custody==null)return DenyNative(42);
                bool completedOriginal=store.HasCompletion(plan);
                if(plan.NativeDeaths!=deaths&&!completedOriginal)return DenyNative(44);
                if(ownerCustodyProof!=null&&ownerCustodyProof.Write().ToString()!=plan.Custody.Write().ToString())return DenyNative(45);
                ownerCustodyProof=plan.Custody;
                if(!completedOriginal&&!RebirthVehicleTransferCoordinator.TryReadNativeReservation(player,requestId,actor,expectedCreationId,fingerprint,ownerCustodyProof,false,Current,out _,out _))return DenyNative(47);
            }
            else
            {
                // Catalogue validation before a new plan also refuses unfinished
                // temp publication and another original's actor/assembly custody.
                var pending=store.Discover();
                if(pending.Any(original=>original.ActorId==actor||original.ReadBefore().AssemblyId==assemblyId))return DenyNative(54);
                if(ownerCustodyProof==null||!RebirthVehicleTransferCoordinator.TryReadNativeReservation(player,requestId,actor,expectedCreationId,fingerprint,ownerCustodyProof,false,Current,out var bag,out var belt))return DenyNative(55);
                if(RebirthVehicleTransferCustody.Image(bag,belt)!=ownerCustodyProof.OwnerImageHash)
                {
                    RebirthVehicleAssembly untouched=null;
                    if(carrier==RebirthVehicleAssemblyCarrier.VehicleEntity)RebirthVehicleAssemblyIndex.TryGetEntity(world.GetEntity(entityId) as EntityVehicle,out untouched);
                    else untouched=(world.GetTileEntity(position) as TileEntityRepairableVehicleRebirth)?.Assembly;
                    return RefuseNative(player,store,Current,untouched,out snapshot,out message,out outcome,Localization.Get("xuiRebirthVehicleChanged"));
                }
                if(ownerLocks==null||ownerLocks.Length!=bag.Length||ownerBeltLocks==null||ownerBeltLocks.Length!=belt.Length||ownerBeltSlots!=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length))return DenyNative(63);
                bool cursorDebit=(action==RebirthVehicleAssemblyAction.Install||action==RebirthVehicleAssemblyAction.Replace)&&sourceLocation==RebirthVehiclePartSourceLocation.None&&offeredPart!=null;
                if(ownerCustodyProof.CursorDebit!=cursorDebit||ownerCustodyProof.CursorCredit!=(action==RebirthVehicleAssemblyAction.Pickup))return DenyNative(65);
                if(!ReauthorizeReply(world,player))return DenyNative(66);
                RebirthVehicleAssembly before;EntityVehicle entity=null;
                if(carrier==RebirthVehicleAssemblyCarrier.VehicleEntity)
                {
                    entity=world.GetEntity(entityId) as EntityVehicle;
                    if(entity==null||!RebirthVehicleAssemblyIndex.TryGetEntity(entity,out before)||before==null)return RefuseNative(player,store,Current,null,out snapshot,out message,out outcome,Localization.Get("xuiRebirthVehicleMissing"));
                    before=before.DeepClone();
                    if(before.AssemblyId!=assemblyId||before.Revision!=expectedRevision||before.PendingInventoryTransferId!=Guid.Empty)return RefuseNative(player,store,Current,before,out snapshot,out message,out outcome);
                    float maximum=entity.vehicle?.GetMaxFuelLevel()??0f,level=entity.vehicle?.GetFuelLevel()??0f;
                    if(maximum>0f)
                    {
                        if(float.IsNaN(level)||float.IsInfinity(level)||level<0f||level>maximum)return DenyNative(77);
                        before.FuelPercent=level/maximum;
                        string derived=RebirthVehicleAssemblySerializer.ToBase64(before);
                        if(!RebirthVehicleAssemblyIndex.TryGetEntity(entity,out var indexed))return DenyNative(80);
                        if(RebirthVehicleAssemblySerializer.ToBase64(indexed)!=derived)
                        {
                            RebirthVehicleAssemblyIndex.RegisterEntity(entity,before.DeepClone());
                            if(!Current()||!RebirthVehicleAssemblyIndex.TryGetEntity(entity,out indexed)||RebirthVehicleAssemblySerializer.ToBase64(indexed)!=derived||entity.vehicle.GetFuelLevel()!=level||entity.vehicle.GetMaxFuelLevel()!=maximum)return DenyNative(84);
                            before=RebirthVehicleAssemblySerializer.FromBase64(derived);
                        }
                    }
                }
                else
                {
                    var tile=world.GetTileEntity(position) as TileEntityRepairableVehicleRebirth;if(tile?.Assembly==null)return RefuseNative(player,store,Current,null,out snapshot,out message,out outcome,Localization.Get("xuiRebirthVehicleMissing"));before=tile.Assembly.DeepClone();
                    if(before.AssemblyId!=assemblyId||before.Revision!=expectedRevision||before.PendingInventoryTransferId!=Guid.Empty)return RefuseNative(player,store,Current,before,out snapshot,out message,out outcome);
                }
                string refusal;
                try { plan=BuildNativePlan(world,player,before,entity,bag,belt,actor,fingerprint,out refusal); }
                catch(InvalidDataException error) when(action==RebirthVehicleAssemblyAction.Refuel)
                {
                    // Planning has not debited inventory or changed native fuel.
                    // Retire the exact saved owner reservation instead of retrying
                    // this deterministic refusal forever with both grids held.
                    Log.Warning("[REBIRTH Vehicle] Refuel plan refused before debit: "+error.Message);
                    return RefuseNative(player,store,Current,before,out snapshot,out message,out outcome,
                        Localization.Get("xuiRebirthVehicleCannotRefuel"));
                }
                if(plan==null)
                {if(refusal!=null&&Current())return RefuseNative(player,store,Current,before,out snapshot,out message,out outcome,refusal);return DenyNative(96);}
                if(plan.CreationId.Length>0&&(action==RebirthVehicleAssemblyAction.Install||action==RebirthVehicleAssemblyAction.Replace||action==RebirthVehicleAssemblyAction.Repair))
                {
                    string componentSource=(carrier==RebirthVehicleAssemblyCarrier.VehicleEntity?"entity:":"block:")+before.ContentId+":"+slotId;
                    string trainingAction=action==RebirthVehicleAssemblyAction.Repair?"repair":before.Parts.ContainsKey(slotId)?"replace":"install";
                    float fraction=plan.Health==null?1f:(float)(plan.Health.After-plan.Health.Before)/plan.Health.Maximum;
                    plan=plan.WithTraining(new RebirthVehicleTransferTraining(RebirthPhase9TechnicalTrainingService.CaptureMechanicsOriginalTraining(trainingAction,fraction,componentSource),componentSource));
                }
                if(!Current())return DenyNative(104);
                store.Prepare(plan);
                if(!Current()||store.Read(requestId)?.ToXml().ToString()!=plan.ToXml().ToString())return DenyNative(106);
            }
            if(!RebirthVehicleTransferCoordinator.TryRetain(player,sender,plan,out var transfer))
            {if(RebirthLogSettings.RuntimeInstallLoggingEnabled&&recoveryAdmissionTraceBudget++<24)Log.Warning("[REBIRTH Vehicle Recovery] server id="+requestId+" transfer retain refused");return true;}
            bool advanced=transfer.Advance();
            if(RebirthLogSettings.RuntimeInstallLoggingEnabled&&recoveryAdmissionTraceBudget++<24)Log.Warning("[REBIRTH Vehicle Recovery] server id="+requestId+" advance="+advanced+" finished="+transfer.Finished);
            snapshot=transfer.Finished&&!transfer.Rejected?plan.ReadAfter():plan.ReadBefore();
            if(!transfer.Finished&&RebirthVehicleTransferResolver.TryObserve(world,plan,Current,out var actual,out _,out _))snapshot=actual;
            if(!transfer.Finished)return true;
            // The original training amount was captured before the first debit.
            // The existing character award receipt settles the exact successful
            // native completion once, including after a failed character save.
            if(!transfer.Rejected&&plan.Training!=null&&!store.HasTrainingCompletion(plan))
            {
                if(!RebirthPhase9TechnicalTrainingService.AwardMechanicsOriginal(player,plan.Training.Raw,plan.Training.Source,"vehicle:"+plan.TransactionId.ToString("N"))||
                    !Current()||!store.TryCompleteTraining(plan,identity))return DenyNative(118);
            }
            outcome=transfer.Rejected?RebirthVehicleRequestOutcome.Rejected:RebirthVehicleRequestOutcome.Applied;
            message=transfer.Rejected?Localization.Get("xuiRebirthVehicleInventoryFull"):NativeCompletionMessage(plan);
            var result=new RebirthVehicleRequestResult{ActorId=userId?.ToString()??string.Empty,Fingerprint=fingerprint,Carrier=carrier,Position=position,EntityId=entityId,Snapshot=snapshot.DeepClone(),Message=message,Outcome=outcome,
                PickupNativeItem=outcome==RebirthVehicleRequestOutcome.Applied&&ownerCustodyProof.CursorCredit?NativePickup(plan):string.Empty};
            if(!RebirthVehicleRequestJournal.RememberNativeCompletion(requestId,result,transfer))outcome=RebirthVehicleRequestOutcome.Indeterminate;
            return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Vehicle] native transfer threw "+error.GetType().Name+": "+error.Message+"\n"+error.StackTrace);return DenyNative(127);}
    }
    // Every fail-closed exit of TryNativeTransfer reports the same "awaiting recovery" text with an
    // Indeterminate outcome, so a valid transfer that trips one predicate looks stuck. Name the exit
    // (line id in the original file) once per exit so the batch log shows which predicate refused.
    private static readonly HashSet<string> DeniedExits=new HashSet<string>();
    private bool DenyNative(int exit)
    {
        if(DeniedExits.Count<64&&DeniedExits.Add(exit+":"+requestId))
            Log.Warning("[REBIRTH Vehicle] native transfer fail-closed exit "+exit+" action="+action+" carrier="+carrier+" readVersion="+readVersion+" request="+requestId);
        return true;
    }
    private bool RefuseNative(EntityPlayer player,RebirthVehicleTransferPlanStore store,Func<bool> current,RebirthVehicleAssembly before,out RebirthVehicleAssembly snapshot,out string message,out RebirthVehicleRequestOutcome outcome,string reason=null)
    {
        RejectUntouched(before,out snapshot,out message,out outcome,reason);
        if(!RebirthVehicleRequestJournal.RememberNativeRefusal(new NativeRefusal(this,player,store,current,before,message)))outcome=RebirthVehicleRequestOutcome.Indeterminate;
        return true;
    }
    private static bool RejectUntouched(RebirthVehicleAssembly before,out RebirthVehicleAssembly snapshot,out string message,out RebirthVehicleRequestOutcome outcome,string reason=null)
    {snapshot=before?.DeepClone();message=reason??Localization.Get("xuiRebirthVehicleChanged");outcome=RebirthVehicleRequestOutcome.Rejected;return true;}
    private RebirthVehicleTransferPlan BuildNativePlan(World world,EntityPlayer player,RebirthVehicleAssembly before,EntityVehicle entity,ItemStack[] bag,ItemStack[] belt,string actor,string fingerprint,out string refusal)
    {
        refusal=null;bool Locked(int index)=>index>=0&&index<ownerLocks.Length&&ownerLocks[index]=='1';
        bool BeltLocked(int index)=>index<0||index>=ownerBeltLocks.Length||ownerBeltLocks[index]=='1';
        var target=new RebirthVehicleTransferTarget(carrier,carrier==RebirthVehicleAssemblyCarrier.RepairableBlock?position.x:0,carrier==RebirthVehicleAssemblyCarrier.RepairableBlock?position.y:0,carrier==RebirthVehicleAssemblyCarrier.RepairableBlock?position.z:0,action==RebirthVehicleAssemblyAction.Hotwire&&carrier==RebirthVehicleAssemblyCarrier.RepairableBlock);
        RebirthVehicleTransferItem Find(string name)
        {
            for(int area=0;area<2;area++)
            {var cells=area==0?bag:belt;int count=area==0?bag.Length:ownerBeltSlots;for(int index=0;index<count;index++)
                if(!(area==0?Locked(index):BeltLocked(index))&&cells[index]!=null&&!cells[index].IsEmpty()&&cells[index].itemValue.ItemClass?.GetItemName()==name)
                    return new RebirthVehicleTransferItem(Guid.NewGuid(),true,RebirthNativeItemCodec.Encode(cells[index].itemValue),1,area==0?RebirthVehiclePartSourceLocation.Backpack:RebirthVehiclePartSourceLocation.Toolbelt,index);}
            return null;
        }
        if(action==RebirthVehicleAssemblyAction.Repair)
        {
            if(entity==null||entity.Health>=entity.GetMaxHealth()){refusal=Localization.Get("xuiRebirthVehicleAlreadyRepaired");return null;}
            var kit=Find("resourceRepairKit");if(kit==null){refusal=Localization.Get("xuiRebirthVehicleNeedRepairKit");return null;}
            return RebirthVehicleTransferPlanner.Repair(requestId,expectedCreationId,actor,fingerprint,target,before,expectedRevision,player,entity.Health,entity.GetMaxHealth(),kit,player.Died).WithCustody(ownerCustodyProof);
        }
        if(action==RebirthVehicleAssemblyAction.Refuel)
        {
            if(entity?.vehicle==null){refusal=Localization.Get("xuiRebirthVehicleCannotRefuel");return null;}float level=entity.vehicle.GetFuelLevel(),maximum=entity.vehicle.GetMaxFuelLevel();
            if(level>=maximum){refusal=Localization.Get("xuiRebirthVehicleFuelFull");return null;}
            int requested=(int)Math.Ceiling(Math.Min(2500f,(maximum-level)*25f));
            var fuel=RebirthVehicleFuelSelection.Select(belt,ownerBeltSlots,bag,Locked,entity.GetVehicle().GetFuelItem(),requested,BeltLocked);
            if(fuel.Count==0){refusal=Localization.Get("xuiRebirthVehicleNeedFuel");return null;}
            return RebirthVehicleTransferPlanner.Refuel(requestId,expectedCreationId,actor,fingerprint,target,before,expectedRevision,entity.GetVehicle().GetFuelItem(),level,maximum,fuel,player.Died).WithCustody(ownerCustodyProof);
        }
        if(action==RebirthVehicleAssemblyAction.Siphon)
        {
            if(entity!=null&&entity.vehicle==null){refusal=Localization.Get("xuiRebirthVehicleCannotRefuel");return null;}
            float gas=entity!=null?entity.vehicle.GetFuelLevel()*25f:before.FuelPercent*500f;
            if(!float.IsNaN(gas)&&!float.IsInfinity(gas)&&Math.Round((double)gas,MidpointRounding.ToEven)<1d)
            {refusal=Localization.Get("xuiRebirthVehicleNoFuel");return null;}
            return RebirthVehicleTransferPlanner.Siphon(requestId,expectedCreationId,actor,fingerprint,target,before,expectedRevision,entity?.vehicle?.GetFuelLevel(),entity?.vehicle?.GetMaxFuelLevel(),entity?.GetVehicle()?.GetFuelItem(),player.Died).WithCustody(ownerCustodyProof);
        }
        var family=RebirthVehicleDefinitionRegistry.GetFamily(before.FamilyId);if(family==null){refusal=Localization.Get("xuiRebirthVehicleInvalidDefinition");return null;}
        var after=before.DeepClone();var items=new List<RebirthVehicleTransferItem>();
        if(action==RebirthVehicleAssemblyAction.Hotwire)
        {
            if(entity!=null){refusal=Localization.Get("xuiRebirthVehicleAlreadyConverted");return null;}
            var kit=Find("HotwireKit");if(kit==null){refusal=Localization.Get("xuiRebirthVehicleNeedHotwireKit");return null;}
            var missing=family.Slots.Where(slot=>slot.Required&&!before.Parts.ContainsKey(slot.SlotId)).ToArray();
            if(missing.Length>0){refusal=Localization.Get("xuiRebirthVehicleIncomplete");return null;}
            if(!RebirthVehicleEntityFactory.HasPersistentOwner(player)){refusal=Localization.Get("xuiRebirthVehicleOwnerUnavailable");return null;}
            var content=RebirthVehicleDefinitionRegistry.GetContent(before.ContentId);if(content?.Availability!=RebirthVehicleContentAvailability.Enabled){refusal=Localization.Get("xuiRebirthVehicleUnavailable");return null;}
            items.Add(kit);
        }
        else
        {
            var slot=family.Slots.SingleOrDefault(value=>value.SlotId==slotId);if(slot==null){refusal=Localization.Get("xuiRebirthVehicleInvalidSlot");return null;}
            before.Parts.TryGetValue(slotId,out var previous);
            if(action==RebirthVehicleAssemblyAction.Install||action==RebirthVehicleAssemblyAction.Replace)
            {
                RebirthInstalledVehiclePart incoming;
                if(ownerCustodyProof.CursorDebit)incoming=RebirthVehicleInventoryTransactions.SanitizeOfferedPart(offeredPart,slot.ItemName);
                else
                {
                    bool inBag=sourceLocation==RebirthVehiclePartSourceLocation.Backpack;
                    if(sourceLocation!=RebirthVehiclePartSourceLocation.Backpack&&sourceLocation!=RebirthVehiclePartSourceLocation.Toolbelt)return null;
                    var cells=inBag?bag:belt;int owned=inBag?bag.Length:ownerBeltSlots;
                    if(sourceSlot<0||sourceSlot>=owned||(inBag?Locked(sourceSlot):BeltLocked(sourceSlot))||cells[sourceSlot].IsEmpty()||cells[sourceSlot].itemValue.ItemClass?.GetItemName()!=slot.ItemName)
                    {refusal=Localization.Get("xuiRebirthVehicleNeedPart");return null;}
                    string original=RebirthNativeItemCodec.Encode(cells[sourceSlot].itemValue);
                    incoming=RebirthVehicleInventoryTransactions.FromItemStack(cells[sourceSlot].Clone(),world);
                    items.Add(new RebirthVehicleTransferItem(Guid.NewGuid(),true,original,1,sourceLocation,sourceSlot));
                }
                if(incoming==null)return null;incoming.SlotId=slotId;after.Parts[slotId]=incoming;
            }
            else if(action==RebirthVehicleAssemblyAction.Remove||action==RebirthVehicleAssemblyAction.Pickup)
            {if(previous==null){refusal=Localization.Get("xuiRebirthVehicleSlotEmpty");return null;}after.Parts.Remove(slotId);}
            else return null;
            if(previous!=null&&!ownerCustodyProof.CursorCredit)
            {
                var stack=RebirthVehicleInventoryTransactions.CreateItemStack(previous);if(stack==null||stack.IsEmpty())return null;
                items.Add(new RebirthVehicleTransferItem(Guid.NewGuid(),false,RebirthNativeItemCodec.Encode(stack.itemValue),1,RebirthVehiclePartSourceLocation.None,-1));
            }
        }
        if(after.Revision==long.MaxValue)return null;after.Revision++;after.PendingInventoryTransferId=Guid.Empty;after.LastAppliedInventoryTransferId=requestId;
        return RebirthVehicleTransferPlan.Create(requestId,expectedCreationId,actor,fingerprint,before,after,items,target,null,null,player.Died,ownerCustodyProof);
    }
    private string NativeCompletionMessage(RebirthVehicleTransferPlan plan)
    {
        switch(action)
        {
            case RebirthVehicleAssemblyAction.Hotwire:return Localization.Get("xuiRebirthVehicleConversionComplete");
            case RebirthVehicleAssemblyAction.Repair:return Localization.Get("xuiRebirthVehicleRepaired");
            case RebirthVehicleAssemblyAction.Refuel:return Localization.Get("xuiRebirthVehicleRefueled");
            case RebirthVehicleAssemblyAction.Siphon:return string.Format(Localization.Get("xuiRebirthVehicleSiphoned"),plan.Items.Where(item=>!item.Debit).Sum(item=>item.Count));
            case RebirthVehicleAssemblyAction.Remove:
            case RebirthVehicleAssemblyAction.Pickup:return Localization.Get("xuiRebirthVehiclePartRemoved");
            default:return Localization.Get(plan.ReadBefore().Parts.ContainsKey(slotId)?"xuiRebirthVehiclePartReplaced":"xuiRebirthVehiclePartInstalled");
        }
    }
    private string NativePickup(RebirthVehicleTransferPlan plan)
    {
        if(!plan.ReadBefore().Parts.TryGetValue(slotId,out var part))return string.Empty;
        var stack=RebirthVehicleInventoryTransactions.CreateItemStack(part);return stack==null||stack.IsEmpty()?string.Empty:RebirthNativeItemCodec.Encode(stack.itemValue);
    }
}
