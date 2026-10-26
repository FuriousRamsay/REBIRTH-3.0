using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityRepairConnectivity
    {
        private sealed class Component
        {public int part,triangles;public float area;public Bounds bounds;public bool initialized;}

        public static void Write(EntitySurfaceRepairInput input,string path)
        {
            int count=input.sourcePositionsWorld.Length;var parent=new int[count];for(int i=0;i<count;i++)parent[i]=i;
            int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
            var welded=new Dictionary<(Vector3Int,int),int>();
            for(int i=0;i<input.sourceTriangles.Length;i+=3)
            {
                int part=input.sourceTriangleParts[i/3];
                for(int j=0;j<3;j++)
                {
                    int vertex=input.sourceTriangles[i+j];var key=(Vector3Int.RoundToInt(input.sourcePositionsWorld[vertex]*100000f),part);
                    if(welded.TryGetValue(key,out int prior))Join(vertex,prior);else welded.Add(key,vertex);
                }
                Join(input.sourceTriangles[i],input.sourceTriangles[i+1]);Join(input.sourceTriangles[i],input.sourceTriangles[i+2]);
            }
            var components=new Dictionary<int,Component>();
            for(int i=0;i<input.sourceTriangles.Length;i+=3)
            {
                int a=input.sourceTriangles[i],b=input.sourceTriangles[i+1],c=input.sourceTriangles[i+2],root=Find(a);
                if(!components.TryGetValue(root,out Component component)){component=new Component{part=input.sourceTriangleParts[i/3]};components.Add(root,component);}
                Vector3 pa=input.sourcePositionsWorld[a],pb=input.sourcePositionsWorld[b],pc=input.sourcePositionsWorld[c];
                component.triangles++;component.area+=Vector3.Cross(pb-pa,pc-pa).magnitude*.5f;
                if(!component.initialized){component.bounds=new Bounds(pa,Vector3.zero);component.initialized=true;}
                component.bounds.Encapsulate(pa);component.bounds.Encapsulate(pb);component.bounds.Encapsulate(pc);
            }
            var sorted=new List<KeyValuePair<int,Component>>(components);sorted.Sort((a,b)=>b.Value.area.CompareTo(a.Value.area));
            var rows=new List<string>{"component,part,triangles,area,minX,minY,minZ,maxX,maxY,maxZ"};
            foreach(var pair in sorted)
            {
                var value=pair.Value;Vector3 min=value.bounds.min,max=value.bounds.max;
                rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R}",pair.Key,value.part,value.triangles,value.area,min.x,min.y,min.z,max.x,max.y,max.z));
            }
            File.WriteAllLines(path,rows);Debug.Log("REPAIR CONNECTIVITY: "+components.Count+" original-surface components; no geometry removed.");
        }
    }
}
