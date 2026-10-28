using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class Program
{
    private static RebirthCamperEnergyState.Source ChargeSource(PowerSource bank)
        =>new RebirthCamperEnergyState.Source {Id=Guid.NewGuid(),Bank=bank};
    private static PowerBatteryBank ChargeBank(float used=100)
    {
        var bank=new PowerBatteryBank {isOn=true,InputPerTick=5,ChargePerInput=1};
        bank.Stacks[0]=Item(5);bank.Stacks[0].itemValue.UseTimes=used;return bank;
    }
    private static void ChargingCases()
    {
        var battery=ChargeBank();var solar=new PowerSolarPanel {isOn=true,HasLight=true};
        var b=ChargeSource(battery);var s=ChargeSource(solar);var sources=new List<RebirthCamperEnergyState.Source>{s,b};
        var budget=new Dictionary<Guid,long>{{s.Id,4}};var charge=new RebirthCamperBatteryCharging();
        charge.Apply(sources,budget,.16,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==100&&budget[s.Id]==4,"less than native input cannot create unpaid charge");
        budget[s.Id]=5;charge.Apply(sources,budget,.16,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==99&&budget[s.Id]==0,"native conversion consumes exact funded solar input");
        battery.isOn=false;budget[s.Id]=20;charge.Apply(sources,budget,.16,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==99&&budget[s.Id]==20,"disabled battery neither charges nor consumes solar");
        battery.isOn=true;battery.Stacks[0].itemValue.UseTimes=0;charge.Apply(sources,budget,.16,false,50);
        Assert(budget[s.Id]==20,"full battery consumes no source power");
        battery.Stacks[0].itemValue.UseTimes=100;solar.HasLight=false;charge.Apply(sources,budget,.16,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==100&&budget[s.Id]==20,"night or shade cannot charge from stale solar budget");
        solar.HasLight=true;solar.isOn=false;charge.Apply(sources,budget,.16,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==100,"disabled solar cannot charge");solar.isOn=true;
        battery.Stacks[0].itemValue.UseTimes=2.3f;budget[s.Id]=20;charge.Apply(sources,budget,1,false,50);
        Assert(Math.Abs(battery.Stacks[0].itemValue.UseTimes-.3f)<.0001&&budget[s.Id]==10,"native integer charge threshold preserved without overcharging");

        // No source other than genuine solar budget or vehicle alternator can fund it.
        var generator=new PowerGenerator {isOn=true,CurrentFuel=5,CurrentPower=400};
        battery.Stacks[0].itemValue.UseTimes=100;battery.CurrentPower=100;budget.Clear();
        sources=new List<RebirthCamperEnergyState.Source>{ChargeSource(generator),b};charge.Reset();
        charge.Apply(sources,budget,1,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==100&&battery.CurrentPower==100&&generator.CurrentFuel==5&&generator.CurrentPower==400,"no generator or battery-buffer charge feedback");
        charge.Apply(sources,budget,1,true,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==100,"engine startup does not bill preceding engine-off interval");
        charge.Apply(sources,budget,1,true,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==94,"running alternator respects native bank acceptance per period");
        charge.Apply(sources,budget,1,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==94,"stopped engine leaves no stored alternator funding");
        charge.Reset();charge.Apply(sources,budget,1,true,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==94,"recovery reset discards prior engine-run eligibility");

        // Short cadence can accumulate time without accelerating or starving native charge ticks.
        float NativeCadence(double[] intervals)
        {
            var bank=ChargeBank();var solarSource=ChargeSource(new PowerSolarPanel {isOn=true,HasLight=true});
            var list=new List<RebirthCamperEnergyState.Source>{solarSource,ChargeSource(bank)};
            var funds=new Dictionary<Guid,long>{{solarSource.Id,1000}};var runner=new RebirthCamperBatteryCharging();
            foreach(double dt in intervals)runner.Apply(list,funds,dt,false,50);
            Assert(funds[solarSource.Id]==1000-(100-bank.Stacks[0].itemValue.UseTimes)*5,"all native charge units paid exactly once");
            return bank.Stacks[0].itemValue.UseTimes;
        }
        Assert(NativeCadence(Enumerable.Repeat(.1,10).ToArray())==NativeCadence(new[]{1d}),"0.1 and 1 second cadence preserve native acceptance rate");
        sources=new List<RebirthCamperEnergyState.Source>{s,b};budget[s.Id]=1000;battery.Stacks[0].itemValue.UseTimes=100;charge.Reset();
        charge.Apply(sources,budget,100000,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==36,"long interval bounded to64 native charging periods");
        charge.Apply(sources,budget,.16,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==35,"bounded long interval stores no future charge debt");
        sources=new List<RebirthCamperEnergyState.Source>{s,b};battery.Stacks[0].itemValue.UseTimes=100;budget[s.Id]=2;charge.Reset();
        charge.Apply(sources,budget,.16,true,3);charge.Apply(sources,budget,.16,true,3);
        Assert(battery.Stacks[0].itemValue.UseTimes==99&&budget[s.Id]==0,"solar and alternator combine only to pay native accepted input");
        battery.ChargePerInput=2;budget[s.Id]=100;charge.Apply(sources,budget,.16,false,50);
        Assert(battery.Stacks[0].itemValue.UseTimes==99&&budget[s.Id]==100,"unsupported overdraw conversion safely refused");

        var c=Start();c.Vehicle.IsEngineRunning=true;
        Assert(!RebirthCamperBatteryCharging.EngineRunning(c.Vehicle),"no driver means no alternator despite stale engine flag");
        c.Vehicle.Seats[0]=c.Player;c.Vehicle.vehicleRB.velocity=new UnityEngine.Vector3(.001f,0,0);
        Assert(RebirthCamperBatteryCharging.EngineRunning(c.Vehicle),"slow driving with native running fueled engine charges");
        c.Vehicle.vehicleRB.velocity=UnityEngine.Vector3.zero;
        Assert(RebirthCamperBatteryCharging.EngineRunning(c.Vehicle),"native engine idling with driver remains running without speed cutoff");
        c.Vehicle.vehicle.Fuel=0;
        Assert(!RebirthCamperBatteryCharging.EngineRunning(c.Vehicle),"empty fuel cannot fund charging");
        EntityVehicle.VehicleFuelUsageModifier=0;
        Assert(RebirthCamperBatteryCharging.EngineRunning(c.Vehicle),"native fuel-disabled option honored");
        EntityVehicle.VehicleFuelUsageModifier=1;c.Vehicle.IsEngineRunning=false;
        Assert(!RebirthCamperBatteryCharging.EngineRunning(c.Vehicle),"parked engine off cannot charge");

        RuntimeChargingCases();
    }

    private static Guid InstallDamagedBattery(Context c,float used=100)
    {
        var id=Install(c,2);var snapshot=c.Runtime.Snapshot().Sources.Single(source=>source.Id==id);
        var slots=snapshot.Components;slots[0].itemValue.UseTimes=used;slots[0].itemValue.Metadata["retained.identity"]="native battery";
        Assert(c.Runtime.TryPrepareContents(c.Runtime.Snapshot().Revision,id,slots,0,true,out var contents,out _)&&c.Runtime.Commit(contents),"damaged battery accepted through existing custody preparation");return id;
    }
    private static float Used(Context c,Guid id)=>c.Runtime.Snapshot().Sources.Single(s=>s.Id==id).Components[0].itemValue.UseTimes;
    private static void RuntimeChargingCases()
    {
        // Actual production Tick/RefreshSolar/Spend/charging/Persist/codec code,
        // with explicit world, native sync/save and item-codec boundaries.
        var c=Start(20,1);Install(c,3);var id=InstallDamagedBattery(c);c.Vehicle.world.Day=false;Camp(c);
        c.Vehicle.world.Day=true;int sync=c.Vehicle.SyncCount,saves=VehicleManager.Instance.Saves;long revision=c.Runtime.Snapshot().Revision;
        Advance(c,20);
        Assert(c.Loads[0].Supply==20&&Used(c,id)==98,"battery supplies consumers while solar charges it: supplied="+c.Loads[0].Supply+" used="+Used(c,id));
        Assert(c.Vehicle.SyncCount==sync+1&&VehicleManager.Instance.Saves==saves+1&&c.Runtime.Snapshot().Revision==revision+1,"charge publishes one existing state revision/sync/save");
        string frame;c.Vehicle.vehicle.Item.TryGetMetadata(RebirthCamperEnergyState.Key,out frame);
        var restored=RebirthCamperEnergyState.DecodeBase64(frame,c.Runtime.Identity);
        Assert(restored.Version==2&&restored.Sources.Single(s=>s.Id==id).Bank.Stacks[0].itemValue.UseTimes==98,"existing v2 save frame contains charged native ItemValue");
        Assert(restored.Sources.Single(s=>s.Id==id).Bank.Stacks[0].itemValue.Metadata["retained.identity"]=="native battery","charging preserves native item metadata");
        var portable=c.Runtime.Snapshot().Revision;Assert(c.Runtime.TryPrepareRemove(portable,id,out var remove,out _),"charged source can prepare native portable image");
        Assert(remove.Removed.itemValue.TryGetMetadata(RebirthCamperEnergyState.PortableKey,out _),"portable source carries charged state through existing custody schema");

        var exact=Start(30,1);Install(exact,3);var exactId=InstallDamagedBattery(exact);exact.Vehicle.world.Day=false;Camp(exact);exact.Vehicle.world.Day=true;Advance(exact,20);
        Assert(exact.Loads[0].Supply==30&&Used(exact,exactId)==98,"solar generation never feeds the load directly: used="+Used(exact,exactId));
        var parked=Start(0,1);Install(parked,3);var parkedId=InstallDamagedBattery(parked);Advance(parked,20);
        Assert(parked.Runtime.Snapshot().Mode==RebirthCamperEnergyMode.Travel&&Used(parked,parkedId)==94,"daytime unused solar charges parked Travel batteries without active devices");
        RebirthCamperStationSaveEvidence.Pending=true;float before=Used(parked,parkedId);Advance(parked,200);RebirthCamperStationSaveEvidence.Pending=false;Advance(parked,20);
        Assert(Used(parked,parkedId)==before-6,"station save hold creates no charging catch-up");
        RebirthCamperStationFuelOriginal.Blocked=true;before=Used(parked,parkedId);Advance(parked,200);RebirthCamperStationFuelOriginal.Blocked=false;Advance(parked,20);
        Assert(Used(parked,parkedId)==before-6,"fuel custody hold creates no charging catch-up");
        before=Used(parked,parkedId);RebirthCamperEnergyRuntime.PauseClock(parked.Vehicle);Advance(parked,2000);
        Assert(Used(parked,parkedId)==before,"unload reset does not award offline charging");Advance(parked,20);
        Assert(Used(parked,parkedId)==before-6,"loaded native interval resumes charging");
        parked.Vehicle.world.Remote=true;before=Used(parked,parkedId);Advance(parked,200);parked.Vehicle.world.Remote=false;
        Assert(Used(parked,parkedId)==before,"remote world cannot mutate installed charge");
        Throws(()=>RebirthCamperEnergyConfiguration.Configure(20,20,10,1,3,new[]{RebirthCamperEnergySourceKind.Solar,RebirthCamperEnergySourceKind.Generator,RebirthCamperEnergySourceKind.Battery},301),"out-of-range alternator rejected");
    }
}
