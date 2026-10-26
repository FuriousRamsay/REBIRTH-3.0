using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Quadric collapse can put a vertex outside the source surface, especially on
    // small planar cut caps. Reproject geometry and interpolate the original skin
    // weights so distant LODs retain both the silhouette and joint ownership.
    public static class RebirthEntityLodSurface
    {
        sealed class Node { public Bounds bounds;public Node left,right;public int start,count; }
        sealed class Surface
        {
            public Vector3[] vertices;public Vector2[] uv;public BoneWeight[] weights;
            public int[] indices,triangles,triangleGroups;public Node root;
            public Surface(Mesh mesh,int[] boneGroups)
            {
                vertices=mesh.vertices;uv=mesh.uv;weights=mesh.boneWeights;indices=mesh.triangles;
                triangles=new int[indices.Length/3];for(int i=0;i<triangles.Length;i++)triangles[i]=i;
                triangleGroups=new int[triangles.Length];
                if(boneGroups!=null&&weights.Length==vertices.Length)
                    for(int i=0;i<triangles.Length;i++)
                    {int a=Group(weights[indices[i*3]],boneGroups),b=Group(weights[indices[i*3+1]],boneGroups),c=Group(weights[indices[i*3+2]],boneGroups);triangleGroups[i]=a==b||a==c?a:b;}
                if(triangles.Length==0)throw new ArgumentException("LOD source has no triangles.");
                root=Build(0,triangles.Length);
            }
            Vector3 Center(int triangle){int i=triangle*3;return (vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3f;}
            Node Build(int start,int count)
            {
                var node=new Node{start=start,count=count,bounds=new Bounds(vertices[indices[triangles[start]*3]],Vector3.zero)};
                for(int i=start;i<start+count;i++)for(int k=0;k<3;k++)node.bounds.Encapsulate(vertices[indices[triangles[i]*3+k]]);
                if(count<=12)return node;
                Vector3 size=node.bounds.size;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
                Array.Sort(triangles,start,count,Comparer<int>.Create((a,b)=>Center(a)[axis].CompareTo(Center(b)[axis])));
                int half=count/2;node.left=Build(start,half);node.right=Build(start+half,count-half);return node;
            }
            public void Find(Node node,Vector3 point,Vector2 targetUv,int requiredGroup,ref float best,ref float bestUv,ref int triangle,ref Vector3 barycentric,ref Vector3 nearest)
            {
                if(node.bounds.SqrDistance(point)>best+1e-10f)return;
                if(node.left!=null)
                {
                    Node first=node.left,second=node.right;if(first.bounds.SqrDistance(point)>second.bounds.SqrDistance(point)){first=node.right;second=node.left;}
                    Find(first,point,targetUv,requiredGroup,ref best,ref bestUv,ref triangle,ref barycentric,ref nearest);
                    Find(second,point,targetUv,requiredGroup,ref best,ref bestUv,ref triangle,ref barycentric,ref nearest);return;
                }
                for(int n=node.start;n<node.start+node.count;n++)
                {
                    int t=triangles[n],i=t*3,a=indices[i],b=indices[i+1],c=indices[i+2];
                    if(requiredGroup>=0&&triangleGroups[t]!=requiredGroup)continue;
                    Vector3 bary;Vector3 candidate=Closest(point,vertices[a],vertices[b],vertices[c],out bary);
                    float distance=(candidate-point).sqrMagnitude;
                    float uvDistance=uv.Length==vertices.Length?(uv[a]*bary.x+uv[b]*bary.y+uv[c]*bary.z-targetUv).sqrMagnitude:0f;
                    if(distance<best-1e-10f||(Mathf.Abs(distance-best)<=1e-10f&&uvDistance<bestUv))
                    {best=distance;bestUv=uvDistance;triangle=t;barycentric=bary;nearest=candidate;}
                }
            }
        }

        public static void Project(Mesh source,Mesh lod,int[] boneGroups=null)
        {
            var surface=new Surface(source,boneGroups);Vector3[] points=lod.vertices;Vector2[] uv=lod.uv;BoneWeight[] provisional=lod.boneWeights;
            var weights=new BoneWeight[points.Length];bool hasWeights=surface.weights.Length==surface.vertices.Length;
            for(int i=0;i<points.Length;i++)
            {
                float best=float.PositiveInfinity,bestUv=float.PositiveInfinity;int triangle=-1;Vector3 bary=Vector3.zero,nearest=points[i];
                int requiredGroup=boneGroups!=null&&provisional.Length==points.Length?Group(provisional[i],boneGroups):-1;
                surface.Find(surface.root,points[i],uv.Length==points.Length?uv[i]:Vector2.zero,requiredGroup,ref best,ref bestUv,ref triangle,ref bary,ref nearest);
                if(triangle<0)throw new InvalidOperationException("Could not project simplified vertex.");points[i]=nearest;
                if(hasWeights)
                {
                    int at=triangle*3;var combined=new Dictionary<int,float>();
                    Add(combined,surface.weights[surface.indices[at]],bary.x);Add(combined,surface.weights[surface.indices[at+1]],bary.y);Add(combined,surface.weights[surface.indices[at+2]],bary.z);
                    var sorted=new List<KeyValuePair<int,float>>(combined);sorted.Sort((a,b)=>b.Value.CompareTo(a.Value));
                    float total=0;for(int k=0;k<Math.Min(4,sorted.Count);k++)total+=sorted[k].Value;
                    BoneWeight result=new BoneWeight();for(int k=0;k<Math.Min(4,sorted.Count);k++)Set(ref result,k,sorted[k].Key,sorted[k].Value/total);weights[i]=result;
                }
            }
            lod.vertices=points;if(hasWeights)lod.boneWeights=weights;lod.RecalculateBounds();
        }
        static int Group(BoneWeight weight,int[] boneGroups)
        {
            float[] sum=new float[3];
            sum[boneGroups[weight.boneIndex0]]+=weight.weight0;sum[boneGroups[weight.boneIndex1]]+=weight.weight1;
            sum[boneGroups[weight.boneIndex2]]+=weight.weight2;sum[boneGroups[weight.boneIndex3]]+=weight.weight3;
            return sum[1]>sum[0]?(sum[1]>=sum[2]?1:2):(sum[0]>=sum[2]?0:2);
        }
        static void Add(Dictionary<int,float> values,BoneWeight weight,float amount)
        {Add(values,weight.boneIndex0,weight.weight0*amount);Add(values,weight.boneIndex1,weight.weight1*amount);Add(values,weight.boneIndex2,weight.weight2*amount);Add(values,weight.boneIndex3,weight.weight3*amount);}
        static void Add(Dictionary<int,float> values,int bone,float weight){if(weight<=0)return;values.TryGetValue(bone,out float current);values[bone]=current+weight;}
        static void Set(ref BoneWeight weight,int index,int bone,float value)
        {switch(index){case 0:weight.boneIndex0=bone;weight.weight0=value;break;case 1:weight.boneIndex1=bone;weight.weight1=value;break;case 2:weight.boneIndex2=bone;weight.weight2=value;break;default:weight.boneIndex3=bone;weight.weight3=value;break;}}

        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c,out Vector3 bary)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0){bary=Vector3.right;return a;}
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);
            if(d3>=0&&d4<=d3){bary=Vector3.up;return b;}
            float vc=d1*d4-d3*d2;
            if(vc<=0&&d1>=0&&d3<=0){float v=d1/(d1-d3);bary=new Vector3(1-v,v,0);return a+v*ab;}
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);
            if(d6>=0&&d5<=d6){bary=Vector3.forward;return c;}
            float vb=d5*d2-d1*d6;
            if(vb<=0&&d2>=0&&d6<=0){float w=d2/(d2-d6);bary=new Vector3(1-w,0,w);return a+w*ac;}
            float va=d3*d6-d5*d4;
            if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0){float w=(d4-d3)/((d4-d3)+(d5-d6));bary=new Vector3(0,1-w,w);return b+w*(c-b);}
            float sum=va+vb+vc;if(Mathf.Abs(sum)<1e-20f){bary=Vector3.right;return a;}
            float y=vb/sum,z=vc/sum;bary=new Vector3(1-y-z,y,z);return a+ab*y+ac*z;
        }
    }
}
