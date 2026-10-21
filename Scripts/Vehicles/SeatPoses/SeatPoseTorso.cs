using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace Rebirth.Vehicles.Authoring
{
    // Native RigBuilder outputs use PreviousInputs at priority 1000. This
    // earlier output modifies the native animated stream before limb IK.
    internal sealed class SeatPoseTorso
    {
        struct LeanJob : IAnimationJob
        {
            public TransformStreamHandle spine, root;
            public float degrees;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                if (degrees == 0 || !spine.IsValid(stream) || !root.IsValid(stream)) return;
                spine.SetRotation(stream, Quaternion.AngleAxis(degrees, root.GetRotation(stream) * Vector3.right) * spine.GetRotation(stream));
            }
        }
        readonly Animator animator;
        AnimationScriptPlayable node;
        internal SeatPoseTorso(Animator value) { animator = value; }
        internal void Apply(float degrees, PlayableGraph graph)
        {
            if (node.IsValid()) { var data = node.GetJobData<LeanJob>(); data.degrees = degrees; node.SetJobData(data); return; }
            if (degrees == 0) return;
            if (!graph.IsValid()) throw new InvalidOperationException("Native animation graph unavailable for torso lean.");
            var spine = animator.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Spine");
            node = AnimationScriptPlayable.Create(graph, new LeanJob { spine = animator.BindStreamTransform(spine), root = animator.BindStreamTransform(animator.transform), degrees = degrees });
            var output = AnimationPlayableOutput.Create(graph, "REBIRTH Torso Before Limb IK", animator);
            output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
            output.SetSortingOrder(999);
            output.SetSourcePlayable(node);
        }
        // The owning SeatPoseRig rebuilds the native graph on disposal.
        internal void Clear() { if (node.IsValid()) { var data = node.GetJobData<LeanJob>(); data.degrees = 0; node.SetJobData(data); } }
    }
}
