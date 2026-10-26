#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Xml;
using NUnit.Framework;
using UnityEngine;
using RebirthVehicleInspector.UnityTools;

namespace RebirthVehicleInspector.UnityTools.Tests
{
    public sealed class VehicleAuthoringTests
    {
        private const string Capture = "{\"schemaVersion\":1,\"gameVersion\":\"SYNTHETIC\",\"xmlConfig\":{\"status\":\"ok\",\"name\":\"vehicleTestTruck\",\"seats\":[{\"index\":0,\"position\":\"-.4,.36,.06\",\"pose\":\"40\",\"rotation\":\"-25,0,0\",\"exit\":\"-1,0,0 ~ 1,0,0\"}],\"parts\":[{\"className\":\"handlebars\",\"properties\":[{\"key\":\"transform\",\"value\":\"Origin/steering_joint\"},{\"key\":\"IKHandLPosition\",\"value\":\"-.15,.12,.03\"}]}]}}";
        private const string Vehicles = "<vehicles><vehicle name='vehicleTestTruck'><property class='seat0'><property name='pose' value='40'/><property name='position' value='0,0,0'/></property><property class='handlebars'><property name='transform' value='Origin/steering_joint'/></property></vehicle></vehicles>";

        [Test] public void ImportPreservesSeatAndIkCoordinateSpaces()
        {
            var p=RebirthVehicleReferenceImporter.FromJson(Capture);
            try
            {
                Assert.AreEqual(1,p.seats.Count);
                Assert.AreEqual(0,p.seats[0].index);
                Assert.AreEqual("40",p.seats[0].poseId);
                Assert.AreEqual(-.4f,p.seats[0].position.x,0.0001f);
                Assert.AreEqual(2,p.seats[0].exits.Count);
                Assert.AreEqual("Origin/steering_joint",p.ik[0].pivotPath);
                Assert.IsTrue(p.ik[0].hasLeftHand);
                Assert.AreEqual(-.15f,p.ik[0].leftHandPosition.x,0.0001f);
            }
            finally {UnityEngine.Object.DestroyImmediate(p);}
        }
        [Test] public void ExportProducesSetAndAppendWithoutOverwritingSource()
        {
            var p=RebirthVehicleReferenceImporter.FromJson(Capture);
            try
            {
                var result=RebirthVehicleXmlExporter.Generate(p,Vehicles,"vehicleTestTruck");
                var doc=new XmlDocument(); doc.LoadXml(result.xml);
                Assert.IsNotNull(doc.SelectSingleNode("/configs/set[@xpath=\"/vehicles/vehicle[@name='vehicleTestTruck']/property[@class='seat0']/property[@name='position']/@value\"]"));
                Assert.IsTrue(result.appendCount>=1); // seat rotation/exits or hand IK not in base
                Assert.IsTrue(result.setCount>=2); // seat position/pose and steering transform
            }
            finally {UnityEngine.Object.DestroyImmediate(p);}
        }
        [Test] public void ExportRefusesTargetThatDoesNotExist()
        {
            var p=RebirthVehicleReferenceImporter.FromJson(Capture);
            try
            { Assert.Throws<InvalidDataException>(()=>RebirthVehicleXmlExporter.Generate(p,Vehicles,"missingVehicle")); }
            finally {UnityEngine.Object.DestroyImmediate(p);}
        }
        [Test] public void ModSpecExporterCarriesSeatWheelAndHandIk()
        {
            var profile=ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            var temp=Path.Combine(Path.GetTempPath(), "REBIRTHVehicleTest_"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                profile.seats.Add(new VehicleSeatAnchor {
                    index=0, poseId="40", position=new Vector3(-.4f,.3f,.1f),
                    rotationEuler=Vector3.zero,
                    exits=new System.Collections.Generic.List<Vector3> {new Vector3(-1f,0f,0f)}
                });
                profile.wheels.Add(new VehicleWheelLink {
                    partClass="wheel0", steerTransformPath="Origin/steer",
                    tireTransformPath="Origin/steer/tire"
                });
                profile.ik.Add(new VehicleIkAnchor {
                    partClass="handlebars", pivotPath="Origin/steer",
                    hasLeftHand=true, leftHandPosition=new Vector3(-.1f,.2f,.3f)
                });
                string bundle=Path.Combine(temp,"synthetic.unity3d");
                File.WriteAllBytes(bundle,new byte[256]); // Test fixture only; not an actual bundle
                string receipt=Path.Combine(temp,"synthetic.unity3d.rebirth-receipt.json");
                string escapedBundle=bundle.Replace("\\", "\\\\");
                File.WriteAllText(receipt,
                    "{\"schemaVersion\":1,\"prefabVerified\":true,\"buildTarget\":\"StandaloneWindows64\","+
                    "\"bundlePath\":\""+escapedBundle+"\",\"prefabName\":\"SyntheticVehicle\"}");
                string output=Path.Combine(temp,"mod_spec.json");
                RebirthVehicleModSpecExporter.ExportToFile(profile,
                    "vehicleDonor4x4", "vehicleDonor4x4", "vehicleDonor4x4Placeable",
                    "rebirthPickup", "rebirthPickup", "rebirthPickupPlaceable",
                    receipt, output, false);
                var content=File.ReadAllText(output);
                Assert.That(content,Does.Contain("rebirthPickupPlaceable"));
                Assert.That(content,Does.Contain("seat0"));
                Assert.That(content,Does.Contain("wheel0"));
                Assert.That(content,Does.Contain("IKHandLPosition"));
                Assert.That(content,Does.Contain("\"prefabName\":\"SyntheticVehicle\""));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                Directory.Delete(temp,true);
            }
        }

        [Test] public void WheelMapperRecognizesOnlyExplicitCorners()
        {
            Assert.AreEqual("front-left",RebirthVehicleWheelMapper.DetectCorner("frontLeftWheel_joint"));
            Assert.AreEqual("front-right",RebirthVehicleWheelMapper.DetectCorner("wheel_FR"));
            Assert.AreEqual("rear-left",RebirthVehicleWheelMapper.DetectCorner("back_left_tire"));
            Assert.AreEqual("rear-right",RebirthVehicleWheelMapper.DetectCorner("rearRightWheel"));
            Assert.AreEqual("",RebirthVehicleWheelMapper.DetectCorner("wheel0"));
            Assert.AreEqual("",RebirthVehicleWheelMapper.DetectCorner("FrontWheel"));
            Assert.AreEqual("",RebirthVehicleWheelMapper.DetectCorner("front_rear_left_right"));
            Assert.IsTrue(RebirthVehicleWheelMapper.LooksLikeWheelVisual("LeftFrontWheel"));
            Assert.IsFalse(RebirthVehicleWheelMapper.LooksLikeWheelVisual("VehicleBody"));
        }
        [Test] public void WheelMapperOnlyProposesOneUnambiguousNamedMesh()
        {
            var root=new GameObject("AuthoringWorkspace");
            var profile=ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            try
            {
                var visualRoot=new GameObject("ImportedFbxSceneInstance");
                visualRoot.transform.SetParent(root.transform);
                var wheelMesh=new GameObject("frontLeftWheel");
                wheelMesh.transform.SetParent(visualRoot.transform);
                wheelMesh.AddComponent<MeshFilter>();
                wheelMesh.AddComponent<MeshRenderer>();
                var workspace=root.AddComponent<RebirthVehicleRigWorkspace>();
                workspace.profile=profile;
                workspace.importedVisuals=visualRoot;
                bool created;
                RebirthVehicleRigBuilder.EnsurePath(root.transform,"Origin/frontLeftWheel_joint",out created);
                profile.wheels.Add(new VehicleWheelLink {
                    partClass="wheel0",tireTransformPath="Origin/frontLeftWheel_joint"
                });
                var proposals=RebirthVehicleWheelMapper.Discover(workspace);
                Assert.AreEqual(1,proposals.Count);
                Assert.AreEqual("candidate",proposals[0].outcome);
                Assert.AreSame(wheelMesh.transform,proposals[0].selectedMesh);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
        [Test] public void WheelMapperRejectsAmbiguousDuplicateFbxNames()
        {
            var root=new GameObject("AuthoringWorkspace");
            var profile=ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            try
            {
                var visualRoot=new GameObject("ImportedFbxSceneInstance");
                visualRoot.transform.SetParent(root.transform);
                for(int i=0;i<2;i++)
                {
                    var wheelMesh=new GameObject("frontLeftWheel"+i);
                    wheelMesh.transform.SetParent(visualRoot.transform);
                    wheelMesh.AddComponent<MeshFilter>();
                    wheelMesh.AddComponent<MeshRenderer>();
                }
                var workspace=root.AddComponent<RebirthVehicleRigWorkspace>();
                workspace.profile=profile; workspace.importedVisuals=visualRoot;
                bool created;
                RebirthVehicleRigBuilder.EnsurePath(root.transform,"Origin/frontLeftWheel_joint",out created);
                profile.wheels.Add(new VehicleWheelLink {
                    partClass="wheel0",tireTransformPath="Origin/frontLeftWheel_joint"
                });
                var proposals=RebirthVehicleWheelMapper.Discover(workspace);
                Assert.AreEqual(1,proposals.Count);
                Assert.AreEqual("no-proposal",proposals[0].outcome);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
        [Test] public void ReviewedAssemblyMovesOnlyVisualInstanceAndSetsCalibratedPivot()
        {
            var root=new GameObject("Workspace");
            var profile=ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            try
            {
                profile.targetVehicleName="rebirthTestTruck";
                var origin=new GameObject("Origin"); origin.transform.SetParent(root.transform);
                var visuals=new GameObject("FBX"); visuals.transform.SetParent(origin.transform);
                var wheel=new GameObject("frontLeftWheel"); wheel.transform.SetParent(visuals.transform);
                wheel.AddComponent<MeshRenderer>();
                var chassis=new GameObject("chassis");chassis.transform.SetParent(visuals.transform);
                chassis.AddComponent<MeshRenderer>();
                bool created;
                var pivot=RebirthVehicleRigBuilder.EnsurePath(root.transform,"Origin/steer/tire",out created);
                profile.wheels.Add(new VehicleWheelLink {partClass="wheel0",tireTransformPath="Origin/steer/tire"});
                var ws=root.AddComponent<RebirthVehicleRigWorkspace>();
                ws.origin=origin.transform; ws.profile=profile; ws.importedVisuals=visuals;
                var plan=new RebirthVehicleAssemblyPlan.Plan {
                    sourceFbxAsset="Assets/Test.fbx",profileAsset="Assets/Test.asset",
                    targetVehicleName="rebirthTestTruck",
                    attachments=new[] {new RebirthVehicleAssemblyPlan.Attachment {
                        role="Wheel", visualPath="frontLeftWheel", targetPivotPath="Origin/steer/tire",
                        approved=true,pivotReviewed=true,
                        pivotVehiclePosition=new Vector3(1,2,3),pivotVehicleRotationEuler=Vector3.zero
                    }}
                };
                var result=RebirthVehicleAssemblyPlan.Apply(ws,plan,"Assets/Test.fbx","Assets/Test.asset");
                Assert.AreEqual(1,result.attached);
                Assert.AreSame(pivot,wheel.transform.parent);
                Assert.AreEqual(1,pivot.position.x,0.001f);
                Assert.AreEqual(3,pivot.position.z,0.001f);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(profile);}
        }
        [Test] public void UnreviewedAssemblyCannotMutateScene()
        {
            var root=new GameObject("Workspace");
            var profile=ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            try
            {
                profile.targetVehicleName="rebirthTestTruck";
                var origin=new GameObject("Origin"); origin.transform.SetParent(root.transform);
                var visuals=new GameObject("FBX"); visuals.transform.SetParent(origin.transform);
                var wheel=new GameObject("frontLeftWheel"); wheel.transform.SetParent(visuals.transform);
                wheel.AddComponent<MeshRenderer>();
                bool created;
                var pivot=RebirthVehicleRigBuilder.EnsurePath(root.transform,"Origin/wheel",out created);
                profile.wheels.Add(new VehicleWheelLink {partClass="wheel0",tireTransformPath="Origin/wheel"});
                var ws=root.AddComponent<RebirthVehicleRigWorkspace>();
                ws.origin=origin.transform;ws.profile=profile;ws.importedVisuals=visuals;
                var plan=new RebirthVehicleAssemblyPlan.Plan {
                    sourceFbxAsset="Assets/Test.fbx",profileAsset="Assets/Test.asset",
                    targetVehicleName="rebirthTestTruck",attachments=new[] {
                        new RebirthVehicleAssemblyPlan.Attachment {
                            role="Wheel",visualPath="frontLeftWheel",targetPivotPath="Origin/wheel",
                            approved=false,pivotReviewed=false
                        }
                    }
                };
                Assert.Throws<InvalidDataException>(()=>RebirthVehicleAssemblyPlan.Apply(ws,plan,"Assets/Test.fbx","Assets/Test.asset"));
                Assert.AreSame(visuals.transform,wheel.transform.parent);
                Assert.AreEqual(Vector3.zero,pivot.position);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(profile);}
        }
        [Test] public void ApprovedCustomRotorCreatesClearlyUnreferencedPivot()
        {
            var root=new GameObject("Workspace");
            var profile=ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            try
            {
                profile.targetVehicleName="rebirthHelicopter";
                var origin=new GameObject("Origin");origin.transform.SetParent(root.transform);
                var visuals=new GameObject("FBX");visuals.transform.SetParent(origin.transform);
                var rotor=new GameObject("mainRotor");rotor.transform.SetParent(visuals.transform);
                rotor.AddComponent<MeshRenderer>();
                var ws=root.AddComponent<RebirthVehicleRigWorkspace>();
                ws.origin=origin.transform;ws.profile=profile;ws.importedVisuals=visuals;
                var plan=new RebirthVehicleAssemblyPlan.Plan {
                    sourceFbxAsset="Assets/Helicopter.fbx",profileAsset="Assets/Helicopter.asset",
                    targetVehicleName="rebirthHelicopter",attachments=new[]{
                        new RebirthVehicleAssemblyPlan.Attachment {
                            role="Rotor",visualPath="mainRotor",
                            targetPivotPath="Origin/REBIRTH_CustomJoints/mainRotor",
                            approved=true,pivotReviewed=true,allowUnreferencedTarget=true,
                            pivotVehiclePosition=new Vector3(0,3,0)
                        }
                    }
                };
                var result=RebirthVehicleAssemblyPlan.Apply(ws,plan,"Assets/Helicopter.fbx","Assets/Helicopter.asset");
                Assert.AreEqual(1,result.attached);
                var joint=RebirthVehicleRigBuilder.FindPath(root.transform,"Origin/REBIRTH_CustomJoints/mainRotor");
                Assert.IsNotNull(joint);
                Assert.AreSame(joint,rotor.transform.parent);
                Assert.AreEqual(3,joint.position.y,0.001f);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(profile);}
        }
        [Test] public void UnreviewedUnreferencedPivotCannotBeCreated()
        {
            var root=new GameObject("Workspace");
            var profile=ScriptableObject.CreateInstance<RebirthVehicleRigProfile>();
            try
            {
                profile.targetVehicleName="rebirthBoat";
                var origin=new GameObject("Origin");origin.transform.SetParent(root.transform);
                var visuals=new GameObject("FBX");visuals.transform.SetParent(origin.transform);
                var propeller=new GameObject("propeller");propeller.transform.SetParent(visuals.transform);
                propeller.AddComponent<MeshRenderer>();
                var ws=root.AddComponent<RebirthVehicleRigWorkspace>();
                ws.origin=origin.transform;ws.profile=profile;ws.importedVisuals=visuals;
                var plan=new RebirthVehicleAssemblyPlan.Plan {
                    sourceFbxAsset="Assets/Boat.fbx",profileAsset="Assets/Boat.asset",
                    targetVehicleName="rebirthBoat",attachments=new[]{
                        new RebirthVehicleAssemblyPlan.Attachment {
                            role="Propeller",visualPath="propeller",targetPivotPath="Origin/propeller",
                            approved=true,pivotReviewed=true,allowUnreferencedTarget=false
                        }
                    }
                };
                Assert.Throws<InvalidDataException>(()=>RebirthVehicleAssemblyPlan.Apply(ws,plan,"Assets/Boat.fbx","Assets/Boat.asset"));
                Assert.IsNull(RebirthVehicleRigBuilder.FindPath(root.transform,"Origin/propeller"));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(profile);}
        }
        [Test] public void AssemblyRejectsAmbiguousSourceNames()
        {
            var root=new GameObject("FBX");
            try
            {
                new GameObject("wheel").transform.SetParent(root.transform);
                new GameObject("wheel").transform.SetParent(root.transform);
                Assert.IsNull(RebirthVehicleAssemblyPlan.FindUniqueDescendant(root.transform,"wheel"));
                Assert.IsNull(RebirthVehicleAssemblyPlan.FindUniqueDescendant(root.transform,"../wheel"));
                Assert.IsNull(RebirthVehicleAssemblyPlan.FindUniqueDescendant(root.transform,"wheel/"));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void RigPathsAreExactAndGeneratedWithoutTouchingImportedFbx()
        {
            var root=new GameObject("VehicleRoot");
            try
            {
                bool created;
                var joint=RebirthVehicleRigBuilder.EnsurePath(root.transform,"Origin/steer/wheel",out created);
                Assert.IsTrue(created);
                Assert.AreEqual("wheel",joint.name);
                Assert.AreSame(joint,RebirthVehicleRigBuilder.FindPath(root.transform,"Origin/steer/wheel"));
                Assert.IsNull(RebirthVehicleRigBuilder.FindPath(root.transform,"Origin/invalid"));
                Assert.Throws<ArgumentException>(()=>RebirthVehicleRigBuilder.EnsurePath(root.transform,"../escape",out created));
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
#endif
