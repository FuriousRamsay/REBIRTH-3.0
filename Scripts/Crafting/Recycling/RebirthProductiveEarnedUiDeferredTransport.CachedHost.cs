using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Admission observes a saved cache through its actual native load/Add
    // lineage. It neither inserts a receipt nor repeats the original craft.
    private static void ObserveCachedHostSource(World world,TileEntityWorkstation station)
    {
        HostContribution candidate=null;IDisposable allocation=null,retention=null;
        try
        {
            if(!RebirthWorkstationCompletionPipeline.TryGetCachedHostOriginal(world,station,out var source)||!source.Current())return;
            if(host.TryGetValue(source.Operation,out var existing))
            {
                if(existing.CachedOriginal!=null)return;
                if(source.NativeSource==null||!ReferenceEquals(existing.NativeOriginal,source.NativeSource)||existing.NativePhase!=1||
                    !existing.SourceCurrentWithoutViewer()||!source.Current())return;
                long copied=source.MeasureMembership();
                if(copied<1||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(existing.Allocation,
                    checked(32*copied+32L*(existing.NativeAcceptedBody?.Length??0)+4L*source.PublishedBody.Length+65536)))return;
                var cachedMembership=source.CopyMembership();var normalized=cachedMembership==null?null:new XElement(cachedMembership);
                normalized?.Attribute("cachedOriginal")?.Remove();normalized?.Attribute("phase")?.Remove();
                if(normalized==null||!XNode.DeepEquals(normalized,existing.NativeMembership)||
                    !source.TryGetAggregate(out var aggregate)||!Row(aggregate).SequenceEqual(existing.Aggregate)||
                    !source.TryResolvePurpose(source.RecipientPurpose,out var owner))return;
                retention=source.Retain();if(retention==null)return;
                existing.Retentions.Add(retention);retention=null;
                existing.CachedOriginal=source;existing.Nonce=source.Nonce;existing.Birth=source.Birth;existing.SourceHash=source.SourceHash;
                existing.ImmutableMembership=cachedMembership;existing.Recipient=owner;existing.AcceptedReceipt=aggregate;
                existing.NativePublication?.Dispose();existing.NativePublication=null;
                return;
            }
            long measured=source.MeasureMembership();
            if(host.Count>=4||measured<1||measured>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measured+4L*source.PublishedBody.Length+65536),out allocation))return;
            var membership=source.CopyMembership();
            if(membership==null||!source.TryGetAggregate(out var row)||
                !source.TryResolvePurpose(source.RecipientPurpose,out var recipient))return;
            var members=membership.Element("members")?.Elements("member").ToArray();
            if(members==null||members.Length<1||members.Length>4||
                members.Select(value=>ReadGuid(value,"operation")).Distinct().Count()!=members.Length||
                !Row(row).SequenceEqual(ReadBytes(membership,"aggregate"))||row.CrafterEntityID!=recipient.StartingEntityId)return;
            long total=0;
            foreach(var member in members)
            {
                if(!source.TryResolvePurpose((string)member.Attribute("source"),out _)||
                    (int?)member.Attribute("contribution") is not int count||count<1)return;
                total=checked(total+count);
            }
            if(total!=row.CraftedItemStack.count)return;
            retention=source.Retain();if(retention==null)return;
            candidate=new HostContribution{CachedOriginal=source,World=world,Station=station,
                Operation=source.Operation,Nonce=source.Nonce,Birth=source.Birth,SourceHash=source.SourceHash,
                Recipient=recipient,RecipientOwner=recipient.OriginalOwnerKey,RecipientCreation=recipient.Creation,
                RecipientEntityId=recipient.StartingEntityId,ImmutableMembership=membership,
                BeforeComplete=ReadBytes(membership,"beforeComplete"),AfterComplete=ReadBytes(membership,"afterComplete"),
                Aggregate=Row(row),AcceptedReceipt=row,Allocation=allocation};allocation=null;
            candidate.Retentions.Add(retention);retention=null;
            if(!source.Current()||!candidate.SourceCurrentWithoutViewer())return;
            host.Add(source.Operation,candidate);candidate=null;
        }
        catch{/* Unknown cache lineage remains retained by its original producer. */}
        finally{candidate?.Dispose();retention?.Dispose();allocation?.Dispose();}
    }
    private static bool TryResolveContributionPurpose(HostContribution source,string purpose,out RebirthProductiveEarnedUiQueueOwner.Witness owner)
    {
        if(source.CachedOriginal!=null)return source.CachedOriginal.TryResolvePurpose(purpose,out owner);
        return RebirthProductiveEarnedUiQueueOwner.TryResolvePublishedPurpose(source.World,source.Station,purpose,out owner);
    }
}
