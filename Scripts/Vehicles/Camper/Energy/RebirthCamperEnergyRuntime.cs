using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

public sealed class RebirthCamperEnergyRuntime
{
    private static readonly ConditionalWeakTable<EntityVehicle,RebirthCamperEnergyRuntime> runtimes=new ConditionalWeakTable<EntityVehicle,RebirthCamperEnergyRuntime>();
    public static Func<EntityVehicle,IEnumerable<IRebirthCamperEnergyConsumer>> Consumers {get;set;}
    // Installed by the energy custody coordinator only after journal recovery.
    // Absence is a refusal, never permission to spend a saved source.
    public static Func<EntityVehicle,bool> CustodyReady {get;set;}
    public static Action<EntityVehicle> SuspendConsumers {get;set;}
    public static bool IsHeld(EntityVehicle vehicle)
        =>ThreadManager.IsMainThread()&&vehicle!=null&&runtimes.TryGetValue(vehicle,out var runtime)&&runtime.holds.Count!=0;
    public static bool IsRegistered {get;private set;}
    public static void SetRegistered(bool registered)
    {if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Energy registration requires game thread.");IsRegistered=registered;}
    private readonly EntityVehicle vehicle;
    private RebirthCamperEnergyState state;
    private readonly HashSet<Guid> holds=new HashSet<Guid>();
    private ulong lastTick;
    private int demand,supplied;
    private double energyDemand;
    private readonly Dictionary<Guid,long> solarBudget=new Dictionary<Guid,long>();
    private readonly RebirthCamperBatteryCharging charging=new RebirthCamperBatteryCharging();
    // Exact current native PowerManager.Update source cadence, not a watt-hour conversion.
    internal const double NativePowerPeriodSeconds=0.16;
    public static void Invalidate(EntityVehicle vehicle)
    {if(ThreadManager.IsMainThread()&&vehicle!=null)runtimes.Remove(vehicle);}
    internal static void PauseClock(EntityVehicle vehicle)
    {if(ThreadManager.IsMainThread()&&vehicle!=null&&runtimes.TryGetValue(vehicle,out var runtime)){runtime.lastTick=0;runtime.charging.Reset();runtime.solarBudget.Clear();}}
    public Guid Identity=>state.Identity;
    private RebirthCamperEnergyRuntime(EntityVehicle owner)
    {
        vehicle=owner;Guid identity=RebirthCamperModuleRuntime.For(owner).Identity;
        string frame;
        state=owner.vehicle.GetUpdatedItemValue().TryGetMetadata(RebirthCamperEnergyState.Key,out frame)
            ?RebirthCamperEnergyState.DecodeBase64(frame,identity):new RebirthCamperEnergyState {Identity=identity};
    }
    public static RebirthCamperEnergyRuntime For(EntityVehicle vehicle)
    {
        if(!Current(vehicle)||vehicle.world.IsRemote())throw new InvalidOperationException("Authoritative current camper required.");
        return runtimes.GetValue(vehicle,key=>new RebirthCamperEnergyRuntime(key));
    }
    private static bool Current(EntityVehicle vehicle)=>ThreadManager.IsMainThread()&&vehicle!=null&&vehicle.world!=null
        &&RebirthCamperRadial.IsCamper(vehicle)&&ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle);
    public static bool TryObserveMode(EntityVehicle vehicle,out RebirthCamperEnergyMode mode,out long revision,out float range)
    { return TryObservePresentation(vehicle,out mode,out revision,out range,out _); }
    // Lightweight presentation observation: does not clone bank inventories.
    public static bool TryObservePresentation(EntityVehicle vehicle,out RebirthCamperEnergyMode mode,out long revision,out float range,out float remaining)
    {
        mode=RebirthCamperEnergyMode.Travel;revision=-1;range=RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle);remaining=0;
        if(!IsRegistered||!Current(vehicle))return false;
        if(!vehicle.world.IsRemote()&&runtimes.TryGetValue(vehicle,out var runtime))
        {mode=runtime.state.Mode;revision=runtime.state.Revision;remaining=runtime.state.Remaining;return true;}
        string identity,frame;var item=vehicle.vehicle.GetUpdatedItemValue();
        if(!item.TryGetMetadata("RebirthCamperIdentity",out identity)||!Guid.TryParse(identity,out var id)
            ||!item.TryGetMetadata(RebirthCamperEnergyState.Key,out frame))return false;
        try{var state=RebirthCamperEnergyState.DecodeBase64(frame,id);mode=state.Mode;revision=state.Revision;remaining=state.Remaining;return true;}
        catch(FormatException){return false;}catch(System.IO.InvalidDataException){return false;}catch(ArgumentException){return false;}
    }
    public static RebirthCamperEnergySnapshot Observe(EntityVehicle vehicle)
    {
        if(!IsRegistered||!Current(vehicle))return null;
        if(!vehicle.world.IsRemote()&&runtimes.TryGetValue(vehicle,out var runtime))return runtime.Snapshot();
        string identity,frame;var item=vehicle.vehicle.GetUpdatedItemValue();
        if(!item.TryGetMetadata("RebirthCamperIdentity",out identity)||!Guid.TryParse(identity,out var id))return null;
        if(!item.TryGetMetadata(RebirthCamperEnergyState.Key,out frame))return null;
        try{return MakeSnapshot(RebirthCamperEnergyState.DecodeBase64(frame,id),0,0,RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle));}
        catch(FormatException){return null;}
        catch(System.IO.InvalidDataException){return null;}
        catch(ArgumentException){return null;}
    }
    public RebirthCamperEnergySnapshot Snapshot()=>MakeSnapshot(state,demand,supplied,RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle),holds.Count==0&&!RebirthCamperModuleRuntime.For(vehicle).TransferPending,energyDemand);
    private static RebirthCamperEnergySnapshot MakeSnapshot(RebirthCamperEnergyState state,int demand,int supplied,float range,bool estimateAuthoritative=false,double energyDemand=0)
    {
        var sources=new List<RebirthCamperEnergySourceSnapshot>();
        foreach(var source in state.Sources)
        {
            var bank=source.Bank;
            bool hasComponents=bank.Stacks.Any(item=>!item.IsEmpty());
            var status=!bank.isOn?RebirthCamperEnergySourceStatus.Disabled:state.Mode==RebirthCamperEnergyMode.Travel?RebirthCamperEnergySourceStatus.Travel:
                state.Mode!=RebirthCamperEnergyMode.Camp?RebirthCamperEnergySourceStatus.Transition:
                !hasComponents?RebirthCamperEnergySourceStatus.Empty:bank is PowerSolarPanel solar&&!solar.HasLight?RebirthCamperEnergySourceStatus.Obstructed:
                bank is PowerGenerator generator&&generator.CurrentFuel==0&&bank.CurrentPower==0?RebirthCamperEnergySourceStatus.Empty:
                bank is PowerBatteryBank&&bank.CurrentPower==0&&!bank.Stacks.Any(item=>!item.IsEmpty()&&item.itemValue.MaxUseTimes-item.itemValue.UseTimes>=1f)
                    ?RebirthCamperEnergySourceStatus.Empty:RebirthCamperEnergySourceStatus.Available;
            sources.Add(new RebirthCamperEnergySourceSnapshot {Id=source.Id,Position=source.Position,Kind=RebirthCamperEnergyState.Kind(bank),installed=source.Installed.Clone(),components=ItemStack.Clone(bank.Stacks),
                CurrentFuel=bank is PowerGenerator g?g.CurrentFuel:(ushort)0,MaxFuel=bank is PowerGenerator g2?g2.MaxFuel:(ushort)0,
                CurrentPower=bank.CurrentPower,MaxOutput=bank.MaxOutput,IsOn=bank.isOn,Status=status,
                AvailableWatts=status==RebirthCamperEnergySourceStatus.Available?bank.CurrentPower:(ushort)0});
        }
        var snapshot=new RebirthCamperEnergySnapshot {Mode=state.Mode,TransitionSeconds=state.Remaining,Revision=state.Revision,
            Range=range,DemandWatts=demand,SuppliedWatts=supplied,
            AvailableWatts=sources.Sum(source=>(int)source.AvailableWatts),Sources=new ReadOnlyCollection<RebirthCamperEnergySourceSnapshot>(sources)};
        RebirthCamperEnergyRuntimeEstimate.Apply(state,snapshot,estimateAuthoritative,energyDemand);return snapshot;
    }
    public static bool TryCreateBank(ItemStack installed,out PowerSource bank,out string reason)
    {bool valid=RebirthCamperEnergyState.TryCreateBank(installed,out bank);reason=valid?null:"Unsupported native power-source item.";return valid;}
    public bool TryCopyBank(Guid sourceId,out PowerSource bank)
    {
        bank=null;var copy=RebirthCamperEnergyState.Decode(RebirthCamperEnergyState.Encode(state),Identity);
        var source=copy.Sources.Find(entry=>entry.Id==sourceId);if(source==null)return false;bank=source.Bank;return true;
    }
    public bool Hold(Guid operation)
    {if(!Current(vehicle)||vehicle.world.IsRemote()||operation==Guid.Empty)return false;
        if(holds.Count!=0)return holds.Contains(operation);
        if(RebirthCamperStationFuelOriginal.BlocksVehicle(vehicle))return false;
        bool added=holds.Add(operation);if(added)SuspendConsumers?.Invoke(vehicle);return added;}
    public void Release(Guid operation){if(Current(vehicle)&&!vehicle.world.IsRemote())holds.Remove(operation);}
    // The adapter persists a bounded restoration lease before changing native
    // world block/light state. Hold its operation until Verify succeeds; no writer
    // other than native VehicleManager is started here.
    public bool RequestDeviceLeasePersistence(Guid operation,byte[] expectedBefore,byte[] after,out RebirthCamperSavedVehicle.Expected saved)
    {
        saved=null;if(!Current(vehicle)||vehicle.world.IsRemote()||operation==Guid.Empty||!holds.Contains(operation)
            ||after==null||after.Length>RebirthCamperEnergyDeviceLeaseStorage.MaximumBytes)return false;
        var live=vehicle.vehicle.GetUpdatedItemValue();
        byte[] before=RebirthCamperEnergyDeviceLeaseStorage.Read(live);
        // A failed native save scheduling call can leave the intended metadata
        // already applied. Retry the same held operation without replaying its
        // mutation; unrelated metadata images still fail the comparison.
        bool alreadyApplied=Same(before,after);
        if(!alreadyApplied&&!Same(before,expectedBefore??new byte[0]))return false;
        if(!alreadyApplied)RebirthCamperEnergyDeviceLeaseStorage.Write(live,after);
        vehicle.SendSyncData(4);
        saved=RebirthCamperSavedVehicle.RequestSave(vehicle);return true;
    }
    public static byte[] ReadDeviceLease(EntityVehicle vehicle)
    {
        if(!Current(vehicle))return null;
        return RebirthCamperEnergyDeviceLeaseStorage.Read(vehicle.vehicle.GetUpdatedItemValue());
    }

    public sealed class Change
    {
        public Guid Identity {get;internal set;}
        public long ExpectedRevision {get;internal set;}
        internal byte[] before,after;
        internal ItemStack removed;
        public byte[] Before=> (byte[])before.Clone();
        public byte[] After=> (byte[])after.Clone();
        public ItemStack Removed=>removed?.Clone();
    }
    private RebirthCamperEnergyState Copy()=>RebirthCamperEnergyState.Decode(RebirthCamperEnergyState.Encode(state),Identity);
    private bool CanPrepare(long expected,out string reason)
    {
        reason=null;
        if(!Current(vehicle)||vehicle.world.IsRemote())reason="Energy source world session changed.";
        else if(expected!=state.Revision)reason="Energy source revision changed.";
        else if(holds.Count!=0)reason="Energy source native save pending: "+string.Join(",",holds);
        else if(RebirthCamperStationFuelOriginal.BlocksVehicle(vehicle))reason="Station fuel recovery is pending.";
        else if(RebirthCamperModuleRuntime.For(vehicle).TransferPending)reason="Camper module transfer recovery is pending.";
        return reason==null;
    }
    private Change Prepare(RebirthCamperEnergyState next,ItemStack removed=null)
    {next.Version=2;next.Revision=checked(state.Revision+1);return new Change {Identity=Identity,ExpectedRevision=state.Revision,before=RebirthCamperEnergyState.Encode(state),after=RebirthCamperEnergyState.Encode(next),removed=removed?.Clone()};}
    public bool TryPrepareInstall(long expectedRevision,ItemStack nativeBankItem,PowerSource bank,out Change change,out string reason,int position=-1,int eligibleCapacity=RebirthCamperEnergyCapacity.Normal)
    {
        change=null;if(!CanPrepare(expectedRevision,out reason))return false;
        if(eligibleCapacity!=RebirthCamperEnergyCapacity.Normal&&eligibleCapacity!=RebirthCamperEnergyCapacity.Electrician){reason="Authoritative source capacity required.";return false;}
        if(position<0)position=Enumerable.Range(0,eligibleCapacity).FirstOrDefault(index=>!state.Sources.Any(entry=>entry.Position==index));
        if(state.Sources.Count>=eligibleCapacity||position>=eligibleCapacity||position<0||state.Sources.Any(entry=>entry.Position==position))
        {reason="Eligible source position is full or unavailable.";return false;}
        if(!TryCreateBank(nativeBankItem,out var empty,out reason))return false;
        var next=Copy();
        string portable;
        if(nativeBankItem.itemValue.TryGetMetadata(RebirthCamperEnergyState.PortableKey,out portable))
        {
            if(portable.Length>((RebirthCamperEnergyState.Limit+2)/3)*4){reason="Portable source frame too large.";return false;}
            var packed=Convert.FromBase64String(portable);
            if(packed.Length<20){reason="Portable source frame invalid.";return false;}
            var sourceIdentity=new Guid(packed.Skip(4).Take(16).ToArray());
            var restored=RebirthCamperEnergyState.DecodeBase64(portable,sourceIdentity);
            if(restored.Sources.Count!=1){reason="Portable source state invalid.";return false;}
            var source=restored.Sources[0];
            if(source.Installed.itemValue.type!=nativeBankItem.itemValue.type){reason="Portable source item mismatch.";return false;}
            var original=nativeBankItem.Clone();original.itemValue.Metadata.Remove(RebirthCamperEnergyState.PortableKey);
            if(RebirthNativeItemCodec.Encode(original.itemValue)!=RebirthNativeItemCodec.Encode(source.Installed.itemValue))
            {reason="Portable source item identity changed.";return false;}
            source.Installed=original;source.Position=position;
            if(next.Sources.Any(entry=>entry.Id==source.Id)){reason="Source already installed.";return false;}next.Sources.Add(source);
        }
        else
        {
            // Initial source is empty. Arbitrary supplied bank snapshots are not authority.
            if(bank!=null&&(bank.CurrentPower!=0||bank.Stacks.Any(item=>!item.IsEmpty())||bank is PowerGenerator g&&g.CurrentFuel!=0))
            {reason="Initial installed source must be an exact empty native bank.";return false;}
            next.Sources.Add(new RebirthCamperEnergyState.Source {Id=Guid.NewGuid(),Position=position,Installed=nativeBankItem.Clone(),Bank=empty});
        }
        change=Prepare(next);return true;
    }
    public bool TryPrepareRemove(long expectedRevision,Guid sourceId,out Change change,out string reason)
    {
        change=null;if(!CanPrepare(expectedRevision,out reason))return false;var next=Copy();var source=next.Sources.Find(entry=>entry.Id==sourceId);
        if(source==null){reason="Source installation changed.";return false;}
        var portable=new RebirthCamperEnergyState {Identity=Identity};portable.Sources.Add(source);
        var removed=source.Installed.Clone();removed.itemValue.SetMetadata(RebirthCamperEnergyState.PortableKey,Convert.ToBase64String(RebirthCamperEnergyState.Encode(portable)));
        next.Sources.Remove(source);change=Prepare(next,removed);return true;
    }
    public bool TryGetSourcePosition(Guid sourceId,out int position)
    {var source=state.Sources.Find(entry=>entry.Id==sourceId);position=source?.Position??-1;return source!=null;}
    public bool TryPrepareContents(long expectedRevision,Guid sourceId,ItemStack[] components,ushort fuel,bool enabled,out Change change,out string reason)
    {
        change=null;if(!CanPrepare(expectedRevision,out reason))return false;var next=Copy();var source=next.Sources.Find(entry=>entry.Id==sourceId);
        if(source==null||!RebirthCamperEnergyState.ValidComponents(source.Bank,components)){reason="Exact native component slots required.";return false;}
        if(source.Bank is PowerGenerator generator){if(fuel>generator.MaxFuel){reason="Generator capacity exceeded.";return false;}
            if(fuel>generator.CurrentFuel)generator.CurrentPower=generator.MaxPower;
            generator.CurrentFuel=fuel;}
        else if(fuel!=0){reason="Fuel is supported by generators only.";return false;}
        source.Bank.SetSlots(ItemStack.Clone(components));
        // Native HandleSendPower clamps storage after a derived-capacity change.
        source.Bank.CurrentPower=Math.Min(source.Bank.CurrentPower,source.Bank.MaxPower);
        source.Bank.isOn=enabled;change=Prepare(next);return true;
    }
    public bool AlreadyMatchesAfter(Change change)=>change!=null&&change.Identity==Identity&&Same(RebirthCamperEnergyState.Encode(state),change.after);
    public bool Commit(Change change)
    {
        if(!Current(vehicle)||vehicle.world.IsRemote()||change==null||change.Identity!=Identity||state.Revision!=change.ExpectedRevision||RebirthCamperStationFuelOriginal.BlocksVehicle(vehicle)
            ||!Same(RebirthCamperEnergyState.Encode(state),change.before))return false;
        var next=RebirthCamperEnergyState.Decode(change.after,Identity);if(next.Revision!=checked(state.Revision+1))return false;
        state=next;Persist();return true;
    }
    public static Change RestoreChange(Guid identity,byte[] before,byte[] after,ItemStack removed)
    {
        var a=RebirthCamperEnergyState.Decode(before,identity);var b=RebirthCamperEnergyState.Decode(after,identity);
        if(b.Revision!=checked(a.Revision+1))throw new ArgumentException("Directional energy change required.");
        return new Change {Identity=identity,ExpectedRevision=a.Revision,before=(byte[])before.Clone(),after=(byte[])after.Clone(),removed=removed?.Clone()};
    }
    private static bool Same(byte[] a,byte[] b)=>a!=null&&b!=null&&a.SequenceEqual(b);
    private void Persist()
    {
        var live=vehicle.vehicle.GetUpdatedItemValue();live.SetMetadata("RebirthCamperIdentity",Identity.ToString("N"));
        live.SetMetadata(RebirthCamperEnergyState.Key,Convert.ToBase64String(RebirthCamperEnergyState.Encode(state)));vehicle.SendSyncData(4);
        VehicleManager.Instance?.TriggerSave();
    }
    public static bool RequestMode(EntityVehicle vehicle,EntityPlayer player,RebirthCamperEnergyMode target,out string reason)
    {
        if(target!=RebirthCamperEnergyMode.Travel&&!RebirthCamperBodyAttachment.HasBody(vehicle)){reason="Install the camper body before deploying camp.";return false;}
        reason=null;
        if(!IsRegistered||!Current(vehicle)||vehicle.world.IsRemote()||player==null||player.IsDead()||!ReferenceEquals(player.world,vehicle.world)
            ||!ReferenceEquals(vehicle.world.GetEntity(player.entityId),player)||(player.position-vehicle.position).sqrMagnitude>64f)
        {reason="Current nearby authoritative actor required.";return false;}
        var owner=vehicle.GetOwner();var data=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if(data==null||(vehicle.IsLocked()&&vehicle.hasLock()
            &&!(owner!=null&&(owner.Equals(data.PrimaryId)||owner.Equals(data.NativeId)))
            &&!(data.PrimaryId!=null&&vehicle.isAllowedUser(data.PrimaryId))
            &&!(data.NativeId!=null&&vehicle.isAllowedUser(data.NativeId))))
        {reason="Vehicle access required.";return false;}
        var runtime=For(vehicle);
        if(CustodyReady?.Invoke(vehicle)!=true||runtime.holds.Count!=0||RebirthCamperStationFuelOriginal.BlocksVehicle(vehicle)||RebirthCamperModuleRuntime.For(vehicle).TransferPending)
        {reason="Source custody recovery is pending.";return false;}
        if(target==RebirthCamperEnergyMode.Camp&&runtime.state.Mode==RebirthCamperEnergyMode.Travel)
        {
            if(vehicle.GetAttached(0)!=null||vehicle.vehicleRB==null||vehicle.vehicleRB.velocity.sqrMagnitude>0.01f||vehicle.vehicleRB.angularVelocity.sqrMagnitude>0.0049f)
            {reason="Park and leave the driver seat before deploying.";return false;}
            runtime.state.Mode=RebirthCamperEnergyMode.Deploying;runtime.state.Remaining=RebirthCamperEnergyConfiguration.Current.DeploySeconds;
        }
        else if(target==RebirthCamperEnergyMode.Travel&&runtime.state.Mode==RebirthCamperEnergyMode.Camp)
        {runtime.state.Mode=RebirthCamperEnergyMode.Packing;runtime.state.Remaining=RebirthCamperEnergyConfiguration.Current.PackSeconds;}
        else {reason="Current camp transition does not admit this request.";return false;}
        // Accepted transitions start now, not at the preceding energy tick.
        runtime.lastTick=GameTimer.Instance.ticks;
        RebirthCamperEnergyImmobility.Apply(vehicle,runtime.state.Mode);
        runtime.state.Revision=checked(runtime.state.Revision+1);runtime.Persist();return true;
    }
    public void Tick()
    {
        if(!IsRegistered||!Current(vehicle)||vehicle.world.IsRemote())return;
        if(!RebirthCamperEnergyImmobility.Apply(vehicle,state.Mode)){charging.Reset();lastTick=0;return;}
        ulong now=GameTimer.Instance.ticks;
        // A native vehicle save includes station and energy metadata together.
        // Do not change another portion of that same record while it is being
        // confirmed; this is an in-memory check and starts no extra saves.
        if(RebirthCamperStationSaveEvidence.IsVehiclePending(vehicle)){charging.Reset();lastTick=now;return;}
        // Reset the interval while recovery refuses, preventing a later catch-up
        // debit or accelerated transition across an unresolved custody window.
        if(RebirthCamperStationFuelOriginal.BlocksVehicle(vehicle)||CustodyReady?.Invoke(vehicle)!=true){charging.Reset();lastTick=now;demand=0;supplied=0;energyDemand=0;SuspendConsumers?.Invoke(vehicle);return;}
        if(lastTick==0||now<lastTick)
        {charging.Reset();string frame;if(!vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata(RebirthCamperEnergyState.Key,out frame))Persist();lastTick=now;return;}
        float elapsed=(now-lastTick)/20f;
        if(elapsed<RebirthCamperEnergyConfiguration.Current.NativeTickSeconds)return;
        lastTick=now;
        var consumers=(Consumers?.Invoke(vehicle)??Enumerable.Empty<IRebirthCamperEnergyConsumer>()).Concat(RebirthCamperRelayWiring.MountedConsumers(vehicle)).Where(c=>c!=null).ToArray();
        if(holds.Count!=0||RebirthCamperModuleRuntime.For(vehicle).TransferPending)
        {charging.Reset();foreach(var c in consumers)c.SetSupply(vehicle,0);return;}
        byte[] before=RebirthCamperEnergyState.Encode(state);
        var modeBefore=state.Mode;
        if(state.Mode==RebirthCamperEnergyMode.Deploying||state.Mode==RebirthCamperEnergyMode.Packing)
        {
            state.Remaining=Math.Max(0,state.Remaining-elapsed);
            if(state.Remaining==0)state.Mode=state.Mode==RebirthCamperEnergyMode.Deploying?RebirthCamperEnergyMode.Camp:RebirthCamperEnergyMode.Travel;
            RebirthCamperEnergyImmobility.Apply(vehicle,state.Mode);
        }
        demand=0;supplied=0;energyDemand=0;
        // A newly completed deployment has not powered devices during the prior
        // waiting interval. Begin its accounting on the next actual Camp tick.
        double quanta=state.Mode==RebirthCamperEnergyMode.Camp&&modeBefore==RebirthCamperEnergyMode.Camp?elapsed/NativePowerPeriodSeconds:0;
        solarBudget.Clear();
        // Daylight generation also charges installed batteries while parked or
        // travelling; an active external consumer is not required.
        RefreshSolar(elapsed/NativePowerPeriodSeconds);
        float activeRange=RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle);
        var consumerIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var c in consumers)
        {
            if(string.IsNullOrEmpty(c.Id)||!consumerIds.Add(c.Id)||c.DemandWatts<0||float.IsNaN(c.Distance)||float.IsInfinity(c.Distance)||c.Distance<0)
                throw new InvalidOperationException("Unique actual energy consumer/demand required.");
            double cost=AccountingDemand(c);
            if(double.IsNaN(cost)||double.IsInfinity(cost)||cost<0||c.DemandWatts==0&&cost!=0||c.DemandWatts>0&&cost<=0)
                throw new InvalidOperationException("Finite positive active energy accounting demand required.");
            if(RebirthCamperEnergyBounds.ContainsDistance(c.Distance,activeRange))
            {demand=checked(demand+c.DemandWatts);energyDemand+=cost;}
            if(double.IsInfinity(energyDemand))throw new InvalidOperationException("Finite total energy accounting demand required.");
        }
        // No MaxOutput/device-count admission cap. Stored native energy/fuel alone limits runtime supply.
        foreach(var c in consumers.OrderBy(c=>c.Priority).ThenBy(c=>c.Id,StringComparer.Ordinal))
        {
            int power=0;
            if((state.Mode==RebirthCamperEnergyMode.Camp||c is IRebirthCamperMobileEnergyConsumer)&&RebirthCamperEnergyBounds.ContainsDistance(c.Distance,activeRange))
            {
                double consumerQuanta=c is IRebirthCamperMobileEnergyConsumer?elapsed/NativePowerPeriodSeconds:quanta;
                double units=AccountingDemand(c)*consumerQuanta+state.ConsumptionRemainder;
                long needed=checked((long)Math.Floor(units));state.ConsumptionRemainder=units-needed;
                bool hasEnergy=HasUsableEnergy();long spent=Spend(needed);
                // Exact-cost ordinary lights still need their entire integer
                // activation supply. Partial payment never authorizes a light.
                power=needed==0?(hasEnergy?c.DemandWatts:0):spent==needed?c.DemandWatts:
                    c is IRebirthCamperEnergyAccountingConsumer?0:(consumerQuanta>0?(int)Math.Min(c.DemandWatts,Math.Floor(spent/consumerQuanta)):0);
            }
            supplied=checked(supplied+power);c.SetSupply(vehicle,power);
        }
        // Loads have already spent their source budget. Only remaining solar
        // production and the running vehicle engine may fund native charging.
        charging.Apply(state.Sources,solarBudget,elapsed,RebirthCamperBatteryCharging.EngineRunning(vehicle),RebirthCamperEnergyConfiguration.Current.AlternatorWatts);
        foreach(var source in state.Sources)
            if(source.Bank is PowerSolarPanel&&solarBudget.TryGetValue(source.Id,out long left))
                source.Bank.CurrentPower=(ushort)Math.Min(source.Bank.MaxPower,left);
        if(!Same(before,RebirthCamperEnergyState.Encode(state))){state.Revision=checked(state.Revision+1);Persist();}
    }
    private static double AccountingDemand(IRebirthCamperEnergyConsumer consumer)
        =>consumer is IRebirthCamperEnergyAccountingConsumer exact?exact.EnergyDemandWatts:consumer.DemandWatts;
    private void RefreshSolar(double quanta)
    {
        var pos=new Vector3i(Mathf.FloorToInt(vehicle.position.x),Mathf.FloorToInt(vehicle.position.y)+2,Mathf.FloorToInt(vehicle.position.z));
        var chunk=vehicle.world.GetChunkFromWorldPos(pos) as Chunk;
        bool light=chunk!=null&&vehicle.world.IsDaytime()&&chunk.GetLight(pos.x&15,pos.y,pos.z&15,Chunk.LIGHT_TYPE.SUN)==15;
        foreach(var source in state.Sources)
        if(source.Bank is PowerSolarPanel solar)
        {
            solar.HasLight=light;
            if(!light)solar.CurrentPower=0;
            else if(solar.isOn)
            {
                double generated=solar.MaxOutput*quanta+source.SolarRemainder;
                long whole=checked((long)Math.Floor(generated));source.SolarRemainder=generated-whole;
                solarBudget[source.Id]=checked(whole+solar.CurrentPower);
            }
        }
    }
    private bool HasUsableEnergy()
    {
        foreach(var source in state.Sources)
        {
            var bank=source.Bank;if(!bank.isOn||!bank.Stacks.Any(item=>!item.IsEmpty()))continue;
            if(bank is PowerSolarPanel){if(solarBudget.TryGetValue(source.Id,out long solar)&&solar>0)return true;continue;}
            if(bank.CurrentPower>0||bank is PowerGenerator generator&&generator.CurrentFuel>0)return true;
            if(bank is PowerBatteryBank&&bank.Stacks.Any(item=>!item.IsEmpty()&&item.itemValue.MaxUseTimes-item.itemValue.UseTimes>=1f))return true;
        }
        return false;
    }
    private long Spend(long requested)
    {
        long remaining=requested;
        foreach(var kind in RebirthCamperEnergyConfiguration.Current.Priority)
        foreach(var source in state.Sources.Where(entry=>RebirthCamperEnergyState.Kind(entry.Bank)==kind))
        {
            var bank=source.Bank;if(!bank.isOn||!bank.Stacks.Any(item=>!item.IsEmpty()))continue;
            if(bank is PowerSolarPanel solar)
            {
                if(!solar.HasLight){bank.CurrentPower=0;continue;}
                long budget=solarBudget.TryGetValue(source.Id,out long available)?available:0;
                long taken=Math.Min(remaining,budget);solarBudget[source.Id]=budget-taken;remaining-=taken;
                if(remaining==0)break;continue;
            }
            while(remaining>0)
            {
                if(bank.CurrentPower==0)
                {
                    if(bank is PowerSolarPanel)break;
                    bank.TickPowerGeneration();if(bank.CurrentPower==0)break;
                }
                int take=(int)Math.Min(remaining,bank.CurrentPower);bank.CurrentPower-=(ushort)take;remaining-=take;
            }
            if(remaining==0)break;
        }
        return requested-remaining;
    }
}

// Retain this registered patch type, but leave seat admission entirely native.
// Attachment starts native vehicle simulation; reassert the existing physical pin
// afterward so entering any camper seat never substitutes for switching to Travel.
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.AttachEntityToSelf))]
public static class RebirthCamperEnergyDriverGuard
{
    private static void Postfix(EntityVehicle __instance,int __result)
    {
        if(__result>=0&&RebirthCamperRadial.IsCamper(__instance))
            RebirthCamperEnergyImmobility.ObserveAndApply(__instance);
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.Update))]
public static class RebirthCamperEnergyVehicleTick
{
    private static void Postfix(EntityVehicle __instance)
    {
        if(RebirthCamperEnergyRuntime.IsRegistered&&ThreadManager.IsMainThread()&&__instance.world!=null&&!__instance.world.IsRemote()&&RebirthCamperRadial.IsCamper(__instance)
            &&ReferenceEquals(__instance.world.GetEntity(__instance.entityId),__instance))
            RebirthCamperEnergyRuntime.For(__instance).Tick();
    }
}
