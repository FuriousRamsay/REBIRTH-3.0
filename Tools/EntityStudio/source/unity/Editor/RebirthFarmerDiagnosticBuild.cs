using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    // An unfinished asset can be exercised in the target game without converting
    // unresolved authoring findings into approvals. Production gates stay strict.
    public static class RebirthFarmerDiagnosticBuild
    {
        public static void Run()
        {
            try
            {
                string[] args=Environment.GetCommandLineArgs();
                string Argument(string key){int index=Array.IndexOf(args,key);if(index<0||index+1>=args.Length)throw new ArgumentException("Missing "+key);return args[index+1];}
                string output=Argument("-entityStudioCandidateOutput"),config=Argument("-entityStudioVanillaEntityConfig");
                var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
                var workspace=scene.GetRootGameObjects().Select(root=>root.GetComponent<RebirthEntityRigWorkspace>()).Single(value=>value!=null);
                if(RebirthEntityRigValidator.HasStaleInputs(workspace))throw new InvalidDataException("Diagnostic source/profile inputs are stale; rebuild before export.");
                RebirthEntityEyeGeometry.ReconcileMaterialInventory(workspace);
                if(workspace.colliderFitReports.Any(fit=>fit.usesPoseUnionCoverage))RebirthEntityColliderCandidate.Verify(workspace);
                workspace.profileBuildFingerprint=RebirthEntityRigBuilder.ComputeProfileFingerprint(workspace.profile,workspace.sourceModelKey);
                UnityEditor.AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                var findings=RebirthEntityRigValidator.Inspect(workspace);
                if(findings.Any(value=>value.severity=="ERROR"))throw new InvalidDataException(string.Join("\n",findings.Where(value=>value.severity=="ERROR")));
                string prefab="Assets/EntityStudioGenerated/Diagnostics/"+Guid.NewGuid().ToString("N")+"/RebirthFarmer.prefab";
                RebirthEntityPrefabExporter.SaveDiagnosticPrefab(workspace,prefab);
                var receipt=RebirthEntityBundleBuilder.BuildAndVerify(prefab,output);
                receipt.status="DIAGNOSTIC_ONLY_NOT_GAME_READY";
                File.WriteAllText(Path.Combine(output,"diagnostic-receipt.json"),JsonUtility.ToJson(receipt,true));
                File.WriteAllLines(Path.Combine(output,"unresolved-authoring-findings.txt"),findings.Select(value=>value.ToString()));
                File.WriteAllText(Path.Combine(output,"entityclasses.xml"),RebirthFarmerEntityConfig.BuildPatch(receipt,config));
                File.WriteAllText(Path.Combine(output,"DIAGNOSTIC_ONLY.txt"),"Unfinished RebirthFarmer for supervised Game Bridge verification. Hand deformation and collider overreach remain unresolved. No review approvals were set. This bundle is not the final game-ready deliverable.");
                Debug.Log("FARMER DIAGNOSTIC BUNDLE: "+output);
            }
            catch(Exception error){Debug.LogException(error);UnityEditor.EditorApplication.Exit(1);}
        }
    }
}
