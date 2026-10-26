using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

// Imports only lower-detail variants. Original renderers, source mesh assets,
// native colliders and animation transforms are retained as LOD0.
public static class RebirthCamperStationLodImport
{
    const string Output="E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output";
    const string Folder=RebirthCamperTurretProduction.Folder+"/WithLods";
    [Serializable] sealed class Level {public int level,triangles;public string file;public float distanceP95,distanceMax;}
    [Serializable] sealed class Entry {public string key,asset,name,guid,armorSurfaceName;public long localId;public int sourceTriangles;public bool smallDetail,armorAware;public Level[] levels;}
    [Serializable] sealed class Manifest {public int schema;public bool complete;public string inputSha256,armorInputSha256,armorPrefabSha256;public Entry[] entries;}
    [Serializable] sealed class ArmorRange {public int order,threshold,start,count;}
    [Serializable] sealed class MeshData
    {public Vector3[] vertices,normals;public Vector2[] uv,uv2;public Color[] colors;public RebirthCamperTurretProduction.Indices[] submeshes;public ArmorRange[] armorRanges;}
    static string Identity(Mesh mesh)
    {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long id);return guid+":"+id+":"+AssetDatabase.GetAssetPath(mesh)+":"+mesh.name;}
    static string Identity(Entry entry)=>entry.guid+":"+entry.localId+":"+entry.asset+":"+entry.name;
    static Mesh Import(Level level,string key,int expectedSubmeshes,out ArmorRange[] armorRanges)
    {
        if(level.level<1||level.level>2||Path.GetFileName(level.file)!=level.file)throw new Exception("Invalid lower LOD identity");
        var data=JsonUtility.FromJson<MeshData>(File.ReadAllText(Output+"/CamperStationLods/"+level.file));
        armorRanges=data.armorRanges??Array.Empty<ArmorRange>();
        if(data.vertices==null||data.normals==null||data.vertices.Length!=data.normals.Length||data.submeshes.Length!=expectedSubmeshes||data.submeshes.Sum(s=>s.triangles.Length/3)!=level.triangles)throw new Exception("Incomplete lower LOD mesh: "+key);
        var mesh=new Mesh{name=key+"_LOD"+level.level,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.vertices=data.vertices;mesh.normals=data.normals;
        if(data.uv?.Length==data.vertices.Length)mesh.uv=data.uv;
        if(data.uv2?.Length==data.vertices.Length)mesh.uv2=data.uv2;
        if(data.colors?.Length==data.vertices.Length)mesh.colors=data.colors;
        mesh.subMeshCount=data.submeshes.Length;
        for(int i=0;i<data.submeshes.Length;i++)
        {if(data.submeshes[i].triangles.Any(index=>index<0||index>=data.vertices.Length))throw new Exception("Invalid lower LOD index");mesh.SetTriangles(data.submeshes[i].triangles,i);}
        mesh.RecalculateBounds();if(mesh.uv.Length==mesh.vertexCount)mesh.RecalculateTangents();
        string path=Folder+"/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    public static void Run()
    {
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Output+"/CamperStationLods/manifest.json"));
        string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Output+"/camper-lod-input.json"))).Replace("-","").ToLowerInvariant();
        if(manifest.schema!=1||!manifest.complete||manifest.inputSha256!=hash)throw new Exception("Incomplete/stale camper LOD generation refused");
        if(manifest.armorInputSha256!=Hash(Output+"/camper-armor-lod-ranges.json")||manifest.armorPrefabSha256!=Hash(RebirthCamperTurretProduction.Folder+"/rebirthCamper80s.prefab"))throw new Exception("Current damage-aware armor LOD generation required");
        Directory.CreateDirectory(Folder);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(RebirthCamperTurretProduction.Folder+"/rebirthCamper80s.prefab");if(!source)throw new Exception("Actual production staging must run first");
        var root=Object.Instantiate(source);root.name="rebirthCamper80s";var lines=new List<string>();
        try
        {
            var covered=new HashSet<Renderer>(root.GetComponentsInChildren<LODGroup>(true).SelectMany(g=>g.GetLODs()).SelectMany(l=>l.renderers));
            var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<MeshRenderer>()&&!covered.Contains(f.GetComponent<MeshRenderer>())).ToArray();
            var groups=filters.GroupBy(f=>Identity(f.sharedMesh)).ToDictionary(g=>g.Key,g=>g.ToArray());
            int variants=0,detailCull=0;
            foreach(var entry in manifest.entries)
            {
                if(!groups.TryGetValue(Identity(entry),out var matches))throw new Exception("LOD input no longer belongs to staged prefab: "+entry.key);
                var original=matches[0].sharedMesh;
                if(Enumerable.Range(0,original.subMeshCount).Sum(s=>(int)original.GetIndexCount(s)/3)!=entry.sourceTriangles)throw new Exception("LOD0 source changed: "+entry.key);
                var lower=new Dictionary<int,Mesh>();
                var damageRanges=new Dictionary<int,ArmorRange[]>();
                bool armor=matches.Any(f=>f.name.StartsWith("REBIRTHArmorSurface_",StringComparison.Ordinal));
                if(armor&&(!entry.armorAware||matches.Any(f=>f.name!=entry.armorSurfaceName)))throw new Exception("Armor damage ownership must be preserved: "+entry.key);
                if(!entry.smallDetail)
                {
                    if(entry.levels.Length!=2||entry.levels.Select(l=>l.level).Distinct().Count()!=2||entry.levels.Any(l=>l.triangles<=0||l.triangles>=entry.sourceTriangles))throw new Exception("Meaningful reduced LOD1/2 required: "+entry.key);
                    foreach(var level in entry.levels)
                    {
                        lower[level.level]=Import(level,entry.key,original.subMeshCount,out var ranges);damageRanges[level.level]=ranges;
                        if(armor)
                        {
                            var expected=matches[0].transform.Cast<Transform>().Where(t=>t.name.StartsWith("ArmorRange_")).Select(t=>t.name.Split('_')).Select(f=>f[1]+":"+f[2]).OrderBy(x=>x).ToArray();
                            if(ranges.Length>64||!expected.SequenceEqual(ranges.Select(r=>r.order+":"+r.threshold).OrderBy(x=>x)))throw new Exception("Damage owner/threshold changed: "+entry.key);
                            int end=0;foreach(var range in ranges){if(range.start!=end||range.count<=0||range.count%3!=0)throw new Exception("Damage LOD range gap/overlap");end+=range.count;}
                            if(end!=level.triangles*3)throw new Exception("Incomplete damage LOD coverage");
                        }
                    }
                }
                foreach(var filter in matches)
                {
                    var renderer=filter.GetComponent<MeshRenderer>();var lod=filter.gameObject.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;
                    if(entry.smallDetail){lod.SetLODs(new[]{new LOD(.003f,new Renderer[]{renderer})});detailCull++;}
                    else
                    {
                        var levels=new List<LOD>{new LOD(.12f,new Renderer[]{renderer})};
                        foreach(int index in new[]{1,2})
                        {
                            var go=new GameObject(armor?entry.armorSurfaceName+"_LOD"+index:"REBIRTHDistanceLOD"+index);go.layer=filter.gameObject.layer;go.transform.SetParent(filter.transform,false);
                            go.AddComponent<MeshFilter>().sharedMesh=lower[index];var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=renderer.sharedMaterials;
                            r.shadowCastingMode=renderer.shadowCastingMode;r.receiveShadows=renderer.receiveShadows;r.lightProbeUsage=renderer.lightProbeUsage;r.reflectionProbeUsage=renderer.reflectionProbeUsage;r.probeAnchor=renderer.probeAnchor;
                            r.lightmapIndex=renderer.lightmapIndex;r.lightmapScaleOffset=renderer.lightmapScaleOffset;
                            if(armor)foreach(var range in damageRanges[index])
                            {var marker=new GameObject("ArmorRange_"+range.order+"_"+range.threshold+"_"+range.start+"_"+range.count);marker.transform.SetParent(go.transform,false);}
                            levels.Add(new LOD(index==1?.04f:.003f,new Renderer[]{r}));
                        }
                        lod.SetLODs(levels.ToArray());variants++;
                    }
                    lod.RecalculateBounds();
                }
                groups.Remove(Identity(entry));lines.Add(entry.key+" source="+entry.sourceTriangles+" lower="+string.Join(",",entry.levels.Select(l=>l.triangles.ToString()))+" instances="+matches.Length);
            }
            if(groups.Count!=0)throw new Exception("Unaccounted uncovered staged mesh assets="+groups.Count);
            if(root.GetComponentsInChildren<WheelCollider>(true).Length!=4)throw new Exception("Native wheel colliders changed");
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/rebirthCamper80s.prefab");AssetDatabase.SaveAssets();
            lines.Add("Reduced renderer groups="+variants+"; tiny-detail distance-cull groups="+detailCull+". Original LOD0 mesh and colliders untouched. Native active/armor visibility, animated door/seats, material quality and runtime LOD changes require validation before deployment.");
            File.WriteAllLines(Output+"/camper-station-lod-import.txt",lines);
        }
        finally{Object.DestroyImmediate(root);}
    }
    static string Hash(string path)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
}
