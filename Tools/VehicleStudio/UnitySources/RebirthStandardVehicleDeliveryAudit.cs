using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml;
using UnityEditor;
using UnityEngine;

// Validates the exact packaged candidates against the matched native definitions.
// No gameplay, source prefab mutation or installed asset writes.
public static class RebirthStandardVehicleDeliveryAudit
{
    [Serializable] public class Row { public string kind,sha256,source; public int seats,wheels,lights,paintMaterials; public string[] omittedVisuals; }
    [Serializable] public class Receipt { public string status="PACKAGED_CONTRACT_PASS_NOT_GAME_VALIDATED"; public Row[] vehicles; }
    public static void RunBatch()
    {
        const string output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/BuggyBikeDelivery_20261015";
        if(File.Exists(output+"/audit.json"))throw new Exception("Preserve prior audit");
        var xml=new XmlDocument();xml.Load("E:/_Haven/shared/REBIRTHTemp/StandardDefinitionsMeasured_v1_20261015/vehicles.xml");
        var rows=new List<Row>();
        foreach(string kind in new[]{"Buggy","Bike"})
        {
            bool bike=kind=="Bike";string version=bike?"v1":"v2";
            string bundlePath="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/"+kind+"Candidate_"+version+"/rebirth_"+kind.ToLowerInvariant()+".unity3d";
            string source="Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Final_"+version+"/rebirth"+kind+".prefab";
            var bundle=AssetBundle.LoadFromFile(bundlePath);if(!bundle)throw new Exception("Cannot load "+bundlePath);
            try
            {
                var prefab=bundle.LoadAsset<GameObject>("rebirth"+kind);if(!prefab)throw new Exception("Missing prefab");
                var vehicle=xml.SelectSingleNode("/configs/append/vehicle[@name='rebirth"+kind+"']");
                var seats=vehicle.SelectNodes("property[starts-with(@class,'seat')]");
                if(seats.Count!=(bike?1:2))throw new Exception("Seat contract "+kind);
                var physics=prefab.transform.Find("Physics");
                if(!physics||physics.tag!="Physics"||physics.gameObject.layer!=21||!physics.GetComponent<Rigidbody>())throw new Exception("Physics contract "+kind);
                var wheels=physics.GetComponentsInChildren<WheelCollider>(true);
                if(wheels.Length!=(bike?2:4))throw new Exception("Wheel contract "+kind);
                if(prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(x=>x))throw new Exception("Bundled runtime script");
                var omissions=new List<string>();
                foreach(XmlNode property in vehicle.SelectNodes(".//property[@name='transform' or @name='steerTransform' or @name='tireTransform' or @name='matT' or @name='modHideT']").Cast<XmlNode>().ToArray())
                {
                    string path=property.Attributes["value"].Value;
                    if(prefab.transform.Find(path))continue;
                    if(property.Attributes["name"].Value!="modHideT")throw new Exception("Missing native target "+kind+" "+property.OuterXml);
                    omissions.Add(property.OuterXml);property.ParentNode.RemoveChild(property);
                }
                if(!prefab.transform.Find("M/RearLights")||!prefab.transform.Find("M/RearLenses"))throw new Exception("Rear lighting contract");
                int paint=prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Count(m=>m&&m.shader&&m.shader.name=="REBIRTH/StandardVehiclePaint");
                if(paint==0)throw new Exception("Missing paint shader");
                foreach(var w in wheels)if(w.radius<=0||float.IsNaN(w.radius))throw new Exception("Invalid wheel radius");
                string hash;using(var sha=SHA256.Create())using(var file=File.OpenRead(bundlePath))hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
                rows.Add(new Row{kind=kind,sha256=hash,source=source,seats=seats.Count,wheels=wheels.Length,lights=prefab.GetComponentsInChildren<Light>(true).Length,paintMaterials=paint,omittedVisuals=omissions.ToArray()});
            }
            finally{bundle.Unload(true);}
        }
        Directory.CreateDirectory(output+"/Config/_Vehicles");
        xml.Save(output+"/Config/_Vehicles/standard_vehicles.xml");
        foreach(string name in new[]{"standard_items.xml","standard_entityclasses.xml"})File.Copy("C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0/Tools/VehicleStudio/DefinitionCandidates/BuggyBike_v1/"+name,output+"/Config/_Vehicles/"+name,false);
        File.WriteAllText(output+"/audit.json",JsonUtility.ToJson(new Receipt{vehicles=rows.ToArray()},true));
    }
}
