using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Orthographic mesh occupancy provides an explicit cost for empty space hit
    // by a capsule. Coverage remains a separate hard constraint supplied by caller.
    public sealed class RebirthEntitySilhouetteFit
    {
        private struct Pixel { public Vector2 point;public bool side; }
        private struct Shape { public Vector2 a,b;public float radiusSquared; }
        private struct Score { public int front,side; }
        private readonly List<Pixel> negative=new List<Pixel>();

        public RebirthEntitySilhouetteFit(RebirthEntityRigWorkspace workspace)
        {
            var points=new List<Vector3>();var triangles=new List<int>();Bounds bounds=new Bounds();bool has=false;
            foreach(var renderer in workspace.sourceSkinnedMeshes)
            {
                int offset=points.Count;
                foreach(Vector3 vertex in renderer.sharedMesh.vertices){Vector3 point=renderer.transform.TransformPoint(vertex);points.Add(point);if(!has){bounds=new Bounds(point,Vector3.zero);has=true;}else bounds.Encapsulate(point);}
                foreach(int index in renderer.sharedMesh.triangles)triangles.Add(offset+index);
            }
            if(!has)throw new InvalidOperationException("Silhouette fitting requires mesh geometry.");
            const float step=.0125f;
            foreach(bool side in new[]{false,true})
            {
                float left=(side?bounds.min.z:bounds.min.x)-.05f,bottom=bounds.min.y-.05f;
                int width=Mathf.CeilToInt(((side?bounds.size.z:bounds.size.x)+.1f)/step)+1,height=Mathf.CeilToInt((bounds.size.y+.1f)/step)+1;
                var occupied=new bool[width*height];
                for(int triangle=0;triangle<triangles.Count;triangle+=3)
                {
                    Vector2 a=Project(points[triangles[triangle]],side),b=Project(points[triangles[triangle+1]],side),c=Project(points[triangles[triangle+2]],side);
                    float area=Cross(b-a,c-a);if(Mathf.Abs(area)<1e-10f)continue;
                    int minX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x,c.x)-left)/step),0,width-1),maxX=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x,c.x)-left)/step),0,width-1);
                    int minY=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y,b.y,c.y)-bottom)/step),0,height-1),maxY=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y,b.y,c.y)-bottom)/step),0,height-1);
                    for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                    {
                        Vector2 p=new Vector2(left+x*step,bottom+y*step);float u=Cross(b-a,p-a)/area,v=Cross(p-a,c-a)/area;
                        if(u>=-1e-5f&&v>=-1e-5f&&u+v<=1.00001f)occupied[y*width+x]=true;
                    }
                }
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(!occupied[y*width+x])negative.Add(new Pixel{point=new Vector2(left+x*step,bottom+y*step),side=side});
            }
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static Vector2 Project(Vector3 p,bool side)=>new Vector2(side?p.z:p.x,p.y);
        private static Shape Projection(CapsuleCollider capsule,bool side)
        {
            Vector3 scale=capsule.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            float across=capsule.direction==0?Mathf.Max(scale.y,scale.z):capsule.direction==1?Mathf.Max(scale.x,scale.z):Mathf.Max(scale.x,scale.y),radius=capsule.radius*across;
            Vector3 axis=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
            axis=capsule.transform.TransformDirection(axis).normalized*Mathf.Max(0,capsule.height*scale[capsule.direction]*.5f-radius);
            Vector3 center=capsule.transform.TransformPoint(capsule.center);
            return new Shape{a=Project(center-axis,side),b=Project(center+axis,side),radiusSquared=radius*radius};
        }
        private static bool Contains(Shape shape,Vector2 point)
        {
            Vector2 segment=shape.b-shape.a;float t=segment.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector2.Dot(point-shape.a,segment)/segment.sqrMagnitude):0;
            return (point-shape.a-segment*t).sqrMagnitude<=shape.radiusSquared;
        }
        public Vector2Int Footprint(CapsuleCollider capsule)
        {
            Shape front=Projection(capsule,false),side=Projection(capsule,true);var count=Vector2Int.zero;
            foreach(var pixel in negative)if(Contains(pixel.side?side:front,pixel.point)){if(pixel.side)count.y++;else count.x++;}return count;
        }
        public void Optimize(CapsuleCollider capsule,CapsuleCollider[] all,Func<bool> coversRequired,float maximumRadius,Vector2 axialSpan)
        {
            var exposed=new List<Pixel>();
            var fronts=new List<Shape>();var sides=new List<Shape>();
            foreach(var other in all)if(other!=capsule){fronts.Add(Projection(other,false));sides.Add(Projection(other,true));}
            foreach(var pixel in negative)
            {
                bool blocked=false;foreach(var shape in pixel.side?sides:fronts)if(Contains(shape,pixel.point)){blocked=true;break;}if(!blocked)exposed.Add(pixel);
            }
            Score Measure()
            {
                Shape front=Projection(capsule,false),side=Projection(capsule,true);var score=new Score();
                foreach(var pixel in exposed)if(Contains(pixel.side?side:front,pixel.point)){if(pixel.side)score.side++;else score.front++;}return score;
            }
            Score initial=Measure(),best=initial;
            foreach(float step in new[]{.015f,.0075f,.00375f})for(int parameter=0;parameter<4;parameter++)foreach(int sign in new[]{-1,1})
            {
                Vector3 center=capsule.center;float height=capsule.height,radius=capsule.radius;
                if(parameter<3){Vector3 moved=center;moved[parameter]+=step*sign;capsule.center=moved;}else capsule.height=Mathf.Max(.03f,height+step*2*sign);
                bool valid=capsule.center[capsule.direction]-capsule.height*.5f>=axialSpan.x-.005f&&capsule.center[capsule.direction]+capsule.height*.5f<=axialSpan.y+.005f;
                if(valid)
                {
                    capsule.radius=maximumRadius;valid=coversRequired();
                    if(valid)
                    {
                        float low=.015f,high=maximumRadius;
                        for(int iteration=0;iteration<14;iteration++){capsule.radius=(low+high)*.5f;if(coversRequired())high=capsule.radius;else low=capsule.radius;}
                        capsule.radius=high;Score candidate=Measure();
                        valid=candidate.front<=best.front&&candidate.side<=best.side&&(candidate.front<best.front||candidate.side<best.side);
                        if(valid)best=candidate;
                    }
                }
                if(!valid){capsule.center=center;capsule.height=height;capsule.radius=radius;}
            }
            Debug.Log("COLLIDER SILHOUETTE FIT: "+capsule.name+" uniqueEmptyFront="+initial.front+"->"+best.front+" uniqueEmptySide="+initial.side+"->"+best.side);
        }
    }
}
