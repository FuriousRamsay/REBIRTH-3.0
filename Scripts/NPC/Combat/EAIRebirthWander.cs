using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthWander : EAIWander
{
    private EntityRebirthNPC actor;
    private float turnWindowStart,turnWindowDegrees,lastYaw;
    private float waypointArcDegrees,waypointTravel,lastWaypointAngle;
    private Vector3 lastWaypoint,lastWaypointActorPosition;
    private bool trackingWaypoint,hasWaypointAngle;
    private bool orbiting;
    private float eligibleSince,normalExecutePercent;
    public override void Init(EntityAlive entity)
    {
        base.Init(entity);
        actor=entity as EntityRebirthNPC;
        normalExecutePercent=Mathf.Max(executePercent,0.5f);
        eligibleSince=Time.time;
    }
    private bool Ready()=>RebirthHumanAiPolicy.Ready(actor)&&!RebirthHumanAiPolicy.Owned(actor)&&
        actor.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.None&&actor.RebirthRuntimeState.Order==RebirthNpcOrderState.None&&
        actor.GetAttackTarget()==null&&!actor.Buffs.HasBuff("buffTalkingTo")&&actor.GetTicksNoPlayerAdjacent()<120;
    public override bool CanExecute()
    {
        if(!Ready())
        {
            eligibleSince=Time.time;
            RebirthNpcAiMovementTrace.WanderDecision(actor,"ready");
            return false;
        }
        // Human NPC AI is evaluated less often than the native wander roll
        // assumes. Keep its selection chance high enough to avoid long idle
        // tails after a completed route, while leaving other task priorities
        // and the native destination check intact.
        // The native random roll can leave an otherwise eligible NPC idle for
        // an unbounded tail. After six seconds, pass only that roll; native
        // sleep/look/stun/adjacency and destination checks still decide.
        executePercent=Time.time-eligibleSince>=6f
            ?Mathf.Max(normalExecutePercent,1f/Mathf.Max(executeWaitTime,0.01f))
            :normalExecutePercent;
        if(!base.CanExecute()){RebirthNpcAiMovementTrace.WanderDecision(actor,"native");return false;}
        Vector3 nativeGoal=position;
        Vector3 nativeOffset=nativeGoal-actor.position;
        float nativeDistanceSq=nativeOffset.x*nativeOffset.x+nativeOffset.z*nativeOffset.z;
        bool nativeHeight=Mathf.Abs(nativeOffset.y)<=1.5f;
        Vector3 heading=actor.GetForwardVector();
        heading.y=0f;
        heading.Normalize();
        // Tiny random hops around the last goal make the human turn back and
        // forth after every arrival. Prefer a walk of at least five metres
        // that does not immediately reverse the current facing direction.
        bool nativeForward=nativeOffset.x*heading.x+nativeOffset.z*heading.z>=
            -0.1f*Mathf.Sqrt(nativeDistanceSq);
        if(nativeHeight&&nativeDistanceSq>=25f&&nativeForward)
        {
            RebirthNpcAiMovementTrace.WanderDecision(actor,"accepted");
            return true;
        }
        for(int i=0;i<8;i++)
        {
            if(!RandomPositionGenerator.CalcAround(actor,10,3,actor.isSwimming,out Vector3 farther))break;
            Vector3 offset=farther-actor.position;
            float distanceSq=offset.x*offset.x+offset.z*offset.z;
            if(Mathf.Abs(offset.y)>1.5f||distanceSq<25f||
                offset.x*heading.x+offset.z*heading.z<-0.1f*Mathf.Sqrt(distanceSq))continue;
            position=farther;
            RebirthNpcAiMovementTrace.WanderDecision(actor,"accepted");
            return true;
        }
        // Preserve a safe native destination if terrain offers no preferred
        // route; a short walk is still better than an unbounded idle.
        if(nativeHeight&&nativeDistanceSq>=4f)
        {
            position=nativeGoal;
            RebirthNpcAiMovementTrace.WanderDecision(actor,"accepted");
            return true;
        }
        // Native candidates on steep ground can be above/below the actor.
        // Try a closer level route before giving up this selection.
        for(int i=0;i<4;i++)
        {
            if(!RandomPositionGenerator.CalcAround(actor,6,3,actor.isSwimming,out Vector3 nearby))
                break;
            Vector3 offset=nearby-actor.position;
            if(Mathf.Abs(offset.y)>1.5f||offset.x*offset.x+offset.z*offset.z<4f)
                continue;
            position=nearby;
            RebirthNpcAiMovementTrace.WanderDecision(actor,"accepted");
            return true;
        }
        RebirthNpcAiMovementTrace.WanderDecision(actor,"height");
        return false;
    }
    public override void Start()
    {
        if(!Ready())return;
        turnWindowStart=Time.time;turnWindowDegrees=0f;
        waypointArcDegrees=waypointTravel=0f;trackingWaypoint=hasWaypointAngle=false;
        lastYaw=actor.rotation.y;orbiting=false;
        executePercent=normalExecutePercent;
        RebirthNpcAiMovementTrace.Task(actor,"wander","start",position);
        base.Start();
    }
    public override void Update()
    {
        base.Update();
        if(actor!=null&&!orbiting)
        {
            float yaw=actor.rotation.y;
            float turn=Mathf.Abs(Mathf.DeltaAngle(lastYaw,yaw));
            turnWindowDegrees+=turn;
            lastYaw=yaw;
            var helper=actor.moveHelper;
            Vector3 waypoint=helper==null?Vector3.zero:helper.moveToPos;
            Vector3 offset=waypoint-actor.position;
            bool nearWaypoint=helper!=null&&helper.IsActive&&
                offset.x*offset.x+offset.z*offset.z<=1.25f*1.25f;
            if(!nearWaypoint)
            {
                trackingWaypoint=false;
                hasWaypointAngle=false;
                waypointArcDegrees=waypointTravel=0f;
            }
            else
            {
                Vector3 waypointShift=waypoint-lastWaypoint;
                if(!trackingWaypoint||waypointShift.x*waypointShift.x+waypointShift.z*waypointShift.z>0.5f*0.5f)
                {
                    trackingWaypoint=true;
                    lastWaypoint=waypoint;
                    lastWaypointActorPosition=actor.position;
                    waypointArcDegrees=waypointTravel=0f;
                    hasWaypointAngle=offset.x*offset.x+offset.z*offset.z>=0.3f*0.3f;
                    if(hasWaypointAngle)lastWaypointAngle=Mathf.Atan2(offset.z,offset.x)*Mathf.Rad2Deg;
                }
                else
                {
                    Vector3 bodyStep=actor.position-lastWaypointActorPosition;
                    float travelled=Mathf.Sqrt(bodyStep.x*bodyStep.x+bodyStep.z*bodyStep.z);
                    if(travelled>=0.01f)
                    {
                        waypointTravel+=travelled;
                        if(offset.x*offset.x+offset.z*offset.z>=0.3f*0.3f)
                        {
                            float angle=Mathf.Atan2(offset.z,offset.x)*Mathf.Rad2Deg;
                            if(hasWaypointAngle)waypointArcDegrees+=Mathf.Abs(Mathf.DeltaAngle(lastWaypointAngle,angle));
                            lastWaypointAngle=angle;
                            hasWaypointAngle=true;
                        }
                        else hasWaypointAngle=false;
                        lastWaypointActorPosition=actor.position;
                    }
                    // Turning in place is not an orbit. End the route only when
                    // the body has actually travelled around this waypoint.
                    if(waypointTravel>=0.8f&&waypointArcDegrees>=145f)
                    {
                        orbiting=true;
                        RebirthNpcAiMovementTrace.Task(actor,"wander","waypoint-orbit",waypoint);
                    }
                }
            }
            if(Time.time-turnWindowStart>=3f)
            {
                if(!orbiting&&turnWindowDegrees>=360f&&helper!=null&&helper.IsActive)
                {
                    orbiting=true;
                    RebirthNpcAiMovementTrace.Task(actor,"wander","orbit",position);
                }
                turnWindowStart=Time.time;turnWindowDegrees=0f;
            }
        }
        RebirthNpcAiMovementTrace.Sample(actor);
    }
    public override bool Continue()
    {
        if(!Ready())
        {
            RebirthNpcAiMovementTrace.Task(actor,"wander","end-not-ready");
            return false;
        }
        if(orbiting)
        {
            RebirthNpcAiMovementTrace.Task(actor,"wander","end-orbit");
            return false;
        }
        // Native wander tries to hit the exact block-centre waypoint. At walking
        // speed the NPC passes through it, reverses, and circles the same spot.
        Vector3 remaining=position-actor.position;
        // The navigation path may end on sloped ground below its original goal
        // height, so arrival here must use the ground-plane distance only.
        if(remaining.x*remaining.x+remaining.z*remaining.z<=0.8f*0.8f)
        {
            RebirthNpcAiMovementTrace.Task(actor,"wander","end-arrived",position);
            return false;
        }
        bool native=base.Continue();
        if(!native)RebirthNpcAiMovementTrace.Task(actor,"wander","end-native",position);
        return native;
    }
    public override void Reset()
    {
        RebirthNpcAiMovementTrace.Task(actor,"wander","end");
        base.Reset();
        // Native Wander arms EAIManager.lookTime for a separate EAILook task.
        // Human NPCs have no Look task, so nothing clears it and later Wander
        // selections fail indefinitely. Restore eligibility after this route.
        if(manager!=null)manager.lookTime=0f;
        executePercent=normalExecutePercent;
        eligibleSince=Time.time;
        if(actor==null)return;
        // Native Stop() ends the route; clear the retained movement command too.
        actor.SetMoveForward(0f);
    }
}
