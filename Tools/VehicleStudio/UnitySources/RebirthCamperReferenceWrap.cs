using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RebirthCamperReferenceWrap
{
 public const string Folder="Assets/REBIRTHCamperWrapGenerated/ReferencePurple";
 public static string Apply(GameObject root,bool disco=true)
 {
  string candidate=Folder+"/Candidates/"+Guid.NewGuid().ToString("N");
  Directory.CreateDirectory(candidate); AssetDatabase.Refresh();
  var shader=Shader.Find("REBIRTH/CamperReferenceWrap");
  if(!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Reference wrap shader failed.");
  var art=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/PatternArtwork.png");
  if(!art) throw new Exception("Pattern artwork missing.");
  foreach(string name in new[]{"body","camper_body"}) {
   var renderer=root.GetComponentsInChildren<MeshRenderer>(true).Single(x=>x.name==name);
   var original=renderer.sharedMaterial;
   var material=new Material(shader){name="ReferencePurple_"+name};
   material.SetTexture("_MainTex",original.mainTexture);
   material.SetTexture("_NeutralTex",name=="body" ? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Perfectvertex/80sCamperTruck/Art/Textures/body_white.png") : original.mainTexture);
   material.SetTexture("_Artwork",art);
   foreach(string key in new[]{"_BumpMap","_OcclusionMap","_MetallicGlossMap"})
    if(original.HasProperty(key)) material.SetTexture(key,original.GetTexture(key));
   material.SetFloat("_IsCamper",name=="camper_body"?1:0);
   material.SetFloat("_PatternEnabled",disco?1:0);
   foreach(string key in new[]{"_BumpScale","_GlossMapScale","_OcclusionStrength"})
    if(original.HasProperty(key)) material.SetFloat(key,original.GetFloat(key));
   var matrix=root.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
   for(int row=0;row<3;row++) material.SetVector("_VehicleRow"+row,matrix.GetRow(row));
   AssetDatabase.CreateAsset(material,candidate+"/"+name+".mat");
   renderer.sharedMaterial=material;
  }
  string prefab=candidate+"/ReferencePurpleCamper.prefab";
  PrefabUtility.SaveAsPrefabAsset(root,prefab); AssetDatabase.SaveAssets();
  return prefab;
 }
}
