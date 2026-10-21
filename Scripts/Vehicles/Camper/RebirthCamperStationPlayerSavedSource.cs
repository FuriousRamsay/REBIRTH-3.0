using System;
using System.IO;

// Detached authenticated final-file source. Caller retains the exact actor,
// connection, creation, station lease and native owned-belt capacity checks.
internal static class RebirthCamperStationPlayerSavedSource
{
    internal static bool TryCopySavedSource(RebirthStablePlayerIdentity identity,out ItemStack[] bag,
        out ItemStack[] belt,out bool[] bagLocks)
    {
        bag=null;belt=null;bagLocks=null;
        try{return ThreadManager.IsMainThread()&&identity!=null&&RebirthGearNativePlayerFile.TryRead(identity,out var saved)&&TryCopySavedSource(saved,out bag,out belt,out bagLocks);}
        catch{bag=null;belt=null;bagLocks=null;return false;}
    }
    internal static bool TryCopySavedSource(PlayerDataFile saved,out ItemStack[] bag,
        out ItemStack[] belt,out bool[] bagLocks)
    {
        bag=null;belt=null;bagLocks=null;
        try
        {
            if(!ThreadManager.IsMainThread()||saved==null)return false;
            var readBag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);
            var readBelt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            if(readBag==null||readBelt==null||saved.bagData==null)return false;
            using(var stream=new MemoryStream(saved.bagData.ToArray(),false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);if(reader.ReadByte()!=2)return false;
                var grid=ItemStackGrid.Read(reader,StreamModeRead.Persistency);
                if(stream.Position!=stream.Length||grid==null||grid.Length!=readBag.Length)return false;
                var locks=grid.SlotLocks;
                if(locks!=null)
                {
                    if(locks.Length!=readBag.Length)return false;
                    bagLocks=new bool[locks.Length];for(int i=0;i<bagLocks.Length;i++)bagLocks[i]=locks[i];
                }
            }
            bag=readBag;belt=readBelt;return true;
        }
        catch{bag=null;belt=null;bagLocks=null;return false;}
    }
}
