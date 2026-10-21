using System;

// Detached native state model. Never register this object as a world tile entity.
// Vehicle ticking and native window binding are separate integration responsibilities.
public sealed class RebirthCamperStationState : TileEntityWorkstation
{
    public Guid InstallationId { get; }
    public BlockValue InstalledBlock { get; }
    public long Revision { get; private set; }
    // RCS4 carries no earned-work authority. This bounded row-owned extension
    // is opaque here; only the productive-work proof codec may interpret it.
    internal bool ProductiveEarnedV5 { get; private set; }
    private byte[] productiveEarnedMarker = Array.Empty<byte>();
    internal byte[] CopyProductiveEarnedMarker() => (byte[])productiveEarnedMarker.Clone();
    internal void RestoreProductiveEarnedMarker(bool version5, byte[] marker)
    {
        if (!ThreadManager.IsMainThread() || marker == null || marker.Length > 512 || (!version5 && marker.Length != 0))
            throw new InvalidOperationException("Invalid earned-row native extension.");
        ProductiveEarnedV5 = version5;
        productiveEarnedMarker = (byte[])marker.Clone();
    }
    internal EntityVehicle MobileOwner { get; private set; }
    internal EntityVehicle BoundVehicle { get; private set; }
    internal void BindVehicle(EntityVehicle owner)
    {
        if (!ThreadManager.IsMainThread() || owner == null || owner.vehicle == null)
            throw new InvalidOperationException("Live station vehicle required.");
        if (BoundVehicle != null && !ReferenceEquals(BoundVehicle, owner))
            throw new InvalidOperationException("Station is already bound to another vehicle.");
        BoundVehicle = owner;
    }

    // Only the authoritative vehicle coordinator calls this, once per native tick.
    // Native UI UpdateTick remains excluded, preventing double processing.
    public void TickMobile(EntityVehicle owner)
    {
        if (!ThreadManager.IsMainThread() || owner == null || owner.world == null || owner.world.IsRemote())
            throw new InvalidOperationException("An authoritative live vehicle is required.");
        if (!ReferenceEquals(BoundVehicle,owner) || !ReferenceEquals(owner.world.GetEntity(owner.entityId),owner)
            || !ReferenceEquals(RebirthCamperModuleRuntime.For(owner).Stations.Find(InstallationId)?.State,this))
            throw new InvalidOperationException("Station is no longer installed in this live vehicle.");
#if !REBIRTH_CAMPER_DIAGNOSTICS
        if(RebirthProductiveEarnedCoordinator.HoldMobileTick(this)){lastTickTime=0;return;}
#endif
        if(RebirthCamperStationFuelOriginal.IsHeld(this)||RebirthCookingMillingPublication.Held(this)||RebirthRemoteResourceFetchedStationOriginal.BlocksStation(this)||RebirthCamperStationNativeActorPayment.IsHeld(this)){lastTickTime=0;return;}
        if (RebirthCamperStationSaveEvidence.IsPending(this)) { lastTickTime=0; return; }
#if !REBIRTH_CAMPER_DIAGNOSTICS
        if (RebirthCamperStationCompletionCallsite.IsHeld(this)) { lastTickTime=0; return; }
        // A native queue prefix alone runs after HandleFuel. Pause the whole
        // mobile clock before fuel/smelting when paid publication is unconfirmed.
        if (!RebirthCamperStationQueuedAuthority.CanProcessNativeQueue(this)) { lastTickTime=0; return; }
#endif
        if (!RebirthCamperStationHeat.IsInstalled)
            throw new InvalidOperationException("Mobile heat-location adapter is not installed.");
        ulong now = GameTimer.Instance.ticks;
        if (lastTickTime == 0 || now < lastTickTime) { lastTickTime = now; return; }
        if (now == lastTickTime) return;
#if !REBIRTH_CAMPER_DIAGNOSTICS
        using(var openQueue=RebirthCamperStationOpenQueueProcessing.Begin(this))
#endif
        {
            MobileOwner = owner;
            bool productiveReturned=false;
            try
            {
                bool fuelUsed = isModuleUsed[3];
                bool activeBefore = fuelUsed ? isBurning : hasRecipeInQueue();
                float elapsed = (now - lastTickTime) / 20f;
                float processing = fuelUsed ? UnityEngine.Mathf.Min(elapsed, BurnTotalTimeLeft) : elapsed;
                var position = new Vector3i(UnityEngine.Mathf.FloorToInt(owner.position.x),
                    UnityEngine.Mathf.FloorToInt(owner.position.y), UnityEngine.Mathf.FloorToInt(owner.position.z));
                isBesideWater = IsByWater(owner.world, position);
                isBurning = isBurning && !isBesideWater;
                if (fuelUsed) HandleFuel(owner.world, processing);
                else if (InstalledBlock.Block.HeatMapStrength > 0 && IsCrafting)
                    emitHeatMapEvent(owner.world, (EnumAIDirectorChunkEvent)0);
                HandleRecipeQueue(processing);
                HandleMaterialInput(processing);
                if (fuelUsed) isBurning = isBurning && BurnTotalTimeLeft > 0;
                lastTickTime = now;
                if (activeBefore || (fuelUsed ? isBurning : hasRecipeInQueue())) setModified();
                productiveReturned=true;
            }
            finally
            {
#if !REBIRTH_CAMPER_DIAGNOSTICS
                RebirthProductiveEarnedCoordinator.ObserveMobileReturned(this,productiveReturned);
#endif
                MobileOwner = null;
            }
        }
    }

    public RebirthCamperStationState(Guid installationId, ItemStack installedItem) : base(null)
    {
        if (installationId == Guid.Empty) throw new ArgumentException("Station identity is required.");
        BlockValue block;
        WorkstationData configuration;
        if (!RebirthCamperCraftingStation.TryResolve(installedItem, out block, out configuration))
            throw new ArgumentException("A single configured crafting station item is required.");
        InstallationId = installationId;
        InstalledBlock = block;
        InitializeConfiguration();
    }

    private void InitializeConfiguration()
    {
        var properties = InstalledBlock.Block.Properties;
        string materials = properties.GetString("Workstation", "InputMaterials");
        materialNames = string.IsNullOrEmpty(materials) ? new string[0] : materials.Replace(" ", "").Split(',');
        input = ItemStack.CreateArray(3 + materialNames.Length);
        for (int index = 0; index < materialNames.Length; index++)
        {
            ItemClass unit = ItemClass.GetItemClass("unit_" + materialNames[index]);
            if (unit == null) throw new InvalidOperationException("Unresolved station input material: " + materialNames[index]);
            input[3 + index] = new ItemStack(new ItemValue(unit.Id, false), 0);
        }
        Array.Clear(isModuleUsed, 0, isModuleUsed.Length);
        string modules = properties.GetString("Workstation", "Modules");
        if (!string.IsNullOrEmpty(modules))
            foreach (string name in modules.Replace(" ", "").Split(','))
            {
                Module module = (Module)Enum.Parse(typeof(Module), name, true);
                int index = (int)module;
                if (index < 0 || index >= isModuleUsed.Length) throw new InvalidOperationException("Invalid station module: " + name);
                isModuleUsed[index] = true;
            }
        if (isModuleUsed[(int)Module.Material_Input]) isModuleUsed[(int)Module.Input] = true;
        EnsureWorkbenchPresentationCapacity();
    }

    // Match the existing workbench workspace before its first open. Native
    // constructor defaults (6 output/4 queue) otherwise change just by visiting it.
    // Restore reads the original saved shape back over these defaults; do not
    // normalize retained journal/recovery payloads in the pure restore primitive.
    private void EnsureWorkbenchPresentationCapacity()
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||InstalledBlock.Block.GetBlockName()!="workbench")return;
        if(output!=null&&output.Length<28)
        {
            var expanded=ItemStack.CreateArray(28);
            Array.Copy(output,expanded,output.Length);
            output=expanded;
        }
        if(queue!=null&&queue.Length<16)
        {
            var expanded=new RecipeQueueItem[16];
            for(int i=0;i<expanded.Length;i++)expanded[i]=new RecipeQueueItem();
            // Native queue executes from the last slot; keep active/order at tail,
            // exactly as XUiC_RebirthStationWorkspace.ExpandQueue does on open.
            Array.Copy(queue,0,expanded,expanded.Length-queue.Length,queue.Length);
            queue=expanded;
        }
    }

    public override void OnSetLocalChunkPosition()
    {
        // Native read invokes this hook. Configuration already belongs to the installed item;
        // never reinitialize inventories or resolve a block from an unrelated world position.
    }

    public override void setModified()
    {
        Revision = checked(Revision + 1);
        // Native world-position packets/chunk writes are intentionally excluded here.
        // The vehicle coordinator must persist and synchronize this revision.
    }

    internal void RestoreRevision(long revision)
    {
        if (!ThreadManager.IsMainThread() || revision < 0)
            throw new InvalidOperationException("Restore a valid station revision on the game thread.");
        Revision = revision;
    }

    public override void UpdateTick(World world)
    {
        if (BoundVehicle != null && ReferenceEquals(BoundVehicle.world, world)) return;
        throw new InvalidOperationException("Detached station state must use the vehicle crafting coordinator, not world tile-entity ticking.");
    }
}
