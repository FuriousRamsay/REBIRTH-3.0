using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityRepairAtlasBake
    {
        public static void Prepare(RebirthEntityRigWorkspace workspace)
        {
            if(workspace.surfaceRepairInputs.Count==0)return;
            var sourceMeshes=new List<Mesh>();
            foreach(var input in workspace.surfaceRepairInputs)
            {
                var sourceMesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                sourceMesh.vertices=input.sourcePositionsWorld;sourceMesh.uv=input.sourceUv;sourceMesh.triangles=input.sourceTriangles;sourceMeshes.Add(sourceMesh);
            }
            List<RebirthEntityRepairCharts.Chart> charts;
            try{charts=RebirthEntityRepairCharts.Plan(workspace.surfaceRepairInputs,new RebirthEntityUvChartAllocator(RebirthEntityUvChartAllocator.BuildOccupancy(sourceMeshes),512));}
            finally{foreach(var mesh in sourceMeshes)UnityEngine.Object.DestroyImmediate(mesh);}
            string folder="Assets/EntityStudioGenerated/"+workspace.profile.entityClass+"/RepairMaps";
            var encoded=new Dictionary<string,byte[]>();
            foreach(string suffix in new[]{"basecolor","normal","roughness","metallic"})
            {
                string source=Directory.GetFiles(workspace.profile.sourceTexturePath,"*",SearchOption.AllDirectories).FirstOrDefault(path=>!path.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)&&Path.GetFileNameWithoutExtension(path).EndsWith("_"+suffix,StringComparison.OrdinalIgnoreCase));
                if(source==null)throw new InvalidOperationException("Repair baking requires the original "+suffix+" map.");
                encoded.Add(suffix,BakeMap(source,charts,512,suffix=="normal",false,workspace.profile.wrapMissingTorsoTexture));
            }
            Directory.CreateDirectory(folder);
            foreach(var pair in encoded)
            {
                string path=folder+"/"+pair.Key+".png";
                if(!File.Exists(path)||!File.ReadAllBytes(path).SequenceEqual(pair.Value))File.WriteAllBytes(path,pair.Value);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Texture2D Load(string suffix,EntityTextureUsage usage)
            {
                var map=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+suffix+".png");
                RebirthEntityTextureTools.Configure(map,usage,usage==EntityTextureUsage.Normal?4096:2048);return map;
            }
            var albedo=Load("basecolor",EntityTextureUsage.Albedo);var normal=Load("normal",EntityTextureUsage.Normal);
            var roughness=Load("roughness",EntityTextureUsage.LinearData);var metallic=Load("metallic",EntityTextureUsage.LinearData);
            CommitUvs(charts,512);
            workspace.profile.bakedAlbedo=albedo;workspace.profile.normalMap=normal;
            workspace.profile.sourceRoughness=roughness;workspace.profile.sourceMetallic=metallic;
            Debug.Log("REPAIR ATLAS: baked four owned maps and committed "+charts.Count+" repair charts before LOD generation.");
        }

        public static byte[] BakeMap(string sourcePath,IReadOnlyList<RebirthEntityRepairCharts.Chart> charts,int allocationResolution,bool flatNormal=false,bool outwardArm=false,bool bodyWrap=false)
        {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if(!source.LoadImage(File.ReadAllBytes(sourcePath)))throw new InvalidDataException("Cannot decode source map: "+sourcePath);
                int width=source.width,height=source.height;
                if(width%allocationResolution!=0||height%allocationResolution!=0)throw new InvalidOperationException("Repair map dimensions must be divisible by chart resolution.");
                int sx=width/allocationResolution,sy=height/allocationResolution;
                // Keep the original texture as the immutable projection source.
                // Only this separate output pixel array receives repair samples.
                Color32[] output=source.GetPixels32();
                foreach(var chart in charts)
                {
                    RectInt rect=new RectInt((chart.pixels.x-2)*sx,(chart.pixels.y-2)*sy,(chart.pixels.width+4)*sx,(chart.pixels.height+4)*sy);
                    var projection=new RebirthEntityRepairCharts.Chart{input=chart.input,capId=chart.capId,part=chart.part,axis=chart.axis};
                    Vector2 size=chart.maximum-chart.minimum;
                    Vector2 units=new Vector2(size.x/(chart.pixels.width-1),size.y/(chart.pixels.height-1));
                    // Chart UV endpoints sit at allocation-cell centres. Extend
                    // projection to reserved rectangle edges, including gutters.
                    projection.minimum=chart.minimum-new Vector2(units.x*2.5f,units.y*2.5f);
                    projection.maximum=chart.maximum+new Vector2(units.x*2.5f,units.y*2.5f);
                    if(bodyWrap&&chart.part==0&&chart.axis==0)
                    {
                        // A source with fused arms has no torso side texture to
                        // project into this cut. Trial a longitudinal strip of
                        // rear clothing at the same height, keeping source UVs.
                        float x=0;foreach(int vertex in chart.vertices)x+=chart.input.repairPositionsWorld[vertex].x;x/=chart.vertices.Count;
                        float mid=chart.input.sourcePositionsWorld[0].x,minX=mid,maxX=mid;
                        foreach(var point in chart.input.sourcePositionsWorld){minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);}mid=(minX+maxX)*.5f;
                        float span=Mathf.Max(.005f,(projection.maximum.x-projection.minimum.x)*.6f);
                        float low=x<mid?x:Mathf.Max(mid,x-span),high=x<mid?Mathf.Min(mid,x+span):x;
                        if(high-low<.00001f)throw new InvalidOperationException("Body wrap has degenerate source strip.");
                        projection.axis=2;projection.minimum=new Vector2(low,projection.minimum.y);projection.maximum=new Vector2(high,projection.maximum.y);
                    }
                    Color32[] samples;
                    if(flatNormal)
                    {samples=new Color32[rect.width*rect.height];for(int i=0;i<samples.Length;i++)samples[i]=new Color32(128,128,255,255);}
                    else samples=RebirthEntityRepairProjection.Sample(projection,source,rect.width,rect.height,out _,outwardArm);
                    for(int y=0;y<rect.height;y++)Array.Copy(samples,y*rect.width,output,(rect.y+y)*width+rect.x,rect.width);
                }
                var baked=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
                try{baked.SetPixels32(output);baked.Apply();return baked.EncodeToPNG();}
                finally{UnityEngine.Object.DestroyImmediate(baked);}
            }
            finally{UnityEngine.Object.DestroyImmediate(source);}
        }

        // Call only after every requested map has baked successfully. Original
        // faces retain their UVs, including interpolated vertices from clipping.
        public static void CommitUvs(IReadOnlyList<RebirthEntityRepairCharts.Chart> charts,int resolution)
        {
            var pending=new Dictionary<Mesh,Vector2[]>();
            foreach(var chart in charts)
            {
                Mesh mesh=chart.input.repairedMesh;
                if(!pending.TryGetValue(mesh,out Vector2[] uv)){uv=mesh.uv;pending.Add(mesh,uv);}
                foreach(int vertex in chart.vertices)uv[vertex]=chart.Uv(chart.input.repairPositionsWorld[vertex],resolution);
            }
            foreach(var pair in pending){pair.Key.uv=pair.Value;pair.Key.RecalculateTangents();}
        }
    }
}
