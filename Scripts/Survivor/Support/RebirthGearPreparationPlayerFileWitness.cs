using System;
using System.Xml.Linq;

internal static class RebirthGearPreparationPlayerFileWitness
{
    // Saved marker data only. The file may already contain an applying/postimage;
    // caller must reconcile that original phase, never repeat initial preparation.
    internal static bool TryRead(RebirthStablePlayerIdentity owner,Guid expectedWorld,out string marker,
        out RebirthGearPreparationIntent intent,out RebirthGearInventorySnapshot inventory)
    {
        marker=null;intent=null;inventory=null;
        if(expectedWorld==Guid.Empty||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||saved.buffData==null||
            saved.buffData.Length>1024*1024||!RebirthGearPreparationMarkerReader.TryRead(saved.buffData.ToArray(),
                out var key,out var world,out var owned,out var original)||world!=expectedWorld)return false;
        var bag=RebirthPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthPlayerDataInventory.ReadSlots(saved,false);
        if(!RebirthGearInventorySnapshot.TryCapture(bag,belt,owned,out var snapshot))return false;
        marker=key;intent=original;inventory=snapshot;return true;
    }
    // Marker, receipt stage and physical grids must come from this SAME final
    // native file for cold adoption. No independent cached receipt substitution.
    internal static bool TryReadOriginalPhase(RebirthStablePlayerIdentity owner,Guid expectedWorld,out string marker,
        out RebirthGearPreparationIntent intent,out RebirthGearInventorySnapshot inventory,out float receipt)
    {
        marker=null;intent=null;inventory=null;receipt=0;
        if(expectedWorld==Guid.Empty||!RebirthGearNativePlayerFile.TryRead(owner,out var saved)||saved.buffData==null||
            saved.buffData.Length>1024*1024)return false;
        var bytes=saved.buffData.ToArray();
        if(!RebirthGearPreparationMarkerReader.TryRead(bytes,out var key,out var world,out var owned,out var original)||
            world!=expectedWorld||!RebirthGearReceiptReader.TryReadStage(bytes,original.TransactionId.ToString("N"),out var stage))return false;
        var bag=RebirthPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthPlayerDataInventory.ReadSlots(saved,false);
        if(!RebirthGearInventorySnapshot.TryCapture(bag,belt,owned,out var snapshot))return false;
        marker=key;intent=original;inventory=snapshot;receipt=stage;return true;
    }
    internal static bool HasOriginal(RebirthStablePlayerIdentity owner,Guid expectedWorld,string marker,RebirthGearPreparationIntent intent)
    {
        return intent!=null&&marker!=null&&TryRead(owner,expectedWorld,out var savedMarker,out var savedIntent,out var snapshot)&&
            savedMarker==marker&&XNode.DeepEquals(intent.Write(),savedIntent.Write())&&intent.MatchesInventory(snapshot);
    }
}