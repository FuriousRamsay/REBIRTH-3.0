using System;
using System.Linq;
using UnityEngine;

// Sender authentication and original native data ownership. This does not invoke
// local-only requirement/UI methods or replace full native rally eligibility.
internal sealed class RebirthPoiAuthenticatedRallyScope
{
    private readonly RebirthPoiWorldBinding binding;
    private readonly Func<bool> current;
    public readonly Guid Request;
    public readonly string QuestId,QuestUniqueId;
    public readonly int PlayerId,QuestCode;
    public readonly byte Phase;
    public readonly Vector3 Poi;
    public readonly Quest OriginalQuest;
    private RebirthPoiRallyCompletionReceipt completion;
    private RebirthPoiAuthenticatedRallyScope(RebirthPoiWorldBinding originalBinding,Guid request,Quest quest,int playerId,Vector3 poi,Func<bool> stillCurrent)
    {binding=originalBinding;Request=request;OriginalQuest=quest;PlayerId=playerId;QuestId=quest.ID;QuestUniqueId=quest.QuestUniqueId;QuestCode=quest.QuestCode;Phase=quest.CurrentPhase;Poi=poi;current=stillCurrent;}
    internal Guid SavedWorldId => binding.WorldId;
    public bool IsOriginalCurrent {get{try{return binding.IsCurrent&&current();}catch{return false;}} }
    internal static bool ValidIdentifier(string value,int maximum)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>maximum)return false;
        for(int i=0;i<value.Length;i++){char c=value[i];if(char.IsControl(c))return false;if(char.IsHighSurrogate(c)){if(++i>=value.Length||!char.IsLowSurrogate(value[i]))return false;}else if(char.IsLowSurrogate(c))return false;}return true;
    }
    public static bool TryCapture(World world,ClientInfo sender,Guid request,Guid savedWorld,int playerId,string questId,string unique,int questCode,byte phase,Vector3 poi,out RebirthPoiAuthenticatedRallyScope scope)
    {
        scope=null;
        try
        {
            if(request==Guid.Empty||savedWorld==Guid.Empty||sender==null||playerId<0||!ValidIdentifier(questId,256)||!ValidIdentifier(unique,512)||
                float.IsNaN(poi.x)||float.IsNaN(poi.y)||float.IsNaN(poi.z)||float.IsInfinity(poi.x)||float.IsInfinity(poi.y)||float.IsInfinity(poi.z))return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var game=GameManager.Instance;
            RebirthPoiWorldBinding binding;if(!RebirthPoiWorldBinding.TryCapture(world,out binding)||binding.WorldId!=savedWorld||manager.Clients==null||
                !ReferenceEquals(manager.Clients.ForEntityId(playerId),sender)||sender.entityId!=playerId)return false;
            var player=world.GetEntity(playerId) as EntityPlayer;var data=sender.latestPlayerData;var journal=player==null?null:player.QuestJournal;
            if(player==null||!ReferenceEquals(player.world,world)||player.IsDead()||!player.IsSpawned()||data==null||data.id!=playerId||journal==null||!ReferenceEquals(data.questJournal,journal)||journal.quests==null)return false;
            var quests=journal.quests;var matches=quests.Where(q=>q!=null&&q.ID==questId&&q.QuestUniqueId==unique&&q.QuestCode==questCode).ToArray();
            if(matches.Length!=1)return false;var quest=matches[0];var questClass=quest.QuestClass;var positions=quest.PositionData;var objectives=quest.Objectives;
            if(questClass==null||!ReferenceEquals(quest.OwnerJournal,journal)||quest.CurrentState!=Quest.QuestState.InProgress||quest.CurrentPhase!=phase||quest.SharedOwnerID!=-1||quest.RallyMarkerActivated||
                journal.ActiveQuest!=null||positions==null||!positions.ContainsKey(Quest.PositionDataTypes.POIPosition)||!positions[Quest.PositionDataTypes.POIPosition].Equals(poi)||objectives==null)return false;
            var rallies=objectives.OfType<ObjectiveRallyPoint>().Where(o=>o.Phase==0||o.Phase==phase).ToArray();
            if(rallies.Length!=1||rallies[0].Complete||!ReferenceEquals(rallies[0].OwnerQuest,quest))return false;var rally=rallies[0];byte value=rally.CurrentValue;
            Func<bool> current=()=>ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,world)&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&manager.IsServer&&
                manager.Clients!=null&&ReferenceEquals(manager.Clients.ForEntityId(playerId),sender)&&sender.entityId==playerId&&ReferenceEquals(sender.latestPlayerData,data)&&data.id==playerId&&ReferenceEquals(data.questJournal,journal)&&
                ReferenceEquals(world.GetEntity(playerId),player)&&ReferenceEquals(player.world,world)&&player.IsSpawned()&&!player.IsDead()&&ReferenceEquals(player.QuestJournal,journal)&&
                ReferenceEquals(journal.quests,quests)&&quests.Contains(quest)&&quests.Count(q=>q!=null&&q.ID==questId&&q.QuestUniqueId==unique&&q.QuestCode==questCode)==1&&
                ReferenceEquals(quest.OwnerJournal,journal)&&ReferenceEquals(quest.QuestClass,questClass)&&quest.ID==questId&&quest.QuestUniqueId==unique&&quest.QuestCode==questCode&&quest.CurrentPhase==phase&&quest.SharedOwnerID==-1&&quest.CurrentState==Quest.QuestState.InProgress&&!quest.RallyMarkerActivated&&journal.ActiveQuest==null&&
                ReferenceEquals(quest.PositionData,positions)&&positions.ContainsKey(Quest.PositionDataTypes.POIPosition)&&positions[Quest.PositionDataTypes.POIPosition].Equals(poi)&&ReferenceEquals(quest.Objectives,objectives)&&objectives.Contains(rally)&&ReferenceEquals(rally.OwnerQuest,quest)&&objectives.OfType<ObjectiveRallyPoint>().Count(o=>o.Phase==0||o.Phase==phase)==1&&(rally.Phase==0||rally.Phase==phase)&&!rally.Complete&&rally.CurrentValue==value;
            scope=new RebirthPoiAuthenticatedRallyScope(binding,request,quest,playerId,poi,current);if(!scope.IsOriginalCurrent){scope=null;return false;}return true;
        }
        catch{scope=null;return false;}
    }
    public bool TryCompleteOriginal(RebirthPoiResetBatchCallerProtocol batch,out RebirthPoiRallyCompletionReceipt receipt)
    {
        receipt=null;RebirthPoiResetBatchCompletion outcome;
        if(batch==null||!IsOriginalCurrent||!batch.TryGetCompleted(out outcome)||!ReferenceEquals(outcome.Scope,binding.Scope)||outcome.World!=binding.WorldId||outcome.Transaction!=Request||!outcome.IsQuestBatch||!outcome.Identities.Any(i=>Poi.x>=i.X&&Poi.x<(long)i.X+i.SizeX&&Poi.z>=i.Z&&Poi.z<(long)i.Z+i.SizeZ))return false;
        if(completion==null)completion=new RebirthPoiRallyCompletionReceipt(Request,binding.WorldId,QuestUniqueId,QuestCode,PlayerId,outcome.GlobalRevision);
        receipt=completion;return true;
    }
}
