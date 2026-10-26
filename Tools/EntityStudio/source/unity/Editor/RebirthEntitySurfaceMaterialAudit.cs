using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntitySurfaceMaterialAudit
    {
        sealed class Stats {public double area,rough,metal,metalHigh,roughLow;public int samples;}
        public static void RunSavedFarmer()
        {
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            var rough=Decode(workspace.profile.sourceRoughness);var metal=Decode(workspace.profile.sourceMetallic);
            var groups=new Dictionary<string,Stats>();
            try
            {
                foreach(var renderer in workspace.sourceSkinnedMeshes)
                {
                    var mesh=renderer.sharedMesh;var points=mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();var uv=mesh.uv;var weights=mesh.boneWeights;var triangles=mesh.triangles;
                    var input=workspace.surfaceRepairInputs.Single(value=>value.repairedMesh==mesh);
                    for(int t=0;t<triangles.Length;t+=3)
                    {
                        int a=triangles[t],b=triangles[t+1],c=triangles[t+2];double area=Vector3.Cross(points[b]-points[a],points[c]-points[a]).magnitude*.5;
                        if(area<1e-12)continue;
                        foreach(var bary in new[]{new Vector3(1f/3,1f/3,1f/3),new Vector3(.6f,.2f,.2f),new Vector3(.2f,.6f,.2f),new Vector3(.2f,.2f,.6f)})
                        {
                            var support=new Dictionary<string,float>();Add(weights[a],bary.x);Add(weights[b],bary.y);Add(weights[c],bary.z);
                            string region=support.OrderByDescending(pair=>pair.Value).First().Key+(input.repairVertexIds[a]>0?"_repair":"_source");
                            if(!groups.TryGetValue(region,out var stats)){stats=new Stats();groups.Add(region,stats);}
                            Vector2 sample=uv[a]*bary.x+uv[b]*bary.y+uv[c]*bary.z;float r=rough.GetPixelBilinear(sample.x,sample.y).r,m=metal.GetPixelBilinear(sample.x,sample.y).r;
                            double share=area*.25;stats.area+=share;stats.rough+=share*r;stats.metal+=share*m;stats.samples++;if(m>.3f)stats.metalHigh+=share;if(r<.4f)stats.roughLow+=share;
                            void Add(BoneWeight w,float blend){Acc(w.boneIndex0,w.weight0*blend);Acc(w.boneIndex1,w.weight1*blend);Acc(w.boneIndex2,w.weight2*blend);Acc(w.boneIndex3,w.weight3*blend);}
                            void Acc(int bone,float amount){if(amount<=0)return;string name=renderer.bones[bone].name;string key=name=="Head"||name=="Neck"||name=="Jaw"?"HeadNeck":name.Contains("Hand")?"Hands":name.Contains("Arm")||name.Contains("Shoulder")?"Arms":name.Contains("Leg")||name.Contains("Foot")||name.Contains("Toe")?"LegsFeet":"TorsoHips";support.TryGetValue(key,out float prior);support[key]=prior+amount;}
                        }
                    }
                }
                var rows=new List<string>{"region,samples,area,meanRoughness,meanMetallic,areaFractionMetalAbovePoint3,areaFractionRoughBelowPoint4"};
                foreach(var pair in groups.OrderBy(pair=>pair.Key)){var s=pair.Value;rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5:R},{6:R}",pair.Key,s.samples,s.area,s.rough/s.area,s.metal/s.area,s.metalHigh/s.area,s.roughLow/s.area));}
                string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerSurfaceMaterials");Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"Regions.csv"),rows);
                Debug.Log("SURFACE MATERIAL AUDIT: "+groups.Count+" area-weighted bone regions; original/repair separated, no assets changed.");
            }
            finally{UnityEngine.Object.DestroyImmediate(rough);UnityEngine.Object.DestroyImmediate(metal);}
        }
        static Texture2D Decode(Texture2D source)
        {if(source==null)throw new InvalidOperationException("Owned PBR map missing.");var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);if(!texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source))))throw new InvalidDataException("Cannot decode PBR map.");return texture;}
    }
}
