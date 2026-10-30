using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

public static class RebirthCamperSeatsBuild
{
    static string Source=>Arg("-rebirthSeatSource","Assets/REBIRTHCamperSeatingGenerated/3a47983a5166470eb9e0b4ffc215e031/rebirthCamper80s.prefab");
    const string Baseline="Assets/REBIRTHCamperPlate_20261015/rebirthCamper80s.prefab";
    static string Output=>Arg("-rebirthSeatOutput","E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/BundleSixSeats");
    static string Arg(string name,string fallback){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:fallback;}
    public static void BuildOnly()
    {
        if(File.Exists(Output+"/rebirth_camper80s.unity3d"))throw new Exception("Refuse overwrite of preserved candidate");
        Validate(AssetDatabase.LoadAssetAtPath<GameObject>(Source),AssetDatabase.LoadAssetAtPath<GameObject>(Baseline));Directory.CreateDirectory(Output);ExportSourceMeshes();
        if(!RebirthAssetBundlePackaging.BuildAssetBundles(Output,new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Source}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Bundle build failed");
        File.WriteAllText(Output+"/build-only.txt","Built candidate from "+Source+". Independent geometry decode/comparison and VerifyExisting are still required. Not installed.");
    }
    [Serializable] public class SourceMesh {public string name,path;public Vector3[] vertices;public int[] triangles;public Vector2[] uv;}
    [Serializable] public class SourceMeshes {public SourceMesh[] meshes;}
    public static void ExportSourceMeshes()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        var rows=source.GetComponentsInChildren<MeshFilter>(true).Select(f=>new SourceMesh{name=f.sharedMesh.name,path=AnimationUtility.CalculateTransformPath(f.transform,source.transform),vertices=f.sharedMesh.vertices,triangles=f.sharedMesh.triangles,uv=f.sharedMesh.uv}).ToArray();
        if(rows.Any(r=>r.vertices.Length==0||r.triangles.Length==0))throw new Exception("Source geometry export incomplete");
        File.WriteAllText(Output+"/source-meshes.json",JsonUtility.ToJson(new SourceMeshes{meshes=rows}));
    }
    public static void MeasureFront()
    {
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        var model=root.transform.Find("GameObject");var lines=new System.Collections.Generic.List<string>();
        var f=root.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="interior");
        var v=f.sharedMesh.vertices.Select(p=>model.InverseTransformPoint(f.transform.TransformPoint(p))).ToArray();var ids=f.sharedMesh.triangles;
        foreach(float x in new[]{-.60f,-.413f,-.23f,.23f,.413f,.60f})foreach(float z in new[]{-.15f,0f,.25f,.45f,.55f,.65f,.75f})
        {
            var hits=new System.Collections.Generic.List<float>();
            for(int n=0;n<ids.Length;n+=3){var a=v[ids[n]];var b=v[ids[n+1]];var c=v[ids[n+2]];float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<.000001f)continue;float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/d;float w=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/d;if(u<0||w<0||u+w>1)continue;hits.Add(u*a.y+w*b.y+(1-u-w)*c.y);}
            lines.Add("x="+x+" z="+z+" surfacesY="+string.Join(",",hits.Distinct().OrderBy(y=>y).Select(y=>y.ToString("F4"))));
        }
        File.WriteAllLines("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front-surfaces.txt",lines);Object.DestroyImmediate(root);
    }
    public static void Run()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        var baseline=AssetDatabase.LoadAssetAtPath<GameObject>(Baseline);
        if(!source||!baseline)throw new Exception("Missing exact source assets");
        Validate(source,baseline);
        Directory.CreateDirectory(Output);
        if(File.Exists(Output+"/rebirth_camper80s.unity3d"))throw new Exception("Preserve existing bundle; select a new output folder before rebuilding");
        if(!RebirthAssetBundlePackaging.BuildAssetBundles(Output,new[]{new AssetBundleBuild{assetBundleName="rebirth_camper80s.unity3d",assetNames=new[]{Source}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64))throw new Exception("Bundle build failed");
        VerifyExisting();
    }
    public static void VerifyExisting()
    {
        var baseline=AssetDatabase.LoadAssetAtPath<GameObject>(Baseline);
        Validate(AssetDatabase.LoadAssetAtPath<GameObject>(Source),baseline);
        var bundle=AssetBundle.LoadFromFile(Output+"/rebirth_camper80s.unity3d");
        if(!bundle)throw new Exception("Bundle reopen failed");
        var root=Object.Instantiate(bundle.LoadAsset<GameObject>("rebirthCamper80s"));
        try
        {
            Validate(root,baseline,false);
            RebirthCamperCampIntegration.SetMode(root,true);
            string entry=RebirthCamperDefectAudit.Walk(root,1.8f,.1f);
            if(!entry.Contains("entry=True"))throw new Exception(entry);
            File.WriteAllText(Output+"/verified.txt","PASS: source and reopened bundle retain 21 interaction targets, four wheels, original rigidbody properties, collider geometry, production visibility defaults, unchanged unrelated meshes/materials, planned76-vertex cab upholstery correction,60mm steering offset, native or generic seat dependencies as required by this source; standing entry passes.\n"+entry+"\nUnity only; no game or multiplayer testing. All paired seat/hand XML deltas and selective native constraint runtime support required.");
        }
        finally{Object.DestroyImmediate(root);bundle.Unload(true);}
    }
    static void Validate(GameObject root,GameObject baseline,bool inspectGeometry=true)
    {
        bool nativeProfile=root.transform.Find("GameObject/M/REBIRTHNativeSeatIKProfiles")!=null;
        if(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length!=0)throw new Exception("Review avatar leaked into bundle");
        if(root.GetComponentsInChildren<WheelCollider>(true).Length!=4)throw new Exception("Wheel count changed");
        var targets=root.GetComponentsInChildren<Collider>(true).Where(c=>c.name.StartsWith("REBIRTHInteraction_")).ToArray();
        var oldTargets=baseline.GetComponentsInChildren<Collider>(true).Where(c=>c.name.StartsWith("REBIRTHInteraction_")).ToArray();
        if(targets.Length!=21||!targets.Select(c=>c.name).OrderBy(n=>n).SequenceEqual(oldTargets.Select(c=>c.name).OrderBy(n=>n)))throw new Exception("Interaction contract changed");
        foreach(var rb in baseline.GetComponentsInChildren<Rigidbody>(true))
        {
            var path=AnimationUtility.CalculateTransformPath(rb.transform,baseline.transform);
            var t=path.Length==0?root.transform:root.transform.Find(path);var b=t.GetComponent<Rigidbody>();
            if(b.mass!=rb.mass||b.isKinematic!=rb.isKinematic||b.constraints!=rb.constraints||b.useGravity!=rb.useGravity||b.drag!=rb.drag||b.angularDrag!=rb.angularDrag)throw new Exception("Rigidbody changed "+path);
        }
        var colliders=root.GetComponentsInChildren<Collider>(true);var original=baseline.GetComponentsInChildren<Collider>(true);
        if(colliders.Length!=original.Length)throw new Exception("Collider count changed");
        for(int index=0;index<original.Length;index++)
        {
            var c=original[index]; string path=AnimationUtility.CalculateTransformPath(c.transform,baseline.transform);
            var dst=root.transform.Find(path);if(path.Length==0)dst=root.transform;
            var b=colliders[index];
            if(!b||b.enabled!=c.enabled||b.isTrigger!=c.isTrigger||b.gameObject.layer!=c.gameObject.layer||(b.sharedMaterial?b.sharedMaterial.name:"")!=(c.sharedMaterial?c.sharedMaterial.name:""))throw new Exception("Collider state changed "+path);
            if(c is BoxCollider a){var d=(BoxCollider)b;if(a.center!=d.center||a.size!=d.size)throw new Exception("Box changed "+path);}
            if(c is MeshCollider m){var d=(MeshCollider)b;if(m.sharedMesh.name!=d.sharedMesh.name||m.convex!=d.convex)throw new Exception("Mesh collider changed "+path);}
        }
        foreach(string name in new[]{"REBIRTHCampRoofStations","REBIRTHCampInteriorStations","REBIRTHCampPowerEquipment","REBIRTHCampStairVisuals","REBIRTHCampWalkInColliders","Fitted rooftop guardrails"})
        {
            var a=baseline.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);var b=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
            if(a.gameObject.activeSelf!=b.gameObject.activeSelf)throw new Exception("Visibility default changed "+name);
        }
        if(inspectGeometry)
        {
        foreach(var f in baseline.GetComponentsInChildren<MeshFilter>(true))
        {
            var path=AnimationUtility.CalculateTransformPath(f.transform,baseline.transform);var d=root.transform.Find(path).GetComponent<MeshFilter>();
            if(f.name!="camper_interior"&&f.name!="interior"&&(f.sharedMesh.name!=d.sharedMesh.name||!Vertices(f.sharedMesh).SequenceEqual(Vertices(d.sharedMesh))||!Indices(f.sharedMesh).SequenceEqual(Indices(d.sharedMesh))))throw new Exception("Unrelated mesh changed "+path);
        }
        var front=root.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="interior");var before=baseline.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="interior");
        if(!UV(front.sharedMesh).SequenceEqual(UV(before.sharedMesh))||!Indices(front.sharedMesh).SequenceEqual(Indices(before.sharedMesh)))throw new Exception("Cab UVs or topology changed");
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText("E:/_Haven/shared/REBIRTHTemp/CamperSeats_20261015/front-cushion-plan.json"));var model=root.transform.Find("GameObject");var oldModel=baseline.transform.Find("GameObject");var oldVertices=Vertices(before.sharedMesh);var newVertices=Vertices(front.sharedMesh);
        if(oldVertices.Length!=newVertices.Length||plan.vertexIds.Length!=76)throw new Exception("Cab layout changed");
        for(int i=0;i<newVertices.Length;i++){int at=Array.IndexOf(plan.vertexIds,i);var expected=at<0?oldModel.InverseTransformPoint(before.transform.TransformPoint(oldVertices[i])):plan.after[at];if(nativeProfile&&at>=0){if(expected.z<-.15f)expected.z-=.08f;else if(expected.y<.9f)expected.y-=expected.x>0?.025f:.015f;}var actual=model.InverseTransformPoint(front.transform.TransformPoint(newVertices[i]));if(Vector3.Distance(expected,actual)>.00001f)throw new Exception("Unexpected cab geometry delta "+i);}
        }
        else
        {
            var receipt=JsonUtility.FromJson<GeometryReceipt>(File.ReadAllText(Output+"/geometry-comparison.json"));
            using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=File.OpenRead(Output+"/rebirth_camper80s.unity3d"))
            if(receipt.status!="PASS_EMBEDDED"||receipt.bundleSha256!=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant())throw new Exception("Independent exact bundle geometry comparison absent or stale");
        }
        if(!inspectGeometry)foreach(var f in baseline.GetComponentsInChildren<MeshFilter>(true).Where(f=>new[]{"Cube","Cylinder"}.Contains(f.sharedMesh.name)))
        {
            var path=AnimationUtility.CalculateTransformPath(f.transform,baseline.transform);var actual=root.transform.Find(path).GetComponent<MeshFilter>().sharedMesh;
            if(actual!=f.sharedMesh||!actual.vertices.SequenceEqual(f.sharedMesh.vertices)||!actual.triangles.SequenceEqual(f.sharedMesh.triangles)||!actual.uv.SequenceEqual(f.sharedMesh.uv))throw new Exception("Builtin mesh changed "+path);
        }
        var steering=root.transform.Find("GameObject/M/SteeringWheel");var originalSteering=baseline.transform.Find("GameObject/M/SteeringWheel");if(Vector3.Distance(steering.localPosition,originalSteering.localPosition+Vector3.forward*.06f)>.00001f||Quaternion.Angle(steering.localRotation,originalSteering.localRotation)>.001f)throw new Exception("Unexpected steering transform delta");
        var oldRenderers=baseline.GetComponentsInChildren<Renderer>(true);var renderers=root.GetComponentsInChildren<Renderer>(true);if(oldRenderers.Length!=renderers.Length)throw new Exception("Renderer count changed");
        for(int i=0;i<renderers.Length;i++){var a=oldRenderers[i].sharedMaterials;var b=renderers[i].sharedMaterials;if(a.Length!=b.Length)throw new Exception("Material slot count changed");for(int j=0;j<a.Length;j++)if((a[j]?a[j].name:"")!=(b[j]?b[j].name:"")||(a[j]?a[j].shader.name:"")!=(b[j]?b[j].shader.name:""))throw new Exception("Unrelated material changed");}
        if(nativeProfile)
        {
            if(root.GetComponentsInChildren<Animator>(true).Length!=0)throw new Exception("Native candidate must not depend on custom pose Animators");
            var holder=root.transform.Find("GameObject/M/REBIRTHNativeSeatIKProfiles");if(holder.childCount!=6||holder.GetComponentsInChildren<Renderer>(true).Length!=0||holder.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("Native profile hierarchy invalid");
            for(int slot=0;slot<6;slot++)
            {
                int pose=slot==0?40:slot==4?0:41;var seat=holder.Find("Seat"+slot+"_Pose"+pose);if(!seat||seat.childCount!=6)throw new Exception("Native seat profile absent/incomplete "+slot);
                foreach(string target in new[]{"IKHandL","IKHandR","IKFootL","IKFootR","IKKneeL","IKKneeR"})if(!seat.Find(target))throw new Exception("Native target absent "+slot+" "+target);
            }
        }
        else foreach(int slot in new[]{0,1,2,3,4,5})
        {
            var holder=root.transform.Find("GameObject/M/"+(slot==4?"BedPoseAsset":"CabinPoseAssets/Seat"+slot));
            var animator=holder==null?null:holder.GetComponent<Animator>();
            if(animator==null||animator.enabled||holder.Find("NativeRigLayout")==null)throw new Exception("Pose holder invalid "+slot);
            var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
            if(clips.Length!=1||clips[0].legacy||clips[0].humanMotion)throw new Exception("Generic clip invalid "+slot);
        }
    }
    // Packaged non-readable meshes are independently decoded, never forced
    // through MeshData: that path crashed Unity2022.3 for this bundle.
    static Vector3[] Vertices(Mesh mesh){return mesh.vertices;}
    static Vector2[] UV(Mesh mesh){return mesh.uv;}
    static int[] Indices(Mesh mesh){return mesh.triangles;}
    [Serializable] public class GeometryReceipt {public string status,bundleSha256;}
    [Serializable] public class Plan {public int[] vertexIds;public Vector3[] after;}
}






