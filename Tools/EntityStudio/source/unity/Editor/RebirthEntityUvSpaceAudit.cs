using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityUvSpaceAudit
    {
        [Serializable] private sealed class Report {public int resolution,occupiedPixels,freePixels;public RectInt largestFreeRectangle;public float freeFraction;}
        public static void RunFarmerSource()
        {
            const int size=512;var used=new bool[size*size];
            string path=AssetDatabase.FindAssets("t:Model",new[]{"Assets/EntityStudioInputs/RebirthFarmer"}).Select(AssetDatabase.GUIDToAssetPath).First(value=>value.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase));
            foreach(var filter in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>(true))
            {
                var uv=filter.sharedMesh.uv;var triangles=filter.sharedMesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    Vector2 a=uv[triangles[i]],b=uv[triangles[i+1]],c=uv[triangles[i+2]];float denominator=Cross(b-a,c-a);if(Mathf.Abs(denominator)<1e-12f)continue;
                    MarkEdge(a,b);MarkEdge(b,c);MarkEdge(c,a);
                    int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)*size),0,size-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)*size),0,size-1);
                    int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y)*size),0,size-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y)*size),0,size-1);
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {Vector2 p=new Vector2((x+.5f)/size,(y+.5f)/size);float u=Cross(p-a,c-a)/denominator,v=Cross(b-a,p-a)/denominator;if(u>=-.0001f&&v>=-.0001f&&u+v<=1.0001f)used[y*size+x]=true;}
                }
            }
            // Reserve a small border around all existing islands for mip bleed.
            var padded=(bool[])used.Clone();for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(used[y*size+x])for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)if(x+dx>=0&&x+dx<size&&y+dy>=0&&y+dy<size)padded[(y+dy)*size+x+dx]=true;
            int occupied=padded.Count(value=>value);RectInt largest=LargestEmpty(padded,size);
            var report=new Report{resolution=size,occupiedPixels=occupied,freePixels=size*size-occupied,freeFraction=1f-(float)occupied/(size*size),largestFreeRectangle=largest};
            string output=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs/EntityStudioValidation");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"FarmerSourceUvSpace.json"),JsonUtility.ToJson(report,true));
            var texture=new Texture2D(size,size,TextureFormat.RGB24,false);try{var pixels=new Color32[used.Length];for(int i=0;i<pixels.Length;i++)pixels[i]=padded[i]?new Color32(200,200,200,255):new Color32(20,30,40,255);texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(Path.Combine(output,"FarmerSourceUvSpace.png"),texture.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(texture);}
            Debug.Log("SOURCE UV SPACE: "+JsonUtility.ToJson(report));
            void MarkEdge(Vector2 a,Vector2 b)
            {
                int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(a.x-b.x),Mathf.Abs(a.y-b.y))*size*2f));
                for(int i=0;i<=steps;i++){Vector2 p=Vector2.Lerp(a,b,(float)i/steps);int x=Mathf.Clamp(Mathf.FloorToInt(p.x*size),0,size-1),y=Mathf.Clamp(Mathf.FloorToInt(p.y*size),0,size-1);used[y*size+x]=true;}
            }
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static RectInt LargestEmpty(bool[] used,int size)
        {
            var heights=new int[size];var stack=new int[size+1];RectInt best=new RectInt();
            for(int y=0;y<size;y++)
            {
                for(int x=0;x<size;x++)heights[x]=used[y*size+x]?0:heights[x]+1;int count=0;
                for(int x=0;x<=size;x++)
                {
                    int height=x==size?0:heights[x];
                    while(count>0&&heights[stack[count-1]]>height){int top=stack[--count],left=count==0?0:stack[count-1]+1,width=x-left;if(width*heights[top]>best.width*best.height)best=new RectInt(left,y-heights[top]+1,width,heights[top]);}
                    if(x<size)stack[count++]=x;
                }
            }
            return best;
        }
    }
}
