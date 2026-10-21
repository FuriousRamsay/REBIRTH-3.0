#if REBIRTH_DEBUG
using System;
using System.IO;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    // Observation only. A pending result is not an exception, a pump, a save or
    // permission to repeat any financial/native action.
    internal static bool ObserveActionTerminalReleaseScenario(Guid operation,int? playerEntity=null)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||operation==Guid.Empty||GameManager.Instance?.World is not World world)
            throw new InvalidOperationException("Actual disposable native world and original required");
        var actor=playerEntity.HasValue?world.GetEntity(playerEntity.Value) as EntityPlayer:world.GetPrimaryPlayer();
        if(actor==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||actor.IsDead()||
            world.IsRemote()&&!ReferenceEquals(world.GetPrimaryPlayer(),actor))return false;
        RebirthStablePlayerIdentity account;
        if(world.IsRemote())
        {if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out account))return false;}
        else if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out account))return false;
        string marker=ActionOwnerMarker(operation);
        if(actionPayments.Any(value=>value.Operation==operation&&!value.Retired&&!value.Terminal)||
            actionRemoteHostOriginals.TryGetValue(operation,out var host)&&!host.Retired||
            actionTerminalCouriers.ContainsKey(operation)||actionRemoteTerminalReceipts.ContainsKey(operation)||actionRemoteClick?.Operation==operation)return false;
        if(!RebirthGearNativePlayerFile.TryRead(account,out var saved)||saved.id!=actor.entityId||saved.deaths!=actor.Died||saved.bDead||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,marker,out _,out bool savedPresent)||savedPresent)return false;
        if(actor is EntityPlayerLocal local)
        {
            if(local.Buffs?.CVars==null||local.Buffs.CVars.ContainsKey(marker)||
                !SameActionItems(local.bag.ItemGrid.items,RebirthGearPlayerDataInventory.ReadSlots(saved,true))||
                !SameActionItems(local.inventory.ItemGrid.items,RebirthGearPlayerDataInventory.ReadSlots(saved,false)))return false;
        }
        else
        {
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);var accepted=peer?.latestPlayerData;
            if(accepted==null||accepted.id!=actor.entityId||accepted.deaths!=actor.Died||accepted.bDead||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var peerAccount)||peerAccount.CanonicalId!=account.CanonicalId||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(accepted,marker,out _,out bool acceptedPresent)||acceptedPresent||
                !ActionOwnerImage(saved).SequenceEqual(ActionOwnerImage(accepted)))return false;
        }
        if(!world.IsRemote())
        {
            string path=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","ActionPayments",operation.ToString("N"),"current.xml");
            ActionPath(path);var head=ReadActionBoundedXml(ActionHeadBytes(path),24*1024*1024);string stage=(string)head.Attribute("stage");
            var original=ReadRemoteActionOriginal(head.Element("original")?.Value);
            if(stage!="used"&&stage!="returned"&&stage!="owner-terminal-saved"||original.Name!="resourceActionOriginal"||
                (string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("account")!=account.CanonicalId||
                (string)original.Attribute("world")!=world.worldState.Guid||(string)original.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor))return false;
        }
        return true;
    }
}
#endif
