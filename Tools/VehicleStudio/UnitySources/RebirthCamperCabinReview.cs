using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Read-only geometry review; no prefab/material/scene assets are saved or altered.
public static class RebirthCamperCabinReview
{
    public static void RunBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        string folder = args[Array.IndexOf(args, "-camperCabinReport") + 1];
        try
        {
            if (Application.unityVersion != "2022.3.62f2") throw new Exception("Exact Unity required.");
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperPlayableGenerated/8bfe6792d86a4397966e109c4ba17924/rebirthCamper80s.prefab");
            if (prefab == null) throw new Exception("Installed candidate authoring prefab missing.");
            var root = UnityEngine.Object.Instantiate(prefab);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var interiors = renderers.Where(r => r.sharedMaterials.Any(m => m != null && m.name.IndexOf("interior", StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            File.WriteAllLines(Path.Combine(folder, "renderer_materials.txt"), renderers.Select(r => r.name + " materials=" + string.Join(",",r.sharedMaterials.Select(m => m == null ? "NULL" : m.name))));
            if (interiors.Length == 0) throw new Exception("No interior material meshes found.");
            foreach (var r in renderers) r.enabled = interiors.Contains(r);
            File.WriteAllLines(Path.Combine(folder, "interior_bounds.txt"), interiors.Select(r => r.name + " center=" + r.bounds.center.ToString("F4") + " size=" + r.bounds.size.ToString("F4")));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white * .7f;
            var light = new GameObject("ReviewLight").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(45, 25, 0);
            Bounds bounds = interiors[0].bounds;
            foreach (var r in interiors) bounds.Encapsulate(r.bounds);
            var camera = new GameObject("ReviewCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.18f,.2f,.22f);
            camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * .58f;
            camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            View(camera, bounds.center + Vector3.up * 8, bounds.center, Vector3.forward, folder, "interior_top");
            View(camera, bounds.center + new Vector3(-7,4,2), bounds.center, Vector3.up, folder, "interior_left");
            View(camera, bounds.center + new Vector3(7,4,-2), bounds.center, Vector3.up, folder, "interior_right");
            File.WriteAllText(Path.Combine(folder,"result.txt"), "PASS_GEOMETRY_REVIEW_NOT_OCCUPANT_FIT\nExterior meshes hidden for interior inspection; original assets unchanged; no passenger capacity approved.");
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(folder,"error.txt"), error.ToString()); throw; }
    }
    private static void View(Camera camera, Vector3 position, Vector3 target, Vector3 up, string folder, string name)
    {
        camera.transform.position = position; camera.transform.rotation = Quaternion.LookRotation(target-position,up);
        var rt = new RenderTexture(1024,1024,24);
        var texture = new Texture2D(1024,1024,TextureFormat.RGB24,false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0,0,1024,1024),0,0); texture.Apply();
            File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());
        }
        finally { camera.targetTexture=null; RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt); }
    }
}
