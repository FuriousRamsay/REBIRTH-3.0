using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Planning is separate from committing UVs: a failed bake must not leave an
    // otherwise usable mesh pointing at empty atlas pixels.
    public static class RebirthEntityRepairCharts
    {
        public sealed class Chart
        {
            public EntitySurfaceRepairInput input;
            public int capId,part,axis;
            public Vector2 minimum,maximum;
            public RectInt pixels;
            public readonly List<int> vertices=new List<int>();
            public Vector2 Project(Vector3 point)=>axis==0?new Vector2(point.z,point.y):axis==1?new Vector2(point.x,point.z):new Vector2(point.x,point.y);
            public Vector2 Uv(Vector3 point,int resolution)
            {
                Vector2 p=Project(point),size=maximum-minimum;
                return new Vector2((pixels.x+.5f+(pixels.width-1)*(p.x-minimum.x)/size.x)/resolution,
                    (pixels.y+.5f+(pixels.height-1)*(p.y-minimum.y)/size.y)/resolution);
            }
        }

        public static List<Chart> Plan(IReadOnlyList<EntitySurfaceRepairInput> inputs,RebirthEntityUvChartAllocator allocator)
        {
            var charts=new List<Chart>();
            foreach(var input in inputs)
            {
                var byCap=new Dictionary<int,Chart>();
                for(int i=0;i<input.repairVertexIds.Length;i++)
                {
                    int id=input.repairVertexIds[i];if(id==0)continue;
                    if(!byCap.TryGetValue(id,out Chart chart))
                    {chart=new Chart{input=input,capId=id,part=input.repairVertexParts[i]};byCap.Add(id,chart);charts.Add(chart);}
                    if(chart.part!=input.repairVertexParts[i])throw new InvalidOperationException("A repair cap crosses anatomical parts.");
                    chart.vertices.Add(i);
                }
                foreach(var chart in byCap.Values)
                {
                    // Sum triangle area normals; choose the projection with the
                    // greatest visible surface area instead of the shortest bounds.
                    Vector3 area=Vector3.zero;int[] triangles=input.repairedMesh.triangles;
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];if(input.repairVertexIds[a]!=chart.capId)continue;
                        area+=Vector3.Cross(input.repairPositionsWorld[b]-input.repairPositionsWorld[a],input.repairPositionsWorld[c]-input.repairPositionsWorld[a]);
                    }
                    area=new Vector3(Mathf.Abs(area.x),Mathf.Abs(area.y),Mathf.Abs(area.z));
                    chart.axis=area.x>=area.y&&area.x>=area.z?0:area.y>=area.z?1:2;
                    chart.minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);chart.maximum=-chart.minimum;
                    foreach(int vertex in chart.vertices){Vector2 point=chart.Project(input.repairPositionsWorld[vertex]);chart.minimum=Vector2.Min(chart.minimum,point);chart.maximum=Vector2.Max(chart.maximum,point);}
                    if((chart.maximum-chart.minimum).x<1e-6f||(chart.maximum-chart.minimum).y<1e-6f)throw new InvalidOperationException("Degenerate repair projection.");
                }
            }
            charts.Sort((a,b)=>Area(b).CompareTo(Area(a)));
            // Large surfaces reserve space first. Density adapts to fragmented
            // unused atlas space; existing islands are never overwritten.
            foreach(var chart in charts)
            {
                Vector2 extent=chart.maximum-chart.minimum;bool allocated=false;
                for(float density=256;density>=8;density*=.75f)
                {
                    int width=Mathf.Max(3,Mathf.CeilToInt(extent.x*density)),height=Mathf.Max(3,Mathf.CeilToInt(extent.y*density));
                    if(allocator.TryAllocate(width,height,2,out RectInt pixels)){chart.pixels=pixels;allocated=true;break;}
                }
                if(!allocated)throw new InvalidOperationException("No protected atlas space remains for all repair caps; UVs were not changed.");
            }
            return charts;
        }
        private static float Area(Chart chart){Vector2 size=chart.maximum-chart.minimum;return size.x*size.y;}
    }
}
