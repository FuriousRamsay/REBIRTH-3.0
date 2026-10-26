using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RebirthStandardVehicleSeatingReview
{
    [Serializable] public class SeatEvidence {public int pose;public string body;public Vector3 anchor,hips,leftHand,rightHand,leftTarget,rightTarget,leftTargetRotation,rightTargetRotation;public string[] clips,skins;public float leftHandError,rightHandError;public FootTarget[] feet;}
    [Serializable] public class FootTarget {public string side;public Vector3 position,rotation,support;public float soleHeightError;}
    [Serializable] public class Report {public string prefab,scope="Native pose/IK candidate; requires visual fit review";public SeatEvidence[] seats;public SteeringEvidence[] steering;}
    [Serializable] public class SteeringEvidence {public string body;public float input,leftError,rightError;}
    [Serializable] public class GripMesh {public string path;public Vector3[] vertices;public int[] triangles;}
    [Serializable] public class GripGeometry {public string scope="Rendered source mesh vertices near bike grips in vehicle coordinates";public GripMesh[] meshes;}
    [Serializable] public class MaterialFaces {public string material;public int[] triangles;}
    [Serializable] public class PassengerMesh {public string path;public Vector3[] vertices;public MaterialFaces[] submeshes;}
    [Serializable] public class PassengerGeometry {public string source;public PassengerMesh[] meshes;}
    public static void ExportBikePassengerGeometry()
    {
        const string output="E:/_Haven/shared/REBIRTHTemp/BikePassengerGeometry_v1_20261015.json";
        if(File.Exists(output))throw new Exception("Preserve previous geometry evidence");
        const string source="Assets/REBIRTHStandardVehiclesGenerated/BikeFinal_v1/rebirthBike.prefab";
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(source);var rows=new List<PassengerMesh>();
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=filter.GetComponent<Renderer>();var mesh=filter.sharedMesh;if(!renderer||!mesh)continue;
            var materials=renderer.sharedMaterials;var groups=new List<MaterialFaces>();
            for(int i=0;i<mesh.subMeshCount;i++)groups.Add(new MaterialFaces{material=i<materials.Length&&materials[i]?materials[i].name:"<missing>",triangles=mesh.GetTriangles(i)});
            rows.Add(new PassengerMesh{path=AnimationUtility.CalculateTransformPath(filter.transform,model.transform),vertices=mesh.vertices.Select(v=>model.transform.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray(),submeshes=groups.ToArray()});
        }
        File.WriteAllText(output,JsonUtility.ToJson(new PassengerGeometry{source=source,meshes=rows.ToArray()}));
    }
    public static void ExportBikeContactGeometry()
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHStandardVehiclesGenerated/BikeFinal_v1/rebirthBike.prefab");
        var meshes=model.GetComponentsInChildren<MeshFilter>(true).Select(f=>new GripMesh{path=AnimationUtility.CalculateTransformPath(f.transform,model.transform),vertices=f.sharedMesh.vertices.Select(v=>model.transform.InverseTransformPoint(f.transform.TransformPoint(v))).ToArray(),triangles=f.sharedMesh.triangles}).ToArray();
        File.WriteAllText("E:/_Haven/shared/REBIRTHTemp/BuggyBikeNativeFit_v16_20261015/bike-contact-geometry.json",JsonUtility.ToJson(new GripGeometry{scope="Full original bike rendered mesh geometry in vehicle coordinates; measure seat/foot supports before fitting",meshes=meshes}));
    }
    public static void Review(GameObject male,GameObject female,string output)
    {
        foreach(string kind in new[]{"Buggy","Bike"})
        {
            bool bike=kind=="Bike";
            // Native avatar templates are scene instances owned by the loader.
            // Keep that scene alive across both reviews; destroy only our copies.
            string path="Assets/REBIRTHStandardVehiclesGenerated/"+kind+(bike?"Final_v1/rebirth":"Final_v2/rebirth")+kind+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)throw new FileNotFoundException(path);
            var arguments=Environment.GetCommandLineArgs();int referenceIndex=Array.IndexOf(arguments,"-rebirthStandardReferenceFit");Report reference=null;
            if(referenceIndex>=0)
            {
                reference=JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(arguments[referenceIndex+1],kind,"result.json")));
                if(reference.prefab!=path||reference.seats.Length!=(bike?1:2))throw new Exception("Fixed fit reference/source mismatch");
            }
            var model=UnityEngine.Object.Instantiate(prefab);
            var owned=new List<UnityEngine.Object>{model};var rows=new List<SeatEvidence>();var sweep=new List<SteeringEvidence>();
            try
            {
                if(bike)
                {
                    var gripMeshes=new List<GripMesh>();
                    foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var points=filter.sharedMesh.vertices.Select(v=>model.transform.InverseTransformPoint(filter.transform.TransformPoint(v))).Where(v=>Mathf.Abs(v.x)>.30f&&Mathf.Abs(v.x)<.7f&&v.y>1.5f&&v.y<1.9f&&v.z>.25f&&v.z<.7f).ToArray();
                        if(points.Length>0)gripMeshes.Add(new GripMesh{path=AnimationUtility.CalculateTransformPath(filter.transform,model.transform),vertices=points});
                    }
                    string gripDirectory=Path.Combine(output,kind);Directory.CreateDirectory(gripDirectory);File.WriteAllText(Path.Combine(gripDirectory,"grip-geometry.json"),JsonUtility.ToJson(new GripGeometry{meshes=gripMeshes.ToArray()},true));
                }
                for(int seat=0;seat<(bike?1:2);seat++)
                {
                    bool useMale=(seat==0)^Environment.GetCommandLineArgs().Contains("-rebirthStandardSwapBodies");var body=UnityEngine.Object.Instantiate(useMale?male:female);owned.Add(body);body.SetActive(true);
                    body.transform.SetParent(model.transform,false);
                    RebirthCamperSeatingReview.AttachActualHead(body,useMale,owned);
                    var animator=body.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    animator.applyRootMotion=false;animator.Rebind();animator.Update(0);
                    int pose=bike?30:seat==0?40:41;
                    RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose);
                    var hips=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Hips");
                    Vector3 hipTarget=bike?new Vector3(0,1.23f,-.20f):new Vector3(seat==0?-.44f:.44f,1.44f,.20f);
                    body.transform.localPosition+=hipTarget-model.transform.InverseTransformPoint(hips.position);
                    if(reference!=null)body.transform.localPosition=reference.seats[seat].anchor;
                    var targets=new Dictionary<string,Transform>();
                    if(seat==0)
                    {
                        for(int hand=0;hand<2;hand++)
                        {
                            var target=new GameObject(hand==0?"IKHandL":"IKHandR");owned.Add(target);target.transform.SetParent(model.transform,false);
                            target.transform.localPosition=bike?new Vector3(hand==0?-.475f:.475f,1.695f,.485f):new Vector3(hand==0?-.608f:-.266f,1.679f,.271f);
                            target.transform.localRotation=Quaternion.Euler(bike?new Vector3(hand==0?-7.29f:4.15f,hand==0?77.3f:105.65f,hand==0?29.69f:-158.73f):new Vector3(0,hand==0?80:100,hand==0?30:-150));
                            targets.Add(target.name,target.transform);
                        }
                    }
                    if(reference!=null)
                    {
                        var row=reference.seats[seat];if(row.pose!=pose)throw new Exception("Native pose reference mismatch");
                        if(seat==0)foreach(bool left in new[]{true,false}){var target=targets[left?"IKHandL":"IKHandR"];target.localPosition=left?row.leftTarget:row.rightTarget;target.localEulerAngles=left?row.leftTargetRotation:row.rightTargetRotation;}
                        if(row.feet!=null)foreach(var foot in row.feet){var go=new GameObject(foot.side=="Left"?"IKFootL":"IKFootR");owned.Add(go);go.transform.SetParent(model.transform,false);go.transform.localPosition=foot.position;go.transform.localEulerAngles=foot.rotation;targets.Add(go.name,go.transform);}
                    }
                    var clips=RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,targets);
                    if(reference==null&&seat==0&&Environment.GetCommandLineArgs().Contains("-rebirthStandardCalibrateGrip"))
                    {
                        var rimCenter=new Vector3(-.437006f,1.679022f,.270551f);var axis=new Vector3(0,-.352771f,.935710f).normalized;
                        foreach(string side in new[]{"Left","Right"})
                        {
                            Vector3 gripAxis;RebirthCamperHandGripCalibration.Grip(body,model.transform,side,out gripAxis);
                            var radial=Vector3.ProjectOnPlane(side=="Left"?Vector3.left:Vector3.right,axis).normalized;var tangent=bike?Vector3.right:Vector3.Cross(axis,radial).normalized;if(Vector3.Dot(gripAxis,tangent)<0)tangent=-tangent;
                            // Measured bike grip rings x=.41711/.60996 share y1.69665,z.48489; brake lever vertices are excluded.
                            var desired=bike?new Vector3(side=="Left"?-.513535f:.513535f,1.69665f,.48489f):rimCenter+radial*.181652f;
                            var target=targets[side=="Left"?"IKHandL":"IKHandR"];
                            target.rotation=model.transform.rotation*Quaternion.FromToRotation(gripAxis,tangent)*Quaternion.Inverse(model.transform.rotation)*target.rotation;
                            RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,targets);
                            Vector3 interimAxis;var center=RebirthCamperHandGripCalibration.Grip(body,model.transform,side,out interimAxis);
                            target.position+=model.transform.TransformVector(desired-center);
                            RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,targets);
                            Vector3 finalAxis;var actual=RebirthCamperHandGripCalibration.Grip(body,model.transform,side,out finalAxis);
                            if(Vector3.Distance(actual,desired)>.003f)throw new Exception("Native finger grip center failed "+side);
                        }
                    }
                    var footRows=new List<FootTarget>();
                    if(reference!=null&&reference.seats[seat].feet!=null)foreach(var foot in reference.seats[seat].feet)
                    {
                        var actual=FootBounds(body,model.transform,foot.side=="Left");
                        footRows.Add(new FootTarget{side=foot.side,position=foot.position,rotation=foot.rotation,support=foot.support,soleHeightError=Mathf.Abs(actual.min.y-foot.support.y)});
                    }
                    if(reference==null&&bike&&Environment.GetCommandLineArgs().Contains("-rebirthStandardCalibrateFeet"))
                    {
                        foreach(string side in new[]{"Left","Right"})
                        {
                            var bones=body.GetComponentsInChildren<Transform>(true);var foot=bones.Single(t=>t.name==side+"Foot");var toe=bones.Single(t=>t.name==side+"ToeBase");
                            var target=new GameObject(side=="Left"?"IKFootL":"IKFootR");owned.Add(target);target.transform.SetParent(model.transform,false);target.transform.position=foot.position;
                            var forward=(toe.position-foot.position).normalized;var points=FootPoints(body,model.transform,side=="Left").Select(v=>model.transform.TransformPoint(v)).ToArray();var center=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;
                            // Find the width axis in the plane perpendicular to the
                            // toe direction. This also fixes sole roll, which a
                            // toe-direction-only rotation leaves unconstrained.
                            var width=Vector3.ProjectOnPlane(model.transform.right,forward).normalized;
                            for(int n=0;n<16;n++){var next=Vector3.zero;foreach(var point in points){var d=Vector3.ProjectOnPlane(point-center,forward);next+=d*Vector3.Dot(d,width);}if(next.sqrMagnitude<1e-12f)throw new Exception("Foot width frame degenerate");width=next.normalized;}
                            var normal=Vector3.Cross(forward,width).normalized;if(Vector3.Dot(normal,model.transform.up)<0)normal=-normal;
                            target.transform.rotation=Quaternion.LookRotation(model.transform.forward,model.transform.up)*Quaternion.Inverse(Quaternion.LookRotation(forward,normal))*foot.rotation;
                            targets.Add(target.name,target.transform);
                        }
                        RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,targets);
                        foreach(string side in new[]{"Left","Right"})
                        {
                            bool left=side=="Left";var target=targets[left?"IKFootL":"IKFootR"];var support=new Vector3(left?-.269f:.269f,.7772f,-.191f);
                            var footBounds=FootBounds(body,model.transform,left);
                            target.position+=model.transform.TransformVector(support-new Vector3(footBounds.center.x,footBounds.min.y,footBounds.center.z));
                            RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,targets);footBounds=FootBounds(body,model.transform,left);
                            float error=Mathf.Abs(footBounds.min.y-support.y);if(error>.003f)throw new Exception("Bike sole support height failed "+side+" "+error);
                            footRows.Add(new FootTarget{side=side,position=target.transform.localPosition,rotation=target.transform.localEulerAngles,support=support,soleHeightError=error});
                        }
                    }
                    if(seat==0&&arguments.Contains("-rebirthStandardSteeringSweep"))
                    {
                        var pivot=model.transform.Find(bike?"M/Forks":"M/Steering");var rest=pivot.localRotation;
                        var left=targets["IKHandL"];var right=targets["IKHandR"];
                        left.SetParent(pivot,true);right.SetParent(pivot,true);
                        foreach(float input in new[]{-1f,-.5f,0f,.5f,1f})
                        {
                            pivot.localRotation=rest*Quaternion.Euler(bike?new Vector3(0,30*input,0):new Vector3(0,0,-90*input));
                            RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,targets);
                            var bones=body.GetComponentsInChildren<Transform>(true);
                            sweep.Add(new SteeringEvidence{body=useMale?"native male":"native female",input=input,leftError=Vector3.Distance(bones.Single(t=>t.name=="LeftHand").position,left.position),rightError=Vector3.Distance(bones.Single(t=>t.name=="RightHand").position,right.position)});
                        }
                        pivot.localRotation=rest;left.SetParent(model.transform,true);right.SetParent(model.transform,true);
                        RebirthCamperAvatarProbe.EvaluateNativePoseSnapshot(body,pose,targets);
                    }
                    var skinDetails=new List<string>();
                    foreach(var skin in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        skin.enabled=true;skin.updateWhenOffscreen=true;
                        var baked=new Mesh();skin.BakeMesh(baked);skin.localBounds=baked.bounds;
                        skinDetails.Add(skin.name+" active="+skin.gameObject.activeInHierarchy+" vertices="+baked.vertexCount+" bounds="+baked.bounds+" scale="+skin.transform.lossyScale);
                        // Freeze evaluated geometry for a deterministic snapshot; this
                        // never replaces native animation on the delivered vehicle.
                        var snapshot=new GameObject("EvaluatedSkin_"+skin.name);owned.Add(snapshot);
                        snapshot.transform.SetParent(skin.transform,false);
                        snapshot.AddComponent<MeshFilter>().sharedMesh=baked;owned.Add(baked);
                        snapshot.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                        skin.enabled=false;
                    }
                    Func<string,Transform> bone=name=>body.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
                    rows.Add(new SeatEvidence {pose=pose,body=useMale?"native male":"native female",anchor=body.transform.localPosition,hips=model.transform.InverseTransformPoint(hips.position),clips=clips,skins=skinDetails.ToArray(),feet=footRows.ToArray(),
                        leftHand=model.transform.InverseTransformPoint(bone("LeftHand").position),rightHand=model.transform.InverseTransformPoint(bone("RightHand").position),
                        leftTarget=targets.ContainsKey("IKHandL")?model.transform.InverseTransformPoint(targets["IKHandL"].position):Vector3.zero,
                        rightTarget=targets.ContainsKey("IKHandR")?model.transform.InverseTransformPoint(targets["IKHandR"].position):Vector3.zero,
                        leftTargetRotation=targets.ContainsKey("IKHandL")?targets["IKHandL"].localEulerAngles:Vector3.zero,
                        rightTargetRotation=targets.ContainsKey("IKHandR")?targets["IKHandR"].localEulerAngles:Vector3.zero,
                        leftHandError=targets.ContainsKey("IKHandL")?Vector3.Distance(bone("LeftHand").position,targets["IKHandL"].position):0,
                        rightHandError=targets.ContainsKey("IKHandR")?Vector3.Distance(bone("RightHand").position,targets["IKHandR"].position):0});
                }
                var renderers=model.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
                var sun=new GameObject("Fit light").AddComponent<Light>();owned.Add(sun.gameObject);sun.type=LightType.Directional;sun.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(35,140,0);
                var camera=new GameObject("Fit camera").AddComponent<Camera>();owned.Add(camera.gameObject);
                camera.backgroundColor=new Color(.12f,.14f,.16f);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.transform.position=bounds.center+new Vector3(-1,.25f,.4f).normalized*bounds.size.magnitude*.9f;camera.transform.LookAt(bounds.center);
                string directory=Path.Combine(output,kind);Directory.CreateDirectory(directory);
                Capture(camera,Path.Combine(directory,"rider-fit.png"));
                camera.fieldOfView=45;
                camera.transform.position=model.transform.TransformPoint(bike?new Vector3(-1.6f,1.9f,.75f):new Vector3(-.44f,2.25f,1.05f));
                camera.transform.LookAt(model.transform.TransformPoint(bike?new Vector3(0,1.4f,.1f):new Vector3(-.4f,1.65f,.25f)));
                Capture(camera,Path.Combine(directory,"driver-close.png"));
                camera.fieldOfView=50;
                foreach(bool right in new[]{false,true})
                {
                    camera.transform.position=model.transform.TransformPoint(bike?new Vector3(right?1.8f:-1.8f,1.35f,-.15f):new Vector3(right?2.2f:-2.2f,1.65f,.1f));
                    camera.transform.LookAt(model.transform.TransformPoint(bike?new Vector3(0,1.05f,-.15f):new Vector3(0,1.4f,.15f)));
                    Capture(camera,Path.Combine(directory,right?"fit-right.png":"fit-left.png"));
                }
                camera.transform.position=model.transform.TransformPoint(bike?new Vector3(0,1.5f,-2.3f):new Vector3(0,1.8f,-2.6f));
                camera.transform.LookAt(model.transform.TransformPoint(bike?new Vector3(0,1.2f,-.1f):new Vector3(0,1.5f,.1f)));
                Capture(camera,Path.Combine(directory,"fit-rear.png"));
                File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Report {prefab=path,scope=reference==null?"Native pose/IK calibration candidate; requires visual fit review":"Fixed saved anchors/targets reused without recalibration on alternate native bodies; foot error measured anew; requires visual fit review",seats=rows.ToArray(),steering=sweep.ToArray()},true));
            }
            finally {foreach(var obj in owned.AsEnumerable().Reverse())if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);}
        }
    }
    static Bounds FootBounds(GameObject body,Transform model,bool left)
    {
        var points=FootPoints(body,model,left);var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);return bounds;
    }
    public static Quaternion LevelFootRotation(GameObject body,Transform model,string side,Vector3 desiredForward)
    {
        var bones=body.GetComponentsInChildren<Transform>(true);var foot=bones.Single(t=>t.name==side+"Foot");var toe=bones.Single(t=>t.name==side+"ToeBase");var forward=(toe.position-foot.position).normalized;
        var points=FootPoints(body,model,side=="Left").Select(v=>model.TransformPoint(v)).ToArray();var center=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;var width=Vector3.ProjectOnPlane(body.transform.right,forward).normalized;
        var toeDirection=forward;
        for(int n=0;n<24;n++){var next=Vector3.zero;foreach(var p in points){var d=p-center;next+=d*Vector3.Dot(d,forward);}if(next.sqrMagnitude<1e-12f)throw new Exception("Foot length frame degenerate");forward=next.normalized;}
        if(Vector3.Dot(forward,toeDirection)<0)forward=-forward;width=Vector3.ProjectOnPlane(body.transform.right,forward).normalized;
        for(int n=0;n<16;n++){var next=Vector3.zero;foreach(var p in points){var d=Vector3.ProjectOnPlane(p-center,forward);next+=d*Vector3.Dot(d,width);}if(next.sqrMagnitude<1e-12f)throw new Exception("Foot frame degenerate");width=next.normalized;}
        var normal=Vector3.Cross(forward,width).normalized;if(Vector3.Dot(normal,model.up)<0)normal=-normal;
        return Quaternion.LookRotation(desiredForward,model.up)*Quaternion.Inverse(Quaternion.LookRotation(forward,normal))*foot.rotation;
    }
    static Vector3[] FootPoints(GameObject body,Transform model,bool left)
    {
        var skin=body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="feet");var mesh=new Mesh();
        try{skin.BakeMesh(mesh);var points=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).Where(v=>left?body.transform.InverseTransformPoint(v).x<0:body.transform.InverseTransformPoint(v).x>0).Select(v=>model.InverseTransformPoint(v)).ToArray();if(points.Length<50)throw new Exception("Native foot geometry absent");return points;}
        finally{UnityEngine.Object.DestroyImmediate(mesh);}
    }
    static void Capture(Camera camera,string path)
    {
        var target=new RenderTexture(1280,900,24);var pixels=new Texture2D(1280,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}




