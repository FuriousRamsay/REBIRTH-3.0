using System.Linq;
using UnityEngine;

internal sealed partial class RebirthCamperTurretRuntime
{
    private static readonly int ActiveLensId=Shader.PropertyToID("_TurretActive");
    private bool idleHumPlaying;
    private static void Lens(Gun gun,bool active)
    {
        if(GameManager.IsDedicatedServer||gun.LensActive==active)return;
        if(gun.LensRenderers==null)
        {
            gun.LensRenderers=gun.Pitch.GetComponentsInChildren<Renderer>(true)
                .Where(r=>r.sharedMaterials.Any(m=>m&&m.HasProperty(ActiveLensId))).ToArray();
            gun.LensBlock=new MaterialPropertyBlock();
        }
        foreach(var renderer in gun.LensRenderers)
        {
            if(!renderer)continue;
            renderer.GetPropertyBlock(gun.LensBlock);
            gun.LensBlock.SetFloat(ActiveLensId,active?1:0);
            renderer.SetPropertyBlock(gun.LensBlock);
        }
        gun.LensActive=active;
    }
    private void IdleScan(Gun gun,int corner,float now)
    {
        float middle=(gun.YawMinimum+gun.YawMaximum)*.5f;
        float amplitude=Mathf.Min(60,(gun.YawMaximum-gun.YawMinimum)*.42f);
        Move(gun,middle+Mathf.Sin(now*.18f+corner*1.9f)*amplitude,Mathf.Clamp(0,gun.PitchMinimum,gun.PitchMaximum),12);
    }
    private void IdleHum(RebirthCamperTurretState snapshot,bool server)
    {
        if(GameManager.IsDedicatedServer)return;
        bool active=false;
        if(!vehicle.IsDead())for(int i=0;i<4;i++)
            active|=snapshot.Slots[i].HasTurret&&RebirthCamperRelayWiring.TurretPowered(vehicle,i)&&(snapshot.Slots[i].Enabled||
                (server?controls[i]!=null&&Time.unscaledTime<controls[i].Until:guns[i]?.RemoteManual==true));
        if(active==idleHumPlaying)return;
        // One quiet native loop per vehicle prevents four overlapping idle loops.
        // Entity-attached native audio follows a moving vehicle and owns cleanup.
        if(active)Audio.Manager.Play(vehicle,"turret_idle_lp",.12f,false);
        else Audio.Manager.Stop(vehicle.entityId,"turret_idle_lp");
        idleHumPlaying=active;
    }
}
