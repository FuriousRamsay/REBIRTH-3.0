using System;using System.IO;using UnityEditor;using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable]public sealed class CharacterGenerationProfile {
  public int version=1;public string name,headSettingsGuid,bodySettingsGuid,headSettingsSha256,bodySettingsSha256,portraitGuid,bodyReferenceGuid;public string[] limitations;
 }
 // Compose the existing generators; this class does not introduce another
 // shape evaluator, hair path, mouth implementation or texture baker.
 public sealed class RebirthCharacterGenerationJob:IDisposable {
  readonly FaceTemplate faceTemplate;readonly BodyTemplate bodyTemplate;readonly BodyRecipe originalBody;readonly bool includeSkin;
  readonly Texture2D bodyReference;readonly string portraitGuid,bodyGuid;readonly string name;
  RebirthFaceImageJob faceJob;RebirthBodyGenerationJob bodyJob;FaceRecipe head;TextAsset headAsset;
  public bool Finished{get;private set;}public string Error{get;private set;}public string Status{get;private set;}="Preparing character";public float Progress{get;private set;}
  public BodyRecipe Candidate{get;private set;}public CharacterGenerationProfile Profile{get;private set;}public TextAsset ProfileAsset{get;private set;}
  public string OutputFolder{get;private set;}
  public static T Clone<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
  public static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
  public RebirthCharacterGenerationJob(string characterName,TextAsset savedHead,TextAsset savedBody,Texture2D portrait,Texture2D bodyImage,bool generateSkin){
   try{
    name=string.IsNullOrWhiteSpace(characterName)?"Character":characterName;faceTemplate=RebirthFaceGeneratorWindow.ReadTemplate();bodyTemplate=RebirthBodyGeneratorModel.ReadTemplate();includeSkin=generateSkin;bodyReference=bodyImage;
    originalBody=savedBody==null?new BodyRecipe():JsonUtility.FromJson<BodyRecipe>(savedBody.text);if(originalBody==null)throw new InvalidDataException("Invalid body settings");originalBody.Validate(bodyTemplate);
    if(savedHead==null&&portrait==null)throw new ArgumentException("Choose saved head settings or a portrait");
    head=savedHead==null?FaceRecipe.FromPreset(faceTemplate,0):JsonUtility.FromJson<FaceRecipe>(savedHead.text);if(head==null)throw new InvalidDataException("Invalid head settings");head.Validate(faceTemplate);
    portraitGuid=portrait==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(portrait));bodyGuid=bodyImage==null?"":AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(bodyImage));
    if(portrait!=null&&string.IsNullOrEmpty(portraitGuid)||bodyImage!=null&&string.IsNullOrEmpty(bodyGuid))throw new ArgumentException("Reference images must be saved project assets");
    if(portrait!=null){faceJob=new RebirthFaceImageJob(faceTemplate,head,portrait);Status="Generating head and appearance";}
    else{RebirthBodyHeadAssembly.ValidateSource(savedHead);SaveHeadAndStartBody();}
   }catch{Dispose();throw;}
  }
  void EnsureFolder(){if(OutputFolder!=null)return;OutputFolder="Assets/CharacterGeneratorGenerated/Character_"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(OutputFolder);AssetDatabase.Refresh();}
  void SaveHeadAndStartBody(){
   EnsureFolder();string path=OutputFolder+"/HeadSettings.json";File.WriteAllText(path,JsonUtility.ToJson(head,true));AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);headAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(path);RebirthBodyHeadAssembly.ValidateSource(headAsset);
   var body=Clone(originalBody);body.attachedHeadSettingsGuid=AssetDatabase.AssetPathToGUID(path);
   if(bodyReference!=null||includeSkin){bodyJob=new RebirthBodyGenerationJob(bodyTemplate,body,bodyGuid,headAsset,bodyReference==null,true,includeSkin);Status="Generating body and shared skin";Progress=Mathf.Max(Progress,.55f);}
   else{Status="Assembling saved head and body";Progress=.9f;Complete(body);}
  }
  public void Poll(){if(Finished)return;try{
   if(faceJob!=null){faceJob.Poll();Progress=Mathf.Max(Progress,faceJob.Progress*.55f);Status=faceJob.Status;if(!faceJob.Finished)return;if(faceJob.Error!=null)throw new InvalidOperationException(faceJob.Error);head=Clone(faceJob.Candidate);faceJob.Dispose();faceJob=null;SaveHeadAndStartBody();return;}
   if(bodyJob!=null){bodyJob.Poll();Progress=Mathf.Max(Progress,.55f+bodyJob.Progress*.4f);Status=bodyJob.Status;if(!bodyJob.Finished)return;if(bodyJob.Error!=null)throw new InvalidOperationException(bodyJob.Error);var body=Clone(bodyJob.Candidate);bodyJob.Dispose();bodyJob=null;Complete(body);}
  }catch(Exception error){Error=error.Message;Status="Character generation failed: "+Error;Finished=true;Dispose();}}
  void Complete(BodyRecipe body){
   body.name=name;body.Validate(bodyTemplate);Mesh mesh=null;Material material=null;try{
    mesh=RebirthBodyGeneratorModel.Build(bodyTemplate,body);material=RebirthBodyGeneratorModel.Skin(body);
    using(var assembly=RebirthBodyHeadAssembly.Build(mesh,body))using(var wounds=RebirthBodyDeepWounds.Build(bodyTemplate,body,assembly.connection.body,material)){}
   }finally{if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);if(material!=null)UnityEngine.Object.DestroyImmediate(material);}
   EnsureFolder();string bodyPath=OutputFolder+"/BodySettings.json";File.WriteAllText(bodyPath,JsonUtility.ToJson(body,true));AssetDatabase.ImportAsset(bodyPath,ImportAssetOptions.ForceSynchronousImport);string headPath=AssetDatabase.GetAssetPath(headAsset);
   Profile=new CharacterGenerationProfile{name=name,headSettingsGuid=AssetDatabase.AssetPathToGUID(headPath),bodySettingsGuid=AssetDatabase.AssetPathToGUID(bodyPath),headSettingsSha256=Hash(headPath),bodySettingsSha256=Hash(bodyPath),portraitGuid=portraitGuid,bodyReferenceGuid=bodyGuid,limitations=new[]{"Image inference retains the existing generators' visibility and likeness limits.","Unseen anatomy is estimated; original hair and final body/wound appearance remain unfinished.","Export is an authoring assembly; game rigging and animation remain separate."}};
   string profilePath=OutputFolder+"/CharacterSettings.json";File.WriteAllText(profilePath,JsonUtility.ToJson(Profile,true));AssetDatabase.ImportAsset(profilePath,ImportAssetOptions.ForceSynchronousImport);ProfileAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(profilePath);Candidate=body;Progress=1;Finished=true;Status="Character ready. Edit head or body settings, or export the assembled character.";
  }
  public static CharacterGenerationProfile ReadProfile(TextAsset asset){
   if(asset==null)throw new ArgumentException("Choose character settings");var p=JsonUtility.FromJson<CharacterGenerationProfile>(asset.text);if(p==null||p.version!=1)throw new InvalidDataException("Unsupported character settings");
   foreach(bool face in new[]{true,false}){string path=AssetDatabase.GUIDToAssetPath(face?p.headSettingsGuid:p.bodySettingsGuid);if(string.IsNullOrEmpty(path)||!File.Exists(path)||Hash(path)!=(face?p.headSettingsSha256:p.bodySettingsSha256))throw new InvalidDataException("Character dependency is missing or was edited; assemble the updated head/body settings as a new result");}
   return p;
  }
  public void Dispose(){faceJob?.Dispose();faceJob=null;bodyJob?.Dispose();bodyJob=null;if(!Finished){Finished=true;Error="Cancelled";Status="Character generation cancelled; previous character retained.";}}
 }
}
