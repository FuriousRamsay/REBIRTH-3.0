using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebirthEntityStudio
{
    public static class RebirthEntityPhysicsReview
    {
        [Serializable]
        public sealed class Result
        {public int bodies,joints;public float simulatedSeconds,maxJointSeparation,maxSpeed,lowestColliderPoint,pelvisVerticalDrop;}

        // Explicit authoring diagnostic. The review scene is saved before this call;
        // its simulated pose is temporary and never replaces the bind/reference pose.
        public static Result Simulate(RebirthEntityRigWorkspace workspace,List<GameObject> temporary)
        {
            var floor=new GameObject("__RagdollReviewFloor");temporary.Add(floor);
            floor.transform.position=new Vector3(0f,-.1f,0f);floor.AddComponent<BoxCollider>().size=new Vector3(10f,.2f,10f);
            Animator animator=workspace.GetComponent<Animator>();if(animator!=null)animator.enabled=false;
            Rigidbody[] bodies=workspace.generatedSkeleton.GetComponentsInChildren<Rigidbody>(true);
            CharacterJoint[] joints=workspace.generatedSkeleton.GetComponentsInChildren<CharacterJoint>(true);
            var result=new Result{bodies=bodies.Length,joints=joints.Length,simulatedSeconds=3f,lowestColliderPoint=float.PositiveInfinity};
            Transform pelvis=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,"Hips");float startY=pelvis.position.y;
            SimulationMode automatic=Physics.simulationMode;
            try
            {
                Physics.simulationMode=SimulationMode.Script;
                foreach(var mode in workspace.profile.colliderModes)
                {
                    Transform bone=RebirthEntityRigBuilder.FindBone(workspace.generatedSkeleton,mode.bone);var capsule=bone!=null?bone.GetComponent<CapsuleCollider>():null;
                    if(capsule==null)continue;capsule.radius*=mode.ragdollScale.x;capsule.height*=mode.ragdollScale.y;capsule.center+=mode.ragdollOffset;capsule.gameObject.layer=mode.ragdollLayer;
                }
                foreach(var body in bodies){body.isKinematic=false;body.velocity=Vector3.forward*.6f;}
                Physics.SyncTransforms();
                for(int step=0;step<360;step++)
                {
                    Physics.Simulate(1f/120f);
                    foreach(var body in bodies)
                    {
                        if(!Finite(body.position)||body.position.magnitude>8f)throw new InvalidOperationException("Ragdoll body escaped or became non-finite: "+body.name);
                        result.maxSpeed=Mathf.Max(result.maxSpeed,body.velocity.magnitude);
                    }
                    foreach(var joint in joints)
                        if(joint.connectedBody!=null)
                            result.maxJointSeparation=Mathf.Max(result.maxJointSeparation,Vector3.Distance(joint.transform.TransformPoint(joint.anchor),joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)));
                }
                foreach(var collider in workspace.generatedSkeleton.GetComponentsInChildren<Collider>(true))result.lowestColliderPoint=Mathf.Min(result.lowestColliderPoint,collider.bounds.min.y);
                result.pelvisVerticalDrop=startY-pelvis.position.y;
                if(result.maxJointSeparation>.25f||result.maxSpeed>20f||result.lowestColliderPoint<-.15f||result.pelvisVerticalDrop<.1f)
                    throw new InvalidOperationException("Ragdoll review failed stability: "+JsonUtility.ToJson(result));
                Debug.Log("REBIRTH RAGDOLL REVIEW: "+JsonUtility.ToJson(result));return result;
            }
            finally{Physics.simulationMode=automatic;}
        }

        private static bool Finite(Vector3 value)
        {return !float.IsNaN(value.x)&&!float.IsNaN(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.x)&&!float.IsInfinity(value.y)&&!float.IsInfinity(value.z);}
    }
}
