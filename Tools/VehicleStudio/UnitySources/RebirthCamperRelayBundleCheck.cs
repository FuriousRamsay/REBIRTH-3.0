using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

public static class RebirthCamperRelayBundleCheck
{
    public static void Run()
    {
        const string bundlePath = "C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Resources/rebirth_camper80s.unity3d";
        const string receiptPath = "C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/_Documentation/Camper/RemainingFixes_20261020/relay-bundle-check.txt";
        var bundle = AssetBundle.LoadFromFile(bundlePath);
        if (!bundle) throw new Exception("Installed camper bundle did not open");
        GameObject instance = null;
        try
        {
            instance = Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));
            var root = instance.transform;
            var battery = root.GetComponentsInChildren<BoxCollider>(true).Single(c => c.name == "REBIRTHInteraction_battery_bank");
            var skin = root.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "camper_body" && f.sharedMesh);
            if (!skin.sharedMesh.isReadable) throw new Exception("Installed camper skin is not CPU-readable");
            var center = root.InverseTransformPoint(battery.transform.TransformPoint(battery.center));
            float top = float.NegativeInfinity;
            for (int i = 0; i < 8; i++) top = Mathf.Max(top, root.InverseTransformPoint(battery.transform.TransformPoint(battery.center + Vector3.Scale(battery.size * .5f,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))).y);
            var vertices = skin.sharedMesh.vertices.Select(v => root.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
            var triangles = skin.sharedMesh.triangles;
            var report = new System.Collections.Generic.List<string> { "PASS installed bundle opened at native rebirthCamper80s address", "PASS camper_body.isReadable=true; production mesh access supported" };
            foreach (int side in new[] { -1, 1 })
            {
                if (!RebirthCamperRelayGeometry.Surface(vertices, triangles, new Vector3(side * 3, top + .12f, center.z), new Vector3(-side, 0, 0), out var point, out var normal))
                    throw new Exception("Relay mounting ray missed installed skin side " + side);
                if (normal.x * side < .9f) throw new Exception("Relay mounting normal not outward");
                report.Add("PASS side=" + side + " point=" + point.ToString("F6") + " normal=" + normal.ToString("F6"));
            }
            report.Add("Offline Unity read-only verification; game not launched; bundle not modified.");
            File.WriteAllLines(receiptPath, report);
            Debug.Log("REBIRTH_RELAY_BUNDLE_CHECK_PASS");
        }
        finally { if (instance) Object.DestroyImmediate(instance); bundle.Unload(true); }
    }
}
