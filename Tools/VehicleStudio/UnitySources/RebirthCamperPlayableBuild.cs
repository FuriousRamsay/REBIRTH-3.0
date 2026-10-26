using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RebirthVehicleInspector.UnityTools;

// Additive authoring from the preserved first-bundle candidate. No game DLL dependency.
public static class RebirthCamperPlayableBuild
{
    [Serializable] public class Seat
    {
        public int index, pose;
        public Vector3 hipGuide, position, rotation;
        public Vector3[] exits;
    }
    [Serializable] public class Wheel
    {
        public string physicsPath, visualPath;
        public Vector3 position;
        public float radius, suspensionDistance;
    }
    [Serializable] public class Report
    {
        public string kind = "REBIRTH_CAMPER_PLAYABLE_CANDIDATE", status = "FAIL", runId, unityVersion;
        public string sourcePrefab, sourceSha256, profileSourceSha256, prefabAsset, prefabName = "rebirthCamper80s";
        public string bundlePath, bundleSha256;
        public string[] assetNames, transformPaths, steps;
        public long bundleBytes;
        public string compressionMode, uncompressedBundlePath, uncompressedBundleSha256, recompressionResult;
        public uint bundleCRC, finalExtractedCRC;
        public bool finalCRCExtractionSucceeded, rawExpectedCRCLoadSucceeded, finalExpectedCRCLoadSucceeded;
        public string verifiedSourceReport, verifiedSourceReportSha256, verifiedOriginalStatus;
        public bool verifiedSourceReportUnchanged;
        public double archiveBuildSeconds, recompressionSeconds;
        public bool recompressionSucceeded;
        public int rendererCount, wheelColliderCount, bodyColliderCount, spotLightCount;
        public float mass;
        public Vector3 boundsCenter, boundsSize, handLeftPosition, handRightPosition;
        public Vector3 handLeftRotation, handRightRotation;
        public string handRotationFrame = "camper-shaft";
        public string dyePaintPath;
        public bool bodyDyeMaterial;
        public bool bodySurfaceMapsVerified;
        public SurfaceMap[] bodySurfaceMaps;
        public TextureCopy[] optimizedTextures;
        public TextureBinding[] optimizedBindings;
        public SourceAsset[] originalSurfaceMaterials;
        public bool headlightEmissionAuthored;
        public string headlightEmissionMaterialName, headlightEmissionShader;
        public bool bedPoseAuthored;
        public string bedPoseClipName, cabinPlanSha256;
        public bool rearLampAuthored;
        public int rearLampTriangles;
        public RebirthCamperDirectionalLampAuthoring.Result directionalLamps;
        public Seat[] seats;
        public Wheel[] wheels;
        public bool prefabVerified, originalsUnchanged, gameTested = false;
        public string error = "";
    }
    [Serializable] public class SurfaceMap
    {
        public string property, texture;
        public int width, height;
    }
    [Serializable] public class TextureCopy
    {
        public string source, generated, sourceHash, sourceMetaHash;
        public bool linear;
        public bool generatedSRGB;
        public string generatedGraphicsFormat;
        public int width, height;
    }
    [Serializable] public class TextureBinding
    {
        public string renderer, property;
        public int slot, textureIndex;
    }
    [Serializable] public class SourceAsset { public string path, hash, metaHash; }

    public static void RunBatch()
    {
        var r = new Report { runId = Arg("-rebirthCamperRunId"), unityVersion = Application.unityVersion };
        string reportFolder = Arg("-rebirthCamperReport");
        var steps = new List<string>();
        GameObject root = null;
        try
        {
            if (Application.unityVersion != "2022.3.62f2") throw new Exception("Exact Unity 2022.3.62f2 required.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(r.runId ?? "", "^[a-f0-9]{32}$")) throw new Exception("Invalid run ID.");
            if (string.IsNullOrEmpty(reportFolder)) throw new Exception("Report folder required.");
            Directory.CreateDirectory(reportFolder);
            if (File.Exists(Path.Combine(reportFolder, "result.json"))) throw new Exception("Result already exists.");
            r.sourcePrefab = Arg("-rebirthCamperSourcePrefab");
            if (!r.sourcePrefab.StartsWith("Assets/REBIRTHCamperFirstBundleGenerated/", StringComparison.Ordinal)) throw new Exception("Expected preserved first-bundle source.");
            const string profilePath = "Assets/REBIRTHCamperDonorPoseGenerated/4c31a760c8fd442b9e2d882e7e16712c/UNAPPROVED_Camper_DonorPose_Profile.asset";
            string project = Directory.GetParent(Application.dataPath).FullName;
            r.sourceSha256 = Hash(Path.Combine(project, r.sourcePrefab));
            r.profileSourceSha256 = Hash(Path.Combine(project, profilePath));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(r.sourcePrefab);
            var prior = AssetDatabase.LoadAssetAtPath<RebirthVehicleRigProfile>(profilePath);
            if (source == null || prior == null || prior.seats.Count != 2 || prior.seats[0].poseId != "40" || prior.seats[1].poseId != "41") throw new Exception("Preserved camper source/profile invalid.");
            root = PrefabUtility.InstantiatePrefab(source) as GameObject;
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = r.prefabName;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 16) throw new Exception("Expected all sixteen original visuals.");
            var before = renderers.ToDictionary(x => x, x => x.bounds);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                if (renderer.sharedMaterials.Any(x => x == null || x.shader == null)) throw new Exception("Missing material/shader: " + renderer.name);
            }
            r.boundsCenter = bounds.center; r.boundsSize = bounds.size;
            steps.Add("PASS: actual camper geometry and sixteen original renderer/material references loaded");

            // Installed Entity.PhysicsInit resolves the Physics tag, then EntityVehicle.SetupWheels
            // requires Wheel0..Wheel3 with WheelCollider under that separate physics root.
            EnsureTag("Physics");
            var physics = Child(root.transform, "Physics");
            physics.gameObject.tag = "Physics";
            physics.gameObject.layer = 21;
            var rb = physics.gameObject.AddComponent<Rigidbody>();
            rb.mass = 2400f; rb.useGravity = false;
            rb.drag = 0.05f; rb.angularDrag = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = new Vector3(0, 0.1f, 0);
            r.mass = rb.mass;
            AddHull(physics, "CabHull", new Vector3(0, 1.0973f, -0.3661f), new Vector3(1.8075f, 1.2496f, 4.8181f));
            AddHull(physics, "CamperHull", new Vector3(0.00257f, 1.59581f, -1.37425f), new Vector3(1.92197f, 2.05811f, 4.18036f));
            // Interaction collider stays with the visual entity, as in the supplied native capture.
            root.layer = 14;
            var interaction = root.AddComponent<BoxCollider>();
            interaction.center = new Vector3(0, 1.15f, -0.5f);
            interaction.size = new Vector3(1.92f, 1.65f, 5.5f);
            var wheelReport = new List<Wheel>();
            for (int i = 0; i < 4; i++)
            {
                Transform visual = root.transform.Find("Wheel" + i);
                if (visual == null) throw new Exception("Missing visual wheel " + i);
                var renderer = visual.GetComponentInChildren<Renderer>(true);
                if (renderer == null || visual.GetComponentsInChildren<Renderer>(true).Length != 1) throw new Exception("Ambiguous wheel renderer.");
                var wheel = Child(physics, "Wheel" + i);
                wheel.localPosition = visual.localPosition;
                wheel.gameObject.layer = 21;
                var wc = wheel.gameObject.AddComponent<WheelCollider>();
                wc.radius = Mathf.Max(renderer.bounds.extents.y, renderer.bounds.extents.z);
                // Native 4x4 values captured from normal placement; retain the
                // camper's measured wheel radius and positions.
                wc.mass = 45f; wc.suspensionDistance = 0.43f;
                // TargetPosition .5 with this centre preserves the measured hub position at rest.
                wc.center = new Vector3(0, wc.suspensionDistance * 0.5f, 0);
                wc.suspensionSpring = new JointSpring { spring = 15000f, damper = 2000f, targetPosition = 0.5f };
                wc.forceAppPointDistance = 0.2f;
                wc.forwardFriction = new WheelFrictionCurve { extremumSlip = 0.4f, extremumValue = 1f,
                    asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.8f };
                wc.sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.2f, extremumValue = 1f,
                    asymptoteSlip = 0.75f, asymptoteValue = 0.75f, stiffness = 1.8f };
                wheelReport.Add(new Wheel { physicsPath = "Physics/Wheel" + i, visualPath = "Wheel" + i, position = wheel.localPosition, radius = wc.radius, suspensionDistance = wc.suspensionDistance });
            }
            r.wheels = wheelReport.ToArray();
            steps.Add("PASS: separate tagged Physics root, rigidbody, measured hulls and four native WheelCollider targets");

            var lights = root.transform.Find("M/HeadLights/Lights");
            if (lights == null) throw new Exception("Headlight path missing.");
            for (int i = 0; i < 2; i++)
            {
                var lightT = Child(lights, i == 0 ? "LeftSpot" : "RightSpot");
                lightT.localPosition = new Vector3(i == 0 ? -0.60f : 0.60f, 0.90844f, 1.957f);
                lightT.localRotation = Quaternion.Euler(5, 0, 0);
                var light = lightT.gameObject.AddComponent<Light>();
                light.type = LightType.Spot; light.range = 50; light.spotAngle = 62;
                light.intensity = 0.75f; light.shadows = LightShadows.None;
            }
            lights.gameObject.SetActive(false);

            // Earlier guides describe desired occupant hips, not the native player attachment root.
            // The supplied pose-40 capture gives Hips - XML seat position=(~0,.99635533,-.1427227).
            // This conversion is provisional until actual camper occupant screenshots/capture.
            Vector3 nativeHipOffset = new Vector3(-0.000000323f, 0.99635533f, -0.1427227f);
            r.seats = new Seat[2];
            for (int i = 0; i < 2; i++)
            {
                Vector3 guide = prior.seats[i].position;
                r.seats[i] = new Seat { index = i, pose = i == 0 ? 40 : 41, hipGuide = guide,
                    position = guide - nativeHipOffset, rotation = Vector3.zero,
                    exits = new [] { new Vector3(i == 0 ? -1.7f : 1.7f, 0, 0), new Vector3(i == 0 ? 1.7f : -1.7f, 0, 0), new Vector3(0, 0, 2.8f), new Vector3(0, 0, -4.1f) } };
                var seat = Child(root.transform, "Seat" + i);
                seat.localPosition = r.seats[i].position;
            }
            r.handLeftPosition = prior.ik[0].leftHandPosition;
            r.handRightPosition = prior.ik[0].rightHandPosition;
            // Captured donor shaft tilt is +18 degrees; camper shaft tilt is -71.2457.
            // Convert the neutral donor wrist frames before applying camper steering.
            // See calibrate_hand_frames.py; these remain runtime visual candidates.
            r.handLeftRotation = new Vector3(-24.132397f, 6.1188402f, 97.300014f);
            r.handRightRotation = new Vector3(34.898045f, 170.12771f, -95.064963f);
            // Add the four owner-specified cabin places from the separately measured
            // source plan. Existing front attachment/hands/steering values stay intact.
            string cabinPlanPath = Arg("-rebirthCamperCabinPlan");
            if (string.IsNullOrEmpty(cabinPlanPath) || !File.Exists(cabinPlanPath))
                throw new Exception("Explicit measured six-occupant cabin authoring plan required.");
            var cabinPlan = JsonUtility.FromJson<CabinPlan>(File.ReadAllText(cabinPlanPath));
            if (cabinPlan.capacity != 6 || cabinPlan.authoringSeats == null || cabinPlan.authoringSeats.Length != 4
                || !cabinPlan.status.StartsWith("STAGED_SIX_OCCUPANT_CANDIDATE_", StringComparison.Ordinal))
                throw new Exception("Measured six-occupant cabin plan invalid.");
            r.cabinPlanSha256 = Hash(cabinPlanPath);
            r.seats = r.seats.Concat(cabinPlan.authoringSeats).ToArray();
            for (int i = 2; i < 6; i++)
            {
                if (r.seats[i].index != i || r.seats[i].pose != 41 || r.seats[i].exits == null || r.seats[i].exits.Length == 0)
                    throw new Exception("Contiguous native passenger seat candidate required.");
                foreach (float value in new[] { r.seats[i].position.x, r.seats[i].position.y, r.seats[i].position.z,
                    r.seats[i].rotation.x, r.seats[i].rotation.y, r.seats[i].rotation.z })
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new Exception("Nonfinite cabin seat candidate.");
                var anchor = Child(root.transform, "Seat" + i);
                anchor.localPosition = r.seats[i].position; anchor.localEulerAngles = r.seats[i].rotation;
            }
            steps.Add("PASS: two pose-40/41 attachment anchors, external exit candidates and steering-local grip candidates; runtime calibration still required");

            foreach (var pair in before)
                if ((pair.Key.bounds.center - pair.Value.center).magnitude > 0.002f || (pair.Key.bounds.size - pair.Value.size).magnitude > 0.002f) throw new Exception("Original mesh moved: " + pair.Key.name);
            string folder = "Assets/REBIRTHCamperPlayableGenerated/" + r.runId;
            EnsureFolder(folder);
            var bedClip = RebirthCamperBedPoseAuthoring.Apply(root, folder);
            r.bedPoseAuthored = true; r.bedPoseClipName = bedClip.name;
            var body = root.transform.Find("M/VisualBody/80sCamperTruck/body");
            if (body == null || body.GetComponent<Renderer>() == null) throw new Exception("Exact truck paint renderer missing.");
            var dyeShader = Shader.Find("REBIRTH/CamperBodyDye");
            if (dyeShader == null || ShaderUtil.ShaderHasError(dyeShader)) throw new Exception("Camper dye shader missing or failed compilation.");
            var blueMaterial = body.GetComponent<Renderer>().sharedMaterial;
            var whiteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Perfectvertex/80sCamperTruck/Art/Materials/80sCamperTruck_body_white.mat");
            if (whiteMaterial == null || whiteMaterial.mainTexture == null) throw new Exception("Matching neutral body texture missing.");
            var paint = new Material(dyeShader) { name = "rebirthCamperBodyDye" };
            paint.SetTexture("_MainTex", blueMaterial.mainTexture);
            paint.SetTexture("_NeutralTex", whiteMaterial.mainTexture);
            foreach (string key in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) paint.SetTexture(key, blueMaterial.GetTexture(key));
            foreach (string key in new[] { "_BumpScale", "_GlossMapScale", "_OcclusionStrength" }) paint.SetFloat(key, blueMaterial.GetFloat(key));
            paint.SetColor("_Color", new Color(255,255,255,1));
            AssetDatabase.CreateAsset(paint, folder + "/rebirthCamperBodyDye.mat");
            body.GetComponent<Renderer>().sharedMaterial = paint;
            OptimizeSurfaceImports(root, folder, r);
            var headlightRenderer = root.transform.Find("M/VisualBody/80sCamperTruck/headlights_glass")?.GetComponent<Renderer>();
            var headlightMaterial = headlightRenderer == null ? null : headlightRenderer.sharedMaterial;
            if (headlightRenderer == null || headlightRenderer.sharedMaterials.Length != 1 || headlightMaterial == null
                || headlightMaterial.name != "80sCamperTruck_headlights_glass"
                || !AssetDatabase.GetAssetPath(headlightMaterial).StartsWith(folder + "/SurfaceMaterials/", StringComparison.Ordinal)
                || headlightMaterial.shader == null || !headlightMaterial.HasProperty("_EmissionColor"))
                throw new Exception("Exact generated headlight lens material required for native emission.");
            // Native VPHeadlight.UpdateOn sets _EmissionColor but does not enable this shader keyword.
            // Change only the owned generated copy; preserve its shader, transparency and native color control.
            // StandardShaderGUI validates keywords again on save. Its FixupEmissiveFlag
            // leaves the source's bare EmissiveIsBlack flag (4) intact and strips _EMISSION.
            // RealtimeEmissive (1) keeps black initial color without disabling the variant.
            headlightMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            headlightMaterial.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(headlightMaterial);
            r.headlightEmissionAuthored = true;
            r.headlightEmissionMaterialName = headlightMaterial.name;
            r.headlightEmissionShader = headlightMaterial.shader.name;
            steps.Add("PASS: generated headlight lens emission keyword enabled; native toggle appearance still requires game validation");
            steps.Add("PASS: generated surface copies preserve texture bytes/resolution, normal imports and material values; scalar maps linear, filtering trilinear/aniso >=4; appearance unvalidated");
            r.dyePaintPath = "M/VisualBody/80sCamperTruck/body";
            r.bodyDyeMaterial = true;
            steps.Add("PASS: isolated truck-body native dye material authored; cosmetic slot/visual color still require game validation");
            string rearSelection = Arg("-rebirthCamperRearSelection");
            if (string.IsNullOrEmpty(rearSelection) || !File.Exists(rearSelection)) throw new Exception("Measured rear-lamp selection receipt required.");
            var measuredRear = JsonUtility.FromJson<RebirthCamperRearLensReview.Result>(File.ReadAllText(rearSelection));
            RebirthCamperRearLampAuthoring.Apply(root, folder, measuredRear);
            r.directionalLamps = RebirthCamperDirectionalLampAuthoring.Apply(root, folder);
            // Lamp splitting follows optimization: emission clones the optimized lamp material.
            // Include its new renderer/property bindings in the same bundle color-space checks.
            var rearRenderer = root.transform.Find("M/VisualBody/80sCamperTruck/lights/RearRedLenses").GetComponent<Renderer>();
            var rearBindings = new List<TextureBinding>(r.optimizedBindings);
            foreach (var emissionRenderer in new[] { rearRenderer, root.transform.Find("M/VisualBody/80sCamperTruck/lights/BodyMarkerLenses").GetComponent<Renderer>() })
            foreach (string property in emissionRenderer.sharedMaterial.GetTexturePropertyNames())
            {
                var texture = emissionRenderer.sharedMaterial.GetTexture(property);
                if (texture == null) continue;
                string texturePath = AssetDatabase.GetAssetPath(texture);
                int index = Array.FindIndex(r.optimizedTextures, t => t.generated == texturePath);
                if (index < 0) throw new Exception("Rear emission texture bypassed generated surface imports: " + property);
                rearBindings.Add(new TextureBinding { renderer = AnimationUtility.CalculateTransformPath(emissionRenderer.transform,root.transform),
                    slot = 0, property = property, textureIndex = index });
            }
            r.optimizedBindings = rearBindings.ToArray();
            r.rearLampAuthored = true;
            r.rearLampTriangles = measuredRear.rearTriangles;
            steps.Add("PASS: measured rear red-lens mesh and two disabled red emitters authored; brake/reverse runtime hook still requires qualification");
            // EntityFactory selects only a direct child named GameObject as ModelTransform.
            // Without it model-follow updates move the Physics parent and feed motion back.
            var entityRoot = new GameObject(r.prefabName);
            root.name = "GameObject";
            root.transform.SetParent(entityRoot.transform, false);
            // Preserve native RootTransform.Find("Physics") initialization fallback.
            physics.SetParent(entityRoot.transform, false);
            root = entityRoot;
            steps.Add("PASS: native entity/model roots separated by direct GameObject wrapper");
            r.prefabAsset = folder + "/" + r.prefabName + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, r.prefabAsset);
            AssetDatabase.SaveAssets();
            Validate(prefab, r);
            var modelRoot = prefab.transform.Find("GameObject");
            r.transformPaths = prefab.GetComponentsInChildren<Transform>(true).Select(x =>
                AnimationUtility.CalculateTransformPath(x, x.IsChildOf(modelRoot) ? modelRoot : prefab.transform)).ToArray();
            // Unity's native archive writer receives paths inside this directory.
            // Keep build scratch on the configured temporary drive and short.
            var output = Arg("-rebirthCamperBundleOutput");
            if (string.IsNullOrEmpty(output)) throw new Exception("Explicit bundle output folder required.");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            var build = new AssetBundleBuild { assetBundleName = "rebirth_camper80s.unity3d", assetNames = new [] { r.prefabAsset } };
            r.compressionMode = Arg("-rebirthCamperCompression");
            if (string.IsNullOrEmpty(r.compressionMode)) r.compressionMode = "NativeHC"; // Historical direct callers.
            if (r.compressionMode != "FastLZ4" && r.compressionMode != "NativeHC") throw new Exception("Unknown bundle compression mode.");
            r.bundlePath = Path.Combine(output, build.assetBundleName);
            if (File.Exists(r.bundlePath)) throw new Exception("Bundle output already exists; refuse overwrite.");
            string archiveOutput = r.compressionMode == "FastLZ4" ? Path.Combine(output, "UncompressedSource") : output;
            if (r.compressionMode == "FastLZ4" && Directory.Exists(archiveOutput)) throw new Exception("Uncompressed archive output already exists.");
            Directory.CreateDirectory(archiveOutput);
            var archiveWatch = System.Diagnostics.Stopwatch.StartNew();
            var options = r.compressionMode == "FastLZ4" ? BuildAssetBundleOptions.UncompressedAssetBundle : BuildAssetBundleOptions.ChunkBasedCompression;
            if (BuildPipeline.BuildAssetBundles(archiveOutput, new [] { build }, options, BuildTarget.StandaloneWindows64) == null)
                throw new Exception("AssetBundle build failed.");
            r.archiveBuildSeconds = archiveWatch.Elapsed.TotalSeconds;
            string archivePath = Path.Combine(archiveOutput, build.assetBundleName);
            if (!BuildPipeline.GetCRCForAssetBundle(archivePath, out uint expectedCRC)) throw new Exception("Native archive CRC unavailable.");
            r.bundleCRC = expectedCRC;
            if (r.compressionMode == "FastLZ4" && expectedCRC == 0) throw new Exception("Fast packaging requires nonzero native CRC verification.");
            if (r.compressionMode == "FastLZ4")
            {
                r.uncompressedBundlePath = archivePath; r.uncompressedBundleSha256 = Hash(archivePath);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var operation = AssetBundle.RecompressAssetBundleAsync(archivePath, r.bundlePath,
                    BuildCompression.LZ4Runtime, expectedCRC, UnityEngine.ThreadPriority.Normal);
                if (operation == null) throw new Exception("Native LZ4 recompression did not start.");
                double nextProgress = 10;
                // Native documentation specifies a background worker. Poll its native completion
                // flag, not a managed completion callback that needs the blocked editor loop.
                while (!operation.isDone)
                {
                    if (watch.Elapsed.TotalMinutes >= 12) throw new TimeoutException("Native LZ4 recompression exceeded 12 minutes; preserve failed outputs.");
                    if (watch.Elapsed.TotalSeconds >= nextProgress)
                    { Debug.Log("[Camper packaging] LZ4 recompression seconds=" + watch.Elapsed.TotalSeconds + " progress=" + operation.progress); nextProgress += 10; }
                    System.Threading.Thread.Sleep(20);
                }
                r.recompressionSeconds = watch.Elapsed.TotalSeconds;
                r.recompressionResult = operation.result + ": " + operation.humanReadableResult;
                r.recompressionSucceeded = operation.success;
                if (!r.recompressionSucceeded || !File.Exists(r.bundlePath)) throw new Exception("Native LZ4 recompression failed: " + r.recompressionResult);
                if (Hash(archivePath) != r.uncompressedBundleSha256) throw new Exception("Uncompressed source changed during recompression.");
                var rawChecked = AssetBundle.LoadFromFile(archivePath, expectedCRC);
                r.rawExpectedCRCLoadSucceeded = rawChecked != null;
                if (rawChecked == null) throw new Exception("Raw archive expected-CRC load failed.");
                rawChecked.Unload(true);
                r.finalCRCExtractionSucceeded = BuildPipeline.GetCRCForAssetBundle(r.bundlePath, out uint finalCRC);
                r.finalExtractedCRC = finalCRC;
                if (r.finalCRCExtractionSucceeded && finalCRC != expectedCRC)
                    throw new Exception("Recompressed native CRC extraction success=" + r.finalCRCExtractionSucceeded
                        + " expected=" + expectedCRC + " extracted=" + finalCRC);
                steps.Add("PASS: distinct native LZ4Runtime bundle recompressed with matching uncompressed-content CRC; performance pending measured build");
            }
            var bundle = AssetBundle.LoadFromFile(r.bundlePath, expectedCRC);
            r.finalExpectedCRCLoadSucceeded = bundle != null && expectedCRC != 0;
            if (bundle == null) throw new Exception("Bundle reopen with expected CRC failed.");
            try
            {
                r.assetNames = bundle.GetAllAssetNames();
                var reopened = bundle.LoadAsset<GameObject>(r.prefabAsset.ToLowerInvariant());
                Validate(reopened, r);
                r.prefabVerified = true;
            }
            finally { bundle.Unload(true); }
            r.bundleSha256 = Hash(r.bundlePath); r.bundleBytes = new FileInfo(r.bundlePath).Length;
            r.originalsUnchanged = Hash(Path.Combine(project, r.sourcePrefab)) == r.sourceSha256 && Hash(Path.Combine(project, profilePath)) == r.profileSourceSha256;
            if (!r.originalsUnchanged) throw new Exception("Preserved source modified.");
            steps.Add("PASS: Windows64 bundle reopened with geometry, physics, lights and seat hierarchy intact");
            r.status = "PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED";
        }
        catch (Exception ex) { r.error = ex.ToString(); Debug.LogError(ex); }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            r.steps = steps.ToArray();
            File.WriteAllText(Path.Combine(reportFolder, "result.json"), JsonUtility.ToJson(r, true));
        }
        EditorApplication.Exit(r.status == "PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED" ? 0 : 1);
    }


    [Serializable] public class PackagingInspection
    {
        public string status = "FAIL", sourceReport, inputHash, outputHash, error;
        public uint expectedCRC, rawExtractedCRC, finalExtractedCRC;
        public bool rawCRCExtractionSucceeded, finalCRCExtractionSucceeded, rawExpectedCRCLoadSucceeded,
            finalExpectedCRCLoadSucceeded, finalPrefabVerified, immutableArtifacts;
    }
    // Read-only diagnostic of retained raw/final archives. Never overwrite the failed build receipt.
    public static void InspectPackagingBatch()
    {
        var evidence = new PackagingInspection { sourceReport = Arg("-rebirthCamperExistingReport") };
        string destination = Arg("-rebirthCamperInspectionReport");
        AssetBundle loaded = null;
        try
        {
            if (Application.unityVersion != "2022.3.62f2" || string.IsNullOrEmpty(destination) || File.Exists(destination))
                throw new Exception("Exact Unity and fresh inspection receipt required.");
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(evidence.sourceReport));
            if (report.compressionMode != "FastLZ4" || !report.recompressionSucceeded || report.bundleCRC == 0
                || string.IsNullOrEmpty(report.uncompressedBundlePath) || !File.Exists(report.uncompressedBundlePath) || !File.Exists(report.bundlePath))
                throw new Exception("Retained successful native recompression with nonzero expected CRC required.");
            evidence.expectedCRC = report.bundleCRC;
            evidence.inputHash = Hash(report.uncompressedBundlePath); evidence.outputHash = Hash(report.bundlePath);
            if (evidence.inputHash != report.uncompressedBundleSha256) throw new Exception("Raw source hash differs from build evidence.");
            evidence.rawCRCExtractionSucceeded = BuildPipeline.GetCRCForAssetBundle(report.uncompressedBundlePath, out uint rawCRC);
            evidence.rawExtractedCRC = rawCRC;
            evidence.finalCRCExtractionSucceeded = BuildPipeline.GetCRCForAssetBundle(report.bundlePath, out uint finalCRC);
            evidence.finalExtractedCRC = finalCRC;
            loaded = AssetBundle.LoadFromFile(report.uncompressedBundlePath, evidence.expectedCRC);
            evidence.rawExpectedCRCLoadSucceeded = loaded != null;
            if (loaded == null) throw new Exception("Raw native expected-CRC load failed.");
            loaded.Unload(true); loaded = null;
            loaded = AssetBundle.LoadFromFile(report.bundlePath, evidence.expectedCRC);
            evidence.finalExpectedCRCLoadSucceeded = loaded != null;
            if (loaded == null) throw new Exception("Final native expected-CRC load failed.");
            Validate(loaded.LoadAsset<GameObject>(report.prefabAsset.ToLowerInvariant()), report);
            evidence.finalPrefabVerified = true;
            loaded.Unload(true); loaded = null;
            evidence.immutableArtifacts = Hash(report.uncompressedBundlePath) == evidence.inputHash && Hash(report.bundlePath) == evidence.outputHash;
            if (!evidence.immutableArtifacts) throw new Exception("Inspection altered archives.");
            evidence.status = "PASS_EXPECTED_CRC_LOAD_AND_PREFAB_INSPECTION_NOT_INSTALLATION";
        }
        catch (Exception error) { evidence.error = error.ToString(); Debug.LogError(error); }
        finally
        {
            if (loaded != null) loaded.Unload(true);
            if (!string.IsNullOrEmpty(destination) && !File.Exists(destination))
            { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))); File.WriteAllText(destination, JsonUtility.ToJson(evidence, true)); }
        }
        EditorApplication.Exit(evidence.finalPrefabVerified && evidence.immutableArtifacts ? 0 : 1);
    }

    // Reverify a completed immutable bundle after a verifier-only correction.
    // Keep the original failed build receipt; never rebuild or replace its bytes.
    public static void VerifyExistingBatch()
    {
        string originalReport = Arg("-rebirthCamperExistingReport");
        string output = Arg("-rebirthCamperVerifyReport");
        if (string.IsNullOrEmpty(output) || File.Exists(Path.Combine(output, "result.json"))
            || Path.GetFullPath(Path.Combine(output, "result.json")) == Path.GetFullPath(originalReport))
            throw new Exception("Fresh distinct verification receipt required; preserve original report.");
        Directory.CreateDirectory(output);
        Report r = JsonUtility.FromJson<Report>(File.ReadAllText(originalReport));
        r.verifiedSourceReport = originalReport; r.verifiedSourceReportSha256 = Hash(originalReport);
        r.verifiedOriginalStatus = r.status; r.status = "FAIL"; r.prefabVerified = false;
        AssetBundle bundle = null;
        string beforeHash = "";
        try
        {
            if (Application.unityVersion != "2022.3.62f2" || string.IsNullOrEmpty(r.bundlePath) || !File.Exists(r.bundlePath))
                throw new Exception("Completed bundle and exact Unity required.");
            beforeHash = Hash(r.bundlePath);
            if (r.compressionMode == "FastLZ4")
            {
                if (!r.recompressionSucceeded || r.bundleCRC == 0 || !File.Exists(r.uncompressedBundlePath)
                    || Hash(r.uncompressedBundlePath) != r.uncompressedBundleSha256)
                    throw new Exception("Fast archive native success/nonzero CRC/preserved raw source evidence required.");
                if (!BuildPipeline.GetCRCForAssetBundle(r.uncompressedBundlePath, out uint rawCRC) || rawCRC != r.bundleCRC)
                    throw new Exception("Raw native CRC differs from build evidence.");
                var raw = AssetBundle.LoadFromFile(r.uncompressedBundlePath, r.bundleCRC);
                r.rawExpectedCRCLoadSucceeded = raw != null;
                if (raw == null) throw new Exception("Raw archive expected-CRC load failed.");
                raw.Unload(true);
                r.finalCRCExtractionSucceeded = BuildPipeline.GetCRCForAssetBundle(r.bundlePath, out uint finalCRC);
                r.finalExtractedCRC = finalCRC;
                if (r.finalCRCExtractionSucceeded && finalCRC != r.bundleCRC)
                    throw new Exception("Extracted final CRC differs from expected native CRC.");
            }
            bundle = r.compressionMode == "FastLZ4" ? AssetBundle.LoadFromFile(r.bundlePath, r.bundleCRC) : AssetBundle.LoadFromFile(r.bundlePath);
            r.finalExpectedCRCLoadSucceeded = bundle != null && r.compressionMode == "FastLZ4";
            if (bundle == null) throw new Exception("Existing bundle reopen failed.");
            r.assetNames = bundle.GetAllAssetNames();
            if (r.assetNames.Length != 1 || r.assetNames[0] != r.prefabAsset.ToLowerInvariant())
                throw new Exception("Existing bundle prefab identity mismatch.");
            var prefab = bundle.LoadAsset<GameObject>(r.prefabAsset.ToLowerInvariant());
            Validate(prefab, r);
            r.transformPaths = prefab.GetComponentsInChildren<Transform>(true).Select(t=>AnimationUtility.CalculateTransformPath(t,prefab.transform)).ToArray();
            string project=Directory.GetParent(Application.dataPath).FullName;
            const string profile="Assets/REBIRTHCamperDonorPoseGenerated/4c31a760c8fd442b9e2d882e7e16712c/UNAPPROVED_Camper_DonorPose_Profile.asset";
            r.originalsUnchanged=Hash(Path.Combine(project,r.sourcePrefab))==r.sourceSha256
                && Hash(Path.Combine(project,profile))==r.profileSourceSha256
                && Hash(Path.Combine(project,"Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab"))=="75357bec0ab35826b9205011811f49ca743ea42270a30fb2baa0b82e358b916d";
            r.verifiedSourceReportUnchanged = Hash(originalReport) == r.verifiedSourceReportSha256;
            if(!r.originalsUnchanged || Hash(r.bundlePath)!=beforeHash || !r.verifiedSourceReportUnchanged
                || (r.compressionMode == "FastLZ4" && Hash(r.uncompressedBundlePath) != r.uncompressedBundleSha256))
                throw new Exception("Source, original receipt or bundle preservation failed.");
            r.bundleSha256=beforeHash; r.bundleBytes=new FileInfo(r.bundlePath).Length;
            r.prefabVerified=true; r.error="";
            r.steps=(r.steps??new string[0]).Concat(new[]{"PASS: immutable existing bundle reverified with native CRC load and full prefab validation; original receipt retained"}).ToArray();
            r.status="PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED";
        }
        catch(Exception error){r.status="FAIL";r.error=error.ToString();Debug.LogError(error);}
        finally
        {
            if(bundle!=null) bundle.Unload(true);
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(r,true));
        }
        EditorApplication.Exit(r.status=="PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED"?0:1);
    }

    static void Validate(GameObject prefab, Report r)
    {
        if (prefab == null) throw new Exception("Prefab missing.");
        var modelRoot = prefab.transform.Find("GameObject");
        var nativePhysicsRoot = prefab.transform.Find("Physics");
        if (modelRoot != null)
        {
            if (prefab.GetComponent<Collider>() != null || prefab.GetComponent<Rigidbody>() != null
                || modelRoot.localPosition != Vector3.zero || modelRoot.localRotation != Quaternion.identity
                || modelRoot.localScale != Vector3.one)
                throw new Exception("Native entity/model separation wrapper invalid.");
            prefab = modelRoot.gameObject;
        }
        ValidateSurfaceImports(prefab, r);
        if (r.bedPoseAuthored)
        {
            var asset = prefab.transform.Find(RebirthCamperBedPoseAuthoring.HolderPath)?.GetComponent<Animator>();
            var clips = asset?.runtimeAnimatorController?.animationClips.Where(c => c != null && c.name == r.bedPoseClipName).Distinct().ToArray();
            if (r.seats == null || r.seats.Length != 6 || asset == null || asset.enabled
                || clips == null || clips.Length != 1 || !RebirthCamperBedPoseAuthoring.Valid(clips[0])
                || asset.transform.Find("NativeRigLayout") == null
                || r.bedPoseClipName != RebirthCamperBedPoseAuthoring.ClipName)
                throw new Exception("Six-seat authored native generic bed clip/layout dependency missing/invalid.");
            for (int i = 0; i < 6; i++)
            {
                var anchor = prefab.transform.Find("Seat" + i);
                if (anchor == null || r.seats[i].index != i || r.seats[i].pose != (i == 0 ? 40 : 41)
                    || Vector3.Distance(anchor.localPosition, r.seats[i].position) > .00001f
                    || Quaternion.Angle(anchor.localRotation, Quaternion.Euler(r.seats[i].rotation)) > .001f)
                    throw new Exception("Cabin anchor missing/changed: Seat" + i);
            }
        }

        // The same Validate method checks saved prefab and reopened bundle. Older receipts
        // do not claim lens-emission authoring and retain their historical verification scope.
        if (r.headlightEmissionAuthored)
        {
            var lens = prefab.transform.Find("M/VisualBody/80sCamperTruck/headlights_glass")?.GetComponent<Renderer>();
            var material = lens == null ? null : lens.sharedMaterial;
            if (lens == null || lens.sharedMaterials.Length != 1 || material == null
                || r.headlightEmissionMaterialName != "80sCamperTruck_headlights_glass"
                || material.name != r.headlightEmissionMaterialName || material.shader == null
                || string.IsNullOrEmpty(r.headlightEmissionShader) || material.shader.name != r.headlightEmissionShader
                || !material.HasProperty("_EmissionColor") || !material.IsKeywordEnabled("_EMISSION")
                || material.globalIlluminationFlags != MaterialGlobalIlluminationFlags.RealtimeEmissive)
                throw new Exception("Generated headlight lens emission missing/changed.");
        }
        var paintRenderer = prefab.transform.Find("M/VisualBody/80sCamperTruck/body")?.GetComponent<Renderer>();
        var paint = paintRenderer == null ? null : paintRenderer.sharedMaterial;
        var blue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Perfectvertex/80sCamperTruck/Art/Materials/80sCamperTruck_body_blue.mat");
        var white = AssetDatabase.LoadAssetAtPath<Material>("Assets/Perfectvertex/80sCamperTruck/Art/Materials/80sCamperTruck_body_white.mat");
        if (paint == null || paint.shader == null || paint.shader.name != "REBIRTH/CamperBodyDye" || blue == null || white == null)
            throw new Exception("Isolated native dye paint material/source missing.");
        var maps = new List<SurfaceMap>();
        foreach (string property in new[] { "_MainTex", "_NeutralTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
        {
            Texture expected = property == "_NeutralTex" ? white.mainTexture : blue.GetTexture(property);
            Texture actual = paint.GetTexture(property);
            if ((expected == null) != (actual == null) || (expected != null
                && (expected.name != actual.name || expected.width != actual.width || expected.height != actual.height)))
                throw new Exception("Paint surface texture changed/missing: " + property);
            maps.Add(new SurfaceMap { property = property, texture = actual == null ? null : actual.name,
                width = actual == null ? 0 : actual.width, height = actual == null ? 0 : actual.height });
        }
        foreach (string property in new[] { "_BumpScale", "_GlossMapScale", "_OcclusionStrength" })
            if (Mathf.Abs(paint.GetFloat(property) - blue.GetFloat(property)) > .00001f)
                throw new Exception("Paint normal/smoothness/occlusion setting changed: " + property);
        r.bodySurfaceMaps = maps.ToArray();
        r.bodySurfaceMapsVerified = true;
        var physics = nativePhysicsRoot != null ? nativePhysicsRoot : prefab.transform.Find("Physics");
        if (physics == null || physics.tag != "Physics" || physics.GetComponent<Rigidbody>() == null) throw new Exception("Native Physics root/rigidbody missing.");
        var body = physics.GetComponent<Rigidbody>();
        if (Mathf.Abs(body.mass - 2400f) > .001f || Mathf.Abs(body.drag - .05f) > .00001f
            || Mathf.Abs(body.angularDrag - .05f) > .00001f || Vector3.Distance(body.centerOfMass, new Vector3(0, .1f, 0)) > .00001f)
            throw new Exception("Native 4x4 rigidbody baseline not preserved.");
        foreach (var wheel in physics.GetComponentsInChildren<WheelCollider>(true))
        {
            var spring = wheel.suspensionSpring;
            var forward = wheel.forwardFriction;
            var side = wheel.sidewaysFriction;
            if (Mathf.Abs(wheel.mass - 45f) > .001f || Mathf.Abs(wheel.suspensionDistance - .43f) > .00001f
                || Mathf.Abs(spring.spring - 15000f) > .001f || Mathf.Abs(spring.damper - 2000f) > .001f
                || Mathf.Abs(spring.targetPosition - .5f) > .00001f
                || Mathf.Abs(forward.extremumSlip - .4f) > .00001f || Mathf.Abs(forward.extremumValue - 1f) > .00001f
                || Mathf.Abs(forward.asymptoteSlip - .8f) > .00001f || Mathf.Abs(forward.asymptoteValue - .5f) > .00001f
                || Mathf.Abs(forward.stiffness - 1.8f) > .00001f
                || Mathf.Abs(side.extremumSlip - .2f) > .00001f || Mathf.Abs(side.extremumValue - 1f) > .00001f
                || Mathf.Abs(side.asymptoteSlip - .75f) > .00001f || Mathf.Abs(side.asymptoteValue - .75f) > .00001f
                || Mathf.Abs(side.stiffness - 1.8f) > .00001f)
                throw new Exception("Native 4x4 suspension/friction baseline not preserved.");
        }
        r.rendererCount = prefab.GetComponentsInChildren<Renderer>(true).Length;
        r.wheelColliderCount = physics.GetComponentsInChildren<WheelCollider>(true).Length;
        r.bodyColliderCount = physics.GetComponentsInChildren<BoxCollider>(true).Length;
        r.spotLightCount = prefab.GetComponentsInChildren<Light>(true).Count(x => x.type == LightType.Spot);
        if (!r.rearLampAuthored || r.directionalLamps == null || r.rendererCount != 18 || r.wheelColliderCount != 4 || r.bodyColliderCount != 2 || r.spotLightCount != 4 + r.directionalLamps.lightCount) throw new Exception("Prefab component counts incorrect.");
        var rearLenses = prefab.transform.Find("M/VisualBody/80sCamperTruck/lights/RearRedLenses");
        var rearLights = prefab.transform.Find("M/RearLights");
        if (rearLenses == null || rearLights == null || rearLenses.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3 != r.rearLampTriangles)
            throw new Exception("Measured rear lens geometry missing.");
        var rearMaterial = rearLenses.GetComponent<Renderer>().sharedMaterial;
        if (rearMaterial == null || rearMaterial.shader == null || rearMaterial.shader.name != "Standard"
            || !rearMaterial.HasProperty("_EmissionColor") || !rearMaterial.IsKeywordEnabled("_EMISSION"))
            throw new Exception("Rear emissive material/shader state missing.");
        var redLights = rearLights.GetComponentsInChildren<Light>(true);
        if (redLights.Length != 2 || redLights.Any(light => light.enabled || light.intensity != 0 || light.color != Color.red))
            throw new Exception("Rear red emitters must initially be disabled.");
        if (redLights.Any(light => light.range <= 0 || Vector3.Dot(prefab.transform.InverseTransformDirection(light.transform.forward),Vector3.back) < .999f))
            throw new Exception("Rear emitters must point backward with positive range.");
                var markerLenses=prefab.transform.Find("M/VisualBody/80sCamperTruck/lights/BodyMarkerLenses");
        var markerLights=prefab.transform.Find("M/BodyMarkerLights");
        if(markerLenses==null || markerLights==null || r.directionalLamps.roofTriangles<=0 || r.directionalLamps.redBodyTriangles<=0
            || markerLenses.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3!=r.directionalLamps.triangles)
            throw new Exception("Measured roof/body marker geometry missing.");
        var markerMaterial=markerLenses.GetComponent<Renderer>().sharedMaterial;
        if(markerMaterial==null || markerMaterial.shader.name!="Standard" || !markerMaterial.IsKeywordEnabled("_EMISSION") || markerMaterial.globalIlluminationFlags!=MaterialGlobalIlluminationFlags.RealtimeEmissive || markerMaterial.mainTexture!=null)
            throw new Exception("Generated red marker material missing/changed.");
        var markers=markerLights.GetComponentsInChildren<Light>(true);
        if(markers.Length!=r.directionalLamps.lightCount || markers.Length<2 || markers.Any(l=>l.enabled || l.intensity!=0 || l.color!=Color.red || l.type!=LightType.Spot || Mathf.Abs(l.range-.65f)>.0001f))
            throw new Exception("Marker local emitters invalid.");
                if(r.directionalLamps.markerPositions==null || r.directionalLamps.markerDirections==null || r.directionalLamps.markerPositions.Length!=markers.Length || r.directionalLamps.markerDirections.Length!=markers.Length)
            throw new Exception("Marker emitter direction receipt missing.");
        for(int i=0;i<markers.Length;i++)
        {
            var lamp=markerLights.Find("RedMarker"+i)?.GetComponent<Light>();
            if(lamp==null || Vector3.Distance(prefab.transform.InverseTransformPoint(lamp.transform.position),r.directionalLamps.markerPositions[i])>.0001f
                || Vector3.Dot(prefab.transform.InverseTransformDirection(lamp.transform.forward),r.directionalLamps.markerDirections[i])<.999f)
                throw new Exception("Marker emitter position/outward direction changed.");
        }
        var front=prefab.transform.Find("M/HeadLights/Lights").GetComponentsInChildren<Light>(true);
        if(front.Length!=2 || front.Any(l=>l.type!=LightType.Spot || Mathf.Abs(l.range-32f)>.0001f || Mathf.Abs(l.spotAngle-48f)>.0001f
            || Vector3.Dot(prefab.transform.InverseTransformDirection(l.transform.forward),Vector3.forward)<.99f))throw new Exception("Forward directional headlight gate failed.");
        if(redLights.Any(l=>Mathf.Abs(l.range-4f)>.0001f || Mathf.Abs(l.spotAngle-48f)>.0001f))throw new Exception("Restrained rear cone gate failed.");
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0 || t.GetComponents<MonoBehaviour>().Length != 0) throw new Exception("Unexpected/missing runtime script: " + t.name);
        for (int i = 0; i < 4; i++)
            if (physics.Find("Wheel" + i)?.GetComponent<WheelCollider>() == null || prefab.transform.Find("Wheel" + i) == null) throw new Exception("Wheel pair missing.");
    }
    [Serializable] public class CabinPlan { public string status; public int capacity; public Seat[] authoringSeats; }
    // Copy imported assets rather than editing the vendor artwork or its importer.
    // Roles come from the shader property, never from a texture's filename.
    static void OptimizeSurfaceImports(GameObject root, string folder, Report r)
    {
        var textures = new List<TextureCopy>();
        var bindings = new List<TextureBinding>();
        var materialCopies = new Dictionary<Material, Material>();
        var textureIndices = new Dictionary<string, int>();
        var originalMaterials = new List<SourceAsset>();
        EnsureFolder(folder + "/SurfaceMaterials");
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                var original = materials[slot];
                if (!materialCopies.TryGetValue(original, out var copy))
                {
                    copy = new Material(original) { name = original.name };
                    materialCopies.Add(original, copy);
                    string originalPath = AssetDatabase.GetAssetPath(original);
                    if (originalPath.StartsWith("Assets/Perfectvertex/", StringComparison.Ordinal))
                        originalMaterials.Add(new SourceAsset { path = originalPath, hash = AssetHash(originalPath), metaHash = AssetHash(originalPath + ".meta") });
                    AssetDatabase.CreateAsset(copy, folder + "/SurfaceMaterials/material_" + materialCopies.Count + ".mat");
                    // CreateAsset adopts its filename; restore the native name used by measured lamp guards.
                    copy.name = original.name;
                    EditorUtility.SetDirty(copy);
                }
                foreach (string property in original.GetTexturePropertyNames())
                {
                    var sourceTexture = original.GetTexture(property);
                    if (sourceTexture == null) continue;
                    string source = AssetDatabase.GetAssetPath(sourceTexture);
                    var importer = AssetImporter.GetAtPath(source) as TextureImporter;
                    if (importer == null) throw new Exception("Texture importer unavailable: " + source);
                    bool standard = original.shader.name == "Standard" || original.shader.name == "REBIRTH/CamperBodyDye";
                    bool linear = standard && (property == "_MetallicGlossMap" || property == "_OcclusionMap"
                        || property == "_DetailMask" || property == "_ParallaxMap");
                    string key = source + "|" + linear;
                    if (!textureIndices.TryGetValue(key, out int index))
                    {
                        index = textures.Count;
                        string textureFolder = folder + "/SurfaceTextures/" + AssetDatabase.AssetPathToGUID(source) + (linear ? "_linear" : "_original");
                        EnsureFolder(textureFolder);
                        string generated = textureFolder + "/" + Path.GetFileName(source);
                        if (!AssetDatabase.CopyAsset(source, generated)) throw new Exception("Texture copy failed: " + source);
                        var generatedImporter = AssetImporter.GetAtPath(generated) as TextureImporter;
                        if (linear) generatedImporter.sRGBTexture = false;
                        generatedImporter.filterMode = FilterMode.Trilinear;
                        generatedImporter.anisoLevel = Math.Max(4, importer.anisoLevel);
                        // CopyAsset retains normal type, compression, mip policy and platform resolution.
                        generatedImporter.SaveAndReimport();
                        var generatedTexture = AssetDatabase.LoadAssetAtPath<Texture>(generated);
                        if (generatedTexture.width != sourceTexture.width || generatedTexture.height != sourceTexture.height)
                            throw new Exception("Imported texture resolution changed: " + source);
                        if (linear && generatedTexture.isDataSRGB) throw new Exception("Scalar texture imported as sRGB: " + generated);
                        textures.Add(new TextureCopy { source = source, generated = generated,
                            sourceHash = AssetHash(source), sourceMetaHash = AssetHash(source + ".meta"), linear = linear,
                            generatedSRGB = generatedTexture.isDataSRGB, generatedGraphicsFormat = generatedTexture.graphicsFormat.ToString(),
                            width = sourceTexture.width, height = sourceTexture.height });
                        textureIndices.Add(key, index);
                    }
                    copy.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture>(textures[index].generated));
                    bindings.Add(new TextureBinding { renderer = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform),
                        slot = slot, property = property, textureIndex = index });
                }
                EditorUtility.SetDirty(copy);
                materials[slot] = copy;
            }
            renderer.sharedMaterials = materials;
        }
        r.optimizedTextures = textures.ToArray();
        r.optimizedBindings = bindings.ToArray();
        r.originalSurfaceMaterials = originalMaterials.ToArray();
    }
    static void ValidateSurfaceImports(GameObject prefab, Report r)
    {
        // Legacy immutable receipts remain verifiable without claiming optimized imports.
        if (r.optimizedTextures == null && r.optimizedBindings == null) return;
        if (r.optimizedTextures == null || r.optimizedBindings == null) throw new Exception("Incomplete surface import receipt.");
        if (r.originalSurfaceMaterials == null) throw new Exception("Original surface material receipt missing.");
        foreach (var material in r.originalSurfaceMaterials)
            if (AssetHash(material.path) != material.hash || AssetHash(material.path + ".meta") != material.metaHash)
                throw new Exception("Original surface material changed: " + material.path);
        foreach (var entry in r.optimizedTextures)
        {
            if (AssetHash(entry.source) != entry.sourceHash || AssetHash(entry.source + ".meta") != entry.sourceMetaHash
                || AssetHash(entry.generated) != entry.sourceHash) throw new Exception("Surface source/copy bytes changed: " + entry.source);
            var source = AssetImporter.GetAtPath(entry.source) as TextureImporter;
            var generated = AssetImporter.GetAtPath(entry.generated) as TextureImporter;
            if (source == null || generated == null || generated.sRGBTexture != (entry.linear ? false : source.sRGBTexture)
                || generated.textureType != source.textureType || generated.mipmapEnabled != source.mipmapEnabled
                || generated.filterMode != FilterMode.Trilinear || generated.anisoLevel != Math.Max(4, source.anisoLevel))
                throw new Exception("Generated surface importer invalid: " + entry.generated);
        }
        foreach (var binding in r.optimizedBindings)
        {
            if (binding.textureIndex < 0 || binding.textureIndex >= r.optimizedTextures.Length)
                throw new Exception("Optimized surface texture index invalid: " + binding.renderer + "/" + binding.property);
            var renderer = prefab.transform.Find(binding.renderer)?.GetComponent<Renderer>();
            if (renderer == null || binding.slot < 0 || binding.slot >= renderer.sharedMaterials.Length)
                throw new Exception("Optimized surface renderer/slot missing: " + binding.renderer);
            var actual = renderer.sharedMaterials[binding.slot].GetTexture(binding.property);
            var entry = r.optimizedTextures[binding.textureIndex];
            var expected = AssetDatabase.LoadAssetAtPath<Texture>(entry.generated);
            if (actual == null || expected == null || actual.name != expected.name || actual.width != entry.width
                || actual.height != entry.height || actual.filterMode != FilterMode.Trilinear || actual.anisoLevel != expected.anisoLevel
                || string.IsNullOrEmpty(entry.generatedGraphicsFormat) || expected.graphicsFormat.ToString() != entry.generatedGraphicsFormat
                || expected.isDataSRGB != entry.generatedSRGB || (entry.linear && expected.isDataSRGB)
                || actual.graphicsFormat != expected.graphicsFormat || actual.isDataSRGB != expected.isDataSRGB)
                throw new Exception("Optimized surface binding invalid: " + binding.renderer + "/" + binding.property);
        }
    }
    static string AssetHash(string path) { return Hash(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path)); }
    static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    static void AddHull(Transform physics, string name, Vector3 center, Vector3 size)
    { var t = Child(physics, name); t.gameObject.layer = 21; var c = t.gameObject.AddComponent<BoxCollider>(); c.center = center; c.size = size; }
    static void EnsureTag(string tag)
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = settings.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++) if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize); tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        settings.ApplyModifiedProperties();
    }
    static void EnsureFolder(string folder)
    { string path = "Assets"; foreach (var segment in folder.Split('/').Skip(1)) { string next = path + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, segment); path = next; } }
    static string Arg(string key) { var args = Environment.GetCommandLineArgs(); for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1]; return ""; }
    static string Hash(string file) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
}




