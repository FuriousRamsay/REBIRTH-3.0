using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public sealed class RebirthEntityUvChartAllocator
    {
        private readonly bool[] occupied;
        public int Resolution {get;}
        public RebirthEntityUvChartAllocator(bool[] mask,int resolution)
        {
            if(resolution<8||mask==null||mask.Length!=resolution*resolution)throw new ArgumentException("A square UV occupancy mask is required.");
            Resolution=resolution;occupied=(bool[])mask.Clone();
        }
        public bool TryAllocate(int width,int height,int padding,out RectInt payload)
        {
            if(width<=0||height<=0||padding<0)throw new ArgumentOutOfRangeException("Chart dimensions/padding must be valid.");
            int w=checked(width+2*padding),h=checked(height+2*padding),stride=Resolution+1;payload=new RectInt();
            if(w>Resolution||h>Resolution)return false;
            var prefix=new int[stride*stride];
            for(int y=0;y<Resolution;y++)for(int x=0;x<Resolution;x++)prefix[(y+1)*stride+x+1]=(occupied[y*Resolution+x]?1:0)+prefix[y*stride+x+1]+prefix[(y+1)*stride+x]-prefix[y*stride+x];
            for(int y=0;y<=Resolution-h;y++)for(int x=0;x<=Resolution-w;x++)
            {
                int used=prefix[(y+h)*stride+x+w]-prefix[y*stride+x+w]-prefix[(y+h)*stride+x]+prefix[y*stride+x];if(used!=0)continue;
                for(int row=y;row<y+h;row++)for(int column=x;column<x+w;column++)occupied[row*Resolution+column]=true;
                payload=new RectInt(x+padding,y+padding,width,height);return true;
            }
            return false;
        }
        public bool[] Snapshot()=>(bool[])occupied.Clone();

        public static void RunFarmerProbe()
        {
            GameObject model=null;
            foreach(string guid in UnityEditor.AssetDatabase.FindAssets("t:Model",new[]{"Assets/EntityStudioInputs/RebirthFarmer"}))
            {string path=UnityEditor.AssetDatabase.GUIDToAssetPath(guid);if(path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)){model=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);break;}}
            if(model==null)throw new InvalidOperationException("Farmer source mesh is missing.");
            var meshes=new List<Mesh>();foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))meshes.Add(filter.sharedMesh);
            bool[] original=BuildOccupancy(meshes);var allocator=new RebirthEntityUvChartAllocator(original,512);var rows=new List<string>{"probe,width,height,padding,x,y"};
            for(int chart=0;chart<24;chart++)
            {
                if(!allocator.TryAllocate(24,8,2,out RectInt rect))break;
                for(int y=rect.yMin-2;y<rect.yMax+2;y++)for(int x=rect.xMin-2;x<rect.xMax+2;x++)if(original[y*512+x])throw new InvalidOperationException("A probe chart overlapped protected source UV space.");
                rows.Add(chart+",24,8,2,"+rect.x+","+rect.y);
            }
            string output=System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation/FarmerUvAllocationProbe.csv");System.IO.File.WriteAllLines(output,rows);
            Debug.Log("FARMER UV ALLOCATION PROBE: "+(rows.Count-1)+" padded24x8 charts; source untouched; this is not actual cap packing or a texture bake");
        }

        // All source meshes passed here must use the same normalized atlas.
        public static bool[] BuildOccupancy(IEnumerable<Mesh> meshes,int size=512,int padding=2)
        {
            if(size<8||padding<0||padding>=size/2)throw new ArgumentOutOfRangeException("UV audit dimensions/padding must be valid.");
            var used=new bool[size*size];
            foreach(var mesh in meshes)
            {
                var uv=mesh.uv;var triangles=mesh.triangles;
                if(uv.Length!=mesh.vertexCount)throw new InvalidOperationException("Repair atlas allocation requires UVs on every vertex.");
                foreach(Vector2 point in uv)if(float.IsNaN(point.x)||float.IsInfinity(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.y)||point.x<0||point.x>1||point.y<0||point.y>1)throw new InvalidOperationException("Repair atlas allocation requires finite UVs inside 0..1.");
                for(int i=0;i<triangles.Length;i+=3)
                {
                    Vector2 a=uv[triangles[i]],b=uv[triangles[i+1]],c=uv[triangles[i+2]];MarkEdge(a,b);MarkEdge(b,c);MarkEdge(c,a);
                    float denominator=Cross(b-a,c-a);if(Mathf.Abs(denominator)<1e-12f)continue;
                    int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)*size),0,size-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)*size),0,size-1);
                    int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y)*size),0,size-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y)*size),0,size-1);
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {Vector2 p=new Vector2((x+.5f)/size,(y+.5f)/size);float u=Cross(p-a,c-a)/denominator,v=Cross(b-a,p-a)/denominator;if(u>=-.0001f&&v>=-.0001f&&u+v<=1.0001f)used[y*size+x]=true;}
                }
            }
            var padded=(bool[])used.Clone();
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                if(x<padding||y<padding||x>=size-padding||y>=size-padding)padded[y*size+x]=true;
                if(!used[y*size+x])continue;
                // Source textures repeat; protect sampling across either atlas edge.
                for(int dy=-padding;dy<=padding;dy++)for(int dx=-padding;dx<=padding;dx++)padded[((y+dy+size)%size)*size+(x+dx+size)%size]=true;
            }
            return padded;
            void MarkEdge(Vector2 a,Vector2 b)
            {
                int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(a.x-b.x),Mathf.Abs(a.y-b.y))*size*2f));
                for(int i=0;i<=steps;i++){Vector2 p=Vector2.Lerp(a,b,(float)i/steps);int x=Mathf.Clamp(Mathf.FloorToInt(p.x*size),0,size-1),y=Mathf.Clamp(Mathf.FloorToInt(p.y*size),0,size-1);used[y*size+x]=true;}
            }
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    }
}
