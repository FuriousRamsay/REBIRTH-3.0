using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AssetStudio;
class BakedScene : IImported {
 public ImportedFrame RootFrame {get;set;}
 public List<ImportedMesh> MeshList {get;set;}
 public List<ImportedMaterial> MaterialList {get;set;}
 public List<ImportedTexture> TextureList {get;set;}
 public List<ImportedKeyframedAnimation> AnimationList {get;set;}
 public List<ImportedMorph> MorphList {get;set;}
 public BakedScene() { MeshList=new List<ImportedMesh>(); MaterialList=new List<ImportedMaterial>(); TextureList=new List<ImportedTexture>(); AnimationList=new List<ImportedKeyframedAnimation>(); MorphList=new List<ImportedMorph>(); }
}
class BakedToFbx {
 static Vector3 V(BinaryReader r) { return new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()); }
 static Vector3 Q(BinaryReader r) { float x=r.ReadSingle(),y=r.ReadSingle(),z=r.ReadSingle(),w=r.ReadSingle(); return Fbx.QuaternionToEuler(new Quaternion(x,-y,-z,w)); }
 static Vector3 P(BinaryReader r) { var v=V(r); return new Vector3(-v.X,v.Y,v.Z); }
 static void Main(string[] args) {
  try { Run(args); } catch(Exception e) { Console.Error.WriteLine(e); Environment.ExitCode=1; }
 }
 static void Run(string[] args) {
  var scene=new BakedScene(); var sources=new HashSet<string>(); var report=new List<string>{"take\tsource\tduration_seconds\tsample_rate\thumanoid\tsamples\tbones\tmoving_bones"};
  using(var r=new BinaryReader(File.OpenRead(args[0]))) {
   if(r.ReadString()!="REBIRTH_ANIMATION_BAKE_1") throw new Exception("Invalid bake format");
   int count=r.ReadInt32(); var bones=new ImportedFrame[count];
   for(int i=0;i<count;i++) { string name=r.ReadString(); int parent=r.ReadInt32(); var p=P(r); var q=Q(r); var s=V(r); var bone=new ImportedFrame(0){Name=name,LocalPosition=p,LocalRotation=q,LocalScale=s}; bones[i]=bone; if(parent<0) scene.RootFrame=bone; else bones[parent].AddChild(bone); }
   int clipCount=r.ReadInt32();
   for(int c=0;c<clipCount;c++) {
    string name=r.ReadString(),source=r.ReadString(); float duration=r.ReadSingle(),rate=r.ReadSingle(); bool human=r.ReadBoolean(); int samples=r.ReadInt32();
    sources.Add(source);
    var anim=new ImportedKeyframedAnimation {Name=name,SampleRate=rate,TrackList=new List<ImportedAnimationKeyframedTrack>()};
    for(int i=0;i<count;i++) anim.TrackList.Add(new ImportedAnimationKeyframedTrack {Path=bones[i].Path,Translations=new List<ImportedKeyframe<Vector3>>(),Rotations=new List<ImportedKeyframe<Vector3>>(),Scalings=new List<ImportedKeyframe<Vector3>>()});
    for(int f=0;f<samples;f++) { float time=r.ReadSingle(); foreach(var t in anim.TrackList) { t.Translations.Add(new ImportedKeyframe<Vector3>(time,P(r))); t.Rotations.Add(new ImportedKeyframe<Vector3>(time,Q(r))); t.Scalings.Add(new ImportedKeyframe<Vector3>(time,V(r))); } }
    int moving=0;
    foreach(var t in anim.TrackList) {
     if(Varies(t.Translations)||Varies(t.Rotations)||Varies(t.Scalings)) moving++;
     ReduceConstant(t.Translations); ReduceConstant(t.Rotations); ReduceConstant(t.Scalings);
    }
    foreach(var t in anim.TrackList) {
     var bone=scene.RootFrame.FindFrameByPath(t.Path);
     if(t.Translations.All(k=>!Different(k.value,bone.LocalPosition))) t.Translations.Clear();
     if(t.Rotations.All(k=>!Different(k.value,bone.LocalRotation))) t.Rotations.Clear();
     if(t.Scalings.All(k=>!Different(k.value,bone.LocalScale))) t.Scalings.Clear();
    }
    anim.TrackList.RemoveAll(t=>t.Translations.Count==0&&t.Rotations.Count==0&&t.Scalings.Count==0);
    scene.AnimationList.Add(anim);
    report.Add(name+"\t"+source+"\t"+duration+"\t"+rate+"\t"+human+"\t"+samples+"\t"+count+"\t"+moving);
    Console.WriteLine(name+" moving="+moving+" samples="+samples);
   }
   if(r.BaseStream.Position!=r.BaseStream.Length) throw new Exception("Trailing bake bytes");
  }
  if(args.Length>2 && args[0].IndexOf("zombie",StringComparison.OrdinalIgnoreCase)<0) {
   var manager=new AssetsManager(); manager.LoadFilesAndFolders(new[]{args[2]});
   var objects=manager.AssetsFileList.SelectMany(f=>f.Objects).ToList();
   var missing=objects.OfType<AnimationClip>().Where(c=>!sources.Contains(c.m_Name)).ToList();
   var animator=objects.OfType<Animator>().First(a=>{ GameObject go; return a.m_GameObject.TryGet(out go)&&go.m_Name=="baseRig"; });
   var extra=new ModelConverter(animator,ImageFormat.Png,missing);
   foreach(var anim in extra.AnimationList) {
    if(anim.TrackList.Count==0||anim.TrackList.Any(t=>String.IsNullOrEmpty(t.Path))) throw new Exception("Incomplete orphan clip: "+anim.Name);
    foreach(var t in anim.TrackList) if(scene.RootFrame.FindFrameByPath(t.Path)==null) throw new Exception("Unbound orphan bone: "+t.Path);
    scene.AnimationList.Add(anim);
    report.Add(anim.Name+"\t"+anim.Name+"\t\t"+anim.SampleRate+"\tFalse\tNativeCurves\t"+anim.TrackList.Count+"\tNativeCurves");
   }
   Console.WriteLine("Additional original clips: "+extra.AnimationList.Count);
  }
  if(args.Length>2 && args[0].IndexOf("zombie",StringComparison.OrdinalIgnoreCase)>=0) {
   var manager=new AssetsManager(); manager.LoadFilesAndFolders(new[]{args[2]});
   var objects=manager.AssetsFileList.SelectMany(f=>f.Objects).ToList();
   var bundle=objects.OfType<AssetBundle>().First();
   var originalRoot=scene.RootFrame;
   var root=new ImportedFrame(0){Name="ZombieAnimations",LocalPosition=Vector3.Zero,LocalRotation=Vector3.Zero,LocalScale=Vector3.One};
   root.AddChild(originalRoot); scene.RootFrame=root;
   foreach(var anim in scene.AnimationList) foreach(var t in anim.TrackList) t.Path=root.Name+"/"+t.Path;
   int index=0;
   foreach(var c in objects.OfType<AnimationClip>().Where(c=>c.m_ClipBindingConstant.genericBindings.Count(b=>b.typeID==ClassIDType.Transform)>10)) {
    Console.WriteLine("Generic candidate "+c.m_Name+" id="+c.m_PathID);
    if(!report.Skip(1).Any(line=>{var fields=line.Split('\t');return fields[1]==c.m_Name&&fields[4]=="False";})) continue;
    GameObject model=null; string sourcePath=null;
    foreach(var entry in bundle.m_Container) {
     var info=entry.Value;
     bool contains=false;
     for(int k=info.preloadIndex;k<info.preloadIndex+info.preloadSize;k++) if(bundle.m_PreloadTable[k].m_PathID==c.m_PathID) contains=true;
     if(!contains) continue;
     AssetStudio.Object asset;
     if(info.asset.TryGet(out asset)&&asset is GameObject) { model=(GameObject)asset; sourcePath=entry.Key; break; }
    }
    if(model==null) throw new Exception("Cannot locate original generic rig: "+c.m_Name);
    var converted=new ModelConverter(model,ImageFormat.Png,new List<AnimationClip>{c});
    var anim=converted.AnimationList.Single();
    var unresolved=anim.TrackList.Where(t=>String.IsNullOrEmpty(t.Path)).ToList();
    if(unresolved.Count==1&&c.m_ClipBindingConstant.genericBindings.Any(b=>b.typeID==ClassIDType.Transform&&b.path==0)) unresolved[0].Path=converted.RootFrame.Path;
    if(anim.TrackList.Count==0||anim.TrackList.Any(t=>String.IsNullOrEmpty(t.Path))) throw new Exception("Incomplete generic zombie: "+c.m_Name);
    string row=report.Skip(1).Single(line=>{var fields=line.Split('\t'); return fields[1]==c.m_Name&&fields[4]=="False";});
    string take=row.Split('\t')[0];
    scene.AnimationList.RemoveAll(a=>a.Name==take); anim.Name=take;
    string oldName=converted.RootFrame.Name;
    converted.RootFrame.Name="LegacyRig_"+(index++);
    root.AddChild(converted.RootFrame);
    foreach(var t in anim.TrackList) {
     string relative=t.Path==oldName?"":t.Path.Substring(oldName.Length);
     t.Path=converted.RootFrame.Path+relative;
     if(root.FindFrameByPath(t.Path)==null) throw new Exception("Unbound legacy zombie bone "+t.Path);
    }
    scene.AnimationList.Add(anim);
    report[report.IndexOf(row)]=take+"\t"+c.m_Name+"\t\t"+anim.SampleRate+"\tFalse\tNativeCurves\t"+anim.TrackList.Count+"\tNativeCurves";
    Console.WriteLine("Preserved generic source rig "+sourcePath+" tracks="+anim.TrackList.Count);
   }
   if(index!=5) throw new Exception("Unexpected generic zombie count "+index);
  }
  File.WriteAllLines(args[1]+".clips.tsv",report);
  Console.WriteLine("EXPORT START takes="+scene.AnimationList.Count);
  if(Environment.GetEnvironmentVariable("REBIRTH_FBX_ASCII")=="1") {
   var durations=new Dictionary<string,float>();
   foreach(var row in report.Skip(1)) { var fields=row.Split('\t'); float duration; if(float.TryParse(fields[2],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out duration)) durations[fields[0]]=duration; }
   FastAsciiFbx.Write(Path.GetFullPath(args[1]),scene,durations);
  } else ModelExporter.ExportFbx(Path.GetFullPath(args[1]),scene,new Fbx.Settings {ExportAllNodes=true,ExportSkins=false,ExportAnimations=true,ExportBlendShape=false,CastToBone=true,ScaleFactor=1,FbxFormat=0});
  Console.WriteLine("EXPORT COMPLETE "+args[1]);
 }
 static bool Different(Vector3 a,Vector3 b) {return Math.Abs(a.X-b.X)>0.00001f||Math.Abs(a.Y-b.Y)>0.00001f||Math.Abs(a.Z-b.Z)>0.00001f;}
 static bool Varies(List<ImportedKeyframe<Vector3>> keys) {return keys.Any(k=>Different(k.value,keys[0].value));}
 static void ReduceConstant(List<ImportedKeyframe<Vector3>> keys) {if(keys.Count>2&&!Varies(keys)) keys.RemoveRange(1,keys.Count-2);}
}
