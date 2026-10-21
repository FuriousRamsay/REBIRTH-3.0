using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostContribution
    {
        internal RebirthWorkstationCompletionPipeline.RemoteOutputHostSource LoadedOwnerSource;
        internal readonly System.Collections.Generic.Dictionary<string,RebirthProductiveEarnedUiQueueOwner.Witness> LoadedOwnerPurposes=
            new System.Collections.Generic.Dictionary<string,RebirthProductiveEarnedUiQueueOwner.Witness>(StringComparer.Ordinal);
    }
    private static bool TryResolveLoadedContributionPurpose(HostContribution candidate,string purpose,out RebirthProductiveEarnedUiQueueOwner.Witness witness)
    {
        if(RebirthProductiveEarnedUiQueueOwner.TryResolvePublishedPurpose(candidate.World,candidate.Station,purpose,out witness))return true;
        if(purpose==null)return false;
        if(candidate.LoadedOwnerPurposes.TryGetValue(purpose,out witness))return witness.SourceCurrent();
        if(candidate.LoadedMemberSource==null||candidate.LoadedOwnerPurposes.Count>=5||
            !RebirthProductiveEarnedUiQueueOwner.TryResolveLoadedAggregatePurpose(candidate.LoadedMemberSource,purpose,out witness))return false;
        candidate.LoadedOwnerPurposes.Add(purpose,witness);candidate.Retentions.Add(witness);return true;
    }
    // Transaction-local installation of an actual native-loaded Add receipt.
    // Rollback removes only this newly attached observer, never a native row.
    internal sealed class LoadedNativeContribution:IDisposable
    {
        private HostContribution value;private bool committed;
        internal LoadedNativeContribution(HostContribution value){this.value=value;}
        internal void Commit()
        {
            if(committed)return;
            if(value==null)throw new ObjectDisposedException(nameof(LoadedNativeContribution));
            // Adopt routing only once the complete owner/PDS attachment has
            // succeeded. A refused attachment leaves prior routes intact.
            SupersedeAggregateMembers(value);
            committed=true;
        }
        internal bool QualifyFinalPhysical(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource loaded)
        {
            if(value==null||value.NativePhase!=1||loaded?.FinalCallbackSaved!=true||loaded.Current()!=true||
                loaded.Native?.Current()!=true||!value.NativeOriginal.IsRemoteOutputRestart(loaded)||
                !NativeViewerPds.ValidateLoadedDeferredFinal(value.NativeOriginal,loaded,out var physical))return false;
            // This is the separately loaded native/PDS final source. No native
            // packet dispatch or Add callback is manufactured by its adoption.
            value.NativePhysical=physical;value.NativePhase=2;
            return true;
        }
        public void Dispose(){var own=value;value=null;if(!committed)own?.Dispose();}
    }
    internal static bool TryAttachLoadedNativeContribution(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,
        RebirthWorkstationCompletionPipeline.RemoteOutputHostSource loaded,out LoadedNativeContribution attachment)
    {
        attachment=null;HostContribution candidate=null;IDisposable allocation=null;RebirthProductiveEarnedUiQueueOwner.Witness ownedRecipient=null;
        try
        {
            if(!ThreadManager.IsMainThread()||original?.CurrentViewer()!=true||!original.IsRemoteOutputRestart(loaded)||
                loaded?.Current()!=true||loaded.DeferredAddBody==null||loaded.Native?.IdentityCurrent()!=true||
                loaded.Native.Current()!=true||!original.BeforeSaved||host.ContainsKey(original.Operation)||host.Count>=4||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,loaded.DeferredAddBody,loaded.DeferredAddOffset))return false;
            var membership=loaded.DeferredMembership??loaded.Request.Element("deferredNativeContribution");
            if(membership==null||membership.Attribute("phase")!=null||membership.Element("physicalDelivery")!=null||
                membership.Element("activeBefore")!=null||membership.Element("actualCachedSpaceTransfer")!=null||
                (int?)membership.Attribute("version")!=1||(string)membership.Attribute("operation")!=original.Operation.ToString("N")||
                (string)membership.Attribute("nonce")!=original.Nonce.ToString("N")||(string)membership.Attribute("birth")!=original.Birth.ToString("N")||
                (string)membership.Attribute("original")!=original.SourceHash||
                (string)membership.Attribute("source")!=RebirthProductiveEarnedUiQueueOwner.Export(original.EarlierOwner)||
                (int?)membership.Attribute("contribution")!=original.Recipe.count||
                !TryDescribeRemoteNativeAddBody(original.Recipe,original.EarlierOwner.StartingEntityId,original.NativeBefore,original.BeforeOffset,
                    membership,out var expected,out int offset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(expected,offset,loaded.DeferredOriginAddBody??loaded.DeferredAddBody,
                    loaded.DeferredOriginAddBody==null?loaded.DeferredAddOffset:loaded.DeferredOriginAddOffset))return false;
            string recipientPurpose=(string)membership.Attribute("recipient");
            if(!RebirthProductiveEarnedUiQueueOwner.TryResolvePublishedPurpose(original.World,original.Station,recipientPurpose,out var recipient))
            {
                if(!RebirthProductiveEarnedUiQueueOwner.TryResolveLoadedAggregatePurpose(loaded,recipientPurpose,out recipient))return false;
                ownedRecipient=recipient;
            }
            var rows=original.Station.CraftCompleteList?.Where(row=>Row(row).SequenceEqual(ReadBytes(membership,"aggregate"))).ToArray();
            if(rows?.Length!=1||rows[0].CrafterEntityID!=recipient.StartingEntityId)return false;
            var members=membership.Element("members")?.Elements("member").ToArray();
            if(members==null||members.Length<1||members.Length>4||members.Select(node=>ReadGuid(node,"operation")).Distinct().Count()!=members.Length)return false;
            long size=MeasurePublicationElement(membership);
            if(size>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32*size+32L*loaded.DeferredAddBody.Length+65536),out allocation))return false;
            var contribution=new XElement(membership);contribution.SetAttributeValue("phase",1);
            candidate=new HostContribution{NativeOriginal=original,World=original.World,Station=original.Station,
                Operation=original.Operation,Nonce=original.Nonce,Birth=original.Birth,SourceHash=original.SourceHash,
                Recipient=recipient,RecipientOwner=recipient.OriginalOwnerKey,RecipientCreation=recipient.Creation,RecipientEntityId=recipient.StartingEntityId,
                ImmutableMembership=contribution,NativeMembership=new XElement(membership),
                BeforeComplete=ReadBytes(membership,"beforeComplete"),AfterComplete=ReadBytes(membership,"afterComplete"),
                Aggregate=ReadBytes(membership,"aggregate"),Allocation=allocation,LoadedOwnerSource=loaded};allocation=null;
            if(ownedRecipient!=null)
            {candidate.LoadedOwnerPurposes.Add(recipientPurpose,ownedRecipient);candidate.Retentions.Add(ownedRecipient);ownedRecipient=null;}
            long total=0;bool own=false;
            foreach(var member in members)
            {
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(member,"nativeAddArgument"),out var argument)||
                    argument.CraftedItemStack.itemValue.GetItemId()!=rows[0].CraftedItemStack.itemValue.GetItemId()||
                    argument.ItemScrapped!=rows[0].ItemScrapped)return false;
                total=checked(total+(int)member.Attribute("contribution"));IDisposable lease;
                if(ReadGuid(member,"operation")==original.Operation)
                {
                    if((string)member.Attribute("kind")!="native"||
                        (int?)member.Attribute("contribution")!=original.Recipe.count||
                        (string)member.Attribute("source")!=RebirthProductiveEarnedUiQueueOwner.Export(original.EarlierOwner)||
                        !XNode.DeepEquals(member.Element("nativeOriginal")?.Elements().SingleOrDefault(),original.Before)||
                        !ReadBytes(member,"nativeAddArgument").SequenceEqual(ReadBytes(membership,"nativeAddArgument"))||
                        !ReadBytes(member,"beforeComplete").SequenceEqual(candidate.BeforeComplete)||!ReadBytes(member,"afterComplete").SequenceEqual(candidate.AfterComplete)||
                        !NativeArgumentMatches(argument,original.Recipe,original.EarlierOwner.StartingEntityId))return false;
                    lease=original.Retain();own=true;
                }
                else if(!ValidateRemoteNativeMember(candidate,member,out lease))return false;
                if(lease==null)return false;candidate.Retentions.Add(lease);
            }
            if(!own||total!=rows[0].CraftedItemStack.count)return false;
            var recipientLease=recipient.RetainPublishedSource();if(recipientLease==null)return false;candidate.Retentions.Add(recipientLease);
            if(!loaded.Current()||!loaded.Native.Current()||!original.CurrentViewer()||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,loaded.DeferredAddBody,loaded.DeferredAddOffset))return false;
            candidate.AcceptedReceipt=rows[0];candidate.NativePhase=1;
            candidate.NativeAcceptedBody=(byte[])loaded.DeferredAddBody.Clone();candidate.NativeAcceptedOffset=loaded.DeferredAddOffset;
            candidate.NativePublication=new NativeStationPublication(candidate,candidate.NativeAcceptedBody,candidate.NativeAcceptedOffset,1);
            host.Add(original.Operation,candidate);attachment=new LoadedNativeContribution(candidate);candidate=null;return true;
        }
        catch{return false;}
        finally{candidate?.Dispose();allocation?.Dispose();ownedRecipient?.Dispose();}
    }
}
