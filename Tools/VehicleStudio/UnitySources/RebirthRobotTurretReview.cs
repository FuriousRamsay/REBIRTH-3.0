using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class RebirthRobotTurretReview
{
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output";
    public static void ReimportAndReview(){RebirthRobotTurretImport.Run();Run();}
    public static void RoofGrooves()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab"));
        var f=root.GetComponentsInChildren<MeshFilter>(true).First(m=>m.name=="camper_body");
        var probe=new GameObject("Roof groove surface probe");probe.transform.SetPositionAndRotation(f.transform.position,f.transform.rotation);probe.transform.localScale=f.transform.lossyScale;
        var c=probe.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;Physics.SyncTransforms();
        var lines=new System.Collections.Generic.List<string>{"x,z,y"};
        foreach(float x in new[]{-.38f,0,.38f})for(int i=0;i<=220;i++)
        {
            float z=.30f+i*.005f;
            if(c.Raycast(new Ray(new Vector3(x,4,z),Vector3.down),out var h,3))lines.Add(x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+z.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+h.point.y.ToString("F5",System.Globalization.CultureInfo.InvariantCulture));
        }
        File.WriteAllLines(Output+"/front-roof-grooves.csv",lines);Object.DestroyImmediate(root);Object.DestroyImmediate(probe);
    }
    public static void FrontCornerReference()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab"));
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
            if(t.name.StartsWith("REBIRTHCamperTurretSlot")||new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
        var clay=new Material(Shader.Find("Standard")){color=new Color(.63f,.65f,.67f)};clay.SetFloat("_Glossiness",.22f);
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>clay).ToArray();
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        var light=new GameObject("Front key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.15f;light.transform.rotation=Quaternion.Euler(40,140,0);
        foreach(bool armor in new[]{false,true})
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor"}.Contains(t.name))t.gameObject.SetActive(armor);
            string suffix=armor?"armor-on":"armor-off";
            Render(new Vector3(-3.8f,4.6f,3.5f),new Vector3(-.7f,2.7f,.90f),.85f,"actual-front-left-"+suffix);
            Render(new Vector3(-.7f,9,.90f),new Vector3(-.7f,0,.90f),.85f,"actual-front-left-overhead-"+suffix);
        }
        File.WriteAllLines(Output+"/front-permanent-members.txt",root.GetComponentsInChildren<Renderer>(true).Where(r=>r.bounds.Intersects(new Bounds(new Vector3(-.8f,2.7f,.9f),Vector3.one*.8f))).Select(r=>r.name+" active="+r.gameObject.activeInHierarchy+" bounds="+r.bounds));
        Object.DestroyImmediate(root);Object.DestroyImmediate(clay);
    }
    public static void ActualCamperPlan()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab"));
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(t.name.StartsWith("REBIRTHCamperTurretSlot")||new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
            if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"}.Contains(t.name))t.gameObject.SetActive(true);
        }
        var clay=new Material(Shader.Find("Standard")){color=new Color(.63f,.65f,.67f)};clay.SetFloat("_Glossiness",.18f);
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>clay).ToArray();
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        var light=new GameObject("Plan key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(45,-35,0);
        Render(new Vector3(0,12,-1),new Vector3(0,0,-1),3.7f,"actual-camper-overhead");
        Render(new Vector3(-.75f,9,-3),new Vector3(-.75f,0,-3),.75f,"actual-camper-rear-left-overhead");
        File.WriteAllText(Output+"/actual-camper-plan.txt","Direct orthographic Unity renders of staged camper prefab. Turret installation roots and stations hidden; camper corner, pipe, ladder and roof geometry unchanged. No generated reinterpretation or new bracket geometry. Perspective camera is exactly vertical. Placement review only.");
        Object.DestroyImmediate(root);Object.DestroyImmediate(clay);
    }
    public static void ClayMount()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab"));
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"}.Contains(t.name))t.gameObject.SetActive(true);
            if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
        }
        var slot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHCamperTurretSlot2");slot.gameObject.SetActive(true);
        var platform=slot.GetComponentsInChildren<MeshFilter>(true).Single(t=>t.name=="Circular fixed turret platform");
        var fixedBase=slot.GetComponentsInChildren<MeshRenderer>(true).Single(t=>t.name=="FixedSupport_LOD0");
        var old=platform.GetComponent<Renderer>().bounds;
        // Fit only the small bottom pedestal, excluding the wider silver bearing above it.
        // Use radial mesh measurements so the 45-degree installation cannot inflate the size.
        var pedestal=fixedBase.GetComponent<MeshFilter>().sharedMesh.vertices.Where(v=>v.y>=.05f&&v.y<=.12f).ToArray();
        if(pedestal.Length==0)throw new Exception("Bottom pedestal measurement missing");
        float radius=pedestal.Max(v=>new Vector2(v.x,v.z).magnitude);
        float diameter=2*radius*Mathf.Max(fixedBase.transform.lossyScale.x,fixedBase.transform.lossyScale.z);
        float ratio=diameter/old.size.x;
        platform.transform.localScale=new Vector3(platform.transform.localScale.x*ratio,platform.transform.localScale.y,platform.transform.localScale.z*ratio);
        // The source's lower locating teeth occupy roughly the first 0.04 model units.
        // Sink the complete assembly together, keeping all bearing and pitch clearances intact.
        var turret=slot.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Optimized robot turret 3");
        float recess=.045f*turret.lossyScale.y+.001f;
        if(recess>=old.size.y-.003f)throw new Exception("Locating teeth need a deeper support socket");
        turret.position-=Vector3.up*recess;
        float wallX=slot.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Camper screw attachment plate").Max(r=>r.bounds.max.x);
        float oldTip=slot.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Welded horizontal shelf").Min(r=>r.bounds.min.x);
        float newTip=old.center.x-diameter*.30f;
        float reach=(newTip-wallX)/(oldTip-wallX);
        string[] names={"Camper screw attachment plate","Welded horizontal shelf","Open welded diagonal bracket","Steel washer","Hex bolt or retaining nut"};
        foreach(var f in slot.GetComponentsInChildren<MeshFilter>(true).Where(f=>names.Contains(f.name)))
        {
            var mesh=Object.Instantiate(f.sharedMesh);var vertices=mesh.vertices;
            // Preserve plate and fastener shapes; move each complete bracket inward.
            float zCenter=f.GetComponent<Renderer>().bounds.center.z;
            float dz=(zCenter-old.center.z)*(ratio-1);
            for(int i=0;i<vertices.Length;i++)
            {
                var p=f.transform.TransformPoint(vertices[i]);
                p.z=zCenter+dz+(p.z-zCenter)*.65f;
                p.y=old.min.y+(p.y-old.min.y)*.70f;
                if(f.name=="Welded horizontal shelf"||f.name=="Open welded diagonal bracket")p.x=wallX+(p.x-wallX)*reach;
                else p.x=wallX+(p.x-wallX)*.75f;
                vertices[i]=f.transform.InverseTransformPoint(p);
            }
            mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();f.sharedMesh=mesh;
        }
        var clay=new Material(Shader.Find("Standard")){color=new Color(.63f,.65f,.67f)};clay.SetFloat("_Glossiness",.18f);
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>clay).ToArray();
        foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
        var key=new GameObject("Clay key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.2f;key.shadows=LightShadows.Soft;key.transform.rotation=Quaternion.Euler(35,-35,0);
        var fill=new GameObject("Clay fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.transform.rotation=Quaternion.Euler(35,150,0);
        QualitySettings.shadowDistance=20;QualitySettings.shadows=ShadowQuality.All;
        Render(new Vector3(-3.6f,3.7f,-1.8f),new Vector3(old.center.x,2.92f,old.center.z),.40f,"clay-smaller-mount-quarter");
        Render(new Vector3(old.center.x,7,old.center.z),old.center,.40f,"clay-smaller-mount-overhead");
        File.WriteAllText(Output+"/clay-smaller-mount.txt","REVIEW ONLY: one rear-left corner. Old diameter="+old.size.x+"m; proposed diameter="+diameter+"m (measured small bottom pedestal); bracket spacing ratio="+ratio+"; bracket width 65%, height 70%, attachment projection 75%; shelf reach ratio="+reach+"; locating teeth recessed="+recess+"m into support. Actual source turret retained; all clay materials. No prefab or game bundle changed. Await owner feedback before applying to all corners. Socket fit is a visual prototype; final production socket geometry and mounted motion clearance remain to be validated.");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),RebirthRobotTurretImport.Folder+"/SmallMountClayReview.unity");
        Object.DestroyImmediate(root);
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab"));
        if(!root)throw new Exception("Staged camper missing");
        try
        {
            RebirthCamperCampIntegration.SetMode(root,true);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name.StartsWith("REBIRTHCamperTurretSlot")||new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"}.Contains(t.name))t.gameObject.SetActive(true);
                if(new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
            }
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            var light=new GameObject("Review key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.15f;light.transform.rotation=Quaternion.Euler(35,-30,0);
            Render(new Vector3(-3.4f,3.8f,-1.7f),new Vector3(-1.03f,3.05f,-3.0f),.66f,"unity-mounted-close");
            Render(new Vector3(-6,5.4f,-8),new Vector3(0,1.9f,-1.2f),3.4f,"unity-mounted-rear");
            Render(new Vector3(0,11,-1),new Vector3(0,0,-1),3.8f,"unity-mounted-overhead");
            foreach(var pitch in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="TurretPitch"))pitch.localRotation=Quaternion.Euler(29.5f,0,0);
            Render(new Vector3(-3.4f,3.8f,-1.7f),new Vector3(-1.03f,3.05f,-3.0f),.66f,"unity-mounted-down");
        }
        finally{Object.DestroyImmediate(root);}
    }
    static void Render(Vector3 eye,Vector3 target,float size,string name)
    {
        var c=new GameObject(name).AddComponent<Camera>();c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.01f;c.transform.position=eye;c.transform.LookAt(target);c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.07f,.07f,.09f);
        var rt=new RenderTexture(1400,1400,24){antiAliasing=4};c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(1400,1400,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1400,1400),0,0);img.Apply();File.WriteAllBytes(Output+"/"+name+".png",img.EncodeToPNG());RenderTexture.active=null;c.targetTexture=null;Object.DestroyImmediate(img);Object.DestroyImmediate(rt);Object.DestroyImmediate(c.gameObject);
    }
}
