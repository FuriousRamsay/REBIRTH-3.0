using System;using System.Linq;using UnityEngine;
internal static class RebirthPoiLocalRallyOwner {    internal static int[] EligibleParty(Quest quest)
    {
        if(quest.sharedWithList==null)return null;
        Rect area=quest.GetLocationRect();var owner=quest.OwnerJournal.OwnerPlayer;
        return quest.sharedWithList.Where(player=>{
            if(player==null)return false;
            if(area==Rect.zero)return Vector3.Distance(owner.position,player.position)<15f;
            Vector3 point=player.position;point.y=point.z;return area.Contains(point);
        }).Select(player=>player.entityId).ToArray();
    }}
internal static class RebirthPoiLocalRallyEntryHook {    private static bool Prefix(ObjectiveRallyPoint __instance,Vector3i blockPos)
    {
        var world=GameManager.Instance?.World;var quest=__instance?.OwnerQuest;
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || world==null || quest?.OwnerJournal?.OwnerPlayer==null || !ReferenceEquals(quest.OwnerJournal.OwnerPlayer,world.GetPrimaryPlayer()) || !(quest.OwnerJournal.OwnerPlayer is EntityPlayerLocal) || !quest.PositionData.ContainsKey(Quest.PositionDataTypes.POIPosition))return true;
        if(quest.SharedOwnerID!=-1 || __instance.Complete || quest.OwnerJournal.ActiveQuest!=null || __instance.RallyPos!=blockPos)return false;
        if(Twitch.TwitchManager.HasInstance && Twitch.TwitchManager.Current.IsVoting)
        { GameManager.ShowTooltip(world.GetPrimaryPlayer(),Localization.Get("ttWaitForVoteQuest"));return false; }
        int hour=GameUtils.WorldTimeToHours(world.worldTime),start=__instance.startTime,end=__instance.endTime;
        if(start!=-1 && end!=-1 && (start<end?(hour<start || hour>=end):(hour<start && hour>=end)))
        { GameManager.ShowTooltip(world.GetPrimaryPlayer(),string.Format(Localization.Get("ObjectiveRallyPointInvalidStartTime"),start,end));return false; }
        Vector3 poi=quest.PositionData[Quest.PositionDataTypes.POIPosition];
        if(world.IsRemote()) { if(!RebirthPoiRemoteRallyOwner.TryStart(__instance,poi))GameManager.ShowTooltip(world.GetPrimaryPlayer(),Localization.Get("xuiRebirthPoiResetUnavailable"));return false; }
        ulong extra;
        var reason=QuestEventManager.Current.CheckForPOILockouts(quest.OwnerJournal.OwnerPlayer.entityId,new Vector2(poi.x,poi.z),out extra);
        __instance.RallyPointActivate(poi,reason==QuestEventManager.POILockoutReasonTypes.None,reason,extra);
        return false;
    } internal static bool Invoke(ObjectiveRallyPoint objective,Vector3i position)=>Prefix(objective,position); }