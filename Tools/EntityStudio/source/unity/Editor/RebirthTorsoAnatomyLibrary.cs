using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;using UnityEditor;using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class TorsoAnatomyLibrary {
  public int version;public string license,attribution,sourceArchiveSha256,templateHash,foundationHash,bindingFile,bindingHash;
  public Vector3[] referenceGuides;public TorsoAnatomyComponent[] parts;
 }
 [Serializable] public sealed class TorsoAnatomyComponent {
  public string name,surfaceRole;public int firstVertex,vertexCount,group;public int[] triangles;public Vector3 minimum,maximum;
  public Vector3[] cornerNormals;
  public int[] renderToBinding;
  public int RenderVertexCount=>renderToBinding!=null&&renderToBinding.Length>0?renderToBinding.Length:vertexCount;
  public string ResolvedSurfaceRole=>string.IsNullOrEmpty(surfaceRole)?(group==1?"bone":"bowel"):surfaceRole;
  public bool HasValidSurfaceRole=>group==1?(ResolvedSurfaceRole=="bone"||ResolvedSurfaceRole=="cartilage"||ResolvedSurfaceRole=="muscle"):group==2&&ResolvedSurfaceRole=="bowel";
 }
 public sealed class TorsoAnatomyMesh {public TorsoAnatomyComponent source;public Vector3[] positions,restPositions;}
 // Retain a resource instance while authoring. It caches only selected ranges;
 // library hash/file checks and binary opening are not slider-draw operations.
 public sealed class RebirthTorsoAnatomyLibrary:IDisposable {
  public readonly TorsoAnatomyLibrary definition;
  readonly string path,hash;readonly RebirthCageBindingFile binding;readonly Vector3[] rest;
  readonly Dictionary<string,RebirthCageBindingRange> ranges=new Dictionary<string,RebirthCageBindingRange>();
  public int LoadedComponentCount=>ranges.Count;
  static string Digest(string path){using(var s=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
  static bool Finite(Vector3 p)=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
  public RebirthTorsoAnatomyLibrary(string file,string expectedHash,string templateHash,string foundationHash){
   path=Path.GetFullPath(file);hash=Digest(path);if(string.IsNullOrEmpty(expectedHash)||hash!=expectedHash)throw new InvalidDataException("Torso library revision differs from saved settings");
   var d=JsonUtility.FromJson<TorsoAnatomyLibrary>(File.ReadAllText(path));
   if(d==null||d.version!=1||d.license!="CC-BY-4.0"||string.IsNullOrEmpty(d.attribution)||d.sourceArchiveSha256!="40665852c49f218326590e204db91064a1ecfc3c6f8cbd7bbbcaac62c7cd409e"||d.templateHash!=templateHash||d.foundationHash!=foundationHash||d.bindingFile!="Binding.bytes"||d.referenceGuides==null||d.referenceGuides.Length!=6||d.referenceGuides.Any(p=>!Finite(p))||d.parts==null||d.parts.Length==0||d.parts.Select(p=>p?.name).Distinct().Count()!=d.parts.Length)throw new InvalidDataException("Invalid or incompatible torso anatomy library");
   int end=0;foreach(var part in d.parts){if(part==null||string.IsNullOrEmpty(part.name)||part.firstVertex!=end||part.vertexCount<3||(part.group!=1&&part.group!=2)||!Finite(part.minimum)||!Finite(part.maximum)||part.minimum.x>part.maximum.x||part.minimum.y>part.maximum.y||part.minimum.z>part.maximum.z||part.triangles==null||part.triangles.Length==0||part.triangles.Length%3!=0||part.triangles.Any(i=>i<0||i>=part.RenderVertexCount))throw new InvalidDataException("Invalid anatomical component topology/range");if(part.renderToBinding!=null&&part.renderToBinding.Length>0&&(part.renderToBinding.Length<3||part.renderToBinding.Any(i=>i<0||i>=part.vertexCount)||part.renderToBinding.Distinct().Count()!=part.vertexCount))throw new InvalidDataException("Invalid anatomical render/binding map");end=checked(end+part.vertexCount);}
   if(d.parts.Any(p=>!p.HasValidSurfaceRole))throw new InvalidDataException("Unknown or region-incompatible anatomical surface role");
   if(d.parts.Any(p=>p.cornerNormals!=null&&p.cornerNormals.Length!=0&&(p.cornerNormals.Length!=p.triangles.Length||p.cornerNormals.Any(n=>!Finite(n)||Mathf.Abs(n.magnitude-1)>.001f))))throw new InvalidDataException("Invalid supplied anatomical normal seams");
   binding=new RebirthCageBindingFile(Path.Combine(Path.GetDirectoryName(path),d.bindingFile),d.bindingHash,templateHash,foundationHash);try{if(binding.VertexCount!=end)throw new InvalidDataException("Anatomical component ranges do not cover their binding");rest=binding.ReadRestCage();definition=d;}catch{binding.Dispose();throw;}
  }
  public List<TorsoAnatomyMesh> Evaluate(Vector3[] cage,Bounds woundBounds,int group){
   if(cage==null||cage.Length!=rest.Length||(group!=1&&group!=2)||!Finite(woundBounds.center)||!Finite(woundBounds.size)||woundBounds.size.x<=0||woundBounds.size.y<=0||woundBounds.size.z<=0)throw new InvalidDataException("Invalid torso exposure volume");
   Vector3 minimum=Vector3.one*float.PositiveInfinity,maximum=Vector3.one*float.NegativeInfinity;
   for(int i=0;i<cage.Length;i++){if(!Finite(cage[i]))throw new InvalidDataException("Non-finite torso cage");var delta=cage[i]-rest[i];minimum=Vector3.Min(minimum,delta);maximum=Vector3.Max(maximum,delta);}
   var result=new List<TorsoAnatomyMesh>();
   foreach(var part in definition.parts){if(part.group!=group)continue;var bound=new Bounds();bound.SetMinMax(part.minimum+minimum-Vector3.one*.001f,part.maximum+maximum+Vector3.one*.001f);if(!bound.Intersects(woundBounds))continue;RebirthCageBindingRange range;if(!ranges.TryGetValue(part.name,out range)){range=binding.LoadRange(part.firstVertex,part.vertexCount);ranges.Add(part.name,range);}result.Add(new TorsoAnatomyMesh{source=part,positions=Expand(range.Evaluate(cage),part),restPositions=part.cornerNormals!=null&&part.cornerNormals.Length>0?Expand(range.baseline,part):null});}
   return result;
  }
  public string Export(string folder){
   if(Digest(path)!=hash||Digest(Path.Combine(Path.GetDirectoryName(path),definition.bindingFile))!=definition.bindingHash)throw new InvalidDataException("Torso library changed before export");
   string assets=Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(!path.StartsWith(assets,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Import the torso library into project Assets before export");string projectFile="Assets/"+path.Substring(assets.Length).Replace('\\','/');
   string target=folder+"/TorsoAnatomyLibrary";if(!AssetDatabase.IsValidFolder(target))AssetDatabase.CreateFolder(folder,"TorsoAnatomyLibrary");
   string sourceFolder=Path.GetDirectoryName(projectFile).Replace('\\','/'),targetLibrary=target+"/TorsoAnatomyLibrary_v1.json";
   if(!AssetDatabase.CopyAsset(sourceFolder+"/"+definition.bindingFile,target+"/"+definition.bindingFile)||!AssetDatabase.CopyAsset(projectFile,targetLibrary))throw new IOException("Could not export torso anatomy source assets");return AssetDatabase.AssetPathToGUID(targetLibrary);
  }
  public void Dispose(){ranges.Clear();binding.Dispose();}
  static Vector3[] Expand(Vector3[] values,TorsoAnatomyComponent part){if(part.renderToBinding==null||part.renderToBinding.Length==0)return values;var result=new Vector3[part.renderToBinding.Length];for(int i=0;i<result.Length;i++)result[i]=values[part.renderToBinding[i]];return result;}
 }
}
