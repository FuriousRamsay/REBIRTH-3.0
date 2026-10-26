using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntitySurfaceRepairProbe
    {
        public static void RunFarmer()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string path=AssetDatabase.FindAssets("t:Model",new[]{"Assets/EntityStudioInputs/RebirthFarmer"}).Select(AssetDatabase.GUIDToAssetPath).First(value=>value.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase));
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));var owned=new List<Mesh>();
            try
            {
                var renderers=instance.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                instance.transform.localScale*=1.8f/bounds.size.y;bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var rows=new List<string>{"mesh,sourceVertices,sourceTriangles,repairVertices,capLoops,originalUvsPreserved"};
                foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    var original=filter.sharedMesh;var mesh=new Mesh{name=original.name+"_RepairProbe",indexFormat=original.indexFormat};owned.Add(mesh);
                    mesh.vertices=original.vertices;mesh.normals=original.normals;mesh.uv=original.uv;mesh.triangles=original.triangles;
                    var inputs=new List<EntitySurfaceRepairInput>();RebirthEntityMeshCleanup.SeparateFusedArms(mesh,filter.transform,bounds,new Vector2(.41f,.56f),inputs);
                    var input=inputs.Single();Vector2[] uv=mesh.uv;bool preserved=true;
                    RebirthEntityRepairConnectivity.Write(input,Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerRepairConnectivity.csv"));
                    for(int i=0;i<input.sourceUv.Length;i++)if(uv[i].x!=input.sourceUv[i].x||uv[i].y!=input.sourceUv[i].y||input.repairVertexIds[i]!=0)preserved=false;
                    if(!preserved)throw new InvalidOperationException("Original clipped surface UVs or cap ownership changed.");
                    int[] triangles=mesh.triangles;
                    for(int i=0;i<triangles.Length;i+=3)
                    {int a=input.repairVertexIds[triangles[i]],b=input.repairVertexIds[triangles[i+1]],c=input.repairVertexIds[triangles[i+2]];if(a!=b||a!=c)throw new InvalidOperationException("Refinement mixed cap/source chart ownership in a triangle.");}
                    int repairVertices=input.repairVertexIds.Count(value=>value>0),loops=input.repairVertexIds.Max();
                    for(int vertex=0;vertex<input.repairVertexIds.Length;vertex++)
                    {
                        if(input.repairVertexIds[vertex]==0)continue;
                        Vector3Int anchors=input.repairWeightAnchors[vertex];Vector3 bary=input.repairWeightBarycentrics[vertex];
                        if(anchors.x>=input.sourcePositionsWorld.Length||anchors.y>=input.sourcePositionsWorld.Length||anchors.z>=input.sourcePositionsWorld.Length)throw new InvalidOperationException("Cap correspondence refers outside original source boundary.");
                        Vector3 reconstructed=input.sourcePositionsWorld[anchors.x]*bary.x+input.sourcePositionsWorld[anchors.y]*bary.y+input.sourcePositionsWorld[anchors.z]*bary.z;
                        if(Vector3.Distance(reconstructed,input.repairPositionsWorld[vertex])>1e-5f)throw new InvalidOperationException("Subdivision lost repair boundary correspondence.");
                    }
                    Debug.Log("REPAIR CORRESPONDENCE: all "+repairVertices+" cap vertices reconstruct from original boundary anchors within 0.01 mm.");
                    rows.Add(original.name+","+input.sourceUv.Length+","+input.sourceTriangles.Length/3+","+repairVertices+","+loops+",true");
                    bool[] protectedPixels=RebirthEntityUvChartAllocator.BuildOccupancy(new[]{original});
                    var charts=RebirthEntityRepairCharts.Plan(inputs,new RebirthEntityUvChartAllocator(protectedPixels,512));
                    var chartRows=new List<string>{"cap,part,axis,x,y,width,height,vertices"};
                    foreach(var chart in charts)
                    {
                        for(int y=chart.pixels.yMin-2;y<chart.pixels.yMax+2;y++)for(int x=chart.pixels.xMin-2;x<chart.pixels.xMax+2;x++)
                            if(protectedPixels[y*512+x])throw new InvalidOperationException("Repair chart overwrote protected source UV space.");
                        foreach(int vertex in chart.vertices)
                        {Vector2 point=chart.Uv(input.repairPositionsWorld[vertex],512);if(float.IsNaN(point.x)||float.IsNaN(point.y)||point.x<0||point.x>1||point.y<0||point.y>1)throw new InvalidOperationException("Invalid repair UV projection.");}
                        chartRows.Add(chart.capId+","+chart.part+","+chart.axis+","+chart.pixels.x+","+chart.pixels.y+","+chart.pixels.width+","+chart.pixels.height+","+chart.vertices.Count);
                    }
                    File.WriteAllLines(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerRepairCharts.csv"),chartRows);
                    if(Array.IndexOf(Environment.GetCommandLineArgs(),"-sampleRepairCharts")>=0)
                    {
                        string imagePath=Directory.GetFiles("Assets/EntityStudioInputs/RebirthFarmer","*basecolor*",SearchOption.AllDirectories).First(value=>!value.EndsWith(".meta",StringComparison.OrdinalIgnoreCase));
                        var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);image.LoadImage(File.ReadAllBytes(imagePath));
                        var sheet=new Texture2D(6*128,4*128,TextureFormat.RGBA32,false,true);var coverageRows=new List<string>{"cap,part,coverage"};
                        try
                        {
                            for(int index=0;index<charts.Count;index++)
                            {
                                Color32[] samples=RebirthEntityRepairProjection.Sample(charts[index],image,128,128,out float coverage);
                                sheet.SetPixels32(index%6*128,index/6*128,128,128,samples);
                                coverageRows.Add(charts[index].capId+","+charts[index].part+","+coverage.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                            }
                            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation");
                            File.WriteAllBytes(Path.Combine(folder,"FarmerRepairProjection.png"),sheet.EncodeToPNG());File.WriteAllLines(Path.Combine(folder,"FarmerRepairProjection.csv"),coverageRows);
                        }
                        finally{UnityEngine.Object.DestroyImmediate(sheet);UnityEngine.Object.DestroyImmediate(image);}
                    }
                    Debug.Log("REPAIR CHART PLAN: "+charts.Count+" caps allocated without protected source overlap; UVs not committed before baking.");
                    if(Array.IndexOf(Environment.GetCommandLineArgs(),"-bakeRepairAtlas")>=0)
                    {
                        string sourcePath=Directory.GetFiles("Assets/EntityStudioInputs/RebirthFarmer","*basecolor*",SearchOption.AllDirectories).First(value=>!value.EndsWith(".meta",StringComparison.OrdinalIgnoreCase));
                        byte[] baked=RebirthEntityRepairAtlasBake.BakeMap(sourcePath,charts,512);
                        var sourceImage=new Texture2D(2,2,TextureFormat.RGBA32,false,true);var resultImage=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                        try
                        {
                            sourceImage.LoadImage(File.ReadAllBytes(sourcePath));resultImage.LoadImage(baked);
                            Color32[] before=sourceImage.GetPixels32(),after=resultImage.GetPixels32();var reserved=new bool[512*512];
                            foreach(var chart in charts)for(int y=chart.pixels.yMin-2;y<chart.pixels.yMax+2;y++)for(int x=chart.pixels.xMin-2;x<chart.pixels.xMax+2;x++)reserved[y*512+x]=true;
                            for(int y=0;y<sourceImage.height;y++)for(int x=0;x<sourceImage.width;x++)
                                if(!reserved[(y*512/sourceImage.height)*512+x*512/sourceImage.width]&&!before[y*sourceImage.width+x].Equals(after[y*sourceImage.width+x]))throw new InvalidOperationException("Bake changed a pixel outside reserved repair regions.");
                            Vector2[] beforeUv=mesh.uv;RebirthEntityRepairAtlasBake.CommitUvs(charts,512);Vector2[] afterUv=mesh.uv;
                            for(int i=0;i<afterUv.Length;i++)if(input.repairVertexIds[i]==0&&beforeUv[i]!=afterUv[i])throw new InvalidOperationException("Bake changed an original-face UV.");
                            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerRepairAtlas_Albedo.png"),baked);
                            Debug.Log("REPAIR ATLAS BAKE: all unreserved source pixels unchanged; all original-face UVs unchanged; diagnostic mesh only.");
                        }
                        finally{UnityEngine.Object.DestroyImmediate(sourceImage);UnityEngine.Object.DestroyImmediate(resultImage);}
                    }
                    Debug.Log("SURFACE REPAIR PROBE: originalUVsPreserved=true capOwnershipConsistent=true sourceTriangles="+input.sourceTriangles.Length/3+" capLoops="+loops+" repairVertices="+repairVertices);
                }
                string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerSurfaceRepairProbe.csv");File.WriteAllLines(output,rows);
            }
            finally{foreach(var mesh in owned)UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(instance);}
        }
    }
}
