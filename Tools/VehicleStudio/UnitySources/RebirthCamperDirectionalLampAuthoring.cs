using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Call after rear-lens authoring, before the native GameObject/Physics wrapper.
// All meshes/materials are copies in the current owned generated asset folder.
public static class RebirthCamperDirectionalLampAuthoring
{
    [Serializable] public class Result { public int triangles, lightCount, roofTriangles, redBodyTriangles, preservedAmberTriangles; public Vector3[] markerPositions,markerDirections; }
    public static Result Apply(GameObject root,string folder)
    {
        var lamp=root.transform.Find("M/VisualBody/80sCamperTruck/lights");
        if(lamp==null || lamp.Find("BodyMarkerLenses")!=null)throw new InvalidOperationException("Unique source lamp mesh required.");
        var renderer=lamp.GetComponent<MeshRenderer>();var filter=lamp.GetComponent<MeshFilter>();
        var source=filter.sharedMesh;var material=renderer.sharedMaterial;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(material.mainTexture))))throw new Exception("Lamp atlas decode failed.");
            var report=new Result();
            var indices=source.triangles;var uv=source.uv;var vertices=source.vertices;
            var selected=new List<int>();var remainder=new List<int>();var centers=new List<Vector3>();
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=uv[indices[i]];var b=uv[indices[i+1]];var c=uv[indices[i+2]];
                var samples=new[]{(a+b+c)/3,a*.6f+b*.2f+c*.2f,a*.2f+b*.6f+c*.2f,a*.2f+b*.2f+c*.6f};
                Vector3 center=root.transform.InverseTransformPoint(lamp.TransformPoint((vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3));
                bool redCandidate=samples.Count(p=>RedColor(texture.GetPixelBilinear(p.x,p.y)))>=3;
                bool amber=samples.Count(p=>MarkerColor(texture.GetPixelBilinear(p.x,p.y)))>=3 && !redCandidate;
                bool marker=redCandidate || (amber && center.y>1.2f); // Roof clearance lamps only; preserve low amber turn-signal lenses.
                if(marker){if(redCandidate)report.redBodyTriangles++;else report.roofTriangles++;}else if(amber)report.preservedAmberTriangles++;
                var target=marker?selected:remainder;
                target.Add(indices[i]);target.Add(indices[i+1]);target.Add(indices[i+2]);
                if(marker)centers.Add(root.transform.InverseTransformPoint(lamp.TransformPoint((vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3)));
            }
            if(selected.Count==0)throw new Exception("No measured amber/red body marker surfaces.");
            var kept=UnityEngine.Object.Instantiate(source);kept.name="CamperLampMarkerRemainder";kept.triangles=remainder.ToArray();kept.RecalculateBounds();
            var lenses=UnityEngine.Object.Instantiate(source);lenses.name="CamperBodyMarkerLenses";lenses.triangles=selected.ToArray();lenses.RecalculateBounds();
            var red=new Material(material){name="CamperBodyMarkerRed"};red.shader=Shader.Find("Standard");
            // Neutral lens base allows requested red marker appearance rather than amber emission tint.
            red.mainTexture=null;red.color=new Color(.25f,.015f,.015f,1);red.SetColor("_EmissionColor",Color.black);red.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;red.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(kept,folder+"/BodyMarkerRemainder.asset");AssetDatabase.CreateAsset(lenses,folder+"/BodyMarkerLenses.asset");AssetDatabase.CreateAsset(red,folder+"/BodyMarkerRed.mat");
            filter.sharedMesh=kept;
            var child=new GameObject("BodyMarkerLenses");child.transform.SetParent(lamp,false);child.layer=lamp.gameObject.layer;
            child.AddComponent<MeshFilter>().sharedMesh=lenses;child.AddComponent<MeshRenderer>().sharedMaterial=red;
            var holder=new GameObject("BodyMarkerLights");holder.transform.SetParent(root.transform.Find("M"),false);
            // Spatial grouping merges triangles of each marker lens, including roof lamps.
            var groups=new List<List<Vector3>>();
            foreach(var p in centers){var group=groups.FirstOrDefault(g=>g.Any(q=>(q-p).sqrMagnitude<.04f));if(group==null){group=new List<Vector3>();groups.Add(group);}group.Add(p);}
            int number=0;var positions=new List<Vector3>();var directions=new List<Vector3>();
            foreach(var group in groups)
            {
                var p=new Vector3(group.Average(v=>v.x),group.Average(v=>v.y),group.Average(v=>v.z));
                var go=new GameObject("RedMarker"+(number++));go.transform.SetParent(holder.transform,false);
                Vector3 direction=p.y>1.2f && (p.z>1f || p.z<-2.7f)?new Vector3(0,0,Mathf.Sign(p.z)):new Vector3(Mathf.Sign(p.x),0,0);
                positions.Add(p+direction*.035f);directions.Add(direction);
                go.transform.position=root.transform.TransformPoint(p+direction*.035f);go.transform.rotation=root.transform.rotation*Quaternion.LookRotation(direction);
                var light=go.AddComponent<Light>();light.type=LightType.Spot;light.color=Color.red;light.range=.65f;light.spotAngle=100;light.intensity=0;light.enabled=false;light.shadows=LightShadows.None;
            }
            foreach(var light in root.transform.Find("M/RearLights").GetComponentsInChildren<Light>(true)){light.range=4;light.spotAngle=48;light.transform.rotation=root.transform.rotation*Quaternion.Euler(0,180,0);}
            foreach(var light in root.transform.Find("M/HeadLights/Lights").GetComponentsInChildren<Light>(true)){light.type=LightType.Spot;light.range=32;light.spotAngle=48;light.transform.rotation=root.transform.rotation*Quaternion.Euler(3,0,0);}
            report.markerPositions=positions.ToArray();report.markerDirections=directions.ToArray();report.lightCount=number;report.triangles=selected.Count/3;return report;
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);}
    }
    private static bool RedColor(Color c){return c.r>.16f && c.g/c.r<.15f && c.b/c.r<.15f;}
    private static bool MarkerColor(Color c){return c.r>.16f && c.g/c.r<.78f && c.b/c.r<.20f;}
}





