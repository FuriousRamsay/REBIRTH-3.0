using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthWork : EAIBase
{
    // No config/installer/activation switch. Combat, native cancellation and teardown proofs are absent.
    internal static bool ActivationApproved=>RebirthNpcQualifiedWorkGate.InfrastructureEnabled;
    internal EntityRebirthNPC Actor {get;private set;}
    internal bool IsOwnedUpdate {get;private set;}
    public override void Init(EntityAlive entity){base.Init(entity);Actor=entity as EntityRebirthNPC;MutexBits=11;executeDelay=0.05f;}
    public override bool CanExecute()=>ActivationApproved&&Actor!=null&&!Actor.IsDead()&&
        (Actor.RebirthRuntimeState?.Order!=RebirthNpcOrderState.Follow||
            RebirthNpcEmbodiedWorkOwner.MayFinishEarnedUnderFollow(Actor))&&
        RebirthNpcEmbodiedWorkOwner.HasQualifiedLiteratureIntent(Actor);
    public override bool Continue()=>CanExecute();
    public override void Start(){ }
    public override void Update()
    {
        if(!CanExecute()||IsOwnedUpdate)return;
        IsOwnedUpdate=true;
        try{RebirthNpcEmbodiedWorkOwner.TryDispatchFromEai(this,Actor,out _);}
        finally{IsOwnedUpdate=false;}
    }
    public override void Reset()
    {
        // No generic release: placement may commit on release. Explicit typed teardown remains required.
        // Follow only PAUSES saved work (D-25): the staged owner, the reservation, the session and every saved
        // original stay exactly as they are, and the same assignment resumes through CanExecute when Follow ends.
        // Cancelling here would retain the owner unresolved (begun) or retire it (not begun) and strand the job.
        if(Actor!=null&&RebirthP6WorkFollowState.IsFollowing(Actor))return;
        var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(intent==null)RebirthNpcEmbodiedWorkOwner.TryGetLiteratureIntent(Actor,out intent);
        if(intent!=null&&ReferenceEquals(intent.Actor,Actor))
        {
            if(RebirthNpcQualifiedWorkGate.Allows(intent))
            {
                if(RebirthNpcLiteratureWorkGate.Allows(intent)&&
                    RebirthHumanRecipeReading.HasSavedWorkRead(intent))
                    return; // Saved learning must settle through its completion outcome, not cancellation.
                // The typed coordinator owns the saved book return, original
                // route, owner, assignment and reservation as one settlement.
                if(RebirthNpcLiteratureWorkGate.Allows(intent))
                {
                    RebirthNpcWorkAssignmentCoordinator.Suspend(intent.PrimaryId,
                        "Original literature EAI interrupted.");
                    return;
                }
            }
            RebirthNpcEmbodiedWorkOwner.RequestCancel(intent,out _);
        }
    }
}
