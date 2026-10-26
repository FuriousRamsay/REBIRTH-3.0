using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityRepairProjection
    {
        // Returns linear, unlit source samples. This method never modifies the
        // source texture, mesh UVs, or authored scene objects.
        public static Color32[] Sample(RebirthEntityRepairCharts.Chart chart,Texture2D source,int width,int height,out float coverage,bool outwardArm=false)
        {
            if(source==null||width<1||height<1)throw new ArgumentException("A source map and positive chart size are required.");
            Shader shader=Shader.Find("Hidden/RebirthEntityStudio/RepairProjection");
            if(shader==null)throw new InvalidOperationException("Repair projection shader is missing.");
            var surface=new GameObject("Temporary repair source"){layer=31,hideFlags=HideFlags.HideAndDontSave};
            var view=new GameObject("Temporary repair camera"){hideFlags=HideFlags.HideAndDontSave};
            var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var readback=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
            RenderTexture previous=RenderTexture.active;
            try
            {
                var input=chart.input;var triangles=new List<int>();
                for(int i=0;i<input.sourceTriangleParts.Length;i++)if(input.sourceTriangleParts[i]==chart.part)
                {triangles.Add(input.sourceTriangles[i*3]);triangles.Add(input.sourceTriangles[i*3+1]);triangles.Add(input.sourceTriangles[i*3+2]);}
                mesh.vertices=input.sourcePositionsWorld;mesh.uv=input.sourceUv;mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
                surface.AddComponent<MeshFilter>().sharedMesh=mesh;surface.AddComponent<MeshRenderer>().sharedMaterial=material;material.mainTexture=source;
                var camera=view.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                camera.cullingMask=1<<31;camera.targetTexture=target;camera.allowHDR=false;camera.allowMSAA=false;camera.nearClipPlane=.01f;camera.farClipPlane=20;
                Vector2 center=(chart.minimum+chart.maximum)*.5f,extent=chart.maximum-chart.minimum;
                // These bases match Chart.Project exactly; +U is screen right.
                Vector3 right=chart.axis==0?Vector3.forward:Vector3.right;
                Vector3 up=chart.axis==1?Vector3.forward:Vector3.up;
                Vector3 forward=Vector3.Cross(right,up).normalized;
                bool reverse=outwardArm&&chart.axis==0&&chart.part==1;
                if(reverse)forward=-forward;
                Vector3 focus=chart.axis==0?new Vector3(mesh.bounds.center.x,center.y,center.x):chart.axis==1?new Vector3(center.x,mesh.bounds.center.y,center.y):new Vector3(center.x,center.y,mesh.bounds.center.z);
                view.transform.SetPositionAndRotation(focus-forward*5,Quaternion.LookRotation(forward,up));
                camera.orthographicSize=extent.y*.5f;camera.aspect=extent.x/extent.y;
                target.Create();camera.Render();RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,width,height),0,0);readback.Apply();
                Color32[] pixels=readback.GetPixels32();
                // Reversing camera direction reverses screen U. Restore chart U
                // before extending samples or writing back to the protected atlas.
                if(reverse)for(int row=0;row<height;row++)Array.Reverse(pixels,row*width,width);
                int covered=0;var queue=new Queue<int>();var seen=new bool[pixels.Length];
                for(int i=0;i<pixels.Length;i++)if(pixels[i].a>0){covered++;seen[i]=true;queue.Enqueue(i);}
                coverage=(float)covered/pixels.Length;
                if(covered==0)throw new InvalidOperationException("Repair projection has no source coverage for cap "+chart.capId+".");
                // Extend valid samples into uncovered pixels; never read unrelated
                // atlas colours through the old interpolated cap UVs.
                while(queue.Count>0)
                {
                    int index=queue.Dequeue(),x=index%width,y=index/width;
                    if(x>0)Visit(index-1);if(x+1<width)Visit(index+1);if(y>0)Visit(index-width);if(y+1<height)Visit(index+width);
                    void Visit(int next){if(seen[next])return;seen[next]=true;pixels[next]=pixels[index];queue.Enqueue(next);}
                }
                return pixels;
            }
            finally
            {
                RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(surface);UnityEngine.Object.DestroyImmediate(view);
                UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);
            }
        }
    }
}
