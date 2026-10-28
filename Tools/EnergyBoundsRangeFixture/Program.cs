using UnityEngine;
using System.Reflection;
static class Program
{
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static void Main()
    {
        string fixture=System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>().Single(a=>a.Key=="FixtureSourceRoot").Value;
        using var manifest=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture,"generated/estimate_manifest.json")));
        Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(manifest.RootElement.GetProperty("source").GetString())))==manifest.RootElement.GetProperty("sourceHash").GetString(),"current production enum source fingerprint");
        Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture,"generated/ActualEstimateStatus.cs"))))==manifest.RootElement.GetProperty("generatedHash").GetString(),"exact extracted enum fingerprint");
        var world=new World();GameManager.Instance.World=world;
        var owner=new PersistentPlayerData {PrimaryId="owner",EntityId=1};var viewer=new PersistentPlayerData {PrimaryId="viewer",EntityId=2};
        GameManager.Instance.Players.Entries["owner"]=owner;GameManager.Instance.Players.Entries["viewer"]=viewer;
        var actor=new EntityPlayer {entityId=1,world=world};world.Entities[1]=actor;
        var vehicle=new EntityVehicle {entityId=7,world=world};world.Entities[7]=vehicle;
        var record=new RebirthWorldCharacterRecord {StablePlayerKey="owner"};RebirthWorldCharacterRepository.Cache["owner"]=record;
        RebirthWorldCharacterRepository.Cache["viewer"]=new(){StablePlayerKey="viewer",Origin=new(){BackgroundId="background.engineer"}};
        Check(RebirthCamperEnergyConfiguration.Current.Range==10&&RebirthCamperEnergyConfiguration.Current.EngineerRange==20,"approved normal10 Engineer20 defaults");
        Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"Engineer viewer does not increase ordinary owner radius");
        record.Origin.BackgroundId="background.engineer";Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==20,"actual cached Engineer owner doubles radius");
        Check(RebirthCamperEnergyBounds.Contains(vehicle.position,new Vector3(20,0,0),20),"Engineer exact radius included");
        Check(!RebirthCamperEnergyBounds.Contains(vehicle.position,new Vector3(20,20,0),20),"Engineer radius remains sphere not cube");
        record.Origin.BackgroundId="background.electrician";Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"Electrician does not obtain Engineer width");
        Check(RebirthCamperEnergyCapacity.ForActor(actor)==6,"actual current Electrician actor obtains six source positions");
        record.IsComplete=false;Check(RebirthCamperEnergyCapacity.ForActor(actor)==3,"incomplete Electrician does not obtain capacity");record.IsComplete=true;
        record.Origin.BackgroundId="background.maintenance_technician";Check(RebirthCamperEnergyCapacity.ForActor(actor)==3&&RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"Maintenance does not inherit Engineer/Electrician capacity/range");
        record.Origin.BackgroundId="background.engineer ";Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"noncanonical background rejected");
        record.Origin.BackgroundId="background.engineer";record.IsComplete=false;Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"incomplete cache does not qualify");record.IsComplete=true;
        record.Current=false;Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"stale cached object does not qualify");record.Current=true;
        record.StablePlayerKey="foreign";RebirthWorldCharacterRepository.Cache["foreign"]=record;Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"foreign stable key does not qualify even when cached current");record.StablePlayerKey="owner";RebirthWorldCharacterRepository.Cache.Remove("foreign");
        RebirthCharacterCreationHoldService.Held.Add(actor);Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"current owner creation hold refuses boost");RebirthCharacterCreationHoldService.Held.Clear();
        world.Entities.Remove(1);Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==20,"offline owner exact current cache retained without viewer substitution");
        vehicle.Owner="missing";Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==10,"unresolved owner never receives boost");vehicle.Owner="owner";
        world.Remote=true;RebirthCamperClaimProjection.Available=false;Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==0,"client never guesses cached Engineer radius");
        RebirthCamperClaimProjection.Available=true;RebirthCamperClaimProjection.Radius=20;Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==20,"client uses projected server radius only");
        world.Remote=false;world.Entities.Remove(7);Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==0,"stale vehicle reference refuses range");world.Entities[7]=vehicle;
        RebirthCamperEnergyConfiguration.Configure(20,20,12,1,3,new[]{RebirthCamperEnergySourceKind.Solar,RebirthCamperEnergySourceKind.Generator,RebirthCamperEnergySourceKind.Battery});
        Check(RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)==24,"Engineer remains exactly twice configured normal radius");
        Console.WriteLine("PASS "+checks+" actual linked policy/model/geometry assertions; native/cache/network dependencies are explicit doubles.");
    }
}
