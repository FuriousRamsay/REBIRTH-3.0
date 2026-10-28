using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static partial class RebirthCamperRelayWiring
{
    private sealed class Selection
    {
        internal EntityVehicle Vehicle;internal EntityPlayer Actor;internal World World;
        internal Guid Token,Identity;internal string Creation;internal int Relay;internal long Revision;internal float Until,Length;
    }
    private static readonly Dictionary<int,Selection> selections=new Dictionary<int,Selection>();
    private static ItemActionConnectPower Tool(EntityPlayer player)
        =>player?.inventory?.holdingItemItemValue?.ItemClass?.Actions?.OfType<ItemActionConnectPower>().FirstOrDefault();
    internal static bool WireToolHeld(EntityVehicle vehicle)
    {var player=vehicle?.world?.GetPrimaryPlayer();return player!=null&&Tool(player)!=null;}
    private static bool CanEdit(EntityVehicle vehicle,EntityPlayer player)
    {
        if(!ThreadManager.IsMainThread()||vehicle?.world==null||player==null||vehicle.IsDead()||player.IsDead()||!RebirthCamperRadial.IsCamper(vehicle)
            ||!ReferenceEquals(vehicle.world,player.world)||!ReferenceEquals(GameManager.Instance?.World,vehicle.world)
            ||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||!ReferenceEquals(vehicle.world.GetEntity(player.entityId),player))return false;
        if(!vehicle.hasLock()||!vehicle.IsLocked())return true;
        var owner=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return owner!=null&&((owner.PrimaryId!=null&&(owner.PrimaryId.Equals(vehicle.GetOwner())||vehicle.isAllowedUser(owner.PrimaryId)))
            ||(owner.NativeId!=null&&(owner.NativeId.Equals(vehicle.GetOwner())||vehicle.isAllowedUser(owner.NativeId))));
    }
    private static bool Reach(EntityPlayer player,Vector3 point,EntityVehicle vehicle)
    {
        var eye=player.position+Vector3.up*player.GetEyeHeight()-Origin.position;var delta=point-eye;float distance=delta.magnitude;
        if(distance>Constants.cDigAndBuildDistance+.1f)return false;
        if(distance<.01f)return true;
        foreach(var hit in Physics.RaycastAll(eye,delta/distance,distance,Physics.AllLayers,QueryTriggerInteraction.Ignore))
            if(hit.collider&&!hit.collider.transform.IsChildOf(player.RootTransform)
                &&!RebirthCamperPhysicalTargets.SelectionProxy(hit.collider,vehicle)&&hit.distance<distance-.08f)return false;
        return true;
    }
    private static bool CanWireBlock(EntityPlayer player,TileEntity tile)
    {
        if(tile==null||tile.IsRemoving||tile.chunk==null||!(tile is TileEntityPowered)&&!(tile is TileEntityLight))return false;
        var world=player.world;var position=tile.ToWorldPos();
        if(world.IsWithinTraderArea(position))return false;
        var owner=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if(owner==null||!world.CanPlaceBlockAt(position,owner,false))return false;
        if(tile is TileEntityPowered powered)
        {
            var node=powered.GetPowerItem();
            if(node==null||node is PowerSource||node.Parent!=null||powered.HasParent())return false;
        }
        // Native block interaction resolves the visible surface, rather than
        // requiring a clear ray all the way through to a block's centre.
        var eye=player.position+Vector3.up*player.GetEyeHeight();var delta=position.ToVector3Center()-eye;
        if(delta.sqrMagnitude>(Constants.cDigAndBuildDistance+1f)*(Constants.cDigAndBuildDistance+1f))return false;
        return Voxel.Raycast(world,new Ray(eye,delta.normalized),delta.magnitude+.1f,-538750989,8,0)
            &&Voxel.voxelRayHitInfo.hit.blockPos==position;
    }
    // External cable admission must use the same owner-specific electrical
    // sphere as supply and its displayed bounds, not the longer wire-tool reach.
    // Existing links may still be removed after the camper moves out of range.
    private static bool CanAdmitExternalLink(EntityVehicle vehicle,Vector3i position)
        =>RebirthCamperEnergyBounds.Contains(RebirthCamperEnergyBounds.CurrentCenter(vehicle),
            position.ToVector3Center(),RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle));
    internal static void Receive(World world,ClientInfo sender,int actorId,int vehicleId,Guid token,byte action,int relay,int corner,Vector3i position)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||token==Guid.Empty||action>3)return;
        var actor=world.GetEntity(actorId) as EntityPlayer;
        if(sender==null?!(actor is EntityPlayerLocal):!RebirthCamperTurretAccess.Authenticated(world,sender,actor))return;
        var vehicle=world.GetEntity(vehicleId) as EntityVehicle;
        bool accepted=false;string status="xuiCamperWireDenied";
        try
        {
            if(action==2)
            {if(selections.TryGetValue(actorId,out var old)&&old.Token==token)selections.Remove(actorId);accepted=true;status="xuiCamperWireCancelled";return;}
            var tool=Tool(actor);
            if(!RebirthCamperBodyAttachment.HasBody(vehicle))return;
            if(!RebirthCamperEnergyRuntime.TryObservePresentation(vehicle,out var mode,out _,out _,out _)||mode!=RebirthCamperEnergyMode.Camp)return;
            if(tool==null||tool.maxWireLength<=0||tool.maxWireLength>1000||!CanEdit(vehicle,actor))return;
            if(!Scope(vehicle,out _,out _)){RebirthCamperModuleRuntime.For(vehicle).Persist(true);if(!Scope(vehicle,out _,out _))return;}
            var frame=Read(vehicle);if(frame==null)return;
            if(action==3)
            {
                var endpoint=relay>=0&&relay<2?Relay(vehicle,relay):null;
                if(!endpoint||!Reach(actor,endpoint.position,vehicle))return;
                var connectedLink=frame.Links.FirstOrDefault(link=>link.Relay==relay);
                if(connectedLink==null){status="xuiCamperWireDenied";return;}
                var disconnectedFrame=new Frame{Vehicle=frame.Vehicle,World=frame.World,Revision=checked(frame.Revision+1)};
                disconnectedFrame.Links.AddRange(frame.Links);disconnectedFrame.Links.Remove(connectedLink);
                if(!connectedLink.Mounted)
                {
                    var linkedTile=world.GetTileEntity(connectedLink.Position);
                    if(linkedTile!=null&&Matches(linkedTile,connectedLink,frame.Vehicle))
                    {linkedTile.chunk.ChunkCustomData.Remove(StampKey(linkedTile));linkedTile.chunk.isModified=true;}
                }
                Save(vehicle,disconnectedFrame);Log.Out("[REBIRTH EnergyFlow] relay-disconnect vehicle="+vehicle.entityId+" relay="+relay+" remaining="+disconnectedFrame.Links.Count);
                accepted=true;status="xuiCamperWireDisconnected";return;
            }
            if(action==0)
            {
                var endpoint=relay>=0&&relay<2?Relay(vehicle,relay):null;
                if(!endpoint||!Reach(actor,endpoint.position,vehicle))return;
                string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);if(string.IsNullOrEmpty(creation))return;
                selections[actorId]=new Selection{Actor=actor,Vehicle=vehicle,World=world,Token=token,Identity=frame.Vehicle,Relay=relay,
                    Revision=frame.Revision,Creation=creation,Until=Time.realtimeSinceStartup+60,Length=tool.maxWireLength};
                accepted=true;status="xuiCamperWireSelectDevice";return;
            }
            if(!selections.TryGetValue(actorId,out var selection)||selection.Token!=token||!ReferenceEquals(selection.Actor,actor)
                ||!ReferenceEquals(selection.Vehicle,vehicle)||!ReferenceEquals(selection.World,world)||selection.Identity!=frame.Vehicle
                ||selection.Until<Time.realtimeSinceStartup||selection.Revision!=frame.Revision
                ||selection.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||selection.Length!=tool.maxWireLength)return;
            var source=Relay(vehicle,selection.Relay);if(!source)return;
            Link existing;Vector3 target;TileEntity tile=null;
            if(corner>=0)
            {
                if(corner>3||RebirthCamperTurretRuntime.For(vehicle).Observe().Slots[corner].HasTurret!=true)return;
                var endpoint=vehicle.RootTransform.GetComponentsInChildren<RebirthCamperWireTarget>(true).FirstOrDefault(t=>t.Kind==1&&t.Slot==corner&&t.gameObject.activeInHierarchy);
                if(!endpoint||!Reach(actor,endpoint.ReachPoint(actor),vehicle))return;target=endpoint.transform.position;
                existing=frame.Links.FirstOrDefault(l=>l.Corner==corner);
            }
            else
            {
                tile=world.GetTileEntity(position);if(!CanWireBlock(actor,tile))return;
                target=position.ToVector3Center()-Origin.position;existing=frame.Links.FirstOrDefault(l=>!l.Mounted&&l.Position==position);
            }
            if(Vector3.Distance(source.position,target)>selection.Length){status="xuiCamperWireTooLong";return;}
            var next=new Frame{Vehicle=frame.Vehicle,World=frame.World,Revision=checked(frame.Revision+1)};next.Links.AddRange(frame.Links);
            if(existing!=null)
            {
                next.Links.Remove(existing);
                if(tile!=null&&Matches(tile,existing,frame.Vehicle)){tile.chunk.ChunkCustomData.Remove(StampKey(tile));tile.chunk.isModified=true;}
                status="xuiCamperWireDisconnected";
            }
            else
            {
                if(corner<0&&!CanAdmitExternalLink(vehicle,position))
                {
                    status="xuiCamperWireDenied";
                    Log.Out("[REBIRTH EnergyFlow] relay-refused vehicle="+vehicle.entityId+" relay="+selection.Relay+
                        " device=block:"+position+" reason=outside-camper-bounds distance="+
                        RebirthCamperEnergyBounds.Distance(RebirthCamperEnergyBounds.CurrentCenter(vehicle),position.ToVector3Center())+
                        " radius="+RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle));
                    return;
                }
                if(next.Links.Count>=128)return;
                var stamp=tile==null?Guid.NewGuid():Claim(tile,frame.Vehicle);
                next.Links.Add(new Link{Relay=selection.Relay,Corner=corner,Position=position,Block=tile?.blockValue.type??0,Stamp=stamp,Length=selection.Length});
                status="xuiCamperWireConnected";
            }
            Save(vehicle,next);Log.Out("[REBIRTH EnergyFlow] relay-edit vehicle="+vehicle.entityId+" relay="+selection.Relay+
                " device="+(corner>=0?"mounted:"+corner:"block:"+position)+" connected="+(existing==null)+" links="+next.Links.Count);
            accepted=true;
        }
        catch(Exception error){Log.Warning("[CamperRelay] Wiring edit refused: "+error.Message);}
        finally
        {
            if(action!=0)selections.Remove(actorId);
            if(sender==null)ReceiveResult(token,action,accepted,status);
            else sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperRelayResultV1>().Setup(token,action,accepted,status));
        }
    }
}
