using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Entity-aware SDCS full-body render for the redesigned Character Overview. Unlike the profile
/// creator snapshot, this path passes the live EntityAlive (player or human companion) so equipped armor/clothing/cosmetics
/// are represented. The render is rebuilt only when the live appearance/equipment signature changes.
/// </summary>
public sealed class RebirthCharacterModelBinder
{
    private const string LogPrefix = "[REBIRTH Survivor][CharacterModel]";
    private const int TextureWidth = 512;
    private const int TextureHeight = 896;
    // Layer 11 is also used by native character previews. Keep this snapshot's camera
    // isolated from any other live or deferred preview rig in the scene.
    private const int SnapshotLayer = 31;

    private readonly XUiController textureController;
    // Texture2D in the legacy (ReadPixels) mode, RenderTexture in the shared GPU mode.
    private Texture currentTexture;
    private int lastSignature = int.MinValue;
    private int lastPlayerInstance;
    private float retryAfter;
    // sharedGpu: finished image is copied GPU-side (no ReadPixels stall) into a RenderTexture owned by a small static
    // cache keyed by entity + appearance signature, so the Companion Overview portrait and the Equipment tab share one
    // render. logLabel (optional) prints "<label> model ms=<n> stage=..." for the profiler batch. The Character and
    // Players windows use the one-argument constructor and keep the original ReadPixels behaviour unchanged.
    private readonly bool sharedGpu;
    private readonly string logLabel;

    public RebirthCharacterModelBinder(XUiController controller) : this(controller, false, null) { }

    public RebirthCharacterModelBinder(XUiController controller, bool sharedGpu, string logLabel)
    {
        textureController = controller;
        this.sharedGpu = sharedGpu;
        this.logLabel = logLabel;
        Clear();
    }

    // A destroyed texture (Unity null) or a RenderTexture that lost its GPU surface (device reset: IsCreated false) is
    // not reusable; the caller then rebinds from the cache or rebuilds.
    private static bool Usable(Texture tex)
    {
        if (tex == null) return false;
        RenderTexture rt = tex as RenderTexture;
        return rt == null || rt.IsCreated();
    }

    // When false the per-stage lines are suppressed so the caller can print one summary line per open.
    public bool LogStages = true;
    // Stage milliseconds of the most recent staged build / cache attach (for caller summaries).
    public long LastPrepareMs, LastFinishMs;

    // Drops every cached shared image (call on world unload if entity ids may be reused).
    public static void ClearSharedCache() { SharedCache.Clear(); }

    // Shared textures belong to the cache; only legacy textures are destroyed by their binder.
    private void ReleaseCurrent()
    {
        if (!sharedGpu && currentTexture != null) UnityEngine.Object.Destroy(currentTexture);
        currentTexture = null;
    }

    // Cheap path for a window that may already have a finished image of this NPC from another binder (shared mode only).
    public bool TryAttachCached(EntityAlive player)
    {
        XUiV_Texture view = textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
        if (view == null || player == null || !sharedGpu) return false;
        return AttachCached(view, player, ComputeAppearanceSignature(player), player.GetInstanceID());
    }

    private bool AttachCached(XUiV_Texture view, EntityAlive player, int signature, int playerInstance)
    {
        if (!sharedGpu) return false;
        Texture hit = SharedCache.Get(player.entityId, signature);
        if (hit == null) return false;
        currentTexture = hit;
        lastSignature = signature;
        lastPlayerInstance = playerInstance;
        retryAfter = 0f;
        view.Texture = hit;
        view.IsVisible = true;
        LastPrepareMs = 0; LastFinishMs = 0;
        if (logLabel != null && LogStages) Log.Out(logLabel + " model ms=0 stage=cache-hit entity=" + player.entityId);
        return true;
    }

    public bool Bind(EntityAlive player, bool force)
    {
        XUiV_Texture view = textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
        if (view == null || player == null)
        {
            Clear();
            return false;
        }

        int signature = ComputeAppearanceSignature(player);
        int playerInstance = player.GetInstanceID();
        if (!force && Usable(currentTexture) && signature == lastSignature && playerInstance == lastPlayerInstance)
        {
            if (view.Texture != currentTexture) view.Texture = currentTexture;
            if (!view.IsVisible) view.IsVisible = true;
            return true;
        }

        if (!force && AttachCached(view, player, signature, playerInstance)) return true;
        if (!force && Time.realtimeSinceStartup < retryAfter) return currentTexture != null;
        Texture next = BuildSnapshot(player);
        if (next == null)
        {
            retryAfter = Time.realtimeSinceStartup + 2f;
            if (currentTexture == null) view.IsVisible = false;
            return currentTexture != null;
        }

        ReleaseCurrent();
        currentTexture = next;
        if (sharedGpu) SharedCache.Put(player.entityId, signature, next);
        lastSignature = signature;
        lastPlayerInstance = playerInstance;
        retryAfter = 0f;
        view.Texture = currentTexture;
        view.IsVisible = true;
        return true;
    }

    private StagedBuild staged;
    private int stagedSignature, stagedInstance;
    public bool IsStaging { get { return staged != null; } }

    // Two-frame variant of Bind(player, true) for the companion Equipment tab. Call once to prepare (returns false and
    // IsStaging becomes true), call again on a later frame to render and install (returns true). A false return with
    // IsStaging false means the build failed or the appearance changed in between; the caller decides whether to retry.
    public bool BindStaged(EntityAlive player)
    {
        XUiV_Texture view = textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
        if (view == null || player == null)
        {
            Clear();
            return false;
        }

        int signature = ComputeAppearanceSignature(player);
        int playerInstance = player.GetInstanceID();
        if (staged != null)
        {
            StagedBuild build = staged;
            staged = null;
            if (signature != stagedSignature || playerInstance != stagedInstance) { build.Cleanup(); return false; }
            Texture finished = build.Finish();
            LastFinishMs = build.FinishMs;
            if (finished == null)
            {
                retryAfter = Time.realtimeSinceStartup + 2f;
                if (currentTexture == null) view.IsVisible = false;
                return false;
            }
            ReleaseCurrent();
            currentTexture = finished;
            if (sharedGpu) SharedCache.Put(player.entityId, signature, finished);
            lastSignature = signature;
            lastPlayerInstance = playerInstance;
            retryAfter = 0f;
            view.Texture = currentTexture;
            view.IsVisible = true;
            return true;
        }

        if (Usable(currentTexture) && signature == lastSignature && playerInstance == lastPlayerInstance)
        {
            if (view.Texture != currentTexture) view.Texture = currentTexture;
            if (!view.IsVisible) view.IsVisible = true;
            return true;
        }

        if (AttachCached(view, player, signature, playerInstance)) return true;
        StagedBuild next = new StagedBuild(sharedGpu, logLabel) { Quiet = !LogStages };
        if (!next.Prepare(player)) return false;
        LastPrepareMs = next.PrepareMs;
        staged = next;
        stagedSignature = signature;
        stagedInstance = playerInstance;
        return false;
    }

    // Closing a menu does not change appearance. Keep only the finished image, not a live
    // preview rig/camera, so an immediate reopen need not instantiate and render SDCS again.
    public void Suspend()
    {
        if (staged != null) { staged.Cleanup(); staged = null; }
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
        ReleaseCurrent();
        lastSignature = int.MinValue;
        lastPlayerInstance = 0;
        retryAfter = 0f;
    }

    private Texture BuildSnapshot(EntityAlive player)
    {
        StagedBuild build = new StagedBuild(sharedGpu, logLabel);
        if (!build.Prepare(player)) return null;
        return build.Finish();
    }

    // One snapshot split in two so a hitch-sensitive window can spread it over two frames: Prepare creates the
    // SDCS rig, the render target and frames the camera; Finish renders, reads the pixels back and always cleans up.
    // BuildSnapshot (Character / Players windows) runs both back to back, so its behaviour is unchanged.
    private sealed class StagedBuild
    {
        private GameObject rig;
        private SDCSUtils.TransformCatalog catalog;
        private RenderTextureSystem renderSystem;
        private Camera camera;
        private int entityId;
        private readonly bool gpuCopy;
        private readonly string label;
        private System.Diagnostics.Stopwatch prepareTimer;
        internal bool Quiet;
        internal long PrepareMs, FinishMs;

        internal StagedBuild(bool gpuCopy, string label)
        {
            this.gpuCopy = gpuCopy;
            this.label = label;
        }

        internal bool Prepare(EntityAlive player)
        {
            try
            {
                prepareTimer = System.Diagnostics.Stopwatch.StartNew();
                entityId = player.entityId;
                EModelSDCS model = player.emodel as EModelSDCS;
                Archetype archetype = (UnityEngine.Object)model != (UnityEngine.Object)null ? model.Archetype : null;
                if ((UnityEngine.Object)model == (UnityEngine.Object)null || archetype == null) { Cleanup(); return false; }

                SDCSUtils.CreateVizUI(archetype, ref rig, ref catalog, player, false);
                if ((UnityEngine.Object)rig == (UnityEngine.Object)null) { Cleanup(); return false; }

                Animator animator = rig.GetComponentInChildren<Animator>();
                if ((UnityEngine.Object)animator != (UnityEngine.Object)null) animator.Update(0f);

                renderSystem = new RenderTextureSystem();
                GameObject target = new GameObject("RebirthCharacterOverviewTarget_" + entityId);
                renderSystem.Create("rebirthCharacterOverview_" + entityId, target, Vector3.zero, Vector3.zero,
                    // Native AA=true doubles both render dimensions. ReadPixels below must read
                    // the complete image, not the lower-left quarter of a doubled render target.
                    new Vector2i(TextureWidth, TextureHeight), false);

                rig.transform.SetParent(renderSystem.TargetGO.transform, false);
                rig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                rig.transform.localPosition = Vector3.zero;
                renderSystem.TargetGO.transform.localPosition = new Vector3(0f, 0f, 3.25f);
                Utils.SetLayerRecursively(renderSystem.TargetGO, SnapshotLayer);

                camera = renderSystem.CameraGO != null ? renderSystem.CameraGO.GetComponent<Camera>() : null;
                if ((UnityEngine.Object)camera == (UnityEngine.Object)null) { Cleanup(); return false; }
                CharacterGazeController gaze = rig.GetComponentInChildren<CharacterGazeController>();
                if ((UnityEngine.Object)gaze != (UnityEngine.Object)null) gaze.SnapNextUpdate();
                if ((UnityEngine.Object)animator != (UnityEngine.Object)null) animator.Update(0f);

                // Frame the entire equipped visual, not a fixed camera offset aimed at the waist.
                Bounds bounds;
                if (!TryGetVisualBounds(rig, out bounds)) { Cleanup(); return false; }
                float aspect = TextureWidth / (float)TextureHeight;
                float halfHeight = Math.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.12f;
                float distance = Math.Max(4f, bounds.extents.z * 2f + 1f);
                camera.transform.rotation = Quaternion.identity;
                camera.transform.position = bounds.center - Vector3.forward * distance;
                camera.orthographic = true;
                // RenderTextureSystem's camera can otherwise include live-world geometry.
                // Only the temporary SDCS rig was assigned to this preview layer.
                camera.cullingMask = 1 << SnapshotLayer;
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
                PrepareMs = prepareTimer.ElapsedMilliseconds;
                if (label != null && !Quiet)
                    Log.Out(label + " model ms=" + PrepareMs + " stage=prepare(CreateVizUI+rig+camera) entity=" + entityId);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(LogPrefix + " build failed: " + ex.GetType().Name + ": " + ex.Message);
                Cleanup();
                return false;
            }
        }

        internal Texture Finish()
        {
            Texture2D snapshot = null;
            RenderTexture gpuImage = null;
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if ((UnityEngine.Object)camera == (UnityEngine.Object)null || renderSystem == null) return null;
                if (gpuCopy)
                {
                    // GPU-side copy: no CPU readback / pipeline stall. The temporary rig and camera are still released below.
                    camera.Render();
                    gpuImage = new RenderTexture(TextureWidth, TextureHeight, 0, RenderTextureFormat.ARGB32);
                    gpuImage.name = "RebirthCharacterOverviewModel_" + entityId;
                    gpuImage.filterMode = FilterMode.Bilinear;
                    gpuImage.wrapMode = TextureWrapMode.Clamp;
                    Graphics.Blit(renderSystem.RenderTex, gpuImage);
                    FinishMs = timer.ElapsedMilliseconds;
                    if (label != null && !Quiet)
                        Log.Out(label + " model ms=" + FinishMs + " stage=finish(render+gpu-copy) entity=" + entityId);
                    return gpuImage;
                }
                RenderTexture previous = RenderTexture.active;
                try
                {
                    camera.Render();
                    RenderTexture.active = renderSystem.RenderTex;
                    snapshot = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
                    snapshot.name = "RebirthCharacterOverviewModel_" + entityId;
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
                if (gpuImage != null) { gpuImage.Release(); UnityEngine.Object.Destroy(gpuImage); }
                Log.Warning(LogPrefix + " build failed: " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
            finally { Cleanup(); }
        }

        internal void Cleanup()
        {
            if ((UnityEngine.Object)rig != (UnityEngine.Object)null)
            {
                try { SDCSUtils.UnloadViz(rig); }
                catch { UnityEngine.Object.Destroy(rig); }
            }
            rig = null;
            if (renderSystem != null)
            {
                // Destroy is deferred; deactivate the temporary preview immediately so a
                // second appearance render in this frame cannot pick up the previous rig.
                renderSystem.SetEnabled(false);
                renderSystem.Cleanup();
                renderSystem = null;
            }
            if (catalog != null) { catalog.Clear(); catalog = null; }
            camera = null;
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

    // Small static cache of finished GPU images keyed by entity and appearance signature (shared mode only). Replaced or
    // evicted textures are destroyed 5 s later, so a view still showing one is rebound by its binder first.
    private static class SharedCache
    {
        private sealed class Entry { internal int Entity, Signature, Stamp; internal Texture Tex; }
        private const int MaxEntries = 4;
        private static readonly System.Collections.Generic.List<Entry> entries = new System.Collections.Generic.List<Entry>();
        private static readonly System.Collections.Generic.List<KeyValuePair5> retired = new System.Collections.Generic.List<KeyValuePair5>();
        private struct KeyValuePair5 { internal float DestroyAt; internal Texture Tex; }
        private static int clock;

        internal static void Clear()
        {
            for (int i = 0; i < entries.Count; i++) Retire(entries[i].Tex);
            entries.Clear();
        }

        private static bool Alive(Texture tex)
        {
            if (tex == null) return false;
            RenderTexture rt = tex as RenderTexture;
            return rt == null || rt.IsCreated();
        }

        internal static Texture Get(int entity, int signature)
        {
            Sweep();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e.Entity == entity && e.Signature == signature && Alive(e.Tex)) { e.Stamp = ++clock; return e.Tex; }
            }
            return null;
        }

        internal static void Put(int entity, int signature, Texture tex)
        {
            Sweep();
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].Entity == entity)
                {
                    if (!ReferenceEquals(entries[i].Tex, tex)) Retire(entries[i].Tex);
                    entries.RemoveAt(i);
                }
            entries.Add(new Entry { Entity = entity, Signature = signature, Tex = tex, Stamp = ++clock });
            while (entries.Count > MaxEntries)
            {
                int oldest = 0;
                for (int i = 1; i < entries.Count; i++) if (entries[i].Stamp < entries[oldest].Stamp) oldest = i;
                Retire(entries[oldest].Tex);
                entries.RemoveAt(oldest);
            }
        }

        private static void Retire(Texture tex)
        {
            if (tex != null) retired.Add(new KeyValuePair5 { DestroyAt = Time.realtimeSinceStartup + 5f, Tex = tex });
        }

        private static void Sweep()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = retired.Count - 1; i >= 0; i--)
                if (retired[i].DestroyAt <= now)
                {
                    Texture tex = retired[i].Tex;
                    retired.RemoveAt(i);
                    if (tex != null)
                    {
                        RenderTexture rt = tex as RenderTexture;
                        if (rt != null) rt.Release();
                        UnityEngine.Object.Destroy(tex);
                    }
                }
        }
    }

    private static int ComputeAppearanceSignature(EntityAlive player)
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

