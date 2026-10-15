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
        =>Calculate(nativeBanks,demand,mode,sourcePriority,demandCarry,null);
    internal static void Apply(RebirthCamperEnergyState state,RebirthCamperEnergySnapshot snapshot,bool authoritative,double energyDemand)
    {
        if(!authoritative)return;
        var carries=state.Sources.ToDictionary(s=>s.Bank,s=>s.SolarRemainder);
        var result=Calculate(state.Sources.Select(s=>s.Bank),energyDemand,state.Mode,RebirthCamperEnergyConfiguration.Current.Priority,state.ConsumptionRemainder,carries);
        snapshot.EstimatedRuntimeSeconds=result.Seconds;snapshot.RuntimeEstimateStatus=result.Status;snapshot.RuntimeEstimateSolarConditional=result.SolarConditional;
    }
    private static RebirthCamperEnergyRuntimeEstimateResult Calculate(IEnumerable<PowerSource> banks,double demand,RebirthCamperEnergyMode mode,
        IEnumerable<RebirthCamperEnergySourceKind> priority,double carry,Dictionary<PowerSource,double> solarCarries)
    {
        var unknown=new RebirthCamperEnergyRuntimeEstimateResult();
        if(banks==null||priority==null||!Finite(demand)||demand<0||mode!=RebirthCamperEnergyMode.Camp||!Finite(carry)||carry<0||carry>=1)return unknown;
        var order=priority.ToArray();if(order.Length!=3||order.Distinct().Count()!=3||order.Any(k=>!Enum.IsDefined(typeof(RebirthCamperEnergySourceKind),k)))return unknown;
        if(demand==0)return new RebirthCamperEnergyRuntimeEstimateResult{Status=RebirthCamperEnergyEstimateStatus.Idle};
        var reserve=new double[3];var solar=new List<PowerSolarPanel>();double solarRate=0;
        foreach(var bank in banks)
        {
            if(bank==null||bank.Stacks==null||bank.CurrentPower>bank.MaxPower||bank.Stacks.Any(i=>i?.itemValue==null))return unknown;
            if(!bank.isOn||!bank.Stacks.Any(i=>i!=null&&!i.IsEmpty()))continue;
            if(bank is PowerSolarPanel panel)
            {if(panel.HasLight){solar.Add(panel);solarRate+=panel.MaxOutput;}continue;}
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
        if(solarRate>=demand)return new RebirthCamperEnergyRuntimeEstimateResult{Status=RebirthCamperEnergyEstimateStatus.DaylightSupported,SolarConditional=true};
        int solarIndex=Array.IndexOf(order,RebirthCamperEnergySourceKind.Solar);double before=0,after=0;
        for(int i=0;i<order.Length;i++)if(i<solarIndex)before+=reserve[(int)order[i]];else if(i>solarIndex)after+=reserve[(int)order[i]];
        // Respect actual source priority: reserves BEFORE solar pay full load;
        // solar may accumulate only within its real native buffer meanwhile.
        double carried=Math.Min(before,carry);before-=carried;
        double afterCarry=Math.Min(after,carry-carried);after-=afterCarry;double solarCarryCost=carry-carried-afterCarry;
        double firstPeriods=before/demand,solarBuffer=0;
        foreach(var panel in solar)
        {
            double fractional=solarCarries!=null&&solarCarries.TryGetValue(panel,out double fraction)?fraction:0;
            solarBuffer+=Math.Min(panel.MaxPower,panel.CurrentPower+panel.MaxOutput*firstPeriods+fractional);
        }
        solarBuffer=Math.Max(0,solarBuffer-solarCarryCost);
        double seconds=(firstPeriods+(after+solarBuffer)/(demand-solarRate))*RebirthCamperEnergyRuntime.NativePowerPeriodSeconds;
        if(!Finite(seconds)||seconds<0)return unknown;
        return new RebirthCamperEnergyRuntimeEstimateResult{Status=RebirthCamperEnergyEstimateStatus.Finite,Seconds=seconds,SolarConditional=solar.Count>0};
    }
    private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
}
