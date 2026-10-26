using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityCapTriangulation
    {
        public static int[] Triangulate(Vector3[] boundary)
        {
            if(boundary==null||boundary.Length<3)throw new ArgumentException("A closed boundary with at least three points is required.");
            Vector3 normal=Vector3.zero;
            for(int i=0;i<boundary.Length;i++)normal+=Vector3.Cross(boundary[i]-boundary[0],boundary[(i+1)%boundary.Length]-boundary[0]);
            Vector3 magnitude=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
            int axis=magnitude.x>=magnitude.y&&magnitude.x>=magnitude.z?0:magnitude.y>=magnitude.z?1:2;
            var points=new Vector2[boundary.Length];var polygon=new List<int>();var result=new List<int>();
            for(int i=0;i<boundary.Length;i++){Vector3 p=boundary[i];points[i]=axis==0?new Vector2(p.y,p.z):axis==1?new Vector2(p.x,p.z):new Vector2(p.x,p.y);polygon.Add(i);}
            double area=0;for(int i=0;i<points.Length;i++)area+=Cross(points[i],points[(i+1)%points.Length]);
            if(Math.Abs(area)<1e-14)return Array.Empty<int>();
            double sign=area>0?1:-1;
            while(polygon.Count>3)
            {
                int best=-1;double bestQuality=-1;
                for(int i=0;i<polygon.Count;i++)
                {
                    int ai=polygon[(i+polygon.Count-1)%polygon.Count],bi=polygon[i],ci=polygon[(i+1)%polygon.Count];
                    Vector2 a=points[ai],b=points[bi],c=points[ci];double cross=Cross(b-a,c-b)*sign;
                    if(cross<=1e-13)continue;
                    bool contains=false;
                    foreach(int p in polygon)
                    {
                        if(p==ai||p==bi||p==ci)continue;
                        Vector2 v=points[p];
                        if(Cross(b-a,v-a)*sign>1e-13&&Cross(c-b,v-b)*sign>1e-13&&Cross(a-c,v-c)*sign>1e-13){contains=true;break;}
                    }
                    if(contains)continue;
                    double quality=cross/Math.Max(1e-20,(a-b).sqrMagnitude+(b-c).sqrMagnitude+(c-a).sqrMagnitude);
                    if(quality>bestQuality){bestQuality=quality;best=i;}
                }
                if(best<0)
                {
                    // Collinear boundary samples need no triangle of their own.
                    int flat=-1;double smallest=double.PositiveInfinity;
                    for(int i=0;i<polygon.Count;i++)
                    {Vector2 a=points[polygon[(i+polygon.Count-1)%polygon.Count]],b=points[polygon[i]],c=points[polygon[(i+1)%polygon.Count]];double value=Math.Abs(Cross(b-a,c-b));if(value<smallest){smallest=value;flat=i;}}
                    if(smallest>1e-10)throw new InvalidOperationException("An anatomical cap boundary cannot be triangulated without crossing its outline.");
                    polygon.RemoveAt(flat);continue;
                }
                result.Add(polygon[(best+polygon.Count-1)%polygon.Count]);result.Add(polygon[best]);result.Add(polygon[(best+1)%polygon.Count]);polygon.RemoveAt(best);
            }
            if(polygon.Count==3){result.Add(polygon[0]);result.Add(polygon[1]);result.Add(polygon[2]);}
            return result.ToArray();
        }
        static double Cross(Vector2 a,Vector2 b){return (double)a.x*b.y-(double)a.y*b.x;}
    }
}
