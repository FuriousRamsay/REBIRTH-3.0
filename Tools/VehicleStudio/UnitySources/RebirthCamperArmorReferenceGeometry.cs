using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

public static partial class RebirthCamperArmorReference
{
    static int profileIndex;
    static readonly List<MeshCollider> fittingSurfaces=new List<MeshCollider>();
    static bool Surface(Ray ray,out RaycastHit selected,bool hood=false)
    {
        selected=default(RaycastHit);float nearest=10;bool found=false;
        foreach(var surface in fittingSurfaces)
            if((!hood||surface.transform.parent.name=="body")&&surface.Raycast(ray,out var hit,nearest)){selected=hit;nearest=hit.distance;found=true;}
        return found;
    }
    static void PrepareClay()
    {
        iron=Clay("Clay steel");paint=Clay("Clay plate");fastener=Clay("Clay hardware");
    }
    static Material Clay(string name)
    {
        var m=new Material(Shader.Find("Standard"));m.name=name;m.color=new Color(.39f,.39f,.39f);m.SetFloat("_Metallic",0);m.SetFloat("_Glossiness",.18f);
        AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
    }
    static Vector2[] RectProfile(float w,float h,int variant=0)
    {
        float a=w*.5f,b=h*.5f,c=Mathf.Min(w,h)*.10f;
        return new[]{new Vector2(-a+c,-b),new Vector2(a-c,-b+.015f*(variant%2)),new Vector2(a,-b+c),new Vector2(a-.025f*(variant%3),b-c),new Vector2(a-c,b),new Vector2(-a+c,b-.02f*(variant%2)),new Vector2(-a,b-c),new Vector2(-a,-b+c)};
    }
    static Mesh ProfileMesh(Vector2[] outline,float thickness)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();int n=outline.Length;
        Vector2 min=outline[0],max=outline[0];foreach(var p in outline){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
        float offset=Noise(profileIndex++,17)*.70f;
        for(int ring=0;ring<4;ring++)foreach(var p in outline)
        {
            var inset=p*(ring==0||ring==3?.975f:1);vertices.Add(new Vector3(inset.x,inset.y,ring==0?-thickness*.5f:ring==1?-thickness*.25f:ring==2?thickness*.25f:thickness*.5f));
            uv.Add(new Vector2((p.x-min.x)/(max.x-min.x)*.72f+offset,(p.y-min.y)/(max.y-min.y)*.72f+offset*.41f));
        }
        for(int r=0;r<3;r++)for(int i=0;i<n;i++){int a=r*n+i,b=r*n+(i+1)%n;triangles.AddRange(new[]{a,b,a+n,b,b+n,a+n});}
        for(int i=1;i<n-1;i++)triangles.AddRange(new[]{0,i+1,i,3*n,3*n+i,3*n+i+1});
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
    }
    static void Profile(Transform parent,string name,Vector3 center,Quaternion rotation,Vector2[] outline,bool yellow=false,float thickness=.022f)
    {
        Part(parent,name,ProfileMesh(outline,thickness),yellow?paint:iron,center,rotation,Vector3.one);
        // Visible perimeter fasteners follow each actual plate contour.
        var centroid=Vector2.zero;foreach(var p in outline)centroid+=p;centroid/=outline.Length;
        foreach(var p in outline.Where((p,i)=>i%2==0))
        {
            // Inset towards the plate itself, not towards the wheel-hub origin
            // used by arch segments; otherwise fasteners land inside the arch.
            var inset=centroid+(p-centroid)*.78f;
            Bolt(parent,center+rotation*new Vector3(inset.x,inset.y,thickness*.5f+.003f),rotation);
        }
        panels++;
    }
    static void SidePlate(Transform armor,int side,float z,float y,float width,float height,bool yellow,int variant=0)
    {
        float x=z<-.45f?.96f:.876f;
        var center=new Vector3(x*side,y,z);var normal=Vector3.right*side;
        if(Surface(new Ray(new Vector3(side*2,y,z),-normal),out var hit)&&hit.normal.x*side>.8f){center=hit.point+hit.normal*.025f;normal=hit.normal;}
        var rotation=Quaternion.LookRotation(normal,Vector3.up);
        Profile(armor,"Reference fitted quarter plate",center,rotation,RectProfile(width,height,variant),yellow);
    }
    static void ReferenceArmor(GameObject root,Transform armor)
    {
        fittingSurfaces.Clear();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="body"||f.name=="camper_body").ToArray())
        {var go=Group(filter.transform,"ReferenceTemporarySurface");var c=go.AddComponent<MeshCollider>();c.sharedMesh=filter.sharedMesh;fittingSurfaces.Add(c);}
        Physics.SyncTransforms();
        // Deliberately no continuous replacement skin: paint remains visible
        // between distinct plate groups and around the door/window boundaries.
        foreach(int side in new[]{-1,1})
        {
            SidePlate(armor,side,.13f,.91f,.78f,.43f,true,1);
            SidePlate(armor,side,-.67f,.91f,.55f,.42f,false,2);
            SidePlate(armor,side,-2.46f,.86f,.63f,.46f,true,3);
            SidePlate(armor,side,-3.02f,.84f,.34f,.46f,false,1);
            SidePlate(armor,side,-1.56f,1.155f,1.00f,.16f,false,2);
            SidePlate(armor,side,-2.94f,1.66f,.43f,.41f,true,3);
            // Narrow hinge/hatch reinforcement rather than a row of top-band tiles.
            SidePlate(armor,side,-.48f,1.72f,.20f,.46f,false);
            foreach(float z in new[]{-.81f,-2.82f})SidePlate(armor,side,z,1.36f,.20f,.12f,false);
            foreach(float hub in new[]{1.289f,-1.620f})
            {
                for(int segment=0;segment<5;segment++)
                {
                    float a=(12+segment*31.2f)*Mathf.Deg2Rad,b=(12+(segment+1)*31.2f-1.6f)*Mathf.Deg2Rad;
                    // Model-local Z is the horizontal axis along each arch.
                    var rotation=Quaternion.LookRotation(Vector3.right*side,Vector3.up);
                    Func<float,float,Vector2> point=(r,t)=>new Vector2(-side*Mathf.Cos(t)*r,Mathf.Sin(t)*r);
                    var shape=new[]{point(.432f,a),point(.626f,a),point(.626f,b),point(.432f,b)};
                    // Reverse one side so front-face winding remains outward.
                    if(side>0)Array.Reverse(shape);
                    Profile(armor,"Segmented wheel arch armor",new Vector3(side*.94f,.375f,hub),rotation,shape,segment%3==1,.028f);
                }
            }
            // Chassis/camper protection rails follow the exposed band, leaving
            // broad original paint regions and the original pattern uncovered.
            Tube(armor,new Vector3(side*1.005f,1.40f,-3.12f),new Vector3(side*1.005f,1.40f,-.50f),.027f);
            Tube(armor,new Vector3(side*.99f,2.94f,-3.13f),new Vector3(side*.99f,2.94f,.71f),.026f);
            Tube(armor,new Vector3(side*.99f,1.44f,-3.18f),new Vector3(side*.99f,2.94f,-3.18f),.030f);
        }
        // Hood: three larger tapered, overlapping pieces, not six small pads.
        for(int i=0;i<3;i++)
        {
            float x=(i-1)*.49f;
            var outline=new[]{new Vector2(-.225f,-.40f),new Vector2(.225f,-.40f),new Vector2(.238f,.32f),new Vector2(.16f,.41f),new Vector2(-.21f,.38f)};
            FittedHood(armor,x,1.30f,outline,i!=1);
        }
        foreach(float x in new[]{-.63f,0,.63f})Profile(armor,"Bumper armor",new Vector3(x,.62f,2.07f),Quaternion.identity,RectProfile(.59f,.28f,(int)(x*10)),x!=0,.035f);
        // Crossed straps add relief and visible plate attachment on the hood.
        foreach(int side in new[]{-1,1})Tube(armor,new Vector3(side*.7f,1.15f,1.66f),new Vector3(side*.22f,1.15f,.97f),.014f);
        foreach(var surface in fittingSurfaces)Object.DestroyImmediate(surface.gameObject);fittingSurfaces.Clear();
    }
    static void FittedHood(Transform parent,float x,float z,Vector2[] outline,bool yellow)
    {
        var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        foreach(var p in outline)
        {
            if(!Surface(new Ray(new Vector3(x+p.x,2,z+p.y),Vector3.down),out var hit,true)||hit.normal.y<.15f)throw new Exception("Hood plate lacks measured support at "+(x+p.x)+","+(z+p.y));
            positions.Add(hit.point+hit.normal*.026f);normals.Add(hit.normal);uv.Add(new Vector2((p.x+.25f)*1.6f,(p.y+.42f)*.9f));
        }
        int count=positions.Count;
        for(int i=0;i<count;i++){positions.Add(positions[i]-normals[i]*.026f);uv.Add(uv[i]);}
        for(int i=1;i<count-1;i++){triangles.AddRange(new[]{0,i+1,i,count,count+i,count+i+1});}
        for(int i=0;i<count;i++){int b=(i+1)%count;triangles.AddRange(new[]{i,b,i+count,b,b+count,i+count});}
        var mesh=new Mesh();mesh.SetVertices(positions);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();
        Part(parent,"Tapered surface-fitted hood armor",mesh,yellow?paint:iron,Vector3.zero,Quaternion.identity,Vector3.one);
        foreach(var p in outline)
        {
            if(Surface(new Ray(new Vector3(x+p.x*.84f,2,z+p.y*.84f),Vector3.down),out var hit,true))Bolt(parent,hit.point+hit.normal*.044f,Quaternion.LookRotation(hit.normal));
        }
    }
    static void ReferenceDoor(Transform door)
    {
        Profile(door,"Lower rear door armor",new Vector3(.0f,1.04f,-3.40f),Quaternion.LookRotation(Vector3.back),RectProfile(.66f,.52f,1),true);
        Profile(door,"Rear door reinforcement",new Vector3(.0f,1.56f,-3.40f),Quaternion.LookRotation(Vector3.back),RectProfile(.66f,.25f,3),false);
    }
    static float BladeZ(float t)=>2.43f+.31f*Mathf.Pow(1-t,2)+.09f*Mathf.Pow(t,5);
    static Mesh CollaredSpike(float length)
    {
        var stations=new[]{new Vector2(0,.067f),new Vector2(.055f,.067f),new Vector2(.075f,.054f),new Vector2(.26f,.044f),new Vector2(.275f,.050f),new Vector2(.305f,.039f),new Vector2(.49f,.022f),new Vector2(.51f,.026f),new Vector2(.54f,.017f),new Vector2(length,.001f)};
        const int sides=12;var vertices=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
        for(int r=0;r<stations.Length;r++)for(int i=0;i<=sides;i++)
        {float a=i*2*Mathf.PI/sides;vertices.Add(new Vector3(Mathf.Cos(a)*stations[r].y,Mathf.Sin(a)*stations[r].y,stations[r].x));uv.Add(new Vector2((float)i/sides,stations[r].x*2));}
        for(int r=0;r<stations.Length-1;r++)for(int i=0;i<sides;i++){int a=r*(sides+1)+i,b=a+1,c=a+sides+1;tr.AddRange(new[]{a,b,c,b,c+1,c});}
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tr,0);mesh.RecalculateNormals();mesh.RecalculateTangents();return mesh;
    }
    static void ReferencePlow(Transform target)
    {
        // Sheet thickness, panel seams and an asymmetric pushing profile.
        int nx=20,ny=24;float width=2.30f;var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
        for(int face=0;face<2;face++)for(int x=0;x<=nx;x++)for(int y=0;y<=ny;y++)
        {
            float u=(float)x/nx,t=(float)y/ny;
            float dent=Mathf.Sin(u*29+t*11)*Mathf.Sin(t*Mathf.PI)*.006f;
            v.Add(new Vector3((u-.5f)*width,.35f+t*.96f,BladeZ(t)+dent-face*.028f));uv.Add(new Vector2(u,t));
        }
        int count=(nx+1)*(ny+1);
        for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)
        {int a=x*(ny+1)+y,b=a+1,c=a+ny+1,d=c+1;tr.AddRange(new[]{a,c,b,b,c,d,a+count,b+count,c+count,b+count,d+count,c+count});}
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tr,0);mesh.RecalculateNormals();mesh.RecalculateTangents();
        Part(target,"Battered curved overlapping moldboard",mesh,paint,Vector3.zero,Quaternion.identity,Vector3.one);
        foreach(float x in new[]{-1.13f,-.57f,0,.57f,1.13f})
        {
            for(int i=0;i<ny;i++)
            {float a=(float)i/ny,b=(float)(i+1)/ny;Tube(target,new Vector3(x,.35f+a*.96f,BladeZ(a)+.017f),new Vector3(x,.35f+b*.96f,BladeZ(b)+.017f),.018f);}
            foreach(float t in new[]{.08f,.29f,.51f,.73f,.94f})Bolt(target,new Vector3(x,.35f+t*.96f,BladeZ(t)+.032f),Quaternion.identity);
        }
        Tube(target,new Vector3(-1.16f,1.31f,BladeZ(1)),new Vector3(1.16f,1.31f,BladeZ(1)),.022f);
        // Eight long, slender stepped spikes in two staggered rows.
        for(int row=0;row<2;row++)for(int i=0;i<4;i++)
        {
            float x=-.94f+i*.55f+row*.14f,t=row==0?.34f:.68f;Vector3 mount=new Vector3(x,.35f+t*.96f,BladeZ(t)+.018f);
            var flange=RectProfile(.15f,.19f,row);
            Profile(target,"Bolted spike mounting flange",mount,Quaternion.identity,flange,false,.025f);
            Part(target,"Stepped long forward spike",CollaredSpike(row==0?.72f:.66f),iron,mount+Vector3.forward*.02f,Quaternion.identity,Vector3.one);
        }
        for(int i=0;i<12;i++)
        {
            float x=-1.055f+i*.192f;
            var tooth=new[]{new Vector2(-.088f,.045f),new Vector2(.088f,.045f),new Vector2(.054f,-.115f),new Vector2(-.018f,-.175f)};
            Profile(target,"Pointed replaceable cutting tooth",new Vector3(x,.35f,BladeZ(0)+.012f),Quaternion.Euler(-19,0,0),tooth,false,.026f);
        }
        foreach(float x in new[]{-.60f,.60f})
        {
            Tube(target,new Vector3(x,.47f,1.8f),new Vector3(x,.48f,2.63f),.05f);
            Tube(target,new Vector3(x,.50f,1.98f),new Vector3(x,1.16f,2.43f),.027f);
            Profile(target,"Chassis mounting bracket",new Vector3(x,.55f,2.01f),Quaternion.identity,RectProfile(.20f,.31f),false,.035f);
        }
    }
}
