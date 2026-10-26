using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityLodVisualReview
    {
        public static void RunSavedFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            var sources=workspace.sourceSkinnedMeshes;var rotations=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localRotation);
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerCurrentLods");Directory.CreateDirectory(folder);
            var rows=new List<string>{"level,transitionHeight,renderers,triangles,vertices,invalidWeights,missingBones"};
            try
            {
                var levels=workspace.lodGroup.GetLODs();
                for(int level=0;level<levels.Length;level++)
                {
                    var renderers=levels[level].renderers.OfType<SkinnedMeshRenderer>().Distinct().ToList();int triangles=0,vertices=0,invalid=0,missing=0;
                    foreach(var renderer in renderers)
                    {
                        var mesh=renderer.sharedMesh;triangles+=mesh.triangles.Length/3;vertices+=mesh.vertexCount;
                        missing+=renderer.bones.Count(bone=>bone==null);
                        foreach(var w in mesh.boneWeights)
                        {
                            float sum=w.weight0+w.weight1+w.weight2+w.weight3;
                            if(float.IsNaN(sum)||Mathf.Abs(sum-1)>.0001f||Mathf.Min(w.weight0,w.weight1,w.weight2,w.weight3)<0)invalid++;
                            if((w.weight0>0&&w.boneIndex0>=renderer.bones.Length)||(w.weight1>0&&w.boneIndex1>=renderer.bones.Length)||(w.weight2>0&&w.boneIndex2>=renderer.bones.Length)||(w.weight3>0&&w.boneIndex3>=renderer.bones.Length))invalid++;
                        }
                    }
                    rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2},{3},{4},{5},{6}",level,levels[level].screenRelativeTransitionHeight,renderers.Count,triangles,vertices,invalid,missing));
                    if(invalid>0||missing>0)throw new InvalidOperationException("Invalid LOD skin level="+level);
                    workspace.sourceSkinnedMeshes=renderers;
                    foreach(string pose in new[]{"rest","reach","crouch"})
                    {
                        foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                        if(pose=="reach")foreach(string side in new[]{"Left","Right"}){Rotate(side+"ForeArm",new Vector3(70,0,0));Rotate(side+"Arm",new Vector3(25,0,side=="Left"?-35:35));}
                        if(pose=="crouch")foreach(string side in new[]{"Left","Right"}){Rotate(side+"UpLeg",new Vector3(-45,0,0));Rotate(side+"Leg",new Vector3(90,0,0));}
                        RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,"LOD"+level+"_"+pose),false);
                    }
                }
                File.WriteAllLines(Path.Combine(folder,"SkinInventory.csv"),rows);Debug.Log("LOD VISUAL REVIEW CAPTURED: "+levels.Length+" levels x3poses; no assets saved.");
            }
            finally{workspace.sourceSkinnedMeshes=sources;foreach(var pair in rotations)pair.Key.localRotation=pair.Value;}
            void Rotate(string name,Vector3 angle){var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);if(bone==null)throw new InvalidOperationException("Missing pose bone "+name);bone.localRotation*=Quaternion.Euler(angle);}
        }
    }
}
