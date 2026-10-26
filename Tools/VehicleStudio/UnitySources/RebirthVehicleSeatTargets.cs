using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RebirthVehicleSeatTargets
{
    const string Mod="C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0";
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/SeatTargets_20261017";
    const string Folder="Assets/REBIRTHSeatTargets_20261017";
    static readonly string[] Names={"rebirthCamper80s","rebirthBuggy","rebirthBike"};
    static readonly string[] Sources={"Assets/REBIRTHCamperFrontProfile_20261015_v7/rebirthCamper80s.prefab","Assets/REBIRTHStandardVehiclesGenerated/BuggyNativeHierarchy_v1/rebirthBuggy.prefab","Assets/REBIRTHStandardVehiclesGenerated/BikeNativeHierarchy_v1/rebirthBike.prefab"};
    static Vector3 Vec(string value) {var p=value.Split(',');return new Vector3(float.Parse(p[0],CultureInfo.InvariantCulture),float.Parse(p[1],CultureInfo.InvariantCulture),float.Parse(p[2],CultureInfo.InvariantCulture));}
    static string Value(XmlNode node,string key,string fallback="0,0,0") => node.SelectSingleNode("property[@name='"+key+"']")?.Attributes["value"]?.Value??fallback;
    public static void Author(GameObject nativeBody)
    {
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var report=new List<string>{"vehicle\tseat\tlimb\tpath\tposition\tprovenance"};
        for(int v=0;v<Names.Length;v++)
        {
            string name=Names[v],destination=Folder+"/"+name+".prefab";
            if(File.Exists(destination))throw new Exception("Preserve existing source candidate: "+destination);
            var xml=new XmlDocument();xml.Load(Mod+"/Config/_Vehicles/"+(v==0?"camper_vehicles.xml":"standard_vehicles.xml"));
            var definition=xml.SelectSingleNode("/configs/append/vehicle[@name='"+name+"']");
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Sources[v]));root.name=name;
            try
            {
                var model=root.transform.Find("GameObject");if(!model)throw new Exception("Native model root missing");
                for(int seat=0;;seat++)
                {
                    var row=definition.SelectSingleNode("property[@class='seat"+seat+"']");if(row==null)break;
                    var body=Object.Instantiate(nativeBody);body.SetActive(true);body.transform.SetParent(model,false);
                    try
                    {
                        body.transform.localPosition=Vec(Value(row,"position"));body.transform.localRotation=Quaternion.Euler(Vec(Value(row,"rotation")));
                        RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,int.Parse(Value(row,"pose","0")));
                        var bones=body.GetComponentsInChildren<Transform>(true);
                        foreach(string limb in new[]{"HandL","HandR","FootL","FootR"})
                        {
                            bool hand=limb.StartsWith("Hand"),left=limb.EndsWith("L");
                            string key="IK"+limb+"Position",origin="native base-pose bone; editable Unity reference";
                            Transform parent=model;
                            var bone=bones.Single(t=>t.name==(left?"Left":"Right")+(hand?"Hand":"Foot"));
                            Vector3 point=model.InverseTransformPoint(bone.position);
                            var steering=definition.SelectSingleNode("property[@class='handlebars']");
                            if(seat==0&&hand&&steering?.SelectSingleNode("property[@name='"+key+"']")!=null)
                            {
                                parent=model.Find(Value(steering,"transform",""));if(!parent)throw new Exception("Steering pivot missing");
                                point=model.InverseTransformPoint(parent.TransformPoint(Vec(Value(steering,key))));
                                origin="measured steering grip/wrist reference from installed native steering contract";
                            }
                            else if(row.SelectSingleNode("property[@name='"+key+"']")!=null)
                            {point=model.InverseTransformPoint(body.transform.TransformPoint(Vec(Value(row,key))));origin="installed seat-specific native IK reference";}
                            string groupName="REBIRTHSeatContactTargets_Seat"+seat;
                            var group=parent.Find(groupName);
                            if(!group){group=new GameObject(groupName).transform;group.SetParent(parent,false);}
                            var marker=new GameObject("REBIRTH_Seat"+seat+"_"+limb);marker.transform.SetParent(group,false);
                            marker.transform.position=model.TransformPoint(point);marker.transform.rotation=Quaternion.identity;
                            report.Add(name+"\t"+seat+"\t"+limb+"\t"+AnimationUtility.CalculateTransformPath(marker.transform,model)+"\t"+point.ToString("F6")+"\t"+origin);
                        }
                    }
                    finally{Object.DestroyImmediate(body);}
                }
                // Only empty transforms were added; no behaviours or colliders.
                PrefabUtility.SaveAsPrefabAsset(root,destination);
            }
            finally{Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();File.WriteAllLines(Output+"/targets.tsv",report);
        ProjectCabFootContacts();
    }
    public static void ProjectCabFootContacts()
    {
        var rows=new List<string>{"vehicle\tmarker\tbefore\tafter\tsupportMesh"};
        // Bike feet already use the measured peg references. Cab feet are
        // projected onto supplied geometry below the native foot position.
        foreach(string name in Names.Take(2))
        {
            string path=Folder+"/"+name+".prefab";
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));root.name=name;
            var temporary=new List<GameObject>();
            try
            {
                var model=root.transform.Find("GameObject");var surfaces=new List<MeshCollider>();
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.sharedMesh||!filter.GetComponent<Renderer>())continue;
                    var go=new GameObject("TemporaryContactQuery");temporary.Add(go);go.transform.SetParent(filter.transform,false);
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;surfaces.Add(collider);
                }
                Physics.SyncTransforms();
                foreach(string limb in new[]{"FootL","FootR"})
                {
                    var marker=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="REBIRTH_Seat0_"+limb);
                    Vector3 before=model.InverseTransformPoint(marker.position);
                    var ray=new Ray(marker.position+Vector3.up*.05f,Vector3.down);float best=1.5f;RaycastHit selected=default(RaycastHit);MeshCollider support=null;
                    foreach(var surface in surfaces)if(surface.Raycast(ray,out RaycastHit hit,best)&&hit.normal.y>.5f)
                    {best=hit.distance;selected=hit;support=surface;}
                    if(!support)throw new Exception("No supplied cab floor support below "+name+"/"+limb);
                    // Contact marker targets the ankle, with 8 cm sole clearance;
                    // rotation remains an explicit editor/user choice.
                    marker.position=selected.point+Vector3.up*.08f;
                    rows.Add(name+"\t"+marker.name+"\t"+before.ToString("F6")+"\t"+model.InverseTransformPoint(marker.position).ToString("F6")+"\t"+AnimationUtility.CalculateTransformPath(support.transform.parent,model));
                }
                foreach(var go in temporary)Object.DestroyImmediate(go);temporary.Clear();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{foreach(var go in temporary)if(go)Object.DestroyImmediate(go);Object.DestroyImmediate(root);}
        }
        AssetDatabase.SaveAssets();File.WriteAllLines(Output+"/cab-foot-contacts.tsv",rows);BuildAndVerify();
    }
    public static void BuildAndVerify()
    {
        Directory.CreateDirectory(Output+"/Bundles");
        var builds=Names.Select(n=>new AssetBundleBuild{assetBundleName=n=="rebirthCamper80s"?"rebirth_camper80s.unity3d":n=="rebirthBuggy"?"rebirth_buggy.unity3d":"rebirth_bike.unity3d",assetNames=new[]{Folder+"/"+n+".prefab"},addressableNames=new[]{n}}).ToArray();
        if(!BuildPipeline.BuildAssetBundles(Output+"/Bundles",builds,BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Build failed");
        var proof=new List<string>();
        for(int v=0;v<Names.Length;v++)
        {
            string path=Output+"/Bundles/"+builds[v].assetBundleName;
            if(!BuildPipeline.GetCRCForAssetBundle(path,out uint crc))throw new Exception("CRC absent");
            var bundle=AssetBundle.LoadFromFile(path,crc);if(!bundle)throw new Exception("CRC reopen failed");
            try
            {
                var expected=AssetDatabase.LoadAssetAtPath<GameObject>(Sources[v]);var actual=bundle.LoadAsset<GameObject>(Names[v]);
                var model=actual.transform.Find("GameObject");var markers=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("REBIRTH_Seat")).ToArray();
                int seats=v==0?6:v==1?2:1;if(markers.Length!=seats*4)throw new Exception("Marker count mismatch");
                for(int seat=0;seat<seats;seat++)foreach(string limb in new[]{"HandL","HandR","FootL","FootR"})
                    if(markers.Count(t=>t.name=="REBIRTH_Seat"+seat+"_"+limb)!=1)throw new Exception("Missing/ambiguous marker");
                // Ignore only the new empty marker transforms. Original ordered
                // hierarchy, local transforms, active state and renderer materials
                // must remain the same after adding contacts and bundling.
                var et=expected.GetComponentsInChildren<Transform>(true);
                var at=actual.GetComponentsInChildren<Transform>(true).Where(t=>!t.name.StartsWith("REBIRTHSeatContactTargets_")&&!t.name.StartsWith("REBIRTH_Seat")).ToArray();
                if(et.Length!=at.Length)throw new Exception("Original hierarchy changed");
                for(int i=0;i<et.Length;i++)if(et[i].name!=at[i].name||et[i].localPosition!=at[i].localPosition||Quaternion.Angle(et[i].localRotation,at[i].localRotation)>.001f||et[i].localScale!=at[i].localScale||et[i].gameObject.activeSelf!=at[i].gameObject.activeSelf)throw new Exception("Original transform changed "+et[i].name);
                var er=expected.GetComponentsInChildren<Renderer>(true);var ar=actual.GetComponentsInChildren<Renderer>(true);
                if(er.Length!=ar.Length)throw new Exception("Renderer count changed");
                for(int i=0;i<er.Length;i++)for(int m=0;m<er[i].sharedMaterials.Length;m++)
                {
                    var e=er[i].sharedMaterials[m];var a=ar[i].sharedMaterials[m];if(!e)continue;
                    if(!a||e.shader.name!=a.shader.name)throw new Exception("Material/shader changed");
                    foreach(string key in e.GetTexturePropertyNames())if(e.HasProperty(key))
                    {var x=e.GetTexture(key) as Texture2D;if(!x)continue;var y=a.GetTexture(key) as Texture2D;if(!y||x.width!=y.width||x.height!=y.height||x.format!=y.format)throw new Exception("Texture changed "+key);}
                }
                proof.Add("PASS "+Names[v]+" seats="+seats+" markers="+markers.Length+" CRC="+crc+" original hierarchy/material textures preserved; Unity verification only.");
            }
            finally{bundle.Unload(true);}
        }
        File.WriteAllLines(Output+"/verified.txt",proof);Debug.Log(string.Join("\n",proof));
    }
}
