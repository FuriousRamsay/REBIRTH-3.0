using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityFingerHitTrial
    {
        public static List<GameObject> Add(RebirthEntityRigWorkspace workspace)
        {
            var created=new List<GameObject>();
            try
            {
                foreach(string side in new[]{"Left","Right"})
                {
                    var hand=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true).Single(s=>s.name=="EntityStudio_Compound_"+side+"HandHit");
                    foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})
                    foreach(int segment in Array.IndexOf(Environment.GetCommandLineArgs(),"-proximalFingerHits")>=0?new[]{1,2}:new[]{2})
                    {
                        var bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Hand"+finger+segment);var end=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,side+"Hand"+finger+(segment==1?2:4));
                        if(bone==null||end==null)throw new InvalidOperationException("Finger trial needs complete distal chains.");
                        var go=new GameObject("EntityStudio_Compound_"+side+finger+"FingerHit"+segment);created.Add(go);go.transform.SetParent(bone,false);go.transform.rotation=Quaternion.FromToRotation(Vector3.up,(end.position-bone.position).normalized);go.layer=hand.gameObject.layer;go.tag=hand.tag;
                        var shape=go.AddComponent<CapsuleCollider>();shape.direction=1;shape.radius=.012f;float length=Vector3.Distance(bone.position,end.position);shape.height=length+.024f;shape.center=Vector3.up*length*.5f;shape.sharedMaterial=hand.sharedMaterial;shape.isTrigger=hand.isTrigger;shape.enabled=hand.enabled;
                        if(shape.attachedRigidbody!=hand.attachedRigidbody)throw new InvalidOperationException("Finger hit shape changed rigidbody ownership.");
                    }
                }
                Debug.Log("FINGER HIT TRIAL: added="+created.Count+" radius12mm, measured chain lengths; existing rigidbody/joints unchanged; no assets saved.");return created;
            }
            catch{foreach(var go in created)UnityEngine.Object.DestroyImmediate(go);throw;}
        }
        public static void FitRadii(RebirthEntityRigWorkspace workspace,List<GameObject> created,string folder)
        {
            var shapes=workspace.generatedSkeleton.GetComponentsInChildren<CapsuleCollider>(true);var fingers=new HashSet<CapsuleCollider>(created.Select(go=>go.GetComponent<CapsuleCollider>()));
            var rotations=workspace.generatedSkeleton.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.localRotation);var samples=RebirthEntityColliderPoseAudit.Capture(workspace,shapes,true,true);
            try
            {
                foreach(string side in new[]{"Left","Right"}){foreach(var pair in rotations)pair.Key.localRotation=pair.Value;RebirthEntityHumanoidPoseProbe.ApplyFingerCurl(workspace,side);var curled=RebirthEntityColliderPoseAudit.Capture(workspace,shapes,true,true);foreach(var sample in curled)sample.name=side+"Curled_"+sample.name;samples.AddRange(curled);}
            }
            finally{foreach(var pair in rotations)pair.Key.localRotation=pair.Value;}
            int adjusted=0,unhandled=0;
            foreach(var sample in samples)for(int point=0;point<sample.points.Length;point++)
            {
                bool covered=false;for(int i=0;i<shapes.Length;i++)if(RebirthEntityColliderPoseAudit.Allows(sample,point,i)&&RebirthEntityColliderPoseAudit.Contains(shapes[i],sample.frames[i],sample.points[point])){covered=true;break;}if(covered)continue;
                int chosen=-1;float chosenRadius=0,bestGrowth=float.PositiveInfinity;
                for(int i=0;i<shapes.Length;i++)
                {
                    var shape=shapes[i];if(!fingers.Contains(shape)||!RebirthEntityColliderPoseAudit.Allows(sample,point,i))continue;float original=shape.radius,low=original,high=.02f/sample.frames[i].across;
                    try
                    {
                        shape.radius=high;if(!RebirthEntityColliderPoseAudit.Contains(shape,sample.frames[i],sample.points[point]))continue;
                        for(int iteration=0;iteration<18;iteration++){float trial=(low+high)*.5f;shape.radius=trial;if(RebirthEntityColliderPoseAudit.Contains(shape,sample.frames[i],sample.points[point]))high=trial;else low=trial;}
                        float growth=(high-original)*sample.frames[i].across;if(growth<bestGrowth){bestGrowth=growth;chosen=i;chosenRadius=high;}
                    }
                    finally{shape.radius=original;}
                }
                if(chosen<0){unhandled++;continue;}shapes[chosen].radius=chosenRadius;adjusted++;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-fitFingerAxial")>=0)
            {
                var originalHeights=fingers.ToDictionary(shape=>shape,shape=>shape.height);int axialAdjusted=0;
                foreach(var sample in samples)for(int point=0;point<sample.points.Length;point++)
                {
                    bool covered=false;for(int i=0;i<shapes.Length;i++)if(RebirthEntityColliderPoseAudit.Allows(sample,point,i)&&RebirthEntityColliderPoseAudit.Contains(shapes[i],sample.frames[i],sample.points[point])){covered=true;break;}if(covered)continue;
                    int chosen=-1;float chosenHeight=0,bestGrowth=float.PositiveInfinity;
                    for(int i=0;i<shapes.Length;i++)
                    {
                        var shape=shapes[i];if(!fingers.Contains(shape)||!RebirthEntityColliderPoseAudit.Allows(sample,point,i))continue;float original=shape.height,low=original,high=originalHeights[shape]+.016f/sample.frames[i].along;
                        try
                        {
                            shape.height=high;if(!RebirthEntityColliderPoseAudit.Contains(shape,sample.frames[i],sample.points[point]))continue;
                            for(int iteration=0;iteration<18;iteration++){float trial=(low+high)*.5f;shape.height=trial;if(RebirthEntityColliderPoseAudit.Contains(shape,sample.frames[i],sample.points[point]))high=trial;else low=trial;}
                            float growth=(high-original)*sample.frames[i].along;if(growth<bestGrowth){bestGrowth=growth;chosen=i;chosenHeight=high;}
                        }
                        finally{shape.height=original;}
                    }
                    if(chosen>=0){shapes[chosen].height=chosenHeight;axialAdjusted++;}
                }
                Debug.Log("FINGER AXIAL FIT adjustments="+axialAdjusted+" maximumTotalHeightGrowth16mm; no original collider dimensions changed.");
            }
            var report=RebirthEntityColliderPoseAudit.Compare(samples,shapes);File.WriteAllText(Path.Combine(folder,"FingerRadiusFit_Coverage.json"),JsonUtility.ToJson(report,true));
            var rows=new List<string>{"shape,radius,height"};foreach(var shape in fingers)rows.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1:R},{2:R}",shape.name,shape.radius,shape.height));File.WriteAllLines(Path.Combine(folder,"FingerRadiusFit.csv"),rows);
            Debug.Log("FINGER RADIUS FIT samples="+report.poses.Sum(p=>p.samples)+" outside="+report.poses.Sum(p=>p.candidateOutside)+" adjustments="+adjusted+" unhandled="+unhandled+" maximumWorldRadius20mm; original colliders unchanged.");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-pruneFingerHits")>=0&&Array.IndexOf(Environment.GetCommandLineArgs(),"-deferFingerPruning")<0)Prune(samples,shapes,created,folder);
        }
        internal static void Prune(List<RebirthEntityColliderPoseAudit.Sample> samples,CapsuleCollider[] shapes,List<GameObject> created,string folder,string reportName="FingerHitPruning.csv")
        {
            if(shapes.Length>63)throw new InvalidOperationException("Coverage bitset supports at most 63 shapes.");
            var masks=new List<ulong>(samples.Sum(sample=>sample.points.Length));
            foreach(var sample in samples)for(int point=0;point<sample.points.Length;point++)
            {
                ulong mask=0;for(int i=0;i<shapes.Length;i++)if(RebirthEntityColliderPoseAudit.Allows(sample,point,i)&&RebirthEntityColliderPoseAudit.Contains(shapes[i],sample.frames[i],sample.points[point]))mask|=1UL<<i;masks.Add(mask);
            }
            ulong active=(1UL<<shapes.Length)-1;var rows=new List<string>{"shape,removed,exclusiveSamples"};int removed=0;
            foreach(var go in created.OrderByDescending(go=>{var c=go.GetComponent<CapsuleCollider>();return c.radius*c.radius*c.height;}).ToArray())
            {
                var shape=go.GetComponent<CapsuleCollider>();int index=Array.IndexOf(shapes,shape);ulong bit=1UL<<index;int exclusive=0;
                foreach(ulong mask in masks)if((mask&active)==bit)exclusive++;
                bool redundant=exclusive==0;rows.Add(shape.name+","+redundant+","+exclusive);
                if(redundant){active&=~bit;UnityEngine.Object.DestroyImmediate(go);removed++;}
            }
            created.RemoveAll(go=>go==null);File.WriteAllLines(Path.Combine(folder,reportName),rows);
            int uncovered=masks.Count(mask=>(mask&active)==0);Debug.Log("FINGER HIT PRUNING removed="+removed+" retained="+created.Count+" existingCoveredSamplesLost=0 remainingOutside="+uncovered+" across="+masks.Count);
        }
    }
}
