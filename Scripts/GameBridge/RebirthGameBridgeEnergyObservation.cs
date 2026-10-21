using System;
using Newtonsoft.Json.Linq;

// Runs only through the existing authenticated, main-thread bridge dispatcher.
public static class RebirthGameBridgeEnergyObservation
{
    public static void Handle(BridgeRequest req)
    {
        if(req.Method!="GET"){req.Fail("Energy observation is GET only.",405);return;}
        var world=GameManager.Instance?.World;
        var vehicle=world?.GetEntity(req.QueryInt("entity",-1)) as EntityVehicle;
        if(!ThreadManager.IsMainThread()||world==null||!RebirthCamperRadial.IsCamper(vehicle)||!ReferenceEquals(vehicle.world,world))
        {req.Fail("Current loaded camper required.",409);return;}
        var snapshot=RebirthCamperEnergyRuntime.Observe(vehicle);
        if(snapshot==null){req.Fail("Registered existing Energy state unavailable; observation creates no state.",409);return;}
        var sources=new JArray();
        foreach(var source in snapshot.Sources)
            sources.Add(new JObject{["id"]=source.Id.ToString("D"),["position"]=source.Position,["kind"]=source.Kind.ToString(),["enabled"]=source.IsOn,
                ["status"]=source.Status.ToString(),["fuel"]=source.CurrentFuel,["maxFuel"]=source.MaxFuel,
                ["charge"]=source.CurrentPower,["nativeMaxOutput"]=source.MaxOutput,["availableWatts"]=source.AvailableWatts,
                ["installed"]=Slots(new[]{source.Installed}),["components"]=Slots(source.Components)});
        var poi=RebirthCamperEnergyDeviceAdapter.ObservePoi(vehicle);
        var relayFrame=RebirthCamperRelayWiring.Read(vehicle);int externalLinks=0;
        if(relayFrame!=null)foreach(var link in relayFrame.Links)if(!link.Mounted)externalLinks++;
        string identity;vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out identity);
        var claimOwner=RebirthCamperClaimIntegration.OwnerOf(vehicle);
        var localPlayer=world.GetPrimaryPlayer();
        var localClaimRelative=localPlayer==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(localPlayer.entityId);
        JObject placementProbe=null;
        if(req.QueryString("x")!=null&&req.QueryString("y")!=null&&req.QueryString("z")!=null)
        {
            var position=new Vector3i(req.QueryInt("x",0),req.QueryInt("y",0),req.QueryInt("z",0));
            var value=world.GetBlock(position);
            placementProbe=new JObject{["position"]=new JArray(position.x,position.y,position.z),
                ["block"]=value.Block?.GetBlockName(),["nativeTilePresent"]=world.GetTileEntity(position)!=null,
                ["placedMarkerPresent"]=RebirthPlacedWorkmanshipIdentity.Read(world,position)!=null,
                ["insideCurrentCamperClaim"]=RebirthCamperClaimIntegration.Contains(vehicle,position),
                ["nativeClaimRelativeToLocal"]=world.GetLandClaimOwner(position,localClaimRelative).ToString()};
        }
        req.Complete(new JObject{["schema"]=1,["observationOnly"]=true,["entity"]=vehicle.entityId,
            ["authoritative"]=!world.IsRemote(),["registered"]=RebirthCamperEnergyRuntime.IsRegistered,
            ["vehicleIdentity"]=identity,["position"]=new JArray(vehicle.position.x,vehicle.position.y,vehicle.position.z),
            ["mobility"]=RebirthGameBridgeEnergyMobilityObservation.Snapshot(vehicle),
            ["engine"]=RebirthGameBridgeEngineObservation.Snapshot(vehicle),
            ["held"]=RebirthCamperEnergyRuntime.IsHeld(vehicle),["mode"]=snapshot.Mode.ToString(),
            ["transitionSeconds"]=snapshot.TransitionSeconds,["revision"]=snapshot.Revision,["range"]=snapshot.Range,
            ["demandWatts"]=snapshot.DemandWatts,["suppliedWatts"]=snapshot.SuppliedWatts,["availableWatts"]=snapshot.AvailableWatts,
            ["sources"]=sources,["poi"]=new JObject{["authoritativeKnown"]=!world.IsRemote(),["inRange"]=poi.InRange,["name"]=poi.PoiName,["requestedOn"]=poi.IsOn,
                ["supportedLights"]=poi.SupportedLights,["range"]=poi.Range,["activeMeteredDemand"]=poi.DemandWatts},
            ["claim"]=new JObject{["scope"]=RebirthCamperClaimIntegration.Scope.ToString(),["ready"]=RebirthCamperClaimIntegration.Active(vehicle),
                ["vehicleOwnerIdentityPresent"]=vehicle.GetOwner()!=null,["ownerPersistentPresent"]=claimOwner!=null,
                ["localPlayerPersistentPresent"]=localClaimRelative!=null,["ownerMatchesLocal"]=claimOwner!=null&&ReferenceEquals(claimOwner,localClaimRelative),
                ["readinessSource"]=world.IsRemote()?"server-channel pin projection":"actual native pin",
                ["physicalOwnerCount"]=claimOwner?.LPBlocks.Count??0,["mobileOwnerCount"]=RebirthCamperClaimIntegration.CountActive(world,claimOwner),
                ["combinedOwnerCount"]=RebirthCamperClaimIntegration.CombinedCount(world,claimOwner),
                ["nativeOwnerAtCenter"]=world.GetLandClaimOwner(vehicle.GetBlockPosition(),localClaimRelative).ToString(),
                ["physicalLimitPolicy"]= "unchanged; human count-limit decision pending"},
            ["placementProbe"]=placementProbe,
            ["externalRelayLinkCount"]=relayFrame==null?JValue.CreateNull():new JValue(externalLinks),
            ["devices"]=RebirthCamperEnergyDeviceAdapter.ObserveDevices(vehicle,Math.Max(1,Math.Min(512,req.QueryInt("limit",256)))),
            ["qualificationScope"]=world.IsRemote()?"source projection only; native device authority unavailable":"native current loaded state; not chunk-save durability or attack hit proof"});
    }
    private static JArray Slots(ItemStack[] slots)
    {
        var result=new JArray();using(var hash=System.Security.Cryptography.SHA256.Create())
            for(int i=0;i<slots.Length;i++)
            {
                var stack=slots[i];bool empty=stack==null||stack.IsEmpty();
                byte[] native=empty?null:RebirthCamperInventorySnapshot.Capture(new[]{stack.Clone()});
                result.Add(new JObject{["slot"]=i,["count"]=empty?0:stack.count,
                    ["useTimes"]=empty?JValue.CreateNull():new JValue(stack.itemValue.UseTimes),
                    ["maxUseTimes"]=empty?JValue.CreateNull():new JValue(stack.itemValue.MaxUseTimes),
                    ["name"]=empty?null:stack.itemValue.ItemClass.GetItemName(),
                    ["nativeHash"]=empty?null:Convert.ToBase64String(hash.ComputeHash(native)),
                    ["nativeData"]=empty?null:Convert.ToBase64String(native)});
            }
        return result;
    }
}
