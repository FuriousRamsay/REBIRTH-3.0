using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed class ImportedContribution
    {
        internal readonly ClientHydration Original;
        internal readonly Guid Operation,AggregateOperation,Birth;
        internal readonly string Kind,CreatorPurpose;
        internal readonly int Contribution;
        internal readonly CraftCompleteData Receipt;
        internal readonly byte[] BeforeComplete,AfterComplete;
        internal RebirthProductiveEarnedUiQueueOwner.DeferredReceipt Token;
        internal RebirthProductiveEarnedUiOwnerFile.DeferredWitness Witness;
        private ImportedContribution(ClientHydration original,XElement aggregate,XElement member,CraftCompleteData receipt)
        {
            Original=original;Operation=ReadGuid(member,"operation");AggregateOperation=ReadGuid(aggregate,"operation");Birth=ReadGuid(aggregate,"birth");
            Kind=(string)member.Attribute("kind");CreatorPurpose=(string)member.Element("creator");Contribution=(int)member.Attribute("contribution");Receipt=receipt;
            int index=(int)member.Attribute("nativeEvent");BeforeComplete=original.NativeEvents[2*index];AfterComplete=original.NativeEvents[2*index+1];
        }
        internal bool SourceCurrentWithoutRow()=>Original.CurrentSource();
        internal IDisposable RetainForCompletion()=>Original.Retain();
        internal static ImportedContribution CreateFromActualRead(ClientHydration original,XElement aggregate,XElement member,CraftCompleteData receipt)
        {
            if(!ReferenceEquals(hydrating,original)||!original.ReadCompleted||original.Reads!=1||!original.CurrentIdentity()||
                original.Station.CraftCompleteList?.Count(value=>ReferenceEquals(value,receipt))!=1)throw new InvalidDataException();
            return new ImportedContribution(original,aggregate,member,receipt);
        }
    }
    internal sealed class ClientHydration
    {
        internal IDisposable CachedHoldAdmission;
        internal IDisposable MetadataBudget;
        internal Guid Group;internal World World;internal TileEntityWorkstation Station;internal EntityPlayerLocal Actor;
        internal INetConnection Connection;internal string Owner,Creation,WorldKey,Path;internal int Deaths;
        internal XElement Header,Evidence,MetadataSelector;internal byte[][] NativeEvents;
        internal byte[] NativeBefore,NativeAfter;internal int NativeBeforeOffset,NativeAfterOffset;
        internal NetPackageTileEntity Packet;internal long ReadVersion;internal object QueueResponse;
        internal int Reads;internal bool ReadCompleted,Processed,Closed,BeforeReady,Attempted,Consumed,Unknown;
        internal bool InvokingCheck;
        internal XUiC_WorkstationWindowGroup Window;internal long WindowVersion;
        internal bool CurrentWindow()=>CurrentSource()&&Window!=null&&windowVersions.TryGetValue(Window,out var version)&&version.Value==WindowVersion&&
            ReferenceEquals(Window.WorkstationData?.TileEntity,Station)&&ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Actor)&&
            Window.WindowGroup?.isShowing==true&&LockManager.Instance?.IsLockedByLocalPlayer(Station,0)==true;
        internal XElement BeforeFrame,AfterFrame,ConsumedReport;
        internal byte[] BeforeWire,AfterWire;internal string BeforeDigest,AfterDigest;
        internal byte OwnerStatus;internal int SendIndex;internal float NextOwnerPump;
        internal ClientTerminalPublication Terminal;
        internal ClientCachedPhysicalObservation CachedPhysicalObservation;
        internal bool HandoffAccepted;
        internal readonly List<ImportedContribution> Members=new List<ImportedContribution>();
        private readonly object gate=new object();private IDisposable allocation;private int references=1;
        private long beforeFrameBytes,afterFrameBytes,nativeReceiptBytes,publicationBytes;
        internal ClientHydration(IDisposable budget){allocation=budget;}
        internal bool CurrentIdentity()=>!Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&
            !Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection)&&!Connection.IsDisconnected()&&
            WorldKey==RebirthProductiveEarnedArtifact.WorldIdentity(World);
        internal bool CurrentSource()
        {
            try{return CurrentIdentity()&&ReadCompleted&&nativeReadVersions.TryGetValue(Station,out var version)&&version.Value==ReadVersion&&
                Evidence!=null&&XNode.DeepEquals(Evidence,RebirthWorkstationCompletionPublication.ReadPrimary(Path));}
            catch{return false;}
        }
        internal IDisposable Retain(){lock(gate){if(Closed||allocation==null)return null;references++;return new Lease(this);}}
        // One measured account owns transport input, actual native owner
        // capture/save scratch, and both retained XML frames. A native worker
        // retains this same token rather than reserving another maximum.
        internal IDisposable RetainNativeCapture(long receiptBytes)
        {
            if(receiptBytes<1||receiptBytes>1024*1024)return null;
            lock(gate){long measured=Math.Max(nativeReceiptBytes,receiptBytes);
                if(Closed||allocation==null||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,
                checked(96L*1024*1024+65536+2*measured+4*(beforeFrameBytes+afterFrameBytes)+publicationBytes)))return null;
                nativeReceiptBytes=measured;
                references++;return new Lease(this);}
        }
        internal bool ReserveOwnerFrame(long utf8Bytes,bool after)
        {
            if(utf8Bytes<1||utf8Bytes>16L*1024*1024)return false;
            lock(gate){if(Closed||allocation==null)return false;
                long before=Math.Max(beforeFrameBytes,after?0:utf8Bytes),later=Math.Max(afterFrameBytes,after?utf8Bytes:0);
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,checked(96L*1024*1024+65536+2*nativeReceiptBytes+4*(before+later)+publicationBytes)))return false;
                beforeFrameBytes=before;afterFrameBytes=later;return true;}
        }
        internal IDisposable TryEnterPublicationWorkspace(long utf8Bound)
        {
            if(utf8Bound<1||utf8Bound>24L*1024*1024)return null;
            lock(gate){long amount=checked(16*utf8Bound);
                if(Closed||allocation==null||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,
                    checked(96L*1024*1024+65536+2*nativeReceiptBytes+4*(beforeFrameBytes+afterFrameBytes)+publicationBytes+amount)))return null;
                publicationBytes+=amount;return new PublicationLease(this,amount);}
        }
        private sealed class PublicationLease:IDisposable
        {
            private ClientHydration scope;private readonly long amount;
            internal PublicationLease(ClientHydration value,long size){scope=value;amount=size;}
            public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref scope,null);if(value==null)return;
                lock(value.gate){value.publicationBytes-=amount;if(value.allocation!=null)
                    RebirthProductiveEarnedObservationBudget.TryResizeDetached(value.allocation,checked(96L*1024*1024+65536+2*value.nativeReceiptBytes+4*(value.beforeFrameBytes+value.afterFrameBytes)+value.publicationBytes));}}
        }
        private void Release(){lock(gate){if(--references!=0)return;allocation?.Dispose();allocation=null;}}
        private sealed class Lease:IDisposable
        {private ClientHydration source;internal Lease(ClientHydration value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        internal void Close()
        {
            if(Closed)return;Closed=true;
            CachedPhysicalObservation?.Dispose();CachedPhysicalObservation=null;
            CachedHoldAdmission?.Dispose();CachedHoldAdmission=null;
            foreach(var member in Members){member.Witness?.Dispose();member.Token?.Dispose();}Members.Clear();Header=Evidence=BeforeFrame=AfterFrame=ConsumedReport=null;BeforeWire=AfterWire=NativeBefore=NativeAfter=null;NativeEvents=null;Packet=null;QueueResponse=null;
            MetadataSelector=null;MetadataBudget?.Dispose();MetadataBudget=null;
            Window=null;Station=null;World=null;Actor=null;Connection=null;Release();
        }
    }
    private sealed class NativeReadVersion{internal long Value;}
    private sealed class ClientAssembly
    {
        internal Guid Group;internal World World;internal INetConnection Connection;internal string Hash;internal int Total,Received;
        internal byte[][] Parts;internal IDisposable Budget;internal double Created;
        internal int MetadataQueries;internal double NextMetadataQuery;
    }
    private static readonly Dictionary<Guid,ClientAssembly> assemblingHydration=new Dictionary<Guid,ClientAssembly>();
    private static readonly Dictionary<Guid,ClientHydration> clientHydrations=new Dictionary<Guid,ClientHydration>();
    private static readonly System.Collections.Generic.Dictionary<Guid,World> queueOwnerHydrationsProcessed=new System.Collections.Generic.Dictionary<Guid,World>();
    private static IDisposable queueOwnerHydrationHistoryMemory;private static World queueOwnerHydrationHistoryWorld;
    internal static void ReleaseQueueOwnerHydrationHistory(World current)
    {
        if(ReferenceEquals(current,queueOwnerHydrationHistoryWorld))return;
        queueOwnerHydrationsProcessed.Clear();queueOwnerHydrationHistoryMemory?.Dispose();queueOwnerHydrationHistoryMemory=null;queueOwnerHydrationHistoryWorld=current;
    }
    private static readonly ConditionalWeakTable<TileEntityWorkstation,NativeReadVersion> nativeReadVersions=new ConditionalWeakTable<TileEntityWorkstation,NativeReadVersion>();
    internal static bool IsCurrentQueueOwnerNativeRead(World world,TileEntityWorkstation station,long version)
        =>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&world?.IsRemote()==true&&
            ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)&&nativeReadVersions.TryGetValue(station,out var current)&&current.Value==version;
    [ThreadStatic]private static ClientHydration hydrating;
    private static Guid ReadGuid(XElement image,string name)
    {if(!Guid.TryParseExact((string)image.Attribute(name),"N",out var value)||value==Guid.Empty)throw new InvalidDataException();return value;}
    private static byte[] Exact(BinaryReader reader,int maximum)
    {int count=reader.ReadInt32();if(count<1||count>maximum)throw new InvalidDataException();var value=reader.ReadBytes(count);if(value.Length!=count)throw new EndOfStreamException();return value;}
    internal static void ReceiveHydration(NetPackageRebirthUiDeferredHydrationV1 packet,World world,GameManager callbacks)
    {
        ClientAssembly assembly=null;ClientHydration original=null;
        try
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||manager?.IsClient!=true||manager.IsServer||
                !TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(manager.connectionToServer?[0],connection)||connection.IsDisconnected()||hydrating!=null)return;
            ReleaseQueueOwnerHydrationHistory(world);
            SweepHydrationMetadata(world);
            if(packet.Component==1){if(!RebirthWorkstationCompletionFixture.Hold("metadata",packet.Group.ToString("N")))ReceiveHydrationMetadata(packet,world,connection,callbacks);return;}
            if(waitingMetadataNative.ContainsKey(packet.Group))return;
            if(clientHydrations.ContainsKey(packet.Group)||queueOwnerHydrationsProcessed.ContainsKey(packet.Group))return; // Native Process is never replayed for this selected original.
            double now=UnityEngine.Time.realtimeSinceStartup;
            foreach(var stale in assemblingHydration.Values.Where(value=>!ReferenceEquals(value.World,world)||!ReferenceEquals(value.Connection,connection)||now-value.Created>30d).ToArray())
            {assemblingHydration.Remove(stale.Group);stale.Parts=null;stale.Budget.Dispose();}
            if(!assemblingHydration.TryGetValue(packet.Group,out assembly))
            {
                if(assemblingHydration.Count>=4||clientHydrations.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(32L*1024*1024,out var budget))return;
                assembly=new ClientAssembly{Group=packet.Group,World=world,Connection=connection,Hash=packet.Digest,Total=packet.Total,
                    Parts=new byte[packet.Count][],Budget=budget,Created=now};assemblingHydration.Add(packet.Group,assembly);
            }
            if(!ReferenceEquals(assembly.World,world)||!ReferenceEquals(assembly.Connection,connection)||assembly.Hash!=packet.Digest||assembly.Total!=packet.Total||assembly.Parts.Length!=packet.Count)return;
            if(assembly.Parts[packet.Index]!=null){if(!assembly.Parts[packet.Index].SequenceEqual(packet.Part))return;return;}
            assembly.Parts[packet.Index]=(byte[])packet.Part.Clone();assembly.Received++;if(assembly.Received!=assembly.Parts.Length)return;
            assemblingHydration.Remove(assembly.Group);
            byte[] image=new byte[assembly.Total];int offset=0;foreach(var part in assembly.Parts){Buffer.BlockCopy(part,0,image,offset,part.Length);offset+=part.Length;}assembly.Parts=null;
            if(HydrationHash(image)!=assembly.Hash)throw new InvalidDataException();
            if(WaitForHydrationMetadata(assembly,image,callbacks)){assembly=null;return;}
            using(var transport=ProcessHydrationImage(assembly,image,callbacks)){image=null;}
        }
        catch{if(original!=null&&clientHydrations.TryGetValue(original.Group,out var registered)&&ReferenceEquals(registered,original))
            {original.Unknown=true;original=null;} /* Preserve the known hold after an uncertain actual read. */}
        finally{original?.Close();if(assembly?.Budget!=null&&assembly.Parts==null){assembly.Budget.Dispose();assembly.Budget=null;}}
    }
    private static IDisposable ProcessHydrationImage(ClientAssembly assembly,byte[] image,GameManager callbacks)
    {
        ClientHydration original=null;IDisposable transport=null;
        try
        {
            original=new ClientHydration(assembly.Budget){Group=assembly.Group,World=assembly.World,Connection=assembly.Connection};assembly.Budget=null;
            transport=original.Retain();if(transport==null)throw new InvalidDataException();
            using(var stream=new MemoryStream(image,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                if(reader.ReadUInt32()!=0x31484455)throw new InvalidDataException();byte[] metadata=Exact(reader,64*1024);
                using(var xml=XmlReader.Create(new StringReader(new UTF8Encoding(false,true).GetString(metadata)),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64*1024,MaxCharactersFromEntities=0}))original.Header=XElement.Load(xml);
                if(!TryUseHydrationMetadata(original,original.Header))throw new InvalidDataException();
                int events=reader.ReadInt32();if(events<0||events>4)throw new InvalidDataException();original.NativeEvents=new byte[2*events][];
                for(int index=0;index<original.NativeEvents.Length;index++)original.NativeEvents[index]=Exact(reader,256*1024);
                byte[] native=Exact(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes+64);if(stream.Position!=stream.Length)throw new InvalidDataException();
                if(original.Header.Name=="recipientActualCloseEcho")BindRemoteCloseEcho(original,native,callbacks);
                else if(original.Header.Name=="ordinaryActualEarnedEcho")BindOrdinaryEarnedEcho(original,native,callbacks);
                else if(original.Header.Name=="recipientCachedPhysicalEcho")BindCachedPhysicalEcho(original,native,callbacks);
                else BindHydrationHeader(original,native,callbacks);
            }
            original.Processed=true;RebirthRecyclerPlacedSettlement.ObserveReturnedRecyclerHydration(original);
            NoteHydrationMetadataConsumed(original,assembly.Hash);
            if(!original.Header.Elements("aggregate").Any())original.Close();original=null;
        }
        catch{if(original!=null&&clientHydrations.TryGetValue(original.Group,out var registered)&&ReferenceEquals(registered,original))
            {original.Unknown=true;original=null;} /* Preserve the known hold after an uncertain actual read. */}
        finally{original?.Close();if(assembly?.Budget!=null&&assembly.Parts==null){assembly.Budget.Dispose();assembly.Budget=null;}}
        return transport;
    }
    private static void BindHydrationHeader(ClientHydration original,byte[] native,GameManager callbacks)
    {
        var header=original.Header;
        if(header.Name!="deferredNativeHydration"||(int?)header.Attribute("version")!=1||ReadGuid(header,"group")!=original.Group)throw new InvalidDataException();
        original.Actor=original.World.GetPrimaryPlayer();original.Owner=(string)header.Attribute("owner");original.Creation=(string)header.Attribute("creation");
        original.Deaths=(int)header.Attribute("deaths");original.WorldKey=(string)header.Attribute("world");
        var position=new Vector3i((int)header.Attribute("x"),(int)header.Attribute("y"),(int)header.Attribute("z"));
        original.Station=original.World.GetTileEntity(position) as TileEntityWorkstation;
        if(original.Station==null||original.Station is RebirthCamperStationState||original.Actor.entityId!=(int)header.Attribute("entity")||!original.CurrentIdentity()||
            original.World.GetBlock(position).type!=(int)header.Attribute("block"))throw new InvalidDataException();
        if(!RebirthProductiveEarnedUiCoordinator.TryBindQueueResponse(original,out original.QueueResponse)||clientHydrations.Values.Any(value=>!value.Closed&&ReferenceEquals(value.Station,original.Station)&&
            !RebirthProductiveEarnedUiCoordinator.CanReplaceQueueResponseSource(original.QueueResponse,value)))throw new InvalidDataException();
        if(!PrepareCachedPrimaryHolds(original,native.Length))throw new InvalidDataException("Cached primary hold capacity retained");
        var aggregates=header.Elements("aggregate").ToArray();var members=aggregates.SelectMany(value=>value.Elements("member")).ToArray();
        if(aggregates.Length>4||members.Length>4||members.Length*2!=original.NativeEvents.Length||
            (aggregates.Length==0?header.Element("queueOwners")==null&&header.Element("nativeOriginalReceipts")==null&&header.Element("cachedPrimaries")==null&&header.Element("recyclerReentry")==null&&original.QueueResponse==null:members.Length<1)||
            members.Select(value=>ReadGuid(value,"operation")).Distinct().Count()!=members.Length||
            members.Select(value=>(int)value.Attribute("nativeEvent")).OrderBy(value=>value).Where((value,index)=>value!=index).Any())throw new InvalidDataException();
        foreach(var aggregate in aggregates)
        {
            ReadGuid(aggregate,"nonce");ReadGuid(aggregate,"birth");var operation=ReadGuid(aggregate,"operation");
            if(!aggregate.Elements("member").Any(value=>ReadGuid(value,"operation")==operation)||((string)aggregate.Attribute("recipe"))?.Length>256)throw new InvalidDataException();
            foreach(var member in aggregate.Elements("member"))
                if(((string)member.Attribute("kind")!="uio"&&(string)member.Attribute("kind")!="native")||(int)member.Attribute("contribution")<1||
                    member.Element("creator")==null||member.Element("creator").Value.Length>4096)throw new InvalidDataException();
        }
        var proof=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Group.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(original.World)),
            new XElement("before",new XElement("nativeHydrationOriginal",new XElement(original.MetadataSelector??header),new XElement("nativePacket",HydrationHash(native)),new XElement("nativeEvents",original.NativeEvents.Select(value=>new XElement("image",HydrationHash(value)))))));
        original.Path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("client"),"uiDeferred",original.Group.ToString("N")+".xml");
        if(!RebirthWorkstationCompletionPublication.Write(original.Path,proof))throw new InvalidDataException();original.Evidence=proof;
        var packet=NetPackageManager.GetPackage<NetPackageTileEntity>();original.Packet=packet;
        try
        {
            using(var stream=new MemoryStream(native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);if(reader.ReadUInt16()!=packet.PackageId)throw new InvalidDataException();packet.read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();}
            if(!packet.teWorldPos.Equals(position)||packet.teBlockId!=(int)header.Attribute("block")||packet.handle!=(byte)(int)header.Attribute("handle"))throw new InvalidDataException();
            if(aggregates.Length>0)clientHydrations.Add(original.Group,original);
            else
            {
                if(queueOwnerHydrationsProcessed.Count>=4096||queueOwnerHydrationHistoryMemory==null&&
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(4096L*128+65536,out queueOwnerHydrationHistoryMemory))throw new InvalidDataException();
                queueOwnerHydrationsProcessed.Add(original.Group,original.World);
            }
            hydrating=original;packet.ProcessPackage(original.World,callbacks);ObserveCachedCreatorProcessReturned(original);
        }
        finally{hydrating=null;original.Packet=null;NetPackageManager.FreePackage(packet);}
        if(!original.ReadCompleted||original.Reads!=1)throw new InvalidDataException();
    }
    private static void EnterHydrationNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        if(!run)return;
        var version=nativeReadVersions.GetValue(station,_=>new NativeReadVersion());version.Value=checked(version.Value+1);
        EnterRemoteCloseEchoRead(station,reader,mode,run,version.Value);
        RebirthProductiveEarnedRemoteClient.EnterOriginalEchoRead(station,reader,mode,run,version.Value);
        EnterCachedPhysicalEchoRead(station,reader,mode,run,version.Value);
        if(mode!=StreamModeRead.FromServer||GameManager.Instance?.World?.IsRemote()!=true)return;
        var original=hydrating;
        if(original!=null&&ReferenceEquals(station,original.Station)&&ReferenceEquals(reader.BaseStream,original.Packet?.ms))
        {original.Reads++;original.ReadVersion=version.Value;}
    }
    private static void ObserveHydrationNativeRead(PooledBinaryReader reader,bool run)
    {
        ObserveRemoteCloseEchoRead(reader,run);
        RebirthProductiveEarnedRemoteClient.ObserveOriginalEchoRead(reader,run);
        ObserveCachedPhysicalEchoRead(reader,run);
        var original=hydrating;
        if(!run||original==null||original.Reads!=1||!ReferenceEquals(reader.BaseStream,original.Packet?.ms)||reader.BaseStream.Position!=original.Packet.ms.Length||!original.CurrentIdentity())return;
        try
        {
            original.ReadCompleted=true;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(original.Station,out original.NativeBefore,out original.NativeBeforeOffset))
                    throw new InvalidDataException("Recipient native before observation unavailable");
            }
            if(!ImportCachedPrimaryHolds(original))throw new InvalidDataException("Original cached primary hold unavailable");
            if(!RebirthProductiveEarnedUiQueueOwner.ImportActualNativeHydration(original))throw new InvalidDataException("Queue-owner native hydration unavailable");
            if(!RebirthProductiveEarnedUiQueueOwner.ImportActualNativeOriginalReceipts(original))throw new InvalidDataException("Native original receipt renewal unavailable");
            if(!RebirthRecyclerPlacedSettlement.ImportActualRecyclerHydration(original))throw new InvalidDataException("Recycler native reentry unavailable");
            ImportHydrationReceipts(original); // BEFORE native NotifyListeners/OnOpen.
        }
        catch{original.Unknown=true;/* Native NotifyListeners still runs; the exact known receipt remains held. */}
    }
    internal static bool IsActualQueueResponsePacket(NetPackageTileEntity packet,World world) => hydrating!=null&&ReferenceEquals(hydrating.Packet,packet)&&hydrating.CurrentIdentity()&&
        RebirthProductiveEarnedUiCoordinator.IsActualQueueResponse(hydrating.QueueResponse,packet,world);
    internal static bool IsSuccessfulQueueResponseRead(NetPackageTileEntity packet,World world)=>IsActualQueueResponsePacket(packet,world)&&
        hydrating.ReadCompleted&&hydrating.Reads==1&&!hydrating.Unknown;
    internal static bool IsActualHydrationRead(ClientHydration original)=>ReferenceEquals(hydrating,original)&&original?.ReadCompleted==true&&original.Reads==1&&original.CurrentIdentity();
    private static void ImportHydrationReceipts(ClientHydration original)
    {
        if(!IsActualHydrationRead(original)||original.Station.CraftCompleteList==null||original.Station.CraftCompleteList.Count>64)throw new InvalidDataException();
        foreach(var aggregate in original.Header.Elements("aggregate"))
        {
            var rows=original.Station.CraftCompleteList.Where(value=>HydrationHash(Row(value))==(string)aggregate.Attribute("native")).ToArray();
            if(rows.Length!=1||rows[0].CrafterEntityID!=original.Actor.entityId||rows[0].RecipeName!=(string)aggregate.Attribute("recipe"))throw new InvalidDataException();
            var members=aggregate.Elements("member").Select(value=>ImportedContribution.CreateFromActualRead(original,aggregate,value,rows[0])).ToArray();
            original.Members.AddRange(members);
            if(!RebirthProductiveEarnedUiQueueOwner.ImportActualHydratedAggregate(original,aggregate,rows[0],members))throw new InvalidDataException();
        }
    }
    private static bool CanUseHydratedNativeRewards(TileEntityWorkstation station,EntityPlayerLocal player)
    {
        foreach(var original in clientHydrations.Values)
        {
            if(original.Closed||!ReferenceEquals(original.Station,station))continue;
            try
            {
                if(station.CraftCompleteList==null||station.CraftCompleteList.Count>64)return false;
                bool known=station.CraftCompleteList.Any(row=>original.Members.Any(member=>ReferenceEquals(member.Receipt,row))||
                    original.Header.Elements("aggregate").Any(value=>(string)value.Attribute("native")==HydrationHash(Row(row))));
                if(!known)continue;
                if(station.CraftCompleteList.Any(row=>row.CrafterEntityID==player.entityId&&!original.Members.Any(member=>ReferenceEquals(member.Receipt,row)))||
                    original.Members.Any(member=>member.Token?.CurrentAggregateMembership()!=true))return false;
                if(original.Unknown||!original.ReadCompleted||!original.CurrentSource()||!ReferenceEquals(original.Actor,player)||
                    LockManager.Instance?.IsLockedByLocalPlayer(station,0)!=true||!original.InvokingCheck||!original.BeforeReady||!original.Attempted)return false;
            }
            catch{return false;}
        }
        return true;
    }
}

