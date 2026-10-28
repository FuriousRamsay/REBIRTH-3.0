using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AssetStudio;
class ExportLogger : ILogger {
 public void Log(LoggerEvent level,string text,bool ignore) { if(level >= LoggerEvent.Info) Console.WriteLine(level+": "+text); }
}
class ExportAnimations {
 static void Main(string[] args) {
  Logger.Default=new ExportLogger();
  var m=new AssetsManager();
  m.LoadFilesAndFolders(args);
  foreach(var f in m.AssetsFileList) {
   Console.WriteLine("FILE "+f.fileName+" "+f.Objects.Count);
   foreach(var g in f.Objects.GroupBy(o=>o.type)) Console.WriteLine(g.Key+" "+g.Count());
   foreach(var c in f.Objects.OfType<AnimatorController>()) Console.WriteLine("CONTROLLER "+c.m_Name+" clips="+c.m_AnimationClips.Count);
   foreach(var a in f.Objects.OfType<Animator>()) { GameObject go; a.m_GameObject.TryGet(out go); Console.WriteLine("ANIMATOR "+a.m_PathID+" "+(go==null?"?":go.m_Name)); }
   File.WriteAllLines(Path.Combine(Environment.CurrentDirectory,"Tools/AnimationExport/"+f.fileName+".clips.txt"),f.Objects.OfType<AnimationClip>().Select(c=>c.m_PathID+"\t"+c.m_Name));
  }
  var all=m.AssetsFileList.SelectMany(f=>f.Objects).ToList();
  var groups=all.OfType<AnimationClip>().GroupBy(c=>c.assetsFile.fileName);
  foreach(var group in groups) {
   if(group.Count()<100) continue;
   string rig=group.Count()<500?"ZAnimBase":"baseRig";
   var a=all.OfType<Animator>().First(x=> { GameObject go; return x.m_GameObject.TryGet(out go)&&go.m_Name==rig; });
   var converted=new ModelConverter(a,ImageFormat.Png,group.ToList());
   File.WriteAllLines("Tools/AnimationExport/"+rig+".conversion.tsv",converted.AnimationList.Select(x=>x.Name+"\t"+x.TrackList.Count+"\t"+x.TrackList.Count(t=>string.IsNullOrEmpty(t.Path))));
  }
 }
}


