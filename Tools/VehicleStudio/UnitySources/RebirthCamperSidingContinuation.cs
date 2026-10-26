using System;
using System.Collections.Generic;
using UnityEngine;

// Repeats measured original siding cross-sections as mesh geometry in the
// inserted wall band. Does not touch original lower siding or shared assets.
public static class RebirthCamperSidingContinuation
{
    const float Bottom=2.42f, Top=2.82f;
    static readonly float[] SideY={0,.006702f,.011003f,.014870f,.017354f,.022100f,.025151f,.029003f,.031480f,.038317f,.042719f,.11778f};
    static readonly float[] SideDepth={0,-.001028f,-.001548f,-.005156f,-.006983f,-.006979f,-.006977f,-.003375f,-.001547f,-.000520f,0,0};
    static readonly float[] FrontY={0,.010403f,.019142f,.025965f,.028167f,.036686f,.101055f};
    static readonly float[] FrontDepth={0,-.001969f,-.008949f,-.008938f,-.001921f,.000293f,0};
    struct Vertex { internal Vector3 p,n; internal Vector2 uv; }
    static float Profile(float y,bool front)
    {
        float[] levels=front?FrontY:SideY, values=front?FrontDepth:SideDepth;
        float phase=front?2.198883f:2.284219f;
        float t=Mathf.Repeat(y-phase,levels[levels.Length-1]);
        for(int i=1;i<levels.Length;i++)if(t<=levels[i])return Mathf.Lerp(values[i-1],values[i],(t-levels[i-1])/(levels[i]-levels[i-1]));
        return 0;
    }
    static float Relief(float y,bool front)
    {
        // Bottom matches the measured cut exactly. Blend only the final10mm
        // into the retained rolled cap to keep that boundary closed.
        return (Profile(y,front)-Profile(Bottom,front))*Mathf.Clamp01((Top-y)/.01f);
    }
    static List<Vertex> Clip(List<Vertex> polygon,float y,bool below)
    {
        var output=new List<Vertex>();
        for(int i=0;i<polygon.Count;i++)
        {
            var a=polygon[i];var b=polygon[(i+1)%polygon.Count];bool ia=below?a.p.y<=y:a.p.y>=y,ib=below?b.p.y<=y:b.p.y>=y;
            if(ia)output.Add(a);
            if(ia!=ib){float t=(y-a.p.y)/(b.p.y-a.p.y);output.Add(new Vertex{p=Vector3.LerpUnclamped(a.p,b.p,t),n=Vector3.LerpUnclamped(a.n,b.n,t),uv=Vector2.LerpUnclamped(a.uv,b.uv,t)});}
        }
        return output;
    }
    public static Mesh Build(Mesh original,Transform transform,out int panels)
    {
        if(original.subMeshCount!=1)throw new Exception("Expected single-material original camper shell.");
        var source=original.vertices;var normals=original.normals;var uv=original.uv;var indices=original.triangles;
        var vertices=new List<Vector3>();var outputNormals=new List<Vector3>();var outputUV=new List<Vector2>();var triangles=new List<int>();panels=0;
        var localToWorld=transform.localToWorldMatrix;var normalToWorld=localToWorld.inverse.transpose;
        Action<List<Vertex>,int,bool> emit=(polygon,direction,front)=>
        {
            if(polygon.Count<3)return;int start=vertices.Count;
            foreach(var vertex in polygon)
            {
                var p=vertex.p;var n=vertex.n;var tex=vertex.uv;
                if(direction!=0)
                {
                    float displacement=Relief(p.y,front),derivative=(Relief(p.y+.00001f,front)-Relief(p.y-.00001f,front))/.00002f;
                    if(Mathf.Abs(direction)==1){p.x+=direction*displacement;n.y-=direction*derivative*n.x;}
                    else {float sign=direction>0?1:-1;p.z+=sign*displacement;n.y-=sign*derivative*n.z;}
                    // Original clean atlas strip repeats every100mm in the old
                    // flat extension. Match its repeat to the measured mesh pitch.
                    tex.y=.85f+(tex.y-.85f)*(.10f/(front?.101055f:.11778f));
                }
                vertices.Add(transform.InverseTransformPoint(p));outputNormals.Add(localToWorld.transpose.MultiplyVector(n).normalized);outputUV.Add(tex);
            }
            for(int i=1;i<polygon.Count-1;i++)
            {
                if(Vector3.Cross(vertices[start+i]-vertices[start],vertices[start+i+1]-vertices[start]).sqrMagnitude<1e-16f)continue;
                triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);
            }
        };
        for(int t=0;t<indices.Length;t+=3)
        {
            var polygon=new List<Vertex>();for(int j=0;j<3;j++){int k=indices[t+j];polygon.Add(new Vertex{p=transform.TransformPoint(source[k]),n=normalToWorld.MultiplyVector(normals[k]).normalized,uv=uv[k]});}
            Vector3 a=polygon[0].p,b=polygon[1].p,c=polygon[2].p,normal=Vector3.Cross(b-a,c-a).normalized;
            float minY=Mathf.Min(a.y,Mathf.Min(b.y,c.y)),maxY=Mathf.Max(a.y,Mathf.Max(b.y,c.y));
            float minX=Mathf.Min(a.x,Mathf.Min(b.x,c.x)),maxX=Mathf.Max(a.x,Mathf.Max(b.x,c.x));
            float minZ=Mathf.Min(a.z,Mathf.Min(b.z,c.z)),maxZ=Mathf.Max(a.z,Mathf.Max(b.z,c.z));
            int direction=0;bool front=false;
            if(minY>=Bottom-.00001f&&maxY<=Top+.00001f)
            {
                if(Mathf.Abs(normal.x)>.9f&&maxZ-minZ>.05f&&Mathf.Min(Mathf.Abs(a.x),Mathf.Min(Mathf.Abs(b.x),Mathf.Abs(c.x)))>.86f)direction=a.x>0?1:-1;
                else if(Mathf.Abs(normal.z)>.9f&&maxX-minX>.05f&&(minZ>.5f||maxZ< -3.15f)){direction=minZ>.5f?2:-2;front=direction==2;}
            }
            if(direction==0){emit(polygon,0,false);continue;}
            panels++;
            float[] levels=front?FrontY:SideY;float phase=front?2.198883f:2.284219f,pitch=levels[levels.Length-1];
            var cuts=new SortedSet<float>{Bottom,Top-.01f,Top};
            for(int repeat=-2;repeat<12;repeat++)foreach(float level in levels){float y=phase+repeat*pitch+level;if(y>Bottom&&y<Top)cuts.Add(y);}
            foreach(float y in cuts)
            {
                var lower=Clip(polygon,y,true);emit(lower,direction,front);polygon=Clip(polygon,y,false);if(polygon.Count<3)break;
            }
        }
        if(panels==0)throw new Exception("No inserted siding faces found.");
        var mesh=new Mesh{name=original.name+"_PhysicalSiding",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.SetVertices(vertices);mesh.SetNormals(outputNormals);mesh.SetUVs(0,outputUV);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
}
