using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using AssetStudio;
// A streaming FBX 7.3 ASCII writer for skeletons and sampled transform curves.
// Avoids constructing hundreds of thousands of native SDK curve objects.
static class FastAsciiFbx {
 sealed class CanonicalWriter : StreamWriter {
  public CanonicalWriter(string path):base(path,false,new UTF8Encoding(false),1024*1024) {}
  public override void WriteLine(string text) {
   var b=new StringBuilder(text.Length+32); bool quoted=false,escaped=false;
   foreach(char ch in text) {
    if(!quoted&&ch=='}') b.Append('\n');
    b.Append(ch);
    if(!quoted&&(ch=='{'||ch=='}')) b.Append('\n');
    if(ch=='"'&&!escaped) quoted=!quoted;
    if(ch=='\\'&&!escaped) escaped=true; else escaped=false;
   }
   base.WriteLine(b.ToString());
  }
 }
 static string N(double n) { if(Double.IsNaN(n)||Double.IsInfinity(n)) throw new Exception("Non-finite FBX value"); return n.ToString("R",CultureInfo.InvariantCulture); }
 static string S(string s) { return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r"," ").Replace("\n"," ")+"\""; }
 static long T(float seconds) { return (long)Math.Round(seconds*46186158000.0); }
 static float Component(Vector3 v,int axis) { return axis==0?v.X:axis==1?v.Y:v.Z; }
 static IEnumerable<ImportedFrame> Frames(ImportedFrame root) { yield return root; for(int i=0;i<root.Count;i++) foreach(var b in Frames(root[i])) yield return b; }
 public static void Write(string path,IImported scene,Dictionary<string,float> durations) {
  var bones=Frames(scene.RootFrame).ToList();
  var ids=new Dictionary<string,long>(); long next=100000;
  foreach(var b in bones) ids.Add(b.Path,next++);
  int nodes=scene.AnimationList.Sum(a=>a.TrackList.Count==0?1:a.TrackList.Sum(t=>(t.Translations.Count>0?1:0)+(t.Rotations.Count>0?1:0)+(t.Scalings.Count>0?1:0)));
  var definitions=new Dictionary<string,int>{{"GlobalSettings",1},{"Model",bones.Count},{"NodeAttribute",bones.Count},{"AnimationStack",scene.AnimationList.Count},{"AnimationLayer",scene.AnimationList.Count},{"AnimationCurveNode",nodes},{"AnimationCurve",nodes*3}};
  var edges=new List<string>();
  var spans=new Dictionary<string,long>();
  using(var w=new CanonicalWriter(path)) {
   w.WriteLine("; FBX 7.3.0 project file\nFBXHeaderExtension: { FBXHeaderVersion: 1003\n FBXVersion: 7300\n Creator: \"REBIRTH animation skeleton exporter\"\n}");
   w.WriteLine("GlobalSettings: { Version: 1000\n Properties70: {");
   w.WriteLine("P: \"UpAxis\", \"int\", \"Integer\", \"\",1\nP: \"UpAxisSign\", \"int\", \"Integer\", \"\",1\nP: \"FrontAxis\", \"int\", \"Integer\", \"\",2\nP: \"FrontAxisSign\", \"int\", \"Integer\", \"\",1\nP: \"CoordAxis\", \"int\", \"Integer\", \"\",0\nP: \"CoordAxisSign\", \"int\", \"Integer\", \"\",1\nP: \"UnitScaleFactor\", \"double\", \"Number\", \"\",1\nP: \"OriginalUnitScaleFactor\", \"double\", \"Number\", \"\",1\nP: \"TimeMode\", \"enum\", \"\", \"\",6\n}\n}");
   w.WriteLine("Documents: { Count: 1\n Document: 1, \"\", \"Scene\" { RootNode: 0\n}\n}\nReferences: {}\nDefinitions: { Version: 100\n Count: "+definitions.Values.Sum());
   foreach(var def in definitions) w.WriteLine("ObjectType: "+S(def.Key)+" { Count: "+def.Value+"\n}");
   w.WriteLine("}\nObjects: {");
   foreach(var b in bones) {
    long id=ids[b.Path],attribute=next++;
    w.WriteLine("Model: "+id+", "+S("Model::"+b.Name)+", \"LimbNode\" { Version: 232\nProperties70: {");
    VecProperty(w,"Lcl Translation",b.LocalPosition); VecProperty(w,"Lcl Rotation",b.LocalRotation); VecProperty(w,"Lcl Scaling",b.LocalScale);
    w.WriteLine("P: \"RotationOrder\", \"enum\", \"\", \"\",0\n}\nShading: T\nCulling: \"CullingOff\"\n}");
    w.WriteLine("NodeAttribute: "+attribute+", "+S("NodeAttribute::"+b.Name)+", \"LimbNode\" { TypeFlags: \"Skeleton\"\nProperties70: { P: \"Size\", \"double\", \"Number\", \"\",1\n}\n}");
    edges.Add("C: \"OO\","+attribute+","+id);
    edges.Add("C: \"OO\","+id+","+(b.Parent==null?0:ids[b.Parent.Path]));
   }
   foreach(var anim in scene.AnimationList) {
    long stack=next++,layer=next++;
    float duration=durations.ContainsKey(anim.Name)?durations[anim.Name]:0;
    foreach(var track in anim.TrackList) foreach(var list in new[]{track.Translations,track.Rotations,track.Scalings}) if(list.Count>0) duration=Math.Max(duration,list[list.Count-1].time);
    spans[anim.Name]=T(duration);
    w.WriteLine("AnimationStack: "+stack+", "+S("AnimStack::"+anim.Name)+", \"\" { Properties70: {");
    foreach(var field in new[]{"LocalStart","ReferenceStart"}) w.WriteLine("P: "+S(field)+", \"KTime\", \"Time\", \"\",0");
    foreach(var field in new[]{"LocalStop","ReferenceStop"}) w.WriteLine("P: "+S(field)+", \"KTime\", \"Time\", \"\","+T(duration));
    w.WriteLine("}\n}\nAnimationLayer: "+layer+", \"AnimLayer::BaseLayer\", \"\" {} ");
    edges.Add("C: \"OO\","+layer+","+stack);
    var tracks=anim.TrackList;
    if(tracks.Count==0) tracks=new List<ImportedAnimationKeyframedTrack>{new ImportedAnimationKeyframedTrack{Path=scene.RootFrame.Path,Translations=new List<ImportedKeyframe<Vector3>>{new ImportedKeyframe<Vector3>(0,scene.RootFrame.LocalPosition),new ImportedKeyframe<Vector3>(duration,scene.RootFrame.LocalPosition)},Rotations=new List<ImportedKeyframe<Vector3>>(),Scalings=new List<ImportedKeyframe<Vector3>>()}};
    foreach(var track in tracks) {
     if(!ids.ContainsKey(track.Path)) throw new Exception("Unbound ASCII curve "+track.Path);
     var channels=new[]{track.Translations,track.Rotations,track.Scalings};
     for(int channel=0;channel<3;channel++) {
      var keys=channels[channel]; if(keys.Count==0) continue;
      long node=next++;
      string property=channel==0?"Lcl Translation":channel==1?"Lcl Rotation":"Lcl Scaling";
      w.WriteLine("AnimationCurveNode: "+node+", "+S("AnimCurveNode::"+(channel==0?"T":channel==1?"R":"S"))+", \"\" { Properties70: {");
      for(int axis=0;axis<3;axis++) w.WriteLine("P: \"d|"+"XYZ"[axis]+"\", \"Number\", \"\", \"A\","+N(Component(keys[0].value,axis)));
      w.WriteLine("}\n}");
      edges.Add("C: \"OO\","+node+","+layer);
      edges.Add("C: \"OP\","+node+","+ids[track.Path]+","+S(property));
      for(int axis=0;axis<3;axis++) {
       long curve=next++;
       var values=keys.Select(k=>(double)Component(k.value,axis)).ToArray();
       if(channel==1) for(int i=1;i<values.Length;i++) { while(values[i]-values[i-1]>180) values[i]-=360; while(values[i]-values[i-1]<-180) values[i]+=360; }
       w.WriteLine("AnimationCurve: "+curve+", \"AnimCurve::\", \"\" { Default: "+N(values[0])+"\nKeyVer: 4009");
       Array(w,"KeyTime",keys.Select(k=>T(k.time).ToString(CultureInfo.InvariantCulture)));
       Array(w,"KeyValueFloat",values.Select(N));
       Array(w,"KeyAttrFlags",new[]{"4"}); Array(w,"KeyAttrDataFloat",new[]{"0","0","0","0"}); Array(w,"KeyAttrRefCount",new[]{keys.Count.ToString(CultureInfo.InvariantCulture)});
       w.WriteLine("}"); edges.Add("C: \"OP\","+curve+","+node+",\"d|"+"XYZ"[axis]+"\"");
      }
     }
    }
   }
   w.WriteLine("}\nConnections: {"); foreach(var edge in edges) w.WriteLine(edge); w.WriteLine("}\nTakes: {\n Current: \"\"");
   foreach(var span in spans) w.WriteLine("Take: "+S(span.Key)+" {\n FileName: "+S(span.Key+".tak")+"\n LocalTime: 0,"+span.Value+"\n ReferenceTime: 0,"+span.Value+"\n}");
   w.WriteLine("}");
  }
 }
 static void VecProperty(StreamWriter w,string name,Vector3 v) {w.WriteLine("P: "+S(name)+", "+S(name)+", \"\", \"A\","+N(v.X)+","+N(v.Y)+","+N(v.Z));}
 static void Array(StreamWriter w,string name,IEnumerable<string> values) {var data=values.ToArray(); w.WriteLine(name+": *"+data.Length+" { a: "+String.Join(",",data)+"\n}");}
}
