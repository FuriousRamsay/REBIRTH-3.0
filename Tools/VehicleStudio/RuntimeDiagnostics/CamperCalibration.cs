using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

namespace Rebirth.VehicleStudio.Diagnostics
{
    // Explicit developer fixtures: local disposable save only; never edits production files.
    [Preserve]
    public sealed class ConsoleCmdCamperCalibration : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperik", "campermatch" };
        public override string getDescription() => "Disposable-save live camper IK/physics calibration; exports each change.";
        public override string getHelp() => "camperik ID L|R x y z rx ry rz: move native grip target. campermatch CAMPER_ID NATIVE_4X4_ID [force]: copy donor physics with preserved wheel rest position; force also matches torque per wheel radius and donor COM relative to axle center. CodexTest only.";
        private static float Number(string text)
        {
            float value = float.Parse(text, CultureInfo.InvariantCulture);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new Exception("Finite values required.");
            return value;
        }
        private static object Vec(Vector3 value) => new { x = value.x, y = value.y, z = value.z };
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                if (GameManager.IsDedicatedServer || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
                    throw new Exception("Only local disposable CodexTest calibration is permitted.");
                var world = GameManager.Instance.World;
                var player = world.GetPrimaryPlayer();
                if (player == null || args.Count < 2) throw new Exception(getHelp());
                var camper = world.GetEntity(int.Parse(args[0])) as EntityVehicle;
                if (camper == null || EntityClass.list[camper.entityClass].entityClassName != "rebirthCamper80s")
                    throw new Exception("Exact camper entity required.");
                object evidence;
                if (args.Count == 8)
                {
                    if (player.AttachedToEntity != camper || camper.GetAttached(0) != player)
                        throw new Exception("Local player must occupy camper driver seat.");
                    string hand = args[1].ToUpperInvariant();
                    if (hand != "L" && hand != "R") throw new Exception("Hand must be L or R.");
                    var target = camper.ModelTransform.Find("M/SteeringWheel/IKHand" + hand);
                    if (target == null) throw new Exception("Native steering hand target missing.");
                    var position = new Vector3(Number(args[2]),Number(args[3]),Number(args[4]));
                    var rotation = new Vector3(Number(args[5]),Number(args[6]),Number(args[7]));
                    if (position.magnitude > .5f) throw new Exception("Grip must remain within .5m of the hub.");
                    var before = new { position = Vec(target.localPosition), rotation = Vec(target.localEulerAngles) };
                    target.localPosition = position;
                    target.localEulerAngles = rotation;
                    evidence = new { kind = "LIVE_NATIVE_HAND_IK_CANDIDATE", entity = camper.entityId, hand, before, position = Vec(position), rotation = Vec(rotation),
                        note = "Native arm solver follows target. Not persisted to XML; screenshot validation required." };
                }
                else if (args.Count == 2 || (args.Count == 3 && args[2] == "force"))
                {
                    var donor = world.GetEntity(int.Parse(args[1])) as EntityVehicle;
                    if (donor == null || EntityClass.list[donor.entityClass].entityClassName != "vehicleTruck4x4")
                        throw new Exception("Exact loaded native 4x4 donor required.");
                    if (camper.vehicleRB == null || donor.vehicleRB == null || camper.wheels == null || donor.wheels == null
                        || camper.wheels.Length != 4 || donor.wheels.Length != 4)
                        throw new Exception("Both vehicles require native rigidbody and four wheels.");
                    // Validate every collider before changing any value; a missing last wheel must not leave a partial candidate.
                    for (int i = 0; i < 4; i++)
                        if (camper.wheels[i].wheelC == null || donor.wheels[i].wheelC == null)
                            throw new Exception("Missing wheel collider.");
                    float radiusScale = 0f;
                    Vector3 camperAxleCenter = Vector3.zero, donorAxleCenter = Vector3.zero;
                    for (int i = 0; i < 4; i++)
                    {
                        var c = camper.wheels[i].wheelC; var d = donor.wheels[i].wheelC;
                        if (c.radius <= 0f || d.radius <= 0f) throw new Exception("Positive wheel radii required.");
                        float ratio = c.radius / d.radius;
                        if (i == 0) radiusScale = ratio;
                        else if (Mathf.Abs(ratio - radiusScale) > .001f)
                            throw new Exception("Different front/rear radius ratios need separate force tuning.");
                        camperAxleCenter += camper.PhysicsTransform.InverseTransformPoint(c.transform.position) * .25f;
                        donorAxleCenter += donor.PhysicsTransform.InverseTransformPoint(d.transform.position) * .25f;
                    }
                    if (camper.vehicleRB.velocity.magnitude > .1f || donor.vehicleRB.velocity.magnitude > .1f)
                        throw new Exception("Stop both vehicles before changing physics.");
                    var rb = camper.vehicleRB; var original = donor.vehicleRB;
                    var before = new { rb.mass, rb.drag, rb.angularDrag, centerOfMass = Vec(rb.centerOfMass) };
                    rb.mass = original.mass; rb.drag = original.drag; rb.angularDrag = original.angularDrag;
                    // Keep hub geometry. Adjust the collider center so changing suspension travel
                    // does not move the wheel's configured resting position relative to its fender.
                    for (int i = 0; i < 4; i++)
                    {
                        var c = camper.wheels[i].wheelC; var d = donor.wheels[i].wheelC;
                        if (c == null || d == null) throw new Exception("Missing wheel collider.");
                        Vector3 center = c.center;
                        center.y += d.suspensionDistance * d.suspensionSpring.targetPosition
                            - c.suspensionDistance * c.suspensionSpring.targetPosition;
                        c.center = center;
                        c.mass = d.mass; c.suspensionDistance = d.suspensionDistance;
                        c.suspensionSpring = d.suspensionSpring; c.forwardFriction = d.forwardFriction;
                        c.sidewaysFriction = d.sidewaysFriction; c.forceAppPointDistance = d.forceAppPointDistance;
                    }
                    bool matchForce = args.Count == 3;
                    if (matchForce)
                    {
                        Vector3 matchedCom = original.centerOfMass;
                        matchedCom.x += camperAxleCenter.x - donorAxleCenter.x;
                        matchedCom.z += camperAxleCenter.z - donorAxleCenter.z;
                        rb.centerOfMass = matchedCom;
                        var targetVehicle = camper.vehicle; var sourceVehicle = donor.vehicle;
                        targetVehicle.MotorTorqueForward = sourceVehicle.MotorTorqueForward * radiusScale;
                        targetVehicle.MotorTorqueBackward = sourceVehicle.MotorTorqueBackward * radiusScale;
                        targetVehicle.MotorTorqueTurboForward = sourceVehicle.MotorTorqueTurboForward * radiusScale;
                        targetVehicle.MotorTorqueTurboBackward = sourceVehicle.MotorTorqueTurboBackward * radiusScale;
                        targetVehicle.brakeTorque = sourceVehicle.brakeTorque * radiusScale;
                    }
                    evidence = new { kind = "LIVE_NATIVE_4X4_PHYSICS_CANDIDATE", camper = camper.entityId, donor = donor.entityId,
                        before, mass = rb.mass, drag = rb.drag, angularDrag = rb.angularDrag,
                        matchForce, radiusScale, centerOfMass = Vec(rb.centerOfMass),
                        torqueForward = camper.vehicle.MotorTorqueForward, brakeTorque = camper.vehicle.brakeTorque,
                        note = "Geometry and wheel rest positions preserved; optional force mode accounts for wheel radius and axle center. Not persisted; clear-route handling test required." };
                }
                else throw new Exception(getHelp());
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string file = Path.Combine(CamperDiagnosticsMod.ExportFolder,"calibration_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(file,JsonConvert.SerializeObject(evidence,Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperCalibration] CANDIDATE " + file);
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperCalibration] " + error.Message); }
        }
    }
}
