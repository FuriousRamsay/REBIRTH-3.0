using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace RebirthEntityStudio {
 [Serializable] public sealed class FaceImageConfiguration {public string python,model,segmentationModel,segmentationModelSha256;public bool matchReferenceHair=true,matchReferenceHairColour=true;public bool applyClosestReferenceHair;}
 [Serializable] public sealed class FaceImageControl {public string id;public float value,minimum,maximum;public float[] positive,negative;}
 [Serializable] public sealed class FaceImageRequest {public string stage,reference,segmentationModel,segmentationModelSha256,skinFoundation,skinFoundationSha256,skinFoundationKey;public float[] vertices,screen,normals,uv,foundationUv,eyeCenters,eyeRadii;public int[] triangles,teethTriangles,eyeTriangles,liningTriangles;public int round,vertexLimit,eyeCount;public bool linearColorSpace;public FaceImageControl[] controls;}
 [Serializable] public sealed class FaceImageFit {public FaceValue[] values;public float rms;public float[] transform;}
 [Serializable] public sealed class FaceImageRefinement {public FaceVertexOffset[] offsets;}
 [Serializable] public sealed class FaceImageProgress {public float progress;public string message;}

 // One local child process per generation stage. Results are isolated until explicitly applied.
 public sealed class RebirthFaceImageJob : IDisposable {
  public string Folder {get;private set;} public string Status {get;private set;}
  public FaceRecipe Candidate {get;private set;} public Material CandidateMaterial {get;private set;}
  public bool Finished {get;private set;} public string Error {get;private set;}
  public float Progress {get;private set;}
  public double ElapsedSeconds => ((Finished?ended:DateTime.UtcNow)-created).TotalSeconds;
  DateTime created=DateTime.UtcNow,ended;IEnumerator preparation;double nextProgressRead;
  FaceTemplate template;FaceRecipe original;FaceImageConfiguration config;string reference,backend,skinFoundation,skinFoundationSha256,skinFoundationKey;Process process;int stage,round,calibrationRound;string generatedFolder;DateTime started;
  public static string ConfigurationPath=>Path.GetFullPath("ProjectSettings/RebirthFaceImage.json");
  public static FaceImageConfiguration ReadConfiguration(){return File.Exists(ConfigurationPath)?JsonUtility.FromJson<FaceImageConfiguration>(File.ReadAllText(ConfigurationPath)):new FaceImageConfiguration();}
  public static string BackendPath(){return AssetDatabase.FindAssets("face_image_backend").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.EndsWith("/face_image_backend.py",StringComparison.Ordinal));}
  static string Quote(string value){if(value.Contains("\"")||value.Contains("\n")||value.Contains("\r"))throw new ArgumentException("Invalid executable argument.");return "\""+value+"\"";}
  public RebirthFaceImageJob(FaceTemplate data,FaceRecipe recipe,Texture2D image){
   template=data;original=JsonUtility.FromJson<FaceRecipe>(JsonUtility.ToJson(recipe));original.fittedOffsets=new FaceVertexOffset[0];original.fittedShapeStrength=1;original.highDefinitionUv=false;original.eyes.mode=0;original.skin.tintEnabled=false;original.skin.foundation=false;original.oralHash=template.oralHash;original.eyeHash=template.eyeHash;original.extensionHash=template.extensionHash;config=ReadConfiguration();backend=BackendPath();
   if(image==null)throw new InvalidOperationException("Import a portrait first.");
   if(config==null||!File.Exists(config.python)||!File.Exists(config.model)||string.IsNullOrEmpty(backend))throw new FileNotFoundException("The local image generator is not configured. Set its Python and landmark model paths in Image generator setup.");
   reference=Path.GetFullPath(AssetDatabase.GetAssetPath(image));
   original.referenceGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image));
   if(!File.Exists(reference))throw new FileNotFoundException("The portrait must be a saved project image.");
   Folder=Path.GetFullPath("Library/RebirthFaceImage/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Folder);
   if(!string.IsNullOrEmpty(original.skin.foundationKey)){
    var selected=RebirthSkinFoundations.Load(original.skin.foundationKey,original.skin.foundationHash);
    skinFoundation=Path.Combine(Folder,"SkinFoundationSource.png");File.Copy(AssetDatabase.GetAssetPath(selected),skinFoundation,false);skinFoundationSha256=original.skin.foundationHash;skinFoundationKey=original.skin.foundationKey;
   }
   Status="Preparing anatomical fit…";preparation=PrepareFit();
  }
  static float[] Flatten(IEnumerable<Vector3> points){return points.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray();}
  static float[] Project(Mesh mesh){return Project(mesh.vertices);}
  static float[] Project(Vector3[] points){
   var go=new GameObject("Face image projection"){hideFlags=HideFlags.HideAndDontSave};
   try{var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.19f;camera.aspect=1;camera.transform.position=new Vector3(0,1.615f,2.08f);camera.transform.LookAt(new Vector3(0,1.615f,.08f));return points.SelectMany(v=>{var p=camera.WorldToViewportPoint(v);return new[]{p.x,1-p.y};}).ToArray();}finally{Object.DestroyImmediate(go);}
  }
  FaceImageRequest Request(FaceRecipe recipe,string operation){
   var copy=JsonUtility.FromJson<FaceRecipe>(JsonUtility.ToJson(recipe));copy.resolution=0;
   var mesh=RebirthFaceGeneratorModel.Build(template,copy);
   try{return new FaceImageRequest{stage=operation,reference=reference,vertices=Flatten(mesh.vertices),screen=Project(mesh),normals=Flatten(mesh.normals),uv=mesh.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray(),foundationUv=mesh.uv2.SelectMany(v=>new[]{v.x,v.y}).ToArray(),triangles=mesh.GetTriangles(0),round=round,vertexLimit=template.positions.Length/3,eyeCount=template.eyes.Length,liningTriangles=mesh.subMeshCount>4?mesh.GetTriangles(4):new int[0],eyeTriangles=mesh.GetTriangles(1),eyeCenters=EyeBounds(mesh,false),eyeRadii=EyeBounds(mesh,true),teethTriangles=mesh.subMeshCount>2?mesh.GetTriangles(2):new int[0],linearColorSpace=QualitySettings.activeColorSpace==ColorSpace.Linear};}finally{Object.DestroyImmediate(mesh);}
  }
  static float[] EyeBounds(Mesh mesh,bool radius){var points=mesh.vertices;var indices=mesh.GetTriangles(1).Distinct().ToArray();var result=new List<float>();for(int side=0;side<2;side++){var group=indices.Select(i=>points[i]).Where(v=>(v.x>=0)==(side==0)).ToArray();if(group.Length==0)return new float[0];var min=group.Aggregate(Vector3.positiveInfinity,Vector3.Min);var max=group.Aggregate(Vector3.negativeInfinity,Vector3.Max);var value=radius?(max-min)*.5f:(max+min)*.5f;result.AddRange(new[]{value.x,value.y,value.z});}return result.ToArray();}
  float[] ControlScreen(FaceRecipe recipe){var p=RebirthFaceGeneratorModel.EvaluateSkinPoints(template,recipe);float shift=1.747f-p.Max(v=>v.y);for(int i=0;i<p.Length;i++)p[i].y+=shift;return Project(p);}
  IEnumerator PrepareFit(){
   Status="Preparing anatomical fit…";var request=Request(original,"fit");var controls=new List<FaceImageControl>();
   foreach(string id in template.controls.Select(c=>c.id).Concat(new[]{"$age","$masculine"})){
    var copy=JsonUtility.FromJson<FaceRecipe>(JsonUtility.ToJson(original));var control=template.controls.FirstOrDefault(c=>c.id==id);
    float value=control==null?(id=="$age"?copy.age:copy.masculine):copy.Get(control),minimum=control==null?0:control.min,maximum=control==null?1:control.max;
    Action<float> set=v=>{if(control==null){if(id=="$age")copy.age=v;else copy.masculine=v;}else copy.Set(id,v);};
    set(0);var zero=ControlScreen(copy);set(maximum);var positive=ControlScreen(copy);set(minimum);var negative=ControlScreen(copy);
    controls.Add(new FaceImageControl{id=id,value=value,minimum=minimum,maximum=maximum,positive=positive.Select((v,i)=>(v-zero[i])/Mathf.Max(maximum,.0001f)).ToArray(),negative=negative.Select((v,i)=>(v-zero[i])/Mathf.Max(-minimum,.0001f)).ToArray()});
    Progress=.01f+round*.18f+.03f*controls.Count/(template.controls.Length+2f);Status="Fitting pass "+(round+1)+" / 3 - preparing control "+controls.Count+" / "+(template.controls.Length+2);yield return null;
   }
   request.controls=controls.ToArray();WriteRequest(request);
   RenderBaseline(original,"baseline.png",new Color(.55f,.46f,.40f));Progress=.04f+round*.18f;Status="Template render prepared";
  }
  void RenderBaseline(FaceRecipe recipe,string filename,Color colour){
   var low=JsonUtility.FromJson<FaceRecipe>(JsonUtility.ToJson(recipe));low.resolution=filename.StartsWith("Shade")?recipe.resolution:0;var mesh=RebirthFaceGeneratorModel.Build(template,low);var material=new Material(Shader.Find("Standard"));material.color=colour;material.SetFloat("_Glossiness",.25f);var eye=new Material(material);eye.color=filename.StartsWith("Shade")?colour:new Color(.7f,.7f,.7f);eye.SetFloat("_Glossiness",.4f);var oral=RebirthFaceGeneratorWindow.CreateOralMaterials();if(filename.StartsWith("Shade"))foreach(var part in oral){part.mainTexture=null;part.color=colour;}var preview=new PreviewRenderUtility();Texture2D image=null;
   try{preview.BeginStaticPreview(new Rect(0,0,1024,1024));RebirthFaceGeneratorWindow.ConfigurePreview(preview,Vector2.zero,.19f,false);preview.DrawMesh(mesh,Matrix4x4.identity,material,0);preview.DrawMesh(mesh,Matrix4x4.identity,eye,1);RebirthFaceGeneratorWindow.DrawOral(preview,mesh,oral);preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,filename),image.EncodeToPNG());}
   finally{preview.Cleanup();Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);Object.DestroyImmediate(eye);foreach(var m in oral)Object.DestroyImmediate(m);if(image!=null)Object.DestroyImmediate(image);}
  }
  void WriteRequest(FaceImageRequest request){request.skinFoundation=skinFoundation;request.skinFoundationSha256=skinFoundationSha256;request.skinFoundationKey=skinFoundationKey;request.segmentationModel=config.segmentationModel;request.segmentationModelSha256=config.segmentationModelSha256;string json=JsonUtility.ToJson(request);File.WriteAllText(Path.Combine(Folder,"Request_"+request.stage+"_"+request.round+".json"),json);File.WriteAllText(Path.Combine(Folder,"request.json"),json);}
  void StartProcess(){
   process=new Process{StartInfo=new ProcessStartInfo{FileName=config.python,Arguments=Quote(Path.GetFullPath(backend))+" --job "+Quote(Folder)+" --model "+Quote(config.model),UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Folder}};
   if(!process.Start())throw new InvalidOperationException("Could not start local image generation.");
   started=DateTime.UtcNow;
   Progress=stage==0?.04f+round*.18f:stage==1?.55f:stage==4?.92f+calibrationRound*.03f:stage==5?.94f+calibrationRound*.03f:.65f;
   Status=stage==0?"Fitting rebuilt mesh - pass "+(round+1)+" / 3":stage==1?"Refining eyelids, nose, lips and jaw":stage==4?"Matching displayed skin tone":stage==5?"Comparing the corrected render":"Baking high-definition skin and calibrated lighting";
  }
  public void Poll(){
   if(Finished)return;
   if(preparation!=null){try{if(preparation.MoveNext())return;preparation=null;if(stage==3){stage=4;RenderCandidate("CalibrationBefore.png");}else if(stage==6){Progress=1;Finished=true;ended=DateTime.UtcNow;Status="Face generated. Shape, materials and skin detail are ready to edit.";return;}StartProcess();}catch(Exception exception){Error=exception.Message;Status=Error;Finished=true;ended=DateTime.UtcNow;Dispose();}return;}
   if(process==null)return;
   if(EditorApplication.timeSinceStartup>=nextProgressRead){nextProgressRead=EditorApplication.timeSinceStartup+.15;try{string path=Path.Combine(Folder,"progress.json");if(File.Exists(path)){FaceImageProgress update;using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))update=JsonUtility.FromJson<FaceImageProgress>(reader.ReadToEnd());if(update!=null&&update.progress>=Progress&&update.progress<=.995f){Progress=update.progress;if(!string.IsNullOrEmpty(update.message))Status=update.message;}}}catch(IOException){}catch(ArgumentException){}}
   if(!process.HasExited){if((DateTime.UtcNow-started).TotalMinutes>8){Dispose();Error="Image generation exceeded eight minutes and was stopped. The original face is unchanged.";Status=Error;Finished=true;ended=DateTime.UtcNow;}return;}
   try{
    int exit=process.ExitCode;process.Dispose();process=null;
    if(exit!=0){string error=Path.Combine(Folder,"error.txt");throw new InvalidOperationException(File.Exists(error)?File.ReadAllLines(error).FirstOrDefault():"The local generator exited with code "+exit+". Check the configured Python dependencies.");}
    if(stage==0){
     var fit=JsonUtility.FromJson<FaceImageFit>(File.ReadAllText(Path.Combine(Folder,"fit.json")));Candidate=JsonUtility.FromJson<FaceRecipe>(JsonUtility.ToJson(original));
     foreach(var value in fit.values){if(value.id=="$age")Candidate.age=value.value;else if(value.id=="$masculine")Candidate.masculine=value.value;else Candidate.Set(value.id,value.value);}
     Candidate.Validate(template);original=Candidate;if(round<2){round++;preparation=PrepareFit();return;}stage=1;WriteRequest(Request(Candidate,"refine"));RenderBaseline(Candidate,"baseline.png",new Color(.55f,.46f,.40f));StartProcess();return;
    }
    if(stage==1){var refinement=JsonUtility.FromJson<FaceImageRefinement>(File.ReadAllText(Path.Combine(Folder,"refine.json")));Candidate.fittedOffsets=refinement.offsets;Candidate.fittedShapeStrength=1;Candidate.highDefinitionUv=true;var frame=JsonUtility.FromJson<FaceImageFit>(File.ReadAllText(Path.Combine(Folder,"fit.json"))).transform;Candidate.portraitScale=frame[0];float roll=frame[1];float cx=frame[0]*(Mathf.Cos(roll)*.5f-Mathf.Sin(roll)*.5f)+frame[2],cy=frame[0]*(Mathf.Sin(roll)*.5f+Mathf.Cos(roll)*.5f)+frame[3];Candidate.portraitX=(cx-.5f)*.38f/frame[0];Candidate.portraitY=(cy-.5f)*.38f/frame[0];WriteRequest(Request(Candidate,"bake"));RenderBaseline(Candidate,"ShadeLow.png",new Color(.25f,.25f,.25f));RenderBaseline(Candidate,"ShadeHigh.png",new Color(.75f,.75f,.75f));stage=2;StartProcess();return;}
    if(stage==4){RefreshCalibratedSkin();stage=5;RenderCandidate("CalibrationAfter.png");StartProcess();return;}
    if(stage==5){RefreshCalibratedSkin();if(calibrationRound<1){calibrationRound++;stage=4;RenderCandidate("CalibrationBefore.png");StartProcess();return;}File.Copy(Path.Combine(Folder,"report.json"),generatedFolder+"/report.json",true);AssetDatabase.ImportAsset(generatedFolder+"/report.json");stage=6;preparation=CompleteResult();return;}
    Progress=.85f;Status="Importing generated textures and materials";stage=3;preparation=ImportResult();
   }catch(Exception exception){Error=exception.Message;Status=Error;Finished=true;ended=DateTime.UtcNow;}
  }
  IEnumerator ImportResult(){
   const string parent="Assets/FaceGeneratorGenerated";if(!AssetDatabase.IsValidFolder(parent))AssetDatabase.CreateFolder("Assets","FaceGeneratorGenerated");
   string name="Portrait_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,6);AssetDatabase.CreateFolder(parent,name);string folder=parent+"/"+name;generatedFolder=folder;
   int imported=0;foreach(string file in new[]{"SkinAlbedo.png","SkinNormal.png","EyeAlbedo.png","TeethAlbedo.png","MouthLiningAlbedo.png","SkinRoughness.png","SkinMetallicSmoothness.png","SourceCoverage.png","report.json","fit.json"}){Progress=.86f+imported++*.0035f;Status="Importing "+file;File.Copy(Path.Combine(Folder,file),folder+"/"+file);AssetDatabase.ImportAsset(folder+"/"+file);yield return null;}
   Progress=.90f;Status="Configuring skin textures";yield return null;
   var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/SkinAlbedo.png");importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.sRGBTexture=true;importer.SaveAndReimport();
   foreach(string file in new[]{"SkinRoughness.png","SkinMetallicSmoothness.png","SourceCoverage.png"}){var data=(TextureImporter)AssetImporter.GetAtPath(folder+"/"+file);data.sRGBTexture=false;data.maxTextureSize=file.StartsWith("Skin")?512:2048;data.SaveAndReimport();}
   Progress=.91f;Status="Saving materials";yield return null;
   var normalImporter=(TextureImporter)AssetImporter.GetAtPath(folder+"/SkinNormal.png");normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.convertToNormalmap=false;normalImporter.maxTextureSize=2048;normalImporter.textureCompression=TextureImporterCompression.Uncompressed;normalImporter.SaveAndReimport();
   CandidateMaterial=new Material(Shader.Find("Standard")){name="Portrait skin"};CandidateMaterial.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/SkinAlbedo.png");CandidateMaterial.color=Color.white;CandidateMaterial.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/SkinNormal.png"));CandidateMaterial.SetFloat("_BumpScale",.3f);CandidateMaterial.EnableKeyword("_NORMALMAP");CandidateMaterial.SetFloat("_Glossiness",.25f);CandidateMaterial.SetFloat("_Metallic",0);CandidateMaterial.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/SkinMetallicSmoothness.png"));CandidateMaterial.EnableKeyword("_METALLICGLOSSMAP");CandidateMaterial.SetFloat("_GlossMapScale",1);AssetDatabase.CreateAsset(CandidateMaterial,folder+"/Skin.mat");
   Candidate.generatedMaterialGuid=AssetDatabase.AssetPathToGUID(folder+"/Skin.mat");File.WriteAllText(folder+"/FaceSettings.json",JsonUtility.ToJson(Candidate,true));AssetDatabase.ImportAsset(folder+"/FaceSettings.json");AssetDatabase.SaveAssets();
   var eyeImporter=(TextureImporter)AssetImporter.GetAtPath(folder+"/EyeAlbedo.png");eyeImporter.maxTextureSize=512;eyeImporter.textureCompression=TextureImporterCompression.Uncompressed;eyeImporter.SaveAndReimport();var eye=new Material(Shader.Find("Standard")){name="Portrait eyes"};eye.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/EyeAlbedo.png");eye.SetFloat("_Glossiness",.4f);AssetDatabase.CreateAsset(eye,folder+"/Eyes.mat");Candidate.generatedEyeMaterialGuid=AssetDatabase.AssetPathToGUID(folder+"/Eyes.mat");var toothImporter=(TextureImporter)AssetImporter.GetAtPath(folder+"/TeethAlbedo.png");toothImporter.maxTextureSize=512;toothImporter.SaveAndReimport();var tooth=new Material(Shader.Find("Standard")){name="Portrait teeth"};tooth.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/TeethAlbedo.png");tooth.SetFloat("_Glossiness",.22f);AssetDatabase.CreateAsset(tooth,folder+"/Teeth.mat");Candidate.generatedToothMaterialGuid=AssetDatabase.AssetPathToGUID(folder+"/Teeth.mat");var liningImporter=(TextureImporter)AssetImporter.GetAtPath(folder+"/MouthLiningAlbedo.png");liningImporter.maxTextureSize=512;liningImporter.textureCompression=TextureImporterCompression.Uncompressed;liningImporter.sRGBTexture=true;liningImporter.SaveAndReimport();var lining=new Material(Shader.Find("Standard")){name="Portrait mouth lining"};lining.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/MouthLiningAlbedo.png");lining.SetFloat("_Glossiness",.1f);lining.SetFloat("_SpecularHighlights",0);lining.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");AssetDatabase.CreateAsset(lining,folder+"/MouthLining.mat");Candidate.generatedLiningMaterialGuid=AssetDatabase.AssetPathToGUID(folder+"/MouthLining.mat");Candidate.mouthLiningVersion=1;File.WriteAllText(folder+"/FaceSettings.json",JsonUtility.ToJson(Candidate,true));AssetDatabase.ImportAsset(folder+"/FaceSettings.json");AssetDatabase.SaveAssets();
  }
  IEnumerator CompleteResult(){
   if(config.matchReferenceHair){
    if(File.Exists(RebirthHairReferenceJob.ConfigurationPath)&&RebirthNativeAppearanceLibrary.Available){
     // Owned by this iterator: cancellation disposes the matching worker as well.
     // Suggestions remain editable; retrieval scores are not calibrated certainty.
     using(var matching=new RebirthHairReferenceJob(Candidate.referenceGuid)){
      while(!matching.Finished){matching.Poll();Progress=Mathf.Max(Progress,.98f+.014f*matching.Progress);Status=matching.Status;yield return null;}
      if(!string.IsNullOrEmpty(matching.Error))throw new InvalidOperationException("Head prepared, but hairstyle comparison failed: "+matching.Error);
      Candidate.referenceHairReportGuid=matching.ResultGuid;
      var appearance=JsonUtility.FromJson<RebirthReferenceHairMatcher.Report>(File.ReadAllText(AssetDatabase.GUIDToAssetPath(matching.ResultGuid)));
      if(config.matchReferenceHairColour)RebirthHairReferenceJob.ApplyObservedColour(Candidate,appearance);
      if(config.applyClosestReferenceHair){
       string referenceHash;using(var sha=System.Security.Cryptography.SHA256.Create())referenceHash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(reference))).Replace("-","").ToLowerInvariant();
       string reason;RebirthReferenceHairSelection.Apply(Candidate,appearance,referenceHash,out reason);appearance.status=reason;
       string reportPath=AssetDatabase.GUIDToAssetPath(matching.ResultGuid);File.WriteAllText(reportPath,JsonUtility.ToJson(appearance,true));AssetDatabase.ImportAsset(reportPath);
      }
     }
    }else{
     var match=RebirthReferenceHairMatcher.Match(template,Candidate,Folder,(p,message)=>{Progress=Mathf.Max(Progress,.98f+p*.014f);Status=message;});
     try{while(match.MoveNext())yield return null;}finally{if(match is IDisposable disposable)disposable.Dispose();}
    }
   }
   Progress=.995f;Status="Saving the final editable face";
   string observations=Path.Combine(Folder,"AppearanceObservations.json");
   if(File.Exists(observations)){foreach(string file in new[]{"AppearanceObservations.json","SemanticCategories.png","SemanticConfidence.npz","hairConfidence.png","face-skinConfidence.png","body-skinConfidence.png"}){File.Copy(Path.Combine(Folder,file),generatedFolder+"/"+file,true);AssetDatabase.ImportAsset(generatedFolder+"/"+file);yield return null;}}
   if(File.Exists(Path.Combine(Folder,"AnatomicalObservationSupport.json"))){File.Copy(Path.Combine(Folder,"AnatomicalObservationSupport.json"),generatedFolder+"/AnatomicalObservationSupport.json",true);AssetDatabase.ImportAsset(generatedFolder+"/AnatomicalObservationSupport.json");yield return null;}
   if(File.Exists(Path.Combine(Folder,"ReferenceHairFrame.json"))){File.Copy(Path.Combine(Folder,"ReferenceHairFrame.json"),generatedFolder+"/ReferenceHairFrame.json",true);AssetDatabase.ImportAsset(generatedFolder+"/ReferenceHairFrame.json");}
   if(File.Exists(Path.Combine(Folder,"HairMatchReport.json"))){
    var report=JsonUtility.FromJson<RebirthReferenceHairMatcher.Report>(File.ReadAllText(Path.Combine(Folder,"HairMatchReport.json")));
    foreach(var score in (report.scores??new RebirthReferenceHairMatcher.Score[0]).Take(3)){if(string.IsNullOrEmpty(score.preview)||Path.GetFileName(score.preview)!=score.preview)continue;File.Copy(Path.Combine(Folder,score.preview),generatedFolder+"/"+score.preview,true);AssetDatabase.ImportAsset(generatedFolder+"/"+score.preview);yield return null;}
    File.Copy(Path.Combine(Folder,"HairMatchReport.json"),generatedFolder+"/HairMatchReport.json",true);AssetDatabase.ImportAsset(generatedFolder+"/HairMatchReport.json");Candidate.referenceHairReportGuid=AssetDatabase.AssetPathToGUID(generatedFolder+"/HairMatchReport.json");
   }
   File.WriteAllText(generatedFolder+"/FaceSettings.json",JsonUtility.ToJson(Candidate,true));AssetDatabase.ImportAsset(generatedFolder+"/FaceSettings.json");AssetDatabase.SaveAssets();yield return null;
  }
  void RefreshCalibratedSkin(){File.Copy(Path.Combine(Folder,"SkinAlbedo.png"),generatedFolder+"/SkinAlbedo.png",true);AssetDatabase.ImportAsset(generatedFolder+"/SkinAlbedo.png",ImportAssetOptions.ForceUpdate);CandidateMaterial.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(generatedFolder+"/SkinAlbedo.png");}
  void RenderCandidate(string filename){
   var mesh=RebirthFaceGeneratorModel.Build(template,Candidate);var eye=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(Candidate.generatedEyeMaterialGuid));var oral=RebirthFaceGeneratorWindow.CreateOralMaterials(Candidate);var preview=new PreviewRenderUtility();Texture2D image=null;
   try{preview.BeginStaticPreview(new Rect(0,0,1024,1024));RebirthFaceGeneratorWindow.ConfigurePreview(preview,Vector2.zero,.19f/Mathf.Max(.3f,Candidate.portraitScale),false,Candidate);preview.DrawMesh(mesh,Matrix4x4.identity,CandidateMaterial,0);preview.DrawMesh(mesh,Matrix4x4.identity,eye,1);RebirthFaceGeneratorWindow.DrawOral(preview,mesh,oral);preview.Render();var coordinates=mesh.vertices.SelectMany(v=>{var point=preview.camera.WorldToViewportPoint(v);return new[]{point.x,1-point.y};}).ToArray();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,filename),image.EncodeToPNG());WriteRequest(new FaceImageRequest{stage=stage==4?"calibrate":"measure",round=calibrationRound,reference=reference,vertices=Flatten(mesh.vertices),screen=coordinates,normals=Flatten(mesh.normals),uv=mesh.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray(),triangles=mesh.GetTriangles(0)});}
   finally{preview.Cleanup();Object.DestroyImmediate(mesh);foreach(var part in oral)Object.DestroyImmediate(part);if(image!=null)Object.DestroyImmediate(image);}
  }
  public void Dispose(){if(preparation is IDisposable disposable)disposable.Dispose();preparation=null;if(process!=null){try{if(!process.HasExited)process.Kill();}finally{process.Dispose();process=null;}}}
 }
}


