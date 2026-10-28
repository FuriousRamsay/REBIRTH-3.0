using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static partial class RebirthCamperEnergyDeviceAdapter
{
    private static bool PoiPositionAuthorized(EntityVehicle vehicle,Vector3i position)
    {
        if(vehicle?.world==null||vehicle.world.IsWithinTraderArea(position))return false;
        var owner=vehicle.GetOwner();
        var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
        if(owner!=null&&player==null)return false;
        var claim=vehicle.world.GetLandClaimOwner(position,player);
        return claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self;
    }
    private static void PreparePoiLights(EntityVehicle vehicle,PrefabInstance poi)
    {
        if(poi==null||!Authority(vehicle))return;
        var cache=poiLookups.GetValue(vehicle,_=>new PoiLookupCache());
        float now=Time.realtimeSinceStartup;if(now<cache.NextMigration)return;
        cache.NextMigration=now+1f;
        RebirthCamperWorldLighting.MigratePoi(vehicle.world,poi);
        RebirthCamperWorldLighting.AdmitPlacedPoiLights(vehicle.world,poi,p=>PoiPositionAuthorized(vehicle,p));
    }
    private static RebirthCamperPoiLightChoices.Plan SelectPoiLights(EntityVehicle vehicle,PrefabInstance poi,int count)
        =>RebirthCamperPoiLightChoices.Select(poi==null?Array.Empty<Vector3i>():RankedPoiLights(vehicle,poi).Where(p=>CurrentPoiPoolPosition(vehicle,poi,p)),
            position=>RebirthCamperPoiLightChoices.Read(vehicle.world,vehicle.world.GetTileEntity(position)),count);
    private static bool CurrentPoiPoolPosition(EntityVehicle vehicle,PrefabInstance poi,Vector3i position)
    {
        if(!Inside(poi,position))return false;
        var tile=vehicle.world.GetTileEntity(position);
        if(tile is TileEntityLight)
        {
            if(!(vehicle.world.GetBlock(position).Block is BlockLight)||RebirthCamperWorldLighting.Stamp(tile)==0)return false;
            return sessions.TryGetValue(vehicle,out var session)?PlainAuthorized(session,position):PoiPositionAuthorized(vehicle,position);
        }
        return PowerManager.HasInstance&&PowerManager.Instance.PowerItemDictionary.TryGetValue(position,out var node)&&
            node!=null&&ReferenceEquals(node.TileEntity,tile)&&Eligible(node)&&PoiPoolLight(node)&&PoiOwnerAuthorized(vehicle,node);
    }
    private static bool PoiPoolLight(PowerItem node)
        =>Light(node)&&(!node.TileEntity.IsPlayerPlaced||RebirthCamperWorldLighting.Stamp(node.TileEntity)!=0);
    private static void InvalidatePoiLights(EntityVehicle vehicle)
    {
        if(poiLookups.TryGetValue(vehicle,out var cache))
        {cache.LightNodes=null;cache.NodesUntil=cache.RankedUntil=cache.NextMigration=0;}
        if(sessions.TryGetValue(vehicle,out var session))session.NextPoiObservation=0;
        poiPowerGates.Remove(vehicle);
    }
    // Native light actions have their own reach, rather than requiring the actor
    // to stand beside the camper's Energy window. The same native owner must act.
    internal static bool TryPoiLightInteraction(EntityPlayer actor,Vector3i position,out EntityVehicle vehicle)
    {
        vehicle=null;var world=actor?.world;
        if(!ThreadManager.IsMainThread()||world==null||actor.IsDead()||world.IsEditor()||
            !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
            RebirthPlacedOriginal.IsPendingTarget(world,position)||world.IsWithinTraderArea(position))return false;
        var block=world.GetBlock(position);
        if(block.ischild||!(block.Block is BlockLight||block.Block is BlockPoweredLight))return false;
        var eye=actor.position+Vector3.up*actor.GetEyeHeight();var delta=position.ToVector3Center()-eye;
        if(delta.sqrMagnitude>(Constants.cDigAndBuildDistance+1f)*(Constants.cDigAndBuildDistance+1f))return false;
        var player=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId);
        if(player==null)return false;
        var claim=world.GetLandClaimOwner(position,player);
        if(claim!=EnumLandClaimOwner.None&&claim!=EnumLandClaimOwner.Self)return false;
        float best=float.MaxValue;
        foreach(var entity in world.Entities.list)
        {
            if(!(entity is EntityVehicle candidate)||candidate.IsDead()||!RebirthCamperRadial.IsCamper(candidate)||
                !RebirthCamperBodyAttachment.HasBody(candidate)||!ReferenceEquals(world.GetEntity(candidate.entityId),candidate))continue;
            var owner=candidate.GetOwner();
            if(owner==null||!owner.Equals(player.PrimaryId)&&!owner.Equals(player.NativeId)||
                !RebirthCamperEnergyRuntime.TryObserveMode(candidate,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Camp)continue;
            var poi=NearestPoi(candidate);if(poi==null||!Inside(poi,position)||!PoiPositionAuthorized(candidate,position))continue;
            if(!world.IsRemote())
            {
                if(!Authority(candidate))continue;
                var tile=world.GetTileEntity(position);
                if(tile is TileEntityPowered powered)
                {
                    var node=powered.GetPowerItem();
                    if(node==null||!Eligible(node)||!Light(node)||!PoiOwnerAuthorized(candidate,node)||
                        node.IsPowered&&!owners.ContainsKey(node))continue;
                    // A current native placement flag can precede the periodic
                    // footprint pass. Admit only this validated target now.
                    if(RebirthCamperWorldLighting.Stamp(tile)==0)
                        RebirthCamperWorldLighting.AdmitPlacedPoiTarget(world,poi,tile,p=>PoiPositionAuthorized(candidate,p));
                    if(!PoiPoolLight(node))continue;
                }
                else if(!(tile is TileEntityLight))continue;
            }
            float distance=(candidate.position-position.ToVector3Center()).sqrMagnitude;
            if(distance<best){best=distance;vehicle=candidate;}
        }
        return vehicle!=null;
    }
    private static bool ManualPoiLightVisible(EntityPlayer actor,World world,Vector3i position,Vector3 eye,Vector3 centerDelta)
    {
        // Keep the original center visibility path. Thin wall/ceiling fixtures can have no mesh there.
        if(Voxel.Raycast(world,new Ray(eye,centerDelta.normalized),centerDelta.magnitude+.1f,-538750989,8,0)&&
            Voxel.voxelRayHitInfo.hit.blockPos==position)return true;
        float reach=Constants.cDigAndBuildDistance+1f;
        Ray look;
        try { look=actor.GetLookRay(); } catch(Exception) { return false; }
        if(!Finite(look.origin.x)||!Finite(look.origin.y)||!Finite(look.origin.z)||
            !Finite(look.direction.x)||!Finite(look.direction.y)||!Finite(look.direction.z)||
            !Finite(look.direction.sqrMagnitude)||look.direction.sqrMagnitude<=.000001f)return false;
        if(!Voxel.Raycast(world,look,reach,-538750989,8,0)||
            Voxel.voxelRayHitInfo.hit.blockPos!=position)return false;
        // Native hit.pos is world-space. Prove reach and visibility again FROM THE ACTOR'S EYE,
        // rather than trusting a third-person camera that can see around an obstruction.
        var surfaceDelta=Voxel.voxelRayHitInfo.hit.pos-eye;
        if(!Finite(surfaceDelta.x)||!Finite(surfaceDelta.y)||!Finite(surfaceDelta.z)||
            surfaceDelta.sqrMagnitude<=.000001f||surfaceDelta.sqrMagnitude>reach*reach)return false;
        return Voxel.Raycast(world,new Ray(eye,surfaceDelta.normalized),surfaceDelta.magnitude+.1f,-538750989,8,0)&&
            Voxel.voxelRayHitInfo.hit.blockPos==position;
    }
    internal static bool SetManualPoiLight(EntityPlayer actor,EntityVehicle vehicle,Vector3i position,bool on,
        ushort expectedBlock,byte expectedRotation,uint expectedStamp,out string status)
    {
        status="xuiCamperWireDenied";
        if(!Authority(vehicle)||!TryPoiLightInteraction(actor,position,out var admitted)||!ReferenceEquals(admitted,vehicle))return false;
        // Manual input may change a light only while the POI bank is already ON.
        // Reject stale remote requests before choice/session/power mutations.
        if(!TryObservePoiOn(vehicle,out bool poiOn)||!poiOn)return false;
        var world=vehicle.world;var value=world.GetBlock(position);
        if(value.type!=expectedBlock||value.rotation!=expectedRotation)return false;
        // Recheck the actual native surface on the server. No client-supplied
        // placement identity, activation result or desired power is authority.
        var eye=actor.position+Vector3.up*actor.GetEyeHeight();var delta=position.ToVector3Center()-eye;
        if(!ManualPoiLightVisible(actor,world,position,eye,delta))return false;
        var poi=NearestPoi(vehicle);PreparePoiLights(vehicle,poi);
        var tile=world.GetTileEntity(position);uint stamp=RebirthCamperWorldLighting.Stamp(tile);
        if(stamp==0||expectedStamp!=0&&expectedStamp!=stamp)return false;
        var session=For(vehicle);
        if(session.Operation!=Guid.Empty||RebirthCamperEnergyAccess.Busy(vehicle))
        {status="xuiRebirthCamperEnergyOwnerWaiting";return false;}
        var affected=sessions.Values.Where(s=>Current(s)&&ReferenceEquals(s.World,world)&&
            NearestPoi(s.Vehicle) is PrefabInstance p&&Inside(p,position)).ToArray();
        if(affected.Any(s=>s.Operation!=Guid.Empty||RebirthCamperEnergyAccess.Busy(s.Vehicle)))
        {status="xuiRebirthCamperEnergyOwnerWaiting";return false;}
        string target=PoiTarget(poi);
        bool wasOn=session.PoiOn&&(session.Poi==null||ReferenceEquals(session.Poi,poi))&&
            (string.IsNullOrEmpty(session.PoiTargetIdentity)||session.PoiTargetIdentity==target)&&
            (session.PoiName==null||session.PoiName==poi.prefab.PrefabName);
        if(!wasOn)return false;
        var previous=new Dictionary<Session,RebirthCamperPoiLightChoices.Plan>();
        foreach(var s in affected)previous[s]=SelectPoiLights(s.Vehicle,NearestPoi(s.Vehicle),s.PoiLimit);
        if(!RebirthCamperPoiLightChoices.Set(world,tile,on))return false;
        foreach(var s in affected)
        {
            var prior=previous[s];
            if(s.PoiOn)
            {
                int count=prior.Selected.Count+(on?(prior.Selected.Contains(position)?0:1):(prior.Selected.Contains(position)?-1:0));
                s.PoiLimit=Math.Max(0,count);s.Dirty=true;
            }
            InvalidatePoiLights(s.Vehicle);
        }
        InvalidatePoiLights(vehicle);
        session.Poi=poi;session.PoiName=poi.prefab.PrefabName;session.PoiTargetIdentity=target;session.PoiEligible=true;session.PoiControl=true;
        session.Dirty=true;
        if(on)
        {
            var observation=ObservePoi(vehicle);
            bool available=CanSupplyPoiLights(RebirthCamperEnergyRuntime.For(vehicle).Snapshot(),observation,out _);
            status=available?"xuiRebirthCamperPoiLightOn":"xuiRebirthCamperPoiLightPowerUnavailable";
        }
        else status="xuiRebirthCamperPoiLightOff";
        // Device baselines, witness and power application remain on the existing
        // Consumers transaction path. No raw metadata/toggle setter is used here.
        return true;
    }
}
