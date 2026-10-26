using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Deterministic model authoring and clay rendering only; no game/session control.
public static class RebirthBareTruckBodywork
{
    const string Source="Assets/REBIRTHCamperOwnerCorrections_20261020_v3/rebirthCamper80s.prefab";
    const string Native="Assets/REBIRTHCamperTurretProduction_20261020/WithLods/rebirthCamper80s.prefab";
    const string Folder="Assets/REBIRTHBareTruckBodywork_20261020_v3";
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/BareTruckBodywork_20261020_v3";
    static Material steel,paint,glass,dark;
    static int serial;
    static Material Mat(string name,Color color,float smooth=.2f)
    {var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Glossiness",smooth);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
    static GameObject Box(Transform parent,string name,Vector3 at,Vector3 size,Material material)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material material)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=(a+b)*.5f;go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);go.transform.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());}
    static void Ellipsoid(Transform parent,string name,Vector3 position,Vector3 size)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=steel;Object.DestroyImmediate(go.GetComponent<Collider>());}
    static Transform Group(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    static void MeshObject(Transform parent,string name,List<Vector3> vertices,List<int> faces,Material material)
    {var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,Folder+"/mesh_"+(serial++)+".asset");var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;}
    static void WheelTub(Transform parent,int side)
    {
        var v=new List<Vector3>();var t=new List<int>();const int steps=24;float inner=side*.43f,outer=side*.81f;
        for(int i=0;i<=steps;i++){float a=i*Mathf.PI/steps;float y=.58f+Mathf.Sin(a)*.47f,z=-1.62f+Mathf.Cos(a)*.51f;v.Add(new Vector3(inner,y,z));v.Add(new Vector3(outer,y,z));}
        for(int i=0;i<steps;i++){int a=i*2;t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
        for(int edge=0;edge<2;edge++){int c=v.Count;v.Add(new Vector3(edge==0?inner:outer,.58f,-1.62f));for(int i=0;i<steps;i++){int a=i*2+edge,b=(i+1)*2+edge;t.AddRange(edge==0?new[]{c,b,a}:new[]{c,a,b});}}
        // Duplicate the reverse faces: interior and underside both have closed surfaces.
        int count=t.Count,offset=v.Count;v.AddRange(v.ToArray());for(int i=0;i<count;i+=3)t.AddRange(new[]{t[i+2]+offset,t[i+1]+offset,t[i]+offset});
        MeshObject(parent,"Pressed rear wheel tub "+side,v,t,paint);
    }
    static void Spring(Transform parent,int side)
    {
        for(int leaf=0;leaf<4;leaf++)
        {
            float length=1.6f-leaf*.22f;
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=12;i++){float z=-length*.5f+length*i/12;float y=.25f+.10f*(z*z/(length*length*.25f))-leaf*.012f;v.Add(new Vector3(side*.49f-.035f,y+.004f,-1.62f+z));v.Add(new Vector3(side*.49f+.035f,y+.004f,-1.62f+z));v.Add(new Vector3(side*.49f-.035f,y-.004f,-1.62f+z));v.Add(new Vector3(side*.49f+.035f,y-.004f,-1.62f+z));}
            for(int i=0;i<12;i++){int a=i*4,b=a+4;t.AddRange(new[]{a,b,a+1,a+1,b,b+1,a+2,a+3,b+2,a+3,b+3,b+2,a,a+2,b,a+2,b+2,b,a+1,b+1,a+3,a+3,b+1,b+3});} t.AddRange(new[]{0,1,2,1,3,2,48,50,49,49,50,51});
            MeshObject(parent,"Rear leaf spring "+side+" leaf "+leaf,v,t,steel);
        }
        foreach(float z in new[]{-2.4f,-.84f}){Box(parent,"Leaf spring hanger",new Vector3(side*.49f,.39f,z),new Vector3(.095f,.18f,.08f),steel);Beam(parent,"Spring eye pin",new Vector3(side*.49f-.065f,.34f,z),new Vector3(side*.49f+.065f,.34f,z),.025f,dark);}
        Beam(parent,"Rear shock body",new Vector3(side*.56f,.56f,-1.2f),new Vector3(side*.60f,.35f,-1.82f),.038f,steel);
        Beam(parent,"Rear shock piston",new Vector3(side*.56f,.66f,-.99f),new Vector3(side*.56f,.52f,-1.29f),.017f,steel);
    }
    static void Pickup(Transform trim)
    {
        Box(trim,"Pressed steel load bed floor",new Vector3(0,.70f,-1.59f),new Vector3(1.57f,.055f,2.25f),paint);
        for(int i=-7;i<=7;i++)Box(trim,"Longitudinal bed floor rib "+i,new Vector3(i*.098f,.737f,-1.59f),new Vector3(.033f,.018f,2.19f),paint);
        for(int side=-1;side<=1;side+=2)
        {
            Box(trim,"Inner bedside "+side,new Vector3(side*.79f,.922f,-1.59f),new Vector3(.038f,.39f,2.27f),paint);
            Box(trim,"Bed rail cap "+side,new Vector3(side*.79f,1.126f,-1.59f),new Vector3(.095f,.027f,2.31f),paint);
            for(int rib=0;rib<5;rib++)Box(trim,"Bedside pressed stiffener",new Vector3(side*.765f,.918f,-.67f-rib*.45f),new Vector3(.025f,.30f,.023f),paint);
            WheelTub(trim,side);
        }
        Box(trim,"Bed front bulkhead",new Vector3(0,.92f,-.465f),new Vector3(1.58f,.4f,.045f),paint);
        // Cab rear closure is split around the window aperture, not a plane over a hole.
        Box(trim,"Cab rear lower wall",new Vector3(0,.91f,-.425f),new Vector3(1.53f,.57f,.045f),paint);
        Box(trim,"Cab rear upper window header",new Vector3(0,1.625f,-.425f),new Vector3(1.50f,.12f,.045f),paint);
        foreach(int side in new[]{-1,1})Box(trim,"Cab rear window pillar",new Vector3(side*.70f,1.385f,-.425f),new Vector3(.13f,.38f,.045f),paint);
        Box(trim,"Cab rear window glass",new Vector3(0,1.385f,-.43f),new Vector3(1.28f,.35f,.012f),glass);
        Box(trim,"Cab rear window bottom seal",new Vector3(0,1.20f,-.449f),new Vector3(1.31f,.025f,.02f),dark);
        Box(trim,"Cab rear window upper seal",new Vector3(0,1.57f,-.449f),new Vector3(1.31f,.025f,.02f),dark);
        foreach(int side in new[]{-1,1})Box(trim,"Cab rear window side seal",new Vector3(side*.65f,1.385f,-.449f),new Vector3(.025f,.38f,.02f),dark);
        Box(trim,"Tailgate outer skin",new Vector3(0,.92f,-2.755f),new Vector3(1.60f,.43f,.06f),paint);
        Box(trim,"Tailgate inner pressed panel",new Vector3(0,.92f,-2.713f),new Vector3(1.44f,.29f,.025f),paint);
        Box(trim,"Tailgate top rolled edge",new Vector3(0,1.145f,-2.755f),new Vector3(1.60f,.035f,.09f),paint);
        Box(trim,"Tailgate recessed handle surround",new Vector3(0,1.04f,-2.794f),new Vector3(.21f,.067f,.015f),dark);
        Box(trim,"Tailgate handle",new Vector3(0,1.045f,-2.804f),new Vector3(.135f,.025f,.012f),steel);
        foreach(int side in new[]{-1,1})
        {
            Beam(trim,"Tailgate lower hinge",new Vector3(side*.59f-.07f,.70f,-2.785f),new Vector3(side*.59f+.07f,.70f,-2.785f),.022f,steel);
            Box(trim,"Rear lamp bezel",new Vector3(side*.845f,.90f,-2.77f),new Vector3(.095f,.34f,.04f),dark);
            Box(trim,"Rear stop lamp lens",new Vector3(side*.845f,.96f,-2.795f),new Vector3(.071f,.20f,.017f),paint);
            Box(trim,"Reverse lamp lens",new Vector3(side*.845f,.80f,-2.795f),new Vector3(.071f,.08f,.017f),glass);
            Box(trim,"Rear bumper bracket",new Vector3(side*.48f,.49f,-2.78f),new Vector3(.10f,.10f,.25f),steel);
        }
        Box(trim,"Factory rear bumper",new Vector3(0,.51f,-2.89f),new Vector3(1.85f,.15f,.16f),steel);
        Box(trim,"Bumper top step tread",new Vector3(0,.594f,-2.89f),new Vector3(1.45f,.018f,.145f),dark);
        Box(trim,"Rear registration plate recess",new Vector3(0,.50f,-2.979f),new Vector3(.32f,.12f,.018f),dark);
    }
    static void Chassis(Transform chassis)
    {
        foreach(int side in new[]{-1,1})
        {
            Box(chassis,"Continuous chassis rail "+side,new Vector3(side*.48f,.49f,-.40f),new Vector3(.105f,.15f,4.52f),steel);
            Box(chassis,"C channel rail lower flange",new Vector3(side*.48f,.413f,-.4f),new Vector3(.14f,.018f,4.52f),steel);
            Box(chassis,"C channel rail upper flange",new Vector3(side*.48f,.567f,-.4f),new Vector3(.14f,.018f,4.52f),steel);
            Spring(chassis,side);
        }
        foreach(float z in new[]{-2.54f,-2.08f,-1.10f,-.52f,.40f,1.48f})Box(chassis,"Chassis crossmember",new Vector3(0,.49f,z),new Vector3(.97f,.075f,.075f),steel);
        Box(chassis,"Cab floor pan",new Vector3(0,.61f,.20f),new Vector3(1.51f,.045f,1.31f),paint);
        Box(chassis,"Engine lower block",new Vector3(0,.67f,1.15f),new Vector3(.60f,.35f,.68f),steel);
        Box(chassis,"Engine sump pan",new Vector3(0,.435f,1.15f),new Vector3(.48f,.12f,.57f),dark);
        for(int i=-2;i<=2;i++)Box(chassis,"Sump pressed rib",new Vector3(i*.077f,.367f,1.15f),new Vector3(.018f,.015f,.45f),steel);
        Ellipsoid(chassis,"Transmission bellhousing",new Vector3(0,.47f,.66f),new Vector3(.39f,.36f,.36f));
        Beam(chassis,"Transmission housing",new Vector3(0,.46f,.63f),new Vector3(0,.40f,.12f),.14f,steel);
        Beam(chassis,"Rear propeller shaft",new Vector3(0,.39f,.1f),new Vector3(0,.38f,-1.62f),.039f,steel);
        foreach(float z in new[]{.06f,-1.48f})Beam(chassis,"Driveshaft universal joint",new Vector3(-.07f,.39f,z),new Vector3(.07f,.39f,z),.033f,steel);
        Beam(chassis,"Rear live axle housing",new Vector3(-.73f,.38f,-1.62f),new Vector3(.73f,.38f,-1.62f),.068f,steel);
        Ellipsoid(chassis,"Rear differential casing",new Vector3(0,.38f,-1.62f),new Vector3(.32f,.31f,.32f));
        Box(chassis,"Differential cover",new Vector3(0,.38f,-1.79f),new Vector3(.22f,.22f,.023f),steel);
        foreach(int side in new[]{-1,1})
        {
            Beam(chassis,"Front lower control arm",new Vector3(side*.24f,.39f,1.23f),new Vector3(side*.69f,.35f,1.29f),.045f,steel);
            Beam(chassis,"Front suspension strut",new Vector3(side*.62f,.36f,1.29f),new Vector3(side*.52f,.72f,1.29f),.045f,steel);
            Beam(chassis,"Steering tie rod",new Vector3(0,.35f,1.47f),new Vector3(side*.65f,.35f,1.47f),.022f,steel);
        }
        Box(chassis,"Fuel tank",new Vector3(-.28f,.38f,-.51f),new Vector3(.39f,.20f,.84f),dark);
        foreach(float z in new[]{-.79f,-.23f})Box(chassis,"Fuel tank retaining strap",new Vector3(-.28f,.272f,z),new Vector3(.41f,.018f,.035f),steel);
        var route=new[]{new Vector3(.24f,.43f,1.15f),new Vector3(.58f,.34f,.7f),new Vector3(.62f,.32f,-.42f),new Vector3(.62f,.32f,-1.08f),new Vector3(.62f,.57f,-1.62f),new Vector3(.66f,.34f,-2.20f),new Vector3(.76f,.32f,-2.89f)};
        for(int i=1;i<route.Length;i++)Beam(chassis,"Exhaust tube "+i,route[i-1],route[i],.031f,steel);
        Beam(chassis,"Exhaust muffler",new Vector3(.62f,.32f,-.47f),new Vector3(.62f,.32f,-1.0f),.09f,steel);
        foreach(float z in new[]{-.76f,-1.52f,-2.29f})Box(chassis,"Bed underside pressed support",new Vector3(0,.635f,z),new Vector3(1.48f,.055f,.065f),steel);
    }
    static void RestoreShared(GameObject root,Transform camper)
    {
        var native=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Native));
        var original=native.transform.Find("GameObject/M/VisualBody/80sCamperTruck");
        var truck=root.transform.Find("GameObject/M/VisualBody/80sCamperTruck");
        // Remove the earlier shared splits; rebuild them using real cab extents.
        foreach(var t in camper.Cast<Transform>().Where(t=>t.name.StartsWith("Camper body ")||t.name=="Camper shared-part LODs").ToArray())Object.DestroyImmediate(t.gameObject);
        foreach(string partName in new[]{"body","windows","lights","parts"})
        {
            var old=truck.Find(partName);if(old)Object.DestroyImmediate(old.gameObject);
            var part=Object.Instantiate(original.Find(partName).gameObject,truck,false);part.name=partName;
            foreach(var f in part.GetComponentsInChildren<MeshFilter>(true))Split(f,camper,partName);
        }
        Object.DestroyImmediate(native);
    }
    static void Split(MeshFilter filter,Transform camper,string category)
    {
        var mesh=filter.sharedMesh;if(!mesh)return;var vertices=mesh.vertices;var common=Object.Instantiate(mesh);var module=Object.Instantiate(mesh);
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            var keep=new List<int>();var hide=new List<int>();var triangles=mesh.GetTriangles(sub);
            for(int i=0;i<triangles.Length;i+=3)
            {
                var c=filter.transform.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);
                bool belongsToCamper=category=="windows"?(c.y>1.78f||c.z<-.53f):category=="body"?(c.y>1.78f||c.z< -2.81f):(c.y>1.78f||c.z< -2.69f);
                (belongsToCamper?hide:keep).AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
            }
            common.SetTriangles(keep,sub);module.SetTriangles(hide,sub);
        }
        AssetDatabase.CreateAsset(common,Folder+"/shared_common_"+(serial++)+".asset");AssetDatabase.CreateAsset(module,Folder+"/shared_camper_"+(serial++)+".asset");filter.sharedMesh=common;
        var copy=new GameObject("Camper shared "+category+" "+filter.name);copy.transform.SetParent(camper,false);copy.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);copy.transform.localScale=filter.transform.lossyScale;copy.AddComponent<MeshFilter>().sharedMesh=module;copy.AddComponent<MeshRenderer>().sharedMaterials=filter.GetComponent<Renderer>().sharedMaterials;
        // Only author the native LOD0 shared copies for the review asset.
        if(filter.name.StartsWith("REBIRTHDistance"))copy.SetActive(false);
    }
    public static void BuildAndRender()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        steel=Mat("Truck structural steel",new Color(.34f,.35f,.36f));paint=Mat("Truck bodywork",new Color(.61f,.62f,.63f));glass=Mat("Truck glass",new Color(.25f,.28f,.31f),.65f);dark=Mat("Truck rubber and recesses",new Color(.20f,.21f,.22f));
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));root.name="rebirthCamper80s";
        var visual=root.transform.Find("GameObject/M/VisualBody");var camper=visual.Find("REBIRTHCamperBodyModule");
        RestoreShared(root,camper);
        Pickup(Group(root.transform,"REBIRTHPickupOnlyBodywork"));Chassis(Group(root.transform,"REBIRTHTruckCommonChassis"));
        File.WriteAllText(Output+"/coordinate-audit.txt","visual position="+visual.position+" scale="+visual.lossyScale+" rotation="+visual.rotation+" root="+root.transform.position);
        camper.gameObject.SetActive(false);
        string prefab=Folder+"/rebirthCamper80s.prefab";PrefabUtility.SaveAsPrefabAsset(root,prefab);AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"/model-source.txt",prefab+"\nPickupOnlyBodywork: active only without camper modifier.\nTruckCommonChassis: active in both configurations.\nCamperBodyModule: active only with camper modifier.\nReview prefab only: not installed into game.");
        Render(root);Object.DestroyImmediate(root);
    }
    static void Render(GameObject root)
    {
        UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(root.scene);
        foreach(var group in root.GetComponentsInChildren<LODGroup>(true))group.ForceLOD(0);
        var clay=new Material(Shader.Find("Standard"));clay.color=new Color(.57f,.59f,.61f);clay.SetFloat("_Glossiness",.18f);
        var darkClay=new Material(clay){color=new Color(.23f,.25f,.27f)};var steelClay=new Material(clay){color=new Color(.40f,.42f,.44f)};
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)){bool window=renderer.name.ToLowerInvariant().Contains("window")&&renderer.name.ToLowerInvariant().Contains("glass")||renderer.name=="windows";renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>window?darkClay:m!=null&&(m.name.Contains("structural")||m.name.Contains("rubber"))?steelClay:clay).ToArray();}
        RenderSettings.ambientLight=new Color(.47f,.47f,.47f);RenderSettings.fog=false;
        var camera=new GameObject("Bodywork review camera").AddComponent<Camera>();camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.145f,.16f);
        var key=new GameObject("Review key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.05f;
        var fill=new GameObject("Review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.65f;
        var views=new[]{("rear-three-quarter",new Vector3(-5,3,-6)),("front-three-quarter",new Vector3(-5,2.8f,6)),("left",new Vector3(-7,0,0)),("right",new Vector3(7,0,0)),("front",new Vector3(0,0,7)),("rear",new Vector3(0,0,-7)),("top",new Vector3(0,8,0)),("bottom",new Vector3(0,-8,0)),("underside-three-quarter",new Vector3(4,-5,-5))};
        Vector3 centre=new Vector3(0,.85f,-.35f);
        foreach(var view in views)
        {
            camera.transform.position=centre+view.Item2;camera.transform.LookAt(centre,Mathf.Abs(view.Item2.y)>7?Vector3.forward:Vector3.up);camera.orthographicSize=view.Item1=="front"||view.Item1=="rear"?1.22f:3.20f;
            key.transform.rotation=camera.transform.rotation*Quaternion.Euler(25,-35,0);fill.transform.rotation=camera.transform.rotation*Quaternion.Euler(-25,120,0);
            var rt=new RenderTexture(1600,1200,24);camera.targetTexture=rt;camera.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();File.WriteAllBytes(Output+"/"+view.Item1+".png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=prior;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
        }
        File.WriteAllText(Output+"/render-complete.txt",DateTime.UtcNow.ToString("o"));
    }
}
