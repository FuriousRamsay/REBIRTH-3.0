using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityArmRemeshExchange
    {
        [Serializable] public sealed class Input
        {public Vector3[] vertices;public Vector2[] uv;public int[] triangles,parts,repairIds;public BoneWeight[] weights;public string[] boneNames;}
        public static void ExportSavedFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            if(workspace.sourceSkinnedMeshes.Count!=1)throw new InvalidOperationException("Remesh exchange requires explicitly mapped single source renderer.");
            var renderer=workspace.sourceSkinnedMeshes[0];var mesh=renderer.sharedMesh;var repair=workspace.surfaceRepairInputs.Single(value=>value.repairedMesh==mesh);
            var data=new Input{vertices=mesh.vertices.Select(renderer.transform.TransformPoint).ToArray(),uv=mesh.uv,triangles=mesh.triangles,parts=repair.repairVertexParts,repairIds=repair.repairVertexIds,weights=mesh.boneWeights,boneNames=renderer.bones.Select(bone=>bone.name).ToArray()};
            string output=Environment.GetCommandLineArgs().SkipWhile(arg=>arg!="-armRemeshOutput").Skip(1).First();Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"Input.json"),JsonUtility.ToJson(data));
            Debug.Log("ARM REMESH EXCHANGE exported current geometry/UV/weights/anatomical provenance, no assets changed.");
        }
        [Serializable] public sealed class Arm {public Vector3[] vertices,normals;public Vector2[] uv;public BoneWeight[] weights;public int[] triangles;public float transferMaxDistance,transferMeanDistance;}
        [Serializable] public sealed class SourceNormals {public string inputSha256;public Vector3[] normals;}
        [Serializable] public sealed class CandidateCollider {public string name,parent,tag;public int layer,direction;public Vector3 center;public Quaternion localRotation;public float radius,height;}
        [Serializable] public sealed class CandidateColliders {public string geometrySha256;public CandidateCollider[] shapes;}
        public static void ExportSourceNormals()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            var renderer=workspace.sourceSkinnedMeshes.Single();var mesh=renderer.sharedMesh;
            string folder=Environment.GetCommandLineArgs().SkipWhile(arg=>arg!="-armRemeshOutput").Skip(1).First();
            byte[] bytes=File.ReadAllBytes(Path.Combine(folder,"Input.json"));var input=JsonUtility.FromJson<Input>(System.Text.Encoding.UTF8.GetString(bytes));
            var vertices=mesh.vertices;var triangles=mesh.triangles;
            if(vertices.Length!=input.vertices.Length||!triangles.SequenceEqual(input.triangles))throw new InvalidDataException("Source geometry changed since arm exchange export.");
            for(int i=0;i<vertices.Length;i++)if(Vector3.Distance(renderer.transform.TransformPoint(vertices[i]),input.vertices[i])>1e-6f)throw new InvalidDataException("Source vertex positions changed since exchange export.");
            if(mesh.normals.Length!=vertices.Length)throw new InvalidDataException("Source normals missing.");
            string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            var normalMatrix=renderer.transform.worldToLocalMatrix.transpose;
            var output=new SourceNormals{inputSha256=hash,normals=mesh.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray()};
            File.WriteAllText(Path.Combine(folder,"SourceNormals.json"),JsonUtility.ToJson(output));
            Debug.Log("ARM SOURCE NORMALS exported="+output.normals.Length+" inputSha256="+hash+"; no assets changed.");
        }
        public static void RenderCandidate()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            var renderer=workspace.sourceSkinnedMeshes.Single();var original=renderer.sharedMesh;Mesh candidate=null;
            bool baked=Array.IndexOf(Environment.GetCommandLineArgs(),"-bakedArmAtlas")>=0;var originalMaterials=renderer.sharedMaterials;var disposableMaterials=new List<Material>();var textures=new List<Texture2D>();
            string folder=Environment.GetCommandLineArgs().SkipWhile(arg=>arg!="-armRemeshOutput").Skip(1).First();
            var input=workspace.surfaceRepairInputs.Single(value=>value.repairedMesh==original);
            var colliderCenters=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true).ToDictionary(s=>s,s=>s.center);
            var colliderRotations=colliderCenters.Keys.ToDictionary(s=>s,s=>s.transform.localRotation);
            var colliderDimensions=colliderCenters.Keys.ToDictionary(s=>s,s=>(radius:s.radius,height:s.height));
            var fingerShapes=new List<GameObject>();
            try
            {
                var vertices=new List<Vector3>(original.vertices);var uv=new List<Vector2>(original.uv);var weights=new List<BoneWeight>(original.boneWeights);var triangles=new List<int>();var sourceTriangles=original.triangles;
                var transferredNormals=new List<Vector3>(original.normals);
                var submeshes=new List<int[]>();var materials=new List<Material>{originalMaterials[0]};
                for(int t=0;t<sourceTriangles.Length;t+=3)if(input.repairVertexParts[sourceTriangles[t]]==0){triangles.Add(sourceTriangles[t]);triangles.Add(sourceTriangles[t+1]);triangles.Add(sourceTriangles[t+2]);}
                if(baked)submeshes.Add(triangles.ToArray());
                foreach(int part in new[]{1,2})
                {
                    var arm=JsonUtility.FromJson<Arm>(File.ReadAllText(Path.Combine(folder,"Arm"+part+(baked?"_Baked.json":"_Transferred.json"))));
                    if(arm.vertices.Length!=arm.uv.Length||arm.vertices.Length!=arm.weights.Length)throw new InvalidDataException("Incomplete remesh transfer.");
                    int offset=vertices.Count;vertices.AddRange(arm.vertices.Select(renderer.transform.InverseTransformPoint));uv.AddRange(arm.uv);weights.AddRange(arm.weights);triangles.AddRange(arm.triangles.Select(i=>i+offset));
                    if(baked)
                    {
                        submeshes.Add(arm.triangles.Select(i=>i+offset).ToArray());var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false);textures.Add(texture);if(!texture.LoadImage(File.ReadAllBytes(Path.Combine(folder,"Arm"+part+"_Albedo.png"))))throw new InvalidDataException("Cannot decode baked arm atlas.");
                        var material=new Material(originalMaterials[0]);disposableMaterials.Add(material);material.mainTexture=texture;material.DisableKeyword("_BUMPMAP");material.DisableKeyword("_METALLICGLOSSMAP");material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",.18f);materials.Add(material);
                        if(arm.normals==null||arm.normals.Length!=arm.vertices.Length)throw new InvalidDataException("Baked tangent basis normals missing.");
                        transferredNormals.AddRange(arm.normals.Select(n=>renderer.transform.localToWorldMatrix.transpose.MultiplyVector(n).normalized));
                        var rough=LoadData("roughness");var metal=LoadData("metallic");var normal=LoadData("normal");
                        if(rough.width!=metal.width||rough.height!=metal.height||rough.width!=normal.width||rough.height!=normal.height)throw new InvalidDataException("Arm map dimensions differ.");
                        var rp=rough.GetPixels32();var mp=metal.GetPixels32();var np=normal.GetPixels32();
                        for(int pixel=0;pixel<rp.Length;pixel++){mp[pixel]=new Color32(mp[pixel].r,0,0,(byte)(255-rp[pixel].r));var n=np[pixel];np[pixel]=new Color32(255,n.g,n.b,n.r);}
                        metal.SetPixels32(mp);metal.Apply();normal.SetPixels32(np);normal.Apply();material.SetTexture("_MetallicGlossMap",metal);material.SetFloat("_GlossMapScale",1);material.EnableKeyword("_METALLICGLOSSMAP");material.SetTexture("_BumpMap",normal);material.EnableKeyword("_BUMPMAP");
                        Texture2D LoadData(string suffix){var data=new Texture2D(2,2,TextureFormat.RGBA32,true,true);textures.Add(data);if(!data.LoadImage(File.ReadAllBytes(Path.Combine(folder,"Arm"+part+"_"+suffix+".png"))))throw new InvalidDataException("Cannot decode arm "+suffix);return data;}
                    }
                    Debug.Log("ARM VOLUME REVIEW part="+part+" transferMax="+arm.transferMaxDistance+" mean="+arm.transferMeanDistance);
                }
                candidate=new Mesh{name="FarmerArmVolumeCandidate",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};candidate.SetVertices(vertices);candidate.SetUVs(0,uv);candidate.boneWeights=weights.ToArray();candidate.bindposes=original.bindposes;candidate.SetTriangles(triangles,0);candidate.RecalculateNormals();
                if(baked){candidate.subMeshCount=submeshes.Count;for(int i=0;i<submeshes.Count;i++)candidate.SetTriangles(submeshes[i],i);renderer.sharedMaterials=materials.ToArray();}
                var normals=candidate.normals;var oldNormals=original.normals;Array.Copy(oldNormals,normals,oldNormals.Length);candidate.normals=normals;candidate.RecalculateTangents();candidate.RecalculateBounds();renderer.sharedMesh=candidate;
                if(baked){candidate.normals=transferredNormals.ToArray();candidate.RecalculateTangents();}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-diffuseRemeshArms")>=0)
                {
                    var pinned=new bool[candidate.vertexCount];var rest=candidate.vertices;
                    for(int i=0;i<pinned.Length;i++)pinned[i]=i<original.vertexCount||Mathf.Abs(renderer.transform.TransformPoint(rest[i]).x)>.62f;
                    RebirthEntityHandWeights.Diffuse(renderer,40,.25f,pinned,name=>name.Contains("Arm")||name.Contains("Shoulder"),.03f);
                    Debug.Log("REMESH ARM DIFFUSION: original body and hand vertices pinned; no assets saved.");
                }
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-palmWeightTrial")>=0)RebirthEntityPalmWeights.Apply(renderer,original.vertexCount);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-diffuseRemeshHands")>=0)
                {
                    var pinned=new bool[candidate.vertexCount];for(int i=0;i<original.vertexCount;i++)pinned[i]=true;
                    RebirthEntityHandWeights.Diffuse(renderer,40,.15f,pinned,name=>name.Contains("Hand"),.015f);
                }
                Compact(candidate);
                string geometrySha256=RebirthEntityColliderCheckpoint.GeometryHash(renderer);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-coverRemeshRest")>=0)
                {
                    var shapes=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
                    var samples=RebirthEntityColliderPoseAudit.Capture(workspace,shapes,true,true);
                    RebirthEntityColliderPoseAudit.CoverAll(samples,shapes,.0002f);
                    Debug.Log("REMESH REST COVERAGE: bounded original capsule radius growth to0.2mm; no assets saved.");
                }
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fingerHitTrial")>=0)fingerShapes=RebirthEntityFingerHitTrial.Add(workspace);
                string checkpoint=Environment.GetCommandLineArgs().SkipWhile(arg=>arg!="-colliderCheckpoint").Skip(1).FirstOrDefault();
                if(checkpoint!=null)
                {
                    if(fingerShapes.Count!=0)throw new InvalidOperationException("Checkpoint replay cannot be combined with fresh finger creation.");
                    RebirthEntityColliderCheckpoint.Apply(workspace,renderer,checkpoint,geometrySha256,fingerShapes);
                }
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fitCurledHands")>=0)RebirthEntityHandPoseColliderFit.Fit(workspace,folder);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fitFingerRadii")>=0)RebirthEntityFingerHitTrial.FitRadii(workspace,fingerShapes,folder);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-refitHandsAfterPrune")>=0)RebirthEntityHandPoseColliderFit.Fit(workspace,folder);
                bool deferFingerPruning=Array.IndexOf(Environment.GetCommandLineArgs(),"-deferFingerPruning")>=0;
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fitRetainedFingers")>=0&&!deferFingerPruning)RebirthEntityHandPoseColliderFit.Fit(workspace,folder,true);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-shrinkArmCapsules")>=0)RebirthEntityArmColliderVolumeFit.Shrink(workspace,folder,deferFingerPruning?fingerShapes:null);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fitRetainedFingers")>=0&&deferFingerPruning)RebirthEntityHandPoseColliderFit.Fit(workspace,folder,true);
                var colliderReport=new CandidateColliders{geometrySha256=geometrySha256,shapes=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true).Select(s=>new CandidateCollider{name=s.name,parent=s.transform.parent.name,tag=s.tag,layer=s.gameObject.layer,direction=s.direction,center=s.center,localRotation=s.transform.localRotation,radius=s.radius,height=s.height}).ToArray()};
                File.WriteAllText(Path.Combine(folder,"CandidateColliders.json"),JsonUtility.ToJson(colliderReport,true));
                bool clay=Array.IndexOf(Environment.GetCommandLineArgs(),"-clayArmReview")>=0;
                if(clay){var material=new Material(Shader.Find("Standard"));material.color=new Color(.55f,.55f,.55f);material.SetFloat("_Glossiness",.1f);disposableMaterials.Add(material);renderer.sharedMaterials=Enumerable.Repeat(material,candidate.subMeshCount).ToArray();}
                RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,clay?"CandidateClayRest":baked?"CandidateBakedRest":"CandidateRest"),false,true);
                if(fingerShapes.Count>0)RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,"FingerHitTrial_Overlay"),true);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-captureHandColliders")>=0)foreach(string side in new[]{"Left","Right"})RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,side+"Hand_Rest_Colliders"),true,false,side);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-validateArmPoses")>=0)
                {
                    var rotations=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localRotation);
                    try
                    {
                        foreach(string pose in new[]{"rest","elbows","reach","leftFingers","rightFingers"})
                        {
                            foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                            if(pose=="elbows"||pose=="reach")foreach(string side in new[]{"Left","Right"}){Rotate(side+"ForeArm",new Vector3(70,0,0));if(pose=="reach")Rotate(side+"Arm",new Vector3(25,0,side=="Left"?-35:35));}
                            if(pose.EndsWith("Fingers",StringComparison.Ordinal))RebirthEntityHumanoidPoseProbe.ApplyFingerCurl(workspace,pose=="leftFingers"?"Left":"Right");
                            RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,"Validated_"+pose),false);
                            if(pose.EndsWith("Fingers",StringComparison.Ordinal)&&Array.IndexOf(Environment.GetCommandLineArgs(),"-captureHandColliders")>=0){string side=pose=="leftFingers"?"Left":"Right";RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,side+"Hand_Curled_Colliders"),true,false,side);}
                            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-auditArmIntersections")>=0)RebirthEntitySurfaceIntersectionAudit.WriteArmReport(renderer,folder,pose);
                            if(pose=="rest"||pose.EndsWith("Fingers",StringComparison.Ordinal))
                            {
                                var shapes=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);var samples=RebirthEntityColliderPoseAudit.Capture(workspace,shapes,true,true);var report=RebirthEntityColliderPoseAudit.Compare(samples,shapes);
                                File.WriteAllText(Path.Combine(folder,"Coverage_"+pose+".json"),JsonUtility.ToJson(report,true));
                                RebirthEntityColliderPoseAudit.WriteMisses(samples,shapes,Path.Combine(folder,"Misses_"+pose+".csv"));
                                Debug.Log("ARM VOLUME COVERAGE pose="+pose+" samples="+report.poses.Sum(value=>value.samples)+" outside="+report.poses.Sum(value=>value.candidateOutside));
                            }
                        }
                        void Rotate(string name,Vector3 angles){RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name).localRotation*=Quaternion.Euler(angles);}
                    }
                    finally{foreach(var pair in rotations)pair.Key.localRotation=pair.Value;}
                }
                Debug.Log("ARM VOLUME REVIEW COMPLETE: temporary mesh only; UV seams/skin/coverage remain unapproved.");
            }
            finally{renderer.sharedMesh=original;renderer.sharedMaterials=originalMaterials;foreach(var go in fingerShapes)UnityEngine.Object.DestroyImmediate(go);foreach(var pair in colliderCenters)pair.Key.center=pair.Value;foreach(var pair in colliderRotations)pair.Key.transform.localRotation=pair.Value;foreach(var pair in colliderDimensions){pair.Key.radius=pair.Value.radius;pair.Key.height=pair.Value.height;}if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate);foreach(var material in disposableMaterials)UnityEngine.Object.DestroyImmediate(material);foreach(var texture in textures)UnityEngine.Object.DestroyImmediate(texture);}
        }
        static void Compact(Mesh mesh)
        {
            var indices=mesh.triangles.Distinct().OrderBy(i=>i).ToArray();var map=indices.Select((old,index)=>(old,index)).ToDictionary(pair=>pair.old,pair=>pair.index);
            var vertices=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;var weights=mesh.boneWeights;var binds=mesh.bindposes;var submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(i=>mesh.GetTriangles(i).Select(index=>map[index]).ToArray()).ToArray();
            int before=mesh.vertexCount;mesh.Clear();mesh.vertices=indices.Select(i=>vertices[i]).ToArray();mesh.uv=indices.Select(i=>uv[i]).ToArray();mesh.normals=indices.Select(i=>normals[i]).ToArray();mesh.boneWeights=indices.Select(i=>weights[i]).ToArray();mesh.bindposes=binds;mesh.subMeshCount=submeshes.Length;
            for(int i=0;i<submeshes.Length;i++)mesh.SetTriangles(submeshes[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();Debug.Log("ARM VOLUME COMPACT vertices="+before+"->"+mesh.vertexCount+" all referenced.");
        }
    }
}
