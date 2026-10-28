using System;
using System.Collections.Generic;
using System.Linq;

public enum RebirthCamperEnergyEstimateStatus:byte {Unavailable,Idle,Finite,DaylightSupported}
public sealed class RebirthCamperEnergyRuntimeEstimateResult
{
    public RebirthCamperEnergyEstimateStatus Status {get;internal set;}
    public double? Seconds {get;internal set;}
    public bool SolarConditional {get;internal set;}
}

// Read-only arithmetic over native bank state. Native energy units are NOT Wh.
public static class RebirthCamperEnergyRuntimeEstimate
{
    public static RebirthCamperEnergyRuntimeEstimateResult Evaluate(IEnumerable<PowerSource> nativeBanks,int demand,RebirthCamperEnergyMode mode,
        IEnumerable<RebirthCamperEnergySourceKind> sourcePriority,double demandCarry=0)
        =>Evaluate(nativeBanks,(double)demand,mode,sourcePriority,demandCarry);
    public static RebirthCamperEnergyRuntimeEstimateResult Evaluate(IEnumerable<PowerSource> nativeBanks,double demand,RebirthCamperEnergyMode mode,
        IEnumerable<RebirthCamperEnergySourceKind> sourcePriority,double demandCarry=0)
        =>Calculate(nativeBanks,demand,mode,sourcePriority,demandCarry);
    internal static void Apply(RebirthCamperEnergyState state,RebirthCamperEnergySnapshot snapshot,bool authoritative,double energyDemand)
    {
        if(!authoritative)return;
        var result=Calculate(state.Sources.Select(s=>s.Bank),energyDemand,state.Mode,RebirthCamperEnergyConfiguration.Current.Priority,state.ConsumptionRemainder);
        snapshot.EstimatedRuntimeSeconds=result.Seconds;snapshot.RuntimeEstimateStatus=result.Status;snapshot.RuntimeEstimateSolarConditional=result.SolarConditional;
    }
    private static RebirthCamperEnergyRuntimeEstimateResult Calculate(IEnumerable<PowerSource> banks,double demand,RebirthCamperEnergyMode mode,
        IEnumerable<RebirthCamperEnergySourceKind> priority,double carry)
    {
        var unknown=new RebirthCamperEnergyRuntimeEstimateResult();
        if(banks==null||priority==null||!Finite(demand)||demand<0||mode!=RebirthCamperEnergyMode.Camp||!Finite(carry)||carry<0||carry>=1)return unknown;
        var order=priority.ToArray();if(order.Length!=3||order.Distinct().Count()!=3||order.Any(k=>!Enum.IsDefined(typeof(RebirthCamperEnergySourceKind),k)))return unknown;
        if(demand==0)return new RebirthCamperEnergyRuntimeEstimateResult{Status=RebirthCamperEnergyEstimateStatus.Idle};
        var reserve=new double[3];
        foreach(var bank in banks)
        {
            if(bank==null||bank.Stacks==null||bank.CurrentPower>bank.MaxPower||bank.Stacks.Any(i=>i?.itemValue==null))return unknown;
            if(!bank.isOn||!bank.Stacks.Any(i=>i!=null&&!i.IsEmpty()))continue;
            if(bank is PowerSolarPanel)continue;
            double units=bank.CurrentPower;
            if(bank is PowerGenerator generator)
            {
                if(!Finite(generator.OutputPerFuel)||generator.OutputPerFuel<0)return unknown;
                // Spend refills only at CurrentPower0. This is the exact native
                // generator capacity predicate and ushort conversion per fuel.
                if(generator.MaxPower>=generator.OutputPerFuel)units+=generator.CurrentFuel*(double)(ushort)generator.OutputPerFuel;
                reserve[(int)RebirthCamperEnergySourceKind.Generator]+=units;
            }
            else if(bank is PowerBatteryBank battery)
            {
                if(battery.OutputPerCharge==0)return unknown;
                if(battery.MaxPower>=battery.OutputPerCharge)foreach(var item in battery.Stacks)
                {
                    if(item==null||item.IsEmpty())continue;
                    double charge=item.itemValue.MaxUseTimes-item.itemValue.UseTimes;
                    if(!Finite(charge))return unknown;
                    units+=Math.Max(0,Math.Floor(charge))*battery.OutputPerCharge;
                }
                reserve[(int)RebirthCamperEnergySourceKind.Battery]+=units;
            }
            else return unknown;
        }
        // Conservative reserve estimate: solar may recharge batteries later,
        // but its production is never counted as direct supply for lights.
        double seconds=Math.Max(0,(reserve[(int)RebirthCamperEnergySourceKind.Generator]+
            reserve[(int)RebirthCamperEnergySourceKind.Battery]-carry)/demand)*RebirthCamperEnergyRuntime.NativePowerPeriodSeconds;
        if(!Finite(seconds)||seconds<0)return unknown;
        return new RebirthCamperEnergyRuntimeEstimateResult{Status=RebirthCamperEnergyEstimateStatus.Finite,Seconds=seconds};
    }
    private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
}
