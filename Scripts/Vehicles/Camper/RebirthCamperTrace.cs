using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class RebirthCamperTrace
{
    private static JArray Inventory(ItemStack[] slots)
    {
        var result = new JArray();
        using (var hash = System.Security.Cryptography.SHA256.Create())
            for (int slot = 0; slots != null && slot < slots.Length; slot++)
            {
                var stack = slots[slot];
                bool empty = stack == null || stack.IsEmpty();
                result.Add(new JObject { ["slot"] = slot,
                    ["name"] = empty ? null : stack.itemValue.ItemClass.GetItemName(),
                    ["count"] = empty ? 0 : stack.count,
                    ["nativeHash"] = empty ? null : Convert.ToBase64String(hash.ComputeHash(
                        RebirthCamperInventorySnapshot.Capture(new[] { stack.Clone() }))) });
            }
        return result;
    }
    public static bool Enabled;
    private static JObject RecipeAdmission(Block block)
    {
        if(block==null)return null;
        string raw=block.Properties.Contains("Workstation","CraftingAreaRecipes")?
            block.Properties.GetString("Workstation","CraftingAreaRecipes"):block.GetBlockName();
        var areas=new JArray();
        if(!string.IsNullOrEmpty(raw))foreach(string area in raw.Split(','))areas.Add(area.Trim()=="player"?"":area.Trim());
        return new JObject{["block"]=block.GetBlockName(),["rawAreas"]=raw,["normalizedAreas"]=areas};
    }
    private static readonly Queue<JObject> events=new Queue<JObject>();
    public static void Write(string stage,EntityVehicle vehicle=null,string detail=null)
    {
        if(!Enabled)return;
        var entry=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["stage"]=stage,["vehicle"]=vehicle?.entityId??-1,["detail"]=detail??""};
        if(vehicle?.vehicle!=null)
        {
            string flag;vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperTransferPending",out flag);
            entry["metadataPending"]=flag;entry["lockOwner"]=RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle);
            entry["cargoSlots"]=vehicle.bag?.ItemGrid?.items.Length??-1;
        }
        events.Enqueue(entry);while(events.Count>256)events.Dequeue();
        Log.Out("[CamperTrace] "+entry.ToString(Newtonsoft.Json.Formatting.None));
    }
    public static JObject Snapshot(EntityVehicle vehicle)
    {
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        var player=vehicle.world?.GetPrimaryPlayer() as EntityPlayerLocal;
        var held=player==null?null:LocalPlayerUI.GetUIForPlayer(player)?.xui?.DragAndDropWindow;
        var timer=GameTimer.Instance;ulong nativeTicks=timer.ticks,nativeLoadedTicks=timer.ticksSincePlayfieldLoaded;
        var result=new JObject{["events"]=new JArray(events),["entity"]=vehicle.entityId,["pending"]=runtime.TransferPending,
            ["capacity"]=runtime.Storage.Capacity,["cargoSlots"]=vehicle.bag.ItemGrid.items.Length,
            ["lockOwner"]=RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle),
            ["inventorySchema"]=1,["vehicleIdentity"]=runtime.Identity.ToString("N"),
            ["nativeGameTicks"]=nativeTicks,["nativeTicksSinceWorldLoaded"]=nativeLoadedTicks,
            ["nativeClockAtWorldLoaded"]=nativeTicks>=nativeLoadedTicks?nativeTicks-nativeLoadedTicks:0,
            ["cargo"]=Inventory(vehicle.bag.ItemGrid.items)};
        result["cursorEmpty"]=held==null?JValue.CreateNull():new JValue(held.IsEmpty());
        result["localPlayerEntityId"]=player?.entityId??-1;
        result["localPlayerAttachedEntityId"]=player?.AttachedToEntity?.entityId??-1;
        result["vehicleDriverEntityId"]=vehicle.GetAttached(0)?.entityId??-1;
        result["presentation"]=Presentation(vehicle,player);
        var storage=new JArray();var stations=new JArray();int queuedJobs=0;
        for(int i=0;i<20;i++)
        {
            var item=runtime.Storage.GetSlot(i);if(item!=null)storage.Add(new JObject{["slot"]=i,["item"]=item.itemValue.ItemClass.GetItemName()});
            var installed=runtime.Stations.GetSlot(i);if(installed==null)continue;
            var queue=new JArray();foreach(var job in installed.State.Queue)if(job?.Recipe!=null)
                queue.Add(new JObject{["recipe"]=job.Recipe.GetName(),["remaining"]=job.CraftingTimeLeft,["count"]=job.Multiplier});
            queuedJobs+=queue.Count;
            stations.Add(new JObject{["slot"]=i,["id"]=installed.Id.ToString("N"),["block"]=installed.State.InstalledBlock.Block.GetBlockName(),["queue"]=queue,
                ["recipeAdmission"]=RecipeAdmission(installed.State.block),
                ["queueCapacity"]=installed.State.Queue?.Length??0,["outputCapacity"]=installed.State.Output?.Length??0,["fuelSeconds"]=installed.State.BurnTotalTimeLeft,
                ["burning"]=installed.State.IsBurning,["besideWater"]=installed.State.isBesideWater,
                ["windowLease"]=RebirthCamperStationWindowSession.IsInUse(installed.State),
                ["gridHeld"]=RebirthCamperStationGridCustody.IsHeld(installed.State),
                ["actorPaymentHeld"]=RebirthCamperStationNativeActorPayment.IsHeld(installed.State),
                ["fuelOriginalHeld"]=RebirthCamperStationFuelOriginal.IsHeld(installed.State),
                ["savePending"]=RebirthCamperStationSaveEvidence.IsPending(installed.State),
                ["productiveGridHeld"]=RebirthProductiveEarnedCoordinator.BlocksMobileGrid(installed.State),
                ["completionHeld"]=RebirthCamperStationCompletionCallsite.IsHeld(installed.State),
                ["cancellationHeld"]=RebirthCamperStationCancellationRemoval.IsHeld(installed.State),
                ["queuedCanProcess"]=RebirthCamperStationQueuedAuthority.CanProcessNativeQueue(installed.State),
                ["tools"]=Inventory(installed.State.Tools),
                ["fuel"]=Inventory(installed.State.Fuel),["input"]=Inventory(installed.State.Input),
                ["output"]=Inventory(installed.State.Output)});
        }
        result["storage"]=storage;result["stations"]=stations;
        result["storageCount"]=storage.Count;result["stationCount"]=stations.Count;
        result["queuedJobCount"]=queuedJobs;
        return result;
    }
    // Explicit diagnostic query only: no per-tick capture, material edits or
    // input injection. Report effective native state, not requested UI state.
    private static JObject Presentation(EntityVehicle vehicle,EntityPlayerLocal player)
    {
        var root=vehicle.RootTransform;var model=vehicle.ModelTransform;
        var fp=player?.vp_FPController;var controller=fp?.CharacterController;
        var localFeet=root!=null&&controller!=null?root.InverseTransformPoint(controller.transform.TransformPoint(
            controller.center-Vector3.up*(controller.height*.5f))):Vector3.zero;
        var ground=fp?.m_GroundHit.collider;
        var proxy=model?.GetComponent<BoxCollider>();
        bool modeKnown=RebirthCamperEnergyRuntime.TryObservePresentation(vehicle,out var mode,out _,out _,out _);
        var result=new JObject{["modeKnown"]=modeKnown,["mode"]=modeKnown?mode.ToString():"unknown",
            ["bodyInstalled"]=RebirthCamperBodyAttachment.HasBody(vehicle),
            ["playerFeetLocalX"]=localFeet.x,["playerFeetLocalY"]=localFeet.y,["playerFeetLocalZ"]=localFeet.z,
            ["playerGrounded"]=fp?.Grounded??false,["playerOnLadder"]=player?.bInElevator??false,
            ["playerSupportedByVehicle"]=ground!=null&&root!=null&&(ground.transform==root||ground.transform.IsChildOf(root)),
            ["selectionProxyIgnoresController"]=proxy!=null&&controller!=null&&Physics.GetIgnoreCollision(proxy,controller),
            ["model14Physics21Ignored"]=Physics.GetIgnoreLayerCollision(14,21),
            ["forwardVelocity"]=vehicle.vehicle.CurrentForwardVelocity,["braking"]=vehicle.vehicle.CurrentIsBreak};
        if(root!=null)
        {
            result["hoodApproach"]=Point(root.TransformPoint(new Vector3(0,0,3.6f))+Origin.position);
            result["hoodAim"]=Point(root.TransformPoint(new Vector3(0,1.1f,1.6f))+Origin.position);
            foreach(var volume in root.GetComponentsInChildren<BoxCollider>(true))
                if(volume.name=="REBIRTHFrontLadderClimbVolume")
                {
                    var center=root.InverseTransformPoint(volume.transform.TransformPoint(volume.center));
                    result["ladderApproach"]=Point(root.TransformPoint(new Vector3(center.x<0?-1.65f:1.65f,0,center.z))+Origin.position);
                    result["ladderAim"]=Point(volume.transform.TransformPoint(volume.center)+Origin.position);break;
                }
        }
        var relays=RebirthCamperRelayVisuals.Group(vehicle);int count=0,active=0,enabled=0;float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
        float minOpacity=float.PositiveInfinity,maxOpacity=float.NegativeInfinity;int renderers=0;
        if(relays!=null)
        {
            foreach(var target in relays.GetComponentsInChildren<RebirthCamperWireTarget>(true))
            {
                if(target.Kind!=0)continue;count++;if(target.gameObject.activeInHierarchy)active++;
                var collider=target.GetComponent<Collider>();if(collider!=null&&collider.enabled&&collider.gameObject.activeInHierarchy)enabled++;
                float y=root.InverseTransformPoint(target.transform.parent.position).y;minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
            }
            var block=new MaterialPropertyBlock();int property=Shader.PropertyToID("_CampVisibility");
            foreach(var renderer in relays.GetComponentsInChildren<Renderer>(true))
            {
                var material=renderer.sharedMaterial;if(material==null||!material.HasProperty(property))continue;
                block.Clear();renderer.GetPropertyBlock(block);float opacity=block.HasFloat(property)?block.GetFloat(property):material.GetFloat(property);
                renderers++;minOpacity=Mathf.Min(minOpacity,opacity);maxOpacity=Mathf.Max(maxOpacity,opacity);
            }
        }
        result["relayCount"]=count;result["relayActiveEndpointCount"]=active;result["relayEnabledEndpointCount"]=enabled;
        result["relayPlateMinY"]=count>0?minY:0;result["relayPlateMaxY"]=count>0?maxY:0;
        result["relayFadeRendererCount"]=renderers;result["relayMinOpacity"]=renderers>0?minOpacity:0;result["relayMaxOpacity"]=renderers>0?maxOpacity:0;
        result["stationFadeOpacity"]=-1;
        if(root!=null)foreach(var node in root.GetComponentsInChildren<Transform>(true))
        {
            if(node.name!="REBIRTHCampRoofStations")continue;
            foreach(var renderer in node.GetComponentsInChildren<Renderer>(true))
            {
                var material=renderer.sharedMaterial;if(material==null||!material.HasProperty("_CampVisibility"))continue;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                result["stationFadeOpacity"]=block.HasFloat(Shader.PropertyToID("_CampVisibility"))?block.GetFloat("_CampVisibility"):material.GetFloat("_CampVisibility");break;
            }
            break;
        }
        var lamps=new JArray();int emitting=0;float maxBeam=0,maxRange=0,maxAngle=0;
        if(model!=null)foreach(var light in model.GetComponentsInChildren<Light>(true))
        {
            bool rear=light.name=="LeftRed"||light.name=="RightRed"||light.name=="REBIRTH pickup rear beam";
            if(!rear)continue;bool live=light.enabled&&light.gameObject.activeInHierarchy;float effective=live?light.intensity:0;
            if(effective>0){emitting++;maxBeam=Mathf.Max(maxBeam,effective);maxRange=Mathf.Max(maxRange,light.range);maxAngle=Mathf.Max(maxAngle,light.spotAngle);}
            lamps.Add(new JObject{["name"]=light.name,["active"]=light.gameObject.activeInHierarchy,["enabled"]=light.enabled,
                ["intensity"]=light.intensity,["effectiveIntensity"]=effective,["range"]=light.range,["spotAngle"]=light.spotAngle,
                ["red"]=light.color.r,["green"]=light.color.g,["blue"]=light.color.b});
        }
        result["rearLights"]=lamps;result["rearEmittingCount"]=emitting;result["rearMaxEffectiveIntensity"]=maxBeam;
        result["rearMaxRange"]=maxRange;result["rearMaxSpotAngle"]=maxAngle;
        return result;
    }
    private static JObject Point(Vector3 point)=>new JObject{["x"]=point.x,["y"]=point.y,["z"]=point.z};
}
