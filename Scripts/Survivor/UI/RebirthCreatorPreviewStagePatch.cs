using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(XUiC_SDCSPreviewWindow), nameof(XUiC_SDCSPreviewWindow.MakePreview))]
internal static class RebirthCreatorPreviewStagePatch
{
    private static void Postfix(XUiC_SDCSPreviewWindow __instance, RenderTextureSystem ___renderTextureSystem)
    {
        if (__instance.GetParentByType<XUiC_RebirthSurvivorCreator>() == null ||
            ___renderTextureSystem?.ParentGO == null) return;

        var stage = ___renderTextureSystem.ParentGO.transform;
        var scene = stage.parent;
        if (scene == null || stage.localPosition.x != 0f) return;

        // Native previews share one scene and culling layer. Move this preview's
        // camera, rig and light together past the native 20-unit camera range.
        const float spacing = 64f;
        float x = spacing;
        for (int i = 0; i < scene.childCount; i++)
            x = Mathf.Max(x, scene.GetChild(i).localPosition.x + spacing);

        var position = stage.localPosition;
        position.x = x;
        stage.localPosition = position;
    }
}
