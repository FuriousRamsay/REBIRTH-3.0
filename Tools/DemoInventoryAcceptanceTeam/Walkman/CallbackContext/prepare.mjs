import{readFile,writeFile}from'node:fs/promises';
const root=process.cwd(),here=root+'/Tools/DemoInventoryAcceptanceTeam/Walkman/CallbackContext';
let s=await readFile(root+'/Scripts/Survivor/Network/RebirthAudiobookLibraryClient.cs','utf8');s=s.replace(/\n/g,'\n');
const old='        // Clear before dispatch: a delayed snapshot must not send the same Use again.\n        var item=pendingUse;ClearUse();\n        if(!RequestTransfer(player,true,0,item))\n        {useWorld=player.world;useOwner=player.entityId;useCreation=CreationId;pendingUse=item;}';
if(!s.includes(old))throw Error('pending anchor');s=s.replace(old,'        // Keep unsent intent scoped until both packages are prepared and admitted.\n        TryDispatchRemoteTransfer(player,true,0,pendingUse,true);');
const a=s.indexOf('        if(\n            !RebirthMusicLibraryClient.EnsureCurrent(player)',s.indexOf('    public static bool RequestTransfer(')),b=s.indexOf('    public static void RefreshLocal(',a);
if(a<0||b<0)throw Error('transfer anchor');
s=s.slice(0,a)+`        return TryDispatchRemoteTransfer(player,insert,index,item,false);
    }
    private static bool remoteTransferPreparing;
    // Admission consumes queued intent. Native sends provide no delivery acknowledgement.
    private static bool TryDispatchRemoteTransfer(EntityPlayerLocal player,bool insert,int index,ItemValue item,bool clearQueued)
    {
        if(remoteTransferPreparing||player?.world==null||player.IsDead()||!player.world.IsRemote()||
            RebirthBackpackLibraryReservation.IsHeld(player)||!RebirthMusicLibraryClient.EnsureCurrent(player)||Revision<0||
            !RebirthSurvivorRequestScope.Matches(CreationId,RebirthMusicLibraryClient.CreationId)||RebirthMusicLibraryClient.PendingTransfer)return false;
        if(insert&&(item==null||item.IsEmpty())||!insert&&(index<0||index>=Items.Count||index>=SlotIds.Count))return false;
        var game=GameManager.Instance;var world=player.world;var owner=player.entityId;
        var creation=CreationId;var revision=Revision;var generation=RebirthMusicLibraryClient.Generation;
        var slotId=insert?null:SlotIds[index];
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(game==null||!ReferenceEquals(game.World,world)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||
            clearQueued&&!ReferenceEquals(pendingUse,item))return false;
        remoteTransferPreparing=true;
        try
        {
            var request=NetPackageManager.GetPackage<NetPackageRebirthMusicLibraryRequest>();
            var inventory=insert?NetPackageManager.GetPackage<NetPackagePlayerInventory>():null;
            if(!RebirthMusicLibraryClient.CanSend(connection,request)||insert&&!RebirthMusicLibraryClient.CanSend(connection,inventory))return false;
            request.Setup(owner,insert?5:6,index,item,revision,creation);
            if(insert)inventory.Setup(player,true,true,false,false);
            // Native Setup invokes serializers. Reject changed context before either send.
            if(!ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(world,game.World)||!ReferenceEquals(world,player.world)||
                !ReferenceEquals(world.GetPrimaryPlayer(),player)||player.entityId!=owner||player.IsDead()||!world.IsRemote()||
                !RebirthMusicLibraryClient.EnsureCurrent(player)||Revision!=revision||CreationId!=creation||
                RebirthMusicLibraryClient.Generation!=generation||RebirthMusicLibraryClient.PendingTransfer||
                RebirthBackpackLibraryReservation.IsHeld(player)||!ReferenceEquals(connection,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !RebirthMusicLibraryClient.CanSend(connection,request)||insert&&!RebirthMusicLibraryClient.CanSend(connection,inventory)||
                !insert&&(index>=SlotIds.Count||SlotIds[index]!=slotId)||clearQueued&&!ReferenceEquals(pendingUse,item))return false;
            if(clearQueued)ClearUse();
            if(insert)connection.SendToServer(inventory);
            connection.SendToServer(request);
            return true;
        }
        finally {remoteTransferPreparing=false;}
    }
`+s.slice(b);
s=s.replace('item.IsEmpty()||player.IsDead()||RebirthBackpackLibraryReservation.IsHeld(player))','item.IsEmpty()||player.IsDead()||pendingUse!=null||remoteTransferPreparing||RebirthBackpackLibraryReservation.IsHeld(player))').replace('if(player?.world==null||RebirthBackpackLibraryReservation.IsHeld(player))return false;','if(remoteTransferPreparing||player?.world==null||RebirthBackpackLibraryReservation.IsHeld(player))return false;').replaceAll('!ReferenceEquals(world.GetPrimaryPlayer(),player)||','!ReferenceEquals(world.GetPrimaryPlayer(),player)||!ReferenceEquals(world.GetEntity(owner),player)||').replace('player.IsDead()||!player.world.IsRemote()','player.IsDead()||!player.IsSpawned()||!player.world.IsRemote()').replace('player.IsDead()||!world.IsRemote()','player.IsDead()||!player.IsSpawned()||!world.IsRemote()').replace('var slotId=insert?null:SlotIds[index];','var slotId=insert?null:SlotIds[index];var itemId=insert?null:Items[index];').replace('!insert&&(index>=SlotIds.Count||SlotIds[index]!=slotId)','!insert&&(index>=SlotIds.Count||SlotIds[index]!=slotId||index>=Items.Count||Items[index]!=itemId)');await writeFile(here+'/RebirthAudiobookLibraryClient.CALLBACK_CANDIDATE.cs.txt',s);