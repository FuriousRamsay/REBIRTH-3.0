using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed class HostLocalContribution
    {
        internal readonly HostLocalReceipt Source;
        internal RebirthProductiveEarnedUiQueueOwner.Witness Creator;
        internal readonly Guid Operation;internal XElement Member;
        internal byte[] BeforeComplete,AfterComplete;
        internal RebirthProductiveEarnedUiQueueOwner.DeferredReceipt Token;
        private HostLocalContribution(HostLocalReceipt source,RebirthProductiveEarnedUiQueueOwner.Witness creator,XElement member)
        {Source=source;Creator=creator;Member=member;Operation=ReadGuid(member,"operation");BeforeComplete=ReadBytes(member,"beforeComplete");AfterComplete=ReadBytes(member,"afterComplete");}
        internal bool Current()=>Source.Current()&&Creator.SourceCurrent();
        internal IDisposable Retain()=>Source.Retain();
        internal void DropDetached(){Token?.Dispose();Token=null;BeforeComplete=AfterComplete=null;Member=null;Creator=null;}
        internal static HostLocalContribution FromAcceptedRead(HostLocalReceipt source,XElement member)
        {
            if(!RebirthProductiveEarnedUiRemote.IsAcceptingHostReceipt(source.Original.Original)||!source.Current()||
                !RebirthProductiveEarnedUiQueueOwner.TryResolvePublishedPurpose(source.Original.World,source.Original.Station,
                    (string)member.Attribute("source"),out var creator))throw new InvalidDataException();
            return new HostLocalContribution(source,creator,member);
        }
        internal static HostLocalContribution FromLoadedCache(HostLocalReceipt source,XElement member)
        {
            if(!source.IsLoadedCache||!source.Current()||!TryResolveContributionPurpose(source.Original,(string)member.Attribute("source"),out var creator))
                throw new InvalidDataException();
            return new HostLocalContribution(source,creator,member);
        }
        internal static HostLocalContribution FromLoadedAuthority(HostLocalReceipt source,XElement member)
        {
            if(!source.IsLoadedAuthority||!source.Current()||!TryResolveLoadedContributionPurpose(source.Original,(string)member.Attribute("source"),out var creator))
                throw new InvalidDataException();
            return new HostLocalContribution(source,creator,member);
        }
    }
    internal sealed class HostLocalReceipt
    {
        internal readonly HostContribution Original;internal readonly CraftCompleteData Receipt;internal readonly long ReadVersion;
        internal readonly List<HostLocalContribution> Members=new List<HostLocalContribution>();
        private readonly object gate=new object();private IDisposable allocation,registration;private int references=1;internal bool Closed;
        private readonly RebirthWorkstationCompletionPipeline.CachedHostOriginal loadedCache;
        private readonly AuthorityLocalLoadedSource loadedAuthority;
        internal bool IsLoadedCache=>loadedCache!=null&&ReferenceEquals(Original.CachedOriginal,loadedCache);
        internal bool IsLoadedAuthority=>loadedAuthority!=null&&ReferenceEquals(Original.AuthorityLoadedOriginal,loadedAuthority);
        internal bool AcceptingSource()=>IsLoadedAuthority?loadedAuthority.Current():IsLoadedCache?loadedCache.Current():RebirthProductiveEarnedUiRemote.IsAcceptingHostReceipt(Original.Original);
        internal HostLocalReceipt(HostContribution original,CraftCompleteData receipt,long version,IDisposable budget,IDisposable retained,bool cached=false,bool authority=false)
        {Original=original;Receipt=receipt;ReadVersion=version;allocation=budget;registration=retained;loadedCache=cached?original.CachedOriginal:null;loadedAuthority=authority?original.AuthorityLoadedOriginal:null;}
        internal bool Current()=>!Closed&&ThreadManager.IsMainThread()&&Original.SourceCurrentWithoutViewer()&&
            (IsLoadedCache||IsLoadedAuthority?TryReadNativeObservationFence(Original.World,Original.Station,out var fence)&&fence==ReadVersion:
                nativeReadVersions.TryGetValue(Original.Station,out var read)&&read.Value==ReadVersion);
        internal IDisposable Retain(){lock(gate){if(Closed||allocation==null)return null;references++;return new Lease(this);}}
        private void Release(){lock(gate){if(--references!=0)return;registration?.Dispose();registration=null;allocation?.Dispose();allocation=null;}}
        private sealed class Lease:IDisposable
        {private HostLocalReceipt source;internal Lease(HostLocalReceipt value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        internal void Close(){if(Closed)return;Closed=true;foreach(var member in Members)member.DropDetached();Members.Clear();Release();}
    }
    private static readonly Dictionary<Guid,HostLocalReceipt> hostLocalReceipts=new Dictionary<Guid,HostLocalReceipt>();
    // Resource retirement only; called after the owner of the real local
    // terminal/actual saved successor has finished all financial consumers.
    internal static bool ReleaseLocalHostReceiptResources(RebirthProductiveEarnedUiOwnerFile.LocalOwnerScope scope,Func<bool> successor=null)
    {
        if(!ThreadManager.IsMainThread()||scope?.Members==null)return false;
        var sources=scope.Members.Where(value=>value.HostLocalOriginal!=null).Select(value=>value.HostLocalOriginal.Source).Distinct().ToArray();
        var loaded=sources.Select(value=>value.Original.AuthorityLoadedOriginal).Where(value=>value!=null).Distinct().ToArray();
        Func<bool> qualification=successor??(()=>scope.NativeCloseObserved&&scope.Terminal?.Current()==true);
        foreach(var source in loaded)if(!source.PrepareRetirement(scope,qualification))return false;
        foreach(var source in sources)
        {
            if(hostLocalReceipts.TryGetValue(source.Original.Operation,out var retained)&&ReferenceEquals(retained,source))hostLocalReceipts.Remove(source.Original.Operation);
            source.Close();source.Original.Dispose();
        }
        foreach(var source in loaded)source.Retire();
        return true;
    }
    private static void ObserveHostLocalReceiptAccepted(HostContribution source,CraftCompleteData receipt)
    {
        HostLocalReceipt original=null;IDisposable budget=null,retained=null;
        try
        {
            // The host is a native local recipient, not a ClientInfo stand-in.
            // The accepted read event is mandatory; no getter can create this.
            var actor=source.World.GetPrimaryPlayer();
            if(!RebirthProductiveEarnedUiRemote.IsAcceptingHostReceipt(source.Original)||actor==null||
                actor.entityId!=source.RecipientEntityId||actor.Died!=source.Recipient.Deaths||
                RebirthCamperCharacterAccess.CurrentCreation(actor)!=source.RecipientCreation||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=source.RecipientOwner||
                !nativeReadVersions.TryGetValue(source.Station,out var version))return;
            if(hostLocalReceipts.TryGetValue(source.Operation,out var prior))
            {
                if(ReferenceEquals(prior.Receipt,receipt)&&prior.Current())return;
                // A replacement read cannot be adopted by an active owner
                // attempt. The old source remains an unknown held original.
                if(RebirthProductiveEarnedUiOwnerFile.HasLocalOwnerAttempt(source.Station))return;
                if(!RebirthProductiveEarnedUiOwnerFile.DropUnusedHostLocalRead(prior))return;
                hostLocalReceipts.Remove(source.Operation);prior.Close();
            }
            if(hostLocalReceipts.Count>=4)return;
            long bytes=MeasurePublicationElement(source.ImmutableMembership);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4*bytes+65536),out budget))return;
            retained=RebirthProductiveEarnedUiRemote.RetainDeferredGroup(source.Original);if(retained==null)return;
            original=new HostLocalReceipt(source,receipt,version.Value,budget,retained);budget=retained=null;
            foreach(var member in source.ImmutableMembership.Element("members").Elements("member"))original.Members.Add(HostLocalContribution.FromAcceptedRead(original,member));
            if(original.Members.Count<1||original.Members.Count>4||
                !RebirthProductiveEarnedUiQueueOwner.ImportActualHostLocalReceipt(original))return;
            hostLocalReceipts.Add(source.Operation,original);original=null;
        }
        catch{/* Unknown local recipient/source cannot borrow an online peer. */}
        finally{original?.Close();budget?.Dispose();retained?.Dispose();}
    }
}
