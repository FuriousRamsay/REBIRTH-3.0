using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;using UnityEngine.Rendering;
namespace RebirthEntityStudio {
 public sealed class BodyDeepWoundPart {public Mesh mesh;public Material material;public string name,surfaceRole;public bool? useBoneBloodCoverage;}
 public sealed class BodyDeepWoundAssembly:IDisposable {
  public readonly List<BodyDeepWoundPart> parts=new List<BodyDeepWoundPart>();public Texture2D field;public BodyWoundResources resources;public RebirthTorsoAnatomyLibrary torsoResources;
  public readonly List<Texture2D> tissueCutFloors=new List<Texture2D>();
  public void Draw(PreviewRenderUtility preview){foreach(var part in parts)RebirthNativePreviewLighting.Draw(preview,part.mesh,Matrix4x4.identity,part.material,0);}
  public void Export(GameObject root,Material skin,BodyRecipe saved,string folder){
   if(parts.Count==0)return;
   saved.damage.deepLibraryGuid=resources.Export(folder);if(torsoResources!=null)saved.damage.torsoLibraryGuid=torsoResources.Export(folder);
   var library=BodyWoundResources.Read(saved.damage.deepLibraryGuid);var textures=RebirthWoundSurfaceTextures.Export(saved.damage,folder);
   var copy=UnityEngine.Object.Instantiate(field);copy.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(copy,folder+"/DeepOpeningField.asset");skin.SetTexture("_DeepSDF",copy);EditorUtility.SetDirty(skin);
   var floorCopies=new Dictionary<Texture,Texture>();foreach(var floor in tissueCutFloors){var exported=UnityEngine.Object.Instantiate(floor);exported.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(exported,folder+"/"+floor.name+".asset");floorCopies.Add(floor,exported);}
   foreach(var part in parts){
    var mesh=UnityEngine.Object.Instantiate(part.mesh);mesh.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(mesh,folder+"/"+part.name+".asset");
    var material=new Material(part.material){hideFlags=HideFlags.None};material.SetTexture("_DeepSDF",copy);
    if(material.HasProperty("_TissueCutFloor")){var originalFloor=part.material.GetTexture("_TissueCutFloor");Texture exportedFloor;if(originalFloor!=null&&floorCopies.TryGetValue(originalFloor,out exportedFloor))material.SetTexture("_TissueCutFloor",exportedFloor);}
    Texture mapped;var original=part.material.GetTexture("_TissueTex");material.SetTexture("_TissueTex",original!=null&&textures.TryGetValue(original,out mapped)?mapped:library.flesh);
    if(material.HasProperty("_SmoothnessTex")){var originalSmoothness=part.material.GetTexture("_SmoothnessTex");if(originalSmoothness!=null&&textures.TryGetValue(originalSmoothness,out mapped))material.SetTexture("_SmoothnessTex",mapped);}
    RebirthFaceAppearance.CopyShader(material,folder);AssetDatabase.CreateAsset(material,folder+"/"+part.name+".mat");
    var child=new GameObject(part.name);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
   }
  }
  public void Dispose(){foreach(var part in parts){if(part.mesh!=null)UnityEngine.Object.DestroyImmediate(part.mesh);if(part.material!=null)UnityEngine.Object.DestroyImmediate(part.material);}parts.Clear();foreach(var floor in tissueCutFloors)if(floor!=null)UnityEngine.Object.DestroyImmediate(floor);tissueCutFloors.Clear();if(field!=null)UnityEngine.Object.DestroyImmediate(field);field=null;}
 }
 public static class RebirthBodyDeepWounds {
  public const int Maximum=4;
  static Vector3 V(float[] p,int offset=0){if(p==null||p.Length<offset+3)throw new InvalidDataException("Invalid wound bone vector");var v=new Vector3(p[offset],p[offset+1],p[offset+2]);if(float.IsNaN(v.sqrMagnitude)||float.IsInfinity(v.sqrMagnitude))throw new InvalidDataException("Invalid wound bone coordinate");return v;}
  static Shader InteriorShader(){string path=AssetDatabase.FindAssets("BodyDeepInterior t:Shader").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/Shaders/BodyDeepInterior.shader",StringComparison.Ordinal));var shader=path==null?null:AssetDatabase.LoadAssetAtPath<Shader>(path);if(shader==null)throw new FileNotFoundException("Install the deep wound interior shader");return shader;}
  public static BodyDeepWound Place(Mesh body,Ray ray,float height){BodyWoundHit hit;if(!new BodyWoundSurface(body).Pick(ray,out hit))throw new InvalidOperationException("Click the visible body surface");if(hit.point.y/height>.88f)throw new InvalidOperationException("Place body wounds below the head boundary");return new BodyDeepWound{uv=hit.uv,referencePoint=hit.point/height,referenceNormal=hit.normal};}
  static Mesh Create(Vector3[] positions,int[] triangles,string name){var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};mesh.vertices=positions;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
  static void Bind(Material material,int index,Vector3 center,Vector3 right,Vector3 up,Vector3 normal,Vector3 radii){material.SetVector("_DeepCenter"+index,new Vector4(center.x,center.y,center.z,1));material.SetVector("_DeepX"+index,right/radii.x);material.SetVector("_DeepY"+index,up/radii.y);material.SetVector("_DeepZ"+index,normal/radii.z);}
  static Material Interior(Texture2D field,Texture2D flesh,Vector3 center,Vector3 right,Vector3 up,Vector3 normal,Vector3 radii,bool bone,BodyDeepWound wound){var material=new Material(InteriorShader()){name=bone?"Exposed anatomical tibia":"Wound tissue lining",hideFlags=HideFlags.HideAndDontSave};Bind(material,0,center,right,up,normal,radii);material.SetTexture("_DeepSDF",field);material.SetTexture("_TissueTex",flesh);material.SetFloat("_ClipInside",bone?1:2);material.SetFloat("_UseTissue",bone?0:1);material.SetColor("_Color",bone?new Color(.78f,.74f,.64f)*wound.boneTint:new Color(.7f,.7f,.7f)*wound.tissueTint);material.SetFloat("_TileSize",wound.tissueTileSize);material.SetFloat("_Smoothness",bone?wound.boneSmoothness:wound.tissueSmoothness);return material;}
  static Mesh CreateLining(BodyWoundSurface local,Texture2D field,BodyWoundHit hit,Vector3 right,Vector3 up,Vector3 normal,Vector3 radii,BodyDeepWound wound,string name){
   const int columns=49,rows=97;
   var vertices=new Vector3[columns*rows];var supported=new bool[vertices.Length];var distances=new float[vertices.Length];
   for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){
    int index=y*columns+x;float u=x/(float)(columns-1),v=y/(float)(rows-1);
    float distance=field.GetPixelBilinear(u,v).r;distances[index]=distance;
    Vector3 plane=hit.point+right*((u*2-1)*radii.x)+up*((v*2-1)*radii.y);BodyWoundHit sample;
    supported[index]=local.Pick(new Ray(plane+normal*.2f,-normal),out sample);
    if(!supported[index]){
     if(distance>0)throw new InvalidOperationException("Wound footprint extends beyond the skin; reduce size or move it");
     // Unreferenced placeholder at a known skin point; never invent a sampled
     // skin intersection for the unused exterior of the opening rectangle.
     vertices[index]=hit.point;continue;
    }
    float depth=.0003f+(wound.depth-.0003f)*Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/.09f));vertices[index]=sample.point-normal*depth;
   }
   // Reject an unsupported corner adjacent to an opening vertex as well: do
   // not remove triangles that are needed to line the visible wound boundary.
   for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)if(!supported[y*columns+x])
    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
     int xx=x+dx,yy=y+dy;if(xx>=0&&xx<columns&&yy>=0&&yy<rows&&distances[yy*columns+xx]>0)throw new InvalidOperationException("Wound boundary approaches unsupported skin; reduce size or move it");
    }
   var triangles=new List<int>();
   for(int y=0;y<rows-1;y++)for(int x=0;x<columns-1;x++){
    int a=y*columns+x,b=a+1,c=a+columns,d=c+1;
    if(supported[a]&&supported[b]&&supported[d])triangles.AddRange(new[]{a,b,d});
    if(supported[a]&&supported[d]&&supported[c])triangles.AddRange(new[]{a,d,c});
   }
   return Create(vertices,triangles.ToArray(),name);
  }
  static Vector3[] FitBone(BodyWoundResources resources,BodyTemplate template,BodyRecipe recipe,BodyWoundHit hit,out int[] triangles){
   var data=resources.bone;if(data==null||data.version!=1||data.license!="CC-BY-4.0"||data.bones==null||data.bones.Length!=2||data.foundationHash!=recipe.authoredBodyHash)throw new InvalidOperationException("Bone exposure currently requires the reviewed surface-bound muscle foundation");var joints=RebirthBodyAnatomicalJoints.Evaluate(template,recipe);BodyWoundBone chosen=null;float score=float.PositiveInfinity;Vector3 ankle=Vector3.zero,knee=Vector3.zero;foreach(var bone in data.bones){Vector3 a=joints["joint-"+bone.side+"-ankle"],k=joints["joint-"+bone.side+"-knee"],axis=k-a;float t=Vector3.Dot(hit.point-a,axis)/axis.sqrMagnitude;if(t<data.minimumShankFraction||t>data.maximumShankFraction)continue;float distance=Vector3.Distance(hit.point,a+axis*t);if(distance<score){score=distance;chosen=bone;ankle=a;knee=k;}}if(chosen==null||score>.09f*recipe.height/data.referenceHeight)throw new InvalidOperationException("Bone exposure currently supports the middle of either shin; use tissue-only wounds elsewhere");if(chosen.positions==null||chosen.positions.Length%3!=0||chosen.triangles==null||chosen.triangles.Any(i=>i<0||i>=chosen.positions.Length/3))throw new InvalidDataException("Invalid anatomical tibia mesh");Vector3 sourceAnkle=V(chosen.sourceAnkle),sourceUp=V(chosen.sourceUp),sourceRight=V(chosen.sourceRight),sourceForward=V(chosen.sourceForward),up=(knee-ankle).normalized,forward=Vector3.ProjectOnPlane(Vector3.forward,up).normalized,right=Vector3.Cross(up,forward);float axial=(knee-ankle).magnitude/(V(chosen.sourceKnee)-sourceAnkle).magnitude,transverse=chosen.referenceTransverseScale*recipe.height/data.referenceHeight;Vector3 shift=-forward*(data.posteriorOffset*recipe.height/data.referenceHeight);var points=new Vector3[chosen.positions.Length/3];for(int i=0;i<points.Length;i++){Vector3 p=V(chosen.positions,i*3)-sourceAnkle;points[i]=ankle+right*(Vector3.Dot(p,sourceRight)*transverse)+up*(Vector3.Dot(p,sourceUp)*axial)+forward*(Vector3.Dot(p,sourceForward)*transverse)+shift;}triangles=chosen.triangles;return points;
  }
  public static BodyDeepWoundAssembly Build(BodyTemplate template,BodyRecipe recipe,Mesh body,Material skin){
   Vector3[] torsoCage=null;
   var result=new BodyDeepWoundAssembly();try{var settings=recipe.damage;var wounds=settings.deepWounds??new BodyDeepWound[0];for(int i=0;i<Maximum;i++)if(skin.HasProperty("_DeepCenter"+i))skin.SetVector("_DeepCenter"+i,Vector4.zero);if(wounds.Length==0)return result;if(wounds.Length>Maximum)throw new InvalidDataException("At most four deep wounds are supported");if(!skin.HasProperty("_DeepCenter0"))throw new InvalidOperationException("Install the body shader with deep wound support");result.resources=BodyWoundResources.Read(settings.deepLibraryGuid);result.field=result.resources.CreateField();var source=new BodyWoundSurface(body);var hits=wounds.Select(w=>source.Locate(w,recipe.height)).ToArray();
    for(int i=0;i<wounds.Length;i++){var wound=wounds[i];wound.Validate();var hit=hits[i];if(hit.point.y/recipe.height>.88f)throw new InvalidOperationException("Deep body wounds cannot cross the head boundary");float span=Mathf.Max(wound.width,wound.height)*.5f;for(int j=0;j<i;j++)if(Vector3.Dot(hit.normal,hits[j].normal)>.25f&&Vector3.Distance(hit.point,hits[j].point)<span+Mathf.Max(wounds[j].width,wounds[j].height)*.5f)throw new InvalidOperationException("Deep wounds are too close: move or resize them");var local=source.Local(hit.point,span*2+wound.depth+.03f);BodyWoundHit back;if(!source.Pick(new Ray(hit.point-hit.normal*.0005f,-hit.normal),out back)||Vector3.Distance(back.point,hit.point)<wound.depth+.004f)throw new InvalidOperationException("Wound depth exceeds local body thickness; reduce depth");Vector3 normal=hit.normal,right=Vector3.Cross(Mathf.Abs(Vector3.Dot(normal,Vector3.up))>.95f?Vector3.forward:Vector3.up,normal).normalized,up=Vector3.Cross(normal,right);var rotate=Quaternion.AngleAxis(wound.rotation,normal);right=rotate*right;up=rotate*up;Vector3 radii=new Vector3(wound.width*.5f,wound.height*.5f,wound.depth*.5f+.002f),center=hit.point-normal*(wound.depth*.5f);
     string name="DeepWound_"+(i+1);var lining=CreateLining(local,result.field,hit,right,up,normal,radii,wound,name+"_Lining");
     result.parts.Add(new BodyDeepWoundPart{name=name+"_Lining",mesh=lining,material=Interior(result.field,result.resources.flesh,center,right,up,normal,radii,false,wound)});
     if(wound.torsoInterior!=TorsoWoundInterior.None){
      var wall=RebirthWoundContourWall.Build(local,result.field,lining,hit.point,right,up,normal,new Vector2(radii.x,radii.y),name+"_Wall");
      var wallMaterial=Interior(result.field,result.resources.flesh,center,right,up,normal,radii,false,wound);wallMaterial.SetFloat("_ClipInside",0);
      result.parts.Add(new BodyDeepWoundPart{name=name+"_Wall",mesh=wall,material=wallMaterial});
     }
     if(wound.exposeBone){int[] indices;var bonePoints=FitBone(result.resources,template,recipe,hit,out indices);int visible=0;foreach(var point in bonePoints){Vector3 delta=point-center;float x=Vector3.Dot(delta,right)/radii.x,y=Vector3.Dot(delta,up)/radii.y,z=Vector3.Dot(delta,normal)/radii.z;if(Mathf.Abs(x)>=1||Mathf.Abs(y)>=1||Mathf.Abs(z)>=1||result.field.GetPixelBilinear(x*.5f+.5f,y*.5f+.5f).r<=0)continue;visible++;if(local.SignedDistance(point)>.001f)throw new InvalidOperationException("Bone protrudes through this edited skin shape; disable bone exposure or adjust the body/wound");}if(visible==0)throw new InvalidOperationException("No tibia intersects this wound; move it toward the front of the shin or increase depth");result.parts.Add(new BodyDeepWoundPart{name=name+"_Bone",mesh=Create(bonePoints,indices,name+"_Bone"),material=Interior(result.field,result.resources.flesh,center,right,up,normal,radii,true,wound)});}
     if(wound.torsoInterior!=TorsoWoundInterior.None){if(result.torsoResources==null){result.torsoResources=RebirthTorsoAnatomyCache.Acquire(template,recipe);torsoCage=RebirthTorsoAnatomyCache.Cage(template,recipe,result.torsoResources);}RebirthTorsoWoundInterior.Append(result,result.torsoResources,torsoCage,body,wound,i,(int)wound.torsoInterior);}
     foreach(var part in result.parts.Where(p=>p.name.StartsWith(name+"_",StringComparison.Ordinal))){RebirthWoundSurfaceTextures.Apply(part,settings,wound);bool bone=part.useBoneBloodCoverage??(part.material.GetFloat("_UseTissue")<.5f);RebirthBloodAppearance.ApplyInterior(part.material,settings,bone?wound.boneBloodCoverage:wound.bloodCoverage);RebirthWoundApertureOcclusion.Apply(part.mesh,result.field,hit.point,right,up,normal,new Vector2(radii.x,radii.y));part.material.SetFloat("_UseApertureOcclusion",1);}
     Bind(skin,i,center,right,up,normal,radii);
    }skin.SetTexture("_DeepSDF",result.field);return result;
   }catch{result.Dispose();throw;}
  }
 }
}
