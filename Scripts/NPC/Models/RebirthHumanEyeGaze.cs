using HarmonyLib;
using UnityEngine;

// The installed 2.6 CharacterGazeController.UpdateEyeGaze constrained the eye
// shader quaternion x/y components. The current game removed those limits.
// Restore the proven limits only for REBIRTH humans, never the player or dogs.
[HarmonyPatch(typeof(CharacterGazeController),"UpdateEyeGaze")]
internal static class RebirthHumanEyeGaze
{
    internal static void Install()=>RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.human.sdcs.eye-gaze"),typeof(RebirthHumanEyeGaze));
    private static void Postfix(CharacterGazeController __instance)
    {
        if(__instance.entityAlive is not EntityRebirthHumanoidNPC||__instance.eyeMaterial==null)return;
        var left=Clamp(__instance.currentLeftEyeRotation);var right=Clamp(__instance.currentRightEyeRotation);
        __instance.currentLeftEyeRotation=left;__instance.currentRightEyeRotation=right;
        __instance.eyeMaterial.SetVector("_LeftEyeRotation",new Vector4(-left.x,left.y,left.z,left.w));
        __instance.eyeMaterial.SetVector("_RightEyeRotation",new Vector4(-right.x,right.y,right.z,right.w));
    }
    internal static Quaternion Clamp(Quaternion value)
    {
        value.x=Mathf.Clamp(value.x,-0.2f,0.2f);value.y=Mathf.Clamp(value.y,-0.4f,0.4f);
        return value;
    }
}
