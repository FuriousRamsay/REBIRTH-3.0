using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class ConsoleCmdRebirthRobotRig : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>true;
    public override string[] getCommands()=>new[]{"rbrobotrigcheck"};
    public override string getDescription()=>"Read-only check of the new camper turret rig, LODs and aim limits.";
    public override string getHelp()=>"rbrobotrigcheck: stand within 30m of the camper after importing the new turret bundle.";
    public override void Execute(List<string> args,CommandSenderInfo senderInfo)
    {
        try
        {
            var world=GameManager.Instance.World;var player=world?.GetPrimaryPlayer();
            if(player==null)throw new InvalidOperationException("Local player required");
            var vehicles=world.GetEntitiesInBounds(typeof(EntityVehicle),new Bounds(player.position,Vector3.one*60),new List<Entity>()).OfType<EntityVehicle>();
            var vehicle=vehicles.FirstOrDefault(v=>v.RootTransform&&v.RootTransform.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="TurretPitchLimits"));
            if(vehicle==null)throw new InvalidOperationException("New turret prefab missing from nearby campers");
            for(int i=0;i<4;i++)
            {
                var slot=vehicle.RootTransform.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot"+i);
                var parts=slot.GetComponentsInChildren<Transform>(true);
                var yaw=parts.Single(t=>t.name=="TurretYaw");var pitch=parts.Single(t=>t.name=="TurretPitch");var muzzle=parts.Single(t=>t.name=="TurretMuzzle");
                if(pitch.parent!=yaw||muzzle.parent!=pitch)throw new Exception("Pitch/muzzle parent mismatch at corner "+i);
                // Support fasteners now have their own LOD groups as well.
                // Qualify the three-part supplied turret, not an arbitrary group.
                var levels=slot.GetComponentsInChildren<LODGroup>(true).Select(g=>g.GetLODs())
                    .Single(l=>l.Length==3&&l[0].renderers.Any(r=>r&&r.name=="PitchBody_LOD0"));
                if(levels.Length!=3)throw new Exception("Expected three LODs");
                int[] limits={40000,16000,6000};
                for(int level=0;level<3;level++)
                {
                    if(levels[level].renderers.Length!=3)throw new Exception("Each LOD needs fixed, yaw and pitch renderers");
                    ulong triangles=0;
                    foreach(var r in levels[level].renderers)
                    {
                        var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                        for(int sub=0;sub<mesh.subMeshCount;sub++)triangles+=mesh.GetIndexCount(sub)/3;
                    }
                    if(triangles==0||triangles>(ulong)limits[level])throw new Exception("LOD budget mismatch at corner "+i);
                }
                float heading=999,elevation=999;RebirthCamperTurretRuntime.For(vehicle).ClampAim(i,ref heading,ref elevation);
                if(heading!=90||Mathf.Abs(elevation-29.5f)>.001f)throw new Exception("Downward clamp mismatch");
                heading=elevation=-999;RebirthCamperTurretRuntime.For(vehicle).ClampAim(i,ref heading,ref elevation);
                if(heading!=-90||elevation!=-75)throw new Exception("Upward clamp mismatch");
            }
            Log.Out("[RobotTurretRig] PASS four rigs; 40k/16k/6k LOD budgets; -75/+29.5 pitch; +/-90 yaw. Read-only contract check, not firing/visual proof.");
        }
        catch(Exception e){Log.Error("[RobotTurretRig] FAIL "+e.Message);}
    }
}
