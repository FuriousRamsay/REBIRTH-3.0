using System;
using System.IO;

// Register only after production adapters and the authenticated character service
// are installed. Remote UI needs the server transport, not this local provider.
public static class RebirthCamperLocalAccess
{
    private static Func<EntityPlayerLocal,string> creationProvider;
    public static void Register(Func<EntityPlayerLocal,string> authenticatedCreation)
    {
        if(authenticatedCreation==null)throw new ArgumentNullException(nameof(authenticatedCreation));
        if(creationProvider!=null)throw new InvalidOperationException("Camper character provider already registered.");
        creationProvider=authenticatedCreation;
        RebirthCamperRadial.OpenSession=Open;
    }
    private static void Open(EntityVehicle vehicle,EntityPlayerLocal player,string command)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread() || world==null || player==null
            || !ReferenceEquals(world.GetPrimaryPlayer(),player) || !ReferenceEquals(vehicle?.world,world))
            throw new InvalidOperationException("Local authoritative camper session required.");
        if(command==RebirthCamperRadial.Energy){RebirthCamperEnergyAccess.Open(vehicle,player);return;}
        if(world.IsRemote())
        {
            if(command==RebirthCamperRadial.Craft){RebirthCamperRemoteStationClient.OpenPicker(vehicle,player);return;}
            if(command!=RebirthCamperRadial.Storage && command!=RebirthCamperRadial.Stations)throw new ArgumentException("Unknown camper management action.");
            RebirthCamperRemoteManagement.Open(vehicle,player,command==RebirthCamperRadial.Stations);return;
        }
        string creation=creationProvider(player);
        if(string.IsNullOrWhiteSpace(creation))throw new InvalidOperationException("Authenticated character creation is unavailable.");
        var owner=GameManager.Instance.getPersistentPlayerID(null);
        if(owner==null)throw new InvalidOperationException("Native player identity is unavailable.");
        string ownerId=owner.CombinedString;
        string saveDirectory=GameIO.GetSaveGameDir();
        Func<bool> current=()=>ReferenceEquals(GameManager.Instance?.World,world)
            && ReferenceEquals(world.GetPrimaryPlayer(),player) && !player.IsDead()
            && creationProvider(player)==creation && GameIO.GetSaveGameDir()==saveDirectory
            && GameManager.Instance.getPersistentPlayerID(null)?.CombinedString==ownerId;
        if(command==RebirthCamperRadial.Craft)
        { RebirthCamperStationAccess.OpenPicker(vehicle,player,current);return; }
        if(command!=RebirthCamperRadial.Storage && command!=RebirthCamperRadial.Stations)
            throw new ArgumentException("Unknown camper management action.");
        if(string.IsNullOrEmpty(saveDirectory))throw new InvalidOperationException("Native save directory is unavailable.");
        // Durable receipts belong to the save, not TEMP, so cleanup cannot remove
        // pending recovery evidence or mix receipts from different worlds.
        var journal=new RebirthCamperTransferJournal(Path.Combine(saveDirectory,"REBIRTH","CamperTransfers"));
        RebirthCamperManagementAccess.Open(vehicle,player,command==RebirthCamperRadial.Stations,
            creation,journal,current,
            (receipt,item)=>RebirthCamperSavedCursor.SaveAndVerifyLocal(player,receipt,item,current),
            (receipt,payload)=>false); // Native async acknowledgement owns vehicle publication.
    }
}
