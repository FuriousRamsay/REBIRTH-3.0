using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
namespace RebirthEntityStudio {
 public static class RebirthBodyFoundationFromHead {
  public static void Apply(BodyRecipe body,TextAsset settings){
   if(settings==null)throw new ArgumentException("Choose saved head settings first");
   var face=JsonUtility.FromJson<FaceRecipe>(settings.text);face.Validate(RebirthFaceGeneratorWindow.ReadTemplate());
   if(!face.skin.foundation||face.highDefinitionUv||string.IsNullOrEmpty(face.skin.foundationKey))throw new InvalidDataException("This action requires a head using an authored skin atlas. Use shared skin generation for portrait-generated skin.");
   var atlas=RebirthSkinFoundations.Load(face.skin.foundationKey,face.skin.foundationHash);
   Material source=null;bool temporary=false;
   try{
    if(!string.IsNullOrEmpty(face.generatedMaterialGuid)){
     source=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(face.generatedMaterialGuid));
     if(source==null||source.mainTexture==null)throw new InvalidDataException("The saved head material is missing");
     string texture=AssetDatabase.GetAssetPath(source.mainTexture);string hash;
     using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(texture))).Replace("-","").ToLowerInvariant();
     if(hash!=face.skin.foundationHash)throw new InvalidDataException("The head texture has been edited. Use shared skin generation to preserve those changes.");
    }else{source=new Material(Shader.Find("Standard"));temporary=true;RebirthFaceAppearance.FoundationSkin(source,face.skin);}
    var colour=RebirthBodySkinJob.SourceTint(face,source);string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(atlas));
    if(string.IsNullOrEmpty(guid))throw new InvalidDataException("The authored atlas has not been imported");
    // All source checks precede mutations. Body geometry, attachment and local
    // pigment/colour controls remain editable and are not reset by this action.
    body.bodyFoundationGuid=guid;body.skinTextureGuid="";body.skinReportGuid="";body.sourceSkinTint=colour;body.sourceSkinTintVersion=1;
   }finally{if(temporary&&source!=null)UnityEngine.Object.DestroyImmediate(source);}
  }
 }
}
