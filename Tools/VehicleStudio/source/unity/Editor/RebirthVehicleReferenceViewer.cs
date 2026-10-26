// Copy into YOUR OWN Unity tooling project under Assets/Editor/.
// Read-only Scene overlay: this does not create/change a 7DTD rig or save a prefab.
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RebirthVehicleInspector.UnityEditorTools
{
    public sealed class RebirthVehicleReferenceViewer : EditorWindow
    {
        [Serializable] private class V3 {public float x,y,z;public Vector3 Vector=>new Vector3(x,y,z);}
        [Serializable] private class Pose {public V3 worldPosition,vehiclePosition,localPosition;}
        [Serializable] private class Pair {public string key,value;}
        [Serializable] private class Part {public string className;public Pair[] properties;}
        [Serializable] private class Seat {public int index;public string pose,position,rotation,exit,mod,modT;}
        [Serializable] private class XmlConfig {public string name,status,prefab;public Seat[] seats;public Part[] parts;}
        [Serializable] private class Bone {public string bone,source;public Pose pose;}
        [Serializable] private class Occupant {public int entityId,reportedSeatIndex;public string association,entityType;public Bone[] bones;}
        [Serializable] private class Track {public string path;public Pose pose;}
        [Serializable] private class Frame {public float secondsFromStart;public Track[] trackedNodes;public Occupant[] occupantCandidates;}
        [Serializable] private class Capture {public int schemaVersion;public string capturedUtc,gameVersion,captureType;public XmlConfig xmlConfig;public Frame[] frames;}

        private Capture capture;
        private Transform modelRoot;
        private string capturePath;
        private int frameIndex;
        private bool showSeats=true,showIK=true,showBones=true,showTracked=false;
        private string warning;
        private Vector2 scroll;

        [MenuItem("Tools/REBIRTH/7DTD Vehicle Reference Viewer")]
        public static void Open() {GetWindow<RebirthVehicleReferenceViewer>("7DTD Vehicle Reference");}

        private void OnEnable() {SceneView.duringSceneGui+=OnSceneView;}
        private void OnDisable() {SceneView.duringSceneGui-=OnSceneView;}

        private void OnGUI()
        {
            EditorGUILayout.LabelField("REBIRTH — 7DTD reference viewer",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Read-only overlays from runtime JSON. The coordinates are relative to the " +
                "inspector's resolved runtime root. Set the matching FBX root manually. No prefab changes are made.",MessageType.Info);
            if(GUILayout.Button("Open runtime capture (.json)")) Load();
            EditorGUILayout.LabelField("Capture",string.IsNullOrEmpty(capturePath)?"None":Path.GetFileName(capturePath));
            modelRoot=(Transform)EditorGUILayout.ObjectField("Model Root (scene)",modelRoot,typeof(Transform),true);
            if(!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning,MessageType.Warning);
            if(capture==null) return;
            EditorGUILayout.LabelField("Version",capture.gameVersion??"unknown");
            EditorGUILayout.LabelField("XML",capture.xmlConfig?.name??"unbound");
            if(capture.frames!=null && capture.frames.Length>1)
                frameIndex=EditorGUILayout.IntSlider("Recorded frame",frameIndex,0,capture.frames.Length-1);
            if(capture.frames!=null && capture.frames.Length>0)
                EditorGUILayout.LabelField("Seconds",capture.frames[frameIndex].secondsFromStart.ToString("F2",CultureInfo.InvariantCulture));
            showSeats=EditorGUILayout.ToggleLeft("Draw XML seat anchors",showSeats);
            showIK=EditorGUILayout.ToggleLeft("Draw XML steering hand / pedal foot IK",showIK);
            showBones=EditorGUILayout.ToggleLeft("Draw observed character bones (may be nearby)",showBones);
            showTracked=EditorGUILayout.ToggleLeft("Draw tracked moving transform markers",showTracked);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(var seat in capture.xmlConfig?.seats??Array.Empty<Seat>())
                EditorGUILayout.LabelField("seat"+seat.index+" · pose "+(seat.pose??"unspecified")+" · "+(seat.position??"?"));
            EditorGUILayout.EndScrollView();
            if(GUI.changed) SceneView.RepaintAll();
        }
        private void Load()
        {
            string file=EditorUtility.OpenFilePanel("Select REBIRTH vehicle capture","","json");
            if(string.IsNullOrEmpty(file)) return;
            try
            {
                var parsed=JsonUtility.FromJson<Capture>(File.ReadAllText(file));
                if(parsed==null || parsed.schemaVersion!=1 || parsed.frames==null || parsed.frames.Length==0)
                    throw new Exception("Invalid or unsupported REBIRTH capture format.");
                capture=parsed;capturePath=file;frameIndex=0;
                warning=(parsed.xmlConfig==null || parsed.xmlConfig.status!="ok")
                    ?"Vehicle XML is unbound, so seat/IK anchors may be unavailable.":null;
                SceneView.RepaintAll();
            }
            catch(Exception ex) {warning="Failed to load JSON: "+ex.Message;}
        }
        private void OnSceneView(SceneView scene)
        {
            if(modelRoot==null || capture==null) return;
            if(showSeats)
            {
                foreach(var seat in capture.xmlConfig?.seats??Array.Empty<Seat>())
                {
                    if(!TryVec(seat.position,out Vector3 at)) continue;
                    Vector3 world=modelRoot.TransformPoint(at);
                    Handles.color=seat.index==0?Color.cyan:Color.yellow;
                    Handles.DrawWireDisc(world,modelRoot.up,0.16f);
                    Quaternion facing=TryVec(seat.rotation,out Vector3 angles)
                        ?modelRoot.rotation*Quaternion.Euler(angles):modelRoot.rotation;
                    Handles.ArrowHandleCap(0,world,facing,0.45f,EventType.Repaint);
                    Handles.Label(world+Vector3.up*0.18f,"seat"+seat.index+" · pose "+(seat.pose??"?"));
                }
            }
            if(showIK)
            {
                foreach(var part in capture.xmlConfig?.parts??Array.Empty<Part>())
                {
                    if(part.className!="handlebars" && part.className!="pedals") continue;
                    string transformPath=ReadProperty(part,"transform");
                    Transform pivot=string.IsNullOrEmpty(transformPath)?null:FindPivot(transformPath);
                    // Never substitute vehicle root for a missing IK pivot: wrong spatial data can look plausible.
                    if(pivot==null) continue;
                    DrawIK(pivot,part,"IKHandLPosition",new Color(0.18f,0.87f,0.37f),"L hand");
                    DrawIK(pivot,part,"IKHandRPosition",new Color(0.18f,0.87f,0.37f),"R hand");
                    DrawIK(pivot,part,"IKFootLPosition",Color.magenta,"L foot");
                    DrawIK(pivot,part,"IKFootRPosition",Color.magenta,"R foot");
                }
            }
            Frame frame=capture.frames!=null && capture.frames.Length>0 ? capture.frames[Mathf.Clamp(frameIndex,0,capture.frames.Length-1)] : null;
            if(frame==null) return;
            if(showBones)
            {
                foreach(var occupant in frame.occupantCandidates??Array.Empty<Occupant>())
                {
                    foreach(var bone in occupant.bones??Array.Empty<Bone>())
                    {
                        if(bone.pose?.vehiclePosition==null) continue;
                        Vector3 at=modelRoot.TransformPoint(bone.pose.vehiclePosition.Vector);
                        Handles.color=bone.bone.Contains("Hand")?Color.green:bone.bone.Contains("Foot")?Color.magenta:Color.white;
                        Handles.SphereHandleCap(0,at,Quaternion.identity,0.055f,EventType.Repaint);
                        Handles.Label(at,"#"+occupant.entityId+" ["+(occupant.association??"unknown")+"] "+bone.bone);
                    }
                }
            }
            if(showTracked)
            {
                foreach(var node in frame.trackedNodes??Array.Empty<Track>())
                {
                    if(node.pose?.vehiclePosition==null) continue;
                    Vector3 at=modelRoot.TransformPoint(node.pose.vehiclePosition.Vector);
                    Handles.color=Color.grey;
                    Handles.DrawWireDisc(at,modelRoot.up,0.09f);
                    Handles.Label(at,node.path);
                }
            }
        }
        private void DrawIK(Transform origin,Part part,string field,Color color,string label)
        {
            if(!TryVec(ReadProperty(part,field),out Vector3 p)) return;
            Vector3 at=origin.TransformPoint(p);Handles.color=color;
            Handles.SphereHandleCap(0,at,Quaternion.identity,0.12f,EventType.Repaint);
            Handles.Label(at,label+" (XML "+part.className+")");
        }
        private Transform FindPivot(string path)
        {
            if(modelRoot==null || string.IsNullOrEmpty(path)) return null;
            path=path.Trim('/');
            Transform result=modelRoot.Find(path);
            if(result!=null) return result;
            if(path==modelRoot.name) return modelRoot;
            string prefix=modelRoot.name+"/";
            return path.StartsWith(prefix,StringComparison.Ordinal)?modelRoot.Find(path.Substring(prefix.Length)):null;
        }
        private static string ReadProperty(Part p,string key)
        {
            foreach(var prop in p.properties??Array.Empty<Pair>()) if(prop.key==key) return prop.value;
            return null;
        }
        private static bool TryVec(string text,out Vector3 value)
        {
            value=default(Vector3);
            if(string.IsNullOrEmpty(text)) return false;
            var parts=text.Split(',');
            if(parts.Length!=3) return false;
            if(!float.TryParse(parts[0],NumberStyles.Float,CultureInfo.InvariantCulture,out float x) ||
               !float.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float y) ||
               !float.TryParse(parts[2],NumberStyles.Float,CultureInfo.InvariantCulture,out float z)) return false;
            value=new Vector3(x,y,z);return true;
        }
    }
}
