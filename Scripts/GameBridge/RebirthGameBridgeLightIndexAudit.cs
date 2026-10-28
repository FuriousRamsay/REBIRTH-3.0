using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;

// Explicit diagnostic only: compare the sparse index against native GetBlock.
internal static class RebirthGameBridgeLightIndexAudit
{
    internal static JObject AuditPoiLookup(EntityVehicle vehicle)
    {
        var prefabs=GameManager.Instance.GetDynamicPrefabDecorator()?.allPrefabs;
        if(prefabs==null)return new JObject{["passed"]=false};
        var points=new HashSet<UnityEngine.Vector3>{vehicle.position};
        foreach(var p in prefabs)
        {
            if(p?.prefab==null)continue;
            var a=p.boundingBoxPosition;var b=p.boundingBoxSize;
            points.Add(new UnityEngine.Vector3(a.x,0,a.z));
            points.Add(new UnityEngine.Vector3(a.x+b.x*.5f,0,a.z+b.z*.5f));
            points.Add(new UnityEngine.Vector3(a.x+b.x-.01f,0,a.z+b.z-.01f));
            points.Add(new UnityEngine.Vector3(a.x+b.x,0,a.z+b.z));
            points.Add(new UnityEngine.Vector3(a.x-.01f,0,a.z-.01f));
        }
        var nearby=new List<PrefabInstance>();var failures=new JArray();int mismatches=0,overlaps=0;
        long referenceTicks=0,spatialTicks=0;
        foreach(var position in points)
        {
            long start=Stopwatch.GetTimestamp();PrefabInstance expected=null;int matches=0;
            foreach(var p in prefabs)
            {
                if(p?.prefab==null||p.boundingBoxSize.x<8||p.boundingBoxSize.z<8)continue;
                var a=p.boundingBoxPosition;var b=p.boundingBoxSize;
                if(position.x>=a.x&&position.x<a.x+b.x&&position.z>=a.z&&position.z<a.z+b.z){expected=p;matches++;}
            }
            referenceTicks+=Stopwatch.GetTimestamp()-start;if(matches>1)overlaps++;
            start=Stopwatch.GetTimestamp();var actual=RebirthCamperEnergyDeviceAdapter.FindPoiAt(position,nearby);
            spatialTicks+=Stopwatch.GetTimestamp()-start;
            if(!ReferenceEquals(expected,actual))
            {mismatches++;if(failures.Count<12)failures.Add(new JObject{["position"]=position.ToString(),["expected"]=expected?.name,["actual"]=actual?.name});}
        }
        return new JObject{["passed"]=points.Count>1&&mismatches==0,["points"]=points.Count,["overlapPoints"]=overlaps,
            ["mismatches"]=mismatches,["referenceMs"]=referenceTicks*1000.0/Stopwatch.Frequency,
            ["spatialMs"]=spatialTicks*1000.0/Stopwatch.Frequency,["failures"]=failures};
    }
    internal static JObject Run(EntityVehicle vehicle)
    {
        var rows=new JArray();int checkedCount=0;bool passed=true;
        var prefabs=GameManager.Instance.GetDynamicPrefabDecorator()?.allPrefabs;
        if(prefabs==null)return new JObject{["passed"]=false,["reason"]="No prefab decorator"};
        var candidates=prefabs.Where(p=>p?.prefab?.blockCells!=null&&p.prefab.damageCells!=null)
            .OrderBy(p=>(p.boundingBoxPosition.ToVector3()-vehicle.position).sqrMagnitude)
            .GroupBy(p=>p.prefab).Select(g=>g.First()).Take(12);
        foreach(var poi in candidates)
        {
            var prefab=poi.prefab;var size=prefab.size;
            long volume=(long)size.x*size.y*size.z;
            if(volume>2000000)continue;
            RebirthCamperAuthoredLightIndex.Invalidate(prefab);
            var watch=Stopwatch.StartNew();
            var indexed=new HashSet<Vector3i>(RebirthCamperAuthoredLightIndex.Lights(prefab));
            double indexMs=watch.Elapsed.TotalMilliseconds;watch.Restart();
            var expected=new HashSet<Vector3i>();
            for(int x=0;x<size.x;x++)for(int z=0;z<size.z;z++)for(int y=0;y<size.y;y++)
            {
                var value=prefab.GetBlock(x,y,z);
                if(!value.ischild&&value.Block is BlockLight lamp&&lamp.HasTileEntity)expected.Add(new Vector3i(x,y,z));
            }
            double referenceMs=watch.Elapsed.TotalMilliseconds;
            bool equal=indexed.SetEquals(expected);passed&=equal;checkedCount++;
            rows.Add(new JObject{["poi"]=poi.name,["rotation"]=prefab.localRotation,["volume"]=volume,
                ["indexed"]=indexed.Count,["reference"]=expected.Count,["equal"]=equal,
                ["indexBuildMs"]=indexMs,["referenceScanMs"]=referenceMs,
                ["missing"]=new JArray(expected.Except(indexed).Take(8).Select(p=>p.ToString())),
                ["extra"]=new JArray(indexed.Except(expected).Take(8).Select(p=>p.ToString()))});
        }
        return new JObject{["passed"]=passed&&checkedCount>0,["checkedPrefabs"]=checkedCount,["prefabs"]=rows};
    }
}
