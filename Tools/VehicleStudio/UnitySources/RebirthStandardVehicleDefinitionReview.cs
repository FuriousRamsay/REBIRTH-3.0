using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Xml;
using UnityEditor;
using UnityEngine;

public static class RebirthStandardVehicleDefinitionReview
{
    [Serializable] public class MeasuredFoot {public string side;public Vector3 position,rotation;}
    [Serializable] public class MeasuredSeat {public int pose;public Vector3 anchor,leftTarget,rightTarget,leftTargetRotation,rightTargetRotation;public MeasuredFoot[] feet;}
    [Serializable] public class MeasuredFit {public string prefab;public MeasuredSeat[] seats;}
    [Serializable] public class ExportReceipt {public string status="REVIEW_CANDIDATE_NOT_INSTALLED",scope="Measured native-pose/IK configuration; requires final asset/runtime validation";public string[] changes;}
    static void Set(XmlDocument xml,XmlNode part,string name,string value)
    {
        var node=part.SelectSingleNode("property[@name='"+name+"']");if(node==null){node=xml.CreateElement("property");var key=xml.CreateAttribute("name");key.Value=name;node.Attributes.Append(key);var val=xml.CreateAttribute("value");node.Attributes.Append(val);part.AppendChild(node);}node.Attributes["value"].Value=value;
    }
    public static void ExportMeasuredBatch()
    {
        const string output="E:/_Haven/shared/REBIRTHTemp/StandardDefinitionsMeasured_v1_20261015";if(Directory.Exists(output))throw new Exception("Preserve previous measured definition export");
        var xml=new XmlDocument();xml.Load("E:/_Haven/shared/REBIRTHTemp/BuggyBikeAudit_20261012/standard_vehicles.xml");var changes=new System.Collections.Generic.List<string>();
        foreach(string kind in new[]{"Buggy","Bike"})
        {
            var fit=JsonUtility.FromJson<MeasuredFit>(File.ReadAllText("E:/_Haven/shared/REBIRTHTemp/BuggyBikeNativeFit_v19_20261015/standard_fit/"+kind+"/result.json"));var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(fit.prefab);if(!prefab)throw new Exception("Measured source missing "+kind);
            var vehicle=xml.SelectSingleNode("/configs/append/vehicle[@name='rebirth"+kind+"']");var steering=vehicle.SelectSingleNode("property[@class='handlebars']");var pivot=prefab.transform.Find(kind=="Bike"?"M/Forks":"M/Steering");
            for(int seat=0;seat<fit.seats.Length;seat++)
            {
                var row=fit.seats[seat];var part=vehicle.SelectSingleNode("property[@class='seat"+seat+"']");if(part==null)throw new Exception("Measured seat definition absent");
                if(part.SelectSingleNode("property[@name='rotation']").Attributes["value"].Value.Replace(" ","")!="0,0,0")throw new Exception("Exporter requires reviewed neutral seat rotation");
                Set(xml,part,"position",Vector(row.anchor));Set(xml,part,"pose",row.pose.ToString(CultureInfo.InvariantCulture));
                if(row.feet!=null)foreach(var foot in row.feet){string side=foot.side=="Left"?"L":"R";Set(xml,part,"IKFoot"+side+"Position",Vector(foot.position-row.anchor));Set(xml,part,"IKFoot"+side+"Rotation",Vector(foot.rotation));}
            }
            var driver=fit.seats[0];foreach(bool left in new[]{true,false}){string hand=left?"IKHandL":"IKHandR";var point=left?driver.leftTarget:driver.rightTarget;var rotation=Quaternion.Euler(left?driver.leftTargetRotation:driver.rightTargetRotation);Set(xml,steering,hand+"Position",Vector(pivot.InverseTransformPoint(prefab.transform.TransformPoint(point))));Set(xml,steering,hand+"Rotation",Vector((Quaternion.Inverse(pivot.rotation)*prefab.transform.rotation*rotation).eulerAngles));}
            foreach(XmlNode property in vehicle.SelectNodes(".//property[@name='transform' or @name='steerTransform' or @name='tireTransform']"))if(!prefab.transform.Find(property.Attributes["value"].Value))throw new Exception("Required transform absent "+kind+" "+property.OuterXml);
            foreach(XmlNode property in vehicle.SelectNodes(".//property[@name='modT' or @name='modOnT' or @name='modRBT' or @name='particleOn' or @name='particleDamaged' or @name='particleBroken']").Cast<XmlNode>().ToArray())if(!prefab.transform.Find(property.Attributes["value"].Value)){changes.Add(kind+": omitted unprovided optional visual "+property.OuterXml);property.ParentNode.RemoveChild(property);}
            changes.Add(kind+": anchors/poses and hand/foot IK exported from actual native-body fit "+fit.prefab+"; gameplay values/sounds retained");
        }
        Directory.CreateDirectory(output);xml.Save(output+"/vehicles.xml");File.WriteAllText(output+"/receipt.json",JsonUtility.ToJson(new ExportReceipt{changes=changes.ToArray()},true));
    }
    static string Vector(Vector3 v){return string.Join(", ",new[]{v.x,v.y,v.z}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));}
    public static void ExportBatch()
    {
        string file="E:/_Haven/shared/REBIRTHTemp/BuggyBikeAudit_20261012/standard_vehicles.xml";
        var xml=new XmlDocument();xml.Load(file);
        foreach(string kind in new[]{"Buggy","Bike"})
        {
            bool bike=kind=="Bike";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHStandardVehiclesGenerated/"+kind+"Final_v1/rebirth"+kind+".prefab");
            if(prefab==null)throw new FileNotFoundException(kind+" candidate");
            var vehicle=xml.SelectSingleNode("/configs/append/vehicle[@name='rebirth"+kind+"']");
            var steering=vehicle.SelectSingleNode("property[@class='handlebars']");
            var pivot=prefab.transform.Find(bike?"M/Forks":"M/Steering");
            for(int hand=0;hand<2;hand++)
            {
                string name=hand==0?"IKHandL":"IKHandR";
                Vector3 point=bike?new Vector3(hand==0?-.475f:.475f,1.695f,.485f):new Vector3(hand==0?-.61f:-.27f,1.66f,.55f);
                Quaternion rotation=Quaternion.Euler(bike?new Vector3(hand==0?-7.29f:4.15f,hand==0?77.3f:105.65f,hand==0?29.69f:-158.73f):new Vector3(0,hand==0?80:100,hand==0?30:-150));
                steering.SelectSingleNode("property[@name='"+name+"Position']").Attributes["value"].Value=Vector(pivot.InverseTransformPoint(prefab.transform.TransformPoint(point)));
                steering.SelectSingleNode("property[@name='"+name+"Rotation']").Attributes["value"].Value=Vector((Quaternion.Inverse(pivot.rotation)*prefab.transform.rotation*rotation).eulerAngles);
            }
            foreach(XmlNode property in vehicle.SelectNodes(".//property[@name='transform' or @name='steerTransform' or @name='tireTransform']"))
                if(prefab.transform.Find(property.Attributes["value"].Value)==null)
                    throw new InvalidDataException("Required native transform missing: "+kind+" "+property.OuterXml);
        }
        xml.Save("E:/_Haven/shared/REBIRTHTemp/BuggyBikeAudit_20261012/standard_vehicles_calibrated.xml");
    }
}
