using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio {
 [Serializable] public sealed class FaceTeethSettings {
  public float[] fractureDepth=new float[32],fractureAngle=new float[32];
  public float width=1,height=1,canines=1,irregularity,wear;public int seed=23;public int[] state=new int[32];public float[] size=Enumerable.Repeat(1f,32).ToArray(),tilt=new float[32];
  public void Validate(){if(fractureDepth==null||fractureDepth.Length!=32)fractureDepth=new float[32];if(fractureAngle==null||fractureAngle.Length!=32)fractureAngle=new float[32];if(state==null||state.Length!=32)state=new int[32];if(size==null||size.Length!=32)size=Enumerable.Repeat(1f,32).ToArray();if(tilt==null||tilt.Length!=32)tilt=new float[32];foreach(float v in new[]{width,height,canines,irregularity,wear}.Concat(size).Concat(tilt).Concat(fractureDepth).Concat(fractureAngle))if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Invalid dental setting.");if(width<=0)width=1;if(height<=0)height=1;if(canines<=0)canines=1;width=Mathf.Clamp(width,.75f,1.2f);height=Mathf.Clamp(height,.6f,1.4f);canines=Mathf.Clamp(canines,.7f,1.8f);irregularity=Mathf.Clamp01(irregularity);wear=Mathf.Clamp01(wear);for(int i=0;i<32;i++){state[i]=Mathf.Clamp(state[i],0,3);fractureDepth[i]=Mathf.Clamp(fractureDepth[i],0,.95f);fractureAngle[i]=Mathf.Clamp(fractureAngle[i],-20,20);if(size[i]<=0)size[i]=1;size[i]=Mathf.Clamp(size[i],.65f,1.35f);tilt[i]=Mathf.Clamp(tilt[i],-12,12);}}
 }
 public static class RebirthTeethGenerator {
  public static readonly string[] States={"Intact","Chipped","Broken","Missing"};
  public static readonly string[] Presets={"Natural / intact","Small teeth","Broad teeth","Worn / aged","Pronounced canines","Chipped","Missing front teeth","Mixed zombie damage"};
  public static int Fdi(int slot){return (slot<16?(slot<8?10:20):(slot<24?40:30))+(slot%8)+1;}
  static float Noise(int index,int seed){uint n=(uint)(index*73856093^seed*19349663);n=(n^(n>>13))*1274126177u;return (n&0xffffff)/16777215f;}
  public static void ClearDamage(FaceTeethSettings d){if(d==null)throw new ArgumentNullException(nameof(d));d.state=new int[32];d.fractureDepth=new float[32];d.fractureAngle=new float[32];}
  static Vector3 FractureNormal(FaceTeethSettings d,int slot)=>Quaternion.AngleAxis(d.fractureAngle[slot],Vector3.forward)*new Vector3((d.state[slot]==1?.65f:.14f)*(slot%16<8?-1:1),slot<16?1:-1,.12f).normalized;
  public static void Preset(FaceTeethSettings d,int preset){
   if(d==null)throw new ArgumentNullException(nameof(d));
   if(preset<0||preset>=Presets.Length)throw new ArgumentOutOfRangeException(nameof(preset));
   // Damage selection must not erase the owner's crown proportions or alignment.
   if(preset>=5){DamagePreset(d,preset);return;}
   d.width=1;d.height=1;d.canines=1;d.irregularity=0;d.wear=0;ClearDamage(d);d.size=Enumerable.Repeat(1f,32).ToArray();d.tilt=new float[32];
   if(preset==1){d.width=.85f;d.height=.85f;}if(preset==2)d.width=1.15f;if(preset==3){d.wear=.35f;d.irregularity=.25f;}if(preset==4)d.canines=1.6f;
  }
  public static void DamagePreset(FaceTeethSettings d,int preset){
   if(d==null)throw new ArgumentNullException(nameof(d));
   if(preset<5||preset>7)throw new ArgumentOutOfRangeException(nameof(preset));
   var states=new int[32];
   if(preset==5)foreach(int i in new[]{1,8,17,26})states[i]=1;
   if(preset==6)foreach(int i in new[]{0,9,16})states[i]=3;
   if(preset==7)for(int i=0;i<32;i++){float n=Noise(i,d.seed);states[i]=n<.10f?3:n<.27f?2:n<.48f?1:0;}
   ClearDamage(d);d.state=states;
   if(preset==7)for(int i=0;i<32;i++)if(states[i]==1||states[i]==2){d.fractureDepth[i]=states[i]==1?Mathf.Lerp(.08f,.28f,Noise(i+71,d.seed)):Mathf.Lerp(.35f,.72f,Noise(i+71,d.seed));d.fractureAngle[i]=Mathf.Lerp(-15,15,Noise(i+97,d.seed));}
  }
  public static void Shape(FaceOralMesh oral,Vector3[] p,FaceTeethSettings d){
   foreach(var g in Enumerable.Range(0,p.Length).Where(i=>oral.toothSlots[i]>=0).GroupBy(i=>oral.toothSlots[i])){
    int slot=g.Key;var ids=g.ToArray();bool upper=slot<16;float root=upper?ids.Max(i=>p[i].y):ids.Min(i=>p[i].y),tip=upper?ids.Min(i=>p[i].y):ids.Max(i=>p[i].y);Vector3 anchor=new Vector3(ids.Average(i=>p[i].x),root,ids.Average(i=>p[i].z));float scale=d.height*d.size[slot]*(slot%8==2?d.canines:1)*(1-d.wear*.25f);float angle=d.tilt[slot]+(Noise(slot,d.seed)-.5f)*d.irregularity*12;
    if(Mathf.Approximately(d.width,1)&&Mathf.Approximately(scale,1)&&Mathf.Abs(angle)<1e-5f)continue;
    foreach(int i in ids){Vector3 q=p[i]-anchor;float crown=Mathf.Clamp01(Mathf.Abs(q.y)/Mathf.Max(.001f,Mathf.Abs(tip-root)));q.x*=Mathf.Lerp(1,d.width*d.size[slot],Mathf.SmoothStep(0,1,crown));q.y*=scale;q=Quaternion.Euler(0,0,angle*crown)*q;p[i]=anchor+q;}
   }
  }
  public static List<int> Damage(FaceOralMesh oral,int start,List<Vector3> p,List<Vector2> uv,FaceTeethSettings d,HashSet<int> smoothCrowns){
   var result=new List<int>();var bounds=new Dictionary<int,Tuple<float,Vector3>>();
   foreach(var group in Enumerable.Range(0,oral.toothSlots.Length).Where(i=>oral.toothSlots[i]>=0).GroupBy(i=>oral.toothSlots[i])){
    int slot=group.Key;var ids=group.Select(i=>i+start).ToArray();if(d.state[slot]==0)continue;foreach(int i in ids)smoothCrowns.Remove(i);if(d.state[slot]==3)continue;
    bool upper=slot<16;float root=upper?ids.Max(i=>p[i].y):ids.Min(i=>p[i].y),tip=upper?ids.Min(i=>p[i].y):ids.Max(i=>p[i].y);float retained=d.fractureDepth[slot]>0?1-d.fractureDepth[slot]:(d.state[slot]==1?.83f:.46f);float cut=Mathf.Lerp(root,tip,retained);Vector3 center=new Vector3(ids.Average(i=>p[i].x),cut,ids.Average(i=>p[i].z));bounds[slot]=Tuple.Create(cut,center);
   }
   var cuts=new Dictionary<int,List<Tuple<Vector3,Vector2>>>();
   for(int t=0;t<oral.triangles.Length;t+=3){int[] ids={oral.triangles[t],oral.triangles[t+1],oral.triangles[t+2]};int slot=oral.toothSlots[ids[0]];if(slot<0||d.state[slot]==0){result.AddRange(ids.Select(i=>i+start));continue;}if(d.state[slot]==3)continue;
    bool upper=slot<16;Vector3 center=bounds[slot].Item2;Vector3 normal=FractureNormal(d,slot);
    Func<Vector3,float> distance=q=>Vector3.Dot(q-center,normal);var poly=ids.Select(i=>Tuple.Create(p[i+start],uv[i+start])).ToList();var clipped=new List<Tuple<Vector3,Vector2>>();
    for(int k=0;k<poly.Count;k++){var a=poly[k];var b=poly[(k+1)%poly.Count];float da=distance(a.Item1),db=distance(b.Item1);if(da>=0)clipped.Add(a);if((da>=0)!=(db>=0)){float w=da/(da-db);var cut=Tuple.Create(Vector3.Lerp(a.Item1,b.Item1,w),Vector2.Lerp(a.Item2,b.Item2,w));clipped.Add(cut);if(!cuts.ContainsKey(slot))cuts[slot]=new List<Tuple<Vector3,Vector2>>();cuts[slot].Add(cut);}}
    if(clipped.Count<3)continue;int first=p.Count;foreach(var q in clipped){p.Add(q.Item1);uv.Add(q.Item2);}for(int k=1;k<clipped.Count-1;k++)result.AddRange(new[]{first,first+k,first+k+1});
   }
   foreach(var item in cuts){int slot=item.Key;var loop=item.Value.GroupBy(q=>Vector3Int.RoundToInt(q.Item1*1000000)).Select(g=>g.First()).ToList();if(loop.Count<3)continue;Vector3 center=loop.Aggregate(Vector3.zero,(sum,q)=>sum+q.Item1)/loop.Count;Vector3 outward=-FractureNormal(d,slot);Vector3 axis=Vector3.Cross(outward,Vector3.forward).normalized,other=Vector3.Cross(outward,axis);loop=loop.OrderBy(q=>Mathf.Atan2(Vector3.Dot(q.Item1-center,other),Vector3.Dot(q.Item1-center,axis))).ToList();int first=p.Count;foreach(var q in loop){p.Add(q.Item1);uv.Add(q.Item2);}for(int k=1;k<loop.Count-1;k++)result.AddRange(new[]{first,first+k,first+k+1});}
   return result;
  }
 }
}
