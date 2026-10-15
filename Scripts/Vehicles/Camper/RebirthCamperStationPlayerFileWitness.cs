using System;
using System.IO;

// Receipt and exact actor inventory must be from one final native file.
// Uploaded cache, backup files and receipt-only evidence cannot qualify debit.
internal static class RebirthCamperStationPlayerFileWitness
{
    internal static bool HasSavedReceipt(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission admission,Guid operation)
    {
        try
        {
            return ThreadManager.IsMainThread()&&identity!=null&&admission?.IsPlayerPaymentConfirmed==true&&operation!=Guid.Empty&&
                operation==RebirthCamperStationPlayerPayment.Operation(admission)&&admission.TryGetPlayerPayment(out var image)&&
                image.OwnerKey==identity.StorageKey&&image.OwnerId==identity.CanonicalId&&
                RebirthGearNativePlayerFile.TryRead(identity,out var saved)&&saved.buffData!=null&&saved.buffData.Length<=1024*1024&&
                Receipt(saved.buffData.ToArray(),operation,1f);
        }
        catch{return false;}
    }
    internal static bool HasSaved(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission admission,
        Guid operation,bool applied)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||identity==null||admission==null||operation==Guid.Empty||
                operation!=RebirthCamperStationPlayerPayment.Operation(admission)||
                !admission.TryGetPlayerPayment(out var image)||image.OwnerKey!=identity.StorageKey||image.OwnerId!=identity.CanonicalId||
                !RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.buffData==null||saved.buffData.Length>1024*1024||
                !Receipt(saved.buffData.ToArray(),operation,applied?1f:2f))return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);
            var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            return applied?image.MatchesAfter(bag,belt):image.MatchesBeforeSlots(bag,belt);
        }
        catch{return false;}
    }
    private static bool Receipt(byte[] bytes,Guid operation,float phase)
    {
        using(var stream=new MemoryStream(bytes,false))
        using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();
            if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            count=reader.ReadUInt16();bool found=false;string key="rbCamperOwner_"+operation.ToString("N");
            for(int i=0;i<count;i++)
            {
                string name=reader.ReadString();float value=reader.ReadSingle();
                if(name!=key)continue;if(found||value!=phase)return false;found=true;
            }
            return found&&stream.Position==stream.Length;
        }
    }
}
