using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Read-only furniture measurement. Region proposals derive from the inspected
// interior top view; they are not seating capacity or avatar-fit acceptance.
public static class RebirthCamperCabinSeatSurfaces
{
    [Serializable] public class Face
    {
        public string region;
        public int triangle;
        public Vector3 a,b,c,center;
        public float normalY,area;
    }
    [Serializable] public class Report
    {
        public string status="FAIL",error="",source="Assets/REBIRTHCamperPlayableGenerated/8bfe6792d86a4397966e109c4ba17924/rebirthCamper80s.prefab";
        public int meshTriangles;
        public Face[] upwardFaces;
        public Face[] allUpwardFaces;
        public bool seatingAcceptance=false;
    }
    public static void RunBatch()
    {
        string[] args=Environment.GetCommandLineArgs();
        string folder=args[Array.IndexOf(args,"-camperSeatSurfaceReport")+1];
        Directory.CreateDirectory(folder);
        var report=new Report(); GameObject root=null;
        try
        {
            if(Application.unityVersion!="2022.3.62f2") throw new Exception("Exact Unity required.");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(report.source);
            if(source==null) throw new Exception("Preserved camper candidate missing.");
            root=UnityEngine.Object.Instantiate(source);
            var interior=root.transform.Find("M/VisualBody/80sCamperTruck/camper_interior");
            if(interior==null) throw new Exception("Measured camper interior path missing.");
            var mesh=interior.GetComponent<MeshFilter>().sharedMesh;
            var vertices=mesh.vertices; var normals=mesh.normals; var indices=mesh.triangles;
            if(normals.Length!=vertices.Length) throw new Exception("Interior normals missing.");
            report.meshTriangles=indices.Length/3;
            var faces=new List<Face>();
            var allFaces=new List<Face>();
            for(int index=0;index<indices.Length;index+=3)
            {
                int ia=indices[index],ib=indices[index+1],ic=indices[index+2];
                Vector3 a=RootPoint(root,interior,vertices[ia]),b=RootPoint(root,interior,vertices[ib]),c=RootPoint(root,interior,vertices[ic]);
                Vector3 center=(a+b+c)/3;
                string region=center.z>=-1.15f && center.z<=-.55f?"front_dinette"
                    :(center.z>=-2.4f && center.z<=-1.8f?"rear_dinette"
                    :(center.z>-1.8f && center.z<-1.15f?"table":""));
                Vector3 normal=root.transform.InverseTransformDirection(interior.TransformDirection(normals[ia]+normals[ib]+normals[ic])).normalized;
                float area=Vector3.Cross(b-a,c-a).magnitude*.5f;
                if(normal.y<.8f || area<.00001f) continue;
                allFaces.Add(new Face{region="unassigned",triangle=index/3,a=a,b=b,c=c,center=center,normalY=normal.y,area=area});
                if(center.x<.4f || center.x>.9f || region=="") continue;
                faces.Add(new Face{region=region,triangle=index/3,a=a,b=b,c=c,center=center,normalY=normal.y,area=area});
            }
            report.upwardFaces=faces.ToArray();
            // Bed and sink-adjacent selections must use explicit measured triangles,
            // not the old dinette-only center region or broad combined bounds.
            report.allUpwardFaces=allFaces.ToArray();
            if(faces.Count==0) throw new Exception("No upward furniture surfaces inside proposed regions.");
            report.status="PASS_SURFACE_MEASUREMENTS_NOT_SEATING_ACCEPTANCE";
        }
        catch(Exception error){report.error=error.ToString();throw;}
        finally
        {
            File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(report,true));
            if(root!=null) UnityEngine.Object.DestroyImmediate(root);
        }
    }
    private static Vector3 RootPoint(GameObject root,Transform mesh,Vector3 vertex)
    {return root.transform.InverseTransformPoint(mesh.TransformPoint(vertex));}
}
