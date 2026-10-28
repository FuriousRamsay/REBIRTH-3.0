using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class Program
{
    private static void Near(double? value,double expected,string name)=>Assert(value.HasValue&&Math.Abs(value.Value-expected)<1e-8,name);
    private static void EstimateCases()
    {
        var solarFirst=new[]{RebirthCamperEnergySourceKind.Solar,RebirthCamperEnergySourceKind.Generator,RebirthCamperEnergySourceKind.Battery};
        RebirthCamperEnergyRuntime.TryCreateBank(Item(1),out var bank,out _);var g=(PowerGenerator)bank;var cells=ItemStack.CreateArray(6);cells[0]=Item(4);
        cells[0].itemValue.Metadata["source-proof"]="unchanged";cells[0].itemValue.TextureFullArray=new byte[]{2,9,17};g.SetSlots(cells);g.CurrentPower=1000;g.CurrentFuel=2;g.isOn=true;
        string original=RebirthNativeItemCodec.Encode(g.Stacks[0].itemValue);
        var finite=RebirthCamperEnergyRuntimeEstimate.Evaluate(new[]{g},100,RebirthCamperEnergyMode.Camp,solarFirst);
        Near(finite.Seconds,37.6,"known23500 nativeunits sustain100 load for37.6s");
        Assert(finite.Status==RebirthCamperEnergyEstimateStatus.Finite&&!finite.SolarConditional,"finite storage-only estimate");
        Assert(g.CurrentFuel==2&&g.CurrentPower==1000&&RebirthNativeItemCodec.Encode(g.Stacks[0].itemValue)==original,"estimate does not burn/degrade/rewrite source");
        Assert(RebirthCamperEnergyRuntimeEstimate.Evaluate(new[]{g},0,RebirthCamperEnergyMode.Camp,solarFirst).Status==RebirthCamperEnergyEstimateStatus.Idle,"idle has no guessed duration");
        Assert(RebirthCamperEnergyRuntimeEstimate.Evaluate(new[]{g},100,RebirthCamperEnergyMode.Travel,solarFirst).Status==RebirthCamperEnergyEstimateStatus.Unavailable,"Travel does not claim Camp runtime");
        g.isOn=false;Near(RebirthCamperEnergyRuntimeEstimate.Evaluate(new[]{g},100,RebirthCamperEnergyMode.Camp,solarFirst).Seconds,0,"disabled bank reserve excluded");g.isOn=true;
        RebirthCamperEnergyRuntime.TryCreateBank(Item(2),out var b,out _);var battery=(PowerBatteryBank)b;cells=ItemStack.CreateArray(6);cells[0]=Item(5);cells[0].itemValue.UseTimes=9997.5f;battery.SetSlots(cells);battery.CurrentPower=10;battery.isOn=true;
        Near(RebirthCamperEnergyRuntimeEstimate.Evaluate(new[]{battery},100,RebirthCamperEnergyMode.Camp,solarFirst).Seconds,.304,"fractional batterycharge is not rounded into free energy");
        Assert(battery.Stacks[0].itemValue.UseTimes==9997.5f&&battery.CurrentPower==10,"battery estimate read-only");
        RebirthCamperEnergyRuntime.TryCreateBank(Item(3),out var s,out _);var panel=(PowerSolarPanel)s;cells=ItemStack.CreateArray(6);cells[0]=Item(6);panel.SetSlots(cells);panel.HasLight=true;panel.isOn=true;
        var daylight=RebirthCamperEnergyRuntimeEstimate.Evaluate(new[]{panel},20,RebirthCamperEnergyMode.Camp,solarFirst);
        Assert(daylight.Status==RebirthCamperEnergyEstimateStatus.Finite&&daylight.Seconds==0&&!daylight.SolarConditional,"solar alone cannot extend direct-load runtime");
        panel.HasLight=false;Near(RebirthCamperEnergyRuntimeEstimate.Evaluate(new[]{panel},20,RebirthCamperEnergyMode.Camp,solarFirst).Seconds,0,"obstructed solar generates no forecast energy");panel.HasLight=true;g.CurrentFuel=0;
        var generatorFirst=new[]{RebirthCamperEnergySourceKind.Generator,RebirthCamperEnergySourceKind.Solar,RebirthCamperEnergySourceKind.Battery};
        var a=RebirthCamperEnergyRuntimeEstimate.Evaluate(new PowerSource[]{g,panel},100,RebirthCamperEnergyMode.Camp,solarFirst);
        var z=RebirthCamperEnergyRuntimeEstimate.Evaluate(new PowerSource[]{g,panel},100,RebirthCamperEnergyMode.Camp,generatorFirst);
        Assert(a.Seconds==z.Seconds&&!a.SolarConditional&&!z.SolarConditional,"solar charging is excluded from conservative direct-load estimate regardless of priority");
        Assert(RebirthCamperEnergyRuntimeEstimate.Evaluate(new[]{g},100,RebirthCamperEnergyMode.Camp,new[]{solarFirst[0],solarFirst[0],solarFirst[2]}).Status==RebirthCamperEnergyEstimateStatus.Unavailable,"ambiguous priority refused");
        var c=Start(100);Install(c,1,10);Camp(c);Advance(c,4);Assert(c.Runtime.Snapshot().EstimatedRuntimeSeconds.HasValue,"authoritative snapshot binds estimator");
        var op=Guid.NewGuid();c.Runtime.Hold(op);Assert(c.Runtime.Snapshot().RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.Unavailable,"custody-held runtime unavailable");c.Runtime.Release(op);
    }
    private static byte[] LegacyObservation(byte version)
    {
        // Independent documented old protocol vector, no new estimate fields.
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream,Encoding.UTF8,true);
        w.Write(version);w.Write(false);w.Write("legacy");w.Write(false);w.Write(false);if(version==3)w.Write((byte)3);w.Write(true);
        w.Write((byte)RebirthCamperEnergyMode.Camp);w.Write(0f);w.Write(10f);w.Write(12L);w.Write(4);w.Write(100);w.Write(100);w.Write(0);w.Write(false);w.Flush();return stream.ToArray();
    }
    private static void WireCases()
    {
        var c=Start(100);Install(c,1,10);Camp(c);Advance(c,4);var snapshot=c.Runtime.Snapshot();
        var bytes=RebirthCamperEnergySnapshotWire.Write(snapshot,null,false,sourceCapacity:3);
        RebirthCamperEnergySnapshotWire.Read(bytes,out var read,out _,out _,out _,out _,out _,out int capacity);
        Assert(bytes[0]==5&&capacity==3&&read.RuntimeEstimateStatus==snapshot.RuntimeEstimateStatus&&read.EstimatedRuntimeSeconds==snapshot.EstimatedRuntimeSeconds,"actual wire5 estimate and capacity roundtrip");
        Assert(RebirthNativeItemCodec.Encode(read.Sources[0].Installed.itemValue)==RebirthNativeItemCodec.Encode(snapshot.Sources[0].Installed.itemValue),"wire native value metadata retained under explicit codec double");
        foreach(byte version in new byte[]{2,3})
        {RebirthCamperEnergySnapshotWire.Read(LegacyObservation(version),out var old,out _,out _,out _,out _,out _,out int cap);Assert(old.RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.Unavailable&&old.EstimatedRuntimeSeconds==null&&cap==(version==3?3:0),"oldwire missing estimate/capability stays unknown "+version);}
        snapshot.EstimatedRuntimeSeconds=double.NaN;Throws(()=>RebirthCamperEnergySnapshotWire.Write(snapshot,null,false),"NaN estimate write refused");
        snapshot.EstimatedRuntimeSeconds=1;snapshot.RuntimeEstimateStatus=RebirthCamperEnergyEstimateStatus.Idle;Throws(()=>RebirthCamperEnergySnapshotWire.Write(snapshot,null,false),"contradictory idle duration refused");
        Throws(()=>RebirthCamperEnergySnapshotWire.Read(bytes.Concat(new byte[]{0}).ToArray(),out _,out _,out _,out _,out _,out _),"trailing wire payload refused");
    }
    private static void MaintenanceFrameCases()
    {
        var receipt=new RebirthCamperEnergyMaintenanceReceipt{Target=Guid.NewGuid(),Operation=Guid.NewGuid(),Vehicle=Guid.NewGuid(),World="explicit-world-double",Owner="owner",Creation="creation",Position=new Vector3i(3,4,5),BlockRaw=7,BeforeDamage=100,AfterDamage=0};
        var item=Item(4,77);item.count=2;item.itemValue.Metadata["owner-proof"]="retain";item.itemValue.TextureFullArray=new byte[]{3,5,8};
        var before=new RebirthCamperEnergyMaintenanceFrame{Receipt=receipt,Cargo=new[]{item},Locks=new[]{false}};
        var after=RebirthCamperEnergyMaintenanceFrame.Decode(before.Encode());after.Receipt.Applied=true;after.Cargo[0].count=1;
        var record=new RebirthCamperTransferJournal.Record(receipt.Operation,receipt.Vehicle,"owner:creation",0,RebirthCamperTransferJournal.Phase.Prepared,before.Encode(),after.Encode());
        RebirthCamperEnergyMaintenanceFrame.ValidatePair(record,out var decodedBefore,out var decodedAfter);
        Assert(decodedBefore.Cargo[0].count==2&&decodedAfter.Cargo[0].count==1&&RebirthNativeItemCodec.Encode(decodedAfter.Cargo[0].itemValue)==RebirthNativeItemCodec.Encode(item.itemValue),"paid maintenance delta preserves exact remaining source payload");
        Throws(()=>RebirthCamperEnergyMaintenanceFrame.ValidatePair(new(receipt.Operation,receipt.Vehicle,"other:creation",0,record.State,record.Before,record.After),out _,out _),"changed owner receipt refused");
        Throws(()=>RebirthCamperEnergyMaintenanceFrame.ValidatePair(new(Guid.NewGuid(),receipt.Vehicle,record.OwnerCreation,0,record.State,record.Before,record.After),out _,out _),"changed operation refused");
        Throws(()=>RebirthCamperEnergyMaintenanceFrame.ValidatePair(new(receipt.Operation,receipt.Vehicle,record.OwnerCreation,0,record.State,record.After,record.Before),out _,out _),"reverse paid images refused");
        var locked=RebirthCamperEnergyMaintenanceFrame.Decode(before.Encode());locked.Locks[0]=true;var lockedAfter=RebirthCamperEnergyMaintenanceFrame.Decode(after.Encode());lockedAfter.Locks[0]=true;
        Throws(()=>RebirthCamperEnergyMaintenanceFrame.ValidatePair(new(receipt.Operation,receipt.Vehicle,record.OwnerCreation,0,record.State,locked.Encode(),lockedAfter.Encode()),out _,out _),"locked native cargo delta refused");
        Throws(()=>RebirthCamperEnergyMaintenanceReceipt.Decode(receipt.Encode().Concat(new byte[]{1}).ToArray()),"malformed target receipt refused");
        Assert(item.count==2&&item.itemValue.Metadata["owner-proof"]=="retain","pure receipt qualification did not debit source");
    }
}
