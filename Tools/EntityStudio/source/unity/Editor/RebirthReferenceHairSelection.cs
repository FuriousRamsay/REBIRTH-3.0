using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace RebirthEntityStudio {
 // Optional library initialization, never a claim of reconstructed hidden hair.
 public static class RebirthReferenceHairSelection {
  static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
  public static bool Apply(FaceRecipe recipe,RebirthReferenceHairMatcher.Report report,string referenceHash,out string reason){
   reason="Current hairstyle retained.";
   if(recipe==null||report==null)throw new ArgumentNullException("Missing hairstyle selection input");
   if(string.IsNullOrEmpty(referenceHash)||report.referenceSha256!=referenceHash)throw new InvalidDataException("Hairstyle suggestions belong to another reference image");
   if(!RebirthNativeAppearanceLibrary.Available||report.sourceHash!=RebirthNativeAppearanceLibrary.Catalog.sourceHash)throw new InvalidDataException("Hairstyle suggestions require their matching native library");
   if(!report.appearanceAvailable||report.insufficientSupport){reason="Current hairstyle retained: supported appearance comparison is unavailable; shape alone is not enough.";return false;}
   var scores=report.scores??new RebirthReferenceHairMatcher.Score[0];
   if(scores.Length<2){reason="Current hairstyle retained: too few compared styles to establish ranking agreement.";return false;}
   if(scores.Any(s=>s==null||string.IsNullOrEmpty(s.key)||!Finite(s.appearanceClassCosine)||!Finite(s.appearancePatchCosine)))throw new InvalidDataException("Invalid hairstyle appearance ranking");
   if(scores.Select(s=>s.key).Distinct().Count()!=scores.Length)throw new InvalidDataException("Duplicate hairstyle suggestions");
   var classes=scores.OrderByDescending(s=>s.appearanceClassCosine).ToArray();var patches=scores.OrderByDescending(s=>s.appearancePatchCosine).ToArray();
   if(classes[0].key!=patches[0].key||classes[0].appearanceClassCosine<=classes[1].appearanceClassCosine||patches[0].appearancePatchCosine<=patches[1].appearancePatchCosine){reason="Current hairstyle retained: appearance rankings disagree or tie. Choose a suggestion manually.";return false;}
   var style=RebirthNativeAppearanceLibrary.Style(classes[0].key);if(style.family!="hair")throw new InvalidDataException("A facial-hair style cannot initialize scalp hair");
   var next=recipe.nativeAppearance==null?new FaceNativeAppearanceSettings():JsonUtility.FromJson<FaceNativeAppearanceSettings>(JsonUtility.ToJson(recipe.nativeAppearance));
   next.enabled=true;next.sourceHash=report.sourceHash;next.hairKey=style.key;
   // Match the neutral source shape used by the candidate renders, not a previous style's edits.
   next.authoredLength=-1;next.authoredMessiness=-1;next.hairVolume=0;next.hairSize=0;next.hairWidth=0;next.hairDepth=0;next.hairLength=0;next.Validate();
   recipe.nativeAppearance=next;report.selectedKey=style.key;
   reason="Editable starting hairstyle selected: "+style.name+". Class and patch rankings agree; this is not calibrated certainty or hidden-hair reconstruction. Previous scalp shape adjustments reset; facial hair retained.";
   return true;
  }
 }
}
