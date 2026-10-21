using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Only the authenticated selected native reader can issue this retained
    // original-primary source. Parsing the durable receipt cannot recreate it.
    internal sealed class RemoteCachedPrimaryGrant:IDisposable
    {
        internal readonly Guid Operation,Nonce,Ticket;internal readonly string Original,SourceHash,OwnerPurpose,Owner,Creation,Path;
        internal readonly Guid RetainedAction;
        internal Guid Group=>group;
        internal readonly World World;internal readonly TileEntityWorkstation Station;internal readonly EntityPlayerLocal Actor;
        internal readonly INetConnection Connection;internal readonly int Deaths,Offset;internal readonly long ReadVersion;
        private readonly Guid group;private bool processReturned;
        internal XUiC_WorkstationWindowGroup Window{get;private set;}
        internal XUiC_RecipeStack Card{get;private set;}
        internal long WindowSession{get;private set;}
        internal long NativeVersion{get;private set;}=-1;
        private XElement grant,record;private byte[] native,image;private IDisposable allocation;private int references=1;private bool closed;
        private readonly string save,worldGuid;
        private RemoteCachedPrimaryGrant(ClientHydration actual,XElement entry,IDisposable memory)
        {
            Operation=ReadGuid(entry,"operation");Original=(string)entry.Attribute("original");grant=new XElement(entry.Element("cachedCreatorGrant"));
            Nonce=ReadGuid(grant,"nonce");Ticket=ReadGuid(grant,"ticket");SourceHash=(string)grant.Attribute("source");OwnerPurpose=(string)grant.Attribute("queueOwner");
            RetainedAction=grant.Attribute("retainedAction")==null?Guid.Empty:ReadGuid(grant,"retainedAction");
            World=actual.World;Station=actual.Station;Actor=actual.Actor;Connection=actual.Connection;Owner=actual.Owner;Creation=actual.Creation;Deaths=actual.Deaths;
            save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());worldGuid=RebirthWorkstationCompletionOutbox.CurrentGuid(World);
            ReadVersion=actual.ReadVersion;Offset=actual.NativeBeforeOffset;native=(byte[])actual.NativeBefore.Clone();allocation=memory;
            group=actual.Group;
            Path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"cachedGrants",Operation.ToString("N")+"."+actual.Group.ToString("N")+".xml");
            record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(World)),new XElement("before",new XElement("actualCachedCreatorHydration",
                    new XAttribute("group",actual.Group.ToString("N")),new XAttribute("original",Original),new XAttribute("readVersion",ReadVersion),
                    new XAttribute("owner",Owner),new XAttribute("creation",Creation),new XAttribute("deaths",Deaths),new XElement(grant),
                    new XElement("native",new XAttribute("offset",Offset),new XAttribute("digest",CachedNativeDigest(native,Offset))))));
        }
        private bool ReadCurrent()=>!closed&&allocation!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&
            save==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())&&worldGuid==RebirthWorkstationCompletionOutbox.CurrentGuid(World)&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&!Actor.IsDead()&&Actor.Died==Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.StorageKey==Owner&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.GetConnectionToServer()?[0],Connection)&&!Connection.IsDisconnected()&&
            IsCurrentQueueOwnerNativeRead(World,Station,ReadVersion)&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,image);
        internal bool Current()=>processReturned&&ReadCurrent();
        internal bool CurrentWindowSource()=>Current()&&Window!=null&&Card!=null&&
            TryGetCurrentNativeUiSession(Window,out var session)&&session==WindowSession&&
            ReferenceEquals(Window.WorkstationData?.TileEntity,Station)&&ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Actor);
        internal bool CurrentWindow()=>CurrentWindowSource()&&
            RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(Card,NativeVersion)&&
            RebirthWorkstationCompletionPipeline.MatchesCachedGrantCard(Window,Card,grant.Element("before").Elements().Single());
        internal void ObserveActualOpen(ClientHydration actual,XUiC_WorkstationWindowGroup window,long session)
        {
            // Retain the actual OnOpen inside this exact native Process. The
            // later returned-Process proof completes custody; neither getter
            // can manufacture an open event or authorize primary restoration.
            if(Window!=null||!IsActualCachedWindowOpen(window,session)||!IsActualHydrationRead(actual)||actual.Group!=group||
                !ReferenceEquals(actual.World,World)||!ReferenceEquals(actual.Station,Station)||!ReferenceEquals(actual.Connection,Connection)||
                !ReferenceEquals(window.WorkstationData?.TileEntity,Station)||!ReferenceEquals(window.xui?.playerUI?.entityPlayer,Actor)||!ReadCurrent())return;
            var before=grant.Element("before").Elements().Single();int index=(int)before.Attribute("slot");var cards=window.craftingQueue.GetRecipesToCraft();
            if(index<0||index>=cards.Length||!RebirthWorkstationCompletionPipeline.MatchesCachedGrantCard(window,cards[index],before)||
                !RebirthRecipeOutputIntegration.TryCaptureOwningOpenVersion(cards[index],window,session,out var version))return;
            Window=window;Card=cards[index];WindowSession=session;NativeVersion=version;
        }
        internal void ObserveReturnedProcess(ClientHydration actual)
        {
            if(!ReferenceEquals(returningCachedCreatorProcess,actual)||!IsActualHydrationRead(actual)||actual.Group!=group||
                !ReferenceEquals(actual.World,World)||!ReferenceEquals(actual.Station,Station)||!ReferenceEquals(actual.Connection,Connection)||!ReadCurrent())return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,native,Offset))processReturned=true;
        }
        internal bool NativeCurrent()
        {if(!Current())return false;using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())return decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,native,Offset);}
        internal long MeasureImage()=>record==null?0:MeasurePublicationElement(record);
        internal XElement CopyImage()=>Current()?new XElement(record):null;
        internal XElement CopyBefore()=>Current()?new XElement(grant.Element("before").Elements().Single()):null;
        internal XElement CopyFull()=>Current()?new XElement(grant.Element("full").Elements().Single()):null;
        internal IDisposable Retain(){if(!Current())return null;references++;return new GrantLease(this);}
        private sealed class GrantLease:IDisposable{private RemoteCachedPrimaryGrant source;internal GrantLease(RemoteCachedPrimaryGrant value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        private void Release(){if(--references!=0)return;Window=null;Card=null;grant=record=null;native=image=null;allocation?.Dispose();allocation=null;}
        public void Dispose(){if(closed)return;closed=true;Release();}
        internal static RemoteCachedPrimaryGrant FromActualRead(ClientHydration actual,XElement entry,IDisposable memory)
        {
            if(!IsActualHydrationRead(actual))return null;
            var grant=entry.Element("cachedCreatorGrant");var before=grant?.Element("before")?.Elements().SingleOrDefault();
            var full=grant?.Element("full")?.Elements().SingleOrDefault();
            if(grant?.Attribute("retainedAction")!=null&&(!Guid.TryParseExact((string)grant.Attribute("retainedAction"),"N",out var retainedAction)||retainedAction==Guid.Empty))return null;
            if(((int?)grant?.Attribute("version")!=1&&(int?)grant?.Attribute("version")!=2)||!Guid.TryParseExact((string)grant.Attribute("ticket"),"N",out var ticket)||ticket==Guid.Empty||
                (int?)grant.Attribute("epoch") is not int epoch||epoch<0||epoch>64||
                (string)entry.Attribute("creator")!=actual.Owner||(string)entry.Attribute("creation")!=actual.Creation||(int?)entry.Attribute("deaths")!=actual.Deaths||
                before==null||(string)before.Attribute("viewer")!=actual.Owner||(string)before.Attribute("creation")!=actual.Creation||(int?)before.Attribute("deaths")!=actual.Deaths||
                full?.Name!="nativeOwningFullBoundary"||(string)full.Attribute("operation")!=(string)entry.Attribute("operation")||
                !RebirthWorkstationCompletionOriginal.ValidKey((string)grant.Attribute("source"))||!RebirthWorkstationCompletionOriginal.ValidKey((string)grant.Attribute("saved"))||
                string.IsNullOrEmpty((string)grant.Attribute("queueOwner")))return null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if((int?)grant.Attribute("version")==2)
                {
                    var attestation=grant.Element("native");
                    if(decoder==null||attestation==null||attestation.HasElements||attestation.Value.Length!=0||attestation.Attributes().Count()!=2||
                        (int?)attestation.Attribute("offset")!=actual.NativeBeforeOffset||
                        (string)attestation.Attribute("digest")!=CachedNativeDigest(actual.NativeBefore,actual.NativeBeforeOffset))return null;
                }
                else
                {
                string encoded=(string)grant.Element("native");int maximum=RebirthProductiveEarnedNativeBody.MaximumBodyBytes;
                if(decoder==null||encoded==null||encoded.Length<4||encoded.Length%4!=0||encoded.Length>4*((maximum+2)/3))return null;
                int padding=encoded.EndsWith("==",StringComparison.Ordinal)?2:encoded.EndsWith("=",StringComparison.Ordinal)?1:0;
                long decoded=3L*(encoded.Length/4)-padding;if(decoded<8||decoded>maximum)return null;
                byte[] expected=Convert.FromBase64String(encoded);int offset=(int)grant.Element("native").Attribute("offset");
                if(expected.Length!=decoded||Convert.ToBase64String(expected)!=encoded||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(expected,offset,actual.NativeBefore,actual.NativeBeforeOffset))return null;
                }
                var primary=RebirthProductiveEarnedUiRemote.ReadGrid(full,"primaryNative");if(primary?.Length!=1||primary[0].IsEmpty())return null;
            }
            RemoteCachedPrimaryGrant result=null;
            try
            {
                result=new RemoteCachedPrimaryGrant(actual,entry,memory);
                if(!RebirthWorkstationCompletionPublication.Write(result.Path,result.record))return null;
                result.image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(result.Path);
                if(result.image==null||!result.ReadCurrent())return null;
                var owned=result;result=null;return owned;
            }
            finally{if(result!=null){result.allocation=null;result.Dispose();}}
        }
    }
    [ThreadStatic]private static ClientHydration returningCachedCreatorProcess;
    internal static void ObserveRemoteCachedPrimaryWindowOpened(XUiC_WorkstationWindowGroup window,long session)
    {
        if(!IsActualCachedWindowOpen(window,session)||!IsActualHydrationRead(hydrating))return;
        foreach(var hold in cachedPrimaryClientHolds.Values)hold.Grant?.ObserveActualOpen(hydrating,window,session);
    }
    private static void ObserveCachedCreatorProcessReturned(ClientHydration actual)
    {
        if(!IsActualHydrationRead(actual))return;returningCachedCreatorProcess=actual;
        try{foreach(var hold in cachedPrimaryClientHolds.Values){hold.Grant?.ObserveReturnedProcess(actual);RebirthWorkstationCompletionPipeline.ObserveCachedPhysicalGrant(hold.Grant);}}
        finally{returningCachedCreatorProcess=null;}
    }
    private static bool ImportRemoteCachedPrimaryGrant(ClientHydration actual,XElement entry,CachedPrimaryClientHold hold)
    {
        if(entry.Element("cachedCreatorGrant")==null)return true;
        if(actual.CachedHoldAdmission is not CachedHoldAdmission admission||!admission.GrantMemory.TryGetValue(hold.Operation,out var memory))return false;
        admission.GrantMemory.Remove(hold.Operation);RemoteCachedPrimaryGrant grant=null;
        try
        {
            grant=RemoteCachedPrimaryGrant.FromActualRead(actual,entry,memory);if(grant==null)return false;memory=null;
            hold.Grant?.Dispose();hold.Grant=grant;grant=null;return true;
        }
        finally{grant?.Dispose();memory?.Dispose();}
    }
    internal static bool TryGetRemoteCachedPrimaryGrant(XUiC_WorkstationWindowGroup window,out RemoteCachedPrimaryGrant grant)
    {
        grant=null;var station=window?.WorkstationData?.TileEntity;
        var matches=cachedPrimaryClientHolds.Values.Where(value=>ReferenceEquals(value.Station,station)&&ReferenceEquals(value.Actor,window?.xui?.playerUI?.entityPlayer)&&
            value.Grant?.Current()==true).ToArray();if(matches.Length!=1)return false;grant=matches[0].Grant;return true;
    }
}
