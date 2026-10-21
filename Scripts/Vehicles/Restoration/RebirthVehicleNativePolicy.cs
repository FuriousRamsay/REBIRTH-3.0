using System;
using System.IO;

internal static class RebirthVehicleNativePolicy
{
    internal static bool IsLocked(EntityPlayerLocal player,bool bag,int slot)
    {
        var grid=bag?player?.bag?.ItemGrid:player?.inventory?.ItemGrid;
        if(grid?.items==null||slot<0||slot>=grid.items.Length)return true;
        return grid.IsLocked(slot)||bag&&player.bag.LockedSlots!=null&&slot<player.bag.LockedSlots.Length&&player.bag.LockedSlots[slot];
    }
    internal static string Capture(EntityPlayerLocal player,bool bag)
    {
        var slots=bag?player?.bag?.ItemGrid?.items:player?.inventory?.ItemGrid?.items;
        if(slots==null||slots.Length>169)throw new InvalidDataException("Native vehicle owner lock dimensions unavailable.");
        var bits=new char[slots.Length];for(int slot=0;slot<bits.Length;slot++)bits[slot]=IsLocked(player,bag,slot)?'1':'0';return new string(bits);
    }
}
