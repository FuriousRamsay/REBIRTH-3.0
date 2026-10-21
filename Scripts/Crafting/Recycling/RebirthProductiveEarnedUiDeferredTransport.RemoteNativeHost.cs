using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostContribution
    {
        internal int NativePhase;
        internal XElement NativePhysical,NativeMembership;
        internal byte[] NativeAcceptedBody;
        internal int NativeAcceptedOffset;
        internal Guid NativeDispatch;
        internal Guid SupersededBy;
        internal bool RoutingSettled;
    }
    internal sealed class NativeStationPublication:IDisposable
    {
        internal readonly HostContribution Source;
        internal readonly byte[] Body;internal readonly int Offset,Phase;
        private RebirthRemoteResourceNativePublication.Watch watch;
        private byte[] saved;private string path;
        internal NativeStationPublication(HostContribution source,byte[] body,int offset,int phase)
        {Source=source;Body=body;Offset=offset;Phase=phase;}
        private bool CurrentBody()=>Source.SourceCurrentWithoutViewer()&&ReferenceEquals(Source.NativePublication,this)&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Source.Station,Body,Offset);
        internal bool Begin()
        {
            if(!CurrentBody()||!RebirthRemoteResourceNativeBinding.TryResolve(Source.World,
                RemoteResourceIdentity.Workstation(Source.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,
                Source.NativeOriginal.CustodyBirth.ToString("N"),out var binding))return false;
            watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,CurrentBody,Source.Memory);return watch!=null;
        }
        internal bool TrySeal()
        {
            if(!Source.SourceCurrentWithoutViewer()||Phase!=3)return false;
            if(saved!=null)return RebirthProductiveEarnedArtifact.FileMatches(path,saved);
            if(watch==null||!CurrentBody()||!watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string digest)||!CurrentBody())return false;
            path=Source.NativeOriginal.Path+".station-saved";
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Source.Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(Source.World)),new XElement("before",
                new XElement("remoteNativeStationSaved",new XAttribute("version",1),new XAttribute("nonce",Source.Nonce.ToString("N")),
                    new XAttribute("birth",Source.Birth.ToString("N")),new XAttribute("source",Source.SourceHash),
                    new XAttribute("native",HydrationHash(Body)),new XAttribute("phase",Phase),new XAttribute("nativePath",native.Path),
                    new XAttribute("payload",native.PayloadDigest),new XAttribute("offset",offset),new XAttribute("length",length),new XAttribute("tile",digest))));
            if(!RebirthWorkstationCompletionPublication.Write(path,record))return false;
            saved=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
            return saved!=null&&RebirthProductiveEarnedArtifact.FileMatches(path,saved);
        }
        public void Dispose(){watch?.Dispose();watch=null;saved=null;}
    }
    private static XElement NativeMembership(XElement value)
    {
        var copy=new XElement(value);copy.Attribute("phase")?.Remove();
        copy.Elements("physicalDelivery").Remove();copy.Elements("activeBefore").Remove();copy.Elements("actualCachedSpaceTransfer").Remove();return copy;
    }
    internal static bool TrySealRemoteNativeStation(RebirthWorkstationCompletionPipeline.NativeHostOriginal source)
        =>ThreadManager.IsMainThread()&&source!=null&&(TrySealSameOwnerNativeStation(source)||host.TryGetValue(source.Operation,out var entry)&&
            ReferenceEquals(entry.NativeOriginal,source)&&entry.NativePhase==3&&entry.TrySealStation());
    // Read-only native state admission, backed by an authenticated original
    // producer. Parsed XML alone cannot issue the post-Process event below.
    internal static bool TryAdmitRemoteNativeEvent(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,
        XElement contribution,byte[] proposed,int elapsedOffset,out HostContribution entry)
    {
        entry=null;HostContribution candidate=null;IDisposable allocation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||source?.CurrentViewer()!=true||!source.BeforeSaved||
                !source.AdmissionCurrent()||contribution?.Name!="deferredNativeContribution"||(int?)contribution.Attribute("version")!=1||
                (string)contribution.Attribute("operation")!=source.Operation.ToString("N")||
                (string)contribution.Attribute("nonce")!=source.Nonce.ToString("N")||(string)contribution.Attribute("birth")!=source.Birth.ToString("N")||
                (string)contribution.Attribute("original")!=source.SourceHash||
                (string)contribution.Attribute("source")!=RebirthProductiveEarnedUiQueueOwner.Export(source.EarlierOwner)||
                (int?)contribution.Attribute("contribution")!=source.Recipe.count||
                !RebirthProductiveEarnedNativeBody.TryRead(proposed,elapsedOffset,out var after,out _))return false;
            int phase=(int?)contribution.Attribute("phase")??0;if(phase<1||phase>4)return false;
            var membership=NativeMembership(contribution);
            bool existing=host.TryGetValue(source.Operation,out var retained);
            if(existing&&(!ReferenceEquals(retained.NativeOriginal,source)||!retained.SourceCurrentWithoutViewer()||
                !XNode.DeepEquals(retained.NativeMembership,membership)))return false;
            byte[] prior=existing?retained.NativeAcceptedBody:source.NativeBefore;
            int priorOffset=existing?retained.NativeAcceptedOffset:source.BeforeOffset;
            if(prior==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,prior,priorOffset))return false;
            XElement physical=null;
            if(phase==1)
            {
                if(existing||!TryDescribeRemoteNativeAddBody(source.Recipe,source.EarlierOwner.StartingEntityId,
                    source.NativeBefore,source.BeforeOffset,contribution,out var expected,out int expectedOffset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(expected,expectedOffset,proposed,elapsedOffset))return false;
            }
            else
            {
                if(!existing||retained.NativePhase==0)return false;
                if(phase==4)
                {if(retained.NativePhase!=1||!ValidateRemoteNativeCachedSpace(source,contribution,prior,priorOffset,proposed,elapsedOffset,out physical))return false;}
                else
                {
                    if(phase==2&&retained.NativePhase!=1||phase==3&&retained.NativePhase!=2||
                        !ValidateRemoteNativePhysical(source,contribution,prior,priorOffset,proposed,elapsedOffset,phase,out physical)||
                        phase==3&&!XNode.DeepEquals(retained.NativePhysical,physical))return false;
                }
            }
            if(existing){entry=retained;return true;}
            long size=MeasurePublicationElement(contribution);
            if(size>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||host.Count>=4||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*size+16L*proposed.Length+65536),out allocation))return false;
            if(!RebirthProductiveEarnedUiQueueOwner.TryResolvePublishedPurpose(source.World,source.Station,(string)contribution.Attribute("recipient"),out var recipient))return false;
            var recipientRows=after.Completions.Where(row=>Row(row).SequenceEqual(ReadBytes(contribution,"aggregate"))).ToArray();
            if(recipientRows.Length!=1||recipientRows[0].CrafterEntityID!=recipient.StartingEntityId)return false;
            candidate=new HostContribution{NativeOriginal=source,World=source.World,Station=source.Station,Operation=source.Operation,Nonce=source.Nonce,Birth=source.Birth,
                SourceHash=source.SourceHash,Recipient=recipient,RecipientOwner=recipient.OriginalOwnerKey,RecipientCreation=recipient.Creation,
                RecipientEntityId=recipient.StartingEntityId,ImmutableMembership=new XElement(contribution),NativeMembership=membership,
                BeforeComplete=ReadBytes(contribution,"beforeComplete"),AfterComplete=ReadBytes(contribution,"afterComplete"),
                Aggregate=ReadBytes(contribution,"aggregate"),Allocation=allocation};allocation=null;
            var members=contribution.Element("members")?.Elements("member").ToArray();
            if(members==null||members.Length<1||members.Length>4||members.Select(node=>ReadGuid(node,"operation")).Distinct().Count()!=members.Length)return false;
            long total=0;
            foreach(var member in members)
            {
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(member,"nativeAddArgument"),out var memberArgument)||
                    memberArgument.CraftedItemStack.itemValue.GetItemId()!=recipientRows[0].CraftedItemStack.itemValue.GetItemId()||
                    memberArgument.ItemScrapped!=recipientRows[0].ItemScrapped)return false;
                total=checked(total+(int)member.Attribute("contribution"));
                if(ReadGuid(member,"operation")==source.Operation&&
                    (!ReadBytes(member,"nativeAddArgument").SequenceEqual(ReadBytes(contribution,"nativeAddArgument"))||
                     !ReadBytes(member,"beforeComplete").SequenceEqual(candidate.BeforeComplete)||!ReadBytes(member,"afterComplete").SequenceEqual(candidate.AfterComplete)))return false;
                if(!ValidateRemoteNativeMember(candidate,member,out var lease))return false;candidate.Retentions.Add(lease);
            }
            if(total!=recipientRows[0].CraftedItemStack.count)return false;
            if(!members.Any(member=>ReadGuid(member,"operation")==source.Operation&&(string)member.Attribute("kind")=="native"))return false;
            var recipientLease=recipient.RetainPublishedSource();if(recipientLease==null)return false;candidate.Retentions.Add(recipientLease);
            var primary=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("world",source.WorldGuid),new XElement("before",new XElement(contribution)));
            if(!RebirthWorkstationCompletionPublication.Write(source.Path+".contribution",primary))return false;
            host.Add(source.Operation,candidate);entry=candidate;candidate=null;return true;
        }
        catch{return false;}
        finally{candidate?.Dispose();allocation?.Dispose();}
    }
    // Causal native Add description only. A caller still needs its own actual
    // authenticated event or native loaded source; these bytes grant no effect.
    internal static bool TryDescribeRemoteNativeAddBody(Recipe recipe,int startingOwner,byte[] original,int offset,
        XElement contribution,out byte[] expected,out int expectedOffset)
    {
        expected=null;expectedOffset=0;
        try
        {
            if(!RebirthProductiveEarnedNativeBody.TryRead(original,offset,out var before,out _)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(contribution,"nativeAddArgument"),out var argument)||
                !NativeArgumentMatches(argument,recipe,startingOwner)||
                !Complete(before.Completions).SequenceEqual(ReadBytes(contribution,"beforeComplete")))return false;
            var rows=Copy(before.Completions);
            var old=rows.FirstOrDefault(row=>row.CraftedItemStack.itemValue.GetItemId()==argument.CraftedItemStack.itemValue.GetItemId()&&row.ItemScrapped==argument.ItemScrapped);
            CraftCompleteData aggregate;
            if(old!=null){old.CraftedItemStack.count=checked(old.CraftedItemStack.count+argument.CraftedItemStack.count);aggregate=old;}
            else{aggregate=argument;rows=rows.Concat(new[]{argument}).ToArray();}
            if(!Complete(rows).SequenceEqual(ReadBytes(contribution,"afterComplete"))||
                !Row(aggregate).SequenceEqual(ReadBytes(contribution,"aggregate")))return false;
            before.Completions=rows;
            expected=RebirthProductiveEarnedNativeBody.Encode(before,0,out expectedOffset);return true;
        }
        catch{expected=null;expectedOffset=0;return false;}
    }
    private static bool ValidateRemoteNativeMember(HostContribution candidate,XElement member,out IDisposable lease)
    {
        lease=null;Guid operation=ReadGuid(member,"operation");
        if(!TryResolveLoadedContributionPurpose(candidate,(string)member.Attribute("source"),out var creator)||
            !RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(member,"nativeAddArgument"),out var argument))return false;
        if((string)member.Attribute("kind")=="native")
        {
            if(!RebirthWorkstationCompletionPipeline.TryFindNativeHostOriginal(operation,candidate.World,candidate.Station,out var original))
            {
                if(!RebirthWorkstationCompletionPipeline.TryFindRetainedNativeAddOriginal(operation,candidate.World,candidate.Station,out var local))
                {
                    if(!LoadedNativeMember.TryCapture(candidate,member,creator,out var loaded))return false;
                    candidate.LoadedNativeMembers.Add(loaded);candidate.Retentions.Add(loaded);
                    lease=loaded.Retain();return lease!=null;
                }
                if(!ReferenceEquals(local.EarlierOwner,creator)||!XNode.DeepEquals(member.Element("nativeOriginal")?.Elements().SingleOrDefault(),local.Before)||
                    !local.TryCopyConfirmedDelivery(out var delivered)||!RebirthProductiveEarnedUiRemote.TryReadDeferredPrimary(delivered,out var primary)||
                    !NativeArgumentMatches(argument,local.Recipe,creator.StartingEntityId)||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(primary,argument.CraftedItemStack)||
                    !RebirthProductiveEarnedNativeBody.TryRead(Convert.FromBase64String((string)local.Before.Element("nativeBefore")),
                        (int)local.Before.Attribute("nativeElapsedOffset"),out var localBody,out _)||
                    !MemberDelta(member,localBody.Completions,local.Recipe,creator.StartingEntityId,primary))return false;
                lease=local.RetainForCompletion();return lease!=null;
            }
            if(
                !ReferenceEquals(original.EarlierOwner,creator)||!original.BeforeSaved||
                !XNode.DeepEquals(member.Element("nativeOriginal")?.Elements().SingleOrDefault(),original.Before)||
                !RebirthProductiveEarnedNativeBody.TryRead(original.NativeBefore,original.BeforeOffset,out var before,out _)||
                !NativeArgumentMatches(argument,original.Recipe,creator.StartingEntityId)||
                !MemberDelta(member,before.Completions,original.Recipe,creator.StartingEntityId,argument.CraftedItemStack))return false;
            if(operation!=candidate.Operation&&(!host.TryGetValue(operation,out var prior)||!prior.TrySealStation()))return false;
            lease=original.Retain();return lease!=null;
        }
        if((string)member.Attribute("kind")!="uio")return false;
        if(!RebirthProductiveEarnedUiRemote.TryFindRegisteredContribution(operation,out var ui))
        {
            if(!LoadedUiMember.TryCapture(candidate,member,creator,out var loaded))return false;
            candidate.LoadedUiMembers.Add(loaded);candidate.Retentions.Add(loaded);lease=loaded.Retain();return lease!=null;
        }
        if(!ReferenceEquals(ui.EarlierOwner,creator)||!ReferenceEquals(ui.Station,candidate.Station)||ui.Hash!=(string)member.Attribute("uio")||
            ui.Nonce.ToString("N")!=(string)member.Attribute("nonce")||!RebirthProductiveEarnedUiRemote.TryGetStationPublication(ui,out var published)||!published.Current()||
            !RebirthProductiveEarnedNativeBody.TryRead(ui.Frame.Earned.AfterNative,ui.Frame.Earned.AfterElapsedOffset,out var body,out _)||
            !NativeArgumentMatches(argument,body.Queue[ui.Frame.Earned.RowIndex].Recipe,creator.StartingEntityId)||
            !MemberDelta(member,body.Completions,body.Queue[ui.Frame.Earned.RowIndex].Recipe,creator.StartingEntityId,argument.CraftedItemStack))return false;
        lease=creator.RetainPublishedSource();return lease!=null;
    }
    private static bool NativeArgumentMatches(CraftCompleteData argument,Recipe recipe,int owner)=>recipe!=null&&argument!=null&&
        argument.CrafterEntityID==owner&&argument.RecipeName==recipe.GetName()&&
        RebirthRecipeOutputIntegration.MatchesNativeOutputScrappedName(recipe,argument.ItemScrapped)&&
        argument.CraftExpGain==recipe.craftExpGain&&argument.RecipeUsedCount==1&&argument.CraftedItemStack?.itemValue!=null&&
        argument.CraftedItemStack.itemValue.type==recipe.itemValueType&&argument.CraftedItemStack.count==recipe.count;
    internal static void ObserveRemoteNativeEventAccepted(RebirthWorkstationCompletionPipeline.RemoteNativeEvent actual)
    {
        if(actual?.IsActualAcceptedRead()!=true)return;
        if(actual.SameOwnerEffect!=null){SameOwnerStationPublication.Observe(actual);return;}
        var source=actual.Source;var entry=actual.AcceptedContribution;
        if(entry==null||!ReferenceEquals(entry.NativeOriginal,source)||!host.TryGetValue(source.Operation,out var retained)||!ReferenceEquals(retained,entry)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,actual.Body,actual.Offset))return;
        int phase=(int)actual.Phase;
        if(phase==1&&entry.NativePhase!=0||phase==2&&entry.NativePhase!=1||phase==3&&entry.NativePhase!=2||phase==4&&entry.NativePhase!=1)return;
        var rows=source.Station.CraftCompleteList?.Where(row=>Row(row).SequenceEqual(entry.Aggregate)).ToArray();if(rows?.Length!=1)return;
        long measured=MeasurePublicationElement(actual.Contribution);
        if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(entry.Allocation,checked(32*measured+32L*actual.Body.Length+65536)))return;
        var body=(byte[])actual.Body.Clone();
        entry.AcceptedReceipt=rows[0];entry.NativeAcceptedBody=body;entry.NativeAcceptedOffset=actual.Offset;entry.NativeDispatch=actual.Dispatch;
        entry.NativePhysical=phase==2?new XElement(actual.Contribution.Element("physicalDelivery").Elements().Single()):entry.NativePhysical;
        entry.NativePhase=phase==4?1:phase;
        entry.NativePublication?.Dispose();entry.NativePublication=new NativeStationPublication(entry,body,actual.Offset,phase);
        entry.NativePublication.Begin();
        if(phase==1)
        {
            // The actual Add has coalesced these earlier contributions into the
            // new aggregate. Retain their originals for group settlement, while
            // routing only the newest complete aggregate to a future viewer.
            SupersedeAggregateMembers(entry);
        }
    }
    private static void SupersedeAggregateMembers(HostContribution entry)
    {
        foreach(var member in entry.ImmutableMembership.Element("members").Elements("member"))
        {
            Guid operation=ReadGuid(member,"operation");
            if(operation!=entry.Operation&&host.TryGetValue(operation,out var earlier)&&
                ReferenceEquals(earlier.World,entry.World)&&ReferenceEquals(earlier.Station,entry.Station))earlier.SupersededBy=entry.Operation;
        }
    }
}
