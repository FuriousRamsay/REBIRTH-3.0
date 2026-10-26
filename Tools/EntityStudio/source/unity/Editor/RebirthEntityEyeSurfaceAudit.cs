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
    public static class RebirthEntityEyeSurfaceAudit
    {
        [Serializable] sealed class Capture {public Node[] transforms;}
        [Serializable] sealed class Node {public string name;public Vector3 localPosition,localScale;public Quaternion localRotation;}
        public static void RunSavedFarmer()
        {
            string[] args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-entityStudioEyePose");if(arg<0||arg+1>=args.Length)throw new ArgumentException("Captured pose JSON required.");
            var scene=EditorSceneManager.OpenScene("Assets/EntityStudioGenerated/RebirthFarmer/RebirthFarmer_RigReview.unity");
            var workspace=scene.GetRootGameObjects().Select(go=>go.GetComponent<RebirthEntityRigWorkspace>()).Single(ws=>ws!=null);
            bool skinned=Array.IndexOf(args,"-skinEyePatch")>=0;
            if(skinned){workspace=UnityEngine.Object.Instantiate(workspace.gameObject).GetComponent<RebirthEntityRigWorkspace>();workspace.profile=UnityEngine.Object.Instantiate(workspace.profile);}
            var eye=workspace.generatedSkeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="FeralEye");
            Texture emission=eye.sharedMaterial.GetTexture("_EmissionMap");var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            if(emission==null||!image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(emission))))throw new InvalidDataException("Owned eye emission image unavailable.");
            if(skinned)RebirthEntityEyeGeometry.Build(workspace,new RebirthEntityGeneratedMaps{eyeEmission=(Texture2D)emission},false);
            string folder=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerEyeSurface"+(skinned?"_Skinned":eye.bones.Length>1?"_SavedSkinned":""));Directory.CreateDirectory(folder);
            var rows=new List<string>{"pose,vertex,u,v,emissionR,emissionG,emissionB,signedBodyDepth,hitBody"};
            var transforms=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true);
            var saved=transforms.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            try
            {
                foreach(string pose in new[]{"rest","elbows","knees","shoulders","neckTurn","crouch","reach","captured"})
                {
                    foreach(var pair in saved){pair.Key.localPosition=pair.Value.Item1;pair.Key.localRotation=pair.Value.Item2;pair.Key.localScale=pair.Value.Item3;}
                    if(pose=="elbows"||pose=="reach")foreach(string side in new[]{"Left","Right"})Rotate(side+"ForeArm",new Vector3(70,0,0));
                    if(pose=="knees")foreach(string side in new[]{"Left","Right"})Rotate(side+"Leg",new Vector3(75,0,0));
                    if(pose=="shoulders"||pose=="reach"){Rotate("LeftArm",new Vector3(25,0,-35));Rotate("RightArm",new Vector3(25,0,35));}
                    if(pose=="neckTurn")Rotate("Neck",new Vector3(20,45,0));
                    if(pose=="crouch")foreach(string side in new[]{"Left","Right"}){Rotate(side+"UpLeg",new Vector3(-45,0,0));Rotate(side+"Leg",new Vector3(90,0,0));}
                    if(pose=="captured")
                    {
                        var capture=JsonUtility.FromJson<Capture>(File.ReadAllText(args[arg+1]));
                        foreach(var spec in workspace.profile.bones)
                        {
                            var node=capture.transforms.SingleOrDefault(n=>n.name==spec.name);var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,spec.name);
                            if(node==null||bone==null)continue;bone.localPosition=node.localPosition;bone.localRotation=node.localRotation;bone.localScale=node.localScale;
                        }
                    }
                    Vector3[] eyePoints=Skin(eye),eyeNormals=SkinNormals(eye);Vector2[] uv=eye.sharedMesh.uv;
                    var bodyPoints=new List<Vector3>();var bodyTriangles=new List<int>();
                    foreach(var renderer in workspace.sourceSkinnedMeshes){int offset=bodyPoints.Count;bodyPoints.AddRange(Skin(renderer));foreach(int index in renderer.sharedMesh.triangles)bodyTriangles.Add(offset+index);}
                    int emitting=0,buried=0,hits=0;float maximumDepth=float.NegativeInfinity,minimumDepth=float.PositiveInfinity,maxEmission=0;
                    for(int i=0;i<eyePoints.Length;i++)
                    {
                        Color color=image.GetPixelBilinear(uv[i].x,uv[i].y);maxEmission=Mathf.Max(maxEmission,color.maxColorComponent);
                        Vector3 normal=eyeNormals[i];
                        Vector3 origin=eyePoints[i]+normal*.05f;float nearest=.1f;bool hit=false;
                        for(int t=0;t<bodyTriangles.Count;t+=3)
                            if(Ray(origin,-normal,bodyPoints[bodyTriangles[t]],bodyPoints[bodyTriangles[t+1]],bodyPoints[bodyTriangles[t+2]],out float distance)&&distance<nearest){nearest=distance;hit=true;}
                        float depth=hit?.05f-nearest:float.NaN;
                        if(color.r>.01f){emitting++;if(hit){hits++;maximumDepth=Mathf.Max(maximumDepth,depth);minimumDepth=Mathf.Min(minimumDepth,depth);if(depth>.0001f)buried++;}}
                        rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8}",pose,i,uv[i].x,uv[i].y,color.r,color.g,color.b,depth,hit));
                    }
                    Debug.Log("EYE SURFACE AUDIT "+pose+" emittingVertices="+emitting+" emittingRayHits="+hits+" buriedEmittingVertices="+buried+" minSignedBodyDepth="+minimumDepth+" maxSignedBodyDepth="+maximumDepth+" maxMapSample="+maxEmission);
                }
                File.WriteAllLines(Path.Combine(folder,"EyeSurface.csv"),rows);
            }
            finally
            {
                foreach(var pair in saved){pair.Key.localPosition=pair.Value.Item1;pair.Key.localRotation=pair.Value.Item2;pair.Key.localScale=pair.Value.Item3;}UnityEngine.Object.DestroyImmediate(image);
                if(skinned)
                {
                    var meshes=workspace.generatedSkeleton.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.CompareTag("E_BP_Eye")).Select(r=>r.sharedMesh).Distinct().ToArray();
                    var materials=workspace.rendererTierMaterials.Where(set=>set.renderer.CompareTag("E_BP_Eye")).SelectMany(set=>set.variants).Distinct().ToArray();
                    UnityEngine.Object.DestroyImmediate(workspace.profile);UnityEngine.Object.DestroyImmediate(workspace.gameObject);
                    foreach(var mesh in meshes)if(!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);foreach(var material in materials)if(!EditorUtility.IsPersistent(material))UnityEngine.Object.DestroyImmediate(material);
                }
            }
            void Rotate(string name,Vector3 angles){var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,name);if(bone==null)throw new InvalidOperationException("Missing eye pose bone "+name);bone.localRotation*=Quaternion.Euler(angles);}
        }
        static Vector3[] Skin(SkinnedMeshRenderer renderer)
        {
            Mesh mesh=renderer.sharedMesh;Vector3[] points=mesh.vertices;BoneWeight[] weights=mesh.boneWeights;Matrix4x4[] binds=mesh.bindposes;
            var matrices=new Matrix4x4[renderer.bones.Length];for(int i=0;i<matrices.Length;i++)matrices[i]=renderer.bones[i].localToWorldMatrix*binds[i];
            for(int i=0;i<points.Length;i++){BoneWeight w=weights[i];Vector3 p=points[i];points[i]=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;}return points;
        }
        static Vector3[] SkinNormals(SkinnedMeshRenderer renderer)
        {
            Mesh mesh=renderer.sharedMesh;Vector3[] normals=mesh.normals;BoneWeight[] weights=mesh.boneWeights;Matrix4x4[] binds=mesh.bindposes;
            var matrices=new Matrix4x4[renderer.bones.Length];for(int i=0;i<matrices.Length;i++)matrices[i]=(renderer.bones[i].localToWorldMatrix*binds[i]).inverse.transpose;
            for(int i=0;i<normals.Length;i++){BoneWeight w=weights[i];Vector3 n=normals[i];normals[i]=(matrices[w.boneIndex0].MultiplyVector(n)*w.weight0+matrices[w.boneIndex1].MultiplyVector(n)*w.weight1+matrices[w.boneIndex2].MultiplyVector(n)*w.weight2+matrices[w.boneIndex3].MultiplyVector(n)*w.weight3).normalized;}return normals;
        }
        static bool Ray(Vector3 origin,Vector3 direction,Vector3 a,Vector3 b,Vector3 c,out float distance)
        {
            Vector3 edge=b-a,other=c-a,p=Vector3.Cross(direction,other);float determinant=Vector3.Dot(edge,p);distance=0;if(Mathf.Abs(determinant)<1e-9f)return false;
            float inverse=1/determinant;Vector3 delta=origin-a;float u=Vector3.Dot(delta,p)*inverse;if(u<0||u>1)return false;
            Vector3 q=Vector3.Cross(delta,edge);float v=Vector3.Dot(direction,q)*inverse;if(v<0||u+v>1)return false;
            distance=Vector3.Dot(other,q)*inverse;return distance>=0;
        }
    }
}
