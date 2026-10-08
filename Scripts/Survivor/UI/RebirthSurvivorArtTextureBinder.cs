using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#nullable disable

/// <summary>
/// Safe mod-local binder for Survivor artwork.
///
/// Revision 6:
/// - Current Background list rows prefer normalized transparent PNG icon thumbnails; legacy JPEG
///   thumbnails remain a fallback for compatibility with older overlay installs.
/// - Background list rows use a shared, synchronously preloaded thumbnail cache so scrolling never
///   performs disk IO or image decoding.
/// - The selected Background detail view still uses the full authored image and remains binder-owned.
/// - Arbitrary paths are never accepted from profile/client data; only authored asset keys resolve.
/// </summary>
public sealed class RebirthSurvivorArtTextureBinder
{
    private static readonly Dictionary<string, Texture2D> BackgroundThumbnailCache =
        new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private static bool thumbnailPreloadReported;
    private static readonly Dictionary<string,int> NegativeAssetGeneration = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);

    private readonly XUiController textureController;
    private Texture lastTexture;
    private Texture2D ownedTexture;
    private string lastPath = string.Empty;
    private float targetAspect;
    private bool sharedTextureBound;
    private string failedPath = string.Empty;
    private long failedPathStamp;

    public RebirthSurvivorArtTextureBinder(XUiController controller, float aspect)
    {
        textureController = controller;
        targetAspect = aspect > 0f ? aspect : 16f / 9f;
    }

    /// <summary>
    /// Synchronously loads every authored Background row thumbnail once. This is intentionally called
    /// before the Creator performs its first render; row scrolling must never trigger image IO/decoding.
    /// Full-resolution Background artwork is not preloaded because those masters are much larger and are
    /// only needed for the selected-detail panel.
    /// </summary>
    public static int PreloadBackgroundThumbnails()
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Backgrounds == null) return 0;

        int loaded = 0;
        for (int i = 0; i < bundle.Backgrounds.Count; i++)
        {
            RebirthBackgroundDefinition background = bundle.Backgrounds[i];
            string key = NormalizeKey(background != null ? background.BackgroundArtKey : string.Empty);
            if (key.Length == 0) continue;

            Texture2D cached;
            if (BackgroundThumbnailCache.TryGetValue(key, out cached) && cached != null)
            {
                loaded++;
                continue;
            }

            string path;
            if (!TryResolveThumbnailPath(key, out path))
            {
                Log.Warning("[REBIRTH Survivor UI] missing preloaded Background thumbnail for '" + key + "'.");
                continue;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "RebirthSurvivorBackgroundThumb_" + key;
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    Log.Warning("[REBIRTH Survivor UI] failed to decode Background thumbnail '" + path + "'.");
                    continue;
                }
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                BackgroundThumbnailCache[key] = texture;
                loaded++;
            }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH Survivor UI] Background thumbnail preload failed for '" + path + "': " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        if (!thumbnailPreloadReported)
        {
            thumbnailPreloadReported = true;
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Survivor UI] Background thumbnail cache ready loaded=" + loaded + "/" + bundle.Backgrounds.Count + "."); }
        }
        return loaded;
    }

    public bool BindBackgroundThumbnail(RebirthBackgroundDefinition background)
    {
        string key = NormalizeKey(background != null ? background.BackgroundArtKey : string.Empty);
        if (key.Length == 0)
        {
            Clear();
            return false;
        }

        Texture2D texture;
        if (!BackgroundThumbnailCache.TryGetValue(key, out texture) || texture == null)
        {
            // Defensive recovery only. Normal Creator flow preloads every thumbnail before first render.
            PreloadBackgroundThumbnails();
            if (!BackgroundThumbnailCache.TryGetValue(key, out texture) || texture == null)
            {
                Clear();
                return false;
            }
        }
        return BindSharedTexture(texture, "thumb:" + key);
    }

    public bool BindBackground(RebirthBackgroundDefinition background)
    {
        if(background==null){Clear();return false;}
        string path;
        if(!TryResolvePath(background.BackgroundArtKey,out path))return HasCurrentTexture();
        return BindPath(path,"RebirthSurvivorBackground_");
    }

    public bool BindLocalSurvivorAsset(string assetKey)
    {
        string key = NormalizeKey(assetKey);
        if (key.Length == 0)
        {
            Clear();
            return false;
        }
        try
        {
            string configRoot = RebirthSurvivorDefinitionRegistry.Bundle != null
                ? RebirthSurvivorDefinitionRegistry.Bundle.ConfigRoot
                : RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
            if (string.IsNullOrEmpty(configRoot)) return HasCurrentTexture();
            string modRoot = Path.GetFullPath(Path.Combine(configRoot, "..", ".."));
            string folder = Path.Combine(modRoot, "UIAssets", "Survivor");
            string jpg = Path.Combine(folder, key + ".jpg");
            string png = Path.Combine(folder, key + ".png");
            string path = File.Exists(jpg) ? jpg : (File.Exists(png) ? png : string.Empty);
            if (path.Length == 0) return HasCurrentTexture();
            return BindPath(path, "RebirthSurvivorUiAsset_");
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor UI] local Survivor texture resolution failed for '" + key + "': " + ex.GetType().Name + ": " + ex.Message);
            return HasCurrentTexture();
        }
    }

    private bool BindSharedTexture(Texture2D texture, string cacheKey)
    {
        if (texture == null)
        {
            Clear();
            return false;
        }

        XUiV_Texture view = GetTextureView();
        if (view == null) return false;
        string boundKey = cacheKey ?? string.Empty;
        if (!sharedTextureBound || !string.Equals(lastPath, boundKey, StringComparison.OrdinalIgnoreCase) || !ReferenceEquals(view.Texture, texture))
        {
            DetachCurrent(view);
            view.Texture = texture;
            view.UVRect = new Rect(0f, 0f, 1f, 1f);
            sharedTextureBound = true;
            lastPath = boundKey;
            lastTexture = null;
        }
        ApplyCrop();
        return true;
    }

    private bool BindPath(string path, string textureNamePrefix)
    {
        if (string.IsNullOrEmpty(path)) return false;
        XUiV_Texture view=GetTextureView(); if(view==null)return false;
        if(!sharedTextureBound && string.Equals(lastPath,path,StringComparison.OrdinalIgnoreCase) && ownedTexture!=null)
        { ApplyCrop(); return true; }

        long stamp=GetPathStamp(path);
        if(string.Equals(failedPath,path,StringComparison.OrdinalIgnoreCase) && failedPathStamp==stamp)
            return false;

        Texture2D loaded=null;
        try
        {
            byte[] bytes=File.ReadAllBytes(path); loaded=new Texture2D(2,2,TextureFormat.RGBA32,false);
            loaded.name=(textureNamePrefix??"RebirthSurvivorTexture_")+Path.GetFileNameWithoutExtension(path);
            if(!ImageConversion.LoadImage(loaded,bytes,false) || loaded.width<=0 || loaded.height<=0)
                throw new InvalidDataException("image decode/dimensions invalid");
            loaded.filterMode=FilterMode.Bilinear; loaded.wrapMode=TextureWrapMode.Clamp;

            // Publish only after the replacement is complete. A corrupt/missing replacement leaves
            // the prior good image attached and owned until a later generation succeeds.
            DetachCurrent(view);
            ownedTexture=loaded; loaded=null; sharedTextureBound=false; lastPath=path; lastTexture=null;
            failedPath=string.Empty; failedPathStamp=0L;
            view.Texture=ownedTexture; view.UVRect=new Rect(0f,0f,1f,1f); ApplyCrop(); return true;
        }
        catch(Exception ex)
        {
            if(loaded!=null)UnityEngine.Object.Destroy(loaded);
            failedPath=path; failedPathStamp=stamp;
            Log.Warning("[REBIRTH Survivor UI] texture load failed for '"+path+"': "+ex.GetType().Name+": "+ex.Message);
            ApplyCrop(); return HasCurrentTexture();
        }
    }

    private static long GetPathStamp(string path)
    {
        try{FileInfo info=new FileInfo(path);return info.Exists?(info.Length^info.LastWriteTimeUtc.Ticks):long.MinValue;}catch{return long.MinValue;}
    }

    private bool HasCurrentTexture()
    {
        XUiV_Texture view=GetTextureView(); return view!=null && view.Texture!=null;
    }

    public void Update()
    {
        ApplyCrop();
    }

    public void Clear()
    {
        DetachCurrent(GetTextureView());
        lastPath = string.Empty;
        failedPath = string.Empty; failedPathStamp = 0L;
    }

    public static bool TryResolvePath(string artKey, out string path)
    {
        return TryResolveBackgroundAsset(artKey, "Backgrounds", false, out path);
    }

    public static bool TryResolveThumbnailPath(string artKey, out string path)
    {
        // Background list art is now authored as normalized transparent PNG icons. Prefer PNG so
        // the current 28-background icon set wins over legacy JPEG thumbnails that may remain on
        // disk for compatibility with older project overlays.
        return TryResolveBackgroundAsset(artKey, "BackgroundThumbnails", true, out path);
    }

    private static bool TryResolveBackgroundAsset(string artKey, string folderName, bool preferPng, out string path)
    {
        path = string.Empty;
        string key = NormalizeKey(artKey);
        if (key.Length == 0) return false;
        int generation=RebirthSurvivorDefinitionRegistry.Generation;
        string negativeKey=(folderName??string.Empty)+"|"+(preferPng?"p":"j")+"|"+key;
        int missedGeneration;
        if(NegativeAssetGeneration.TryGetValue(negativeKey,out missedGeneration) && missedGeneration==generation)return false;
        try
        {
            // Use the same authoritative root that loaded the definitions. This avoids a second
            // dependency on ModManager enumeration and guarantees artwork/definition parity.
            string configRoot = RebirthSurvivorDefinitionRegistry.Bundle != null
                ? RebirthSurvivorDefinitionRegistry.Bundle.ConfigRoot
                : RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
            if (string.IsNullOrEmpty(configRoot)) return false;

            string modRoot = Path.GetFullPath(Path.Combine(configRoot, "..", ".."));
            string folder = Path.Combine(modRoot, "UIAssets", "Survivor", folderName ?? string.Empty);
            string jpg = Path.Combine(folder, key + ".jpg");
            string png = Path.Combine(folder, key + ".png");
            path = preferPng
                ? (File.Exists(png) ? png : (File.Exists(jpg) ? jpg : string.Empty))
                : (File.Exists(jpg) ? jpg : (File.Exists(png) ? png : string.Empty));
            if(path.Length==0){NegativeAssetGeneration[negativeKey]=generation;return false;}
            NegativeAssetGeneration.Remove(negativeKey);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor UI] Background-art resolution failed for '" + key + "': " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private XUiV_Texture GetTextureView()
    {
        return textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
    }

    private void DetachCurrent(XUiV_Texture view)
    {
        if (view != null)
        {
            if (sharedTextureBound || (ownedTexture != null && ReferenceEquals(view.Texture, ownedTexture)))
                view.Texture = null;
            view.UVRect = new Rect(0f, 0f, 1f, 1f);
        }
        if (ownedTexture != null)
            UnityEngine.Object.Destroy(ownedTexture);
        ownedTexture = null;
        sharedTextureBound = false;
        lastTexture = null;
    }

    private void ApplyCrop()
    {
        XUiV_Texture view = GetTextureView();
        if (view == null || view.Texture == null || ReferenceEquals(lastTexture, view.Texture)) return;
        lastTexture = view.Texture;
        view.KeepSourceAspectRatio = false;
        view.SourceAspectRatioRespectPivot = false;
        float sourceAspect = view.Texture.height > 0 ? view.Texture.width / (float)view.Texture.height : targetAspect;
        Rect uv = new Rect(0f, 0f, 1f, 1f);
        if (sourceAspect > targetAspect)
        {
            float width = targetAspect / sourceAspect;
            uv.x = (1f - width) * 0.5f;
            uv.width = width;
        }
        else if (sourceAspect > 0f && sourceAspect < targetAspect)
        {
            float height = sourceAspect / targetAspect;
            uv.y = (1f - height) * 0.5f;
            uv.height = height;
        }
        view.UVRect = uv;
    }

    private static string NormalizeKey(string value)
    {
        string key = (value ?? string.Empty).Trim();
        if (key.Length == 0 || key.IndexOf("..", StringComparison.Ordinal) >= 0 || key.IndexOf('/') >= 0 || key.IndexOf('\\') >= 0 || key.IndexOf(':') >= 0) return string.Empty;
        for (int i = 0; i < key.Length; i++)
        {
            char c = key[i];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return string.Empty;
        }
        return key;
    }
}
