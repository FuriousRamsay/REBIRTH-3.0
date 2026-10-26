using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
namespace RebirthEntityStudio {
 public static class RebirthNativeHairInventory {
  const string Root="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Data/Addressables/Standalone";
  public static void Run(){try{
   string output=Environment.GetEnvironmentVariable("REBIRTH_APPEARANCE_OUTPUT");var rows=new List<string>{"Native installed-game reference; not an assertion of 3.0b259 compatibility.","Unity="+Application.unityVersion};
   foreach(string p in Directory.GetFiles(Root,"*unitybuiltinshaders.bundle"))AssetBundle.LoadFromFile(p);
   AssetBundle.LoadFromFile(Root+"/shaders_assets_all.bundle");
   foreach(string p in new[]{"player_assets_entities/player/common.bundle","player_assets_entities/player/female/common.bundle","player_assets_entities/player/male/common.bundle"}){var b=AssetBundle.LoadFromFile(Root+"/"+p);rows.Add("DEPENDENCY "+p+" loaded="+(b!=null));}
   var paths=Directory.GetFiles(Root+"/player_assets_entities/player","*.bundle",SearchOption.AllDirectories).Where(p=>p.Replace('\\','/').Contains("/hair/")||p.Replace('\\','/').Contains("/facialhair/")).OrderBy(p=>p).ToArray();
   foreach(string path in paths){var bundle=AssetBundle.LoadFromFile(path);rows.Add("BUNDLE "+path.Substring(Root.Length+1));if(bundle==null){rows.Add("LOAD_FAILED");continue;}
    foreach(string name in bundle.GetAllAssetNames())rows.Add(" ASSET "+name);
    foreach(var prefab in bundle.LoadAllAssets<GameObject>()){
     rows.Add(" PREFAB "+prefab.name);
     foreach(var lod in prefab.GetComponentsInChildren<LODGroup>(true))foreach(var level in lod.GetLODs())rows.Add("  LOD screen="+level.screenRelativeTransitionHeight+" renderers="+string.Join(",",level.renderers.Where(r=>r!=null).Select(r=>r.name)));
     foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true)){
      Mesh mesh=renderer is SkinnedMeshRenderer?((SkinnedMeshRenderer)renderer).sharedMesh:renderer.GetComponent<MeshFilter>()?.sharedMesh;
      rows.Add("  RENDERER "+AnimationUtility.CalculateTransformPath(renderer.transform,prefab.transform)+" type="+renderer.GetType().Name+" matrix="+renderer.localToWorldMatrix);
      if(mesh!=null){rows.Add("   MESH "+mesh.name+" vertices="+mesh.vertexCount+" submeshes="+mesh.subMeshCount+" readable="+mesh.isReadable+" bounds="+mesh.bounds+" blendShapes="+mesh.blendShapeCount+" bindposes="+mesh.bindposes.Length);for(int s=0;s<mesh.subMeshCount;s++)rows.Add("    SUB "+s+" indices="+mesh.GetIndexCount(s)+" topology="+mesh.GetTopology(s));
       using(var read=MeshUtility.AcquireReadOnlyMeshData(mesh)){var data=read[0];rows.Add("    VERTEX_FORMAT "+string.Join(";",mesh.GetVertexAttributes().Select(a=>a.attribute+":"+a.format+"x"+a.dimension)));var verts=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);data.GetVertices(verts);float min=verts.Min(v=>v.y),max=verts.Max(v=>v.y);rows.Add("    LOCAL_Y "+min+" .. "+max);verts.Dispose();}
      }
      if(renderer is SkinnedMeshRenderer){var sk=(SkinnedMeshRenderer)renderer;rows.Add("   SKIN root="+(sk.rootBone?sk.rootBone.name:"null")+" bones="+string.Join(",",sk.bones.Select(b=>b?b.name:"null")));}
      foreach(var material in renderer.sharedMaterials){if(material==null){rows.Add("   MATERIAL null");continue;}rows.Add("   MATERIAL "+material.name+" shader="+(material.shader?material.shader.name:"null")+" queue="+material.renderQueue+" keywords="+string.Join(",",material.shaderKeywords));if(material.shader==null)continue;var shader=material.shader;for(int i=0;i<ShaderUtil.GetPropertyCount(shader);i++){string key=ShaderUtil.GetPropertyName(shader,i);var type=ShaderUtil.GetPropertyType(shader,i);if(type==ShaderUtil.ShaderPropertyType.TexEnv){var tex=material.GetTexture(key);rows.Add("    TEXTURE "+key+"="+(tex?tex.name+" "+tex.width+"x"+tex.height:"null")+" scale="+material.GetTextureScale(key)+" offset="+material.GetTextureOffset(key));}else if(type==ShaderUtil.ShaderPropertyType.Color)rows.Add("    COLOR "+key+"="+material.GetColor(key));else if(type==ShaderUtil.ShaderPropertyType.Float||type==ShaderUtil.ShaderPropertyType.Range)rows.Add("    FLOAT "+key+"="+material.GetFloat(key));}}
     }
    }
    File.WriteAllLines(Path.Combine(output,"NativeHairInventory.txt"),rows);
   }
   rows.Add("BUNDLES_INSPECTED="+paths.Length);File.WriteAllLines(Path.Combine(output,"NativeHairInventory.txt"),rows);Debug.Log("REBIRTH_NATIVE_HAIR_INVENTORY_READY count="+paths.Length);EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}

