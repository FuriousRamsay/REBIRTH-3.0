using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthFarmerPhysicsRefit
    {
        public static void Run()
        {
            const string path="Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity";
            var scene=EditorSceneManager.OpenScene(path);RebirthEntityRigWorkspace workspace=null;
            foreach(var root in scene.GetRootGameObjects())if(root.TryGetComponent(out RebirthEntityRigWorkspace found)){workspace=found;break;}
            if(workspace==null)throw new InvalidOperationException("Saved Farmer workspace missing.");
            workspace.profile.compoundTerminalColliders=true;
            RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);RebirthEntityRigBuilder.BuildPhysics(workspace);RebirthFarmerNativePhysics.Configure(workspace);RebirthEntitySkinnedBounds.FitRestPose(workspace);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("Cannot save Farmer physics refit.");AssetDatabase.SaveAssets();
            string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/RebirthFarmerReview");Directory.CreateDirectory(output);
            RebirthEntityCoverageReview.Write(workspace,Path.Combine(output,"RebirthFarmer_Coverage.json"));
            File.WriteAllText(Path.Combine(output,"RebirthFarmer_PhysicsBodies.xml"),RebirthEntityPhysicsLayoutExporter.BuildPatch(workspace.generatedSkeleton,workspace.profile.entityClass,workspace.profile.colliderModes.ToArray()));
            var temporary=new List<GameObject>();try{File.WriteAllText(Path.Combine(output,"RebirthFarmer_PhysicsReview.json"),JsonUtility.ToJson(RebirthEntityPhysicsReview.Simulate(workspace,temporary),true));}finally{foreach(var item in temporary)UnityEngine.Object.DestroyImmediate(item);}
            Debug.Log("FARMER PHYSICS REFIT COMPLETE: "+output);
        }
    }
}
