using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RebirthEntityStudio
{
    public static class RebirthEntityEyeGeometry
    {
        public static void RebuildSavedFarmer()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            var current=workspace.generatedSkeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="FeralEye");
            var emission=current.sharedMaterial.GetTexture("_EmissionMap") as Texture2D;if(emission==null)throw new InvalidOperationException("Existing owned eye emission missing.");
            Build(workspace,new RebirthEntityGeneratedMaps{eyeEmission=emission});
            workspace.profile.reviewedMaterials=false;
            RebirthEntityColliderCandidate.Verify(workspace);
            workspace.profileBuildFingerprint=RebirthEntityRigBuilder.ComputeProfileFingerprint(workspace.profile,workspace.sourceModelKey);
            EditorUtility.SetDirty(workspace);EditorUtility.SetDirty(workspace.profile);AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Eye skinning scene save failed.");
            RebirthEntityEyeSurfaceAudit.RunSavedFarmer();
            Debug.Log("SOURCE-FACE EYE SKINNING SAVED AND AUDITED; material review remains false.");
        }

        private struct Point
        {
            public Vector3 world,normal;public Vector2 uv;public BoneWeight weight;
            public static Point Lerp(Point a,Point b,float t)=>new Point{world=Vector3.Lerp(a.world,b.world,t),normal=Vector3.Lerp(a.normal,b.normal,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),weight=Blend(a.weight,b.weight,t)};
        }

        // Build an owned surface patch from the supplied face, preserving its atlas.
        // No game geometry or eye textures are copied into the authored bundle.
        public static void Build(RebirthEntityRigWorkspace workspace,RebirthEntityGeneratedMaps maps,bool persistAssets=true)
        {
            Transform head=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"Head");
            if(head==null)throw new InvalidOperationException("Eye geometry requires Head.");
            var profile=workspace.profile;float h=profile.estimatedHeight;
            workspace.rendererTierMaterials.RemoveAll(set=>set.renderer!=null&&set.renderer.CompareTag("E_BP_Eye"));
            float eyeY=h*profile.eyeHeightRatio,eyeX=h*profile.eyeHalfSeparationRatio,rx=h*profile.eyeWidthRatio,ry=h*profile.eyeRadiusHeightRatio;
            var points=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            var weights=new List<BoneWeight>();var bones=workspace.sourceSkinnedMeshes.SelectMany(r=>r.bones).Distinct().ToArray();
            foreach(var source in workspace.sourceSkinnedMeshes)
            {
                Mesh mesh=source.sharedMesh;var vertices=mesh.vertices;var sourceNormals=mesh.normals;var sourceUv=mesh.uv;var indices=mesh.triangles;var sourceWeights=mesh.boneWeights;
                var boneMap=source.bones.Select(b=>Array.IndexOf(bones,b)).ToArray();
                for(int i=0;i<indices.Length;i+=3)foreach(int side in new[]{-1,1})
                {
                    var polygon=new List<Point>();
                    for(int corner=0;corner<3;corner++){int index=indices[i+corner];BoneWeight w=sourceWeights[index];w.boneIndex0=boneMap[w.boneIndex0];w.boneIndex1=boneMap[w.boneIndex1];w.boneIndex2=boneMap[w.boneIndex2];w.boneIndex3=boneMap[w.boneIndex3];polygon.Add(new Point{world=source.transform.TransformPoint(vertices[index]),normal=source.transform.TransformDirection(sourceNormals[index]).normalized,uv=sourceUv[index],weight=w});}
                    // Only the front face participates. Each eye uses a 16-sided ellipse.
                    polygon=Clip(polygon,p=>(p.world-workspace.transform.position).z-.02f);
                    for(int plane=0;plane<16&&polygon.Count>=3;plane++)
                    {
                        float angle=(plane+.5f)*Mathf.PI*2f/16f;Vector2 axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                        polygon=Clip(polygon,p=>{Vector3 local=p.world-workspace.transform.position;return Mathf.Cos(Mathf.PI/16f)-Vector2.Dot(new Vector2((local.x-side*eyeX)/rx,(local.y-eyeY)/ry),axis);});
                    }
                    for(int corner=1;corner+1<polygon.Count;corner++)foreach(int index in new[]{0,corner,corner+1})
                    {
                        Point point=polygon[index];triangles.Add(points.Count);points.Add(head.InverseTransformPoint(point.world+point.normal*.0003f));normals.Add(head.InverseTransformDirection(point.normal));uv.Add(point.uv);weights.Add(point.weight);
                    }
                }
            }
            if(points.Count<6)throw new InvalidOperationException("Configured eye landmarks did not intersect the face.");
            var eyeMesh=new Mesh{name=profile.entityClass+"_OwnedEyes"};eyeMesh.SetVertices(points);eyeMesh.SetNormals(normals);eyeMesh.SetUVs(0,uv);eyeMesh.SetTriangles(triangles,0);
            eyeMesh.boneWeights=weights.ToArray();eyeMesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*head.localToWorldMatrix).ToArray();eyeMesh.RecalculateBounds();eyeMesh.RecalculateTangents();
            string folder="Assets/EntityStudioGenerated/"+profile.entityClass;if(persistAssets)AssetDatabase.CreateAsset(eyeMesh,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+eyeMesh.name+".asset"));
            var eyes=new List<Renderer>();
            foreach(string name in new[]{"NormalEye","FeralEye"})
            {
                Transform anchor=RebirthEntityRigBuilder.FindBone(head,name);if(anchor==null){anchor=new GameObject(name).transform;anchor.SetParent(head,false);}
                anchor.SetParent(head,false);anchor.localPosition=Vector3.zero;anchor.localRotation=Quaternion.identity;anchor.localScale=Vector3.one;anchor.gameObject.tag="E_BP_Eye";
                var renderer=anchor.GetComponent<SkinnedMeshRenderer>();if(renderer==null)renderer=anchor.gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh=eyeMesh;renderer.bones=bones;renderer.rootBone=head;renderer.quality=SkinQuality.Bone4;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*.5f);
                var set=new EntityRendererTierMaterials{renderer=renderer,slotCount=1};
                for(int tier=0;tier<5;tier++)
                {
                    var body=workspace.rendererTierMaterials[0];var material=new Material(body.variants[tier*body.slotCount]){name=profile.entityClass+"_"+name+"_"+((ZombieTier)tier)};
                    if(name=="FeralEye"){material.SetTexture("_MetallicGlossMap",null);material.DisableKeyword("_METALLICGLOSSMAP");material.SetFloat("_Metallic",.256f);material.SetFloat("_Glossiness",.885f);material.SetTexture("_EmissionMap",maps.eyeEmission);material.SetColor("_EmissionColor",Color.white);material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;material.EnableKeyword("_EMISSION");}
                    if(persistAssets)AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(folder+"/"+material.name+".mat"));set.variants.Add(material);profile.generatedTierMaterials.Add(material);
                }
                workspace.rendererTierMaterials.Add(set);renderer.sharedMaterial=set.variants[0];anchor.gameObject.SetActive(name=="NormalEye");eyes.Add(renderer);
            }
            if(workspace.lodGroup!=null){var levels=workspace.lodGroup.GetLODs();for(int i=0;i<levels.Length;i++){var renderers=new List<Renderer>(levels[i].renderers);renderers.AddRange(eyes);levels[i].renderers=renderers.Distinct().ToArray();}workspace.lodGroup.SetLODs(levels);}
            ReconcileMaterialInventory(workspace);
            EditorUtility.SetDirty(workspace);Debug.Log("OWNED EYE GEOMETRY: vertices="+points.Count+"; source-face four-influence skinning; native Normal/Feral activation; source UV preserved");
        }

        static BoneWeight Blend(BoneWeight a,BoneWeight b,float t)
        {
            var values=new Dictionary<int,float>();Add(a,1-t);Add(b,t);var ordered=values.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key).Take(4).ToArray();float total=ordered.Sum(p=>p.Value);
            int I(int i)=>i<ordered.Length?ordered[i].Key:0;float W(int i)=>i<ordered.Length?ordered[i].Value/total:0;
            return new BoneWeight{boneIndex0=I(0),weight0=W(0),boneIndex1=I(1),weight1=W(1),boneIndex2=I(2),weight2=W(2),boneIndex3=I(3),weight3=W(3)};
            void Add(BoneWeight w,float scale){Acc(w.boneIndex0,w.weight0*scale);Acc(w.boneIndex1,w.weight1*scale);Acc(w.boneIndex2,w.weight2*scale);Acc(w.boneIndex3,w.weight3*scale);}
            void Acc(int bone,float amount){if(amount<=0)return;values.TryGetValue(bone,out float previous);values[bone]=previous+amount;}
        }

        public static void ReconcileMaterialInventory(RebirthEntityRigWorkspace workspace)
        {
            int slots=0;var materials=new List<Material>();
            foreach(var set in workspace.rendererTierMaterials)
            {
                if(set==null||set.renderer==null||set.slotCount<1||set.variants==null||set.variants.Count!=set.slotCount*5)
                    throw new InvalidOperationException("Cannot reconcile an incomplete renderer tier set.");
                foreach(var material in set.variants)if(material==null||material.shader==null)throw new InvalidOperationException("Cannot reconcile missing tier materials.");
                slots+=set.slotCount;materials.AddRange(set.variants);
            }
            workspace.profile.generatedMaterialSlotCount=slots;workspace.profile.generatedTierMaterials=materials;
            EditorUtility.SetDirty(workspace.profile);
        }

        private static List<Point> Clip(List<Point> input,Func<Point,float> distance)
        {
            var output=new List<Point>();if(input.Count==0)return output;Point previous=input[input.Count-1];float prior=distance(previous);
            foreach(Point point in input){float current=distance(point);if((prior>=0)!=(current>=0))output.Add(Point.Lerp(previous,point,prior/(prior-current)));if(current>=0)output.Add(point);previous=point;prior=current;}return output;
        }
    }
}
