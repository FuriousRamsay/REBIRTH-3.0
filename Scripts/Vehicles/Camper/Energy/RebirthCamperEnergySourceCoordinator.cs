using System;
using System.Collections.Generic;
using System.Linq;

internal static class RebirthCamperEnergySourceCoordinator
{
    private sealed class Pending
    {
        internal EntityVehicle Vehicle;internal EntityPlayer Player;internal RebirthCamperEnergyTransferFrame Frame;
        internal Func<bool> Current;internal Action<bool> Finished;internal string Directory;
        internal RebirthCamperSavedVehicle.Expected BeforeSaved;internal int Stage;internal bool Sending;
        internal int LocalSavedStage=-1;
        internal Guid DeliveryScope;
        internal float LastProgressLog;
    }
    private static readonly Dictionary<Guid,Pending> pending=new Dictionary<Guid,Pending>();
    private static bool installed;
    // Applied receipts may still be retiring the owner's bookkeeping marker.
    // Once the vehicle receipt is terminal and the local owner inventory hold is
    // gone, that metadata acknowledgement must not freeze unrelated controls.
    internal static bool IsBusy(EntityVehicle vehicle)=>pending.Values.Any(p=>ReferenceEquals(p.Vehicle,vehicle)&&
        (p.Stage!=4||!(p.Player is EntityPlayerLocal local)||RebirthCamperEnergyOwnerHold.Blocks(local.bag)));
    internal static void Install()
    {
        if(installed)return;
        ModEvents.GameUpdate.RegisterHandler(Tick);ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);installed=true;
    }
    internal static bool Begin(EntityVehicle vehicle,EntityPlayer player,RebirthStablePlayerIdentity owner,string creation,
        long revision,RebirthCamperEnergySourcePlan.Action action,Guid source,int component,bool enabled,Func<bool> current,Action<bool> finished,int backpackSlot=-1,Guid deliveryScope=default(Guid))
    {
        if(!installed||!ThreadManager.IsMainThread()||vehicle?.world==null||vehicle.world.IsRemote()||player==null||owner==null||
            player.IsDead()||!ReferenceEquals(player.world,vehicle.world)||current==null||!current()||finished==null||
            string.IsNullOrWhiteSpace(creation)||RebirthCamperCharacterAccess.CurrentCreation(player)!=creation||deliveryScope==Guid.Empty)
        {RebirthCamperEnergyGestureTrace.Write("source.refused","reason=actor-session-admission action="+action+" component="+component+" bagSlot="+backpackSlot);return false;}
        if(action==RebirthCamperEnergySourcePlan.Action.Install&&(source!=Guid.Empty||component<0||component>=RebirthCamperEnergyCapacity.ForActor(player)))
        {RebirthCamperEnergyGestureTrace.Write("source.refused","reason=eligible-card-admission");return false;}
        try
        {
            var runtime=RebirthCamperEnergyRuntime.For(vehicle);var journal=RebirthCamperEnergyCustody.Journal();
            if(action==RebirthCamperEnergySourcePlan.Action.Remove&&(component<0||component>=12||
                !runtime.TryGetSourcePosition(source,out int actualPosition)||actualPosition!=component))
            {RebirthCamperEnergyGestureTrace.Write("source.refused","reason=source-card-identity");return false;}
            if(RebirthCamperEnergyCustody.FindPending(runtime.Identity)!=null||IsBusy(vehicle))
            {RebirthCamperEnergyGestureTrace.Write("source.refused","reason=existing-pending action="+action);return false;}
            if(journal.FindApplied(runtime.Identity,creation).Any(r=>
            {
                var f=RebirthCamperEnergyTransferFrame.Read(r);return f.Owner==owner.CanonicalId&&f.OwnerKey==owner.StorageKey&&
                !RebirthCamperEnergySavedOwner.HasSaved(owner,f.Operation,3,null)&&
                !RebirthCamperEnergySavedOwner.HasSaved(owner,f.Operation,1,f.After);
            })){RebirthCamperEnergyGestureTrace.Write("source.refused","reason=unretired-owner-receipt action="+action);return false;}
            RebirthCamperEnergyActorImage before;
            if(player is EntityPlayerLocal local)before=RebirthCamperEnergyActorImage.Capture(local);
            else if(!RebirthCamperEnergySavedOwner.TryRead(owner,out before,out _))
            {RebirthCamperEnergyGestureTrace.Write("source.refused","reason=saved-owner-read action="+action);return false;}
            // Power ticks advance the presentation revision independently of the
            // selected bank and owner inventory. Prepare against the current
            // authoritative image; TryPrepare still checks the exact source and
            // actor images before any debit is made.
            long planningRevision=runtime.Snapshot().Revision;
            if(!RebirthCamperEnergySourcePlan.TryPrepare(runtime,planningRevision,action,source,component,enabled,before,out var plan,out var reason,backpackSlot,RebirthCamperEnergyCapacity.ForActor(player)))
            {RebirthCamperEnergyGestureTrace.Write("source.plan-refused","action="+action+" component="+component+" bagSlot="+backpackSlot+" expectedRevision="+revision+
                " actualRevision="+runtime.Snapshot().Revision+" reason="+reason+" cursor "+RebirthCamperEnergyGestureTrace.Stack(before.Cursor));return false;}
            var frame=new RebirthCamperEnergyTransferFrame {Operation=Guid.NewGuid(),Vehicle=runtime.Identity,Actor=player.entityId,
                Owner=owner.CanonicalId,OwnerKey=owner.StorageKey,Creation=creation,Survivor=RebirthSurvivorMode.IsEnabledForCurrentWorld(),
                Before=plan.Before,After=plan.After,Change=plan.Change};
            if(!(player is EntityPlayerLocal))RebirthCamperRemoteTransport.AssertFrame(RebirthCamperEnergyOwnerPayload.Write(frame,RebirthCamperEnergyOwnerPayload.Stage.Apply,deliveryScope));
            // Refuse stale native metadata before creating a durable pending
            // receipt or taking a runtime hold. Otherwise a harmless drift can
            // leave the entire energy window busy until recovery.
            var item=vehicle.vehicle.GetUpdatedItemValue();string prior;
            if(item.TryGetMetadata(RebirthCamperEnergyState.Key,out prior)&&prior!=Convert.ToBase64String(frame.Change.Before))
            {Log.Out("[REBIRTH EnergyFlow] begin-refused action="+action+" reason=native-source-before-drift");return false;}
            // A refused in-memory hold must not leave a durable Prepared receipt
            // that blocks every later action in the Energy window.
            if(!runtime.Hold(frame.Operation)){RebirthCamperEnergyGestureTrace.Write("source.refused","reason=source-hold action="+action);return false;}
            try { journal.Prepare(frame.Operation,frame.Vehicle,creation,planningRevision,frame.Write(false),frame.Write(true)); }
            catch
            {
                // PublicationFinished can throw after the receipt reaches disk.
                // Retain the hold whenever that durable receipt may exist.
                bool mayHaveReceipt;
                try { mayHaveReceipt=journal.Read(frame.Operation)!=null; }
                catch { mayHaveReceipt=true; }
                if(!mayHaveReceipt)runtime.Release(frame.Operation);
                throw;
            }
            // Publish the exact source identity/before image through the sole
            // native writer before allowing any actor debit. Journal is already durable.
            item.SetMetadata("RebirthCamperIdentity",runtime.Identity.ToString("N"));
            item.SetMetadata(RebirthCamperEnergyState.Key,Convert.ToBase64String(frame.Change.Before));vehicle.SendSyncData(4);
            pending.Add(frame.Operation,new Pending {Vehicle=vehicle,Player=player,Frame=frame,Current=current,Finished=finished,
                Directory=GameIO.GetSaveGameDir(),DeliveryScope=deliveryScope,BeforeSaved=RebirthCamperSavedVehicle.RequestSave(vehicle)});
            Log.Out("[REBIRTH EnergyFlow] begin operation="+frame.Operation+" action="+action+" requestedRevision="+revision+" plannedRevision="+planningRevision);
            RebirthCamperEnergyGestureTrace.Write("source.accepted","action="+action+" component="+component+" bagSlot="+backpackSlot+" revision="+planningRevision);return true;
        }
        catch(Exception error){Log.Error("[REBIRTH EnergyFlow] begin-failed vehicle="+vehicle?.entityId+" action="+action+" error="+error);return false;}
    }
    internal static bool Resume(EntityVehicle vehicle,EntityPlayer player,RebirthStablePlayerIdentity owner,string creation,Func<bool> current,Action<bool> finished,Guid deliveryScope)
    {
        try
        {
            if(!installed||current==null||!current()||owner==null||player==null||finished==null||deliveryScope==Guid.Empty)return false;
            var runtime=RebirthCamperEnergyRuntime.For(vehicle);var journal=RebirthCamperEnergyCustody.Journal();var record=RebirthCamperEnergyCustody.FindPending(runtime.Identity);
            if(record==null)
            {
                // Applied source publication may precede owner retirement across
                // a disconnect. Resume only that exact historical operation.
                record=journal.FindApplied(runtime.Identity,creation).FirstOrDefault(r=>
                {
                    var f=RebirthCamperEnergyTransferFrame.Read(r);
                    return f.Owner==owner.CanonicalId&&f.OwnerKey==owner.StorageKey&&f.Actor==player.entityId&&
                        !RebirthCamperEnergySavedOwner.HasSaved(owner,f.Operation,3,null);
                });
            }
            if(record==null)return false;
            if(pending.TryGetValue(record.Operation,out var existing))
            {
                bool existingCurrent;
                try{existingCurrent=Current(existing);}
                catch(Exception error)
                {existingCurrent=false;Log.Error("[REBIRTH EnergyFlow] resume-old-session-invalid operation="+record.Operation+" error="+error);}
                if(existingCurrent)return false;
                // Closing and immediately reopening the window may happen before
                // Tick retires its old session. Keep the durable receipt and
                // rebind its recovery to the new, current session now.
                pending.Remove(record.Operation);
                existing.BeforeSaved?.Dispose();
                existing.BeforeSaved=null;
                Log.Out("[REBIRTH EnergyFlow] resume-rebind operation="+record.Operation+" oldStage="+existing.Stage);
            }
            var frame=RebirthCamperEnergyTransferFrame.Read(record);
            if(frame.Owner!=owner.CanonicalId||frame.OwnerKey!=owner.StorageKey||frame.Creation!=creation||frame.Actor!=player.entityId||
                frame.Survivor!=RebirthSurvivorMode.IsEnabledForCurrentWorld())
            {Log.Out("[REBIRTH EnergyFlow] resume-refused operation="+record.Operation+" reason=owner-or-actor-scope");return false;}
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied&&!runtime.Hold(frame.Operation))
            {Log.Out("[REBIRTH EnergyFlow] resume-refused operation="+record.Operation+" reason=runtime-hold");return false;}
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared&&RebirthCamperEnergySavedOwner.HasSaved(owner,frame.Operation,1,frame.After))
                record=journal.Advance(frame.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            var p=new Pending {Vehicle=vehicle,Player=player,Frame=frame,Current=current,Finished=finished,Directory=GameIO.GetSaveGameDir(),
                DeliveryScope=deliveryScope,Stage=record.State==RebirthCamperTransferJournal.Phase.Applied?4:record.State==RebirthCamperTransferJournal.Phase.Uncertain?3:0};
            if(p.Stage==0)p.BeforeSaved=RebirthCamperSavedVehicle.RequestSave(vehicle);
            pending.Add(frame.Operation,p);
            Log.Out("[REBIRTH EnergyFlow] resume operation="+frame.Operation+" stage="+p.Stage+" receipt="+record.State);return true;
        }
        catch(Exception error){Log.Error("[REBIRTH EnergyFlow] resume-failed vehicle="+vehicle?.entityId+" error="+error);return false;}
    }
    private static bool Current(Pending p)=>ThreadManager.IsMainThread()&&p.Current!=null&&p.Current()&&p.Player?.world!=null&&
        !p.Player.IsDead()&&ReferenceEquals(GameManager.Instance?.World,p.Player.world)&&ReferenceEquals(p.Vehicle.world,p.Player.world)&&
        ReferenceEquals(p.Player.world.GetEntity(p.Player.entityId),p.Player)&&ReferenceEquals(p.Vehicle.world.GetEntity(p.Vehicle.entityId),p.Vehicle)&&
        GameIO.GetSaveGameDir()==p.Directory&&RebirthCamperCharacterAccess.CurrentCreation(p.Player)==p.Frame.Creation;
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(pending.Count==0)return;
        foreach(var p in pending.Values.ToArray())
        {
            if(!Current(p)){Log.Out("[REBIRTH EnergyFlow] current-lost operation="+p.Frame.Operation+" stage="+p.Stage);Finish(p,false);continue;}
            try
            {
                if(p.Sending)continue;
                if(UnityEngine.Time.realtimeSinceStartup-p.LastProgressLog>=10f)
                {p.LastProgressLog=UnityEngine.Time.realtimeSinceStartup;Log.Out("[REBIRTH EnergyFlow] waiting operation="+p.Frame.Operation+" stage="+p.Stage+" savedStage="+p.LocalSavedStage);}
                if(p.Stage==0)
                {
                    if(!RebirthCamperSavedVehicle.Verify(p.Vehicle,p.BeforeSaved,()=>Current(p)))continue;
                    Log.Out("[REBIRTH EnergyFlow] vehicle-before-saved operation="+p.Frame.Operation);
                    Send(p,RebirthCamperEnergyOwnerPayload.Stage.Prepare,()=>p.Stage=1);continue;
                }
                if(p.Stage==1){Send(p,RebirthCamperEnergyOwnerPayload.Stage.Apply,()=>p.Stage=2);continue;}
                if(p.Stage==2)
                {
                    // Send's success requires exact native final owner AFTER.
                    // This transition is the durable paid proof; recovery no longer needs a live actor image.
                    RebirthCamperEnergyCustody.Journal().Advance(p.Frame.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                    p.Stage=3;Log.Out("[REBIRTH EnergyFlow] owner-applied operation="+p.Frame.Operation);continue;
                }
                if(p.Stage==3)
                {
                    RebirthCamperEnergyCustody.Ready(p.Vehicle);
                    var record=RebirthCamperEnergyCustody.Journal().Read(p.Frame.Operation);
                    if(record.State!=RebirthCamperTransferJournal.Phase.Applied)continue;
                    p.Stage=4;Log.Out("[REBIRTH EnergyFlow] vehicle-after-saved operation="+p.Frame.Operation);continue;
                }
                if(p.Stage==4)Send(p,RebirthCamperEnergyOwnerPayload.Stage.Retire,()=>Finish(p,true));
            }
            catch(Exception error){Log.Error("[REBIRTH EnergyFlow] failed operation="+p.Frame.Operation+" stage="+p.Stage+" error="+error);Finish(p,false);} // Durable receipt/holds intentionally retained.
        }
    }
    private static void Send(Pending p,RebirthCamperEnergyOwnerPayload.Stage stage,Action accepted)
    {
        if(p.Player is EntityPlayerLocal local)
        {
            if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=p.Frame.Owner||owner.StorageKey!=p.Frame.OwnerKey)return;
            int savedPhase=stage==RebirthCamperEnergyOwnerPayload.Stage.Prepare?2:stage==RebirthCamperEnergyOwnerPayload.Stage.Apply?1:3;
            var savedImage=stage==RebirthCamperEnergyOwnerPayload.Stage.Prepare?p.Frame.Before:stage==RebirthCamperEnergyOwnerPayload.Stage.Apply?p.Frame.After:null;
            if(p.LocalSavedStage==(int)stage)
            {if(Current(p)&&RebirthCamperEnergySavedOwner.HasSaved(owner,p.Frame.Operation,savedPhase,savedImage))accepted();return;}
            string key="rbEnergy_"+p.Frame.Operation.ToString("N");bool applied;
            if(stage==RebirthCamperEnergyOwnerPayload.Stage.Prepare)
            {applied=RebirthCamperEnergyOwnerHold.Prepare(local,p.Frame);if(applied)local.Buffs.SetCustomVar(key,2f,true);}
            else if(stage==RebirthCamperEnergyOwnerPayload.Stage.Apply)
            {applied=local.Buffs.GetCustomVar(key)==1f?p.Frame.After.Matches(RebirthCamperEnergyActorImage.Capture(local)):
                local.Buffs.GetCustomVar(key)==2f&&RebirthCamperEnergyOwnerHold.Apply(local,p.Frame);if(applied)local.Buffs.SetCustomVar(key,1f,true);}
            else {applied=local.Buffs.GetCustomVar(key)==3f||local.Buffs.GetCustomVar(key)==1f&&RebirthCamperEnergyOwnerHold.Retire(local,p.Frame);if(applied)local.Buffs.SetCustomVar(key,3f,true);}
            if(!applied){if(UnityEngine.Time.realtimeSinceStartup-p.LastProgressLog>=2f){p.LastProgressLog=UnityEngine.Time.realtimeSinceStartup;Log.Out("[REBIRTH EnergyFlow] owner-stage-refused operation="+p.Frame.Operation+" stage="+stage+" marker="+local.Buffs.GetCustomVar(key));}return;}
            p.LocalSavedStage=(int)stage;
            Log.Out("[REBIRTH EnergyFlow] owner-stage-applied operation="+p.Frame.Operation+" stage="+stage);
            GameManager.Instance.SaveLocalPlayerData();
            if(Current(p)&&RebirthCamperEnergySavedOwner.HasSaved(owner,p.Frame.Operation,savedPhase,savedImage))accepted();
            return;
        }
        p.Sending=true;
        if(!RebirthCamperRemoteOwnerApplication.SendOwnerApplication(p.Player,RebirthCamperEnergyOwnerPayload.Effect(p.Frame.Operation,stage),
            RebirthCamperRemoteOwnerEffectKind.EnergyTransfer,RebirthCamperEnergyOwnerPayload.Write(p.Frame,stage,p.DeliveryScope),success=>
            {p.Sending=false;if(!Current(p)){Finish(p,false);return;}if(success)accepted();else Finish(p,false);}))p.Sending=false;
    }
    private static void Finish(Pending p,bool success)
    {if(!pending.TryGetValue(p.Frame.Operation,out var active)||!ReferenceEquals(active,p))return;
        pending.Remove(p.Frame.Operation);p.BeforeSaved?.Dispose();p.BeforeSaved=null;
        Log.Out("[REBIRTH EnergyFlow] finish operation="+p.Frame.Operation+" success="+success+" stage="+p.Stage);try{p.Finished(success);}catch{}}
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {foreach(var p in pending.Values){p.BeforeSaved?.Dispose();p.BeforeSaved=null;}pending.Clear();} // Saved journal is authority after load.
}
