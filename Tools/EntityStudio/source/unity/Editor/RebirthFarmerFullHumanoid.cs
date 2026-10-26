using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    // Full humanoid anatomy and the roll/finger naming observed on the captured
    // Worker. Coordinates are initial Farmer landmarks, requiring mesh pose review.
    public static class RebirthFarmerFullHumanoid
    {
        public static void FitHandsToSource(RebirthEntityRigProfile profile,GameObject model)
        {
            var raw=new List<Vector3>();Bounds bounds=new Bounds();bool has=false;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))foreach(Vector3 vertex in filter.sharedMesh.vertices)
            {Vector3 point=model.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));raw.Add(point);if(!has){bounds=new Bounds(point,Vector3.zero);has=true;}else bounds.Encapsulate(point);}
            var points=new List<Vector3>();foreach(Vector3 point in raw)points.Add(RebirthEntityRigBuilder.NormalizeBonePositionInSourceBounds(bounds,point,profile.sourceUpAxis));
            foreach(string side in new[]{"Left","Right"})
            {
                bool left=side=="Left";var wrist=profile.bones.Find(value=>value.name==side+"Hand");var hand=new List<Vector3>();
                foreach(Vector3 point in points)if((left?point.x<.145f:point.x>.855f)&&point.y>.30f&&point.y<wrist.normalizedPosition.y-.005f)hand.Add(point);
                if(hand.Count<16)throw new InvalidOperationException("Too few source hand samples for "+side);
                var heights=new List<float>();var depths=new List<float>();foreach(var point in hand){heights.Add(point.y);depths.Add(point.z);}heights.Sort();depths.Sort();
                float minY=heights[Mathf.FloorToInt((heights.Count-1)*.02f)],minZ=depths[Mathf.FloorToInt((depths.Count-1)*.05f)],maxZ=depths[Mathf.FloorToInt((depths.Count-1)*.95f)];
                float length=wrist.normalizedPosition.y-minY;
                string[] fingers={"Thumb","Index","Middle","Ring","Pinky"};float[] ends={.55f,.88f,.98f,.90f,.72f};
                for(int finger=0;finger<5;finger++)for(int joint=1;joint<=4;joint++)
                {
                    var spec=profile.bones.Find(value=>value.name==side+"Hand"+fingers[finger]+joint);
                    float progress=Mathf.Lerp(finger==0?.16f:.32f,ends[finger],(joint-1)/3f);
                    Vector3 target=new Vector3(wrist.normalizedPosition.x,wrist.normalizedPosition.y-length*progress,Mathf.Lerp(minZ,maxZ,(finger+.5f)/5f));
                    var nearest=new List<KeyValuePair<float,Vector3>>();
                    foreach(var point in hand)
                    {
                        Vector3 delta=point-target;float score=delta.y*delta.y*36f+delta.z*delta.z*2f+delta.x*delta.x;
                        int at=nearest.FindIndex(value=>value.Key>score);if(at<0)at=nearest.Count;
                        if(at<16){nearest.Insert(at,new KeyValuePair<float,Vector3>(score,point));if(nearest.Count>16)nearest.RemoveAt(16);}
                    }
                    Vector3 center=Vector3.zero;foreach(var point in nearest)center+=point.Value;center/=nearest.Count;
                    spec.normalizedPosition=new Vector3(center.x,target.y,center.z);
                    spec.useInfluenceBounds=true;spec.influenceMin=new Vector3(left?-.01f:.78f,minY-.015f,minZ-.05f);spec.influenceMax=new Vector3(left?.22f:1.01f,wrist.normalizedPosition.y+.01f,maxZ+.05f);
                }
                foreach(string finger in fingers)
                {
                    // Independently snapping each joint to surface samples can
                    // alternate between the palm and back. Fit a centreline to
                    // the four landmarks before creating bone axes/bind poses.
                    var chain=new List<EntityBoneSpec>();Vector3 mean=Vector3.zero;
                    for(int joint=1;joint<=4;joint++){var spec=profile.bones.Find(value=>value.name==side+"Hand"+finger+joint);chain.Add(spec);mean+=spec.normalizedPosition;}
                    mean/=chain.Count;float variance=0,covarianceX=0,covarianceZ=0;
                    foreach(var spec in chain){Vector3 delta=spec.normalizedPosition-mean;variance+=delta.y*delta.y;covarianceX+=delta.y*delta.x;covarianceZ+=delta.y*delta.z;}
                    if(variance<1e-10f)throw new InvalidOperationException("Finger landmarks have no usable length.");
                    foreach(var spec in chain){float along=spec.normalizedPosition.y-mean.y;spec.normalizedPosition=new Vector3(mean.x+along*covarianceX/variance,spec.normalizedPosition.y,mean.z+along*covarianceZ/variance);}
                }
                Debug.Log("FARMER HAND FIT "+side+" samples="+hand.Count+" normalizedY="+minY+".."+wrist.normalizedPosition.y+" normalizedZ="+minZ+".."+maxZ+" straightFingerCentrelines=true");
            }
        }

        public static void Extend(RebirthEntityRigProfile profile)
        {
            foreach(string side in new[]{"Left","Right"})
            {
                bool left=side=="Left";float x=left?.10f:.90f;
                var arm=Find(side+"Arm");var fore=Find(side+"ForeArm");var hand=Find(side+"Hand");
                Add(side+"ArmRoll",arm.name,Vector3.Lerp(arm.normalizedPosition,fore.normalizedPosition,.5f));
                fore.parent=side+"ArmRoll";
                Add(side+"ForeArmRoll",fore.name,Vector3.Lerp(fore.normalizedPosition,hand.normalizedPosition,.5f));
                hand.parent=side+"ForeArmRoll";
                string[] fingers={"Thumb","Index","Middle","Ring","Pinky"};
                float[] depth={.29f,.32f,.40f,.48f,.55f};float[] tipY={.389f,.359f,.345f,.354f,.373f};
                for(int finger=0;finger<fingers.Length;finger++)
                {
                    string parent=hand.name;float baseY=finger==0?.450f:.425f;
                    for(int joint=1;joint<=4;joint++)
                    {
                        float t=(joint-1)/3f;
                        float fingerX=finger==0?Mathf.Lerp(left?.14f:.86f,left?.18f:.82f,t):x;
                        string name=side+"Hand"+fingers[finger]+joint;
                        var spec=Add(name,parent,new Vector3(fingerX,Mathf.Lerp(baseY,tipY[finger],t),depth[finger]),joint<4);
                        spec.useInfluenceBounds=true;spec.influenceMin=new Vector3(left?-.01f:.78f,tipY[finger]-.025f,depth[finger]-.065f);
                        spec.influenceMax=new Vector3(left?.22f:1.01f,.475f,depth[finger]+.065f);
                        parent=name;
                    }
                }
                Vector3 foot=Find(side+"Foot").normalizedPosition;
                Add(side+"ToeBase",side+"Foot",new Vector3(foot.x,.027f,.20f));
                Add(side+"ToeEnd",side+"ToeBase",new Vector3(foot.x,.027f,.055f),false);
            }
            var jaw=Add("Jaw","Head",new Vector3(.5f,.91f,.22f));jaw.useInfluenceBounds=true;jaw.influenceMin=new Vector3(.40f,.89f,-.01f);jaw.influenceMax=new Vector3(.60f,.95f,.42f);
            Add("HeadEnd","Head",new Vector3(.5f,.995f,.40f),false);
            // The native roll bones sit between the existing joints. Preserve
            // parent-before-child ordering required by the hierarchy builder.
            var pending=new List<EntityBoneSpec>(profile.bones);var ordered=new List<EntityBoneSpec>();var known=new HashSet<string>{""};
            while(pending.Count>0)
            {
                int count=pending.Count;
                for(int i=0;i<pending.Count;)
                {var spec=pending[i];if(spec.name=="Origin"||known.Contains(spec.parent??"")){ordered.Add(spec);known.Add(spec.name);pending.RemoveAt(i);}else i++;}
                if(count==pending.Count)throw new InvalidOperationException("Full humanoid rig has an unresolved bone parent.");
            }
            profile.bones=ordered;
            EntityBoneSpec Find(string name){return profile.bones.Find(value=>value.name==name)??throw new InvalidOperationException("Missing humanoid seed "+name);}
            EntityBoneSpec Add(string name,string parent,Vector3 point,bool deform=true)
            {
                var spec=profile.bones.Find(value=>value.name==name);
                if(spec==null){spec=new EntityBoneSpec{name=name};profile.bones.Add(spec);}
                spec.parent=parent;spec.normalizedPosition=point;spec.deformsMesh=deform;return spec;
            }
        }
    }
}
