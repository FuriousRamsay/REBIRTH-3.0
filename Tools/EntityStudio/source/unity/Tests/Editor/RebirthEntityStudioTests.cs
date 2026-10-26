using NUnit.Framework;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio.Tests
{
    public sealed class RebirthEntityStudioTests
    {
        [Test]
        public void PhysicsLayoutPreservesNativeCollisionAndRagdollModes()
        {
            var root=new GameObject("Origin");root.tag="E_BP_BipedRoot";
            try
            {
                var head=new GameObject("Head");head.transform.SetParent(root.transform,false);head.tag="E_BP_Head";head.AddComponent<CapsuleCollider>();
                var mode=new EntityColliderModeSpec{bone="Head",ragdollScale=new Vector3(.8f,.88f,1),ragdollOffset=new Vector3(0,.02f,0)};
                var xml=System.Xml.Linq.XDocument.Parse(RebirthEntityPhysicsLayoutExporter.BuildPatch(root.transform,"ModeFixture",new[]{mode}));
                var entry=xml.Descendants("collider").Single();
                string Value(string name){return (string)entry.Elements("property").Single(value=>(string)value.Attribute("name")==name).Attribute("value");}
                Assert.That(Value("collisionScale"),Is.EqualTo("1 1 1"));Assert.That(Value("ragdollScale"),Is.EqualTo("0.8 0.88 1"));
                Assert.That(Value("ragdollOffset"),Is.EqualTo("0 0.02 0"));Assert.That(Value("path"),Is.EqualTo("Head"));Assert.That(Value("ragdollLayer"),Is.EqualTo("21"));
            }
            finally{Object.DestroyImmediate(root);}
        }

        [Test]
        public void FarmerFullHumanoidHasOrderedFingerToeAndRollChains()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                profile.InitializeBipedDefaults();RebirthFarmerModelInspection.ApplyRebirthFarmerLandmarks(profile);
                int count=profile.bones.Count;RebirthFarmerModelInspection.ApplyRebirthFarmerLandmarks(profile);Assert.That(profile.bones.Count,Is.EqualTo(count));
                Assert.That(profile.bones.Count,Is.GreaterThanOrEqualTo(65));var seen=new HashSet<string>{""};
                foreach(var bone in profile.bones){if(bone.name!="Origin")Assert.That(seen.Contains(bone.parent),Is.True,bone.name);Assert.That(seen.Add(bone.name),Is.True,bone.name);}
                foreach(string side in new[]{"Left","Right"})
                {
                    Assert.That(profile.bones.Find(value=>value.name==side+"ForeArm").parent,Is.EqualTo(side+"ArmRoll"));
                    Assert.That(profile.bones.Find(value=>value.name==side+"Hand").parent,Is.EqualTo(side+"ForeArmRoll"));
                    Assert.That(profile.bones.Find(value=>value.name==side+"ToeBase").deformsMesh,Is.True);
                    foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int joint=1;joint<=4;joint++)
                    {var bone=profile.bones.Find(value=>value.name==side+"Hand"+finger+joint);Assert.That(bone,Is.Not.Null);Assert.That(bone.deformsMesh,Is.EqualTo(joint<4));}
                }
            }
            finally{Object.DestroyImmediate(profile);}
        }

        [Test]
        public void ConcaveArmCapUsesBoundaryVerticesWithoutCentralShortcuts()
        {
            Vector3[] boundary={new Vector3(0,0,0),new Vector3(0,2,0),new Vector3(0,2,1),new Vector3(0,1,1),new Vector3(0,1,2),new Vector3(0,0,2)};
            int[] triangles=RebirthEntityCapTriangulation.Triangulate(boundary);
            Assert.That(triangles.Length,Is.EqualTo(12));float area=0;
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=boundary[triangles[i]],b=boundary[triangles[i+1]],c=boundary[triangles[i+2]];
                area+=Vector3.Cross(b-a,c-a).magnitude*.5f;
                Vector3 center=(a+b+c)/3f;Assert.That(center.y<=1f||center.z<=1f,Is.True,"No cap triangle may cross the concave outline.");
            }
            Assert.That(area,Is.EqualTo(3f).Within(.0001f));
        }

        [Test]
        public void LodProjectionKeepsCoincidentArmAndBodyCapsSeparate()
        {
            var source=new Mesh();var lod=new Mesh();
            try
            {
                source.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.zero,Vector3.right,Vector3.up};source.triangles=new[]{0,1,2,3,4,5};
                source.boneWeights=Enumerable.Range(0,6).Select(index=>new BoneWeight{boneIndex0=index<3?0:1,weight0=1}).ToArray();
                lod.vertices=new[]{new Vector3(.25f,.25f,.01f),Vector3.right,Vector3.up};lod.triangles=new[]{0,1,2};
                lod.boneWeights=Enumerable.Range(0,3).Select(index=>new BoneWeight{boneIndex0=1,weight0=1}).ToArray();
                RebirthEntityLodSurface.Project(source,lod,new[]{0,1});
                foreach(BoneWeight weight in lod.boneWeights){Assert.That(weight.boneIndex0,Is.EqualTo(1));Assert.That(weight.weight0,Is.EqualTo(1f).Within(.0001f));}
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(lod);}
        }

        [Test]
        public void LodProjectionRemovesOutlyingVerticesAndTransfersSkinWeights()
        {
            var source=new Mesh();var lod=new Mesh();
            try
            {
                source.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};source.triangles=new[]{0,1,2};
                source.boneWeights=new[]{new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=1,weight0=1},new BoneWeight{boneIndex0=2,weight0=1}};
                lod.vertices=new[]{new Vector3(.25f,.25f,7f),new Vector3(5f,0f,0f),new Vector3(0f,5f,0f)};lod.triangles=new[]{0,1,2};
                RebirthEntityLodSurface.Project(source,lod);
                Assert.That(Vector3.Distance(lod.vertices[0],new Vector3(.25f,.25f,0f)),Is.LessThan(.0001f));
                Assert.That(lod.vertices[1],Is.EqualTo(Vector3.right));Assert.That(lod.vertices[2],Is.EqualTo(Vector3.up));
                BoneWeight weight=lod.boneWeights[0];Assert.That(weight.boneIndex0,Is.EqualTo(0));Assert.That(weight.weight0,Is.EqualTo(.5f).Within(.0001f));
                Assert.That(weight.weight0+weight.weight1+weight.weight2+weight.weight3,Is.EqualTo(1f).Within(.0001f));
                Assert.That(lod.boneWeights[1].boneIndex0,Is.EqualTo(1));Assert.That(lod.boneWeights[2].boneIndex0,Is.EqualTo(2));
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(lod);}
        }

        [Test]
        public void BoxTorsoAndLegsExportResolvableNativePhysicsPaths()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            GameObject source=CreateSyntheticUnriggedHumanoid(out Mesh denseTorso);RebirthEntityRigWorkspace workspace=null;
            var meshes=new List<Mesh>();
            try
            {
                profile.InitializeBipedDefaults();FitSyntheticProfileToSource(profile,source.transform);
                profile.useBoxTorsoAndLegs=true;profile.generateLods=false;profile.generateTierMaterials=false;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);meshes.AddRange(workspace.sourceSkinnedMeshes.Select(renderer=>renderer.sharedMesh));
                Assert.That(workspace.generatedSkeleton.GetComponentsInChildren<BoxCollider>(true).Length,Is.EqualTo(6));
                Assert.That(workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true).Length,Is.EqualTo(5));
                Assert.That(workspace.colliderFitReports.Count,Is.EqualTo(11));
                var hip=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"Hips").GetComponent<BoxCollider>();
                // The synthetic capsule depth plus the configured fit padding is
                // slightly over .4 m; the limit includes that authored padding.
                Assert.That(hip.size.x,Is.LessThan(.6f));Assert.That(hip.size.z,Is.LessThan(.42f));
                var xml=System.Xml.Linq.XDocument.Parse(RebirthEntityPhysicsLayoutExporter.BuildPatch(workspace.generatedSkeleton,"BoxFitFixture"));
                var entries=xml.Descendants("collider").ToArray();Assert.That(entries.Length,Is.EqualTo(11));
                foreach(var entry in entries)
                {
                    string path=entry.Elements("property").First(value=>(string)value.Attribute("name")=="path").Attribute("value").Value;
                    Transform bone=workspace.generatedSkeleton.Find(path);Assert.That(bone,Is.Not.Null,path);
                    Assert.That(bone.GetComponent<Collider>(),Is.Not.Null,path);
                    Assert.That(bone.GetComponent<Rigidbody>(),Is.Not.Null,path);
                }
                Assert.That(xml.ToString(),Does.Not.Contain("LeftArmRoll/LeftForeArm"));
                Assert.That(RebirthEntityRigValidator.Inspect(workspace).Any(finding=>finding.severity=="ERROR"),Is.False);
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);Object.DestroyImmediate(source);Object.DestroyImmediate(profile);Object.DestroyImmediate(denseTorso);
                foreach(var mesh in meshes)if(mesh!=null)Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void SavedWorkspaceRetainsRendererReferencesAfterSceneReload()
        {
            string folder="Assets/EntityStudioGenerated/ScenePersistence_"+System.Guid.NewGuid().ToString("N");
            EnsureAssetFolder(folder);
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                profile.InitializeBipedDefaults();profile.generateLods=false;profile.generateTierMaterials=false;
                profile.createHitColliders=false;profile.createRagdoll=false;
                AssetDatabase.CreateAsset(profile,folder+"/Profile.asset");
                var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source,scene);
                var workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(workspace.gameObject,scene);
                AssetDatabase.CreateAsset(workspace.sourceSkinnedMeshes[0].sharedMesh,folder+"/Mesh.asset");
                AssetDatabase.SaveAssets();
                Assert.That(UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,folder+"/Review.unity"),Is.True);
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
                scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(folder+"/Review.unity",UnityEditor.SceneManagement.OpenSceneMode.Single);
                var restored=scene.GetRootGameObjects().Select(root=>root.GetComponent<RebirthEntityRigWorkspace>()).First(value=>value!=null);
                RebirthEntityRigBuilder.BindWorkspaceReferences(restored);
                Assert.That(restored.sourceSkinnedMeshes.Count,Is.EqualTo(1));
                Assert.That(restored.sourceSkinnedMeshes[0],Is.Not.Null);
                Assert.That(restored.sourceSkinnedMeshes[0].sharedMesh.vertexCount,Is.GreaterThan(0));
                Assert.That(restored.profile.sourceSkinnedMesh,Is.EqualTo(restored.sourceSkinnedMeshes[0]));
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void RadialFitRecentersAnOffsetLimbInsteadOfInflatingItsRadius()
        {
            var points=new List<Vector2>();
            for(int i=0;i<64;i++){float angle=i*Mathf.PI*2f/64;points.Add(new Vector2(.2f+Mathf.Cos(angle)*.05f,-.1f+Mathf.Sin(angle)*.05f));}
            var circle=RebirthEntityRigBuilder.MinimumRadialCircle(points);
            Assert.That(Vector2.Distance(circle.center,new Vector2(.2f,-.1f)),Is.LessThan(.0001f));
            Assert.That(circle.radius,Is.EqualTo(.05f).Within(.0001f));
        }

        [Test]
        public void SurfaceDistancesFollowTheMeshAroundAJoinInsteadOfJumpingAcrossAGap()
        {
            var points=new List<Vector3>();var triangles=new List<int>();
            for(int side=0;side<2;side++)
                for(int row=0;row<5;row++)
                    for(int edge=0;edge<2;edge++)points.Add(new Vector3(side*.05f,row*.25f,edge*.02f));
            for(int side=0;side<2;side++)
                for(int row=0;row<4;row++)
                {int at=side*10+row*2;triangles.AddRange(new[]{at,at+2,at+1,at+1,at+2,at+3});}
            triangles.AddRange(new[]{8,18,9,9,18,19});
            var eligible=new[]{new bool[20],new bool[20]};
            for(int i=0;i<20;i++){eligible[0][i]=i<10;eligible[1][i]=i>=10;}
            float[][] distances=RebirthEntitySurfaceWeights.Build(points.ToArray(),triangles.ToArray(),
                new[]{new Vector3(0f,0f,.01f),new Vector3(.05f,0f,.01f)},
                new[]{new Vector3(0f,1f,.01f),new Vector3(.05f,1f,.01f)},eligible,1f);
            Assert.That(distances[0][0],Is.LessThan(.3f));
            Assert.That(distances[1][0],Is.GreaterThan(1f),"The opposite strip is close in space but connected only at the top.");
        }

        [Test]
        public void CompoundTerminalShapesShareNativeBodyChainAcrossPhysicsRebuilds()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            GameObject source=CreateSyntheticUnriggedHumanoid(out Mesh mesh);RebirthEntityRigWorkspace workspace=null;
            try
            {
                profile.InitializeBipedDefaults();RebirthFarmerFullHumanoid.Extend(profile);FitSyntheticProfileToSource(profile,source.transform);
                profile.generateLods=false;profile.generateTierMaterials=false;profile.compoundTerminalColliders=true;profile.includeTerminalPartsInColliders=true;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                for(int pass=0;pass<2;pass++)
                {
                    if(pass>0)RebirthEntityRigBuilder.BuildPhysics(workspace);
                    Assert.That(workspace.generatedSkeleton.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(15));
                    Assert.That(workspace.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true).Length,Is.EqualTo(11));
                    Assert.That(workspace.generatedSkeleton.GetComponentsInChildren<CharacterJoint>(true).Length,Is.EqualTo(10));
                    int compounds=0;
                    foreach(var collider in workspace.generatedSkeleton.GetComponentsInChildren<Collider>(true))
                    {
                        if(!collider.name.StartsWith("EntityStudio_Compound_",System.StringComparison.Ordinal))continue;compounds++;
                        Assert.That(collider.GetComponent<Rigidbody>(),Is.Null);Assert.That(collider.attachedRigidbody,Is.Not.Null);
                        Assert.That(collider.tag,Does.StartWith("E_BP_"));
                    }
                    Assert.That(compounds,Is.EqualTo(4));
                    Assert.That(RebirthEntityRigValidator.Inspect(workspace).Exists(finding=>finding.code=="RAGDOLL_BODY_COUNT"),Is.False);
                }
            }
            finally{if(workspace!=null)UnityEngine.Object.DestroyImmediate(workspace.gameObject);UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(profile);}
        }

        [Test]
        public void RepairWeightsInterpolateSourceBoundaryWithoutChangingOriginalWeights()
        {
            var weights=new[]{new BoneWeight{boneIndex0=0,weight0=1},new BoneWeight{boneIndex0=1,weight0=1},new BoneWeight{boneIndex0=2,weight0=1},new BoneWeight{boneIndex0=9,weight0=1}};
            var input=new EntitySurfaceRepairInput{repairVertexIds=new[]{0,0,0,1},repairWeightAnchors=new[]{Vector3Int.zero,Vector3Int.zero,Vector3Int.zero,new Vector3Int(0,1,2)},repairWeightBarycentrics=new[]{Vector3.zero,Vector3.zero,Vector3.zero,new Vector3(.25f,.25f,.5f)}};
            RebirthEntityRepairWeights.Interpolate(input,weights);
            Assert.That(weights[0].boneIndex0,Is.EqualTo(0));Assert.That(weights[0].weight0,Is.EqualTo(1));
            Assert.That(weights[1].boneIndex0,Is.EqualTo(1));Assert.That(weights[2].boneIndex0,Is.EqualTo(2));
            Assert.That(weights[3].boneIndex0,Is.EqualTo(2));Assert.That(weights[3].weight0,Is.EqualTo(.5f).Within(1e-6f));
            Assert.That(weights[3].boneIndex1,Is.EqualTo(0));Assert.That(weights[3].weight1,Is.EqualTo(.25f).Within(1e-6f));
            Assert.That(weights[3].boneIndex2,Is.EqualTo(1));Assert.That(weights[3].weight2,Is.EqualTo(.25f).Within(1e-6f));
        }

        [Test]
        public void NarrowFingerSeedMarginDoesNotSeedAnAdjacentDisconnectedSurface()
        {
            var points=new[]{new Vector3(0,.3f,0),new Vector3(.001f,.3f,0),new Vector3(0,.7f,0),new Vector3(.001f,.7f,0),
                new Vector3(.006f,.3f,0),new Vector3(.007f,.3f,0),new Vector3(.006f,.7f,0),new Vector3(.007f,.7f,0)};
            var triangles=new[]{0,2,1,1,2,3,4,6,5,5,6,7};
            var starts=new[]{new Vector3(0,.2f,0)};var ends=new[]{new Vector3(0,.8f,0)};
            float[][] broad=RebirthEntitySurfaceWeights.Build(points,triangles,starts,ends,null,1.8f);
            float[][] narrow=RebirthEntitySurfaceWeights.Build(points,triangles,starts,ends,null,1.8f,null,new[]{.00135f});
            Assert.That(float.IsInfinity(broad[0][4]),Is.False,"The old broad margin seeds the neighbouring sheet directly.");
            Assert.That(float.IsInfinity(narrow[0][4]),Is.True,"A narrow margin must not bypass disconnected surface topology.");
            Assert.That(narrow[0][0],Is.LessThan(.002f));
        }

        [Test]
        public void ElbowRotationMovesForearmWhileUpperArmStaysNearItsRestPose()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            GameObject source=CreateSyntheticUnriggedHumanoid(out Mesh denseTorso);
            RebirthEntityRigWorkspace workspace=null;
            var bakedBefore=new Mesh();var bakedAfter=new Mesh();
            var meshes=new List<Mesh>();
            try
            {
                profile.InitializeBipedDefaults();FitSyntheticProfileToSource(profile,source.transform);
                profile.generateLods=false;profile.generateTierMaterials=false;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                meshes.AddRange(profile.sourceSkinnedMeshes.Select(renderer=>renderer.sharedMesh));
                Transform elbow=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"LeftForeArm");
                Assert.That(RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"Hips").GetComponent<Rigidbody>(),Is.Not.Null);
                Assert.That(elbow.GetComponent<Rigidbody>(),Is.Not.Null);
                Assert.That(elbow.GetComponent<CharacterJoint>().connectedBody.transform.name,Is.EqualTo("LeftArm"));
                Assert.That(elbow.GetComponent<CharacterJoint>().autoConfigureConnectedAnchor,Is.False);
                Assert.That(elbow.GetComponent<CharacterJoint>().lowTwistLimit.limit,Is.EqualTo(-90f));
                Assert.That(elbow.GetComponent<CharacterJoint>().highTwistLimit.limit,Is.EqualTo(0f));
                Assert.That(Vector3.Dot(elbow.TransformDirection(elbow.GetComponent<CharacterJoint>().axis),Vector3.right),Is.GreaterThan(.99f));
                Assert.That(workspace.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true).Sum(body=>body.mass),Is.EqualTo(profile.ragdollMass).Within(.01f));
                var upper=profile.sourceSkinnedMeshes.First(renderer=>renderer.name.EndsWith("_LeftUpperArm"));
                var lower=profile.sourceSkinnedMeshes.First(renderer=>renderer.name.EndsWith("_LeftLowerArm"));
                Vector3[] upperBefore=WorldVertices(upper),lowerBefore=WorldVertices(lower);
                elbow.localRotation*=Quaternion.Euler(70f,0f,0f);
                float upperMotion=MeanDistance(upperBefore,WorldVertices(upper));
                float lowerMotion=MeanDistance(lowerBefore,WorldVertices(lower));
                Assert.That(lowerMotion,Is.GreaterThan(.05f));
                Assert.That(upperMotion,Is.LessThan(lowerMotion*.35f),"Bending the elbow must not drag the upper arm as if it belonged to the forearm.");
                RebirthEntityRigBuilder.BuildPhysics(workspace);
                Assert.That(RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"Hips"),Is.Not.Null,"Rebuilding physics must preserve deforming bones.");
                Assert.That(workspace.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true).Length,Is.EqualTo(11));
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);Object.DestroyImmediate(profile);Object.DestroyImmediate(denseTorso);
                Object.DestroyImmediate(bakedBefore);Object.DestroyImmediate(bakedAfter);
                foreach(var mesh in meshes)if(mesh!=null)Object.DestroyImmediate(mesh);
            }
            float MeanDistance(Vector3[] before,Vector3[] after)
            {float total=0f;for(int i=0;i<before.Length;i++)total+=(after[i]-before[i]).magnitude;return total/before.Length;}
            Vector3[] WorldVertices(SkinnedMeshRenderer renderer)
            {
                var points=renderer.sharedMesh.vertices;var weights=renderer.sharedMesh.boneWeights;var bindposes=renderer.sharedMesh.bindposes;
                var matrices=renderer.bones.Select((bone,index)=>bone.localToWorldMatrix*bindposes[index]).ToArray();
                for(int i=0;i<points.Length;i++)
                {Vector3 p=points[i];BoneWeight w=weights[i];points[i]=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;}
                return points;
            }
        }

        [Test]
        public void DefaultProfileContainsGameBipedContracts()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                profile.InitializeBipedDefaults();
                Assert.That(profile.bones.Exists(b=>b.name=="Origin"&&b.gameTag=="E_BP_BipedRoot"),Is.True);
                Assert.That(profile.bones.Exists(b=>b.name=="Head"&&b.gameTag=="E_BP_Head"),Is.True);
                Assert.That(profile.bones.Exists(b=>b.name=="LeftLeg"&&b.gameTag=="E_BP_LLowerLeg"),Is.True);
                Assert.That(profile.requiredGoreAnchors.Count,Is.EqualTo(9));
                Assert.That(profile.reviewedTargetCompatibility,Is.False);
                Assert.That(profile.targetCompatibilityEvidence,Is.Empty);
                Assert.That(profile.requiredGoreAnchors.Contains("L_LeftUpperArmGore"),Is.True);
                Assert.That(profile.requiredGoreAnchors.Contains("L_RightUpperLegGore"),Is.True);
                AssertColor(profile.radiatedTint,new Color(.295f,1f,0f,0f));
                AssertColor(profile.chargedTint,new Color(0f,.8f,3f,.7f));
                AssertColor(profile.infernalTint,new Color(2.7f,.8f,0f,.55f));
                profile.requiredGoreAnchors=new List<string>{"L_HeadGore","L_LeftLowerArmGore","L_RightLowerArmGore","L_LeftLowerLegGore","L_RightLowerLegGore"};
                profile.EnsureDefaultGoreAnchors();
                Assert.That(profile.requiredGoreAnchors.Count,Is.EqualTo(9));
                profile.requiredEyeAnchors=new List<string>{"FeralEye"};
                profile.EnsureDefaultEyeAnchors();
                Assert.That(profile.requiredEyeAnchors.Contains("NormalEye"),Is.True);
                Assert.That(profile.requiredEyeAnchors.Contains("FeralEye"),Is.True);
                Assert.That(RebirthEntityRigBuilder.EstimateBodySegments(profile),Is.GreaterThanOrEqualTo(20));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void TierSetContainsFiveRequiredVariants()
        {
            Assert.That(System.Enum.GetNames(typeof(ZombieTier)),Is.EqualTo(new[]{"Normal","Feral","Radiated","Charged","Infernal"}));
        }

        [Test]
        public void SkinWeightsDoNotLeakAcrossDisconnectedNearbyBranches()
        {
            var starts=new[]{Vector3.zero,new Vector3(0f,1f,0f),new Vector3(0f,1f,0f),new Vector3(.05f,0f,0f)};
            var ends=new[]{new Vector3(0f,1f,0f),new Vector3(1f,1f,0f),new Vector3(-1f,1f,0f),new Vector3(.05f,1f,0f)};
            var parents=new[]{-1,0,0,-1};

            BoneWeight weight=RebirthEntityRigBuilder.CalculateConnectedBoneWeight(new Vector3(.4f,.92f,0f),starts,ends,parents,4f);

            Assert.That(weight.weight0+weight.weight1+weight.weight2+weight.weight3,Is.EqualTo(1f).Within(.001f));
            Assert.That(weight.weight0+weight.weight1,Is.GreaterThan(.999f));
            Assert.That(weight.weight2,Is.EqualTo(0f));
            Assert.That(weight.weight3,Is.EqualTo(0f));
            Assert.That(weight.boneIndex0==1||weight.boneIndex1==1,Is.True);
            Assert.That(weight.boneIndex0==0||weight.boneIndex1==0,Is.True);
        }

        [Test]
        public void WorkspaceReusesExistingEyeAnchorsAndAddsRequiredTags()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var normalEye=new GameObject("NormalEye");normalEye.transform.SetParent(source.transform,false);
            var feralEye=new GameObject("FeralEye");feralEye.transform.SetParent(source.transform,false);
            RebirthEntityRigWorkspace workspace=null;
            var generatedMeshes=new List<Mesh>();
            try
            {
                profile.InitializeBipedDefaults();
                profile.generateLods=false;profile.generateTierMaterials=false;
                profile.createHitColliders=false;profile.createRagdoll=false;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);

                var workspaceNormal=RebirthEntityRigBuilder.FindBone(workspace.sourceVisual,"NormalEye");
                var workspaceFeral=RebirthEntityRigBuilder.FindBone(workspace.sourceVisual,"FeralEye");
                Assert.That(workspaceNormal,Is.Not.Null);
                Assert.That(workspaceFeral,Is.Not.Null);
                Assert.That(workspaceNormal,Is.Not.SameAs(normalEye.transform));
                Assert.That(workspaceFeral,Is.Not.SameAs(feralEye.transform));
                Assert.That(workspaceNormal.tag,Is.EqualTo("E_BP_Eye"));
                Assert.That(workspaceFeral.tag,Is.EqualTo("E_BP_Eye"));
                Assert.That(RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"NormalEye"),Is.Null);
                Assert.That(RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"FeralEye"),Is.Null);
                var findings=RebirthEntityRigValidator.Inspect(workspace);
                Assert.That(findings.Exists(f=>f.code=="EYE_ANCHOR_MISSING"||f.code=="EYE_ANCHOR_TAG"),Is.False);
                Assert.That(normalEye.tag,Is.EqualTo("Untagged"));
                Assert.That(feralEye.tag,Is.EqualTo("Untagged"));
                foreach(var renderer in profile.sourceSkinnedMeshes)generatedMeshes.Add(renderer.sharedMesh);
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);
                foreach(var generated in generatedMeshes)if(generated!=null)Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void ExistingProfilesMigrateBonesToWeightEligibleExactlyOnce()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                profile.bones=new List<EntityBoneSpec>
                {
                    new EntityBoneSpec{name="OldBoneA",deformsMesh=false},
                    new EntityBoneSpec{name="OldBoneB",deformsMesh=false}
                };
                Assert.That(profile.EnsureBoneDeformFlags(),Is.True);
                Assert.That(profile.bones.TrueForAll(b=>b.deformsMesh),Is.True);
                profile.bones[1].deformsMesh=false;
                Assert.That(profile.EnsureBoneDeformFlags(),Is.False);
                Assert.That(profile.bones[1].deformsMesh,Is.False);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void GameBridgeInspectionImportsRendererHierarchyTagsAndRestRotations()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                string json="{\"transforms\":["+
                    "{\"index\":0,\"parent\":-1,\"name\":\"Zombie\",\"path\":\"Zombie\",\"unityTag\":\"Untagged\",\"localPosition\":{\"x\":0,\"y\":0,\"z\":0},\"localRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"localScale\":{\"x\":1,\"y\":1,\"z\":1}},"+
                    "{\"index\":1,\"parent\":0,\"name\":\"Origin\",\"path\":\"Zombie/Origin\",\"unityTag\":\"E_BP_BipedRoot\",\"localPosition\":{\"x\":0,\"y\":0,\"z\":0},\"localRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"localScale\":{\"x\":1,\"y\":1,\"z\":1}},"+
                    "{\"index\":2,\"parent\":1,\"name\":\"Hips\",\"path\":\"Zombie/Origin/Hips\",\"unityTag\":\"E_BP_Body\",\"localPosition\":{\"x\":0,\"y\":0.5,\"z\":0},\"localRotation\":{\"x\":0,\"y\":0.0871557,\"z\":0,\"w\":0.9961947},\"localScale\":{\"x\":1,\"y\":1,\"z\":1}},"+
                    "{\"index\":3,\"parent\":2,\"name\":\"Head\",\"path\":\"Zombie/Origin/Hips/Head\",\"unityTag\":\"E_BP_Head\",\"localPosition\":{\"x\":0,\"y\":0.5,\"z\":0},\"localRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"localScale\":{\"x\":1,\"y\":1,\"z\":1}},"+
                    "{\"index\":4,\"parent\":3,\"name\":\"L_HeadGore\",\"path\":\"Zombie/Origin/Hips/Head/L_HeadGore\",\"unityTag\":\"L_HeadGore\",\"localPosition\":{\"x\":0.1,\"y\":0,\"z\":0},\"localRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"localScale\":{\"x\":1,\"y\":1,\"z\":1}},"+
                    "{\"index\":5,\"parent\":4,\"name\":\"GoreHelper\",\"path\":\"Zombie/Origin/Hips/Head/L_HeadGore/GoreHelper\",\"unityTag\":\"Untagged\",\"localPosition\":{\"x\":0,\"y\":0,\"z\":0},\"localRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"localScale\":{\"x\":1,\"y\":1,\"z\":1}}],"+
                    "\"modelComponents\":[{\"kind\":\"SkinnedMeshRenderer\",\"bones\":[{\"index\":0,\"path\":\"Zombie/Origin/Hips\"},{\"index\":1,\"path\":\"Zombie/Origin/Hips/Head\"}]}]}";
                int imported=RebirthEntityBridgeSkeletonImporter.ImportJsonIntoProfile(json,profile);
                Assert.That(imported,Is.EqualTo(5));
                Assert.That(profile.bones[1].name,Is.EqualTo("Hips"));
                Assert.That(profile.bones[1].gameTag,Is.EqualTo("E_BP_Body"));
                Assert.That(profile.bones[2].parent,Is.EqualTo("Hips"));
                Assert.That(profile.bones[2].normalizedPosition.y,Is.EqualTo(1f).Within(.001f));
                Assert.That(profile.bones[1].referenceLocalEuler.y,Is.EqualTo(10f).Within(.1f));
                EntityBoneSpec gore=profile.bones.Find(b=>b.name=="L_HeadGore");
                Assert.That(gore,Is.Not.Null);Assert.That(gore.deformsMesh,Is.False);Assert.That(gore.gameTag,Is.EqualTo("L_HeadGore"));
                EntityBoneSpec helper=profile.bones.Find(b=>b.name=="GoreHelper");
                Assert.That(helper,Is.Not.Null);Assert.That(helper.parent,Is.EqualTo("L_HeadGore"));Assert.That(helper.deformsMesh,Is.False);
                Assert.That(profile.reviewedSkeletonMapping,Is.False);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void LiveUtilityWorkerCaptureImportsItsActual58BoneContract()
        {
            const string fixturePath="Assets/REBIRTHEntityStudio/Tests/Fixtures/UtilityWorker_3_3_0_EntityModel.json";
            TextAsset fixture=AssetDatabase.LoadAssetAtPath<TextAsset>(fixturePath);
            Assert.That(fixture,Is.Not.Null,"Live Game Bridge fixture must be imported as a Unity TextAsset.");
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            RebirthEntityRigWorkspace workspace=null;
            var generatedMeshes=new List<Mesh>();
            try
            {
                int importedCount=RebirthEntityBridgeSkeletonImporter.ImportJsonIntoProfile(fixture.text,profile);

                Assert.That(importedCount,Is.EqualTo(72));
                Assert.That(profile.bones.FindAll(b=>b.deformsMesh).Count,Is.EqualTo(58));
                Assert.That(profile.bones.Exists(b=>b.name=="Origin"&&b.gameTag=="E_BP_BipedRoot"),Is.True);
                Assert.That(profile.bones.Exists(b=>b.name=="Hips"&&b.gameTag=="E_BP_Body"),Is.True);
                Assert.That(profile.bones.Exists(b=>b.name=="Head"&&b.gameTag=="E_BP_Head"),Is.True);
                Assert.That(profile.bones.Exists(b=>b.name=="AudioSource_Footsteps(Clone)"&&b.gameTag=="AudioRigidBody"&&!b.deformsMesh),Is.True);
                foreach(var bone in profile.bones)
                    Assert.That(string.IsNullOrEmpty(bone.parent)||profile.bones.Exists(parent=>parent.name==bone.parent),Is.True,"Missing imported parent for "+bone.name);
                Assert.That(profile.reviewedSkeletonMapping,Is.False);
                Assert.That(profile.reviewedColliderFit,Is.False);
                Assert.That(profile.reviewedLods,Is.False);
                Assert.That(profile.reviewedMaterials,Is.False);
                profile.generateLods=false;profile.generateTierMaterials=false;
                profile.createHitColliders=false;profile.createRagdoll=false;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                Assert.That(RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"AudioSource_Footsteps(Clone)").tag,Is.EqualTo("AudioRigidBody"));
                var renderer=profile.sourceSkinnedMeshes[0];
                Assert.That(renderer.bones.Length,Is.EqualTo(58));
                Assert.That(renderer.rootBone,Is.EqualTo(workspace.generatedSkeleton));
                Assert.That(renderer.sharedMesh.bindposes.Length,Is.EqualTo(renderer.bones.Length));
                Assert.That(renderer.sharedMesh.boneWeights.Length,Is.EqualTo(renderer.sharedMesh.vertexCount));
                foreach(var weight in renderer.sharedMesh.boneWeights)
                {
                    Assert.That(weight.weight0+weight.weight1+weight.weight2+weight.weight3,Is.EqualTo(1f).Within(.001f));
                    Assert.That(weight.boneIndex0,Is.GreaterThanOrEqualTo(0));
                    Assert.That(weight.boneIndex0,Is.LessThan(renderer.bones.Length));
                    if(weight.weight1>0f)Assert.That(weight.boneIndex1,Is.InRange(0,renderer.bones.Length-1));
                    if(weight.weight2>0f)Assert.That(weight.boneIndex2,Is.InRange(0,renderer.bones.Length-1));
                    if(weight.weight3>0f)Assert.That(weight.boneIndex3,Is.InRange(0,renderer.bones.Length-1));
                }
                foreach(var generatedRenderer in profile.sourceSkinnedMeshes)generatedMeshes.Add(generatedRenderer.sharedMesh);
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);Object.DestroyImmediate(profile);
                foreach(var generated in generatedMeshes)if(generated!=null)Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void WorkspaceBindPosesUseRendererTransformForOffsetSource()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=new GameObject("OffsetSource");
            var mesh=new Mesh { name="SyntheticBody" };
            RebirthEntityRigWorkspace workspace=null;
            Mesh generatedMesh=null;
            try
            {
                profile.InitializeBipedDefaults();
                profile.bones.Find(b=>b.name=="Head").referenceLocalEuler=new Vector3(0f,20f,0f);
                profile.generateLods=false;profile.generateTierMaterials=false;
                profile.reviewedSkeletonMapping=true;profile.reviewedColliderFit=true;profile.reviewedLods=true;profile.reviewedMaterials=true;
                source.transform.position=new Vector3(3.2f,1.1f,-2.4f);
                source.transform.rotation=Quaternion.Euler(0f,37f,0f);
                source.transform.localScale=new Vector3(1.2f,0.9f,1.1f);
                mesh.vertices=new[]{new Vector3(-.3f,0f,-.2f),new Vector3(.3f,0f,-.2f),new Vector3(0f,1.8f,.2f)};
                mesh.triangles=new[]{0,2,1};
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                // Common imported object names may overlap skeleton bone names. Source meshes
                // must never shadow generated rig bones during bone and tag lookup.
                var geometry=new GameObject("Head");geometry.transform.SetParent(source.transform,false);
                geometry.AddComponent<MeshFilter>().sharedMesh=mesh;
                geometry.AddComponent<MeshRenderer>();

                Bounds localBounds=RebirthEntityRigBuilder.SourceBoundsInRootSpace(source.transform);
                Assert.That(localBounds.center.x,Is.EqualTo(0f).Within(.001f));
                Assert.That(localBounds.center.y,Is.EqualTo(.9f).Within(.001f));
                Assert.That(localBounds.center.z,Is.EqualTo(0f).Within(.001f));
                Vector3 normalized=RebirthEntityRigBuilder.NormalizeBonePositionInBounds(localBounds,new Vector3(.1f,.7f,.08f));
                Vector3 restored=RebirthEntityRigBuilder.BonePositionInBounds(localBounds,normalized);
                Assert.That(Vector3.Distance(restored,new Vector3(.1f,.7f,.08f)),Is.LessThan(.0001f));

                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                Assert.That(Vector3.Distance(workspace.transform.position,source.transform.position),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(workspace.transform.rotation,Quaternion.identity),Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(workspace.sourceVisual.rotation,source.transform.rotation),Is.LessThan(.001f));
                Assert.That(workspace.transform.localScale,Is.EqualTo(Vector3.one));
                Assert.That(workspace.sourceVisual.localScale,Is.EqualTo(source.transform.lossyScale));
                Assert.That(RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"NormalEye").tag,Is.EqualTo("E_BP_Eye"));
                Assert.That(RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"FeralEye").tag,Is.EqualTo("E_BP_Eye"));
                Assert.That(profile.reviewedSkeletonMapping,Is.EqualTo(false));
                Assert.That(profile.reviewedColliderFit,Is.EqualTo(false));
                Assert.That(profile.reviewedLods,Is.EqualTo(false));
                Assert.That(profile.reviewedMaterials,Is.EqualTo(false));
                Assert.That(workspace.gameObject.tag,Is.EqualTo("E_Enemy"));
                Assert.That(workspace.sourceModelIsSceneInstance,Is.True);
                Assert.That(workspace.sourceModelObject,Is.SameAs(source));
                Vector3 originalSourceScale=source.transform.localScale;
                source.transform.localScale=originalSourceScale+new Vector3(.01f,0f,0f);
                Assert.That(RebirthEntityRigValidator.HasStaleInputs(workspace),Is.True,"Scene-instance source edits must invalidate the workspace.");
                source.transform.localScale=originalSourceScale;
                Assert.That(RebirthEntityRigValidator.HasStaleInputs(workspace),Is.False,"Restoring the source input should clear the stale-input finding.");
                Assert.That(workspace.generatedSkeleton.tag,Is.EqualTo("E_BP_BipedRoot"));
                var generatedHead=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"Head");
                Assert.That(generatedHead,Is.Not.Null);
                Assert.That(generatedHead.tag,Is.EqualTo("E_BP_Head"));
                Assert.That(Quaternion.Angle(generatedHead.localRotation,Quaternion.Euler(0f,20f,0f)),Is.LessThan(.01f));
                Vector3 expectedHead=workspace.sourceVisual.TransformPoint(RebirthEntityRigBuilder.BonePositionInBounds(localBounds,profile.bones.Find(b=>b.name=="Head").normalizedPosition));
                Assert.That(Vector3.Distance(generatedHead.position,expectedHead),Is.LessThan(.0001f));
                var renderer=workspace.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderer,Is.Not.Null);
                Assert.That(System.Array.IndexOf(renderer.bones,generatedHead),Is.GreaterThanOrEqualTo(0));
                Assert.That(renderer.sharedMesh.boneWeights.Length,Is.EqualTo(renderer.sharedMesh.vertexCount));
                Assert.That(renderer.sharedMesh.bindposes.Length,Is.EqualTo(renderer.bones.Length));
                foreach(var weight in renderer.sharedMesh.boneWeights)
                {
                    Assert.That(weight.weight0+weight.weight1+weight.weight2+weight.weight3,Is.EqualTo(1f).Within(0.001f));
                    Assert.That(weight.boneIndex0,Is.GreaterThanOrEqualTo(0));Assert.That(weight.boneIndex0,Is.LessThan(renderer.bones.Length));
                    if(weight.weight1>0f)Assert.That(weight.boneIndex1,Is.GreaterThanOrEqualTo(0));Assert.That(weight.boneIndex1,Is.LessThan(renderer.bones.Length));
                    if(weight.weight2>0f)Assert.That(weight.boneIndex2,Is.GreaterThanOrEqualTo(0));Assert.That(weight.boneIndex2,Is.LessThan(renderer.bones.Length));
                    if(weight.weight3>0f)Assert.That(weight.boneIndex3,Is.GreaterThanOrEqualTo(0));Assert.That(weight.boneIndex3,Is.LessThan(renderer.bones.Length));
                }
                var expected=new List<Matrix4x4>();
                foreach(var bone in renderer.bones)expected.Add(bone.worldToLocalMatrix*renderer.transform.localToWorldMatrix);
                for(int i=0;i<expected.Count;i++)
                for(int row=0;row<4;row++)
                for(int column=0;column<4;column++)
                    Assert.That(renderer.sharedMesh.bindposes[i][row,column],Is.EqualTo(expected[i][row,column]).Within(0.0001f));
                profile.bones.Find(b=>b.name=="Head").normalizedPosition.y+=0.01f;
                Assert.That(RebirthEntityRigValidator.HasStaleInputs(workspace),Is.EqualTo(true));
                Assert.That(RebirthEntityRigValidator.Inspect(workspace).Exists(f=>f.code=="STALE_PROFILE"),Is.EqualTo(true));
                generatedMesh=renderer.sharedMesh;
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(profile);
                if(generatedMesh!=null)Object.DestroyImmediate(generatedMesh);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void ZUpSourceIsConvertedToGameYUpWithoutChangingVisibleMeshPose()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=new GameObject("ZUpSource");
            var mesh=new Mesh{name="ZUpSyntheticBody"};
            RebirthEntityRigWorkspace workspace=null;
            try
            {
                profile.InitializeBipedDefaults();profile.sourceUpAxis=EntityModelUpAxis.Z;
                profile.generateLods=false;profile.generateTierMaterials=false;
                mesh.vertices=new[]{new Vector3(-.3f,-.2f,0f),new Vector3(.3f,-.2f,0f),new Vector3(0f,.2f,1.8f)};
                mesh.triangles=new[]{0,1,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
                var geometry=new GameObject("Geometry");geometry.transform.SetParent(source.transform,false);
                geometry.transform.localRotation=RebirthEntityRigBuilder.SourceToGameRotation(EntityModelUpAxis.Z);
                geometry.AddComponent<MeshFilter>().sharedMesh=mesh;geometry.AddComponent<MeshRenderer>();
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                Assert.That(Quaternion.Angle(workspace.transform.rotation,Quaternion.identity),Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(workspace.sourceVisual.rotation,source.transform.rotation),Is.LessThan(.001f),"Visible source pose should preserve the original model orientation.");
                Assert.That(profile.overallBoundsSize.y,Is.EqualTo(1.8f).Within(.001f));
                Assert.That(profile.overallBoundsSize.z,Is.EqualTo(.4f).Within(.001f));
                var generatedHead=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"Head");
                Assert.That(generatedHead.position.y,Is.GreaterThan(generatedHead.position.z));
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);Object.DestroyImmediate(profile);Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void WorkspaceBuildsMultipleMeshChildrenWithoutLosingTheirTransforms()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=new GameObject("MultiMeshSource");
            var firstMesh=new Mesh{name="BodyPartA"};
            var secondMesh=new Mesh{name="BodyPartB"};
            RebirthEntityRigWorkspace workspace=null;
            var generatedMeshes=new List<Mesh>();
            try
            {
                profile.InitializeBipedDefaults();
                profile.generateLods=false;profile.generateTierMaterials=false;
                source.transform.position=new Vector3(-2.1f,0.7f,3.4f);
                source.transform.rotation=Quaternion.Euler(0f,-23f,0f);
                source.transform.localScale=new Vector3(1.15f,0.9f,1.05f);
                firstMesh.vertices=new[]{new Vector3(-.25f,0f,-.15f),new Vector3(.25f,0f,-.15f),new Vector3(0f,.9f,.15f)};
                firstMesh.triangles=new[]{0,2,1};firstMesh.RecalculateBounds();
                secondMesh.vertices=new[]{new Vector3(-.2f,0f,-.1f),new Vector3(.2f,0f,-.1f),new Vector3(0f,.7f,.1f)};
                secondMesh.triangles=new[]{0,2,1};secondMesh.RecalculateBounds();
                AddStaticMesh(source.transform,"BodyPartA",firstMesh,new Vector3(-.25f,.45f,.1f),Quaternion.Euler(0f,17f,0f));
                AddStaticMesh(source.transform,"BodyPartB",secondMesh,new Vector3(.31f,.88f,-.12f),Quaternion.Euler(0f,-31f,0f));

                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                Assert.That(profile.sourceSkinnedMeshes.Count,Is.EqualTo(2));
                foreach(var renderer in profile.sourceSkinnedMeshes)
                {
                    Assert.That(renderer,Is.Not.Null);
                    Assert.That(renderer.sharedMesh.boneWeights.Length,Is.EqualTo(renderer.sharedMesh.vertexCount));
                    Assert.That(renderer.sharedMesh.bindposes.Length,Is.EqualTo(renderer.bones.Length));
                    foreach(var weight in renderer.sharedMesh.boneWeights)
                    {
                        Assert.That(weight.weight0+weight.weight1+weight.weight2+weight.weight3,Is.EqualTo(1f).Within(0.001f));
                        Assert.That(weight.boneIndex0,Is.GreaterThanOrEqualTo(0));Assert.That(weight.boneIndex0,Is.LessThan(renderer.bones.Length));
                        if(weight.weight1>0f)Assert.That(weight.boneIndex1,Is.GreaterThanOrEqualTo(0));Assert.That(weight.boneIndex1,Is.LessThan(renderer.bones.Length));
                        if(weight.weight2>0f)Assert.That(weight.boneIndex2,Is.GreaterThanOrEqualTo(0));Assert.That(weight.boneIndex2,Is.LessThan(renderer.bones.Length));
                        if(weight.weight3>0f)Assert.That(weight.boneIndex3,Is.GreaterThanOrEqualTo(0));Assert.That(weight.boneIndex3,Is.LessThan(renderer.bones.Length));
                    }
                    generatedMeshes.Add(renderer.sharedMesh);
                }
                var partA=workspace.sourceVisual.Find("BodyPartA").GetComponentInChildren<SkinnedMeshRenderer>(true);
                var partB=workspace.sourceVisual.Find("BodyPartB").GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(partA.transform.position,Is.EqualTo(workspace.sourceVisual.Find("BodyPartA").position));
                Assert.That(partB.transform.position,Is.EqualTo(workspace.sourceVisual.Find("BodyPartB").position));
                Assert.That(partA.transform.rotation,Is.EqualTo(workspace.sourceVisual.Find("BodyPartA").rotation));
                Assert.That(partB.transform.rotation,Is.EqualTo(workspace.sourceVisual.Find("BodyPartB").rotation));
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);Object.DestroyImmediate(profile);
                foreach(var generated in generatedMeshes)if(generated!=null)Object.DestroyImmediate(generated);
                Object.DestroyImmediate(firstMesh);Object.DestroyImmediate(secondMesh);
            }
        }

        [Test]
        public void ImportedObjAssetBuildsAWorkspaceWithoutChangingTheSourceAsset()
        {
            const string folder="Assets/EntityStudioGenerated/AssetIntake";
            string objPath=null,albedoPath=null;
            RebirthEntityRigWorkspace workspace=null;
            var generatedMeshes=new List<Mesh>();
            var materialPaths=new List<string>();
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                EnsureAssetFolder(folder);
                objPath=folder+"/UnriggedCharacter.obj";
                File.WriteAllText(objPath,"o UnriggedCharacter\n"+
                    "v -0.35 0 -0.2\n"+
                    "v 0.35 0 -0.2\n"+
                    "v 0.0 1.8 0.2\n"+
                    "vt 0 0\nvt 1 0\nvt 0.5 1\n"+
                    "vn 0 -0.21693 0.976187\n"+
                    "f 1/1/1 2/2/1 3/3/1\n");
                AssetDatabase.ImportAsset(objPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                albedoPath=CreateTextureAsset(folder,"ArbitraryName_ColorTexture");
                var sourceAsset=AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
                Assert.That(sourceAsset,Is.Not.Null);
                var sourceRenderer=sourceAsset.GetComponentInChildren<MeshRenderer>(true);
                var sourceFilter=sourceAsset.GetComponentInChildren<MeshFilter>(true);
                Assert.That(sourceRenderer,Is.Not.Null);
                Assert.That(sourceFilter,Is.Not.Null);
                Mesh originalMesh=sourceFilter.sharedMesh;
                int originalVertexCount=originalMesh.vertexCount;
                int originalSubmeshCount=originalMesh.subMeshCount;

                profile.InitializeBipedDefaults();
                profile.entityClass="EntityObjIntakeTest_"+System.Guid.NewGuid().ToString("N");
                profile.characterShader=Shader.Find("Standard");
                profile.bakedAlbedo=AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                profile.generateLods=false;profile.generateTierMaterials=true;
                profile.createHitColliders=false;profile.createRagdoll=false;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(sourceAsset,profile);
                foreach(var renderer in profile.sourceSkinnedMeshes)generatedMeshes.Add(renderer.sharedMesh);
                foreach(var material in profile.generatedTierMaterials)
                {string path=AssetDatabase.GetAssetPath(material);if(!string.IsNullOrEmpty(path))materialPaths.Add(path);}

                Assert.That(workspace.sourceModelAssetPath,Is.EqualTo(objPath));
                Assert.That(RebirthEntityRigValidator.HasStaleInputs(workspace),Is.False);
                Assert.That(profile.sourceSkinnedMeshes.Count,Is.EqualTo(1));
                Assert.That(profile.sourceSkinnedMeshes[0].sharedMesh.vertexCount,Is.EqualTo(originalVertexCount));
                Assert.That(profile.sourceSkinnedMeshes[0].sharedMesh.subMeshCount,Is.EqualTo(originalSubmeshCount));
                Assert.That(profile.generatedTierMaterials.Count,Is.EqualTo(profile.generatedMaterialSlotCount*5));
                foreach(var material in profile.generatedTierMaterials)Assert.That(material.mainTexture,Is.EqualTo(profile.bakedAlbedo));
                foreach(var weight in profile.sourceSkinnedMeshes[0].sharedMesh.boneWeights)
                    Assert.That(weight.weight0+weight.weight1+weight.weight2+weight.weight3,Is.EqualTo(1f).Within(0.001f));

                sourceAsset=AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
                Assert.That(sourceAsset.GetComponentInChildren<SkinnedMeshRenderer>(true),Is.Null);
                Assert.That(sourceAsset.GetComponentInChildren<MeshRenderer>(true),Is.Not.Null);
                Assert.That(sourceAsset.GetComponentInChildren<MeshFilter>(true).sharedMesh,Is.EqualTo(originalMesh));
            }
            finally
            {
                string generatedMaterialFolder="Assets/EntityMaterials/"+profile.entityClass;
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                foreach(var mesh in generatedMeshes)if(mesh!=null)Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(profile);
                foreach(string path in materialPaths)AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(generatedMaterialFolder);
                if(!string.IsNullOrEmpty(albedoPath))AssetDatabase.DeleteAsset(albedoPath);
                if(!string.IsNullOrEmpty(objPath))AssetDatabase.DeleteAsset(objPath);
                if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void ColliderFitReportsAndRagdollChainAreGenerated()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            RebirthEntityRigWorkspace workspace=null;
            var generatedMeshes=new List<Mesh>();
            try
            {
                profile.InitializeBipedDefaults();
                profile.generateLods=false;profile.generateTierMaterials=false;
                profile.createHitColliders=true;profile.createRagdoll=true;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                var colliders=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
                var bodies=workspace.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true);
                var joints=workspace.generatedSkeleton.GetComponentsInChildren<CharacterJoint>(true);
                Assert.That(colliders.Length,Is.GreaterThanOrEqualTo(8));
                Assert.That(workspace.colliderFitReports.Count,Is.EqualTo(colliders.Length));
                Assert.That(bodies.Length,Is.EqualTo(colliders.Length));
                Assert.That(joints.Length,Is.EqualTo(bodies.Length-1));
                Assert.That(workspace.colliderFitReports.Exists(report=>report.sampledVertices>0),Is.True);
                foreach(var collider in colliders)
                {Assert.That(collider.radius,Is.GreaterThan(0f));Assert.That(collider.height,Is.GreaterThanOrEqualTo(2f*collider.radius));}
                foreach(var joint in joints)Assert.That(joint.connectedBody,Is.Not.Null);
                foreach(var renderer in profile.sourceSkinnedMeshes)generatedMeshes.Add(renderer.sharedMesh);
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);Object.DestroyImmediate(profile);
                foreach(var generated in generatedMeshes)if(generated!=null)Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void ColliderCoverageUsesRoundedCapsuleEnds()
        {
            Assert.That(RebirthEntityRigBuilder.IsInsideCapsule(0.9f,0.3f,0.5f,2f),Is.EqualTo(true));
            Assert.That(RebirthEntityRigBuilder.IsInsideCapsule(0.9f,0.31f,0.5f,2f),Is.EqualTo(false));
            Assert.That(RebirthEntityRigBuilder.IsInsideCapsule(1f,0f,0.5f,2f),Is.EqualTo(true));
            Assert.That(RebirthEntityRigBuilder.IsInsideCapsule(1f,0.01f,0.5f,2f),Is.EqualTo(false));
        }

        [Test]
        public void ColliderFitKeepsEachAxialAndRadialMeshSamplePaired()
        {
            var axial=new List<float>();
            var radial=new List<float>();
            for(int i=0;i<50;i++){axial.Add(-1.5f);radial.Add(0f);}
            for(int i=0;i<50;i++){axial.Add(-.5f);radial.Add(.49f);}
            for(int i=0;i<50;i++){axial.Add(.5f);radial.Add(.49f);}
            for(int i=0;i<50;i++){axial.Add(1.5f);radial.Add(0f);}

            var fit=RebirthEntityRigBuilder.FitCapsuleSamples(axial,radial,0f);

            Assert.That(fit.sampleCount,Is.EqualTo(200));
            Assert.That(fit.minAxial,Is.EqualTo(-1.5f).Within(.001f));
            Assert.That(fit.maxAxial,Is.EqualTo(1.5f).Within(.001f));
            Assert.That(fit.radius,Is.EqualTo(.49f).Within(.001f));
            Assert.That(fit.height,Is.EqualTo(3f).Within(.001f));
            Assert.That(fit.coverage,Is.EqualTo(1f).Within(.001f));
            Assert.That(radial[0],Is.EqualTo(0f));
            Assert.That(radial[50],Is.EqualTo(.49f));
            Assert.That(radial[150],Is.EqualTo(0f));
        }

        [Test]
        public void AsymmetricCapsuleFitSolvesCenterWithoutDroppingSamples()
        {
            var axial=new List<float>{0,0,0,0,3,3,3,3};
            var radial=new List<float>{1,1,1,1,0,0,0,0};
            var fit=RebirthEntityRigBuilder.FitCapsuleSamples(axial,radial,0f,true);
            Assert.That(fit.centerAxial,Is.EqualTo(1f).Within(.0001f));
            Assert.That(fit.height,Is.EqualTo(4f).Within(.0001f));
            Assert.That(fit.radius,Is.EqualTo(1f).Within(.0001f));
            for(int i=0;i<axial.Count;i++)Assert.That(RebirthEntityRigBuilder.IsInsideCapsule(axial[i]-fit.centerAxial,radial[i],fit.radius,fit.height),Is.True);
        }

        [Test]
        public void ColliderSampleOwnershipKeepsJointBlendsBelowSeventyPercent()
        {
            var groups=new List<List<int>>{new List<int>{0,1},new List<int>{2,3}};
            var weight=new BoneWeight{boneIndex0=0,weight0=.3f,boneIndex1=1,weight1=.2f,boneIndex2=2,weight2=.3f,boneIndex3=3,weight3=.2f};
            Assert.That(RebirthEntityRigBuilder.DominantSampleGroup(weight,groups),Is.EqualTo(0));
            weight.weight0=.2f;weight.weight2=.4f;
            Assert.That(RebirthEntityRigBuilder.DominantSampleGroup(weight,groups),Is.EqualTo(1));
        }

        [Test]
        public void AdjacentFingerBranchesBlendContinuouslyWhenNearestFingerChanges()
        {
            var method=typeof(RebirthEntityRigBuilder).GetMethod("WeightsFromDistances",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var parents=new[]{-1,0,0};var eligible=new[]{true,true,true};var hand=new[]{true,true,true};
            BoneWeight left=(BoneWeight)method.Invoke(null,new object[]{new[]{100f,.01f,.010001f},parents,4f,eligible,hand});
            BoneWeight right=(BoneWeight)method.Invoke(null,new object[]{new[]{100f,.010001f,.01f},parents,4f,eligible,hand});
            Assert.That(Amount(left,1),Is.GreaterThan(.49f));Assert.That(Amount(left,2),Is.GreaterThan(.49f));
            Assert.That(System.Math.Abs(Amount(left,1)-Amount(right,1)),Is.LessThan(.001f));
            float Amount(BoneWeight weight,int index){return (weight.boneIndex0==index?weight.weight0:0)+(weight.boneIndex1==index?weight.weight1:0)+(weight.boneIndex2==index?weight.weight2:0)+(weight.boneIndex3==index?weight.weight3:0);}
        }

        [Test]
        public void FourthArmInfluenceVanishesBeforeFifthReplacesIt()
        {
            var method=typeof(RebirthEntityRigBuilder).GetMethod("WeightsFromDistances",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var parents=new[]{-1,0,0,0,0};var flags=new[]{true,true,true,true,true};
            BoneWeight before=(BoneWeight)method.Invoke(null,new object[]{new[]{.1f,.2f,.3f,.4f,.4001f},parents,4f,flags,flags});
            BoneWeight after=(BoneWeight)method.Invoke(null,new object[]{new[]{.1f,.2f,.3f,.4001f,.4f},parents,4f,flags,flags});
            Assert.That(Amount(before,3),Is.LessThan(.0001f));Assert.That(Amount(after,4),Is.LessThan(.0001f));
            Assert.That(System.Math.Abs(Amount(before,0)-Amount(after,0)),Is.LessThan(.0001f));
            float Amount(BoneWeight weight,int index){return (weight.boneIndex0==index?weight.weight0:0)+(weight.boneIndex1==index?weight.weight1:0)+(weight.boneIndex2==index?weight.weight2:0)+(weight.boneIndex3==index?weight.weight3:0);}
        }

        [Test]
        public void RepairUvChartsNeverOverwriteSourceOrOtherPaddedCharts()
        {
            const int size=32;var source=new bool[size*size];for(int y=0;y<size;y++)for(int x=0;x<12;x++)source[y*size+x]=true;
            var allocator=new RebirthEntityUvChartAllocator(source,size);
            Assert.That(allocator.TryAllocate(6,6,2,out RectInt first),Is.True);
            Assert.That(allocator.TryAllocate(6,6,2,out RectInt second),Is.True);
            RectInt Padded(RectInt rect)=>new RectInt(rect.x-2,rect.y-2,rect.width+4,rect.height+4);
            Assert.That(Padded(first).Overlaps(Padded(second)),Is.False);
            foreach(var rect in new[]{Padded(first),Padded(second)})for(int y=rect.yMin;y<rect.yMax;y++)for(int x=rect.xMin;x<rect.xMax;x++)Assert.That(source[y*size+x],Is.False);
            Assert.That(source[0],Is.True);Assert.That(source[31],Is.False);
        }

        [Test]
        public void RepairUvAllocationFailsWithoutChangingAFullAtlas()
        {
            var mask=new bool[64];for(int i=0;i<mask.Length;i++)mask[i]=true;
            var allocator=new RebirthEntityUvChartAllocator(mask,8);
            Assert.That(allocator.TryAllocate(2,2,1,out _),Is.False);
            Assert.That(allocator.Snapshot(),Is.EqualTo(mask));
        }

        [Test]
        public void RepairUvOccupancyProtectsThinTriangleEdges()
        {
            var mesh=new Mesh();
            try
            {
                mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.uv=new[]{new Vector2(.1f,.1f),new Vector2(.9f,.101f),new Vector2(.1f,.102f)};mesh.triangles=new[]{0,1,2};
                bool[] mask=RebirthEntityUvChartAllocator.BuildOccupancy(new[]{mesh},32,1);
                Assert.That(mask[3*32+16],Is.True);Assert.That(mask[16*32+16],Is.False);Assert.That(mask[0],Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }

        [Test]
        public void PhysicsCanBeDisabledWithoutFailingValidation()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            RebirthEntityRigWorkspace workspace=null;
            var generatedMeshes=new List<Mesh>();
            try
            {
                profile.InitializeBipedDefaults();
                profile.generateLods=false;profile.generateTierMaterials=false;
                profile.createHitColliders=false;profile.createRagdoll=false;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                Assert.That(workspace.colliderRoot,Is.EqualTo(null));
                Assert.That(RebirthEntityRigValidator.Inspect(workspace).Exists(f=>f.code=="NO_PHYSICS"),Is.EqualTo(false));
                Assert.That(source.GetComponent<Collider>(),Is.Not.Null);
                Assert.That(workspace.sourceVisual.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(0));
                foreach(var renderer in profile.sourceSkinnedMeshes)generatedMeshes.Add(renderer.sharedMesh);
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);Object.DestroyImmediate(profile);
                foreach(var generated in generatedMeshes)if(generated!=null)Object.DestroyImmediate(generated);
            }
        }

        [Test]
        public void ExistingLodGroupRequiresExplicitLodZeroMapping()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            profile.InitializeBipedDefaults();
            source.AddComponent<LODGroup>();
            bool rejected=false;
            try{RebirthEntityRigBuilder.CreateWorkspace(source,profile);}
            catch(System.InvalidOperationException){rejected=true;}
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(profile);}
            Assert.That(rejected,Is.EqualTo(true));
        }

        [Test]
        public void GeneratedSkinnedLodsReduceTriangleCounts()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var denseMesh=Subdivide(source.GetComponent<MeshFilter>().sharedMesh,2);
            source.GetComponent<MeshFilter>().sharedMesh=denseMesh;
            RebirthEntityRigWorkspace workspace=null;
            string generatedFolder=null;
            try
            {
                profile.InitializeBipedDefaults();
                profile.createHitColliders=false;profile.createRagdoll=false;profile.generateTierMaterials=false;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                Assert.That(workspace.lodGroup,Is.Not.Null);
                var levels=workspace.lodGroup.GetLODs();
                Assert.That(levels.Length,Is.EqualTo(4));
                Assert.That(levels[0].renderers.Length,Is.EqualTo(profile.sourceSkinnedMeshes.Count),"LOD0 must use the original generated skinned renderers.");
                for(int rendererIndex=0;rendererIndex<profile.sourceSkinnedMeshes.Count;rendererIndex++)
                {
                    var sourceRenderer=profile.sourceSkinnedMeshes[rendererIndex];
                    Assert.That(levels[0].renderers,Does.Contain(sourceRenderer));
                    Assert.That(sourceRenderer.enabled,Is.True,"LOD0 source renderers must remain enabled for LODGroup selection.");
                }
                int previous=TriangleCount(levels[0]);
                Assert.That(previous,Is.GreaterThan(0));
                for(int i=1;i<levels.Length;i++)
                {
                    int current=TriangleCount(levels[i]);
                    Assert.That(current,Is.GreaterThan(0));
                    Assert.That(current,Is.LessThan(previous),"LOD"+i+" must reduce the previous level's triangle count.");
                    previous=current;
                }
                generatedFolder="Assets/EntityStudioGenerated/LODs/"+profile.entityClass+"_"+workspace.generatedAssetId;
                foreach(var lodRenderer in workspace.lodGroup.GetLODs().Skip(1).SelectMany(level=>level.renderers).OfType<SkinnedMeshRenderer>())
                {
                    string savedLodPath=AssetDatabase.GetAssetPath(lodRenderer.sharedMesh);
                    Assert.That(savedLodPath,Does.StartWith(generatedFolder+"/"),"Generated LOD meshes must be persisted as assets.");
                    Assert.That(savedLodPath,Does.EndWith(".asset"));
                    int sourceIndex=int.Parse(lodRenderer.name.Substring(0,3));
                    int levelIndex=System.Array.FindIndex(levels,level=>level.renderers.Contains(lodRenderer));
                    var sourceRenderer=profile.sourceSkinnedMeshes[sourceIndex];
                    Assert.That(levelIndex,Is.GreaterThan(0),"Simplified renderers must only appear below LOD0.");
                    Assert.That(lodRenderer.sharedMesh.subMeshCount,Is.EqualTo(sourceRenderer.sharedMesh.subMeshCount));
                    Assert.That(lodRenderer.rootBone,Is.EqualTo(sourceRenderer.rootBone));
                    Assert.That(lodRenderer.bones,Is.EqualTo(sourceRenderer.bones));
                    Assert.That(lodRenderer.sharedMaterials,Is.EqualTo(sourceRenderer.sharedMaterials));
                    Assert.That(lodRenderer.sharedMesh.bindposes,Is.EqualTo(sourceRenderer.sharedMesh.bindposes));
                    BoneWeight[] generatedWeights=lodRenderer.sharedMesh.boneWeights;
                    Assert.That(generatedWeights.Length,Is.EqualTo(lodRenderer.sharedMesh.vertexCount));
                    foreach(BoneWeight weight in generatedWeights)
                    {
                        float sum=weight.weight0+weight.weight1+weight.weight2+weight.weight3;
                        Assert.That(sum,Is.EqualTo(1f).Within(.001f),"Simplified LOD skin weights must remain normalized.");
                        Assert.That(weight.boneIndex0,Is.InRange(0,lodRenderer.bones.Length-1));
                    }
                }
                string firstLodId=workspace.generatedAssetId;
                RebirthEntityRigBuilder.RebuildLods(workspace);
                Assert.That(workspace.generatedAssetId,Is.Not.EqualTo(firstLodId));
                Assert.That(AssetDatabase.IsValidFolder(generatedFolder),Is.False,"Regeneration must remove the prior generated asset folder.");
                generatedFolder="Assets/EntityStudioGenerated/LODs/"+profile.entityClass+"_"+workspace.generatedAssetId;
                foreach(var lodRenderer in workspace.lodGroup.GetLODs().Skip(1).SelectMany(level=>level.renderers).OfType<SkinnedMeshRenderer>())
                    Assert.That(AssetDatabase.GetAssetPath(lodRenderer.sharedMesh),Does.StartWith(generatedFolder+"/"),"Regenerated LOD references must point to their new durable assets.");
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(denseMesh);
                Object.DestroyImmediate(profile);
                if(!string.IsNullOrEmpty(generatedFolder))AssetDatabase.DeleteAsset(generatedFolder);
            }
        }

        [Test]
        public void TierMaterialGenerationRejectsExplicitMapMissingFromShader()
        {
            const string testFolder="Assets/EntityStudioGenerated/UnsupportedMapTests";
            string rmoePath=null;
            string materialFolder=null;
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            RebirthEntityRigWorkspace workspace=null;
            try
            {
                profile.InitializeBipedDefaults();
                profile.entityClass="UnsupportedRmoeTest_"+System.Guid.NewGuid().ToString("N");
                materialFolder="Assets/EntityMaterials/"+profile.entityClass;
                profile.characterShader=Shader.Find("Standard");
                profile.generateLods=false;profile.generateTierMaterials=false;
                profile.createHitColliders=false;profile.createRagdoll=false;
                rmoePath=CreateTextureAsset(testFolder,"ExplicitRmoe");
                profile.rmoeMap=AssetDatabase.LoadAssetAtPath<Texture2D>(rmoePath);
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);

                var error=Assert.Throws<System.InvalidOperationException>(()=>RebirthEntityRigBuilder.RebuildTierMaterials(workspace));
                Assert.That(error.Message,Does.Contain("no supported RMOE/mask texture property"));
                Assert.That(profile.generatedTierMaterials,Is.Empty,"A rejected map binding must not leave a partial material set.");
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);
                if(!string.IsNullOrEmpty(materialFolder)&&AssetDatabase.IsValidFolder(materialFolder))AssetDatabase.DeleteAsset(materialFolder);
                if(!string.IsNullOrEmpty(rmoePath))AssetDatabase.DeleteAsset(rmoePath);
                if(AssetDatabase.IsValidFolder(testFolder))AssetDatabase.DeleteAsset(testFolder);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TierMaterialVariantsKeepSourceSlotLayout()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            RebirthEntityRigWorkspace workspace=null;
            Material sourceMaterial=null;
            Texture2D sourceTexture=null;
            string sourceTexturePath=null;
            string generatedMaterialFolder=null;
            var materialPaths=new List<string>();
            try
            {
                profile.InitializeBipedDefaults();
                profile.entityClass="EntityMaterialTest_"+System.Guid.NewGuid().ToString("N");
                generatedMaterialFolder="Assets/EntityMaterials/"+profile.entityClass;
                profile.characterShader=Shader.Find("Standard");
                profile.generateLods=false;profile.createHitColliders=false;profile.createRagdoll=false;
                sourceMaterial=new Material(profile.characterShader);
                sourceMaterial.color=new Color(.7f,.8f,.9f,1f);
                sourceTexturePath=CreateTextureAsset("Assets/EntityStudioGenerated/TextureTests","MaterialSource");
                sourceTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(sourceTexturePath);
                sourceMaterial.mainTexture=sourceTexture;
                sourceMaterial.mainTextureScale=new Vector2(2f,3f);
                sourceMaterial.mainTextureOffset=new Vector2(.2f,.35f);
                source.GetComponent<MeshRenderer>().sharedMaterial=sourceMaterial;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                var renderer=profile.sourceSkinnedMesh;
                Assert.That(profile.generatedTierMaterials.Count,Is.EqualTo(profile.generatedMaterialSlotCount*5));
                Assert.That(renderer.sharedMaterials.Length,Is.EqualTo(profile.generatedMaterialSlotCount));
                var normal=profile.rendererTierMaterials[0].variants[0];
                Assert.That(normal.color.r,Is.EqualTo(.7f).Within(.001f));
                Assert.That(normal.mainTexture,Is.EqualTo(sourceTexture));
                Assert.That(normal.mainTextureScale.x,Is.EqualTo(2f).Within(.001f));
                Assert.That(normal.mainTextureScale.y,Is.EqualTo(3f).Within(.001f));
                var feral=profile.rendererTierMaterials[0].variants[profile.rendererTierMaterials[0].slotCount];
                Assert.That(feral.color.r,Is.EqualTo(.7f*profile.feralTint.r).Within(.001f));
                var radiated=profile.rendererTierMaterials[0].variants[2*profile.rendererTierMaterials[0].slotCount];
                Assert.That(radiated.color.r,Is.EqualTo(.7f*profile.radiatedTint.r).Within(.001f));
                Assert.That(radiated.color.a,Is.EqualTo(1f).Within(.001f));
                Assert.That(radiated.GetColor("_EmissionColor").g,Is.EqualTo(profile.radiatedTint.g).Within(.001f));
                var charged=profile.rendererTierMaterials[0].variants[3*profile.rendererTierMaterials[0].slotCount];
                Assert.That(charged.color.a,Is.EqualTo(1f).Within(.001f));
                Assert.That(charged.GetColor("_EmissionColor").b,Is.EqualTo(profile.chargedTint.b*(1f+profile.chargedTint.a)*profile.tierEmission).Within(.001f));
                for(int tier=0;tier<5;tier++)
                {
                    RebirthEntityRigBuilder.ApplyTierMaterials(workspace,(ZombieTier)tier);
                    Assert.That(renderer.sharedMaterials.Length,Is.EqualTo(profile.generatedMaterialSlotCount));
                    for(int slot=0;slot<profile.generatedMaterialSlotCount;slot++)
                    {
                        var expected=profile.generatedTierMaterials[tier*profile.generatedMaterialSlotCount+slot];
                        Assert.That(renderer.sharedMaterials[slot],Is.EqualTo(expected));
                    }
                }
                string[] previousMaterialPaths=profile.generatedTierMaterials.Select(AssetDatabase.GetAssetPath).ToArray();
                RebirthEntityRigBuilder.RebuildTierMaterials(workspace);
                Assert.That(workspace.activeTier,Is.EqualTo(ZombieTier.Normal));
                Assert.That(profile.generatedTierMaterials.Count,Is.EqualTo(profile.generatedMaterialSlotCount*5));
                Assert.That(profile.generatedTierMaterials.Select(m=>m.name).Distinct().Count(),Is.EqualTo(profile.generatedTierMaterials.Count),"Regeneration must not leave duplicate generated material assets.");
                foreach(string previousPath in previousMaterialPaths)
                    Assert.That(AssetDatabase.LoadAssetAtPath<Material>(previousPath),Is.Null,"Superseded generated material assets must be removed after successful replacement.");
                string[] currentMaterialPaths=AssetDatabase.FindAssets("t:Material",new[]{generatedMaterialFolder}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
                Assert.That(currentMaterialPaths.Length,Is.EqualTo(profile.generatedTierMaterials.Count),"Only the current tier material set should remain in the entity output folder.");
                foreach(var material in profile.generatedTierMaterials)
                {
                    string path=AssetDatabase.GetAssetPath(material);
                    if(!string.IsNullOrEmpty(path))materialPaths.Add(path);
                }
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);
                if(sourceMaterial!=null)Object.DestroyImmediate(sourceMaterial);
                Object.DestroyImmediate(profile);
                foreach(string path in materialPaths)AssetDatabase.DeleteAsset(path);
                if(!string.IsNullOrEmpty(sourceTexturePath))AssetDatabase.DeleteAsset(sourceTexturePath);
                if(!string.IsNullOrEmpty(generatedMaterialFolder))AssetDatabase.DeleteAsset(generatedMaterialFolder);
                DeleteAssetFolderIfEmpty("Assets/EntityStudioGenerated/TextureTests");
            }
        }

        [Test]
        public void TierMaterialRegenerationRollsBackWhenLodRendererCannotBeMapped()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            profile.InitializeBipedDefaults();
            profile.entityClass="EntityMaterialRollback_"+System.Guid.NewGuid().ToString("N");
            profile.characterShader=Shader.Find("Standard");
            profile.generateLods=false;profile.createHitColliders=false;profile.createRagdoll=false;
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            RebirthEntityRigWorkspace workspace=null;
            string materialFolder="Assets/EntityMaterials/"+profile.entityClass;
            GameObject unmappedObject=null;
            try
            {
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                Material[] originalSet=profile.generatedTierMaterials.ToArray();
                Material[] originalRendererAssignment=(Material[])profile.sourceSkinnedMesh.sharedMaterials.Clone();
                string[] originalPaths=originalSet.Select(AssetDatabase.GetAssetPath).ToArray();
                var group=workspace.generatedSkeleton.gameObject.AddComponent<LODGroup>();
                unmappedObject=new GameObject("UnmappedTierMaterialRenderer");
                unmappedObject.transform.SetParent(workspace.generatedSkeleton,false);
                var unmapped=unmappedObject.AddComponent<SkinnedMeshRenderer>();
                group.SetLODs(new[]{new LOD(.4f,new Renderer[]{profile.sourceSkinnedMesh}),new LOD(.1f,new Renderer[]{unmapped})});
                workspace.lodGroup=group;

                Assert.Throws<System.InvalidOperationException>(()=>RebirthEntityRigBuilder.RebuildTierMaterials(workspace));
                Assert.That(profile.generatedTierMaterials,Is.EqualTo(originalSet));
                Assert.That(profile.generatedMaterialSlotCount,Is.EqualTo(originalSet.Length/5));
                Assert.That(profile.sourceSkinnedMesh.sharedMaterials,Is.EqualTo(originalRendererAssignment));
                foreach(string path in originalPaths)Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path),Is.Not.Null);
                string[] remaining=AssetDatabase.FindAssets("t:Material",new[]{materialFolder}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
                Assert.That(remaining.Length,Is.EqualTo(originalSet.Length),"Failed replacement must remove all partial new assets.");
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(profile);
                if(AssetDatabase.IsValidFolder(materialFolder))AssetDatabase.DeleteAsset(materialFolder);
            }
        }

        [Test]
        public void TierMaterialVariantsKeepEachRendererSlotLayout()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            var source=GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var extraMesh=new Mesh{name="ExtraMaterialPart"};
            RebirthEntityRigWorkspace workspace=null;
            var materialPaths=new List<string>();
            var generatedMeshes=new List<Mesh>();
            string generatedMaterialFolder=null;
            try
            {
                profile.InitializeBipedDefaults();
                profile.entityClass="EntityMultiMaterialTest_"+System.Guid.NewGuid().ToString("N");
                generatedMaterialFolder="Assets/EntityMaterials/"+profile.entityClass;
                profile.characterShader=Shader.Find("Standard");
                profile.generateLods=false;profile.createHitColliders=false;profile.createRagdoll=false;
                extraMesh.vertices=new[]{new Vector3(-.2f,0f,-.2f),new Vector3(.2f,0f,-.2f),new Vector3(0f,.5f,.2f)};
                extraMesh.triangles=new[]{0,2,1};extraMesh.RecalculateBounds();
                AddStaticMesh(source.transform,source.name,extraMesh,new Vector3(.2f,.4f,0f),Quaternion.identity);
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                Assert.That(profile.sourceSkinnedMeshes.Count,Is.EqualTo(2));
                Assert.That(profile.rendererTierMaterials.Count,Is.EqualTo(2));
                Assert.That(profile.generatedMaterialSlotCount,Is.EqualTo(2));
                Assert.That(profile.generatedTierMaterials.Count,Is.EqualTo(10));
                Assert.That(profile.generatedTierMaterials.Select(m=>m.name).Distinct().Count(),Is.EqualTo(10),"Duplicate source object names must not create colliding tier material names.");
                foreach(var renderer in profile.sourceSkinnedMeshes)generatedMeshes.Add(renderer.sharedMesh);
                for(int tier=0;tier<5;tier++)
                {
                    RebirthEntityRigBuilder.ApplyTierMaterials(workspace,(ZombieTier)tier);
                    foreach(var renderer in profile.sourceSkinnedMeshes)
                    {
                        var set=profile.rendererTierMaterials.Find(s=>s.renderer==renderer);
                        Assert.That(renderer.sharedMaterials.Length,Is.EqualTo(set.slotCount));
                        for(int slot=0;slot<set.slotCount;slot++)
                            Assert.That(renderer.sharedMaterials[slot],Is.EqualTo(set.variants[tier*set.slotCount+slot]));
                    }
                }
                foreach(var material in profile.generatedTierMaterials)
                {string path=AssetDatabase.GetAssetPath(material);if(!string.IsNullOrEmpty(path))materialPaths.Add(path);}
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);Object.DestroyImmediate(profile);
                foreach(var generated in generatedMeshes)if(generated!=null)Object.DestroyImmediate(generated);
                Object.DestroyImmediate(extraMesh);
                foreach(string path in materialPaths)AssetDatabase.DeleteAsset(path);
                if(!string.IsNullOrEmpty(generatedMaterialFolder))AssetDatabase.DeleteAsset(generatedMaterialFolder);
            }
        }

        private static void DeleteAssetFolderIfEmpty(string folder)
        {
            if(!AssetDatabase.IsValidFolder(folder))return;
            if(AssetDatabase.FindAssets(string.Empty,new[]{folder}).Length==0)AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void PrefabExportRequiresReviewAndRemovesEditorWorkspaceMarker()
        {
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            Mesh denseTorso=null;
            var source=CreateSyntheticUnriggedHumanoid(out denseTorso);
            RebirthEntityRigWorkspace workspace=null;
            var generatedMeshes=new List<Mesh>();
            string prefabPath=null;
            string bundleOutput=null;
            string generatedLodFolder=null;
            try
            {
                profile.InitializeBipedDefaults();
                profile.entityClass="EntityExportTest_"+System.Guid.NewGuid().ToString("N");
                FitSyntheticProfileToSource(profile,source.transform);
                profile.generateLods=true;profile.generateTierMaterials=true;
                profile.createHitColliders=true;profile.createRagdoll=true;
                workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);
                generatedLodFolder="Assets/EntityStudioGenerated/LODs/"+profile.entityClass+"_"+workspace.generatedAssetId;
                foreach(var renderer in profile.sourceSkinnedMeshes)generatedMeshes.Add(renderer.sharedMesh);
                prefabPath="Assets/EntityStudioGenerated/ExportTests/"+profile.entityClass+".prefab";
                Assert.Throws<System.IO.InvalidDataException>(()=>RebirthEntityPrefabExporter.SaveCleanPrefab(workspace,prefabPath));
                RebirthEntityRigBuilder.RebuildTierMaterials(workspace);
                foreach(var mapping in workspace.lodRendererMappings)
                    Assert.That(mapping.renderer.sharedMaterials,Is.EqualTo(mapping.source.sharedMaterials),"Regenerated materials must reach every LOD.");

                profile.reviewedSkeletonMapping=true;
                profile.reviewedColliderFit=true;
                profile.reviewedLods=true;
                profile.reviewedMaterials=true;
                Assert.That(RebirthEntityRigValidator.Inspect(workspace).Exists(f=>f.code=="TARGET_COMPATIBILITY_NEEDS_REVIEW"),Is.True);
                Assert.Throws<System.IO.InvalidDataException>(()=>RebirthEntityPrefabExporter.SaveCleanPrefab(workspace,prefabPath));
                profile.targetCompatibilityEvidence="Test fixture only: 7DTD 3.0 b259 build 23906531; illustrative evidence note for exercising the target review gate.";
                Assert.Throws<System.IO.InvalidDataException>(()=>RebirthEntityPrefabExporter.SaveCleanPrefab(workspace,prefabPath));
                profile.reviewedTargetCompatibility=true;
                Assert.That(RebirthEntityRigValidator.Inspect(workspace).Exists(f=>f.severity=="ERROR"||f.severity=="WARNING"),Is.False);
                Assert.That(RebirthEntityRigValidator.Inspect(workspace).Exists(f=>f.severity=="ERROR"||f.severity=="WARNING"),Is.False);
                Assert.That(RebirthEntityPrefabExporter.SaveCleanPrefab(workspace,prefabPath),Is.EqualTo(prefabPath));
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab,Is.Not.Null);
                Assert.That(prefab.GetComponentInChildren<RebirthEntityRigWorkspace>(true),Is.Null);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length,Is.EqualTo(0));
                Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,Is.GreaterThanOrEqualTo(11));
                Assert.That(prefab.tag,Is.EqualTo("E_Enemy"));
                string companionMeshFolder="Assets/EntityStudioGenerated/ExportTests/"+profile.entityClass+"_Meshes";
                var prefabRenderer=prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(prefabRenderer.sharedMesh,Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(prefabRenderer.sharedMesh),Does.StartWith(companionMeshFolder+"/"));
                string projectRoot=Directory.GetParent(Application.dataPath).FullName;
                bundleOutput=Path.Combine(projectRoot,"Library","EntityStudioBundleTests",profile.entityClass);
                RebirthEntityBundleReceipt receipt=RebirthEntityBundleBuilder.BuildAndVerify(prefabPath,bundleOutput);
                Assert.That(receipt.status,Is.EqualTo("BUNDLE_RELOADED_NOT_GAME_TESTED"));
                Assert.That(receipt.skinnedRendererCount,Is.GreaterThanOrEqualTo(11));
                Assert.That(receipt.distinctBoneCount,Is.GreaterThan(0));
                Assert.That(receipt.skinnedVertexCount,Is.GreaterThan(0));
                Assert.That(receipt.runtimeScriptCount,Is.EqualTo(0));
                Assert.That(receipt.bundleReference,Does.Contain("#@modfolder:Resources/rebirth_"));
                Assert.That(receipt.tierMaterialCount,Is.EqualTo(55));
                Assert.That(receipt.tierMaterialReferences.Length,Is.EqualTo(55));
                Assert.That(receipt.tierMaterialReferences.Any(r=>r.Contains("_Normal_Slot0")),Is.True);
                Assert.That(receipt.tierMaterialReferences.Any(r=>r.Contains("_Feral_Slot0")),Is.True);
                Assert.That(receipt.tierMaterialReferences.Any(r=>r.Contains("_Radiated_Slot0")),Is.True);
                Assert.That(receipt.tierMaterialReferences.Any(r=>r.Contains("_Charged_Slot0")),Is.True);
                Assert.That(receipt.tierMaterialReferences.Any(r=>r.Contains("_Infernal_Slot0")),Is.True);
                Assert.That(receipt.lodGroupCount,Is.EqualTo(1));
                Assert.That(receipt.lodTriangles.Length,Is.EqualTo(4));
                Assert.That(receipt.lodTriangles[0],Is.GreaterThan(receipt.lodTriangles[1]));
                Assert.That(receipt.lodTriangles[1],Is.GreaterThan(receipt.lodTriangles[2]));
                Assert.That(receipt.lodTriangles[2],Is.GreaterThan(receipt.lodTriangles[3]));
                foreach(var lodRenderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Assert.That(AssetDatabase.GetAssetPath(lodRenderer.sharedMesh),Does.StartWith("Assets/"),"All exported LOD meshes must be durable assets.");
                Assert.That(receipt.capsuleColliderCount,Is.EqualTo(11));
                Assert.That(receipt.rigidbodyCount,Is.EqualTo(receipt.capsuleColliderCount));
                Assert.That(receipt.jointCount,Is.EqualTo(receipt.rigidbodyCount-1));
                Assert.That(File.Exists(receipt.bundlePath),Is.True);
                Assert.That(File.Exists(Path.Combine(bundleOutput,"receipt.json")),Is.True);
            }
            finally
            {
                if(workspace!=null)Object.DestroyImmediate(workspace.gameObject);
                Object.DestroyImmediate(source);Object.DestroyImmediate(profile);
                foreach(var generated in generatedMeshes)if(generated!=null&&string.IsNullOrEmpty(AssetDatabase.GetAssetPath(generated)))Object.DestroyImmediate(generated);
                if(!string.IsNullOrEmpty(prefabPath))AssetDatabase.DeleteAsset(prefabPath);
                if(!string.IsNullOrEmpty(prefabPath))
                {
                    string prefabParent=Path.GetDirectoryName(prefabPath).Replace('\\','/');
                    string companionMeshFolder=prefabParent+"/"+Path.GetFileNameWithoutExtension(prefabPath)+"_Meshes";
                    if(AssetDatabase.IsValidFolder(companionMeshFolder))AssetDatabase.DeleteAsset(companionMeshFolder);
                }
                if(!string.IsNullOrEmpty(profile.entityClass))
                {
                    string materialFolder="Assets/EntityMaterials/"+profile.entityClass;
                    if(AssetDatabase.IsValidFolder(materialFolder))AssetDatabase.DeleteAsset(materialFolder);
                }
                Object.DestroyImmediate(profile);
                if(denseTorso!=null)Object.DestroyImmediate(denseTorso);
                if(!string.IsNullOrEmpty(generatedLodFolder)&&AssetDatabase.IsValidFolder(generatedLodFolder))AssetDatabase.DeleteAsset(generatedLodFolder);
                string bundleTestRoot=Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Library","EntityStudioBundleTests"))+Path.DirectorySeparatorChar;
                if(!string.IsNullOrEmpty(bundleOutput)&&Directory.Exists(bundleOutput)&&bundleOutput.StartsWith(bundleTestRoot,System.StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(bundleOutput,true);
            }
        }

        [Test]
        public void ProfileFingerprintTracksTextureImporterSettings()
        {
            const string testFolder="Assets/EntityStudioGenerated/FingerprintTests";
            string texturePath=null;
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                texturePath=CreateTextureAsset(testFolder,"FingerprintAlbedo");
                profile.bakedAlbedo=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                string before=RebirthEntityRigBuilder.ComputeProfileFingerprint(profile,"fixture-model");
                var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.filterMode=importer.filterMode==FilterMode.Point?FilterMode.Bilinear:FilterMode.Point;
                // The dependency hash does not change until the importer actually reimports this image.
                importer.SaveAndReimport();
                profile.bakedAlbedo=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                string after=RebirthEntityRigBuilder.ComputeProfileFingerprint(profile,"fixture-model");
                Assert.That(after,Is.Not.EqualTo(before),"Changing texture import settings at the same asset path must invalidate prior review state.");
            }
            finally
            {
                Object.DestroyImmediate(profile);
                if(!string.IsNullOrEmpty(texturePath))AssetDatabase.DeleteAsset(texturePath);
                if(AssetDatabase.IsValidFolder(testFolder))AssetDatabase.DeleteAsset(testFolder);
            }
        }

        [Test]
        public void TexturePipelineConfiguresAlbedoNormalAndRmoeImports()
        {
            string albedoPath=null,normalPath=null,rmoePath=null,emissionPath=null;
            const string testFolder="Assets/EntityStudioGenerated/TextureTests";
            var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                // Deliberately misleading names: assigned map roles decide the color space.
                albedoPath=CreateTextureAsset(testFolder,"StudioTest_MaskAlbedo");
                normalPath=CreateTextureAsset(testFolder,"StudioTest_Normal");
                rmoePath=CreateTextureAsset(testFolder,"StudioTest_SurfacePack");
                emissionPath=CreateTextureAsset(testFolder,"StudioTest_Glow");
                profile.bakedAlbedo=AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                profile.normalMap=AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                profile.rmoeMap=AssetDatabase.LoadAssetAtPath<Texture2D>(rmoePath);
                RebirthEntityTextureTools.ConfigureProfileMaps(profile);
                RebirthEntityTextureTools.Configure(AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath),EntityTextureUsage.Emission);

                var albedo=(TextureImporter)AssetImporter.GetAtPath(albedoPath);
                var normal=(TextureImporter)AssetImporter.GetAtPath(normalPath);
                var rmoe=(TextureImporter)AssetImporter.GetAtPath(rmoePath);
                var emission=(TextureImporter)AssetImporter.GetAtPath(emissionPath);
                foreach(var importer in new[]{albedo,normal,rmoe})
                {
                    Assert.That(importer.mipmapEnabled,Is.True);
                    Assert.That(importer.streamingMipmaps,Is.True);
                    Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Trilinear));
                    Assert.That(importer.anisoLevel,Is.EqualTo(8));
                    Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.CompressedHQ));
                }
                Assert.That(albedo.textureType,Is.EqualTo(TextureImporterType.Default));
                Assert.That(albedo.sRGBTexture,Is.True);
                Assert.That(normal.textureType,Is.EqualTo(TextureImporterType.NormalMap));
                Assert.That(normal.sRGBTexture,Is.False);
                Assert.That(rmoe.textureType,Is.EqualTo(TextureImporterType.Default));
                Assert.That(rmoe.sRGBTexture,Is.False);
                Assert.That(emission.alphaSource,Is.EqualTo(TextureImporterAlphaSource.FromInput));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                if(!string.IsNullOrEmpty(albedoPath))AssetDatabase.DeleteAsset(albedoPath);
                if(!string.IsNullOrEmpty(normalPath))AssetDatabase.DeleteAsset(normalPath);
                if(!string.IsNullOrEmpty(rmoePath))AssetDatabase.DeleteAsset(rmoePath);
                if(!string.IsNullOrEmpty(emissionPath))AssetDatabase.DeleteAsset(emissionPath);
                if(AssetDatabase.IsValidFolder(testFolder))AssetDatabase.DeleteAsset(testFolder);
            }
        }

        [Test]
        public void TexturePipelineRollsBackEarlierImporterChangesWhenLaterMapIsInvalid()
        {
            const string testFolder="Assets/EntityStudioGenerated/TextureRollbackTests";
            string albedoPath=null;var profile=ScriptableObject.CreateInstance<RebirthEntityRigProfile>();
            try
            {
                albedoPath=CreateTextureAsset(testFolder,"RollbackAlbedo");
                var albedoImporter=(TextureImporter)AssetImporter.GetAtPath(albedoPath);
                albedoImporter.textureType=TextureImporterType.Default;
                albedoImporter.sRGBTexture=false;
                albedoImporter.mipmapEnabled=false;
                albedoImporter.streamingMipmaps=false;
                albedoImporter.anisoLevel=2;
                albedoImporter.maxTextureSize=512;
                albedoImporter.filterMode=FilterMode.Point;
                albedoImporter.wrapMode=TextureWrapMode.Clamp;
                albedoImporter.textureCompression=TextureImporterCompression.Uncompressed;
                albedoImporter.alphaSource=TextureImporterAlphaSource.None;
                albedoImporter.SaveAndReimport();
                profile.bakedAlbedo=AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                profile.normalMap=Texture2D.whiteTexture; // Built-in, non-asset texture fails after the albedo was prepared.
                Assert.Throws<System.InvalidOperationException>(()=>RebirthEntityTextureTools.ConfigureProfileMaps(profile));
                albedoImporter=(TextureImporter)AssetImporter.GetAtPath(albedoPath);
                Assert.That(albedoImporter.textureType,Is.EqualTo(TextureImporterType.Default));
                Assert.That(albedoImporter.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(albedoImporter.sRGBTexture,Is.False);
                Assert.That(albedoImporter.mipmapEnabled,Is.False);
                Assert.That(albedoImporter.streamingMipmaps,Is.False);
                Assert.That(albedoImporter.anisoLevel,Is.EqualTo(2));
                Assert.That(albedoImporter.maxTextureSize,Is.EqualTo(512));
                Assert.That(albedoImporter.wrapMode,Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(albedoImporter.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(albedoImporter.alphaSource,Is.EqualTo(TextureImporterAlphaSource.None));
                Assert.That(profile.bakedAlbedo,Is.Not.Null);
                Assert.That(profile.normalMap,Is.SameAs(Texture2D.whiteTexture));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                if(!string.IsNullOrEmpty(albedoPath))AssetDatabase.DeleteAsset(albedoPath);
                if(AssetDatabase.IsValidFolder(testFolder))AssetDatabase.DeleteAsset(testFolder);
            }
        }

        private static void AddStaticMesh(Transform parent,string name,Mesh mesh,Vector3 localPosition,Quaternion localRotation)
        {
            var part=new GameObject(name);part.transform.SetParent(parent,false);part.transform.localPosition=localPosition;part.transform.localRotation=localRotation;
            part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>();
        }

        private static int TriangleCount(LOD lod)
        {
            int result=0;
            foreach(var renderer in lod.renderers)
            {
                var skinned=renderer as SkinnedMeshRenderer;
                if(skinned==null||skinned.sharedMesh==null)continue;
                for(int sub=0;sub<skinned.sharedMesh.subMeshCount;sub++)result+=skinned.sharedMesh.GetTriangles(sub).Length/3;
            }
            return result;
        }

        private static void AssertColor(Color actual,Color expected)
        {
            Assert.That(actual.r,Is.EqualTo(expected.r).Within(.0001f));
            Assert.That(actual.g,Is.EqualTo(expected.g).Within(.0001f));
            Assert.That(actual.b,Is.EqualTo(expected.b).Within(.0001f));
            Assert.That(actual.a,Is.EqualTo(expected.a).Within(.0001f));
        }

        private static GameObject CreateSyntheticUnriggedHumanoid(out Mesh denseTorso)
        {
            var root=new GameObject("SyntheticUnriggedHumanoid");
            GameObject Part(PrimitiveType type,string name,Vector3 position,Vector3 euler,Vector3 scale)
            {
                var part=GameObject.CreatePrimitive(type);
                part.name=name;part.transform.SetParent(root.transform,false);
                part.transform.localPosition=position;part.transform.localRotation=Quaternion.Euler(euler);part.transform.localScale=scale;
                return part;
            }

            var torso=Part(PrimitiveType.Capsule,"Torso",new Vector3(0f,1.29f,0f),Vector3.zero,new Vector3(.48f,.40f,.31f));
            denseTorso=Subdivide(torso.GetComponent<MeshFilter>().sharedMesh,2);
            torso.GetComponent<MeshFilter>().sharedMesh=denseTorso;
            Part(PrimitiveType.Sphere,"Head",new Vector3(0f,1.88f,0f),Vector3.zero,new Vector3(.22f,.25f,.22f));
            Part(PrimitiveType.Capsule,"LeftUpperArm",new Vector3(-.39f,1.36f,0f),new Vector3(0f,0f,-52f),new Vector3(.12f,.18f,.12f));
            Part(PrimitiveType.Capsule,"LeftLowerArm",new Vector3(-.74f,1.145f,0f),new Vector3(0f,0f,-63.4f),new Vector3(.10f,.235f,.10f));
            Part(PrimitiveType.Capsule,"RightUpperArm",new Vector3(.39f,1.36f,0f),new Vector3(0f,0f,52f),new Vector3(.12f,.18f,.12f));
            Part(PrimitiveType.Capsule,"RightLowerArm",new Vector3(.74f,1.145f,0f),new Vector3(0f,0f,63.4f),new Vector3(.10f,.235f,.10f));
            Part(PrimitiveType.Capsule,"LeftUpperLeg",new Vector3(-.19f,.70f,0f),Vector3.zero,new Vector3(.18f,.28f,.18f));
            Part(PrimitiveType.Capsule,"LeftLowerLeg",new Vector3(-.19f,.23f,0f),Vector3.zero,new Vector3(.14f,.28f,.14f));
            Part(PrimitiveType.Capsule,"RightUpperLeg",new Vector3(.19f,.70f,0f),Vector3.zero,new Vector3(.18f,.28f,.18f));
            Part(PrimitiveType.Capsule,"RightLowerLeg",new Vector3(.19f,.23f,0f),Vector3.zero,new Vector3(.14f,.28f,.14f));
            Part(PrimitiveType.Cube,"SyntheticAccessoryMesh",new Vector3(.03f,1.06f,.12f),new Vector3(0f,19f,0f),new Vector3(.24f,.20f,.12f));
            return root;
        }

        private static void FitSyntheticProfileToSource(RebirthEntityRigProfile profile,Transform sourceRoot)
        {
            Bounds bounds=RebirthEntityRigBuilder.SourceBoundsInRootSpace(sourceRoot);
            void Place(string bone,float x,float y,float z=0f)
            {
                var spec=profile.bones.Find(candidate=>candidate!=null&&candidate.name==bone);
                Assert.That(spec,Is.Not.Null,"Synthetic profile is missing bone "+bone);
                spec.normalizedPosition=RebirthEntityRigBuilder.NormalizeBonePositionInBounds(bounds,new Vector3(x,y,z));
            }

            Place("Hips",0f,.94f);Place("Spine",0f,1.08f);Place("Spine1",0f,1.22f);
            Place("Spine2",0f,1.36f);Place("Spine3",0f,1.53f);Place("Neck",0f,1.74f);Place("Head",0f,1.88f);
            Place("LeftShoulder",-.16f,1.51f);Place("LeftArm",-.25f,1.47f);Place("LeftForeArm",-.53f,1.25f);Place("LeftHand",-.95f,1.04f);
            Place("RightShoulder",.16f,1.51f);Place("RightArm",.25f,1.47f);Place("RightForeArm",.53f,1.25f);Place("RightHand",.95f,1.04f);
            Place("LeftUpLeg",-.19f,.94f);Place("LeftLeg",-.19f,.48f);Place("LeftFoot",-.19f,-.02f);
            Place("RightUpLeg",.19f,.94f);Place("RightLeg",.19f,.48f);Place("RightFoot",.19f,-.02f);
        }

        private static string CreateTextureAsset(string folder,string name)
        {
            EnsureAssetFolder(folder);
            var image=new Texture2D(4,4,TextureFormat.RGBA32,false);
            image.SetPixels(new[]{Color.white,Color.gray,Color.black,Color.red,
                Color.green,Color.blue,Color.cyan,Color.magenta,
                Color.yellow,Color.gray,Color.white,Color.black,
                Color.red,Color.green,Color.blue,Color.white});
            image.Apply();
            byte[] png=image.EncodeToPNG();
            Object.DestroyImmediate(image);
            string path=folder+"/"+name+".png";
            File.WriteAllBytes(path,png);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] parts=folder.Split('/');string current=parts[0];
            for(int i=1;i<parts.Length;i++)
            {
                string next=current+"/"+parts[i];
                if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);
                current=next;
            }
        }

        private static Mesh Subdivide(Mesh source,int levels)
        {
            var vertices=new List<Vector3>(source.vertices);
            var uv=new List<Vector2>(source.uv);
            var submeshes=new List<int[]>();
            for(int s=0;s<source.subMeshCount;s++)submeshes.Add(source.GetTriangles(s));
            for(int level=0;level<levels;level++)
            {
                var midpointCache=new Dictionary<long,int>();
                int Midpoint(int a,int b)
                {
                    int low=Mathf.Min(a,b),high=Mathf.Max(a,b);long key=((long)low<<32)|(uint)high;
                    if(midpointCache.TryGetValue(key,out int existing))return existing;
                    int index=vertices.Count;vertices.Add((vertices[a]+vertices[b])*.5f);
                    uv.Add((uv[a]+uv[b])*.5f);midpointCache[key]=index;return index;
                }
                for(int s=0;s<submeshes.Count;s++)
                {
                    int[] triangles=submeshes[s];var refined=new List<int>(triangles.Length*4);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                        int ab=Midpoint(a,b),bc=Midpoint(b,c),ca=Midpoint(c,a);
                        refined.AddRange(new[]{a,ab,ca, ab,b,bc, ca,bc,c, ab,bc,ca});
                    }
                    submeshes[s]=refined.ToArray();
                }
            }
            var result=new Mesh{name="SyntheticDenseCapsule"};
            result.indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16;
            result.SetVertices(vertices);result.SetUVs(0,uv);result.subMeshCount=submeshes.Count;
            for(int s=0;s<submeshes.Count;s++)result.SetTriangles(submeshes[s],s,false);
            result.RecalculateNormals();result.RecalculateBounds();return result;
        }
    }
}
