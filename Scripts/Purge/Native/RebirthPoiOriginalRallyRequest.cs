using System;
using UnityEngine;

internal enum RebirthPoiRallyApplicationState { Captured, Applying, Applied, Refused, Unknown }
// This is a server-producer assertion token, not a native reset detector. The actual
// caller adapter may create it only after every original batch terminal witness.
internal sealed class RebirthPoiRallyCompletionReceipt
{
    public readonly Guid Request,World;
    public readonly string QuestUniqueId;
    public readonly int QuestCode,PlayerId;
    public readonly long GlobalRevision;
    public RebirthPoiRallyCompletionReceipt(Guid request,Guid world,string questUniqueId,int questCode,int playerId,long globalRevision)
    {if(request==Guid.Empty||world==Guid.Empty||string.IsNullOrWhiteSpace(questUniqueId)||questUniqueId.Length>512||playerId<0||globalRevision<1)throw new ArgumentException("Incomplete original rally completion receipt.");Request=request;World=world;QuestUniqueId=questUniqueId;QuestCode=questCode;PlayerId=playerId;GlobalRevision=globalRevision;}
}
// Captures the actual native effect boundary before HandleParty/map/tracking changes.
// No hook or request transport is installed merely by declaring this owner scope.
internal sealed class RebirthPoiOriginalRallyRequest
{
    public readonly Guid RequestId,SavedWorldId;
    public readonly int PlayerId,QuestCode;
    public readonly string QuestId,QuestUniqueId;
    public readonly Vector3 PoiPosition;
    private readonly ObjectiveRallyPoint objective;
    private readonly Quest quest;
    private readonly EntityPlayerLocal player;
    private readonly Func<bool> originalCurrent;
    public RebirthPoiRallyApplicationState State {get;private set;}
    public Exception NativeFailure {get;private set;}
    private RebirthPoiOriginalRallyRequest(Guid savedWorld,ObjectiveRallyPoint rally,Quest original,EntityPlayerLocal owner,Vector3 poi,Func<bool> current)
    {RequestId=Guid.NewGuid();SavedWorldId=savedWorld;objective=rally;quest=original;player=owner;PlayerId=owner.entityId;QuestCode=original.QuestCode;QuestId=original.ID;QuestUniqueId=original.QuestUniqueId;PoiPosition=poi;originalCurrent=current;State=RebirthPoiRallyApplicationState.Captured;}
    public bool IsOriginalCurrent {get{try{return State==RebirthPoiRallyApplicationState.Captured&&originalCurrent();}catch{return false;}} }
    public static bool TryCapture(ObjectiveRallyPoint objective,Vector3 poiPosition,out RebirthPoiOriginalRallyRequest request)
    {
        request=null;
        try
        {
            var game=GameManager.Instance;var world=game==null?null:game.World;var state=world==null?null:world.worldState;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var quest=objective==null?null:objective.OwnerQuest;var journal=quest==null?null:quest.OwnerJournal;var player=journal==null?null:journal.OwnerPlayer as EntityPlayerLocal;
            if(!ThreadManager.IsMainThread()||world==null||state==null||manager==null||quest==null||journal==null||player==null||
                !ReferenceEquals(world.GetPrimaryPlayer(),player)||!ReferenceEquals(world.GetEntity(player.entityId),player)||player.IsDead()||!player.IsSpawned()||
                quest.RallyMarkerActivated||objective.Complete||journal.ActiveQuest!=null||quest.CurrentState!=Quest.QuestState.InProgress||
                !quest.PositionData.ContainsKey(Quest.PositionDataTypes.POIPosition)||!quest.PositionData[Quest.PositionDataTypes.POIPosition].Equals(poiPosition)||
                string.IsNullOrWhiteSpace(quest.ID)||quest.ID.Length>256||string.IsNullOrWhiteSpace(quest.QuestUniqueId)||quest.QuestUniqueId.Length>512||
                !quest.CheckRequirements()||!quest.QuestClass.CanActivate()||ObjectiveRallyPoint.OutstandingRallyPoint!=null)return false;
            bool server=manager.IsServer;if(server==world.IsRemote())return false;
            string nativeWorld=server?state.Guid:GamePrefs.GetString(EnumGamePrefs.GameGuidClient);Guid savedWorld;if(!Guid.TryParse(nativeWorld,out savedWorld)||savedWorld==Guid.Empty)return false;
            var connection=server?null:manager.connectionToServer==null||manager.connectionToServer.Length==0?null:manager.connectionToServer[0];if(!server&&(connection==null||connection.IsDisconnected()))return false;
            int playerId=player.entityId,questCode=quest.QuestCode;string questId=quest.ID,unique=quest.QuestUniqueId;byte phase=quest.CurrentPhase,currentValue=objective.CurrentValue;bool tracked=quest.Tracked;var trackedQuest=journal.TrackedQuest;var quests=journal.quests;var questClass=quest.QuestClass;var positions=quest.PositionData;string activationEvent=objective.activateEvent;int difficulty=questClass.DifficultyTier;bool hadTrader=positions.ContainsKey(Quest.PositionDataTypes.TraderPosition);Vector3 trader=hadTrader?positions[Quest.PositionDataTypes.TraderPosition]:default(Vector3);
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,world)&&ReferenceEquals(world.worldState,state)&&
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&manager.IsServer==server&&world.IsRemote()!=server&&
                (server?state.Guid:GamePrefs.GetString(EnumGamePrefs.GameGuidClient))==nativeWorld&&
                (server||manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&ReferenceEquals(manager.connectionToServer[0],connection)&&!connection.IsDisconnected())&&
                ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(playerId),player)&&ReferenceEquals(player.world,world)&&player.IsSpawned()&&!player.IsDead()&&
                ReferenceEquals(objective.OwnerQuest,quest)&&ReferenceEquals(quest.OwnerJournal,journal)&&ReferenceEquals(journal.OwnerPlayer,player)&&ReferenceEquals(journal.quests,quests)&&quests.Contains(quest)&&
                ReferenceEquals(quest.QuestClass,questClass)&&questClass.DifficultyTier==difficulty&&objective.activateEvent==activationEvent&&ReferenceEquals(quest.PositionData,positions)&&positions.ContainsKey(Quest.PositionDataTypes.TraderPosition)==hadTrader&&(!hadTrader||positions[Quest.PositionDataTypes.TraderPosition].Equals(trader))&&quest.QuestCode==questCode&&quest.ID==questId&&quest.QuestUniqueId==unique&&quest.CurrentPhase==phase&&quest.CurrentState==Quest.QuestState.InProgress&&
                !quest.RallyMarkerActivated&&!objective.Complete&&objective.CurrentValue==currentValue&&journal.ActiveQuest==null&&quest.Tracked==tracked&&ReferenceEquals(journal.TrackedQuest,trackedQuest)&&
                quest.PositionData.ContainsKey(Quest.PositionDataTypes.POIPosition)&&quest.PositionData[Quest.PositionDataTypes.POIPosition].Equals(poiPosition)&&ObjectiveRallyPoint.OutstandingRallyPoint==null;
            if(!current())return false;request=new RebirthPoiOriginalRallyRequest(savedWorld,objective,quest,player,poiPosition,current);return true;
        }
        catch{return false;}
    }
    public bool MatchesOriginal(Guid request,Guid savedWorld,string questUniqueId,int questCode,int playerId)
    {return request==RequestId&&savedWorld==SavedWorldId&&questUniqueId==QuestUniqueId&&questCode==QuestCode&&playerId==PlayerId&&IsOriginalCurrent;}
    public bool RefuseOriginal(Guid request,Guid savedWorld)
    {if(request!=RequestId||savedWorld!=SavedWorldId||!IsOriginalCurrent)return false;State=RebirthPoiRallyApplicationState.Refused;return true;}
    public bool TryApplyConfirmed(RebirthPoiRallyCompletionReceipt receipt)
    {
        if(receipt==null||!MatchesOriginal(receipt.Request,receipt.World,receipt.QuestUniqueId,receipt.QuestCode,receipt.PlayerId))return false;
        // Once any native success side effect starts, retries must not duplicate party/events.
        State=RebirthPoiRallyApplicationState.Applying;
        try
        {
            objective.HandleParty();quest.RemoveMapObject();quest.RallyMarkerActivated=true;
            quest.OwnerJournal.ActiveQuest=quest;quest.Tracked=true;quest.OwnerJournal.TrackedQuest=quest;quest.OwnerJournal.RefreshTracked();
            if(quest.PositionData.ContainsKey(Quest.PositionDataTypes.TraderPosition))quest.OwnerJournal.AddPOIToTraderData(quest.QuestClass.DifficultyTier,quest.PositionData[Quest.PositionDataTypes.TraderPosition],quest.PositionData[Quest.PositionDataTypes.POIPosition]);
            objective.RallyPointActivated();
            if(objective.activateEvent!="")GameEventManager.Current.HandleAction(objective.activateEvent,null,player,twitchActivated:false,new Vector3i(PoiPosition));
            State=RebirthPoiRallyApplicationState.Applied;return true;
        }
        catch(Exception error){NativeFailure=error;State=RebirthPoiRallyApplicationState.Unknown;return false;}
    }
}
