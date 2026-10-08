using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Entity-aware SDCS full-body render for the redesigned Character Overview. Unlike the profile
/// creator snapshot, this path passes the live EntityPlayer so equipped armor/clothing/cosmetics
/// are represented. The render is rebuilt only when the live appearance/equipment signature changes.
/// </summary>
public sealed class RebirthCharacterModelBinder
{
    private const string LogPrefix = "[REBIRTH Survivor][CharacterModel]";
    private const int TextureWidth = 512;
    private const int TextureHeight = 896;

    private readonly XUiController textureController;
    private Texture2D currentTexture;
    private int lastSignature = int.MinValue;
    private int lastPlayerInstance;
    private float retryAfter;

    public RebirthCharacterModelBinder(XUiController controller)
    {
        textureController = controller;
        Clear();
    }

    public bool Bind(EntityPlayer player, bool force)
    {
        XUiV_Texture view = textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
        if (view == null || player == null)
        {
            Clear();
            return false;
        }

        int signature = ComputeAppearanceSignature(player);
        int playerInstance = player.GetInstanceID();
        if (!force && currentTexture != null && signature == lastSignature && playerInstance == lastPlayerInstance)
        {
            if (view.Texture != currentTexture) view.Texture = currentTexture;
            if (!view.IsVisible) view.IsVisible = true;
            return true;
        }

        if (!force && Time.realtimeSinceStartup < retryAfter) return currentTexture != null;
        Texture2D next = BuildSnapshot(player);
        if (next == null)
        {
            retryAfter = Time.realtimeSinceStartup + 2f;
            if (currentTexture == null) view.IsVisible = false;
            return currentTexture != null;
        }

        if (currentTexture != null) UnityEngine.Object.Destroy(currentTexture);
        currentTexture = next;
        lastSignature = signature;
        lastPlayerInstance = playerInstance;
        retryAfter = 0f;
        view.Texture = currentTexture;
        view.IsVisible = true;
        return true;
    }

    // Closing a menu does not change appearance. Keep only the finished image, not a live
    // preview rig/camera, so an immediate reopen need not instantiate and render SDCS again.
    public void Suspend()
    {
        XUiV_Texture view = textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
        if (view != null)
        {
            view.Texture = null;
            view.IsVisible = false;
        }
    }

    public void Clear()
    {
        Suspend();
        if (currentTexture != null)
        {
            UnityEngine.Object.Destroy(currentTexture);
            currentTexture = null;
        }
        lastSignature = int.MinValue;
        lastPlayerInstance = 0;
        retryAfter = 0f;
    }

    private static Texture2D BuildSnapshot(EntityPlayer player)
    {
        Texture2D snapshot = null;
        GameObject rig = null;
        SDCSUtils.TransformCatalog catalog = null;
        RenderTextureSystem renderSystem = null;
        try
        {
            EModelSDCS model = player.emodel as EModelSDCS;
            Archetype archetype = (UnityEngine.Object)model != (UnityEngine.Object)null ? model.Archetype : null;
            if ((UnityEngine.Object)model == (UnityEngine.Object)null || archetype == null) return null;

            SDCSUtils.CreateVizUI(archetype, ref rig, ref catalog, player, false);
            if ((UnityEngine.Object)rig == (UnityEngine.Object)null) return null;

            Animator animator = rig.GetComponentInChildren<Animator>();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null) animator.Update(0f);

            renderSystem = new RenderTextureSystem();
            GameObject target = new GameObject("RebirthCharacterOverviewTarget_" + player.entityId);
            renderSystem.Create("rebirthCharacterOverview_" + player.entityId, target, Vector3.zero, Vector3.zero,
                // Native AA=true doubles both render dimensions. ReadPixels below must read
                // the complete image, not the lower-left quarter of a doubled render target.
                new Vector2i(TextureWidth, TextureHeight), false);

            rig.transform.SetParent(renderSystem.TargetGO.transform, false);
            rig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rig.transform.localPosition = Vector3.zero;
            renderSystem.TargetGO.transform.localPosition = new Vector3(0f, 0f, 3.25f);
            Utils.SetLayerRecursively(renderSystem.TargetGO, 11);

            Camera camera = renderSystem.CameraGO != null ? renderSystem.CameraGO.GetComponent<Camera>() : null;
            if ((UnityEngine.Object)camera == (UnityEngine.Object)null) return null;
            CharacterGazeController gaze = rig.GetComponentInChildren<CharacterGazeController>();
            if ((UnityEngine.Object)gaze != (UnityEngine.Object)null) gaze.SnapNextUpdate();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null) animator.Update(0f);

            // Frame the entire equipped visual, not a fixed camera offset aimed at the waist.
            Bounds bounds;
            if (!TryGetVisualBounds(rig, out bounds)) return null;
            float aspect = TextureWidth / (float)TextureHeight;
            float halfHeight = Math.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.12f;
            float distance = Math.Max(4f, bounds.extents.z * 2f + 1f);
            camera.transform.rotation = Quaternion.identity;
            camera.transform.position = bounds.center - Vector3.forward * distance;
            camera.orthographic = true;
            camera.orthographicSize = Math.Max(0.25f, halfHeight);
            camera.aspect = aspect;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = distance + bounds.extents.z + 5f;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            Light light = renderSystem.LightGO != null ? renderSystem.LightGO.GetComponent<Light>() : null;
            if ((UnityEngine.Object)light != (UnityEngine.Object)null)
            {
                light.transform.position = bounds.center + new Vector3(1.25f, bounds.extents.y, -2f);
                light.type = LightType.Point;
                light.range = 20f;
                light.intensity = 1.45f;
                light.color = new Color(1f, 0.93f, 0.86f, 1f);
            }
            if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled)
                Log.Out(LogPrefix + " full-body bounds=" + bounds.size + " orthographicSize=" + camera.orthographicSize
                    + " aspect=" + aspect + " texture=" + TextureWidth + "x" + TextureHeight);

            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = renderSystem.RenderTex;
                snapshot = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
                snapshot.name = "RebirthCharacterOverviewModel_" + player.entityId;
                snapshot.ReadPixels(new Rect(0f, 0f, TextureWidth, TextureHeight), 0, 0);
                snapshot.Apply();
                snapshot.filterMode = FilterMode.Trilinear;
                snapshot.anisoLevel = 2;
                snapshot.wrapMode = TextureWrapMode.Clamp;
            }
            finally { RenderTexture.active = previous; }
            return snapshot;
        }
        catch (Exception ex)
        {
            if (snapshot != null) UnityEngine.Object.Destroy(snapshot);
            Log.Warning(LogPrefix + " build failed: " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
        finally
        {
            if ((UnityEngine.Object)rig != (UnityEngine.Object)null)
            {
                try { SDCSUtils.UnloadViz(rig); }
                catch { UnityEngine.Object.Destroy(rig); }
            }
            if (renderSystem != null)
            {
                // Destroy is deferred; deactivate the temporary preview immediately so a
                // second appearance render in this frame cannot pick up the previous rig.
                renderSystem.SetEnabled(false);
                renderSystem.Cleanup();
            }
            if (catalog != null) catalog.Clear();
        }
    }

    private static bool TryGetVisualBounds(GameObject rig, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        Renderer[] renderers = rig.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))) continue;
            Bounds candidate = renderer.bounds;
            Vector3 c = candidate.center, size = candidate.size;
            if (!Finite(c.x) || !Finite(c.y) || !Finite(c.z) || !Finite(size.x) || !Finite(size.y) || !Finite(size.z) || size.sqrMagnitude < 0.000001f) continue;
            if (!found) { bounds = candidate; found = true; }
            else bounds.Encapsulate(candidate);
        }
        return found && bounds.size.y > 0.05f;
    }

    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    private static int ComputeAppearanceSignature(EntityPlayer player)
    {
        unchecked
        {
            int hash = 17;
            EModelSDCS model = player.emodel as EModelSDCS;
            Archetype archetype = (UnityEngine.Object)model != (UnityEngine.Object)null ? model.Archetype : null;
            if (archetype != null)
            {
                hash = hash * 31 + archetype.GetHashCode();
                hash = hash * 31 + StableStringHash(archetype.Name);
                hash = hash * 31 + StableStringHash(archetype.Race);
                hash = hash * 31 + archetype.Variant;
                hash = hash * 31 + StableStringHash(archetype.Hair);
                hash = hash * 31 + StableStringHash(archetype.HairColor);
                hash = hash * 31 + StableStringHash(archetype.MustacheName);
                hash = hash * 31 + StableStringHash(archetype.ChopsName);
                hash = hash * 31 + StableStringHash(archetype.BeardName);
                hash = hash * 31 + StableStringHash(archetype.EyeColorName);
                hash = hash * 31 + (archetype.IsMale ? 1 : 0);
            }
            Equipment equipment = player.equipment;
            if (equipment == null) return hash;
            int count = equipment.GetSlotCount();
            hash = hash * 31 + count;
            for (int i = 0; i < count; i++)
            {
                ItemValue item = equipment.GetSlotItem(i);
                hash = hash * 31 + (item != null ? item.type : 0);
                // A dye or visible attachment can change without replacing the armor itself.
                // Do not hash durability: wear does not change the preview model.
                if (item != null)
                {
                    hash = HashModTypes(hash, item.modifications);
                    hash = HashModTypes(hash, item.cosmeticMods);
                }
                ItemClass cosmetic = equipment.GetCosmeticSlot(i, false);
                hash = hash * 31 + (cosmetic != null ? StableStringHash(cosmetic.GetItemName()) : 0);
            }
            return hash;
        }
    }

    private static int HashModTypes(int hash, ItemValue[] mods)
    {
        unchecked
        {
            hash = hash * 31 + (mods != null ? mods.Length : 0);
            if (mods != null)
                for (int i = 0; i < mods.Length; i++)
                    hash = hash * 31 + (mods[i] != null ? mods[i].type : 0);
            return hash;
        }
    }

    private static int StableStringHash(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash;
        }
    }
}
