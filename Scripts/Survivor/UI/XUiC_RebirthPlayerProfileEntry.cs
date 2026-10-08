using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native Player Profile list entry with a lightweight SDCS face snapshot.
/// Selection and profile data remain owned by the base XUiC_ProfilesList entry;
/// REBIRTH only supplies the portrait and forwards mouse-wheel input to the
/// Survivor Creator's companion-style scrollbar.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthPlayerProfileEntry : XUiC_ProfilesList.EntryController
{
    private const string LogPrefix = "[REBIRTH Survivor][PlayerProfilePortrait]";
    private static float nextGlobalBuildAt;

    private XUiV_Texture faceTexture;
    private Texture2D faceSnapshot;
    private GameObject portraitRig;
    private SDCSUtils.TransformCatalog portraitBoneCatalog;
    private RenderTextureSystem portraitRenderSystem;
    private int portraitSignature = int.MinValue;
    private float nextProbeAt;
    private bool loggedResolutionFailure;

    public override void Init()
    {
        base.Init();
        XUiController face = GetChildById("profileFace");
        faceTexture = face != null ? face.ViewComponent as XUiV_Texture : null;
        SetFaceVisible(false);

        if (ViewComponent != null)
            ViewComponent.EventOnScroll = true;
        OnScroll += Entry_OnScroll;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (faceTexture == null) return;

        float now = Time.realtimeSinceStartup;
        if (now < nextProbeAt)
            return;
        nextProbeAt = now + 0.15f;

        Archetype archetype;
        if (!TryResolveArchetype(this, out archetype) || archetype == null)
        {
            ClearFace();
            return;
        }

        int signature = ComputeArchetypeSignature(archetype);
        if (signature == portraitSignature && faceSnapshot != null)
            return;

        if (now < nextGlobalBuildAt)
            return;
        nextGlobalBuildAt = now + 0.025f;
        TryBuildFace(archetype, signature);
    }

    public override void OnClose()
    {
        CleanupPortrait();
        base.OnClose();
    }

    public override void Cleanup()
    {
        CleanupPortrait();
        base.Cleanup();
    }

    private void Entry_OnScroll(XUiController sender, float delta)
    {
        XUiC_RebirthSurvivorCreator creator = xui != null ? xui.GetChildByType<XUiC_RebirthSurvivorCreator>() : null;
        if (creator != null)
            creator.ScrollEmbeddedPlayerProfiles(delta);
    }

    private bool TryBuildFace(Archetype archetype, int signature)
    {
        CleanupPortrait();

        if (faceTexture == null)
            return false;

        try
        {
            // Native profile creation/preview uses the same SDCS archetype data.
            // No world EntityPlayer is required for an appearance-only profile.
            SDCSUtils.CreateVizUI(archetype, ref portraitRig, ref portraitBoneCatalog, (EntityPlayer)null, false);
            if ((UnityEngine.Object)portraitRig == (UnityEngine.Object)null)
                return false;

            Animator animator = portraitRig.GetComponentInChildren<Animator>();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null)
                animator.Update(0f);

            portraitRenderSystem = new RenderTextureSystem();
            GameObject target = new GameObject("RebirthPlayerProfilePortraitTarget");
            portraitRenderSystem.Create(
                "rebirthPlayerProfilePortrait",
                target,
                Vector3.zero,
                Vector3.zero,
                new Vector2i(96, 96),
                true);

            portraitRig.transform.SetParent(portraitRenderSystem.TargetGO.transform, false);
            portraitRig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            portraitRig.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            portraitRenderSystem.TargetGO.transform.localPosition = new Vector3(-0.015f, -0.78f, 2.14f);
            Utils.SetLayerRecursively(portraitRenderSystem.TargetGO, 11);

            Camera camera = portraitRenderSystem.CameraGO != null
                ? portraitRenderSystem.CameraGO.GetComponent<Camera>()
                : null;
            if ((UnityEngine.Object)camera == (UnityEngine.Object)null)
                return false;

            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.Euler(1.5f, 0f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 9f;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            Light light = portraitRenderSystem.LightGO != null
                ? portraitRenderSystem.LightGO.GetComponent<Light>()
                : null;
            if ((UnityEngine.Object)light != (UnityEngine.Object)null)
            {
                portraitRenderSystem.LightGO.transform.localPosition = new Vector3(0.35f, 0.45f, 0.5f);
                light.type = LightType.Point;
                light.range = 20f;
                light.intensity = 1.5f;
                light.color = new Color(1f, 0.92f, 0.84f, 1f);
            }

            camera.transform.RotateAround(portraitRig.transform.position, Vector3.up, 30f);
            if (portraitRenderSystem.LightGO != null)
                portraitRenderSystem.LightGO.transform.RotateAround(portraitRig.transform.position, Vector3.up, 30f);

            CharacterGazeController gazeController = portraitRig.GetComponentInChildren<CharacterGazeController>();
            if ((UnityEngine.Object)gazeController != (UnityEngine.Object)null)
                gazeController.SnapNextUpdate();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null)
                animator.Update(0f);

            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = portraitRenderSystem.RenderTex;
                faceSnapshot = new Texture2D(96, 96, TextureFormat.RGBA32, false);
                faceSnapshot.name = "RebirthPlayerProfileFace";
                faceSnapshot.ReadPixels(new Rect(0f, 0f, 96f, 96f), 0, 0);
                faceSnapshot.Apply();
                faceSnapshot.filterMode = FilterMode.Bilinear;
            }
            finally
            {
                RenderTexture.active = previous;
            }

            faceTexture.Texture = faceSnapshot;
            portraitSignature = signature;
            SetFaceVisible(true);
            ReleaseRenderObjects();
            loggedResolutionFailure = false;
            return true;
        }
        catch (Exception ex)
        {
            if (!loggedResolutionFailure)
            {
                loggedResolutionFailure = true;
                Log.Warning(LogPrefix + " portrait build failed: " + ex.GetType().Name + ": " + ex.Message);
            }
            CleanupPortrait();
            return false;
        }
    }

    private static bool TryResolveArchetype(object root, out Archetype archetype)
    {
        archetype = null;
        if (root == null)
            return false;
        HashSet<object> visited = new HashSet<object>();
        return TryResolveArchetypeRecursive(root, 0, 4, visited, out archetype);
    }

    private static bool TryResolveArchetypeRecursive(object value, int depth, int maxDepth, HashSet<object> visited, out Archetype archetype)
    {
        archetype = value as Archetype;
        if (archetype != null)
            return true;
        archetype = null;

        if (value == null || depth >= maxDepth || value is string)
            return false;
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum)
            return false;
        if (value is UnityEngine.Object || value is XUi)
            return false;
        if (!visited.Add(value))
            return false;

        bool profileObject = type.Name.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             type.Name.IndexOf("Character", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             type.Name.IndexOf("Archetype", StringComparison.OrdinalIgnoreCase) >= 0;

        for (Type scan = type; scan != null; scan = scan.BaseType)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields;
            try { fields = scan.GetFields(flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!profileObject && !LooksAppearanceRelated(field.Name))
                    continue;
                object child;
                try { child = field.GetValue(value); } catch { continue; }
                if (TryResolveArchetypeRecursive(child, depth + 1, maxDepth, visited, out archetype))
                    return true;
            }

            PropertyInfo[] properties;
            try { properties = scan.GetProperties(flags); } catch { properties = new PropertyInfo[0]; }
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;
                if (!profileObject && !LooksAppearanceRelated(property.Name))
                    continue;
                object child;
                try { child = property.GetValue(value, null); } catch { continue; }
                if (TryResolveArchetypeRecursive(child, depth + 1, maxDepth, visited, out archetype))
                    return true;
            }
        }
        return false;
    }

    private static bool LooksAppearanceRelated(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name.IndexOf("profile", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("archetype", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("appearance", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("avatar", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("selected", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("entry", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("data", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int ComputeArchetypeSignature(Archetype archetype)
    {
        if (archetype == null)
            return 0;
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StableHash(archetype.Name);
            hash = hash * 31 + StableHash(archetype.Race);
            hash = hash * 31 + archetype.Variant;
            hash = hash * 31 + StableHash(archetype.Hair);
            hash = hash * 31 + StableHash(archetype.HairColor);
            hash = hash * 31 + StableHash(archetype.MustacheName);
            hash = hash * 31 + StableHash(archetype.ChopsName);
            hash = hash * 31 + StableHash(archetype.BeardName);
            hash = hash * 31 + StableHash(archetype.EyeColorName);
            hash = hash * 31 + (archetype.IsMale ? 1 : 0);
            return hash;
        }
    }

    private static int StableHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash;
        }
    }

    private void ClearFace()
    {
        if (faceTexture != null)
            faceTexture.IsVisible = false;
    }

    private void SetFaceVisible(bool visible)
    {
        if (faceTexture != null)
            faceTexture.IsVisible = visible;
    }

    private void CleanupPortrait()
    {
        if (faceTexture != null)
        {
            faceTexture.Texture = null;
            faceTexture.IsVisible = false;
        }
        if ((UnityEngine.Object)faceSnapshot != (UnityEngine.Object)null)
            UnityEngine.Object.Destroy(faceSnapshot);
        faceSnapshot = null;
        portraitSignature = int.MinValue;
        ReleaseRenderObjects();
    }

    private void ReleaseRenderObjects()
    {
        if ((UnityEngine.Object)portraitRig != (UnityEngine.Object)null)
        {
            try { SDCSUtils.UnloadViz(portraitRig); }
            catch { UnityEngine.Object.Destroy(portraitRig); }
        }
        portraitRig = null;
        portraitBoneCatalog = null;

        if (portraitRenderSystem != null)
        {
            portraitRenderSystem.Cleanup();
            portraitRenderSystem = null;
        }
    }
}
