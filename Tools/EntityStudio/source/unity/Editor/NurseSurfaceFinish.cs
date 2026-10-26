using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class NurseSurfaceFinish {
 public const string MeshBaselinePrefix="BeforeHairWidthV16_";
 const string Out=NurseVisualOptimization.Out;const string Folder=NurseVisualOptimization.Folder;
 static Texture2D Read(string p){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(p));return t;}
 static float S(float a,float b,float v){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));}
 static Vector2 Project(Vector3 p,float angle,bool body){p=Quaternion.Euler(0,angle,0)*p;return new Vector2(.5f-p.x/(body?2.04f:.62f),.5f+(p.y-(body?.91f:1.53f))/(body?2.04f:.62f));}
 static void Raster(Vector2 a,Vector2 b,Vector2 c,int size,Action<int,int,float,float,float> visit){float d=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(d)<1e-8)return;int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,size-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,size-1),y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,size-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,size-1);for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){float u=((b.y-c.y)*(x+.5f-c.x)+(c.x-b.x)*(y+.5f-c.y))/d,v=((c.y-a.y)*(x+.5f-c.x)+(a.x-c.x)*(y+.5f-c.y))/d,w=1-u-v;if(u>=-1e-5&&v>=-1e-5&&w>=-1e-5)visit(x,y,u,v,w);}}
 static float[] Blur(float[] source,int size,int radius){var temp=new float[source.Length];var result=new float[source.Length];float scale=1f/(radius*2+1);for(int y=0;y<size;y++){float sum=0;for(int x=0;x<=radius;x++)sum+=source[y*size+x];for(int x=0;x<size;x++){temp[y*size+x]=sum*scale;if(x-radius>=0)sum-=source[y*size+x-radius];if(x+radius+1<size)sum+=source[y*size+x+radius+1];}}for(int x=0;x<size;x++){float sum=0;for(int y=0;y<=radius;y++)sum+=temp[y*size+x];for(int y=0;y<size;y++){result[y*size+x]=sum*scale;if(y-radius>=0)sum-=temp[(y-radius)*size+x];if(y+radius+1<size)sum+=temp[(y+radius+1)*size+x];}}return result;}
 static void Save(Color[] p,int n,string path){var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.SetPixels(p);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
 public static void Run(){try{
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EntityStudioGenerated/RebirthNurseNative/RebirthNurse_Authoring.prefab"));var r=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(x=>x.name=="LOD0");r.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Blender_20000.asset");var mesh=r.sharedMesh;var p=mesh.vertices.Select(r.transform.TransformPoint).ToArray();var norms=mesh.normals.Select(n=>r.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();var uv=mesh.uv;var tri=mesh.triangles;
  int n=2048;var original=Read(Out+"/Atlas_Albedo.png");var pixels=original.GetPixels();var feral=Read(Out+"/Atlas_Albedo_Feral.png").GetPixels();var oldPixels=(Color[])pixels.Clone();var face=Read(Out+"/Face_DistinctFemale_V1.png");var hair=new[]{0,90,180,270}.Select(a=>Read(Out+"/Hair_NoBow_"+a+".png")).ToArray();var cloth=new[]{0,180}.Select(a=>Read(Out+"/Body_Restored_"+a+".png")).ToArray();var painted=new float[n*n];var faceMask=new float[n*n];var faceStatistics=new bool[n*n];var hairMask=new float[n*n];var skinMask=new float[n*n];var occupied=new bool[n*n];var bowMask=new float[n*n];
  // Complete original surface controls visibility for every projection.
  var high=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Original_LOD0.asset");var hp=high.vertices.Select(r.transform.TransformPoint).ToArray();var ht=high.triangles;var depths=new Dictionary<string,float[]>();
  foreach(bool body in new[]{false,true})foreach(int angle in body?new[]{0,180}:new[]{0,90,180,270}){var depth=Enumerable.Repeat(float.NegativeInfinity,1024*1024).ToArray();var rot=Quaternion.Euler(0,angle,0);for(int j=0;j<ht.Length;j+=3){int a=ht[j],b=ht[j+1],c=ht[j+2];if(!body&&Mathf.Max(hp[a].y,Mathf.Max(hp[b].y,hp[c].y))<1.20f)continue;Raster(Project(hp[a],angle,body)*1024,Project(hp[b],angle,body)*1024,Project(hp[c],angle,body)*1024,1024,(x,y,u,v,w)=>{int i=y*1024+x;depth[i]=Mathf.Max(depth[i],(rot*(hp[a]*u+hp[b]*v+hp[c]*w)).z);});}depths[(body?"B":"H")+angle]=depth;}
  for(int j=0;j<tri.Length;j+=3){int a=tri[j],b=tri[j+1],c=tri[j+2];Raster(uv[a]*n,uv[b]*n,uv[c]*n,n,(x,y,u,v,w)=>{
   int i=y*n+x;occupied[i]=true;var pos=p[a]*u+p[b]*v+p[c]*w;faceStatistics[i]=pos.y>1.652f&&pos.y<1.695f&&Mathf.Abs(pos.x)<.040f&&pos.z>.045f;var normal=(norms[a]*u+norms[b]*v+norms[c]*w).normalized;var old=oldPixels[i];bool blue=old.b>old.r*1.25f&&old.g>old.r*1.1f;float fm=S(1.475f,1.50f,pos.y)*(1-S(1.73f,1.755f,pos.y))*(1-S(.070f,.090f,Mathf.Abs(pos.x)))*S(.015f,.045f,pos.z);
   bool hairRegion=pos.y>1.28f&&pos.y<1.74f&&Mathf.Abs(pos.x)<.245f&&(pos.z<-.065f||(Mathf.Abs(pos.x)>Mathf.Lerp(.055f,.085f,S(1.50f,1.57f,pos.y))&&pos.y>1.30f&&pos.z<.16f));float hm=hairRegion&&!blue?S(1.28f,1.34f,pos.y)*(1-S(1.715f,1.74f,pos.y))*(1-fm):0;
   if(fm>.001f&&normal.z>.1f){var screen=new Vector2(.5f-pos.x/.31f,.5f+(pos.y-1.64f)/.31f);if(screen.x>0&&screen.x<1&&screen.y>0&&screen.y<1){pixels[i]=Color.Lerp(old,face.GetPixelBilinear(screen.x,screen.y),fm*.97f);faceMask[i]=fm;painted[i]=fm;}}
   if(hm>.001f){Color sum=Color.clear;float total=0;for(int view=0;view<4;view++){int angle=view*90;var rot=Quaternion.Euler(0,angle,0);var screen=Project(pos,angle,false);if(screen.x<0||screen.x>1||screen.y<0||screen.y>1)continue;int sx=Mathf.Clamp((int)(screen.x*1024),0,1023),sy=Mathf.Clamp((int)(screen.y*1024),0,1023);float amount=Mathf.Pow(Mathf.Max(0,(rot*normal).z),2);if(view==0||view==2)amount+=.08f;else amount*=.01f;sum+=hair[view].GetPixelBilinear(screen.x,screen.y)*amount;total+=amount;}if(total>.005f){pixels[i]=Color.Lerp(pixels[i],sum/total,hm*.95f);hairMask[i]=hm;painted[i]=Mathf.Max(painted[i],hm);}}
   if(!blue&&hm<.1f&&fm<.1f&&pos.y<1.69f)skinMask[i]=1;if(blue){float l=.2126f*old.r+.7152f*old.g+.0722f*old.b;var muted=new Color(old.r*.87f+l*.13f,old.g*.72f+l*.13f,old.b*.60f+l*.13f,old.a);Color sum=Color.clear;float total=0;for(int view=0;view<2;view++){int angle=view*180;var rot=Quaternion.Euler(0,angle,0);var screen=Project(pos,angle,true);int sx=Mathf.Clamp((int)(screen.x*1024),0,1023),sy=Mathf.Clamp((int)(screen.y*1024),0,1023);if((rot*pos).z<depths["B"+angle][sy*1024+sx]-.016f)continue;float amount=Mathf.Pow(Mathf.Max(0,(rot*normal).z),4);sum+=cloth[view].GetPixelBilinear(screen.x,screen.y)*amount;total+=amount;}pixels[i]=total>.005f?Color.Lerp(muted,sum/total,.85f):muted;painted[i]=1;}
   if(pos.y>1.68f){Color sum=Color.clear;float total=0;for(int view=0;view<4;view++){int angle=view*90;var rot=Quaternion.Euler(0,angle,0);var screen=Project(pos,angle,false);float amount=Mathf.Pow(Mathf.Max(0,(rot*normal).z),2)+.08f;sum+=(view==3?hair[1].GetPixelBilinear(1-screen.x,screen.y):hair[view].GetPixelBilinear(screen.x,screen.y))*amount;total+=amount;}float forehead=S(.035f,.055f,pos.z)*(1-S(.055f,.080f,Mathf.Abs(pos.x)))*(1-S(1.700f,1.715f,pos.y));float crown=S(1.68f,1.695f,pos.y)*(1-forehead);pixels[i]=Color.Lerp(pixels[i],sum/total,crown);faceMask[i]=Mathf.Max(faceMask[i]*(1-crown),forehead);pixels[i]=Color.Lerp(pixels[i],face.GetPixelBilinear(.5f-pos.x/.31f,.5f+(pos.y-1.64f)/.31f),forehead);skinMask[i]*=1-crown;bowMask[i]=crown;hairMask[i]=Mathf.Max(hairMask[i]*(1-forehead),crown);painted[i]=Mathf.Max(painted[i],Mathf.Max(crown,forehead));}
   pixels[i].a=old.a;
  });}
  for(int i=0;i<faceMask.Length;i++){hairMask[i]*=1-faceMask[i];bowMask[i]*=1-faceMask[i];}
  var detailPixels=(Color[])pixels.Clone();
  // Normalize broad baked skin lighting separately from pores and fine wrinkles.
  var bodyLocalLuma=new float[n*n];var bodyCoverage=(float[])skinMask.Clone();for(int i=0;i<bodyLocalLuma.Length;i++)bodyLocalLuma[i]=(oldPixels[i].r*.2126f+oldPixels[i].g*.7152f+oldPixels[i].b*.0722f)*skinMask[i];for(int pass=0;pass<3;pass++){bodyLocalLuma=Blur(bodyLocalLuma,n,12);bodyCoverage=Blur(bodyCoverage,n,12);}for(int i=0;i<bodyLocalLuma.Length;i++)bodyLocalLuma[i]=bodyCoverage[i]>.0001f?bodyLocalLuma[i]/bodyCoverage[i]:0;
  Func<Color,bool> neutral=c=>{float hi=Mathf.Max(c.r,Mathf.Max(c.g,c.b)),lo=Mathf.Min(c.r,Mathf.Min(c.g,c.b));return hi>.10f&&hi<.90f&&(hi-lo)/hi<.50f&&!(c.r>c.g*1.6f&&c.r>c.b*1.5f);};
  var reference=JsonUtility.FromJson<NurseMoeReference.Reference>(File.ReadAllText(Out+"/moe-skin-reference.json"));Color faceMean=Color.clear,skinMean=Color.clear;int fc=0,sc=0;
  for(int i=0;i<pixels.Length;i++){if(faceMask[i]>.8f&&faceStatistics[i]&&neutral(pixels[i])){faceMean+=pixels[i];fc++;}if(skinMask[i]>.8f&&neutral(oldPixels[i])){skinMean+=oldPixels[i];sc++;}}
  if(fc<100||sc<100)throw new Exception("Skin samples missing");faceMean/=fc;skinMean/=sc;
  for(int i=0;i<pixels.Length;i++){
   float faceAmount=faceMask[i],bodyAmount=skinMask[i];if(Mathf.Max(faceAmount,bodyAmount)<=0)continue;var c=pixels[i];var mean=faceAmount>bodyAmount?faceMean:skinMean;
   // One native colour target across the raw source body and the entirely new face.
   var adjusted=new Color(Mathf.Clamp01(c.r*reference.albedo.r/mean.r),Mathf.Clamp01(c.g*reference.albedo.g/mean.g),Mathf.Clamp01(c.b*reference.albedo.b/mean.b),c.a);
   // Face and body use the same measured native skin target without a separate exposure offset.
   if(bodyAmount>faceAmount){float l=c.r*.2126f+c.g*.7152f+c.b*.0722f,meanL=skinMean.r*.2126f+skinMean.g*.7152f+skinMean.b*.0722f;float local=bodyLocalLuma[i]>.01f?bodyLocalLuma[i]:meanL;float broad=.85f+.15f*Mathf.Clamp(local/meanL,.5f,1.5f),detail=Mathf.Clamp(l/local,.45f,1.8f);var match=reference.albedo*(broad*detail);match.a=c.a;float adjustedL=adjusted.r*.2126f+adjusted.g*.7152f+adjusted.b*.0722f,desiredL=match.r*.2126f+match.g*.7152f+match.b*.0722f;adjusted*=desiredL/Mathf.Max(.01f,adjustedL);adjusted.a=c.a;adjusted=Color.Lerp(adjusted,match,.80f);}pixels[i]=Color.Lerp(c,adjusted,Mathf.Max(faceAmount,bodyAmount));painted[i]=Mathf.Max(painted[i],Mathf.Max(faceAmount,bodyAmount));
  }
  File.WriteAllText(Out+"/exposed-skin.txt","Raw untouched Color.jpg used before projection; old partial feet-only colour grade removed. Moe intact facial skin="+reference.albedo+"; native packed="+reference.packed+"; native sample count="+reference.samples+"; body samples="+sc+" face samples="+fc+"; raw body mean="+skinMean+"; new face mean="+faceMean+". All front/back exposed skin and facial artwork mapped to the same native measured target; no arbitrary grey tint.\n");
  // Dilate new artwork into unused chart gutters without overwriting another surface.
  for(int pass=0;pass<6;pass++){var next=(Color[])pixels.Clone();var mask=(float[])painted.Clone();for(int y=1;y<n-1;y++)for(int x=1;x<n-1;x++){int i=y*n+x;if(occupied[i]||painted[i]>0)continue;foreach(int d in new[]{-1,1,-n,n})if(painted[i+d]>mask[i]){mask[i]=painted[i+d];next[i]=pixels[i+d];}}pixels=next;painted=mask;}
  for(int i=0;i<pixels.Length;i++)if(painted[i]>0){var old=oldPixels[i];var f=feral[i];feral[i]=new Color(pixels[i].r*Mathf.Clamp((f.r+.025f)/(old.r+.025f),.6f,1.5f),pixels[i].g*Mathf.Clamp((f.g+.025f)/(old.g+.025f),.6f,1.5f),pixels[i].b*Mathf.Clamp((f.b+.025f)/(old.b+.025f),.6f,1.5f),f.a);}
  Save(pixels,n,Folder+"/Atlas_Albedo.png");Save(feral,n,Folder+"/Atlas_Albedo_Feral.png");
  // Keep the baked tangent frame; suppress legacy facial seam bumps and add subtle artwork microdetail.
  var normalTex=Read(Out+"/Atlas_Normal.png");var np=normalTex.GetPixels();var rawDetail=new float[n*n];for(int i=0;i<rawDetail.Length;i++)rawDetail[i]=detailPixels[i].r*.2126f+detailPixels[i].g*.7152f+detailPixels[i].b*.0722f;var broadDetail=Blur(rawDetail,n,4);Func<int,float> luma=i=>Mathf.Lerp(rawDetail[i],rawDetail[i]-broadDetail[i],faceMask[i]);
  for(int y=1;y<n-1;y++)for(int x=1;x<n-1;x++){int i=y*n+x;float amount=Mathf.Max(skinMask[i],Mathf.Max(bowMask[i],Mathf.Max(faceMask[i],hairMask[i])));if(amount<=0)continue;var nn=new Vector3(np[i].r*2-1,np[i].g*2-1,np[i].b*2-1).normalized;nn=Vector3.Lerp(nn,Vector3.forward,Mathf.Max(skinMask[i]*.35f,Mathf.Max(bowMask[i]*.95f,Mathf.Max(faceMask[i],hairMask[i])))).normalized;nn.x+=(luma(i-1)-luma(i+1))*Mathf.Lerp(.22f,.65f,faceMask[i])*amount;nn.y+=(luma(i-n)-luma(i+n))*Mathf.Lerp(.22f,.65f,faceMask[i])*amount;nn.Normalize();np[i]=new Color(nn.x*.5f+.5f,nn.y*.5f+.5f,nn.z*.5f+.5f,1);}
  Save(np,n,Folder+"/Atlas_Normal.png");
  foreach(string tier in new[]{"Normal","Feral","Special"}){
   string map=Folder+"/Atlas_RMOE_"+tier+".png";var packed=Read(map).GetPixels();
   for(int y=0;y<512;y++)for(int x=0;x<512;x++){
    float hairAmount=0,skinAmount=0;for(int dy=0;dy<4;dy++)for(int dx=0;dx<4;dx++){int sample=(y*4+dy)*n+x*4+dx;hairAmount+=Mathf.Max(hairMask[sample],bowMask[sample]);skinAmount+=Mathf.Max(skinMask[sample],faceMask[sample]);}hairAmount/=16;skinAmount/=16;int i=y*512+x;
    packed[i].r=Mathf.Lerp(packed[i].r,.80f,hairAmount);packed[i].g=Mathf.Lerp(packed[i].g,0,hairAmount);
    packed[i].r=Mathf.Lerp(packed[i].r,reference.packed.r,skinAmount);packed[i].g=Mathf.Lerp(packed[i].g,reference.packed.g,skinAmount);packed[i].b=Mathf.Lerp(packed[i].b,reference.packed.b,skinAmount);
   }Save(packed,512,map);
  }
  var widths=new List<string>();
  foreach(int target in new[]{20000,9500,3000}){var m=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Blender_"+target+".asset");string backup=Folder+"/"+MeshBaselinePrefix+target+".asset";if(!File.Exists(backup))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(m),backup);var originalMesh=AssetDatabase.LoadAssetAtPath<Mesh>(backup);var points=originalMesh.vertices;var weights=originalMesh.boneWeights;int moved=0;float before=0,after=0;for(int i=0;i<points.Length;i++){var wp=r.transform.TransformPoint(points[i]);var wgt=weights[i];int[] ids={wgt.boneIndex0,wgt.boneIndex1,wgt.boneIndex2,wgt.boneIndex3};float[] ws={wgt.weight0,wgt.weight1,wgt.weight2,wgt.weight3};float hairWeight=0;for(int k=0;k<4;k++)if(r.bones[ids[k]].name.StartsWith("Hair_"))hairWeight+=ws[k];if(hairWeight<.01f||Mathf.Abs(wp.x)<.095f)continue;before=Mathf.Max(before,Mathf.Abs(wp.x));float strength=S(.01f,.35f,hairWeight)*S(.095f,.16f,Mathf.Abs(wp.x));wp.x*=1-.12f*strength;after=Mathf.Max(after,Mathf.Abs(wp.x));points[i]=r.transform.InverseTransformPoint(wp);moved++;}m.vertices=points;m.RecalculateBounds();m.RecalculateTangents();EditorUtility.SetDirty(m);widths.Add(target+" hair vertices narrowed="+moved+" halfWidthBefore="+before+" after="+after);}
  AssetDatabase.SaveAssets();File.WriteAllLines(Out+"/surface-finish.txt",new[]{"Face artwork texels="+faceMask.Count(x=>x>.1f),"Hair artwork texels="+hairMask.Count(x=>x>.1f),"Rounded crown hair texels="+bowMask.Count(x=>x>.1f),"Albedo and normal 2048; RMOE unchanged 512; six texel gutters; four hair projections; front/back garment projections."}.Concat(widths));Debug.Log("NURSE_SURFACE_FINISH_DONE");
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}



















