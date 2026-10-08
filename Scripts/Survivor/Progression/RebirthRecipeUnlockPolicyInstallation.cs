using System;using System.Linq;using System.Reflection;using HarmonyLib;
#nullable disable
// Install only the existing owned unlock policy bridge; native crafting transaction guards are unchanged.
internal static class RebirthRecipeUnlockPolicyInstallation {
 internal static void EnsureInstalled(Harmony harmony){
  if(harmony==null)throw new ArgumentNullException(nameof(harmony));
  if(!RebirthCraftingProgressionRegistry.IsReady||!RebirthCapabilityRegistry.IsReady)throw new InvalidOperationException("Recipe policy registries are not ready.");
  var target=typeof(Recipe).GetMethod("IsUnlocked",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(EntityPlayer)},null);
  var postfix=typeof(RebirthSurvivorRecipePresentationKnowledgePatch).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
  if(target==null||postfix==null)throw new InvalidOperationException("Recipe unlock policy boundary is unavailable.");
  if(!HasExpectedPostfix(target,postfix,harmony.Id))RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthSurvivorRecipePresentationKnowledgePatch));
  if(!HasExpectedPostfix(target,postfix,harmony.Id))throw new InvalidOperationException("Owned recipe unlock policy postfix was not installed.");
 }
 static bool HasExpectedPostfix(MethodInfo target,MethodInfo postfix,string owner){var patches=Harmony.GetPatchInfo(target);return patches!=null&&patches.Postfixes.Count(p=>p.owner==owner&&p.PatchMethod==postfix)==1;}
}