using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace RebirthEntityStudio {
 public sealed class RebirthNativeFacialFitJob : IDisposable {
  [Serializable] sealed class Geometry {public float[] positions,uv;public int[] triangles;}
  [Serializable] sealed class Item {public string key,file;}
  [Serializable] sealed class Manifest {public string sourceHash;public Item[] styles;}
  Process process;bool started,finalizing;Mesh reference;string output,input,stderr="",sourceHash;Item[] items;int next;
  NativeFacialRootAttachment.Builder builder;readonly List<NativeFacialStyleAttachment> bindings=new List<NativeFacialStyleAttachment>();
  readonly DateTime start=DateTime.UtcNow;
  public bool Finished{get;private set;}public string Error{get;private set;}public string ResultGuid{get;private set;}
  public float Progress{get;private set;}public string Status{get;private set;}="Preparing facial-hair attachments";
  public double ElapsedSeconds=>(DateTime.UtcNow-start).TotalSeconds;
  static string Quote(string value){if(value==null||value.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new ArgumentException("Invalid facial fit path.");return "\""+value+"\"";}
  public RebirthNativeFacialFitJob(Mesh head){try{
   var config=RebirthNativeAppearanceImport.Configuration();
   string backend=AssetDatabase.FindAssets("native_facial_fit").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/native_facial_fit.py",StringComparison.Ordinal));
   if(!File.Exists(config.python)||backend==null)throw new FileNotFoundException("The local facial fitting worker or Python runtime is unavailable.");
   var catalog=RebirthNativeAppearanceLibrary.Catalog;sourceHash=catalog.sourceHash;
   reference=UnityEngine.Object.Instantiate(head);reference.hideFlags=HideFlags.HideAndDontSave;
   string job=Path.GetFullPath("Library/RebirthNativeFacialFit/"+Guid.NewGuid().ToString("N"));input=Path.Combine(job,"Input");output=Path.Combine(job,"Output");Directory.CreateDirectory(input);Directory.CreateDirectory(output);
   File.WriteAllText(Path.Combine(input,"SourceHead.json"),JsonUtility.ToJson(RebirthNativeAppearanceLibrary.Head("male")));
   File.WriteAllText(Path.Combine(input,"TargetHead.json"),JsonUtility.ToJson(new Geometry{positions=reference.vertices.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),triangles=reference.GetTriangles(0)}));
   items=catalog.styles.Where(s=>s.family!="hair").Select(style=>{
    var mesh=RebirthNativeAppearanceLibrary.Source(style).sharedMesh;var rest=RebirthNativeAppearanceLibrary.Fit(style);string file=style.key.Replace(':','_')+".json";
    File.WriteAllText(Path.Combine(input,"Source_"+file),JsonUtility.ToJson(new NativeFittedVertices{name=rest.name,positions=rest.positions,normals=mesh.normals.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),tangents=mesh.tangents.SelectMany(p=>new[]{p.x,p.y,p.z,p.w}).ToArray()}));
    File.WriteAllText(Path.Combine(input,"Topology_"+file),JsonUtility.ToJson(new Geometry{triangles=mesh.triangles,uv=mesh.uv.SelectMany(p=>new[]{p.x,p.y}).ToArray()}));return new Item{key=style.key,file=file};
   }).ToArray();if(items.Length==0)throw new InvalidDataException("The local library contains no facial-hair styles.");
   File.WriteAllText(Path.Combine(input,"Sources.json"),JsonUtility.ToJson(new Manifest{sourceHash=sourceHash,styles=items}));
   var info=new ProcessStartInfo(config.python,Quote(Path.GetFullPath(backend))+" --input "+Quote(input)+" --output "+Quote(output)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   process=new Process{StartInfo=info};process.OutputDataReceived+=(s,e)=>{};process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(this){stderr+=e.Data+"\n";if(stderr.Length>12000)stderr=stderr.Substring(stderr.Length-12000);}};
   process.Start();started=true;process.BeginOutputReadLine();process.BeginErrorReadLine();
  }catch{Dispose();throw;}}
  public void Poll(){if(Finished)return;try{
   if(!finalizing){string progress=Path.Combine(output,"progress.json");try{if(File.Exists(progress)){var p=JsonUtility.FromJson<NativeAppearanceProgress>(File.ReadAllText(progress));Progress=Mathf.Clamp01(p.progress)*.92f;Status=p.message;}}catch(IOException){}
    if(!process.HasExited)return;process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException("Facial-hair fit failed: "+stderr);
    if(RebirthNativeAppearanceLibrary.Catalog.sourceHash!=sourceHash)throw new InvalidDataException("The native library changed during fitting; run the fit again.");
    builder=new NativeFacialRootAttachment.Builder(reference);finalizing=true;
   }
   if(next<items.Length){var item=items[next];string file=Path.Combine(output,"RootBound_"+item.file);if(!File.Exists(file))throw new InvalidDataException("A fitted facial style is missing: "+item.key);
    var fitted=JsonUtility.FromJson<NativeFittedVertices>(File.ReadAllText(file));bindings.Add(builder.Bind(item.key,fitted,RebirthNativeAppearanceLibrary.Source(RebirthNativeAppearanceLibrary.Style(item.key)).sharedMesh));next++;Progress=.92f+.07f*next/items.Length;Status="Saving attachment "+next+" / "+items.Length;return;
   }
   string folder="Assets/EntityStudioGenerated/NativeFacialFits/"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);string path=folder+"/FacialHairFit.json";
   File.WriteAllText(path,JsonUtility.ToJson(new NativeFacialAttachmentData{sourceHash=sourceHash,skinTopologyHash=builder.skinTopologyHash,styles=bindings.ToArray()}));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
   ResultGuid=AssetDatabase.AssetPathToGUID(path);if(string.IsNullOrEmpty(ResultGuid))throw new IOException("The facial-hair fit could not be saved as an editor asset.");
   Finished=true;Progress=1;Status="Facial hair fitted to the head";
  }catch(Exception e){Finished=true;Error=e.Message;Status=Error;}}
  public void Dispose(){if(process!=null){if(started&&!process.HasExited)process.Kill();process.Dispose();process=null;}if(reference!=null){UnityEngine.Object.DestroyImmediate(reference);reference=null;}}
 }
}
