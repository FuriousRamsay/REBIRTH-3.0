using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// Exact current mesh surfaces only. No physics scene, colliders or body proxies.
public static class RebirthCamperShinCollisionReview
{
    [Serializable] public class Contact
    {
        public int seat,skinTriangle,interiorTriangle;public string skinPath,skinMesh,skinBones,interiorPath,staticMesh,staticSurfaceClass;
        public bool coplanar;public Vector3 skinA,skinB,skinC,interiorA,interiorB,interiorC;
    }
    [Serializable] public class SkinEvidence
    {
        public int seat,vertices,totalTriangles,selectedShinTriangles,selectedSurfaceTriangles,staticBroadphaseCandidates,triangleTests,intersections;
        public string path,mesh;public bool allNativeBoneWeightsRead;
    }
    [Serializable] public class Report
    {
        public string status="FAIL",error;
        public string scope="Actual baked lower-leg skin triangles versus actual static camper_interior triangles in model coordinates. Contacts include boundary touch within 0.00001m; no proxy or volume-containment claim.";
        public int interiorTriangles,bodyShellTriangles,staticSurfaceTriangles,totalTriangleTests,totalIntersections,storedContacts,algorithmSelfTests;
        public bool fullBodyCabin;
        public string selectionMode="weightedShins";
        public int[] reviewedSeats;
        public bool surfaceIntersectionDetected;
        public SkinEvidence[] skins;public Contact[] contacts;
        public string duplicationPolicy="Each original renderer/mesh triangle is retained; overlapping LOD meshes can produce duplicate geometric contacts, so counts are not unique penetration counts.";
    }
    struct Triangle
    {
        internal int index;internal string path,mesh,surfaceClass;internal Vector3 a,b,c;internal Bounds bounds;
        internal Triangle(int n,string owner,Vector3 x,Vector3 y,Vector3 z,string meshName=null,string classification=null){index=n;path=owner;mesh=meshName;surfaceClass=classification;a=x;b=y;c=z;bounds=new Bounds(x,Vector3.zero);bounds.Encapsulate(y);bounds.Encapsulate(z);bounds.Expand(.00002f);}
    }
    const float Epsilon=.00001f;
    public static void Review(Transform model,string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);var report=new Report();var rows=new List<SkinEvidence>();var contacts=new List<Contact>();
        try
        {
            if(!Application.isBatchMode || Application.isPlaying || model==null)throw new Exception("Non-playing dedicated batch model required.");
            var arguments=Environment.GetCommandLineArgs();int fullArgument=Array.IndexOf(arguments,"-rebirthAvatarFullCabinCollision");
            report.fullBodyCabin=fullArgument>=0&&fullArgument+1<arguments.Length&&arguments[fullArgument+1]=="1";
            report.reviewedSeats=new[]{0,1,2,3,4,5};
            if(report.fullBodyCabin){report.selectionMode="fullBodyCabin";report.scope="All actual baked skin/head triangles for slots0..5 versus actual enabled static rendered camper surfaces including current stations in model coordinates. Cushion/floor touches may be legitimate; tolerance0.00001m, no proxy, volume-containment or comfort claim.";}
            report.algorithmSelfTests=SelfTests();
            var actors=model.GetComponentsInChildren<Animator>(true).Where(a=>a.name.StartsWith("ActualNativeOccupant_",StringComparison.Ordinal)).Select(a=>a.gameObject).Distinct().ToArray();
            if(actors.Length!=6)throw new Exception("Six real native body instances are required for collision review.");
            var interior=new List<Triangle>();
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="camper_interior"||(report.fullBodyCabin&&f.GetComponent<Renderer>()!=null&&f.GetComponent<Renderer>().enabled&&f.gameObject.activeInHierarchy)))
            {
                var mesh=filter.sharedMesh;if(mesh==null)continue;var vertices=mesh.vertices;var indices=mesh.triangles;
                for(int n=0;n<indices.Length;n+=3){var a=Point(model,filter.transform,vertices[indices[n]]);var b=Point(model,filter.transform,vertices[indices[n+1]]);var c=Point(model,filter.transform,vertices[indices[n+2]]);if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-14f)continue;interior.Add(new Triangle(n/3,Path(model,filter.transform),a,b,c,mesh.name,filter.name=="camper_interior"?"interiorFurniture":filter.name=="camper_body"?"bodyShell":"station"));}
            }
            report.interiorTriangles=interior.Count(t=>t.surfaceClass=="interiorFurniture");report.bodyShellTriangles=interior.Count(t=>t.surfaceClass=="bodyShell");report.staticSurfaceTriangles=interior.Count;
            if(report.interiorTriangles==0 || (report.fullBodyCabin&&report.bodyShellTriangles==0))throw new Exception("Requested actual static interior/body shell triangles unavailable.");
            foreach(var actor in actors)
            {
                int seat=int.Parse(actor.name.Substring("ActualNativeOccupant_".Length));
                if(!report.reviewedSeats.Contains(seat))continue;
                var transforms=actor.GetComponentsInChildren<Transform>(true);Func<string,Transform> bone=name=>{var matches=transforms.Where(t=>t.name==name).ToArray();if(matches.Length!=1)throw new Exception("Required native shin bone absent/ambiguous: "+name);return matches[0];};
                var leftLeg=bone("LeftLeg");var rightLeg=bone("RightLeg");var leftFoot=bone("LeftFoot");var rightFoot=bone("RightFoot");
                var leftKnee=Point(model,leftLeg,Vector3.zero);var leftAnkle=Point(model,leftFoot,Vector3.zero);var rightKnee=Point(model,rightLeg,Vector3.zero);var rightAnkle=Point(model,rightFoot,Vector3.zero);
                foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var source=renderer.sharedMesh;if(source==null)continue;var row=new SkinEvidence{seat=seat,path=Path(model,renderer.transform),mesh=source.name,totalTriangles=source.triangles.Length/3};rows.Add(row);
                    var weights=report.fullBodyCabin?null:LowerLegWeights(source,renderer.bones,leftLeg,rightLeg,leftFoot,rightFoot);row.allNativeBoneWeightsRead=!report.fullBodyCabin;
                    var baked=new Mesh();
                    try
                    {
                        renderer.BakeMesh(baked);var vertices=baked.vertices;var indices=baked.triangles;row.vertices=vertices.Length;
                        if(weights!=null&&weights.Length!=vertices.Length)throw new Exception("Baked/native weight vertex mapping differs.");
                        var selected=new List<Triangle>();
                        for(int n=0;n<indices.Length;n+=3)
                        {
                            int ia=indices[n],ib=indices[n+1],ic=indices[n+2];if(!report.fullBodyCabin&&Mathf.Max(weights[ia],Mathf.Max(weights[ib],weights[ic]))<.20f)continue;
                            var a=Point(model,renderer.transform,vertices[ia]);var b=Point(model,renderer.transform,vertices[ib]);var c=Point(model,renderer.transform,vertices[ic]);var center=(a+b+c)/3;
                            // Real weights identify the limb, and its real ankle/knee
                            // segment limits the report to shins, not hips or toe skin.
                            if(!report.fullBodyCabin&&!WithinShinBand(center,leftAnkle,leftKnee)&&!WithinShinBand(center,rightAnkle,rightKnee))continue;
                            if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-14f)continue;selected.Add(new Triangle(n/3,row.path,a,b,c));
                        }
                        row.selectedSurfaceTriangles=selected.Count;row.selectedShinTriangles=report.fullBodyCabin?0:selected.Count;if(selected.Count==0)continue;
                        var selectionBounds=selected[0].bounds;foreach(var triangle in selected)selectionBounds.Encapsulate(triangle.bounds);
                        var candidates=interior.Where(t=>t.bounds.Intersects(selectionBounds)).ToArray();row.staticBroadphaseCandidates=candidates.Length;
                        foreach(var skin in selected)foreach(var furniture in candidates)
                        {
                            if(!skin.bounds.Intersects(furniture.bounds))continue;row.triangleTests++;report.totalTriangleTests++;
                            bool coplanar;if(!Intersects(skin,furniture,out coplanar))continue;row.intersections++;report.totalIntersections++;
                            if(contacts.Count<5000)contacts.Add(new Contact{seat=seat,skinTriangle=skin.index,interiorTriangle=furniture.index,skinPath=skin.path,skinMesh=source.name,skinBones=TriangleBones(source,renderer.bones,skin.index),interiorPath=furniture.path,staticMesh=furniture.mesh,staticSurfaceClass=furniture.surfaceClass,coplanar=coplanar,skinA=skin.a,skinB=skin.b,skinC=skin.c,interiorA=furniture.a,interiorB=furniture.b,interiorC=furniture.c});
                        }
                    }
                    finally{UnityEngine.Object.DestroyImmediate(baked);}
                }
            }
            if(rows.Sum(r=>r.selectedSurfaceTriangles)==0)throw new Exception("No actual skin surface triangles selected in requested scope.");
            report.surfaceIntersectionDetected=report.totalIntersections>0;report.status=report.fullBodyCabin?(report.surfaceIntersectionDetected?"ACTUAL_CABIN_BODY_STATIC_SURFACE_CONTACTS_FOUND":"NO_CONTACT_IN_FULL_CABIN_SKIN_TRIANGLE_SCOPE"):(report.surfaceIntersectionDetected?"ACTUAL_SHIN_INTERIOR_SURFACE_CONTACTS_FOUND":"NO_CONTACT_IN_WEIGHTED_SHIN_TRIANGLE_SCOPE");
        }
        catch(Exception error){report.error=error.ToString();throw;}
        finally{report.skins=rows.ToArray();report.contacts=contacts.ToArray();report.storedContacts=contacts.Count;File.WriteAllText(System.IO.Path.Combine(outputDirectory,"shin_collision.json"),JsonUtility.ToJson(report,true));}
    }
    static string TriangleBones(Mesh mesh,Transform[] bones,int triangle)
    {
        var weights=mesh.boneWeights;var indices=mesh.triangles;var sums=new Dictionary<string,float>();
        foreach(int index in indices.Skip(triangle*3).Take(3)){var w=weights[index];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};for(int j=0;j<4;j++){if(values[j]<=0||bones[ids[j]]==null)continue;string n=bones[ids[j]].name;if(!sums.ContainsKey(n))sums[n]=0;sums[n]+=values[j];}}
        return string.Join(",",sums.OrderByDescending(p=>p.Value).Take(3).Select(p=>p.Key));
    }
    static float[] LowerLegWeights(Mesh mesh,Transform[] bones,Transform leftLeg,Transform rightLeg,Transform leftFoot,Transform rightFoot)
    {
        var accepted=bones.Select(b=>b!=null&&((b==leftLeg||b.IsChildOf(leftLeg))||(b==rightLeg||b.IsChildOf(rightLeg)))&&!(b==leftFoot||b.IsChildOf(leftFoot)||b==rightFoot||b.IsChildOf(rightFoot))).ToArray();
        var result=new float[mesh.vertexCount];var counts=mesh.GetBonesPerVertex();var all=mesh.GetAllBoneWeights();
        try{if(counts.Length!=result.Length)throw new Exception("Native per-vertex weights absent");int offset=0;for(int vertex=0;vertex<result.Length;vertex++)for(int n=0;n<counts[vertex];n++){var weight=all[offset++];if(weight.boneIndex<0||weight.boneIndex>=accepted.Length)throw new Exception("Native skin bone index invalid");if(accepted[weight.boneIndex])result[vertex]+=weight.weight;}if(offset!=all.Length)throw new Exception("Native weights were not completely read");}
        finally{counts.Dispose();all.Dispose();}return result;
    }
    static bool WithinShinBand(Vector3 point,Vector3 ankle,Vector3 knee)
    {var direction=knee-ankle;if(direction.sqrMagnitude<1e-8f)throw new Exception("Actual native shin length zero");float along=Vector3.Dot(point-ankle,direction)/direction.sqrMagnitude;return along>=-.05f&&along<=1.05f;}
    static bool Intersects(Triangle a,Triangle b,out bool coplanar)
    {
        var edgesA=new[]{a.b-a.a,a.c-a.b,a.a-a.c};var edgesB=new[]{b.b-b.a,b.c-b.b,b.a-b.c};
        var normalA=Vector3.Cross(edgesA[0],edgesA[1]).normalized;var normalB=Vector3.Cross(edgesB[0],edgesB[1]).normalized;
        coplanar=Vector3.Cross(normalA,normalB).sqrMagnitude<1e-8f&&Mathf.Abs(Vector3.Dot(normalA,b.a-a.a))<=Epsilon;
        if(Separates(normalA,a,b)||Separates(normalB,a,b))return false;
        foreach(var edgeA in edgesA)foreach(var edgeB in edgesB)if(Separates(Vector3.Cross(edgeA,edgeB),a,b))return false;
        // Degenerate planar polytopes require their in-plane edge-normal axes.
        if(coplanar){foreach(var edge in edgesA)if(Separates(Vector3.Cross(normalA,edge),a,b))return false;foreach(var edge in edgesB)if(Separates(Vector3.Cross(normalA,edge),a,b))return false;}
        return true;
    }
    static bool Separates(Vector3 axis,Triangle a,Triangle b)
    {
        if(axis.sqrMagnitude<1e-14f)return false;axis.Normalize();float a0=Vector3.Dot(axis,a.a),a1=Vector3.Dot(axis,a.b),a2=Vector3.Dot(axis,a.c),b0=Vector3.Dot(axis,b.a),b1=Vector3.Dot(axis,b.b),b2=Vector3.Dot(axis,b.c);
        return Mathf.Max(a0,Mathf.Max(a1,a2))<Mathf.Min(b0,Mathf.Min(b1,b2))-Epsilon||Mathf.Max(b0,Mathf.Max(b1,b2))<Mathf.Min(a0,Mathf.Min(a1,a2))-Epsilon;
    }
    static int SelfTests()
    {
        var baseTriangle=new Triangle(0,"fixture",new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0));
        var examples=new[]{
            new Triangle(1,"overlap",new Vector3(.1f,.1f,0),new Vector3(.5f,.1f,0),new Vector3(.1f,.5f,0)),
            new Triangle(2,"disjoint coplanar",new Vector3(2,0,0),new Vector3(3,0,0),new Vector3(2,1,0)),
            new Triangle(3,"parallel separation",new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(0,1,1)),
            new Triangle(4,"actual sheet crossing",new Vector3(.2f,.2f,-1),new Vector3(.2f,.2f,1),new Vector3(.2f,.8f,0)),
            new Triangle(5,"sheet misses",new Vector3(2,.2f,-1),new Vector3(2,.2f,1),new Vector3(2,.8f,0)),
            new Triangle(6,"shared edge",new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,-1,0)),
            new Triangle(7,"shared point",new Vector3(1,0,0),new Vector3(2,0,0),new Vector3(1,-1,0)),
            new Triangle(8,"outside diagonal",new Vector3(.7f,.7f,0),new Vector3(1,.7f,0),new Vector3(.7f,1,0))};
        var expected=new[]{true,false,false,true,false,true,true,false};for(int n=0;n<examples.Length;n++){bool planar;if(Intersects(baseTriangle,examples[n],out planar)!=expected[n]||Intersects(examples[n],baseTriangle,out planar)!=expected[n])throw new Exception("Triangle intersection algorithm self-test failed: "+examples[n].path);}return examples.Length*2;
    }
    static Vector3 Point(Transform model,Transform owner,Vector3 local){var point=model.InverseTransformPoint(owner.TransformPoint(local));if(float.IsNaN(point.x)||float.IsNaN(point.y)||float.IsNaN(point.z)||float.IsInfinity(point.x)||float.IsInfinity(point.y)||float.IsInfinity(point.z))throw new Exception("Nonfinite mesh point");return point;}
    static string Path(Transform root,Transform target){var names=new List<string>();while(target!=null&&target!=root){names.Add(target.name);target=target.parent;}names.Reverse();return string.Join("/",names);}
}


