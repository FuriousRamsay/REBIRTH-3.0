using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

// Presentation only. Native ItemValue cosmetic modifications remain the save and
// network selector. This bundle contains shaders/art only, never a vehicle prefab.
internal static class RebirthCamperWrapAppearance
{
    private sealed class Binding
    {
        internal Material[] Original, Applied;
        internal readonly List<Material> Observed=new List<Material>(2);
        internal string Id;
        internal int Generation;
    }
    private sealed class MaterialOwner { internal string Id; internal int Generation; }
    private static readonly ConditionalWeakTable<Material,MaterialOwner> OwnedInstances=new ConditionalWeakTable<Material,MaterialOwner>();
    private sealed class ArtEntry { internal Texture2D Texture; internal Texture2D[] Layers; internal int Users; }
    private static readonly string[] LayerNames={"hoodmaster","scenelayers","botanicallayers","gardencorner","musiccorner","leftbandsharp","rightbandsharp","rearsharp","frontsharp","reardoor","rearleft","rearright","fightpanda","fightrex","teapanda","tearex"};
    private static readonly string[] LayerProperties={"_ConceptHoodAtlas","_ConceptSceneAtlas","_ConceptBotanicalAtlas","_ConceptGardenCorner","_ConceptMusicCorner","_WrapLeftBandSharp","_WrapRightBandSharp","_WrapRearSharp","_WrapFrontSharp","_WrapRearDoorArt","_WrapRearLeftArt","_WrapRearRightArt","_WrapFightPanda","_WrapFightRex","_WrapTeaPanda","_WrapTeaRex"};
    private static readonly ConditionalWeakTable<Renderer, Binding> Bindings = new ConditionalWeakTable<Renderer, Binding>();
    private static readonly Dictionary<string, ArtEntry> Artwork = new Dictionary<string, ArtEntry>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Shader> Shaders = new Dictionary<string, Shader>(StringComparer.Ordinal);
    private static AssetBundle bundle;
    private static readonly Dictionary<string,AssetBundle> DedicatedBundles=new Dictionary<string,AssetBundle>(StringComparer.Ordinal);
    private static readonly HashSet<string> DedicatedAttempts=new HashSet<string>(StringComparer.Ordinal);
    // Only promote an original catalogue ID here after its complete twelve-panel
    // set is staged for native review. Deploy this list with reviewed bundles;
    // all unconverted IDs retain the original collection route.
    private static readonly HashSet<string> NativeBodyIds=new HashSet<string>(StringComparer.Ordinal)
    {
        "MissBanjo",
        "FuriousRamsay",
        "HeyWeps",
        "DeadZoneRunner",
        "JensClinic",
        "RektsLastNerve",
        "HughsHeavySupply",
        "BobsSalvageRoute",
        "JoelsFrontierFaith",
        "PoppysOddities",
        "RektsHarvestHauler",
        "HughsWinterOutpost",
        "BobsTowService",
        "JoelsDesertMission",
        "JensNightShift",
        "RektsNoRefunds",
        "HughsBrassStandard",
        "JoelsTradingCaravan",
        "BobsCopperheadGarage",
        "ShamwaySupperRun",
        "PopNPillsHouseCall",
        "MoPowerServiceVan",
        "PassNGasRoadCrew",
        "WorkingStiffsToolRun",
        "DishongExecutiveFleet",
        "BoarandCorn",
        "CrackABookBookmobile",
        "ZoesSalonCruiser",
        "RedMesaDispatch",
        "SavageCountryOutfitter",
        "NavezganeGeneralHospital",
        "FatHawaiianFeast",
        "ProwlinPetesRescue",
        "TheLastSunsetMotel",
        "NavezganePostalRoute",
        "PurpleLlama",
        "GenmaSaotome", "CaptStress", "MidnightCartographer", "TheHungryRoad", "BloodMoonExpress", "NavezganeExplorer"
    };
    internal static bool UsesNativeBodyLayout(string id)=>id!=null&&NativeBodyIds.Contains(id);
    internal static int NativeBodyLayoutVersion(string id)=>!UsesNativeBodyLayout(id)?0:
        id=="GenmaSaotome"?1:2;
    internal static bool IsLayeredTexture(string stem)
    {
        if(stem.StartsWith("UndeadChill",StringComparison.Ordinal))return true;
        foreach(string id in NativeBodyIds)if(stem.StartsWith(id,StringComparison.Ordinal))return true;
        return false;
    }
    internal static bool IsLayered(string id)=>id=="UndeadChill"||id=="UndeadChillAfterHours"||UsesNativeBodyLayout(id);
    internal static string DedicatedBundleName(string id)=>id=="MidnightCartographer"?"rebirth_midnight_cartographer.unity3d":id=="CaptStress"?"rebirth_captstress.unity3d":id=="GenmaSaotome"?"rebirth_genma_saotome.unity3d":id=="UndeadChill"?"rebirth_undead_chill.unity3d":id=="UndeadChillAfterHours"?"rebirth_undead_chill_after_hours.unity3d":"rebirth_wrap_"+id.ToLowerInvariant()+".unity3d";
    private static bool attempted;
    private static bool registered;
    private static int generation;
    internal static int CachedAtlasCount=>Artwork.Count;
    private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

    private static void Report(string key, string message)
    {
        if (Reported.Add(key)) Log.Warning("[REBIRTH Camper Wraps] " + message);
    }

    private static bool EnsureBundle()
    {
        if (bundle != null) return true;
        if (attempted || GameManager.IsDedicatedServer) return false;
        attempted = true;
        if (!registered) { ModEvents.WorldShuttingDown.RegisterHandler(Shutdown); registered = true; }
        try
        {
            string root = ModManager.GetMod("zzz_REBIRTH__3_0")?.Path;
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Mod path unavailable.");
            bundle = AssetBundle.LoadFromFile(Path.Combine(root, "Resources", "rebirth_camper_wraps.unity3d"));
            if (bundle == null) throw new InvalidOperationException("Collection asset bundle unavailable.");
            return true;
        }
        catch (Exception e) { Report("bundle", e.Message); return false; }
    }

    private static Texture2D GetArtwork(string id)
    {
        ArtEntry entry;
        if (Artwork.TryGetValue(id, out entry)) return entry.Texture;
        if (!IsLayered(id)&&!EnsureBundle()) return null;
        var selectedBundle=BundleFor(id);
        if(selectedBundle==null)return null;
        Texture2D value = selectedBundle.LoadAsset<Texture2D>("assets/rebirthcamperwrapcollection/artwork/" + id.ToLowerInvariant() + ".png");
        if (value == null) Report(id, "Missing artwork for " + id + ".");
        entry=new ArtEntry { Texture = value };
        // Optional on older bundles; the layered Undead Chill candidate includes
        // a complete set. Load once per cached design, never in the stable path.
        if(value!=null&&IsLayered(id)
            &&selectedBundle.Contains("assets/rebirthcamperwrapcollection/artwork/"+id.ToLowerInvariant()+LayerNames[0]+".png"))
        {
            int layerCount=UsesNativeBodyLayout(id)?12:LayerNames.Length;
            entry.Layers=new Texture2D[layerCount];
            for(int i=0;i<layerCount;i++)
            {
                if(!UsesNativeBodyLayout(id)&&i>=5&&!selectedBundle.Contains("assets/rebirthcamperwrapcollection/artwork/"+id.ToLowerInvariant()+LayerNames[i]+".png"))continue;
                entry.Layers[i]=selectedBundle.LoadAsset<Texture2D>("assets/rebirthcamperwrapcollection/artwork/"+id.ToLowerInvariant()+LayerNames[i]+".png");
                if(entry.Layers[i]!=null)continue;
                Report(id+"-layers","Incomplete layered artwork for "+id+"; retaining native appearance.");
                foreach(var layer in entry.Layers)if(layer!=null)UnityEngine.Resources.UnloadAsset(layer);
                UnityEngine.Resources.UnloadAsset(value);return null;
            }
        }
        Artwork[id] = entry;
        return value;
    }

    private static AssetBundle BundleFor(string id)
    {
        if(!IsLayered(id))return bundle;
        AssetBundle result;
        if(DedicatedBundles.TryGetValue(id,out result))return result;
        if(!DedicatedAttempts.Add(id)||GameManager.IsDedicatedServer)return null;
        try{
            if(!registered){ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);registered=true;}
            string root=ModManager.GetMod("zzz_REBIRTH__3_0")?.Path;
            result=AssetBundle.LoadFromFile(Path.Combine(root,"Resources",DedicatedBundleName(id)));
            if(result)DedicatedBundles[id]=result;
            else Report(id+"-bundle","Layered artwork bundle unavailable for "+id);
        }catch(Exception e){Report(id+"-bundle",e.Message);result=null;}
        return result;
    }

    private static Shader GetShader(Material original,string id)
    {
        if (original == null || original.shader == null) return null;
        string name = original.shader.name;
        string file;
        switch (name)
        {
            case "REBIRTH/CamperReferenceWrap": file = "rebirthcamperreferencewrapcollection"; break;
            case "REBIRTH/CamperTallReferenceWrap": file = "rebirthcampertallreferencewrapcollection"; break;
            case "REBIRTH/PickupOriginalFinish": file = "rebirthpickuporiginalfinishcollection"; break;
            case "REBIRTH/CamperAlignedStripe": file = "rebirthcamperalignedstripecollection"; break;
            default: Report(name, "Unqualified paint shader " + name + "; retaining original material."); return null;
        }
        Shader shader;
        string cacheKey=IsLayered(id)?id+"/"+file:file;
        if (Shaders.TryGetValue(cacheKey, out shader)) return shader;
        var selectedBundle=BundleFor(id);
        shader = selectedBundle==null?null:selectedBundle.LoadAsset<Shader>("assets/rebirthcamperwrapcollection/shaders/" + file + ".shader");
        if (shader == null || !shader.isSupported) { Report(file, "Collection shader unavailable: " + file); shader = null; }
        Shaders[cacheKey] = shader;
        return shader;
    }

    private static void SetRearDoorMapping(Material target,Renderer renderer,Material source)
    {
        var filter=renderer.GetComponent<MeshFilter>();
        if(filter==null||filter.sharedMesh==null){target.SetFloat("_WrapRearDoorPanel",0);return;}
        var native=Matrix4x4.identity;
        native.SetRow(0,source.GetVector("_VehicleRow0"));native.SetRow(1,source.GetVector("_VehicleRow1"));native.SetRow(2,source.GetVector("_VehicleRow2"));
        var local=filter.sharedMesh.bounds;Bounds mapped=new Bounds(native.MultiplyPoint3x4(local.min),Vector3.zero);
        for(int i=0;i<8;i++)mapped.Encapsulate(native.MultiplyPoint3x4(new Vector3((i&1)==0?local.min.x:local.max.x,(i&2)==0?local.min.y:local.max.y,(i&4)==0?local.min.z:local.max.z)));
        if(mapped.size.x<.001f||mapped.size.y<.001f){target.SetFloat("_WrapRearDoorPanel",0);return;}
        // Canonical closed native door extents, measured on the installed prefab.
        // Mapping is attached to local vertices, so opening the door does not swim the print.
        float sx=1.06f/mapped.size.x,sy=2.06f/mapped.size.y;
        Vector4 x=native.GetRow(0)*sx,y=native.GetRow(1)*sy;
        x.w+=-.53f-mapped.min.x*sx;y.w+=.70f-mapped.min.y*sy;
        target.SetVector("_WrapDoorRow0",x);target.SetVector("_WrapDoorRow1",y);target.SetVector("_WrapDoorRow2",new Vector4(0,0,0,-3.31f));
    }

    // Called in the existing bounded renderer path; no file or asset work repeats
    // for stable selection. Original material references are restored on removal.
    internal static bool Apply(Renderer renderer, string id)
    {
        if (renderer == null) return false;
        Binding binding;
        Bindings.TryGetValue(renderer, out binding);
        bool selected = RebirthCamperWrapCatalog.Contains(id);
        if (!selected)
        {
            if (binding != null) Restore(renderer, binding);
            return false;
        }
        if (binding != null && binding.Generation==generation && string.Equals(binding.Id, id, StringComparison.Ordinal))
        {
            // Native VehiclePart.SetColors obtains renderer.material, instantiating
            // a copy of our shared clone, and can assign it to LOD renderers.
            // An unchanged selector is stable only while our slot identities survive.
            var current=binding.Observed;current.Clear();renderer.GetSharedMaterials(current);
            bool intact=current.Count==binding.Applied.Length;
            for(int i=0;intact&&i<current.Count;i++)
                if(!ReferenceEquals(binding.Original[i],binding.Applied[i])&&!ReferenceEquals(current[i],binding.Applied[i]))intact=false;
            current.Clear();
            if(intact)return true;
        }
        Texture2D atlas = GetArtwork(id);
        if (atlas == null) { if (binding != null) Restore(renderer, binding); return false; }
        if (binding != null) Restore(renderer, binding);
        Material[] original = renderer.sharedMaterials;
        var applied = (Material[])original.Clone();
        bool changed = false;
        for (int i = 0; i < original.Length; i++)
        {
            if (original[i] == null || !original[i].HasProperty("_PatternEnabled")) continue;
            Shader shader = GetShader(original[i],id);
            if (shader == null) continue;
            var material = new Material(original[i]) { name = original[i].name + "_Wrap_" + id, shader = shader };
            material.SetTexture("_WrapAtlas", atlas);
            var layers=Artwork[id].Layers;
            if(layers!=null)
            {
                for(int layer=0;layer<layers.Length;layer++)
                    if(material.HasProperty(LayerProperties[layer]))material.SetTexture(LayerProperties[layer],layers[layer]);
                if(material.HasProperty("_ConceptHoodEnabled"))material.SetFloat("_ConceptHoodEnabled",1);
                if(material.HasProperty("_ConceptSceneEnabled"))material.SetFloat("_ConceptSceneEnabled",1);
                if(material.HasProperty("_WrapSharpPanels"))material.SetFloat("_WrapSharpPanels",layers.Length>=9&&layers[5]&&layers[6]&&layers[7]&&layers[8]?1:0);
            }
            bool frontPanel=string.Equals(id,"FuriousRamsaySignature",StringComparison.Ordinal)
                ||IsLayered(id);
            material.SetFloat("_WrapFrontPanel",frontPanel?1f:0f);
            bool fullCoverage=IsLayered(id);
            if(material.HasProperty("_WrapLayoutVariant"))material.SetFloat("_WrapLayoutVariant",NativeBodyLayoutVersion(id));
            if(material.HasProperty("_GenmaNativeBody12V2"))material.SetFloat("_GenmaNativeBody12V2",UsesNativeBodyLayout(id)?1:0);
            if(material.HasProperty("_WrapBaseCoat"))material.SetVector("_WrapBaseCoat",id=="UndeadChillAfterHours"?new Vector4(.008f,.033f,.036f,1):new Vector4(.012f,.014f,.014f,1));
            material.SetFloat("_WrapFullCoverage",fullCoverage?1f:0f);
            bool rearDoor=fullCoverage&&string.Equals(renderer.name,"RearDoor_camper_body",StringComparison.Ordinal);
            material.SetFloat("_WrapRearDoorPanel",rearDoor?1f:0f);
            if(rearDoor)SetRearDoorMapping(material,renderer,original[i]);
            material.SetFloat("_WrapMetallic", .08f);
            material.SetFloat("_WrapSmoothness", .45f);
            OwnedInstances.Add(material,new MaterialOwner{Id=id,Generation=generation});
            applied[i] = material;
            changed = true;
        }
        if (!changed) return false;
        binding = new Binding { Original = original, Applied = applied, Id = id, Generation=generation };
        Bindings.Add(renderer, binding);
        var lifetime = renderer.GetComponent<RebirthCamperWrapMaterialLifetime>();
        if (lifetime == null) lifetime = renderer.gameObject.AddComponent<RebirthCamperWrapMaterialLifetime>();
        Artwork[id].Users++;
        lifetime.Own(original, applied, id, generation);
        renderer.sharedMaterials = applied;
        return true;
    }

    private static bool IsOwnedSlot(Material current,Binding binding,int slot)
    {
        if(ReferenceEquals(current,binding.Applied[slot]))return true;
        // Destroyed aliases qualify only with a weak ownership record. A
        // destroyed foreign wrapper and a literal null remain external.
        if(current==null)
        {
            MaterialOwner owner;
            return !ReferenceEquals(current,null)&&OwnedInstances.TryGetValue(current,out owner)
                &&owner.Generation==binding.Generation&&string.Equals(owner.Id,binding.Id,StringComparison.Ordinal);
        }
        var original=binding.Original[slot];
        if(original==null||original.shader==null||current.shader==null)return false;
        if(!string.Equals(current.shader.name,original.shader.name+"Collection",StringComparison.Ordinal))return false;
        string expected=original.name+"_Wrap_"+binding.Id;
        return string.Equals(current.name,expected,StringComparison.Ordinal)
            ||current.name.StartsWith(expected+" (Instance)",StringComparison.Ordinal);
    }

    private static void Restore(Renderer renderer, Binding binding)
    {
        // Restore our clones and native-instantiated copies, while preserving
        // unrelated materials installed by another appearance feature.
        var current = renderer.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < current.Length && i < binding.Applied.Length; i++)
            if (!ReferenceEquals(binding.Original[i], binding.Applied[i]) && IsOwnedSlot(current[i],binding,i))
            {
                // Native tint was written to the instantiated material, not to
                // the saved original. Carry it back before cosmetic removal.
                if(current[i]!=null&&binding.Original[i]!=null&&current[i].HasProperty("_Color")&&binding.Original[i].HasProperty("_Color"))
                {
                    Color tint=current[i].GetColor("_Color"),saved=binding.Original[i].GetColor("_Color");
                    if(Mathf.Max(Mathf.Abs(tint.r-saved.r),Mathf.Abs(tint.g-saved.g),Mathf.Abs(tint.b-saved.b),Mathf.Abs(tint.a-saved.a))>.002f)
                    {
                        var owner=renderer.GetComponent<RebirthCamperWrapMaterialLifetime>();
                        if(owner==null)owner=renderer.gameObject.AddComponent<RebirthCamperWrapMaterialLifetime>();
                        // Never mutate a saved original that may be a shared asset.
                        if(!owner.OwnsTintBase(binding.Original[i]))
                        {
                            binding.Original[i]=new Material(binding.Original[i]){name=binding.Original[i].name};
                            owner.OwnTintBase(binding.Original[i]);
                        }
                        binding.Original[i].SetColor("_Color",tint);
                    }
                }
                current[i] = binding.Original[i]; changed = true;
            }
        if (changed) renderer.sharedMaterials = current;
        for (int i = 0; i < binding.Applied.Length; i++)
            if (!ReferenceEquals(binding.Original[i], binding.Applied[i])) DestroyOwned(binding.Applied[i]);
        var lifetime=renderer.GetComponent<RebirthCamperWrapMaterialLifetime>();
        if(lifetime!=null)lifetime.Forget();
        ReleaseArtwork(binding.Id,binding.Generation);
        Bindings.Remove(renderer);
    }

    internal static void ReleaseArtwork(string id,int ownerGeneration)
    {
        if(ownerGeneration!=generation)return;
        ArtEntry entry;
        if(id==null||!Artwork.TryGetValue(id,out entry))return;
        entry.Users--;
        if(entry.Users>0)return;
        Artwork.Remove(id);
        if(entry.Texture!=null)UnityEngine.Resources.UnloadAsset(entry.Texture);
        if(entry.Layers!=null)foreach(var layer in entry.Layers)if(layer!=null)UnityEngine.Resources.UnloadAsset(layer);
    }

    internal static void DestroyOwned(Material material)
    {
        if(material==null)return;
#if UNITY_EDITOR
        // Offline mesh review links this exact source in an editor assembly.
        if(!Application.isPlaying){UnityEngine.Object.DestroyImmediate(material);return;}
#endif
        UnityEngine.Object.Destroy(material);
    }

    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {
        Artwork.Clear(); Shaders.Clear(); Reported.Clear();
        if (bundle != null) bundle.Unload(true);
        foreach(var dedicated in DedicatedBundles.Values)if(dedicated)dedicated.Unload(true);
        DedicatedBundles.Clear();DedicatedAttempts.Clear();
        bundle = null; attempted = false; generation++;
    }
}

// Shared-material clones otherwise outlive destroyed renderers in Unity. A local
// lifetime owner releases only our clones when the model is unloaded or replaced.
internal sealed class RebirthCamperWrapMaterialLifetime : MonoBehaviour
{
    private Material[] original, applied;
    private readonly List<Material> tintBases=new List<Material>();
    internal bool OwnsTintBase(Material material)=>tintBases.Contains(material);
    internal void OwnTintBase(Material material){tintBases.Add(material);}
    private string id;
    private int generation;
    internal void Own(Material[] source, Material[] copies, string wrap,int session) { original = source; applied = copies; id=wrap;generation=session; }
    internal void Forget(){original=null;applied=null;id=null;}
    private void OnDestroy()
    {
        if (applied != null)
        {
            for (int i = 0; i < applied.Length; i++)
                if (!ReferenceEquals(original[i], applied[i]) && applied[i] != null) RebirthCamperWrapAppearance.DestroyOwned(applied[i]);
            RebirthCamperWrapAppearance.ReleaseArtwork(id,generation);
        }
        foreach(var material in tintBases)RebirthCamperWrapAppearance.DestroyOwned(material);
        tintBases.Clear();Forget();
    }
}
