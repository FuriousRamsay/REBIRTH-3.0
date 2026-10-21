using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private static readonly Dictionary<Guid,RecipientScope> recipients=new Dictionary<Guid,RecipientScope>();
    // Issued only from the retained actual native contribution and the current
    // authenticated recipient body. A wire frame cannot construct this token.
    internal sealed class RecipientScope:IDisposable
    {
        internal readonly HostContribution Source;
        internal readonly HydrationDispatch Hydration;
        internal readonly EntityPlayer Actor;
        internal readonly ClientInfo Peer;
        internal readonly World World;
        internal readonly TileEntityWorkstation Station;
        internal readonly Guid Operation,Nonce,Birth,CustodyBirth;
        internal readonly string Owner,Creation;
        internal readonly int Deaths;
        internal XElement ExpectedBefore {get;private set;}
        internal XElement ExpectedAfter {get;private set;}
        private bool closed;
        private readonly object budgetGate=new object();
        private IDisposable budget;private int budgetReferences=1;
        private long beforeBytes,afterBytes,publicationBytes;
        private const long NativeOwnerWorkspace=64L*1024*1024+65536;
        private RecipientScope(HydrationDispatch hydration,IDisposable reservation)
        {
            var source=hydration.Sources[0];var actor=hydration.Actor;var peer=hydration.Peer;Hydration=hydration;
            budget=reservation;
            Source=source;Actor=actor;Peer=peer;World=source.World;Station=source.Station;
            Operation=source.Operation;Nonce=source.Nonce;Birth=source.Birth;
            CustodyBirth=source.CustodyBirth;
            Owner=source.RecipientOwner;Creation=source.RecipientCreation;Deaths=actor.Died;
        }
        // OwnerPds workers retain this SAME reservation. Closing the scope drops
        // its XML first; actual file-save workers can outlive the main-thread
        // scope without releasing the retained native image charge early.
        internal IDisposable RetainOwnerPdsBudget()
        {lock(budgetGate){if(closed||budget==null)return null;budgetReferences++;return new BudgetLease(this);}}
        private void ReleaseBudget()
        {lock(budgetGate){if(--budgetReferences!=0)return;budget?.Dispose();budget=null;}}
        private sealed class BudgetLease:IDisposable
        {private RecipientScope scope;internal BudgetLease(RecipientScope value){scope=value;}public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref scope,null);value?.ReleaseBudget();}}
        internal bool Current()=>!closed&&ThreadManager.IsMainThread()&&Hydration.Enqueued&&Hydration.Current()&&Source.TrySealStation()&&
            ReferenceEquals(Source.World,World)&&ReferenceEquals(Source.Station,Station)&&
            Source.Operation==Operation&&Source.Nonce==Nonce&&Source.Birth==Birth&&
            Actor.entityId==Source.RecipientEntityId&&Actor.Died==Deaths&&
            RebirthProductiveEarnedArtifact.RemoteAuthority(Actor,Peer,Station,Creation)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Peer,out var identity)&&identity.StorageKey==Owner&&
            Source.Recipient.Deaths==Deaths;
        internal bool BindObservedAfter(XElement after)
        {
            if(!Current()||!OwnerFrame(after))return false;
            if(ExpectedAfter!=null)return XNode.DeepEquals(ExpectedAfter,after);
            if(!ReserveFrameInput(Encoding.UTF8.GetByteCount(after.ToString(SaveOptions.DisableFormatting)),true))return false;
            ExpectedAfter=new XElement(after);return true;
        }
        internal bool BindBefore(XElement before)
        {if(!Current()||!OwnerFrame(before))return false;if(ExpectedBefore!=null)return XNode.DeepEquals(ExpectedBefore,before);
            if(!ReserveFrameInput(Encoding.UTF8.GetByteCount(before.ToString(SaveOptions.DisableFormatting)),false))return false;ExpectedBefore=new XElement(before);return true;}
        // The authenticated request header calls this BEFORE buffering/parsing
        // the frame. Retained before/after XML and native PDS scratch use the
        // same lease; no temporary release or second global reservation occurs.
        internal bool ReserveFrameInput(long utf8Bytes,bool after)
        {
            if(utf8Bytes<1||utf8Bytes>16L*1024*1024)return false;
            lock(budgetGate)
            {
                if(closed||budget==null)return false;
                long before=Math.Max(beforeBytes,after?0:utf8Bytes),later=Math.Max(afterBytes,after?utf8Bytes:0);
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(budget,checked(NativeOwnerWorkspace+4*(before+later)+publicationBytes)))return false;
                beforeBytes=before;afterBytes=later;return true;
            }
        }
        internal IDisposable TryEnterPublicationWorkspace(long utf8Bound)
        {
            if(utf8Bound<1||utf8Bound>24L*1024*1024)return null;
            lock(budgetGate){long amount=checked(16*utf8Bound);
                if(closed||budget==null||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(budget,
                    checked(NativeOwnerWorkspace+4*(beforeBytes+afterBytes)+publicationBytes+amount)))return null;
                publicationBytes+=amount;return new PublicationLease(this,amount);}
        }
        private sealed class PublicationLease:IDisposable
        {
            private RecipientScope scope;private readonly long amount;
            internal PublicationLease(RecipientScope value,long size){scope=value;amount=size;}
            public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref scope,null);if(value==null)return;
                lock(value.budgetGate){value.publicationBytes-=amount;if(value.budget!=null)
                    RebirthProductiveEarnedObservationBudget.TryResizeDetached(value.budget,checked(NativeOwnerWorkspace+4*(value.beforeBytes+value.afterBytes)+value.publicationBytes));}}
        }
        public void Dispose(){if(closed)return;closed=true;if(recipients.TryGetValue(Operation,out var retained)&&ReferenceEquals(retained,this))recipients.Remove(Operation);
            ExpectedBefore=ExpectedAfter=null;OwnerPds.Release(this);ReleaseBudget();}
        internal static bool TryCreate(HydrationDispatch hydration,out RecipientScope scope)
        {
            scope=null;
            if(hydration==null||!hydration.Enqueued||!dispatches.TryGetValue(hydration.Group,out var actual)||!ReferenceEquals(actual,hydration)||!hydration.Current())return false;
            var source=hydration.Sources[0];var actor=hydration.Actor;var peer=hydration.Peer;
            if(recipients.TryGetValue(source.Operation,out var prior))
            {if(!prior.Current()||!ReferenceEquals(prior.Actor,actor)||!ReferenceEquals(prior.Peer,peer)||!ReferenceEquals(prior.Hydration,hydration))return false;scope=prior;return true;}
            if(recipients.Count>=4)return false;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(NativeOwnerWorkspace,out var reservation))return false;
            RecipientScope candidate;
            try{candidate=new RecipientScope(hydration,reservation);}
            catch{reservation.Dispose();throw;}
            if(!candidate.Current()){candidate.Dispose();return false;}
            recipients.Add(source.Operation,candidate);scope=candidate;return true;
        }
    }
    internal static bool CanUseKnownNativeRewards(TileEntityWorkstation station,EntityPlayerLocal player)
    {
        // Unknown native rows keep their original behavior. Only a positively
        // retained exact aggregate is held while its own recipient is unready.
        if(!ThreadManager.IsMainThread()||station==null||player==null)return true;
        if(!RebirthWorkstationCompletionPipeline.CanUseCachedNativeRewards(station,player))return false;
        if(!CanUseHydratedNativeRewards(station,player))return false;
        if(!RebirthProductiveEarnedUiOwnerFile.CanUseLocalKnownNativeRewards(station,player))return false;
        foreach(var contribution in host.Values)
        {
            if(contribution.Closed||!ReferenceEquals(contribution.Station,station)||station.CraftCompleteList==null)continue;
            try{if(!station.CraftCompleteList.Any(value=>Row(value).SequenceEqual(contribution.Aggregate)))continue;}
            catch{return false;}
            if(player.entityId!=contribution.RecipientEntityId||player.Died!=contribution.Recipient.Deaths||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=contribution.RecipientOwner||
                RebirthCamperCharacterAccess.CurrentCreation(player)!=contribution.RecipientCreation)return false;
            if(RebirthProductiveEarnedUiOwnerFile.IsInvokingLocalHostContribution(contribution,station,player))continue;
            if(!recipients.TryGetValue(contribution.Operation,out var scope)||!ReferenceEquals(scope.Actor,player)||!scope.Current()||
                !OwnerPds.TryReadBefore(scope,out var before)||!before.Current())return false;
        }
        return true;
    }
    private static bool OwnerFrame(XElement frame)
    {
        bool equipment=(int?)frame?.Attribute("version")==2;
        if(frame?.Name!="nativeOwnerFrame"||(!equipment&&(int?)frame.Attribute("version")!=1)||
            frame.Elements("equipment").Count()!=(equipment?1:0)||
            Encoding.UTF8.GetByteCount(frame.ToString(SaveOptions.DisableFormatting))>16*1024*1024)return false;
        var recipes=frame.Element("recipes")?.Elements("recipe").ToArray();
        return recipes!=null&&recipes.Length>=1&&recipes.Length<=4&&
            recipes.Select(value=>(string)value.Attribute("name")).All(value=>!string.IsNullOrEmpty(value))&&
            recipes.Select(value=>(string)value.Attribute("name")).Distinct(StringComparer.Ordinal).Count()==recipes.Length&&
            recipes.All(value=>value.Element("projection")!=null)&&frame.Element("bag")!=null&&frame.Element("belt")!=null;
    }
}
