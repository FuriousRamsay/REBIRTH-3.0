using System;
using System.Runtime.CompilerServices;

// One authoritative module owner per native vehicle. Native ItemValue carries
// its snapshots through the existing vehicle save/pickup/sync channel.
public sealed class RebirthCamperModuleRuntime
{
    private const string IdentityKey = "RebirthCamperIdentity";
    private static readonly ConditionalWeakTable<EntityVehicle, RebirthCamperModuleRuntime> owners
        = new ConditionalWeakTable<EntityVehicle, RebirthCamperModuleRuntime>();
    public Guid Identity { get; private set; }
    public RebirthCamperStorageInstallations Storage { get; internal set; }
    public RebirthCamperStationInstallations Stations { get; internal set; }
    private readonly EntityVehicle vehicle;
    private long persistedStorage = -1, persistedStations = -1;
    private long[] persistedState = new long[RebirthCamperCraftingStation.ModuleSlots];
    public bool TransferPending { get; private set; }
    private ulong lastProcessingTick;
    private bool energyClockHeld;

    private RebirthCamperModuleRuntime(EntityVehicle vehicle)
    {
        this.vehicle = vehicle;
        ItemValue item = vehicle.vehicle.GetUpdatedItemValue();
        string encoded;
        if (item.TryGetMetadata(IdentityKey, out encoded))
        {
            Guid id;
            if (!Guid.TryParse(encoded, out id) || id == Guid.Empty)
                throw new InvalidOperationException("Invalid camper identity; retain vehicle data.");
            Identity = id;
        }
        else
        {
            string existing;
            if (item.TryGetMetadata("RebirthCamperModules", out existing))
                throw new InvalidOperationException("Camper modules have no matching identity; retain vehicle data.");
            Identity = Guid.NewGuid();
        }
        RebirthCamperStorageInstallations storage;
        RebirthCamperStationInstallations stations;
        if (!RebirthCamperModuleItemCandidate.TryRestore(Identity, item, out storage, out stations))
        {
            storage = new RebirthCamperStorageInstallations(Identity);
            stations = new RebirthCamperStationInstallations(Identity);
        }
        Storage = storage;
        Stations = stations;
        BindStations();
        string saveDirectory=GameIO.GetSaveGameDir();
        if(!string.IsNullOrEmpty(saveDirectory))
        {
            string receipts=System.IO.Path.Combine(saveDirectory,"REBIRTH","CamperTransfers");
            if(System.IO.Directory.Exists(receipts))
                try{new RebirthCamperTransferJournal(receipts).AssertAvailable(Identity,Guid.NewGuid());}
                catch(Exception error){TransferPending=true;Log.Warning("[CamperTransfer] Processing paused for recovery: "+error.Message);}
        }
        // Do not resize cargo here: existing legacy cargo must be reconciled by
        // the storage transaction, never discarded during vehicle loading.
        // Receipts determine pending custody; metadata from an older successful
        // publication can still carry "1" even though no receipt is outstanding.
        item.SetMetadata("RebirthCamperTransferPending", TransferPending ? "1" : "0");
    }

    public static RebirthCamperModuleRuntime For(EntityVehicle vehicle)
    {
        if (!ThreadManager.IsMainThread() || vehicle == null || vehicle.world == null || vehicle.world.IsRemote()
            || vehicle.vehicle == null || !vehicle.vehicle.GetName().Equals("rebirthCamper80s", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Authoritative camper required.");
        return owners.GetValue(vehicle, key => new RebirthCamperModuleRuntime(key));
    }

    // Native loading reads the saved ItemValue after entering ReadSyncData.
    // A guard must not create an owner from the pre-load default item.
    internal static bool TryGetExisting(EntityVehicle vehicle,out RebirthCamperModuleRuntime runtime)
    {
        runtime=null;
        return vehicle!=null && owners.TryGetValue(vehicle,out runtime);
    }

    public void Persist(bool force = false, ushort syncFlags = 4)
    {
        BindStations();
        if (!ThreadManager.IsMainThread() || vehicle.world.IsRemote()) throw new InvalidOperationException("Server game thread required.");
        bool changed = force || persistedStorage != Storage.Revision || persistedStations != Stations.Revision;
        for (int slot = 0; slot < persistedState.Length; slot++)
            if (persistedState[slot] != (Stations.GetSlot(slot)?.State.Revision ?? -1)) changed = true;
        if (!changed) return;
        ItemValue source = vehicle.vehicle.GetUpdatedItemValue().Clone();
        source.SetMetadata(IdentityKey, Identity.ToString("N"));
        ItemValue candidate = RebirthCamperModuleItemCandidate.Prepare(source, Storage, Stations);
        // SetItemValue recalculates health/fuel/mods/colors/seats; a routine module
        // save must not perform that full vehicle reinitialization.
        string modules;
        if (!candidate.TryGetMetadata("RebirthCamperModules", out modules))
            throw new InvalidOperationException("Prepared module snapshot is missing.");
        ItemValue live = vehicle.vehicle.GetUpdatedItemValue();
        live.SetMetadata(IdentityKey, Identity.ToString("N"));
        live.SetMetadata("RebirthCamperModules", modules);
        live.SetMetadata("RebirthCamperTransferPending", TransferPending ? "1" : "0");
        vehicle.SendSyncData(syncFlags);
        persistedStorage = Storage.Revision;
        persistedStations = Stations.Revision;
        for (int slot = 0; slot < persistedState.Length; slot++) persistedState[slot] = Stations.GetSlot(slot)?.State.Revision ?? -1;
    }

    public void InitializeEmptyCargo()
    {
        if(!ThreadManager.IsMainThread() || vehicle.world.IsRemote() || vehicle.bag==null)
            throw new InvalidOperationException("Authoritative native cargo required.");
        var cargo=vehicle.bag.ItemGrid.items;
        if(cargo==null)throw new InvalidOperationException("Native cargo is unavailable.");
        if(cargo.Length==Storage.Capacity)return;
        string modules;
        if(vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperModules",out modules))
        {
            // Native reload/sync can restore the old bag dimensions separately
            // from the module item. Preserve every occupied position; only pad
            // missing cells or remove a proven-empty tail.
            var reconciled=ItemStack.CreateArray(Storage.Capacity);
            for(int slot=0;slot<cargo.Length;slot++)
            {
                if(cargo[slot]==null)throw new InvalidOperationException("Saved cargo contains an invalid item.");
                if(slot>=reconciled.Length)
                {
                    if(!cargo[slot].IsEmpty())throw new InvalidOperationException("Saved cargo exceeds installed capacity; retain overflow for recovery.");
                }
                else reconciled[slot]=cargo[slot].Clone();
            }
            RebirthCamperTransferCommit.AssertCargoPolicy(vehicle.bag.ItemGrid,cargo,reconciled);
            vehicle.bag.SetSlots(reconciled,true);
            Persist(true);vehicle.SetBagModified();
            return;
        }
        if(Storage.Capacity!=0 || Storage.Revision!=0 || Stations.Revision!=0)
            throw new InvalidOperationException("Only an unmodified legacy cargo bag can be initialized.");
        foreach(var stack in cargo)
            if(stack==null || !stack.IsEmpty())
                throw new InvalidOperationException("Empty existing vehicle storage before enabling container-based capacity.");
        RebirthCamperTransferCommit.AssertCargoPolicy(vehicle.bag.ItemGrid,cargo,ItemStack.CreateArray(0));
        // No item custody changes: the native legacy bag contains no cargo.
        // SetSlots changes dimensions atomically and preserves listener behavior.
        vehicle.bag.SetSlots(ItemStack.CreateArray(0),true);
        Persist(true);
        vehicle.SetBagModified();
    }

    internal void SetTransferPending(bool pending)
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Game thread required.");
        if(TransferPending && !pending)
            for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
                if(Stations.GetSlot(slot)!=null)Stations.GetSlot(slot).State.lastTickTime=0;
        TransferPending=pending;
        ItemValue live=vehicle.vehicle.GetUpdatedItemValue();
        string wanted=pending?"1":"0",previous;
        if(!live.TryGetMetadata("RebirthCamperTransferPending",out previous)||previous!=wanted)
        {
            live.SetMetadata("RebirthCamperTransferPending",wanted);
            vehicle.SendSyncData(4);
        }
    }

    internal void TickStations()
    {
        // A brief exact energy receipt/lease save hold stabilizes native station
        // metadata for the shared native vehicle save witness. Energy mode alone
        // never pauses crafting and this does not alter any other custody hold.
        if(RebirthCamperEnergyRuntime.IsHeld(vehicle)){energyClockHeld=true;return;}
        if(energyClockHeld)
        {
            for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
                if(Stations.GetSlot(slot)!=null)Stations.GetSlot(slot).State.lastTickTime=0;
            lastProcessingTick=0;energyClockHeld=false;
        }
        if(TransferPending)return;
        ulong now=GameTimer.Instance.ticks;
        if(now>=lastProcessingTick && now-lastProcessingTick<5)return;
        lastProcessingTick=now;
        BindStations();
        for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
            Stations.GetSlot(slot)?.State.TickMobile(vehicle);
        Persist();
    }

    internal void BindStations()
    {
        for (int slot = 0; slot < RebirthCamperCraftingStation.ModuleSlots; slot++)
            Stations.GetSlot(slot)?.State.BindVehicle(vehicle);
    }
}
