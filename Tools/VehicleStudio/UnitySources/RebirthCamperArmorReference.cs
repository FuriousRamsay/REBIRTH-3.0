using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Source geometry, not a rendered mockup. Native VehiclePart owns mod visibility.
public static partial class RebirthCamperArmorReference
{
    public const string Folder="Assets/REBIRTHCamperArmorReference_20261017_v5";
    public const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperArmorReference_20261017";
    const string Source="Assets/REBIRTHSeatPelvis_20261017/rebirthCamper80s.prefab";
    static Material iron,paint,fastener;
    static Mesh plate,bolt,washer,spike;
    static int panels,grilles;
    static GameObject Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
    static Mesh Shape(int sides,float bottom,float top,float length)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
        for(int ring=0;ring<2;ring++)for(int i=0;i<=sides;i++)
        {float a=i*2*Mathf.PI/sides,r=ring==0?bottom:top;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,ring*length));uv.Add(new Vector2((float)i/sides,ring));}
        for(int i=0;i<sides;i++){int a=i,b=i+1,c=i+sides+1,d=c+1;tr.AddRange(new[]{a,b,c,b,d,c});}
        v.Add(new Vector3(0,0,0));uv.Add(Vector2.one*.5f);int back=v.Count-1;
        v.Add(new Vector3(0,0,length));uv.Add(Vector2.one*.5f);int front=v.Count-1;
        for(int i=0;i<sides;i++)tr.AddRange(new[]{back,i+1,i,front,i+sides+1,i+sides+2});
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateTangents();return m;
    }
    static Mesh Plate()
    {
        var ring=new[]{new Vector2(-.43f,-.5f),new Vector2(.43f,-.5f),new Vector2(.5f,-.43f),new Vector2(.5f,.43f),new Vector2(.43f,.5f),new Vector2(-.43f,.5f),new Vector2(-.5f,.43f),new Vector2(-.5f,-.43f)};
        var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        for(int r=0;r<4;r++)foreach(var p in ring){float s=r==0||r==3?.96f:1;v.Add(new Vector3(p.x*s,p.y*s,r==0?-.5f:r==1?-.3f:r==2?.3f:.5f));uv.Add(p+Vector2.one*.5f);}
        for(int r=0;r<3;r++)for(int i=0;i<8;i++){int a=r*8+i,b=r*8+(i+1)%8;triangles.AddRange(new[]{a,b,a+8,b,b+8,a+8});}
        for(int i=1;i<7;i++){triangles.AddRange(new[]{0,i+1,i,24,24+i,25+i});}
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
    }
    static GameObject Part(Transform parent,string name,Mesh mesh,Material material,Vector3 pos,Quaternion rot,Vector3 scale)
    {
        var go=Group(parent,name);go.transform.SetPositionAndRotation(pos,rot);go.transform.localScale=scale;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
    }
    static void Tube(Transform parent,Vector3 a,Vector3 b,float radius,Material material=null)
    {Part(parent,"Welded tubular guard",washer,material??iron,a,Quaternion.LookRotation(b-a),new Vector3(radius/.025f,radius/.025f,Vector3.Distance(a,b)/.008f));}
    static void Bolt(Transform parent,Vector3 p,Quaternion rotation)
    {
        Part(parent,"Washer",washer,fastener,p,rotation,Vector3.one);
        Part(parent,"Hex fastener",bolt,fastener,p+rotation*Vector3.forward*.007f,rotation,Vector3.one);
    }
    static void Panel(Transform parent,Vector3 center,Vector3 normal,float width,float height,bool yellow=false)
    {
        var rotation=Quaternion.LookRotation(normal,Mathf.Abs(normal.y)>.8f?Vector3.forward:Vector3.up);
        var go=Part(parent,"Bolted chamfered armor plate",plate,yellow?paint:iron,center+normal*.022f,rotation,new Vector3(width,height,.028f));
        foreach(float x in new[]{-.5f,.5f})foreach(float y in new[]{-.5f,.5f})
            Bolt(parent,go.transform.position+rotation*new Vector3(x*(width-.075f),y*(height-.075f),.016f),rotation);
        panels++;
    }

    static float Noise(int x,int y){unchecked{uint n=(uint)(x*374761393+y*668265263);n=(n^(n>>13))*1274126177;return (n&65535)/65535f;}}
    static void SaveTexture(string name,Color32[] pixels,int size,bool normal,bool linear=false)
    {
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
        string path=Folder+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=!normal&&!linear;importer.maxTextureSize=size;importer.mipmapEnabled=true;importer.crunchedCompression=true;importer.compressionQuality=90;
        importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
    }



    // Connected glass islands are measured from the actual mesh; duplicated seam
    // vertices are welded for connectivity only. Source meshes remain untouched.
    static void WindowGrilles(GameObject root,Transform armor,Transform doorArmor)
    {
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="windows"||f.name=="RearDoor_windows"||f.name=="headlights_glass").ToArray())
        {
            var vertices=filter.sharedMesh.vertices;var triangles=filter.sharedMesh.triangles;
            var parent=Enumerable.Range(0,vertices.Length).ToArray();Func<int,int> find=null;find=i=>parent[i]==i?i:parent[i]=find(parent[i]);
            var weld=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++){var p=Vector3Int.RoundToInt(vertices[i]*10000);if(weld.TryGetValue(p,out int same))parent[find(i)]=find(same);else weld.Add(p,i);}
            for(int i=0;i<triangles.Length;i+=3){int a=find(triangles[i]);parent[find(triangles[i+1])]=a;parent[find(triangles[i+2])]=a;}
            foreach(var island in Enumerable.Range(0,vertices.Length).GroupBy(find))
            {
                var points=island.Select(i=>filter.transform.TransformPoint(vertices[i])).ToArray();var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
                if(bounds.size.magnitude<.32f||bounds.size.y<.12f)continue;
                Vector3 normal;
                if(bounds.size.x<.18f)normal=new Vector3(Mathf.Sign(bounds.center.x),0,0);
                else if(bounds.size.z<.6f)
                {
                    normal=new Vector3(0,0,Mathf.Sign(bounds.center.z+.6f));
                    // Preserve the cab windshield rake rather than placing a
                    // vertical grille through its sloping glass.
                    if(bounds.center.z>0&&bounds.size.y>.25f)
                        normal=new Vector3(0,bounds.size.z/bounds.size.y,1).normalized;
                }
                else continue;
                Vector3 right=Vector3.Cross(Vector3.up,normal),up=Vector3.up;
                if(normal.y!=0){right=Vector3.right;up=Vector3.Cross(normal,right).normalized;}
                Vector3 center=bounds.center+normal*.085f;float width=normal.x==0?bounds.size.x:bounds.size.z,height=normal.y==0?bounds.size.y:bounds.size.y/Vector3.Dot(up,Vector3.up);
                if(width<.18f||width>1.9f||height>1.2f)continue;
                var target=filter.name.StartsWith("RearDoor")?doorArmor:armor;
                for(int side=-1;side<=1;side+=2)
                {
                    Tube(target,center+right*width*.5f*side-up*height*.5f,center+right*width*.5f*side+up*height*.5f,.025f);
                    Tube(target,center+up*height*.5f*side-right*width*.5f,center+up*height*.5f*side+right*width*.5f,.025f);
                }
                for(float x=-width*.5f+.12f;x<width*.5f-.04f;x+=.12f)Tube(target,center+right*x-up*height*.5f,center+right*x+up*height*.5f,.009f,fastener);
                for(float y=-height*.5f+.12f;y<height*.5f-.04f;y+=.12f)Tube(target,center+up*y-right*width*.5f,center+up*y+right*width*.5f,.009f,fastener);
                foreach(float x in new[]{-width*.5f,width*.5f})foreach(float y in new[]{-height*.5f,height*.5f})
                {Panel(target,center+right*x+up*y-normal*.025f,normal,.09f,.09f);Bolt(target,center+right*x+up*y+normal*.024f,Quaternion.LookRotation(normal));}
                grilles++;
            }
        }
    }


    static void Combine(GameObject group,string name)
    {
        var filters=group.GetComponentsInChildren<MeshFilter>(true);var materials=new[]{iron,paint,fastener};var meshes=new List<Mesh>();
        foreach(var material in materials)
        {
            var rows=filters.Where(f=>f.GetComponent<Renderer>().sharedMaterial==material).Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=group.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
            var m=new Mesh();m.indexFormat=IndexFormat.UInt32;if(rows.Length>0)m.CombineMeshes(rows,true,true);meshes.Add(m);
        }
        var combined=new Mesh();combined.name=name;combined.indexFormat=IndexFormat.UInt32;combined.CombineMeshes(meshes.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);combined.RecalculateBounds();
        AssetDatabase.CreateAsset(combined,Folder+"/"+name+".asset");
        foreach(Transform child in group.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
        group.AddComponent<MeshFilter>().sharedMesh=combined;group.AddComponent<MeshRenderer>().sharedMaterials=materials;
        foreach(var m in meshes)Object.DestroyImmediate(m);
    }
    public static void Author()
    {
        if(File.Exists(Folder+"/rebirthCamper80s.prefab"))throw new Exception("Preserve existing source; use a new version");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        PrepareClay();plate=Plate();bolt=Shape(6,.016f,.016f,.014f);washer=Shape(12,.025f,.025f,.008f);spike=Shape(12,.075f,.004f,.44f);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));root.name="rebirthCamper80s";
        var model=root.transform.Find("GameObject");var m=model.Find("M");
        var armor=Group(m,"REBIRTHModArmor");var plow=Group(m,"REBIRTHModPlow");
        var pivot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHRearDoorPivot");var door=Group(pivot,"REBIRTHModArmorDoor");
        ReferenceArmor(root,armor.transform);WindowGrilles(root,armor.transform,door.transform);ReferenceDoor(door.transform);ReferencePlow(plow.transform);
        Combine(armor,"ArmorBody");Combine(door,"ArmorDoor");Combine(plow,"RaisedSpikedPlow");
        var physical=Group(root.transform.Find("Physics"),"REBIRTHModPlowHull");physical.layer=21;physical.tag="E_VehicleStrong";
        var collider=physical.AddComponent<BoxCollider>();collider.center=new Vector3(0,.75f,2.72f);collider.size=new Vector3(2.30f,1.05f,.75f);
        armor.SetActive(false);door.SetActive(false);plow.SetActive(false);physical.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"/authored.txt","panels="+panels+" glass guards="+grilles+"; native mod roots default hidden; original source preserved");
        Object.DestroyImmediate(root);Build();
    }
    public static void Build()
    {
        Directory.CreateDirectory(Output+"/Bundles");var builds=new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Folder+"/rebirthCamper80s.prefab"},addressableNames=new[]{"rebirthCamper80s"}}};
        if(!BuildPipeline.BuildAssetBundles(Output+"/Bundles",builds,BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Build failed");
        RenderAndVerify();
    }

    public static void RenderAndVerify()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/rebirthCamper80s.prefab"));
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))for(int i=0;i<renderer.sharedMaterials.Length;i++)
        {
            var material=renderer.sharedMaterials[i];if(!material||!material.HasProperty("_PatternEnabled"))continue;
            var block=new MaterialPropertyBlock();block.SetFloat("_PatternEnabled",1);renderer.SetPropertyBlock(block,i);
        }
        var model=root.transform.Find("GameObject");var armor=model.Find("M/REBIRTHModArmor").gameObject;var plow=model.Find("M/REBIRTHModPlow").gameObject;
        var pivot=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTHRearDoorPivot");var door=pivot.Find("REBIRTHModArmorDoor").gameObject;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.56f,.60f);
        var reflection=new Cubemap(16,TextureFormat.RGB24,false);
        var reflectionPixels=Enumerable.Repeat(new Color(.40f,.43f,.48f),256).ToArray();
        foreach(CubemapFace face in new[]{CubemapFace.PositiveX,CubemapFace.NegativeX,CubemapFace.PositiveY,CubemapFace.NegativeY,CubemapFace.PositiveZ,CubemapFace.NegativeZ})reflection.SetPixels(reflectionPixels,face);
        reflection.Apply();RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflection=reflection;
        var sun=new GameObject("Key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(40,-35,0);
        var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.transform.rotation=Quaternion.Euler(25,135,0);
        var camera=new GameObject("ReviewCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.12f,.14f);camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.orthographic=true;camera.orthographicSize=3.0f;
        Action<string,Vector3> render=(label,eye)=>{camera.transform.position=eye;camera.transform.LookAt(new Vector3(0,1.65f,-.3f));var rt=new RenderTexture(1600,1100,24);var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(Output+"/"+label+".png",image.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);};
        armor.SetActive(true);door.SetActive(true);plow.SetActive(true);
        render("both-front-left",new Vector3(-6,4,7));render("both-front-right",new Vector3(6,4,7));render("both-rear-left",new Vector3(-6,4,-8));
        pivot.localRotation=Quaternion.Euler(0,-110,0);render("armor-door-open",new Vector3(-6,3,-8));pivot.localRotation=Quaternion.identity;
        armor.SetActive(false);door.SetActive(false);render("plow-only",new Vector3(-6,3.8f,7));plow.SetActive(false);armor.SetActive(true);door.SetActive(true);render("armor-only",new Vector3(-6,3.8f,7));
        armor.SetActive(false);door.SetActive(false);render("no-mods",new Vector3(-6,3.8f,7));
        armor.SetActive(true);door.SetActive(true);plow.SetActive(true);
        CloseView(camera,"plow-detail",new Vector3(-3.1f,1.9f,6.0f),new Vector3(0,.80f,2.60f),1.28f);
        CloseView(camera,"armor-detail",new Vector3(-5,2.0f,.35f),new Vector3(-.60f,1.14f,-.30f),1.48f);
        string bundle=Output+"/Bundles/rebirth_camper80s.unity3d";if(!BuildPipeline.GetCRCForAssetBundle(bundle,out uint crc))throw new Exception("CRC absent");
        var loaded=AssetBundle.LoadFromFile(bundle,crc);if(!loaded)throw new Exception("Bundle CRC reopen failed");
        try
        {
            var asset=loaded.LoadAsset<GameObject>("rebirthCamper80s");
            foreach(string n in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow","REBIRTHModPlowHull"})
            {var t=asset.GetComponentsInChildren<Transform>(true).Single(x=>x.name==n);if(t.gameObject.activeSelf)throw new Exception("Mod root default visible "+n);}
            int markers=asset.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("REBIRTH_Seat"));if(markers!=30)throw new Exception("Seat markers lost");
            if(asset.GetComponentsInChildren<WheelCollider>(true).Length!=4)throw new Exception("Wheel contract changed");
            File.WriteAllText(Output+"/verified.txt","PASS CRC="+crc+"; 4 hidden native mod groups; 30 seat markers; 4 original wheels. Source/compiled geometry and Unity renders, no in-game tests.");
        }
        finally{loaded.Unload(true);Object.DestroyImmediate(root);}
    }
}







