#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools.Tests
{
    public sealed class VehicleStudioV06Tests
    {
        [Test] public void UnreviewedSteeringParentFailsBeforeSceneMutation()
        {
            var root = new GameObject("Workspace");
            var profile = ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            try
            {
                profile.targetVehicleName = "rebirthTestTruck";
                var origin = new GameObject("Origin"); origin.transform.SetParent(root.transform);
                var visuals = new GameObject("Model"); visuals.transform.SetParent(origin.transform);
                var chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chassis.name = "Chassis"; chassis.transform.SetParent(visuals.transform);
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "FrontWheel"; wheel.transform.SetParent(visuals.transform);
                var steer = new GameObject("Steer"); steer.transform.SetParent(origin.transform);
                var tire = new GameObject("Tire"); tire.transform.SetParent(steer.transform);
                profile.links.Add(new VehicleTransformLink {
                    partClass="wheel0", propertyName="steerTransform",
                    transformPath="Origin/Steer", authoredPlaceholder=true
                });
                profile.links.Add(new VehicleTransformLink {
                    partClass="wheel0", propertyName="tireTransform",
                    transformPath="Origin/Steer/Tire", authoredPlaceholder=true
                });
                profile.wheels.Add(new VehicleWheelLink {
                    partClass="wheel0", steerTransformPath="Origin/Steer",
                    tireTransformPath="Origin/Steer/Tire"
                });
                var ws = root.AddComponent<RebirthVehicleRigWorkspace>();
                ws.profile=profile; ws.origin=origin.transform; ws.importedVisuals=visuals;
                var plan = new RebirthVehicleAssemblyPlan.Plan {
                    sourceFbxAsset="Assets/TestTruck.fbx", profileAsset="Assets/TestTruck.asset",
                    targetVehicleName="rebirthTestTruck",
                    attachments=new[] { new RebirthVehicleAssemblyPlan.Attachment {
                        role="Wheel", visualPath="FrontWheel", targetPivotPath="Origin/Steer/Tire",
                        approved=true, pivotReviewed=true, allowUnreferencedTarget=false,
                        pivotVehiclePosition=new Vector3(1f,.5f,2f)
                    } }
                };
                Assert.Throws<InvalidDataException>(() => RebirthVehicleAssemblyPlan.Apply(
                    ws,plan,"Assets/TestTruck.fbx","Assets/TestTruck.asset"));
                Assert.AreEqual(Vector3.zero,steer.transform.localPosition);
                Assert.AreSame(visuals.transform,wheel.transform.parent);
                plan.pivotAdjustments=new[] { new RebirthVehicleAssemblyPlan.PivotAdjustment {
                    targetPivotPath="Origin/Steer", approved=true,pivotReviewed=true,
                    pivotVehiclePosition=new Vector3(1f,.5f,2f)
                } };
                var result=RebirthVehicleAssemblyPlan.Apply(ws,plan,
                    "Assets/TestTruck.fbx","Assets/TestTruck.asset");
                Assert.AreEqual(1,result.attached);
                Assert.AreEqual(1f,steer.transform.position.x,0.001f);
                Assert.AreEqual(1f,tire.transform.position.x,0.001f);
                Assert.AreSame(tire.transform,wheel.transform.parent);
                Assert.IsFalse(profile.links[0].authoredPlaceholder);
                Assert.IsFalse(profile.links[1].authoredPlaceholder);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test] public void ExportValidatorRejectsAuthoringScriptAndMissingGeometry()
        {
            var root = new GameObject("Vehicle");
            var profile = ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            try
            {
                root.AddComponent<RebirthVehicleRigWorkspace>();
                profile.seats.Add(new VehicleSeatAnchor { index=0, poseId="40", exits=new List<Vector3>{Vector3.one} });
                var preview=RebirthVehicleRigValidator.Inspect(root.transform,profile,true);
                Assert.IsTrue(preview.Exists(f=>f.code=="AUTHORING_SCRIPT_IN_BUNDLE"));
                Assert.IsTrue(preview.Exists(f=>f.code=="NO_VISUAL_GEOMETRY"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test] public void FbxAuditDescribesMeshesWithoutChangingOriginalPrefab()
        {
            string path = "Assets/REBIRTH_AuditTest_" + Guid.NewGuid().ToString("N") + ".prefab";
            var root=new GameObject("AuditTest");
            try
            {
                var mesh=GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.name="FrontLeftWheel";
                mesh.transform.SetParent(root.transform);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                Assert.IsNotNull(prefab);
                var report=RebirthVehicleFbxAudit.Inspect(prefab,path);
                Assert.AreEqual("rebirthVehicleFbxAudit",report.kind);
                Assert.AreEqual(1,report.geometry.Length);
                Assert.AreEqual("FrontLeftWheel",report.geometry[0].path);
                Assert.AreEqual(64,report.sourceSha256.Length);
                Assert.IsTrue(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
#endif
