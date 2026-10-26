using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

// Additive authoring from a measured UV/geometry receipt. Originals are never edited.
public static class RebirthCamperRearLampAuthoring
{
    public static void RunBatch()
    {
        string[] args=Environment.GetCommandLineArgs();
        string reportFolder=args[Array.IndexOf(args,"-camperLampReport")+1];
        string selection=args[Array.IndexOf(args,"-camperLampSelection")+1];
        string runId=args[Array.IndexOf(args,"-camperLampRunId")+1];
        if(!System.Text.RegularExpressions.Regex.IsMatch(runId,"^[a-f0-9]{32}$")) throw new Exception("Fresh run ID required.");
        Directory.CreateDirectory(reportFolder);
        string folder="Assets/REBIRTHCamperRearLampGenerated/"+runId;
        if(Directory.Exists(folder)) throw new Exception("Generated folder already exists.");
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        GameObject root=null;
        try
        {
            var measured=JsonUtility.FromJson<RebirthCamperRearLensReview.Result>(File.ReadAllText(selection));
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/REBIRTHCamperPlayableGenerated/8bfe6792d86a4397966e109c4ba17924/rebirthCamper80s.prefab");
            if(source==null) throw new Exception("Preserved candidate missing.");
            root=PrefabUtility.InstantiatePrefab(source) as GameObject;
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            Apply(root,folder,measured);
            string prefabPath=folder+"/rebirthCamperRearLampCandidate.prefab";
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            AssetDatabase.SaveAssets();
            var reopened=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var original=reopened.transform.Find(measured.rendererPath);
            int remainder=original.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
            int red=original.Find("RearRedLenses").GetComponent<MeshFilter>().sharedMesh.triangles.Length/3;
            var lights=reopened.transform.Find("M/RearLights").GetComponentsInChildren<Light>(true);
            if(red!=measured.rearTriangles || remainder+red!=measured.totalTriangles || lights.Length!=2
                || lights.Any(l=>l.enabled || l.intensity!=0 || l.color!=Color.red))
                throw new Exception("Reopened generated lamp candidate failed integrity checks.");
            File.WriteAllText(Path.Combine(reportFolder,"result.json"),
                "{\"status\":\"PASS_AUTHORED_LAMP_CANDIDATE_NOT_RUNTIME_TESTED\",\"prefab\":\""+prefabPath
                +"\",\"rearTriangles\":"+red+",\"remainingTriangles\":"+remainder+",\"redLights\":2,\"gameTested\":false}");
        }
        catch(Exception error) { File.WriteAllText(Path.Combine(reportFolder,"error.txt"),error.ToString());throw; }
        finally {if(root!=null) UnityEngine.Object.DestroyImmediate(root);}
    }

    public static void Apply(GameObject root, string assetFolder, RebirthCamperRearLensReview.Result measured)
    {
        if (root == null || measured == null || measured.status != "PASS_GEOMETRY_PROPOSAL_NOT_LIGHTING_ACCEPTANCE")
            throw new ArgumentException("Measured rear-lens selection required.");
        if (!assetFolder.StartsWith("Assets/REBIRTHCamper", StringComparison.Ordinal)
            || !AssetDatabase.IsValidFolder(assetFolder)) throw new ArgumentException("Owned generated folder required.");
        var original = root.transform.Find(measured.rendererPath);
        if (original == null || root.transform.Find("M/RearLights") != null)
            throw new InvalidOperationException("Original lamp path missing or rear lamps already authored.");
        var renderer = original.GetComponent<MeshRenderer>();
        var filter = original.GetComponent<MeshFilter>();
        if (renderer == null || filter == null || filter.sharedMesh == null || filter.sharedMesh.subMeshCount != 1
            || renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial.name != "80sCamperTruck_lights_glass")
            throw new InvalidOperationException("Measured single-material source lamp mesh required.");
        var mesh = filter.sharedMesh;
        var vertices = mesh.vertices;
        int[] source = mesh.GetTriangles(0);
        if (source.Length/3 != measured.totalTriangles) throw new InvalidOperationException("Lamp source topology changed.");
        var selected = new HashSet<int>();
        foreach (var triangle in measured.triangles.Where(t=>t.rearCandidate))
        {
            if (triangle.submesh != 0 || triangle.index < 0 || triangle.index >= source.Length/3 || !selected.Add(triangle.index))
                throw new InvalidOperationException("Invalid or duplicate measured triangle.");
            int offset=triangle.index*3;
            Vector3[] expected = {triangle.a,triangle.b,triangle.c};
            for (int corner=0;corner<3;corner++)
            {
                Vector3 actual=root.transform.InverseTransformPoint(original.TransformPoint(vertices[source[offset+corner]]));
                if ((actual-expected[corner]).sqrMagnitude > .00000001f)
                    throw new InvalidOperationException("Lamp geometry differs from its measured receipt.");
            }
        }
        if (selected.Count != measured.rearTriangles || selected.Count == 0)
            throw new InvalidOperationException("Rear selection count mismatch.");
        var remaining=new List<int>(); var red=new List<int>();
        for(int offset=0;offset<source.Length;offset+=3)
        {
            var list=selected.Contains(offset/3)?red:remaining;
            list.Add(source[offset]);list.Add(source[offset+1]);list.Add(source[offset+2]);
        }
        string basePath=assetFolder+"/RearLamp";
        foreach(string suffix in new[]{"Remainder.asset","Red.asset","Emission.mat"})
            if(AssetDatabase.LoadMainAssetAtPath(basePath+suffix)!=null)
                throw new InvalidOperationException("Generated lamp assets already exist; refuse overwrite.");
        Mesh remainder=Copy(mesh,remaining.ToArray(),"RearLampRemainder");
        Mesh lenses=Copy(mesh,red.ToArray(),"RearLampRed");
        var emission=new Material(renderer.sharedMaterial) {name="rebirthCamperRearRedEmission"};
        // The source lamp shader did not expose emission in the actual editor.
        // Assign Standard only to this generated material; keep source material intact.
        var shader=Shader.Find("Standard");
        if(shader==null) throw new InvalidOperationException("Standard lamp shader missing.");
        emission.shader=shader;
        if(!emission.HasProperty("_EmissionColor")) throw new InvalidOperationException("Source shader cannot emit.");
        emission.EnableKeyword("_EMISSION");
        emission.SetTexture("_EmissionMap",renderer.sharedMaterial.mainTexture);
        emission.SetColor("_EmissionColor",Color.black);
        emission.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(remainder,basePath+"Remainder.asset");
        AssetDatabase.CreateAsset(lenses,basePath+"Red.asset");
        AssetDatabase.CreateAsset(emission,basePath+"Emission.mat");
        filter.sharedMesh=remainder;
        var redObject=new GameObject("RearRedLenses");
        redObject.transform.SetParent(original,false);
        redObject.AddComponent<MeshFilter>().sharedMesh=lenses;
        var redRenderer=redObject.AddComponent<MeshRenderer>();
        redRenderer.sharedMaterial=emission;
        redRenderer.shadowCastingMode=renderer.shadowCastingMode;
        redRenderer.receiveShadows=renderer.receiveShadows;
        redObject.layer=original.gameObject.layer;
        var holder=new GameObject("RearLights");
        holder.transform.SetParent(root.transform.Find("M"),false);
        foreach(int side in new[]{-1,1})
        {
            var candidates=measured.triangles.Where(t=>t.rearCandidate
                && t.center.z<measured.rearMin.z+.05f && t.center.x*side>0).ToArray();
            if(candidates.Length==0) throw new InvalidOperationException("Rear bumper lens on each side required.");
            var lightObject=new GameObject(side<0?"LeftRed":"RightRed");
            lightObject.transform.SetParent(holder.transform,false);
            Vector3 position=new Vector3(candidates.Average(t=>t.center.x),candidates.Average(t=>t.center.y),measured.rearMin.z-.06f);
            lightObject.transform.position=root.transform.TransformPoint(position);
            lightObject.transform.rotation=root.transform.rotation*Quaternion.Euler(0,180,0);
            var light=lightObject.AddComponent<Light>();
            light.type=LightType.Spot; light.color=Color.red; light.range=12; light.spotAngle=90;
            light.intensity=0; light.shadows=LightShadows.None; light.enabled=false;
        }
        // Runtime code must bind brake/headlight/reverse state before these lamps work.
    }

    private static Mesh Copy(Mesh source,int[] indices,string name)
    {
        var clone=UnityEngine.Object.Instantiate(source);
        clone.name=name; clone.SetTriangles(indices,0,false);
        var vertices=clone.vertices;
        Bounds bounds=new Bounds(vertices[indices[0]],Vector3.zero);
        foreach(int index in indices) bounds.Encapsulate(vertices[index]);
        clone.bounds=bounds;
        return clone;
    }
}
