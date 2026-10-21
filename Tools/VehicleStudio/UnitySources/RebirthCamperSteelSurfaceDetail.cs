using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Editor-only UV/material correction. No collider, transform or shape edits.
public static class RebirthCamperSteelSurfaceDetail
{
    const string Folder="Assets/REBIRTHCamperSteelSurfaceDetail_20261021";
    const string Prefab="Assets/REBIRTHPickupRearCornerCandidate_20261021_v1/rebirthCamper80s.prefab";
    const string Source="Assets/REBIRTHCamperArmorPlow_20261017_v4/SalvagedSteel_Albedo.mat";
    static bool Selected(Material material)
    {
        if(!material)return false;
        string n=material.name;
        return n=="grilles" || n=="Camper ladder close-detail steel" || n=="Rear ladder brushed weathered steel";
    }
    static bool Selected(Material material,Transform part)
    {
        if(Selected(material))return true;
        if(!material||material.name!="plough")return false;
        for(var ancestor=part;ancestor!=null;ancestor=ancestor.parent)
            if(ancestor.name=="REBIRTHRearLadders"||ancestor.name=="Short straight front ladders and ladder mounted supports"||ancestor.name=="Fitted rooftop guardrails"||ancestor.name=="Four outboard corner turrets - static placement review")return true;
        return false;
    }
    static string Geometry(Mesh mesh)
    {
        using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory))
        {
            var v=mesh.vertices;var n=mesh.normals;
            for(int s=0;s<mesh.subMeshCount;s++)foreach(int i in mesh.GetTriangles(s))
            {
                writer.Write(s);writer.Write(v[i].x);writer.Write(v[i].y);writer.Write(v[i].z);
                if(n.Length==v.Length){writer.Write(n[i].x);writer.Write(n[i].y);writer.Write(n[i].z);}
            }
            writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(memory.ToArray()));
        }
    }
    static string PathOf(Transform t,Transform root)=>t==root?t.name:PathOf(t.parent,root)+"/"+t.name;
    static Mesh Project(MeshFilter filter,Transform root)
    {
        var source=filter.sharedMesh;var vertices=source.vertices;var normals=source.normals;
        if(normals.Length!=vertices.Length)throw new Exception("Missing authored normals: "+filter.name);
        var points=new List<Vector3>();var ns=new List<Vector3>();var uvs=new List<Vector2>();
        var matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
        var mesh=new Mesh{name=source.name+" metre steel UV"};
        mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.subMeshCount=source.subMeshCount;
        var parts=new List<int[]>();
        for(int s=0;s<source.subMeshCount;s++)
        {
            var old=source.GetTriangles(s);var indices=new int[old.Length];
            for(int i=0;i<old.Length;i+=3)
            {
                var a=matrix.MultiplyPoint3x4(vertices[old[i]]);var b=matrix.MultiplyPoint3x4(vertices[old[i+1]]);var c=matrix.MultiplyPoint3x4(vertices[old[i+2]]);
                var normal=Vector3.Cross(b-a,c-a).normalized;
                var absolute=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
                int axis=absolute.x>=absolute.y&&absolute.x>=absolute.z?0:absolute.y>=absolute.z?1:2;
                for(int j=0;j<3;j++)
                {
                    int original=old[i+j];var p=matrix.MultiplyPoint3x4(vertices[original]);
                    // Full 2048px source spans two physical metres:1024px/m.
                    Vector2 uv=axis==0?new Vector2(p.z,p.y):axis==1?new Vector2(p.x,p.z):new Vector2(p.x,p.y);
                    if(normal[axis]<0)uv.x=-uv.x;
                    indices[i+j]=points.Count;points.Add(vertices[original]);ns.Add(normals[original]);uvs.Add(uv*.5f);
                }
            }
            parts.Add(indices);
        }
        mesh.SetVertices(points);mesh.SetNormals(ns);mesh.SetUVs(0,uvs);
        for(int s=0;s<parts.Count;s++)mesh.SetTriangles(parts[s],s);
        mesh.RecalculateTangents();mesh.bounds=source.bounds;
        if(Geometry(source)!=Geometry(mesh))throw new Exception("Geometry changed: "+filter.name);
        return mesh;
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var original=AssetDatabase.LoadAssetAtPath<Material>(Source);if(!original)throw new Exception("Missing full steel source material");
        var steel=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/FullMapSteel.mat");
        bool create=steel==null;
        if(create)steel=new Material(original){name="Camper full-map metre steel detail"};
        foreach(string map in new[]{"_MainTex","_BumpMap","_MetallicGlossMap","_OcclusionMap"})
        {
            var texture=steel.GetTexture(map);if(!texture)throw new Exception("Missing source map "+map);
            if((map=="_MainTex"||map=="_BumpMap")&&(texture.width!=2048||texture.height!=2048))throw new Exception("Unexpected detail map resolution "+map);
            if(texture.wrapMode!=TextureWrapMode.Repeat)throw new Exception("Source must already repeat: "+map);
            steel.SetTextureScale(map,Vector2.one);steel.SetTextureOffset(map,Vector2.zero);
        }
        steel.EnableKeyword("_NORMALMAP");steel.EnableKeyword("_METALLICGLOSSMAP");
        if(create)AssetDatabase.CreateAsset(steel,Folder+"/FullMapSteel.mat");
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        root.name="rebirthCamper80s";
        var report=new List<string>{"Full source maps: "+Source,"Density:2048px/2m=1024px/m; no sharpening/cropping/importer edits"};
        int serial=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!filter.sharedMesh)continue;
            if(!renderer.sharedMaterials.Any(m=>Selected(m,filter.transform)))continue;
            if(renderer.sharedMaterials.Any(m=>!Selected(m,filter.transform)))throw new Exception("Mixed protected/steel material: "+filter.name);
            var before=Geometry(filter.sharedMesh);var mesh=Project(filter,root.transform);
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder+"/SteelUV"+(serial++)+".asset"));
            filter.sharedMesh=mesh;renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>steel).ToArray();
            report.Add(PathOf(filter.transform,root.transform)+" geometrySHA256="+before+" unchanged=true");
        }
        if(serial==0)throw new Exception("No intended grille/ladder steel renderers found");
        PrefabUtility.SaveAsPrefabAsset(root,Prefab);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        File.WriteAllLines(Folder+"/steel-detail-audit.txt",report);
        Debug.Log("REBIRTH steel surface detail complete: "+serial+" meshes, geometry/normals preserved");
    }
}
