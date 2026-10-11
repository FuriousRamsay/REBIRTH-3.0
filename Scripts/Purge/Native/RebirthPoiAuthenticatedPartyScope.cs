using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Remote owner's sharedWithList is not serialized and native server forwarding
// does not populate it. Resolve accepted guests from actual server party/journals.
internal sealed class RebirthPoiAuthenticatedPartyScope
{
    private readonly Func<bool> current;
    private readonly Func<int[]> eligible;
    internal readonly int[] Accepted;
    private RebirthPoiAuthenticatedPartyScope(int[] accepted,Func<bool> stillCurrent,Func<int[]> inRange)
    { Accepted=accepted;current=stillCurrent;eligible=inRange; }
    internal bool IsCurrent {get{try{return current();}catch{return false;}}}
    internal int[] Eligible {get{try{return IsCurrent?eligible():null;}catch{return null;}}}
    private static QuestJournal Journal(World world,EntityPlayer member)
    {
        if(member is EntityPlayerLocal)return member.QuestJournal;
        var client=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(member.entityId);
        var data=client?.latestPlayerData;
        return client!=null && client.entityId==member.entityId && data!=null && data.id==member.entityId && ReferenceEquals(data.questJournal,member.QuestJournal)?data.questJournal:null;
    }
    private static Quest[] AcceptedQuests(QuestJournal journal,Quest owner,int ownerId)
    {
        if(journal?.quests==null)return null;
        return journal.quests.Where(q=>q!=null && q.SharedOwnerID==ownerId && q.ID==owner.ID && q.QuestUniqueId==owner.QuestUniqueId && q.QuestCode==owner.QuestCode && q.CurrentState==Quest.QuestState.InProgress && !q.RallyMarkerActivated && q.PositionData.ContainsKey(Quest.PositionDataTypes.POIPosition) && q.PositionData[Quest.PositionDataTypes.POIPosition].Equals(owner.PositionData[Quest.PositionDataTypes.POIPosition])).ToArray();
    }
    internal static bool TryCapture(World world,Quest quest,int ownerId,out RebirthPoiAuthenticatedPartyScope scope)
    {
        scope=null;
        try
        {
            var owner=world.GetEntity(ownerId) as EntityPlayer;
            if(owner==null || !ReferenceEquals(owner.world,world))return false;
            var party=owner.Party;
            if(party==null)
            { scope=new RebirthPoiAuthenticatedPartyScope(null,()=>ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetEntity(ownerId),owner)&&owner.Party==null,()=>null);return true; }
            var list=party.MemberList;if(list==null || list.Count>64)return false;
            var members=list.ToArray();var guests=new Dictionary<EntityPlayer,Tuple<QuestJournal,Quest>>();
            foreach(var member in members)
            {
                if(member==null || !ReferenceEquals(world.GetEntity(member.entityId),member) || !ReferenceEquals(member.Party,party))return false;
                if(ReferenceEquals(member,owner))continue;
                var journal=Journal(world,member);var matches=AcceptedQuests(journal,quest,ownerId);
                if(matches==null || matches.Length>1)return false;
                if(matches.Length==1)guests.Add(member,Tuple.Create(journal,matches[0]));
            }
            Func<bool> current=()=>ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetEntity(ownerId),owner)&&ReferenceEquals(owner.Party,party)&&ReferenceEquals(party.MemberList,list)&&list.Count==members.Length&&list.SequenceEqual(members)&&members.All(member=>ReferenceEquals(world.GetEntity(member.entityId),member)&&ReferenceEquals(member.Party,party))&&members.Where(member=>!ReferenceEquals(member,owner)).All(member=>{
                var journal=Journal(world,member);var matches=AcceptedQuests(journal,quest,ownerId);Tuple<QuestJournal,Quest> original;
                if(matches==null)return false;
                if(!guests.TryGetValue(member,out original))return matches.Length==0;
                return ReferenceEquals(journal,original.Item1)&&matches.Length==1&&ReferenceEquals(matches[0],original.Item2)&&!member.IsDead()&&member.IsSpawned();
            });
            Func<int[]> eligible=()=>{
                Rect area=quest.GetLocationRect();
                return guests.Keys.Where(member=>{if(area==Rect.zero)return Vector3.Distance(owner.position,member.position)<15f;Vector3 point=member.position;point.y=point.z;return area.Contains(point);}).Select(member=>member.entityId).ToArray();
            };
            scope=new RebirthPoiAuthenticatedPartyScope(guests.Keys.Select(member=>member.entityId).ToArray(),current,eligible);
            if(!scope.IsCurrent){scope=null;return false;}return true;
        }
        catch{scope=null;return false;}
    }
}