using System;
using System.IO;
using System.Linq;
namespace RebirthEntityStudio {
 public static class RebirthAppearanceStartingPresets {
  // Matches the existing starting-preset button. Resolve dependencies before
  // the caller replaces its current recipe, so a missing asset cannot reset it.
  public static FaceRecipe WomanAfro(FaceTemplate template){
   if(template==null||template.presets==null||template.presets.Length<2)throw new InvalidDataException("The female starting preset is unavailable");
   if(!RebirthNativeAppearanceLibrary.Available)throw new InvalidDataException("Import the appearance library before choosing this preset");
   const string key="female:hair:hair_afro_curly",colour="06 Black";
   var catalog=RebirthNativeAppearanceLibrary.Catalog;var style=catalog.styles.SingleOrDefault(s=>s.key==key);
   if(style==null||style.family!="hair"||style.gender!="female")throw new InvalidDataException("The imported library does not contain the female curly Afro style");
   if(!RebirthNativeAppearanceLibrary.Colours.swatches.Any(s=>s.name==colour))throw new InvalidDataException("The imported library is missing the black hair swatch");
   var result=FaceRecipe.FromPreset(template,1);result.name="Woman - afro";result.africanShape=1;result.eyes.mode=1;result.eyes.colour=0;result.skin.foundation=true;
   RebirthFaceAppearance.SkinPreset(result.skin,4);result.nativeAppearance.enabled=true;result.nativeAppearance.sourceHash=catalog.sourceHash;result.nativeAppearance.hairKey=key;result.nativeAppearance.colourName=colour;
   // Reviewed artist finish for this preset only. The imported swatch and
   // library assets remain untouched; users retain the game-finish option.
   result.nativeAppearance.overrideEnvironmentResponse=true;result.nativeAppearance.environmentResponse=.1f;result.Validate(template);return result;
  }
 }
}
