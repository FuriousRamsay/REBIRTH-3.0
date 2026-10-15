using System;

// Local authoritative UI path. Bootstrap/transport supplies a verified character
// session and durable native save acknowledgement, never values from client UI.
public static class RebirthCamperManagementAccess
{
    public static void Open(EntityVehicle vehicle, EntityPlayerLocal player, bool crafting,
        string authenticatedCreation, RebirthCamperTransferJournal journal,
        Func<bool> authenticatedSession, Func<Guid,ItemStack,bool> verifyDurableNativeSave,
        Func<Guid,byte[],bool> verifyDurableVehicleSave)
    {
        if (!ThreadManager.IsMainThread() || journal == null || authenticatedSession == null
            || verifyDurableNativeSave == null || verifyDurableVehicleSave == null || string.IsNullOrWhiteSpace(authenticatedCreation)
            || !CanAccess(vehicle,player) || !authenticatedSession())
            throw new InvalidOperationException("Authenticated camper management session required.");
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        var unresolved=journal.FindPending(runtime.Identity);
        if(unresolved!=null)
        {
            try
            {
                if(unresolved.OwnerCreation!=authenticatedCreation
                    || unresolved.State!=RebirthCamperTransferJournal.Phase.Uncertain)
                    throw new InvalidOperationException("Pending transfer does not belong to this published character session.");
                RebirthCamperSaveAcknowledgement.Start(journal,unresolved.Operation,vehicle,player,authenticatedSession,true);
                RebirthCamperTrace.Write("acknowledgement.resume",vehicle,unresolved.Operation.ToString("N"));
            }
            catch(Exception error)
            {
                // Preserve uncertain custody and its receipt. Recovery never
                // repeats a debit or clears a marker just to open the window.
                Log.Warning("[CamperTransfer] Pending publication needs reconciliation: "+error.Message);
                RebirthCamperTrace.Write("acknowledgement.resume-refused",vehicle,error.Message);
            }
            finally { RebirthCamperModuleInteraction.Release(vehicle,player); }
            GameManager.ShowTooltip(player,Localization.Get("xuiRebirthCamperTransferRecover"));
            return;
        }
        journal.AssertAvailable(runtime.Identity,Guid.NewGuid());
        runtime.InitializeEmptyCargo();
        var ui=LocalPlayerUI.GetUIForPlayer(player);
        string name=crafting ? "rebirthCamperStationManagement" : "rebirthCamperStorageManagement";
        GUIWindow window;
        if(ui==null || ui.windowManager.IsWindowOpen(name) || !ui.windowManager.TryGetWindow(name,out window)
            || !(window is XUiWindowGroup)) throw new InvalidOperationException("Management window unavailable.");
        var view=((XUiWindowGroup)window).Controller.GetChildByType<XUiC_RebirthCamperModules>();
        var cursor=ui.xui.GetChildByType<XUiC_DragAndDropWindow>();
        if(view==null || cursor==null) throw new InvalidOperationException("Management window controllers missing.");
        RebirthCamperStationContentsDelivery.RecoverHeld(player,vehicle,ui.xui,journal,authenticatedCreation);
        var nativeCurrent=RebirthCamperModuleInteraction.CaptureCurrent(vehicle,player);
        var nativeRelease=RebirthCamperModuleInteraction.CaptureRelease(vehicle,player);
        Func<bool> current=()=>nativeCurrent() && CanAccess(vehicle,player) && authenticatedSession();
        Func<int,ItemStack,int,bool> publish=(slot,offered,backpackSlot)=>
            {
                bool admittedCurrent=current();
                if(!admittedCurrent || runtime.TransferPending){view.ReportRefusal("local.admission",slot,backpackSlot,"current="+admittedCurrent+" transferPending="+runtime.TransferPending);return false;}
                if(backpackSlot>=0&&(backpackSlot>=player.bag.ItemGrid.items.Length||!cursor.IsEmpty())){view.ReportRefusal("local.owner",slot,backpackSlot,"Backpack source out of bounds or cursor occupied");return false;}
                ItemStack held=backpackSlot<0?cursor.CurrentStack:player.bag.ItemGrid.items[backpackSlot];
                if(held==null || !Same(held,offered)){view.ReportRefusal("local.source",slot,backpackSlot,"Held item changed");throw new InvalidOperationException("Held item changed.");}
                if(held.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)==true)
                {
                    if(backpackSlot>=0)return false;
                    if(!RebirthCamperStationContentsDelivery.RecoverHeld(player,vehicle,ui.xui,journal,authenticatedCreation))return false;
                    held=cursor.CurrentStack;
                }
                byte[] prior=RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,vehicle.bag.ItemGrid.items,held,backpackSlot);
                var before=RebirthCamperTransferSnapshot.Restore(runtime.Identity,prior);
                RebirthCamperStorageInstallations storage=before.Storage;
                RebirthCamperStationInstallations stations=before.Stations;
                ItemStack[] cargo=before.Cargo;
                ItemStack nextCursor;
                if(crafting)
                {
                    var plan=RebirthCamperStationTransfer.PrepareCursor(runtime.Stations,runtime.Stations.Revision,held,slot);
                    stations=plan.Stations; nextCursor=plan.Backpack[0];
                }
                else
                {
                    RebirthCamperStorageTransfer plan;
                    try { plan=RebirthCamperStorageTransfer.PrepareCursor(runtime.Storage,runtime.Storage.Revision,held,cargo,slot); }
                    catch(Exception error) when(error is InvalidOperationException || error is ArgumentException)
                    {
                        view.ReportRefusal("local.storage-plan",slot,backpackSlot,error.GetType().Name+": "+error.Message);
                        // Planning has no custody writes. Full cargo and invalid
                        // containers are normal refusals, not unhandled UI errors.
                        GameManager.ShowTooltip(player,Localization.Get("xuiRebirthCamperModuleTransferRefused"));
                        return false;
                    }
                    storage=plan.Storage; cargo=plan.Cargo; nextCursor=plan.Backpack[0];
                }
                var after=RebirthCamperTransferSnapshot.Restore(runtime.Identity,
                    RebirthCamperTransferSnapshot.Capture(storage,stations,cargo,nextCursor,backpackSlot));
                try { RebirthCamperTransferCommit.AssertCargoPolicy(vehicle.bag.ItemGrid,before.Cargo,after.Cargo); }
                catch(InvalidOperationException policyError)
                {
                    view.ReportRefusal("local.cargo-policy",slot,backpackSlot,policyError.Message);
                    GameManager.ShowTooltip(player,policyError.Message);
                    return false;
                }
                RebirthCamperTrace.Write("transfer.owner",vehicle,"module="+slot+" backpack="+backpackSlot+" count="+held.count);
                RebirthCamperTransferJournal.Phase phase;
                try{phase=RebirthCamperTransferCommit.Publish(journal,Guid.NewGuid(),authenticatedCreation,
                    vehicle,player,cursor,before,after,current,verifyDurableNativeSave,verifyDurableVehicleSave,authenticatedSession);}
                catch(Exception error){view.ReportRefusal("local.publish",slot,backpackSlot,error.GetType().Name+": "+error.Message);throw;}
                if(backpackSlot>=0)view.GetChildByType<XUiC_RebirthBackpackSectionBackpack>()?.SetStacks(player.bag.ItemGrid.items);
                if(phase==RebirthCamperTransferJournal.Phase.Uncertain)
                {
                    view.ReportRefusal("local.publish-pending",slot,backpackSlot,"phase=Uncertain; native save acknowledgement pending, not an affected-slot lock refusal");
                    // Keep the authenticated window alive while the durable
                    // save observer resolves publication. Further module
                    // transfers remain blocked by TransferPending. Refresh
                    // from live state, never from the prepared candidate.
                    view.RefreshModules();
                    return false;
                }
                return phase==RebirthCamperTransferJournal.Phase.Applied;
            };
        view.Bind(Localization.Get(crafting ? "xuiRebirthCamperStationManagement" : "xuiRebirthCamperStorageManagement"),current,
            slot=>crafting ? runtime.Stations.GetSlot(slot)?.InstalledItem : runtime.Storage.GetSlot(slot),
            stack=>!runtime.TransferPending && Accepts(stack,crafting),
            (slot,offered)=>publish(slot,offered,-1),nativeRelease,crafting,
            (slot,backpackSlot)=>publish(slot,player.bag.ItemGrid.items[backpackSlot].Clone(),backpackSlot));
        view.BoundVehicle=vehicle;
        ui.windowManager.Open(window,true,false);
    }

    internal static bool Accepts(ItemStack source,bool crafting)
    {
        if(source==null || source.IsEmpty()) return false;
        var one=new ItemStack(source.itemValue.Clone(),1);
        if(crafting)
        {
            string portable;
            if(source.itemValue.TryGetMetadata(RebirthCamperPortableStation.MetadataKey,out portable) && source.count!=1) return false;
            BlockValue block; WorkstationData data;
            return RebirthCamperCraftingStation.TryResolve(one,out block,out data);
        }
        int capacity; return RebirthCamperStorageContainer.TryGetCapacity(one,out capacity);
    }
    private static bool Same(ItemStack left,ItemStack right)
    {
        byte[] a=RebirthCamperInventorySnapshot.Capture(new[]{left}), b=RebirthCamperInventorySnapshot.Capture(new[]{right});
        if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;
    }
    private static bool CanAccess(EntityVehicle vehicle,EntityPlayerLocal player)
        =>vehicle!=null && vehicle.world!=null && !vehicle.world.IsRemote() && player!=null && !player.IsDead()
            && ReferenceEquals(vehicle.world,player.world) && ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)
            && RebirthCamperModuleInteraction.Current(vehicle,player)
            && (vehicle.position-player.position).sqrMagnitude<=64f && vehicle.AllowActivationCommand("service",player);
}
