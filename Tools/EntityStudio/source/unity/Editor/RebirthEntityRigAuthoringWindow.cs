using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public sealed class RebirthEntityRigAuthoringWindow : EditorWindow
    {
        private GameObject source;
        private RebirthEntityRigProfile profile;
        private RebirthEntityRigWorkspace workspace;
        private Vector2 scroll;
        private string status="Ready";
        private bool showBoneFitPreview=true;

        private void OnEnable(){SceneView.duringSceneGui+=DrawBoneFitPreview;}
        private void OnDisable(){SceneView.duringSceneGui-=DrawBoneFitPreview;}

        [MenuItem("Tools/REBIRTH/7DTD Entity Rig Authoring")]
        public static void Open(){GetWindow<RebirthEntityRigAuthoringWindow>("7DTD Entity Rig");}

        private void OnGUI()
        {
            RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("REBIRTH • ENTITY RIG AUTHORING",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates a review workspace from an unrigged model with one or more static mesh children. It estimates skin weights from bone segments and fits collider capsules to weighted mesh vertices. Review the skeleton map, deformation, collider coverage, LOD silhouettes, shader channels and tier effects against the exact target game assets before export.",MessageType.Warning);
            profile=(RebirthEntityRigProfile)EditorGUILayout.ObjectField("Entity profile",profile,typeof(RebirthEntityRigProfile),false);
            if(GUILayout.Button("Create new entity rig profile"))CreateProfile();
            if(profile!=null)
            {
                if(profile.EnsureBoneDeformFlags())EditorUtility.SetDirty(profile);
                EditorGUILayout.LabelField("Target",profile.gameVersion+" • Unity "+profile.unityVersion);
                profile.entityClass=EditorGUILayout.TextField("Entity class",profile.entityClass);
                profile.prefabFamily=EditorGUILayout.TextField("Prefab family",profile.prefabFamily);
                profile.sourceUpAxis=(EntityModelUpAxis)EditorGUILayout.EnumPopup("Imported model up axis",profile.sourceUpAxis);
                profile.targetHeight=EditorGUILayout.FloatField("Target height (0 preserves size)",profile.targetHeight);
                profile.useSurfaceGeodesicWeights=EditorGUILayout.Toggle("Surface distance skin weights",profile.useSurfaceGeodesicWeights);
                profile.separateFusedArms=EditorGUILayout.Toggle("Repair RebirthFarmer fused arms",profile.separateFusedArms);
                profile.wrapMissingTorsoTexture=EditorGUILayout.Toggle("Wrap rear clothing onto missing torso sides",profile.wrapMissingTorsoTexture);
                profile.refineBodyWeightsWithPoses=EditorGUILayout.Toggle("Refine body weights across poses",profile.refineBodyWeightsWithPoses);
                EditorGUILayout.HelpBox("The imported mesh will be rotated into the game's Y-up skeleton space. Set this from the source model's export axis before building the workspace.",MessageType.Info);
                Undo.RecordObject(profile,"Edit entity skeleton mapping");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("Skeleton mapping",EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Edit names, parent links, 7DTD GameObject tags and normalized bone positions before building. Bone layout is a biped starting map; adapt it to the supplied model.",MessageType.Info);
                profile.skinWeightFalloffExponent=EditorGUILayout.Slider("Weight falloff",profile.skinWeightFalloffExponent,0.5f,8f);
                if(GUILayout.Button("Reset skeleton to biped defaults")){Undo.RecordObject(profile,"Reset entity skeleton");profile.InitializeBipedDefaults();}
                if(GUILayout.Button("Import skeleton from Game Bridge JSON"))Run(()=>RebirthEntityBridgeSkeletonImporter.ImportIntoProfile(profile));
                EditorGUILayout.HelpBox("Import a saved /entitymodel?entity=ID response from the Game Bridge. It seeds the profile with the inspected renderer-bone hierarchy, Unity tags and rest rotations. Bone positions are normalized to the supplied mesh and still need visual fitting.",MessageType.Info);
                for(int i=0;i<profile.bones.Count;i++)
                {
                    var bone=profile.bones[i];if(bone==null)continue;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();EditorGUILayout.LabelField("Bone "+i,EditorStyles.boldLabel);
                    if(GUILayout.Button("Remove",GUILayout.Width(62))){Undo.RecordObject(profile,"Remove entity bone");profile.bones.RemoveAt(i);i--;EditorGUILayout.EndHorizontal();EditorGUILayout.EndVertical();continue;}
                    EditorGUILayout.EndHorizontal();
                    bone.name=EditorGUILayout.TextField("Name",bone.name);
                    bone.parent=EditorGUILayout.TextField("Parent",bone.parent);
                    bone.gameTag=EditorGUILayout.TextField("7DTD tag",bone.gameTag);
                    bone.normalizedPosition=EditorGUILayout.Vector3Field("Bounds position",bone.normalizedPosition);
                    bone.referenceLocalEuler=EditorGUILayout.Vector3Field("Reference local rotation",bone.referenceLocalEuler);
                    bone.deformsMesh=EditorGUILayout.Toggle("Deforms mesh / receives weights",bone.deformsMesh);
                    bone.required=EditorGUILayout.Toggle("Required",bone.required);
                    EditorGUILayout.EndVertical();
                }
                if(GUILayout.Button("Add bone")){Undo.RecordObject(profile,"Add entity bone");profile.bones.Add(new EntityBoneSpec{name="NewBone",parent="Origin",normalizedPosition=new Vector3(.5f,.5f,.5f)});}
                if(EditorGUI.EndChangeCheck())
                {
                    profile.reviewedSkeletonMapping=false;
                    profile.reviewedTargetCompatibility=false;
                    EditorUtility.SetDirty(profile);
                }
                profile.generateLods=EditorGUILayout.Toggle("Generate LODs",profile.generateLods);
                if(profile.generateLods)
                {
                    EditorGUILayout.LabelField("LOD simplification",EditorStyles.boldLabel);
                    profile.lod0TransitionHeight=EditorGUILayout.Slider("LOD0 screen height",profile.lod0TransitionHeight,0.01f,0.95f);
                    profile.lod1TransitionHeight=EditorGUILayout.Slider("LOD1 screen height",profile.lod1TransitionHeight,0.005f,0.75f);
                    profile.lod2TransitionHeight=EditorGUILayout.Slider("LOD2 screen height",profile.lod2TransitionHeight,0.001f,0.5f);
                    profile.lod3TransitionHeight=EditorGUILayout.Slider("LOD3 screen height",profile.lod3TransitionHeight,0.001f,0.25f);
                    profile.lod1Ratio=EditorGUILayout.Slider("LOD1 quality",profile.lod1Ratio,0.01f,1f);
                    profile.lod2Ratio=EditorGUILayout.Slider("LOD2 quality",profile.lod2Ratio,0.01f,1f);
                    profile.lod3Ratio=EditorGUILayout.Slider("LOD3 quality",profile.lod3Ratio,0.01f,1f);
                    EditorGUILayout.HelpBox("Quality is an input to mesh simplification, not a promised triangle percentage. Validation records the actual triangle counts and blocks export unless each level reduces triangles.",MessageType.Info);
                }
                profile.createRagdoll=EditorGUILayout.Toggle("Generate ragdoll",profile.createRagdoll);
                if(profile.createRagdoll)profile.ragdollMass=Mathf.Max(.1f,EditorGUILayout.FloatField("Ragdoll total mass (kg)",profile.ragdollMass));
                profile.createHitColliders=EditorGUILayout.Toggle("Fit hit colliders",profile.createHitColliders);
                profile.useBoxTorsoAndLegs=EditorGUILayout.Toggle("Box fit torso and legs",profile.useBoxTorsoAndLegs);
                profile.colliderPadding=EditorGUILayout.Slider("Collider fit padding",profile.colliderPadding,0.005f,0.15f);
                profile.generateTierMaterials=EditorGUILayout.Toggle("Create tier materials",profile.generateTierMaterials);
                profile.albedoMaxSize=EditorGUILayout.IntPopup("Albedo texture limit",profile.albedoMaxSize,new[]{"1024","2048","4096","8192"},new[]{1024,2048,4096,8192});
                profile.normalMaxSize=EditorGUILayout.IntPopup("Normal texture limit",profile.normalMaxSize,new[]{"1024","2048","4096","8192"},new[]{1024,2048,4096,8192});
                profile.dataMaxSize=EditorGUILayout.IntPopup("Packed map texture limit",profile.dataMaxSize,new[]{"1024","2048","4096","8192"},new[]{1024,2048,4096,8192});
                profile.bakedAlbedo=(Texture2D)EditorGUILayout.ObjectField("Baked albedo",profile.bakedAlbedo,typeof(Texture2D),false);
                profile.normalMap=(Texture2D)EditorGUILayout.ObjectField("Normal map",profile.normalMap,typeof(Texture2D),false);
                profile.rmoeMap=(Texture2D)EditorGUILayout.ObjectField("RMOE/mask map",profile.rmoeMap,typeof(Texture2D),false);
                profile.characterShader=(Shader)EditorGUILayout.ObjectField("Target character shader",profile.characterShader,typeof(Shader),false);
                profile.feralTint=EditorGUILayout.ColorField(new GUIContent("Feral tint"),profile.feralTint,true,true,true);
                profile.radiatedTint=EditorGUILayout.ColorField(new GUIContent("Radiated MatColor"),profile.radiatedTint,true,true,true);
                profile.chargedTint=EditorGUILayout.ColorField(new GUIContent("Charged MatColor"),profile.chargedTint,true,true,true);
                profile.infernalTint=EditorGUILayout.ColorField(new GUIContent("Infernal MatColor"),profile.infernalTint,true,true,true);
                profile.tierEmission=EditorGUILayout.Slider("Tier emission",profile.tierEmission,0f,8f);
                EditorGUILayout.HelpBox("Radiated, Charged and Infernal RGB/emission-alpha presets match the researched public v3.0 entityclasses.xml; its exact b259 build provenance is not pinned. Feral variants use family-specific meshes/materials in the base game; this tint is a configurable approximation when only one source model is supplied.",MessageType.Info);
                EditorGUILayout.HelpBox("Baked albedo alone supplies color only. It cannot recover normal, RMOE, emissive channels or skinning weights. Assign the target shader and any available maps; confirm the target shader property names.",MessageType.Info);
                if(GUILayout.Button("Extract embedded maps and configure imports"))Run(()=>RebirthEntityTextureTools.ConfigureProfileMaps(profile));
                if(GUILayout.Button("Save profile")){EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();status="Profile saved.";}
            }
            EditorGUILayout.Space();
            source=(GameObject)EditorGUILayout.ObjectField("Unrigged model / scene instance",source,typeof(GameObject),true);
            showBoneFitPreview=EditorGUILayout.Toggle("Edit skeleton in Scene view",showBoneFitPreview);
            if(source!=null&&EditorUtility.IsPersistent(source))
                EditorGUILayout.HelpBox("For visual bone fitting, place the unrigged model in the Scene and assign that instance here. Drag the Scene handles; the profile stores bounds-relative positions. The source asset remains unchanged.",MessageType.Info);
            if(GUILayout.Button("Build rig workspace (keep source asset unchanged)"))Run(BuildWorkspace);
            workspace=(RebirthEntityRigWorkspace)EditorGUILayout.ObjectField("Workspace",workspace,typeof(RebirthEntityRigWorkspace),true);
            if(workspace!=null)
            {
                bool stale=RebirthEntityRigValidator.HasStaleInputs(workspace);
                if(stale)EditorGUILayout.HelpBox("Source model or profile changed after this workspace was built. Build a fresh workspace before regenerating outputs or marking any review gate.",MessageType.Error);
                using(new EditorGUI.DisabledScope(stale))
                {
                    if(GUILayout.Button("Rebuild mesh-fitted colliders and ragdoll"))Run(()=>RebirthEntityRigBuilder.BuildPhysics(workspace));
                    if(GUILayout.Button("Regenerate LODs"))Run(()=>RebirthEntityRigBuilder.RebuildLods(workspace));
                    if(GUILayout.Button("Create / refresh tier materials"))Run(()=>RebirthEntityRigBuilder.RebuildTierMaterials(workspace));
                    workspace.activeTier=(ZombieTier)EditorGUILayout.EnumPopup("Preview tier",workspace.activeTier);
                    if(GUILayout.Button("Apply tier material preview"))Run(()=>RebirthEntityRigBuilder.ApplyTierMaterials(workspace,workspace.activeTier));
                    if(GUILayout.Button("Mark skeleton mapping reviewed")){Undo.RecordObject(profile,"Review skeleton");profile.reviewedSkeletonMapping=true;EditorUtility.SetDirty(profile);}
                    if(GUILayout.Button("Mark collider fit reviewed")){Undo.RecordObject(profile,"Review colliders");profile.reviewedColliderFit=true;EditorUtility.SetDirty(profile);}
                    if(GUILayout.Button("Mark LODs reviewed")){Undo.RecordObject(profile,"Review LODs");profile.reviewedLods=true;EditorUtility.SetDirty(profile);}
                    if(GUILayout.Button("Mark materials reviewed")){Undo.RecordObject(profile,"Review materials");profile.reviewedMaterials=true;EditorUtility.SetDirty(profile);}
                    EditorGUILayout.LabelField("Exact target build review",EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("3.0 b259 compatibility is not independently verified by available 3.3.0 captures or unpinned public v3.0 XML. Record the exact build and evidence inspected. Export requires this review.",MessageType.Warning);
                    string evidence=EditorGUILayout.TextArea(profile.targetCompatibilityEvidence,GUILayout.MinHeight(44));
                    if(evidence!=profile.targetCompatibilityEvidence){Undo.RecordObject(profile,"Edit target compatibility evidence");profile.targetCompatibilityEvidence=evidence;profile.reviewedTargetCompatibility=false;EditorUtility.SetDirty(profile);}
                    using(new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(profile.targetCompatibilityEvidence)))
                        if(GUILayout.Button(profile.reviewedTargetCompatibility?"Target compatibility reviewed":"Mark target compatibility reviewed")){Undo.RecordObject(profile,"Review target compatibility");profile.reviewedTargetCompatibility=true;EditorUtility.SetDirty(profile);}
                }
                if(GUILayout.Button("Validate workspace"))Validate();
                EditorGUILayout.HelpBox(status,MessageType.None);
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateProfile()
        {
            string path=EditorUtility.SaveFilePanelInProject("Create entity rig profile","EntityRigProfile","asset","Choose a project asset location.");
            if(string.IsNullOrEmpty(path))return;
            profile=CreateInstance<RebirthEntityRigProfile>();profile.InitializeBipedDefaults();AssetDatabase.CreateAsset(profile,path);AssetDatabase.SaveAssets();status="Created "+path;
        }
        private void BuildWorkspace(){workspace=RebirthEntityRigBuilder.CreateWorkspace(source,profile);status="Workspace built. Inspect wireframes, deformation, LODs, materials and warnings before exporting.";}

        private void DrawBoneFitPreview(SceneView view)
        {
            if(!showBoneFitPreview||source==null||profile==null||profile.bones==null)return;
            Bounds bounds;
            try{bounds=RebirthEntityRigBuilder.SourceBoundsInRootSpace(source.transform);}
            catch{return;}
            Vector3 rootPosition=source.transform.position;
            var positions=new Dictionary<string,Vector3>(StringComparer.OrdinalIgnoreCase);
            foreach(var spec in profile.bones)
            {
                if(spec==null||string.IsNullOrWhiteSpace(spec.name))continue;
                positions[spec.name]=string.Equals(spec.name,"Origin",StringComparison.OrdinalIgnoreCase)?rootPosition:
                    source.transform.TransformPoint(RebirthEntityRigBuilder.BonePositionInSourceBounds(bounds,spec.normalizedPosition,profile.sourceUpAxis));
            }

            Handles.zTest=UnityEngine.Rendering.CompareFunction.LessEqual;
            foreach(var spec in profile.bones)
            {
                if(spec==null||string.IsNullOrWhiteSpace(spec.name)||!positions.TryGetValue(spec.name,out Vector3 position))continue;
                Vector3 parentPosition=rootPosition;
                if(!string.IsNullOrWhiteSpace(spec.parent))positions.TryGetValue(spec.parent,out parentPosition);
                Handles.color=new Color(.15f,.85f,1f,.8f);
                if(!string.Equals(spec.name,"Origin",StringComparison.OrdinalIgnoreCase))Handles.DrawLine(parentPosition,position);
                Handles.color=string.IsNullOrEmpty(spec.gameTag)?new Color(1f,.82f,.25f,1f):new Color(.2f,1f,.5f,1f);
                float size=HandleUtility.GetHandleSize(position);
                Handles.SphereHandleCap(GUIUtility.GetControlID(FocusType.Passive),position,Quaternion.identity,size*.035f,EventType.Repaint);
                Handles.Label(position,spec.name+(string.IsNullOrEmpty(spec.gameTag)?string.Empty:"  ["+spec.gameTag+"]"));
                if(string.Equals(spec.name,"Origin",StringComparison.OrdinalIgnoreCase))continue;
                EditorGUI.BeginChangeCheck();
                Vector3 moved=Handles.PositionHandle(position,Quaternion.identity);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(profile,"Fit entity skeleton to source mesh");
                    Vector3 local=source.transform.InverseTransformPoint(moved);
                    spec.normalizedPosition=RebirthEntityRigBuilder.NormalizeBonePositionInSourceBounds(bounds,local,profile.sourceUpAxis);
                    profile.reviewedSkeletonMapping=false;
                    EditorUtility.SetDirty(profile);
                    Repaint();view.Repaint();
                }
            }
        }

        private void Validate()
        {
            var findings=RebirthEntityRigValidator.Inspect(workspace);int errors=0,warnings=0;
            foreach(var f in findings){if(f.severity=="ERROR")errors++;else if(f.severity=="WARNING")warnings++;Debug.Log(f.ToString(),workspace);}
            workspace.validationReport="Errors: "+errors+", warnings: "+warnings+". See Console for findings.";
            status=workspace.validationReport;
        }
        private void Run(Action action){try{if(profile==null)throw new InvalidOperationException("Create/select profile first.");action();EditorUtility.SetDirty(profile);status="Completed. Review output in Scene and Console.";}catch(Exception ex){status=ex.Message;Debug.LogException(ex);}}
    }

    [CustomEditor(typeof(RebirthEntityRigWorkspace))]
    public sealed class RebirthEntityRigWorkspaceEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var ws=(RebirthEntityRigWorkspace)target;if(ws==null||ws.colliderRoot==null)return;
            foreach(var c in ws.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true))
            {
                Handles.color=new Color(1f,.72f,.1f,.9f);
                using(new Handles.DrawingScope(c.transform.localToWorldMatrix))
                {
                    Vector3 axis=c.direction==0?Vector3.right:c.direction==1?Vector3.up:Vector3.forward;
                    float half=Mathf.Max(0,c.height*.5f-c.radius);
                    Vector3 a=c.center+axis*half,b=c.center-axis*half;
                    Handles.DrawWireDisc(a,axis,c.radius);Handles.DrawWireDisc(b,axis,c.radius);
                    Vector3 side=Vector3.Cross(axis,Vector3.up);if(side.sqrMagnitude<.01f)side=Vector3.Cross(axis,Vector3.right);side.Normalize();
                    Handles.DrawLine(a+side*c.radius,b+side*c.radius);Handles.DrawLine(a-side*c.radius,b-side*c.radius);
                    Handles.DrawLine(a+Vector3.Cross(axis,side)*c.radius,b+Vector3.Cross(axis,side)*c.radius);
                    Handles.DrawLine(a-Vector3.Cross(axis,side)*c.radius,b-Vector3.Cross(axis,side)*c.radius);
                    var fit=ws.colliderFitReports!=null?ws.colliderFitReports.Find(item=>item.collider==c):null;
                    string coverage=fit==null?"":fit.usesPoseUnionCoverage?"region union "+(fit.poseUnionCoverage*100f).ToString("0")+"% / "+fit.poseUnionSampleCount+" pose samples":fit.usedProportionalFallback?"fallback":(fit.estimatedSurfaceCoverage*100f).ToString("0")+"% / "+fit.sampledVertices+"v";
                    string label=fit==null?c.name:fit.colliderName+" "+coverage+
                        " r="+fit.radius.ToString("F2")+" / ref="+fit.baseGameReferenceRadius.ToString("F2");
                    Handles.Label(c.transform.position,label);
                }
            }
            foreach(var c in ws.generatedSkeleton.GetComponentsInChildren<BoxCollider>(true))
            {
                Handles.color=new Color(1f,.72f,.1f,.9f);
                using(new Handles.DrawingScope(c.transform.localToWorldMatrix))Handles.DrawWireCube(c.center,c.size);
                Handles.Label(c.transform.TransformPoint(c.center),c.name+" box "+c.size.ToString("F2"));
            }
        }
    }
}
