// REBIRTH Vehicle Studio - first actual camper AssetBundle candidate.
// Unity 2022.3.62f2; additive Editor-only harness. DOES NOT install into 7DTD.
// Does NOT establish playable vehicle, seat fit, exit safety, IK reach or collisions.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RebirthVehicleInspector.UnityTools;

public static class RebirthUnityCamperFirstBundleTest
{
    const string UnityVersion = "2022.3.62f2";
    const string Source = "Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab";
    const string CockpitRun = "06678d094a7c44d3a09edd5c90792442";
    const string PoseRun = "4c31a760c8fd442b9e2d882e7e16712c";
    const string CockpitPrefab = "Assets/REBIRTHCamperCockpitGenerated/" + CockpitRun + "/UNAPPROVED_Camper_WheelsAndSteering.prefab";
    const string ProfileAsset = "Assets/REBIRTHCamperDonorPoseGenerated/" + PoseRun + "/UNAPPROVED_Camper_DonorPose_Profile.asset";
    const string OutputRoot = "Assets/REBIRTHCamperFirstBundleGenerated";
    const string ProductPrefabName = "rebirthCamper80sPrototype";

    [Serializable] class Summary
    {
        public string kind = "REBIRTH_CAMPER_FIRST_BUNDLE_TEST_V1_9";
        public string status = "FAIL", runId, editorVersion, sourcePrefab = Source;
        public string sourceCopkitPrefab = CockpitPrefab, sourcePoseProfile = ProfileAsset;
        public string prefabAsset, authoringProfileAsset, bundlePath, bundleReceipt, editorLog;
        public string sourceSha256, sourceMetaSha256, sourceCockpitSha256, sourceProfileSha256;
        public int rendererCount, wheelCount, seatCount, nativeDriverPose, nativePassengerPose;
        public bool originalAssetsUnchanged, exactTransformPaths, visualsPreserved;
        public bool gameLoaded = false, seatFitVerified = false, xmlCreated = false, localizationCreated = false;
        public string[] steps = new string[0], warnings = new string[0];
        public string error = "";
    }

    public static void RunBatch()
    {
        bool pass = Execute();
        EditorApplication.Exit(pass ? 0 : 1);
    }
    private static bool Execute()
    {
        string id = Arg("-rebirthCamperRunId");
        if (!System.Text.RegularExpressions.Regex.IsMatch(id ?? "", "^[a-f0-9]{32}$"))
            id = Guid.NewGuid().ToString("N");
        string project = Directory.GetParent(Application.dataPath).FullName;
        string report = Path.Combine(project,"REBIRTH_CamperFirstBundle_Reports",id);
        Directory.CreateDirectory(report);
        Summary r = new Summary { runId=id, editorVersion=Application.unityVersion };
        var steps = new List<string>();
        var warnings = new List<string>();
        try
        {
            if(Application.unityVersion != UnityVersion)
                throw new InvalidDataException("Editor must be exactly Unity " + UnityVersion);
            var pathSource = Disk(project,Source);
            var pathCockpit = Disk(project,CockpitPrefab);
            var pathProfile = Disk(project,ProfileAsset);
            foreach (var p in new []{pathSource,pathSource+".meta",pathCockpit,pathProfile})
                if(!File.Exists(p)) throw new FileNotFoundException("Previous successful asset missing",p);
            r.sourceSha256=Hash(pathSource);
            r.sourceMetaSha256=Hash(pathSource+".meta");
            r.sourceCockpitSha256=Hash(pathCockpit);
            r.sourceProfileSha256=Hash(pathProfile);
            steps.Add("PASS: exact Unity version and existing camper inputs found");

            var prior = AssetDatabase.LoadAssetAtPath<RebirthVehicleRigProfile>(ProfileAsset);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CockpitPrefab);
            if(prior==null || source==null || prior.seats.Count != 2 || prior.ik.Count != 1 ||
                prior.seats[0].index!=0 || prior.seats[0].poseId!="40" ||
                prior.seats[1].index!=1 || prior.seats[1].poseId!="41" ||
                prior.seats[0].exits.Count!=0 || prior.seats[1].exits.Count!=0 ||
                prior.ik[0].hasLeftHand || prior.ik[0].hasRightHand)
                throw new InvalidDataException("v1.8 pose profile is not the expected uncalibrated 2-seat 40/41 profile.");
            steps.Add("PASS: two existing native seat pose IDs 40/41 verified; no unapproved exits/IK");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            // Reload after scene switch to avoid the destroyed asset reference bug seen in v1.1.
            source=AssetDatabase.LoadAssetAtPath<GameObject>(CockpitPrefab);
            prior=AssetDatabase.LoadAssetAtPath<RebirthVehicleRigProfile>(ProfileAsset);
            if(source==null || prior==null) throw new InvalidDataException("Prefab/profile asset reload failed.");
            var root = new GameObject(ProductPrefabName);
            var instance=PrefabUtility.InstantiatePrefab(source) as GameObject;
            if(instance==null) throw new InvalidDataException("Failed to instantiate camper cockpit prefab.");
            var linked=PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
            if(linked!=null) PrefabUtility.UnpackPrefabInstance(linked,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.transform.SetParent(root.transform,true);
            instance.name="WORKING_SOURCE_SCAFFOLD";
            var initial = new List<Renderer>(root.GetComponentsInChildren<Renderer>(true));
            if(initial.Count != 16) throw new InvalidDataException("Expected exactly 16 real camper visuals, got "+initial.Count);
            var bounds = new Dictionary<Renderer,Bounds>();
            foreach(var render in initial) bounds[render]=render.bounds;
            steps.Add("PASS: 16 renderer objects loaded from successful cockpit preview");

            // The installed 4x4 XML references Wheel0..Wheel3 directly below its vehicle root.
            var donorWheel = new []{
                "Origin/FrontLeftSteer/FrontLeftWheel",
                "Origin/FrontRightSteer/FrontRightWheel",
                "Origin/RearLeftWheel", "Origin/RearRightWheel"
            };
            for(int i=0;i<donorWheel.Length;i++)
            {
                Transform oldJoint = instance.transform.Find(donorWheel[i]);
                if(oldJoint==null) throw new InvalidDataException("Missing verified wheel joint: "+donorWheel[i]);
                var mesh=oldJoint.GetComponentInChildren<Renderer>(true);
                if(mesh==null || mesh.transform==oldJoint || oldJoint.GetComponentsInChildren<Renderer>(true).Length !=1)
                    throw new InvalidDataException("Wheel visual hierarchy differs from successful v1.5 preview: "+donorWheel[i]);
                Transform pivot=new GameObject("Wheel"+i).transform;
                pivot.SetParent(root.transform,false);
                pivot.position=oldJoint.position;
                pivot.rotation=oldJoint.rotation;
                pivot.localScale=Vector3.one;
                mesh.transform.SetParent(pivot,true);
                r.wheelCount++;
            }
            // Set up vanilla-named visual group and steering pivot; never mutate Asset Store original.
            Transform m=new GameObject("M").transform;
            m.SetParent(root.transform,false);
            Transform steering = instance.transform.Find("Origin/CamperSteeringWheelPivot_UNVERIFIED");
            if(steering==null || steering.GetComponentsInChildren<Renderer>(true).Length!=1)
                throw new InvalidDataException("Prior steering wheel visual/pivot missing or changed.");
            steering.SetParent(m,true);
            steering.name="SteeringWheel";
            Transform body=instance.transform.Find("Origin/assembled_vehicle");
            if(body==null) throw new InvalidDataException("Can't locate 4-wheel camper body in Origin/assembled_vehicle.");
            body.SetParent(m,true);
            body.name="VisualBody";
            Transform lights=new GameObject("HeadLights").transform;
            lights.SetParent(m,false);
            new GameObject("Lights").transform.SetParent(lights,false);
            // Renderer remaining in source scaffold means hierarchy assumptions broke; fail, don't lose visuals.
            if(instance.GetComponentsInChildren<Renderer>(true).Length != 0)
                throw new InvalidDataException("Not all camper renderers moved into target 7DTD hierarchy.");
            UnityEngine.Object.DestroyImmediate(instance);
            string[] required={"Wheel0","Wheel1","Wheel2","Wheel3","M/SteeringWheel","M/HeadLights/Lights","M/VisualBody"};
            foreach(string path in required) if(root.transform.Find(path)==null)
                throw new InvalidDataException("Prototype path missing: "+path);
            r.exactTransformPaths=true;
            steps.Add("PASS: 4 native wheel paths + M/SteeringWheel + M/HeadLights/Lights present");

            foreach(var pair in bounds)
            {
                if(pair.Key==null) throw new InvalidDataException("Camper lost a renderer in conversion.");
                var current=pair.Key.bounds;
                if((current.center-pair.Value.center).magnitude>0.002f ||
                   (current.size-pair.Value.size).magnitude>0.002f)
                    throw new InvalidDataException("Visible mesh shifted while preparing game hierarchy: "+pair.Key.name);
            }
            if(root.GetComponentsInChildren<Renderer>(true).Length!=16)
                throw new InvalidDataException("Visual count changed during prototype conversion.");
            r.rendererCount=16; r.visualsPreserved=true;
            steps.Add("PASS: all 16 camper meshes retained with unchanged world-space bounds");

            string output = OutputRoot+"/"+id;
            EnsureFolder(output);
            r.prefabAsset=output+"/"+ProductPrefabName+".prefab";
            r.authoringProfileAsset=output+"/UNAPPROVED_FirstPlayable_Profile.asset";
            if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(r.prefabAsset)!=null ||
               AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(r.authoringProfileAsset)!=null)
                throw new IOException("Refusing to overwrite existing prototype assets.");
            var clone=UnityEngine.Object.Instantiate(prior);
            clone.name="UNAPPROVED_FirstPlayable_Profile";
            clone.targetVehicleName="rebirthCamper80sVehicle";
            clone.sourcePrefabReference=r.prefabAsset;
            clone.sourceXmlStatus="VANILLA_4X4_SEAT0_DIRECTLY_VERIFIED_CUSTOM_CAMPER_UNCALIBRATED";
            clone.wheels.Clear();
            for(int i=0;i<4;i++) clone.wheels.Add(new VehicleWheelLink{
                partClass="wheel"+i,steerTransformPath=(i<2?"Wheel"+i:""), tireTransformPath="Wheel"+i
            });
            clone.ik[0].partClass="handlebars";
            clone.ik[0].pivotPath="M/SteeringWheel";
            clone.ik[0].hasLeftHand=false;
            clone.ik[0].hasRightHand=false;
            clone.ik[0].hasLeftFoot=false;
            clone.ik[0].hasRightFoot=false;
            clone.importWarnings.Add("First playable prototype: seats from camper geometry; NOT real occupant-calibrated.");
            clone.importWarnings.Add("Steering hand IK intentionally disabled for first game-load check. Configure after observing native player.");
            clone.importWarnings.Add("Do not infer that 4x4 donor's 6 seats or plow optional upgrade fit this camper.");
            clone.importWarnings.Add("Seat exit candidates unconfigured; before deployment, define safe exits in target XML and verify them in game.");
            AssetDatabase.CreateAsset(clone,r.authoringProfileAsset);
            r.seatCount=clone.seats.Count;
            r.nativeDriverPose=40;
            r.nativePassengerPose=41;
            var saved=PrefabUtility.SaveAsPrefabAsset(root,r.prefabAsset);
            UnityEngine.Object.DestroyImmediate(root);
            if(saved==null || saved.GetComponentsInChildren<Renderer>(true).Length!=16 ||
               saved.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)
                throw new InvalidDataException("Clean candidate prefab wasn't saved or contained editor scripts.");
            AssetDatabase.SaveAssets();
            steps.Add("PASS: clean camper prototype prefab saved; driver 40 and passenger 41 in separate Unity authoring profile");

            var folder=Path.Combine(report,"bundles");
            r.bundlePath=RebirthVehicleRigBuilder.BuildOneAssetBundle(r.prefabAsset,folder,
                "rebirth_camper80s_"+id+".unity3d",BuildTarget.StandaloneWindows64);
            r.bundleReceipt=r.bundlePath+".rebirth-receipt.json";
            if(!File.Exists(r.bundleReceipt)) throw new IOException("Missing Unity bundle verification receipt.");
            steps.Add("PASS: actual camper Windows64 AssetBundle built and reopened by Unity");
            // Original user model and earlier tested sources must remain byte identical.
            r.originalAssetsUnchanged=Hash(pathSource)==r.sourceSha256 &&
                Hash(pathSource+".meta")==r.sourceMetaSha256 &&
                Hash(pathCockpit)==r.sourceCockpitSha256 && Hash(pathProfile)==r.sourceProfileSha256;
            if(!r.originalAssetsUnchanged) throw new InvalidDataException("Original camper source / prior project assets changed.");
            warnings.Add("BUNDLE_ONLY: do not install into REBIRTH. New custom vehicle XML/localization is still required.");
            warnings.Add("Experimental 7DTD naming/hierarchy: Unity bundle reload does not prove 7DTD loads or drives it.");
            warnings.Add("Seat markers are provisional; untested exits and inactive hand IK prevent finished-vehicle claim.");
            warnings.Add("No Rigidbody/Collider behaviour is qualified by this test; game may require additional components.");
            r.status="PASS_FIRST_CAMPER_BUNDLE_NEEDS_GAME_XML_AND_TEST";
        }
        catch(Exception ex)
        {
            r.error=ex.ToString();r.status="FAIL";Debug.LogError("Camper first bundle test: "+ex);
        }
        finally
        {
            r.steps=steps.ToArray();r.warnings=warnings.ToArray();
            try {
                AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(report,"result.json"),JsonUtility.ToJson(r,true),
                    new System.Text.UTF8Encoding(false));
            } catch(Exception ex) {Debug.LogError("Failed writing test report: "+ex);r.status="FAIL";}
        }
        return r.status=="PASS_FIRST_CAMPER_BUNDLE_NEEDS_GAME_XML_AND_TEST";
    }
    private static string Arg(string key)
    {
        var a=Environment.GetCommandLineArgs();
        for(int i=0;i<a.Length-1;i++) if(a[i]==key) return a[i+1];
        return "";
    }
    private static string Disk(string project,string asset)
    { return Path.Combine(project,asset.Replace('/',Path.DirectorySeparatorChar)); }
    private static string Hash(string file)
    {
        using(var sha=SHA256.Create()) using(var stream=File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }
    private static void EnsureFolder(string folder)
    {
        string[] segs=folder.Split('/');string current=segs[0];
        for(int i=1;i<segs.Length;i++)
        {
            string next=current+"/"+segs[i];
            if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,segs[i]);
            current=next;
        }
    }
}
