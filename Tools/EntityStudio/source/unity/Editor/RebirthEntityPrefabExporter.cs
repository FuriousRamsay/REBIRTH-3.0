using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    [Serializable]
    public sealed class RebirthEntityPrefabManifest
    {
        public string entityClass,targetVersion;
        public bool targetCompatibilityVerified;
        public bool gameTestCandidate;
        public bool diagnosticOnly;
        public string[] pendingFindings;
        public EntityColliderModeSpec[] colliderModes;
        public string[] tierMaterialAssetPaths,nativeTextureAssetPaths;
        public string tagMapAssetPath;
    }

    [Serializable] public sealed class RebirthEntityTagEntry {public string path,tag;}
    [Serializable] public sealed class RebirthEntityTagMap {public int version=1;public RebirthEntityTagEntry[] entries;}

    public static class RebirthEntityPrefabExporter
    {
        public static string SaveCleanPrefab(RebirthEntityRigWorkspace workspace,string assetPath)
        {return SavePrefab(workspace,assetPath,false,false);}

        public static string SaveGameTestCandidate(RebirthEntityRigWorkspace workspace,string assetPath)
        {return SavePrefab(workspace,assetPath,true,false);}

        public static string SaveDiagnosticPrefab(RebirthEntityRigWorkspace workspace,string assetPath)
        {return SavePrefab(workspace,assetPath,true,true);}

        private static string SavePrefab(RebirthEntityRigWorkspace workspace,string assetPath,bool gameTestCandidate,bool diagnosticOnly)
        {
            if(workspace==null||workspace.profile==null)throw new ArgumentException("Entity workspace/profile required.");
            if(string.IsNullOrEmpty(assetPath)||!assetPath.StartsWith("Assets/",StringComparison.Ordinal)||!assetPath.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output prefab must be Assets/.../*.prefab");
            if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)!=null)throw new IOException("Refusing to overwrite "+assetPath);
            if(workspace.colliderFitReports.Exists(fit=>fit.usesPoseUnionCoverage))RebirthEntityColliderCandidate.Verify(workspace);
            var findings=RebirthEntityRigValidator.Inspect(workspace);
            foreach(var f in findings)if(f.severity=="ERROR")throw new InvalidDataException("Export blocked: "+f);
            foreach(var f in findings)if(f.severity=="WARNING"&&!diagnosticOnly&&!(gameTestCandidate&&IsPendingReview(f.code)))throw new InvalidDataException("Export blocked until review gate is resolved: "+f);
            string parent=Path.GetDirectoryName(assetPath).Replace('\\','/');
            string meshFolder=parent+"/"+Path.GetFileNameWithoutExtension(assetPath)+"_Meshes";
            if(AssetDatabase.IsValidFolder(meshFolder))throw new IOException("Refusing to overwrite generated mesh assets in "+meshFolder);
            var clone=UnityEngine.Object.Instantiate(workspace.gameObject);
            bool meshFolderCreated=false,prefabCreated=false;
            var persistedMeshes=new System.Collections.Generic.Dictionary<Mesh,Mesh>();
            try
            {
                clone.name=workspace.profile.entityClass;
                foreach(var marker in clone.GetComponentsInChildren<RebirthEntityRigWorkspace>(true))UnityEngine.Object.DestroyImmediate(marker);
                // Mesh Simplifier keeps editor restoration data on the workspace root.
                // The generated LOD renderers/meshes are already in the hierarchy; the
                // backup component is only needed by the authoring scene and must not ship.
                foreach(var component in clone.GetComponentsInChildren<MonoBehaviour>(true))
                    if(component!=null&&component.GetType().FullName=="UnityMeshSimplifier.LODBackupComponent")
                        UnityEngine.Object.DestroyImmediate(component);
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent,Path.GetFileName(meshFolder));meshFolderCreated=true;
                foreach(var renderer in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Mesh mesh=renderer.sharedMesh;
                    if(mesh==null||!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))continue;
                    if(!persistedMeshes.TryGetValue(mesh,out Mesh saved))
                    {
                        saved=UnityEngine.Object.Instantiate(mesh);
                        saved.name=mesh.name;
                        string meshPath=AssetDatabase.GenerateUniqueAssetPath(meshFolder+"/"+Sanitize(mesh.name)+".asset");
                        AssetDatabase.CreateAsset(saved,meshPath);
                        persistedMeshes.Add(mesh,saved);
                    }
                    renderer.sharedMesh=saved;
                }
                AssetDatabase.SaveAssets();
                var prefab=PrefabUtility.SaveAsPrefabAsset(clone,assetPath);
                if(prefab==null)throw new IOException("Unity prefab save failed.");
                prefabCreated=true;
                string tagMapPath=meshFolder+"/"+workspace.profile.entityClass+"_Tags.json";
                var tagMap=new RebirthEntityTagMap{entries=clone.GetComponentsInChildren<Transform>(true).Select(node=>new RebirthEntityTagEntry{path=AnimationUtility.CalculateTransformPath(node,clone.transform),tag=node.tag}).ToArray()};
                File.WriteAllText(tagMapPath,JsonUtility.ToJson(tagMap,true));AssetDatabase.ImportAsset(tagMapPath,ImportAssetOptions.ForceSynchronousImport);
                var manifest=new RebirthEntityPrefabManifest
                {
                    entityClass=workspace.profile.entityClass,targetVersion=workspace.profile.gameVersion,
                    targetCompatibilityVerified=workspace.profile.reviewedTargetCompatibility,
                    gameTestCandidate=gameTestCandidate,
                    diagnosticOnly=diagnosticOnly,
                    tagMapAssetPath=tagMapPath,
                    pendingFindings=findings.Where(value=>value.severity=="WARNING").Select(value=>value.ToString()).ToArray(),
                    colliderModes=workspace.profile.colliderModes.ToArray(),
                    tierMaterialAssetPaths=workspace.profile.generatedTierMaterials.Where(value=>value!=null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray(),
                    nativeTextureAssetPaths=workspace.profile.nativeRmoeMaps.Where(value=>value!=null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray()
                };
                File.WriteAllText(meshFolder+"/entity.json",JsonUtility.ToJson(manifest,true));
                AssetDatabase.SaveAssets();return assetPath;
            }
            catch
            {
                if(prefabCreated)AssetDatabase.DeleteAsset(assetPath);
                if(meshFolderCreated&&AssetDatabase.IsValidFolder(meshFolder))AssetDatabase.DeleteAsset(meshFolder);
                throw;
            }
            finally{UnityEngine.Object.DestroyImmediate(clone);}
        }

        public static string ManifestPath(string prefabAssetPath)
        {return Path.GetDirectoryName(prefabAssetPath).Replace('\\','/')+"/"+Path.GetFileNameWithoutExtension(prefabAssetPath)+"_Meshes/entity.json";}

        private static bool IsPendingReview(string code)
        {return code=="TARGET_COMPATIBILITY_NEEDS_REVIEW"||code=="BASE_SKELETON_DENSITY"||code=="WEIGHTS_NEED_REVIEW"||code=="FIT_NEEDS_REVIEW"||code=="LOD_NEEDS_REVIEW"||code=="SHADER_NEEDS_REVIEW";}

        private static void EnsureFolder(string folder)
        {string[] p=folder.Split('/');string current=p[0];for(int i=1;i<p.Length;i++){string next=current+"/"+p[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,p[i]);current=next;}}

        private static string Sanitize(string value)
        {
            foreach(char invalid in Path.GetInvalidFileNameChars())value=value.Replace(invalid,'_');
            return string.IsNullOrWhiteSpace(value)?"EntityMesh":value;
        }
    }

    public sealed class RebirthEntityPrefabExportWindow : EditorWindow
    {
        private RebirthEntityRigWorkspace workspace;private string outputName="Zombie_Custom.prefab";private string result="";
        [MenuItem("Tools/REBIRTH/Export Entity Prefab")]
        public static void Open(){GetWindow<RebirthEntityPrefabExportWindow>("Entity Export");}
        private void OnGUI()
        {
            workspace=(RebirthEntityRigWorkspace)EditorGUILayout.ObjectField("Workspace",workspace,typeof(RebirthEntityRigWorkspace),true);
            outputName=EditorGUILayout.TextField("Prefab name",outputName);
            EditorGUILayout.HelpBox("Export refuses any validation errors or unreviewed skeleton, collider, LOD, or material gates. Target-game compatibility still requires a live 7DTD test.",MessageType.Warning);
            if(GUILayout.Button("Validate and save new clean prefab"))
            {
                string path=EditorUtility.SaveFilePanelInProject("Export entity prefab",outputName,"prefab","Choose a new output path.");
                if(string.IsNullOrEmpty(path))return;
                try{result=RebirthEntityPrefabExporter.SaveCleanPrefab(workspace,path);}
                catch(Exception ex){result=ex.Message;Debug.LogException(ex);}
            }
            EditorGUILayout.HelpBox(result,MessageType.None);
        }
    }
}
