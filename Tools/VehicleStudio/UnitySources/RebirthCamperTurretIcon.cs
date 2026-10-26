using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class RebirthCamperTurretIcon
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camper=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperTurrets_20261019/rebirthCamper80s.prefab"));
        var mount=camper.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot0");
        var gun=mount.Cast<Transform>().Single(t=>t.name.StartsWith("Optimized Tripo turret"));
        gun.SetParent(null,true);gun.gameObject.SetActive(true);gun.rotation=Quaternion.identity;Object.DestroyImmediate(camper);
        foreach(var lod in gun.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
        var renderers=gun.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.62f,.67f);
        var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.6f;key.transform.rotation=Quaternion.Euler(35,-40,0);
        var fill=new GameObject("Rim").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.7f;fill.transform.rotation=Quaternion.Euler(15,160,0);
        var camera=new GameObject("Icon camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=bounds.size.magnitude*.46f;camera.nearClipPlane=.01f;
        camera.transform.position=bounds.center+new Vector3(-1,.5f,1.3f)*bounds.size.magnitude;camera.transform.LookAt(bounds.center);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){antiAliasing=4};camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(512,512,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();
        string output="E:/_Haven/shared/REBIRTHStaged/CamperTurretRefresh_20261019/Artifacts";Directory.CreateDirectory(output);File.WriteAllBytes(output+"/rebirthCamperTurret.png",image.EncodeToPNG());
        Debug.Log("PASS actual Camper Turret icon rendered with alpha");
    }
}
