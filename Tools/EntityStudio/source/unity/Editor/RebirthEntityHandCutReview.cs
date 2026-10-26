using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityHandCutReview
    {
        public static void RunSavedFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var saved=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(saved.sourceModelAssetPath);
            bool wrist=Array.IndexOf(Environment.GetCommandLineArgs(),"-reviewWristCut")>=0;
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/"+(wrist?"FarmerWristCut":"FarmerHandCut"));Directory.CreateDirectory(folder);
            saved.gameObject.SetActive(false);
            try
            {
                foreach(float inset in new[]{0f,.025f,.045f})
                {
                    var profile=UnityEngine.Object.Instantiate(saved.profile);if(wrist)profile.wristCutInset=inset;else profile.handCutInset=inset;profile.generateLods=false;profile.generateTierMaterials=false;
                    RebirthEntityRigWorkspace candidate=null;Avatar avatar=null;Mesh[] meshes=null;
                    try
                    {
                        candidate=RebirthEntityRigBuilder.CreateWorkspace(model,profile,false);meshes=candidate.sourceSkinnedMeshes.Select(r=>r.sharedMesh).ToArray();
                        avatar=RebirthEntityHumanoidBuilder.Build(candidate,null,true);
                        foreach(var renderer in candidate.sourceSkinnedMeshes)renderer.sharedMaterials=saved.sourceSkinnedMeshes[0].sharedMaterials;
                        RebirthFarmerReviewCapture.CaptureColliderCandidate(candidate,Path.Combine(folder,"Inset"+Mathf.RoundToInt(inset*1000)),false,true);
                        Debug.Log("HAND CUT REVIEW inset="+inset+" vertices="+meshes.Sum(m=>m.vertexCount)+" no assets saved.");
                    }
                    finally{if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate.gameObject);if(avatar!=null)UnityEngine.Object.DestroyImmediate(avatar);if(meshes!=null)foreach(var mesh in meshes)if(!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(profile);}
                }
            }
            finally{saved.gameObject.SetActive(true);}
        }
    }
}
