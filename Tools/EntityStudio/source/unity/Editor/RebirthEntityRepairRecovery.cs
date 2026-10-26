using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityRepairRecovery
    {
        // Older scenes did not serialize repair provenance. Recreate it from
        // the actual source, requiring identical indexed topology and T-pose
        // positions before attaching any correspondence to the saved mesh.
        public static void RecoverSavedFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var saved=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            var profile=UnityEngine.Object.Instantiate(saved.profile);
            profile.generateLods=false;profile.generateTierMaterials=false;
            RebirthEntityRigWorkspace candidate=null;Avatar avatar=null;
            var meshes=new List<Mesh>();
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(saved.sourceModelAssetPath);
                if(source==null)throw new InvalidOperationException("Original model unavailable.");
                candidate=RebirthEntityRigBuilder.CreateWorkspace(source,profile,false);
                foreach(var renderer in candidate.sourceSkinnedMeshes)meshes.Add(renderer.sharedMesh);
                avatar=RebirthEntityHumanoidBuilder.Build(candidate,null,true);
                var recovered=new List<EntitySurfaceRepairInput>();
                if(candidate.sourceSkinnedMeshes.Count!=saved.sourceSkinnedMeshes.Count)throw new InvalidOperationException("Renderer count changed.");
                int pointsChecked=0;
                for(int r=0;r<saved.sourceSkinnedMeshes.Count;r++)
                {
                    var target=saved.sourceSkinnedMeshes[r];var generated=candidate.sourceSkinnedMeshes[r];
                    Mesh a=target.sharedMesh,b=generated.sharedMesh;
                    if(a.vertexCount!=b.vertexCount||!a.triangles.SequenceEqual(b.triangles))throw new InvalidOperationException("Repair topology mismatch: "+target.name);
                    Vector3[] av=a.vertices,bv=b.vertices;
                    for(int i=0;i<av.Length;i++)
                    {
                        if(Vector3.Distance(target.transform.TransformPoint(av[i]),generated.transform.TransformPoint(bv[i]))>.00001f)
                            throw new InvalidOperationException("Repair position mismatch: "+target.name+" vertex="+i);
                        pointsChecked++;
                    }
                    var input=candidate.surfaceRepairInputs.Single(value=>value.repairedMesh==b);
                    if(input.repairVertexIds.Length!=a.vertexCount||input.repairVertexParts.Length!=a.vertexCount)throw new InvalidOperationException("Incomplete repair provenance.");
                    input.repairedMesh=a;recovered.Add(input);
                }
                // Commit only after every renderer has matched. Existing meshes,
                // bones, colliders, materials and proof signatures are untouched.
                UnityEngine.Object.DestroyImmediate(candidate.gameObject);candidate=null;
                saved.surfaceRepairInputs=recovered;EditorUtility.SetDirty(saved);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save repair provenance.");
                var reloadedScene=EditorSceneManager.OpenScene(scene.path);
                var reloaded=reloadedScene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
                if(reloaded.surfaceRepairInputs.Count!=recovered.Count)throw new InvalidOperationException("Repair provenance failed reload.");
                for(int i=0;i<recovered.Count;i++)
                    if(reloaded.surfaceRepairInputs[i].repairedMesh!=recovered[i].repairedMesh||
                        !reloaded.surfaceRepairInputs[i].repairVertexIds.SequenceEqual(recovered[i].repairVertexIds)||
                        !reloaded.surfaceRepairInputs[i].repairVertexParts.SequenceEqual(recovered[i].repairVertexParts))
                        throw new InvalidOperationException("Repair correspondence changed on reload.");
                Debug.Log("REPAIR PROVENANCE RECOVERED: renderers="+recovered.Count+" verifiedPositions="+pointsChecked);
            }
            finally
            {
                if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate.gameObject);
                foreach(var mesh in meshes)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                if(avatar!=null&&!EditorUtility.IsPersistent(avatar))UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
