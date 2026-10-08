using System;
using System.Collections.Generic;
using UnityEngine;

// Holds only an explicit authenticated reading request while native held-state replication catches up.
internal static class RebirthPendingRemoteStudy
{
    private sealed class Request
    {
        internal EntityPlayer Player;internal World World;internal string Creation;
        internal int Type;internal ushort Seed;internal float Until;
    }
    private static readonly Dictionary<int,Request> Pending=new Dictionary<int,Request>();
    private static readonly List<int> Ids=new List<int>();
    private static float nextPoll;
    internal static bool TryDefer(EntityPlayer player,int type,ushort seed,string creation)
    {
        // A different explicit request supersedes an old one even if the new request is refused.
        if(player!=null&&Pending.TryGetValue(player.entityId,out var previous)&&
            (!ReferenceEquals(previous.Player,player)||!ReferenceEquals(previous.World,player.world)||
             previous.Creation!=creation||previous.Type!=type||previous.Seed!=seed))Cancel(player);
        if(player==null||player is EntityPlayerLocal||player.world==null||player.world.IsRemote()||
            !RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSandboxOptionManager.Current.RequireTimedReading||
            player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player)||
            RebirthBackpackLibraryReservation.BlocksResourceUse(player)){Cancel(player);return false;}
        var held=player.inventory?.holdingItemItemValue;
        if(held!=null&&held.type==type&&held.Seed==seed){Pending.Remove(player.entityId);return false;}
        RebirthWorldCharacterRecord record;ItemStack stack;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||!record.IsComplete||
            !RebirthSurvivorRequestScope.Matches(creation,record.Origin?.CreationId)||
            !RebirthLiteratureService.TryFindMatchingInventoryStackPublic(player,type,seed,out stack)||
            stack?.itemValue?.ItemClass==null||stack.IsEmpty()){Cancel(player);return false;}
        RebirthLiteratureDefinition definition;
        if(!RebirthProgressionRuntimeConfig.TryGetLiterature(stack.itemValue.ItemClass.GetItemName(),out definition)||definition==null){Cancel(player);return false;}
        // A duplicate cannot prolong the same outstanding request indefinitely.
        if(Pending.TryGetValue(player.entityId,out var old)&&ReferenceEquals(old.Player,player)&&
            ReferenceEquals(old.World,player.world)&&old.Creation==creation&&old.Type==type&&old.Seed==seed)return true;
        Pending[player.entityId]=new Request{Player=player,World=player.world,Creation=creation,
            Type=type,Seed=seed,Until=Time.unscaledTime+3f};
        return true;
    }
    internal static void Cancel(EntityPlayer player){if(player!=null)Pending.Remove(player.entityId);}
    internal static void Clear(){Pending.Clear();Ids.Clear();nextPoll=0f;}
    internal static void Pump()
    {
        if(Pending.Count==0)return;
        var world=GameManager.Instance?.World;
        if(world==null||world.IsRemote()||!RebirthSandboxOptionManager.Current.RequireTimedReading){Clear();return;}
        float now=Time.unscaledTime;if(now<nextPoll)return;nextPoll=now+0.1f;
        Ids.Clear();Ids.AddRange(Pending.Keys);
        foreach(int id in Ids)
        {
            if(!Pending.TryGetValue(id,out var request))continue;
            var player=request.Player;
            RebirthWorldCharacterRecord record;
            if(!ReferenceEquals(world,request.World)||!ReferenceEquals(world.GetEntity(id),player)||
                player.IsDead()||!RebirthWorldCharacterService.TryGet(player,out record)||record==null||
                !record.IsComplete||!RebirthSurvivorRequestScope.Matches(request.Creation,record.Origin?.CreationId))
            {Pending.Remove(id);continue;}
            var held=player.inventory?.holdingItemItemValue;
            bool ready=held!=null&&held.type==request.Type&&held.Seed==request.Seed;
            if(!ready&&now<request.Until)continue;
            Pending.Remove(id); // One admission attempt; never retry a possibly completed learning action.
            string message=Localization.Get("xuiRebirthReadingHoldItem");
            bool success=now<request.Until&&ready&&RebirthLiteratureService.TryReadMatchingInventoryItem(player,request.Type,request.Seed,out message);
            try
            {
                var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
                if(connection!=null&&connection.IsServer)
                    connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionResult>().Setup(success,message),_attachedToEntityId:id);
            }
            catch { } // Feedback delivery cannot restart study.
        }
    }
}