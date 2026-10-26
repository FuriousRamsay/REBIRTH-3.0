using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Read-only material binding export for the approved pickup texture authoring.
public static class RebirthTruckTextureSurvey
{
    [Serializable] public class Texture { public string property,path; }
    [Serializable] public class Number { public string property; public float value; }
    [Serializable] public class Tint { public string property; public Color value; }
    [Serializable] public class Entry { public string part,asset,shader; public List<Texture> textures=new List<Texture>(); public List<Number> floats=new List<Number>(); public List<Tint> colors=new List<Tint>(); }
    [Serializable] public class Data { public List<Entry> materials=new List<Entry>(); }
    public static void Export()
    {
        var data=new Data();
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHBareTruckBodywork_20261020_v3/rebirthCamper80s.prefab"));
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))
        {
            string path=AnimationUtility.CalculateTransformPath(r.transform,root.transform);
            if(path.Contains("REBIRTHPickupOnly")||path.Contains("REBIRTHTruckCommon")||path.Contains("DistanceLOD"))continue;
            foreach(var m in r.sharedMaterials)
            {
                if(!m)continue;
                var e=new Entry{part=path,asset=AssetDatabase.GetAssetPath(m),shader=m.shader.name};
                foreach(string key in m.GetTexturePropertyNames())e.textures.Add(new Texture{property=key,path=AssetDatabase.GetAssetPath(m.GetTexture(key))});
                foreach(string key in new[]{"_Glossiness","_Metallic","_GlossMapScale","_BumpScale","_OcclusionStrength","_PatternEnabled"})if(m.HasProperty(key))e.floats.Add(new Number{property=key,value=m.GetFloat(key)});
                foreach(string key in new[]{"_Color","_EmissionColor"})if(m.HasProperty(key))e.colors.Add(new Tint{property=key,value=m.GetColor(key)});
                data.materials.Add(e);
            }
        }
        string output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/TruckBodyworkTextured_20261020";
        Directory.CreateDirectory(output);File.WriteAllText(output+"/source-materials.json",JsonUtility.ToJson(data,true));
        Object.DestroyImmediate(root);Debug.Log("TRUCK_TEXTURE_SURVEY_COMPLETE "+data.materials.Count);
    }
}
