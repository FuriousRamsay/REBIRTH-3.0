using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthWork : EAIBase
{
    // No config/installer/activation switch. Combat, native cancellation and teardown proofs are absent.
    internal static bool ActivationApproved=>false;
    internal EntityRebirthNPC Actor {get;private set;}
    internal bool IsOwnedUpdate {get;private set;}
    public override void Init(EntityAlive entity){base.Init(entity);Actor=entity as EntityRebirthNPC;MutexBits=11;executeDelay=0.05f;}
    public override bool CanExecute()=>ActivationApproved&&Actor!=null&&!Actor.IsDead();
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
        var intent=RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent;
        if(intent!=null&&ReferenceEquals(intent.Actor,Actor))RebirthNpcEmbodiedWorkOwner.RequestCancel(intent,out _);
    }
}
