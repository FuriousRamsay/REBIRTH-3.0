using System;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public enum EntityTextureUsage { Albedo, Normal, LinearData, Emission }

    public static class RebirthEntityTextureTools
    {
        public static void Configure(Texture2D texture,EntityTextureUsage usage,int maximumSize=0)
        {
            if(texture==null)throw new ArgumentNullException("texture");
            if(!Enum.IsDefined(typeof(EntityTextureUsage),usage))throw new ArgumentOutOfRangeException("usage");
            texture=ExtractEmbeddedTexture(texture);
            string path=AssetDatabase.GetAssetPath(texture);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Texture must be an imported project asset.");
            string before=EditorJsonUtility.ToJson(importer);
            bool normalMap=usage==EntityTextureUsage.Normal;
            importer.textureType=normalMap?TextureImporterType.NormalMap:TextureImporterType.Default;
            // Color-space interpretation comes from the assigned map role, never its filename.
            importer.sRGBTexture=usage==EntityTextureUsage.Albedo;
            importer.mipmapEnabled=true;
            importer.streamingMipmaps=true;
            importer.anisoLevel=8;
            importer.filterMode=FilterMode.Trilinear;
            importer.wrapMode=TextureWrapMode.Repeat;
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth,out int sourceHeight);
            int sourceLimit=Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(sourceWidth,sourceHeight)),256,8192);
            importer.maxTextureSize=maximumSize>0?Mathf.Min(sourceLimit,Mathf.Clamp(Mathf.NextPowerOfTwo(maximumSize),256,8192)):sourceLimit;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            if(usage==EntityTextureUsage.Emission)importer.alphaSource=TextureImporterAlphaSource.FromInput;
            if(before!=EditorJsonUtility.ToJson(importer))importer.SaveAndReimport();
        }

        public static void ConfigureProfileMaps(RebirthEntityRigProfile profile)
        {
            if(profile==null)throw new ArgumentNullException("profile");
            Texture2D originalAlbedo=profile.bakedAlbedo,originalNormal=profile.normalMap,originalRmoe=profile.rmoeMap;
            var slotOriginals=new System.Collections.Generic.Dictionary<EntityMaterialTextureOverride,(Texture2D albedo,Texture2D normal,Texture2D rough,Texture2D metal)>();
            var importerBackups=new System.Collections.Generic.Dictionary<string,TextureImportSnapshot>(StringComparer.OrdinalIgnoreCase);
            Texture2D albedo=null,normal=null,rmoe=null;
            try
            {
                if(originalAlbedo!=null)albedo=PrepareProfileMap(originalAlbedo,EntityTextureUsage.Albedo,importerBackups,profile.albedoMaxSize);
                if(originalNormal!=null)normal=PrepareProfileMap(originalNormal,EntityTextureUsage.Normal,importerBackups,profile.normalMaxSize);
                if(originalRmoe!=null)rmoe=PrepareProfileMap(originalRmoe,EntityTextureUsage.LinearData,importerBackups,profile.dataMaxSize);
                if(profile.materialTextureOverrides!=null)foreach(var maps in profile.materialTextureOverrides)
                {
                    if(maps==null)throw new InvalidOperationException("A material texture override is null.");
                    slotOriginals.Add(maps,(maps.albedo,maps.normalMap,maps.roughness,maps.metallic));
                    maps.albedo=PrepareProfileMap(maps.albedo,EntityTextureUsage.Albedo,importerBackups,profile.albedoMaxSize);
                    maps.normalMap=PrepareProfileMap(maps.normalMap,EntityTextureUsage.Normal,importerBackups,profile.normalMaxSize);
                    maps.roughness=PrepareProfileMap(maps.roughness,EntityTextureUsage.LinearData,importerBackups,profile.dataMaxSize);
                    maps.metallic=PrepareProfileMap(maps.metallic,EntityTextureUsage.LinearData,importerBackups,profile.dataMaxSize);
                }
                profile.bakedAlbedo=albedo;profile.normalMap=normal;profile.rmoeMap=rmoe;
                EditorUtility.SetDirty(profile);
            }
            catch(Exception failure)
            {
                profile.bakedAlbedo=originalAlbedo;profile.normalMap=originalNormal;profile.rmoeMap=originalRmoe;
                foreach(var prior in slotOriginals){prior.Key.albedo=prior.Value.albedo;prior.Key.normalMap=prior.Value.normal;prior.Key.roughness=prior.Value.rough;prior.Key.metallic=prior.Value.metal;}
                var rollbackErrors=new System.Collections.Generic.List<string>();
                foreach(var backup in importerBackups)
                {
                    try
                    {
                        backup.Value.Restore(backup.Key);
                    }
                    catch(Exception rollbackFailure){rollbackErrors.Add(backup.Key+": "+rollbackFailure.Message);}
                }
                if(rollbackErrors.Count>0)throw new InvalidOperationException("Texture map setup failed and importer settings could not be fully restored: "+string.Join("; ",rollbackErrors.ToArray()),failure);
                throw;
            }
        }

        public static Texture2D ExtractEmbeddedTexture(Texture2D texture)
        {
            if(texture==null)throw new ArgumentNullException("texture");
            string modelPath=AssetDatabase.GetAssetPath(texture);
            if(string.IsNullOrEmpty(modelPath))throw new InvalidOperationException("Texture must be an imported project asset or an embedded model texture.");
            if(AssetImporter.GetAtPath(modelPath) is TextureImporter)return texture;
            var modelImporter=AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if(modelImporter==null)throw new InvalidOperationException("Texture is not a standalone image or an embedded texture in an imported model: "+modelPath);

            string modelGuid=AssetDatabase.AssetPathToGUID(modelPath);
            if(string.IsNullOrEmpty(modelGuid))throw new InvalidOperationException("Could not identify the model asset containing texture '"+texture.name+"'.");
            string folder="Assets/EntityStudioGenerated/ExtractedTextures/"+modelGuid;
            EnsureFolder("Assets/EntityStudioGenerated");
            EnsureFolder("Assets/EntityStudioGenerated/ExtractedTextures");
            EnsureFolder(folder);

            Texture2D extracted=FindExtractedTexture(folder,texture.name);
            if(extracted==null)
            {
                if(!modelImporter.ExtractTextures(folder))
                    throw new InvalidOperationException("Unity could not extract embedded textures from model '"+modelPath+"'.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                extracted=FindExtractedTexture(folder,texture.name);
            }
            if(extracted==null)
                throw new InvalidOperationException("Unity extracted textures from '"+modelPath+"', but none matched embedded texture '"+texture.name+"'. Check for duplicate or renamed embedded texture assets.");
            return extracted;
        }

        private static Texture2D ConfigureAndReturn(Texture2D texture,EntityTextureUsage usage)
        {
            Texture2D asset=ExtractEmbeddedTexture(texture);
            Configure(asset,usage);
            return asset;
        }

        private static Texture2D PrepareProfileMap(Texture2D texture,EntityTextureUsage usage,System.Collections.Generic.Dictionary<string,TextureImportSnapshot> backups,int maximumSize)
        {
            Texture2D asset=ExtractEmbeddedTexture(texture);
            string path=AssetDatabase.GetAssetPath(asset);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Texture must be an imported project asset.");
            if(!backups.ContainsKey(path))backups.Add(path,new TextureImportSnapshot(importer));
            Configure(asset,usage,maximumSize);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private sealed class TextureImportSnapshot
        {
            private readonly TextureImporterType textureType;
            private readonly bool srgb,mipmaps,streamingMipmaps;
            private readonly int anisotropy,maxTextureSize;
            private readonly FilterMode filterMode;
            private readonly TextureWrapMode wrapMode;
            private readonly TextureImporterCompression compression;
            private readonly TextureImporterAlphaSource alphaSource;

            public TextureImportSnapshot(TextureImporter importer)
            {
                textureType=importer.textureType;srgb=importer.sRGBTexture;mipmaps=importer.mipmapEnabled;
                streamingMipmaps=importer.streamingMipmaps;anisotropy=importer.anisoLevel;maxTextureSize=importer.maxTextureSize;
                filterMode=importer.filterMode;wrapMode=importer.wrapMode;compression=importer.textureCompression;alphaSource=importer.alphaSource;
            }

            public void Restore(string path)
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Texture importer disappeared during rollback.");
                importer.textureType=textureType;importer.sRGBTexture=srgb;importer.mipmapEnabled=mipmaps;
                importer.streamingMipmaps=streamingMipmaps;importer.anisoLevel=anisotropy;importer.maxTextureSize=maxTextureSize;
                importer.filterMode=filterMode;importer.wrapMode=wrapMode;importer.textureCompression=compression;importer.alphaSource=alphaSource;
                importer.SaveAndReimport();
            }
        }

        private static Texture2D FindExtractedTexture(string folder,string textureName)
        {
            string[] guids=AssetDatabase.FindAssets("t:Texture2D",new[]{folder});
            Texture2D match=null;
            foreach(string guid in guids)
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                Texture2D candidate=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(candidate==null||!string.Equals(candidate.name,textureName,StringComparison.OrdinalIgnoreCase))continue;
                if(match!=null&&!string.Equals(AssetDatabase.GetAssetPath(match),path,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("More than one extracted texture is named '"+textureName+"'. Rename the embedded textures in the source model before importing.");
                match=candidate;
            }
            return match;
        }

        private static void EnsureFolder(string folder)
        {
            if(AssetDatabase.IsValidFolder(folder))return;
            int slash=folder.LastIndexOf('/');
            if(slash<0)throw new InvalidOperationException("Cannot create project folder: "+folder);
            string parent=folder.Substring(0,slash),name=folder.Substring(slash+1);
            EnsureFolder(parent);
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(parent,name);
        }
    }
}
