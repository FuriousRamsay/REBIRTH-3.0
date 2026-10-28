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
    [Preserve]
    public sealed class CamperDiagnosticsMod : IModApi
    {
        internal static string ExportFolder;
        public void InitMod(Mod mod)
        {
            ExportFolder = Path.Combine(mod.Path, "Exports", "Camper");
            var assembly = typeof(CamperDiagnosticsMod).Assembly;
            var harmony = new HarmonyLib.Harmony("rebirth.vehiclestudio.physicsfailure.diagnostic");
            var productionNames = new HashSet<string>(new[] {
                "RebirthCamperCargoSize",
                "RebirthCamperStationTick",
                "RebirthCamperRadialCommands", "RebirthCamperRadialPermission", "RebirthCamperRadialActivate",
                "RebirthCamperInitialDye", "RebirthCamperCosmeticMigration", "RebirthCamperTerrainLog", "RebirthCamperRearLights", "RebirthCamperStationHeat", "RebirthCamperStationWindowBinding", "RebirthCamperStationPosition", "RebirthCamperStationBlock", "RebirthCamperStationWindowClose", "RebirthCamperStationWindowValidity"
            });
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsDefined(typeof(HarmonyLib.HarmonyPatch), false)) continue;
                // The coordinated root build explicitly installs these production hooks.
                // This mod initializes first: checking current Harmony owners here cannot
                // predict later bootstrap installation. Never compete with that installer.
                if (productionNames.Contains(type.FullName))
                {
                    Log.Out("[CamperDiagnostics] Deferring production hook to REBIRTH bootstrap: " + type.FullName);
                    continue;
                }
                harmony.CreateClassProcessor(type).Patch();
            }
            Log.Out("[CamperDiagnostics] Read-only developer command registered: camperdiag [entityId]");
        }
    }

    [Preserve]
    public sealed class ConsoleCmdCamperDiagnostics : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows | DeviceFlag.StandaloneLinux | DeviceFlag.StandaloneOSX;
        public override string[] getCommands() => new[] { "camperdiag" };
        public override string getDescription() => "Read-only REBIRTH camper effective config, native physics and occupant capture.";
        public override string getHelp() => "camperdiag [entityId]: write JSON evidence for the exact REBIRTH camper. Does not spawn, attach, drive or change configuration.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                var world = GameManager.Instance.World;
                var player = world.GetPrimaryPlayer();
                if (player == null || GameManager.IsDedicatedServer) throw new Exception("A local client player in an active world is required.");
                int? id = args.Count > 0 ? int.Parse(args[0]) : (int?)null;
                var vehicles = world.Entities.list.OfType<EntityVehicle>().Where(x => EntityClass.list[x.entityClass].entityClassName.Equals("rebirthCamper80s", StringComparison.OrdinalIgnoreCase)
                    || (id.HasValue && EntityClass.list[x.entityClass].entityClassName.Equals("vehicleTruck4x4", StringComparison.OrdinalIgnoreCase)));
                var vehicle = id.HasValue ? vehicles.FirstOrDefault(x => x.entityId == id) : vehicles.OrderBy(x => (x.GetPosition() - player.GetPosition()).sqrMagnitude).FirstOrDefault();
                if (vehicle == null) throw new Exception("No matching loaded REBIRTH camper found.");
                var model = vehicle.ModelTransform;
                var physics = vehicle.PhysicsTransform;
                var rb = vehicle.vehicleRB;
                var classes = vehicle.vehicle.Properties.Classes;
                var seats = new List<object>();
                for (int slot = 0; slot < vehicle.GetAttachMaxCount(); slot++)
                {
                    var occupant = vehicle.GetAttached(slot);
                    var alive = occupant as EntityAlive;
                    var bones = new List<object>();
                    if (occupant != null && occupant.ModelTransform != null)
                    {
                        var names = new HashSet<string>(new[] { "Hips", "Head", "LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftLeg", "RightLeg", "LeftUpLeg", "RightUpLeg" });
                        foreach (var t in occupant.ModelTransform.GetComponentsInChildren<Transform>(true))
                            if (names.Contains(t.name)) bones.Add(new { name = t.name, path = PathOf(t, model), vehicleLocal = Vec(model.InverseTransformPoint(t.position)),
                                vehicleLocalRotation = Vec((Quaternion.Inverse(model.rotation) * t.rotation).eulerAngles), world = Vec(t.position) });
                    }
                    DynamicProperties seat;
                    classes.TryGetValue("seat" + slot, out seat);
                    var attachment = vehicle.GetAttachedToInfo(slot);
                    var ikFrames = occupant == null ? new object[0] : occupant.GetComponentsInChildren<IKController>(true)
                        .Select(ik => (object)new { path = PathOf(ik.transform, model),
                            vehicleLocalPosition = Vec(model.InverseTransformPoint(ik.transform.position)),
                            vehicleLocalRotation = Vec((Quaternion.Inverse(model.rotation) * ik.transform.rotation).eulerAngles),
                            worldPosition = Vec(ik.transform.position), worldRotation = Vec(ik.transform.eulerAngles),
                            worldScale = Vec(ik.transform.lossyScale),
                            targets = ik.targets == null ? new object[0] : ik.targets.Select(t => (object)new {
                                goal = t.avatarGoal.ToString(), hasTransform = t.transform != null,
                                targetPath = t.transform == null ? null : PathOf(t.transform, model),
                                configuredPosition = Vec(t.position), configuredRotation = Vec(t.rotation),
                                targetVehicleLocal = t.transform == null ? null : Vec(model.InverseTransformPoint(t.transform.position))
                            }).ToArray()
                        }).ToArray();
                    seats.Add(new { slot, configuredPose = vehicle.vehicle.GetSeatPose(slot), configuredPosition = seat == null ? null : seat.GetString("position"), exits = seat == null ? null : seat.GetString("exit"),
                        effectiveAttachmentPosition = attachment == null ? null : Vec(attachment.enterPosition),
                        effectiveAttachmentRotation = attachment == null ? null : Vec(attachment.enterRotation),
                        occupantId = occupant == null ? -1 : occupant.entityId, occupantType = occupant == null ? null : occupant.GetType().FullName,
                        isPlayer = occupant is EntityPlayer, occupantAlive = alive != null && !alive.IsDead(),
                        isLocalPlayer = occupant == player, runtimePose = alive == null ? -1 : alive.vehiclePoseMode,
                        attachedToCamper = occupant != null && occupant.AttachedToEntity == vehicle,
                        bedPose = slot == 4 && alive != null
                            ? Rebirth.Vehicles.Camper.RebirthCamperBedPoseService.Inspect(alive) : null,
                        bones, ikFrames });
                }
                var wheelStates = new List<object>();
                if (vehicle.wheels != null)
                    for (int i = 0; i < vehicle.wheels.Length; i++)
                    {
                        var w = vehicle.wheels[i]; var c = w.wheelC;
                        WheelHit contact = default(WheelHit);
                        bool hasContact = c != null && c.GetGroundHit(out contact);
                        wheelStates.Add(new { index = i, colliderPresent = c != null, steerTargetPresent = w.steerT != null, tireTargetPresent = w.tireT != null,
                            contactPoint = hasContact ? Vec(contact.point + Origin.position) : null,
                            contactNormal = hasContact ? Vec(contact.normal) : null,
                            contactForce = hasContact ? (float?)contact.force : null,
                            contactCollider = hasContact && contact.collider != null ? ColliderState(contact.collider, rb) : null,
                            forwardSlip = hasContact ? contact.forwardSlip : 0, sidewaysSlip = hasContact ? contact.sidewaysSlip : 0,
                            grounded = c != null && c.isGrounded, radius = c == null ? 0 : c.radius, rpm = c == null ? 0 : c.rpm,
                            steerAngle = c == null ? 0 : c.steerAngle, motorTorque = c == null ? 0 : c.motorTorque, brakeTorque = c == null ? 0 : c.brakeTorque,
                            tireLocalPosition = w.tireT == null ? null : Vec(model.InverseTransformPoint(w.tireT.position)),
                            wheelMass = c == null ? 0 : c.mass, suspensionDistance = c == null ? 0 : c.suspensionDistance,
                            spring = c == null ? 0 : c.suspensionSpring.spring, damper = c == null ? 0 : c.suspensionSpring.damper,
                            springTarget = c == null ? 0 : c.suspensionSpring.targetPosition,
                            colliderCenter = c == null ? null : Vec(c.center),
                            forceAppPointDistance = c == null ? (float?)null : c.forceAppPointDistance,
                            colliderPose = c == null ? null : Pose(c.transform),
                            forwardFriction = c == null ? (object)null : c.forwardFriction, sidewaysFriction = c == null ? (object)null : c.sidewaysFriction,
                            tireLocalRotation = w.tireT == null ? null : Vec(w.tireT.localEulerAngles) });
                    }
                var renderers = model.GetComponentsInChildren<Renderer>(true).Select(x => new { name = x.name, active = x.gameObject.activeInHierarchy,
                    materials = x.sharedMaterials.Select(m => new { name = m == null ? null : m.name, shader = m == null || m.shader == null ? null : m.shader.name }).ToArray() }).ToArray();
                var targets = model.GetComponentsInChildren<Transform>(true).Where(x => x.name == "IKHandL" || x.name == "IKHandR")
                    .Select(x => new { name = x.name, path = PathOf(x, model), vehicleLocal = Vec(model.InverseTransformPoint(x.position)), localPosition = Vec(x.localPosition), localRotation = Vec(x.localEulerAngles) }).ToArray();
                var steering = model.Find("M/SteeringWheel");
                var steeringMesh = steering == null ? null : steering.GetComponentInChildren<MeshFilter>();
                var steeringState = steering == null ? null : new { path = PathOf(steering, model), localPosition = Vec(steering.localPosition),
                    localRotation = Vec(steering.localEulerAngles), localQuaternion = Quat(steering.localRotation), shaftVehicleLocal = Vec(model.InverseTransformDirection(steering.up)),
                    pivotVehicleLocal = Vec(model.InverseTransformPoint(steering.position)),
                    meshCenterVehicleLocal = steeringMesh == null || steeringMesh.sharedMesh == null ? null : Vec(model.InverseTransformPoint(steeringMesh.transform.TransformPoint(steeringMesh.sharedMesh.bounds.center))),
                    meshBoundsCenter = steeringMesh == null ? null : Vec(steeringMesh.sharedMesh.bounds.center),
                    meshBoundsSize = steeringMesh == null ? null : Vec(steeringMesh.sharedMesh.bounds.size) };
                var lights = model.GetComponentsInChildren<Light>(true).Select(x => new { name = x.name, path = PathOf(x.transform,model), active = x.gameObject.activeInHierarchy, enabled = x.enabled, intensity = x.intensity, range = x.range,
                    color = new { r=x.color.r,g=x.color.g,b=x.color.b }, vehicleLocalPosition = Vec(model.InverseTransformPoint(x.transform.position)),
                    vehicleLocalDirection = Vec(model.InverseTransformDirection(x.transform.forward)) }).ToArray();
                var nativeHeadlight = vehicle.vehicle.FindPart("headlight") as VPHeadlight;
                var rearLensT = model.Find("M/VisualBody/80sCamperTruck/lights/RearRedLenses");
                var rearLens = rearLensT == null ? null : rearLensT.GetComponent<Renderer>();
                var rearBlock = new MaterialPropertyBlock();
                Color rearEmission = Color.black;
                if (rearLens != null)
                {
                    rearLens.GetPropertyBlock(rearBlock);
                    var material = rearLens.sharedMaterial;
                    rearEmission = !rearBlock.isEmpty ? rearBlock.GetColor("_EmissionColor")
                        : (material != null && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black);
                }
                var rearPatchInfo = HarmonyLib.Harmony.GetPatchInfo(HarmonyLib.AccessTools.Method(typeof(VPHeadlight),"Update"));
                var rearOwners = rearPatchInfo == null ? new string[0] : rearPatchInfo.Postfixes
                    .Where(p=>p.PatchMethod.DeclaringType.FullName=="RebirthCamperRearLights")
                    .Select(p=>p.owner+":"+p.PatchMethod.DeclaringType.Assembly.GetName().Name).ToArray();
                var physicalColliders = physics == null ? new Collider[0] : physics.GetComponentsInChildren<Collider>(true);
                var interactionColliders = model.GetComponentsInChildren<Collider>(true)
                    .Where(c => !physicalColliders.Contains(c)).ToArray();
                var playerColliders = player.GetComponentsInChildren<Collider>(true);
                var collisionPairs = physicalColliders.SelectMany(c => interactionColliders.Concat(playerColliders)
                    .Distinct().Where(other => other != c).Select(other => new {
                        physicsCollider = PathOf(c.transform, null), physicsInstanceId = c.GetInstanceID(),
                        otherCollider = PathOf(other.transform, null), otherInstanceId = other.GetInstanceID(),
                        otherIsPlayer = playerColliders.Contains(other),
                        physicsLayer = c.gameObject.layer, otherLayer = other.gameObject.layer,
                        layerCollisionIgnored = Physics.GetIgnoreLayerCollision(c.gameObject.layer, other.gameObject.layer),
                        pairCollisionIgnored = Physics.GetIgnoreCollision(c, other)
                    })).ToArray();
                var report = new { kind = "REBIRTH_CAMPER_RUNTIME_DIAGNOSTIC", capturedUtc = DateTime.UtcNow.ToString("o"), entityId = vehicle.entityId,
                    productionBootstrap = new {
                        state = Rebirth.Bootstrap.RebirthFreshInit.State.ToString(),
                        lastFailure = Rebirth.Bootstrap.RebirthFreshInit.LastFailure,
                        moduleInstalled = Rebirth.Vehicles.Camper.RebirthCamperModulePatchInstaller.IsInstalled },
                    entityClass = EntityClass.list[vehicle.entityClass].entityClassName, runtimeClass = vehicle.GetType().FullName,
                    vehicleName = vehicle.vehicle.GetName(), position = Vec(vehicle.GetPosition()), rotation = Vec(vehicle.transform.eulerAngles),
                    forwardVelocity = vehicle.vehicle.CurrentForwardVelocity, braking = vehicle.vehicle.CurrentIsBreak,
                    nativeAudio = CamperAudioEvidence.Capture(vehicle),
                    headlightsOn = nativeHeadlight != null && nativeHeadlight.isOn,
                    headlightBroken = nativeHeadlight != null && nativeHeadlight.IsBroken(),
                    rearLamp = new { present=rearLens!=null, productionOwners=rearOwners,
                        emission=new {r=rearEmission.r,g=rearEmission.g,b=rearEmission.b} },
                    turnTowardsLook = EntityVehicle.isTurnTowardsLook,
                    attachMaxCount = vehicle.GetAttachMaxCount(), configuredSeatClasses = classes.Keys.Where(x => x.StartsWith("seat", StringComparison.Ordinal)).OrderBy(x => x).ToArray(),
                    rigidbodyPresent = rb != null, physicsRootPresent = physics != null,
                    speed = rb == null ? 0 : rb.velocity.magnitude, mass = rb == null ? 0 : rb.mass,
                    drag = rb == null ? 0 : rb.drag, angularDrag = rb == null ? 0 : rb.angularDrag,
                    centerOfMass = rb == null ? null : Vec(rb.centerOfMass),
                    rigidbodyState = rb == null ? null : new {
                        instanceId = rb.GetInstanceID(), path = PathOf(rb.transform, null),
                        linearVelocity = Vec(rb.velocity), angularVelocity = Vec(rb.angularVelocity),
                        position = Vec(rb.position + Origin.position), enginePosition = Vec(rb.position), rotation = Quat(rb.rotation),
                        worldCenterOfMass = Vec(rb.worldCenterOfMass + Origin.position),
                        isKinematic = rb.isKinematic, useGravity = rb.useGravity, sleeping = rb.IsSleeping(),
                        constraints = rb.constraints.ToString(), collisionDetectionMode = rb.collisionDetectionMode.ToString() },
                    transforms = new { origin = Vec(Origin.position), root = Pose(vehicle.RootTransform),
                        model = Pose(model), physics = Pose(physics) },
                    physicsColliders = physicalColliders.Select(c => ColliderState(c, rb)).ToArray(),
                    interactionColliders = interactionColliders.Select(c => ColliderState(c, rb)).ToArray(),
                    playerColliders = playerColliders.Select(c => ColliderState(c, rb)).ToArray(), collisionPairs,
                    physicsColliderCount = physics == null ? 0 : physics.GetComponentsInChildren<Collider>(true).Length,
                    seats, wheels = wheelStates, renderers, handTargets = targets, steering = steeringState, lights,
                    note = "Read-only snapshot. Visual fit, collision behavior and input-driven handling require separate gameplay evidence." };
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string output = Path.Combine(CamperDiagnosticsMod.ExportFolder, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(output, JsonConvert.SerializeObject(report, Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperDiagnostics] PASS_CAPTURE " + output);
                Log.Out("[CamperDiagnostics] PASS_CAPTURE " + output);
            }
            catch (Exception ex)
            {
                SdtdConsole.Instance.Output("*** ERROR [CamperDiagnostics] " + ex.Message);
                Log.Error("[CamperDiagnostics] " + ex);
            }
        }
        private static object Vec(Vector3 v) => new { x = v.x, y = v.y, z = v.z };
        private static object Quat(Quaternion q) => new { x = q.x, y = q.y, z = q.z, w = q.w };
        private static object Pose(Transform t) => t == null ? null : new {
            path = PathOf(t, null), parentPath = t.parent == null ? null : PathOf(t.parent, null),
            instanceId = t.GetInstanceID(), worldPosition = Vec(t.position + Origin.position), enginePosition = Vec(t.position),
            worldRotation = Quat(t.rotation), worldScale = Vec(t.lossyScale),
            localPosition = Vec(t.localPosition), localRotation = Quat(t.localRotation), localScale = Vec(t.localScale) };
        private static object ColliderState(Collider c, Rigidbody vehicleBody)
        {
            if (c == null) return null;
            var body = c.attachedRigidbody;
            return new { path = PathOf(c.transform, null), instanceId = c.GetInstanceID(), type = c.GetType().FullName,
                enabled = c.enabled, active = c.gameObject.activeInHierarchy, isTrigger = c.isTrigger,
                layer = c.gameObject.layer, layerName = LayerMask.LayerToName(c.gameObject.layer), pose = Pose(c.transform),
                boundsCenter = Vec(c.bounds.center + Origin.position), boundsSize = Vec(c.bounds.size),
                attachedRigidbodyId = body == null ? (int?)null : body.GetInstanceID(),
                attachedRigidbodyPath = body == null ? null : PathOf(body.transform, null),
                attachedToVehicleBody = body != null && body == vehicleBody };
        }
        private static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}

