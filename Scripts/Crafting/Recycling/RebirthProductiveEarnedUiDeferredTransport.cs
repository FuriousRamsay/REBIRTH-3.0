using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Purpose-specific deferred native owner lineage. Parsed membership alone never
// grants progression, native setters, receipt removal or current-body reentry.
internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostContribution:IDisposable
    {
        internal RebirthProductiveEarnedUiRemote.Registration Original;
        internal RebirthWorkstationCompletionPipeline.NativeHostOriginal NativeOriginal;
        internal RebirthWorkstationCompletionPipeline.CachedHostOriginal CachedOriginal;
        internal AuthorityNativePublication AuthorityOriginal;
        internal NativeStationPublication NativePublication;
        internal RebirthProductiveEarnedObservationBudget Memory=>Original?.Memory??NativeOriginal?.Memory??CachedOriginal?.Memory??AuthorityOriginal?.Memory;
        internal byte[] PublishedBody=>Original?.TerminalBody??NativePublication?.Body??CachedOriginal?.PublishedBody??AuthorityOriginal?.Body;
        internal int PublishedOffset=>Original!=null?Original.TerminalOffset:NativePublication?.Offset??CachedOriginal?.PublishedOffset??AuthorityOriginal?.Offset??-1;
        internal Guid CustodyBirth=>NativeOriginal?.CustodyBirth??CachedOriginal?.CustodyBirth??AuthorityOriginal?.CustodyBirth??Birth;
        internal World World;internal TileEntityWorkstation Station;
        internal Guid Operation,Nonce,Birth;internal string SourceHash,RecipientOwner,RecipientCreation;
        internal int RecipientEntityId;internal XElement ImmutableMembership;
        internal byte[] BeforeComplete,AfterComplete,Aggregate;
        internal CraftCompleteData AcceptedReceipt;
        internal RebirthProductiveEarnedUiQueueOwner.Witness Recipient;
        internal RebirthProductiveEarnedUiRemote.TypedStationPublication StationPublication;
        internal readonly List<IDisposable> Retentions=new List<IDisposable>();
        internal IDisposable Allocation;internal bool Closed;
        internal bool SourceCurrentWithoutViewer()=>!Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&Recipient.SourceCurrent()&&
            (AuthorityLoadedOriginal!=null?AuthorityLoadedOriginal.IdentityCurrent():AuthorityOriginal!=null?AuthorityOriginal.SourceCurrent():CachedOriginal!=null?CachedOriginal.SourceCurrentWithoutRows():NativeOriginal!=null?NativeOriginal.SourceCurrent():StationPublication!=null?StationPublication.Current():Original?.Current()==true);
        internal bool TrySealStation()
        {
            if(!SourceCurrentWithoutViewer())return false;
            if(AuthorityLoadedOriginal!=null)return AuthorityLoadedOriginal.IdentityCurrent();
            if(AuthorityOriginal!=null)return AuthorityOriginal.TrySeal();
            if(CachedOriginal!=null)return CachedOriginal.SourceCurrentWithoutRows();
            if(NativeOriginal!=null)
            {
                if(NativePublication?.TrySeal()!=true)return false;
                return NativeOriginal.TryReleaseStationWorkHold(NativePublication);
            }
            if(StationPublication!=null)return StationPublication.Current();
            if(!RebirthProductiveEarnedUiRemote.TryGetStationPublication(Original,out var published)||!published.Current())return false;
            StationPublication=published;return true;
        }
        public void Dispose(){if(Closed)return;Closed=true;if(host.TryGetValue(Operation,out var retained)&&ReferenceEquals(retained,this))host.Remove(Operation);foreach(var retention in Retentions)retention.Dispose();Retentions.Clear();
            BeforeComplete=AfterComplete=Aggregate=null;AcceptedReceipt=null;ImmutableMembership=null;LoadedUiMembers.Clear();LoadedNativeMembers.Clear();LoadedOwnerPurposes.Clear();LoadedOwnerSource=null;NativePublication?.Dispose();NativePublication=null;AuthorityOriginal?.Dispose();AuthorityOriginal=null;Allocation?.Dispose();Allocation=null;}
    }
    private static readonly Dictionary<Guid,HostContribution> host=new Dictionary<Guid,HostContribution>();
    internal static void ObserveHostReceiptAccepted(RebirthProductiveEarnedUiRemote.Registration original)
    {
        try
        {
            if(!RebirthProductiveEarnedUiRemote.IsAcceptingHostReceipt(original)||original?.Deferred is not HostContribution source||
                !host.TryGetValue(source.Operation,out var retained)||!ReferenceEquals(source,retained)||!source.SourceCurrentWithoutViewer())return;
            using(var callback=original.Memory.TryEnterCallbacks())
            {
                if(callback==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,original.TerminalBody,original.TerminalOffset))return;
                var rows=source.Station.CraftCompleteList?.Where(value=>Row(value).SequenceEqual(source.Aggregate)).ToArray();
                if(rows?.Length!=1||rows[0].CrafterEntityID!=source.RecipientEntityId)return;
                source.AcceptedReceipt=rows[0];
                SupersedeAggregateMembers(source);
                ObserveHostLocalReceiptAccepted(source,rows[0]);
            }
        }
        catch{/* An actual accepted callback without exact evidence remains unknown. */}
    }
    internal static bool TryValidateTerminal(RebirthProductiveEarnedUiRemote.Registration original,XElement contribution,
        CraftCompleteData[] before,CraftCompleteData[] after,ItemStack primary,out HostContribution entry)
    {
        entry=null;HostContribution candidate=null;
        try
        {
            if(original?.Current()!=true||contribution?.Name!="deferredContribution"||before==null||after==null||primary?.itemValue==null||
                (int)contribution.Attribute("version")!=1||(string)contribution.Attribute("operation")!=original.Frame.Operation.ToString("N")||
                (string)contribution.Attribute("uio")!=original.Hash||(string)contribution.Attribute("nonce")!=original.Nonce.ToString("N")||
                (string)contribution.Attribute("birth")!=original.Frame.Birth.ToString("N")||
                (string)contribution.Attribute("source")!=original.Frame.OwnerPurpose||
                !RebirthProductiveEarnedUiQueueOwner.TryResolvePublishedPurpose(original.Actor.world,original.Station,(string)contribution.Attribute("recipient"),out var recipient))return false;
            var members=contribution.Element("members")?.Elements("member").ToArray();
            if(members==null||members.Length<1||members.Length>4||members.Select(value=>(string)value.Attribute("operation")).Distinct().Count()!=members.Length)return false;
            if(!Complete(before).SequenceEqual(ReadBytes(contribution,"beforeComplete"))||!Complete(after).SequenceEqual(ReadBytes(contribution,"afterComplete")))return false;
            var own=members.SingleOrDefault(value=>(string)value.Attribute("operation")==original.Frame.Operation.ToString("N"));
            if(own==null||(string)own.Attribute("kind")!="uio"||!ReadBytes(own,"beforeComplete").SequenceEqual(Complete(before))||
                !ReadBytes(own,"afterComplete").SequenceEqual(Complete(after)))return false;
            // Exact installed AddCraftComplete grammar, preserving native
            // coalescing by item ID+scrap and the FIRST row's recipient/XP/name.
            var expected=Copy(before);var old=expected.FirstOrDefault(value=>value.CraftedItemStack.itemValue.GetItemId()==primary.itemValue.GetItemId()&&value.ItemScrapped==string.Empty);
            var frame=original.Frame.Earned;
            if(!RebirthProductiveEarnedNativeBody.TryRead(frame.AfterNative,frame.AfterElapsedOffset,out var sourceBody,out _)||
                frame.RowIndex<0||frame.RowIndex>=sourceBody.Queue.Length)return false;
            var row=sourceBody.Queue[frame.RowIndex];
            if(row?.Recipe==null||row.StartingEntityId!=original.EarlierOwner.StartingEntityId||primary.count!=row.Recipe.count||
                (int)contribution.Attribute("contribution")!=row.Recipe.count)return false;
            CraftCompleteData aggregate;
            if(old!=null){old.CraftedItemStack.count=checked(old.CraftedItemStack.count+row.Recipe.count);aggregate=old;}
            else{aggregate=new CraftCompleteData(row.StartingEntityId,primary.Clone(),row.Recipe.GetName(),string.Empty,row.Recipe.craftExpGain,1);expected=expected.Concat(new[]{aggregate}).ToArray();}
            if(!Complete(expected).SequenceEqual(Complete(after))||!Row(aggregate).SequenceEqual(ReadBytes(contribution,"aggregate"))||
                aggregate.CrafterEntityID!=recipient.StartingEntityId)return false;
            long size=Encoding.UTF8.GetByteCount(contribution.ToString(SaveOptions.DisableFormatting));
            if(size>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12*size+65536),out var allocation))return false;
            candidate=new HostContribution{Original=original,World=original.Actor.world,Station=original.Station,Operation=original.Frame.Operation,Nonce=original.Nonce,
                Birth=original.Frame.Birth,SourceHash=original.Hash,Recipient=recipient,RecipientOwner=recipient.OriginalOwnerKey,RecipientCreation=recipient.Creation,
                RecipientEntityId=recipient.StartingEntityId,ImmutableMembership=new XElement(contribution),BeforeComplete=Complete(before),AfterComplete=Complete(after),Aggregate=Row(aggregate),Allocation=allocation};
            foreach(var member in members)
            {
                if(!Guid.TryParseExact((string)member.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                    !RebirthProductiveEarnedUiQueueOwner.TryResolvePublishedPurpose(candidate.World,candidate.Station,(string)member.Attribute("source"),out var creator)||
                    (int)member.Attribute("contribution")<1)return false;
                IDisposable retention=null;
                if((string)member.Attribute("kind")=="uio")
                {
                    if(!RebirthProductiveEarnedUiRemote.TryFindRegisteredContribution(operation,out var registered)||
                        !ReferenceEquals(registered.EarlierOwner,creator)||registered.Hash!=(string)member.Attribute("uio")||
                        registered.Nonce.ToString("N")!=(string)member.Attribute("nonce")||
                        !ReferenceEquals(registered.Station,candidate.Station))return false;
                    if(operation!=candidate.Operation&&(!RebirthProductiveEarnedUiRemote.TryGetStationPublication(registered,out var prior)||!prior.Current()))return false;
                    ItemStack memberPrimary=primary;
                    if(operation!=candidate.Operation&&!RebirthProductiveEarnedUiRemote.TryReadDeferredPrimary(registered.Terminal?.Delivery,out memberPrimary))return false;
                    if(!RebirthProductiveEarnedNativeBody.TryRead(registered.Frame.Earned.AfterNative,registered.Frame.Earned.AfterElapsedOffset,out var memberBody,out _)||
                        registered.Frame.Earned.RowIndex<0||registered.Frame.Earned.RowIndex>=memberBody.Queue.Length||
                        !MemberDelta(member,memberBody.Completions,memberBody.Queue[registered.Frame.Earned.RowIndex].Recipe,creator.StartingEntityId,memberPrimary))return false;
                    retention=creator.RetainPublishedSource();
                }
                else if((string)member.Attribute("kind")=="native")
                {
                    if(RebirthWorkstationCompletionPipeline.TryFindNativeHostOriginal(operation,candidate.World,candidate.Station,out var remoteNative))
                    {
                        if(!ReferenceEquals(remoteNative.EarlierOwner,creator)||!remoteNative.BeforeSaved||
                            !XNode.DeepEquals(member.Element("nativeOriginal")?.Elements().SingleOrDefault(),remoteNative.Before)||
                            !host.TryGetValue(operation,out var priorNative)||!priorNative.TrySealStation()||
                            !RebirthRemoteResourcePlacedStationNativeReader.TryReadCompletionArgument(ReadBytes(member,"nativeAddArgument"),out var argument)||
                            !NativeArgumentMatches(argument,remoteNative.Recipe,creator.StartingEntityId)||
                            !RebirthProductiveEarnedNativeBody.TryRead(remoteNative.NativeBefore,remoteNative.BeforeOffset,out var remoteBody,out _)||
                            !MemberDelta(member,remoteBody.Completions,remoteNative.Recipe,creator.StartingEntityId,argument.CraftedItemStack))return false;
                        retention=remoteNative.Retain();
                    }
                    else
                    {
                    if(!RebirthWorkstationCompletionPipeline.TryFindRetainedNativeAddOriginal(operation,candidate.World,candidate.Station,out var native)||
                        !ReferenceEquals(native.EarlierOwner,creator)||!XNode.DeepEquals(member.Element("nativeOriginal")?.Elements().SingleOrDefault(),native.Before)||
                        !native.TryCopyConfirmedDelivery(out var nativeDelivery)||
                        !RebirthProductiveEarnedUiRemote.TryReadDeferredPrimary(nativeDelivery,out var nativePrimary))return false;
                    var nativeBefore=native.Before;
                    byte[] nativeImage=Convert.FromBase64String((string)nativeBefore.Element("nativeBefore"));
                    if(nativeImage.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||
                        !RebirthProductiveEarnedNativeBody.TryRead(nativeImage,(int)nativeBefore.Attribute("nativeElapsedOffset"),out var nativeBody,out _)||
                        !MemberDelta(member,nativeBody.Completions,native.Recipe,creator.StartingEntityId,nativePrimary))return false;
                    retention=native.RetainForCompletion();
                    }
                }
                else return false;
                if(retention==null)return false;candidate.Retentions.Add(retention);
            }
            var recipientHold=recipient.RetainPublishedSource();if(recipientHold==null)return false;candidate.Retentions.Add(recipientHold);
            if(host.TryGetValue(candidate.Operation,out var retained))
            {if(!retained.SourceCurrentWithoutViewer()||!XNode.DeepEquals(retained.ImmutableMembership,contribution))return false;entry=retained;return true;}
            if(host.Count>=4)return false;
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"uiDeferred",candidate.Operation.ToString("N")+".xml");
            var saved=new XElement("workstationCompletionIntent",new XAttribute("operation",candidate.Operation.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(candidate.World)),
                new XElement("before",new XElement(contribution)));
            if(!RebirthWorkstationCompletionPublication.Write(path,saved))return false;
            host.Add(candidate.Operation,candidate);entry=candidate;candidate=null;return true;
        }
        catch{return false;}
        finally{candidate?.Dispose();}
    }
    private static byte[] ReadBytes(XElement image,string name)
    {var text=image.Element(name);if(text==null||text.HasElements||text.Value.Length>349528)throw new InvalidDataException();var bytes=Convert.FromBase64String(text.Value);
     if(bytes.Length>256*1024||Convert.ToBase64String(bytes)!=text.Value)throw new InvalidDataException();return bytes;}
    private static bool MemberDelta(XElement member,CraftCompleteData[] before,Recipe recipe,int owner,ItemStack primary)
    {
        if(recipe==null||primary?.itemValue==null||primary.count!=recipe.count||primary.itemValue.type!=recipe.itemValueType||
            (int?)member.Attribute("contribution")!=recipe.count||!ReadBytes(member,"beforeComplete").SequenceEqual(Complete(before)))return false;
        string scrapped=recipe.IsScrap&&RebirthRecipeOutputIntegration.HasNativeScrapOutputs(recipe)?recipe.ingredients[0].itemValue.ItemClass.GetItemName():string.Empty;
        if(!RebirthRecipeOutputIntegration.MatchesNativeOutputScrappedName(recipe,scrapped))return false;
        var expected=Copy(before);
        var old=expected.FirstOrDefault(value=>value.CraftedItemStack.itemValue.GetItemId()==primary.itemValue.GetItemId()&&value.ItemScrapped==scrapped);
        if(old!=null)old.CraftedItemStack.count=checked(old.CraftedItemStack.count+recipe.count);
        else expected=expected.Concat(new[]{new CraftCompleteData(owner,primary.Clone(),recipe.GetName(),scrapped,recipe.craftExpGain,1)}).ToArray();
        return Complete(expected).SequenceEqual(ReadBytes(member,"afterComplete"));
    }
    private static byte[] Complete(CraftCompleteData[] rows)
    {if(rows.Length>64)throw new InvalidDataException();return Native(writer=>{writer.Write((short)rows.Length);foreach(var row in rows)row.Write(writer);});}
    private static byte[] Row(CraftCompleteData row)=>Native(row.Write);
    private static byte[] Native(Action<PooledBinaryWriter> write)
    {using(var stream=new Limited())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);write(writer);writer.Flush();return stream.ToArray();}}
    private static CraftCompleteData[] Copy(CraftCompleteData[] rows)=>rows.Select(value=>new CraftCompleteData(value.CrafterEntityID,value.CraftedItemStack.Clone(),value.RecipeName,value.ItemScrapped,value.CraftExpGain,value.RecipeUsedCount)).ToArray();
    private sealed class Limited:MemoryStream
    {public override void Write(byte[] buffer,int offset,int count){if(count<0||Position>256*1024-count)throw new InvalidDataException();base.Write(buffer,offset,count);}
     public override void WriteByte(byte value){if(Position>=256*1024)throw new InvalidDataException();base.WriteByte(value);}}
}
