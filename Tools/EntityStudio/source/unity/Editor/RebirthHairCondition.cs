using System;
using UnityEngine;
namespace RebirthEntityStudio {
 [Serializable] public sealed class HairConditionSettings {
  public float dirt,rootConcentration;
  public Color dirtColour=new Color(.16f,.12f,.075f,1);
  public void Validate(){foreach(float v in new[]{dirt,rootConcentration,dirtColour.r,dirtColour.g,dirtColour.b})if(float.IsNaN(v)||float.IsInfinity(v))throw new ArgumentException("Invalid hair condition.");dirt=Mathf.Clamp01(dirt);rootConcentration=Mathf.Clamp01(rootConcentration);dirtColour=new Color(Mathf.Clamp01(dirtColour.r),Mathf.Clamp01(dirtColour.g),Mathf.Clamp01(dirtColour.b),1);}
 }
 public static class RebirthHairCondition {
  public static readonly string[] Presets={"Clean","Worn","Dirty","Filthy"};
  public static void Preset(HairConditionSettings s,int index){s.dirt=new[]{0f,.2f,.55f,.9f}[Mathf.Clamp(index,0,3)];}
  public static void Apply(Material material,HairConditionSettings s){
   if(s==null)return;s.Validate();if(material.HasProperty("_Dirt")){material.SetFloat("_Dirt",s.dirt);material.SetColor("_DirtColor",s.dirtColour);return;}
   if(s.dirt==0)return;
   // The compiled native shader has no dirt mask input. Its fallback is a
   // uniform treatment at zero root concentration. Native root darkening
   // uses its existing authored mask; this is not a dirt deposit simulation.
   bool native=material.shader!=null&&material.shader.name=="Game/SDCS/Hair";
   float concentration=native||material.HasProperty("_RootDarkening")?s.rootConcentration:0;
   float uniform=s.dirt;
   if(concentration>0)material.SetFloat("_RootDarkening",Mathf.Lerp(material.GetFloat("_RootDarkening"),1,s.dirt*concentration));
   foreach(string property in new[]{"_Tint1","_Tint2","_Tint3"})if(material.HasProperty(property))material.SetColor(property,Color.Lerp(material.GetColor(property),s.dirtColour,uniform*.65f));
   if(material.HasProperty("_SmoothnessMultiplier"))material.SetFloat("_SmoothnessMultiplier",material.GetFloat("_SmoothnessMultiplier")*(1-s.dirt*.6f));
  }
 }
}
