using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace RebirthEntityStudio
{
    public static class RebirthEntityRigBuilder
    {
        private static readonly string[] RequiredGameTags =
        {
            "E_Enemy", "E_BP_BipedRoot", "E_BP_Body", "E_BP_Head",
            "E_BP_LArm", "E_BP_RArm", "E_BP_LLowerArm", "E_BP_RLowerArm",
            "E_BP_LLeg", "E_BP_RLeg", "E_BP_LLowerLeg", "E_BP_RLowerLeg",
            "E_BP_Eye", "AudioRigidBody", "LOD", "L_HeadGore", "L_LeftLowerArmGore",
            "L_RightLowerArmGore", "L_LeftLowerLegGore", "L_RightLowerLegGore",
            "L_LeftUpperArmGore", "L_RightUpperArmGore", "L_LeftUpperLegGore", "L_RightUpperLegGore"
        };

        public static RebirthEntityRigWorkspace CreateWorkspace(GameObject model, RebirthEntityRigProfile profile,bool bakeRepairAtlas=false)
        {
            if (model == null || profile == null) throw new ArgumentException("Model and entity rig profile are required.");
            profile.EnsureBoneDeformFlags();
            if (model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                throw new InvalidOperationException("This workflow is for an unrigged/static source mesh. Existing skinned rigs need an explicit retarget/import path.");
            if (model.GetComponentInChildren<LODGroup>(true) != null)
                throw new InvalidOperationException("Source model contains an LODGroup. Map only its intended LOD0 renderers before building; existing LOD meshes cannot be treated as separate body parts.");
            if (model.GetComponentInChildren<MeshRenderer>(true) == null)
                throw new InvalidOperationException("Source model has no MeshRenderer geometry.");
            EnsureRequiredGameTags();
            // Review flags belong to one generated workspace and its exact inputs. Never carry
            // approvals from a previous model/profile state into a fresh build.
            profile.reviewedSkeletonMapping=false;
            profile.reviewedColliderFit=false;
            profile.reviewedLods=false;
            profile.reviewedMaterials=false;
            profile.reviewedTargetCompatibility=false;
            var root = new GameObject("REBIRTH_Entity_" + model.name);
            root.transform.SetPositionAndRotation(model.transform.position,Quaternion.identity);
            root.transform.localScale=Vector3.one;
            Undo.RegisterCreatedObjectUndo(root, "Create entity rig workspace");
            RebirthEntityRigWorkspace ws=null;
            try
            {
                TryAssignTag(root, "E_Enemy");
                ws = Undo.AddComponent<RebirthEntityRigWorkspace>(root);
                ws.profile = profile;
                var origin = Child(root.transform, "Origin", "E_BP_BipedRoot");
                ws.generatedSkeleton = origin;
                var visuals = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (visuals == null) visuals = UnityEngine.Object.Instantiate(model);
                Undo.RegisterCreatedObjectUndo(visuals, "Add source entity mesh");
                visuals.name = "SourceVisual_Unrigged";
                StripSourcePhysics(visuals);
                // Keep the imported hierarchy under the biped root. Generated skinned renderers
                // are parented under each source mesh node so all authored local transforms,
                // including nested/non-uniform transforms, remain exact.
                Undo.SetTransformParent(visuals.transform, origin, "Keep unrigged source mesh hierarchy");
                visuals.transform.localPosition = Vector3.zero;
                // SetTransformParent preserves the imported instance's world pose; leave its
                // authored local axis correction intact for the game-space fit below.
                visuals.transform.localScale = model.transform.lossyScale;
                ws.sourceVisual = visuals.transform;
                if(profile.targetHeight>0f)
                {
                    Bounds importedBounds=SourceBounds(ws.sourceVisual);
                    if(importedBounds.size.y<.00001f)throw new InvalidOperationException("Imported model has no usable world height.");
                    visuals.transform.localScale*=profile.targetHeight/importedBounds.size.y;
                }
                if (profile.bones == null || profile.bones.Count == 0) profile.InitializeBipedDefaults();
                profile.EnsureDefaultGoreAnchors();
                profile.EnsureDefaultEyeAnchors();
                ws.sourceModelAssetPath=AssetDatabase.GetAssetPath(model);
                ws.sourceModelObject=string.IsNullOrEmpty(ws.sourceModelAssetPath)?model:null;
                ws.sourceModelKey=BuildModelKey(model);
                ws.profileBuildFingerprint=ComputeProfileFingerprint(profile,ws.sourceModelKey);
                BuildBones(ws);
                BuildSkinnedMesh(ws);
                if(bakeRepairAtlas)RebirthEntityRepairAtlasBake.Prepare(ws);
                BuildGoreAnchors(ws);
                BuildEyeAnchors(ws);
                BuildCollidersAndRagdoll(ws);
                if (profile.generateLods) RebuildLods(ws);
                if (profile.generateTierMaterials) RebuildTierMaterials(ws);
                ws.sourceModelIsSceneInstance=!EditorUtility.IsPersistent(model);
                ws.sourceModelObject=ws.sourceModelIsSceneInstance?model:null;
                Selection.activeGameObject = root;
                return ws;
            }
            catch
            {
                if(ws!=null&&!string.IsNullOrEmpty(ws.generatedAssetId))
                    AssetDatabase.DeleteAsset("Assets/EntityStudioGenerated/LODs/"+SanitizeAssetName(profile.entityClass)+"_"+ws.generatedAssetId);
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public static int EstimateBodySegments(RebirthEntityRigProfile profile)
        { if(profile==null||profile.bones==null)return 0;int count=0;foreach(var bone in profile.bones)if(bone!=null&&bone.deformsMesh)count++;return count; }

        private static void StripSourcePhysics(GameObject visuals)
        {
            foreach(var collider in visuals.GetComponentsInChildren<Collider>(true))Undo.DestroyObjectImmediate(collider);
            foreach(var body in visuals.GetComponentsInChildren<Rigidbody>(true))Undo.DestroyObjectImmediate(body);
            foreach(var joint in visuals.GetComponentsInChildren<Joint>(true))Undo.DestroyObjectImmediate(joint);
        }

        public static string ComputeProfileFingerprint(RebirthEntityRigProfile p,string sourceModelKey)
        {
            var b=new StringBuilder(2048);
            b.Append("outgoing-bone-physics-v3-owned-eyes-v1-hand-field-v2|");
            b.Append(sourceModelKey).Append('|').Append(p.entityClass).Append('|').Append(p.prefabFamily).Append('|').Append(p.gameVersion).Append('|').Append(p.sourceUpAxis).Append('|');
            Number(b,p.skinWeightFalloffExponent);Number(b,p.colliderPadding);Number(b,p.targetHeight);
            b.Append(p.useSurfaceGeodesicWeights).Append('|');
            b.Append(p.separateFusedArms).Append('|');
            if(p.handCutInset!=0)b.Append("handCutInset=").Append(p.handCutInset.ToString("R",CultureInfo.InvariantCulture)).Append('|');
            if(p.wristCutInset!=0)b.Append("wristCutInset=").Append(p.wristCutInset.ToString("R",CultureInfo.InvariantCulture)).Append('|');
            b.Append(p.wrapMissingTorsoTexture).Append('|');
            b.Append(p.refineBodyWeightsWithPoses).Append('|');
            Number(b,p.ragdollMass);
            b.Append(p.useBoxTorsoAndLegs).Append('|');
            b.Append(p.useBoxTorso).Append('|').Append(p.includeNeckCollider).Append('|');
            b.Append(p.includeTerminalPartsInColliders).Append('|').Append(p.preferNarrowCapsules).Append('|');
            AppendAssetDependency(b,p.ragdollMaterial);
            foreach(var mode in p.colliderModes){b.Append(mode.bone).Append('|').Append(mode.collisionLayer).Append('|').Append(mode.ragdollLayer).Append('|');AppendVector(b,mode.collisionScale);AppendVector(b,mode.ragdollScale);AppendVector(b,mode.collisionOffset);AppendVector(b,mode.ragdollOffset);}
            b.Append(p.albedoMaxSize).Append('|').Append(p.normalMaxSize).Append('|').Append(p.dataMaxSize).Append('|');
            b.Append(p.createRagdoll).Append('|').Append(p.createHitColliders).Append('|').Append(p.generateLods).Append('|').Append(p.generateTierMaterials).Append('|');
            Number(b,p.lod0TransitionHeight);Number(b,p.lod1TransitionHeight);Number(b,p.lod2TransitionHeight);Number(b,p.lod3TransitionHeight);
            Number(b,p.lod1Ratio);Number(b,p.lod2Ratio);Number(b,p.lod3Ratio);
            AppendColor(b,p.feralTint);AppendColor(b,p.radiatedTint);AppendColor(b,p.chargedTint);AppendColor(b,p.infernalTint);Number(b,p.tierEmission);
            AppendAssetDependency(b,p.bakedAlbedo);AppendAssetDependency(b,p.normalMap);AppendAssetDependency(b,p.rmoeMap);
            if(p.nativeRmoeMaps!=null)foreach(var map in p.nativeRmoeMaps)AppendAssetDependency(b,map);
            AppendAssetDependency(b,p.sourceRoughness);AppendAssetDependency(b,p.sourceMetallic);
            if(p.materialTextureOverrides!=null)foreach(var maps in p.materialTextureOverrides){b.Append(maps.mapName).Append('|');AppendAssetDependency(b,maps.sourceMaterial);AppendAssetDependency(b,maps.albedo);AppendAssetDependency(b,maps.normalMap);AppendAssetDependency(b,maps.roughness);AppendAssetDependency(b,maps.metallic);}
            Number(b,p.eyeHeightRatio);Number(b,p.eyeHalfSeparationRatio);Number(b,p.eyeWidthRatio);Number(b,p.eyeRadiusHeightRatio);
            b.Append(p.characterShader!=null?p.characterShader.name:string.Empty).Append('|');
            AppendAssetDependency(b,p.characterShader);
            if(p.bones!=null)foreach(var bone in p.bones)
            {
                if(bone==null){b.Append("<null>|");continue;}
                b.Append(bone.name).Append('|').Append(bone.parent).Append('|').Append(bone.gameTag).Append('|').Append(bone.required).Append('|');
                b.Append(bone.deformsMesh).Append('|');
                b.Append(bone.useInfluenceBounds).Append('|');AppendVector(b,bone.influenceMin);AppendVector(b,bone.influenceMax);
                Number(b,bone.normalizedPosition.x);Number(b,bone.normalizedPosition.y);Number(b,bone.normalizedPosition.z);
                Number(b,bone.referenceLocalEuler.x);Number(b,bone.referenceLocalEuler.y);Number(b,bone.referenceLocalEuler.z);
            }
            if(p.requiredGoreAnchors!=null)foreach(string anchor in p.requiredGoreAnchors)b.Append(anchor).Append('|');
            if(p.requiredEyeAnchors!=null)foreach(string anchor in p.requiredEyeAnchors)b.Append(anchor).Append('|');
            return Hash128.Compute(b.ToString()).ToString();
        }

        public static string BuildModelKey(GameObject model)
        {
            var b=new StringBuilder();
            string assetPath=AssetDatabase.GetAssetPath(model);
            b.Append(assetPath).Append('|').Append(model.name).Append('|');
            if(!string.IsNullOrEmpty(assetPath))b.Append(AssetDatabase.GetAssetDependencyHash(assetPath)).Append('|');
            foreach(Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                b.Append(AnimationUtility.CalculateTransformPath(t,model.transform)).Append('|');
                AppendVector(b,t.localPosition);AppendVector(b,t.localEulerAngles);AppendVector(b,t.localScale);
                var filter=t.GetComponent<MeshFilter>();
                if(filter!=null&&filter.sharedMesh!=null)
                {
                    string meshPath=AssetDatabase.GetAssetPath(filter.sharedMesh);
                    b.Append(meshPath).Append(':').Append(filter.sharedMesh.name).Append(':').Append(filter.sharedMesh.vertexCount).Append(':').Append(filter.sharedMesh.subMeshCount).Append('|');
                    if(!string.IsNullOrEmpty(meshPath))b.Append(AssetDatabase.GetAssetDependencyHash(meshPath)).Append('|');
                }
                var skinned=t.GetComponent<SkinnedMeshRenderer>();
                if(skinned!=null&&skinned.sharedMesh!=null)
                {
                    string meshPath=AssetDatabase.GetAssetPath(skinned.sharedMesh);
                    b.Append(meshPath).Append(':').Append(skinned.sharedMesh.name).Append(':').Append(skinned.sharedMesh.vertexCount).Append(':').Append(skinned.sharedMesh.subMeshCount).Append('|');
                    if(!string.IsNullOrEmpty(meshPath))b.Append(AssetDatabase.GetAssetDependencyHash(meshPath)).Append('|');
                }
                var renderer=t.GetComponent<Renderer>();
                if(renderer!=null)foreach(var material in renderer.sharedMaterials)
                {
                    if(material==null){b.Append("<null>");continue;}
                    string materialPath=AssetDatabase.GetAssetPath(material);
                    b.Append(materialPath).Append(':').Append(material.name).Append('|');
                    if(!string.IsNullOrEmpty(materialPath))b.Append(AssetDatabase.GetAssetDependencyHash(materialPath)).Append('|');
                }
            }
            return Hash128.Compute(b.ToString()).ToString();
        }

        private static void Number(StringBuilder b,float value){b.Append(value.ToString("R",CultureInfo.InvariantCulture)).Append('|');}
        private static void AppendVector(StringBuilder b,Vector3 value){Number(b,value.x);Number(b,value.y);Number(b,value.z);}
        private static void AppendColor(StringBuilder b,Color value){Number(b,value.r);Number(b,value.g);Number(b,value.b);Number(b,value.a);}
        private static void AppendAssetDependency(StringBuilder b,UnityEngine.Object asset)
        {
            string path=asset!=null?AssetDatabase.GetAssetPath(asset):string.Empty;
            b.Append(path).Append('|');
            if(!string.IsNullOrEmpty(path))b.Append(AssetDatabase.GetAssetDependencyHash(path)).Append('|');
        }

        private static void BuildBones(RebirthEntityRigWorkspace ws)
        {
            var p = ws.profile;
            Bounds bounds = SourceBoundsInRootSpace(ws.sourceVisual);
            Bounds worldBounds=SourceBounds(ws.sourceVisual);
            p.overallBoundsCenter = ws.transform.InverseTransformPoint(worldBounds.center);
            p.overallBoundsSize = worldBounds.size;
            p.estimatedHeight = Mathf.Max(0.1f, p.overallBoundsSize.y);
            var nodes = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase) { ["Origin"] = ws.generatedSkeleton };
            foreach (var spec in p.bones)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.name) || spec.name == "Origin") continue;
                if (!nodes.TryGetValue(spec.parent ?? "Origin", out Transform parent))
                    throw new InvalidOperationException("Bone '" + spec.name + "' parent is missing or comes after its child: " + spec.parent);
                Transform bone = Child(parent, spec.name, spec.gameTag);
                Vector3 n = spec.normalizedPosition;
                // FBX local bounds may remain Z-up even though its imported hierarchy rotates
                // the model upright in world space. Map the canonical profile's Y height onto
                // source Z, then let the preserved importer transform orient the point.
                bone.position = ws.sourceVisual.TransformPoint(BonePositionInSourceBounds(bounds,n,p.sourceUpAxis));
                bone.localRotation = Quaternion.Euler(spec.referenceLocalEuler);
                nodes.Add(spec.name, bone);
            }
            // Orient seed joints along their outgoing segments before binding the mesh.
            // CapsuleCollider supports cardinal local axes; these axes also give joints a
            // useful anatomical frame. Imported reference orientations take precedence.
            bool hasReferenceAxes=false;
            foreach(var spec in p.bones)if(spec!=null&&spec.referenceLocalEuler.sqrMagnitude>.000001f)hasReferenceAxes=true;
            if(!hasReferenceAxes)
            {
                var deforming=new List<Transform>();
                foreach(var spec in p.bones)if(spec!=null&&spec.deformsMesh&&nodes.TryGetValue(spec.name,out Transform node))deforming.Add(node);
                var positions=new Dictionary<Transform,Vector3>();var orientations=new Dictionary<Transform,Quaternion>();
                var helperPositions=new Dictionary<Transform,Vector3>();
                foreach(var pair in nodes)if(!deforming.Contains(pair.Value))helperPositions[pair.Value]=pair.Value.position;
                foreach(var bone in deforming)
                {
                    positions[bone]=bone.position;
                    Transform child=PrimaryDeformingChild(bone,deforming);
                    Vector3 direction=child!=null?child.position-bone.position:bone.parent!=null?bone.position-bone.parent.position:Vector3.up;
                    if(bone.name.Contains("Foot"))direction=Vector3.forward;
                    else if(bone.name=="Head")direction=Vector3.up;
                    if(direction.sqrMagnitude>.000001f)
                    {
                        Vector3 up=direction.normalized,forward=Vector3.ProjectOnPlane(ws.transform.forward,up);
                        if(forward.sqrMagnitude<.000001f)forward=Vector3.ProjectOnPlane(ws.transform.up,up);
                        orientations[bone]=Quaternion.LookRotation(forward.normalized,up);
                    }
                    else orientations[bone]=Quaternion.identity;
                }
                foreach(var bone in deforming)bone.SetPositionAndRotation(positions[bone],orientations[bone]);
                foreach(var pair in helperPositions)pair.Key.position=pair.Value;
            }
        }

        private static void BuildSkinnedMesh(RebirthEntityRigWorkspace ws)
        {
            var p = ws.profile;
            Bounds canonicalBounds=SourceBoundsInRootSpace(ws.sourceVisual);
            MeshFilter[] filters = ws.sourceVisual.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
                throw new InvalidOperationException("Source hierarchy contains no static MeshFilter geometry.");
            foreach (var group in new HashSet<GameObject>(Array.ConvertAll(filters, filter => filter.gameObject)))
            {
                int count=0;foreach(var filter in filters)if(filter.gameObject==group)count++;
                if(count>1)throw new InvalidOperationException("Multiple MeshFilters on one GameObject are ambiguous. Put each source mesh on its own child GameObject; material submeshes on one MeshFilter are supported.");
            }
            var bones = new List<Transform>();
            foreach (var spec in p.bones)
            {
                if(spec==null||!spec.deformsMesh)continue;
                Transform found = FindRelative(ws.generatedSkeleton, spec.name);
                if (found != null) bones.Add(found);
            }
            if (bones.Count == 0)
                throw new InvalidOperationException("The rig profile has no usable bones. Restore or add at least one bone before building the skinned mesh.");
            if(p.sourceSkinnedMeshes==null)p.sourceSkinnedMeshes=new List<SkinnedMeshRenderer>();
            p.sourceSkinnedMeshes.Clear();
            if(p.rendererTierMaterials==null)p.rendererTierMaterials=new List<EntityRendererTierMaterials>();
            p.rendererTierMaterials.Clear();
            if(p.generatedTierMaterials==null)p.generatedTierMaterials=new List<Material>();
            p.generatedTierMaterials.Clear();p.generatedMaterialSlotCount=0;p.reviewedMaterials=false;
            p.sourceSkinnedMesh = null;
            int index=0;
            foreach (MeshFilter source in filters)
            {
                if(source.sharedMesh==null)throw new InvalidOperationException("Source MeshFilter has no mesh: "+AnimationUtility.CalculateTransformPath(source.transform,ws.sourceVisual));
                var sourceRenderer=source.GetComponent<MeshRenderer>();
                if(sourceRenderer==null)throw new InvalidOperationException("Source MeshFilter has no MeshRenderer: "+AnimationUtility.CalculateTransformPath(source.transform,ws.sourceVisual));

                // Matching the source node's transform avoids approximating a nested transform
                // chain with lossyScale (which cannot represent shear from rotated parents).
                var go = new GameObject("GeneratedSkinnedMesh_"+index+"_"+source.gameObject.name);
                Undo.RegisterCreatedObjectUndo(go, "Create skinned entity mesh");
                Undo.SetTransformParent(go.transform, source.transform, "Preserve source mesh transform");
                go.transform.localPosition=Vector3.zero;
                go.transform.localRotation=Quaternion.identity;
                go.transform.localScale=Vector3.one;
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                var clone = CreateReadableMeshCopy(source.sharedMesh);
                clone.name = source.sharedMesh.name + "_REBIRTH_Skinned_"+index;
                var handRange=new Vector2(float.PositiveInfinity,float.NegativeInfinity);
                foreach(var spec in p.bones)if(spec.name.Contains("Hand")){handRange.x=Mathf.Min(handRange.x,spec.normalizedPosition.y);handRange.y=Mathf.Max(handRange.y,spec.normalizedPosition.y);}
                if(float.IsInfinity(handRange.x))handRange=new Vector2(.42f,.535f);
                else if(handRange.y-handRange.x<.02f)handRange.x-=.10f;
                handRange+=new Vector2(-.015f,.025f);
                int[] surfaceGroups=p.separateFusedArms?RebirthEntityMeshCleanup.SeparateFusedArms(clone,source.transform,new Bounds(ws.transform.TransformPoint(p.overallBoundsCenter),p.overallBoundsSize),handRange,ws.surfaceRepairInputs,p.handCutInset,p.wristCutInset):null;
                Vector3[] vertices = clone.vertices;
                if(vertices.Length==0)throw new InvalidOperationException("The readable authoring mesh has no vertices: "+source.name);
                var weights = new BoneWeight[vertices.Length];
                Vector3[] starts = new Vector3[bones.Count];
                Vector3[] ends = new Vector3[bones.Count];
                float[] envelopes = new float[bones.Count];
                var eligibility=new bool[bones.Count];
                var specs=new EntityBoneSpec[bones.Count];
                int[] parentBoneIndices = new int[bones.Count];
                for (int i=0;i<bones.Count;i++)
                {
                    starts[i]=bones[i].position;
                    parentBoneIndices[i]=FindNearestDeformingParentIndex(bones[i],bones);
                    Transform child=PrimaryDeformingChild(bones[i],bones);
                    ends[i]=child!=null?child.position:starts[i];
                    if(bones[i].name=="Head")ends[i]=starts[i]+ws.transform.up*p.estimatedHeight*.10f;
                    if(child==null&&parentBoneIndices[i]>=0)
                    {
                        Vector3 direction=(starts[i]-bones[parentBoneIndices[i]].position).normalized;
                        if(bones[i].name=="Head"||bones[i].name.Contains("Foot"))direction=bones[i].up;
                        float extent=bones[i].name=="Head"?.10f:bones[i].name.Contains("Foot")?.08f:.025f;
                        ends[i]+=direction*p.estimatedHeight*extent;
                    }
                    envelopes[i]=SkinningEnvelopeRadius(bones[i].name,p.estimatedHeight);
                    specs[i]=p.bones.Find(spec=>spec.name==bones[i].name);
                }
                var handBones=new bool[bones.Count];for(int i=0;i<bones.Count;i++)handBones[i]=bones[i].name.Contains("Hand")||bones[i].name.Contains("Arm")||bones[i].name.Contains("Shoulder");
                float falloff=Mathf.Clamp(p.skinWeightFalloffExponent,0.5f,8f);
                Vector3[] worldPoints=new Vector3[vertices.Length];
                bool[][] regionEligibility=new bool[bones.Count][];
                for(int i=0;i<bones.Count;i++)regionEligibility[i]=new bool[vertices.Length];
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 sourceWorld=source.transform.TransformPoint(vertices[v]);
                    worldPoints[v]=sourceWorld;
                    Vector3 normalized=NormalizeBonePositionInSourceBounds(canonicalBounds,ws.sourceVisual.InverseTransformPoint(sourceWorld),p.sourceUpAxis);
                    for(int i=0;i<specs.Length;i++)
                    {
                        var spec=specs[i];
                        eligibility[i]=!spec.useInfluenceBounds||(normalized.x>=spec.influenceMin.x&&normalized.x<=spec.influenceMax.x&&
                            normalized.y>=spec.influenceMin.y&&normalized.y<=spec.influenceMax.y&&normalized.z>=spec.influenceMin.z&&normalized.z<=spec.influenceMax.z);
                        if(surfaceGroups!=null)
                        {
                            bool arm=spec.name.Contains("Arm")||spec.name.Contains("Hand");
                            bool armChain=arm||spec.name.Contains("Shoulder");
                            if(surfaceGroups[v]==0)
                            {
                                bool vertical=!spec.useInfluenceBounds||(normalized.y>=spec.influenceMin.y&&normalized.y<=spec.influenceMax.y);
                                bool side=!spec.name.Contains("Leg")&&!spec.name.Contains("Foot")||
                                    (spec.name.StartsWith("Left",StringComparison.Ordinal)?normalized.x<=.51f:normalized.x>=.49f);
                                if(spec.name=="Head"||spec.name=="Neck")side=normalized.x>=spec.influenceMin.x&&normalized.x<=spec.influenceMax.x;
                                eligibility[i]=!arm&&vertical&&side;
                            }
                            else
                            {
                                // The separated arm is already an anatomical mask.
                                // A boolean finger box creates a weight cliff at
                                // the wrist; distance support must fade it smoothly.
                                eligibility[i]=armChain&&spec.name.StartsWith(surfaceGroups[v]==1?"Left":"Right",StringComparison.Ordinal);
                            }
                        }
                        regionEligibility[i][v]=eligibility[i];
                    }
                    weights[v] = CalculateConnectedBoneWeight(sourceWorld, starts, ends, parentBoneIndices, falloff,envelopes,eligibility,handBones);
                }
                if(p.useSurfaceGeodesicWeights)
                {
                    var seedMargins=new float[bones.Count];
                    for(int i=0;i<bones.Count;i++)
                    {
                        // Narrow finger seeds remain an unsuccessful Farmer
                        // experiment; retain the established field until reviewed.
                        seedMargins[i]=p.estimatedHeight*.004f;
                    }
                    float[][] surface=RebirthEntitySurfaceWeights.Build(worldPoints,clone.triangles,starts,ends,regionEligibility,p.estimatedHeight,surfaceGroups,seedMargins);
                    var distances=new float[bones.Count];int fallbackVertices=0;
                    for(int v=0;v<vertices.Length;v++)
                    {
                        bool hasFinite=false;
                        for(int i=0;i<bones.Count;i++)
                        {
                            eligibility[i]=regionEligibility[i][v];
                            float distance=surface[i][v];
                            distances[i]=eligibility[i]?Mathf.Max(distance*distance,.000001f):float.PositiveInfinity;
                            if(eligibility[i]&&!float.IsInfinity(distance))hasFinite=true;
                        }
                        if(hasFinite)weights[v]=WeightsFromDistances(distances,parentBoneIndices,falloff,eligibility,handBones);
                        else fallbackVertices++;
                    }
                    Debug.Log("REBIRTH SURFACE WEIGHTS: renderer="+smr.name+" vertices="+vertices.Length+" disconnectedFallbackVertices="+fallbackVertices);
                }
                clone.boneWeights = weights;
                // The renderer transform is source-aligned, so each bind pose keeps the
                // original mesh-local vertex positions and nested source transforms intact.
                clone.bindposes = bindposes(bones, smr.transform);
                clone.RecalculateBounds();
                smr.sharedMesh = clone;
                smr.sharedMaterials = sourceRenderer.sharedMaterials;
                CopyRendererSettings(sourceRenderer,smr);
                // Native EModelBase downgrades Auto to Bone2. Preserve the four
                // influences authored for joint/finger deformation explicitly.
                smr.quality=SkinQuality.Bone4;
                smr.bones = bones.ToArray();
                smr.rootBone = ws.generatedSkeleton;
                smr.updateWhenOffscreen = false;
                p.sourceSkinnedMeshes.Add(smr);
                if(p.sourceSkinnedMesh==null)p.sourceSkinnedMesh=smr;

                // This is an instantiated workspace copy, never the model asset. Removing its
                // static renderer/filter prevents LOD generation from collecting duplicate LOD0.
                Undo.DestroyObjectImmediate(sourceRenderer);
                Undo.DestroyObjectImmediate(source);
                index++;
            }
            ws.sourceSkinnedMeshes=p.sourceSkinnedMeshes;
        }

        public static void BindWorkspaceReferences(RebirthEntityRigWorkspace workspace)
        {
            if(workspace==null||workspace.profile==null)return;
            workspace.profile.sourceSkinnedMeshes=workspace.sourceSkinnedMeshes;
            workspace.profile.sourceSkinnedMesh=workspace.sourceSkinnedMeshes.Count>0?workspace.sourceSkinnedMeshes[0]:null;
            workspace.profile.rendererTierMaterials=workspace.rendererTierMaterials;
        }

        private static void CopyRendererSettings(MeshRenderer source,SkinnedMeshRenderer destination)
        {
            destination.enabled=source.enabled;
            destination.shadowCastingMode=source.shadowCastingMode;
            destination.receiveShadows=source.receiveShadows;
            destination.lightProbeUsage=source.lightProbeUsage;
            destination.reflectionProbeUsage=source.reflectionProbeUsage;
            destination.motionVectorGenerationMode=source.motionVectorGenerationMode;
            destination.allowOcclusionWhenDynamic=source.allowOcclusionWhenDynamic;
            destination.renderingLayerMask=source.renderingLayerMask;
            destination.sortingLayerID=source.sortingLayerID;
            destination.sortingOrder=source.sortingOrder;
            destination.probeAnchor=source.probeAnchor;
            destination.lightProbeProxyVolumeOverride=source.lightProbeProxyVolumeOverride;
        }

        private static Mesh CreateReadableMeshCopy(Mesh source)
        {
            // Instantiating an imported mesh can retain its non-readable flag. A new
            // authoring mesh must retain CPU data while weights/topology are generated.
            var mesh=new Mesh{indexFormat=source.indexFormat};
            mesh.vertices=source.vertices;mesh.normals=source.normals;mesh.tangents=source.tangents;
            if(source.colors32.Length>0)mesh.colors32=source.colors32;
            for(int channel=0;channel<8;channel++)
            {var uv=new List<Vector4>();source.GetUVs(channel,uv);if(uv.Count>0)mesh.SetUVs(channel,uv);}
            mesh.subMeshCount=source.subMeshCount;
            for(int sub=0;sub<source.subMeshCount;sub++)mesh.SetTriangles(source.GetTriangles(sub),sub);
            mesh.bounds=source.bounds;return mesh;
        }

        private static int FindNearestDeformingParentIndex(Transform bone,List<Transform> bones)
        {
            for(Transform ancestor=bone!=null?bone.parent:null;ancestor!=null;ancestor=ancestor.parent)
                for(int i=0;i<bones.Count;i++)if(bones[i]==ancestor)return i;
            return -1;
        }

        private static Transform PrimaryDeformingChild(Transform bone,List<Transform> bones)
        {
            // Tip helpers define a distal segment's real length even though they
            // intentionally have no mesh weights of their own.
            foreach(Transform child in bone)
                if(child.name.EndsWith("ToeEnd",StringComparison.Ordinal)||(bone.name.Contains("Hand")&&child.name.EndsWith("4",StringComparison.Ordinal))||(bone.name=="Head"&&child.name=="HeadEnd"))return child;
            // The spine owns the torso; shoulder and hip branches must not replace its axis.
            string preferred=bone.name=="Hips"?"Spine":bone.name=="Spine3"?"Neck":
                bone.name=="LeftHand"||bone.name=="RightHand"?bone.name+"Middle1":null;
            if(preferred!=null)foreach(Transform candidate in bones)if(candidate.name==preferred)return candidate;
            foreach(Transform candidate in bones)if(candidate.parent==bone)return candidate;
            return null;
        }

        private static float SkinningEnvelopeRadius(string name,float height)
        {
            float ratio=name=="Hips"||name.StartsWith("Spine",StringComparison.Ordinal)?.065f:
                name=="Head"?.06f:name.Contains("Shoulder")?.045f:
                name.Contains("Arm")||name.Contains("Hand")?.045f:name.Contains("Leg")||name.Contains("Foot")?.055f:.04f;
            return Mathf.Max(.001f,height*ratio);
        }

        public static BoneWeight CalculateConnectedBoneWeight(Vector3 world,Vector3[] starts,Vector3[] ends,int[] parentBoneIndices,float falloff,float[] envelopes=null,bool[] eligibility=null,bool[] branchBlendBones=null)
        {
            if(starts==null||ends==null||parentBoneIndices==null||starts.Length==0||starts.Length!=ends.Length||starts.Length!=parentBoneIndices.Length)
                throw new ArgumentException("Skin-weight segments and parent indices must be non-empty arrays of equal length.");
            for(int i=0;i<parentBoneIndices.Length;i++)
                if(parentBoneIndices[i]<-1||parentBoneIndices[i]>=parentBoneIndices.Length||parentBoneIndices[i]==i)
                    throw new ArgumentException("Each deforming bone parent index must be -1 or refer to a different valid bone.");
            if(envelopes!=null&&envelopes.Length!=starts.Length)throw new ArgumentException("Envelope radii must match the bone count.");
            if(eligibility!=null&&eligibility.Length!=starts.Length)throw new ArgumentException("Eligibility mask must match the bone count.");
            if(branchBlendBones!=null&&branchBlendBones.Length!=starts.Length)throw new ArgumentException("Branch blend mask must match the bone count.");

            var distances=new float[starts.Length];
            int nearest=-1;
            for(int i=0;i<starts.Length;i++)
            {
                if(eligibility!=null&&!eligibility[i]){distances[i]=float.PositiveInfinity;continue;}
                Vector3 segment=ends[i]-starts[i];
                float lengthSquared=segment.sqrMagnitude;
                float t=lengthSquared>0.000001f?Mathf.Clamp01(Vector3.Dot(world-starts[i],segment)/lengthSquared):0f;
                Vector3 closest=starts[i]+segment*t;
                distances[i]=Mathf.Max((world-closest).sqrMagnitude,0.000001f);
                if(envelopes!=null)distances[i]/=Mathf.Max(envelopes[i]*envelopes[i],.000001f);
                if(nearest<0||distances[i]<distances[nearest])nearest=i;
            }
            if(nearest<0)throw new InvalidOperationException("Influence bounds leave a mesh vertex outside every deforming bone at "+world);
            return WeightsFromDistances(distances,parentBoneIndices,falloff,eligibility,branchBlendBones);
        }

        private static BoneWeight WeightsFromDistances(float[] distances,int[] parentBoneIndices,float falloff,bool[] eligibility,bool[] handBones=null)
        {
            int nearest=-1;
            for(int i=0;i<distances.Length;i++)if(!float.IsInfinity(distances[i])&&(nearest<0||distances[i]<distances[nearest]))nearest=i;
            if(nearest<0)throw new InvalidOperationException("No finite bone distance was supplied.");

            int i0=-1,i1=-1,i2=-1,i3=-1;
            float d0=float.PositiveInfinity,d1=float.PositiveInfinity,d2=float.PositiveInfinity,d3=float.PositiveInfinity,d4=float.PositiveInfinity;
            for(int i=0;i<distances.Length;i++)
            {
                bool connected=i==nearest||parentBoneIndices[nearest]==i||parentBoneIndices[i]==nearest||
                    (handBones!=null&&handBones[nearest]&&handBones[i]);
                if(!connected||(eligibility!=null&&!eligibility[i]))continue;
                float distance=distances[i];
                if(distance<d0){d4=d3;d3=d2;i3=i2;d2=d1;i2=i1;d1=d0;i1=i0;d0=distance;i0=i;}
                else if(distance<d1){d4=d3;d3=d2;i3=i2;d2=d1;i2=i1;d1=distance;i1=i;}
                else if(distance<d2){d4=d3;d3=d2;i3=i2;d2=distance;i2=i;}
                else if(distance<d3){d4=d3;d3=distance;i3=i;}
                else if(distance<d4)d4=distance;
            }
            float exponent=Mathf.Clamp(falloff,0.5f,8f);
            // Thin finger surfaces need a gentler field than the torso. A fourth
            // power inverse-distance field makes submillimetre changes dominate.
            float e=(handBones!=null&&handBones[nearest]?Mathf.Min(exponent,2f):exponent)*0.5f;
            float a=Mathf.Pow(d0,-e),b=i1>=0?Mathf.Pow(d1,-e):0f,c=i2>=0?Mathf.Pow(d2,-e):0f,dWeight=i3>=0?Mathf.Pow(d3,-e):0f;
            if(handBones!=null&&handBones[nearest]&&!float.IsInfinity(d4))
            {
                // A hard top-four cutoff drops a nonzero influence when the fifth
                // bone enters. Compact support makes that boundary weight zero.
                float cutoff=1f/d4;
                float ca=Mathf.Pow(Mathf.Max(0f,1f/d0-cutoff),e),cb=i1>=0?Mathf.Pow(Mathf.Max(0f,1f/d1-cutoff),e):0f;
                float cc=i2>=0?Mathf.Pow(Mathf.Max(0f,1f/d2-cutoff),e):0f,cd=i3>=0?Mathf.Pow(Mathf.Max(0f,1f/d3-cutoff),e):0f;
                if(ca+cb+cc+cd>0f){a=ca;b=cb;c=cc;dWeight=cd;}
            }
            float total=a+b+c+dWeight;
            var w=new BoneWeight{boneIndex0=i0,weight0=a/total};
            if(i1>=0){w.boneIndex1=i1;w.weight1=b/total;}
            if(i2>=0){w.boneIndex2=i2;w.weight2=c/total;}
            if(i3>=0){w.boneIndex3=i3;w.weight3=dWeight/total;}
            return w;
        }

        private static Matrix4x4[] bindposes(List<Transform> bones, Transform rendererTransform)
        {
            var result = new Matrix4x4[bones.Count];
            for (int i=0;i<bones.Count;i++) result[i]=bones[i].worldToLocalMatrix*rendererTransform.localToWorldMatrix;
            return result;
        }

        private static void BuildGoreAnchors(RebirthEntityRigWorkspace ws)
        {
            var root = new GameObject("GoreAnchors");
            Undo.RegisterCreatedObjectUndo(root, "Create gore attachment anchors");
            Undo.SetTransformParent(root.transform, ws.generatedSkeleton, "Attach gore anchors");
            ws.goreAnchorRoot = root.transform;
            foreach (string name in ws.profile.requiredGoreAnchors)
            {
                string side = name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 ? "Right" : "Left";
                bool upper=name.IndexOf("Upper",StringComparison.OrdinalIgnoreCase)>=0;
                string boneName = name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0 ? "Head" :
                    name.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0 ? side + (upper?"Arm":"ForeArm") :
                    name.IndexOf("Leg", StringComparison.OrdinalIgnoreCase) >= 0 ? side + (upper?"UpLeg":"Leg") : "Hips";
                Transform bone=FindRelative(ws.generatedSkeleton,boneName);
                Transform anchor=FindRelative(ws.generatedSkeleton,name);
                if(anchor!=null)TryAssignTag(anchor.gameObject,name);
                else
                {
                    anchor=Child(bone!=null?bone:root.transform,name,name);
                    if(bone!=null)anchor.position=bone.position;
                }
                if(bone!=null&&anchor.parent!=bone)anchor.SetParent(bone,true);
            }
        }

        private static void BuildEyeAnchors(RebirthEntityRigWorkspace ws)
        {
            Transform head=FindRelative(ws.generatedSkeleton,"Head");
            if(head==null)throw new InvalidOperationException("Cannot create eye attachment anchors: the skeleton has no Head bone.");
            foreach(string name in ws.profile.requiredEyeAnchors)
            {
                Transform anchor=FindRelative(ws.generatedSkeleton,name);
                if(anchor==null&&ws.sourceVisual!=null)anchor=FindRelative(ws.sourceVisual,name);
                if(anchor==null)anchor=Child(head,name,"E_BP_Eye");
                else TryAssignTag(anchor.gameObject,"E_BP_Eye");
            }
        }

        public static void BuildPhysics(RebirthEntityRigWorkspace ws)
        {
            BindWorkspaceReferences(ws);
            if(ws==null||ws.profile==null)throw new ArgumentException("Workspace/profile required.");
            if(ws.colliderFitReports!=null)
                foreach(var report in ws.colliderFitReports)
                    if(report!=null&&report.collider!=null)
                    {
                        GameObject owner=report.collider.gameObject;
                        if(owner.name.StartsWith("EntityStudio_Compound_",StringComparison.Ordinal)){Undo.DestroyObjectImmediate(owner);continue;}
                        foreach(var joint in owner.GetComponents<Joint>())Undo.DestroyObjectImmediate(joint);
                        foreach(var body in owner.GetComponents<Rigidbody>())Undo.DestroyObjectImmediate(body);
                        Undo.DestroyObjectImmediate(report.collider);
                    }
            if(ws.colliderRoot!=null)Undo.DestroyObjectImmediate(ws.colliderRoot.gameObject);
            ws.colliderRoot=null;
            BuildCollidersAndRagdoll(ws);
        }

        private static void BuildCollidersAndRagdoll(RebirthEntityRigWorkspace ws)
        {
            if (!ws.profile.createHitColliders && !ws.profile.createRagdoll) return;
            var root=new GameObject("PhysicsRig");
            Undo.RegisterCreatedObjectUndo(root,"Create ragdoll collider hierarchy");
            Undo.SetTransformParent(root.transform,ws.generatedSkeleton,"Attach physics rig");
            ws.colliderRoot=root.transform;
            ws.colliderFitReports=new List<EntityColliderFitReport>();
            ws.profile.reviewedColliderFit=false;
            Bounds b=SourceBounds(ws.sourceVisual);
            // Culling bounds deliberately include animation margins; they must
            // not enlarge the anatomy reference used by collider fit checks.
            float h=ws.profile.estimatedHeight>0f?ws.profile.estimatedHeight:b.size.y, width=b.size.x, depth=b.size.z;
            var specs=new List<ColliderSpec>{
                new ColliderSpec("Hips","Hips","Spine2","Hips",""),
                new ColliderSpec("Spine2","Spine2","Neck","Spine2","Hips"),
                new ColliderSpec("Head","Head","","Head","Spine2"),
                new ColliderSpec("LeftUpperArm","LeftArm","LeftForeArm","LeftArm","Spine2"),
                new ColliderSpec("LeftLowerArm","LeftForeArm","LeftHand","LeftForeArm","LeftUpperArm"),
                new ColliderSpec("RightUpperArm","RightArm","RightForeArm","RightArm","Spine2"),
                new ColliderSpec("RightLowerArm","RightForeArm","RightHand","RightForeArm","RightUpperArm"),
                new ColliderSpec("LeftUpperLeg","LeftUpLeg","LeftLeg","LeftUpLeg","Hips"),
                new ColliderSpec("LeftLowerLeg","LeftLeg","LeftFoot","LeftLeg","LeftUpperLeg"),
                new ColliderSpec("RightUpperLeg","RightUpLeg","RightLeg","RightUpLeg","Hips"),
                new ColliderSpec("RightLowerLeg","RightLeg","RightFoot","RightLeg","RightUpperLeg")};
            if(ws.profile.includeNeckCollider)
            {
                int headIndex=specs.FindIndex(spec=>spec.name=="Head");
                specs[headIndex]=new ColliderSpec("Head","Head","","Head","Neck");
                specs.Insert(headIndex,new ColliderSpec("Neck","Neck","Head","Neck","Spine2"));
            }
            var bodiesByName=new Dictionary<string,Rigidbody>(StringComparer.OrdinalIgnoreCase);
            if(ws.profile.compoundTerminalColliders)
                foreach(string side in new[]{"Left","Right"})
                {
                    specs.Add(new ColliderSpec(side+"HandHit",side+"Hand",side+"HandMiddle4",side+"Hand",side+"LowerArm",true));
                    specs.Add(new ColliderSpec(side+"FootHit",side+"Foot",side+"ToeEnd",side+"Foot",side+"LowerLeg",true));
                }
            foreach(var spec in specs)
            {
                Transform bone=FindRelative(ws.generatedSkeleton,spec.bone);
                if(bone==null) continue;
                Transform end=string.IsNullOrEmpty(spec.endBone)?null:FindRelative(ws.generatedSkeleton,spec.endBone);
                Transform jointBone=FindRelative(ws.generatedSkeleton,spec.jointBone);
                var fit=FitColliderToMesh(ws,spec,bone,end,h,width,depth,specs);
                // Physics must move the deforming joint itself. A Rigidbody on a child
                // proxy moves only that proxy and leaves the animated mesh bone behind.
                var bodyGo=bone.gameObject;
                if(spec.compound)
                {
                    bodyGo=new GameObject("EntityStudio_Compound_"+spec.name);bodyGo.transform.SetParent(bone,false);
                    TryAssignTag(bodyGo,"E_BP_"+(spec.name.StartsWith("Left",StringComparison.Ordinal)?"L":"R")+(spec.name.Contains("Hand")?"LowerArm":"LowerLeg"));
                }
                Rigidbody body=null;
                if(ws.profile.createRagdoll&&!spec.compound)
                {
                    body=bodyGo.AddComponent<Rigidbody>();
                    float massWeight=spec.name=="Hips"||spec.name=="Spine2"?2f:spec.name=="Head"?.8f:spec.name.Contains("UpperLeg")?1f:.5f;
                    body.mass=Mathf.Max(.1f,ws.profile.ragdollMass)*massWeight/(ws.profile.includeNeckCollider?10.3f:9.8f);
                    body.drag=.25f;body.angularDrag=.05f;
                    body.isKinematic=true;
                    bodiesByName[spec.name]=body;
                }
                Collider collider;
                bool torso=spec.name=="Hips"||spec.name=="Spine2";
                bool useBox=(ws.profile.useBoxTorso&&torso)||(ws.profile.useBoxTorsoAndLegs&&(torso||spec.name.Contains("Leg")));
                if(useBox)
                {
                    var box=bodyGo.AddComponent<BoxCollider>();box.center=fit.boxCenter;box.size=fit.boxSize;collider=box;
                }
                else
                {
                var capsule=bodyGo.AddComponent<CapsuleCollider>();collider=capsule;
                Vector3 localAxis=bone.InverseTransformDirection(fit.axis);
                capsule.direction=DominantAxis(localAxis);
                float axisScale=Mathf.Abs(bone.lossyScale[capsule.direction]);
                float radialScale=Mathf.Max(Mathf.Abs(bone.lossyScale[(capsule.direction+1)%3]),Mathf.Abs(bone.lossyScale[(capsule.direction+2)%3]));
                capsule.radius=fit.radius/Mathf.Max(radialScale,.00001f);
                capsule.height=fit.height/Mathf.Max(axisScale,.00001f);
                capsule.center=bone.InverseTransformPoint(fit.center);
                }
                CharacterJoint joint=null;
                collider.sharedMaterial=ws.profile.ragdollMaterial;
                if(ws.profile.createRagdoll&&!spec.compound&&!string.IsNullOrEmpty(spec.parentBody)&&jointBone!=null&&bodiesByName.TryGetValue(spec.parentBody,out Rigidbody parentBody))
                {
                    joint=bodyGo.AddComponent<CharacterJoint>();
                    joint.connectedBody=parentBody;
                    joint.autoConfigureConnectedAnchor=false;
                    joint.anchor=bodyGo.transform.InverseTransformPoint(jointBone.position);
                    joint.connectedAnchor=parentBody.transform.InverseTransformPoint(jointBone.position);
                    joint.enablePreprocessing=false;joint.enableCollision=false;joint.enableProjection=true;
                    ConfigureJointFrame(ws,spec.name,joint);
                }
                ws.colliderFitReports.Add(new EntityColliderFitReport{colliderName=spec.name,sampledVertices=fit.samples,estimatedSurfaceCoverage=useBox&&!fit.fallback?1f:fit.coverage,
                    radius=fit.radius,baseGameReferenceRadius=spec.compound?0f:BaseGameReferenceRadius(spec.name,h),height=fit.height,usedProportionalFallback=fit.fallback,collider=collider,boxSize=useBox?fit.boxSize:Vector3.zero});
            }
            EditorUtility.SetDirty(ws);EditorUtility.SetDirty(ws.profile);
        }

        private static void ConfigureJointFrame(RebirthEntityRigWorkspace workspace,string name,CharacterJoint joint)
        {
            // Angular ranges measured on the installed 3.3.0 Worker. Their axes are
            // adapted to the generated seed's anatomical frame, not copied as Euler
            // rotations from an animated source pose. b259 verification remains open.
            bool leg=name.Contains("Leg");
            Vector3 hinge=workspace.transform.right*(leg?-1f:1f);
            joint.axis=joint.transform.InverseTransformDirection(hinge).normalized;
            joint.swingAxis=joint.transform.InverseTransformDirection(workspace.transform.forward).normalized;
            float low=-20f,high=20f,swing=10f;
            if(name.Contains("LowerArm")){low=-90f;high=0f;swing=0f;}
            else if(name.Contains("UpperArm")){low=-70f;high=10f;swing=50f;}
            else if(name.Contains("LowerLeg")){low=-80f;high=0f;swing=0f;}
            else if(name.Contains("UpperLeg")){low=-20f;high=70f;swing=30f;}
            else if(name=="Head"){low=-40f;high=25f;swing=25f;}
            joint.lowTwistLimit=new SoftJointLimit{limit=low};joint.highTwistLimit=new SoftJointLimit{limit=high};
            joint.swing1Limit=new SoftJointLimit{limit=swing};joint.swing2Limit=new SoftJointLimit{limit=0f};
        }

        private struct ColliderSpec
        {public string name,bone,endBone,jointBone,parentBody;public bool compound;public ColliderSpec(string n,string b,string e,string j,string p,bool c=false){name=n;bone=b;endBone=e;jointBone=j;parentBody=p;compound=c;}}

        private struct ColliderFit
        {public Vector3 center,axis,boxCenter,boxSize;public float radius,height,coverage;public int samples;public bool fallback;}

        public static float BaseGameReferenceRadius(string colliderName,float modelHeight)
        {
            // Installed 3.3 Worker measurements, normalized to the existing nominal
            // 1.8m comparison convention. This is an aid, not a radius clamp or a
            // universal family/b259 contract. Recognize actual native bone names;
            // LeftForeArm/LeftUpLeg must not fall through to a torso reference.
            string name=colliderName??"";
            float reference=name.IndexOf("Head",StringComparison.OrdinalIgnoreCase)>=0 ? .09001862f :
                name.IndexOf("LowerArm",StringComparison.OrdinalIgnoreCase)>=0||name.EndsWith("ForeArm",StringComparison.OrdinalIgnoreCase) ? .05f :
                name.IndexOf("UpperArm",StringComparison.OrdinalIgnoreCase)>=0||name.EndsWith("Arm",StringComparison.OrdinalIgnoreCase) ? .08f :
                name.IndexOf("UpperLeg",StringComparison.OrdinalIgnoreCase)>=0||name.EndsWith("UpLeg",StringComparison.OrdinalIgnoreCase) ? .10f :
                name.IndexOf("LowerLeg",StringComparison.OrdinalIgnoreCase)>=0||name.EndsWith("Leg",StringComparison.OrdinalIgnoreCase) ? .07f :
                name=="Hips" ? .1454227f :name=="Spine2" ? .1443624f :0f;
            return reference*Mathf.Max(modelHeight,.1f)/1.8f;
        }

        public struct CapsuleSampleFit
        {public float minAxial,maxAxial,centerAxial,radius,height,coverage;public int sampleCount;}

        private static ColliderFit FitColliderToMesh(RebirthEntityRigWorkspace ws,ColliderSpec spec,Transform start,Transform end,float modelHeight,float modelWidth,float modelDepth,IList<ColliderSpec> specs)
        {
            Vector3 axis=end!=null?end.position-start.position:Vector3.up;
            if(axis.sqrMagnitude<0.000001f)axis=Vector3.up;
            int localDirection=DominantAxis(start.InverseTransformDirection(axis));
            Vector3 localCardinal=localDirection==0?Vector3.right:localDirection==1?Vector3.up:Vector3.forward;
            axis=start.TransformDirection(localCardinal).normalized;
            var axial=new List<float>();var radial=new List<float>();
            var radialPlane=new List<Vector2>();
            Vector3 u=Vector3.Cross(axis,Mathf.Abs(Vector3.Dot(axis,Vector3.up))>.95f?Vector3.right:Vector3.up).normalized;
            Vector3 v=Vector3.Cross(axis,u).normalized;
            float diagnosticMax=-1f;Vector3 diagnosticPoint=Vector3.zero;
            Bounds localSampleBounds=new Bounds();bool hasLocalSamples=false;
            foreach(var renderer in SourceRenderers(ws.profile))
            {
                if(renderer==null||renderer.sharedMesh==null)continue;
                List<int> segmentBoneIndices=SegmentBoneIndices(renderer,start,end);
                if(ws.profile.includeTerminalPartsInColliders||ws.profile.compoundTerminalColliders)
                {
                    Transform terminal=spec.name=="Head"?start:spec.name.Contains("LowerArm")||spec.name.Contains("LowerLeg")?end:null;
                    if(terminal!=null)for(int boneIndex=0;boneIndex<renderer.bones.Length;boneIndex++)
                        if((renderer.bones[boneIndex]==terminal||renderer.bones[boneIndex].IsChildOf(terminal)||(spec.name=="Head"&&renderer.bones[boneIndex].name=="Neck"))&&!segmentBoneIndices.Contains(boneIndex))segmentBoneIndices.Add(boneIndex);
                }
                if(segmentBoneIndices.Count==0)continue;
                var groups=new List<List<int>>();int selectedGroup=-1;
                if(ws.profile.includeTerminalPartsInColliders)
                    for(int bodyIndex=0;bodyIndex<specs.Count;bodyIndex++)
                    {
                        ColliderSpec candidate=specs[bodyIndex];
                        Transform candidateStart=FindRelative(ws.generatedSkeleton,candidate.bone),candidateEnd=FindRelative(ws.generatedSkeleton,candidate.endBone);
                        var indices=SegmentBoneIndices(renderer,candidateStart,candidateEnd);
                        Transform terminal=candidate.name=="Head"||candidate.compound?candidateStart:
                            !ws.profile.compoundTerminalColliders&&(candidate.name.Contains("LowerArm")||candidate.name.Contains("LowerLeg"))?candidateEnd:null;
                        for(int boneIndex=0;boneIndex<renderer.bones.Length;boneIndex++)
                        {
                            Transform sampleBone=renderer.bones[boneIndex];if(sampleBone==null)continue;
                            bool belongs=terminal!=null&&(sampleBone==terminal||sampleBone.IsChildOf(terminal));
                            // Clavicle and neck skin is a torso transition. Pulling
                            // it into an arm/head capsule inflates radial dimensions.
                            if(candidate.name=="Spine2"&&(sampleBone.name=="LeftShoulder"||sampleBone.name=="RightShoulder"||(!ws.profile.includeNeckCollider&&sampleBone.name=="Neck")))belongs=true;
                            if(belongs&&!indices.Contains(boneIndex))indices.Add(boneIndex);
                        }
                        groups.Add(indices);if(candidate.name==spec.name)selectedGroup=bodyIndex;
                    }
                var mesh=renderer.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                int hipsGroup=-1,chestGroup=-1;float chestY=float.PositiveInfinity;
                var upperLegY=new Dictionary<int,float>();
                var roleIndices=new Dictionary<string,int>();var roleStarts=new Dictionary<int,Transform>();
                for(int role=0;role<specs.Count;role++)
                {
                    roleIndices[specs[role].name]=role;roleStarts[role]=FindRelative(ws.generatedSkeleton,specs[role].bone);
                    if(specs[role].name=="Hips")hipsGroup=role;
                    if(specs[role].name=="Spine2"){chestGroup=role;chestY=ws.transform.InverseTransformPoint(FindRelative(ws.generatedSkeleton,specs[role].bone).position).y;}
                    if(specs[role].name.Contains("UpperLeg"))upperLegY[role]=ws.transform.InverseTransformPoint(FindRelative(ws.generatedSkeleton,specs[role].bone).position).y;
                }
                for(int i=0;i<vertices.Length&&i<weights.Length;i++)
                {
                    BoneWeight weight=weights[i];
                    Vector3 sourceWorld=renderer.transform.TransformPoint(vertices[i]);
                    float influence=0f;
                    foreach(int boneIndex in segmentBoneIndices)influence+=WeightForBone(weight,boneIndex);
                    // A joint owns its outgoing segment. Include the start joint and
                    // intermediate joints, excluding the end joint's next body segment.
                    if(ws.profile.includeTerminalPartsInColliders||ws.profile.compoundTerminalColliders)
                    {
                        int owner=DominantSampleGroup(weight,groups);float y=ws.transform.InverseTransformPoint(sourceWorld).y;
                        if(owner>=0)
                        {
                            string side=specs[owner].name.StartsWith("Left",StringComparison.Ordinal)?"Left":"Right";
                            if(specs[owner].name.EndsWith("FootHit",StringComparison.Ordinal)&&y>ws.transform.InverseTransformPoint(roleStarts[owner].position).y)
                                owner=roleIndices[side+"LowerLeg"];
                            if(specs[owner].name.EndsWith("LowerLeg",StringComparison.Ordinal)&&y>ws.transform.InverseTransformPoint(roleStarts[owner].position).y)
                                owner=roleIndices[side+"UpperLeg"];
                            if(specs[owner].name.EndsWith("HandHit",StringComparison.Ordinal))
                            {
                                Transform forearm=roleStarts[roleIndices[side+"LowerArm"]],hand=roleStarts[owner];
                                if(Vector3.Dot(sourceWorld-hand.position,hand.position-forearm.position)<0)owner=roleIndices[side+"LowerArm"];
                            }
                        }
                        // Blend weights can assign crotch/torso vertices to a
                        // neighbouring body. Keep the sample, but fit it to the
                        // body on its anatomical side of the joint boundary.
                        if(hipsGroup>=0&&upperLegY.TryGetValue(owner,out float top)&&y>top)owner=hipsGroup;
                        if(owner==hipsGroup&&chestGroup>=0&&y>chestY)owner=chestGroup;
                        if(owner!=selectedGroup)continue;
                    }
                    else if(influence<(spec.name=="Neck"?.1f:.7f))continue;
                    if(spec.name=="Head"&&ws.profile.includeTerminalPartsInColliders)
                    {Transform neck=FindRelative(ws.generatedSkeleton,"Neck");if(neck!=null&&sourceWorld.y<neck.position.y-ws.profile.colliderPadding)continue;}
                    if(spec.name=="Neck"&&end!=null)
                    {float alongNeck=Vector3.Dot(sourceWorld-start.position,(end.position-start.position).normalized);if(alongNeck<-.005f||alongNeck>Vector3.Distance(start.position,end.position)+.005f)continue;}
                    Vector3 localPoint=start.InverseTransformPoint(sourceWorld);
                    if(!hasLocalSamples){localSampleBounds=new Bounds(localPoint,Vector3.zero);hasLocalSamples=true;}else localSampleBounds.Encapsulate(localPoint);
                    Vector3 offset=sourceWorld-start.position;
                    float along=Vector3.Dot(offset,axis);
                    Vector3 perpendicular=offset-axis*along;
                    axial.Add(along);radial.Add(perpendicular.magnitude);
                    radialPlane.Add(new Vector2(Vector3.Dot(perpendicular,u),Vector3.Dot(perpendicular,v)));
                    if(perpendicular.magnitude>diagnosticMax){diagnosticMax=perpendicular.magnitude;diagnosticPoint=sourceWorld;}
                }
            }

            float minAxial,maxAxial,radius,coverage,length,centerAxial;
            if(ws.profile.entityClass=="RebirthFarmer")Debug.Log("FARMER SAMPLE "+spec.name+" start="+start.position.ToString("F4")+" axis="+axis.ToString("F4")+" maxRadial="+diagnosticMax+" at="+diagnosticPoint.ToString("F4"));
            bool fallback=axial.Count<8;
            Vector3 lateralOffset=Vector3.zero;
            if(!fallback)
            {
                RadialCircle circle=MinimumRadialCircle(radialPlane);
                lateralOffset=u*circle.center.x+v*circle.center.y;
                for(int i=0;i<radial.Count;i++)radial[i]=(radialPlane[i]-circle.center).magnitude;
            }
            if(fallback)
            {
                float fallbackRadius=spec.name.Contains("Head")?Mathf.Max(Mathf.Min(modelWidth,modelDepth)*.22f,.045f):
                    spec.name.Contains("Arm")?Mathf.Max(modelDepth*.11f,.02f):Mathf.Max(modelWidth*.12f,.03f);
                float fallbackLength=spec.name.Contains("Head")?Mathf.Max(modelHeight*.13f,.13f):modelHeight*.18f;
                radius=fallbackRadius+ws.profile.colliderPadding;
                minAxial=-fallbackLength*.5f;maxAxial=fallbackLength*.5f;
                coverage=0f;
                length=Mathf.Max(maxAxial-minAxial+2f*ws.profile.colliderPadding,2f*radius);
                centerAxial=(minAxial+maxAxial)*.5f;
            }
            else
            {
                CapsuleSampleFit sampleFit=FitCapsuleSamples(axial,radial,ws.profile.colliderPadding,ws.profile.preferNarrowCapsules);
                minAxial=sampleFit.minAxial;maxAxial=sampleFit.maxAxial;radius=sampleFit.radius;
                coverage=sampleFit.coverage;length=sampleFit.height;
                centerAxial=sampleFit.centerAxial;
            }
            radius=Mathf.Max(radius,.015f);
            Vector3 boxSize=localSampleBounds.size;
            for(int coordinate=0;coordinate<3;coordinate++)boxSize[coordinate]=Mathf.Max(.01f,boxSize[coordinate]+2f*ws.profile.colliderPadding/Mathf.Max(.00001f,Mathf.Abs(start.lossyScale[coordinate])));
            return new ColliderFit{axis=axis,center=start.position+lateralOffset+axis*centerAxial,radius=radius,height=length,coverage=coverage,samples=axial.Count,fallback=fallback,boxCenter=localSampleBounds.center,boxSize=boxSize};
        }

        public struct RadialCircle { public Vector2 center;public float radius; }
        public static RadialCircle MinimumRadialCircle(IList<Vector2> input)
        {
            if(input==null||input.Count==0)throw new ArgumentException("Radial samples required.");
            var points=new List<Vector2>(input);var random=new System.Random(37);
            for(int i=points.Count-1;i>0;i--){int j=random.Next(i+1);Vector2 swap=points[i];points[i]=points[j];points[j]=swap;}
            var circle=new RadialCircle{center=points[0],radius=0f};
            for(int i=0;i<points.Count;i++)
                if(!Inside(circle,points[i]))
                {
                    circle=new RadialCircle{center=points[i],radius=0f};
                    for(int j=0;j<i;j++)
                        if(!Inside(circle,points[j]))
                        {
                            circle=Diameter(points[i],points[j]);
                            for(int k=0;k<j;k++)if(!Inside(circle,points[k]))circle=ThroughThree(points[i],points[j],points[k]);
                        }
                }
            return circle;

            bool Inside(RadialCircle c,Vector2 point){return (point-c.center).sqrMagnitude<=c.radius*c.radius+.00000001f;}
            RadialCircle Diameter(Vector2 a,Vector2 b){return new RadialCircle{center=(a+b)*.5f,radius=Vector2.Distance(a,b)*.5f};}
            RadialCircle ThroughThree(Vector2 a,Vector2 b,Vector2 c)
            {
                Vector2 ab=b-a,ac=c-a;float denominator=2f*(ab.x*ac.y-ab.y*ac.x);
                if(Mathf.Abs(denominator)<.000000001f)
                {var result=Diameter(a,b);var candidate=Diameter(a,c);if(candidate.radius>result.radius)result=candidate;candidate=Diameter(b,c);if(candidate.radius>result.radius)result=candidate;return result;}
                Vector2 offset=new Vector2((ac.y*ab.sqrMagnitude-ab.y*ac.sqrMagnitude)/denominator,(ab.x*ac.sqrMagnitude-ac.x*ab.sqrMagnitude)/denominator);
                return new RadialCircle{center=a+offset,radius=offset.magnitude};
            }
        }

        public static int DominantSampleGroup(BoneWeight weight,IList<List<int>> groups)
        {
            int owner=-1;float greatest=0f;
            for(int group=0;group<groups.Count;group++)
            {
                float sum=0f;foreach(int bone in groups[group])sum+=WeightForBone(weight,bone);
                // Stable first-group tie break gives every weighted sample one owner.
                if(sum>greatest){greatest=sum;owner=group;}
            }
            return owner;
        }

        private static List<int> SegmentBoneIndices(SkinnedMeshRenderer renderer,Transform start,Transform end)
        {
            var result=new List<int>();
            if(renderer==null||renderer.bones==null)return result;
            if(end==null)
            {
                int startOnly=Array.IndexOf(renderer.bones,start);
                if(startOnly>=0)result.Add(startOnly);
                return result;
            }

            Transform cursor=end.parent;
            while(cursor!=null)
            {
                int index=Array.IndexOf(renderer.bones,cursor);
                if(index>=0)result.Add(index);
                if(cursor==start)break;
                cursor=cursor.parent;
            }
            if(cursor==start&&result.Count>0)return result;

            // Profiles may define a collider between bones in separate helper branches.
            // Keep their explicitly named endpoint influences as a fallback in that case.
            result.Clear();
            int startIndex=Array.IndexOf(renderer.bones,start);
            int endIndex=Array.IndexOf(renderer.bones,end);
            if(startIndex>=0)result.Add(startIndex);
            if(endIndex>=0&&endIndex!=startIndex)result.Add(endIndex);
            return result;
        }

        private static int DominantAxis(Vector3 value)
        {value=new Vector3(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));return value.x>value.y&&value.x>value.z?0:value.z>value.y?2:1;}

        public static CapsuleSampleFit FitCapsuleSamples(IList<float> axial,IList<float> radial,float padding,bool preferNarrow=false)
        {
            if(axial==null||radial==null||axial.Count!=radial.Count||axial.Count<8)
                throw new ArgumentException("Capsule fit requires at least eight paired axial/radial mesh samples.");
            if(float.IsNaN(padding)||float.IsInfinity(padding)||padding<0f)
                throw new ArgumentOutOfRangeException("padding");
            float minAxial=float.PositiveInfinity,maxAxial=float.NegativeInfinity,maxRadial=0f;
            for(int i=0;i<axial.Count;i++)
            {
                if(float.IsNaN(axial[i])||float.IsInfinity(axial[i])||float.IsNaN(radial[i])||float.IsInfinity(radial[i])||radial[i]<0f)
                    throw new ArgumentException("Capsule fit samples must be finite and radial distances non-negative.");
                minAxial=Mathf.Min(minAxial,axial[i]);maxAxial=Mathf.Max(maxAxial,axial[i]);maxRadial=Mathf.Max(maxRadial,radial[i]);
            }
            float centerAlong=(minAxial+maxAxial)*.5f;
            float minimumRadius=Mathf.Max(maxRadial+padding,.015f);
            float maximumRadius=minimumRadius;
            for(int i=0;i<axial.Count;i++)
            {
                float axialOffset=axial[i]-centerAlong;
                maximumRadius=Mathf.Max(maximumRadius,Mathf.Sqrt(axialOffset*axialOffset+radial[i]*radial[i])+padding);
            }

            // Search the radius/segment-length trade-off and select the smallest capsule volume
            // that contains every assigned surface sample. Independent axial/radial percentiles
            // can create a large capsule that still misses many samples near rounded ends.
            float bestVolume=float.PositiveInfinity,bestRadius=minimumRadius,bestHalfSegment=0f,bestCenter=centerAlong;
            const int radiusSteps=128;
            for(int step=0;step<=(preferNarrow?0:radiusSteps);step++)
            {
                float radius=Mathf.Lerp(minimumRadius,maximumRadius,(float)step/radiusSteps);
                float lowerCenterLimit=float.PositiveInfinity,upperCenterLimit=float.NegativeInfinity;
                for(int i=0;i<axial.Count;i++)
                {
                    float radialPart=Mathf.Sqrt(Mathf.Max(0f,radius*radius-radial[i]*radial[i]));
                    lowerCenterLimit=Mathf.Min(lowerCenterLimit,axial[i]+radialPart);
                    upperCenterLimit=Mathf.Max(upperCenterLimit,axial[i]-radialPart);
                }
                float candidateCenter=(lowerCenterLimit+upperCenterLimit)*.5f;
                float halfSegment=Mathf.Max(0f,(upperCenterLimit-lowerCenterLimit)*.5f);
                float cylinderLength=2f*halfSegment;
                float volume=Mathf.PI*radius*radius*cylinderLength+(4f/3f)*Mathf.PI*radius*radius*radius;
                if(volume<bestVolume){bestVolume=volume;bestRadius=radius;bestHalfSegment=halfSegment;bestCenter=candidateCenter;}
            }
            float radiusFit=bestRadius;
            float height=2f*(radiusFit+bestHalfSegment);
            int inside=0;
            for(int i=0;i<axial.Count;i++)
                if(IsInsideCapsule(axial[i]-bestCenter,radial[i],radiusFit,height))inside++;
            return new CapsuleSampleFit{minAxial=minAxial,maxAxial=maxAxial,centerAxial=bestCenter,radius=radiusFit,height=height,coverage=(float)inside/axial.Count,sampleCount=axial.Count};
        }

        public static bool IsInsideCapsule(float axialOffset,float radialDistance,float radius,float height)
        {
            float halfCylinder=Mathf.Max(0f,height*.5f-radius);
            float capOffset=Mathf.Max(0f,Mathf.Abs(axialOffset)-halfCylinder);
            return capOffset*capOffset+radialDistance*radialDistance<=radius*radius+0.000001f;
        }

        private static float WeightForBone(BoneWeight weight,int boneIndex)
        {
            if(boneIndex<0)return 0f;
            return (weight.boneIndex0==boneIndex?weight.weight0:0f)+(weight.boneIndex1==boneIndex?weight.weight1:0f)+
                (weight.boneIndex2==boneIndex?weight.weight2:0f)+(weight.boneIndex3==boneIndex?weight.weight3:0f);
        }

        public static void RebuildLods(RebirthEntityRigWorkspace ws)
        {
            BindWorkspaceReferences(ws);
            ws.lodRendererMappings=new List<EntityLodRendererMapping>();
            if(ws==null||ws.profile==null)throw new ArgumentException("Workspace/profile required.");
            var sourceRenderers=SourceRenderers(ws.profile);
            if(sourceRenderers.Count==0)return;
            foreach(var renderer in sourceRenderers)
                if(renderer.sharedMesh==null)throw new InvalidOperationException("Cannot generate LODs: a generated skinned mesh is missing.");

            string previousLodId=ws.generatedAssetId;
            var preservedLodZeroMeshes=new Dictionary<SkinnedMeshRenderer,Mesh>();
            if(ws.lodGroup!=null)
            {
                string previousFolder=GeneratedLodAssetFolder(ws.profile.entityClass,previousLodId);
                foreach(var renderer in sourceRenderers)
                {
                    if(renderer==null||renderer.sharedMesh==null)continue;
                    string meshPath=AssetDatabase.GetAssetPath(renderer.sharedMesh);
                    if(!string.IsNullOrEmpty(previousFolder)&&meshPath.StartsWith(previousFolder+"/",StringComparison.Ordinal))
                        preservedLodZeroMeshes[renderer]=UnityEngine.Object.Instantiate(renderer.sharedMesh);
                }
                LODGenerator.DestroyLODs(ws.generatedSkeleton.gameObject);
                ws.lodGroup=null;
            }
            if(!string.IsNullOrEmpty(previousLodId))
            {
                foreach(var preserved in preservedLodZeroMeshes)preserved.Key.sharedMesh=preserved.Value;
                DeleteGeneratedLodAssets(ws.profile.entityClass,previousLodId);
            }
            for(int i=1;i<=3;i++)
            {
                var legacy=ws.generatedSkeleton.Find("GeneratedLOD"+i);
                if(legacy!=null)Undo.DestroyObjectImmediate(legacy.gameObject);
            }

            float[] heights={ws.profile.lod0TransitionHeight,ws.profile.lod1TransitionHeight,ws.profile.lod2TransitionHeight,ws.profile.lod3TransitionHeight};
            float[] qualities={1f,ws.profile.lod1Ratio,ws.profile.lod2Ratio,ws.profile.lod3Ratio};
            for(int i=0;i<heights.Length;i++)
            {
                if(heights[i]<=0f||heights[i]>=1f||(i>0&&heights[i]>=heights[i-1]))
                    throw new InvalidOperationException("LOD screen transition heights must be between 0 and 1 and descend from LOD0 to the last LOD.");
                if(qualities[i]<=0f||qualities[i]>1f||(i>0&&qualities[i]>=qualities[i-1]))
                    throw new InvalidOperationException("LOD simplification quality must be between 0 and 1 and descend for each generated level.");
            }

            var options=SimplificationOptions.Default;
            options.PreserveBorderEdges=true;
            options.PreserveUVSeamEdges=true;
            options.PreserveUVFoldoverEdges=true;
            options.PreserveSurfaceCurvature=true;
            // Coincident vertices can belong to separately capped arm/body parts.
            // Linking them would reconnect the cut and mix their skin ownership.
            options.EnableSmartLink=false;

            string safeName=SanitizeAssetName(ws.profile.entityClass);
            string newGeneratedAssetId=Guid.NewGuid().ToString("N");
            string assetFolder="EntityStudioGenerated/LODs/"+safeName+"_"+newGeneratedAssetId;
            EnsureFolder("Assets/"+assetFolder);
            GameObject lodParentObject=null;
            LODGroup group=null;
            try
            {
            // Build the LOD hierarchy ourselves. LODGenerator's Editor path always
            // CreateAsset + SaveAndReimport's each simplified mesh, even when its
            // save path argument is null. Direct MeshSimplifier use keeps the output
            // in memory until the stable .asset has been created exactly once.
            lodParentObject=new GameObject(LODGenerator.LODParentGameObjectName);
            Undo.RegisterCreatedObjectUndo(lodParentObject,"Generate entity LOD hierarchy");
            var lodParent=lodParentObject.transform;
            lodParent.SetParent(ws.generatedSkeleton,false);
            lodParent.localPosition=Vector3.zero;
            lodParent.localRotation=Quaternion.identity;
            lodParent.localScale=Vector3.one;
            var lods=new LOD[heights.Length];
            var lod0Renderers=new List<Renderer>(sourceRenderers.Count);
            foreach(var source in sourceRenderers)lod0Renderers.Add(source);
            lods[0]=new LOD(heights[0],lod0Renderers.ToArray());
            for(int levelIndex=1;levelIndex<heights.Length;levelIndex++)
            {
                var levelObject=new GameObject(string.Format("Level{0:00}",levelIndex));
                Undo.RegisterCreatedObjectUndo(levelObject,"Generate entity LOD level");
                var levelTransform=levelObject.transform;
                levelTransform.SetParent(lodParent,false);
                levelTransform.localPosition=Vector3.zero;
                levelTransform.localRotation=Quaternion.identity;
                levelTransform.localScale=Vector3.one;
                var levelRenderers=new List<Renderer>(sourceRenderers.Count);
                for(int rendererIndex=0;rendererIndex<sourceRenderers.Count;rendererIndex++)
                {
                    var source=sourceRenderers[rendererIndex];
                    var sourceMesh=source.sharedMesh;
                    var simplifier=new MeshSimplifier(sourceMesh);
                    simplifier.SimplificationOptions=options;
                    simplifier.SimplifyMesh(qualities[levelIndex]);
                    Mesh simplified=simplifier.ToMesh();
                    int[] boneGroups=null;
                    if(ws.profile.separateFusedArms)
                    {
                        boneGroups=new int[source.bones.Length];
                        for(int b=0;b<boneGroups.Length;b++)
                        {string name=source.bones[b].name;bool arm=name.Contains("Arm")||name.Contains("Hand")||name.Contains("Shoulder");boneGroups[b]=arm?(name.StartsWith("Left",StringComparison.Ordinal)?1:2):0;}
                    }
                    RebirthEntityLodSurface.Project(sourceMesh,simplified,boneGroups);
                    simplified.bindposes=sourceMesh.bindposes;
                    simplified.name=string.Format("{0}_LOD{1}",sourceMesh.name,levelIndex);
                    string meshPath=AssetDatabase.GenerateUniqueAssetPath("Assets/"+assetFolder+"/"+SanitizeAssetName(simplified.name)+".asset");
                    AssetDatabase.CreateAsset(simplified,meshPath);

                    var rendererObject=new GameObject(string.Format("{0:000}_skinned_{1}",rendererIndex,source.name),typeof(SkinnedMeshRenderer));
                    Undo.RegisterCreatedObjectUndo(rendererObject,"Generate simplified entity LOD");
                    var rendererTransform=rendererObject.transform;
                    rendererTransform.position=source.transform.position;
                    rendererTransform.rotation=source.transform.rotation;
                    rendererTransform.localScale=source.transform.lossyScale;
                    rendererTransform.SetParent(levelTransform,true);
                    var renderer=rendererObject.GetComponent<SkinnedMeshRenderer>();
                    ws.lodRendererMappings.Add(new EntityLodRendererMapping{source=source,renderer=renderer});
                    renderer.sharedMesh=simplified;
                    renderer.sharedMaterials=source.sharedMaterials;
                    renderer.rootBone=source.rootBone;
                    renderer.bones=source.bones;
                    renderer.localBounds=source.localBounds;
                    renderer.quality=source.quality;
                    renderer.updateWhenOffscreen=source.updateWhenOffscreen;
                    renderer.skinnedMotionVectors=source.skinnedMotionVectors;
                    renderer.shadowCastingMode=source.shadowCastingMode;
                    renderer.receiveShadows=source.receiveShadows;
                    renderer.lightProbeUsage=source.lightProbeUsage;
                    renderer.reflectionProbeUsage=source.reflectionProbeUsage;
                    renderer.probeAnchor=source.probeAnchor;
                    renderer.motionVectorGenerationMode=source.motionVectorGenerationMode;
                    renderer.allowOcclusionWhenDynamic=source.allowOcclusionWhenDynamic;
                    renderer.renderingLayerMask=source.renderingLayerMask;
                    renderer.sortingLayerID=source.sortingLayerID;
                    renderer.sortingOrder=source.sortingOrder;
                    renderer.enabled=true;
                    rendererObject.layer=source.gameObject.layer;
                    TryAssignTag(rendererObject,"LOD");
                    levelRenderers.Add(renderer);
                }
                lods[levelIndex]=new LOD(heights[levelIndex],levelRenderers.ToArray());
            }
            group=ws.generatedSkeleton.gameObject.GetComponent<LODGroup>();
            if(group==null)group=Undo.AddComponent<LODGroup>(ws.generatedSkeleton.gameObject);
            group.animateCrossFading=false;
            group.SetLODs(lods);
            foreach(var source in sourceRenderers)source.enabled=true;
            ws.lodGroup=group;
            foreach(var set in ws.rendererTierMaterials)
            {
                set.lodRenderers=new List<SkinnedMeshRenderer>();
                foreach(var mapping in ws.lodRendererMappings)if(mapping.source==set.renderer)set.lodRenderers.Add(mapping.renderer);
            }
            ws.generatedAssetId=newGeneratedAssetId;
            group.RecalculateBounds();
            Undo.RecordObject(ws.profile,"Invalidate LOD review after regeneration");
            ws.profile.reviewedLods=false;
            EditorUtility.SetDirty(ws.profile);
            if(ws.profile.generatedTierMaterials!=null&&ws.profile.generatedTierMaterials.Count>0)
                ApplyTierMaterials(ws,ws.activeTier);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.Default);
            }
            catch
            {
                if(group!=null)Undo.DestroyObjectImmediate(group);
                if(lodParentObject!=null)Undo.DestroyObjectImmediate(lodParentObject);
                foreach(var source in sourceRenderers)if(source!=null)source.enabled=true;
                ws.lodGroup=null;
                ws.generatedAssetId=null;
                DeleteGeneratedLodAssets(ws.profile.entityClass,newGeneratedAssetId);
                throw;
            }
        }

        private static void DeleteGeneratedLodAssets(string entityClass,string generatedAssetId)
        {
            string folder=GeneratedLodAssetFolder(entityClass,generatedAssetId);
            if(!string.IsNullOrEmpty(folder)&&AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);
        }

        private static string GeneratedLodAssetFolder(string entityClass,string generatedAssetId)
        {
            if(string.IsNullOrEmpty(entityClass)||string.IsNullOrEmpty(generatedAssetId))return null;
            string prefix="Assets/EntityStudioGenerated/LODs/";
            string folder=prefix+SanitizeAssetName(entityClass)+"_"+generatedAssetId;
            return folder.StartsWith(prefix,StringComparison.Ordinal)?folder:null;
        }

        private static string SanitizeAssetName(string value)
        {if(string.IsNullOrWhiteSpace(value))return "Entity";foreach(char c in System.IO.Path.GetInvalidFileNameChars())value=value.Replace(c,'_');return value.Replace('/','_').Replace('\\','_');}

        public static void RebuildTierMaterials(RebirthEntityRigWorkspace ws)
        {
            BindWorkspaceReferences(ws);
            if(ws==null||ws.profile==null)throw new ArgumentException("Workspace/profile required.");
            var profile=ws.profile;
            var sourceRenderers=SourceRenderers(profile);if(sourceRenderers.Count==0)return;
            string folder="Assets/EntityMaterials/"+SanitizeAssetName(profile.entityClass);
            EnsureFolder(folder);
            Color[] tints={Color.white,profile.feralTint,profile.radiatedTint,profile.chargedTint,profile.infernalTint};
            string[] names={"Normal","Feral","Radiated","Charged","Infernal"};
            var oldGeneratedMaterials=profile.generatedTierMaterials;
            var oldRendererSets=profile.rendererTierMaterials;
            int oldSlotCount=profile.generatedMaterialSlotCount;
            bool oldReviewedMaterials=profile.reviewedMaterials;
            ZombieTier oldActiveTier=ws.activeTier;
            var oldAssignments=new Dictionary<SkinnedMeshRenderer,Material[]>();
            var affectedRenderers=new HashSet<SkinnedMeshRenderer>(sourceRenderers);
            if(ws.lodGroup!=null)
                foreach(var lod in ws.lodGroup.GetLODs())
                    foreach(var renderer in lod.renderers)
                        if(renderer is SkinnedMeshRenderer skinned)affectedRenderers.Add(skinned);
            foreach(var renderer in affectedRenderers)if(renderer!=null)oldAssignments[renderer]=(Material[])renderer.sharedMaterials.Clone();
            var originalMaterialsByRenderer=new Dictionary<SkinnedMeshRenderer,Material[]>();
            foreach(var renderer in sourceRenderers)originalMaterialsByRenderer[renderer]=GetOriginalMaterials(profile,renderer);
            var newGeneratedMaterials=new List<Material>();
            var newRendererSets=new List<EntityRendererTierMaterials>();
            var createdMaterials=new List<Material>();
            var createdPaths=new List<string>();
            int newSlotCount=0;
            Undo.RecordObject(profile,"Generate entity tier materials");
            try
            {
                for(int rendererIndex=0;rendererIndex<sourceRenderers.Count;rendererIndex++)
                {
                    var smr=sourceRenderers[rendererIndex];
                    Material[] sourceMaterials=originalMaterialsByRenderer[smr];
                    int slotCount=Mathf.Max(1,sourceMaterials.Length);
                    var set=new EntityRendererTierMaterials{renderer=smr,slotCount=slotCount,sourceMaterials=new List<Material>(sourceMaterials)};
                    foreach(var mapping in ws.lodRendererMappings)if(mapping.source==smr)set.lodRenderers.Add(mapping.renderer);
                    newSlotCount+=slotCount;
                    for(int tier=0;tier<names.Length;tier++)
                    {
                        for(int slot=0;slot<slotCount;slot++)
                        {
                            Material source=sourceMaterials.Length==0?null:sourceMaterials[slot];
                            var textureOverride=RebirthEntityMaterialMaps.FindOverride(profile,source);
                            Texture2D slotAlbedo=textureOverride!=null?textureOverride.albedo:profile.bakedAlbedo;
                            Texture2D slotNormal=textureOverride!=null?textureOverride.normalMap:profile.normalMap;
                            Shader shader=profile.characterShader!=null?profile.characterShader:source!=null?source.shader:Shader.Find("Standard");
                            if(shader==null)throw new InvalidOperationException("No character shader assigned and Standard shader is unavailable.");
                            var material=source!=null?new Material(source):new Material(shader);
                            createdMaterials.Add(material);
                            if(profile.characterShader!=null)material.shader=profile.characterShader;
                            string rendererKey=rendererIndex.ToString("D3",CultureInfo.InvariantCulture)+"_"+SanitizeAssetName(smr.gameObject.name);
                            material.name=profile.entityClass+"_"+rendererKey+"_"+names[tier]+"_Slot"+slot;
                            if(slotAlbedo!=null)
                            {
                                if(!SetTexture(material,new[]{"_Albedo","_MainTex","_BaseMap"},slotAlbedo))
                                    throw new InvalidOperationException("Shader '"+material.shader.name+"' has no supported albedo texture property (_Albedo, _MainTex, or _BaseMap).");
                            }
                            else if(source!=null)CopyTexture(source,material,new[]{"_Albedo","_MainTex","_BaseMap"});
                            if(slotNormal!=null)
                            {
                                if(!SetTexture(material,new[]{"_Normal","_BumpMap","_NormalMap"},slotNormal))
                                    throw new InvalidOperationException("Shader '"+material.shader.name+"' has no supported normal texture property (_Normal, _BumpMap, or _NormalMap).");
                                if(material.shader.name=="Standard")material.EnableKeyword("_BUMPMAP");
                            }
                            else if(source!=null)CopyTexture(source,material,new[]{"_Normal","_BumpMap","_NormalMap"});
                            if(profile.rmoeMap!=null)
                            {
                                if(!SetTexture(material,new[]{"_RMOE","_MaskMap"},profile.rmoeMap))
                                    throw new InvalidOperationException("Shader '"+material.shader.name+"' has no supported RMOE/mask texture property (_RMOE or _MaskMap).");
                            }
                            else if(source!=null)CopyTexture(source,material,new[]{"_RMOE","_MaskMap"});
                            if(source!=null)
                            {
                                CopyTexture(source,material,new[]{"_EmissionMap","_EmissiveMap"});
                                CopyColor(source,material,new[]{"_EmissionColor"});
                            }
                            if(tier>0)
                            {
                                Color baseColor=GetColor(material,new[]{"_Color","_BaseColor"},Color.white);
                                Color matColor=tints[tier];
                                SetColor(material,new[]{"_Color","_BaseColor"},new Color(baseColor.r*matColor.r,baseColor.g*matColor.g,baseColor.b*matColor.b,baseColor.a));
                            }
                            if(tier>=2&&material.HasProperty("_EmissionColor"))
                            {
                                Color matColor=tints[tier];
                                Color emissionRgb=new Color(matColor.r,matColor.g,matColor.b,1f);
                                float emissionMultiplier=(1f+Mathf.Max(0f,matColor.a))*profile.tierEmission;
                                material.SetColor("_EmissionColor",emissionRgb*emissionMultiplier);
                                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                                material.EnableKeyword("_EMISSION");
                            }
                            string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+rendererKey+"_"+names[tier]+"_Slot"+slot+".mat");
                            AssetDatabase.CreateAsset(material,path);
                            createdPaths.Add(path);
                            newGeneratedMaterials.Add(material);
                            set.variants.Add(material);
                        }
                    }
                    newRendererSets.Add(set);
                }

                profile.generatedTierMaterials=newGeneratedMaterials;
                profile.rendererTierMaterials=newRendererSets;
                ws.rendererTierMaterials=newRendererSets;
                profile.generatedMaterialSlotCount=newSlotCount;
                profile.reviewedMaterials=false;
                EditorUtility.SetDirty(profile);
                ApplyTierMaterials(ws,ZombieTier.Normal);
                AssetDatabase.SaveAssets();
            }
            catch
            {
                profile.generatedTierMaterials=oldGeneratedMaterials;
                profile.rendererTierMaterials=oldRendererSets;
                ws.rendererTierMaterials=oldRendererSets;
                profile.generatedMaterialSlotCount=oldSlotCount;
                profile.reviewedMaterials=oldReviewedMaterials;
                foreach(var assignment in oldAssignments)if(assignment.Key!=null)assignment.Key.sharedMaterials=assignment.Value;
                ws.activeTier=oldActiveTier;
                foreach(string path in createdPaths)if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)AssetDatabase.DeleteAsset(path);
                foreach(Material material in createdMaterials)
                    if(material!=null&&string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))UnityEngine.Object.DestroyImmediate(material);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                throw;
            }

            if(oldGeneratedMaterials!=null)
            {
                foreach(Material previous in oldGeneratedMaterials)
                {
                    if(previous==null)continue;
                    string previousPath=AssetDatabase.GetAssetPath(previous);
                    if(!string.IsNullOrEmpty(previousPath)&&previousPath.StartsWith(folder+"/",StringComparison.Ordinal)&&!createdPaths.Contains(previousPath))
                        AssetDatabase.DeleteAsset(previousPath);
                }
            }
            AssetDatabase.SaveAssets();
        }

        public static void ApplyTierMaterials(RebirthEntityRigWorkspace ws,ZombieTier tier)
        {
            BindWorkspaceReferences(ws);
            if(ws==null||ws.profile==null)throw new ArgumentException("Workspace/profile required.");
            if(ws.profile.rendererTierMaterials==null||ws.profile.rendererTierMaterials.Count==0)
                throw new InvalidOperationException("Generate a complete five-tier material set for every source renderer before applying a tier.");
            var renderers=new HashSet<SkinnedMeshRenderer>(SourceRenderers(ws.profile));
            if(ws.lodGroup!=null)
                foreach(var lod in ws.lodGroup.GetLODs())
                    foreach(var renderer in lod.renderers)
                        if(renderer is SkinnedMeshRenderer skinned)renderers.Add(skinned);
            foreach(var renderer in renderers)
            {
                if(renderer==null)continue;
                var set=FindTierSet(ws.profile,renderer);
                if(set==null)throw new InvalidOperationException("Cannot map renderer '"+renderer.name+"' to its source material slots. Regenerate the LODs and material sets before previewing a tier.");
                int start=(int)tier*set.slotCount;
                var selected=new Material[set.slotCount];
                for(int i=0;i<selected.Length;i++)selected[i]=set.variants[start+i];
                Undo.RecordObject(renderer,"Apply "+tier+" entity materials");
                renderer.sharedMaterials=(Material[])selected.Clone();
                if(renderer.CompareTag("E_BP_Eye"))renderer.gameObject.SetActive(renderer.name=="FeralEye"?tier==ZombieTier.Feral:tier!=ZombieTier.Feral);
                EditorUtility.SetDirty(renderer);
            }
            ws.activeTier=tier;
            EditorUtility.SetDirty(ws);
        }

        private static List<SkinnedMeshRenderer> SourceRenderers(RebirthEntityRigProfile profile)
        {
            var result=new List<SkinnedMeshRenderer>();
            if(profile.sourceSkinnedMeshes!=null)foreach(var renderer in profile.sourceSkinnedMeshes)if(renderer!=null&&!result.Contains(renderer))result.Add(renderer);
            if(result.Count==0&&profile.sourceSkinnedMesh!=null)result.Add(profile.sourceSkinnedMesh);
            return result;
        }

        private static Material[] GetOriginalMaterials(RebirthEntityRigProfile profile,SkinnedMeshRenderer renderer)
        {
            if(profile.rendererTierMaterials!=null)
                foreach(var set in profile.rendererTierMaterials)if(set!=null&&set.renderer==renderer&&set.sourceMaterials!=null)return set.sourceMaterials.ToArray();
            return renderer.sharedMaterials??new Material[0];
        }

        private static EntityRendererTierMaterials FindTierSet(RebirthEntityRigProfile profile,SkinnedMeshRenderer renderer)
        {
            foreach(var set in profile.rendererTierMaterials)
            {
                if(set==null||set.renderer==null||set.slotCount<=0||set.variants==null||set.variants.Count!=set.slotCount*5)continue;
                if(set.renderer==renderer)return set;
                if(set.lodRenderers!=null&&set.lodRenderers.Contains(renderer))return set;
                Material[] current=renderer.sharedMaterials??new Material[0];
                if(MaterialArrayMatches(current,set.sourceMaterials))return set;
                for(int tier=0;tier<5;tier++)
                    if(MaterialArrayMatches(current,set.variants.GetRange(tier*set.slotCount,set.slotCount).ToArray()))return set;
                if(renderer.name==set.renderer.name||renderer.name.StartsWith(set.renderer.name+"_",StringComparison.Ordinal))return set;
            }
            return null;
        }

        private static bool MaterialArrayMatches(Material[] left,IList<Material> right)
        {
            if(right==null)return false;
            if(left.Length==0&&right.Count==0)return true;
            if(left.Length!=right.Count)return false;
            for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false;
            return true;
        }

        private static bool SetTexture(Material m,string[] props,Texture2D t){foreach(string p in props)if(m.HasProperty(p)){m.SetTexture(p,t);return true;}return false;}
        private static void CopyTexture(Material from,Material to,string[] props){foreach(string p in props)if(from.HasProperty(p)&&to.HasProperty(p)){to.SetTexture(p,from.GetTexture(p));return;}}
        private static void CopyColor(Material from,Material to,string[] props){foreach(string p in props)if(from.HasProperty(p)&&to.HasProperty(p)){to.SetColor(p,from.GetColor(p));return;}}
        private static Color GetColor(Material material,string[] props,Color fallback){foreach(string p in props)if(material.HasProperty(p))return material.GetColor(p);return fallback;}
        private static void SetColor(Material m,string[] props,Color c){foreach(string p in props)if(m.HasProperty(p)){m.SetColor(p,c);return;}}

        private static Transform Child(Transform parent,string name,string tag)
        {var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create entity rig node "+name);Undo.SetTransformParent(go.transform,parent,"Parent entity rig node");go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;TryAssignTag(go,tag);return go.transform;}

        private static void TryAssignTag(GameObject go,string tag)
        {try{go.tag=string.IsNullOrEmpty(tag)?"Untagged":tag;}catch{go.tag="Untagged";Debug.LogWarning("Unity tag not defined in project TagManager: "+tag);}}

        private static void EnsureRequiredGameTags()
        {
            UnityEngine.Object[] assets=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if(assets==null||assets.Length==0)throw new InvalidOperationException("Unity TagManager.asset is unavailable; required 7DTD tags cannot be prepared.");
            var manager=new SerializedObject(assets[0]);
            var tags=manager.FindProperty("tags");
            if(tags==null||!tags.isArray)throw new InvalidOperationException("Unity TagManager has no editable tag list.");
            bool changed=false;
            foreach(string required in RequiredGameTags)
            {
                bool found=false;
                for(int i=0;i<tags.arraySize;i++)
                    if(string.Equals(tags.GetArrayElementAtIndex(i).stringValue,required,StringComparison.Ordinal)){found=true;break;}
                if(found)continue;
                int index=tags.arraySize;
                tags.InsertArrayElementAtIndex(index);
                tags.GetArrayElementAtIndex(index).stringValue=required;
                changed=true;
            }
            if(changed){manager.ApplyModifiedProperties();AssetDatabase.SaveAssets();}
        }

        public static Transform FindBone(Transform root,string name)
        {
            if(root==null||string.IsNullOrEmpty(name))return null;
            Transform sourceVisual=root.Find("SourceVisual_Unrigged");
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(sourceVisual!=null&&(t==sourceVisual||t.IsChildOf(sourceVisual)))continue;
                if(string.Equals(t.name,name,StringComparison.OrdinalIgnoreCase))return t;
            }
            return null;
        }

        private static Transform FindRelative(Transform root,string name){return FindBone(root,name);}

        public static Bounds SourceBoundsInRootSpace(Transform root)
        {
            if(root==null)throw new ArgumentNullException("root");
            bool sourceOnly=root.name=="SourceVisual_Unrigged";
            var filters=root.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length==0)throw new InvalidOperationException("No static mesh filters found for source bounds.");
            bool initialized=false;
            Bounds bounds=new Bounds();
            Matrix4x4 worldToRoot=root.worldToLocalMatrix;
            foreach(var filter in filters)
            {
                if(filter.sharedMesh==null||(sourceOnly&&!filter.transform.IsChildOf(root)))continue;
                Bounds meshBounds=filter.sharedMesh.bounds;
                Matrix4x4 toRoot=worldToRoot*filter.transform.localToWorldMatrix;
                for(int mask=0;mask<8;mask++)
                {
                    Vector3 corner=new Vector3((mask&1)==0?meshBounds.min.x:meshBounds.max.x,
                        (mask&2)==0?meshBounds.min.y:meshBounds.max.y,(mask&4)==0?meshBounds.min.z:meshBounds.max.z);
                    Vector3 point=toRoot.MultiplyPoint3x4(corner);
                    if(!initialized){bounds=new Bounds(point,Vector3.zero);initialized=true;}
                    else bounds.Encapsulate(point);
                }
            }
            if(!initialized)throw new InvalidOperationException("Source model has no readable mesh bounds.");
            return bounds;
        }

        public static Vector3 SourcePositionToGame(Vector3 sourcePosition,EntityModelUpAxis sourceUpAxis)
        {return SourceToGameRotation(sourceUpAxis)*sourcePosition;}

        public static Vector3 BonePositionInSourceBounds(Bounds bounds,Vector3 normalized,EntityModelUpAxis sourceUpAxis)
        {return BonePositionInBounds(bounds,sourceUpAxis==EntityModelUpAxis.Z?new Vector3(normalized.x,normalized.z,normalized.y):normalized);}

        public static Vector3 NormalizeBonePositionInSourceBounds(Bounds bounds,Vector3 sourcePosition,EntityModelUpAxis sourceUpAxis)
        {
            Vector3 normalized=NormalizeBonePositionInBounds(bounds,sourcePosition);
            return sourceUpAxis==EntityModelUpAxis.Z?new Vector3(normalized.x,normalized.z,normalized.y):normalized;
        }

        public static Vector3 SourcePointToGameAxes(Vector3 point,EntityModelUpAxis sourceUpAxis)
        {return sourceUpAxis==EntityModelUpAxis.Z?new Vector3(point.x,point.z,point.y):point;}

        public static Vector3 SourceSizeToGameAxes(Vector3 size,EntityModelUpAxis sourceUpAxis)
        {return sourceUpAxis==EntityModelUpAxis.Z?new Vector3(size.x,size.z,size.y):size;}

        public static Quaternion SourceToGameRotation(EntityModelUpAxis sourceUpAxis)
        {return sourceUpAxis==EntityModelUpAxis.Z?Quaternion.FromToRotation(Vector3.forward,Vector3.up):Quaternion.identity;}

        public static Vector3 BonePositionInBounds(Bounds bounds,Vector3 normalized)
        {
            return new Vector3(Mathf.LerpUnclamped(bounds.min.x,bounds.max.x,normalized.x),
                Mathf.LerpUnclamped(bounds.min.y,bounds.max.y,normalized.y),
                Mathf.LerpUnclamped(bounds.min.z,bounds.max.z,normalized.z));
        }

        public static Vector3 NormalizeBonePositionInBounds(Bounds bounds,Vector3 position)
        {
            return new Vector3(NormalizeBoundsAxis(position.x,bounds.min.x,bounds.size.x),
                NormalizeBoundsAxis(position.y,bounds.min.y,bounds.size.y),NormalizeBoundsAxis(position.z,bounds.min.z,bounds.size.z));
        }

        private static float NormalizeBoundsAxis(float value,float min,float size)
        {return size>0.000001f?(value-min)/size:0.5f;}

        private static Bounds SourceBounds(Transform root)
        {var rs=root.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new InvalidOperationException("No renderers found.");Bounds b=rs[0].bounds;for(int i=1;i<rs.Length;i++)b.Encapsulate(rs[i].bounds);return b;}

        private static void EnsureFolder(string folder)
        {string[] p=folder.Split('/');string cur=p[0];for(int i=1;i<p.Length;i++){string next=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(cur,p[i]);cur=next;}}
    }
}
