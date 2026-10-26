using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio {
 [Serializable] public sealed class FaceHairSettings {
  public string guideKey="";public float guideLength,guideWidth=.014f,guideTaper=.6f,guideCoverage=1;public int guideLayers=2;
  public HairConditionSettings condition=new HairConditionSettings();
  public int style,seed=17,quality=1,surfaceVersion=1;public float length=.32f,volume=.35f,wave=.12f,curl=.12f,frizz=.08f,part,fringe=.15f,hairline,grey;
  public Color color=new Color(.30f,.21f,.105f,1),rootColor=new Color(.075f,.052f,.03f,1);public float shine=.32f;
  public void Validate(){if(condition==null)condition=new HairConditionSettings();condition.Validate();if(surfaceVersion==0)surfaceVersion=1;if(surfaceVersion!=1&&surfaceVersion!=2)throw new InvalidOperationException("Unsupported hair surface revision.");style=Mathf.Clamp(style,0,13);quality=Mathf.Clamp(quality,0,2);foreach(float v in new[]{guideCoverage,guideLength,guideWidth,guideTaper,length,volume,wave,curl,frizz,part,fringe,hairline,grey,shine,color.r,color.g,color.b,rootColor.r,rootColor.g,rootColor.b})if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Invalid hair setting.");guideCoverage=Mathf.Clamp01(guideCoverage);guideLength=Mathf.Clamp(guideLength,-.5f,1);guideWidth=Mathf.Clamp(guideWidth,.002f,.025f);guideTaper=Mathf.Clamp01(guideTaper);guideLayers=Mathf.Clamp(guideLayers,1,3);length=Mathf.Clamp(length,.008f,.75f);volume=Mathf.Clamp01(volume);wave=Mathf.Clamp01(wave);curl=Mathf.Clamp01(curl);frizz=Mathf.Clamp01(frizz);part=Mathf.Clamp(part,-1,1);fringe=Mathf.Clamp01(fringe);hairline=Mathf.Clamp(hairline,-1,1);grey=Mathf.Clamp01(grey);shine=Mathf.Clamp01(shine);}
 }
 public static class RebirthHairGenerator {
  public static readonly string[] Styles={"Bald","Short crop","Short layers","Bob / fringe","Bob / side part","Long straight","Ponytail","Short ponytail","Short swept","Receding crop","Afro / curls","Side swept","Braid","Long wavy"};
  public static readonly string[] Colors={"Black","Dark brown","Brown","Auburn","Dark blonde","Blonde","Platinum","Grey","White"};
  static readonly Color[] Palette={new Color(.025f,.020f,.017f),new Color(.065f,.039f,.023f),new Color(.16f,.085f,.035f),new Color(.25f,.075f,.025f),new Color(.28f,.20f,.10f),new Color(.45f,.34f,.19f),new Color(.62f,.59f,.47f),new Color(.25f,.26f,.25f),new Color(.7f,.7f,.65f)};
  public static void SetColor(FaceHairSettings h,int index){h.color=Palette[Mathf.Clamp(index,0,Palette.Length-1)];h.rootColor=h.color*.35f;h.rootColor.a=1;}
  public static void Preset(FaceHairSettings h,int style){h.guideKey="";h.surfaceVersion=2;h.style=style;h.wave=style==13?.65f:0;h.curl=0;h.fringe=0;h.volume=.30f;h.part=0;h.frizz=.05f;h.hairline=style==9?.8f:0;h.length=new[]{.008f,.05f,.12f,.23f,.27f,.45f,.40f,.20f,.10f,.05f,.16f,.16f,.40f,.45f}[style];}
  public static bool HasHair(FaceRecipe recipe)=>recipe.nativeAppearance!=null&&recipe.nativeAppearance.enabled?!string.IsNullOrEmpty(recipe.nativeAppearance.hairKey):!string.IsNullOrEmpty(recipe.hair.guideKey)||recipe.hair.style!=0;
  public static Mesh Build(Mesh head,FaceHairSettings h,FaceTemplate template,FaceRecipe recipe){h.Validate();if(recipe.nativeAppearance!=null&&recipe.nativeAppearance.enabled)return RebirthNativeAppearanceLibrary.Hair(head,recipe.nativeAppearance);if(!string.IsNullOrEmpty(h.guideKey))return RebirthGuideHair.BuildWithCoverage(template,recipe,head);if(h.style==0)return new Mesh{name="Bald hair"};return RebirthAuthoredHair.Build(template,recipe);}
  public static Material[] Materials(FaceHairSettings h,FaceRecipe recipe){if(recipe?.nativeAppearance!=null&&recipe.nativeAppearance.enabled)return RebirthNativeAppearanceLibrary.Materials(recipe.nativeAppearance.hairKey,recipe.nativeAppearance);if(!string.IsNullOrEmpty(h.guideKey))return new[]{Material(h,recipe),RebirthGuideHair.ScalpMaterial(h)};return new[]{Material(h,recipe)};}
  public static Material Material(FaceHairSettings h,FaceRecipe recipe=null){if(recipe?.nativeAppearance!=null&&recipe.nativeAppearance.enabled)return RebirthNativeAppearanceLibrary.Material(recipe.nativeAppearance.hairKey,recipe.nativeAppearance);if(!string.IsNullOrEmpty(h.guideKey))return RebirthGuideHair.Material(h);return RebirthAuthoredHair.Material(h);}
 }
}


