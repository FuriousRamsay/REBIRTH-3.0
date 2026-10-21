using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class CachedPrimaryHydrationPublication:IDisposable
    {
        private CachedCustody[] originals;private IDisposable[] holds;private IDisposable memory;private XElement image;
        private byte[][] grantNative,grantSaved;
        private ClientInfo peer;private INetConnection connection;private EntityPlayer actor;private Guid group;
        private readonly World world;private readonly TileEntityWorkstation station;private readonly string save,worldGuid;private bool enqueued;
        private CachedPrimaryHydrationPublication(World world,TileEntityWorkstation station,CachedCustody[] sources,IDisposable allocation)
        {this.world=world;this.station=station;originals=sources;memory=allocation;save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());worldGuid=RebirthWorkstationCompletionOutbox.CurrentGuid(world);}
        internal XElement Image=>new XElement(image);
        internal long MeasureImage()=>image==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasureHydrationMetadataElement(image);
        internal bool Current()=>SourceCurrent()&&(enqueued||originals.Select((value,index)=>value.Active&&!value.Physical&&IsKnownCachedStation(value,station)&&value.PrimaryCurrent()&&
            (grantNative?[index]==null||ReferenceEquals(grantNative[index],value.Native)&&ReferenceEquals(grantSaved[index],value.SavedImage)&&value.SavedCurrent())).All(value=>value));
        internal bool SourceCurrent()=>memory!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&
            ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)&&System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==save&&RebirthWorkstationCompletionOutbox.CurrentGuid(world)==worldGuid;
        internal void MarkEnqueued()
        {
            if(enqueued||!Current())return;
            for(int i=0;i<originals.Length;i++)
            {
                var entry=image.Elements("pending").ElementAt(i);var grant=entry.Element("cachedCreatorGrant");
                if(grant!=null)
                {
                    issuingCachedCreatorCache=originals[i];
                    try{originals[i].CreatorIssue=CachedCreatorIssue.FromActualEnqueue(originals[i],grant,group,actor,peer,connection);}
                    finally{issuingCachedCreatorCache=null;}
                }
            }
            enqueued=true;DropHolds();originals=null;grantNative=grantSaved=null;
        }
        private void DropHolds(){if(holds!=null)foreach(var lease in holds)lease?.Dispose();holds=null;}
        public void Dispose(){DropHolds();originals=null;grantNative=grantSaved=null;image=null;peer=null;connection=null;actor=null;memory?.Dispose();memory=null;}
        internal static CachedPrimaryHydrationPublication FromKnownOriginals(World world,TileEntityWorkstation station,CachedCustody[] sources,EntityPlayer actor,string owner,ClientInfo peer,INetConnection connection,Guid group)
        {
            if(sources.Length<1||sources.Length>4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128*1024,out var allocation))return null;
            var result=new CachedPrimaryHydrationPublication(world,station,sources,allocation);
            result.actor=actor;result.peer=peer;result.connection=connection;result.group=group;
            try
            {
                result.holds=new IDisposable[sources.Length];result.grantNative=new byte[sources.Length][];result.grantSaved=new byte[sources.Length][];
                result.image=new XElement("cachedPrimaries",new XAttribute("version",1));long grantBytes=0;
                for(int i=0;i<sources.Length;i++)
                {
                    var source=sources[i];if(!source.Active||source.Physical||!IsKnownCachedStation(source,station)||!source.PrimaryCurrent())return null;
                    result.holds[i]=source.RetainResource();if(result.holds[i]==null)return null;
                    var before=source.Original.Element("before").Elements().Single();
                    var pending=new XElement("pending",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("original",HistoricalHash(source.Image)),
                        new XAttribute("creator",(string)before.Attribute("viewer")),new XAttribute("creation",(string)before.Attribute("creation")),
                        new XAttribute("deaths",(int)before.Attribute("deaths")),new XAttribute("slot",(int)before.Attribute("slot")));
                    if(peer!=null&&connection!=null&&group!=Guid.Empty&&!source.PhysicalObserved&&
                        (bool?)source.Original.Element("cachedOriginal")?.Attribute("remoteCreator")==true&&source.SavedCurrent()&&
                        actor!=null&&owner==(string)before.Attribute("viewer")&&actor.Died==(int)before.Attribute("deaths")&&
                        RebirthCamperCharacterAccess.CurrentCreation(actor)==(string)before.Attribute("creation"))
                    {
                        if(!BindActualLoadedCachedCreator(source,world,station)||!CurrentCachedCreatorFence(source,world))return null;
                        long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(before)+
                            RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.CurrentFull)+2L*source.Native.Length+65536;
                        grantBytes=checked(grantBytes+measured);
                        if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(result.memory,checked(128*1024+16*grantBytes))||!source.NativeCurrent())return null;
                        result.grantNative[i]=source.Native;result.grantSaved[i]=source.SavedImage;
                        var cached=source.Original.Element("cachedOriginal");
                        if(!TryDescribeRetainedCachedAction(source,out var retainedTicket,out var retainedAction))return null;
                        // Cold discovery marks every historical attempted
                        // action. Only its positively described remote action
                        // can receive a retained grant; a local/unknown attempt
                        // remains pending without a new action grant.
                        if(!source.RestoreAttempted||retainedAction!=Guid.Empty)
                        pending.Add(new XElement("cachedCreatorGrant",new XAttribute("version",2),new XAttribute("epoch",source.Epoch),
                            new XAttribute("ticket",(retainedTicket==Guid.Empty?Guid.NewGuid():retainedTicket).ToString("N")),
                            retainedAction==Guid.Empty?null:new XAttribute("retainedAction",retainedAction.ToString("N")),
                            new XAttribute("nonce",(string)cached.Attribute("nonce")),new XAttribute("source",(string)cached.Attribute("source")),
                            new XAttribute("queueOwner",(string)cached.Attribute("queueOwner")),new XAttribute("saved",HistoricalHash(source.SavedImage)),
                            new XElement("before",new XElement(before)),new XElement("full",new XElement(source.CurrentFull)),
                            new XElement("native",new XAttribute("offset",source.Offset),new XAttribute("digest",RebirthProductiveEarnedUiDeferredTransport.CachedNativeDigest(source.Native,source.Offset)))));
                    }
                    result.image.Add(pending);
                }
                if(RebirthProductiveEarnedUiDeferredTransport.MeasureHydrationMetadataElement(result.image)>4L*RebirthWorkstationCompletionPublication.MaximumBytes+65536||!result.Current())return null;
                var owned=result;result=null;return owned;
            }
            finally{result?.Dispose();}
        }
    }
    internal static bool HasCachedPrimaryHydrationCandidate(World world,TileEntityWorkstation station)=>world?.IsRemote()==false&&
        ReferenceEquals(GameManager.Instance?.World,world)&&(HasExecutingCachedBoundary(world,station)||cachedCustodies.Values.Any(value=>value.Active&&!value.Physical&&IsKnownCachedStation(value,station)));
    internal static bool TryCaptureCachedPrimaryHydration(World world,TileEntityWorkstation station,out CachedPrimaryHydrationPublication proof,EntityPlayer actor=null,string owner=null,ClientInfo peer=null,INetConnection connection=null,Guid group=default(Guid))
    {
        proof=null;if(!HasCachedPrimaryHydrationCandidate(world,station))return false;
        try
        {proof=CachedPrimaryHydrationPublication.FromKnownOriginals(world,station,cachedCustodies.Values.Where(value=>value.Active&&!value.Physical&&IsKnownCachedStation(value,station)).ToArray(),actor,owner,peer,connection,group);return proof!=null;}
        catch{proof?.Dispose();proof=null;return false;}
    }
    private static bool BindActualLoadedCachedCreator(CachedCustody source,World world,TileEntityWorkstation station)
    {
        if(ReferenceEquals(source.World,world)&&ReferenceEquals(source.Station,station)&&source.Memory!=null)return source.SourceCurrent();
        var loaded=source.Load?.Loaded;
        if(loaded?.Installed(world)!=true||!ReferenceEquals(loaded.Tile,station)||!source.SavedCurrent()||!source.PrimaryCurrent())return false;
        source.World=world;source.Station=station;source.Chunk=station.GetChunk();
        if(source.Memory==null)
        {
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var text)||!Guid.TryParseExact(text,"N",out var birth)||birth==Guid.Empty)return false;
            source.Memory=RebirthProductiveEarnedObservationBudget.TryOpenNativeCompletion(world,birth,source.Operation,source.Image);
        }
        return source.Memory!=null&&source.SourceCurrent()&&source.NativeCurrent();
    }
    private static bool CurrentCachedCreatorFence(CachedCustody source,World world)
    {
        if(source.Load?.Loaded?.Installed(world)==true)return true;
        if(source.CreatorCloseObserved&&source.CreatorCloseImage!=null&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(source.CreatorClosePath,source.CreatorCloseImage)&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(world,source.Station,out var closeFence)&&closeFence==source.CreatorCloseReadFence)return true;
        return source.RemoteSource?.AdmissionCurrent()==true||source.HasOwnerReadFence&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(world,source.Station,out var current)&&current==source.OwnerReadFence;
    }
}

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class CachedPrimaryClientHold:IDisposable
    {
        internal Guid Operation,Query=Guid.NewGuid();internal string Original,Owner,Creation;internal int Deaths;internal double Next;
        internal World World;internal TileEntityWorkstation Station;internal EntityPlayerLocal Actor;internal INetConnection Connection;internal IDisposable Allocation;
        internal RemoteCachedPrimaryGrant Grant;
        internal bool CurrentPeer()=>ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&!Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.GetConnectionToServer()?[0],Connection)&&!Connection.IsDisconnected();
        public void Dispose(){Grant?.Dispose();Grant=null;World=null;Station=null;Actor=null;Connection=null;Allocation?.Dispose();Allocation=null;}
    }
    private static readonly System.Collections.Generic.Dictionary<Guid,CachedPrimaryClientHold> cachedPrimaryClientHolds=new System.Collections.Generic.Dictionary<Guid,CachedPrimaryClientHold>();
    private sealed class CachedHoldAdmission:IDisposable
    {
        internal readonly System.Collections.Generic.Dictionary<Guid,IDisposable> Memory=new System.Collections.Generic.Dictionary<Guid,IDisposable>();
        internal readonly System.Collections.Generic.Dictionary<Guid,IDisposable> GrantMemory=new System.Collections.Generic.Dictionary<Guid,IDisposable>();
        public void Dispose(){foreach(var value in Memory.Values)value.Dispose();Memory.Clear();foreach(var value in GrantMemory.Values)value.Dispose();GrantMemory.Clear();}
    }
    private static bool PrepareCachedPrimaryHolds(ClientHydration original,int nativeLength)
    {
        var section=original.Header.Element("cachedPrimaries");if(section==null)return true;
        var owned=new CachedHoldAdmission();
        try
        {
            var entries=section.Elements().ToArray();var seen=new System.Collections.Generic.HashSet<Guid>();if((int?)section.Attribute("version")!=1||entries.Length<1||entries.Length>4)return false;
            foreach(var entry in entries)
            {
                if(entry.Name!="pending"||!Guid.TryParseExact((string)entry.Attribute("operation"),"N",out var op)||op==Guid.Empty||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)entry.Attribute("original"))||!seen.Add(op))return false;
                if(cachedPrimaryClientHolds.TryGetValue(op,out var prior))
                {if(prior.Original!=(string)entry.Attribute("original")||!ReferenceEquals(prior.World,original.World)||!ReferenceEquals(prior.Connection,original.Connection))return false;}
                else
                {
                    if(cachedPrimaryClientHolds.Count+owned.Memory.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(16*1024,out var memory))return false;
                    owned.Memory.Add(op,memory);
                }
                if(entry.Element("cachedCreatorGrant")!=null)
                {
                    long measured=checked(16*MeasurePublicationElement(entry)+8L*nativeLength+65536);
                    if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(measured,out var grantMemory))return false;
                    owned.GrantMemory.Add(op,grantMemory);
                }
            }
            original.CachedHoldAdmission=owned;owned=null;return true;
        }
        finally{owned?.Dispose();}
    }
    private static bool ImportCachedPrimaryHolds(ClientHydration original)
    {
        var section=original.Header.Element("cachedPrimaries");if(section==null)return true;
        if(!IsActualHydrationRead(original)||(int?)section.Attribute("version")!=1)return false;
        var entries=section.Elements().ToArray();if(entries.Length<1||entries.Length>4)return false;
        foreach(var entry in entries)
        {
            if(entry.Name!="pending"||!Guid.TryParseExact((string)entry.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                !RebirthWorkstationCompletionOriginal.ValidKey((string)entry.Attribute("original")))return false;
            if(cachedPrimaryClientHolds.TryGetValue(operation,out var prior))
            {
                if(prior.Original!=(string)entry.Attribute("original")||!ReferenceEquals(prior.World,original.World)||!ReferenceEquals(prior.Connection,original.Connection))return false;
                // This exact new authenticated native read may renew only a
                // hold on its actual TE; it grants no native row/action source.
                prior.Station=original.Station;
                if(!ImportRemoteCachedPrimaryGrant(original,entry,prior))return false;
                continue;
            }
            if(original.CachedHoldAdmission is not CachedHoldAdmission admission||!admission.Memory.TryGetValue(operation,out var memory))return false;
            admission.Memory.Remove(operation);
            var hold=new CachedPrimaryClientHold{Operation=operation,Original=(string)entry.Attribute("original"),
                World=original.World,Station=original.Station,Actor=original.Actor,Connection=original.Connection,Owner=original.Owner,Creation=original.Creation,Deaths=original.Deaths,Allocation=memory};
            cachedPrimaryClientHolds.Add(operation,hold);if(!ImportRemoteCachedPrimaryGrant(original,entry,hold))return false;
        }
        return true;
    }
    private static bool HoldsCachedPrimary(TileEntityWorkstation station)=>station!=null&&cachedPrimaryClientHolds.Values.Any(value=>
        ReferenceEquals(value.World,GameManager.Instance?.World)&&ReferenceEquals(value.Station,station));
    internal static void PumpCachedPrimaryHolds(World world)
    {
        foreach(var pair in cachedPrimaryClientHolds.ToArray())
        {
            var hold=pair.Value;if(!ReferenceEquals(hold.World,world)){cachedPrimaryClientHolds.Remove(pair.Key);hold.Dispose();continue;}
            RebirthWorkstationCompletionPipeline.ObserveCachedPhysicalGrant(hold.Grant);
            if(!hold.CurrentPeer()||UnityEngine.Time.realtimeSinceStartup<hold.Next)continue;
            hold.Next=UnityEngine.Time.realtimeSinceStartup+2;RebirthWorkstationCompletionPipeline.SendCachedPrimaryQuery(hold.Operation,hold.Original,hold.Query,hold.Actor);
        }
    }
    internal static void AcceptCachedPrimarySettled(EntityPlayer actor,Guid operation,XElement ack)
    {
        if(!cachedPrimaryClientHolds.TryGetValue(operation,out var hold)||!ReferenceEquals(hold.Actor,actor)||!hold.CurrentPeer()||
            (string)ack.Attribute("digest")!=hold.Original||(string)ack.Attribute("query")!=hold.Query.ToString("N"))return;
        CachedSettlementPublication proof;
        settlingCachedPrimary=hold;
        try{proof=CachedSettlementPublication.Issue(hold,ack);}
        finally{settlingCachedPrimary=null;}
        if(proof==null)return;
        RebirthWorkstationCompletionPipeline.ObserveCachedPhysicalSettlement(proof);
        cachedPrimaryClientHolds.Remove(operation);hold.Dispose();
    }
    [ThreadStatic]private static CachedPrimaryClientHold settlingCachedPrimary;
    internal sealed class CachedSettlementPublication:IDisposable
    {
        internal readonly Guid Operation;internal readonly string Original,Path;
        internal readonly World World;internal readonly TileEntityWorkstation Station;internal readonly EntityPlayerLocal Actor;internal readonly INetConnection Connection;
        private byte[] image;private IDisposable allocation;
        private CachedSettlementPublication(CachedPrimaryClientHold source,byte[] bytes,string path,IDisposable memory)
        {Operation=source.Operation;Original=source.Original;World=source.World;Station=source.Station;Actor=source.Actor;Connection=source.Connection;image=bytes;Path=path;allocation=memory;}
        internal bool Current()=>allocation!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.GetConnectionToServer()?[0],Connection)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,image);
        internal static CachedSettlementPublication Issue(object original,XElement ack)
        {
            if(original is not CachedPrimaryClientHold source||!ReferenceEquals(source,settlingCachedPrimary)||!source.CurrentPeer())return null;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),
                new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(source.World)),new XElement("cachedPrimarySettlement",new XElement(ack)));
            long bytes=MeasurePublicationElement(record);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*bytes+16384),out var memory))return null;
            try
            {
                string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"cachedSettled",source.Operation.ToString("N")+"."+source.Query.ToString("N"));
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return null;
                var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(image==null)return null;
                var proof=new CachedSettlementPublication(source,image,path,memory);memory=null;return proof;
            }
            finally{memory?.Dispose();}
        }
        public void Dispose(){image=null;allocation?.Dispose();allocation=null;}
    }
}
