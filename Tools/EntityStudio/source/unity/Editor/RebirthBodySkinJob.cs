using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio {
 public sealed class RebirthBodySkinJob:IDisposable {
  [Serializable] sealed class Mapping {public float[] uv,foundationUv,positions,normals;public int[] triangles;}
  [Serializable] sealed class Request {
   public string template,templateHash,headRequest,headRequestHash,albedo,albedoHash,coverage,coverageHash,foundation,foundationHash;
   public string sourceMaterial,sourceMaterialHash,sourceSettings,sourceSettingsHash;public Color sourceTint;public BodySkinPatternSettings skinPattern;
  }
  readonly string folder;readonly Request request;readonly BodyRecipe original;
  readonly Stopwatch elapsed=Stopwatch.StartNew();Process process;string errors="";
  public float Progress{get;private set;}
  public string Status{get;private set;}="Preparing shared body skin";
  public string Error{get;private set;}
  public bool Finished{get;private set;}
  public BodyRecipe Candidate{get;private set;}
  public double ElapsedSeconds=>elapsed.Elapsed.TotalSeconds;
  public int? WorkerProcessId{get{try{return process==null?null:(int?)process.Id;}catch(InvalidOperationException){return null;}}}
  static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
  static string Quote(string path){if(string.IsNullOrEmpty(path)||path.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new ArgumentException("Invalid skin path");return "\""+path+"\"";}
  static string Find(string filename){return AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(filename)).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith(filename=="SkinFoundation.png"?"/FaceGeneratorData/SkinFoundation.png":filename=="BodyTemplate.json"?"/BodyGeneratorData/BodyTemplate.json":"/BodyGeneratorData/Backend/"+filename,StringComparison.Ordinal));}
  public static Color SourceTint(FaceRecipe face,Material source){
   if(source.mainTextureScale!=Vector2.one||source.mainTextureOffset!=Vector2.zero)throw new InvalidDataException("Shared skin requires an untransformed generated skin atlas");
   var copy=new Material(source);try{RebirthFaceAppearance.ApplySkin(copy,face);var colour=copy.HasProperty("_Color")?copy.color:Color.white;foreach(float value in new[]{colour.r,colour.g,colour.b})if(float.IsNaN(value)||float.IsInfinity(value)||value<0)throw new InvalidDataException("Invalid generated head material colour");colour.a=1;return colour;}finally{UnityEngine.Object.DestroyImmediate(copy);}
  }
  public static void ValidateSource(TextAsset settings){
   if(settings==null)throw new ArgumentException("Choose generated face settings first");
   var face=JsonUtility.FromJson<FaceRecipe>(settings.text);if(face==null)throw new InvalidDataException("Choose a saved FaceSettings.json with generated skin");face.Validate(RebirthFaceGeneratorWindow.ReadTemplate());
   var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(face.generatedMaterialGuid));if(material==null||material.mainTexture==null)throw new InvalidDataException("Face settings must contain a generated skin material");
   SourceTint(face,material);
   string skin=AssetDatabase.GetAssetPath(material.mainTexture);var config=RebirthFaceImageJob.ReadConfiguration();
   foreach(string path in new[]{skin,Path.Combine(Path.GetDirectoryName(skin),"SourceCoverage.png"),Find("body_skin_backend.py"),Find("skin_patch_transfer.py"),Find("SkinFoundation.png"),Find("BodyTemplate.json"),config.python})if(string.IsNullOrEmpty(path)||!File.Exists(path))throw new FileNotFoundException("Shared skin input missing: "+path);
  }
  public RebirthBodySkinJob(BodyTemplate body,BodyRecipe recipe,TextAsset headSettings){
   try {
    if(headSettings==null)throw new ArgumentException("Choose generated face settings first");
    original=JsonUtility.FromJson<BodyRecipe>(JsonUtility.ToJson(recipe));original.Validate(body);
    var face=JsonUtility.FromJson<FaceRecipe>(headSettings.text);var faceTemplate=RebirthFaceGeneratorWindow.ReadTemplate();face.Validate(faceTemplate);
    string materialPath=AssetDatabase.GUIDToAssetPath(face.generatedMaterialGuid);
    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    if(material==null||material.mainTexture==null)throw new InvalidDataException("Face settings must contain a generated skin material");
    Color sourceTint=SourceTint(face,material);string settingsPath=AssetDatabase.GetAssetPath(headSettings);if(!File.Exists(settingsPath))throw new InvalidDataException("Save generated head settings before sharing skin");
    string skin=AssetDatabase.GetAssetPath(material.mainTexture),coverage=Path.Combine(Path.GetDirectoryName(skin),"SourceCoverage.png");
    string backend=Find("body_skin_backend.py"),foundation=RebirthBodyGeneratorModel.FoundationPath(original),template=Find("BodyTemplate.json");
    var config=RebirthFaceImageJob.ReadConfiguration();
    foreach(string path in new[]{skin,coverage,backend,Find("skin_patch_transfer.py"),foundation,template,config.python})if(string.IsNullOrEmpty(path)||!File.Exists(path))throw new FileNotFoundException("Shared skin input missing: "+path);
    folder=Path.GetFullPath("Library/RebirthBodySkin/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
    string mapping=Path.Combine(folder,"HeadMapping.json");var mesh=RebirthFaceGeneratorModel.Build(faceTemplate,face);
    try {File.WriteAllText(mapping,JsonUtility.ToJson(new Mapping{positions=mesh.vertices.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),normals=mesh.normals.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),uv=mesh.uv.SelectMany(p=>new[]{p.x,p.y}).ToArray(),foundationUv=mesh.uv2.SelectMany(p=>new[]{p.x,p.y}).ToArray(),triangles=mesh.GetTriangles(0)}));}
    finally {UnityEngine.Object.DestroyImmediate(mesh);}
    request=new Request{template=Path.GetFullPath(template),templateHash=Hash(template),headRequest=mapping,headRequestHash=Hash(mapping),albedo=Path.GetFullPath(skin),albedoHash=Hash(skin),coverage=Path.GetFullPath(coverage),coverageHash=Hash(coverage),foundation=Path.GetFullPath(foundation),foundationHash=Hash(foundation)};
    request.skinPattern=original.skinPattern;request.sourceMaterial=Path.GetFullPath(materialPath);request.sourceMaterialHash=Hash(materialPath);request.sourceSettings=Path.GetFullPath(settingsPath);request.sourceSettingsHash=Hash(settingsPath);request.sourceTint=sourceTint;
    File.WriteAllText(Path.Combine(folder,"request.json"),JsonUtility.ToJson(request,true));
    var info=new ProcessStartInfo(config.python,Quote(Path.GetFullPath(backend))+" --job "+Quote(folder)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
    info.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"]="1";
    process=new Process{StartInfo=info};process.OutputDataReceived+=(s,e)=>{};process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(this){errors+=e.Data+"\n";if(errors.Length>12000)errors=errors.Substring(errors.Length-12000);}};
    process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
   }catch {Dispose();throw;}
  }
  public void Poll(){if(Finished)return;try {
   string progress=Path.Combine(folder,"progress.json");try {if(File.Exists(progress)){FaceImageProgress state;using(var stream=new FileStream(progress,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))state=JsonUtility.FromJson<FaceImageProgress>(reader.ReadToEnd());if(state!=null){Progress=Mathf.Max(Progress,state.progress);Status=state.message;}}}catch(IOException){}
   if(!process.HasExited){if(ElapsedSeconds>240)throw new TimeoutException("Shared skin generation exceeded four minutes");return;}
   process.WaitForExit();if(process.ExitCode!=0){File.WriteAllText(Path.Combine(folder,"worker-error.log"),errors);throw new InvalidOperationException("Shared skin generation failed. Details: "+Path.Combine(folder,"worker-error.log"));}
   foreach(var pair in new[]{new[]{request.template,request.templateHash},new[]{request.albedo,request.albedoHash},new[]{request.coverage,request.coverageHash},new[]{request.foundation,request.foundationHash},new[]{request.sourceMaterial,request.sourceMaterialHash},new[]{request.sourceSettings,request.sourceSettingsHash}})if(Hash(pair[0])!=pair[1])throw new InvalidDataException("Skin input changed while generating");
   var currentFace=JsonUtility.FromJson<FaceRecipe>(File.ReadAllText(request.sourceSettings));currentFace.Validate(RebirthFaceGeneratorWindow.ReadTemplate());var currentMaterial=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(currentFace.generatedMaterialGuid));if(currentMaterial==null||SourceTint(currentFace,currentMaterial)!=request.sourceTint)throw new InvalidDataException("Head skin colour changed while generating");
   string target="Assets/BodyGeneratorGenerated/Skin_"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(target);
   foreach(string file in new[]{"BodySkinAlbedo.png","BodySourceCoverage.png","FaceSkinDonorCoverage.png","report.json"}){File.Copy(Path.Combine(folder,file),target+"/"+file);AssetDatabase.ImportAsset(target+"/"+file);}
   var importer=(TextureImporter)AssetImporter.GetAtPath(target+"/BodySkinAlbedo.png");importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.sRGBTexture=true;importer.SaveAndReimport();
   Candidate=original;Candidate.sourceSkinTint=request.sourceTint;Candidate.sourceSkinTintVersion=1;Candidate.skinTextureGuid=AssetDatabase.AssetPathToGUID(target+"/BodySkinAlbedo.png");Candidate.skinReportGuid=AssetDatabase.AssetPathToGUID(target+"/report.json");
   File.WriteAllText(target+"/BodySettings.json",JsonUtility.ToJson(Candidate,true));AssetDatabase.ImportAsset(target+"/BodySettings.json");
   Progress=1;Status="Shared body skin ready. Torso, limbs and rear colour are inferred from head observations; review all angles.";Finished=true;
  }catch(Exception e){Error=e.Message;Status=Error;Finished=true;Dispose();}}
  public void Dispose(){if(process==null)return;try{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}}finally{process.Dispose();process=null;}}
 }
}
