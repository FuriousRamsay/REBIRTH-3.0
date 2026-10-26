using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityArmColliderVolumeFit
    {
        public static void Shrink(RebirthEntityRigWorkspace workspace,string folder,List<GameObject> pruneFingersAfter=null)
        {
            var shapes=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);
            var rotations=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localRotation);
            var samples=RebirthEntityColliderPoseAudit.Capture(workspace,shapes,true,true);
            try
            {
                foreach(string side in new[]{"Left","Right"})
                {
                    foreach(var pair in rotations)pair.Key.localRotation=pair.Value;
                    RebirthEntityHumanoidPoseProbe.ApplyFingerCurl(workspace,side);
                    var curled=RebirthEntityColliderPoseAudit.Capture(workspace,shapes,true,true);foreach(var sample in curled)sample.name=side+"Curled_"+sample.name;samples.AddRange(curled);
                }
            }
            finally{foreach(var pair in rotations)pair.Key.localRotation=pair.Value;}
            var rows=new List<string>{"shape,oldRadius,newRadius,oldHeight,newHeight,exclusiveSamples"};
            bool tradeLength=Array.IndexOf(Environment.GetCommandLineArgs(),"-tradeArmCapsuleLength")>=0;
            foreach(var shape in shapes.Where(s=>s.name=="LeftArm"||s.name=="RightArm"||s.name=="LeftForeArm"||s.name=="RightForeArm"||s.name.Contains("HandHit")||s.name.Contains("FingerHit")).OrderByDescending(s=>s.radius*s.radius*s.height))
            {
                int index=Array.IndexOf(shapes,shape);var required=new List<(Vector3 point,RebirthEntityColliderPoseAudit.Frame frame)>();
                foreach(var sample in samples)for(int point=0;point<sample.points.Length;point++)
                {
                    if(!RebirthEntityColliderPoseAudit.Allows(sample,point,index)||!RebirthEntityColliderPoseAudit.Contains(shape,sample.frames[index],sample.points[point]))continue;
                    bool other=false;for(int i=0;i<shapes.Length;i++)if(i!=index&&RebirthEntityColliderPoseAudit.Allows(sample,point,i)&&RebirthEntityColliderPoseAudit.Contains(shapes[i],sample.frames[i],sample.points[point])){other=true;break;}
                    if(!other)required.Add((sample.points[point],sample.frames[index]));
                }
                bool Conserves(){foreach(var sample in required)if(!RebirthEntityColliderPoseAudit.Contains(shape,sample.frame,sample.point))return false;return true;}
                float oldRadius=shape.radius,oldHeight=shape.height;
                float low=Mathf.Min(shape.radius,.004f/samples[0].frames[index].across),high=shape.radius;
                for(int step=0;step<24;step++){shape.radius=(low+high)*.5f;if(Conserves())high=shape.radius;else low=shape.radius;}shape.radius=high;
                low=Mathf.Min(shape.height,shape.radius*2);high=shape.height;
                for(int step=0;step<24;step++){shape.height=(low+high)*.5f;if(Conserves())high=shape.height;else low=shape.height;}shape.height=high;
                if(tradeLength&&required.Count>0)
                {
                    var local=new List<(float axial,float radialSquared,float along,float across)>();
                    foreach(var sample in required)
                    {
                        var delta=sample.point-sample.frame.localToWorld.MultiplyPoint3x4(shape.center);float axial=Mathf.Abs(Vector3.Dot(delta,sample.frame.axis));
                        local.Add((axial,Mathf.Max(0,delta.sqrMagnitude-axial*axial),sample.frame.along,sample.frame.across));
                    }
                    var frame=samples[0].frames[index];
                    double Volume(float radius,float height){double r=radius*frame.across,h=height*frame.along;return Math.PI*r*r*Math.Max(0,h-2*r)+4.0/3*Math.PI*r*r*r;}
                    float bestRadius=shape.radius,bestHeight=shape.height;double bestVolume=Volume(bestRadius,bestHeight);
                    for(int step=1;step<=16;step++)
                    {
                        float height=oldHeight+step*.005f/frame.along,radius=.004f/frame.across;
                        foreach(var point in local)
                        {
                            float half=height*point.along*.5f,k=half-point.axial,radial=Mathf.Sqrt(point.radialSquared),worldRadius;
                            if(k>=radial)worldRadius=radial;
                            else if(k>0)
                            {float cap=(point.radialSquared+k*k)/(2*k);worldRadius=cap<=half?cap:Mathf.Sqrt(point.axial*point.axial+point.radialSquared);}
                            else worldRadius=Mathf.Sqrt(point.axial*point.axial+point.radialSquared);
                            radius=Mathf.Max(radius,(worldRadius+1e-6f)/point.across);
                        }
                        double volume=Volume(radius,height);if(radius>bestRadius||volume>=bestVolume)continue;
                        shape.radius=radius;shape.height=height;
                        if(Conserves()){bestRadius=radius;bestHeight=height;bestVolume=volume;}
                    }
                    shape.radius=bestRadius;shape.height=bestHeight;
                }
                if(!Conserves())throw new InvalidOperationException("Arm capsule shrink lost an exclusive point: "+shape.name);
                rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5}",shape.name,oldRadius,shape.radius,oldHeight,shape.height,required.Count));
            }
            var report=RebirthEntityColliderPoseAudit.Compare(samples,shapes);
            if(report.poses.Sum(p=>p.newMisses)!=0)throw new InvalidOperationException("Arm capsule shrink lost previously covered points.");
            File.WriteAllLines(Path.Combine(folder,"ArmColliderVolumeFit.csv"),rows);File.WriteAllText(Path.Combine(folder,"ArmColliderVolumeFit_Coverage.json"),JsonUtility.ToJson(report,true));
            Debug.Log("ARM CAPSULE VOLUME FIT: previously covered points lost=0; outside="+report.poses.Sum(p=>p.candidateOutside)+"; "+(tradeLength?"bounded length/radius trade requires lower capsule volume":"radius/height only decrease")+"; no assets saved.");
            if(pruneFingersAfter!=null)RebirthEntityFingerHitTrial.Prune(samples,shapes,pruneFingersAfter,folder,"FingerHitPruning_AfterVolume.csv");
        }
    }
}
