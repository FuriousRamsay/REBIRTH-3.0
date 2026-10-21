using System;
using System.Collections.Generic;

public static partial class RebirthNpcDialogueService
{
    private sealed class DialogueNativeCustody
    {
        internal World World,Incoming;internal Dictionary<Guid,RebirthNpcConversationState> Records;
        internal readonly HashSet<Guid> Resolved=new HashSet<Guid>();internal readonly RebirthNpcNativeActorReentryMap Actors=new RebirthNpcNativeActorReentryMap();
        internal RebirthNpcNativeTransientAdoption Receipt;
        internal bool Pending {get{foreach(var pair in Records)if(!Resolved.Contains(pair.Key)&&pair.Value.ExpiresUtcTicks>DateTime.UtcNow.Ticks)return true;return false;}}
        internal bool Holds(RebirthNpcStableId id){foreach(var pair in Records)if(!Resolved.Contains(pair.Key)&&pair.Value.NpcId==id&&pair.Value.ExpiresUtcTicks>DateTime.UtcNow.Ticks)return true;return false;}
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>StageScope(source.World);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>source?.Current()==true&&Records.Count==0&&StageScope(source.World);
        private bool StageScope(World incoming)
        {
            lock(Sync)
            {
                if(Incoming!=null)return ReferenceEquals(Incoming,incoming)&&ReferenceEquals(dialogueWorld,Incoming);
                if(!CanRetireEmptyDialogue||dialogueWorld!=null)return false;dialogueWorld=Incoming=incoming;dialogueNativeCustodies.Add(this);return true;
            }
        }
        internal bool TryResume(RebirthNpcInteractionContext context,out RebirthNpcConversationState resumed)
        {
            resumed=null;if(!ReferenceEquals(Incoming,GameManager.Instance?.World))return false;
            RebirthNpcConversationState selected=null;
            foreach(var pair in Records)
            {
                var state=pair.Value;
                if(Resolved.Contains(pair.Key)||state.ExpiresUtcTicks<=DateTime.UtcNow.Ticks||state.NpcId!=context.NpcId||
                    !string.Equals(state.ActorId,context.ActorId,StringComparison.Ordinal))continue;
                if(selected!=null)return false;selected=state;
            }
            if(selected==null||!RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out int current)||
                Incoming.GetEntity(current) is not EntityRebirthNPC npc||npc.RebirthProfileId!=selected.ProfileId||
                !Actors.Contains(context.NpcId)||!CurrentDialogueActor(context.ActorId)||
                RebirthNpcInteractionSessionService.Authorize(context.SessionKey,context.ActorId,RebirthNpcInteractionAction.Dialogue,out var authorized)!=RebirthNpcInteractionResult.Allowed||
                authorized.NpcId!=context.NpcId||authorized.SessionKey!=context.SessionKey)return false;
            // Verify the actual NPC is the independently matched native source,
            // rather than trusting the stable-id registry lookup alone.
            bool matched=false;
            foreach(var source in originalEntities)if(source.Value==context.NpcId&&Actors.TryResolve(source.Key,out var actor)&&ReferenceEquals(actor,npc)){matched=true;break;}
            if(!matched)return false;
            var copy=new RebirthNpcConversationState{ConversationId=selected.ConversationId,SessionKey=authorized.SessionKey,ActorId=selected.ActorId,
                NpcId=selected.NpcId,ProfileId=selected.ProfileId,Revision=selected.Revision,ExpiresUtcTicks=selected.ExpiresUtcTicks,
                RecoveryGeneration=RebirthNpcRecoveryGeneration.RequireCurrent(),Turns=new List<RebirthNpcDialogueTurn>()};
            foreach(var turn in selected.Turns)copy.Turns.Add(new RebirthNpcDialogueTurn{Index=turn.Index,TopicId=turn.TopicId,Text=turn.Text,CreatedUtcTicks=turn.CreatedUtcTicks});
            lock(Sync)
            {
                if(Conversations.Count>=MaxConversations||Conversations.ContainsKey(copy.ConversationId))return false;
                Conversations.Add(copy.ConversationId,copy);ConversationOrder.Add(copy.ConversationId);Resolved.Add(copy.ConversationId);resumed=copy;return true;
            }
        }
        internal readonly Dictionary<int,RebirthNpcStableId> originalEntities=new Dictionary<int,RebirthNpcStableId>();
    }
    private static readonly List<DialogueNativeCustody> dialogueNativeCustodies=new List<DialogueNativeCustody>();
    private static bool CurrentDialogueActor(string actorId)
    {
        var world=GameManager.Instance?.World;var local=world?.GetPrimaryPlayer();
        if(local!=null&&!local.IsDead()&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==actorId)return true;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;if(manager?.Clients==null)return false;
        foreach(var peer in manager.Clients.List)if(peer!=null&&peer.loginDone&&!peer.disconnecting&&peer.bAttachedToEntity&&
            ReferenceEquals(manager.Clients.ForEntityId(peer.entityId),peer)&&world.GetEntity(peer.entityId) is EntityPlayer player&&!player.IsDead()&&
            RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)&&identity.CanonicalId==actorId)return true;
        return false;
    }
    private static bool TryResumeNativeConversation(RebirthNpcInteractionContext context,out RebirthNpcConversationState state)
    {foreach(var custody in dialogueNativeCustodies)if(custody.TryResume(context,out state))return true;state=null;return false;}
    private static bool HasNativeConversationCustody(RebirthNpcInteractionContext context)
    {
        foreach(var custody in dialogueNativeCustodies)if(ReferenceEquals(custody.Incoming,GameManager.Instance?.World))
            foreach(var pair in custody.Records)if(!custody.Resolved.Contains(pair.Key)&&pair.Value.ExpiresUtcTicks>DateTime.UtcNow.Ticks&&
                pair.Value.NpcId==context.NpcId&&pair.Value.ActorId==context.ActorId)return true;
        return false;
    }
    internal static RebirthNpcNativeTransientAdoption CaptureNativeDialogueAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||dialogueWorld!=null&&!ReferenceEquals(dialogueWorld,scope.World))return null;
            var custody=new DialogueNativeCustody{World=scope.World,Records=Conversations};
            foreach(var state in Conversations.Values)
            {
                if(!RebirthNpcRuntimeRegistry.TryGetEntityId(state.NpcId,out int entity))return null;
                custody.Actors.Add(scope.World,entity);custody.originalEntities[entity]=state.NpcId;
            }
            var records=Conversations;var order=ConversationOrder;string image=DialogueImage().OuterXml;
            bool Original(){lock(Sync)return ReferenceEquals(Conversations,records)&&ReferenceEquals(ConversationOrder,order)&&custody.Actors.Current()&&DialogueImage().OuterXml==image;}
            bool Available(){lock(Sync)return custody.Incoming==null?CanRetireEmptyDialogue&&dialogueWorld==null:ReferenceEquals(dialogueWorld,custody.Incoming);}
            var receipt=new RebirthNpcNativeTransientAdoption("dialogue-conversations",scope,Original,Available,()=>custody.Pending,custody.Actors.Observe,custody.Stage,image.Length,()=>dialogueNativeCustodies.Remove(custody),custody.Holds,custody.StageOwnerScope);
            custody.Receipt=receipt;return receipt;
        }
    }
}
