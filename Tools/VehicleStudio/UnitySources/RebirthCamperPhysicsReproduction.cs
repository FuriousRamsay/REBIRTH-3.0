using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Copy into Assets/Editor. Never edits/saves the prefab or builds a bundle.
public static class RebirthCamperPhysicsReproduction
{
    const string DefaultReport = "E:/_Haven/shared/REBIRTHTemp/CamperBuildReports/80031d3d124a41aab0bb0b522d8ac48b/verified/result.json";
    [Serializable] class Source { public string prefabAsset; }
    [Serializable] class Sample
    {
        public float time, speed, angularSpeed, roll, tilt, displacement;
        public Vector3 position, velocity, angularVelocity;
        public Quaternion rotation;
        public int groundedWheels;
    }
    [Serializable] class ColliderRecord
    {
        public string path, type, rigidbodyPath;
        public int layer;
        public bool enabled, trigger, active;
        public Vector3 localPosition, localScale, worldCenter, worldSize;
        public Quaternion localRotation;
    }
    [Serializable] class Case
    {
        public string name, status = "FAIL", reason;
        public string rootPath, modelPath, physicsPath;
        public bool modelIsEntityRoot;
        public bool physicsInitializedByNameFallback, physicsIsRootSiblingOfModel;
        public float spawnHeight, mass, drag, angularDrag;
        public Vector3 centerOfMass, inertiaTensor;
        public ColliderRecord[] colliders;
        public List<Sample> samples = new List<Sample>();
    }
    [Serializable] class Report
    {
        public string status = "FAIL", unityVersion, prefabAsset, sourceReport, error;
        public string scope = "Offline isolated Unity physics; not game validation. Native custom gravity and separated hierarchy; no game controllers/network/world origin.";
        public float duration = 10, step = .02f;
        public bool interactionPhysicsIgnored = true;
        public List<Case> cases = new List<Case>();
    }

    public static void RunBatch()
    {
        var report = new Report { unityVersion = Application.unityVersion, sourceReport = Arg("-rebirthPhysicsSource", DefaultReport) };
        string output = Arg("-rebirthPhysicsOutput", "E:/_Haven/shared/REBIRTHTemp/CamperPhysicsReproduction");
        Directory.CreateDirectory(output);
        bool oldIgnore = Physics.GetIgnoreLayerCollision(14, 21);
        bool oldGroundIgnore = Physics.GetIgnoreLayerCollision(0, 21);
        bool oldAutoSimulation = Physics.autoSimulation;
        try
        {
            if (!Application.isBatchMode || Application.isPlaying)
                throw new Exception("Run only in a dedicated Unity batch editor process; scene replacement is intentional.");
            Physics.autoSimulation = false;
            report.prefabAsset = Arg("-rebirthPhysicsPrefab", JsonUtility.FromJson<Source>(File.ReadAllText(report.sourceReport)).prefabAsset);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(report.prefabAsset);
            if (!prefab) throw new Exception("Prefab not found: " + report.prefabAsset);
            Physics.IgnoreLayerCollision(14, 21, true);
            Physics.IgnoreLayerCollision(0, 21, false);
            foreach (bool uneven in new[] { false, true })
                foreach (float height in new[] { 0f, .5f })
                    report.cases.Add(RunCase(prefab, uneven, height));
            report.status = report.cases.All(c => c.status == "PASS") ? "PASS" : "FAIL";
        }
        catch (Exception e) { report.error = e.ToString(); }
        finally
        {
            Physics.IgnoreLayerCollision(14, 21, oldIgnore);
            Physics.IgnoreLayerCollision(0, 21, oldGroundIgnore);
            Physics.autoSimulation = oldAutoSimulation;
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(report, true));
        }
        if (report.status != "PASS") throw new Exception("Offline physics reproduction failed; see " + output + "/result.json");
    }

    static Case RunCase(GameObject prefab, bool uneven, float height)
    {
        var result = new Case { name = (uneven ? "uneven" : "flat") + "_height_" + height, spawnHeight = height };
        // Dedicated batch editor only: runtime SceneManager.CreateScene is unavailable in edit mode.
        // NewScene discards the disposable scene without saving assets or touching existing scene files.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            // Native EntityFactory.CompleteEntity uses the prefab root as RootTransform.
            // Only a direct child named GameObject separates ModelTransform from that root.
            var entity = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(entity, scene);
            entity.name = "Entity";
            Transform model = entity.transform.Find("GameObject") ?? entity.transform;
            Transform physics = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.CompareTag("Physics"));
            result.physicsInitializedByNameFallback = physics == null;
            if (physics == null) physics = entity.transform.Find("Physics");
            if (physics == null) throw new Exception("Native PhysicsInit found neither a tagged model descendant nor direct root child Physics.");
            physics.SetParent(entity.transform, false);
            result.rootPath = PathOf(entity.transform); result.modelPath = PathOf(model);
            result.physicsPath = PathOf(physics); result.modelIsEntityRoot = model == entity.transform;
            result.physicsIsRootSiblingOfModel = model != entity.transform &&
                model.parent == entity.transform && physics.parent == entity.transform;
            if (model != entity.transform && !result.physicsIsRootSiblingOfModel)
                throw new Exception("Wrapped model and Physics must be direct Entity-root siblings.");
            var rb = physics.GetComponent<Rigidbody>();
            if (!rb || rb.isKinematic || rb.useGravity) throw new Exception("Expected dynamic native custom-gravity Rigidbody.");
            if (entity.GetComponent<Rigidbody>() || model.GetComponent<Rigidbody>()) throw new Exception("Interaction root must have no Rigidbody.");
            foreach (var t in physics.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 21;
            foreach (var c in model.GetComponentsInChildren<Collider>(true))
                if (!c.transform.IsChildOf(physics)) c.gameObject.layer = 14;
            foreach (var behaviour in entity.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            var wheels = physics.GetComponentsInChildren<WheelCollider>();
            if (wheels.Length != 4) throw new Exception("Expected four wheels.");
            foreach (var w in wheels) { w.motorTorque = 0; w.brakeTorque = 0; w.steerAngle = 0; }
            // Preserve authored origin; placement height is explicit rather than auto-lifting hulls.
            rb.position = new Vector3(0, height, 0); rb.rotation = Quaternion.identity;
            // Spawn position is set once before physics; later native root/model feedback is
            // intentionally retained even when ModelTransform and RootTransform are identical.
            entity.transform.position = new Vector3(0, height, 0);
            rb.position = new Vector3(0, height, 0);
            Ground(scene, "Ground", new Vector3(0, -.5f, 0), new Vector3(100, 1, 100));
            if (uneven) Ground(scene, "LowStep", new Vector3(-.8f, .06f, 1.3f), new Vector3(1, .12f, 1));
            Physics.SyncTransforms();
            result.mass = rb.mass; result.drag = rb.drag; result.angularDrag = rb.angularDrag;
            result.centerOfMass = rb.centerOfMass; result.inertiaTensor = rb.inertiaTensor;
            result.colliders = entity.GetComponentsInChildren<Collider>(true).Select(c => new ColliderRecord {
                path = PathOf(c.transform), type = c.GetType().Name, layer = c.gameObject.layer,
                rigidbodyPath = c.attachedRigidbody ? PathOf(c.attachedRigidbody.transform) : "none",
                enabled = c.enabled, trigger = c.isTrigger, active = c.gameObject.activeInHierarchy,
                localPosition = c.transform.localPosition, localRotation = c.transform.localRotation,
                localScale = c.transform.localScale, worldCenter = c.bounds.center, worldSize = c.bounds.size
            }).ToArray();
            Vector3 start = rb.position;
            for (int i = 0; i <= 500; i++)
            {
                float roll = Mathf.DeltaAngle(0, rb.rotation.eulerAngles.z);
                var s = new Sample { time = i * .02f, position = rb.position, rotation = rb.rotation,
                    velocity = rb.velocity, angularVelocity = rb.angularVelocity,
                    speed = rb.velocity.magnitude, angularSpeed = rb.angularVelocity.magnitude,
                    roll = roll, tilt = Vector3.Angle(rb.rotation * Vector3.up, Vector3.up),
                    displacement = Vector3.Distance(start, rb.position), groundedWheels = wheels.Count(w => w.isGrounded) };
                result.samples.Add(s);
                if (!Finite(s.position) || !Finite(s.velocity) || !Finite(s.angularVelocity) ||
                    s.speed > 15 || s.angularSpeed > 10 || s.tilt > 80 || s.displacement > 15 || s.position.y < -3)
                { result.reason = "Runaway/flip/nonfinite threshold at " + s.time + " seconds"; return result; }
                if (i == 500) break;
                rb.AddForce(new Vector3(0, -9.81f * rb.mass, 0), ForceMode.Force);
                Physics.Simulate(.02f);
                // Native model follows the body. Without GameObject the model is also the
                // entity root, so this moves/rotates the body's own parent: preserve the defect.
                model.SetPositionAndRotation(rb.position, rb.rotation);
                Physics.SyncTransforms();
            }
            var last = result.samples.Last();
            if (last.groundedWheels < 3 || last.speed > .5f || last.angularSpeed > .5f || last.tilt > 25)
            { result.reason = "Did not settle upright on at least three wheels by 10 seconds"; return result; }
            result.status = "PASS"; result.reason = "Bounded simulation completed and settled within stated thresholds";
        }
        catch (Exception e) { result.reason = e.ToString(); }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        return result;
    }
    static void Ground(Scene scene, string name, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = pos; go.AddComponent<BoxCollider>().size = size;
    }
    static bool Finite(Vector3 v) { return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)); }
    static string PathOf(Transform t) { return t.parent ? PathOf(t.parent) + "/" + t.name : t.name; }
    static string Arg(string name, string fallback)
    { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback; }
}
