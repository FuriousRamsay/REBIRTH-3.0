#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperVehicleIdentity.cs')).Replace('using System;','')
$camperFixture=@'
using System;
using System.Collections.Generic;
public static class ThreadManager { public static bool Main=true;public static bool IsMainThread()=>Main; }
public class ItemValue { public string Identity;public bool TryGetMetadata(string key,out string text){text=Identity;return text!=null;} }
public class Vehicle { public string Name="rebirthCamper80s";public ItemValue Item=new ItemValue();public string GetName()=>Name;public ItemValue GetUpdatedItemValue()=>Item; }
public class Entity { public int entityId; }
public class EntityVehicle:Entity { public World world;public Vehicle vehicle=new Vehicle(); }
public class EntityList { public List<Entity> list=new List<Entity>(); }
public class World { public bool Remote;public EntityList Entities=new EntityList();public bool IsRemote()=>Remote;public Entity GetEntity(int id)=>Entities.list.Find(e=>e.entityId==id); }
public class GameManager { public static GameManager Instance=new GameManager();public World World; }
public static class VehicleIdentityFixture {
 private static void Check(bool v,string n){if(!v)throw new Exception(n);}
 public static void Run(){
  var id=Guid.NewGuid();var world=new World();GameManager.Instance.World=world;var truck=new EntityVehicle{entityId=10,world=world};truck.vehicle.Item.Identity=id.ToString("N");world.Entities.list.Add(truck);
  Check(RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"unique live identity");
  truck.entityId=11;Check(RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"temporary numeric ID irrelevant to GUID identity");
  var other=new EntityVehicle{entityId=12,world=world};other.vehicle.Item.Identity=id.ToString("D");world.Entities.list.Add(other);
  Check(!RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"cloned GUID denied regardless of formatting");other.vehicle.Item.Identity=Guid.NewGuid().ToString();Check(RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"distinct truck permitted");
  world.Entities.list.Add(truck);Check(!RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"duplicate list reference denied");world.Entities.list.RemoveAt(2);
  truck.vehicle.Item.Identity="bad";Check(!RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"malformed identity denied");truck.vehicle.Item.Identity=id.ToString();
  world.Remote=true;Check(!RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"remote authority denied");world.Remote=false;
  GameManager.Instance.World=new World();Check(!RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"old world denied");GameManager.Instance.World=world;
  ThreadManager.Main=false;Check(!RebirthCamperVehicleIdentity.IsUniqueCurrent(truck,id),"off-thread denied");
  Console.WriteLine("PASS actual live camper GUID check: unique identity, numeric-ID independence, GUID formatting/duplicates, malformed metadata, authority world and thread. Native entities doubled; unloaded saved duplicates and gameplay unproven.");
 }
}
'@
Add-Type -TypeDefinition ($camperFixture+"`n"+$camperSource)
[VehicleIdentityFixture]::Run()
