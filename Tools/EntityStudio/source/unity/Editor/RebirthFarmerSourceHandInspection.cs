using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthFarmerSourceHandInspection
    {
        private struct Sample {public Vector3 normalized;public Vector2 uv;public Color color;}
        public static void Run()
        {
            const string folder="Assets/EntityStudioInputs/RebirthFarmer";
            string modelPath=AssetDatabase.FindAssets("t:Model",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).First(path=>path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase));
            string albedoPath=AssetDatabase.FindAssets("t:Texture2D",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).First(path=>Path.GetFileNameWithoutExtension(path).IndexOf("basecolor",StringComparison.OrdinalIgnoreCase)>=0);
            GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);var texture=new Texture2D(2,2,TextureFormat.RGB24,false);
            try
            {
                if(!texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(albedoPath))))throw new IOException("Cannot decode source albedo.");
                var raw=new List<Sample>();Bounds bounds=new Bounds();bool has=false;
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    Vector3[] vertices=filter.sharedMesh.vertices;Vector2[] uv=filter.sharedMesh.uv;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        Vector3 point=model.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                        if(!has){bounds=new Bounds(point,Vector3.zero);has=true;}else bounds.Encapsulate(point);
                        raw.Add(new Sample{normalized=point,uv=uv[i],color=texture.GetPixelBilinear(uv[i].x,uv[i].y)});
                    }
                }
                var samples=new List<Sample>();foreach(var sample in raw){var value=sample;value.normalized=RebirthEntityRigBuilder.NormalizeBonePositionInSourceBounds(bounds,sample.normalized,EntityModelUpAxis.Z);samples.Add(value);}
                var rows=new List<string>{"side,x,y,z,u,v,r,g,b,redDominant"};var stats=new List<string>{"side,boundary,count,minY,p02Y,redCount,redMinY,redP02Y"};
                foreach(string side in new[]{"Left","Right"})
                {
                    bool left=side=="Left";
                    foreach(var sample in samples)
                    {
                        Vector3 p=sample.normalized;if((left?p.x>=.24f:p.x<=.76f)||p.y<.28f||p.y>.68f)continue;
                        rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9}",side,p.x,p.y,p.z,sample.uv.x,sample.uv.y,sample.color.r,sample.color.g,sample.color.b,Red(sample.color)?1:0));
                    }
                    foreach(float boundary in new[]{.13f,.145f,.16f,.18f,.20f,.24f})
                    {
                        var selected=samples.Where(sample=>(left?sample.normalized.x<boundary:sample.normalized.x>1f-boundary)&&sample.normalized.y>.30f&&sample.normalized.y<.470f).ToList();
                        var heights=selected.Select(sample=>sample.normalized.y).OrderBy(value=>value).ToList();var red=selected.Where(sample=>Red(sample.color)).Select(sample=>sample.normalized.y).OrderBy(value=>value).ToList();
                        stats.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2},{3:R},{4:R},{5},{6:R},{7:R}",side,boundary,heights.Count,At(heights,0),At(heights,.02f),red.Count,At(red,0),At(red,.02f)));
                    }
                }
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation");Directory.CreateDirectory(output);
                File.WriteAllLines(Path.Combine(output,"FarmerSourceHandSamples.csv"),rows);File.WriteAllLines(Path.Combine(output,"FarmerSourceHandExtents.csv"),stats);
                var bands=new List<string>{"side,sourceY,height,count,meanX,widthX,depth05,depth95,depthSpan,redFraction"};
                foreach(string side in new[]{"Left","Right"})for(float y=.44f;y<.641f;y+=.01f)
                {
                    bool left=side=="Left";float boundary=Mathf.Lerp(.145f,.17f,Mathf.InverseLerp(.51f,.57f,y));
                    var selected=samples.Where(sample=>(left?sample.normalized.x<boundary:sample.normalized.x>1f-boundary)&&Mathf.Abs(sample.normalized.y-y)<.0075f).ToList();
                    if(selected.Count==0)continue;var depths=selected.Select(sample=>sample.normalized.z).OrderBy(value=>value).ToList();var widths=selected.Select(sample=>left?sample.normalized.x:1f-sample.normalized.x).OrderBy(value=>value).ToList();
                    bands.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R}",side,y,y*1.8f,selected.Count,widths.Average(),At(widths,.95f)-At(widths,.05f),At(depths,.05f),At(depths,.95f),At(depths,.95f)-At(depths,.05f),(float)selected.Count(sample=>Red(sample.color))/selected.Count));
                }
                File.WriteAllLines(Path.Combine(output,"FarmerSourceWristBands.csv"),bands);
                Debug.Log("SOURCE HAND INSPECTION: "+output+"; redDominant is a color measurement, not a skin classification");
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        private static bool Red(Color color)=>color.r>color.g*1.1f&&color.r>color.b*1.1f;
        private static float At(List<float> values,float fraction)=>values.Count==0?-1f:values[Mathf.FloorToInt((values.Count-1)*fraction)];
    }
}
