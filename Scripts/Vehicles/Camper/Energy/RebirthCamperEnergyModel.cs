using System;
using System.Collections.ObjectModel;

public enum RebirthCamperEnergyMode : byte { Travel, Deploying, Camp, Packing }
public enum RebirthCamperEnergySourceKind : byte { Generator, Battery, Solar }
public enum RebirthCamperEnergySourceStatus : byte { Disabled, Travel, Transition, Empty, Obstructed, Available }

// Tuning choices are provisional, not permanent gameplay requirements.
// Native block definitions supply power/fuel/charge capacities and conversion rates.
public sealed class RebirthCamperEnergyConfiguration
{
    public static RebirthCamperEnergyConfiguration Current { get; private set; } = new RebirthCamperEnergyConfiguration();
    public float DeploySeconds { get; private set; } = 8f;
    public float PackSeconds { get; private set; } = 8f;
    public float Range { get; private set; } = 10f;
    // Approved Engineer width is exactly twice normal width; radius scales equally.
    public float EngineerRange => Range*2f;
    public float NativeTickSeconds { get; private set; } = 1f;
    public int MaximumSources { get; private set; } = 3;
    public int AlternatorWatts { get; private set; } = 50;
    public ReadOnlyCollection<RebirthCamperEnergySourceKind> Priority { get; private set; } =
        new ReadOnlyCollection<RebirthCamperEnergySourceKind>(new[]{ RebirthCamperEnergySourceKind.Solar, RebirthCamperEnergySourceKind.Generator, RebirthCamperEnergySourceKind.Battery });
    public static void Configure(float deploySeconds,float packSeconds,float range,float tickSeconds,int maximumSources,
        RebirthCamperEnergySourceKind[] priority,int alternatorWatts=50)
    {
        if(!ThreadManager.IsMainThread()||!Finite(deploySeconds)||!Finite(packSeconds)||!Finite(range)||!Finite(tickSeconds)
            ||deploySeconds<=0||deploySeconds>3600||packSeconds<=0||packSeconds>3600||range<=0||range>100||tickSeconds<0.1f||tickSeconds>10
            ||maximumSources<1||maximumSources>12||priority==null||priority.Length!=3||alternatorWatts<0||alternatorWatts>300)
            throw new ArgumentException("Bounded camper energy configuration required.");
        int mask=0;foreach(var kind in priority){int bit=1<<(int)kind;if((int)kind<0||(int)kind>2||(mask&bit)!=0)throw new ArgumentException("Unique source priority required.");mask|=bit;}
        Current=new RebirthCamperEnergyConfiguration {DeploySeconds=deploySeconds,PackSeconds=packSeconds,Range=range,
            NativeTickSeconds=tickSeconds,MaximumSources=maximumSources,AlternatorWatts=alternatorWatts,Priority=new ReadOnlyCollection<RebirthCamperEnergySourceKind>((RebirthCamperEnergySourceKind[])priority.Clone())};
    }
    private static bool Finite(float number)=>!float.IsNaN(number)&&!float.IsInfinity(number);
}

public sealed class RebirthCamperEnergySourceSnapshot
{
    public int Position {get;internal set;}
    public Guid Id {get;internal set;}
    public RebirthCamperEnergySourceKind Kind {get;internal set;}
    public bool IsOn {get;internal set;}
    public ushort CurrentFuel {get;internal set;}
    public ushort MaxFuel {get;internal set;}
    public ushort CurrentPower {get;internal set;}
    public ushort MaxOutput {get;internal set;}
    public ushort AvailableWatts {get;internal set;}
    public RebirthCamperEnergySourceStatus Status {get;internal set;}
    internal ItemStack installed;
    internal ItemStack[] components;
    public ItemStack Installed => installed.Clone();
    public ItemStack[] Components => ItemStack.Clone(components);
}

public sealed class RebirthCamperEnergySnapshot
{
    public RebirthCamperEnergyMode Mode {get;internal set;}
    public float TransitionSeconds {get;internal set;}
    public float Range {get;internal set;}
    public long Revision {get;internal set;}
    public int AvailableWatts {get;internal set;}
    public int DemandWatts {get;internal set;}
    public int ActiveDemandWatts=>DemandWatts;
    public int SolarWatts=>System.Linq.Enumerable.Sum(Sources,source=>source.Kind==RebirthCamperEnergySourceKind.Solar?(int)source.AvailableWatts:0);
    public int SuppliedWatts {get;internal set;}
    public double? EstimatedRuntimeSeconds {get;internal set;}
    public RebirthCamperEnergyEstimateStatus RuntimeEstimateStatus {get;internal set;}
    public bool RuntimeEstimateSolarConditional {get;internal set;}
    public ReadOnlyCollection<RebirthCamperEnergySourceSnapshot> Sources {get;internal set;}
}

// Adapter reports actual consumers every authoritative tick. Supply callback must
// not run a native bank tick or spend fuel; the core alone debits native sources.
public interface IRebirthCamperEnergyConsumer
{
    string Id {get;}
    int Priority {get;}
    int DemandWatts {get;}
    float Distance {get;}
    void SetSupply(EntityVehicle vehicle,int suppliedWatts);
}

// Optional exact resource cost; integer watts remain native activation/display demand.
public interface IRebirthCamperEnergyAccountingConsumer
{
    double EnergyDemandWatts {get;}
}
