using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class RebirthCamperFourTurretClay
{
    const string Folder="Assets/REBIRTHCamperFourTurretReview_20261019_v9";
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperFourTurretReview_20261019_v9";
    const float AssemblyScale=.65f;
    static Material metal;
    static Mesh plateMesh, hexMesh, washerMesh, gussetMesh;
    static GameObject Steel(Transform parent,string name,Mesh mesh,Vector3 pos,Quaternion rot,Vector3 scale)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localRotation=rot;g.transform.localScale=scale;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=metal;return g;
    }
    static void Fastener(Transform parent,Vector3 pos,Vector3 normal)
    {
        var rot=Quaternion.LookRotation(normal);
        Steel(parent,"Steel washer",washerMesh,pos,rot,Vector3.one);
        Steel(parent,"Hex bolt or retaining nut",hexMesh,pos+normal*.008f,rot,Vector3.one);
    }
    static Transform Find(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    static GameObject Part(Transform p,PrimitiveType type,string name,Vector3 position,Vector3 size)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=position;g.transform.localScale=size;
        Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=metal;return g;
    }
    static Mesh Gusset()
    {
        // Open diagonal bracket: mitred ends meet the wall plate and shelf.
        var ring=new[]{new Vector2(.942f,2.60f),new Vector2(1.49f,2.98f),new Vector2(1.38f,2.98f),new Vector2(.942f,2.677f)};
        var v=ring.Select(p=>new Vector3(p.x,p.y,-.025f)).Concat(ring.Select(p=>new Vector3(p.x,p.y,.025f))).ToArray();
        var m=new Mesh();m.vertices=v;m.uv=Enumerable.Repeat(Vector2.zero,8).ToArray();m.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};return Flat(m);
    }
    static void Rail(Transform p,Vector3 a,Vector3 b,float radius,string name)
    {
        var g=Part(p,PrimitiveType.Cylinder,name,(a+b)/2,new Vector3(radius*2,Vector3.Distance(a,b)/2,radius*2));
        g.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);
    }
    static void ExtendRails(GameObject root,List<string> report)
    {
        var rails=Find(root,"Fitted rooftop guardrails");
        var original=rails.GetComponentsInChildren<Renderer>(true);
        var group=new GameObject("Extended rear side guardrails").transform;group.SetParent(root.transform.Find("GameObject/M/VisualBody"),false);
        foreach(int side in new[]{-1,1})
        {
            var candidates=original.Where(r=>r && r.bounds.center.x*side>.5f && r.bounds.size.z>.15f && r.bounds.size.y<.10f).OrderBy(r=>r.bounds.min.z).ToArray();
            if(candidates.Length==0)throw new Exception("Side rail endpoint not found");
            var segments=candidates.OrderByDescending(r=>r.bounds.center.z).ToArray();
            var path=new List<Vector3>();float x=0,y=0;
            foreach(var old in segments)
            {
                var filter=old.GetComponent<MeshFilter>();var pts=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
                float avgZ=pts.Average(v=>v.z),avgY=pts.Average(v=>v.y);x=pts.Average(v=>v.x);
                float slope=pts.Sum(v=>(v.z-avgZ)*(v.y-avgY))/pts.Sum(v=>(v.z-avgZ)*(v.z-avgZ));
                float forward=pts.Max(v=>v.z)-.014f,back=pts.Min(v=>v.z)+.014f;
                if(path.Count==0)path.Add(new Vector3(x,avgY+slope*(forward-avgZ),forward));
                path.Add(new Vector3(x,avgY+slope*(back-avgZ),back));y=avgY+slope*(-3.10f-avgZ);
            }
            path.Add(new Vector3(x,y,-3.10f));
            for(int n=1;n<=24;n++){float a=n*Mathf.PI/48;path.Add(new Vector3(x,y-.04f+.04f*Mathf.Cos(a),-3.10f-.04f*Mathf.Sin(a)));}
            path.Add(new Vector3(x,2.88f,-3.14f));
            RebirthCamperTubeFinish.Tube(group,"Unbroken side rail with bent rear post",path,.014f,metal,Folder);
            foreach(var old in segments)Object.DestroyImmediate(old.gameObject);
            report.Add("Replaced rear side rail segment with continuous aligned bend, side "+side+"; ladder exit remains open.");
        }
    }
    static Mesh Flat(Mesh source)
    {
        var indices=source.triangles;var old=source.vertices;var uv=source.uv;
        var m=new Mesh();m.vertices=indices.Select(i=>old[i]).ToArray();m.uv=indices.Select(i=>uv[i]).ToArray();m.triangles=Enumerable.Range(0,indices.Length).ToArray();m.RecalculateNormals();m.RecalculateTangents();return m;
    }
    public static void Run()
    {
        // Reuse the verified roof-fitted ladder candidate without rebuilding it.
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperRearLadderBuild.Folder+"/rebirthCamper80s.prefab"));root.name="rebirthCamper80s_FourTurretReview";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RebirthTripoTurretImport.Folder+"/RustyCombatTurret.prefab");
        if(!prefab)throw new Exception("Optimized Tripo turret missing");
        metal=AssetDatabase.LoadAssetAtPath<Material>(RebirthCamperRearLadderBuild.Folder+"/LadderSteel.mat");
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
        plateMesh=Flat((Mesh)typeof(RebirthCamperArmorPlow).GetMethod("Plate",flags).Invoke(null,null));
        hexMesh=(Mesh)typeof(RebirthCamperArmorPlow).GetMethod("Shape",flags).Invoke(null,new object[]{6,.022f,.022f,.018f});
        washerMesh=(Mesh)typeof(RebirthCamperArmorPlow).GetMethod("Shape",flags).Invoke(null,new object[]{16,.028f,.028f,.008f});
        AssetDatabase.CreateAsset(plateMesh,Folder+"/PloughStylePlate.asset");AssetDatabase.CreateAsset(hexMesh,Folder+"/HexFastener.asset");AssetDatabase.CreateAsset(washerMesh,Folder+"/Washer.asset");
        gussetMesh=Gusset();AssetDatabase.CreateAsset(gussetMesh,Folder+"/WeldedBracket.asset");
        var mounts=new GameObject("Four outboard corner turrets - static placement review").transform;mounts.SetParent(root.transform.Find("GameObject/M/VisualBody"),false);
        var report=new List<string>{"Actual optimized Tripo model, four instances; placement review only, not a functional turret rig.","User correction: reduce every turret AND its complete support assembly by35% in every linear dimension. Final turret source scale0.78; platform diameter0.468m. v9 raises all platform tops to2.95m (+18cm); rear centres move30cm rearward toZ-2.98; front centres move12cm forward toZ0.92. Corner clearance remains subject to articulation checks. Open welded brackets replace solid triangular webs. Screws only on camper attachment plates; no support-joint or added platform fasteners. Camper and ladders unchanged; side guardrails extended to rear corners."};
        for(int i=0;i<4;i++)
        {
            int side=i%2==0?-1:1;bool rear=i>=2;float x=side*1.30f,z=rear?-3.10f:.80f;
            var corner=new GameObject((rear?"Rear":"Front")+(side<0?" left":" right")+" turret mount").transform;corner.SetParent(mounts,false);
            // Round platform with body-connected twin gusset supports, clear of ladder returns.
            Part(corner,PrimitiveType.Cylinder,"Circular fixed turret platform",new Vector3(x,3.06f,z),new Vector3(.72f,.03f,.72f));
            foreach(float dz in new[]{-.17f,.17f})
            {
                Steel(corner,"Camper screw attachment plate",plateMesh,new Vector3(side*.925f,2.78f,z+dz),Quaternion.Euler(0,90,0),new Vector3(.22f,.50f,.035f));
                Part(corner,PrimitiveType.Cube,"Welded horizontal shelf",new Vector3(side*1.205f,3.005f,z+dz),new Vector3(.56f,.05f,.12f));
                var rib=Steel(corner,"Open welded diagonal bracket",gussetMesh,new Vector3(0,0,z+dz),Quaternion.identity,Vector3.one);
                // Rotate rather than mirror scale so triangle winding stays valid.
                if(side<0)rib.transform.localRotation=Quaternion.Euler(0,180,0);
                foreach(float y in new[]{2.61f,2.94f})
                foreach(float offset in new[]{-.073f,.073f})
                    Fastener(corner,new Vector3(side*.944f,y,z+dz+offset),Vector3.right*side);
            }
            var turret=Object.Instantiate(prefab,corner);turret.name="Optimized Tripo turret "+(i+1);turret.transform.localScale=Vector3.one*1.2f;
            // Bounds of the bottom ring, not the off-centre whole-model bounds,
            // determine the rotation/mount centre.
            var filter=turret.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Turret_LOD0");
            var points=filter.sharedMesh.vertices.Select(v=>turret.transform.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
            float bottom=points.Min(v=>v.y);var ring=points.Where(v=>v.y<bottom+.045f).ToArray();
            var baseCentre=new Vector3((ring.Min(v=>v.x)+ring.Max(v=>v.x))/2,bottom,(ring.Min(v=>v.z)+ring.Max(v=>v.z))/2);
            float yaw=rear?side*135f:side*45f;turret.transform.localRotation=Quaternion.Euler(0,yaw,0);
            turret.transform.localPosition=new Vector3(x,3.09f,z)-turret.transform.localRotation*(baseCentre*1.2f);
            turret.GetComponent<LODGroup>().ForceLOD(0);
            // Scale the entire assembly around the body's attachment face and
            // platform top, not the world origin: all brackets stay connected.
            var anchor=new Vector3(side*.9075f,3.09f,z);
            var renderers=corner.GetComponentsInChildren<Renderer>(true);
            var before=renderers[0].bounds;foreach(var r in renderers)before.Encapsulate(r.bounds);
            corner.localScale=Vector3.one*AssemblyScale;
            corner.localPosition=anchor*(1-AssemblyScale)+new Vector3(0,-.14f,rear?.12f:.12f);
            var after=renderers[0].bounds;foreach(var r in renderers)after.Encapsulate(r.bounds);
            if((after.size-before.size*AssemblyScale).magnitude>.001f)throw new Exception("Assembly did not shrink uniformly");
            var centre=anchor+(new Vector3(x,3.09f,z)-anchor)*AssemblyScale+new Vector3(0,-.14f,rear?.12f:.12f);
            report.Add("PASS "+corner.name+" uniformScale="+AssemblyScale+" centre="+centre.ToString("F6")+" yaw="+yaw+" beforeBounds="+before.size+" afterBounds="+after.size+" body contact x="+(side*.9075f));
        }
        // Remove only the two orphaned upper rear marker-lens tabs highlighted by the user.
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="BodyMarkerLenses"||f.name=="lights"))
        {
            var mesh=Object.Instantiate(f.sharedMesh);var vs=mesh.vertices;int removed=0;
            Func<int,bool> tab=i=>{var v=f.transform.TransformPoint(vs[i]);return Mathf.Abs(v.x)>.88f&&Mathf.Abs(v.x)<.91f&&v.y>2.99f&&v.y<3.04f&&v.z> -3.20f&&v.z< -3.10f;};
            for(int sub=0;sub<mesh.subMeshCount;sub++){var ts=mesh.GetTriangles(sub);var keep=new List<int>();for(int t=0;t<ts.Length;t+=3){if(tab(ts[t])&&tab(ts[t+1])&&tab(ts[t+2])){removed++;continue;}keep.AddRange(new[]{ts[t],ts[t+1],ts[t+2]});}mesh.SetTriangles(keep,sub);}
            if(removed>0){AssetDatabase.CreateAsset(mesh,Folder+"/Cleaned_"+f.name+".asset");f.sharedMesh=mesh;report.Add("Removed green tabs: "+f.name+" triangles="+removed);}
        }
        ExtendRails(root,report);
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/FourTurretCamperReview.prefab");AssetDatabase.SaveAssets();
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(string name in new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"})Find(root,name).gameObject.SetActive(false);
        foreach(string name in new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"})
        {var t=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);if(t)t.gameObject.SetActive(true);}
        Find(root,"REBIRTHRearDoorPivot").localRotation=Quaternion.Euler(0,-90,0);
        var clay=new Material(Shader.Find("Standard")){color=new Color(.64f,.64f,.64f)};clay.SetFloat("_Glossiness",.22f);
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>clay).ToArray();
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
        var key=new GameObject("Clay key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.05f;key.shadows=LightShadows.Soft;key.transform.rotation=Quaternion.Euler(40,-35,0);
        var fill=new GameObject("Clay fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.55f;fill.transform.rotation=Quaternion.Euler(30,160,0);
        QualitySettings.shadowDistance=30;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;
        Render(new Vector3(7,6,8),new Vector3(0,1.85f,-.6f),3.8f,"clay-front-quarter");
        Render(new Vector3(-6,5.4f,-8),new Vector3(0,1.9f,-1.2f),3.4f,"clay-rear-quarter");
        Render(new Vector3(0,12,-.8f),new Vector3(0,0,-.8f),3.9f,"clay-top");
        Render(new Vector3(-3.2f,4.2f,-6.7f),new Vector3(0,2.55f,-3.05f),2.0f,"clay-rear-ladders-turrets");
        File.WriteAllLines(Output+"/placement-review.txt",report);Object.DestroyImmediate(root);RebirthCamperTurretCornerCloseups.Run();
    }
    static void Render(Vector3 eye,Vector3 target,float size,string name)
    {
        var camera=new GameObject(name).AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=size;camera.nearClipPlane=.01f;camera.transform.position=eye;camera.transform.LookAt(target);camera.backgroundColor=new Color(.16f,.17f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
        var rt=new RenderTexture(1700,1400,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1700,1400,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1700,1400),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
}
