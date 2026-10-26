using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityHandPoseColliderFit
    {
        public static void Fit(RebirthEntityRigWorkspace workspace,string folder,bool fingersOnly=false)
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
            var rows=new List<string>{"shape,oldX,oldY,oldZ,newX,newY,newZ,radius,height,exclusiveSamples,remainingExclusiveMisses"};
            foreach(var shape in shapes.Where(s=>fingersOnly?s.name.Contains("FingerHit"):s.name.Contains("HandHit")))
            {
                bool orient=fingersOnly||Array.IndexOf(Environment.GetCommandLineArgs(),"-orientCurledHands")>=0;
                int index=Array.IndexOf(shapes,shape);var required=new List<(Vector3 point,bool hard)>();
                Matrix4x4 originalMatrix=shape.transform.localToWorldMatrix;Quaternion originalRotation=shape.transform.localRotation;
                foreach(var sample in samples)
                {
                    var toRest=shape.transform.localToWorldMatrix*sample.frames[index].localToWorld.inverse;
                    for(int p=0;p<sample.points.Length;p++)
                    {
                        if(!RebirthEntityColliderPoseAudit.Allows(sample,p,index))continue;
                        bool other=false;for(int i=0;i<shapes.Length;i++)if(i!=index&&RebirthEntityColliderPoseAudit.Allows(sample,p,i)&&RebirthEntityColliderPoseAudit.Contains(shapes[i],sample.frames[i],sample.points[p])){other=true;break;}
                        if(!other)required.Add((toRest.MultiplyPoint3x4(sample.points[p]),!sample.name.Contains("Curled_")));
                    }
                }
                Vector3 old=shape.center;var scale=shape.transform.lossyScale;float along=Mathf.Abs(scale[shape.direction]),across=Mathf.Max(Mathf.Abs(scale[(shape.direction+1)%3]),Mathf.Abs(scale[(shape.direction+2)%3]));float radius=shape.radius*across,half=Mathf.Max(0,shape.height*along*.5f-radius);
                Vector3 axis=shape.transform.TransformDirection(shape.direction==0?Vector3.right:shape.direction==1?Vector3.up:Vector3.forward).normalized;
                Vector3 originalWorldCenter=shape.transform.TransformPoint(old);
                (int hard,int missing,double error) Score()
                {
                    Vector3 center=shape.transform.TransformPoint(shape.center);int missing=0,hard=0;double error=0;
                    axis=shape.transform.TransformDirection(shape.direction==0?Vector3.right:shape.direction==1?Vector3.up:Vector3.forward).normalized;
                    foreach(var sample in required){Vector3 delta=sample.point-center;var radial=delta-axis*Mathf.Clamp(Vector3.Dot(delta,axis),-half,half);float excess=radial.sqrMagnitude-radius*radius-1e-8f;if(excess>0){missing++;if(sample.hard)hard++;error+=excess;}}
                    return (hard,missing,error);
                }
                var best=Score();
                foreach(float step in new[]{.008f,.004f,.002f,.001f,.0005f})for(int pass=0;pass<3;pass++)for(int dimension=0;dimension<(orient?6:3);dimension++)foreach(int sign in new[]{-1,1})
                {
                    Vector3 prior=shape.center;Quaternion priorRotation=shape.transform.localRotation;
                    if(dimension<3){Vector3 moved=prior;moved[dimension]+=step*sign;shape.center=moved;}else{Vector3 angle=Vector3.zero;angle[dimension-3]=step*1000*sign;shape.transform.localRotation*=Quaternion.Euler(angle);}
                    bool valid=Vector3.Distance(shape.transform.TransformPoint(shape.center),originalWorldCenter)<=.02f&&Quaternion.Angle(originalRotation,shape.transform.localRotation)<=20;
                    var trial=valid?Score():(int.MaxValue,int.MaxValue,double.PositiveInfinity);
                    bool better=orient?(trial.Item1<best.hard||(trial.Item1==best.hard&&(trial.Item2<best.missing||(trial.Item2==best.missing&&trial.Item3<best.error)))):(trial.Item2<best.missing||(trial.Item2==best.missing&&trial.Item3<best.error));
                    if(better)best=trial;else{shape.center=prior;shape.transform.localRotation=priorRotation;}
                }
                if(orient)
                {
                    Matrix4x4 delta=originalMatrix.inverse*shape.transform.localToWorldMatrix;Vector3 localAxis=shape.direction==0?Vector3.right:shape.direction==1?Vector3.up:Vector3.forward;
                    foreach(var sample in samples){var frame=sample.frames[index];frame.localToWorld=frame.localToWorld*delta;frame.axis=frame.localToWorld.MultiplyVector(localAxis).normalized;sample.frames[index]=frame;}
                }
                rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9},{10}",shape.name,old.x,old.y,old.z,shape.center.x,shape.center.y,shape.center.z,shape.radius,shape.height,required.Count,best.missing));
                Debug.Log("HAND POSE CENTER FIT shape="+shape.name+" displacement="+Vector3.Distance(originalWorldCenter,shape.transform.TransformPoint(shape.center))+" rotationDegrees="+Quaternion.Angle(originalRotation,shape.transform.localRotation)+" hardMisses="+best.hard+" exclusiveMisses="+best.missing+" radius/height unchanged.");
            }
            string reportName=fingersOnly?"FingerPlacementFit":"HandCenterFit";
            File.WriteAllLines(Path.Combine(folder,reportName+".csv"),rows);var report=RebirthEntityColliderPoseAudit.Compare(samples,shapes);File.WriteAllText(Path.Combine(folder,reportName+"_Coverage.json"),JsonUtility.ToJson(report,true));
            Debug.Log("HAND POSE CENTER FIT COMPLETE samples="+report.poses.Sum(p=>p.samples)+" outside="+report.poses.Sum(p=>p.candidateOutside)+" no scene saved.");
        }
    }
}
