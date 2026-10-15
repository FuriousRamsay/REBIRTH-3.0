using System;

// Original native owner reward operation. Retained in-flight state is never
// replayed. Success requires acknowledgement in the final native player file.
internal sealed class RebirthCamperStationNativeRewardDelivery
{
    private readonly RebirthCamperStationTrainingPlan plan;
    private readonly EntityPlayer player;
    private readonly CraftCompleteData reward;
    private readonly string key;
    private bool invoked,uncertain;
    private bool confirmed;
    private float nextObservation;
    private readonly Guid operation;
    private byte[] remotePayload;
    private bool callbackReceived,remoteAcknowledged;
    private RebirthCamperStationNativeRewardDelivery(RebirthCamperStationTrainingPlan plan,EntityPlayer player,CraftCompleteData reward)
    {this.plan=plan;this.player=player;this.reward=reward;key=RebirthCamperStationNativeRewardReceipt.Key(plan.Binding.Admission);
        if(!(player is EntityPlayerLocal))operation=Guid.ParseExact(plan.Binding.Admission.JobId,"N");}
    internal static bool TryBegin(RebirthCamperStationTrainingPlan plan,out RebirthCamperStationNativeRewardDelivery delivery)
    {
        delivery=null;
        try
        {
            var player=plan?.Binding.Player;
            if(!ThreadManager.IsMainThread()||plan==null||!(player is EntityPlayer)||player.Buffs==null||
                RebirthCharacterCreationHoldService.IsHeld(player)||
                !plan.Completed.TryGetNativeReward(plan,out var reward))return false;
            if(player is EntityPlayerLocal)
            {if(!ReferenceEquals(player,GameManager.Instance?.World?.GetPrimaryPlayer()))return false;}
            else if(player.world==null||player.world.IsRemote()||!ReferenceEquals(player.world,GameManager.Instance?.World)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player)||!RebirthCamperRemoteOwnerApplication.IsRegistered)return false;
            delivery=new RebirthCamperStationNativeRewardDelivery(plan,player,reward);return true;
        }
        catch{return false;}
    }
    internal bool TryDeliver()
    {
        try
        {
            if(!ThreadManager.IsMainThread()||uncertain||!plan.IsCurrent()||
                player.Buffs==null||
                RebirthCharacterCreationHoldService.IsHeld(player)||
                !plan.Completed.TryGetNativeReward(plan,out _))return false;
            if(!(player is EntityPlayerLocal))return TryDeliverRemote();
            if(!ReferenceEquals(player,GameManager.Instance?.World?.GetPrimaryPlayer()))return false;
            float stamp=player.Buffs.GetCustomVar(key);
            if(stamp==0f&&!invoked)
            {
                // Mark before native effects. A throw/changed owner cannot
                // retry this invocation through either this or a new operation.
                invoked=true;
                player.Buffs.SetCustomVar(key,2f,true);
                if(player.Buffs.GetCustomVar(key)!=2f||!plan.IsCurrent()){uncertain=true;return false;}
                ((EntityPlayerLocal)player).GiveExp(reward);
                if(!plan.IsCurrent()){uncertain=true;return false;}
                player.Buffs.SetCustomVar(key,1f,true);
                stamp=player.Buffs.GetCustomVar(key);
            }
            if(stamp!=1f)return false;
            float now=UnityEngine.Time.realtimeSinceStartup;if(now<nextObservation)return false;nextObservation=now+1f;
            GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            return confirmed=plan.IsCurrent()&&RebirthCamperStationNativeRewardReceipt.HasSaved(plan.Binding.Identity,plan.Binding.Admission,true)&&
                player.Buffs.GetCustomVar(key)==1f&&plan.IsCurrent();
        }
        catch(Exception error)
        {
            uncertain=true;
            Log.Warning("[Camper] Native crafting reward retained without replay for job "+plan.Binding.Admission.JobId+": "+error.GetType().Name);
            return false;
        }
    }
    internal bool IsConfirmedFor(RebirthCamperStationTrainingPlan expected)
        =>ThreadManager.IsMainThread()&&!uncertain&&confirmed&&ReferenceEquals(plan,expected)&&
            (player is EntityPlayerLocal?ReferenceEquals(player,GameManager.Instance?.World?.GetPrimaryPlayer()):RemoteOwnerCurrent()&&remoteAcknowledged)&&
            player.Buffs?.GetCustomVar(key)==1f&&
            RebirthCamperStationNativeRewardReceipt.HasSaved(plan.Binding.Identity,plan.Binding.Admission,true);
    private bool RemoteCurrent()=>plan.IsCurrent()&&RemoteOwnerCurrent();
    // Native receipt retirement intentionally invalidates MatchesLiveOutput.
    // Its original confirmed delivery still needs owner scope, not a live receipt row.
    private bool RemoteOwnerCurrent()=>ThreadManager.IsMainThread()&&plan.Binding.IsOwnerCurrent()&&player.world!=null&&!player.world.IsRemote()&&
        ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
        !player.IsDead()&&!RebirthCharacterCreationHoldService.IsHeld(player);
    private bool TryDeliverRemote()
    {
        if(!RemoteCurrent())return false;
        if(!invoked)
        {
            if(!RebirthCamperStationRemoteRewardPayload.TryCreate(plan,reward,out var payload))return false;
            remotePayload=payload;invoked=true;
            if(!RebirthCamperRemoteOwnerApplication.SendOwnerApplication(player,operation,RebirthCamperRemoteOwnerEffectKind.Reward,
                remotePayload,acknowledged=>
                {
                    if(callbackReceived)return;callbackReceived=true;
                    remoteAcknowledged=acknowledged&&RemoteCurrent();
                    if(!remoteAcknowledged)uncertain=true;
                }))uncertain=true;
        }
        return confirmed=!uncertain&&remoteAcknowledged&&RemoteCurrent()&&player.Buffs.GetCustomVar(key)==1f&&
            RebirthCamperStationNativeRewardReceipt.HasSaved(plan.Binding.Identity,plan.Binding.Admission,true)&&RemoteCurrent();
    }
}
