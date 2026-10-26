using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class HairReferenceConfiguration {public string python,repository,checkpoint,checkpointSha256,candidates;}
 public sealed class RebirthHairReferenceJob : IDisposable {
  [Serializable] sealed class Region {public string name;public float confidentFraction;public float[] medianObservedSrgb;}
  [Serializable] sealed class Observation {public string sourceSha256;public Region[] regions;}
  [Serializable] sealed class CandidateSource {public string sourceHash;public float eyeSpacing;}
  [Serializable] sealed class Timing {public double[] stageSeconds;public double importSeconds,totalSeconds;}
  DateTime stageStart;double[] stageSeconds=new double[3];Process process;readonly DateTime start=DateTime.UtcNow;int stage;string errors="",reference,referenceGuid,folder,sourceHash;HairReferenceConfiguration config;FaceImageConfiguration face;
  public bool Finished{get;private set;}public string Error{get;private set;}public string ResultGuid{get;private set;}
  public float Progress{get;private set;}public string Status{get;private set;}public double ElapsedSeconds=>(DateTime.UtcNow-start).TotalSeconds;
  public static string ConfigurationPath=>Path.GetFullPath("ProjectSettings/RebirthHairReference.json");
  public static bool ApplyObservedColour(FaceRecipe recipe,RebirthReferenceHairMatcher.Report report){
   if(!report.observedHairColourAvailable||recipe.nativeAppearance==null)return false;
   var settings=recipe.nativeAppearance;
   if(settings.customTint&&!settings.tintFromReference)return false;
   settings.customTint=true;settings.tint=report.observedHairColour;settings.tintFromReference=true;settings.Validate();return true;
  }
  public static void AttachObservedColour(RebirthReferenceHairMatcher.Report report,string observationPath){
   report.observedHairColourAvailable=false;
   var observation=JsonUtility.FromJson<Observation>(File.ReadAllText(observationPath));
   if(observation.sourceSha256!=report.referenceSha256)throw new InvalidDataException("Hair colour observation belongs to another reference.");
   var hair=(observation.regions??new Region[0]).FirstOrDefault(r=>r.name=="hair");var rgb=hair?.medianObservedSrgb;
   if(hair==null||hair.confidentFraction<.003f||rgb==null||rgb.Length!=3)return;
   foreach(float value in rgb)if(float.IsNaN(value)||float.IsInfinity(value)||value<0||value>1)throw new InvalidDataException("Invalid observed hair colour.");
   report.observedHairColour=new Color(rgb[0],rgb[1],rgb[2],1);report.observedHairColourAvailable=true;
  }
  public static HairReferenceConfiguration ReadConfiguration()=>File.Exists(ConfigurationPath)?JsonUtility.FromJson<HairReferenceConfiguration>(File.ReadAllText(ConfigurationPath)):new HairReferenceConfiguration();
  static string Quote(string value){if(string.IsNullOrEmpty(value)||value.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new ArgumentException("Invalid reference matching path.");return "\""+value+"\"";}
  static string Worker(string name){string path=AssetDatabase.FindAssets(name).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/"+name+".py",StringComparison.Ordinal));if(path==null)throw new FileNotFoundException("Missing reference matching worker: "+name);return Path.GetFullPath(path);}
  public RebirthHairReferenceJob(string guid){try{
   config=ReadConfiguration();face=RebirthFaceImageJob.ReadConfiguration();referenceGuid=guid;reference=Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid));
   foreach(string path in new[]{reference,config.python,config.checkpoint,config.candidates,face.python,face.model,face.segmentationModel})if(!File.Exists(path))throw new FileNotFoundException("Configure reference-hair matching and image observation paths first: "+path);
   if(!Directory.Exists(config.repository))throw new DirectoryNotFoundException("The local DINOv2 repository is unavailable.");
   var source=JsonUtility.FromJson<CandidateSource>(File.ReadAllText(config.candidates));sourceHash=RebirthNativeAppearanceLibrary.Catalog.sourceHash;if(source.sourceHash!=sourceHash||source.eyeSpacing<=0)throw new InvalidDataException("Candidate library has no valid matching source/scale metadata.");
   folder=Path.GetFullPath("Library/RebirthHairReference/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);StartStage();
  }catch{Dispose();throw;}}
  void StartStage(){
   string executable=stage==1?config.python:face.python,arguments;
   if(stage==0){Status="Observing reference hair and eye landmarks";arguments=Quote(Worker("reference_hair_observe"))+" --reference "+Quote(reference)+" --model "+Quote(face.model)+" --segmentation-model "+Quote(face.segmentationModel)+" --segmentation-sha256 "+Quote(face.segmentationModelSha256)+" --output "+Quote(folder);}
   else if(stage==1){Status="Comparing the reference with local hairstyles";arguments=Quote(Worker("reference_hair_retrieval"))+" --repository "+Quote(config.repository)+" --checkpoint "+Quote(config.checkpoint)+" --checkpoint-sha256 "+Quote(config.checkpointSha256)+" --reference "+Quote(reference)+" --mask "+Quote(Path.Combine(folder,"hairConfidence.png"))+" --candidates "+Quote(config.candidates)+" --output "+Quote(Path.Combine(folder,"Ranking.json"))+" --cache "+Quote(Path.GetFullPath("Library/RebirthHairReferenceFeatures"))+" --progress "+Quote(Path.Combine(folder,"progress.json"));}
   else{Status="Measuring visible length and preparing suggestions";arguments=Quote(Worker("reference_hair_scale"))+" --ranking "+Quote(Path.Combine(folder,"Ranking.json"))+" --candidates "+Quote(config.candidates)+" --fit "+Quote(Path.Combine(folder,"fit.json"))+" --observations "+Quote(Path.Combine(folder,"AppearanceObservations.json"))+" --reference-mask "+Quote(Path.Combine(folder,"hairConfidence.png"))+" --output "+Quote(Path.Combine(folder,"MeasuredRanking.json"))+" --tool-report-folder "+Quote(Path.Combine(folder,"Report"));}
   stageStart=DateTime.UtcNow;errors="";process=new Process{StartInfo=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true}};process.StartInfo.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"]="1";process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(this){errors+=e.Data+"\n";if(errors.Length>12000)errors=errors.Substring(errors.Length-12000);}};process.OutputDataReceived+=(s,e)=>{};process.Start();process.BeginErrorReadLine();process.BeginOutputReadLine();Progress=stage==0?.05f:stage==1?.35f:.9f;
  }
  public void Poll(){if(Finished)return;try{if(stage==1){string progress=Path.Combine(folder,"progress.json");try{if(File.Exists(progress)){FaceImageProgress data;using(var stream=new FileStream(progress,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))data=JsonUtility.FromJson<FaceImageProgress>(reader.ReadToEnd());Progress=Mathf.Clamp(data.progress,.35f,.85f);Status=data.message;}}catch(IOException){}}if(!process.HasExited)return;process.WaitForExit();int code=process.ExitCode;stageSeconds[stage]=(DateTime.UtcNow-stageStart).TotalSeconds;process.Dispose();process=null;if(code!=0)throw new InvalidOperationException("Reference matching failed: "+errors);if(stage==0){var observation=JsonUtility.FromJson<Observation>(File.ReadAllText(Path.Combine(folder,"AppearanceObservations.json")));var hair=(observation.regions??new Region[0]).FirstOrDefault(r=>r.name=="hair");
    // Minimum observation support only; this does not classify baldness or infer hidden hair.
    if(hair==null||hair.confidentFraction<.003f){string reportFolder=Path.Combine(folder,"Report");Directory.CreateDirectory(reportFolder);File.WriteAllText(Path.Combine(reportFolder,"HairMatchReport.json"),JsonUtility.ToJson(new RebirthReferenceHairMatcher.Report{method="Semantic observation support gate",status="Not enough visible hair for a reliable comparison. Current hairstyle retained.",sourceHash=sourceHash,referenceSha256=observation.sourceSha256,insufficientSupport=true,scores=new RebirthReferenceHairMatcher.Score[0]},true));ResultGuid=RebirthFaceGeneratorWindow.ImportHairComparison(Path.Combine(reportFolder,"HairMatchReport.json"),referenceGuid);Progress=1;Status="Insufficient visible hair; current hairstyle retained";Finished=true;return;}
   }if(++stage<3){StartStage();return;}if(RebirthNativeAppearanceLibrary.Catalog.sourceHash!=sourceHash)throw new InvalidDataException("The game appearance library changed during matching.");string reportPath=Path.Combine(folder,"Report","HairMatchReport.json");var report=JsonUtility.FromJson<RebirthReferenceHairMatcher.Report>(File.ReadAllText(reportPath));AttachObservedColour(report,Path.Combine(folder,"AppearanceObservations.json"));File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));var importStart=DateTime.UtcNow;ResultGuid=RebirthFaceGeneratorWindow.ImportHairComparison(Path.Combine(folder,"Report","HairMatchReport.json"),referenceGuid);File.WriteAllText(Path.Combine(folder,"StageTimings.json"),JsonUtility.ToJson(new Timing{stageSeconds=stageSeconds,importSeconds=(DateTime.UtcNow-importStart).TotalSeconds,totalSeconds=ElapsedSeconds},true));Progress=1;Status="Hairstyle suggestions ready; current head and hair retained";Finished=true;}catch(Exception e){Error=e.Message;Status=Error;Finished=true;Dispose();}}
  public void Dispose(){if(process!=null){try{if(!process.HasExited)process.Kill();}finally{process.Dispose();process=null;}}}
 }
}


