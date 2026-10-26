using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// B07 standing maintenance. Opt-in per NPC (Work Board saves percent + enabled + Farm target through
// RebirthP8NpcWaterPolicy.TryConfigure). Driven by RebirthNpcFarmOriginal.Tick (5 s cadence, 4 NPCs per
// pass), so there is no per-frame work. At most MaxProviders registry entries are examined per NPC pass,
// and at most one refill assignment is created per pass. The assignment goes through the normal
// RebirthNpcWorkAssignmentService.Create path (and so through RebirthNpcQualifiedWorkGate); this class
// never touches RebirthNpcWorkReleaseGate and never mutates world or inventory.
internal static class RebirthP8NpcWaterMaintainer
{
    private const int MaxProviders=8;
    private const float RecreateCooldown=60f,BlockedCooldown=180f,SettlingCooldown=10f;
    private sealed class State{internal float NextCheck;internal string Status="";}
    private static readonly Dictionary<RebirthNpcStableId,State> States=new Dictionary<RebirthNpcStableId,State>();
    private static readonly Dictionary<long,float> ProviderCooldown=new Dictionary<long,float>();
    private static World stateWorld;

    // Latest truthful maintenance status for the Work Board (not persisted; recomputed every pass).
    internal static string Status(RebirthNpcStableId npc)=>States.TryGetValue(npc,out var s)?s.Status:string.Empty;

    private static long Key(Vector3i p)=>((long)(p.x&0x1FFFFF)<<42)|((long)(p.z&0x1FFFFF)<<21)|(long)(p.y&0x1FFFFF);

    internal static void Tick(World world,EntityRebirthHumanoidNPC npc,RebirthNpcStableId id)
    {
        try
        {
            if(!ReferenceEquals(world,stateWorld)){stateWorld=world;States.Clear();ProviderCooldown.Clear();}
            if(world==null||world.IsRemote()||!ThreadManager.IsMainThread()||!AdvancedFarmingRuntimePolicy.Enabled||
               npc==null||npc.IsDead()||!RebirthP8NpcWaterPolicy.GetEnabled(id,out string farm))
            {if(States.Count>0)States.Remove(id);return;}
            if(!States.TryGetValue(id,out var state)){state=new State();States[id]=state;}
            if(Time.unscaledTime<state.NextCheck)return;
            state.NextCheck=Time.unscaledTime+10f;
            if(npc.IsPreparedRestorationPending||RebirthP6WorkFollowState.IsFollowing(npc)||npc.GetAttackTarget()!=null)
            {state.Status="Waiting: worker is following, fighting or restoring.";return;}
            var runtime=npc.RebirthRuntimeState;
            if(runtime==null||runtime.OwnershipKind!=RebirthNpcOwnershipKind.Player||string.IsNullOrEmpty(runtime.OwnerId))return;
            foreach(var current in RebirthNpcWorkAssignmentService.GetForNpc(id,false))
            {
                // A released terminal refill (filled bucket kept after failure, or settled) never blocks the next one.
                if(RebirthP8NpcWaterRefillWork.IsDefinition(current.DefinitionId)&&
                   RebirthP8NpcWaterRefillWork.IsReleasedTerminal(id,current.AssignmentId))continue;
                // Idle = nothing reserved/executing; an unfinished refill for any provider is never duplicated.
                if(current.Status==RebirthNpcWorkAssignmentStatus.Reserved||current.Status==RebirthNpcWorkAssignmentStatus.Executing)
                {state.Status="Waiting: worker is busy.";return;}
                if(RebirthP8NpcWaterRefillWork.IsDefinition(current.DefinitionId))
                {state.Status="Refill assignment is active or retrying.";return;}
            }
            if(RebirthP8NpcWaterRefillWork.HasUnfinishedOriginal(id)){state.Status="Waiting: a saved refill original is settling.";return;}
            var probe=new RebirthNpcWorkAssignment{DefinitionId=RebirthP8NpcWaterRefillWork.DefinitionId,TargetKey=farm,
                HasTargetPosition=true,ActorId=runtime.OwnerId,NpcId=id};
            if(!RebirthP8NpcWaterRefillWork.Shape(probe.DefinitionId,farm,true,Vector3.zero))
            {state.Status="Blocked: Farm target invalid.";return;}
            if(!RebirthNpcDeskVolumeWorkTargetAdapter.TryResolveFarmVolume(probe,out var bounds,out _))
            {state.Status="Blocked: saved Farm target is unavailable.";return;}
            var center=new Vector3i(Mathf.FloorToInt(bounds.center.x),Mathf.FloorToInt(bounds.center.y),Mathf.FloorToInt(bounds.center.z));
            int radius=Mathf.CeilToInt(Mathf.Max(bounds.extents.x,bounds.extents.z))+1,height=Mathf.CeilToInt(bounds.extents.y)+1;
            var entries=AdvancedFarmingWaterProviderRegistry.CollectProvidersInArea(world,center,radius,center.y-height,center.y+height,true,false,MaxProviders);
            int percent=RebirthP8NpcWaterPolicy.GetPercent(id);
            string blocker=null;bool anyNeeds=false;
            foreach(var entry in entries)
            {
                if(entry?.Tank==null)continue;
                probe.TargetPosition=entry.Pos.ToVector3();
                if(!RebirthNpcDeskVolumeWorkTargetAdapter.ContainsFarmPoint(probe,entry.Pos.ToVector3()+new Vector3(.5f,.5f,.5f),out _)||
                   !RebirthP8NpcWaterRefillWork.NeedsRefill(probe))continue;
                anyNeeds=true;
                long key=Key(entry.Pos);
                if(ProviderCooldown.TryGetValue(key,out float until)&&Time.unscaledTime<until)continue;
                // Another NPC's live refill (saved row) already serves this provider: wait, no cooldown needed.
                if(RebirthP8NpcWaterRefillWork.ClaimedByOther(entry.Pos,Guid.Empty)){blocker="Another refill is already serving this provider.";continue;}
                if(!RebirthP8NpcWaterRefillWork.CanStart(probe,npc,out blocker))
                {ProviderCooldown[key]=Time.unscaledTime+BlockedCooldown;continue;}
                if(RebirthNpcInteractionSessionService.Open(runtime.OwnerId,id,TimeSpan.FromSeconds(30),out var session)!=RebirthNpcInteractionResult.Allowed||session==null)
                {state.Status="Interaction authorization is unavailable.";return;}
                try
                {
                    var result=RebirthP8NpcWaterRefillWork.Assign(session.SessionKey,farm,entry.Pos,out _,out string detail);
                    // An equivalent assignment still settling (Conflict) is retried soon; other rejections back off.
                    ProviderCooldown[key]=Time.unscaledTime+(result==RebirthNpcWorkAssignmentResult.Succeeded?RecreateCooldown:
                        result==RebirthNpcWorkAssignmentResult.Conflict?SettlingCooldown:BlockedCooldown);
                    state.Status=result==RebirthNpcWorkAssignmentResult.Succeeded?
                        "Refill assigned for provider at "+entry.Pos+".":"Refill not assigned: "+detail;
                }
                finally{RebirthNpcInteractionSessionService.Close(session.SessionKey);}
                return;
            }
            state.Status=!anyNeeds?"All providers in the Farm are at or above the minimum.":
                "Blocked: "+(blocker??"providers are cooling down after a blocked attempt.");
        }
        catch{}
    }
}
