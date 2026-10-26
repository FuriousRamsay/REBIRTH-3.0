using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RebirthStandardVehicleLights
{
    public static void BuildBothBatch()
    {
        foreach(string kind in new[]{"Buggy","Bike"})
        {
            bool bike=kind=="Bike";
            string source="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Physics_v1/rebirth"+kind+".prefab";
            string folder="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Lights_v2";
            string output=folder+"/rebirth"+kind+".prefab";
            if(File.Exists(output)) throw new InvalidOperationException("Candidate already exists");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if(prefab==null) throw new FileNotFoundException(source);
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var root=UnityEngine.Object.Instantiate(prefab);
            try
            {
                root.name="rebirth"+kind;
                var shader=Shader.Find("REBIRTH/VehicleRearLens");
                if(shader==null) throw new InvalidDataException("Rear lens shader missing");
                var rearMaterial=new Material(shader);
                rearMaterial.color=new Color(.25f,.005f,.005f);
                rearMaterial.SetFloat("_Glossiness",.8f);rearMaterial.EnableKeyword("_EMISSION");
                rearMaterial.SetColor("_EmissionColor",Color.black);
                AssetDatabase.CreateAsset(rearMaterial,folder+"/RearLens.mat");
                var head=Ensure(root.transform,bike?"M/Forks/HeadLights/Lights":"M/HeadLights/Lights");
                for(int i=0;i<(bike?1:2);i++)
                {
                    var light=new GameObject("HeadSpot"+i).AddComponent<Light>();light.transform.SetParent(head,false);
                    light.transform.position=root.transform.TransformPoint(bike?new Vector3(0,1.25f,.95f):new Vector3(i==0?-.65f:.65f,1.35f,2.2f));
                    light.type=LightType.Spot;light.range=32;light.spotAngle=48;light.intensity=1.1f;light.enabled=false;
                }
                var rear=Ensure(root.transform,"M/RearLights");
                var lenses=Ensure(root.transform,"M/RearLenses");
                for(int i=0;i<(bike?1:2);i++)
                {
                    Vector3 position=bike?new Vector3(0,1.18f,-.94f):new Vector3(i==0?-.8f:.8f,1.05f,-1.7f);
                    var lens=GameObject.CreatePrimitive(PrimitiveType.Cube);lens.name="RearLens"+i;
                    UnityEngine.Object.DestroyImmediate(lens.GetComponent<Collider>());
                    lens.transform.SetParent(lenses,false);lens.transform.localPosition=position;
                    lens.transform.localScale=bike?new Vector3(.16f,.07f,.025f):new Vector3(.2f,.1f,.025f);
                    lens.GetComponent<Renderer>().sharedMaterial=rearMaterial;
                    var light=new GameObject("RearSpot"+i).AddComponent<Light>();light.transform.SetParent(rear,false);
                    light.transform.localPosition=position-new Vector3(0,0,.03f);light.transform.localRotation=Quaternion.Euler(0,180,0);
                    light.type=LightType.Spot;light.color=Color.red;light.range=4;light.spotAngle=48;light.intensity=0;light.enabled=false;
                }
                PrefabUtility.SaveAsPrefabAsset(root,output);AssetDatabase.SaveAssets();
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
    }
    static Transform Ensure(Transform root,string path)
    {
        foreach(string segment in path.Split('/'))
        {var child=root.Find(segment);if(child==null){child=new GameObject(segment).transform;child.SetParent(root,false);}root=child;}
        return root;
    }
}
