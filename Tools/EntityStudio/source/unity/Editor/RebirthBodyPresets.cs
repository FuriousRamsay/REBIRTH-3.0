using System;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio {
 public static class RebirthBodyPresets {
  public static readonly string[] Names={"Female neutral","Male neutral","Slender","Broad torso","Athletic","Muscular","Higher body fat","Muscular with body fat"};
  static readonly string[] BuildControls={"torso_width","waist","hip_width","muscle_mass","body_fat","muscle_definition","upperarm_muscle","upperleg_muscle","shoulder_muscle"};
  public static BodyRecipe Apply(BodyTemplate template,BodyRecipe current,int preset){
   if(template==null||current==null)throw new ArgumentNullException("Body preset requires a template and current recipe");
   if(preset<0||preset>=Names.Length)throw new ArgumentOutOfRangeException(nameof(preset));
   foreach(string id in BuildControls)if(!template.controls.Any(c=>c.id==id))throw new InvalidOperationException("Body preset needs matching control: "+id);
   var result=JsonUtility.FromJson<BodyRecipe>(JsonUtility.ToJson(current));
   result.Validate(template);result.name=Names[preset];
   if(preset<2){result.values=new FaceValue[0];result.masculine=preset==1?1:0;}
   else{
    foreach(string id in BuildControls)result.Set(id,template.controls.First(c=>c.id==id).defaultValue);
    if(preset==2){result.Set("torso_width",-.25f);result.Set("waist",-.3f);result.Set("hip_width",-.15f);}
    if(preset==3){result.Set("torso_width",.3f);result.Set("waist",.25f);}
    if(preset==4){result.Set("muscle_mass",.5f);result.Set("body_fat",-.3f);result.Set("muscle_definition",.35f);}
    if(preset==5||preset==7){result.Set("muscle_mass",.85f);result.Set("upperarm_muscle",.4f);result.Set("upperleg_muscle",.35f);result.Set("shoulder_muscle",.3f);}
    if(preset==6||preset==7)result.Set("body_fat",.75f);
   }
   // Previous fit reports describe a different body shape. Input references,
   // generated skin and the attached head remain reproducible dependencies.
   result.bodyImageReportGuid="";result.generationReportGuid="";
   result.Validate(template);return result;
  }
 }
}
