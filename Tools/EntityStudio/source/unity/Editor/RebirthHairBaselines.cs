using System;
namespace RebirthEntityStudio {
 public static class RebirthHairBaselines {
  public const string RecoveredLongKey="female:hair:hair_midpart_long";
  // Exact native style/material choices recovered from the owner's RGB-identical
  // NativeHairSourceFitReview/Original_0.png. The normal head-fit path remains.
  public static void ApplyRecoveredLong(FaceRecipe recipe){
   if(recipe==null)throw new ArgumentNullException(nameof(recipe));
   var catalog=RebirthNativeAppearanceLibrary.Catalog;
   var style=RebirthNativeAppearanceLibrary.Style(RecoveredLongKey);
   if(style.family!="hair")throw new InvalidOperationException("Recovered baseline is not a head hairstyle");
   if(recipe.nativeAppearance==null)recipe.nativeAppearance=new FaceNativeAppearanceSettings();
   var s=recipe.nativeAppearance;s.enabled=true;s.sourceHash=catalog.sourceHash;
   s.hairKey=RecoveredLongKey;s.colourName="02 Dark Blond";s.customTint=false;
   s.tintFromReference=false;s.preserveNativeFinish=true;
   s.overrideEnvironmentResponse=false;s.overrideSmoothness=false;
   s.hairVolume=0;s.hairSize=0;s.hairWidth=0;s.hairDepth=0;s.hairLength=0;s.authoredLength=-1;s.authoredMessiness=-1;
   s.authoredShapeVersion=1;s.hairCondition=new HairConditionSettings();
  }
 }
}
