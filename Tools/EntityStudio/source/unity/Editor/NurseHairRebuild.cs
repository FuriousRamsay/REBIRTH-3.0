using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class NurseHairRebuild {
 const string Native="Assets/EntityStudioGenerated/RebirthNurseNative",Folder="Assets/EntityStudioGenerated/RebirthNurseHairRebuild";
 static int quality;static bool renderViews=true;
 public static BoneWeight PinNonHair(BoneWeight w,Transform[] bones){int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] values={w.weight0,w.weight1,w.weight2,w.weight3};float total=0;for(int i=0;i<4;i++){if(bones[ids[i]].name.StartsWith("Hair_"))values[i]=0;total+=values[i];}if(total<.00001f)return new BoneWeight{boneIndex0=Array.FindIndex(bones,b=>b.name=="Head"),weight0=1};return new BoneWeight{boneIndex0=ids[0],weight0=values[0]/total,boneIndex1=ids[1],weight1=values[1]/total,boneIndex2=ids[2],weight2=values[2]/total,boneIndex3=ids[3],weight3=values[3]/total};}
 public static GameObject BuildPreview(int lod,bool render=false){quality=lod;renderViews=render;return Execute();}
 static readonly List<Vector3> points=new List<Vector3>();static readonly List<Vector2> uv=new List<Vector2>();static readonly List<int> triangles=new List<int>();
 static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float u=1-t;return a*u*u*u+b*3*u*u*t+c*3*u*t*t+d*t*t*t;}
 static void Triangle(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
 static float SmoothRoot(float t){return Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.20f));}
 static float ScalpY(float x,float z){return 1.623f+.124f*Mathf.Sqrt(Mathf.Max(.01f,1-x*x/(.080f*.080f)-(z-.005f)*(z-.005f)/(.082f*.082f)));}
 static Vector3 Guide(int card,float t){
  bool rear=card>=40;float side=card<20?-1:1;int index=rear?card-40:card%20;float phase=card*1.731f;
  float z=rear?-.055f:Mathf.Lerp(.065f,-.065f,index/19f);float rx=rear?Mathf.Lerp(-.070f,.070f,index/19f):side*.006f;
  Vector3 a,b,c,d;if(rear){a=new Vector3(rx,ScalpY(rx,z)-.003f,z);b=new Vector3(rx,a.y-.020f,-.078f);c=new Vector3(rx*1.1f,1.625f,-.088f);d=new Vector3(rx*1.2f,1.575f,-.090f);}else{a=new Vector3(rx,ScalpY(rx,z)-.003f,z);b=new Vector3(side*.042f,ScalpY(side*.042f,z)+.001f,z);c=new Vector3(side*.077f,1.655f,z+.008f);d=new Vector3(side*Mathf.Lerp(.081f,.095f,index/19f),1.600f,z+.012f);}
  if(t<=.44f)return Bezier(a,b,c,d,t/.44f);
  float h=(t-.44f)/.56f;float wave=Mathf.Sin(h*5.5f+phase)*.010f*Mathf.Sin(h*Mathf.PI);float length=.245f+.042f*(.5f+.5f*Mathf.Sin(card*2.17f));
  var p=d+new Vector3(rear?wave:side*(wave-.012f*h*Mathf.Max(0,z/.065f)),-length*h,rear?wave:Mathf.Sin(h*4.8f+phase)*.009f*Mathf.Sin(h*Mathf.PI));p.x+=Mathf.Sin(phase)*.004f*h;return p;
 }
 static Texture2D Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true);t.LoadImage(File.ReadAllBytes(path));return t;}
 static float Luma(Color c){return c.r*.2126f+c.g*.7152f+c.b*.0722f;}
 static Color HairSurface(Texture2D source,float u,float v,bool normal){var centre=source.GetPixelBilinear(u,v);if(!normal){float roughness=.64f+.12f*(1-Mathf.Clamp01(Luma(centre)*2));return new Color(roughness,roughness,roughness,1);}if(centre.a<.8f)return new Color(.5f,.5f,1,1);float d=1f/2048;float dx=Luma(source.GetPixelBilinear(u-d,v))-Luma(source.GetPixelBilinear(u+d,v))-(Luma(source.GetPixelBilinear(u-d*4,v))-Luma(source.GetPixelBilinear(u+d*4,v)))*.25f;float dy=Luma(source.GetPixelBilinear(u,v-d))-Luma(source.GetPixelBilinear(u,v+d))-(Luma(source.GetPixelBilinear(u,v-d*4))-Luma(source.GetPixelBilinear(u,v+d*4)))*.25f;var n=new Vector3(dx*4,dy*4,1).normalized;return new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);}
 static void Composite(string name,string oldPath,bool cards){int size=name=="HairAlbedo"||name=="HairNormal"?2048:512;var source=Read(NurseVisualOptimization.Out+"/HairCards_Albedo_Working.png");var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){float u=(x+.5f)/(size/2),v=(y+.5f)/size;Color value;if(cards){value=source.GetPixelBilinear(x<size/2?u:u-1,v);}else value=name=="HairOcclusion"?Color.white:HairSurface(source,x<size/2?u:u-1,v,name=="HairNormal");pixels[y*size+x]=value;}var t=new Texture2D(size,size,TextureFormat.RGBA32,false,true);t.SetPixels(pixels);t.Apply();string path=Folder+"/"+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=size;importer.alphaIsTransparency=cards;importer.sRGBTexture=cards;importer.textureType=name=="HairNormal"?TextureImporterType.NormalMap:TextureImporterType.Default;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();}
 public static void Preview(){try{BuildPreview(0,true);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void ReviewWithFace(){NurseFaceSculpt.Run();NurseEyeAlignment.Run();Preview();}
 static GameObject Execute(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Directory.CreateDirectory(Folder);points.Clear();uv.Clear();triangles.Clear();
  var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Native+"/RebirthNurse_Authoring.prefab"));go.GetComponent<Animator>().enabled=false;go.GetComponent<LODGroup>().ForceLOD(0);RebirthEntityStudio.RebirthNurseNativeShaderPreview.Bind(go);
  var body=go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="LOD0");if(quality>0)body.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Native+"/RebirthNurse_LOD"+quality+"_0.asset");var old=body.sharedMesh;var world=NurseAnatomyRender.WorldSkin(body);var wp=world.vertices;var weights=old.boneWeights;var oldUV=old.uv;var hairIndices=old.GetTriangles(1).Distinct().ToArray();int head=Array.FindIndex(body.bones,b=>b.name=="Head");
  if(head<0||hairIndices.Length<100)throw new Exception("Missing hair/head source");
  // New smooth anatomical scalp, independent of the old faceted outer hair volume.
  int around=quality==0?48:quality==1?24:12,rows=quality==0?16:quality==1?8:4,panelColumns=around/4;for(int panel=0;panel<4;panel++)for(int row=0;row<=rows;row++)for(int col=0;col<=panelColumns;col++){
   float theta=(panel*panelColumns+col)*Mathf.PI*2/around,front=Mathf.Max(0,Mathf.Cos(theta)),maximum=Mathf.Lerp(2.00f,.95f,Mathf.Pow(front,3));float polar=.0001f+(maximum-.0001f)*row/rows;
   var p=new Vector3(.080f*Mathf.Sin(polar)*Mathf.Sin(theta),1.623f+.124f*Mathf.Cos(polar),.005f+.082f*Mathf.Sin(polar)*Mathf.Cos(theta));
   points.Add(p);float across=col/(float)panelColumns;uv.Add(new Vector2(Mathf.Lerp(.32f,.68f,panel%2==0?across:1-across)/8f,.99f-row/(float)rows*.94f));
  }
  for(int panel=0;panel<4;panel++)for(int row=0;row<rows;row++)for(int col=0;col<panelColumns;col++){int a=panel*(rows+1)*(panelColumns+1)+row*(panelColumns+1)+col,b=a+1,c=a+panelColumns+1,d=c+1;Triangle(a,c,b);Triangle(b,c,d);}
  int scalpVertices=points.Count;
  // Sixty individually curved locks, with a central part, side sweeps and a distinct rear layer.
  int locks=quality==0?60:quality==1?30:12,segments=quality==0?20:quality==1?10:6;for(int card=0;card<locks;card++){int guide=card*60/locks;int start=points.Count;for(int step=0;step<=segments;step++){
   float t=step/(float)segments;var centre=Guide(guide,t);var tangent=(Guide(guide,Mathf.Min(1,t+.001f))-Guide(guide,Mathf.Max(0,t-.001f))).normalized;
   var outward=guide>=40?Vector3.back:new Vector3(guide<20?-1:1,0,Mathf.Lerp(.90f,-.90f,(guide%20)/19f)).normalized;var across=Vector3.Cross(tangent,outward).normalized;
   float width=(.032f+.008f*(.5f+.5f*Mathf.Sin(card*2.71f)))*Mathf.Lerp(.12f,1,SmoothRoot(t));width*=Mathf.Lerp(1,.68f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,t)));
   width*=quality==0?1:quality==1?1.35f:2;for(int cross=0;cross<3;cross++){float f=cross*.5f;points.Add(centre+across*((f-.5f)*width)+outward*(cross==1?.0025f:0));float u=(guide%4+Mathf.Lerp(.018f,.982f,f))/4f;uv.Add(new Vector2(.5f+u*.5f,1-t));}
  }for(int step=0;step<segments;step++)for(int cross=0;cross<2;cross++){int a=start+step*3+cross,b=a+1,c=a+3,d=c+1;Triangle(a,b,c);Triangle(b,d,c);}}
  var posed=new Mesh{name="RebirthNurse_RebuiltHair_LOD0",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};posed.vertices=points.ToArray();posed.uv=uv.ToArray();posed.triangles=triangles.ToArray();posed.RecalculateNormals();var scalpNormals=posed.normals;for(int i=0;i<scalpVertices;i++){var p=points[i];scalpNormals[i]=new Vector3(p.x/(.080f*.080f),(p.y-1.623f)/(.124f*.124f),(p.z-.005f)/(.082f*.082f)).normalized;}posed.normals=scalpNormals;posed.RecalculateTangents();
  var vertices=posed.vertices;var normals=posed.normals;var tangents=posed.tangents;var bw=new BoneWeight[vertices.Length];float error=0;
  for(int i=0;i<vertices.Length;i++){
   var desired=vertices[i];var weight=new BoneWeight{boneIndex0=head,weight0=1};if(i>=scalpVertices){int step=((i-scalpVertices)%(3*(segments+1)))/3;float t=step/(float)segments,strength=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.44f,t))*.95f;float f=Mathf.Clamp((1.65f-desired.y)/.075f,0,4);int a=Mathf.Min(3,Mathf.FloorToInt(f));float blend=f-a;int first=Array.FindIndex(body.bones,b=>b.name=="Hair_Back_Left"+(a==0?"":a.ToString())),second=Array.FindIndex(body.bones,b=>b.name=="Hair_Back_Left"+(a+1));if(first<0||second<0)throw new Exception("Native hair chain is missing");weight=new BoneWeight{boneIndex0=head,weight0=1-strength,boneIndex1=first,weight1=strength*(1-blend),boneIndex2=second,weight2=strength*blend};}bw[i]=weight;
   int[] ids={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};float[] ws={weight.weight0,weight.weight1,weight.weight2,weight.weight3};var skin=Matrix4x4.zero;for(int k=0;k<4;k++){var matrix=body.bones[ids[k]].localToWorldMatrix*old.bindposes[ids[k]];for(int a=0;a<4;a++)for(int b=0;b<4;b++)skin[a,b]+=matrix[a,b]*ws[k];}
   vertices[i]=skin.inverse.MultiplyPoint3x4(desired);error=Mathf.Max(error,Vector3.Distance(desired,skin.MultiplyPoint3x4(vertices[i])));normals[i]=skin.inverse.MultiplyVector(normals[i]).normalized;var tan=skin.inverse.MultiplyVector(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z)).normalized;tangents[i]=new Vector4(tan.x,tan.y,tan.z,tangents[i].w);
  }
  posed.vertices=vertices;posed.normals=normals;posed.tangents=tangents;posed.boneWeights=bw;posed.bindposes=old.bindposes;posed.RecalculateBounds();if(error>.00001f)throw new Exception("Hair bind mismatch");
  if(quality==0){Composite("HairAlbedo",NurseVisualOptimization.Folder+"/Atlas_Albedo.png",true);Composite("HairNormal",NurseVisualOptimization.Folder+"/Atlas_Normal.png",false);Composite("HairSpecGloss",NurseVisualOptimization.Folder+"/Atlas_HairSpecGloss.png",false);Composite("HairOcclusion",NurseVisualOptimization.Folder+"/Atlas_HairOcclusion.png",false);}
  var material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/NativeReferences/Moe/Material/HD_MoeHair.mat"));material.name="EntityStudioHair_RebirthNurse_Rebuilt";material.shader=RebirthEntityStudio.RebirthNativeShaderLibrary.Get("Game/Autodesk");foreach(var pair in new[]{new[]{"_MainTex","HairAlbedo"},new[]{"_BumpMap","HairNormal"},new[]{"_SpecGlossMap","HairSpecGloss"},new[]{"_OcclusionMap","HairOcclusion"}})material.SetTexture(pair[0],AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+pair[1]+".png"));material.SetColor("_Color",Color.white);material.SetFloat("_Cutoff",.5f);material.SetFloat("_BumpScale",.75f);material.SetFloat("_GlossMapScale",.2f);material.SetFloat("_Glossiness",.78f);material.SetFloat("_SpecularHighlights",0);material.SetFloat("_GlossyReflections",0);
  string meshPath=Folder+"/Hair_LOD"+quality+".asset",matPath=Folder+"/Hair.mat";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existing==null)AssetDatabase.CreateAsset(posed,meshPath);else{EditorUtility.CopySerialized(posed,existing);EditorUtility.SetDirty(existing);posed=existing;}var existingMaterial=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(existingMaterial==null)AssetDatabase.CreateAsset(material,matPath);else{EditorUtility.CopySerialized(material,existingMaterial);EditorUtility.SetDirty(existingMaterial);material=existingMaterial;}AssetDatabase.SaveAssets();
  // Rebind after saving: the native compiled shader is a live bundle object, not a project shader asset.
  material=new Material(material);material.shader=RebirthEntityStudio.RebirthNativeShaderLibrary.Get("Game/Autodesk");material.shaderKeywords=new[]{"_ALPHATEST_ON","_GLOSSYREFLECTIONS_OFF","_NORMALMAP","_SPECGLOSSMAP","_SPECULARHIGHLIGHTS_OFF"};
  var hidden=UnityEngine.Object.Instantiate(old);hidden.SetTriangles(new int[0],1);
  var retained=new List<int>();var bodyIndices=old.GetTriangles(0);var ownAlbedo=Read(NurseVisualOptimization.Out+"/Atlas_Albedo.png");var legacyHair=new bool[wp.Length];
  foreach(int id in bodyIndices.Distinct()){
   var p=wp[id];var color=ownAlbedo.GetPixelBilinear(oldUV[id].x,oldUV[id].y);bool blue=color.b>color.r*1.15f&&color.g>color.r*1.03f;
   var w=weights[id];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] values={w.weight0,w.weight1,w.weight2,w.weight3};float hairWeight=0,headWeight=0;for(int k=0;k<4;k++){string name=body.bones[ids[k]].name;if(name.StartsWith("Hair_"))hairWeight+=values[k];if(name=="Head")headWeight+=values[k];}
   bool face=p.y>1.47f&&p.y<1.72f&&Mathf.Abs(p.x)<.073f&&p.z>.025f;bool outside=p.y>1.24f&&(Mathf.Abs(p.x)>.075f||p.z<-.055f);
   bool blond=color.r>color.b*1.20f&&color.g>color.b*1.08f&&color.r<color.g*1.3f;
   bool oldCrown=p.y>1.707f&&headWeight>.5f;
   legacyHair[id]=!blue&&(oldCrown||(!face&&outside&&(hairWeight>.025f||headWeight>.5f||(blond&&p.y<1.55f&&Mathf.Abs(p.x)<.24f))));
  }
  for(int i=0;i<bodyIndices.Length;i+=3){int a=bodyIndices[i],b=bodyIndices[i+1],c=bodyIndices[i+2];if((legacyHair[a]?1:0)+(legacyHair[b]?1:0)+(legacyHair[c]?1:0)<2)retained.AddRange(new[]{a,b,c});}hidden.SetTriangles(retained,0);
  RepairConcealedGarment(go,body,world,hairIndices.Concat(Enumerable.Range(0,legacyHair.Length).Where(i=>legacyHair[i])).Distinct().ToArray());body.sharedMesh=hidden;var child=new GameObject("RebuiltHairPreview");child.transform.SetParent(body.transform,false);var renderer=child.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=posed;renderer.sharedMaterial=material;renderer.bones=body.bones;renderer.rootBone=body.rootBone;renderer.updateWhenOffscreen=true;
  if(renderViews)foreach(int angle in new[]{0,90,180,270}){go.transform.rotation=Quaternion.Euler(0,angle,0);NurseVisualOptimization.Render(go,"Rebuilt_Hair_"+angle,0,true,true);}
  File.WriteAllText(NurseVisualOptimization.Out+"/hair-rebuild-review.txt","New smooth scalp and 60 curved three-vertex-wide locks, central part/side sweeps/rear layer; old outer hair submesh removed in preview. New triangles="+posed.triangles.Length/3+"; vertices="+posed.vertexCount+"; bind error="+error+"m. Native cutout shader/keywords, owned 2K albedo/normal and 512 auxiliary maps. Existing rig weights transferred to loose lengths, scalp Head-pinned. Candidate only: LOD integration and visual review pending; no mod or main-project installation.\n");Debug.Log("NURSE_REBUILT_HAIR_REVIEW_READY");
  return go;
 }
 static void RepairConcealedGarment(GameObject go,SkinnedMeshRenderer body,Mesh world,int[] hairIndices){
  var original=body.sharedMesh;var positions=world.vertices;var sourceNormals=world.normals;var sourceUV=original.uv;var sourceWeights=original.boneWeights;var raw=Read(NurseVisualOptimization.Out+"/Atlas_Albedo.png");
  Func<Vector2,bool> isBlue=coord=>{var c=raw.GetPixelBilinear(coord.x,coord.y);return c.b>c.r*1.2f&&c.g>c.r*1.06f;};
  Func<int,bool> isTorso=i=>{var w=sourceWeights[i];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] amounts={w.weight0,w.weight1,w.weight2,w.weight3};float head=0;for(int k=0;k<4;k++){string name=body.bones[ids[k]].name;if(name=="Head"||name=="Jaw"||name=="Neck"||name.StartsWith("Hair_"))head+=amounts[k];}return head<.1f;};
  var blue=original.GetTriangles(0).Distinct().Where(i=>positions[i].y>1.20f&&positions[i].y<1.56f&&Mathf.Abs(positions[i].x)<.31f&&isBlue(sourceUV[i])&&isTorso(i)).ToArray();
  File.WriteAllLines(NurseVisualOptimization.Out+"/cloth-top-anchors.csv",new[]{"x,y,z,bone"}.Concat(blue.OrderByDescending(i=>positions[i].y).Take(60).Select(i=>positions[i].x+","+positions[i].y+","+positions[i].z+","+body.bones[sourceWeights[i].boneIndex0].name)));
  var removed=hairIndices.Where(i=>positions[i].y>1.20f&&positions[i].y<1.57f).Select(i=>positions[i]).ToArray();if(blue.Length<20)throw new Exception("Too few intact cloth anchors");
  Vector2 ua=Vector2.zero,ub=Vector2.zero,uc=Vector2.zero;float bestArea=0;var indices=original.GetTriangles(0);
  for(int i=0;i<indices.Length;i+=3){int a=indices[i],b=indices[i+1],c=indices[i+2];var centre=(positions[a]+positions[b]+positions[c])/3;if(centre.y<1.20f||centre.y>1.48f||Mathf.Abs(centre.x)>.25f||!isBlue(sourceUV[a])||!isBlue(sourceUV[b])||!isBlue(sourceUV[c])||!isBlue((sourceUV[a]+sourceUV[b]+sourceUV[c])/3))continue;
   var d=sourceUV[b]-sourceUV[a];var e=sourceUV[c]-sourceUV[a];float area=Mathf.Abs(d.x*e.y-d.y*e.x);if(area>bestArea){bestArea=area;ua=sourceUV[a];ub=sourceUV[b];uc=sourceUV[c];}}
  if(bestArea<=0)throw new Exception("No safe intact cloth texture triangle");
  var pv=new List<Vector3>();var pn=new List<Vector3>();var pu=new List<Vector2>();var pw=new List<BoneWeight>();var triangles=new List<int>();int columns=quality==0?48:quality==1?24:12,rows=quality==0?24:quality==1?16:8;
  foreach(int facing in new[]{1,-1}){
   int offset=pv.Count;var anchors=blue.Where(i=>positions[i].z*facing>0).ToArray();if(anchors.Length<12)throw new Exception("Cloth support has no front/back anchors");
   for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++){
    float x=Mathf.Lerp(-.26f,.26f,col/(float)columns);float top=Mathf.Min(1.51f,anchors.Max(i=>positions[i].y-2f*(positions[i].x-x)*(positions[i].x-x)))-.004f;float y=Mathf.Lerp(1.24f,Mathf.Max(1.25f,top),row/(float)rows);
    var nearest=anchors.OrderBy(i=>(positions[i].x-x)*(positions[i].x-x)+(positions[i].y-y)*(positions[i].y-y)).Take(10).ToArray();float total=0,z=0;var normal=Vector3.zero;
    foreach(int i in nearest){float distance=(positions[i].x-x)*(positions[i].x-x)+(positions[i].y-y)*(positions[i].y-y),w=1/(distance+.0001f);z+=positions[i].z*w;normal+=sourceNormals[i]*w;total+=w;}z=z/total-facing*.006f;
    pv.Add(new Vector3(x,y,z));normal.Normalize();if(Vector3.Dot(normal,Vector3.forward*facing)<0)normal=-normal;pn.Add(normal);pw.Add(PinNonHair(sourceWeights[nearest[0]],body.bones));
    float a=.15f+.25f*(.5f+.5f*Mathf.Sin(x*60)),b=.15f+.25f*(.5f+.5f*Mathf.Cos(y*55));pu.Add(ua*a+ub*b+uc*(1-a-b));
   }
   for(int row=0;row<rows;row++)for(int col=0;col<columns;col++){
    int a=offset+row*(columns+1)+col,b=a+1,c=a+columns+1,d=c+1;var centre=(pv[a]+pv[b]+pv[c]+pv[d])*.25f;float width=Mathf.Lerp(.15f,.26f,Mathf.InverseLerp(1.24f,1.49f,centre.y));if(Mathf.Abs(centre.x)>width)continue;
    if(facing>0&&centre.y>1.335f&&Mathf.Abs(centre.x)<Mathf.Clamp01((centre.y-1.335f)/.15f)*.078f)continue;
    if(!removed.Any(p=>(p-centre).sqrMagnitude<.055f*.055f))continue;
    if(facing>0)triangles.AddRange(new[]{a,b,c,b,d,c});else triangles.AddRange(new[]{a,c,b,b,c,d});
   }
  }
  var matrices=body.bones.Select((bone,i)=>bone.localToWorldMatrix*original.bindposes[i]).ToArray();var vertices=new Vector3[pv.Count];var normals=new Vector3[pv.Count];
  for(int i=0;i<pv.Count;i++){var w=pw[i];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] amounts={w.weight0,w.weight1,w.weight2,w.weight3};var skin=Matrix4x4.zero;for(int k=0;k<4;k++)for(int a=0;a<4;a++)for(int b=0;b<4;b++)skin[a,b]+=matrices[ids[k]][a,b]*amounts[k];vertices[i]=skin.inverse.MultiplyPoint3x4(pv[i]);normals[i]=skin.inverse.MultiplyVector(pn[i]).normalized;}
  var mesh=new Mesh{name="NurseConcealedGarmentSupport",vertices=vertices,normals=normals,uv=pu.ToArray(),boneWeights=pw.ToArray(),bindposes=original.bindposes,triangles=triangles.ToArray()};mesh.RecalculateTangents();mesh.RecalculateBounds();
  var child=new GameObject("ConcealedGarmentSupport");child.transform.SetParent(body.transform,false);var renderer=child.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=body.bones;renderer.rootBone=body.rootBone;renderer.sharedMaterial=body.sharedMaterials[0];renderer.updateWhenOffscreen=true;
  File.WriteAllText(NurseVisualOptimization.Out+"/hair-concealed-garment-support.txt","Local support only near removed fused hair; fitted from intact front/back cloth anchors, 6 mm inset; V-neck opening excluded. Triangles="+triangles.Count/3+"; cloth anchors="+blue.Length+"; removed-hair samples="+removed.Length+". UVs remain inside one verified blue fabric triangle; original body geometry not edited by support generation. Candidate only.\n");
 }
}
