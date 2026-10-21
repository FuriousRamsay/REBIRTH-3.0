using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // A real installed cached native load supplies this local receipt. No old
    // UIO registration, network viewer, native Add or callback is recreated.
    internal static void ObserveLoadedLocalOwnerReceipt(World world,TileEntityWorkstation station,EntityPlayerLocal actor)
    {
        ObserveAuthorityLoadedLocalOwnerReceipt(world,station,actor);
        HostLocalReceipt receipt=null;IDisposable memory=null,retention=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
                !ReferenceEquals(world.GetPrimaryPlayer(),actor)||actor==null||actor.IsDead()||
                !ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
            ObserveCachedHostSource(world,station);
            var sources=host.Values.Where(value=>!value.Closed&&ReferenceEquals(value.World,world)&&ReferenceEquals(value.Station,station)&&
                value.CachedOriginal?.Current()==true&&value.RecipientOwner==owner.StorageKey&&value.RecipientEntityId==actor.entityId&&
                value.RecipientCreation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&value.Recipient.Deaths==actor.Died).ToArray();
            if(sources.Length!=1)return;var source=sources[0];
            if(hostLocalReceipts.TryGetValue(source.Operation,out var prior))
            {
                if(prior.Current())return;
                if(RebirthProductiveEarnedUiOwnerFile.HasLocalOwnerAttempt(station)||!RebirthProductiveEarnedUiOwnerFile.DropUnusedHostLocalRead(prior))return;
                hostLocalReceipts.Remove(source.Operation);prior.Close();
            }
            if(hostLocalReceipts.Count>=4||!source.CachedOriginal.TryGetAggregate(out var row)||
                !TryReadNativeObservationFence(world,station,out long fence)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4*MeasurePublicationElement(source.ImmutableMembership)+65536),out memory))return;
            retention=source.CachedOriginal.Retain();if(retention==null)return;
            receipt=new HostLocalReceipt(source,row,fence,memory,retention,true);memory=retention=null;
            foreach(var member in source.ImmutableMembership.Element("members").Elements("member"))
                receipt.Members.Add(HostLocalContribution.FromLoadedCache(receipt,member));
            if(receipt.Members.Count<1||receipt.Members.Count>4||!RebirthProductiveEarnedUiQueueOwner.ImportActualHostLocalReceipt(receipt))return;
            hostLocalReceipts.Add(source.Operation,receipt);receipt=null;
        }
        catch{/* A failed attachment retains the original cached custody. */}
        finally{receipt?.Close();memory?.Dispose();retention?.Dispose();}
    }
    private static void ObserveAuthorityLoadedLocalOwnerReceipt(World world,TileEntityWorkstation station,EntityPlayerLocal actor)
    {
        HostLocalReceipt receipt=null;IDisposable memory=null,retention=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||actor==null||!ReferenceEquals(world.GetPrimaryPlayer(),actor)||actor.IsDead())return;
            var candidates=authorityLocalLoaded.Where(value=>ReferenceEquals(value.World,world)&&value.Position==station.ToWorldPos()&&value.Current()).ToArray();
            if(candidates.Length!=1)return;var loaded=candidates[0];
            if(!loaded.TryAttach(actor)||!loaded.IdentityCurrent())return;var source=loaded.Contribution;
            if(hostLocalReceipts.ContainsKey(source.Operation)||hostLocalReceipts.Count>=4||
                !TryReadNativeObservationFence(world,station,out long fence)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4*MeasurePublicationElement(source.ImmutableMembership)+65536),out memory))return;
            retention=loaded.Retain();if(retention==null)return;
            receipt=new HostLocalReceipt(source,source.AcceptedReceipt,fence,memory,retention,false,true);memory=retention=null;
            foreach(var member in source.ImmutableMembership.Element("members").Elements("member"))receipt.Members.Add(HostLocalContribution.FromLoadedAuthority(receipt,member));
            if(!RebirthProductiveEarnedUiQueueOwner.ImportActualHostLocalReceipt(receipt))return;
            hostLocalReceipts.Add(source.Operation,receipt);receipt=null;
        }
        catch{/* Actual installed source remains retained when local attachment refuses. */}
        finally{receipt?.Close();memory?.Dispose();retention?.Dispose();}
    }
}
