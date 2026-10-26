using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio {
 [Serializable] public sealed class FaceControl { public string id,label,group; public float min,max,defaultValue; public string[] negative,positive; }
 [Serializable] public sealed class FaceMorph { public string id; public int[] indices; public float[] deltas; }
 [Serializable] public sealed class FaceValue { public string id; public float value; public FaceValue(string key,float amount){id=key;value=amount;} }
 [Serializable] public sealed class FacePreset { public string name; public float age,masculine; public FaceValue[] values; }
 [Serializable] public sealed class FaceEye {public string name;public int[] indices;}
 [Serializable] public sealed class FaceOralMesh {public string name;public float[] positions,uv,bindingWeights;public int[] triangles,bindingIds;public FaceMorph[] morphs;[NonSerialized]public float[] jawWeights;[NonSerialized]public bool[] enamelVertices;[NonSerialized]public int[] toothSlots;}
 [Serializable] public sealed class FaceOralData {public FaceOralMesh[] meshes;public string[] compatibleHashes;}
 [Serializable] public sealed class FaceExtendedData {public FaceControl[] controls;public FaceMorph[] morphs;public string sourceCommit;public string[] compatibleHashes;}
 [Serializable] public sealed class FaceVertexOffset {public int index;public float x,y,z;}
 [Serializable] public sealed class FaceTemplate {
  public int version; public string sourceCommit,license; public float[] positions,uv;public int[] sourceIds,triangles,faceSizes,faceIndices;
  public FaceControl[] controls;public FaceMorph[] morphs;public FacePreset[] presets;public FaceEye[] eyes;
  [NonSerialized] public string contentHash,oralHash,extensionHash,eyeHash,identityHash;
  [NonSerialized] public FaceOralData oral;
  [NonSerialized] public FaceOralMesh eyeSurface;
  [NonSerialized] public string[] extensionCompatibleHashes,eyeCompatibleHashes;
 }
 [Serializable] public sealed class FaceRecipe {
  public FaceNativeAppearanceSettings nativeAppearance=new FaceNativeAppearanceSettings();public FaceFacialHairSettings facialHair=new FaceFacialHairSettings();public FaceHairSettings hair=new FaceHairSettings();public FaceTeethSettings dentition=new FaceTeethSettings();public FaceSkinSettings skin=new FaceSkinSettings();public FaceEyeSettings eyes=new FaceEyeSettings();
  public FaceBloodSettings blood=new FaceBloodSettings();
  public int version=1;public string sourceCommit,templateHash,oralHash,eyeHash;public string name="Nurse base";
  public string identityHash;public float africanShape,asianShape;public float age=.7f,masculine,forehead=.35f,crowsFeet=.3f,eyeFolds=.3f,nasolabial=.35f;
  public int resolution=1;public FaceValue[] values=new FaceValue[0];
  public string referenceGuid,referenceHairReportGuid,generatedMaterialGuid,generatedEyeMaterialGuid,generatedToothMaterialGuid,generatedLiningMaterialGuid;public float referenceScale=1,referenceX,referenceY,referenceOpacity=.45f;
  public string extensionHash;public bool highDefinitionUv;public float fittedShapeStrength=1;
  public float portraitScale=1,portraitX,portraitY;
  public float dentalArchWidth=1,innerCheekFullness=.8f;
  public float lipWrap=1,tissueClearance=.003f;public int mouthLiningVersion,mouthSupportVersion;
  public float eyeSize=1,upperTeethHeight,lowerTeethHeight,poreDetail=.3f,skinSmoothness=.25f,toothBrightness=.65f;
  public FaceVertexOffset[] fittedOffsets=new FaceVertexOffset[0];
  public float Get(FaceControl control){var item=values.FirstOrDefault(v=>v.id==control.id);return item==null?control.defaultValue:Mathf.Clamp(item.value,control.min,control.max);}
  public void Set(string id,float value){var item=values.FirstOrDefault(v=>v.id==id);if(item!=null)item.value=value;else values=values.Concat(new[]{new FaceValue(id,value)}).ToArray();}
  public static FaceRecipe FromPreset(FaceTemplate template,int index){var p=template.presets[index];return new FaceRecipe{sourceCommit=template.sourceCommit,templateHash=template.contentHash,oralHash=template.oralHash,eyeHash=template.eyeHash,extensionHash=template.extensionHash,name=p.name,age=p.age,masculine=p.masculine,forehead=p.age*.5f,crowsFeet=p.age*.45f,eyeFolds=p.age*.5f,nasolabial=p.age*.5f,values=p.values.Select(v=>new FaceValue(v.id,v.value)).ToArray()};}
  public void Validate(FaceTemplate template){
   if(blood==null)blood=new FaceBloodSettings();blood.Validate();if(nativeAppearance==null)nativeAppearance=new FaceNativeAppearanceSettings();nativeAppearance.Validate();if(facialHair==null)facialHair=new FaceFacialHairSettings();facialHair.Validate();if(hair==null)hair=new FaceHairSettings();if(dentition==null)dentition=new FaceTeethSettings();if(skin==null)skin=new FaceSkinSettings();if(eyes==null)eyes=new FaceEyeSettings();hair.Validate();dentition.Validate();skin.Validate();eyes.Validate();RebirthFaceIdentity.Validate(this,template);if(eyes.mode!=0&&template.eyeSurface==null)throw new InvalidOperationException("Authored eyes require the anatomical eye surface data.");
   if(version!=1)throw new InvalidOperationException("Unsupported face settings version: "+version);
   if(sourceCommit!=template.sourceCommit)throw new InvalidOperationException("These settings use a different anatomical template. Keep the matching FaceGeneratorData folder with the settings.");
   if(templateHash!=template.contentHash)throw new InvalidOperationException("These settings require a different face-data revision. Keep their matching FaceGeneratorData folder; loading them onto this revision would not reproduce the same face.");
   if(!string.IsNullOrEmpty(eyeHash)&&eyeHash!=template.eyeHash&&!(template.eyeCompatibleHashes??new string[0]).Contains(eyeHash))throw new InvalidOperationException("These settings require their matching EyeSurfaceTemplate.json revision.");
   if(!string.IsNullOrEmpty(oralHash)&&oralHash!=template.oralHash&&!(template.oral?.compatibleHashes??new string[0]).Contains(oralHash))throw new InvalidOperationException("These settings require their matching OralTemplate.json revision.");
   if(!string.IsNullOrEmpty(extensionHash)&&extensionHash!=template.extensionHash&&!(template.extensionCompatibleHashes??new string[0]).Contains(extensionHash))throw new InvalidOperationException("These settings require their matching extended target data.");
   if(float.IsNaN(fittedShapeStrength)||float.IsInfinity(fittedShapeStrength))throw new InvalidOperationException("Invalid fitted shape strength.");
   foreach(float value in new[]{dentalArchWidth,innerCheekFullness,lipWrap,tissueClearance,portraitScale,portraitX,portraitY,eyeSize,upperTeethHeight,lowerTeethHeight,poreDetail,skinSmoothness,toothBrightness})if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Invalid portrait or surface setting.");
   if(mouthSupportVersion==0){lipWrap=1;tissueClearance=.003f;mouthSupportVersion=1;}if(mouthSupportVersion!=1)throw new InvalidOperationException("Unsupported mouth support settings.");
   lipWrap=Mathf.Clamp01(lipWrap);if(tissueClearance<=0)tissueClearance=.003f;tissueClearance=Mathf.Clamp(tissueClearance,.001f,.006f);
   if(dentalArchWidth<=0)dentalArchWidth=1;dentalArchWidth=Mathf.Clamp(dentalArchWidth,.8f,1.25f);innerCheekFullness=Mathf.Clamp01(innerCheekFullness);
   if(portraitScale<=0)portraitScale=1;if(eyeSize<=0)eyeSize=1;if(toothBrightness<=0)toothBrightness=.65f;
   eyeSize=Mathf.Clamp(eyeSize,.75f,1.3f);upperTeethHeight=Mathf.Clamp(upperTeethHeight,-1,1);lowerTeethHeight=Mathf.Clamp(lowerTeethHeight,-1,1);poreDetail=Mathf.Clamp(poreDetail,0,2);skinSmoothness=Mathf.Clamp01(skinSmoothness);toothBrightness=Mathf.Clamp(toothBrightness,.1f,1);
   if(values==null)values=new FaceValue[0];
   Func<float,bool> finite=v=>!float.IsNaN(v)&&!float.IsInfinity(v);
   if(!finite(age)||!finite(masculine)||!finite(forehead)||!finite(crowsFeet)||!finite(eyeFolds)||!finite(nasolabial)||!finite(referenceScale)||!finite(referenceX)||!finite(referenceY)||!finite(referenceOpacity)||values.Any(v=>v==null||!finite(v.value)))throw new InvalidOperationException("Settings contain an invalid number.");
   if(values.GroupBy(v=>v.id).Any(g=>g.Count()>1))throw new InvalidOperationException("Settings contain duplicate controls.");
   var known=new HashSet<string>(template.controls.Select(c=>c.id));if(values.Any(v=>!known.Contains(v.id)))throw new InvalidOperationException("Settings contain controls missing from this template.");
   age=Mathf.Clamp01(age);masculine=Mathf.Clamp01(masculine);resolution=Mathf.Clamp(resolution,0,2);forehead=Mathf.Clamp01(forehead);crowsFeet=Mathf.Clamp01(crowsFeet);eyeFolds=Mathf.Clamp01(eyeFolds);nasolabial=Mathf.Clamp01(nasolabial);
   referenceScale=Mathf.Clamp(referenceScale,.4f,2.5f);referenceX=Mathf.Clamp(referenceX,-1,1);referenceY=Mathf.Clamp(referenceY,-1,1);referenceOpacity=Mathf.Clamp01(referenceOpacity);
  }
 }
 public static class RebirthFaceGeneratorModel {
  internal static void Apply(Vector3[] positions,FaceMorph morph,float amount){if(Mathf.Abs(amount)<.00001f)return;for(int i=0;i<morph.indices.Length;i++)positions[morph.indices[i]]+=new Vector3(morph.deltas[i*3],morph.deltas[i*3+1],morph.deltas[i*3+2])*amount;}
  public static Mesh Build(FaceTemplate template,FaceRecipe recipe){
   var p=EvaluateSkinPoints(template,recipe);float f=1-recipe.masculine,a=recipe.age;
   var rawTop=p.Max(v=>v.y);float shift=1.747f-rawTop;for(int i=0;i<p.Length;i++)p[i].y+=shift;
   var eyeCenters=(template.eyes??new FaceEye[0]).Select(e=>e.indices.Aggregate(Vector3.zero,(sum,index)=>sum+p[index])/e.indices.Length).ToArray();
   var positions=p.ToList();var uv=Enumerable.Range(0,p.Length).Select(i=>new Vector2(template.uv[i*2],template.uv[i*2+1])).ToList();var triangles=template.triangles.ToList();
   if(recipe.resolution>0)CatmullClark(template,positions,uv,recipe.resolution,out positions,out uv,out triangles);
   p=positions.ToArray();var normals=Normals(p,triangles);var locators=recipe.resolution==0?new HashSet<int>((template.eyes??new FaceEye[0]).SelectMany(e=>e.indices)):new HashSet<int>();
   for(int i=0;i<p.Length;i++){
    if(locators.Contains(i)||normals[i].z<.2f||p[i].z<.075f||p[i].y<1.52f)continue;
    float displacement=Creases(p[i],recipe)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.65f,normals[i].z));p[i]+=normals[i]*displacement;
   }
   positions=p.ToList();var liningTriangles=new List<int>();var skinTriangles=new List<int>();
   for(int i=0;i<triangles.Count;i+=3){bool lining=true;for(int k=0;k<3;k++){var coord=uv[triangles[i+k]];lining&=coord.x>.75f&&coord.x<.95f&&coord.y<.14f;}var destination=lining?liningTriangles:skinTriangles;destination.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});}
   MouthFrame mouth;liningTriangles=BuildMouthLining(positions,uv,liningTriangles,recipe.innerCheekFullness,out mouth);
   var fitted=new Vector3[template.positions.Length/3];foreach(var correction in recipe.fittedOffsets??new FaceVertexOffset[0])fitted[correction.index]=new Vector3(correction.x,correction.y,correction.z)*Mathf.Clamp(recipe.fittedShapeStrength,0,1.5f);
   var eyeTriangles=new List<int>();
   if(template.eyeSurface!=null){
    var surface=template.eyeSurface;var points=EvaluateAccessory(surface,template,recipe,fitted);var centers=new Vector3[2];var radii=new Vector3[2];
    for(int side=0;side<2;side++){var group=points.Where(v=>(v.x>=0)==(side==0)).ToArray();var min=group.Aggregate(Vector3.positiveInfinity,Vector3.Min);var max=group.Aggregate(Vector3.negativeInfinity,Vector3.Max);centers[side]=(min+max)*.5f;radii[side]=(max-min)*.5f;}
    int start=positions.Count;for(int i=0;i<points.Length;i++){int side=points[i].x>=0?0:1;var center=centers[side];var radius=radii[side];var relative=points[i]-center;float theta=Mathf.Atan2(relative.x/Mathf.Max(radius.x,1e-5f),relative.z/Mathf.Max(radius.z,1e-5f)),phi=Mathf.Asin(Mathf.Clamp(relative.y/Mathf.Max(radius.y,1e-5f),-1,1));uv.Add(recipe.eyes.mode==0?new Vector2((theta/(2*Mathf.PI)+.5f+side)*.5f,phi/Mathf.PI+.5f):new Vector2(surface.uv[i*2],surface.uv[i*2+1]));positions.Add(center+relative*Mathf.Clamp(recipe.eyeSize,.75f,1.3f)+Vector3.up*shift);}eyeTriangles.AddRange(surface.triangles.Select(i=>i+start));
   }else{
   int eyeIndex=0;foreach(var center in eyeCenters){int start=positions.Count;const int cols=24,rows=16;for(int row=0;row<=rows;row++)for(int col=0;col<=cols;col++){float phi=-Mathf.PI*.5f+row*Mathf.PI/rows,theta=(recipe.highDefinitionUv?-Mathf.PI:0)+col*Mathf.PI*2/cols;positions.Add(center+new Vector3(Mathf.Cos(phi)*Mathf.Sin(theta),Mathf.Sin(phi),Mathf.Cos(phi)*Mathf.Cos(theta))*.0115f*Mathf.Clamp(recipe.eyeSize,.75f,1.3f));uv.Add(new Vector2(recipe.highDefinitionUv?(col/(float)cols+eyeIndex)*.5f:col/(float)cols,row/(float)rows));}for(int row=0;row<rows;row++)for(int col=0;col<cols;col++){int a0=start+row*(cols+1)+col,b=a0+1,c=a0+cols+1,d=c+1;eyeTriangles.AddRange(new[]{a0,b,c,b,d,c});}eyeIndex++;}
   }
   var envelope=new OralEnvelope(positions,skinTriangles,mouth);var submeshes=new List<List<int>>{skinTriangles,eyeTriangles};var dentalSupport=new List<Vector3>();float lowerBiteY=float.NaN,lowerTeethFront=0,oralWidthScale=1,lowerJawTranslation=0;
   foreach(var oral in template.oral?.meshes??new FaceOralMesh[0]){
    var points=Enumerable.Range(0,oral.positions.Length/3).Select(i=>new Vector3(oral.positions[i*3],oral.positions[i*3+1],oral.positions[i*3+2])).ToArray();var targets=oral.morphs.ToDictionary(m=>m.id);
    RebirthFaceIdentity.Apply(points,targets,recipe);
    var anatomical=oral.name=="teeth_base"?points.ToArray():null;
    foreach(var control in template.controls){if(IsEarControl(control))continue;float value=recipe.Get(control);foreach(string id in value<0?control.negative:control.positive){FaceMorph target;if(targets.TryGetValue(id,out target))Apply(points,target,Mathf.Abs(value));}}
    if(oral.bindingIds!=null&&oral.bindingWeights!=null&&oral.bindingIds.Length==points.Length*4&&oral.bindingWeights.Length==points.Length*4){for(int i=0;i<points.Length;i++)for(int k=0;k<4;k++){int binding=oral.bindingIds[i*4+k];if(binding>=0&&binding<fitted.Length)points[i]+=fitted[binding]*oral.bindingWeights[i*4+k];}}
    if(oral.name=="teeth_base"){
     PrepareDentalGroups(oral);FitRigidDentalArches(oral,anatomical,points,mouth);var enamel=Enumerable.Range(0,points.Length).Where(i=>oral.enamelVertices[i]).ToArray();
     float front=enamel.Max(i=>points[i].z);var anterior=enamel.Where(i=>points[i].z>front-.012f).ToArray();float left=anterior.Min(i=>points[i].x),right=anterior.Max(i=>points[i].x),center=(left+right)*.5f;
     oralWidthScale=Mathf.Clamp(mouth.halfWidth*.78f/Mathf.Max(.001f,(right-left)*.5f),.85f,1.10f)*recipe.dentalArchWidth;
     float biteCenter=points.Where((v,i)=>oral.enamelVertices[i]).Average(v=>v.y);
     for(int i=0;i<points.Length;i++){points[i].x=mouth.center.x+(points[i].x-center)*oralWidthScale;points[i].y=biteCenter+(points[i].y-biteCenter)*oralWidthScale;points[i].z=mouth.front-.004f+(points[i].z-front)*oralWidthScale;}
     var centralRim=mouth.rim.Where(v=>Mathf.Abs(v.x-mouth.center.x)<mouth.halfWidth*.25f).ToArray();if(centralRim.Length<2)centralRim=mouth.rim;
     float centerTop=centralRim.Max(v=>v.y),centerBottom=centralRim.Min(v=>v.y),opening=Mathf.Max(.0001f,centerTop-centerBottom);var translations=new float[2];
     for(int arch=0;arch<2;arch++){
      bool upper=arch==0;var group=enamel.Where(i=>(oral.jawWeights[i]<.5f)==upper).ToArray();if(group.Length==0)continue;
      float archFront=group.Max(i=>points[i].z);var incisors=group.Where(i=>points[i].z>archFront-.01f&&Mathf.Abs(points[i].x-mouth.center.x)<mouth.halfWidth*.55f).ToArray();if(incisors.Length==0)incisors=group;
      float tip=upper?incisors.Min(i=>points[i].y):incisors.Max(i=>points[i].y);
      float destination=upper?centerTop-shift-Mathf.Min(opening*.62f,.009f)+recipe.upperTeethHeight*.004f:centerBottom-shift+Mathf.Min(opening*.24f,.005f)+recipe.lowerTeethHeight*.004f;
      translations[arch]=destination-tip;
     }
     for(int i=0;i<points.Length;i++)points[i].y+=Mathf.Lerp(translations[0],translations[1],oral.jawWeights[i]);lowerJawTranslation=translations[1];
     var lower=enamel.Where(i=>oral.jawWeights[i]>.5f).Select(i=>points[i]).ToArray();var levels=lower.Select(v=>v.y).OrderBy(v=>v).ToArray();lowerBiteY=levels[levels.Length/2];lowerTeethFront=lower.Max(v=>v.z);
    }
    if(oral.name=="tongue01"&&!float.IsNaN(lowerBiteY)){float center=(points.Min(v=>v.x)+points.Max(v=>v.x))*.5f,tipDepth=points.Max(v=>v.z);for(int i=0;i<points.Length;i++){points[i].x=mouth.center.x+(points[i].x-center)*oralWidthScale;points[i].z=tipDepth+(points[i].z-tipDepth)*Mathf.Sqrt(oralWidthScale);points[i].y+=lowerJawTranslation;}float front=points.Max(v=>v.z);float down=Mathf.Min(0,lowerBiteY-.004f-points.Max(v=>v.y)),back=Mathf.Min(0,lowerTeethFront-.015f-front);for(int i=0;i<points.Length;i++)points[i]+=new Vector3(0,down,back);}
    for(int i=0;i<points.Length;i++)if(oral.name!="teeth_base"||!oral.enamelVertices[i])points[i]=envelope.Contain(points[i]+Vector3.up*shift)-Vector3.up*shift;
    if(oral.name=="teeth_base")dentalSupport.AddRange(points.Where((v,i)=>oral.enamelVertices[i]).Select(v=>v+Vector3.up*shift));
    if(oral.name=="teeth_base")RebirthTeethGenerator.Shape(oral,points,recipe.dentition);
    int start=positions.Count;positions.AddRange(points.Select(v=>v+Vector3.up*shift));uv.AddRange(Enumerable.Range(0,points.Length).Select(i=>new Vector2(oral.uv[i*2],oral.uv[i*2+1])));var oralTriangles=oral.triangles.Select(i=>i+start).ToList();
    if(oral.name=="teeth_base"){var crowns=new HashSet<int>(Enumerable.Range(0,points.Length).Where(i=>oral.enamelVertices[i]).Select(i=>i+start));oralTriangles=RebirthTeethGenerator.Damage(oral,start,positions,uv,recipe.dentition,crowns);oralTriangles=RefineCrowns(positions,uv,oralTriangles,crowns);}
    submeshes.Add(oralTriangles);
   }
   ApplyMouthSupport(positions,skinTriangles,liningTriangles,mouth,dentalSupport.ToArray(),recipe);
   submeshes.Add(liningTriangles);
   var all=submeshes.SelectMany(t=>t).ToList();p=positions.ToArray();var mesh=new Mesh{name="Face_"+recipe.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=p;mesh.uv2=uv.ToArray();if(recipe.highDefinitionUv){foreach(int i in submeshes[0].Concat(liningTriangles).Distinct())uv[i]=PackSkinUv(uv[i]);}mesh.uv=uv.ToArray();mesh.subMeshCount=submeshes.Count;for(int i=0;i<submeshes.Count;i++)mesh.SetTriangles(submeshes[i],i);mesh.normals=Normals(p,all);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
  }
  static Vector3[] EvaluateAccessory(FaceOralMesh surface,FaceTemplate template,FaceRecipe recipe,Vector3[] fitted){
   var points=Enumerable.Range(0,surface.positions.Length/3).Select(i=>new Vector3(surface.positions[i*3],surface.positions[i*3+1],surface.positions[i*3+2])).ToArray();var targets=surface.morphs.ToDictionary(m=>m.id);float f=1-recipe.masculine,a=recipe.age;
   RebirthFaceIdentity.Apply(points,targets,recipe);
   foreach(var control in template.controls){if(IsEarControl(control))continue;float value=recipe.Get(control);foreach(string id in value<0?control.negative:control.positive){FaceMorph target;if(targets.TryGetValue(id,out target))Apply(points,target,Mathf.Abs(value));}}
   if(surface.bindingIds!=null&&surface.bindingWeights!=null&&surface.bindingIds.Length==points.Length*4&&surface.bindingWeights.Length==points.Length*4)for(int i=0;i<points.Length;i++)for(int k=0;k<4;k++){int index=surface.bindingIds[i*4+k];if(index>=0&&index<fitted.Length)points[i]+=fitted[index]*surface.bindingWeights[i*4+k];}
   return points;
  }
  // Ear targets belong to skin. Proxy-transferred ear deltas can contain small
  // unrelated eye/oral movements; accessories must not respond to ear sliders.
  static bool IsEarControl(FaceControl control)=>control.id.StartsWith("l_ear_",StringComparison.Ordinal)||control.id.StartsWith("r_ear_",StringComparison.Ordinal);
  sealed class OralEnvelope {
   const float Cell=.004f;readonly List<Vector3> vertices;readonly List<int> faces;readonly Vector3[] opening;readonly Dictionary<Vector2Int,List<int>> cells=new Dictionary<Vector2Int,List<int>>();
   static Vector2Int Key(Vector3 p){return new Vector2Int(Mathf.FloorToInt(p.x/Cell),Mathf.FloorToInt(p.y/Cell));}
   public OralEnvelope(List<Vector3> points,List<int> triangles,MouthFrame mouth){vertices=points;faces=triangles;opening=mouth.rim;for(int i=0;i<triangles.Count;i+=3){Vector3 a=points[triangles[i]],b=points[triangles[i+1]],c=points[triangles[i+2]];Vector3 min=Vector3.Min(a,Vector3.Min(b,c)),max=Vector3.Max(a,Vector3.Max(b,c));if(max.y<mouth.bottom-.08f||min.y>mouth.top+.05f||max.x<mouth.center.x-mouth.halfWidth*2||min.x>mouth.center.x+mouth.halfWidth*2)continue;var lo=Key(min);var hi=Key(max);for(int x=lo.x;x<=hi.x;x++)for(int y=lo.y;y<=hi.y;y++){var key=new Vector2Int(x,y);List<int> list;if(!cells.TryGetValue(key,out list)){list=new List<int>();cells[key]=list;}list.Add(i);}}}
   public Vector3 Contain(Vector3 p){bool inside=false;if(opening!=null)for(int i=0,j=opening.Length-1;i<opening.Length;j=i++){var a=opening[i];var b=opening[j];if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;}if(inside)return p;List<int> candidates;if(!cells.TryGetValue(Key(p),out candidates))return p;float surface=float.NegativeInfinity;foreach(int i in candidates){Vector3 a=vertices[faces[i]],b=vertices[faces[i+1]],c=vertices[faces[i+2]];float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(det)<1e-10f)continue;float wa=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/det,wb=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/det,wc=1-wa-wb;if(wa>=0&&wb>=0&&wc>=0)surface=Mathf.Max(surface,wa*a.z+wb*b.z+wc*c.z);}if(!float.IsNegativeInfinity(surface))p.z=Mathf.Min(p.z,surface-.002f);return p;}
  }
  static void PrepareDentalGroups(FaceOralMesh oral){
   if(oral.jawWeights!=null&&oral.enamelVertices!=null&&oral.toothSlots!=null)return;int count=oral.positions.Length/3;var parent=Enumerable.Range(0,count).ToArray();var welded=new Dictionary<Vector3Int,int>();
   Func<int,int> root=null;root=i=>{while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;};
   for(int i=0;i<count;i++){var key=Vector3Int.RoundToInt(new Vector3(oral.positions[i*3],oral.positions[i*3+1],oral.positions[i*3+2])*1000000);int first;if(welded.TryGetValue(key,out first))parent[root(i)]=root(first);else welded[key]=i;}
   for(int t=0;t<oral.triangles.Length;t+=3){int first=root(oral.triangles[t]);parent[root(oral.triangles[t+1])]=first;parent[root(oral.triangles[t+2])]=first;}
   var motion=new float[count];var opening=oral.morphs.FirstOrDefault(m=>m.id=="expression/units/caucasian/mouth-open");if(opening==null)throw new InvalidOperationException("The dental template has no jaw-opening motion.");for(int i=0;i<opening.indices.Length;i++)motion[opening.indices[i]]=opening.deltas[i*3+1];
   oral.jawWeights=new float[count];oral.enamelVertices=new bool[count];
   foreach(var group in Enumerable.Range(0,count).GroupBy(i=>root(i))){var ids=group.ToArray();bool tooth=ids.Length<512;float lower=ids.Average(i=>motion[i])<-.003f?1:0;foreach(int i in ids){oral.enamelVertices[i]=tooth;oral.jawWeights[i]=tooth?lower:Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.003f,-.020f,motion[i]));}}
   oral.toothSlots=Enumerable.Repeat(-1,count).ToArray();
   var teeth=Enumerable.Range(0,count).Where(i=>oral.enamelVertices[i]).GroupBy(i=>root(i)).Select(g=>g.ToArray()).ToArray();
   for(int quadrant=0;quadrant<4;quadrant++){
    bool upper=quadrant<2,negative=quadrant%2==0;int rank=0;
    foreach(var tooth in teeth.Where(ids=>(oral.jawWeights[ids[0]]<.5f)==upper&&(ids.Average(i=>oral.positions[i*3])<0)==negative).OrderByDescending(ids=>ids.Average(i=>oral.positions[i*3+2]))){if(rank>=8)throw new InvalidOperationException("Unexpected dental template: more than eight teeth in a quadrant.");foreach(int i in tooth)oral.toothSlots[i]=quadrant*8+rank;rank++;}
   }
  }
  static void FitRigidDentalArches(FaceOralMesh oral,Vector3[] anatomical,Vector3[] deformed,MouthFrame mouth){
   // Facial morphs and image offsets describe soft tissue. They may locate a jaw,
   // but must not stretch crowns or shear individual teeth.
   var centers=new Vector3[2];var origins=new Vector3[2];var rotations=new Quaternion[2];
   for(int arch=0;arch<2;arch++){
    var ids=Enumerable.Range(0,anatomical.Length).Where(i=>oral.enamelVertices[i]&&((oral.jawWeights[i]<.5f)==(arch==0))).ToArray();
    origins[arch]=ids.Aggregate(Vector3.zero,(sum,i)=>sum+anatomical[i])/ids.Length;centers[arch]=ids.Aggregate(Vector3.zero,(sum,i)=>sum+deformed[i])/ids.Length;
    float front=ids.Max(i=>anatomical[i].z),back=ids.Min(i=>anatomical[i].z);var anterior=ids.Where(i=>anatomical[i].z>front-.010f).ToArray();var posterior=ids.Where(i=>anatomical[i].z<back+.010f).ToArray();
    Func<Vector3[],int[],Vector3> mean=(p,group)=>group.Aggregate(Vector3.zero,(sum,i)=>sum+p[i])/group.Length;
    bool upper=arch==0;float frontTip=upper?anterior.Min(i=>anatomical[i].y):anterior.Max(i=>anatomical[i].y),backTip=upper?posterior.Min(i=>anatomical[i].y):posterior.Max(i=>anatomical[i].y);
    float sourcePitch=Mathf.Atan2(frontTip-backTip,mean(anatomical,anterior).z-mean(anatomical,posterior).z)*Mathf.Rad2Deg;
    // Use aperture-driven jaw rotation, not soft-tissue morph distortion.
    float destinationPitch=upper?0:-Mathf.Clamp((mouth.top-mouth.bottom)/.09f*Mathf.Rad2Deg*.35f,0,14);
    rotations[arch]=Quaternion.Euler(sourcePitch-destinationPitch,0,0);
   }
   for(int i=0;i<deformed.Length;i++){float lower=oral.jawWeights[i];Vector3 upper=centers[0]+rotations[0]*(anatomical[i]-origins[0]),bottom=centers[1]+rotations[1]*(anatomical[i]-origins[1]);deformed[i]=Vector3.Lerp(upper,bottom,lower);}
  }
  static List<int> RefineCrowns(List<Vector3> positions,List<Vector2> uv,List<int> triangles,HashSet<int> crowns){
   // Interpolating curved-edge subdivision retains the authored crown landmarks
   // and UV seams; it does not shrink every tooth or soften the gum attachment.
   var normals=Normals(positions.ToArray(),triangles);var edges=new Dictionary<Tuple<int,int>,int>();var result=new List<int>();
   Func<int,int,int> midpoint=(a,b)=>{var key=Tuple.Create(Mathf.Min(a,b),Mathf.Max(a,b));int index;if(edges.TryGetValue(key,out index))return index;
    Vector3 delta=positions[b]-positions[a],point=(positions[a]+positions[b])*.5f-(Vector3.Dot(delta,normals[a])*normals[a]-Vector3.Dot(delta,normals[b])*normals[b])*.125f;
    index=positions.Count;positions.Add(point);uv.Add((uv[a]+uv[b])*.5f);edges[key]=index;return index;};
   for(int t=0;t<triangles.Count;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2];if(!crowns.Contains(a)||!crowns.Contains(b)||!crowns.Contains(c)){result.AddRange(new[]{a,b,c});continue;}int ab=midpoint(a,b),bc=midpoint(b,c),ca=midpoint(c,a);result.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}
   return result;
  }
  struct MouthFrame {public Vector3[] rim;public Vector3 center;public float halfWidth,top,bottom,front;}
  static List<int> BuildMouthLining(List<Vector3> positions,List<Vector2> uv,List<int> original,float fullness,out MouthFrame frame){
   frame=new MouthFrame{halfWidth=.03f,top=1.64f,bottom=1.62f,front=.14f};
   // Recover the welded lip-opening loop before replacing the old cavity.
   var canonical=new Dictionary<int,int>();var welded=new Dictionary<Vector3Int,int>();
   foreach(int i in original.Distinct()){var key=Vector3Int.RoundToInt(positions[i]*1000000);int id;if(!welded.TryGetValue(key,out id)){id=i;welded[key]=i;}canonical[i]=id;}
   var edges=new Dictionary<Tuple<int,int>,int>();for(int t=0;t<original.Count;t+=3)for(int k=0;k<3;k++){int a=canonical[original[t+k]],b=canonical[original[t+(k+1)%3]];var edge=Tuple.Create(Mathf.Min(a,b),Mathf.Max(a,b));int count;edges.TryGetValue(edge,out count);edges[edge]=count+1;}
   var adjacent=new Dictionary<int,List<int>>();foreach(var edge in edges.Where(e=>e.Value==1).Select(e=>e.Key)){if(!adjacent.ContainsKey(edge.Item1))adjacent[edge.Item1]=new List<int>();if(!adjacent.ContainsKey(edge.Item2))adjacent[edge.Item2]=new List<int>();adjacent[edge.Item1].Add(edge.Item2);adjacent[edge.Item2].Add(edge.Item1);}
   if(adjacent.Count<8||adjacent.Any(e=>e.Value.Count!=2))return original;
   var loop=new List<int>();int start=adjacent.Keys.Min(),current=start,previous=-1;
   do{loop.Add(current);var neighbours=adjacent[current];int next=neighbours[0]==previous?neighbours[1]:neighbours[0];previous=current;current=next;if(loop.Count>adjacent.Count)return original;}while(current!=start);
   if(loop.Count!=adjacent.Count)return original;
   var rim=loop.Select(i=>positions[i]).ToArray();var center=rim.Aggregate(Vector3.zero,(sum,p)=>sum+p)/rim.Length;float width=Mathf.Max(.001f,rim.Max(p=>Mathf.Abs(p.x-center.x))),height=Mathf.Max(.001f,rim.Max(p=>Mathf.Abs(p.y-center.y)));
   frame=new MouthFrame{rim=rim,center=center,halfWidth=width,top=rim.Max(p=>p.y),bottom=rim.Min(p=>p.y),front=rim.Max(p=>p.z)};float cavityScale=Mathf.Clamp(width/.03f,.5f,2f);
   // Keep the anatomical vestibule/inner-lip topology. The mouth support pass
   // deforms this and the exterior together after the dental arches are fitted.
   return original;
  }
  static void ApplyMouthSupport(List<Vector3> positions,List<int> skin,List<int> lining,MouthFrame mouth,Vector3[] crowns,FaceRecipe recipe){
   if(mouth.rim==null||crowns.Length==0||recipe.lipWrap<=0)return;
   var anchors=new[]{mouth.rim.OrderBy(p=>p.x).First(),mouth.rim.OrderByDescending(p=>p.x).First()};var offsets=new Vector3[2];
   float front=crowns.Max(p=>p.z),archDepth=front-crowns.Min(p=>p.z);
   // The support rail is on the premolar side of the fitted arch, not the
   // posterior cavity. Its location follows the actual size of the dentition.
   float supportZ=front-archDepth*.50f;
   for(int side=0;side<2;side++){
    var section=crowns.Where(p=>(p.x>=mouth.center.x)==(side==1)&&Mathf.Abs(p.z-supportZ)<archDepth*.08f).ToArray();if(section.Length==0)continue;
    float outer=side==1?section.Max(p=>p.x):section.Min(p=>p.x);float targetX=outer+(side==1?1:-1)*recipe.tissueClearance;
    targetX=side==1?Mathf.Min(targetX,anchors[side].x):Mathf.Max(targetX,anchors[side].x);float targetY=(section.Min(p=>p.y)+section.Max(p=>p.y))*.5f;
    offsets[side]=new Vector3(Mathf.Clamp(targetX-anchors[side].x,-.004f,.004f),Mathf.Clamp(targetY-anchors[side].y,-.004f,.004f),Mathf.Clamp(supportZ-anchors[side].z,-.012f,0))*recipe.lipWrap*Mathf.Lerp(.65f,1,recipe.innerCheekFullness);
   }
   var skinNormals=Normals(positions.ToArray(),skin);var skinSet=new HashSet<int>(skin);
   // A broad C2 falloff distributes corner support through the lip/cheek mass;
   // a separate local push creates dimples and breaks the mucosal attachment.
   foreach(int index in skin.Concat(lining).Distinct()){
    Vector3 p=positions[index],delta=Vector3.zero;
    for(int side=0;side<2;side++){
     var d=p-anchors[side];float radius=Mathf.Max(.030f,mouth.halfWidth*1.45f);float r=Mathf.Sqrt(d.x*d.x+d.y*d.y*1.8f+d.z*d.z*.40f)/radius;
     if(r>=1)continue;float w=Mathf.Pow(1-r,4)*(1+4*r);float lateral=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.75f,Mathf.Abs(p.x-mouth.center.x)/mouth.halfWidth));delta+=offsets[side]*w*lateral;
    }
    if(delta.sqrMagnitude<1e-12f)continue;var q=p+delta;
    if(skinSet.Contains(index)&&skinNormals[index].z>.10f){
     var contact=crowns.Where(v=>Mathf.Abs(v.x-q.x)<.0025f&&Mathf.Abs(v.y-q.y)<.0025f).ToArray();
     if(contact.Length>0)q.z=Mathf.Max(q.z,contact.Max(v=>v.z)+recipe.tissueClearance*.5f);
    }
    positions[index]=q;
   }
  }
  public static Vector2 PackSkinUv(Vector2 uv){
   if(uv.x<.16f&&uv.y>.54f&&uv.y<.64f)return new Vector2(.76f+uv.x/.16f*.22f,.29f+(uv.y-.54f)/.10f*.04f);
   if(uv.x>.63f&&uv.y>.145f&&uv.y<.83f)return new Vector2(.02f+(uv.x-.64f)/.36f*.96f,.34f+(uv.y-.149f)/.67f*.64f);
   if(uv.x>.74f&&uv.y<.145f)return new Vector2(.80f+(uv.x-.75f)/.19f*.18f,.02f+uv.y/.14f*.26f);
   if(uv.x>.19f&&uv.x<.58f&&uv.y>.79f)return new Vector2(.02f+(uv.x-.20f)/.37f*.72f,.02f+(uv.y-.80f)/.18f*.26f);
   return uv;
  }
  public static Vector3[] EvaluateSkinPoints(FaceTemplate template,FaceRecipe recipe){
   recipe.Validate(template);var p=Enumerable.Range(0,template.positions.Length/3).Select(i=>new Vector3(template.positions[i*3],template.positions[i*3+1],template.positions[i*3+2])).ToArray();
   var morphs=template.morphs.ToDictionary(m=>m.id);float f=1-recipe.masculine,a=recipe.age;
   RebirthFaceIdentity.Apply(p,morphs,recipe);
   
   foreach(var c in template.controls){float value=recipe.Get(c);foreach(string id in value<0?c.negative:c.positive)Apply(p,morphs[id],Mathf.Abs(value));}
   foreach(var delta in recipe.fittedOffsets??new FaceVertexOffset[0]){if(delta.index<0||delta.index>=p.Length||float.IsNaN(delta.x)||float.IsNaN(delta.y)||float.IsNaN(delta.z)||float.IsInfinity(delta.x)||float.IsInfinity(delta.y)||float.IsInfinity(delta.z))throw new InvalidOperationException("Invalid reference shape correction.");p[delta.index]+=new Vector3(delta.x,delta.y,delta.z)*Mathf.Clamp(recipe.fittedShapeStrength,0,1.5f);}
   
   return p;
  }
  static Vector3[] Normals(Vector3[] p,List<int> triangles){
   var sums=new Dictionary<Vector3Int,Vector3>();var keys=p.Select(v=>Vector3Int.RoundToInt(v*1000000)).ToArray();
   for(int i=0;i<triangles.Count;i+=3){int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var n=Vector3.Cross(p[b]-p[a],p[c]-p[a]);foreach(int id in new[]{a,b,c}){Vector3 value;sums.TryGetValue(keys[id],out value);sums[keys[id]]=value+n;}}
   return keys.Select(key=>{Vector3 n;if(!sums.TryGetValue(key,out n))return Vector3.forward;float len=n.magnitude;return len>1e-12f?n/len:Vector3.forward;}).ToArray();
  }
  sealed class Poly {public int[] v;public Vector2[] uv;public Poly(int[] indices,Vector2[] coords){v=indices;uv=coords;}}
  sealed class Edge {public int a,b,index;public System.Collections.Generic.List<int> faces=new System.Collections.Generic.List<int>();}
  static ulong EdgeKey(int a,int b){return ((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);}
  internal static void CatmullClark(FaceTemplate template,List<Vector3> original,List<Vector2> originalUV,int levels,out List<Vector3> output,out List<Vector2> outputUV,out List<int> triangles){
   var lookup=new Dictionary<int,int>();var geo=new List<Vector3>();var remap=new int[original.Count];
   for(int i=0;i<original.Count;i++){int id;if(!lookup.TryGetValue(template.sourceIds[i],out id)){id=geo.Count;lookup.Add(template.sourceIds[i],id);geo.Add(original[i]);}remap[i]=id;}
   var polys=new List<Poly>();int offset=0;foreach(int size in template.faceSizes){var ids=new int[size];var coords=new Vector2[size];for(int i=0;i<size;i++){int source=template.faceIndices[offset++];ids[i]=remap[source];coords[i]=originalUV[source];}polys.Add(new Poly(ids,coords));}
   for(int level=0;level<levels;level++){
    var facePoints=polys.Select(f=>f.v.Aggregate(Vector3.zero,(sum,id)=>sum+geo[id])/f.v.Length).ToArray();var edges=new Dictionary<ulong,Edge>();var incident=Enumerable.Range(0,geo.Count).Select(i=>new System.Collections.Generic.List<int>()).ToArray();
    for(int fi=0;fi<polys.Count;fi++){var f=polys[fi];for(int j=0;j<f.v.Length;j++){int a=f.v[j],b=f.v[(j+1)%f.v.Length];incident[a].Add(fi);ulong key=EdgeKey(a,b);Edge edge;if(!edges.TryGetValue(key,out edge)){edge=new Edge{a=a,b=b};edges.Add(key,edge);}edge.faces.Add(fi);}}
    var neighbor=Enumerable.Range(0,geo.Count).Select(i=>new System.Collections.Generic.List<Edge>()).ToArray();foreach(var edge in edges.Values){neighbor[edge.a].Add(edge);neighbor[edge.b].Add(edge);}
    var next=new List<Vector3>(geo.Count+polys.Count+edges.Count);
    for(int i=0;i<geo.Count;i++){var boundary=neighbor[i].Where(e=>e.faces.Count==1).ToArray();if(boundary.Length==2){next.Add(geo[i]*.75f+(geo[boundary[0].a==i?boundary[0].b:boundary[0].a]+geo[boundary[1].a==i?boundary[1].b:boundary[1].a])*.125f);continue;}int n=incident[i].Count;if(n==0){next.Add(geo[i]);continue;}var f=incident[i].Aggregate(Vector3.zero,(sum,id)=>sum+facePoints[id])/n;var r=neighbor[i].Aggregate(Vector3.zero,(sum,e)=>sum+(geo[e.a]+geo[e.b])*.5f)/neighbor[i].Count;next.Add((f+2*r+(n-3)*geo[i])/n);}
    int faceStart=next.Count;next.AddRange(facePoints);foreach(var edge in edges.Values){edge.index=next.Count;next.Add(edge.faces.Count==2?(geo[edge.a]+geo[edge.b]+facePoints[edge.faces[0]]+facePoints[edge.faces[1]])*.25f:(geo[edge.a]+geo[edge.b])*.5f);}
    var result=new List<Poly>(polys.Count*4);for(int fi=0;fi<polys.Count;fi++){var f=polys[fi];var center=f.uv.Aggregate(Vector2.zero,(sum,value)=>sum+value)/f.uv.Length;for(int j=0;j<f.v.Length;j++){int following=(j+1)%f.v.Length,previous=(j+f.v.Length-1)%f.v.Length;result.Add(new Poly(new[]{f.v[j],edges[EdgeKey(f.v[j],f.v[following])].index,faceStart+fi,edges[EdgeKey(f.v[previous],f.v[j])].index},new[]{f.uv[j],(f.uv[j]+f.uv[following])*.5f,center,(f.uv[previous]+f.uv[j])*.5f}));}}
    geo=next;polys=result;
   }
   output=new List<Vector3>();outputUV=new List<Vector2>();triangles=new List<int>();var corners=new Dictionary<Tuple<int,Vector2>,int>();foreach(var f in polys){var ids=new int[f.v.Length];for(int j=0;j<ids.Length;j++){var key=Tuple.Create(f.v[j],f.uv[j]);int id;if(!corners.TryGetValue(key,out id)){id=output.Count;corners.Add(key,id);output.Add(geo[f.v[j]]);outputUV.Add(f.uv[j]);}ids[j]=id;}for(int j=1;j<ids.Length-1;j++)triangles.AddRange(new[]{ids[0],ids[j],ids[j+1]});}
  }
  // Authored crease paths on the anatomical surface. These are editable geometry,
  // not albedo brightness converted into relief and not a substitute for a detail bake.
  static float Stroke(Vector2 p,Vector2 a,Vector2 b,float width,float depth){var edge=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,edge)/Mathf.Max(1e-10f,edge.sqrMagnitude));float d=Vector2.Distance(p,a+edge*t)/width;float taper=Mathf.Sin(Mathf.PI*t);return depth*taper*(-Mathf.Exp(-d*d*2)+.28f*Mathf.Exp(-Mathf.Pow((d-1.3f)/.65f,2)));}
  static float Creases(Vector3 p,FaceRecipe r){
   Vector2 q=new Vector2(p.x,p.y);float value=0;
   foreach(float y in new[]{1.675f,1.688f,1.700f}){float d=Mathf.Abs(q.y-y-1.6f*q.x*q.x)/.0014f,taper=Mathf.Clamp01(1-q.x*q.x/(.043f*.043f));value+=.0008f*r.forehead*taper*(-Mathf.Exp(-2*d*d)+.28f*Mathf.Exp(-Mathf.Pow((d-1.3f)/.65f,2)));}
   foreach(float side in new[]{-1f,1f}){
    value+=Stroke(q,new Vector2(side*.005f,1.644f),new Vector2(side*.008f,1.672f),.0016f,.0010f*r.forehead);
    foreach(float y in new[]{1.618f,1.623f})value+=Stroke(q,new Vector2(side*.012f,y+.003f),new Vector2(side*.044f,y),.0016f,.0009f*r.eyeFolds);
    foreach(float dy in new[]{-.010f,0,.009f})value+=Stroke(q,new Vector2(side*.041f,1.636f),new Vector2(side*.057f,1.636f+dy),.0013f,.0009f*r.crowsFeet);
    value+=Stroke(q,new Vector2(side*.014f,1.605f),new Vector2(side*.029f,1.575f),.0020f,.0015f*r.nasolabial);
    value+=Stroke(q,new Vector2(side*.026f,1.573f),new Vector2(side*.031f,1.552f),.0018f,.0011f*r.nasolabial);
   }return value;
  }
 }
}




