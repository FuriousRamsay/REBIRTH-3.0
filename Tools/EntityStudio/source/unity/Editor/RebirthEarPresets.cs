using System;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio {
 public static class RebirthEarPresets {
  public static readonly string[] Names={"Neutral controls","Compact","Rounded","Longer lobes","Prominent","Elongated"};
  public static readonly string[] Sides={"Both ears","Left ear","Right ear"};
  // Combinations of the existing authored ear targets, not new anatomy data.
  static readonly string[] Keys={"size","length","depth","rotation","spread","lobe","flap","height","roundness"};
  static readonly float[][] Values={
   new float[]{0,0,0,0,0,0,0,0,0},
   new float[]{-.25f,-.15f,-.1f,0,-.1f,-.15f,0,0,.15f},
   new float[]{0,-.1f,0,0,0,0,0,0,.6f},
   new float[]{0,0,0,0,0,.55f,0,0,.1f},
   new float[]{.1f,0,.1f,0,.45f,0,0,0,0},
   new float[]{.1f,.4f,0,0,0,.1f,0,0,-.15f}
  };
  public static void Apply(FaceTemplate template,FaceRecipe recipe,int preset,int side,float strength){
   if(template==null||recipe==null)throw new ArgumentNullException("Ear preset needs a template and recipe");
   if(preset<0||preset>=Values.Length||side<0||side>=Sides.Length)throw new ArgumentOutOfRangeException("Ear preset selection");
   if(float.IsNaN(strength)||float.IsInfinity(strength)||strength<0||strength>1)throw new ArgumentOutOfRangeException(nameof(strength));
   string[] selected=side==0?new[]{"l","r"}:new[]{side==1?"l":"r"};
   // Validate the full operation before changing the recipe.
   foreach(string prefix in selected)foreach(string key in Keys)
    if(!template.controls.Any(c=>c.group=="Ears"&&c.id==prefix+"_ear_"+key))throw new InvalidOperationException("Ear presets require the matching extended ear targets");
   foreach(string prefix in selected)for(int i=0;i<Keys.Length;i++){
    var control=template.controls.First(c=>c.id==prefix+"_ear_"+Keys[i]);
    recipe.Set(control.id,Mathf.Clamp(Mathf.Lerp(control.defaultValue,Values[preset][i],strength),control.min,control.max));
   }
  }
 }
}
