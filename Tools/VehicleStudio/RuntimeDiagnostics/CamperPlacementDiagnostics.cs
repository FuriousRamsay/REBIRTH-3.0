using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

namespace Rebirth.VehicleStudio.Diagnostics
{
    // Observe the native calculation after it returns; never rerun or alter it.
    [HarmonyLib.HarmonyPatch(typeof(ItemActionSpawnVehicle), nameof(ItemActionSpawnVehicle.CalcSpawnPosition))]
    internal static class CamperPlacementCalculationObserver
    {
        internal static ItemActionSpawnVehicle.ItemActionDataSpawnVehicle Data;
        internal static bool Result;
        internal static Vector3 Position;
        internal static float CapturedAt;
        static void Postfix(ItemActionSpawnVehicle.ItemActionDataSpawnVehicle __0, Vector3 __1, bool __result)
        {
            Data=__0; Result=__result; Position=__1; CapturedAt=Time.unscaledTime;
        }
    }
    [Preserve]
    public sealed class ConsoleCmdCamperPlacementDiagnostics : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows | DeviceFlag.StandaloneLinux | DeviceFlag.StandaloneOSX;
        public override string[] getCommands() => new[] { "camperplacementdiag" };
        public override string getDescription() => "Read-only native held vehicle placement preview evidence.";
        public override string getHelp() => "camperplacementdiag: hold a camper or native4x4 placeable; writes preview evidence without recalculating, spawning or overriding placement.";
        static object Vec(Vector3 v) => new { x=v.x, y=v.y, z=v.z };
        static object[] GroundBlocks(World world, Vector3 candidate)
        {
            var rows=new List<object>();
            int cx=Mathf.FloorToInt(candidate.x), cy=Mathf.FloorToInt(candidate.y), cz=Mathf.FloorToInt(candidate.z);
            for(int x=-1;x<=1;x++) for(int z=-1;z<=1;z++) for(int y=-2;y<=0;y++)
            {
                var position=new Vector3i(cx+x,cy+y,cz+z);
                bool loaded=world.ChunkCache.GetChunkFromWorldPos(position.x,position.y,position.z)!=null;
                if(!loaded) { rows.Add(new { position, loaded=false }); continue; }
                var block=world.GetBlock(position);
                rows.Add(new { position, loaded=true, type=block.type, name=block.Block?.GetBlockName(),
                    air=block.isair, collideMovement=block.Block!=null && block.Block.IsCollideMovement });
            }
            return rows.ToArray();
        }
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                var world=GameManager.Instance.World;
                var player=world.GetPrimaryPlayer();
                if(player==null || GameManager.IsDedicatedServer || args.Count!=0) throw new Exception(getHelp());
                var inventory=player.inventory;
                var action=inventory.GetHoldingSecondary() as ItemActionSpawnVehicle;
                var data=inventory.holdingItemData?.actionData?.OfType<ItemActionSpawnVehicle.ItemActionDataSpawnVehicle>().SingleOrDefault();
                if(action==null || data==null || data.VehiclePreviewT==null) throw new Exception("An active native vehicle placement preview is required.");
                var preview=data.VehiclePreviewT;
                var ray=player.GetLookRay();
                var colliders=Physics.OverlapBox(preview.position+Vector3.up*(action.vehicleSize.y*.5f), action.vehicleSize*.5f,
                    preview.rotation,28901376,QueryTriggerInteraction.Ignore);
                var evidence=new {
                    kind="NATIVE_VEHICLE_PLACEMENT_PREVIEW", readOnly=true, gameplayAcceptance=false,
                    heldItem=inventory.holdingItem.GetItemName(), validPosition=data.ValidPosition, candidateWorld=Vec(data.Position),
                    nativeCalculationObserved=ReferenceEquals(CamperPlacementCalculationObserver.Data,data),
                    nativeCalculationPassed=ReferenceEquals(CamperPlacementCalculationObserver.Data,data)
                        ? (bool?)CamperPlacementCalculationObserver.Result : null,
                    nativeCalculationAgeSeconds=ReferenceEquals(CamperPlacementCalculationObserver.Data,data)
                        ? (float?)(Time.unscaledTime-CamperPlacementCalculationObserver.CapturedAt) : null,
                    nativeCalculationPosition=ReferenceEquals(CamperPlacementCalculationObserver.Data,data)
                        ? Vec(CamperPlacementCalculationObserver.Position) : null,
                    vehicleSize=Vec(action.vehicleSize), playerWorld=Vec(player.GetPosition()),
                    previewEngine=Vec(preview.position), previewWorld=Vec(preview.position+Origin.position),
                    previewRotation=Vec(preview.eulerAngles), previewScale=Vec(preview.lossyScale),
                    lookOrigin=Vec(ray.origin), lookDirection=Vec(ray.direction),
                    placementRange=4+action.vehicleSize.x,
                    groundBlocks=GroundBlocks(world,data.Position),
                    renderers=preview.GetComponentsInChildren<Renderer>(true).Select(r=>new {
                        name=r.name, enabled=r.enabled, centerEngine=Vec(r.bounds.center), size=Vec(r.bounds.size) }).ToArray(),
                    spaceMask=28901376,
                    nearbyColliders=colliders.Select(c=>new { name=c.name, layer=c.gameObject.layer, trigger=c.isTrigger,
                        centerEngine=Vec(c.bounds.center), size=Vec(c.bounds.size), previewChild=c.transform.IsChildOf(preview) }).ToArray(),
                    groundRays=Physics.RaycastAll(preview.position+Vector3.up*5,Vector3.down,10,28901376,QueryTriggerInteraction.Ignore)
                        .OrderBy(h=>h.distance).Select(h=>new { distance=h.distance, pointEngine=Vec(h.point), normal=Vec(h.normal),
                            collider=h.collider.name, layer=h.collider.gameObject.layer }).ToArray()
                };
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string file=Path.Combine(CamperDiagnosticsMod.ExportFolder,"placement_"+Guid.NewGuid().ToString("N")+".json");
                File.WriteAllText(file,JsonConvert.SerializeObject(evidence,Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperPlacement] CAPTURED "+file);
            }
            catch(Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperPlacement] "+error.Message); }
        }
    }
}
