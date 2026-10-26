using UnityEngine;
using UnityEngine.Scripting;

// Moves a FOLLOWING NPC to a nearby storage container for Smart Looting, then ends so Follow resumes.
// Combat tasks (AITask-1..4) outrank this one; Follow yields through RebirthP6FollowApproach.YieldsFollow.
[Preserve]
public sealed class EAIRebirthP6FollowApproach : EAIBase
{
    private EntityRebirthNPC actor; private float nextStep;
    public override void Init(EntityAlive entity){base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=1;executeDelay=.1f;}
    public override bool CanExecute()=>actor!=null&&RebirthP6FollowApproach.WantsEai(actor);
    public override bool Continue()=>CanExecute();
    public override void Start(){nextStep=0f;}
    public override void Update()
    {
        if(Time.time<nextStep)return;
        nextStep=Time.time+.75f;
        RebirthP6FollowApproach.Step(actor);
    }
    // No old-id resume (D-25): an interrupted approach is failed; the caller must request a new one.
    public override void Reset(){if(actor!=null)RebirthP6FollowApproach.Cancel(actor,"Approach task interrupted.");}
}
