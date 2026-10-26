using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

public static class RebirthVehicleSeatUpgrade
{
    const string Mod="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0";
    public const string Folder="Assets/REBIRTHSeatPelvis_20261017";
    public const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/SeatPelvis_20261017";
    static readonly string[] Names={"rebirthCamper80s","rebirthBuggy","rebirthBike"};
    static Vector3 Vec(string value) {var p=value.Split(',');return new Vector3(float.Parse(p[0],CultureInfo.InvariantCulture),float.Parse(p[1],CultureInfo.InvariantCulture),float.Parse(p[2],CultureInfo.InvariantCulture));}
    static string Value(XmlNode node,string key,string fallback="0,0,0")=>node.SelectSingleNode("property[@name='"+key+"']")?.Attributes["value"]?.Value??fallback;
    public static void Author(GameObject nativeBody)
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        var report=new List<string>();
        for(int v=0;v<Names.Length;v++)
        {
            string name=Names[v],path=Folder+"/"+name+".prefab";
            if(File.Exists(path))
            {
                var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path).transform.Find("GameObject");
                for(int seat=0;seat<(v==0?6:v==1?2:1);seat++)Rebirth.Vehicles.Authoring.SeatPoseVehicleTargets.Read(existing,seat);
                report.Add(name+" existing completed candidate preserved");continue;
            }
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHSeatTargets_20261017/"+name+".prefab"));root.name=name;
            try
            {
                var model=root.transform.Find("GameObject");
                var xml=new XmlDocument();xml.Load(Mod+"/Config/_Vehicles/"+(v==0?"camper_vehicles.xml":"standard_vehicles.xml"));
                var definition=xml.SelectSingleNode("/configs/append/vehicle[@name='"+name+"']");
                int seats=v==0?6:v==1?2:1;
                for(int seat=0;seat<seats;seat++)
                {
                    var row=definition.SelectSingleNode("property[@class='seat"+seat+"']");
                    var body=Object.Instantiate(nativeBody);body.SetActive(true);body.transform.SetParent(model,false);
                    try
                    {
                        body.transform.localPosition=Vec(Value(row,"position"));body.transform.localRotation=Quaternion.Euler(Vec(Value(row,"rotation")));
                        RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,int.Parse(Value(row,"pose","0")));
                        var hips=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Hips");
                        Vector3 position=hips.position;
                        // Supplied bike's authored saddle pivot, with pelvis clearance.
                        // Other seats retain the accepted placement; user fine-tunes in editor.
                        if(v==2)position=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Seat").position+model.up*.10f;
                        var marker=new GameObject("REBIRTH_Seat"+seat+"_Seat").transform;
                        marker.SetParent(model.Find("REBIRTHSeatContactTargets_Seat"+seat),false);marker.position=position;
                        report.Add(name+" seat="+seat+" pelvis="+model.InverseTransformPoint(position).ToString("F6"));
                    }
                    finally{Object.DestroyImmediate(body);}
                }
                if(v==2)
                {
                    var rb=root.GetComponentInChildren<Rigidbody>();
                    // Match native motorcycle serialization. Native Init chooses
                    // (0,.1,0) when automaticCenterOfMass is true.
                    rb.ResetCenterOfMass();rb.ResetInertiaTensor();rb.useGravity=true;
                    rb.collisionDetectionMode=CollisionDetectionMode.Discrete;
                    foreach(var wheel in rb.GetComponentsInChildren<WheelCollider>())
                    {
                        wheel.center=Vector3.zero;
                        var p=wheel.transform.localPosition;p.x=0;wheel.transform.localPosition=p;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();File.WriteAllLines(Output+"/seat-references.txt",report);Build();
    }
    public static void Build()
    {
        Directory.CreateDirectory(Output+"/Bundles");
        var builds=Names.Select(n=>new AssetBundleBuild{assetBundleName=n=="rebirthCamper80s"?"rebirth_camper80s.unity3d":n=="rebirthBuggy"?"rebirth_buggy.unity3d":"rebirth_bike.unity3d",assetNames=new[]{CurrentPrefab(n)},addressableNames=new[]{n}}).ToArray();
        if(!BuildPipeline.BuildAssetBundles(Output+"/Bundles",builds,BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Bundle build failed");
        var proof=new List<string>();
        for(int v=0;v<Names.Length;v++)
        {
            string path=Output+"/Bundles/"+builds[v].assetBundleName;
            if(!BuildPipeline.GetCRCForAssetBundle(path,out uint crc))throw new Exception("CRC missing");
            var baseline=AssetBundle.LoadFromFile(Mod+"/Resources/"+builds[v].assetBundleName);
            if(!baseline)throw new Exception("Installed baseline unavailable");
            string[] expected;
            try{expected=Snapshot(baseline.LoadAsset<GameObject>(Names[v]));}finally{baseline.Unload(true);}
            var bundle=AssetBundle.LoadFromFile(path,crc);
            if(!bundle)throw new Exception("CRC reopen failed");
            try
            {
                var prefab=bundle.LoadAsset<GameObject>(Names[v]);
                var model=prefab.transform.Find("GameObject");int seats=v==0?6:v==1?2:1;
                for(int seat=0;seat<seats;seat++)Rebirth.Vehicles.Authoring.SeatPoseVehicleTargets.Read(model,seat);
                var actual=Snapshot(prefab);
                if(!expected.SequenceEqual(actual))throw new Exception("Compiled visual/texture snapshot changed: "+string.Join("; ",expected.Except(actual).Take(5)));                if(v==2)
                {
                    var rb=prefab.GetComponentInChildren<Rigidbody>();
                    if(!rb.automaticCenterOfMass||rb.collisionDetectionMode!=CollisionDetectionMode.Discrete)throw new Exception("Native body contract mismatch");
                    foreach(var wheel in rb.GetComponentsInChildren<WheelCollider>())if(wheel.center!=Vector3.zero||wheel.transform.localPosition.x!=0)throw new Exception("Wheel alignment mismatch");
                }
                proof.Add("PASS "+Names[v]+" seats="+seats+" CRC="+crc+" reader, visuals and texture formats; bike native body/wheel serialization checked. Not in-game validation.");
            }
            finally{bundle.Unload(true);}
        }
        File.WriteAllLines(Output+"/verified.txt",proof);
    }
    public static void FinishChecks() { RebirthBikeIdleChecks.Run(); Build(); }
    static string CurrentPrefab(string name)
    {
        const string doorHardware="Assets/REBIRTHCamperDoorHardware_20261018_v1/rebirthCamper80s.prefab";
        if(name=="rebirthCamper80s"&&File.Exists(doorHardware))return doorHardware;
        const string rearFit="Assets/REBIRTHCamperRearFit_20261018_v1/rebirthCamper80s.prefab";
        if(name=="rebirthCamper80s"&&File.Exists(rearFit))return rearFit;
        const string damage="Assets/REBIRTHCamperDamageArmor_20261018_v1/rebirthCamper80s.prefab";
        if(name=="rebirthCamper80s"&&File.Exists(damage))return damage;
        const string approved="Assets/REBIRTHCamperApprovedArmor_20261018_v4/rebirthCamper80s.prefab";
        if(name=="rebirthCamper80s"&&File.Exists(approved))return approved;
        const string equipped="Assets/REBIRTHCamperArmorPlow_20261017_v4/rebirthCamper80s.prefab";
        return name=="rebirthCamper80s"&&File.Exists(equipped)?equipped:Folder+"/"+name+".prefab";
    }
    static string[] Snapshot(GameObject root)
    {
        var result=new List<string>();
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string path=AnimationUtility.CalculateTransformPath(renderer.transform,root.transform);
            result.Add(path+" pos="+renderer.transform.localPosition.ToString("F6")+" rot="+renderer.transform.localRotation.ToString("F6"));
            foreach(var material in renderer.sharedMaterials)
            {
                if(!material){result.Add(path+" null material");continue;}
                result.Add(path+" material="+material.name+" shader="+material.shader.name);
                foreach(string key in material.GetTexturePropertyNames().OrderBy(k=>k))
                {
                    var t=material.GetTexture(key) as Texture2D;if(!t)continue;
                    result.Add(path+" "+material.name+" "+key+" "+t.width+"x"+t.height+" "+t.format);
                }
            }
        }
        return result.ToArray();
    }
}



