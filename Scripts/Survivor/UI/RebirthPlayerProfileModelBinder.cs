using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Safe, self-contained full-player SDCS snapshots for UI contexts that do not own the native
/// PlayerProfile/SDCSPreviewWindow controller hierarchy (for example the native spawnselection
/// window).  The native SDCSPreviewWindow assumes Player Profile editor state during Init and
/// must not be embedded directly in arbitrary window groups.
/// </summary>
public sealed class RebirthPlayerProfileModelBinder
{
    private const string LogPrefix = "[REBIRTH Survivor][PlayerProfileModel]";
    private const int TextureWidth = 640;
    private const int TextureHeight = 334;
    private const string RenderIdentity = "spawn-model-v2-640x334";

    private sealed class CacheEntry { public string IdentityKey; public Texture2D Texture; }
    private static readonly Dictionary<string, CacheEntry> CacheByProfile =
        new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

    private readonly XUiController textureController;
    private string boundProfileName = string.Empty;

    public RebirthPlayerProfileModelBinder(XUiController controller)
    {
        textureController = controller;
        Clear();
    }

    public bool Bind(string profileName)
    {
        XUiV_Texture view = GetView();
        if (view == null) return false;

        if (string.IsNullOrEmpty(profileName))
        {
            Clear();
            return false;
        }

        Archetype archetype;
        string source;
        if (!RebirthNativePlayerProfileBridge.TryResolvePlayerProfileArchetype(profileName, out archetype, out source) || archetype == null)
        {
            // A transient native lookup failure must not erase the last good image for the same profile.
            CacheEntry last;
            if (CacheByProfile.TryGetValue(profileName, out last) && last != null && last.Texture != null)
            {
                view.Texture = last.Texture; view.IsVisible = true; boundProfileName = profileName; return true;
            }
            if (!string.Equals(boundProfileName,profileName,StringComparison.OrdinalIgnoreCase) || view.Texture == null)
            { view.Texture = null; view.IsVisible = false; }
            boundProfileName = profileName;
            if (RebirthSurvivorDebug.Enabled)
                Log.Warning(LogPrefix + " archetype unavailable profile='" + profileName + "' source='" + (source ?? string.Empty) + "'.");
            return false;
        }

        string identityKey = RebirthNativePlayerProfileBridge.BuildAppearanceCacheKey(profileName, archetype, RenderIdentity);
        CacheEntry existing;
        if (CacheByProfile.TryGetValue(profileName, out existing) && existing != null && existing.Texture != null &&
            string.Equals(existing.IdentityKey, identityKey, StringComparison.Ordinal))
        {
            view.Texture = existing.Texture; view.IsVisible = true; boundProfileName = profileName; return true;
        }

        Texture2D replacement = BuildSnapshot(profileName, archetype);
        if (replacement == null)
        {
            // Replacement failed: keep the previous owned snapshot visible and retry next bind.
            if (existing != null && existing.Texture != null)
            { view.Texture=existing.Texture; view.IsVisible=true; boundProfileName=profileName; return false; }
            if (!string.Equals(boundProfileName,profileName,StringComparison.OrdinalIgnoreCase) || view.Texture == null)
            { view.Texture=null; view.IsVisible=false; }
            boundProfileName=profileName; return false;
        }

        CacheByProfile[profileName] = new CacheEntry { IdentityKey=identityKey, Texture=replacement };
        view.Texture=replacement; view.IsVisible=true; boundProfileName=profileName;
        if(existing!=null && existing.Texture!=null && !ReferenceEquals(existing.Texture,replacement)) UnityEngine.Object.Destroy(existing.Texture);
        return true;
    }

    public static void Invalidate(string profileName)
    {
        if(string.IsNullOrEmpty(profileName))return;
        CacheEntry entry;
        if(CacheByProfile.TryGetValue(profileName,out entry))
        {
            CacheByProfile.Remove(profileName);
            if(entry!=null && entry.Texture!=null)UnityEngine.Object.Destroy(entry.Texture);
        }
    }

    public static void InvalidateAll()
    {
        foreach(CacheEntry entry in CacheByProfile.Values) if(entry!=null && entry.Texture!=null)UnityEngine.Object.Destroy(entry.Texture);
        CacheByProfile.Clear();
    }

    public void Clear()
    {
        XUiV_Texture view = GetView();
        if (view != null)
        {
            view.Texture = null;
            view.IsVisible = false;
        }
        boundProfileName = string.Empty;
    }

    private XUiV_Texture GetView()
    {
        return textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
    }

    private static Texture2D BuildSnapshot(string profileName, Archetype archetype)
    {
        Texture2D snapshot = null;
        GameObject rig = null;
        SDCSUtils.TransformCatalog boneCatalog = null;
        RenderTextureSystem renderSystem = null;
        try
        {
            SDCSUtils.CreateVizUI(archetype, ref rig, ref boneCatalog, (EntityPlayer)null, false);
            if ((UnityEngine.Object)rig == (UnityEngine.Object)null)
                throw new InvalidOperationException("CreateVizUI returned no model rig.");

            Animator animator = rig.GetComponentInChildren<Animator>();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null) animator.Update(0f);

            renderSystem = new RenderTextureSystem();
            GameObject target = new GameObject("RebirthSpawnProfileModelTarget_" + SafeName(profileName));
            renderSystem.Create("rebirthSpawnProfileModel_" + SafeName(profileName), target,
                Vector3.zero, Vector3.zero, new Vector2i(TextureWidth, TextureHeight), true);

            rig.transform.SetParent(renderSystem.TargetGO.transform, false);
            rig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rig.transform.localPosition = new Vector3(0f, -0.95f, 0f);
            renderSystem.TargetGO.transform.localPosition = new Vector3(0f, -0.28f, 3.0f);
            Utils.SetLayerRecursively(renderSystem.TargetGO, 11);

            Camera camera = renderSystem.CameraGO != null ? renderSystem.CameraGO.GetComponent<Camera>() : null;
            if ((UnityEngine.Object)camera == (UnityEngine.Object)null)
                throw new InvalidOperationException("Model preview camera was not created.");

            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.Euler(1.5f, 0f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 24f;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.05f, 1f);

            Light light = renderSystem.LightGO != null ? renderSystem.LightGO.GetComponent<Light>() : null;
            if ((UnityEngine.Object)light != (UnityEngine.Object)null)
            {
                renderSystem.LightGO.transform.localPosition = new Vector3(0.6f, 0.9f, 0.8f);
                light.type = LightType.Point;
                light.range = 20f;
                light.intensity = 1.45f;
                light.color = new Color(1f, 0.93f, 0.86f, 1f);
            }

            camera.transform.RotateAround(rig.transform.position, Vector3.up, 16f);
            if (renderSystem.LightGO != null)
                renderSystem.LightGO.transform.RotateAround(rig.transform.position, Vector3.up, 16f);

            CharacterGazeController gaze = rig.GetComponentInChildren<CharacterGazeController>();
            if ((UnityEngine.Object)gaze != (UnityEngine.Object)null) gaze.SnapNextUpdate();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null) animator.Update(0f);

            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = renderSystem.RenderTex;
                snapshot = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
                snapshot.name = "RebirthSpawnPlayerModel_" + SafeName(profileName);
                snapshot.ReadPixels(new Rect(0f, 0f, TextureWidth, TextureHeight), 0, 0);
                snapshot.Apply();
                snapshot.filterMode = FilterMode.Trilinear;
                snapshot.anisoLevel = 2;
                snapshot.wrapMode = TextureWrapMode.Clamp;
            }
            finally
            {
                RenderTexture.active = previous;
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            if (snapshot != null) UnityEngine.Object.Destroy(snapshot);
            Log.Warning(LogPrefix + " build failed profile='" + (profileName ?? string.Empty) + "': " + ex.GetType().Name + ": " + ex.Message);
            return null;
        }
        finally
        {
            if ((UnityEngine.Object)rig != (UnityEngine.Object)null)
            {
                try { SDCSUtils.UnloadViz(rig); }
                catch { UnityEngine.Object.Destroy(rig); }
            }
            if (renderSystem != null) renderSystem.Cleanup();
            if (boneCatalog != null) boneCatalog.Clear();
        }
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "profile";
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') chars[i] = '_';
        }
        return new string(chars);
    }
}
