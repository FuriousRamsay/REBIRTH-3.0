using System;
using System.Linq;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityHumanoidPoseProbe
    {
        public static void ApplyFingerCurl(RebirthEntityRigWorkspace workspace,string side,float amount=-.6f)
        {
            var animator=workspace.GetComponent<Animator>();if(animator==null||animator.avatar==null||!animator.avatar.isHuman)throw new InvalidOperationException("Humanoid avatar required for anatomical finger pose.");
            var transforms=workspace.GetComponentsInChildren<Transform>(true);var positions=transforms.ToDictionary(t=>t,t=>t.localPosition);var rotations=transforms.ToDictionary(t=>t,t=>t.localRotation);var scales=transforms.ToDictionary(t=>t,t=>t.localScale);
            var handler=new HumanPoseHandler(animator.avatar,workspace.transform);
            System.Collections.Generic.Dictionary<Transform,Quaternion> fingers=null;
            try
            {
                var pose=new HumanPose();handler.GetHumanPose(ref pose);int changed=0;
                for(int i=0;i<HumanTrait.MuscleCount;i++)
                {string name=HumanTrait.MuscleName[i];if(name.StartsWith(side,StringComparison.Ordinal)&&name.IndexOf("Stretched",StringComparison.Ordinal)>=0){pose.muscles[i]=amount;changed++;}}
                if(changed!=15)throw new InvalidOperationException("Expected 15 humanoid finger stretch muscles for "+side+", found "+changed);
                handler.SetHumanPose(ref pose);
                fingers=transforms.Where(t=>t.name.StartsWith(side+"Hand",StringComparison.Ordinal)&&t.name!=side+"Hand").ToDictionary(t=>t,t=>t.localRotation);
                Debug.Log("HUMANOID FINGER POSE side="+side+" muscles="+changed+" value="+amount);
            }
            finally
            {
                handler.Dispose();foreach(var t in transforms){t.localPosition=positions[t];t.localRotation=rotations[t];t.localScale=scales[t];}
            }
            if(fingers!=null)foreach(var pair in fingers)pair.Key.localRotation=pair.Value;
        }
    }
}
