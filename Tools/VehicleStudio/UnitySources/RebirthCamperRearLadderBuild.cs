using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

public static class RebirthCamperRearLadderBuild
{
    public const string Folder = "Assets/REBIRTHCamperRearLadders_20261018_v3";
    public const string Output = "E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperRearLadders_20261018_v3";
    static Material steel;
    static int index;
    // Parallel to the rear panel's shallow forward lean, with a short stand-off.
    static float LadderZ(float y) => Mathf.Lerp(-3.46f,-3.34f,Mathf.InverseLerp(1.60f,2.86f,y));
    static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
    static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 p, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 21;
        go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = steel;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static void Tube(Transform parent, string name, Vector3 a, Vector3 b, float radius)
    {
        var go = Part(parent, PrimitiveType.Cylinder, name, (a+b)/2, new Vector3(radius*2, Vector3.Distance(a,b)/2, radius*2));
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b-a);
        var c = go.AddComponent<CapsuleCollider>(); c.radius = .5f; c.height = 2; c.direction = 1;
    }
    static void Bolt(Transform parent, Vector3 p)
    {
        var b = Part(parent, PrimitiveType.Cylinder, "Mount bolt and washer", p, new Vector3(.023f,.008f,.023f));
        b.transform.localRotation = Quaternion.Euler(90,0,0);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperPhysicalCollision.Folder+"/rebirthCamper80s.prefab");
        if (!source) throw new Exception("Missing current physical-collision prefab");
        var root = Object.Instantiate(source); root.name = "rebirthCamper80s";
        steel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Material>("Assets/REBIRTHCamperArmorPlow_20261017_v4/SalvagedSteel_Albedo.mat"));
        steel.name = "Rear ladder brushed weathered steel"; steel.SetFloat("_Metallic",.75f); steel.SetFloat("_Glossiness",.32f);
        AssetDatabase.CreateAsset(steel,Folder+"/LadderSteel.mat");
        var owner = new GameObject("REBIRTHRearLadders").transform; owner.SetParent(root.transform.Find("GameObject/M/VisualBody"),false);
        var report = new List<string>();
        var body=Find(root,"camper_body"); var surface=body.gameObject.AddComponent<MeshCollider>(); surface.sharedMesh=body.GetComponent<MeshFilter>().sharedMesh;
        Physics.SyncTransforms();
        // Keep the open leaf between the two ladders instead of sweeping across the right one.
        Find(root,"REBIRTHRearDoorOpenPose").localRotation = Quaternion.Euler(0,-90,0);
        foreach (int side in new[]{-1,1})
        {
            var ladder = new GameObject(side<0?"Left rear metal ladder":"Right rear metal ladder").transform;
            ladder.SetParent(owner,false); float x = side*.78f;
            foreach (float edge in new[]{-.16f,.16f})
            {
                float topX=x+edge;
                float shoeX=Mathf.Clamp(topX,-.84f,.84f);
                var flange=Part(ladder,PrimitiveType.Cylinder,"Roof-conforming mounting flange",new Vector3(shoeX,0,-3.12f),new Vector3(.078f,1,.078f));
                var flangeMesh=Object.Instantiate(flange.GetComponent<MeshFilter>().sharedMesh);
                var fv=flangeMesh.vertices;var heights=new float[fv.Length];
                bool fitted=false;float flangeZ=-3.12f;
                for(int step=0;step<=10&&!fitted;step++)
                for(int inset=0;inset<=4&&!fitted;inset++)
                {
                    flange.transform.localPosition=new Vector3(shoeX-Mathf.Sign(shoeX)*inset*.02f,0,-3.12f+step*.02f);
                    bool ok=true;
                    for(int k=0;k<fv.Length;k++)
                    {
                        var wp=flange.transform.TransformPoint(fv[k]);
                        if(!surface.Raycast(new Ray(new Vector3(wp.x,4,wp.z),Vector3.down),out var roofHit,2)||roofHit.point.y<2.6f){ok=false;break;}
                        heights[k]=roofHit.point.y;
                    }
                    if(ok){fitted=true;shoeX=flange.transform.localPosition.x;flangeZ=flange.transform.localPosition.z;}
                }
                if(!fitted)throw new Exception("No fully supported roof flange footprint found");
                float flangeTop=heights.Max()+.008f;
                for(int k=0;k<fv.Length;k++)fv[k].y=fv[k].y>0?flangeTop:heights[k]-.001f;
                flangeMesh.vertices=fv;flangeMesh.RecalculateNormals();flangeMesh.RecalculateBounds();
                AssetDatabase.CreateAsset(flangeMesh,Folder+"/FittedRoofFlange"+(index++)+".asset");flange.GetComponent<MeshFilter>().sharedMesh=flangeMesh;
                report.Add("Roof flange "+shoeX+" measured roof range="+heights.Min()+".."+heights.Max()+" top="+flangeTop+"; underside fitted to every sampled roof vertex with1mm seating.");
                var path=new List<Vector3>{new Vector3(topX,1.60f,LadderZ(1.60f)),new Vector3(topX,2.84f,-3.342f),new Vector3(topX,2.86f,-3.34f)};
                for(int n=1;n<=24;n++)
                {
                    float angle=n*Mathf.PI/48;float t=(float)n/24;
                    path.Add(new Vector3(Mathf.Lerp(topX,shoeX,Mathf.SmoothStep(0,1,t)),2.86f+.12f*Mathf.Sin(angle),-3.22f-.12f*Mathf.Cos(angle)));
                }
                path.Add(new Vector3(shoeX,2.98f,flangeZ-.06f));
                for(int n=1;n<=16;n++)
                {
                    float angle=n*Mathf.PI/32;
                    path.Add(new Vector3(shoeX,2.92f+.06f*Mathf.Cos(angle),flangeZ-.06f+.06f*Mathf.Sin(angle)));
                }
                path.Add(new Vector3(shoeX,flangeTop-.001f,flangeZ));
                RebirthCamperTubeFinish.Tube(ladder,"Continuous bent ladder handhold",path,.023f,steel,Folder);
                foreach(float dx in new[]{-.027f,.027f})
                    Part(ladder,PrimitiveType.Cylinder,"Roof flange screw",new Vector3(shoeX+dx,flangeTop+.003f,flangeZ),new Vector3(.010f,.003f,.010f));
            }
            for(int n=0;n<5;n++)
            {
                float y=1.64f+n*.25f;
                Tube(ladder,"Serrated metal rung",new Vector3(x-.16f,y,LadderZ(y)),new Vector3(x+.16f,y,LadderZ(y)),.021f);
                // Raised grip rings give a visible tread without decorative floating straps.
                for(int g=-3;g<=3;g++)
                {
                    var grip=Part(ladder,PrimitiveType.Cylinder,"Rung grip ring",new Vector3(x+g*.037f,y,LadderZ(y)),new Vector3(.046f,.004f,.046f));
                    grip.transform.localRotation=Quaternion.Euler(0,0,90);
                }
            }
            foreach(float y in new[]{1.74f,2.22f,2.72f}) foreach(float edge in new[]{-.16f,.16f})
            {
                float mountX=Mathf.Clamp(x+edge,-.84f,.84f);
                if(!surface.Raycast(new Ray(new Vector3(mountX,y,-4),Vector3.forward),out var hit,1)) throw new Exception("Mount misses camper body");
                foreach(float dx in new[]{-.045f,.045f}) foreach(float dy in new[]{-.07f,.07f})
                    if(!surface.Raycast(new Ray(new Vector3(mountX+dx,y+dy,-4),Vector3.forward),out var corner,1)) throw new Exception("Mount corner outside body panel");
                float z=hit.point.z-.009f;
                var plate=Part(ladder,PrimitiveType.Cube,"Body fixing plate",new Vector3(mountX,y,z),new Vector3(.09f,.14f,.018f));
                Tube(ladder,"Welded ladder stand-off",new Vector3(mountX,y,z-.009f),new Vector3(x+edge,y,LadderZ(y)),.017f);
                Bolt(ladder,new Vector3(mountX,y-.045f,z-.017f)); Bolt(ladder,new Vector3(mountX,y+.045f,z-.017f));
                report.Add("Body-supported mount "+new Vector3(mountX,y,z)+"; all four corners hit body mesh");
            }
            var volume=new GameObject("REBIRTHRearLadderClimbVolume"); volume.transform.SetParent(ladder,false);
            var box=volume.AddComponent<BoxCollider>(); box.center=new Vector3(x,1.835f,-3.64f); box.size=new Vector3(.49f,2.57f,.74f); box.isTrigger=true; box.enabled=false;
            report.Add(ladder.name+": starts1.60m at user green line; first rung1.64m; climb feet0.55..3.12m; 0.25m spacing; mounts fully inside x+-0.885m; constant stile spacing0.32m; low roof return top2.963m; short body-following stand-offs");
        }
        var rails=Find(root,"Fitted rooftop guardrails");
        Object.DestroyImmediate(surface);
        // Rear ladder exits must not retain the original blocking guardrail.
        foreach(var f in rails.GetComponentsInChildren<MeshFilter>(true))
        {
            var bounds=f.GetComponent<Renderer>().bounds;
            if(bounds.center.z < -2.7f)
            {
                foreach(var c in Find(root,"REBIRTHPermanentPhysicalColliders").GetComponentsInChildren<BoxCollider>())
                    if(Vector3.Distance(c.bounds.center,bounds.center)<.015f) Object.DestroyImmediate(c.gameObject);
                Object.DestroyImmediate(f.gameObject);
            }
        }
        var rearPath=new List<Vector3>{new Vector3(-.38f,2.88f,-3.10f),new Vector3(-.38f,3.07f,-3.10f)};
        for(int n=1;n<=16;n++){float a=n*Mathf.PI/32;rearPath.Add(new Vector3(-.34f-.04f*Mathf.Cos(a),3.07f+.04f*Mathf.Sin(a),-3.10f));}
        rearPath.Add(new Vector3(.34f,3.11f,-3.10f));
        for(int n=1;n<=16;n++){float a=n*Mathf.PI/32;rearPath.Add(new Vector3(.34f+.04f*Mathf.Sin(a),3.07f+.04f*Mathf.Cos(a),-3.10f));}
        rearPath.Add(new Vector3(.38f,2.88f,-3.10f));
        RebirthCamperTubeFinish.Tube(owner,"Continuous bent rear safety rail",rearPath,.014f,steel,Folder);
        // Combine ladder visuals into one draw call. Preserve physical members and climb markers.
        var meshes=owner.GetComponentsInChildren<MeshFilter>(true);
        var combined=new Mesh{name="Rear ladders joined steel geometry",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        combined.CombineMeshes(meshes.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=owner.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray());
        AssetDatabase.CreateAsset(combined,Folder+"/RearLadders.asset");
        owner.gameObject.AddComponent<MeshFilter>().sharedMesh=combined; owner.gameObject.AddComponent<MeshRenderer>().sharedMaterial=steel;
        foreach(var f in meshes){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab"); AssetDatabase.SaveAssets();
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(string name in new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}) Find(root,name).gameObject.SetActive(false);
        foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor"}) Find(root,name).gameObject.SetActive(true);
        Find(root,"REBIRTHRearDoorPivot").localRotation=Quaternion.Euler(0,-90,0);
        Render(root,new Vector3(0,2.4f,-7.3f),new Vector3(0,1.9f,-3.3f),2.05f,"rear-open");
        Render(root,new Vector3(-3.6f,4.1f,-6.6f),new Vector3(0,2.0f,-3.25f),2.15f,"rear-quarter");
        File.WriteAllLines(Output+"/author-report.txt",report); Object.DestroyImmediate(root);
    }
    static void Render(GameObject root,Vector3 eye,Vector3 target,float size,string label)
    {
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        var light=new GameObject().AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.2f; light.transform.rotation=Quaternion.Euler(25,0,0);
        var camera=new GameObject().AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=size; camera.nearClipPlane=.01f; camera.transform.position=eye; camera.transform.LookAt(target);
        camera.backgroundColor=new Color(.15f,.17f,.18f); camera.clearFlags=CameraClearFlags.SolidColor;
        var rt=new RenderTexture(1400,1200,24); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var image=new Texture2D(1400,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1200),0,0);image.Apply();File.WriteAllBytes(Output+"/"+label+".png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);
    }
}
