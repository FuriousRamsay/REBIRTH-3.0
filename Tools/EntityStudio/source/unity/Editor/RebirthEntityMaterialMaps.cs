using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public sealed class RebirthEntityGeneratedMaps
    {
        public Texture2D normalRmoe,feralRmoe,specialRmoe,feralAlbedo,eyeEmission,specialEmission,previewSmoothness;
        public string[] packedAssetPaths;
    }

    public static class RebirthEntityMaterialMaps
    {
        public static EntityMaterialTextureOverride FindOverride(RebirthEntityRigProfile profile,Material source)
        {
            if(source==null||profile.materialTextureOverrides==null)return null;
            var matches=profile.materialTextureOverrides.Where(value=>value!=null&&value.sourceMaterial==source).ToArray();
            if(matches.Length>1)throw new InvalidDataException("Duplicate texture override for "+source.name);
            return matches.SingleOrDefault();
        }
        public static RebirthEntityGeneratedMaps Generate(RebirthEntityRigWorkspace workspace,bool mapsOnly=false)
        {
            var profile=workspace.profile;
            if(profile.characterShader==null||profile.characterShader.name!="Standard")throw new InvalidOperationException("This native-map authoring pass uses Standard for the editor preview; the game adapter supplies Game/Character.");
            Color32[] albedo=ReadPixels(profile.bakedAlbedo,out int width,out int height);
            Color32[] rough=ReadOptional("roughness"),metal=ReadOptional("metallic");
            float[] eyeMask=EyeMask(workspace,width,height);int glowingPixels=0;
            var normalPixels=new Color32[albedo.Length];var feralPixels=new Color32[albedo.Length];var specialPixels=new Color32[albedo.Length];
            var feralAlbedo=new Color32[albedo.Length];var eyePreview=new Color32[albedo.Length];var specialPreview=new Color32[albedo.Length];
            var previewSmoothness=new Color32[albedo.Length];
            for(int i=0;i<albedo.Length;i++)
            {
                Color32 color=albedo[i];byte r=rough!=null?rough[i].r:(byte)204,m=metal!=null?metal[i].r:(byte)0;
                byte eye=(byte)Mathf.RoundToInt(255f*eyeMask[i]);if(eye>0)glowingPixels++;
                float redness=Mathf.Clamp01((color.r-Mathf.Max(color.g,color.b)*1.08f)/255f*4f);
                byte special=(byte)Mathf.RoundToInt(255f*Mathf.Max(eyeMask[i],redness));
                normalPixels[i]=new Color32(r,m,255,0);feralPixels[i]=new Color32(r,m,255,0);specialPixels[i]=new Color32(r,m,255,special);
                previewSmoothness[i]=new Color32(0,0,0,(byte)(255-r));
                feralAlbedo[i]=new Color32((byte)(color.r*.94f),(byte)(color.g*.88f),(byte)(color.b*.85f),color.a);
                // Decoded linear nonblack means measured from installed Worker
                // feral_eye_e; owned mask/UVs remain specific to this model.
                eyePreview[i]=new Color32((byte)Mathf.RoundToInt(eye*.211286f),(byte)Mathf.RoundToInt(eye*.049165f),(byte)Mathf.RoundToInt(eye*.001234f),255);specialPreview[i]=new Color32(special,special,special,255);
            }
            if(glowingPixels==0)throw new InvalidDataException("The configured eye region did not reach any source UV pixels.");
            string folder="Assets/EntityStudioGenerated/"+profile.entityClass+"/Maps";Directory.CreateDirectory(Path.GetFullPath(folder));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var maps=new RebirthEntityGeneratedMaps
            {
                normalRmoe=Write("RMOE_Normal",normalPixels,EntityTextureUsage.LinearData),
                feralRmoe=Write("RMOE_Feral",feralPixels,EntityTextureUsage.LinearData),
                specialRmoe=Write("RMOE_Special",specialPixels,EntityTextureUsage.LinearData),
                feralAlbedo=Write("Albedo_Feral",feralAlbedo,EntityTextureUsage.Albedo),
                eyeEmission=Write("Emission_Eyes",eyePreview,EntityTextureUsage.Emission),
                specialEmission=Write("Emission_Special",specialPreview,EntityTextureUsage.Emission),
                previewSmoothness=Write("Preview_Nonmetal_Smoothness",previewSmoothness,EntityTextureUsage.LinearData)
            };
            maps.packedAssetPaths=new[]{AssetDatabase.GetAssetPath(maps.normalRmoe),AssetDatabase.GetAssetPath(maps.feralRmoe),AssetDatabase.GetAssetPath(maps.specialRmoe)};
            if(mapsOnly)
            {
                if(profile.materialTextureOverrides!=null)foreach(var textureOverride in profile.materialTextureOverrides)
                    maps.packedAssetPaths=maps.packedAssetPaths.Concat(GenerateSlotMaps(profile,textureOverride).packedAssetPaths).Distinct().ToArray();
                Debug.Log("REBIRTH MATERIAL MAPS: refreshed owned maps; existing material and eye bindings retained.");
                return maps;
            }
            RebirthEntityRigBuilder.BindWorkspaceReferences(workspace);profile.rmoeMap=null;profile.nativeRmoeMaps=new List<Texture2D>{maps.normalRmoe,maps.feralRmoe,maps.specialRmoe};profile.feralTint=Color.white;
            RebirthEntityRigBuilder.RebuildTierMaterials(workspace);
            var slotMaps=new Dictionary<EntityMaterialTextureOverride,RebirthEntityGeneratedMaps>();
            foreach(var set in workspace.rendererTierMaterials)
                for(int tier=0;tier<5;tier++)
                    for(int slot=0;slot<set.slotCount;slot++)
                    {
                        var textureOverride=FindOverride(profile,slot<set.sourceMaterials.Count?set.sourceMaterials[slot]:null);
                        var activeMaps=maps;
                        if(textureOverride!=null&&!slotMaps.TryGetValue(textureOverride,out activeMaps))
                        {
                            activeMaps=GenerateSlotMaps(profile,textureOverride);slotMaps.Add(textureOverride,activeMaps);
                            profile.nativeRmoeMaps.AddRange(new[]{activeMaps.normalRmoe,activeMaps.feralRmoe,activeMaps.specialRmoe});
                        }
                        Material material=set.variants[tier*set.slotCount+slot];material.SetColor("_Color",Color.white);
                        material.SetFloat("_Metallic",0f);material.SetFloat("_Glossiness",.18f);
                        material.SetTexture("_MetallicGlossMap",activeMaps.previewSmoothness);material.SetFloat("_GlossMapScale",1f);material.EnableKeyword("_METALLICGLOSSMAP");
                        if(tier==1)material.SetTexture("_MainTex",activeMaps.feralAlbedo);
                        if(tier>1)
                        {
                            material.SetTexture("_EmissionMap",activeMaps.specialEmission);
                            Color color=tier==1?new Color(1f,.55f,.08f,1f):tier==2?profile.radiatedTint:tier==3?profile.chargedTint:profile.infernalTint;
                            material.SetColor("_EmissionColor",new Color(color.r,color.g,color.b,1f)*(1f+color.a));material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;material.EnableKeyword("_EMISSION");
                        }
                        EditorUtility.SetDirty(material);
                    }
            RebirthEntityRigBuilder.ApplyTierMaterials(workspace,ZombieTier.Normal);
            maps.packedAssetPaths=profile.nativeRmoeMaps.Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
            workspace.profileBuildFingerprint=RebirthEntityRigBuilder.ComputeProfileFingerprint(profile,workspace.sourceModelKey);
            EditorUtility.SetDirty(profile);EditorUtility.SetDirty(workspace);AssetDatabase.SaveAssets();
            Debug.Log("REBIRTH MATERIAL MAPS: dimensions="+width+"x"+height+" eyeMaskPixels="+glowingPixels+" packed=R:roughness,G:metallic,B:occlusion,A:emission (native appearance requires game verification)");
            return maps;

            Color32[] ReadOptional(string hint)
            {
                Texture2D prepared=hint=="roughness"?profile.sourceRoughness:profile.sourceMetallic;
                if(prepared!=null)
                {Color32[] pixels=ReadPixels(prepared,out int preparedWidth,out int preparedHeight);if(preparedWidth!=width||preparedHeight!=height)throw new InvalidDataException("Prepared PBR maps must share the albedo's dimensions.");return pixels;}
                foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{profile.sourceTexturePath}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    if(Path.GetFileNameWithoutExtension(path).EndsWith("_"+hint,StringComparison.OrdinalIgnoreCase))
                    {var asset=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(hint=="roughness")profile.sourceRoughness=asset;else profile.sourceMetallic=asset;Color32[] pixels=ReadPixels(asset,out int w,out int h);if(w!=width||h!=height)throw new InvalidDataException("PBR maps must share the albedo's dimensions.");return pixels;}
                }
                return null;
            }
            Texture2D Write(string suffix,Color32[] pixels,EntityTextureUsage usage)
            {
                string path=folder+"/"+profile.entityClass+"_"+suffix+".png";
                var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true);texture.SetPixels32(pixels);texture.Apply();
                byte[] encoded=texture.EncodeToPNG();UnityEngine.Object.DestroyImmediate(texture);
                string absolute=Path.GetFullPath(path);bool changed=true;
                if(File.Exists(absolute)){byte[] prior=File.ReadAllBytes(absolute);changed=prior.Length!=encoded.Length;if(!changed)for(int i=0;i<prior.Length;i++)if(prior[i]!=encoded[i]){changed=true;break;}}
                if(changed)File.WriteAllBytes(absolute,encoded);
                var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(changed||imported==null){AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);imported=AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
                RebirthEntityTextureTools.Configure(imported,usage,usage==EntityTextureUsage.Albedo?profile.albedoMaxSize:profile.dataMaxSize);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        public static RebirthEntityGeneratedMaps GenerateSlotMaps(RebirthEntityRigProfile profile,EntityMaterialTextureOverride source)
        {
            if(source==null||source.sourceMaterial==null||string.IsNullOrEmpty(source.mapName)||!System.Text.RegularExpressions.Regex.IsMatch(source.mapName,"^[A-Za-z0-9_]+$"))throw new InvalidDataException("A slot map needs a source material and safe unique map name.");
            if(profile.materialTextureOverrides==null||profile.materialTextureOverrides.Count(value=>value!=null&&value.mapName==source.mapName)!=1)throw new InvalidDataException("Duplicate or unregistered slot map name "+source.mapName);
            if(source.normalMap==null||source.roughness==null||source.metallic==null)throw new InvalidDataException("Slot PBR maps must be complete.");
            Color32[] albedo=ReadPixels(source.albedo,out int width,out int height),rough=ReadPixels(source.roughness,out int rw,out int rh),metal=ReadPixels(source.metallic,out int mw,out int mh);
            if(rw!=width||rh!=height||mw!=width||mh!=height||source.normalMap.width!=width||source.normalMap.height!=height)throw new InvalidDataException("Slot PBR map dimensions differ.");
            var normal=new Color32[albedo.Length];var special=new Color32[albedo.Length];var feral=new Color32[albedo.Length];var emission=new Color32[albedo.Length];var preview=new Color32[albedo.Length];
            for(int i=0;i<albedo.Length;i++)
            {
                var c=albedo[i];byte mask=(byte)Mathf.RoundToInt(255*Mathf.Clamp01((c.r-Mathf.Max(c.g,c.b)*1.08f)/255f*4));
                normal[i]=new Color32(rough[i].r,metal[i].r,255,0);special[i]=new Color32(rough[i].r,metal[i].r,255,mask);
                feral[i]=new Color32((byte)(c.r*.94f),(byte)(c.g*.88f),(byte)(c.b*.85f),c.a);emission[i]=new Color32(mask,mask,mask,255);preview[i]=new Color32(metal[i].r,0,0,(byte)(255-rough[i].r));
            }
            string folder="Assets/EntityStudioGenerated/"+profile.entityClass+"/Maps";Directory.CreateDirectory(Path.GetFullPath(folder));if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var result=new RebirthEntityGeneratedMaps{normalRmoe=Write("RMOE_Normal",normal,EntityTextureUsage.LinearData),feralRmoe=Write("RMOE_Feral",normal,EntityTextureUsage.LinearData),specialRmoe=Write("RMOE_Special",special,EntityTextureUsage.LinearData),feralAlbedo=Write("Albedo_Feral",feral,EntityTextureUsage.Albedo),specialEmission=Write("Emission_Special",emission,EntityTextureUsage.Emission),previewSmoothness=Write("Preview_Metal_Smoothness",preview,EntityTextureUsage.LinearData)};
            result.packedAssetPaths=new[]{AssetDatabase.GetAssetPath(result.normalRmoe),AssetDatabase.GetAssetPath(result.feralRmoe),AssetDatabase.GetAssetPath(result.specialRmoe)};
            return result;
            Texture2D Write(string suffix,Color32[] pixels,EntityTextureUsage usage)
            {
                string path=folder+"/"+profile.entityClass+"_"+source.mapName+"_"+suffix+".png";
                var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true);byte[] encoded;
                try{texture.SetPixels32(pixels);texture.Apply();encoded=texture.EncodeToPNG();}finally{UnityEngine.Object.DestroyImmediate(texture);}
                if(!File.Exists(path)||!File.ReadAllBytes(path).SequenceEqual(encoded))File.WriteAllBytes(path,encoded);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                RebirthEntityTextureTools.Configure(imported,usage,usage==EntityTextureUsage.Albedo?profile.albedoMaxSize:profile.dataMaxSize);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        private static Color32[] ReadPixels(Texture2D texture,out int width,out int height)
        {
            if(texture==null)throw new InvalidDataException("A baked albedo is required.");
            var copy=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {if(!copy.LoadImage(File.ReadAllBytes(Path.GetFullPath(AssetDatabase.GetAssetPath(texture)))))throw new InvalidDataException("Could not decode source texture.");width=copy.width;height=copy.height;return copy.GetPixels32();}
            finally{UnityEngine.Object.DestroyImmediate(copy);}
        }

        private static float[] EyeMask(RebirthEntityRigWorkspace workspace,int width,int height)
        {
            var mask=new float[width*height];float h=workspace.profile.estimatedHeight;
            float eyeY=workspace.transform.position.y+h*workspace.profile.eyeHeightRatio,eyeX=h*workspace.profile.eyeHalfSeparationRatio,rx=h*workspace.profile.eyeWidthRatio,ry=h*workspace.profile.eyeRadiusHeightRatio;
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                Mesh mesh=renderer.sharedMesh;Vector3[] points=mesh.vertices;Vector2[] uv=mesh.uv;int[] triangles=mesh.triangles;
                for(int i=0;i<points.Length;i++)points[i]=renderer.transform.TransformPoint(points[i])-workspace.transform.position;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];Vector3 a=points[ia],b=points[ib],c=points[ic];
                    float targetY=eyeY-workspace.transform.position.y;
                    if(Mathf.Max(a.y,Mathf.Max(b.y,c.y))<targetY-ry||Mathf.Min(a.y,Mathf.Min(b.y,c.y))>targetY+ry||Mathf.Max(a.z,Mathf.Max(b.z,c.z))<.02f)continue;
                    Vector2 au=uv[ia],bu=uv[ib],cu=uv[ic];float denominator=(bu.y-cu.y)*(au.x-cu.x)+(cu.x-bu.x)*(au.y-cu.y);if(Mathf.Abs(denominator)<1e-10f)continue;
                    int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(au.x,Mathf.Min(bu.x,cu.x))*width),0,width-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(au.x,Mathf.Max(bu.x,cu.x))*width),0,width-1);
                    int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(au.y,Mathf.Min(bu.y,cu.y))*height),0,height-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(au.y,Mathf.Max(bu.y,cu.y))*height),0,height-1);
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {
                        float u=(x+.5f)/width,v=(y+.5f)/height;
                        float wa=((bu.y-cu.y)*(u-cu.x)+(cu.x-bu.x)*(v-cu.y))/denominator,wb=((cu.y-au.y)*(u-cu.x)+(au.x-cu.x)*(v-cu.y))/denominator,wc=1f-wa-wb;
                        if(wa<0f||wb<0f||wc<0f)continue;Vector3 p=a*wa+b*wb+c*wc;if(p.z<.02f)continue;
                        float dx=(Mathf.Abs(p.x)-eyeX)/rx,dy=(p.y-targetY)/ry;float value=Mathf.Clamp01(1f-dx*dx-dy*dy);
                        mask[y*width+x]=Mathf.Max(mask[y*width+x],Mathf.SmoothStep(0f,1f,value));
                    }
                }
            }
            return mask;
        }
    }
}
