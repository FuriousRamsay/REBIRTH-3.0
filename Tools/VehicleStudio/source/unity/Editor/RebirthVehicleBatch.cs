using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    // A concrete, LOCAL Codex→Unity bridge: call from Unity's -batchmode / -executeMethod.
    // No listening server, game modification, or automatic code execution from the game.
    public static class RebirthVehicleBatch
    {
        [Serializable] private sealed class Job
        {
            public string action, captureFile, profileAsset, fbxAsset, prefabAsset,
                baseVehiclesXml, targetVehicleName, xmlOutput, bundleOutput,
                bundleName, resultFile, donorVehicle, donorEntityClass, donorPlaceableItem,
                targetEntityClass, targetPlaceableItem, unityReceipt, specOutput, assemblyPlan, fbxAuditOutput;
            public bool updateItemMeshfile;
            public bool acknowledgeWarnings;
        }
        [Serializable] private sealed class JobResult
        {
            public bool success;
            public string action, message;
            public string runId, unityVersion;
            public string[] findings;
        }
        public static void Execute()
        {
            if(!Application.isBatchMode)
                throw new InvalidOperationException("Batch bridge only runs with Unity -batchmode.");
            string jobPath = "";
            string runId = "";
            var args = Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)
            {
                if(args[i]=="-rebirthJob") jobPath=args[i+1];
                if(args[i]=="-rebirthRunId") runId=args[i+1];
            }
            if(string.IsNullOrEmpty(jobPath) || !File.Exists(jobPath))
                throw new FileNotFoundException("Pass -rebirthJob <absolute-path-to-job.json>");
            Job job=JsonUtility.FromJson<Job>(File.ReadAllText(jobPath));
            if(job==null || string.IsNullOrEmpty(job.action)) throw new InvalidDataException("Missing action.");
            var result=new JobResult {action=job.action,success=false,
                runId=runId,unityVersion=Application.unityVersion};
            try
            {
                var profile=AssetDatabase.LoadAssetAtPath<RebirthVehicleRigProfile>(job.profileAsset);
                switch(job.action)
                {
                    case "audit-fbx":
                        var auditSource=AssetDatabase.LoadAssetAtPath<GameObject>(job.fbxAsset);
                        if(auditSource==null) throw new FileNotFoundException("FBX/prefab asset missing: "+job.fbxAsset);
                        var audit=RebirthVehicleFbxAudit.Inspect(auditSource,job.fbxAsset);
                        result.message="Wrote read-only FBX geometry audit: "+RebirthVehicleFbxAudit.WriteNew(audit,job.fbxAuditOutput);
                        result.findings=audit.warnings;
                        break;
                    case "import-capture":
                        var importedProfile=RebirthVehicleReferenceImporter.Import(job.captureFile,job.profileAsset);
                        if(!string.IsNullOrWhiteSpace(job.targetVehicleName))
                        {
                            if(!System.Text.RegularExpressions.Regex.IsMatch(job.targetVehicleName,
                                @"^[A-Za-z][A-Za-z0-9_]{2,79}$"))
                                throw new InvalidDataException("Invalid targetVehicleName on imported rig profile.");
                            importedProfile.targetVehicleName=job.targetVehicleName;
                            EditorUtility.SetDirty(importedProfile);
                        }
                        result.message="Imported reference capture into "+job.profileAsset+
                            "; authoring TARGET is "+importedProfile.targetVehicleName+
                            ". Capture geometry remains an unverified reference.";
                        break;
                    case "prepare-prefab":
                        RequireProfile(profile);
                        GameObject fbx=AssetDatabase.LoadAssetAtPath<GameObject>(job.fbxAsset);
                        if(fbx==null) throw new FileNotFoundException("Source FBX not found: "+job.fbxAsset);
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                        var workspace=RebirthVehicleRigBuilder.CreateWorkspace(fbx,profile);
                        try
                        {
                            var report=RebirthVehicleRigValidator.Inspect(workspace.transform,profile);
                            result.findings=ToStrings(report);
                            RebirthVehicleRigBuilder.SaveCleanPrefab(workspace,job.prefabAsset);
                            result.message="Created clean hierarchy prefab. See findings for calibration requirements.";
                        }
                        finally {UnityEngine.Object.DestroyImmediate(workspace.gameObject);}
                        break;
                    case "assemble-prefab":
                        RequireProfile(profile);
                        GameObject assembleFbx=AssetDatabase.LoadAssetAtPath<GameObject>(job.fbxAsset);
                        if(assembleFbx==null) throw new FileNotFoundException("Source FBX not found: "+job.fbxAsset);
                        var reviewedPlan=RebirthVehicleAssemblyPlan.Read(job.assemblyPlan);
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                        var assembleWorkspace=RebirthVehicleRigBuilder.CreateWorkspace(assembleFbx,profile);
                        try
                        {
                            var assemblyResult=RebirthVehicleAssemblyPlan.Apply(assembleWorkspace,reviewedPlan,
                                job.fbxAsset,job.profileAsset);
                            var assemblyFindings=RebirthVehicleRigValidator.Inspect(assembleWorkspace.transform,profile);
                            foreach(var f in assemblyFindings)
                                if(f.severity=="ERROR") throw new InvalidDataException("Assembly validation: "+f.ToString());
                            var allFindings=new List<string>(assemblyResult.details);
                            allFindings.AddRange(ToStrings(assemblyFindings));
                            result.findings=allFindings.ToArray();
                            RebirthVehicleRigBuilder.SaveCleanPrefab(assembleWorkspace,job.prefabAsset);
                            result.message="Built reviewed FBX assembly with "+assemblyResult.attached+
                                " attached visual groups. Motion/poses NOT verified in 7DTD.";
                        }
                        finally {UnityEngine.Object.DestroyImmediate(assembleWorkspace.gameObject);}
                        break;
                    case "wheel-map-preview":
                        RequireProfile(profile);
                        GameObject mapFbx=AssetDatabase.LoadAssetAtPath<GameObject>(job.fbxAsset);
                        if(mapFbx==null) throw new FileNotFoundException("Source FBX not found: "+job.fbxAsset);
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                        var mapWorkspace=RebirthVehicleRigBuilder.CreateWorkspace(mapFbx,profile);
                        try
                        {
                            var proposals=RebirthVehicleWheelMapper.Discover(mapWorkspace);
                            var strings=new List<string>();
                            foreach(var p in proposals)
                                strings.Add(p.xmlWheelClass+" | "+p.wheelCorner+" | "+p.outcome+
                                    " | "+p.sourceMeshPath+" -> "+p.tirePivotPath+" | "+p.explanation);
                            result.findings=strings.ToArray();
                            result.message="Read-only FBX wheel mapping preview. No visuals attached or source FBX modified.";
                        }
                        finally {UnityEngine.Object.DestroyImmediate(mapWorkspace.gameObject);}
                        break;
                    case "validate-prefab":
                        RequireProfile(profile);
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(job.prefabAsset);
                        if(prefab==null) throw new FileNotFoundException("Prefab not found: "+job.prefabAsset);
                        var report2=RebirthVehicleRigValidator.Inspect(prefab.transform,profile,true);
                        result.findings=ToStrings(report2);
                        foreach(var finding in report2)
                            if(finding.severity=="ERROR") throw new InvalidDataException(finding.ToString());
                        result.message="Structure has no validator ERRORs. Does not prove 7DTD playability.";
                        break;
                    case "export-xml":
                        RequireProfile(profile);
                        var patch=RebirthVehicleXmlExporter.ExportToFile(profile,
                            job.baseVehiclesXml,job.targetVehicleName,job.xmlOutput);
                        result.findings=patch.warnings.ToArray();
                        result.message="Generated "+patch.setCount+" sets and "+patch.appendCount+" appends.";
                        break;
                    case "export-mod-spec":
                        RequireProfile(profile);
                        result.message="Generated deployment spec: " +
                            RebirthVehicleModSpecExporter.ExportToFile(profile,
                                job.donorVehicle, job.donorEntityClass, job.donorPlaceableItem,
                                job.targetVehicleName, job.targetEntityClass, job.targetPlaceableItem,
                                job.unityReceipt, job.specOutput, job.updateItemMeshfile);
                        break;
                    case "build-bundle":
                        RequireProfile(profile);
                        var toBuild=AssetDatabase.LoadAssetAtPath<GameObject>(job.prefabAsset);
                        if(toBuild==null) throw new FileNotFoundException("No prefab to build: "+job.prefabAsset);
                        var buildFindings=RebirthVehicleRigValidator.Inspect(toBuild.transform,profile,true);
                        var buildWarnings=new List<string>();
                        foreach(var finding in buildFindings)
                        {
                            if(finding.severity=="ERROR")
                                throw new InvalidDataException("Cannot package structurally invalid prefab: "+finding);
                            if(finding.severity=="WARNING") buildWarnings.Add(finding.ToString());
                        }
                        if(buildWarnings.Count>0 && !job.acknowledgeWarnings)
                            throw new InvalidDataException("Unresolved rig warnings: "+
                                string.Join(" | ",buildWarnings.ToArray())+
                                ". Set acknowledgeWarnings=true ONLY after reviewing these findings.");
                        result.findings=ToStrings(buildFindings);
                        result.message=RebirthVehicleRigBuilder.BuildOneAssetBundle(
                            job.prefabAsset,job.bundleOutput,job.bundleName,
                            BuildTarget.StandaloneWindows64);
                        break;
                    default: throw new InvalidDataException("Unsupported action: "+job.action);
                }
                AssetDatabase.SaveAssets();
                result.success=true;
            }
            catch(Exception ex)
            {
                result.message=ex.ToString();
                Debug.LogError("REBIRTH Unity job failed: "+ex);
            }
            if(!string.IsNullOrEmpty(job.resultFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(job.resultFile)));
                // A unique, immutable result file prevents stale job success from being reused.
                using (var output = new FileStream(job.resultFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(output, new System.Text.UTF8Encoding(false)))
                    writer.Write(JsonUtility.ToJson(result,true));
            }
            Debug.Log("REBIRTH Unity job result: "+JsonUtility.ToJson(result,true));
            if(!result.success) EditorApplication.Exit(1);
        }
        private static void RequireProfile(RebirthVehicleRigProfile p)
        { if(p==null) throw new FileNotFoundException("Profile asset must exist before running this action."); }
        private static string[] ToStrings(List<RebirthVehicleRigValidator.Finding> f)
        { var s=new List<string>(); foreach(var v in f) s.Add(v.ToString()); return s.ToArray(); }
    }
}
