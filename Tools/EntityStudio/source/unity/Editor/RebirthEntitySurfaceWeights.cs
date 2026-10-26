using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Distances travel over triangle edges, so an arm touching the torso does not
    // make torso vertices closer to the arm through empty space. UV seams are welded
    // by position only for this graph; the original render mesh remains untouched.
    public static class RebirthEntitySurfaceWeights
    {
        public static float[][] Build(Vector3[] points,int[] triangles,Vector3[] starts,Vector3[] ends,bool[][] eligible,float height,int[] groups=null,float[] seedMargins=null)
        {
            if(seedMargins!=null)
            {
                if(seedMargins.Length!=starts.Length)throw new ArgumentException("One seed margin is required per bone.");
                foreach(float margin in seedMargins)if(float.IsNaN(margin)||float.IsInfinity(margin)||margin<0)throw new ArgumentException("Seed margins must be finite and nonnegative.");
            }
            var ids=new int[points.Length];var weld=new Dictionary<Vector4,int>();var nodes=new List<Vector3>();
            float precision=Mathf.Max(height*.000001f,.000001f);
            for(int i=0;i<points.Length;i++)
            {
                Vector3 point=points[i];var key=new Vector4(Mathf.Round(point.x/precision),Mathf.Round(point.y/precision),Mathf.Round(point.z/precision),groups!=null?groups[i]:0);
                if(!weld.TryGetValue(key,out int id)){id=nodes.Count;weld.Add(key,id);nodes.Add(point);}ids[i]=id;
            }
            var neighbors=new List<int>[nodes.Count];for(int i=0;i<neighbors.Length;i++)neighbors[i]=new List<int>();
            for(int i=0;i<triangles.Length;i+=3)
            {Connect(ids[triangles[i]],ids[triangles[i+1]]);Connect(ids[triangles[i+1]],ids[triangles[i+2]]);Connect(ids[triangles[i+2]],ids[triangles[i]]);}
            var result=new float[starts.Length][];
            for(int bone=0;bone<starts.Length;bone++)
            {
                var distance=new float[nodes.Count];for(int i=0;i<distance.Length;i++)distance[i]=float.PositiveInfinity;
                var bandMinimum=new float[5];for(int i=0;i<5;i++)bandMinimum[i]=float.PositiveInfinity;
                Vector3 segment=ends[bone]-starts[bone];float lengthSquared=segment.sqrMagnitude;
                var radial=new float[points.Length];var bands=new int[points.Length];
                for(int i=0;i<points.Length;i++)
                {
                    bands[i]=-1;if(eligible!=null&&!eligible[bone][i])continue;
                    float along=lengthSquared>.000001f?Vector3.Dot(points[i]-starts[bone],segment)/lengthSquared:.5f;
                    if(along<.12f||along>.88f)continue;
                    int band=Mathf.Clamp((int)(along*5),0,4);bands[i]=band;
                    radial[i]=(points[i]-(starts[bone]+segment*along)).magnitude;
                    bandMinimum[band]=Mathf.Min(bandMinimum[band],radial[i]);
                }
                var heap=new MinHeap();
                for(int i=0;i<points.Length;i++)
                {
                    int band=bands[i];float margin=seedMargins!=null?seedMargins[bone]:height*.004f;
                    if(band<0||radial[i]>bandMinimum[band]+margin)continue;
                    int id=ids[i];if(radial[i]<distance[id]){distance[id]=radial[i];heap.Push(id,radial[i]);}
                }
                while(heap.Count>0)
                {
                    Entry entry=heap.Pop();if(entry.distance>distance[entry.node])continue;
                    foreach(int next in neighbors[entry.node])
                    {
                        float candidate=entry.distance+Vector3.Distance(nodes[entry.node],nodes[next]);
                        if(candidate>=distance[next])continue;
                        distance[next]=candidate;heap.Push(next,candidate);
                    }
                }
                result[bone]=new float[points.Length];
                for(int i=0;i<points.Length;i++)result[bone][i]=distance[ids[i]];
            }
            return result;

            void Connect(int a,int b)
            {if(a==b)return;if(!neighbors[a].Contains(b))neighbors[a].Add(b);if(!neighbors[b].Contains(a))neighbors[b].Add(a);}
        }

        private struct Entry{public int node;public float distance;public Entry(int n,float d){node=n;distance=d;}}
        private sealed class MinHeap
        {
            private readonly List<Entry> entries=new List<Entry>();public int Count=>entries.Count;
            public void Push(int node,float distance)
            {
                var value=new Entry(node,distance);int at=entries.Count;entries.Add(value);
                while(at>0){int parent=(at-1)/2;if(entries[parent].distance<=distance)break;entries[at]=entries[parent];at=parent;}entries[at]=value;
            }
            public Entry Pop()
            {
                Entry first=entries[0],last=entries[entries.Count-1];entries.RemoveAt(entries.Count-1);
                if(entries.Count==0)return first;int at=0;
                while(at*2+1<entries.Count)
                {
                    int child=at*2+1;if(child+1<entries.Count&&entries[child+1].distance<entries[child].distance)child++;
                    if(entries[child].distance>=last.distance)break;entries[at]=entries[child];at=child;
                }
                entries[at]=last;return first;
            }
        }
    }
}
