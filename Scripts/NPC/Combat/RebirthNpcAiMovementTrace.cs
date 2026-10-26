using System;
using System.Collections.Generic;
using UnityEngine;

// Opt-in, bounded evidence for a single human-NPC spawn observation.
internal static class RebirthNpcAiMovementTrace
{
    private sealed class SampleState { internal float Next,NextDecision,LastAt,NextAvatar,LastAvatarAt; internal Vector3 Position,AvatarPosition; internal float Yaw; internal bool Seen,AvatarSeen; internal string Task="none"; internal int WanderAttempts,WanderReadyRejected,WanderNativeRejected,WanderHeightRejected,WanderAccepted; }
    private static readonly Dictionary<int,SampleState> States=new Dictionary<int,SampleState>();
    private static float until;
    private static int selectedId=-1;
    private static int lines;
    private static float sampleInterval=0.5f;
    private const int MaxLines=1200;

    internal static bool Active=>until>0f&&Time.unscaledTime<until&&lines<MaxLines;
    internal static void Start(float seconds,int entityId,float interval=0.5f)
    {
        States.Clear();lines=0;selectedId=entityId;
        sampleInterval=Mathf.Clamp(interval,0.05f,0.5f);
        until=Time.unscaledTime+Mathf.Clamp(seconds,5f,90f);
        Log.Out("[RB NPC AI TRACE] start duration="+(int)Mathf.Clamp(seconds,5f,90f)+"s entity="+(entityId<0?"all":entityId.ToString())+" interval="+sampleInterval.ToString("F2"));
    }
    internal static void Stop()
    {
        if(until>0f)Log.Out("[RB NPC AI TRACE] stop lines="+lines);
        until=0f;States.Clear();
    }
    private static bool Accept(EntityRebirthNPC npc)
    {
        if(!Active||npc==null||!(npc is EntityRebirthHumanoidNPC)||
            npc.world==null||npc.world.IsRemote()||!ThreadManager.IsMainThread()||
            selectedId>=0&&npc.entityId!=selectedId)return false;
        if(selectedId<0)
        {
            var player=npc.world.GetClosestPlayer(npc.position,60f,false);
            if(player==null)return false;
        }
        return true;
    }
    private static SampleState State(EntityRebirthNPC npc)
    {
        if(States.TryGetValue(npc.entityId,out var state))return state;
        if(States.Count>=16)return null;
        state=new SampleState();States.Add(npc.entityId,state);return state;
    }
    internal static void Task(EntityRebirthNPC npc,string task,string action,Vector3? goal=null)
    {
        if(!Accept(npc))return;
        var state=State(npc);if(state==null)return;
        if(action=="start")state.Task=task;
        else if(action=="end"&&state.Task==task)state.Task="none";
        Write(npc,"task="+task+" action="+action+
            (action=="start"?" origin="+npc.position.ToString("F2"):"")+
            (goal.HasValue?" goal="+goal.Value.ToString("F2"):""));
    }
    internal static void Activation(EntityRebirthNPC npc,EntityPlayerLocal player,string command)
    {
        if(!Accept(npc))return;
        Write(npc,"activation command="+command+" player="+(player==null?-1:player.entityId));
    }
    internal static void RangedDecision(EntityRebirthNPC npc,string reason,float distance,float alignment)
    {
        if(!Accept(npc))return;
        var state=State(npc);if(state==null)return;
        // A shot can occur between throttled half-second decision samples.
        // Always record it so a combat trace proves the ranged action fired.
        if(reason!="shot"&&Time.unscaledTime<state.NextDecision)return;
        state.NextDecision=Time.unscaledTime+0.5f;
        Write(npc,"ranged decision="+reason+" distance="+distance.ToString("F2")+" alignment="+alignment.ToString("F3"));
    }
    internal static void MeleeAttack(EntityRebirthNPC npc,EntityAlive target,float distance,float range,float alignment)
    {
        if(!Accept(npc))return;
        Write(npc,"melee attack target="+(target==null?-1:target.entityId)+
            " targetDead="+(target==null||target.IsDead())+
            " distance="+distance.ToString("F2")+
            " range="+range.ToString("F2")+
            " facing="+alignment.ToString("F3"));
    }
    internal static void WanderDecision(EntityRebirthNPC npc,string reason)
    {
        if(!Accept(npc))return;
        var state=State(npc);if(state==null)return;
        state.WanderAttempts++;
        switch(reason)
        {
            case "ready":state.WanderReadyRejected++;break;
            case "native":state.WanderNativeRejected++;break;
            case "height":state.WanderHeightRejected++;break;
            case "accepted":state.WanderAccepted++;break;
        }
    }
    internal static void Sample(EntityRebirthNPC npc)
    {
        if(!Accept(npc))return;
        var state=State(npc);if(state==null||Time.unscaledTime<state.Next)return;
        float elapsed=state.Seen?Time.unscaledTime-state.LastAt:0f;
        state.LastAt=Time.unscaledTime;
        state.Next=Time.unscaledTime+sampleInterval;
        var position=npc.position;float yaw=npc.rotation.y;
        var delta=state.Seen?position-state.Position:Vector3.zero;
        float turn=state.Seen?Mathf.DeltaAngle(state.Yaw,yaw):0f;
        state.Position=position;state.Yaw=yaw;state.Seen=true;
        var target=npc.GetAttackTarget();
        var navigator=npc.getNavigator();
        var localPlayer=npc.world.GetPrimaryPlayer();
        float reach=localPlayer==null?-1f:Vector3.Distance(localPlayer.position,position);
        bool talkAllowed=localPlayer!=null&&npc.AllowActivationCommand("talk",localPlayer);
        var animator=npc.emodel?.avatarController?.GetAnimator();
        var animState=animator!=null&&animator.layerCount>0?animator.GetCurrentAnimatorStateInfo(0):default(AnimatorStateInfo);
        int wanderAttempts=state.WanderAttempts, wanderReady=state.WanderReadyRejected,
            wanderNative=state.WanderNativeRejected, wanderHeight=state.WanderHeightRejected,
            wanderAccepted=state.WanderAccepted;
        state.WanderAttempts=state.WanderReadyRejected=state.WanderNativeRejected=
            state.WanderHeightRejected=state.WanderAccepted=0;
        Write(npc,"sample task="+state.Task+" pos="+position.ToString("F2")+
            " dt="+elapsed.ToString("F3")+" delta="+delta.ToString("F2")+" yaw="+yaw.ToString("F1")+
            " turn="+turn.ToString("F1")+" order="+npc.RebirthRuntimeState?.Order+
            " target="+(target==null?-1:target.entityId)+
            " noPath="+(navigator==null?"null":navigator.noPathAndNotPlanningOne().ToString())+
            " wanderChecks="+wanderAttempts+" wanderReady="+wanderReady+
            " wanderNative="+wanderNative+" wanderHeight="+wanderHeight+
            " wanderAccepted="+wanderAccepted+
            " blocked="+(npc.moveHelper==null?0f:npc.moveHelper.BlockedTime).ToString("F2")+
            " playerDist="+reach.ToString("F2")+" talkAllowed="+talkAllowed+
            " rootMotion="+npc.RootMotion+" moveSpeed="+npc.GetMoveSpeed().ToString("F2")+
            " speedForward="+npc.speedForward.ToString("F2")+
            " speedStrafe="+npc.speedStrafe.ToString("F2")+
            " movementState="+npc.MovementState+
            " animForward="+(animator==null?"missing":animator.GetFloat(AvatarController.forwardHash).ToString("F2"))+
            " animMoving="+(animator==null?"missing":animator.GetBool(AvatarController.isMovingHash).ToString())+
            " animState="+(animator==null?"missing":animState.fullPathHash.ToString())+
            " animTime="+(animator==null?"missing":animState.normalizedTime.ToString("F2"))+
            " motion="+npc.motion.ToString("F2")+
            " moveDirection="+npc.moveDirection.ToString("F2")+
            " speedModifier="+npc.GetSpeedModifier().ToString("F2")+
            " moveTo="+(npc.moveHelper==null?"null":npc.moveHelper.moveToPos.ToString("F2"))+
            " moveNext="+(npc.moveHelper==null||!npc.moveHelper.hasNextPos?"none":npc.moveHelper.nextMoveToPos.ToString("F2"))+
            " moveActive="+(npc.moveHelper!=null&&npc.moveHelper.IsActive));
    }
    internal static void AvatarSample(EntityRebirthNPC npc,Animator animator)
    {
        if(!Accept(npc)||animator==null)return;
        var state=State(npc);if(state==null||Time.unscaledTime<state.NextAvatar)return;
        float now=Time.unscaledTime;
        float elapsed=state.AvatarSeen?now-state.LastAvatarAt:0f;
        var position=npc.position;
        var delta=state.AvatarSeen?position-state.AvatarPosition:Vector3.zero;
        state.AvatarSeen=true;state.AvatarPosition=position;state.LastAvatarAt=now;
        state.NextAvatar=now+sampleInterval;
        Write(npc,"avatar task="+state.Task+" dt="+elapsed.ToString("F3")+
            " delta="+delta.ToString("F2")+
            " speedForward="+npc.speedForward.ToString("F3")+
            " speedStrafe="+npc.speedStrafe.ToString("F3")+
            " animForward="+animator.GetFloat(AvatarController.forwardHash).ToString("F3")+
            " animMoving="+animator.GetBool(AvatarController.isMovingHash)+
            " moveActive="+(npc.moveHelper!=null&&npc.moveHelper.IsActive));
    }
    private static void Write(EntityRebirthNPC npc,string detail)
    {
        if(lines++>=MaxLines)return;
        Log.Out("[RB NPC AI TRACE] t="+Time.unscaledTime.ToString("F2")+" entity="+npc.entityId+" "+detail);
        if(lines==MaxLines){until=0f;Log.Out("[RB NPC AI TRACE] line cap reached");}
    }
}

#if DEBUG
public sealed class ConsoleCmdRebirthNpcAiMovementTrace:ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>false;
    public override string[] getCommands()=>new[]{"rbnpcaitrace"};
    public override string getDescription()=>"Capture a bounded human NPC AI movement trace.";
    public override string getHelp()=>"rbnpcaitrace on [seconds=45] [entityId|nearest] [sampleInterval=0.5] | off";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        if(args==null||args.Count==0){Log.Out(getHelp());return;}
        if(args[0].Equals("off",StringComparison.OrdinalIgnoreCase)){RebirthNpcAiMovementTrace.Stop();return;}
        if(!args[0].Equals("on",StringComparison.OrdinalIgnoreCase)){Log.Out(getHelp());return;}
        float seconds=45f,interval=0.5f;int id=-1;
        if(args.Count>1&&!float.TryParse(args[1],System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,out seconds)){Log.Out(getHelp());return;}
        if(args.Count>2)
        {
            if(args[2].Equals("nearest",StringComparison.OrdinalIgnoreCase))
            {
                var world=GameManager.Instance?.World;
                var player=world?.GetPrimaryPlayer();
                if(player==null){Log.Out("rbnpcaitrace: no primary player");return;}
                var candidates=new List<Entity>();
                world.GetEntitiesInBounds(typeof(EntityAlive),new Bounds(player.position,Vector3.one*40f),candidates);
                float best=float.MaxValue;
                foreach(var candidate in candidates)
                {
                    if(!(candidate is EntityRebirthHumanoidNPC npc)||npc.IsDead())continue;
                    float distance=(npc.position-player.position).sqrMagnitude;
                    if(distance<best){best=distance;id=npc.entityId;}
                }
                if(id<0){Log.Out("rbnpcaitrace: no nearby human NPC");return;}
            }
            else if(!int.TryParse(args[2],out id)){Log.Out(getHelp());return;}
        }
        if(args.Count>3&&!float.TryParse(args[3],System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,out interval)){Log.Out(getHelp());return;}
        RebirthNpcAiMovementTrace.Start(seconds,id,interval);
    }
}
#endif
