using System;
using System.Collections.Generic;

// UI projection contains slot identities/titles only; exact items remain server custody.
public static class RebirthAudiobookLibraryClient
{
    public static string CreationId {get;private set;}=string.Empty;
    public static long Revision {get;private set;}=-1;
    public static readonly List<string> SlotIds=new List<string>();
    public static readonly List<string> Items=new List<string>();
    private static World useWorld;
    private static int useOwner;
    private static string useCreation=string.Empty;
    private static ItemValue pendingUse;
    public static bool RequestUse(EntityPlayerLocal player,ItemValue item)
    {
        if(player?.world==null||item==null||item.IsEmpty()||player.IsDead()||RebirthBackpackLibraryReservation.IsHeld(player))return false;
        bool current=RebirthMusicLibraryClient.EnsureCurrent(player);
        if(!player.world.IsRemote())
        {if(!current||Revision<0)RefreshLocal(player);return RequestTransfer(player,true,0,item);}
        if(current&&Revision>=0)return RequestTransfer(player,true,0,item);
        string creation=RebirthSurvivorClientState.GetProjectedCreationId(player);
        string canonical;
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out canonical)||pendingUse!=null)return false;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        if(!RebirthMusicLibraryClient.CanSend(connection,request))return false;
        useWorld=player.world;useOwner=player.entityId;useCreation=canonical;pendingUse=item.Clone();
        RebirthMusicLibraryClient.Dispatch(player,0);
        return true;
    }
    private static void ClearUse(){useWorld=null;useOwner=0;useCreation=string.Empty;pendingUse=null;}
    private static void ApplyPendingUse(EntityPlayerLocal player)
    {
        if(pendingUse==null)return;
        if(player==null||player.IsDead()||!ReferenceEquals(player.world,useWorld)||player.entityId!=useOwner||
            !RebirthSurvivorRequestScope.Matches(useCreation,CreationId)){ClearUse();return;}
        if(RebirthMusicLibraryClient.PendingTransfer||RebirthBackpackLibraryReservation.IsHeld(player))return;
        // Clear before dispatch: a delayed snapshot must not send the same Use again.
        var item=pendingUse;ClearUse();
        if(!RequestTransfer(player,true,0,item))
        {useWorld=player.world;useOwner=player.entityId;useCreation=CreationId;pendingUse=item;}
    }
    public static bool RequestPlayback(EntityPlayerLocal player,bool resume)
    {
        if(player?.world==null||!RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||RebirthMusicLibraryClient.PendingTransfer)return false;
        if(!player.world.IsRemote())
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||
                record.Support.AudiobookRevision!=Revision||!RebirthSurvivorRequestScope.Matches(CreationId,record.Origin?.CreationId))return false;
            string message;bool ok=resume ? RebirthAudiobookListeningSessionService.TryResume(player,out message)
                : RebirthAudiobookListeningSessionService.TryPause(player,out message);
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);return ok;
        }
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        if(!RebirthMusicLibraryClient.CanSend(connection,request))return false;
        connection.SendToServer(request.Setup(player.entityId,resume?9:8,0,null,Revision,CreationId));return true;
    }
    public static bool RequestListen(EntityPlayerLocal player,int index,string slotId)
    {
        if(player?.world==null||!RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||
            RebirthMusicLibraryClient.PendingTransfer||index<0||index>=SlotIds.Count||SlotIds[index]!=slotId)return false;
        if(!player.world.IsRemote())
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||
                record.Support.AudiobookRevision!=Revision||!RebirthSurvivorRequestScope.Matches(CreationId,record.Origin?.CreationId))return false;
            string message;return RebirthAudiobookListeningSessionService.TryBeginStored(player,slotId,out message);
        }
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        if(!RebirthMusicLibraryClient.CanSend(connection,request))return false;
        connection.SendToServer(request.Setup(player.entityId,7,index,null,Revision,CreationId));return true;
    }
    public static bool RequestTransfer(EntityPlayerLocal player,bool insert,int index,ItemValue item)
    {
        if(player?.world==null||RebirthBackpackLibraryReservation.IsHeld(player))return false;
        if(!player.world.IsRemote())
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||!record.IsComplete)return false;
            if(!RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||
                !RebirthSurvivorRequestScope.Matches(CreationId,record.Origin?.CreationId))
            {RefreshLocal(player);return false;}
            RebirthMusicTransferState offer;
            string sourceProof; RebirthMusicSourceProof.TryCreate(item,out sourceProof);
            if(!RebirthMusicTransferServer.PrepareLocalAudiobookSource(player,insert?1:2,index,item?.type??0,item?.Seed??0,
                Revision,CreationId,sourceProof,out offer))return false;
            bool settled=RebirthMusicTransferServer.ApplyLocalPending(player,CreationId);
            RefreshLocal(player);
            return settled;
        }
        if(
            !RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||
            !RebirthSurvivorRequestScope.Matches(CreationId,RebirthMusicLibraryClient.CreationId)||
            RebirthMusicLibraryClient.PendingTransfer)return false;
        if(insert && (item==null||item.IsEmpty()) || !insert && (index<0||index>=Items.Count))return false;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
        var inventory=insert ? NetPackageManager.GetPackage<NetPackagePlayerInventory>() : null;
        if(!RebirthMusicLibraryClient.CanSend(connection,request)||
            insert&&!RebirthMusicLibraryClient.CanSend(connection,inventory))return false;
        if(insert)connection.SendToServer(inventory.Setup(player,true,true,false,false));
        connection.SendToServer(request.Setup(player.entityId,insert?5:6,index,item,Revision,CreationId));
        return true;
    }
    public static void RefreshLocal(EntityPlayerLocal player)
    {
        if(player?.world==null||player.world.IsRemote())return;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record?.Support==null||!record.IsComplete)return;
        var music=new List<string>();foreach(var entry in record.Support.MusicCassettes)music.Add(entry.ItemId);
        RebirthMusicLibraryClient.Receive(record.Origin.CreationId,record.Support.MusicRevision,record.Support.MusicShuffle,music,record.Support.PendingMusicTransfer!=null);
        var slots=new List<string>();var ids=new List<string>();
        foreach(var entry in record.Support.AudiobookCassettes){slots.Add(entry.SlotId);ids.Add(entry.ItemId);}
        Receive(record.Origin.CreationId,record.Support.AudiobookRevision,slots,ids);
    }
    public static void Reset(){ClearUse();CreationId=string.Empty;Revision=-1;SlotIds.Clear();Items.Clear();}
    public static void Receive(string creationId,long revision,IList<string> slots,IList<string> ids)
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(!RebirthMusicLibraryClient.EnsureCurrent(player)||
            !RebirthSurvivorRequestScope.Matches(creationId,RebirthMusicLibraryClient.CreationId)||revision<0||
            CreationId==creationId&&revision<Revision||slots==null||ids==null||slots.Count!=ids.Count||slots.Count>RebirthAudiobookLibraryPersistence.Capacity)return;
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<slots.Count;i++)
        {Guid slot;if(!Guid.TryParseExact(slots[i],"N",out slot)||slot==Guid.Empty||!seen.Add(slots[i])||string.IsNullOrEmpty(ids[i]))return;}
        CreationId=creationId;Revision=revision;SlotIds.Clear();SlotIds.AddRange(slots);Items.Clear();Items.AddRange(ids);
        ApplyPendingUse(player);
    }
}