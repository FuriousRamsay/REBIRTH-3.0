using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Read-only bridge evidence. Does not bind rigs, create runtimes, acquire control,
// move transforms, transfer items or advance targeting/fire state.
internal sealed partial class RebirthCamperTurretRuntime
{
    internal static JObject Observation(EntityVehicle vehicle,bool assets)
    {
        owners.TryGetValue(vehicle,out var runtime);
        var state=RebirthCamperTurretState.Read(vehicle);var corners=new JArray();
        for(int i=0;i<4;i++)
        {
            var slot=state.Slots[i];var gun=runtime?.guns[i];var control=runtime?.controls[i];
            var ammo=new JArray(slot.Ammo.Select(stack=>new JObject{["item"]=stack.IsEmpty()?null:stack.itemValue.ItemClass.GetItemName(),["count"]=stack.count}));
            var target=gun==null?null:vehicle.world.GetEntity(gun.Target) as EntityAlive;
            corners.Add(new JObject{
                ["corner"]=i,["installed"]=slot.HasTurret,["enabled"]=slot.Enabled,["loadedSlot"]=slot.LoadedAmmo,["ammo"]=ammo,
                ["silencer"]=slot.Silencer.IsEmpty()?null:slot.Silencer.itemValue.ItemClass.GetItemName(),
                ["sniperMode"]=slot.SniperMode,["zoomCap"]=RebirthCamperTurretOptics.Cap(slot),
                ["scopes"]=new JArray(slot.Scopes.Select(stack=>stack.IsEmpty()?null:stack.itemValue.ItemClass.GetItemName())),
                ["availableZoom"]=new JArray(RebirthCamperTurretOptics.Available(slot)),
                ["loadedRange"]=RebirthCamperTurretProfiles.Range(slot),["runtimeBound"]=gun!=null,
                ["yaw"]=gun?.Yaw?new JValue(Mathf.DeltaAngle(0,(Quaternion.Inverse(gun.Rest)*gun.Yaw.localRotation).eulerAngles.y)):JValue.CreateNull(),
                ["pitch"]=gun?.Pitch?new JValue(Mathf.DeltaAngle(0,gun.Pitch.localEulerAngles.x)):JValue.CreateNull(),
                ["yawMin"]=gun==null?JValue.CreateNull():new JValue(gun.YawMinimum),["yawMax"]=gun==null?JValue.CreateNull():new JValue(gun.YawMaximum),
                ["pitchMin"]=gun==null?JValue.CreateNull():new JValue(gun.PitchMinimum),["pitchMax"]=gun==null?JValue.CreateNull():new JValue(gun.PitchMaximum),
                ["shotsConsumed"]=gun==null?JValue.CreateNull():new JValue(gun.ShotsFired),
                ["firingWeapon"]=gun?.ShotWeapon,["firingSound"]=gun?.ShotSound,["shotInterval"]=gun==null?JValue.CreateNull():new JValue(gun.ShotInterval),
                ["lensActive"]=gun?.LensActive.HasValue==true?new JValue(gun.LensActive.Value):JValue.CreateNull(),
                ["lensRendererCount"]=gun?.LensRenderers==null?0:gun.LensRenderers.Length,
                ["targetEntity"]=target==null?JValue.CreateNull():new JValue(target.entityId),["targetHealth"]=target==null?JValue.CreateNull():new JValue(target.Health),
                ["controllerEntity"]=control!=null&&Time.unscaledTime<control.Until?new JValue(control.Player.entityId):JValue.CreateNull()
            });
        }
        var result=new JObject{["schema"]=1,["entity"]=vehicle.entityId,["time"]=Time.realtimeSinceStartup,
            ["health"]=vehicle.Health,["maximumHealth"]=vehicle.Stats.Health.BaseMax,
            ["authoritative"]=!vehicle.world.IsRemote(),["revision"]=state.Revision,["corners"]=corners,
            ["idleHumPlaying"]=runtime!=null&&runtime.idleHumPlaying,["scope"]="native state/transform observation; does not prove visual/audio quality or remote gameplay"};
        if(assets)
        {
            var lods=new JArray();
            foreach(var group in vehicle.RootTransform.GetComponentsInChildren<LODGroup>(true))
            {
                var levels=new JArray();foreach(var level in group.GetLODs())
                {
                    int triangles=0;foreach(var renderer in level.renderers){var f=renderer?renderer.GetComponent<MeshFilter>():null;if(f?.sharedMesh)for(int s=0;s<f.sharedMesh.subMeshCount;s++)triangles+=(int)f.sharedMesh.GetIndexCount(s)/3;}
                    levels.Add(new JObject{["screenHeight"]=level.screenRelativeTransitionHeight,["triangles"]=triangles,["renderers"]=level.renderers.Length});
                }
                lods.Add(new JObject{["name"]=group.name,["levels"]=levels});
            }
            result["lodGroups"]=lods;
            var armor=new JArray();
            foreach(var filter in vehicle.RootTransform.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("REBIRTHArmorSurface_",System.StringComparison.Ordinal)))
            {
                var mesh=filter.sharedMesh;long triangles=0;if(mesh)for(int s=0;s<mesh.subMeshCount;s++)triangles+=mesh.GetIndexCount(s)/3;
                armor.Add(new JObject{["name"]=filter.name,["active"]=filter.gameObject.activeInHierarchy,["triangles"]=triangles,
                    ["rangeMarkers"]=filter.transform.Cast<Transform>().Count(t=>t.name.StartsWith("ArmorRange_",System.StringComparison.Ordinal))});
            }
            result["armorSurfaces"]=armor;
        }
        result["presentation"]=RebirthCamperTurretAccess.ObservePresentation(vehicle);
        result["wirePreview"]=JToken.FromObject(RebirthCamperRelayWiring.ObserveWireFeedback());
        return result;
    }
}
