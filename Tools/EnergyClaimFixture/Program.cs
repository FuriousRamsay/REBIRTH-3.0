using System.Reflection;
using UnityEngine;

static class Program
{
 static int count;
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
 static object Post(Type type,string method,params object[] args){type.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);return args[^1];}
 static void Main()
 {
  var world=new World();GameManager.Instance.World=world;
  var owner=new PersistentPlayerData{EntityId=1};var ally=new PersistentPlayerData{EntityId=2};var foreign=new PersistentPlayerData{EntityId=3};owner.Allies.Add(ally);
  GameManager.Instance.Players.Players["owner"]=owner;GameManager.Instance.Players.Players["foreign"]=foreign;
  owner.LPBlocks.Add(new Vector3i(90,0,90));
  var vehicle=new EntityVehicle{entityId=7,Owner="owner",world=world,position=new Vector3(.5f,.5f,.5f)};
  vehicle.vehicle.Item.Metadata["RebirthCamperIdentity"]=Guid.NewGuid().ToString("D");world.Entities.list.Add(vehicle);
  var inside=new Vector3i(6,6,4);var outside=new Vector3i(11,0,0);var above=new Vector3i(0,11,0);
  Check(RebirthCamperClaimIntegration.Resolve(world,inside,owner)==EnumLandClaimOwner.Self,"owner sphere interior");
  Check(RebirthCamperClaimIntegration.Resolve(world,inside,ally)==EnumLandClaimOwner.Ally,"ally inside");
  Check(RebirthCamperClaimIntegration.Resolve(world,inside,foreign)==EnumLandClaimOwner.Other,"foreign inside");
  Check(RebirthCamperClaimIntegration.Resolve(world,outside,owner)==EnumLandClaimOwner.None,"outside x");  RebirthCamperEnergyImmobility.Checks=0;
  Check(!RebirthCamperClaimIntegration.Contains(vehicle,outside),"outside rejected before pin check");
  Check(RebirthCamperEnergyImmobility.Checks==0,"outside avoids native pin observation");
  Check(RebirthCamperClaimIntegration.Contains(vehicle,inside)&&RebirthCamperEnergyImmobility.Checks==1,"inside retains pin observation");
  Check(RebirthCamperClaimIntegration.Resolve(world,above,owner)==EnumLandClaimOwner.None,"outside y not native infinite column");
  Check(RebirthCamperClaimIntegration.Resolve(world,new Vector3i(10,10,0),owner)==EnumLandClaimOwner.None,"sphere excludes former cube diagonal");
  Check(RebirthCamperClaimIntegration.Resolve(world,new Vector3i(10,0,0),owner)==EnumLandClaimOwner.Self,"sphere includes exact radius");
  Check((bool)Post(typeof(RebirthCamperClaimSpacing),"Postfix",world,new Vector3i(28,0,28),foreign,true),"native claim column diagonal outside sphere spacing");
  Check(!(bool)Post(typeof(RebirthCamperClaimSpacing),"Postfix",world,new Vector3i(27,0,27),foreign,true),"native claim column intersects sphere spacing");
  var pickup=typeof(World).GetMethod(nameof(World.CanPickupBlockAt))!;
  Check(!(bool)Post(typeof(RebirthCamperClaimPermissions),"Postfix",world,pickup,new object[]{inside,foreign},true),"foreign pickup refused");
  Check((bool)Post(typeof(RebirthCamperClaimPermissions),"Postfix",world,pickup,new object[]{inside,owner},true),"owner native allowed preserved");
  Check((bool)Post(typeof(RebirthCamperClaimPermissions),"Postfix",world,pickup,new object[]{inside,ally},true),"ally native allowed preserved");
  Check(!(bool)Post(typeof(RebirthCamperClaimPermissions),"Postfix",world,pickup,new object[]{inside,owner},false),"native refusal retained");
  Check((bool)Post(typeof(RebirthCamperClaimPermissions),"Postfix",world,pickup,new object[]{outside,foreign},true),"outside foreign ordinary native decision preserved");
  var mine=typeof(World).GetMethod(nameof(World.IsMyLandProtectedBlock))!;
  Check((bool)Post(typeof(RebirthCamperClaimPermissions),"Postfix",world,mine,new object[]{inside,owner},false),"actual owner pickup-area predicate gains Self");
  Check(!(bool)Post(typeof(RebirthCamperClaimPermissions),"Postfix",world,mine,new object[]{inside,ally},false),"ally is not fabricated as Self");
  world.PhysicalOwner=foreign;
  Check(RebirthCamperClaimIntegration.Resolve(world,inside,owner)==EnumLandClaimOwner.None,"foreign physical claim blocks overlay");
  Check((EnumLandClaimOwner)Post(typeof(RebirthCamperClaimOwnership),"Postfix",world,inside,owner,EnumLandClaimOwner.Other)==EnumLandClaimOwner.Other,"normal physical ownership unchanged");
  Check(!(bool)Post(typeof(RebirthCamperClaimPermissions),"Postfix",world,mine,new object[]{inside,owner},false),"foreign physical claim cannot grant owner pickup");
  world.PhysicalOwner=null;
  Check(RebirthCamperClaimIntegration.CombinedCount(world,owner)==2,"read-only physical plus mobile count");
  Check(owner.LPBlocks.Count==1&&owner.LPBlocks[0].x==90,"ordinary LPBlocks never inserted or evicted");
  RebirthCamperEnergyImmobility.Pinned=false;Check(!RebirthCamperClaimIntegration.Active(vehicle),"Camp unpinned refuses");RebirthCamperEnergyImmobility.Pinned=true;
  foreach(var mode in new[]{RebirthCamperEnergyMode.Travel,RebirthCamperEnergyMode.Packing,RebirthCamperEnergyMode.Deploying}){RebirthCamperEnergyRuntime.Mode=mode;Check(!RebirthCamperClaimIntegration.Active(vehicle),"nonCamp revoke "+mode);}
  RebirthCamperEnergyRuntime.Mode=RebirthCamperEnergyMode.Camp;
  world.Remote=true;var identity=vehicle.vehicle.Item.Metadata["RebirthCamperIdentity"];
  Check(!RebirthCamperClaimIntegration.Active(vehicle),"client no projection refuses");
  RebirthCamperClaimProjection.Receive(world,7,"fixture-world",identity,vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Camp,10,10,true,1);
  Check(RebirthCamperClaimIntegration.Active(vehicle),"client authenticated projection ready");
  Check(RebirthCamperClaimIntegration.Resolve(world,inside,owner)==EnumLandClaimOwner.Self,"client owner menu inside");
  Check(RebirthCamperClaimIntegration.Resolve(world,inside,ally)==EnumLandClaimOwner.Ally,"client ally menu inside");
  Check(RebirthCamperClaimIntegration.Resolve(world,inside,foreign)==EnumLandClaimOwner.Other,"client foreign menu inside");
  Check(RebirthCamperClaimIntegration.Resolve(world,outside,foreign)==EnumLandClaimOwner.None,"client outside sphere");
  RebirthCamperClaimProjection.Receive(world,7,"fixture-world",identity,vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Packing,10,10,false,2);
  Check(!RebirthCamperClaimProjection.Ready(vehicle),"server revoke");
  RebirthCamperClaimProjection.Receive(world,7,"fixture-world",identity,vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Camp,10,10,true,1);
  Check(!RebirthCamperClaimProjection.Ready(vehicle),"reordered older ready cannot undo revoke");
  RebirthCamperClaimProjection.Receive(world,7,"foreign-world",identity,vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Camp,10,10,true,3);
  Check(!RebirthCamperClaimProjection.Ready(vehicle),"foreign world refused");
  RebirthCamperClaimProjection.Receive(world,7,"fixture-world",Guid.NewGuid().ToString("D"),vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Camp,10,10,true,3);
  Check(!RebirthCamperClaimProjection.Ready(vehicle),"foreign camper identity refused");
  RebirthCamperClaimProjection.Receive(world,7,"fixture-world",identity,vehicle.position,new Quaternion(float.NaN,0,0,1),RebirthCamperEnergyMode.Camp,10,10,true,3);
  Check(!RebirthCamperClaimProjection.Ready(vehicle),"malformed native pose refused");
  RebirthCamperClaimProjection.Receive(world,7,"fixture-world",identity,vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Camp,10,10,true,3);
  Time.realtimeSinceStartup=16;Check(!RebirthCamperClaimProjection.Ready(vehicle),"client TTL expired");
  RebirthCamperClaimProjection.Receive(world,7,"fixture-world",identity,vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Camp,10,10,true,4);
  vehicle.position=new Vector3(2,.5f,.5f);Check(!RebirthCamperClaimProjection.Ready(vehicle),"changed pose refuses");vehicle.position=new Vector3(.5f,.5f,.5f);
  RebirthCamperEnergyRuntime.Revision=11;Check(RebirthCamperClaimProjection.Ready(vehicle),"newer resource revision does not revoke pin");
  RebirthCamperEnergyRuntime.Mode=RebirthCamperEnergyMode.Travel;Check(!RebirthCamperClaimProjection.Ready(vehicle),"native Travel immediate revoke");RebirthCamperEnergyRuntime.Mode=RebirthCamperEnergyMode.Camp;
  RebirthCamperClaimProjection.Revoke(vehicle);Check(!RebirthCamperClaimProjection.Ready(vehicle),"teardown clears client cache");
  world.Remote=false;RebirthCamperEnergyImmobility.Pinned=false;
  RebirthCamperClaimProjection.Receive(world,7,"fixture-world",identity,vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Camp,11,10,true,999);
  Check(!RebirthCamperClaimIntegration.Active(vehicle),"client payload cannot create server pin");
  Check(SingletonMonoBehaviour<ConnectionManager>.Instance.Sent.Count==0,"query and refused payload never send/debit/save");
  Check(owner.LPBlocks.Count==1&&world.PhysicalOwner==null,"all checks preserve native physical map");
  Console.WriteLine($"PASS {count} actual linked claim/projection assertions; native integration still parent-qualified");
 }
}
