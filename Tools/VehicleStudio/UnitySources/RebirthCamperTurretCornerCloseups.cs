using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Read-only scene review of the exact reduced-size candidate; no geometry edits.
public static class RebirthCamperTurretCornerCloseups
{
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperFourTurretReview_20261019_v9/Closeups";
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperFourTurretReview_20261019_v9/FourTurretCamperReview.prefab"));
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
            if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"}.Contains(t.name))t.gameObject.SetActive(true);
            if(t.name=="REBIRTHRearDoorPivot")t.localRotation=Quaternion.Euler(0,-90,0);
        }
        foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
        var clay=new Material(Shader.Find("Standard")){color=new Color(.64f,.64f,.64f)};clay.SetFloat("_Glossiness",.2f);
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>clay).ToArray();
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
        var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1;key.shadows=LightShadows.Soft;key.transform.rotation=Quaternion.Euler(35,35,0);
        var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.transform.rotation=Quaternion.Euler(25,-130,0);
        QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=20;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;
        var target=new Vector3(-1.03f,2.86f,-2.88f);
        Render(new Vector3(-3.4f,4.05f,-5.2f),target,.73f,"01-rear-outside-quarter");
        Render(new Vector3(-1.03f,3.01f,-6),target,.73f,"02-straight-rear");
        Render(new Vector3(-4,2.86f,-2.88f),target,.73f,"03-straight-side");
        Render(new Vector3(-1.03f,6,-3.15f),target,.73f,"04-top");
        Render(new Vector3(-3.4f,3.8f,-1.7f),target,.73f,"05-forward-outside-quarter");
        Render(new Vector3(-2.8f,2.25f,-4.5f),new Vector3(-1.03f,2.80f,-2.80f),.58f,"06-under-platform");
        Render(new Vector3(3.4f,3.8f,-1.7f),new Vector3(1.03f,2.86f,-2.88f),.73f,"07-right-corner");
        File.WriteAllText(Output+"/review.txt","Six clay closeups of highlighted rear-left corner. Exact v9 prefab, all assemblies uniformly reduced35%. Same geometry and positioning; no installation, no material/mesh/prefab saves. Views: rear-outside quarter, rear, side, top, forward-outside quarter, under-platform. Native source LOD0 forced for inspection.");
        Object.DestroyImmediate(root);
    }
    static void Render(Vector3 eye,Vector3 target,float size,string name)
    {
        var c=new GameObject(name).AddComponent<Camera>();c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.01f;c.transform.position=eye;c.transform.LookAt(target);c.backgroundColor=new Color(.16f,.17f,.18f);c.clearFlags=CameraClearFlags.SolidColor;
        var rt=new RenderTexture(1400,1400,24){antiAliasing=4};c.targetTexture=rt;c.Render();RenderTexture.active=rt;
        var image=new Texture2D(1400,1400,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1400),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;c.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(c.gameObject);
    }
}
