using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    [Serializable]
    public sealed class RebirthEntityBundleReceipt
    {
        public string status="BUNDLE_RELOADED_NOT_GAME_TESTED";
        public string unityVersion;
        public string buildTarget;
        public string prefabAssetPath;
        public string bundlePath;
        public string assetName;
        public string bundleReference;
        public string sha256;
        public uint crc;
        public int skinnedRendererCount;
        public int distinctBoneCount;
        public int skinnedVertexCount;
        public int materialSlotCount;
        public int lodGroupCount;
        public int[] lodTriangles;
        public int tierMaterialCount;
        public string[] tierMaterialReferences;
        public int capsuleColliderCount;
        public int boxColliderCount;
        public int colliderCount;
        public int rigidbodyCount;
        public int jointCount;
        public int runtimeScriptCount;
        public string[] shaderNames;
        public string physicsBodyName;
        public string[] extraTextureReferences;
        public bool gameTestCandidate;
        public bool diagnosticOnly;
        public string tagMapReference;
        public string[] pendingAuthoringFindings;
    }

    /// <summary>Builds a 7DTD-style Windows AssetBundle and audits the prefab after reloading the bundle.</summary>
    public static class RebirthEntityBundleBuilder
    {
        public static RebirthEntityBundleReceipt BuildAndVerify(string prefabAssetPath,string outputDirectory,string[] extraTexturePaths=null)
        {
            if(string.IsNullOrWhiteSpace(prefabAssetPath)||!prefabAssetPath.StartsWith("Assets/",StringComparison.Ordinal)||
               !prefabAssetPath.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Prefab must be an imported Assets/.../*.prefab asset.","prefabAssetPath");
            if(string.IsNullOrWhiteSpace(outputDirectory))throw new ArgumentException("Choose a new bundle output directory.","outputDirectory");
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if(prefab==null)throw new FileNotFoundException("Unity prefab asset was not found.",prefabAssetPath);

            string fullOutput=Path.GetFullPath(outputDirectory);
            string manifestPath=RebirthEntityPrefabExporter.ManifestPath(prefabAssetPath);
            var authoringManifest=File.Exists(manifestPath)?JsonUtility.FromJson<RebirthEntityPrefabManifest>(File.ReadAllText(manifestPath)):null;
            if(extraTexturePaths==null&&authoringManifest!=null)extraTexturePaths=authoringManifest.nativeTextureAssetPaths;
            extraTexturePaths=(extraTexturePaths??Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            foreach(string path in extraTexturePaths)if(AssetDatabase.LoadAssetAtPath<Texture2D>(path)==null)throw new InvalidDataException("Extra bundle texture is missing: "+path);
            string assetName=Path.GetFileNameWithoutExtension(prefabAssetPath);
            string tierMaterialFolder="Assets/EntityMaterials/"+assetName;
            string[] tierMaterialPaths=authoringManifest!=null?authoringManifest.tierMaterialAssetPaths:AssetDatabase.IsValidFolder(tierMaterialFolder)
                ?AssetDatabase.FindAssets("t:Material",new[]{tierMaterialFolder}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p,StringComparer.Ordinal).ToArray()
                :Array.Empty<string>();
            string[] tierMaterialNames=tierMaterialPaths.Select(p=>AssetDatabase.LoadAssetAtPath<Material>(p))
                .Where(m=>m!=null).Select(m=>m.name).ToArray();
            if(tierMaterialNames.Length!=tierMaterialPaths.Length)
                throw new InvalidDataException("A generated tier material asset could not be loaded from "+tierMaterialFolder+".");
            if(tierMaterialNames.Distinct(StringComparer.Ordinal).Count()!=tierMaterialNames.Length)
                throw new InvalidDataException("Generated tier material names must be unique for AssetBundle references.");
            string bundleFile="rebirth_"+Sanitize(assetName)+".unity3d";
            string bundlePath=Path.Combine(fullOutput,bundleFile);
            string receiptPath=Path.Combine(fullOutput,"receipt.json");
            if(File.Exists(bundlePath)||File.Exists(receiptPath))
                throw new IOException("Preserve the existing bundle candidate; choose a new output directory.");
            if(Directory.Exists(fullOutput)&&Directory.EnumerateFileSystemEntries(fullOutput).Any())
                throw new IOException("Bundle output directory must be empty: "+fullOutput);

            Directory.CreateDirectory(fullOutput);
            string staging=Path.Combine(fullOutput,"UncompressedSource");
            Directory.CreateDirectory(staging);
            string[] tagMapPaths=authoringManifest!=null&&!string.IsNullOrEmpty(authoringManifest.tagMapAssetPath)?new[]{authoringManifest.tagMapAssetPath}:Array.Empty<string>();
            var build=new AssetBundleBuild{assetBundleName=bundleFile,assetNames=new[]{prefabAssetPath}.Concat(tierMaterialPaths).Concat(extraTexturePaths).Concat(tagMapPaths).Distinct(StringComparer.Ordinal).ToArray()};
            AssetBundleManifest manifest=BuildPipeline.BuildAssetBundles(staging,new[]{build},BuildAssetBundleOptions.UncompressedAssetBundle,BuildTarget.StandaloneWindows64);
            if(manifest==null||!manifest.GetAllAssetBundles().Contains(bundleFile,StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Unity did not produce the requested entity AssetBundle.");

            string uncompressed=Path.Combine(staging,bundleFile);
            if(!File.Exists(uncompressed))throw new FileNotFoundException("Uncompressed AssetBundle output is missing.",uncompressed);
            if(!BuildPipeline.GetCRCForAssetBundle(uncompressed,out uint crc))
                throw new InvalidDataException("Unity did not produce a CRC for the AssetBundle.");
            AssetBundleRecompressOperation operation=AssetBundle.RecompressAssetBundleAsync(uncompressed,bundlePath,BuildCompression.LZ4Runtime,crc,UnityEngine.ThreadPriority.Normal);
            DateTime deadline=DateTime.UtcNow.AddMinutes(12);
            while(!operation.isDone)
            {
                if(DateTime.UtcNow>deadline)throw new TimeoutException("Timed out recompressing the entity AssetBundle.");
                System.Threading.Thread.Sleep(20);
            }
            if(operation.result!=AssetBundleLoadResult.Success)
                throw new InvalidDataException("AssetBundle recompression failed: "+operation.humanReadableResult);

            // Vehicle Studio uses the source bundle CRC for the LZ4Runtime output.
            // Some Unity Editor builds omit the AssetBundle runtime module and return null here;
            // that is an Editor capability failure, not a malformed entity prefab.
            AssetBundle loaded=AssetBundle.LoadFromFile(bundlePath,crc);
            if(loaded==null)throw new InvalidDataException("The recompressed bundle failed CRC reload. Verify that this Unity Editor build has AssetBundle support enabled.");
            try
            {
                GameObject bundledPrefab=loaded.LoadAsset<GameObject>(assetName);
                if(bundledPrefab==null)throw new InvalidDataException("Bundle does not contain prefab entry '"+assetName+"'.");
                foreach(string path in tagMapPaths)
                {
                    TextAsset text=loaded.LoadAsset<TextAsset>(Path.GetFileNameWithoutExtension(path));
                    if(text==null)throw new InvalidDataException("Bundle tag map reload failed.");
                    var map=JsonUtility.FromJson<RebirthEntityTagMap>(text.text);
                    if(map==null||map.version!=1||map.entries==null||map.entries.Length!=bundledPrefab.GetComponentsInChildren<Transform>(true).Length)
                        throw new InvalidDataException("Bundle tag map does not cover the owned hierarchy.");
                    var paths=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                    foreach(var entry in map.entries)
                    {
                        Transform node=string.IsNullOrEmpty(entry.path)?bundledPrefab.transform:bundledPrefab.transform.Find(entry.path);
                        if(node==null||string.IsNullOrEmpty(entry.tag)||!paths.Add(entry.path)||node.tag!=entry.tag)
                            throw new InvalidDataException("Bundle tag map path/tag correspondence failed: "+entry.path);
                    }
                }
                foreach(string path in extraTexturePaths)
                {string name=AssetDatabase.LoadAssetAtPath<Texture2D>(path).name;if(loaded.LoadAsset<Texture2D>(name)==null)throw new InvalidDataException("Bundle texture reload failed: "+name);}
                foreach(string materialName in tierMaterialNames)
                {
                    Material bundledMaterial=loaded.LoadAsset<Material>(materialName);
                    if(bundledMaterial==null)throw new InvalidDataException("Bundle does not contain generated tier material '"+materialName+"'.");
                }
                var marker=bundledPrefab.GetComponentInChildren<RebirthEntityRigWorkspace>(true);
                if(marker!=null)throw new InvalidDataException("Editor workspace marker leaked into the packaged prefab.");
                if(bundledPrefab.tag!="E_Enemy")throw new InvalidDataException("Packaged entity root is not tagged E_Enemy.");

                SkinnedMeshRenderer[] renderers=bundledPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(renderers.Length==0)throw new InvalidDataException("Packaged prefab has no SkinnedMeshRenderer.");
                var bones=new System.Collections.Generic.HashSet<Transform>();
                int vertices=0,slots=0;
                foreach(SkinnedMeshRenderer renderer in renderers)
                {
                    Mesh mesh=renderer.sharedMesh;
                    if(mesh==null||renderer.rootBone==null||renderer.bones==null||renderer.bones.Length==0)
                        throw new InvalidDataException("Packaged skinned renderer has missing mesh, root bone, or bone references: "+renderer.name);
                    BoneWeight[] weights=mesh.boneWeights;
                    if(weights.Length!=mesh.vertexCount||mesh.bindposes.Length!=renderer.bones.Length)
                        throw new InvalidDataException("Packaged mesh weights or bind poses do not match its vertices/bones: "+renderer.name);
                    for(int i=0;i<weights.Length;i++)
                    {
                        BoneWeight weight=weights[i];
                        float sum=weight.weight0+weight.weight1+weight.weight2+weight.weight3;
                        if(float.IsNaN(sum)||float.IsInfinity(sum)||Mathf.Abs(sum-1f)>0.001f||
                           !ValidIndex(weight.boneIndex0,weight.weight0,renderer.bones.Length)||
                           !ValidIndex(weight.boneIndex1,weight.weight1,renderer.bones.Length)||
                           !ValidIndex(weight.boneIndex2,weight.weight2,renderer.bones.Length)||
                           !ValidIndex(weight.boneIndex3,weight.weight3,renderer.bones.Length))
                            throw new InvalidDataException("Packaged mesh contains invalid skin weights: "+renderer.name+" vertex "+i);
                    }
                    foreach(Transform bone in renderer.bones)
                    {
                        if(bone==null||(!bone.IsChildOf(bundledPrefab.transform)&&bone!=bundledPrefab.transform))
                            throw new InvalidDataException("Packaged renderer bone is missing or outside the entity prefab: "+renderer.name);
                        bones.Add(bone);
                    }
                    vertices+=mesh.vertexCount;
                    slots+=renderer.sharedMaterials.Length;
                }

                LODGroup[] groups=bundledPrefab.GetComponentsInChildren<LODGroup>(true);
                int[] lodTriangles=groups.Length==1?CountLodTriangles(groups[0]):Array.Empty<int>();
                CapsuleCollider[] capsules=bundledPrefab.GetComponentsInChildren<CapsuleCollider>(true);
                Rigidbody[] bodies=bundledPrefab.GetComponentsInChildren<Rigidbody>(true);
                Joint[] joints=bundledPrefab.GetComponentsInChildren<Joint>(true);
                MonoBehaviour[] scripts=bundledPrefab.GetComponentsInChildren<MonoBehaviour>(true);
                if(scripts.Length>0)
                {
                    string[] scriptNames=scripts.Select(s=>s==null?"<missing script>":s.GetType().FullName+" on "+s.gameObject.name).ToArray();
                    throw new InvalidDataException("Runtime MonoBehaviour scripts are not allowed in the packaged entity prefab: "+string.Join(", ",scriptNames));
                }
                string[] shaders=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.shader!=null)
                    .Select(m=>m.shader.name).Distinct().OrderBy(n=>n,StringComparer.Ordinal).ToArray();
                string hash;
                using(var sha=SHA256.Create())using(var stream=File.OpenRead(bundlePath))
                    hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",string.Empty).ToLowerInvariant();

                var receipt=new RebirthEntityBundleReceipt
                {
                    unityVersion=Application.unityVersion,
                    buildTarget="StandaloneWindows64",
                    prefabAssetPath=prefabAssetPath,
                    bundlePath=bundlePath,
                    assetName=assetName,
                    bundleReference="#@modfolder:Resources/"+bundleFile+"?"+assetName,
                    crc=crc,
                    sha256=hash,
                    skinnedRendererCount=renderers.Length,
                    distinctBoneCount=bones.Count,
                    skinnedVertexCount=vertices,
                    materialSlotCount=slots,
                    lodGroupCount=groups.Length,
                    lodTriangles=lodTriangles,
                    tierMaterialCount=tierMaterialNames.Length,
                    tierMaterialReferences=tierMaterialNames.Select(n=>"#@modfolder:Resources/"+bundleFile+"?"+n).ToArray(),
                    capsuleColliderCount=capsules.Length,
                    boxColliderCount=bundledPrefab.GetComponentsInChildren<BoxCollider>(true).Length,
                    colliderCount=bundledPrefab.GetComponentsInChildren<Collider>(true).Length,
                    rigidbodyCount=bodies.Length,
                    jointCount=joints.Length,
                    runtimeScriptCount=scripts.Length,
                    shaderNames=shaders
                };
                receipt.gameTestCandidate=authoringManifest!=null&&authoringManifest.gameTestCandidate;
                receipt.diagnosticOnly=authoringManifest!=null&&authoringManifest.diagnosticOnly;
                receipt.tagMapReference=tagMapPaths.Length>0?"#@modfolder:Resources/"+bundleFile+"?"+Path.GetFileNameWithoutExtension(tagMapPaths[0]):null;
                if(receipt.diagnosticOnly)receipt.status="DIAGNOSTIC_ONLY_NOT_GAME_READY";
                receipt.pendingAuthoringFindings=authoringManifest!=null?authoringManifest.pendingFindings:Array.Empty<string>();
                receipt.extraTextureReferences=extraTexturePaths.Select(path=>"#@modfolder:Resources/"+bundleFile+"?"+AssetDatabase.LoadAssetAtPath<Texture2D>(path).name).ToArray();
                Transform skeleton=bundledPrefab.transform.Find("Origin");
                File.WriteAllText(Path.Combine(fullOutput,"physicsbodies.xml"),RebirthEntityPhysicsLayoutExporter.BuildPatch(skeleton,assetName,authoringManifest!=null?authoringManifest.colliderModes:null));
                receipt.physicsBodyName=assetName+"Physics";
                File.WriteAllText(receiptPath,JsonUtility.ToJson(receipt,true));
                return receipt;
            }
            finally{loaded.Unload(true);}
        }

        private static bool ValidIndex(int index,float weight,int count)
        {return weight<=0f||(index>=0&&index<count);}

        private static int[] CountLodTriangles(LODGroup group)
        {
            LOD[] levels=group.GetLODs();
            var result=new int[levels.Length];
            for(int i=0;i<levels.Length;i++)
            {
                foreach(Renderer renderer in levels[i].renderers)
                {
                    var skinned=renderer as SkinnedMeshRenderer;
                    if(skinned==null||skinned.sharedMesh==null)continue;
                    for(int sub=0;sub<skinned.sharedMesh.subMeshCount;sub++)
                        result[i]+=skinned.sharedMesh.GetTriangles(sub).Length/3;
                }
                if(result[i]<=0||(i>0&&result[i]>=result[i-1]))
                    throw new InvalidDataException("Packaged LOD levels are empty or do not reduce triangle count at LOD"+i+".");
            }
            return result;
        }

        private static string Sanitize(string value)
        {
            if(string.IsNullOrWhiteSpace(value))throw new ArgumentException("Prefab entry name is empty.");
            foreach(char invalid in Path.GetInvalidFileNameChars())value=value.Replace(invalid,'_');
            return value.ToLowerInvariant();
        }
    }

    public sealed class RebirthEntityBundleExportWindow : EditorWindow
    {
        private GameObject prefab;
        private string outputDirectory="";
        private string status="Select a reviewed clean prefab and a new output directory.";

        [MenuItem("Tools/REBIRTH/Build Entity AssetBundle")]
        public static void Open(){GetWindow<RebirthEntityBundleExportWindow>("Entity Bundle");}

        private void OnGUI()
        {
            prefab=(GameObject)EditorGUILayout.ObjectField("Clean prefab",prefab,typeof(GameObject),false);
            using(new EditorGUILayout.HorizontalScope())
            {
                outputDirectory=EditorGUILayout.TextField("Output directory",outputDirectory);
                if(GUILayout.Button("Browse",GUILayout.Width(72)))
                {
                    string selected=EditorUtility.OpenFolderPanel("Choose a new bundle output directory",string.IsNullOrEmpty(outputDirectory)?Application.dataPath:outputDirectory,"");
                    if(!string.IsNullOrEmpty(selected))outputDirectory=selected;
                }
            }
            EditorGUILayout.HelpBox("Builds a Windows64 AssetBundle, recompresses it to LZ4 with CRC, reloads and audits the packaged prefab, and writes a SHA-256 receipt. The receipt includes the 7DTD #@modfolder bundle reference. It does not prove the prefab works in game.",MessageType.Info);
            string prefabPath=prefab!=null?AssetDatabase.GetAssetPath(prefab):string.Empty;
            bool validPrefab=prefab!=null&&prefabPath.StartsWith("Assets/",StringComparison.Ordinal)&&prefabPath.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase);
            using(new EditorGUI.DisabledScope(!validPrefab||string.IsNullOrWhiteSpace(outputDirectory)))
            {
                if(GUILayout.Button("Build, CRC reload and audit bundle"))
                {
                    try
                    {
                        RebirthEntityBundleReceipt receipt=RebirthEntityBundleBuilder.BuildAndVerify(prefabPath,outputDirectory);
                        status="Bundle reloaded. Reference: "+receipt.bundleReference+"\nSHA-256: "+receipt.sha256+"\nReceipt: "+Path.Combine(outputDirectory,"receipt.json");
                    }
                    catch(Exception ex){status=ex.Message;Debug.LogException(ex);}
                }
            }
            EditorGUILayout.HelpBox(status,MessageType.None);
        }
    }
}
