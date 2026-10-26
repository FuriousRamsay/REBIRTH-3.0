using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityRepairTextureReview
    {
        public static void RunSavedFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            bool commit=Array.IndexOf(Environment.GetCommandLineArgs(),"-commitBodyWrapRepair")>=0;
            bool combined=Array.IndexOf(Environment.GetCommandLineArgs(),"-outwardWithBodyWrap")>=0;
            bool bodyWrap=combined||commit||Array.IndexOf(Environment.GetCommandLineArgs(),"-bodyWrapRepair")>=0;
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/"+(combined?"FarmerOutwardBodyWrapRepairTexture":bodyWrap?"FarmerBodyWrapRepairTexture":"FarmerOutwardRepairTexture"));Directory.CreateDirectory(folder);
            var meshes=new List<Mesh>();var materials=new List<Material>();Texture2D texture=null;
            var originals=workspace.sourceSkinnedMeshes.ToDictionary(r=>r,r=>r.sharedMaterials);
            try
            {
                foreach(var input in workspace.surfaceRepairInputs)
                {var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=input.sourcePositionsWorld;mesh.uv=input.sourceUv;mesh.triangles=input.sourceTriangles;meshes.Add(mesh);}
                var charts=RebirthEntityRepairCharts.Plan(workspace.surfaceRepairInputs,new RebirthEntityUvChartAllocator(RebirthEntityUvChartAllocator.BuildOccupancy(meshes),512));
                foreach(var chart in charts)
                {
                    Vector2[] uv=chart.input.repairedMesh.uv;
                    foreach(int vertex in chart.vertices)if(Vector2.Distance(uv[vertex],chart.Uv(chart.input.repairPositionsWorld[vertex],512))>.000001f)
                        throw new InvalidOperationException("Replanned chart differs from saved UVs; diagnostic bake refused.");
                }
                string source=Directory.GetFiles(workspace.profile.sourceTexturePath,"*",SearchOption.AllDirectories).First(path=>!path.EndsWith(".meta")&&Path.GetFileNameWithoutExtension(path).EndsWith("_basecolor",StringComparison.OrdinalIgnoreCase));
                var rows=new List<string>{"cap,part,axis,minU,minV,maxU,maxV,vertices,debugHue"};
                foreach(var chart in charts)rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5:R},{6:R},{7},{8:R}",chart.capId,chart.part,chart.axis,chart.minimum.x,chart.minimum.y,chart.maximum.x,chart.maximum.y,chart.vertices.Count,(chart.capId*.61803398875f)%1f));
                File.WriteAllLines(Path.Combine(folder,"RepairCharts.csv"),rows);
                CaptureIds(workspace,folder);
                byte[] png=RebirthEntityRepairAtlasBake.BakeMap(source,charts,512,false,combined||!bodyWrap,bodyWrap);File.WriteAllBytes(Path.Combine(folder,"CandidateAlbedo.png"),png);
                texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false);
                if(!texture.LoadImage(png))throw new InvalidDataException("Candidate albedo decode failed.");
                texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=8;
                RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,"Baseline"),false,true);
                foreach(var pair in originals)
                {
                    var replacements=new Material[pair.Value.Length];
                    for(int i=0;i<replacements.Length;i++){var material=new Material(pair.Value[i]);materials.Add(material);material.mainTexture=texture;replacements[i]=material;}
                    pair.Key.sharedMaterials=replacements;
                }
                RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,"Candidate"),false,true);
                Debug.Log("OUTWARD REPAIR TEXTURE REVIEW: "+charts.Count+" verified charts; no assets or scene saved.");
            }
            finally
            {
                foreach(var pair in originals)pair.Key.sharedMaterials=pair.Value;
                foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);
                foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
                if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);
            }
            if(commit)
            {
                workspace.profile.wrapMissingTorsoTexture=true;workspace.profile.reviewedMaterials=false;
                RebirthEntityRepairAtlasBake.Prepare(workspace);
                RebirthEntityMaterialMaps.Generate(workspace,true);
                workspace.profileBuildFingerprint=RebirthEntityRigBuilder.ComputeProfileFingerprint(workspace.profile,workspace.sourceModelKey);
                EditorUtility.SetDirty(workspace.profile);EditorUtility.SetDirty(workspace);AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save material repair.");
                Debug.Log("BODY WRAP REPAIR COMMITTED: four owned input maps and derived RMOE/variant maps refreshed; material review remains false.");
            }
        }
        static void CaptureIds(RebirthEntityRigWorkspace workspace,string folder)
        {
            var originals=workspace.sourceSkinnedMeshes.ToDictionary(r=>r,r=>(r.sharedMesh,r.sharedMaterials));
            var meshes=new List<Mesh>();var materials=new List<Material>();
            try
            {
                Shader shader=Shader.Find("Unlit/Color");if(shader==null)throw new InvalidOperationException("Unlit debug shader unavailable.");
                foreach(var renderer in workspace.sourceSkinnedMeshes)
                {
                    var input=workspace.surfaceRepairInputs.Single(i=>i.repairedMesh==renderer.sharedMesh);
                    int maximum=input.repairVertexIds.Max();var groups=new List<int>[maximum+1];for(int i=0;i<groups.Length;i++)groups[i]=new List<int>();
                    int[] triangles=renderer.sharedMesh.triangles;
                    for(int t=0;t<triangles.Length;t+=3){int cap=input.repairVertexIds[triangles[t]];groups[cap].Add(triangles[t]);groups[cap].Add(triangles[t+1]);groups[cap].Add(triangles[t+2]);}
                    var mesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);meshes.Add(mesh);mesh.subMeshCount=groups.Length;
                    var slots=new Material[groups.Length];
                    for(int i=0;i<groups.Length;i++){mesh.SetTriangles(groups[i],i);var material=new Material(shader);material.color=i==0?new Color(.25f,.25f,.25f):Color.HSVToRGB((i*.61803398875f)%1f,.8f,1);materials.Add(material);slots[i]=material;}
                    renderer.sharedMesh=mesh;renderer.sharedMaterials=slots;
                }
                RebirthFarmerReviewCapture.CaptureColliderCandidate(workspace,Path.Combine(folder,"CapIds"),false,true);
            }
            finally
            {
                foreach(var pair in originals){pair.Key.sharedMesh=pair.Value.Item1;pair.Key.sharedMaterials=pair.Value.Item2;}
                foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
