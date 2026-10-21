using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Actual authority-local Add, not a remote prepare nonce or UIO original.
    internal sealed class AuthorityNativePublication:IDisposable
    {
        internal readonly RebirthWorkstationCompletionPipeline.NativeAddOriginal NativeOriginal;
        internal readonly World World;internal readonly TileEntityWorkstation Station;internal readonly Guid Operation,CustodyBirth;
        internal RebirthProductiveEarnedObservationBudget Memory {get;private set;}
        internal byte[] Body {get;private set;}internal int Offset {get;private set;}
        private HostContribution contribution;private RebirthProductiveEarnedUiQueueOwner.DeferredReceipt receipt;
        private IDisposable retention,receiptRetention,bodyRetention;private RebirthRemoteResourceNativePublication.Watch watch;
        private byte[] initialAggregate;private CraftCompleteData initialReceipt;
        private XElement original,physical;private byte[] originalImage,saved;private readonly string path;
        private bool closed,actualSync,nativeSavedObserved;
        internal bool HasObservedNativeSnapshot=>nativeSavedObserved;
        private bool membersRetained;private readonly HashSet<Guid> retainedMembers=new HashSet<Guid>();
        internal bool Saved=>saved!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path+".saved",saved);
        private AuthorityNativePublication(RebirthWorkstationCompletionPipeline.NativeAddOriginal source,string path)
        {NativeOriginal=source;World=source.World;Station=source.Station;Operation=source.Operation;CustodyBirth=source.EarlierOwner.CurrentCustodyBirth;this.path=path;}
        private bool IdentityCurrent()=>!closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&
            NativeOriginal.SourceCurrentWithoutRow()&&NativeOriginal.AuthoritySavedBeforeCurrent()&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==CustodyBirth.ToString("N");
        internal bool SourceCurrent()=>IdentityCurrent()&&originalImage!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,originalImage);
        private bool CurrentBody()=>SourceCurrent()&&actualSync&&Body!=null&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Body,Offset);
        internal bool TrySeal()
        {
            if(!TryPublishOriginal())return false;if(Saved)return true;if(Memory==null||!CurrentBody())return false;
            if(watch==null)
            {
                if(!RebirthRemoteResourceNativeBinding.TryResolve(World,RemoteResourceIdentity.Workstation(Station.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,CustodyBirth.ToString("N"),out var binding))return false;
                watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,CurrentBody,Memory);if(watch==null)return false;
            }
            if(!watch.TryReadPublishedTileDescriptor(out var native,out long at,out int length,out string digest)||!CurrentBody())return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(original)),
                new XElement("authorityNativePhysicalSaved",new XAttribute("source",NativeOriginal.AuthoritySourceHash),new XAttribute("custodyBirth",CustodyBirth.ToString("N")),
                    new XAttribute("native",HydrationHash(Body)),new XAttribute("nativePath",native.Path),new XAttribute("payload",native.PayloadDigest),
                    new XAttribute("offset",at),new XAttribute("length",length),new XAttribute("tile",digest),new XElement("physicalDelivery",new XElement(physical)),
                    new XElement("nativeBody",new XAttribute("elapsedOffset",Offset),Convert.ToBase64String(Body))));
            if(!RebirthWorkstationCompletionPublication.Write(path+".saved",record))return false;
            var published=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".saved");
            if(published==null||!XNode.DeepEquals(RebirthWorkstationCompletionPublication.ReadPrimary(path+".saved"),record))return false;
            saved=published;if(!Saved)return false;nativeSavedObserved=true;
            watch.Dispose();watch=null;return true;
        }
        private bool AcceptSync(RebirthWorkstationCompletionPipeline.Frame frame,RebirthWorkstationCompletionPipeline.NativeSync sync)
        {
            if(actualSync)return TrySeal();
            if(!IdentityCurrent()||!ReferenceEquals(frame.NativeOriginal,NativeOriginal)||frame.Observation.World.IsRemote()||!frame.NativeAccepted||!frame.Saved||
                !ReferenceEquals(sync.Frame,frame)||!ReferenceEquals(sync.World,World)||!ReferenceEquals(sync.Station,Station)||
                !XNode.DeepEquals(frame.Intent,RebirthWorkstationCompletionPublication.ReadPrimary(frame.Path))||
                !NativeOriginal.TryCopyConfirmedDelivery(out var delivery)||receipt?.CurrentAggregateMembership()!=true||
                !ReferenceEquals(initialReceipt,receipt.Receipt)||!Row(receipt.Receipt).SequenceEqual(initialAggregate))return false;
            var account=NativeOriginal.CopyAuthorityAccountImage();if(account==null)return false;
            Memory=RebirthProductiveEarnedObservationBudget.TryOpenNativeCompletion(World,CustodyBirth,Operation,account);if(Memory==null)return false;
            try
            {
                using(var callback=Memory.TryEnterCallbacks())
                {
                    if(callback==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(Station,out var body,out int offset))return false;
                    if(!Memory.TryReserveAllocation(checked(8L*body.Length+8L*MeasurePublicationElement(delivery)+65536),out var bodyLease))return false;
                    bodyRetention=bodyLease;
                    Body=body;Offset=offset;physical=new XElement(delivery);actualSync=true;
                }
                return TrySeal();
            }
            finally{if(!actualSync){Memory.Dispose();Memory=null;}}
        }
        internal static void ObserveSync(RebirthWorkstationCompletionPipeline.Frame frame,RebirthWorkstationCompletionPipeline.NativeSync sync)
        {if(frame?.NativeOriginal!=null&&authorityNative.TryGetValue(frame.NativeOriginal.Operation,out var source))try{source.AcceptSync(frame,sync);}catch{/* Original remains held. */}}
        private bool TryPublishOriginal()
        {
            if(SourceCurrent())
            {
                if(host.TryGetValue(Operation,out var current))return ReferenceEquals(current,contribution);
                if(host.Count>=4)return false;host.Add(Operation,contribution);SupersedeAggregateMembers(contribution);return true;
            }
            if(!IdentityCurrent()||receipt==null||retention==null||receiptRetention==null)return false;IDisposable allocation=null;
            try
            {
                if(contribution==null)
                {
                if(!ReferenceEquals(initialReceipt,receipt.Receipt)||!Row(receipt.Receipt).SequenceEqual(initialAggregate)||
                    !RebirthProductiveEarnedUiQueueOwner.TryCopyAuthorityNativeLedger(receipt,out var ledger,out allocation))return false;
                var recipient=receipt.aggregate.Recipient;
                if(!recipient.SourceCurrent()||!RebirthProductiveEarnedUiQueueOwner.TryResolvePublishedPurpose(World,Station,(string)ledger.Attribute("recipient"),out var same)||!ReferenceEquals(same,recipient))return false;
                var held=recipient.RetainPublishedSource();if(held==null)return false;
                contribution=new HostContribution{AuthorityOriginal=this,World=World,Station=Station,Operation=Operation,Nonce=Guid.Empty,
                    Birth=NativeOriginal.EarlierOwner.Birth,SourceHash=NativeOriginal.AuthoritySourceHash,Recipient=recipient,RecipientOwner=recipient.OriginalOwnerKey,
                    RecipientCreation=recipient.Creation,RecipientEntityId=recipient.StartingEntityId,ImmutableMembership=new XElement(ledger),
                    BeforeComplete=(byte[])receipt.BeforeComplete.Clone(),AfterComplete=(byte[])receipt.AfterComplete.Clone(),Aggregate=receipt.AggregateImage,
                    AcceptedReceipt=receipt.Receipt,Allocation=allocation};allocation=null;contribution.Retentions.Add(held);original=new XElement(ledger);
                }
                if(!membersRetained)foreach(var member in receipt.AllContributions)
                {
                    if(retainedMembers.Contains(member.Operation))continue;
                    IDisposable memberHold=null;
                    if(member.Original!=null)
                    {if(!RebirthProductiveEarnedUiRemote.TryFindRegisteredContribution(member.Operation,out var registered)||!ReferenceEquals(registered.EarlierOwner,member.Source))return false;
                        memberHold=RebirthProductiveEarnedUiRemote.RetainDeferredGroup(registered);}
                    else memberHold=member.NativeOriginal?.RetainForCompletion();
                    if(memberHold==null)return false;contribution.Retentions.Add(memberHold);retainedMembers.Add(member.Operation);
                }
                membersRetained=true;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement(original)));
                if(!RebirthWorkstationCompletionPublication.Write(path,record))return false;
                var published=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                if(published==null||!XNode.DeepEquals(RebirthWorkstationCompletionPublication.ReadPrimary(path),record))return false;originalImage=published;
                if(host.TryGetValue(Operation,out var retained))return ReferenceEquals(retained,contribution);
                if(host.Count>=4)return false;host.Add(Operation,contribution);SupersedeAggregateMembers(contribution);return true;
            }
            finally{allocation?.Dispose();}
        }
        internal static void ObserveAdd(RebirthProductiveEarnedUiQueueOwner.ActualPendingAdd call)
        {
            try
            {
                var native=call?.NativeOriginal;var receipt=call?.PositiveToken;
                if(call?.Current()!=true||native==null||native.World.IsRemote()||receipt?.NativeOriginal!=native||!receipt.CurrentAggregateMembership()||
                    !native.AuthoritySavedBeforeCurrent()||authorityNative.ContainsKey(native.Operation))return;
                var source=new AuthorityNativePublication(native,Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"authorityNative",native.Operation.ToString("N")+".xml"))
                    {receipt=receipt,initialReceipt=receipt.Receipt,initialAggregate=receipt.aggregate.Image};
                source.retention=native.RetainForCompletion();source.receiptRetention=receipt.RetainLocalOwnerObservation();
                authorityNative.Add(source.Operation,source); // Unknown is retained even if metadata publication fails.
                if(source.retention==null||source.receiptRetention==null){source.receipt=null;source.initialAggregate=null;source.initialReceipt=null;return;}
                source.TryPublishOriginal();
            }
            catch{/* Actual Add remains native; no parsed substitute. */}
        }
        public void Dispose()
        {
            if(closed)return;closed=true;authorityNative.Remove(Operation);watch?.Dispose();watch=null;Body=saved=originalImage=null;original=physical=null;
            contribution?.Dispose();contribution=null;initialAggregate=null;initialReceipt=null;bodyRetention?.Dispose();bodyRetention=null;receiptRetention?.Dispose();receiptRetention=null;receipt=null;
            retention?.Dispose();retention=null;Memory?.Dispose();Memory=null;
        }
    }
    private static readonly Dictionary<Guid,AuthorityNativePublication> authorityNative=new Dictionary<Guid,AuthorityNativePublication>();
    // Classification only: setModified runs inside native Add, before its
    // postfix can issue the positive contribution. Do not leak that early
    // broadcast as an unauthenticated receipt hydration.
    internal static bool HasActualAuthorityNativeAdd(World world,TileEntityWorkstation station)=>
        world?.IsRemote()==false&&RebirthProductiveEarnedUiQueueOwner.TryGetActualPendingAdd(station,out var pending)&&
        pending.Current()&&ReferenceEquals(pending.World,world)&&pending.NativeOriginal!=null&&pending.UiOriginal==null&&
        pending.NativeOriginal.AuthoritySavedBeforeCurrent();
    private static bool authorityNativeInstalled;
    internal static void InstallAuthorityNativeReceiptObserver(HarmonyLib.Harmony harmony)
    {if(authorityNativeInstalled)return;RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(AuthorityNativeLockRequest));authorityNativeInstalled=true;RebirthProductiveEarnedUiQueueOwner.ActualAddObserved+=AuthorityNativePublication.ObserveAdd;}
    internal static void PumpAuthorityNative(World world)
    {
        foreach(var source in authorityLocalLoaded.Where(value=>!ReferenceEquals(value.World,world)).ToArray())
        {authorityLocalLoaded.Remove(source);source.Dispose();}
        foreach(var source in authorityNative.Values.ToArray())
            if(!ReferenceEquals(source.World,world))source.Dispose();else try{source.TrySeal();}catch{}
        PumpAuthorityNativeRequests(world);
    }
    private static AuthorityNativePublication PendingAuthority(TileEntityWorkstation station)=>station==null?null:
        authorityNative.Values.FirstOrDefault(source=>ReferenceEquals(source.World,GameManager.Instance?.World)&&ReferenceEquals(source.Station,station)&&!source.HasObservedNativeSnapshot);
    internal static bool CanAdmitAuthorityNative(RebirthWorkstationCompletionPipeline.Frame frame)=>frame.Observation.World.IsRemote()||
        frame.Observation.StartingOwnerEntityId==frame.Observation.Viewer.entityId||authorityNative.ContainsKey(frame.Observation.Operation)||authorityNative.Count<4;
    internal static bool CanTickAuthorityNative(TileEntityWorkstation station)=>PendingAuthority(station)==null&&!HoldsLoadedAuthorityLocal(station);
    internal static bool CanSyncAuthorityNative(XUiC_WorkstationWindowGroup group)
    {var station=group?.WorkstationData?.TileEntity;var source=PendingAuthority(station);return !HoldsLoadedAuthorityLocal(station)&&(source==null||source.Body==null);}
    internal static bool CanAdvanceAuthorityNative(XUiC_RecipeStack card)
    {var station=card?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;var source=PendingAuthority(station);
        return !HoldsLoadedAuthorityLocal(station)&&(source==null||source.NativeOriginal.IsKnownCachedCard(card));}
    internal static bool CanRotateAuthorityNative(XUiC_CraftingQueue queue)=>PendingAuthority(queue?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity)==null&&
        !HoldsLoadedAuthorityLocal(queue?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity);
    internal sealed class AuthorityNativeRetirement
    {
        private AuthorityNativePublication source;
        private AuthorityNativeRetirement(AuthorityNativePublication value){source=value;}
        internal void Retire(){var value=source;source=null;value?.Dispose();}
        private static bool Prepare(RecipientScope scope,HostTerminalPublication final,out AuthorityNativeRetirement[] result)
        {
            result=null;
            if(scope==null||final==null||!final.IsCompleteGroupFor(scope)||final.nativeMembers==null)return false;
            var operations=scope.Hydration.Sources.SelectMany(value=>value.ImmutableMembership.Element("members").Elements("member"))
                .Select(value=>ReadGuid(value,"operation")).Distinct().ToArray();
            var values=operations.Where(operation=>authorityNative.ContainsKey(operation)).Select(operation=>authorityNative[operation]).ToArray();
            foreach(var value in values)
                if(!value.SourceCurrent()||!value.Saved||!value.NativeOriginal.RemoteTerminalSaved||
                    !ReferenceEquals(value.World,scope.World)||!ReferenceEquals(value.Station,scope.Station)||
                    !final.nativeMembers.Any(native=>ReferenceEquals(native,value.NativeOriginal)))return false;
            result=values.Select(value=>new AuthorityNativeRetirement(value)).ToArray();return true;
        }
        internal static bool FromSaved(RebirthProductiveEarnedUiRemote.SavedQueueSuccessor successor,out AuthorityNativeRetirement[] result)
        {result=null;return successor?.Current()==true&&Prepare(successor.PreviousScope,successor.PreviousProof,out result);}
        internal static bool FromSaved(RebirthProductiveEarnedUiRemote.SavedEmptyRecipientQueue successor,out AuthorityNativeRetirement[] result)
        {result=null;return successor?.Current()==true&&Prepare(successor.PreviousScope,successor.PreviousProof,out result);}
        internal static bool FromCached(HostCachedPhysicalReceived successor,out AuthorityNativeRetirement[] result)
        {result=null;return successor?.Current()==true&&Prepare(successor.Previous.Recipient,successor.Previous,out result);}
    }
    internal static bool TryPrepareAuthorityNativeRetirements(RebirthProductiveEarnedUiRemote.SavedQueueSuccessor successor,out AuthorityNativeRetirement[] result)
        =>AuthorityNativeRetirement.FromSaved(successor,out result);
    internal static bool TryPrepareAuthorityNativeRetirements(RebirthProductiveEarnedUiRemote.SavedEmptyRecipientQueue successor,out AuthorityNativeRetirement[] result)
        =>AuthorityNativeRetirement.FromSaved(successor,out result);
}
