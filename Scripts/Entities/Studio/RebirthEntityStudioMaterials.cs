using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public static class RebirthEntityStudioMaterials
{
    public const string SourceProperty="RebirthEntityStudioMaterialSource";
    public const string HairSourceProperty="RebirthEntityStudioHairMaterialSource";
    public const string PackedMapProperty="RebirthEntityStudioRmoeMap";
    public const string TierProperty="RebirthEntityStudioTier";
    public const string SlotMaterialPrefix="RebirthEntityStudioMaterialSlot";
    public const string SlotPackedMapPrefix="RebirthEntityStudioRmoeMapSlot";
    private static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>(StringComparer.Ordinal);

    public static void Bind(Transform model,EntityClass entityClass)
    {
        string source=entityClass.Properties.GetString(SourceProperty);if(string.IsNullOrWhiteSpace(source))return;
        string packedPath=entityClass.Properties.GetString(PackedMapProperty);
        if(string.IsNullOrWhiteSpace(packedPath))throw new InvalidOperationException("A native character material requires an authored RMOE map.");
        GameObject prefab=DataLoader.LoadAsset<GameObject>(source);Material template=null;
        if(prefab!=null)
            foreach(var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach(var material in renderer.sharedMaterials)
                    if(template==null&&renderer.name!="NormalEye"&&renderer.name!="FeralEye"&&material!=null&&material.shader!=null&&material.shader.name=="Game/Character"){template=material;break;}
        if(template==null)throw new InvalidOperationException("Native Game/Character template not found: "+source);
        Texture packed=DataLoader.LoadAsset<Texture2D>(packedPath);
        if(packed==null)throw new InvalidOperationException("Authored RMOE map not found: "+packedPath);
        string tier=entityClass.Properties.GetString(TierProperty).ToLowerInvariant();
        var slotMaterials=new Dictionary<int,Material>();var slotMaps=new Dictionary<int,Texture>();
        foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            bool isEye=renderer.CompareTag("E_BP_Eye");
            if(isEye)
            {
                renderer.gameObject.SetActive(renderer.name=="FeralEye"?tier=="feral":tier!="feral");
                if(renderer.name=="FeralEye")
                {
                    Material eyeTemplate=null;
                    foreach(var reference in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if(reference.name=="FeralEye"&&reference.sharedMaterial!=null){eyeTemplate=reference.sharedMaterial;break;}
                    if(eyeTemplate==null||eyeTemplate.shader==null||eyeTemplate.shader.name!="Game/Autodesk")throw new InvalidOperationException("Native FeralEye Game/Autodesk template is missing.");
                    Material authoredEye=renderer.sharedMaterial;
                    Texture eyeAlbedo=TextureOf(authoredEye,"_Albedo","_MainTex"),eyeEmission=authoredEye.GetTexture("_EmissionMap");
                    if(eyeAlbedo==null||eyeEmission==null)throw new InvalidOperationException("Owned eye albedo/emission maps are missing.");
                    string eyeKey="eye|"+eyeTemplate.GetInstanceID()+"|"+authoredEye.GetInstanceID();
                    if(!Materials.TryGetValue(eyeKey,out Material eyeMaterial)||eyeMaterial==null)
                    {
                        eyeMaterial=new Material(eyeTemplate){name=authoredEye.name+"_GameAutodesk"};
                        eyeMaterial.SetTexture("_MainTex",eyeAlbedo);eyeMaterial.SetTexture("_EmissionMap",eyeEmission);
                        eyeMaterial.SetColor("_EmissionColor",authoredEye.GetColor("_EmissionColor"));eyeMaterial.EnableKeyword("_EMISSION");
                        CopyUv(authoredEye,eyeMaterial,"_MainTex","_MainTex");CopyUv(authoredEye,eyeMaterial,"_EmissionMap","_EmissionMap");
                        Materials[eyeKey]=eyeMaterial;
                    }
                    renderer.sharedMaterial=eyeMaterial;continue;
                }
            }
            Material[] slots=renderer.sharedMaterials;
            for(int slot=0;slot<slots.Length;slot++)
            {
                // Optional explicit authoring bindings survive native AltMats
                // replacement and keep independently unwrapped submeshes intact.
                Material authored=null;
                if(!isEye&&!slotMaterials.TryGetValue(slot,out authored))
                {
                    string reference=entityClass.Properties.GetString(SlotMaterialPrefix+slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if(!string.IsNullOrWhiteSpace(reference))
                    {authored=DataLoader.LoadAsset<Material>(reference);if(authored==null)throw new InvalidOperationException("Authored slot material not found: "+reference);slotMaterials.Add(slot,authored);}
                }
                if(authored==null)authored=slots[slot];if(authored==null)throw new InvalidOperationException("Authored material slot is missing.");
                Texture slotPacked=packed;
                // NormalEye can have an independent UV layout. Its packed map
                // must follow that layout rather than the body's atlas.
                if(isEye&&authored.HasProperty("_RMOE")&&authored.GetTexture("_RMOE")!=null)
                    slotPacked=authored.GetTexture("_RMOE");
                if(!isEye&&!slotMaps.TryGetValue(slot,out slotPacked))
                {
                    string reference=entityClass.Properties.GetString(SlotPackedMapPrefix+slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    slotPacked=string.IsNullOrWhiteSpace(reference)?packed:DataLoader.LoadAsset<Texture2D>(reference);
                    if(slotPacked==null)throw new InvalidOperationException("Authored slot RMOE map not found: "+reference);
                    slotMaps.Add(slot,slotPacked);
                }
                if(!isEye&&authored.name.StartsWith("EntityStudioHair_",StringComparison.Ordinal))
                {
                    string hairSource=entityClass.Properties.GetString(HairSourceProperty);
                    GameObject hairPrefab=string.IsNullOrWhiteSpace(hairSource)?prefab:DataLoader.LoadAsset<GameObject>(hairSource);
                    Material hairTemplate=null;
                    if(hairPrefab!=null)foreach(var nativeRenderer in hairPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        foreach(var nativeMaterial in nativeRenderer.sharedMaterials)
                            if(nativeMaterial!=null&&nativeMaterial.shader!=null&&nativeMaterial.shader.name=="Game/Autodesk"&&nativeMaterial.name.IndexOf("Hair",StringComparison.OrdinalIgnoreCase)>=0){hairTemplate=nativeMaterial;break;}
                    if(hairTemplate==null)throw new InvalidOperationException("Native Game/Autodesk hair template is missing.");
                    string hairKey="hair|"+hairTemplate.GetInstanceID()+"|"+authored.GetInstanceID();
                    if(!Materials.TryGetValue(hairKey,out Material hairMaterial)||hairMaterial==null)
                    {
                        hairMaterial=new Material(hairTemplate){name=authored.name+"_GameAutodesk"};
                        foreach(string property in new[]{"_MainTex","_BumpMap","_SpecGlossMap","_OcclusionMap"})
                        {
                            Texture map=authored.GetTexture(property);
                            if(map==null)throw new InvalidOperationException("Authored hair map missing: "+authored.name+" "+property);
                            hairMaterial.SetTexture(property,map);CopyUv(authored,hairMaterial,property,property);
                        }
                        foreach(string property in new[]{"_Cutoff","_BumpScale","_Glossiness","_GlossMapScale","_SpecularHighlights","_GlossyReflections"})
                            if(authored.HasProperty(property)&&hairMaterial.HasProperty(property))hairMaterial.SetFloat(property,authored.GetFloat(property));
                        Materials[hairKey]=hairMaterial;
                    }
                    slots[slot]=hairMaterial;continue;
                }
                Texture albedo=TextureOf(authored,"_Albedo","_MainTex"),normal=TextureOf(authored,"_Normal","_BumpMap");
                if(albedo==null||normal==null)throw new InvalidOperationException("Authored albedo/normal maps are missing on "+authored.name);
                string key=template.GetInstanceID()+"|"+authored.GetInstanceID()+"|"+albedo.GetInstanceID()+"|"+normal.GetInstanceID()+"|"+slotPacked.GetInstanceID()+"|"+tier;
                if(!Materials.TryGetValue(key,out Material material)||material==null)
                {
                    material=new Material(template){name=authored.name+"_GameCharacter"};
                    foreach(string property in new[]{"_Albedo","_Normal","_RMOE"})
                        if(!material.HasProperty(property))throw new InvalidOperationException("Native character shader lacks "+property);
                    material.SetTexture("_Albedo",albedo);material.SetTexture("_Normal",normal);material.SetTexture("_RMOE",slotPacked);
                    CopyUv(authored,material,authored.HasProperty("_Albedo")?"_Albedo":"_MainTex","_Albedo");
                    CopyUv(authored,material,authored.HasProperty("_Normal")?"_Normal":"_BumpMap","_Normal");
                    if(tier=="feral"||tier=="radiated"||tier=="charged"||tier=="infernal")
                    {
                        if(!material.HasProperty("_EmissiveColor"))throw new InvalidOperationException("Native character shader lacks _EmissiveColor.");
                        Color color=tier=="feral"?new Color(1f,.55f,.08f,1f):EntityClass.sColors[tier];
                        material.SetColor("_EmissiveColor",color);material.EnableKeyword("_EMISSION");
                        if(tier=="feral")material.DisableKeyword("_IRRADIATED_ON");else material.EnableKeyword("_IRRADIATED_ON");
                    }
                    Materials[key]=material;
                }
                slots[slot]=material;
            }
            renderer.sharedMaterials=slots;
        }
    }

    public static void Clear()
    {foreach(var material in Materials.Values)if(material!=null)UnityEngine.Object.Destroy(material);Materials.Clear();}
    private static Texture TextureOf(Material material,string native,string standard)
    {return material.HasProperty(native)?material.GetTexture(native):material.HasProperty(standard)?material.GetTexture(standard):null;}
    private static void CopyUv(Material source,Material target,string from,string to)
    {if(source.HasProperty(from)){target.SetTextureScale(to,source.GetTextureScale(from));target.SetTextureOffset(to,source.GetTextureOffset(from));}}
}
