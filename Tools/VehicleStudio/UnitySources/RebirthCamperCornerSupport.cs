using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Actual-camper placement candidate. No source turret meshes are changed.
public static class RebirthCamperCornerSupport
{
    const string Folder="Assets/REBIRTHCornerSupportReview_20261019";
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output";
    static Transform owner;
    static Material steel;
    static int index;
    static string meshPrefix="Part";
    static bool showTurrets;
    static bool frontAudit=true;
    static bool stationReview;
    static bool productionStage;
    internal static int LastAuditHits;
    public static void ProductionStage(){productionStage=true;stationReview=true;frontAudit=false;FrontLadders();}
    public static void StationsReview(){stationReview=true;frontAudit=false;FrontLadders();}
    public static void FrontLaddersPreview(){frontAudit=false;FrontLadders();}
    public static void WithTurrets(){showTurrets=true;Run();}
    static readonly List<string> report=new List<string>();
    static Mesh Geometry(string name,List<Vector3[]> faces)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
        foreach(var face in faces)
        {
            int start=vertices.Count;
            var normal=Vector3.Cross(face[1]-face[0],face[2]-face[0]).normalized;
            foreach(var p in face)
            {
                vertices.Add(owner.InverseTransformPoint(p));
                var q=Mathf.Abs(normal.y)>.7f?new Vector2(p.x,p.z):Mathf.Abs(normal.x)>.7f?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
                uv.Add(new Vector2(.01f+Mathf.Repeat(q.x*.12f,.07f),.015f+Mathf.Repeat(q.y*.12f,.07f)));
            }
            for(int i=1;i<face.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        string path=Folder+"/"+meshPrefix+(index++)+".asset";
        if(AssetDatabase.LoadAssetAtPath<Mesh>(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject(name);go.transform.SetParent(owner,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=steel;
        return mesh;
    }
    static void Face(List<Vector3[]> faces,IEnumerable<Vector3> points,Vector3 outward)
    {
        var a=points.ToArray();if(Vector3.Dot(Vector3.Cross(a[1]-a[0],a[2]-a[0]),outward)<0)Array.Reverse(a);faces.Add(a);
    }
    static Mesh Box(string name,Vector3 center,Vector3 size,Quaternion rotation,float bevel=.0015f)
    {
        var faces=new List<Vector3[]>();var half=size*.5f;bevel=Mathf.Min(bevel,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.4f);
        var inner=half-Vector3.one*bevel;
        Func<Vector3,Vector3> point=p=>center+rotation*p;
        // Six inset faces, twelve chamfer edges and eight corner triangles.
        for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
        {
            int b=(axis+1)%3,c=(axis+2)%3;var list=new List<Vector3>();
            foreach(var pair in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})
            {var p=Vector3.zero;p[axis]=half[axis]*sign;p[b]=inner[b]*pair.x;p[c]=inner[c]*pair.y;list.Add(point(p));}
            var n=Vector3.zero;n[axis]=sign;Face(faces,list,rotation*n);
        }
        for(int a=0;a<3;a++)for(int b=a+1;b<3;b++)for(int sa=-1;sa<=1;sa+=2)for(int sb=-1;sb<=1;sb+=2)
        {
            int c=3-a-b;var list=new List<Vector3>();
            foreach(var pair in new[]{new Vector2(0,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(0,1)})
            {var p=Vector3.zero;p[a]=sa*(pair.x==0?half[a]:inner[a]);p[b]=sb*(pair.x==0?inner[b]:half[b]);p[c]=pair.y*inner[c];list.Add(point(p));}
            var n=Vector3.zero;n[a]=sa;n[b]=sb;Face(faces,list,rotation*n);
        }
        for(int sx=-1;sx<=1;sx+=2)for(int sy=-1;sy<=1;sy+=2)for(int sz=-1;sz<=1;sz+=2)
        {
            var s=new Vector3(sx,sy,sz);var list=new List<Vector3>();
            for(int a=0;a<3;a++){var p=Vector3.Scale(inner,s);p[a]=half[a]*s[a];list.Add(point(p));}Face(faces,list,rotation*s);
        }
        return Geometry(name,faces);
    }
    static void Beam(string name,Vector3 a,Vector3 b,float width=.028f,float depth=.018f)
    {Box(name,(a+b)*.5f,new Vector3(width,depth,Vector3.Distance(a,b)+.001f),Quaternion.LookRotation(b-a,Vector3.up));}
    static void Disc(string name,Vector3 center,float radius,float height,int count=64)
    {
        var faces=new List<Vector3[]>();float bevel=.0015f;
        float[] rs={radius-bevel,radius,radius,radius-bevel};float[] ys={-height/2,-height/2+bevel,height/2-bevel,height/2};
        Func<int,int,Vector3> p=(ring,i)=>center+new Vector3(Mathf.Cos(i*2*Mathf.PI/count)*rs[ring],ys[ring],Mathf.Sin(i*2*Mathf.PI/count)*rs[ring]);
        for(int i=0;i<count;i++)
        {
            for(int ring=0;ring<3;ring++)Face(faces,new[]{p(ring,i),p(ring,i+1),p(ring+1,i+1),p(ring+1,i)},(p(ring,i)-center).normalized+Vector3.up*(ring==0?-1:ring==2?1:0));
            Face(faces,new[]{center+Vector3.up*height/2,p(3,i),p(3,i+1)},Vector3.up);
            Face(faces,new[]{center-Vector3.up*height/2,p(0,i),p(0,i+1)},Vector3.down);
        }
        Geometry(name,faces);
    }
    // Probe every back corner, rather than seating a flat plate on one surface
    // normal and leaving the remaining corners suspended over the sloping skin.
    static Vector3 ConformingBodyFoot(MeshCollider body,Vector3 centre,Vector3 normal,float width,float height,float thickness)
    {
        var right=Vector3.Cross(Vector3.up,normal).normalized;
        var up=Vector3.Cross(normal,right).normalized;
        var back=new Vector3[4];var front=new Vector3[4];
        var pairs=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};
        var average=Vector3.zero;
        for(int i=0;i<4;i++)
        {
            var p=centre+right*pairs[i].x*width*.5f+up*pairs[i].y*height*.5f;
            if(!body.Raycast(new Ray(p+normal*.15f,-normal),out var hit,.3f))throw new Exception("Body fixing foot corner misses camper skin");
            back[i]=hit.point+normal*.0005f;front[i]=back[i]+normal*thickness;average+=front[i]*.25f;
        }
        var faces=new List<Vector3[]>();Face(faces,back,-normal);Face(faces,front,normal);
        for(int i=0;i<4;i++){int j=(i+1)%4;Face(faces,new[]{back[i],back[j],front[j],front[i]},(back[i]+back[j])*.5f-centre);}
        Geometry("Front ladder rigid fixing foot",faces);return average;
    }
    static void Collar(Vector3 center,float radius,float height,Vector3? railAxis=null)
    {
        var faces=new List<Vector3[]>();const int count=48;float inside=radius+.0004f,outside=radius+.007f;
        var rotation=Quaternion.FromToRotation(Vector3.up,railAxis??Vector3.up);
        Func<float,float,int,Vector3> p=(r,y,i)=>center+rotation*new Vector3(Mathf.Cos(i*2*Mathf.PI/count)*r,y,Mathf.Sin(i*2*Mathf.PI/count)*r);
        for(int i=0;i<count;i++)
        {
            if(i==11||i==35)continue;
            Face(faces,new[]{p(outside,-height/2,i),p(outside,-height/2,i+1),p(outside,height/2,i+1),p(outside,height/2,i)},p(outside,0,i)-center);
            Face(faces,new[]{p(inside,-height/2,i),p(inside,height/2,i),p(inside,height/2,i+1),p(inside,-height/2,i+1)},center-p(inside,0,i));
            foreach(int s in new[]{-1,1})Face(faces,new[]{p(inside,s*height/2,i),p(outside,s*height/2,i),p(outside,s*height/2,i+1),p(inside,s*height/2,i+1)},Vector3.up*s);
            foreach(int edge in new[]{i,i+1})if(edge==11||edge==12||edge==35||edge==36)Face(faces,new[]{p(inside,-height/2,edge),p(outside,-height/2,edge),p(outside,height/2,edge),p(inside,height/2,edge)},Quaternion.Euler(0,90,0)*(p(outside,0,edge)-center)*(edge==11||edge==35?1:-1));
        }
        Geometry("Split saddle on ladder left rail",faces);
    }
    static void RecessedSocket(Vector3 center,Transform turret,List<string> lines)
    {
        var fixedMesh=turret.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="FixedSupport_LOD0");
        var points=fixedMesh.sharedMesh.vertices.Select(v=>fixedMesh.transform.TransformPoint(v)).ToArray();
        float top=center.y+.011f,bottom=center.y-.011f;
        var teeth=points.Where(p=>p.y<top+.002f).ToArray();if(teeth.Length==0)throw new Exception("No locating teeth in support socket height");
        float floor=teeth.Min(p=>p.y)-.001f;
        if(floor<=bottom+.001f)throw new Exception("Platform too shallow for measured locating teeth");
        float inside=teeth.Max(p=>new Vector2(p.x-center.x,p.z-center.z).magnitude)+.0015f,outside=inside+.005f;
        Disc("Socket platform floor",new Vector3(center.x,(bottom+floor)/2,center.z),outside,floor-bottom);
        const int count=96;var faces=new List<Vector3[]>();
        Func<float,float,int,Vector3> point=(r,y,i)=>new Vector3(center.x+Mathf.Cos(i*Mathf.PI*2/count)*r,y,center.z+Mathf.Sin(i*Mathf.PI*2/count)*r);
        for(int i=0;i<count;i++)
        {
            var radial=point(outside,center.y,i)-center;
            Face(faces,new[]{point(outside,bottom,i),point(outside,bottom,i+1),point(outside,top,i+1),point(outside,top,i)},radial);
            Face(faces,new[]{point(inside,floor,i),point(inside,top,i),point(inside,top,i+1),point(inside,floor,i+1)},-radial);
            Face(faces,new[]{point(inside,top,i),point(outside,top,i),point(outside,top,i+1),point(inside,top,i+1)},Vector3.up);
        }
        Geometry("Recessed locating tooth socket wall",faces);
        lines.Add("Measured socket centre="+center.ToString("F5")+" innerRadius="+inside.ToString("F5")+" outerRadius="+outside.ToString("F5")+" floor="+floor.ToString("F5")+" teethMinY="+teeth.Min(p=>p.y).ToString("F5")+" sampled tooth vertices="+teeth.Length+". Minimum1.5mm radial and1mm floor clearance.");
    }
    static void Fastener(string name,Vector3 center,float radius,float length,int sides)
    {
        var faces=new List<Vector3[]>();
        Func<float,int,Vector3> p=(x,i)=>center+new Vector3(x,Mathf.Cos(i*2*Mathf.PI/sides)*radius,Mathf.Sin(i*2*Mathf.PI/sides)*radius);
        for(int i=0;i<sides;i++)
        {
            Face(faces,new[]{p(-length/2,i),p(-length/2,i+1),p(length/2,i+1),p(length/2,i)},p(0,i)-center);
            Face(faces,new[]{center-Vector3.right*length/2,p(-length/2,i),p(-length/2,i+1)},Vector3.left);
            Face(faces,new[]{center+Vector3.right*length/2,p(length/2,i),p(length/2,i+1)},Vector3.right);
        }
        Geometry(name,faces);
    }
    static Transform MirrorRight(Transform left)
    {
        var right=new GameObject("Rear right ladder support placement candidate").transform;right.SetParent(left.parent,false);
        foreach(var source in left.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=Object.Instantiate(source.sharedMesh);var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++){var p=source.transform.TransformPoint(vertices[i]);p.x=-p.x;vertices[i]=right.InverseTransformPoint(p);}
            mesh.vertices=vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string path=Folder+"/RightPart"+(index++)+".asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(source.name.Replace("left rail","right rail"));go.transform.SetParent(right,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<Renderer>().sharedMaterials;
        }
        return right;
    }
    public static void FrontLeft()
    {
        index=0;meshPrefix="FrontPart";showTurrets=false;
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab"));
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
            if(t.name.StartsWith("REBIRTHCamperTurretSlot")||new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
        steel=AssetDatabase.LoadAssetAtPath<Material>("Assets/REBIRTHCamperApprovedArmor_20261018_v4/plates.mat");
        if(!steel)throw new Exception("Approved armor material missing");
        owner=new GameObject("Front left permanent body bracket candidate").transform;
        owner.SetParent(root.transform.Find("GameObject/M/VisualBody"),false);
        var body=root.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="camper_body");
        var probe=new GameObject("Temporary permanent body surface probe");probe.transform.SetPositionAndRotation(body.transform.position,body.transform.rotation);probe.transform.localScale=body.transform.lossyScale;
        var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=body.sharedMesh;Physics.SyncTransforms();
        var lines=new List<string>();var platform=new Vector3(-1.13f,2.94f,1.13f);
        Disc("Single circular front support platform",platform,.111224f,.022f);
        // Probe the permanent mesh rather than optional armor. Feet and hardware share the measured face normal.
        for(int side=0;side<2;side++)
        {
            var ray=side==0?new Ray(new Vector3(-3,2.79f,.69f),Vector3.right):new Ray(new Vector3(-.66f,2.79f,3),Vector3.back);
            if(!collider.Raycast(ray,out var hit,5))throw new Exception("Permanent front attachment surface missing: "+side);
            var n=-ray.direction;var across=Vector3.Cross(Vector3.up,n).normalized;
            Func<float,float,Vector3> surface=(dx,dy)=>{
                var sample=new Ray(ray.origin+across*dx+Vector3.up*dy,ray.direction);
                if(!collider.Raycast(sample,out var h,5))throw new Exception("Attachment foot surface sample missing");
                return h.point;
            };
            // A rigid foot follows the panel's overall slope, with fitted pads behind it.
            var vertical=(surface(0,.065f)-surface(0,-.065f)).normalized;
            var faceNormal=Vector3.Cross(across,vertical).normalized;if(Vector3.Dot(faceNormal,n)<0)faceNormal=-faceNormal;
            float crest=float.NegativeInfinity;
            for(int row=0;row<=40;row++)foreach(float dx in new[]{-.032f,0,.032f})
                crest=Mathf.Max(crest,Vector3.Dot(surface(dx,Mathf.Lerp(-.079f,.079f,row/40f))-hit.point,faceNormal));
            var foot=hit.point+faceNormal*(crest+.006f);var footRotation=Quaternion.LookRotation(faceNormal,vertical);
            Box("Rigid bevelled body attachment foot "+side,foot,new Vector3(.064f,.16f,.008f),footRotation,.002f);
            var end=platform+new Vector3(side==0?-.018f:.035f,-.018f,side==0?-.035f:.018f);
            var upper=foot+vertical*.039f+faceNormal*.007f;var lower=foot-vertical*.039f+faceNormal*.007f;
            Beam("Welded upper cantilever "+side,upper,end,.022f,.016f);
            Beam("Open welded diagonal brace "+side,lower,end,.022f,.016f);
            foreach(var joint in new[]{upper,lower})
            {
                var seam=joint-faceNormal*.0015f-vertical*.009f;var weldFaces=new List<Vector3[]>();
                Func<int,int,Vector3> wp=(r,a)=>seam+across*Mathf.Lerp(-.012f,.012f,r/24f)+(vertical*Mathf.Cos(a*Mathf.PI/4)+faceNormal*Mathf.Sin(a*Mathf.PI/4))*(.0022f+.0002f*Mathf.Cos(r*Mathf.PI));
                for(int r=0;r<24;r++)for(int a=0;a<8;a++)Face(weldFaces,new[]{wp(r,a),wp(r+1,a),wp(r+1,a+1),wp(r,a+1)},wp(r,a)-(seam+across*Mathf.Lerp(-.012f,.012f,r/24f)));
                Geometry("Continuous attachment fillet weld",weldFaces);
            }
            foreach(float dy in new[]{-.060f,.060f})
            {
                // Fastener geometry is authored along world X; rotate its vertices about its own centre.
                var padFace=foot+vertical*dy-faceNormal*.004f;
                var padRay=new Ray(padFace+faceNormal*.08f,-faceNormal);
                if(!collider.Raycast(padRay,out var padHit,.2f))throw new Exception("Rigid foot backing pad surface missing");
                float padDepth=Vector3.Dot(padFace-padHit.point,faceNormal)+.001f;
                Box("Fitted body spacer pad",padFace-faceNormal*padDepth*.5f,new Vector3(.027f,.026f,padDepth),footRotation,.001f);
                var boltNormal=faceNormal;var center=foot+vertical*dy+faceNormal*.0055f;int first=owner.childCount;
                Fastener("Body attachment washer",center,.009f,.002f,24);
                Fastener("Body attachment hex bolt",center+boltNormal*.004f,.0065f,.006f,6);
                var rotation=Quaternion.FromToRotation(Vector3.right,boltNormal);
                for(int i=first;i<owner.childCount;i++)
                {
                    var f=owner.GetChild(i).GetComponent<MeshFilter>();var vs=f.sharedMesh.vertices;
                    var pivot=i==first?center:center+boltNormal*.004f;
                    for(int v=0;v<vs.Length;v++)vs[v]=owner.InverseTransformPoint(pivot+rotation*(owner.TransformPoint(vs[v])-pivot));
                    f.sharedMesh.vertices=vs;f.sharedMesh.RecalculateNormals();f.sharedMesh.RecalculateTangents();f.sharedMesh.RecalculateBounds();
                }
            }
            lines.Add("Foot "+side+" permanent camper_body point="+hit.point.ToString("F5")+" normal="+n.ToString("F5"));
        }
        Object.DestroyImmediate(probe);
        PrefabUtility.SaveAsPrefabAsset(owner.gameObject,Folder+"/FrontLeftSupport.prefab");AssetDatabase.SaveAssets();
        lines.Add("Staged visual candidate only. Feet raycast onto permanent camper_body; no ladder/armor pipe dependency. Platform="+platform.ToString("F5")+" diameter=.222448. Armor and installed turret sweep audits pending.");
        File.WriteAllLines(Output+"/front-left-support-placement.txt",lines);
        var clay=new Material(Shader.Find("Standard")){color=new Color(.38f,.40f,.43f)};clay.SetFloat("_Glossiness",.22f);
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(_=>clay).ToArray();
        RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.12f,.12f,.12f);
        QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowDistance=10;QualitySettings.shadowCascades=0;
        var light=new GameObject("Front support key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.7f;light.shadows=LightShadows.Soft;light.shadowBias=.08f;light.shadowNormalBias=.15f;light.transform.rotation=Quaternion.Euler(35,135,0);
        foreach(bool armor in new[]{false,true})
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor"}.Contains(t.name))t.gameObject.SetActive(armor);
            string suffix=armor?"armor-on":"armor-off";
            Render(new Vector3(-3.8f,4.3f,3.5f),new Vector3(-.92f,2.85f,.96f),.43f,"front-left-support-"+suffix);
            Render(new Vector3(-.92f,9,.96f),new Vector3(-.92f,0,.96f),.43f,"front-left-support-overhead-"+suffix);
            if(!armor)
            {
                Render(new Vector3(-1.8f,3.05f,.35f),new Vector3(-.90f,2.79f,.69f),.125f,"front-left-body-foot-closeup");
                Render(new Vector3(-.35f,3.05f,1.9f),new Vector3(-.66f,2.79f,.94f),.125f,"front-front-body-foot-closeup");
            }
        }
        Object.DestroyImmediate(root);Object.DestroyImmediate(clay);
    }
    public static void FrontLadders()
    {
        index=0;meshPrefix="FrontLadderPart";showTurrets=false;Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab"));RebirthCamperCampIntegration.SetMode(root,true);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name.StartsWith("REBIRTHCamperTurretSlot")||new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
        steel=AssetDatabase.LoadAssetAtPath<Material>("Assets/REBIRTHCamperApprovedArmor_20261018_v4/plates.mat");
        owner=new GameObject("Short straight front ladders and ladder mounted supports").transform;owner.SetParent(root.transform.Find("GameObject/M/VisualBody"),false);
        var body=root.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="camper_body");
        var probe=new GameObject("Front ladder permanent body probe");probe.transform.SetPositionAndRotation(body.transform.position,body.transform.rotation);probe.transform.localScale=body.transform.lossyScale;
        var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=body.sharedMesh;Physics.SyncTransforms();var lines=new List<string>();
        foreach(int side in new[]{-1,1})
        {
            foreach(float x in new[]{side*.94f,side*.62f})
            {
                var path=new List<Vector3>{new Vector3(x,2.28f,1.17f),new Vector3(x,2.86f,1.17f)};
                float shoe=Mathf.Clamp(x,-.82f,.82f);
                for(int i=1;i<=24;i++){float t=i/24f;path.Add(new Vector3(Mathf.Lerp(x,shoe,t),2.86f+.12f*Mathf.Sin(t*Mathf.PI/2),1.05f+.12f*Mathf.Cos(t*Mathf.PI/2)));}
                path.Add(new Vector3(shoe,2.98f,.76f));
                for(int i=1;i<=16;i++){float a=i*Mathf.PI/32;path.Add(new Vector3(shoe,2.92f+.06f*Mathf.Cos(a),.76f-.06f*Mathf.Sin(a)));}
                if(!collider.Raycast(new Ray(new Vector3(shoe,4,.70f),Vector3.down),out var roof,2))throw new Exception("Front ladder roof shoe misses permanent camper");
                path.Add(new Vector3(shoe,roof.point.y+.004f,.70f));
                RebirthCamperTubeFinish.Tube(owner,"Short front ladder continuous handhold",path,.023f,steel,Folder);
                Disc("Front ladder roof flange",new Vector3(shoe,roof.point.y+.004f,.70f),.037f,.008f,32);
                foreach(float y in new[]{2.38f,2.72f})
                {
                    float mx=Mathf.Clamp(x,-.83f,.83f);
                    if(!collider.Raycast(new Ray(new Vector3(mx,y,3),Vector3.back),out var hit,4))throw new Exception("Front ladder body mount misses camper");
                    var n=hit.normal;if(n.z<0)n=-n;
                    var foot=ConformingBodyFoot(collider,hit.point,n,.07f,.115f,.018f);
                    Beam("Variable length welded ladder stand off",foot,new Vector3(x,y,1.17f),.025f,.025f);
                    lines.Add("side="+side+" railX="+x+" mountY="+y+" permanent body="+hit.point.ToString("F4")+" stand-off="+Vector3.Distance(foot,new Vector3(x,y,1.17f)).ToString("F4"));
                }
            }
            foreach(float y in new[]{2.34f,2.59f,2.84f})RebirthCamperTubeFinish.Tube(owner,"Front metal ladder rung",new List<Vector3>{new Vector3(side*.62f,y,1.17f),new Vector3(side*.94f,y,1.17f)},.021f,steel,Folder);
            var disk=new Vector3(side*1.152f,2.891f,1.25f);Disc("Single ladder mounted front turret platform",disk,.111224f,.022f);
            foreach(float y in new[]{2.68f,2.82f})Collar(new Vector3(side*.94f,y,1.17f),.023f,.044f);
            foreach(float dz in new[]{-.016f,.016f})
            {
                var end=new Vector3(side*1.14f,2.873f,1.25f+dz);
                Beam("Ladder mounted welded upper arm",new Vector3(side*.969f,2.82f,1.17f+dz),end,.018f,.014f);
                Beam("Ladder mounted open diagonal brace",new Vector3(side*.969f,2.68f,1.17f+dz),end,.018f,.014f);
            }
        }
        // Mirror the actual rear guardrail geometry, including the open ladder exits.
        var rails=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Fitted rooftop guardrails");
        var rear=rails.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<Renderer>().bounds.max.z< -2.70f).ToArray();
        foreach(var f in rails.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<Renderer>().bounds.max.z>.35f).ToArray())Object.DestroyImmediate(f.gameObject);
        foreach(var source in rear)
        {
            var mesh=Object.Instantiate(source.sharedMesh);var vs=mesh.vertices;
            for(int i=0;i<vs.Length;i++){var p=source.transform.TransformPoint(vs[i]);p.z=-p.z-2.45f;vs[i]=owner.InverseTransformPoint(p);}mesh.vertices=vs;
            var ts=mesh.triangles;for(int i=0;i<ts.Length;i+=3){int swap=ts[i+1];ts[i+1]=ts[i+2];ts[i+2]=swap;}mesh.triangles=ts;mesh.RecalculateNormals();mesh.RecalculateBounds();
            string asset=Folder+"/FrontLadderPart"+(index++)+".asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(asset))AssetDatabase.DeleteAsset(asset);AssetDatabase.CreateAsset(mesh,asset);
            var g=new GameObject("Mirrored rear guardrail at front");g.transform.SetParent(owner,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=steel;
        }
        if(rear.Length==0)
        {
            // The source rear centre rail is combined with the ladder mesh. Reproduce its exact authored path.
            // Roof probe measured the first forward groove at .690 and the second at .810.
            const float groove=.810f;
            var centre=new List<Vector3>{new Vector3(-.38f,2.88f,groove),new Vector3(-.38f,3.07f,groove)};
            for(int i=1;i<=16;i++){float a=i*Mathf.PI/32;centre.Add(new Vector3(-.34f-.04f*Mathf.Cos(a),3.07f+.04f*Mathf.Sin(a),groove));}
            centre.Add(new Vector3(.34f,3.11f,groove));
            for(int i=1;i<=16;i++){float a=i*Mathf.PI/32;centre.Add(new Vector3(.34f+.04f*Mathf.Sin(a),3.07f+.04f*Mathf.Cos(a),groove));}
            centre.Add(new Vector3(.38f,2.88f,groove));
            RebirthCamperTubeFinish.Tube(owner,"Front centre safety rail mirrored from rear path",centre,.014f,steel,Folder);
            foreach(int side in new[]{-1,1})
            {
                var original=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Unbroken side rail with bent rear post"&&f.GetComponent<Renderer>().bounds.center.x*side>0).ToArray();
                foreach(var source in original)
                {
                    // Keep source geometry behind the cut, clipping triangles at the new front return.
                    var faces=new List<Vector3[]>();var vs=source.sharedMesh.vertices;var ts=source.sharedMesh.triangles;
                    for(int i=0;i<ts.Length;i+=3)
                    {
                        var polygon=new List<Vector3>{source.transform.TransformPoint(vs[ts[i]]),source.transform.TransformPoint(vs[ts[i+1]]),source.transform.TransformPoint(vs[ts[i+2]])};var clipped=new List<Vector3>();
                        for(int j=0;j<3;j++){var a=polygon[j];var b=polygon[(j+1)%3];bool inside=a.z<=.35f;if(inside)clipped.Add(a);if(inside!=(b.z<=.35f))clipped.Add(Vector3.Lerp(a,b,(.35f-a.z)/(b.z-a.z)));}
                        if(clipped.Count>=3)faces.Add(clipped.ToArray());
                    }
                    Geometry("Side safety rail trimmed for mirrored front return",faces);Object.DestroyImmediate(source.gameObject);
                }
                var path=new List<Vector3>{new Vector3(side*.88f,3.19f,.35f),new Vector3(side*.88f,3.19f,.61f)};
                for(int i=1;i<=24;i++){float a=i*Mathf.PI/48;path.Add(new Vector3(side*.88f,3.15f+.04f*Mathf.Cos(a),.61f+.04f*Mathf.Sin(a)));}
                path.Add(new Vector3(side*.88f,2.94f,.65f));
                RebirthCamperTubeFinish.Tube(owner,"Side guardrail front return leaving ladder exit open",path,.014f,steel,Folder);
            }
            lines.Add("Front centre guardrail reproduced from exact rear build path reflected about Z; side returns added and original side tips clipped at Z.35. Front ladder exits remain open.");
        }
        lines.Add("Straight front stiles constant Z1.17, bottom2.28, top bend2.98; variable stand-offs to actual permanent body. Three rungs. Rear guardrail mesh count mirrored="+rear.Length+". Driving sightline, roof flange full footprint and pipe/rig clearances NOT yet validated. Visual candidate only; prior body-mounted turret support superseded.");
        File.WriteAllLines(Output+"/short-front-ladders.txt",lines);Object.DestroyImmediate(probe);
        PrefabUtility.SaveAsPrefabAsset(owner.gameObject,Folder+"/ShortFrontLadders.prefab");AssetDatabase.SaveAssets();
        var fitted=new List<Transform>();
        foreach(int side in new[]{-1,1})
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/RustyRobotTurret.prefab");
            var old=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Optimized robot turret "+(side<0?1:2));float scale=old.lossyScale.y;
            var turret=Object.Instantiate(source,owner);turret.name="Fitted actual front "+(side<0?"left":"right")+" turret";
            turret.transform.position=new Vector3(side*1.152f,2.891f+.011f-.045f*scale-.001f,1.25f);
            turret.transform.rotation=Quaternion.LookRotation(new Vector3(side,0,1));var ps=owner.lossyScale;turret.transform.localScale=new Vector3(scale/ps.x,scale/ps.y,scale/ps.z);
            foreach(var lod in turret.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);fitted.Add(turret.transform);
        }
        foreach(var f in owner.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Single ladder mounted front turret platform").ToArray())Object.DestroyImmediate(f.gameObject);
        for(int i=0;i<2;i++)RecessedSocket(new Vector3(i==0?-1.152f:1.152f,2.891f,1.25f),fitted[i],lines);
        foreach(var t in fitted)t.SetParent(null,true);
        PrefabUtility.SaveAsPrefabAsset(owner.gameObject,Folder+"/ShortFrontLadders.prefab");
        foreach(var t in fitted)t.SetParent(owner,true);AssetDatabase.SaveAssets();
        File.WriteAllLines(Output+"/short-front-ladders.txt",lines);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor"}.Contains(t.name))t.gameObject.SetActive(true);
        if(frontAudit)for(int i=0;i<2;i++)AuditPipeZone(root,fitted[i],fitted[1-i],new Bounds(new Vector3(i==0?-.95f:.95f,2.90f,1.12f),new Vector3(.85f,.9f,1.1f)),fitted[i],"front-"+(i==0?"left":"right")+"-clearance.txt",true);
        if(stationReview){ReviewStations(root,fitted,lines);Object.DestroyImmediate(root);return;}
        var clay=new Material(Shader.Find("Standard")){color=new Color(.40f,.42f,.45f)};clay.SetFloat("_Glossiness",.2f);
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(_=>clay).ToArray();
        RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.13f,.13f,.13f);
        var light=new GameObject("Short ladder review key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,135,0);
        foreach(bool armor in new[]{false,true})
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor"}.Contains(t.name))t.gameObject.SetActive(armor);
            Render(new Vector3(-3.8f,4.3f,3.5f),new Vector3(-.8f,2.65f,1.05f),.65f,"short-front-ladder-"+(armor?"armor-on":"armor-off"));
        }
        Render(new Vector3(0,3,5),new Vector3(0,2.35f,.9f),1.25f,"short-front-ladders-front");
        Render(new Vector3(0,9,.7f),new Vector3(0,0,.7f),1.35f,"short-front-ladders-overhead");
        foreach(float angle in new[]{-90,0,90})
        {
            foreach(var t in fitted.SelectMany(f=>f.GetComponentsInChildren<Transform>()).Where(t=>t.name=="TurretYaw"))t.localRotation=Quaternion.Euler(0,angle,0);
            Render(new Vector3(-3.8f,4.3f,3.5f),new Vector3(-1,2.96f,1.18f),.48f,"front-turret-yaw-"+angle);
        }
        foreach(var t in fitted.SelectMany(f=>f.GetComponentsInChildren<Transform>()).Where(t=>t.name=="TurretYaw"))t.localRotation=Quaternion.identity;
        foreach(float angle in new[]{-75,29.5f})
        {
            foreach(var t in fitted.SelectMany(f=>f.GetComponentsInChildren<Transform>()).Where(t=>t.name=="TurretPitch"))t.localRotation=Quaternion.Euler(angle,0,0);
            Render(new Vector3(-3.8f,4.3f,3.5f),new Vector3(-1,2.96f,1.18f),.48f,"front-turret-pitch-"+angle.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Object.DestroyImmediate(root);Object.DestroyImmediate(clay);
    }
    static void ReviewStations(GameObject root,List<Transform> front,List<string> lines)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))if(new[]{"REBIRTHCampRoofStations","REBIRTHCampPowerEquipment","REBIRTHModPlow"}.Contains(t.name))t.gameObject.SetActive(true);
        if(productionStage)foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="REBIRTHModArmor"||t.name=="REBIRTHModArmorDoor")t.gameObject.SetActive(true);
        var visual=root.transform.Find("GameObject/M/VisualBody");
        foreach(int side in new[]{-1,1})
        {
            var support=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+(side<0?"/RearLeftSupport.prefab":"/RearRightSupport.prefab")),visual);
            foreach(var r in support.GetComponentsInChildren<Renderer>())r.sharedMaterial=steel;
            foreach(var f in support.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Single circular support platform").ToArray())Object.DestroyImmediate(f.gameObject);
            var old=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Optimized robot turret "+(side<0?3:4));float scale=old.lossyScale.y;
            var turret=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/RustyRobotTurret.prefab"),support.transform);
            turret.name="Fitted actual rear "+(side<0?"left":"right")+" turret";
            turret.transform.position=new Vector3(side*1.152f,2.891f+.011f-.045f*scale-.001f,-3.405f);turret.transform.rotation=Quaternion.LookRotation(new Vector3(side,0,-1));
            var ps=support.transform.lossyScale;turret.transform.localScale=new Vector3(scale/ps.x,scale/ps.y,scale/ps.z);foreach(var lod in turret.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
            var previous=owner;owner=support.transform;RecessedSocket(new Vector3(side*1.152f,2.891f,-3.405f),turret.transform,lines);owner=previous;
        }
        ApplyUnifiedFinish(root,lines);
        var stationGroups=root.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"REBIRTHCampRoofStations","REBIRTHCampPowerEquipment"}.Contains(t.name)).ToArray();
        var stations=stationGroups.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>()).Where(f=>f.GetComponent<Renderer>()).ToArray();
        var members=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<Renderer>()&&!stationGroups.Any(t=>f.transform.IsChildOf(t))&&
            (f.transform.IsChildOf(owner)||f.name=="REBIRTHRearLadders"||f.name.IndexOf("rail",StringComparison.OrdinalIgnoreCase)>=0)&&!f.name.EndsWith("LOD0")&&!f.name.EndsWith("LOD1")&&!f.name.EndsWith("LOD2")).ToArray();
        var probes=new List<GameObject>();bool oldBack=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;int contacts=0;
        try
        {
            foreach(var station in stations)
            {
                var p=new GameObject("Station clearance probe");p.transform.SetPositionAndRotation(station.transform.position,station.transform.rotation);p.transform.localScale=station.transform.lossyScale;
                var c=p.AddComponent<MeshCollider>();c.sharedMesh=station.sharedMesh;probes.Add(p);Physics.SyncTransforms();
                lines.Add("Station "+station.name+" bounds="+station.GetComponent<Renderer>().bounds);
                foreach(var member in members)
                {
                    if(!station.GetComponent<Renderer>().bounds.Intersects(member.GetComponent<Renderer>().bounds))continue;
                    bool found=false;var vs=member.sharedMesh.vertices;var ts=member.sharedMesh.triangles;
                    for(int i=0;i<ts.Length&&!found;i+=3)
                    {
                        var pts=new[]{member.transform.TransformPoint(vs[ts[i]]),member.transform.TransformPoint(vs[ts[i+1]]),member.transform.TransformPoint(vs[ts[i+2]])};
                        for(int e=0;e<3&&!found;e++){var delta=pts[(e+1)%3]-pts[e];if(delta.sqrMagnitude<.00000001f)continue;if(c.Raycast(new Ray(pts[e],delta.normalized),out var h,delta.magnitude)){found=true;contacts++;lines.Add("STATION INTERSECTION station="+station.name+" member="+member.name+" point="+h.point.ToString("F5"));}}
                    }
                }
            }
        }
        finally{Physics.queriesHitBackfaces=oldBack;foreach(var p in probes)Object.DestroyImmediate(p);}
        lines.Add("Station/ladder/guardrail intersecting mesh pairs="+contacts+"; stations="+stations.Length+" tested members="+members.Length+". Edge/surface authoring check only; not gameplay navigation or a contained-volume proof.");
        lines.Add("Bracket material asset="+AssetDatabase.GetAssetPath(steel)+" albedo="+AssetDatabase.GetAssetPath(steel.mainTexture));
        foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"REBIRTHModPlow","REBIRTHModArmor"}.Contains(t.name)))
            foreach(var material in t.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct())lines.Add(t.name+" material="+AssetDatabase.GetAssetPath(material)+" albedo="+AssetDatabase.GetAssetPath(material.mainTexture));
        File.WriteAllLines(Output+"/stations-and-supports-review.txt",lines);
        if(productionStage)
        {
            if(contacts!=0)throw new Exception("Production staging refused: station/support intersections="+contacts);
            var fitted=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Fitted actual ")&&t.name.EndsWith(" turret")).ToArray();
            if(fitted.Length!=4)throw new Exception("Exactly four fitted turret rigs required");
            int auditHits=0;
            foreach(bool camp in new[]{true,false})
            {
                RebirthCamperCampIntegration.SetMode(root,camp);
                foreach(var turret in fitted)
                {
                    string corner=turret.name.Replace("Fitted actual ","").Replace(" turret","");
                    string mode=camp?"camp":"travel";
                    Debug.Log("[CamperTurretProduction] Dense sweep "+corner+" "+mode+" armor on");
                    var centre=turret.position;centre.y=2.90f;
                    AuditPipeZone(root,turret,turret,new Bounds(centre,new Vector3(1.4f,1.4f,1.4f)),turret,"production-"+corner.Replace(' ','-')+"-"+mode+"-clearance.txt",true);
                    auditHits+=LastAuditHits;
                }
            }
            RebirthCamperCampIntegration.SetMode(root,true);
            if(auditHits!=0)throw new Exception("Production staging refused: sampled turret intersections="+auditHits);
            RebirthCamperTurretProduction.Stage(root);
        }
        RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.23f,.23f,.23f);
        var key=new GameObject("Textured station review key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.3f;key.transform.rotation=Quaternion.Euler(45,135,0);
        Render(new Vector3(0,10,-1.2f),new Vector3(0,0,-1.2f),2.85f,"stations-all-four-turrets-overhead-textured");
        Render(new Vector3(-4.5f,6,4.5f),new Vector3(0,2.5f,-.9f),2.65f,"stations-front-quarter-textured");
        Render(new Vector3(-4.5f,6,-6),new Vector3(0,2.5f,-1.7f),2.65f,"stations-rear-quarter-textured");
        Render(new Vector3(-3.8f,4.3f,3.5f),new Vector3(-.8f,2.8f,1.05f),.65f,"front-brackets-armor-finish-textured");
        Render(new Vector3(-3.8f,4.3f,-5.5f),new Vector3(-.8f,2.8f,-3.3f),.65f,"rear-brackets-armor-finish-textured");
    }
    static void ApplyUnifiedFinish(GameObject root,List<string> lines)
    {
        var plough=AssetDatabase.LoadAssetAtPath<Material>("Assets/REBIRTHCamperApprovedArmor_20261018_v4/plough.mat");
        if(!plough)throw new Exception("Approved plough material missing");
        var stations=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("REBIRTHCamp")&&t.name!="REBIRTHCamperTurretSlot").ToArray();
        int count=0;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<Renderer>()&&!f.name.EndsWith("LOD0")&&!f.name.EndsWith("LOD1")&&!f.name.EndsWith("LOD2")&&
            !stations.Any(t=>f.transform.IsChildOf(t))&&
            (f.name.IndexOf("rail",StringComparison.OrdinalIgnoreCase)>=0||f.name.IndexOf("ladder",StringComparison.OrdinalIgnoreCase)>=0||f.name.IndexOf("handhold",StringComparison.OrdinalIgnoreCase)>=0||f.name.IndexOf("rung",StringComparison.OrdinalIgnoreCase)>=0)))
        {
            var source=f.sharedMesh;var old=source.vertices;var ns=source.normals;var ts=source.triangles;
            var vs=new Vector3[ts.Length];var normals=new Vector3[ts.Length];var uv=new Vector2[ts.Length];var ids=new int[ts.Length];
            for(int tri=0;tri<ts.Length;tri+=3)
            {
                var a=f.transform.TransformPoint(old[ts[tri]]);var b=f.transform.TransformPoint(old[ts[tri+1]]);var c=f.transform.TransformPoint(old[ts[tri+2]]);var n=Vector3.Cross(b-a,c-a).normalized;
                for(int k=0;k<3;k++){int i=tri+k,j=ts[i];var p=f.transform.TransformPoint(old[j]);vs[i]=old[j];normals[i]=ns[j];ids[i]=i;var q=Mathf.Abs(n.y)>.7f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>.7f?new Vector2(p.z,p.y):new Vector2(p.x,p.y);uv[i]=new Vector2(.065f+Mathf.Repeat(q.x*.12f,.05f),.245f+Mathf.Repeat(q.y*.12f,.05f));}
            }
            var mesh=new Mesh{name="Unified armor steel UV "+f.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=vs;mesh.normals=normals;mesh.uv=uv;mesh.triangles=ids;mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=Folder+"/UnifiedSteel"+(count++)+".asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);
            f.sharedMesh=mesh;f.GetComponent<Renderer>().sharedMaterial=plough;
        }
        var shader=Shader.Find("REBIRTH/Robot Weathered Steel");if(!shader)throw new Exception("Weathered turret finish shader missing");
        string matPath=RebirthRobotTurretImport.Folder+"/RobotWeathered.mat";
        var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(RebirthRobotTurretImport.Folder+"/Robot.mat")){name="Robot muted weathered armor tone",shader=shader};
        material.SetColor("_RustTint",new Color(.62f,.55f,.46f));material.SetFloat("_RustDesaturation",.8f);material.SetFloat("_GlossMapScale",.7f);
        var saved=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(saved){EditorUtility.CopySerialized(material,saved);Object.DestroyImmediate(material);material=saved;}else AssetDatabase.CreateAsset(material,matPath);
        foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.EndsWith("LOD0")||r.name.EndsWith("LOD1")||r.name.EndsWith("LOD2")))
            if(r.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name.StartsWith("Fitted actual")))r.sharedMaterials=r.sharedMaterials.Select(m=>m&&m.name=="Robot source finish"?material:m).ToArray();
        AssetDatabase.SaveAssets();lines.Add("Exact approved plough material/UV applied to "+count+" ladder/guardrail meshes: "+AssetDatabase.GetAssetPath(plough)+" albedo="+AssetDatabase.GetAssetPath(plough.mainTexture)+". Brackets retain armor plates material. Orange turret paint graded in material shader, preserving source texture dimensions/normal/metal maps and red eye.");
    }
    public static void Run()
    {
        meshPrefix="Part";
        index=0;report.Clear();Directory.CreateDirectory(Folder);AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/rebirthCamper80s.prefab"));
        RebirthCamperCampIntegration.SetMode(root,true);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            if(t.name.StartsWith("REBIRTHCamperTurretSlot")||new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment"}.Contains(t.name))t.gameObject.SetActive(false);
            if(new[]{"REBIRTHModArmor","REBIRTHModArmorDoor","REBIRTHModPlow"}.Contains(t.name))t.gameObject.SetActive(true);
        }
        steel=AssetDatabase.LoadAssetAtPath<Material>("Assets/REBIRTHCamperApprovedArmor_20261018_v4/plates.mat");if(!steel)throw new Exception("Approved armor finish missing");
        owner=new GameObject("Rear left corner support placement candidate").transform;owner.SetParent(root.transform.Find("GameObject/M/VisualBody"),false);
        // Move the platform 40mm farther out along both horizontal axes after the
        // actual cradle/pipe triangle audit found contact at several yaw angles.
        var platform=new Vector3(-1.152f,2.891f,-3.405f);Disc("Single circular support platform",platform,.111224f,.022f);
        // The actual ladder's nearest rail is x=-.94 and follows this shallow rear-panel lean.
        Func<float,float> railZ=y=>Mathf.Lerp(-3.46f,-3.34f,Mathf.InverseLerp(1.60f,2.86f,y));
        var rearRailAxis=new Vector3(0,1,.12f/1.26f).normalized;
        foreach(float y in new[]{2.68f,2.82f})Collar(new Vector3(-.94f,y,railZ(y)),.023f,.044f,rearRailAxis);
        foreach(float offset in new[]{-.016f,.016f})
        {
            var b0=new Vector3(-.969f,2.82f,railZ(2.82f)+offset);var b1=new Vector3(-1.075f,2.873f,-3.442f+offset);var b2=new Vector3(-1.14f,2.873f,-3.452f+offset);
            Beam("Ladder-side welded upper arm",b0,b1,.018f,.014f);Beam("Welded platform return",b1,b2,.018f,.014f);
            Beam("Open ladder-side diagonal brace",new Vector3(-.969f,2.68f,railZ(2.68f)+offset),b2,.018f,.014f);
        }
        Beam("Welded crossmember beneath disk",new Vector3(-1.14f,2.871f,-3.48f),new Vector3(-1.14f,2.871f,-3.34f),.026f,.018f);
        Box("Saddle connecting spine",new Vector3(-.972f,2.75f,railZ(2.75f)),new Vector3(.012f,.19f,.052f),Quaternion.FromToRotation(Vector3.up,rearRailAxis));
        // Small attachment hardware, confined to foot/saddle; support joints are welded.
        foreach(float y in new[]{2.68f,2.82f})foreach(float z in new[]{-.034f,.034f})
        {
            Box("B saddle bolt lug",new Vector3(-.958f,y,railZ(y)+z),new Vector3(.025f,.025f,.015f),Quaternion.identity);
            Fastener("B saddle washer",new Vector3(-.973f,y,railZ(y)+z),.008f,.002f,20);
            Fastener("B saddle hex fastener",new Vector3(-.977f,y,railZ(y)+z),.006f,.006f,6);
        }
        PrefabUtility.SaveAsPrefabAsset(owner.gameObject,Folder+"/RearLeftSupport.prefab");AssetDatabase.SaveAssets();
        var mirrored=MirrorRight(owner);PrefabUtility.SaveAsPrefabAsset(mirrored.gameObject,Folder+"/RearRightSupport.prefab");AssetDatabase.SaveAssets();
        if(showTurrets)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(RebirthRobotTurretImport.Folder+"/RustyRobotTurret.prefab");
            if(!source)throw new Exception("Actual optimized turret prefab missing");
            foreach(int side in new[]{-1,1})
            {
                var old=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Optimized robot turret "+(side<0?3:4));
                float scale=old.lossyScale.y;var parent=side<0?owner:mirrored;
                var turret=Object.Instantiate(source,parent);turret.name=side<0?"Fitted actual rear left turret":"Fitted actual rear right turret";
                turret.transform.position=new Vector3(side*Mathf.Abs(platform.x),platform.y+.011f-.045f*scale-.001f,platform.z);
                turret.transform.rotation=Quaternion.LookRotation(new Vector3(side,0,-1));
                var ps=parent.lossyScale;turret.transform.localScale=new Vector3(scale/ps.x,scale/ps.y,scale/ps.z);
                foreach(var lod in turret.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
            }
            AuditPipeZone(root,owner,mirrored);
        }
        report.Add("Single rear-left candidate on actual camper. Latest owner correction: ladder left rail ONLY; no camper-side foot or bracket. Two split saddles and twin welded cantilever braces follow actual authored left ladder rail x=-0.94, radius0.023 and LadderZ(y). Platform center="+platform.ToString("F4")+", diameter0.222448. No turret geometry or transforms changed. Production pipe/ladder clearance audit and other corners remain pending.");
        File.WriteAllLines(Output+"/corner-support-placement.txt",report);
        File.AppendAllText(Output+"/corner-support-placement.txt","\nRight counterpart reflected across actual camper centreline with corrected winding/normals; attaches to outer right ladder rail x=+0.94. Both candidates preserve source camper and turret geometry.\n");
        var clay=new Material(Shader.Find("Standard")){color=new Color(.64f,.66f,.68f)};clay.SetFloat("_Glossiness",.24f);
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(_=>clay).ToArray();
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);
        var light=new GameObject("Support key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(40,-30,0);
        Render(new Vector3(-3,4.6f,-5.5f),new Vector3(-1,2.77f,-3.32f),.48f,"actual-support-quarter");
        Render(new Vector3(-1.04f,9,-3.31f),new Vector3(-1.04f,0,-3.31f),.43f,"actual-support-overhead");
        Render(new Vector3(3,4.6f,-5.5f),new Vector3(1,2.77f,-3.32f),.48f,"actual-support-right-quarter");
        Render(new Vector3(0,3.6f,-7),new Vector3(0,2.75f,-3.30f),1.44f,"actual-support-both-rear");
        Render(new Vector3(0,9,-3.25f),new Vector3(0,0,-3.25f),1.44f,"actual-support-both-overhead");
        if(showTurrets)
        {
            var pitches=new[]{owner,mirrored}.SelectMany(t=>t.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="TurretPitch").ToArray();
            foreach(var pitch in pitches)pitch.localRotation=Quaternion.Euler(-75,0,0);
            Render(new Vector3(-3,4.6f,-5.5f),new Vector3(-1,2.98f,-3.32f),.48f,"up75-fit");
            foreach(var pitch in pitches)pitch.localRotation=Quaternion.Euler(29.5f,0,0);
            Render(new Vector3(-3,4.6f,-5.5f),new Vector3(-1,2.98f,-3.32f),.48f,"down29-fit");
            foreach(var pitch in pitches)pitch.localRotation=Quaternion.identity;
            var yaws=new[]{owner,mirrored}.SelectMany(t=>t.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="TurretYaw").ToArray();
            YawSheet(yaws);
        }
        Object.DestroyImmediate(root);Object.DestroyImmediate(clay);
    }
    static void YawSheet(Transform[] yaws)
    {
        const int tile=700;var sheet=new Texture2D(tile*3,tile*2,TextureFormat.RGB24,false);
        var camera=new GameObject("Actual rig yaw review").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.43f;camera.nearClipPlane=.01f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.07f,.09f);
        var rt=new RenderTexture(tile,tile,24){antiAliasing=4};camera.targetTexture=rt;
        float[] angles={-90,0,90};
        for(int column=0;column<3;column++)
        {
            foreach(var yaw in yaws)yaw.localRotation=Quaternion.Euler(0,angles[column],0);
            for(int row=0;row<2;row++)
            {
                camera.transform.position=row==0?new Vector3(-3,4.6f,-5.5f):new Vector3(-1.1f,9,-3.34f);
                camera.transform.rotation=row==0?Quaternion.LookRotation(new Vector3(-1.1f,2.98f,-3.34f)-camera.transform.position):Quaternion.Euler(90,0,0);
                camera.Render();RenderTexture.active=rt;sheet.ReadPixels(new Rect(0,0,tile,tile),column*tile,(1-row)*tile);
            }
        }
        sheet.Apply();File.WriteAllBytes(Output+"/actual-turret-yaw-limits.png",sheet.EncodeToPNG());
        File.WriteAllText(Output+"/actual-turret-yaw-limits.txt","Actual prefab TurretYaw transforms, left-to-right columns -90 / 0 / +90 degrees relative to outward diagonal rest. Top perspective, bottom true overhead. FixedSupport mesh remains outside TurretYaw. This is a direct rig-pose render, not an in-game movement/collision test.");
        RenderTexture.active=null;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(sheet);
        foreach(var yaw in yaws)yaw.localRotation=Quaternion.identity;
    }
    static void AuditPipeZone(GameObject root,Transform left,Transform right,Bounds? customZone=null,Transform fittedTurret=null,string reportName="actual-pipe-zone-audit.txt",bool dense=false)
    {
        var zone=customZone??new Bounds(new Vector3(-1,2.88f,-3.23f),new Vector3(.15f,.35f,.16f));
        var vertices=new List<Vector3>();var indices=new List<int>();var sources=new List<string>();
        foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject.activeInHierarchy&&!f.transform.IsChildOf(left)&&!f.transform.IsChildOf(right)))
        {
            if(!f.GetComponent<Renderer>().bounds.Intersects(zone))continue;
            var vs=f.sharedMesh.vertices;var ts=f.sharedMesh.triangles;
            for(int i=0;i<ts.Length;i+=3)
            {
                var a=f.transform.TransformPoint(vs[ts[i]]);var b=f.transform.TransformPoint(vs[ts[i+1]]);var c=f.transform.TransformPoint(vs[ts[i+2]]);
                var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);if(!bounds.Intersects(zone))continue;
                int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(start);indices.Add(start+1);indices.Add(start+2);sources.Add(f.name);
            }
        }
        var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        var probe=new GameObject("Actual corner pipe-zone triangle probe");var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
        var turret=fittedTurret??left.GetComponentsInChildren<Transform>().Single(t=>t.name=="Fitted actual rear left turret");
        var yaw=turret.GetComponentsInChildren<Transform>().Single(t=>t.name=="TurretYaw");var pitch=turret.GetComponentsInChildren<Transform>().Single(t=>t.name=="TurretPitch");
        var lines=new List<string>{"Direct triangle edge intersection probe against actual source camper geometry in the selected corner zone. Sampled yaw every5 degrees at level; pitch extrema at centre and both yaw limits. This is an authoring geometry test, not in-game collision proof. Source triangle count="+sources.Count};
        bool oldBackfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;int hits=0;
        try
        {
            var poses=new List<Vector2>();for(int y=-90;y<=90;y+=5)poses.Add(new Vector2(y,0));
            foreach(float y in new[]{-90,0,90})foreach(float p in new[]{-75,29.5f})poses.Add(new Vector2(y,p));
            if(dense){poses.Clear();for(int y=-90;y<=90;y+=5){for(int p=-75;p<=25;p+=10)poses.Add(new Vector2(y,p));poses.Add(new Vector2(y,29.5f));}lines.Add("Expanded grid: yaw every5 degrees; pitch -75..25 every10 degrees plus29.5. Includes source body, ladder, support and armor triangles in crop. Poses="+poses.Count);}
            foreach(var pose in poses)
            {
                yaw.localRotation=Quaternion.Euler(0,pose.x,0);pitch.localRotation=Quaternion.Euler(pose.y,0,0);
                foreach(var f in turret.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.EndsWith("LOD0")))
                {
                    var vs=f.sharedMesh.vertices;var ts=f.sharedMesh.triangles;bool found=false;
                    for(int i=0;i<ts.Length&&!found;i+=3)
                    {
                        var points=new[]{f.transform.TransformPoint(vs[ts[i]]),f.transform.TransformPoint(vs[ts[i+1]]),f.transform.TransformPoint(vs[ts[i+2]])};
                        var bounds=new Bounds(points[0],Vector3.zero);bounds.Encapsulate(points[1]);bounds.Encapsulate(points[2]);if(!bounds.Intersects(zone))continue;
                        for(int edge=0;edge<3&&!found;edge++)
                        {
                            var start=points[edge];var delta=points[(edge+1)%3]-start;float distance=delta.magnitude;if(distance<.000001f)continue;
                            if(collider.Raycast(new Ray(start,delta/distance),out var hit,distance+.00001f))
                            {hits++;lines.Add("INTERSECTION yaw="+pose.x+" pitch="+pose.y+" turret="+f.name+" camper="+sources[hit.triangleIndex]+" point="+hit.point.ToString("F5"));found=true;}
                        }
                    }
                }
            }
            LastAuditHits=hits;
            lines.Add("Intersecting mesh/pose cases="+hits+". No source camper or turret mesh changed.");
            File.WriteAllLines(Output+"/"+reportName,lines);
        }
        finally{Physics.queriesHitBackfaces=oldBackfaces;yaw.localRotation=Quaternion.identity;pitch.localRotation=Quaternion.identity;Object.DestroyImmediate(probe);Object.DestroyImmediate(mesh);}
    }
    static void Render(Vector3 eye,Vector3 target,float size,string name)
    {
        if(showTurrets)name="fitted-turrets-"+name;
        var c=new GameObject(name).AddComponent<Camera>();c.orthographic=true;c.orthographicSize=size;c.nearClipPlane=.05f;c.farClipPlane=20;c.transform.position=eye;
        c.transform.rotation=Mathf.Abs(eye.x-target.x)<.0001f&&Mathf.Abs(eye.z-target.z)<.0001f?Quaternion.Euler(90,0,0):Quaternion.LookRotation(target-eye);
        c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.07f,.07f,.09f);
        var fill=new GameObject("Review camera fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.55f;fill.transform.rotation=c.transform.rotation;
        var rt=new RenderTexture(1400,1400,24){antiAliasing=4};c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(1400,1400,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1400,1400),0,0);img.Apply();File.WriteAllBytes(Output+"/"+name+".png",img.EncodeToPNG());RenderTexture.active=null;c.targetTexture=null;Object.DestroyImmediate(img);Object.DestroyImmediate(rt);Object.DestroyImmediate(fill.gameObject);Object.DestroyImmediate(c.gameObject);
    }
}
