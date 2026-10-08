using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Persistent SDCS face snapshots used by the Survivor Creator Player Profile roster.
/// Portraits are generated at high resolution once, written to the user's Rebirth cache,
/// and reused on later visits so scrolling and reopening the creator do not rebuild them.
/// </summary>
public sealed class RebirthPlayerProfilePortraitBinder
{
    private const string LogPrefix = "[REBIRTH Survivor][PlayerProfilePortrait]";
    private const int PortraitSize = 384;
    private const int CacheVersion = 5;
    private const string ManifestExtension = ".current";

    private sealed class CacheEntry
    {
        public Texture2D Texture;
        public long Signature;
    }

    private static readonly Dictionary<string, CacheEntry> Cache =
        new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,long> PersistentFailureStamp =
        new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);

    private readonly XUiController textureController;
    private string boundProfileName = string.Empty;

    public RebirthPlayerProfilePortraitBinder(XUiController controller)
    {
        textureController = controller;
        SetVisible(false);
    }

    public void Bind(RebirthNativePlayerProfileBridge.EmbeddedProfileInfo profile)
    {
        if (profile == null || string.IsNullOrEmpty(profile.Name))
        {
            ClearView();
            return;
        }

        if (profile.Archetype != null)
            Capture(profile.Name, profile.Archetype);

        BindCached(profile.Name);
    }

    public bool BindCached(string profileName)
    {
        XUiV_Texture view = GetView();
        if (view == null) return false;

        CacheEntry entry;
        if (!TryGetEntry(profileName, out entry))
        {
            if (!string.Equals(boundProfileName, profileName ?? string.Empty, StringComparison.OrdinalIgnoreCase) || view.Texture == null)
            { view.Texture = null; view.IsVisible = false; }
            else view.IsVisible = true;
            boundProfileName = profileName ?? string.Empty;
            return false;
        }

        view.Texture = entry.Texture;
        view.IsVisible = true;
        boundProfileName = profileName;
        return true;
    }

    public bool HasPortraitFor(string profileName) { return IsCached(profileName); }
    public bool ShowExisting(string profileName) { return BindCached(profileName); }

    public void Bind(string profileName, Archetype archetype)
    {
        if (!Capture(profileName, archetype))
        {
            BindCached(profileName);
            return;
        }
        BindCached(profileName);
    }

    /// <summary>
    /// Captures the portrait directly from the embedded native SDCSPreviewWindow texture.
    /// This is the authoritative portrait path for Survivor Creator because it derives the
    /// thumbnail from the exact model renderer the player is looking at rather than building
    /// a second hand-authored portrait camera/rig.
    ///
    /// The native preview is full-body. We crop the upper ~42% of the source texture, centered,
    /// which produces a stable head/shoulders thumbnail while preserving the real native model.
    /// </summary>
    public static bool CaptureNativePreview(string profileName, XUiController creatorRoot, Archetype archetype)
    {
        if (string.IsNullOrEmpty(profileName) || creatorRoot == null) return false;

        Texture source = null;
        XUiController preview = creatorRoot.GetChildById("nativePlayerPreview");
        if (preview != null)
        {
            XUiController textureController = preview.GetChildById("playerPreview");
            XUiV_Texture textureView = textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
            if (textureView != null) source = textureView.Texture;
        }

        if (source == null || source.width <= 0 || source.height <= 0)
        {
            Log.Warning(LogPrefix + " native preview texture unavailable profile='" + profileName + "'.");
            return false;
        }

        long signature = archetype != null ? ComputeSignature(archetype) : StableHash64(profileName);
        Texture2D snapshot = null;
        RenderTexture temp = null;
        try
        {
            temp = RenderTexture.GetTemporary(PortraitSize, PortraitSize, 0, RenderTextureFormat.ARGB32);
            temp.filterMode = FilterMode.Bilinear;
            RenderTexture previous = RenderTexture.active;
            try
            {
                // Crop a square in source pixels from the upper portion of the native full-body
                // preview. Keeping the crop square before scaling prevents portrait distortion.
                float cropPixels = Mathf.Clamp(source.height * 0.42f, 96f, source.height);
                float xScale = Mathf.Clamp01(cropPixels / source.width);
                float yScale = Mathf.Clamp01(cropPixels / source.height);
                float xOffset = (1f - xScale) * 0.5f;
                float yOffset = Mathf.Clamp01(1f - yScale);

                Graphics.Blit(source, temp, new Vector2(xScale, yScale), new Vector2(xOffset, yOffset));
                RenderTexture.active = temp;

                snapshot = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false);
                snapshot.name = "RebirthPlayerProfileNativeFace_" + SafeName(profileName);
                snapshot.ReadPixels(new Rect(0f, 0f, PortraitSize, PortraitSize), 0, 0);
                snapshot.Apply();
                snapshot.filterMode = FilterMode.Trilinear;
                snapshot.anisoLevel = 2;
                snapshot.wrapMode = TextureWrapMode.Clamp;
            }
            finally
            {
                RenderTexture.active = previous;
            }

            if (!TryPublish(profileName, snapshot, signature))
            {
                UnityEngine.Object.Destroy(snapshot); snapshot = null; return false;
            }
            ReplaceMemory(profileName, snapshot, signature);
            snapshot = null;
            Log.Out(LogPrefix + " native preview captured profile='" + profileName +
                "' source=" + source.width + "x" + source.height + " cacheVersion=" + CacheVersion);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(LogPrefix + " native preview capture failed profile='" + profileName +
                "': " + ex.GetType().Name + ": " + ex.Message);
            if (snapshot != null) UnityEngine.Object.Destroy(snapshot);
            return false;
        }
        finally
        {
            if (temp != null) RenderTexture.ReleaseTemporary(temp);
        }
    }

    /// <summary>
    /// Creates or refreshes one shared cached portrait using the same SDCS head-framing
    /// recipe as the working REBIRTH party HUD portrait renderer. The resulting 256px PNG
    /// is persisted outside world saves because Player Profiles themselves are global.
    /// </summary>
    public static bool Capture(string profileName, Archetype archetype)
    {
        if (string.IsNullOrEmpty(profileName) || archetype == null) return false;

        long signature = ComputeSignature(archetype);
        CacheEntry existing;
        if (TryGetEntry(profileName, out existing) && existing.Signature == signature)
            return true;

        Texture2D snapshot = null;
        GameObject portraitRig = null;
        SDCSUtils.TransformCatalog boneCatalog = null;
        RenderTextureSystem renderSystem = null;
        RenderTexture normalizedPortrait = null;
        try
        {
            SDCSUtils.CreateVizUI(archetype, ref portraitRig, ref boneCatalog, (EntityPlayer)null, false);
            if ((UnityEngine.Object)portraitRig == (UnityEngine.Object)null)
                throw new InvalidOperationException("CreateVizUI returned no portrait rig.");

            Animator animator = portraitRig.GetComponentInChildren<Animator>();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null) animator.Update(0f);

            renderSystem = new RenderTextureSystem();
            GameObject target = new GameObject("RebirthPlayerProfilePortraitTarget_" + SafeName(profileName));
            renderSystem.Create("rebirthPlayerProfilePortrait_" + SafeName(profileName), target,
                Vector3.zero, Vector3.zero, new Vector2i(PortraitSize, PortraitSize), true);

            portraitRig.transform.SetParent(renderSystem.TargetGO.transform, false);
            portraitRig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            portraitRig.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            renderSystem.TargetGO.transform.localPosition = new Vector3(-0.015f, -0.78f, 2.14f);
            Utils.SetLayerRecursively(renderSystem.TargetGO, 11);

            Camera camera = renderSystem.CameraGO != null ? renderSystem.CameraGO.GetComponent<Camera>() : null;
            if ((UnityEngine.Object)camera == (UnityEngine.Object)null)
                throw new InvalidOperationException("Portrait camera was not created.");

            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.Euler(1.5f, 0f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 9f;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            Light light = renderSystem.LightGO != null ? renderSystem.LightGO.GetComponent<Light>() : null;
            if ((UnityEngine.Object)light != (UnityEngine.Object)null)
            {
                renderSystem.LightGO.transform.localPosition = new Vector3(0.35f, 0.45f, 0.5f);
                light.type = LightType.Point;
                light.range = 20f;
                light.intensity = 1.5f;
                light.color = new Color(1f, 0.92f, 0.84f, 1f);
            }

            camera.transform.RotateAround(portraitRig.transform.position, Vector3.up, 30f);
            if (renderSystem.LightGO != null)
                renderSystem.LightGO.transform.RotateAround(portraitRig.transform.position, Vector3.up, 30f);

            CharacterGazeController gaze = portraitRig.GetComponentInChildren<CharacterGazeController>();
            if ((UnityEngine.Object)gaze != (UnityEngine.Object)null) gaze.SnapNextUpdate();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null) animator.Update(0f);

            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                // Native AA doubles the render dimensions. Downsample to the cache's
                // fixed size before encoding, as the native-preview capture path does.
                normalizedPortrait = RenderTexture.GetTemporary(PortraitSize, PortraitSize, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(renderSystem.RenderTex, normalizedPortrait);
                RenderTexture.active = normalizedPortrait;
                snapshot = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false);
                snapshot.name = "RebirthPlayerProfileFace_" + SafeName(profileName);
                snapshot.ReadPixels(new Rect(0f, 0f, PortraitSize, PortraitSize), 0, 0);
                snapshot.Apply();
                snapshot.filterMode = FilterMode.Trilinear;
                snapshot.anisoLevel = 2;
                snapshot.wrapMode = TextureWrapMode.Clamp;
            }
            finally
            {
                RenderTexture.active = previous;
            }

            if (!TryPublish(profileName, snapshot, signature))
            {
                UnityEngine.Object.Destroy(snapshot); snapshot = null; return false;
            }
            ReplaceMemory(profileName, snapshot, signature);
            snapshot = null; // cache owns it now
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(LogPrefix + " build failed profile='" + profileName + "': " + ex.GetType().Name + ": " + ex.Message);
            if (snapshot != null) UnityEngine.Object.Destroy(snapshot);
            return false;
        }
        finally
        {
            if ((UnityEngine.Object)portraitRig != (UnityEngine.Object)null)
            {
                try { SDCSUtils.UnloadViz(portraitRig); }
                catch { UnityEngine.Object.Destroy(portraitRig); }
            }
            if (renderSystem != null) renderSystem.Cleanup();
            if (normalizedPortrait != null) RenderTexture.ReleaseTemporary(normalizedPortrait);
            if (boneCatalog != null) boneCatalog.Clear();
        }
    }

    public static bool IsCached(string profileName)
    {
        CacheEntry entry;
        return TryGetEntry(profileName, out entry);
    }

    public static int GetCachedCount()
    {
        int count = 0;
        foreach (KeyValuePair<string, CacheEntry> pair in Cache)
            if (pair.Value != null && pair.Value.Texture != null) count++;
        return count;
    }

    /// <summary>
    /// Removes a portrait from memory and disk. Use this when a native profile is edited or
    /// deleted. Normal window close only clears row bindings and deliberately keeps the cache.
    /// </summary>
    public static void Invalidate(string profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return;
        InvalidateMemory(profileName);
        try
        {
            string key=GetProfileDiskKey(profileName);
            string dir=GetCacheDirectory();
            string manifest=GetManifestPath(profileName);
            if(File.Exists(manifest))File.Delete(manifest);
            if(Directory.Exists(dir))
            {
                string[] files=Directory.GetFiles(dir,key+".*.png");
                for(int i=0;i<files.Length;i++)try{File.Delete(files[i]);}catch{}
            }
        }
        catch (Exception ex)
        {
            Log.Warning(LogPrefix + " persistent invalidate failed profile='" + profileName + "': " + ex.Message);
        }
    }

    public static void InvalidateAll()
    {
        foreach (KeyValuePair<string, CacheEntry> pair in Cache)
            if (pair.Value != null && pair.Value.Texture != null) UnityEngine.Object.Destroy(pair.Value.Texture);
        Cache.Clear();
        PersistentFailureStamp.Clear();
        try
        {
            string dir = GetCacheDirectory();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (Exception ex)
        {
            Log.Warning(LogPrefix + " persistent cache clear failed: " + ex.Message);
        }
    }

    public void Clear() { ClearView(); }

    public void ClearView()
    {
        XUiV_Texture view = GetView();
        if (view != null)
        {
            view.Texture = null;
            view.IsVisible = false;
        }
        boundProfileName = string.Empty;
    }

    private static bool TryGetEntry(string profileName, out CacheEntry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(profileName)) return false;
        if (Cache.TryGetValue(profileName, out entry) && entry != null && entry.Texture != null) return true;
        return TryLoadPersistent(profileName, out entry);
    }

    private static bool TryLoadPersistent(string profileName, out CacheEntry entry)
    {
        entry = null;
        Texture2D texture = null;
        string manifestPath=GetManifestPath(profileName);
        long currentStamp=GetFileStamp(manifestPath);
        long failedStamp;
        if(PersistentFailureStamp.TryGetValue(profileName,out failedStamp) && failedStamp==currentStamp)return false;
        try
        {
            if(!File.Exists(manifestPath)){PersistentFailureStamp[profileName]=currentStamp;return false;}
            string[] lines=File.ReadAllLines(manifestPath);
            int version=0,size=0; long signature=0L; string name64=string.Empty,fileName=string.Empty;
            for(int i=0;i<lines.Length;i++)
            {
                string line=lines[i]??string.Empty; int eq=line.IndexOf('='); if(eq<=0)continue;
                string key=line.Substring(0,eq), value=line.Substring(eq+1);
                if(key=="version")int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out version);
                else if(key=="size")int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out size);
                else if(key=="signature")long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out signature);
                else if(key=="name64")name64=value;
                else if(key=="file")fileName=value;
            }
            if(version!=CacheVersion||size!=PortraitSize||signature==0L||string.IsNullOrEmpty(fileName))
            {PersistentFailureStamp[profileName]=currentStamp;return false;}
            string decoded=Encoding.UTF8.GetString(Convert.FromBase64String(name64));
            if(!string.Equals(decoded,profileName,StringComparison.Ordinal))
            {PersistentFailureStamp[profileName]=currentStamp;return false;}
            if(Path.GetFileName(fileName)!=fileName||fileName.IndexOf("..",StringComparison.Ordinal)>=0)
            {PersistentFailureStamp[profileName]=currentStamp;return false;}
            string expectedPrefix=GetProfileDiskKey(profileName)+".";
            if(!fileName.StartsWith(expectedPrefix,StringComparison.OrdinalIgnoreCase)||!fileName.EndsWith(".png",StringComparison.OrdinalIgnoreCase))
            {PersistentFailureStamp[profileName]=currentStamp;return false;}
            string pngPath=Path.Combine(GetCacheDirectory(),fileName);
            if(!File.Exists(pngPath)){PersistentFailureStamp[profileName]=currentStamp;return false;}
            byte[] bytes=File.ReadAllBytes(pngPath); if(bytes==null||bytes.Length==0){PersistentFailureStamp[profileName]=currentStamp;return false;}
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(texture,bytes,false)||texture.width!=PortraitSize||texture.height!=PortraitSize||!HasVisiblePortrait(texture))
            {UnityEngine.Object.Destroy(texture);texture=null;PersistentFailureStamp[profileName]=currentStamp;return false;}
            texture.name="RebirthPlayerProfileFaceDisk_"+SafeName(profileName);
            texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=2;texture.wrapMode=TextureWrapMode.Clamp;
            entry=new CacheEntry{Texture=texture,Signature=signature};texture=null;Cache[profileName]=entry;PersistentFailureStamp.Remove(profileName);return true;
        }
        catch(Exception ex)
        {
            if(texture!=null)UnityEngine.Object.Destroy(texture);
            PersistentFailureStamp[profileName]=currentStamp;
            Log.Warning(LogPrefix+" persistent load failed profile='"+profileName+"': "+ex.Message);return false;
        }
    }

    private static bool TryPublish(string profileName, Texture2D texture, long signature)
    {
        if(texture==null||string.IsNullOrEmpty(profileName)||signature==0L)return false;
        // The native preview can exist before its rig has rendered. Never persist that
        // blank frame: callers can use the explicit rig capture instead, and old blank
        // cache entries are rejected on load above.
        if (!HasVisiblePortrait(texture)) return false;
        string stagedPng=string.Empty, stagedManifest=string.Empty, generationPath=string.Empty;
        try
        {
            string dir=GetCacheDirectory(); Directory.CreateDirectory(dir);
            byte[] bytes=texture.EncodeToPNG(); if(bytes==null||bytes.Length==0)return false;
            string key=GetProfileDiskKey(profileName);
            string generation=signature.ToString("X16",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N");
            string fileName=key+"."+generation+".png"; generationPath=Path.Combine(dir,fileName);
            stagedPng=generationPath+".tmp"; File.WriteAllBytes(stagedPng,bytes);

            // Verify the exact bytes before they can be named by an authoritative manifest.
            Texture2D verify=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                byte[] verifyBytes=File.ReadAllBytes(stagedPng);
                if(!ImageConversion.LoadImage(verify,verifyBytes,false)||verify.width!=PortraitSize||verify.height!=PortraitSize)
                    throw new InvalidDataException("staged portrait failed dimension/content validation");
            }
            finally { UnityEngine.Object.Destroy(verify); }
            File.Move(stagedPng,generationPath); stagedPng=string.Empty;

            string manifest=GetManifestPath(profileName); stagedManifest=manifest+".tmp";
            string name64=Convert.ToBase64String(Encoding.UTF8.GetBytes(profileName));
            File.WriteAllText(stagedManifest,"version="+CacheVersion.ToString(CultureInfo.InvariantCulture)+"\nname64="+name64+"\nsignature="+signature.ToString(CultureInfo.InvariantCulture)+"\nsize="+PortraitSize.ToString(CultureInfo.InvariantCulture)+"\nfile="+fileName+"\n",new UTF8Encoding(false));
            string[] check=File.ReadAllLines(stagedManifest); if(check.Length<5)throw new InvalidDataException("staged portrait manifest incomplete");
            string error; if(!RebirthDurableFileCommit.TryPublish(stagedManifest,manifest,out error))throw new IOException("manifest publication failed: "+error);
            stagedManifest=string.Empty;
            CleanupObsoleteGenerations(profileName,fileName);
            return true;
        }
        catch(Exception ex)
        {
            Log.Warning(LogPrefix+" persistent save failed profile='"+profileName+"': "+ex.Message);
            try{if(stagedPng.Length>0&&File.Exists(stagedPng))File.Delete(stagedPng);}catch{}
            try{if(stagedManifest.Length>0&&File.Exists(stagedManifest))File.Delete(stagedManifest);}catch{}
            // An unpublished generation is non-authoritative and safe to remove.
            try{if(generationPath.Length>0&&File.Exists(generationPath))File.Delete(generationPath);}catch{}
            return false;
        }
    }

    private static bool HasVisiblePortrait(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int visible = 0;
        for (int i = 0; i < pixels.Length; i += 16)
        {
            Color32 pixel = pixels[i];
            if (pixel.a > 32 && Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) > 24 && ++visible >= 32)
                return true;
        }
        return false;
    }

    private static void CleanupObsoleteGenerations(string profileName,string currentFileName)
    {
        try
        {
            string dir=GetCacheDirectory(), key=GetProfileDiskKey(profileName);
            string[] files=Directory.GetFiles(dir,key+".*.png");
            for(int i=0;i<files.Length;i++)if(!string.Equals(Path.GetFileName(files[i]),currentFileName,StringComparison.OrdinalIgnoreCase))try{File.Delete(files[i]);}catch{}
        }
        catch{}
    }

    private static void ReplaceMemory(string profileName,Texture2D texture,long signature)
    {
        CacheEntry previous;
        Cache.TryGetValue(profileName,out previous);
        Cache[profileName]=new CacheEntry{Texture=texture,Signature=signature};
        PersistentFailureStamp.Remove(profileName);
        if(previous!=null&&previous.Texture!=null&&!ReferenceEquals(previous.Texture,texture))UnityEngine.Object.Destroy(previous.Texture);
    }

    private static void InvalidateMemory(string profileName)
    {
        CacheEntry entry;
        if(Cache.TryGetValue(profileName,out entry))
        { Cache.Remove(profileName); if(entry!=null&&entry.Texture!=null)UnityEngine.Object.Destroy(entry.Texture); }
        PersistentFailureStamp.Remove(profileName);
    }

    private XUiV_Texture GetView()
    {
        return textureController != null ? textureController.ViewComponent as XUiV_Texture : null;
    }

    private void SetVisible(bool visible)
    {
        XUiV_Texture view = GetView();
        if (view != null) view.IsVisible = visible;
    }

    private static string GetCacheDirectory()
    {
        string root = GameIO.GetUserGameDataDir();
        return Path.Combine(root, "Rebirth", "PlayerProfilePortraitCache", "v" + CacheVersion.ToString(CultureInfo.InvariantCulture));
    }

    private static long GetFileStamp(string path)
    {
        try{FileInfo info=new FileInfo(path);return info.Exists?(info.Length^info.LastWriteTimeUtc.Ticks):long.MinValue;}catch{return long.MinValue;}
    }

    private static string GetManifestPath(string profileName)
    { return Path.Combine(GetCacheDirectory(),GetProfileDiskKey(profileName)+ManifestExtension); }

    private static string GetProfileDiskKey(string profileName)
    { return SafeName(profileName)+"_"+StableHash64(profileName).ToString("X16",CultureInfo.InvariantCulture); }

    private static long ComputeSignature(Archetype archetype)
    { return RebirthNativePlayerProfileBridge.ComputeAppearanceSignature(archetype); }

    private static long StableHash64(string value)
    {
        unchecked
        {
            ulong hash=1469598103934665603UL; string text=value??string.Empty;
            for(int i=0;i<text.Length;i++){hash^=text[i];hash*=1099511628211UL;}
            long result=(long)(hash&0x7fffffffffffffffUL); return result==0L?1L:result;
        }
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "profile";
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
        return new string(chars);
    }
}
