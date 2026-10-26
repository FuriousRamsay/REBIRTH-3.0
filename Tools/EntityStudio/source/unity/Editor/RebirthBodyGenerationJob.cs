using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class BodyGenerationReport {
  public int version=1;public string templateHash,extensionHash,referenceGuid,referenceSha256,sourceHeadSettingsSha256,imageReportGuid,skinReportGuid,inputRecipeSha256;public float elapsedSeconds;public string[] stages,limitations;
  public bool detailFromHead;public bool ageFromHead,shapeFromHead;public string topologyHash;public int subdivision;
  public bool authoredSurfaceFrames;public string authoredBodyHash;
 }
 public sealed class RebirthBodyGenerationJob:IDisposable {
  readonly BodyTemplate template;readonly BodyRecipe input;readonly TextAsset head;
  readonly string imageGuid,imageHash,headPath,headHash;readonly bool skinOnly,attachHead,includeSkin,detailOnly;
  readonly Stopwatch watch=Stopwatch.StartNew();readonly List<string> stages=new List<string>();
  RebirthBodyImageJob imageJob;RebirthBodySkinJob skinJob;RebirthHeadSkinDetailJob detailJob;BodyRecipe working;
  public bool Finished{get;private set;}public string Error{get;private set;}
  public string Status{get;private set;}="Preparing body generation";public float Progress{get;private set;}
  public BodyRecipe Candidate{get;private set;}public double ElapsedSeconds=>watch.Elapsed.TotalSeconds;
  public int? WorkerProcessId=>imageJob?.WorkerProcessId??detailJob?.WorkerProcessId??skinJob?.WorkerProcessId;
  public static BodyRecipe Clone(BodyRecipe value)=>JsonUtility.FromJson<BodyRecipe>(JsonUtility.ToJson(value));
  static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
  static string HashText(string value){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
  public RebirthBodyGenerationJob(BodyTemplate data,BodyRecipe recipe,string referenceGuid,TextAsset sourceHead=null,bool skinOnly=false,bool attachHead=false,bool includeSkin=true,bool detailOnly=false){
   try{
    template=data;input=Clone(recipe);input.Validate(data);working=Clone(input);head=sourceHead;this.skinOnly=skinOnly;this.attachHead=attachHead;this.includeSkin=includeSkin;this.detailOnly=detailOnly;imageGuid=referenceGuid;
    if(NeedsDetail){if(head==null)throw new ArgumentException("Choose generated head settings for head-derived surface detail");RebirthHeadSkinDetailJob.ValidateSource(head);}
    if(head!=null){if(includeSkin&&!detailOnly)RebirthBodySkinJob.ValidateSource(head);if(attachHead)RebirthBodyHeadAssembly.ValidateSource(head);headPath=AssetDatabase.GetAssetPath(head);if(!File.Exists(headPath))throw new InvalidDataException("Generated face settings must be saved in this project");headHash=Hash(headPath);}
    if(head!=null&&!skinOnly&&!detailOnly&&(working.inheritHeadAge||working.inheritHeadShape)){RebirthBodyIdentity.InitializeFromHead(working,head.text);stages.Add("Head age/shape initialization (selected options)");}
    if(detailOnly){StartDetail();}
    else if(skinOnly){if(head==null)throw new ArgumentException("Choose generated face settings first");if(NeedsDetail)StartDetail();else StartSkin();}
    else{string path=AssetDatabase.GUIDToAssetPath(imageGuid);if(!File.Exists(path))throw new FileNotFoundException("Choose a full-body reference image");imageHash=Hash(path);imageJob=new RebirthBodyImageJob(data,working,imageGuid);}
   }catch{Dispose();throw;}
  }
  void CheckSources(){if(!skinOnly&&!detailOnly&&Hash(AssetDatabase.GUIDToAssetPath(imageGuid))!=imageHash)throw new InvalidDataException("Body reference changed during generation");if(head!=null&&Hash(headPath)!=headHash)throw new InvalidDataException("Head settings changed during generation");var current=RebirthBodyGeneratorModel.ReadTemplate();if(current.contentHash!=template.contentHash||current.extensionHash!=template.extensionHash||(working.subdivision>0&&current.topologyHash!=template.topologyHash))throw new InvalidDataException("Body template changed during generation");}
  bool NeedsDetail=>detailOnly||working.includeHeadDetail;
  bool NeedsSkin=>!detailOnly&&head!=null&&(skinOnly||includeSkin);
  float ImageEnd=>NeedsSkin?.5f:NeedsDetail?.75f:.95f;
  float DetailStart=>detailOnly||skinOnly?0:ImageEnd;
  float DetailEnd=>NeedsSkin?(skinOnly?.3f:.65f):.95f;
  float SkinStart=>skinOnly?(NeedsDetail?.3f:0):(NeedsDetail?.65f:.5f);
  void StartDetail(){CheckSources();Status="Sampling head surface detail";detailJob=new RebirthHeadSkinDetailJob(template,working,head);}
  void StartSkin(){CheckSources();Status="Building shared body skin";skinJob=new RebirthBodySkinJob(template,working,head);}
  public void Poll(){if(Finished)return;try{
   if(imageJob!=null){imageJob.Poll();Progress=Mathf.Max(Progress,imageJob.Progress*ImageEnd);Status=imageJob.Status;if(!imageJob.Finished)return;if(imageJob.Error!=null)throw new InvalidOperationException(imageJob.Error);working=Clone(imageJob.Candidate);imageJob.Dispose();imageJob=null;stages.Add("Visible pose/proportion initialization");if(NeedsDetail){StartDetail();return;}if(NeedsSkin){StartSkin();return;}Complete();return;}
   if(detailJob!=null){detailJob.Poll();Progress=Mathf.Max(Progress,DetailStart+detailJob.Progress*(DetailEnd-DetailStart));Status=detailJob.Status;if(!detailJob.Finished)return;if(detailJob.Error!=null)throw new InvalidOperationException(detailJob.Error);working=Clone(detailJob.Candidate);detailJob.Dispose();detailJob=null;stages.Add("Pore detail from observed head-normal patch");if(NeedsSkin){StartSkin();return;}Complete();return;}
   if(skinJob!=null){skinJob.Poll();Progress=Mathf.Max(Progress,SkinStart+skinJob.Progress*(.95f-SkinStart));Status=skinJob.Status;if(!skinJob.Finished)return;if(skinJob.Error!=null)throw new InvalidOperationException(skinJob.Error);working=Clone(skinJob.Candidate);skinJob.Dispose();skinJob=null;stages.Add("Shared skin colour from generated head");Complete();}
  }catch(Exception e){Error=e.Message;Status="Body generation failed: "+Error+". Previous body retained.";Finished=true;Dispose();}}
  void Complete(){
   if(head!=null&&attachHead){working.attachedHeadSettingsGuid=AssetDatabase.AssetPathToGUID(headPath);stages.Add("Authored head and neck connection");}
   CheckSources();working.Validate(template);Mesh mesh=null;Material material=null;try{mesh=RebirthBodyGeneratorModel.Build(template,working);material=RebirthBodyGeneratorModel.Skin(working);if(!string.IsNullOrEmpty(working.attachedHeadSettingsGuid)){using(var assembly=RebirthBodyHeadAssembly.Build(mesh,working,false))using(var wounds=RebirthBodyDeepWounds.Build(template,working,assembly.connection.body,material)){} }else{using(var wounds=RebirthBodyDeepWounds.Build(template,working,mesh,material)){} } }finally{if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);if(material!=null)UnityEngine.Object.DestroyImmediate(material);}
   if(head!=null&&!detailOnly)working.sourceHeadSettingsGuid=AssetDatabase.AssetPathToGUID(headPath);
   var report=new BodyGenerationReport{detailFromHead=NeedsDetail,subdivision=working.subdivision,topologyHash=working.topologyHash,ageFromHead=head!=null&&!skinOnly&&!detailOnly&&working.inheritHeadAge,shapeFromHead=head!=null&&!skinOnly&&!detailOnly&&working.inheritHeadShape,templateHash=template.contentHash,extensionHash=template.extensionHash,referenceGuid=working.referenceGuid,referenceSha256=imageHash??"",sourceHeadSettingsSha256=headHash??"",imageReportGuid=working.bodyImageReportGuid,skinReportGuid=working.skinReportGuid,inputRecipeSha256=HashText(JsonUtility.ToJson(input)),elapsedSeconds=(float)ElapsedSeconds,stages=stages.ToArray(),limitations=new[]{detailOnly?"Body shape and colour retained; only head-derived surface detail initialized.":"Body proportions are initialized from estimated visible joints; full surface/silhouette fitting is unfinished.",NeedsSkin?"Unseen body skin is inferred from generated-head observations; final regional body appearance remains unfinished.":"Existing skin and surface maps were retained; no skin was inferred from the body photograph.","Manual composition, pigment and surface controls are preserved and remain editable."}};
   report.authoredBodyHash=working.authoredBodyHash;report.authoredSurfaceFrames=working.authoredSurfaceFrames;
   string folder="Assets/BodyGeneratorGenerated/Generation_"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);string path=folder+"/GenerationReport.json";File.WriteAllText(path,JsonUtility.ToJson(report,true));AssetDatabase.ImportAsset(path);working.generationReportGuid=AssetDatabase.AssetPathToGUID(path);File.WriteAllText(folder+"/BodySettings.json",JsonUtility.ToJson(working,true));AssetDatabase.ImportAsset(folder+"/BodySettings.json");Candidate=working;Progress=1;Status="Generated body candidate ready. Edit sliders, then Apply or Discard.";Finished=true;
  }
  public void Dispose(){imageJob?.Dispose();imageJob=null;detailJob?.Dispose();detailJob=null;skinJob?.Dispose();skinJob=null;if(!Finished){Error="Cancelled";Status="Body generation cancelled; previous body retained.";Finished=true;}}
  public static void ExportReport(BodyRecipe saved,string folder){
   if(!string.IsNullOrEmpty(saved.generationReportGuid)){var source=AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(saved.generationReportGuid));if(source==null)throw new FileNotFoundException("Body generation report is missing");var report=JsonUtility.FromJson<BodyGenerationReport>(source.text);if(report.version!=1||report.templateHash!=saved.templateHash||report.authoredSurfaceFrames!=saved.authoredSurfaceFrames)throw new InvalidDataException("Body generation report does not match the recipe template or surface deformation mode");report.referenceGuid=saved.referenceGuid;report.imageReportGuid=saved.bodyImageReportGuid;report.skinReportGuid=saved.skinReportGuid;string path=folder+"/GenerationReport.json";File.WriteAllText(path,JsonUtility.ToJson(report,true));AssetDatabase.ImportAsset(path);saved.generationReportGuid=AssetDatabase.AssetPathToGUID(path);}
   // The exported result is self contained; regeneration can accept another
   // generated head explicitly. The historical source hash remains in the report.
   saved.sourceHeadSettingsGuid="";
  }
 }
}
