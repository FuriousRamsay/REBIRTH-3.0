using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;using Object=UnityEngine.Object;
public static class RebirthCamperFinalBundle {
 const string P="Assets/REBIRTHCamperFrontProfile_20261015_v7/rebirthCamper80s.prefab";
 const string O="E:/_Haven/shared/REBIRTHTemp/CamperFrontProfile_20261015_v7";
 public static void Run(){RebirthCamperBodyTextureBudget.Run();
 if(!File.Exists("E:/_Haven/shared/REBIRTHTemp/CamperBodyTextureBudget_20261015/verified.txt"))throw new Exception("Camper texture import receipt missing");
 if(!File.Exists("E:/_Haven/shared/REBIRTHTemp/CamperTextureBudget_20261015/verified.txt"))throw new Exception("Station texture import receipt missing");
 if(File.Exists(O+"/bundle-verified.txt"))File.Move(O+"/bundle-verified.txt",O+"/bundle-verified-before-final-"+DateTime.UtcNow.Ticks+".txt");
 RebirthCamperFrontProfileFix.Build();VerifyTextures();}
 public static void VerifyOnly(){RebirthCamperFrontProfileFix.VerifyExisting();VerifyTextures();}
 public static void VerifyTextures(){var b=AssetBundle.LoadFromFile(O+"/Bundle/rebirth_camper80s.unity3d");if(!b)throw new Exception("Missing bundle");try{var expected=AssetDatabase.LoadAssetAtPath<GameObject>(P);var actual=b.LoadAsset<GameObject>("rebirthCamper80s");var er=expected.GetComponentsInChildren<Renderer>(true);var ar=actual.GetComponentsInChildren<Renderer>(true);if(er.Length!=ar.Length)throw new Exception("Renderer count mismatch");var rows=new List<string>{"renderer\tproperty\tsourcePath\twidth\theight\tformat"};for(int r=0;r<er.Length;r++){var em=er[r].sharedMaterials;var am=ar[r].sharedMaterials;if(em.Length!=am.Length)throw new Exception("Material count mismatch");for(int m=0;m<em.Length;m++){if(!em[m])continue;foreach(var key in em[m].GetTexturePropertyNames()){var et=em[m].GetTexture(key) as Texture2D;if(!et)continue;var at=am[m].GetTexture(key) as Texture2D;if(!at||at.width!=et.width||at.height!=et.height||at.format!=et.format)throw new Exception("Bundled texture differs: "+er[r].name+"/"+key);rows.Add(er[r].name+"\t"+key+"\t"+AssetDatabase.GetAssetPath(et)+"\t"+at.width+"\t"+at.height+"\t"+at.format);}}}File.WriteAllLines(O+"/bundle-textures-verified.tsv",rows);File.WriteAllText(O+"/final-texture-verification.txt","PASS all referenced bundle texture dimensions/formats equal current Unity source imports; station and camper texture budget receipts present. Not installed by this tool. No in-game validation.");}finally{b.Unload(true);}}
}


