using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    // Full UI workflow for the UNITY authoring stage. Does not call 7DTD runtime APIs.
    public sealed class RebirthVehicleAuthoringWindow : EditorWindow
    {
        private GameObject sourceFbx;
        private RebirthVehicleRigProfile profile;
        private RebirthVehicleRigWorkspace workspace;
        private GameObject meshVisual;
        private int selectedLink;
        private string prefabAssetPath="";
        private string baseVehiclesXml="";
        private string patchFile="";
        private string bundleName="rebirth_vehicle";
        private string bundleFolder="";
        private BuildTarget buildTarget=BuildTarget.StandaloneWindows64;
        private Vector2 scroll;
        private string lastMessage="";

        [MenuItem("Tools/REBIRTH/7DTD Vehicle Rig Authoring")]
        public static void Open() { GetWindow<RebirthVehicleAuthoringWindow>("7DTD Vehicle Rig"); }

        private void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            try
            {
                EditorGUILayout.LabelField("REBIRTH 7DTD • FBX rigging / occupants",EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Editable Unity rig scaffolding + 7DTD XML patches. NOT yet a verified " +
                    "playable vehicle. Use a Unity version compatible with your installed game.",MessageType.Info);
                Section("1 • Import runtime vehicle reference");
                profile=(RebirthVehicleRigProfile)EditorGUILayout.ObjectField("Rig profile",profile,
                    typeof(RebirthVehicleRigProfile),false);
                if(GUILayout.Button("Import 7DTD capture JSON → new profile")) Execute(ImportCapture);
                if(profile!=null)
                {
                    EditorGUILayout.LabelField("Reference",profile.sourceVehicleName + " · " + profile.sourceGameVersion);
                    EditorGUILayout.LabelField("XML association",profile.sourceXmlStatus);
                    foreach(var warning in profile.importWarnings) EditorGUILayout.HelpBox(warning,MessageType.Warning);
                }
                Section("2 • Create or edit the FBX workspace");
                sourceFbx=(GameObject)EditorGUILayout.ObjectField("Imported FBX / prefab asset",sourceFbx,
                    typeof(GameObject),false);
                if(GUILayout.Button("Create scene workspace + referenced rig hierarchy"))
                    Execute(()=> {workspace=RebirthVehicleRigBuilder.CreateWorkspace(sourceFbx,profile);
                        lastMessage="Created workspace. Align placeholder joints before exporting.";});
                workspace=(RebirthVehicleRigWorkspace)EditorGUILayout.ObjectField("Scene workspace",workspace,
                    typeof(RebirthVehicleRigWorkspace),true);
                if(workspace!=null)
                {
                    if(GUILayout.Button("Select workspace to edit seats, exits, hands and feet"))
                        Selection.activeGameObject=workspace.gameObject;
                    if(GUILayout.Button("Create / refresh missing joint paths"))
                        Execute(()=>lastMessage="Created " +
                            RebirthVehicleRigBuilder.EnsureReferenceHierarchy(workspace)+" missing path(s). Align manually.");
                }
                Section("3 • Assign physical FBX meshes to generated joints");
                EditorGUILayout.HelpBox("Optional. Explicitly choose each wheel/rotor/steering mesh and target " +
                    "joint. This unpacks the SCENE model instance (not source FBX) and reparents the selected " +
                    "geometry after aligning the pivot at its bounds center. Verify pivot orientation.",MessageType.None);
                meshVisual=(GameObject)EditorGUILayout.ObjectField("Visual to attach (scene)",meshVisual,
                    typeof(GameObject),true);
                string[] links=new string[profile==null?0:profile.links.Count];
                for(int i=0;i<links.Length;i++)
                    links[i]=profile.links[i].partClass+"."+profile.links[i].propertyName+" → "+
                        profile.links[i].transformPath;
                if(links.Length>0) selectedLink=EditorGUILayout.Popup("Target joint",Mathf.Clamp(selectedLink,0,links.Length-1),links);
                if(GUILayout.Button("Align target joint and attach selected visual"))
                    Execute(AttachSelectedVisual);
                Section("4 • Export clean Unity prefab");
                if(GUILayout.Button("Save rigged hierarchy as clean .prefab (no authoring script)"))
                    Execute(SavePrefab);
                EditorGUILayout.LabelField("Prefab",string.IsNullOrEmpty(prefabAssetPath)?"Not saved":prefabAssetPath);
                Section("5 • Export 7DTD seating/IK/wheel XML patch");
                if(profile!=null)
                {
                    string targetName=EditorGUILayout.TextField("Target existing vehicle",profile.targetVehicleName);
                    if(targetName!=profile.targetVehicleName)
                    { Undo.RecordObject(profile,"Change XML vehicle target");
                      profile.targetVehicleName=targetName; EditorUtility.SetDirty(profile); }
                }
                EditorGUILayout.BeginHorizontal();
                baseVehiclesXml=EditorGUILayout.TextField("Base vehicles.xml",baseVehiclesXml);
                if(GUILayout.Button("Browse",GUILayout.Width(65)))
                    baseVehiclesXml=EditorUtility.OpenFilePanel("Choose game's or mod's vehicles.xml", "", "xml");
                EditorGUILayout.EndHorizontal();
                if(GUILayout.Button("Export XPath patch (never overwrites base XML)")) Execute(ExportXml);
                if(!string.IsNullOrEmpty(patchFile)) EditorGUILayout.LabelField("Patch",patchFile);
                Section("6 • Build one AssetBundle for game testing");
                bundleName=EditorGUILayout.TextField("Bundle name",bundleName);
                buildTarget=(BuildTarget)EditorGUILayout.EnumPopup("Build target",buildTarget);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.TextField("Output folder",bundleFolder);
                if(GUILayout.Button("Browse",GUILayout.Width(65)))
                    bundleFolder=EditorUtility.OpenFolderPanel("AssetBundle output", "", "");
                EditorGUILayout.EndHorizontal();
                if(GUILayout.Button("Build ONE selected prefab as AssetBundle")) Execute(BuildBundle);
                EditorGUILayout.HelpBox("Bundle export alone does NOT create the full 7DTD entity/item/vehicle " +
                    "definition, physics behavior, materials or validated animation state.",MessageType.Warning);
                if(!string.IsNullOrEmpty(lastMessage)) EditorGUILayout.HelpBox(lastMessage,MessageType.Info);
            }
            finally { EditorGUILayout.EndScrollView(); }
        }
        private static void Section(string heading)
        { GUILayout.Space(10); EditorGUILayout.LabelField(heading,EditorStyles.boldLabel); }
        private void Execute(Action action)
        {
            try { action(); }
            catch(Exception e)
            {
                lastMessage="FAILED: "+e.Message;
                Debug.LogException(e);
                EditorUtility.DisplayDialog("REBIRTH vehicle authoring",lastMessage,"OK");
            }
        }
        private void ImportCapture()
        {
            string path=EditorUtility.OpenFilePanel("Select 7DTD runtime capture", "", "json");
            if(string.IsNullOrEmpty(path)) return;
            string outPath=EditorUtility.SaveFilePanelInProject("New rig profile", "VehicleRigProfile", "asset",
                "Save a dedicated rig profile inside your Unity Assets folder.");
            if(string.IsNullOrEmpty(outPath)) return;
            profile=RebirthVehicleReferenceImporter.Import(path,outPath);
            lastMessage="Imported profile from "+Path.GetFileName(path)+". Reference/coordinates are NOT automatically verified.";
        }
        private void AttachSelectedVisual()
        {
            if(workspace==null || profile==null || meshVisual==null || profile.links.Count==0)
                throw new InvalidOperationException("Choose a workspace, profile, visual and reference joint.");
            var path=profile.links[Mathf.Clamp(selectedLink,0,profile.links.Count-1)].transformPath;
            if(!EditorUtility.DisplayDialog("Unpack scene instance and attach mesh?",
                "This changes the scene workspace and may unpack its FBX prefab instance. The original " +
                "FBX asset remains unchanged. Target: "+path+". Continue?","Attach","Cancel")) return;
            RebirthVehicleRigBuilder.AttachVisualToJoint(workspace,meshVisual.transform,path);
            lastMessage="Visual attached to "+path+". Check joint axis and center before use.";
        }
        private void SavePrefab()
        {
            if(workspace==null) throw new InvalidOperationException("Create/select a workspace first.");
            string path=EditorUtility.SaveFilePanelInProject("Save game test prefab",workspace.gameObject.name,
                "prefab","The authoring MonoBehaviour is stripped during save.");
            if(string.IsNullOrEmpty(path)) return;
            prefabAssetPath=RebirthVehicleRigBuilder.SaveCleanPrefab(workspace,path);
            lastMessage="Saved clean prefab. Physics/game-specific components are NOT yet guaranteed.";
        }
        private void ExportXml()
        {
            if(profile==null || string.IsNullOrEmpty(baseVehiclesXml))
                throw new InvalidOperationException("Choose profile and base vehicles.xml first.");
            string path=EditorUtility.SaveFilePanel("Save 7DTD modlet patch", "", "REBIRTH_vehicle_patch.xml", "xml");
            if(string.IsNullOrEmpty(path)) return;
            var result=RebirthVehicleXmlExporter.ExportToFile(profile,baseVehiclesXml,
                profile.targetVehicleName,path);
            patchFile=path;
            lastMessage="XML: "+result.setCount+" set(s), "+result.appendCount+
                " append(s). Warnings: "+string.Join(" | ",result.warnings.ToArray());
        }
        private void BuildBundle()
        {
            if(string.IsNullOrEmpty(prefabAssetPath) || string.IsNullOrEmpty(bundleFolder))
                throw new InvalidOperationException("Save prefab and set output folder first.");
            if (profile == null) throw new InvalidOperationException("A rig profile is required to validate the prefab.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if (prefab == null) throw new FileNotFoundException("Rigged prefab missing: " + prefabAssetPath);
            var findings = RebirthVehicleRigValidator.Inspect(prefab.transform, profile);
            var warnings = new System.Collections.Generic.List<string>();
            foreach(var item in findings)
            {
                if(item.severity == "ERROR") throw new InvalidDataException("Prefab validation failed: " + item);
                if(item.severity == "WARNING") warnings.Add(item.ToString());
            }
            if(warnings.Count>0 && !EditorUtility.DisplayDialog("Rig warnings require review",
                string.Join("\n", warnings.ToArray()) +
                "\n\nBuild for LOCAL TESTING despite these limitations?", "Build test bundle", "Cancel")) return;
            string outFile=RebirthVehicleRigBuilder.BuildOneAssetBundle(prefabAssetPath,
                bundleFolder,bundleName,buildTarget);
            lastMessage="Built AssetBundle: "+outFile+". Must still test with the proper Unity version and game XML.";
        }
    }
}
