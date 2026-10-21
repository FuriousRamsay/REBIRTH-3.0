using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private static Guid NativeCancellationOperation(Guid job)
    {
        byte[] digest;using(var hash=SHA256.Create())digest=hash.ComputeHash(Encoding.UTF8.GetBytes(job.ToString("N")+":native-actor-cancel"));
        var bytes=new byte[16];Array.Copy(digest,bytes,16);return new Guid(bytes);
    }
    // Exact remote native actor cancellation. Its existing native payment
    // journal retains queue removal + owner refund images together. Full
    // destination keeps the original row/claim unchanged; never drop overflow.
    internal static bool CancelFetched(EntityPlayer actor,RebirthCamperStationAddress address,string creation,Guid job)
    {
        try
        {
            if(!ProtocolActive||job==Guid.Empty||!RebirthCamperStationNativeActorPayment.RemoteIdentity(actor,out var owner,out _)||
                !RebirthStationLiveAccess.TryGetMobileLease(actor,address,creation,out var station,out var lease))return false;
            Guid cancellation=NativeCancellationOperation(job);
            if(RebirthCamperStationNativeActorPayment.IsHeld(station))return RebirthCamperStationNativeActorPayment.MatchesPendingOperation(station,cancellation)&&
                RebirthCamperStationNativeActorPayment.TryResume(actor,station,address,creation);
            if(!TryPlanCancellation(actor,station,address,creation,job,out var plan)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var before,out var saved)||saved.id!=actor.entityId||saved.deaths!=actor.Died||before.Cursor?.IsEmpty()!=true||
                !RebirthRecyclerOwnerDelivery.Plan(before.Bag,before.Belt,before.BagLocks,null,actor.inventory.Length,plan.CopyRefunds(),out var bag,out var belt))return false;
            var originalRow=station.Queue[plan.Slot];var queue=(RecipeQueueItem[])station.Queue.Clone();queue[plan.Slot]=new RecipeQueueItem{StartingEntityId=-1};
            if(!RebirthStationLiveAccess.TryGetMobileLease(actor,address,creation,out var current,out var finalLease)||!ReferenceEquals(current,station)||!ReferenceEquals(finalLease,lease)||
                !ReferenceEquals(station.Queue[plan.Slot],originalRow)||RebirthRemoteResourceSourceCustody.BlocksStation(station))return false;
            // Native Begin preflights its actual immutable paired Frames and
            // rechecks final owner Before before any setter/queue publication.
            // It saves the actual owner refund and exact empty native row before
            // settlement. The already-used fetch root never rolls back sources.
            return RebirthCamperStationNativeActorPayment.Begin(actor,station,address,creation,cancellation,before.Bag,bag,before.Belt,belt,queue,owner,before.BagLocks);
        }
        catch{return false;}
    }
}
