using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityRigValidator
    {
        public sealed class Finding { public string severity,code,message; public Finding(string s,string c,string m){severity=s;code=c;message=m;} public override string ToString(){return severity+" ["+code+"] "+message;} }
        public static List<Finding> Inspect(RebirthEntityRigWorkspace ws)
        {
            RebirthEntityRigBuilder.BindWorkspaceReferences(ws);
            var f=new List<Finding>();
            if(ws==null||ws.profile==null){f.Add(new Finding("ERROR","NO_PROFILE","Workspace/profile is missing."));return f;}
            var p=ws.profile;
            if(!p.reviewedTargetCompatibility||string.IsNullOrWhiteSpace(p.targetCompatibilityEvidence))
                f.Add(new Finding("WARNING","TARGET_COMPATIBILITY_NEEDS_REVIEW","Review the generated rig, tags, animation contract, tier shader/material behavior, LODs and hit/ragdoll setup against the exact target build, then record the build and evidence source in the profile."));
            if(!string.IsNullOrEmpty(ws.sourceModelAssetPath)&&AssetDatabase.LoadAssetAtPath<GameObject>(ws.sourceModelAssetPath)==null)
                f.Add(new Finding("ERROR","SOURCE_MODEL_MISSING","Original model asset is missing: "+ws.sourceModelAssetPath));
            if(HasStaleInputs(ws))
                f.Add(new Finding("ERROR","STALE_PROFILE","The source model or profile inputs changed after this workspace was built. Rebuild the workspace before reviewing or exporting it."));
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var b in p.bones)
            {
                if(b==null||string.IsNullOrWhiteSpace(b.name)){f.Add(new Finding("ERROR","EMPTY_BONE","Bone name is empty."));continue;}
                if(!names.Add(b.name))f.Add(new Finding("ERROR","DUPLICATE_BONE","Duplicate bone: "+b.name));
                if(!string.IsNullOrWhiteSpace(b.name)&&b.name!="Origin"&&!string.IsNullOrWhiteSpace(b.parent)&&!names.Contains(b.parent)&&!string.Equals(b.parent,"Origin",StringComparison.OrdinalIgnoreCase))
                    f.Add(new Finding("ERROR","BONE_PARENT_ORDER",b.name+" has a missing parent or its parent appears later in the profile: "+b.parent));
                if(b.required&&RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,b.name)==null)f.Add(new Finding("ERROR","MISSING_BONE","Required bone absent: "+b.name));
                if(!string.IsNullOrEmpty(b.gameTag))
                {var t=RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,b.name);if(t!=null&&t.tag!=b.gameTag)f.Add(new Finding("ERROR","TAG_MISMATCH",b.name+" has tag "+t.tag+", expected "+b.gameTag+". Add the tag in TagManager first."));}
            }
            int deformBoneCount=RebirthEntityRigBuilder.EstimateBodySegments(p);
            if(!p.reviewedSkeletonMapping&&deformBoneCount<58)
                f.Add(new Finding("WARNING","BASE_SKELETON_DENSITY","The generated skeleton has "+deformBoneCount+" mesh-deforming bones; sampled 3.3.0 Worker and Arlene rigs use 58 and 74 renderer bones. This biped seed is not a drop-in animation rig until the target model's full bone contract is mapped and reviewed."));
            var skinnedRenderers=new List<SkinnedMeshRenderer>();
            if(p.sourceSkinnedMeshes!=null)foreach(var renderer in p.sourceSkinnedMeshes)if(renderer!=null)skinnedRenderers.Add(renderer);
            if(skinnedRenderers.Count==0&&p.sourceSkinnedMesh!=null)skinnedRenderers.Add(p.sourceSkinnedMesh);
            if(skinnedRenderers.Count==0)f.Add(new Finding("ERROR","NO_SKIN","Generated SkinnedMeshRenderer missing."));
            else
            {
                foreach(var smr in skinnedRenderers)
                {
                    if(smr.sharedMesh==null||smr.sharedMesh.boneWeights.Length!=smr.sharedMesh.vertexCount)f.Add(new Finding("ERROR","WEIGHTS","Mesh vertex weights incomplete: "+smr.name));
                    else
                    {
                        if(smr.sharedMesh.bindposes.Length!=smr.bones.Length)f.Add(new Finding("ERROR","BINDPOSE_COUNT","Bind pose count does not match bones: "+smr.name));
                        var weights=smr.sharedMesh.boneWeights;
                        for(int vertex=0;vertex<weights.Length;vertex++)
                        {
                            var w=weights[vertex];
                            float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                            if(float.IsNaN(sum)||float.IsInfinity(sum)||Mathf.Abs(sum-1f)>0.001f)
                            {f.Add(new Finding("ERROR","WEIGHT_NORMALIZATION",smr.name+" vertex "+vertex+" has total skin weight "+sum+", expected 1."));break;}
                            if(!ValidInfluence(w.boneIndex0,w.weight0,smr.bones.Length)||!ValidInfluence(w.boneIndex1,w.weight1,smr.bones.Length)||!ValidInfluence(w.boneIndex2,w.weight2,smr.bones.Length)||!ValidInfluence(w.boneIndex3,w.weight3,smr.bones.Length))
                            {f.Add(new Finding("ERROR","WEIGHT_BONE_INDEX",smr.name+" vertex "+vertex+" references an invalid bone or has a negative influence."));break;}
                        }
                    }
                    foreach(var m in smr.sharedMaterials)if(m==null||m.shader==null)f.Add(new Finding("ERROR","MATERIAL","Missing material or shader on "+smr.name));
                }
                if(!p.reviewedSkeletonMapping)f.Add(new Finding("WARNING","WEIGHTS_NEED_REVIEW","Generated nearest-bone weights are provisional; review the deforming mesh in multiple poses before export."));
            }
            bool physicsRequested=p.createHitColliders||p.createRagdoll;
            if(physicsRequested&&ws.colliderRoot==null)f.Add(new Finding("ERROR","NO_PHYSICS","Collider/ragdoll hierarchy missing."));
            else if(physicsRequested&&ws.colliderRoot!=null)
            {
                var colliders=ws.generatedSkeleton.GetComponentsInChildren<Collider>(true);
                if(colliders.Length<8)f.Add(new Finding("ERROR","COLLIDER_COVERAGE","Fewer than 8 body colliders were generated."));
                foreach(var c in colliders)
                {
                    if(c is CapsuleCollider capsule&&(capsule.radius<=0||capsule.height<2*capsule.radius))f.Add(new Finding("ERROR","BAD_CAPSULE",c.name+" has invalid radius/height."));
                    else if(c is BoxCollider box&&(box.size.x<=0||box.size.y<=0||box.size.z<=0))f.Add(new Finding("ERROR","BAD_BOX",c.name+" has invalid box dimensions."));
                    else if(!(c is CapsuleCollider)&&!(c is BoxCollider))f.Add(new Finding("ERROR","UNSUPPORTED_COLLIDER","Native physics body binding requires a supported box/capsule: "+c.name));
                }
                if(p.createRagdoll)
                {
                    var bodies=ws.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true);
                    var joints=ws.generatedSkeleton.GetComponentsInChildren<CharacterJoint>(true);
                    foreach(var collider in colliders)
                    {
                        bool compound=collider.name.StartsWith("EntityStudio_Compound_",StringComparison.Ordinal);
                        if(collider.attachedRigidbody==null||(!compound&&collider.GetComponent<Rigidbody>()==null)||
                            (compound&&(!p.compoundTerminalColliders||collider.GetComponent<Rigidbody>()!=null)))
                            f.Add(new Finding("ERROR","RAGDOLL_BODY_COUNT",collider.name+" must have its own body or be an explicitly enabled compound shape on an existing parent body."));
                    }
                    foreach(var joint in joints)if(joint.connectedBody==null)f.Add(new Finding("ERROR","RAGDOLL_JOINT_PARENT",joint.name+" has no connected parent Rigidbody."));
                    if(bodies.Length>0&&joints.Length<bodies.Length-1)f.Add(new Finding("ERROR","RAGDOLL_CHAIN_INCOMPLETE","Ragdoll body chain is missing one or more explicit parent joints."));
                }
                if(ws.colliderFitReports==null||ws.colliderFitReports.Count!=colliders.Length)
                    f.Add(new Finding("ERROR","COLLIDER_FIT_REPORT","Each generated collider must have a mesh-fit report."));
                else foreach(var fit in ws.colliderFitReports)
                {
                    if(fit.usesPoseUnionCoverage)
                    {
                        if(fit.poseUnionSampleCount<=0||!(fit.poseUnionCoverage>=1f))f.Add(new Finding("ERROR","POSE_UNION_COVERAGE",fit.colliderName+" has no complete posed region-union coverage proof."));
                    }
                    else if(fit.usedProportionalFallback)f.Add(new Finding("WARNING","COLLIDER_FALLBACK",fit.colliderName+" used a proportional fallback because too few weighted mesh vertices were available."));
                    else if(fit.sampledVertices>0&&fit.estimatedSurfaceCoverage<0.95f)f.Add(new Finding("WARNING","COLLIDER_COVERAGE_LOW",fit.colliderName+" encloses only "+(fit.estimatedSurfaceCoverage*100f).ToString("0.0")+"% of its assigned mesh samples."));
                    if(!p.reviewedColliderFit&&fit.collider is CapsuleCollider&&fit.baseGameReferenceRadius>0f&&fit.radius>fit.baseGameReferenceRadius*1.5f)
                        f.Add(new Finding("WARNING","COLLIDER_OVERSIZED",fit.colliderName+" radius "+fit.radius.ToString("F3")+
                            " exceeds 1.5x the measured 3.3.0 biped reference "+fit.baseGameReferenceRadius.ToString("F3")+
                            "; inspect bone placement and assigned vertices."));
                }
                if(ws.colliderFitReports!=null&&ws.colliderFitReports.Exists(fit=>fit.usesPoseUnionCoverage)&&ws.poseColliderProofSignature!=RebirthEntityColliderCandidate.Signature(ws))
                    f.Add(new Finding("ERROR","POSE_UNION_PROOF_STALE","Collider or mesh state changed after posed region-union verification; rerun that verification before export."));
                if(!p.reviewedColliderFit)f.Add(new Finding("WARNING","FIT_NEEDS_REVIEW","Inspect each mesh-fitted capsule wireframe and its reported sample coverage against the silhouette, then test hit coverage."));
            }
            if(p.generateLods&&ws.lodGroup==null)f.Add(new Finding("ERROR","NO_LOD","LODGroup requested but not generated."));
            else if(ws.lodGroup!=null)
            {
                var levels=ws.lodGroup.GetLODs();
                if(levels.Length<2)f.Add(new Finding("ERROR","LOD_LEVEL_COUNT","At least one simplified LOD is required below LOD0."));
                int previousTriangles=int.MaxValue;
                for(int i=0;i<levels.Length;i++)
                {
                    int triangles=0;int rendererCount=0;
                    foreach(var renderer in levels[i].renderers)
                    {
                        if(renderer==null)continue;
                        var skinned=renderer as SkinnedMeshRenderer;
                        if(skinned==null||skinned.sharedMesh==null)continue;
                        rendererCount++;
                        for(int sub=0;sub<skinned.sharedMesh.subMeshCount;sub++)
                            triangles+=skinned.sharedMesh.GetTriangles(sub).Length/3;
                    }
                    if(rendererCount==0||triangles==0)f.Add(new Finding("ERROR","LOD_EMPTY","LOD"+i+" has no valid skinned mesh renderers or triangles."));
                    if(i>0&&triangles>=previousTriangles)
                        f.Add(new Finding("ERROR","LOD_NO_REDUCTION","LOD"+i+" has "+triangles+" triangles; it must be lower than the previous level's "+previousTriangles+"."));
                    previousTriangles=triangles;
                }
                if(!p.reviewedLods)f.Add(new Finding("WARNING","LOD_NEEDS_REVIEW","Inspect every simplified LOD's silhouette, UV seams, material slots, skinning, transition and measured triangle count."));
            }
            if(p.generateTierMaterials)
            {
                int totalSlots=0,totalVariants=0;
                if(p.rendererTierMaterials!=null)foreach(var set in p.rendererTierMaterials)
                {
                    if(set==null||set.renderer==null||set.slotCount<=0||set.variants==null||set.variants.Count!=set.slotCount*5)
                    {f.Add(new Finding("ERROR","TIER_RENDERER_SET","A source renderer is missing its complete five-tier material set."));continue;}
                    totalSlots+=set.slotCount;totalVariants+=set.variants.Count;
                    if(set.renderer.sharedMaterials.Length!=set.slotCount)f.Add(new Finding("ERROR","MATERIAL_SLOT_COUNT","Active renderer material slots do not match the tier set: "+set.renderer.name));
                    foreach(var material in set.variants)if(material==null||material.shader==null)f.Add(new Finding("ERROR","TIER_MATERIAL_MISSING","Tier material or shader is missing for "+set.renderer.name));
                }
                if(totalSlots!=p.generatedMaterialSlotCount||totalSlots==0||p.generatedTierMaterials==null||p.generatedTierMaterials.Count!=totalVariants||totalVariants!=totalSlots*5)
                    f.Add(new Finding("ERROR","TIER_MATERIAL_SET","A complete five-tier material set is required for every renderer slot."));
                if(!p.reviewedMaterials)f.Add(new Finding("WARNING","SHADER_NEEDS_REVIEW","Tier variants use assigned shader properties; verify the target Game/Character shader and its tint/emission contract."));
            }
            foreach(string anchor in p.requiredGoreAnchors)if(RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,anchor)==null)f.Add(new Finding("ERROR","GORE_ANCHOR_MISSING","Required gore attachment anchor missing: "+anchor));
            foreach(string anchor in p.requiredEyeAnchors)
            {
                var eye=RebirthEntityRigBuilder.FindBone(ws.generatedSkeleton,anchor);
                if(eye==null&&ws.sourceVisual!=null)eye=RebirthEntityRigBuilder.FindBone(ws.sourceVisual,anchor);
                if(eye==null)f.Add(new Finding("ERROR","EYE_ANCHOR_MISSING","Required eye attachment anchor missing: "+anchor));
                else if(eye.tag!="E_BP_Eye")f.Add(new Finding("ERROR","EYE_ANCHOR_TAG",anchor+" must use the E_BP_Eye GameObject tag."));
            }
            f.Add(new Finding("INFO","TARGET_VERSION","Configured target: "+p.gameVersion+". Public v3.0 XML and 3.3.0 runtime captures do not independently verify the b259 prefab, shader or animation contract."));
            return f;
        }

        public static bool HasStaleInputs(RebirthEntityRigWorkspace ws)
        {
            if(ws==null||ws.profile==null||string.IsNullOrEmpty(ws.profileBuildFingerprint))return true;
            string currentSourceKey=ws.sourceModelKey;
            if(ws.sourceModelIsSceneInstance)
            {
                if(ws.sourceModelObject==null)return true;
                currentSourceKey=RebirthEntityRigBuilder.BuildModelKey(ws.sourceModelObject);
            }
            else if(!string.IsNullOrEmpty(ws.sourceModelAssetPath))
            {
                var sourceAsset=AssetDatabase.LoadAssetAtPath<GameObject>(ws.sourceModelAssetPath);
                if(sourceAsset==null)return true;
                currentSourceKey=RebirthEntityRigBuilder.BuildModelKey(sourceAsset);
            }
            else if(string.IsNullOrEmpty(ws.sourceModelAssetPath))return true;
            return ws.profileBuildFingerprint!=RebirthEntityRigBuilder.ComputeProfileFingerprint(ws.profile,currentSourceKey);
        }

        private static bool ValidInfluence(int index,float weight,int boneCount)
        {return weight>=0f&&(weight==0f||(index>=0&&index<boneCount));}
    }
}
