using System;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntitySkinnedBounds
    {
        public static void RepairSavedFarmer()
        {
            const string path="Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity";
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            RebirthEntityRigWorkspace workspace=null;
            foreach(var root in scene.GetRootGameObjects())if(root.TryGetComponent(out RebirthEntityRigWorkspace found)){workspace=found;break;}
            if(workspace==null)throw new InvalidOperationException("Saved Farmer workspace missing.");
            RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);FitRestPose(workspace);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,path))throw new InvalidOperationException("Could not save corrected skinned bounds.");
            UnityEditor.AssetDatabase.SaveAssets();Debug.Log("SAVED FARMER BOUNDS: hips frame; explicit Bone4; all body LODs");
        }
        public static void FitRestPose(RebirthEntityRigWorkspace workspace)
        {
            Transform hips=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"Hips");
            if(hips==null)throw new InvalidOperationException("Skinned bounds require Hips.");
            foreach(var renderer in workspace.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(renderer.CompareTag("E_BP_Eye")||renderer.sharedMesh==null)continue;
                // Unity's skinned culling bounds follow rootBone. Imported raw
                // mesh bounds are in a different frame for rotated/scaled FBXs.
                Bounds bounds=new Bounds();bool has=false;
                foreach(Vector3 vertex in renderer.sharedMesh.vertices)
                {
                    Vector3 point=hips.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if(!has){bounds=new Bounds(point,Vector3.zero);has=true;}else bounds.Encapsulate(point);
                }
                if(!has)throw new InvalidOperationException("Empty skinned renderer: "+renderer.name);
                bounds.Expand(workspace.profile.estimatedHeight*.08f);
                renderer.rootBone=hips;renderer.localBounds=bounds;renderer.quality=SkinQuality.Bone4;
                UnityEditor.EditorUtility.SetDirty(renderer);
            }
        }
    }
}
