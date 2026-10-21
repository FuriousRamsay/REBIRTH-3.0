using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

internal static partial class Program
{
    private static int assertions;
    private sealed class Consumer:IRebirthCamperEnergyConsumer
    {public string Id{get;set;}="load";public int Priority{get;set;}public int DemandWatts{get;set;}public float Distance{get;set;}public int Supply,Calls;public void SetSupply(EntityVehicle vehicle,int supplied){Supply=supplied;Calls++;}}
    private sealed class Context
    {public EntityVehicle Vehicle;public EntityPlayerLocal Player;public RebirthCamperEnergyRuntime Runtime;public Consumer[] Loads;}
    private static void Assert(bool condition,string reason)
    {assertions++;if(!condition)throw new Exception("FAIL "+reason);}
    private static void Throws(Action action,string reason)
    {bool failed=false;try{action();}catch{failed=true;}Assert(failed,reason);}
    private static void FreshSourceManifest()
    {
        string root=Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="FixtureSourceRoot").Value;
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"generated/source_manifest.json")));
        string project=json.RootElement.GetProperty("projectRoot").GetString();
        foreach(var item in json.RootElement.GetProperty("sources").EnumerateArray())
        {string current=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(project,item.GetProperty("path").GetString()))));
            Assert(current==item.GetProperty("sha256").GetString(),"currentSourceMatches "+item.GetProperty("path").GetString());}
        Assert(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"generated/ActualStationClock.cs"))))==json.RootElement.GetProperty("generatedHash").GetString(),"generated actual clock fingerprint");
        foreach(var file in json.RootElement.GetProperty("extracted").EnumerateArray())Assert(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"generated",file.GetProperty("name").GetString()))))==file.GetProperty("sha256").GetString(),"exact extracted source "+file.GetProperty("name").GetString());
    }
    private static void Definitions()
    {
        void Item(int id,string name,int maximum=0)=>ItemClass.Classes[id]=new ItemClass {Id=id,Name=name,NativeMaxUse=maximum};
        Item(1,"generatorbank");Item(2,"batterybank");Item(3,"solarbank");Item(4,"smallEngine");Item(5,"carBattery",10000);Item(6,"solarCell");
        void Bank(int type,string name,string cls,string component,int output,int max,string extra,string value)
        {var b=new BlockPowerSource {Name=name,SlotItemName=component,OutputPerStack=output};b.Properties.Values["Class"]=cls;b.Properties.Values["MaxPower"]=max.ToString();b.Properties.Values[extra]=value;Block.list[type]=b;}
        Bank(1,"generatorbank","Generator","smallEngine",100,12250,"OutputPerFuel","11250");Block.list[1].Properties.Values["MaxFuel"]="1000";
        Bank(2,"batterybank","BatteryBank","carBattery",50,400,"OutputPerCharge","90");Bank(3,"solarbank","SolarPanel","solarCell",30,60000,"unused","0");
    }
    private static ItemStack Item(int type,int seed=17)=>new ItemStack(new ItemValue {type=type,Seed=seed},1);
    private static Context Start(int demand=0,float cadence=0.1f)
    {
        RebirthCamperEnergyConfiguration.Configure(5,5,15,cadence,3,new[]{RebirthCamperEnergySourceKind.Solar,RebirthCamperEnergySourceKind.Generator,RebirthCamperEnergySourceKind.Battery});
        var world=new World();var vehicle=new EntityVehicle {entityId=1,world=world};var player=new EntityPlayerLocal {entityId=2,world=world};
        world.Entities[1]=vehicle;world.Entities[2]=player;GameManager.Instance.World=world;GameTimer.Instance.ticks+=100;UnityEngine.Time.realtimeSinceStartup+=1;
        RebirthCamperEnergyRuntime.SetRegistered(true);RebirthCamperEnergyRuntime.CustodyReady=_=>true;RebirthCamperEnergyRuntime.SuspendConsumers=_=>{};
        var c=new Context {Vehicle=vehicle,Player=player,Runtime=RebirthCamperEnergyRuntime.For(vehicle),Loads=new[]{new Consumer {DemandWatts=demand}}};
        RebirthCamperEnergyRuntime.Consumers=_=>c.Loads;c.Runtime.Tick();return c;
    }
    private static Guid Install(Context c,int kind,ushort fuel=0)
    {
        Assert(RebirthCamperEnergyRuntime.TryCreateBank(Item(kind),out var bank,out _),"empty native bank accepted "+kind);
        Assert(c.Runtime.TryPrepareInstall(c.Runtime.Snapshot().Revision,Item(kind),bank,out var installed,out _),"prepare empty source");
        Assert(c.Runtime.Commit(installed),"commit empty source");Guid id=c.Runtime.Snapshot().Sources.Last().Id;
        var slots=ItemStack.CreateArray(6);slots[0]=Item(kind==1?4:kind==2?5:6);
        Assert(c.Runtime.TryPrepareContents(c.Runtime.Snapshot().Revision,id,slots,fuel,true,out var filled,out _),"prepare source components");
        Assert(c.Runtime.Commit(filled),"commit source components");return id;
    }
    private static void Camp(Context c)
    {
        Assert(RebirthCamperEnergyRuntime.RequestMode(c.Vehicle,c.Player,RebirthCamperEnergyMode.Camp,out _),"parked owner starts deployment");
        // Deployment is observed on a configured processing tick. A ten-second
        // resource cadence cannot be asserted after only five elapsed seconds.
        GameTimer.Instance.ticks+=(ulong)Math.Ceiling(Math.Max(5,RebirthCamperEnergyConfiguration.Current.NativeTickSeconds)*20);
        c.Runtime.Tick();Assert(c.Runtime.Snapshot().Mode==RebirthCamperEnergyMode.Camp,"actual timer completes Camp");
    }
    private static void Advance(Context c,int ticks){GameTimer.Instance.ticks+=(ulong)ticks;c.Runtime.Tick();}
    private static void ConfigAndDriver()
    {
        Throws(()=>RebirthCamperEnergyConfiguration.Configure(0,5,15,1,3,new[]{RebirthCamperEnergySourceKind.Solar,RebirthCamperEnergySourceKind.Generator,RebirthCamperEnergySourceKind.Battery}),"zero deployment refused");
        Throws(()=>RebirthCamperEnergyConfiguration.Configure(5,0,15,1,3,new[]{RebirthCamperEnergySourceKind.Solar,RebirthCamperEnergySourceKind.Generator,RebirthCamperEnergySourceKind.Battery}),"zero packing refused");
        var c=Start();c.Vehicle.Seats[0]=c.Player;Assert(!RebirthCamperEnergyRuntime.RequestMode(c.Vehicle,c.Player,RebirthCamperEnergyMode.Camp,out _),"current driver cannot deploy");
        c.Vehicle.Seats[0]=null;c.Vehicle.vehicleRB.velocity=new UnityEngine.Vector3(1,0,0);Assert(!RebirthCamperEnergyRuntime.RequestMode(c.Vehicle,c.Player,RebirthCamperEnergyMode.Camp,out _),"moving vehicle cannot deploy");
        c.Vehicle.vehicleRB.velocity=new UnityEngine.Vector3();Assert(RebirthCamperEnergyRuntime.RequestMode(c.Vehicle,c.Player,RebirthCamperEnergyMode.Camp,out _),"parked deploy admitted");
        var guard=typeof(RebirthCamperEnergyDriverGuard).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static);
        Assert(guard!=null,"registered driver guard retains native seat admission through postfix");
        c.Vehicle.Seats[0]=c.Player;guard.Invoke(null,new object[]{c.Vehicle,0});
        Assert(c.Vehicle.vehicleRB.constraints==UnityEngine.RigidbodyConstraints.FreezeAll,"native driver attachment reasserts accepted deployment pin");
        c.Vehicle.Seats[0]=null;Advance(c,100);
        Assert(RebirthCamperEnergyRuntime.RequestMode(c.Vehicle,c.Player,RebirthCamperEnergyMode.Travel,out _),"Camp starts packing");
        Advance(c,100);guard.Invoke(null,new object[]{c.Vehicle,0});
        Assert(c.Vehicle.vehicleRB.constraints!=UnityEngine.RigidbodyConstraints.FreezeAll,"completed Travel releases camp pin");
    }
    private static (ushort Fuel,ushort Power) GeneratorRun(float cadence,int[] advances)
    {
        var c=Start(1000,cadence);Guid id=Install(c,1,10);Camp(c);c.Runtime.TryCopyBank(id,out var initial);
        ushort start=((PowerGenerator)initial).CurrentFuel;
        foreach(int ticks in advances)Advance(c,ticks);
        c.Runtime.TryCopyBank(id,out var bank);var g=(PowerGenerator)bank;
        Assert(c.Loads[0].Supply==1000&&g.MaxOutput==100,"demand above native MaxOutput is fully supplied while resource exists");
        long spent=(start-g.CurrentFuel)*11250L+initial.CurrentPower-g.CurrentPower;
        Assert(spent==62500,"native0.16 elapsed budget for ten seconds is62500 units");return(g.CurrentFuel,g.CurrentPower);
    }
    private static void ResourceTiming()
    {
        var a=GeneratorRun(0.1f,Enumerable.Repeat(2,100).ToArray());var b=GeneratorRun(1,Enumerable.Repeat(20,10).ToArray());var d=GeneratorRun(10,new[]{200});
        Assert(a==b&&b==d,"0.1/1/10s cadence and lag conserve same elapsed generator fuel/buffer");
        var c=Start(1000);Guid id=Install(c,2);Camp(c);Assert(c.Runtime.TryCopyBank(id,out var before),"copy actual battery source");Advance(c,4);c.Runtime.TryCopyBank(id,out var after);
        long used=(long)(after.Stacks[0].itemValue.UseTimes-before.Stacks[0].itemValue.UseTimes)*90+before.CurrentPower-after.CurrentPower;
        Assert(used==1250&&c.Loads[0].Supply==1000&&after.MaxOutput==50,"battery native charge chunks conserve1250 units and ignore output cap");
        var x=Start();Install(x,1,10);x.Loads=Enumerable.Range(0,100).Select(i=>new Consumer {Id="device"+i,DemandWatts=10}).ToArray();Camp(x);Advance(x,4);
        Assert(x.Loads.All(v=>v.Supply==10),"100 real-provider consumer doubles admitted above source MaxOutput, no device-count cap");
    }
    private static void SolarBudget()
    {
        var c=Start();Guid id=Install(c,3);c.Loads=new[]{new Consumer {Id="first",DemandWatts=20},new Consumer {Id="second",DemandWatts=20}};Camp(c);Advance(c,4);
        Assert(c.Loads[0].Supply==20&&c.Loads[1].Supply==20,"finite68-unit solar budget includes prior native30-unit buffer and fractional production carry; no refresh per consumer");
        Assert(c.Runtime.TryCopyBank(id,out var solar)&&solar.CurrentPower==18,"shared solar budget debited by exactly50 units");
        c.Vehicle.world.Chunk.Sun=14;Advance(c,4);Assert(c.Loads.All(v=>v.Supply==0),"native partial sunlight supplies zero solar");
        c.Vehicle.world.Chunk.Sun=15;c.Vehicle.world.Day=false;Advance(c,4);Assert(c.Loads.All(v=>v.Supply==0),"night supplies zero solar");
    }
    private static void SourceImagesAndRefusals()
    {
        var c=Start();Guid id=Install(c,1,9);var slots=c.Runtime.Snapshot().Sources.Single().Components;
        slots[0].itemValue.TextureFullArray=new byte[]{0,255,4,7};slots[0].itemValue.Metadata["foreign.paint"]="exact";
        slots[0].itemValue.Metadata["foreign.origin"]="do not strip";slots[0].itemValue.Seed=819;
        Assert(c.Runtime.TryPrepareContents(c.Runtime.Snapshot().Revision,id,slots,9,true,out var change,out _),"complete-data native codec DOUBLE input accepted");
        Assert(c.Runtime.Commit(change),"actual state encode/decode commit retains double full image");
        Assert(c.Runtime.Snapshot().Sources[0].Components[0].itemValue.TextureFullArray.SequenceEqual(new byte[]{0,255,4,7}),"paint bytes preserved at doubled native codec boundary");
        Assert(c.Runtime.TryPrepareRemove(c.Runtime.Snapshot().Revision,id,out var removed,out _),"prepare full-source packing");
        var retained=removed.Removed;Assert(c.Runtime.Commit(removed),"remove source once");Assert(c.Runtime.AlreadyMatchesAfter(removed)&&!c.Runtime.Commit(removed),"already-applied observation no second source write");
        var target=Start();Assert(target.Runtime.TryPrepareInstall(target.Runtime.Snapshot().Revision,retained,null,out var insert,out _),"packed source can move campers");Assert(target.Runtime.Commit(insert),"insert packed source once");
        var snapshot=target.Runtime.Snapshot().Sources.Single();Assert(snapshot.Id==id&&snapshot.CurrentFuel==9&&snapshot.Components[0].itemValue.Seed==819&&snapshot.Components[0].itemValue.Metadata["foreign.origin"]=="do not strip","sourceId/fuel/native metadata retained");
        var bytes=insert.After;Throws(()=>RebirthCamperEnergyState.Decode(bytes.Concat(new byte[]{1}).ToArray(),target.Runtime.Identity),"trailing frame bytes refused");
        Throws(()=>RebirthCamperEnergyState.Decode(bytes.Take(bytes.Length-1).ToArray(),target.Runtime.Identity),"truncated native source refused");
        var malformed=(byte[])bytes.Clone();
        using(var reader=new BinaryReader(new MemoryStream(malformed)))
        {
            reader.ReadInt32();reader.ReadBytes(16);reader.ReadInt64();reader.ReadByte();reader.ReadSingle();reader.ReadDouble();reader.ReadByte();reader.ReadBytes(16);
            reader.ReadByte();reader.ReadInt32();reader.ReadString();reader.ReadByte();reader.ReadInt32();reader.ReadString();
            long secondCount=reader.BaseStream.Position;
            // Second component is canonical native empty. Advertising count1 with
            // that empty image must be rejected by the actual Energy decoder.
            Array.Copy(BitConverter.GetBytes(1),0,malformed,secondCount,4);
        }
        Throws(()=>RebirthCamperEnergyState.Decode(malformed,target.Runtime.Identity),"malformed count1 native-empty component refused");
        Throws(()=>RebirthCamperEnergyState.Decode(bytes,Guid.NewGuid()),"foreign vehicle frame identity refused");
        var bad=snapshot.Components;bad[0]=Item(5);Assert(!target.Runtime.TryPrepareContents(target.Runtime.Snapshot().Revision,id,bad,9,true,out _,out _),"wrong native component type refused without changes");
        byte[] before=target.Runtime.Snapshot().Sources[0].Components[0].itemValue.TextureFullArray;
        snapshot.Components[0].itemValue.TextureFullArray[0]=99;Assert(target.Runtime.Snapshot().Sources[0].Components[0].itemValue.TextureFullArray.SequenceEqual(before),"public source snapshots are detached");
    }
    private static void HoldsAndRecovery()
    {
        var c=Start(1000);Guid id=Install(c,1,10);Camp(c);Advance(c,4);c.Runtime.TryCopyBank(id,out var original);
        int suspends=0;RebirthCamperEnergyRuntime.SuspendConsumers=_=>suspends++;
        Guid op=Guid.NewGuid();Assert(c.Runtime.Hold(op)&&c.Runtime.Hold(op)&&!c.Runtime.Hold(Guid.NewGuid()),"one operation hold idempotent, foreign overlap refused");
        Assert(suspends==1&&RebirthCamperEnergyRuntime.IsHeld(c.Vehicle),"firsthold suspends actual-existing consumer boundary once");
        var modules=RebirthCamperModuleRuntime.For(c.Vehicle);var station=modules.Stations.GetSlot(0).State;station.lastTickTime=GameTimer.Instance.ticks;
        modules.TickStations();Advance(c,200);modules.TickStations();Assert(station.Effects==0,"actual TickStations body pauses only exact Energy hold");
        c.Runtime.Release(Guid.NewGuid());Assert(RebirthCamperEnergyRuntime.IsHeld(c.Vehicle),"foreign release cannot clear operation");
        c.Runtime.Release(op);modules.TickStations();Assert(station.Effects==0&&station.lastTickTime==GameTimer.Instance.ticks,"firstrelease resets station clock, no catchup effects");
        Advance(c,4);c.Runtime.TryCopyBank(id,out var after);
        long spent=(((PowerGenerator)original).CurrentFuel-((PowerGenerator)after).CurrentFuel)*11250L+original.CurrentPower-after.CurrentPower;
        Assert(spent==1250,"held ten seconds are not billed after exact release");
        RebirthCamperEnergyRuntime.CustodyReady=null;long revision=c.Runtime.Snapshot().Revision;Advance(c,100);
        Assert(c.Runtime.Snapshot().Revision==revision&&suspends>1,"defaultfalse custody gate no source/timer progression and suspends existing nodes");
        Assert(!RebirthCamperEnergyRuntime.RequestMode(c.Vehicle,c.Player,RebirthCamperEnergyMode.Travel,out _),"pending recovery refuses mode request");
        RebirthCamperEnergyRuntime.CustodyReady=_=>true;Advance(c,4);c.Runtime.TryCopyBank(id,out var ready);
        long resumed=(((PowerGenerator)after).CurrentFuel-((PowerGenerator)ready).CurrentFuel)*11250L+after.CurrentPower-ready.CurrentPower;
        Assert(resumed==1250,"five seconds recovery gate refusal does not catch up resource debit");
        modules.TransferPending=true;station.lastTickTime=GameTimer.Instance.ticks;ulong prior=station.lastTickTime;
        c.Runtime.Hold(op);modules.TickStations();GameTimer.Instance.ticks+=200;c.Runtime.Release(op);modules.TickStations();
        Assert(modules.TransferPending&&station.Effects==0&&station.lastTickTime==0,"release does not clear another transfer hold");
    }
    private static void ReadOnlyHud()
    {
        var world=new World {Remote=true};var vehicle=new EntityVehicle {entityId=77,world=world};var player=new EntityPlayerLocal {entityId=78,world=world};
        world.Entities[77]=vehicle;world.Entities[78]=player;GameManager.Instance.World=world;
        Guid id=Guid.NewGuid();vehicle.vehicle.Item.SetMetadata("RebirthCamperIdentity",id.ToString("N"));
        vehicle.vehicle.Item.SetMetadata(RebirthCamperEnergyState.Key,Convert.ToBase64String(RebirthCamperEnergyState.Encode(new RebirthCamperEnergyState {Identity=id,Mode=RebirthCamperEnergyMode.Camp})));
        player.HitInfo=new Hit {bHitValid=true,tag="E_vehicle",transform=new Transform {Vehicle=vehicle}};
        var hud=new XUiC_RebirthVehicleHudStatBar {statType=HUDStatTypes.VehicleFuel};hud.xui.playerUI.entityPlayer=player;
        string before=RebirthNativeItemCodec.Encode(vehicle.vehicle.Item);int factories=RebirthCamperModuleRuntime.ForCalls;string value=null;
        Assert(hud.GetBindingValueInternal(ref value,"statcurrentwithmax")&&value=="345/1200","actual HUD binding uses native amount/max, not percent back-calculation");
        Assert(hud.GetBindingValueInternal(ref value,"rebirthvehiclefuelcaption")&&value=="FUEL \u00b7 CAMP","actual HUD displays genuine native-synced mode projection");
        Assert(factories==RebirthCamperModuleRuntime.ForCalls&&before==RebirthNativeItemCodec.Encode(vehicle.vehicle.Item)&&vehicle.SyncCount==0,"readonly HUD does not For/create/sync/mutate vehicle");
        RebirthCamperEnergyRuntime.SetRegistered(false);Assert(hud.GetBindingValueInternal(ref value,"rebirthvehiclefuelcaption")&&value=="FUEL","unregistered core adds no guessed mode");
    }
    private static void LeaseRetryBoundary()
    {
        var c=Start();Guid operation=Guid.NewGuid();Assert(c.Runtime.Hold(operation),"exact lease operation held");
        int writes=RebirthCamperEnergyDeviceLeaseStorage.Writes,requests=RebirthCamperSavedVehicle.Requests;
        byte[] after={1,2,3};c.Vehicle.ThrowNextSync=true;
        Throws(()=>c.Runtime.RequestDeviceLeasePersistence(operation,Array.Empty<byte>(),after,out _),"injected native sync failure retains live afterimage");
        Assert(RebirthCamperEnergyRuntime.ReadDeviceLease(c.Vehicle).SequenceEqual(after),"exact lease setter afterimage survived sync failure");
        Assert(c.Runtime.RequestDeviceLeasePersistence(operation,Array.Empty<byte>(),after,out var saved)&&saved!=null,"same-operation exact afterimage retry can request native save");
        Assert(RebirthCamperEnergyDeviceLeaseStorage.Writes==writes+1&&RebirthCamperSavedVehicle.Requests==requests+1,"retry did not write lease twice; native save boundary requested once");
        Assert(!c.Runtime.RequestDeviceLeasePersistence(Guid.NewGuid(),after,after,out _),"foreign operation refuses even matching afterimage");
        RebirthCamperEnergyDeviceLeaseStorage.Write(c.Vehicle.vehicle.Item,new byte[]{9});
        Assert(!c.Runtime.RequestDeviceLeasePersistence(operation,Array.Empty<byte>(),after,out _),"changed lease refuses overwrite");
        c.Runtime.Release(operation);
    }
    public static int Main()
    {
        try{FreshSourceManifest();Definitions();ConfigAndDriver();ResourceTiming();SolarBudget();SourceImagesAndRefusals();HoldsAndRecovery();LeaseRetryBoundary();ChargingCases();ReadOnlyHud();EstimateCases();WireCases();MaintenanceFrameCases();
            Console.WriteLine("PASS actual-source Energy assertions="+assertions+"; explicit native/world/custody/codec doubles. NOT actual-native paint ABI, actual multiplayer, live driver, UI layout or durable-save qualification.");return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
