using System;
using UnityEngine;

internal sealed partial class RebirthCamperTurretRuntime
{
    private sealed class ControlLease
    {
        internal EntityPlayer Player;
        internal Guid Token;
        internal float Until,InputUntil,Yaw,Pitch,NextRangeWarning,NextRangeProbe;
        internal bool Fire;
        internal readonly RebirthCamperManualPress Press=new RebirthCamperManualPress();
    }
    private readonly ControlLease[] controls=new ControlLease[4];
    private ControlLease LiveControl(int corner)
    {
        var lease=controls[corner];
        if(lease!=null&&(Time.unscaledTime>=lease.Until||!RebirthCamperTurretAccess.CanUse(vehicle,lease.Player)
            ||!Observe().Slots[corner].HasTurret||!Connected(lease.Player)))controls[corner]=lease=null;
        return lease;
    }
    private bool Connected(EntityPlayer player)
    {
        if(player is EntityPlayerLocal)return true;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return RebirthCamperTurretAccess.Authenticated(vehicle.world,connection?.Clients?.ForEntityId(player.entityId),player);
    }
    internal int ControlOwner(int corner)=>corner>=0&&corner<4?LiveControl(corner)?.Player.entityId??-1:-1;
    internal bool AcquireControl(EntityPlayer player,int corner,Guid token)
    {
        if(vehicle.world.IsRemote()||token==Guid.Empty||corner<0||corner>=4
            ||!RebirthCamperTurretAccess.CanUse(vehicle,player)||!Observe().Slots[corner].HasTurret)return false;
        var existing=LiveControl(corner);
        if(existing!=null&&(existing.Player!=player||existing.Token!=token))return false;
        // One selected turret per player; other players may use the other corners.
        for(int i=0;i<4;i++)if(i!=corner&&controls[i]?.Player==player)controls[i]=null;
        if(existing==null)
        {
            var gun=Bind(corner);if(gun==null)return false;
            existing=controls[corner]=new ControlLease{Player=player,Token=token,Yaw=gun.YawAngle,Pitch=gun.PitchAngle};
        }
        existing.Until=Time.unscaledTime+3;
        return true;
    }
    internal void ReleaseControl(EntityPlayer player,Guid token)
    {
        for(int i=0;i<4;i++)if(controls[i]?.Player==player&&controls[i].Token==token)controls[i]=null;
    }
    internal bool ManualInput(EntityPlayer player,int corner,Guid token,float yaw,float pitch,bool fire)
    {
        if(vehicle.world.IsRemote()||corner<0||corner>=4||float.IsNaN(yaw)||float.IsInfinity(yaw)
            ||float.IsNaN(pitch)||float.IsInfinity(pitch))return false;
        var lease=LiveControl(corner);
        if(lease==null||lease.Player!=player||lease.Token!=token)return false;
        ClampAim(corner,ref yaw,ref pitch);
        lease.Yaw=yaw;lease.Pitch=pitch;lease.Press.Observe(fire,Time.unscaledTime);lease.Fire=fire;
        lease.InputUntil=Time.unscaledTime+.25f;lease.Until=Time.unscaledTime+3;
        return true;
    }
}
