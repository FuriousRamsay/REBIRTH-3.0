using System;
using System.Collections.Generic;
using System.Linq;

// Owning native player checkpoint. Runtime receipt setters and an upload request
// are not saved evidence. The exact native inventory accompanies every receipt.
internal static class RebirthVehicleOwnerCheckpoint
{
    internal static bool Persist(EntityPlayerLocal player,IEnumerable<Guid> operations,float stage,Func<bool> current)
    {
        try
        {
            var ids=operations?.ToArray();
            if(ids==null||ids.Length<1||ids.Length>256||!ThreadManager.IsMainThread()||current==null||!current()||
                player?.Buffs==null||!ReferenceEquals(player,player.world?.GetPrimaryPlayer())||!ReferenceEquals(player.world,GameManager.Instance?.World))return false;
            var bag=RebirthVehicleInventoryWrite.Snapshot(player.bag?.ItemGrid.items);var belt=RebirthVehicleInventoryWrite.Snapshot(player.inventory?.ItemGrid.items);
            if(bag==null||belt==null||ids.Any(id=>player.Buffs.GetCustomVar("rbVehicle_"+id.ToString("N"))!=stage))return false;
            var game=GameManager.Instance;var world=player.world;var nativeBag=player.bag;var nativeBelt=player.inventory;
            bool Exact()=>current()&&ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(world,player.world)&&ReferenceEquals(game.World,world)&&
                ReferenceEquals(nativeBag,player.bag)&&ReferenceEquals(nativeBelt,player.inventory)&&
                RebirthVehicleInventoryWrite.SameSlots(bag,player.bag?.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(belt,player.inventory?.ItemGrid.items)&&
                ids.All(id=>player.Buffs.GetCustomVar("rbVehicle_"+id.ToString("N"))==stage);
            bool Verified()
            {
            if(!Exact()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||!RebirthGearNativePlayerFile.TryRead(owner,out var file)||file.deaths!=player.Died||
                file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024||
                !RebirthVehicleReceiptReader.TryRead(file.buffData.ToArray(),ids,out var saved))return false;
            var expected=stage==1f?RebirthVehicleOwnerTransferResult.Applied:stage==-1f?RebirthVehicleOwnerTransferResult.Rejected:RebirthVehicleOwnerTransferResult.Indeterminate;
            return saved.Count==ids.Length&&ids.All(id=>saved.TryGetValue(id,out var result)&&result==expected)&&
                RebirthVehicleInventoryWrite.SameSlots(bag,RebirthGearPlayerDataInventory.ReadSlots(file,true))&&
                RebirthVehicleInventoryWrite.SameSlots(belt,RebirthGearPlayerDataInventory.ReadSlots(file,false))&&Exact();
            }
            if(Verified())return true;
            if(!Exact())return false;game.SaveLocalPlayerData();return Verified();
        }
        catch{return false;}
    }
}
