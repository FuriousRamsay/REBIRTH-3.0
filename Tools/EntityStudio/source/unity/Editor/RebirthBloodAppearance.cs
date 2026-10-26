using System;
using UnityEngine;
namespace RebirthEntityStudio {
 public static class RebirthBloodAppearance {
  // Coverage belongs to the surface. Colour and drying/wetness belong to the
  // character's existing damage profile, avoiding divergent interior settings.
  public static void ApplyInterior(Material material,BodyDamageSettings profile,float coverage){
   if(material==null||profile==null)throw new ArgumentNullException();
   if(!material.HasProperty("_BloodAmounts")||!material.HasProperty("_BloodTint"))throw new InvalidOperationException("Install the wound shader with shared blood support");
   material.SetVector("_BloodAmounts",new Vector4(Mathf.Clamp01(coverage),profile.freshness,profile.wetness,0));
   material.SetColor("_BloodTint",profile.bloodTint);
  }
 }
}
