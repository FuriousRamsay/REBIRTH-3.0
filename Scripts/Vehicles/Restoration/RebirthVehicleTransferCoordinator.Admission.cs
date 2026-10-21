using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class RebirthVehicleTransferCoordinator
{
    private static readonly Dictionary<Guid,Transfer> active=new Dictionary<Guid,Transfer>();
    private static readonly List<Transfer> revoked=new List<Transfer>();
    private static bool admitting;
    internal static bool IsAdmitting=>admitting;
    internal static void SetAdmitting(bool value)
    {admitting=value;if(!value)foreach(var transfer in active.Values)transfer.RevokeEvidence();}
    internal static bool TryRetain(EntityPlayer player,ClientInfo sender,RebirthVehicleTransferPlan plan,out Transfer transfer)
    {
        transfer=null;
        try
        {
            if(player==null||plan?.Custody==null||plan.NativeDeaths<0||plan.NativeDeaths>player.Died)return false;
            if(active.TryGetValue(plan.TransactionId,out var prior))
            {
                if(prior.Plan.ToXml().ToString()!=plan.ToXml().ToString())return false;
                if(prior.Current()){transfer=prior;return true;}
            }
            if(active.Count+revoked.Count>=64)return false;
            if(!Transfer.TryCreate(player,sender,plan,out var next))return false;
            if(prior!=null){prior.RevokeEvidence();revoked.Add(prior);}
            active[plan.TransactionId]=next;transfer=next;return true;
        }
        catch{return false;}
    }
    internal static bool TryGet(Guid operation,out Transfer transfer)
    {transfer=null;if(!active.TryGetValue(operation,out var found)||!found.Current())return false;transfer=found;return true;}
    internal static void RetirePublished(Transfer transfer)
    {
        if(transfer==null||!transfer.CanPublishResult()||!active.TryGetValue(transfer.Plan.TransactionId,out var exact)||!ReferenceEquals(exact,transfer))return;
        active.Remove(transfer.Plan.TransactionId);revoked.RemoveAll(original=>original.Plan.TransactionId==transfer.Plan.TransactionId&&!original.Current());
    }
    internal static bool TryReadNativeReservation(EntityPlayer player,Guid operation,string owner,string creation,string fingerprint,RebirthVehicleTransferCustody proof,bool untouched,Func<bool> current,out ItemStack[] bag,out ItemStack[] belt)
    {
        bag=belt=null;
        try
        {
            if(!ThreadManager.IsMainThread()||current==null||!current()||player?.world==null||player.world.IsRemote()||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||proof==null||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.CanonicalId!=owner||
                proof.Binding!=RebirthVehicleTransferCustody.Bind(operation,proof.Reservation,owner,player.world.worldState.Guid,creation,fingerprint,proof.OwnerImageHash)||
                !RebirthGearNativePlayerFile.TryRead(identity,out var native)||native.deaths!=player.Died||native.buffData==null||native.buffData.Length<1||native.buffData.Length>1024*1024||
                !RebirthVehicleReceiptReader.TryRead(native.buffData.ToArray(),new[]{proof.Reservation,proof.Binding},out var stages)||
                stages[proof.Reservation]!=RebirthVehicleOwnerTransferResult.Applied||stages[proof.Binding]!=RebirthVehicleOwnerTransferResult.Applied)return false;
            bag=RebirthVehicleInventoryWrite.Snapshot(RebirthGearPlayerDataInventory.ReadSlots(native,true));belt=RebirthVehicleInventoryWrite.Snapshot(RebirthGearPlayerDataInventory.ReadSlots(native,false));
            return (!untouched||RebirthVehicleTransferCustody.Image(bag,belt)==proof.OwnerImageHash)&&current();
        }
        catch{bag=belt=null;return false;}
    }
    internal static void Tick()
    {
        if(!ThreadManager.IsMainThread())return;
        double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        if(now<lastTick)nextTick=now;if(now<nextTick)return;lastTick=now;nextTick=now+0.75d;
        foreach(var transfer in active.Values.ToArray())if(transfer.Current()&&!transfer.Finished)transfer.Advance();
    }
    private static double nextTick,lastTick;
}
