using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;

public static partial class RemoteResourceTransactions
{
    internal sealed class ActionTargetNativeOriginal
    {
        internal Guid Operation;internal World World;internal string Save,NativeWorld,Region,Directory,Original,MarkerKey;
        internal ulong Recovery;internal Vector3i Position;internal IRegionFileChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal byte[] Head,Snapshot,Tile,Payload,Marker,MarkerAfter;internal long Offset;internal TileEntityType Type;internal ActionPayment Payment;
        internal Chunk Chunk;internal TileEntityPowered Body;internal ChunkCustomData NativeMarker;internal IDisposable Memory;internal volatile bool Active=true;internal bool Installed,BeforeFirstGrant,LoadedAfterPublication,NativeSavedOrigin;internal int Loads;
        internal bool Current()=>Active&&Installed&&Loads==0&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World?.IsRemote()==false&&World.worldState.Guid==NativeWorld&&
            Save==Path.GetFullPath(GameIO.GetSaveGameDir())&&Recovery==RebirthNpcRecoveryGeneration.RequireCurrent()&&
            World.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,Util)&&
            ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,Access)&&ReferenceEquals(World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&!Chunk.InProgressUnloading&&
            ReferenceEquals(Chunk.GetTileEntity(Body.localChunkPos),Body)&&ReferenceEquals(Body.GetChunk(),Chunk)&&Chunk.ChunkCustomData.dict.TryGetValue(MarkerKey,out var marker)&&
            ReferenceEquals(marker,NativeMarker)&&marker.data!=null&&marker.data.SequenceEqual(MarkerAfter??Marker)&&HeadCurrent();
        private bool HeadCurrent()
        {
            if(Payment==null)return ActionEnvelopeFileEquals(Path.Combine(Directory,"current.xml"),Head);
            if(Payment.Retired||Payment.Original!=Original||!actionPayments.Contains(Payment))return false;
            try{ValidateActionHead(Payment);return true;}catch{return false;}
        }
    }
    private sealed class ActionTargetNativeLoading
    {
        internal ActionTargetNativeOriginal Original;internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityPowered Body;
        internal bool Stream,Factory,BaseRead,TopRead,Instantiated;
    }
    private static readonly object actionTargetLoadGate=new object();
    private static readonly List<ActionTargetNativeOriginal> actionTargetNativeOriginals=new List<ActionTargetNativeOriginal>();
    [ThreadStatic]private static ActionTargetNativeLoading[] actionTargetLoading;
    [ThreadStatic]private static ActionTargetNativeLoading actionTargetInstantiating;
    private static void ReleaseActionTargetNativeGraph(ActionTargetNativeOriginal original)
    {
        if(original.Active||original.Loads!=0)return;
        original.Head=original.Snapshot=original.Tile=original.Payload=original.Marker=original.MarkerAfter=null;original.Body=null;original.Chunk=null;original.NativeMarker=null;original.Payment=null;
        original.World=null;original.Util=null;original.Access=null;original.Original=null;original.Memory?.Dispose();original.Memory=null;
    }
    private static void RetireCompletedActionTarget(ActionPayment payment)
    {
        var original=payment.RestartTargetSource;
        if(original==null||!payment.Terminal||payment.RemoteOwner!=null&&!payment.RemoteTerminalReleased)return;
        lock(actionTargetLoadGate)
        {
            payment.RestartTargetSource=null;original.Active=false;original.Payment=null;actionTargetNativeOriginals.Remove(original);ReleaseActionTargetNativeGraph(original);
        }
    }
    internal static bool ObservePaidActionTargetMarker(ActionPayment payment)
    {
        var source=payment?.RestartTargetSource;if(source==null)return true;
        if(!ThreadManager.IsMainThread()||!source.Active||!source.Installed||source.Loads!=0||!ReferenceEquals(source.Payment,payment)||!payment.PublicationEntered||payment.PublicationReturned||
            !ReferenceEquals(source.Body,payment.ServiceTarget)||!ReferenceEquals(source.Chunk.GetTileEntity(source.Body.localChunkPos),source.Body)||
            !source.Chunk.ChunkCustomData.dict.TryGetValue(source.MarkerKey,out var marker)||marker.data==null||marker.data.Length<22||marker.data.Length>16406||
            !RebirthElectricalSourceIdentity.Matches(payment.World,payment.ServiceCandidate))return false;
        // The actual owning Defer just returned in the once-only publication
        // scope. Preserve historical Marker and adopt only this original's new
        // native marker object; it is not a source or payment permit.
        source.MarkerAfter=(byte[])marker.data.Clone();source.NativeMarker=marker;return true;
    }
    internal static bool ObserveRestoredReturnedActionTargetMarker(ActionPayment payment)
    {
        var source=payment?.RestartTargetSource;
        if(!ThreadManager.IsMainThread()||source==null||!source.Active||!source.Installed||source.Loads!=0||!ReferenceEquals(source.Payment,payment)||
            !payment.PublicationEntered||!payment.PublicationReturned||!payment.ReturnedPublicationNeedsRestore||!payment.Events.ContainsKey("publication-returned")||
            !ReferenceEquals(source.Body,payment.ServiceTarget)||!source.Chunk.ChunkCustomData.dict.TryGetValue(source.MarkerKey,out var marker)||marker.data==null||
            !RebirthElectricalSourceIdentity.Matches(payment.World,payment.ServiceCandidate))return false;
        source.MarkerAfter=(byte[])marker.data.Clone();source.NativeMarker=marker;return true;
    }
    private static XElement ReadActionBoundedXml(byte[] bytes,int maximum)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>maximum)throw new InvalidDataException();
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum};
        using(var stream=new MemoryStream(bytes,false))using(var reader=XmlReader.Create(stream,settings))
        {int nodes=0;while(reader.Read())if(++nodes>32768||reader.Depth>24||reader.AttributeCount>32)throw new InvalidDataException();}
        using(var stream=new MemoryStream(bytes,false))using(var reader=XmlReader.Create(stream,settings))return XElement.Load(reader);
    }
    private static byte[] ActionNativeBase64(XElement element,int maximum)
    {
        string text=element?.Value;if(element==null||element.HasElements||text.Length<1||text.Length>((maximum+2L)/3)*4)throw new InvalidDataException();
        byte[] bytes=Convert.FromBase64String(text);if(bytes.Length>maximum||Convert.ToBase64String(bytes)!=text)throw new InvalidDataException();return bytes;
    }
    private static bool ActionNativeSpan(Stream stream,long offset,byte[] bytes,int start=0,int length=-1)
    {
        if(bytes==null)return false;if(length<0)length=bytes.Length-start;
        if(stream==null||!stream.CanSeek||!stream.CanRead||offset<0||start<0||length<0||start>bytes.Length-length||offset>stream.Length-length)return false;
        long position=stream.Position;byte[] buffer=new byte[32768];
        try
        {
            stream.Position=offset;int index=0;
            while(index<length){int count=stream.Read(buffer,0,Math.Min(buffer.Length,length-index));if(count<1)return false;for(int n=0;n<count;n++)if(buffer[n]!=bytes[start+index+n])return false;index+=count;}
            return true;
        }
        finally{stream.Position=position;}
    }
    internal static void DiscoverActionPaymentNativeTargets(RegionFileManager manager)
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World is not World world||world.IsRemote()||manager?.snapshotUtil==null||manager.regionFileAccess is not RegionFileAccessSectorBased||
            Path.GetFullPath(manager.saveDirectory)!=Path.GetFullPath(GameIO.GetSaveGameRegionDir()))return;
        lock(actionTargetLoadGate)
        {
            foreach(var old in actionTargetNativeOriginals.Where(value=>!ReferenceEquals(value.World,world)||!ReferenceEquals(value.Util,manager.snapshotUtil)).ToArray())
            {old.Active=false;actionTargetNativeOriginals.Remove(old);ReleaseActionTargetNativeGraph(old);}
            string save=Path.GetFullPath(GameIO.GetSaveGameDir()),root=Path.Combine(save,"RebirthData","ActionPayments");ActionPath(root);if(!Directory.Exists(root))return;
            foreach(string directory in PendingActionTargetDirectories(save))
            {
                if(actionTargetNativeOriginals.Count>=4)return;
                ActionTargetNativeOriginal candidate=null;
                try
                {
                    ActionPath(directory);if(!Guid.TryParseExact(Path.GetFileName(directory),"N",out var operation)||actionTargetNativeOriginals.Any(value=>value.Operation==operation))continue;
                    string headPath=Path.Combine(directory,"current.xml"),artifactPath=Path.Combine(directory,"target-before-saved.native.xml");ActionPath(headPath);ActionPath(artifactPath);
                    long headLength=new FileInfo(headPath).Length,artifactLength=new FileInfo(artifactPath).Length;
                    if(headLength<1||headLength>24L*1024*1024||artifactLength<1||artifactLength>48L*1024*1024||
                        !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*headLength+6*artifactLength+65536),out var memory))continue;
                    candidate=new ActionTargetNativeOriginal{Operation=operation,World=world,Save=save,NativeWorld=world.worldState.Guid,Recovery=RebirthNpcRecoveryGeneration.RequireCurrent(),
                        Region=Path.GetFullPath(manager.saveDirectory),Directory=directory,Util=manager.snapshotUtil,Access=manager.regionFileAccess,Memory=memory};
                    candidate.Head=ActionBytes(headPath,(int)headLength);var head=ReadActionBoundedXml(candidate.Head,24*1024*1024);
                    // Both heads are positive original checkpoints. The saved
                    // target Before head precedes paying/any financial intent;
                    // paid precedes the original publication intent.
                    string stage=(string)head.Attribute("stage");
                    bool returned=stage=="publication-returned"||stage=="service-after-saved"||stage=="target-after-saved"||stage=="training-saved"||stage=="training-not-required";
                    if(head.Name!="actionPaymentEvent"||(int?)head.Attribute("version")!=1||stage!="paid"&&stage!="target-before-saved"&&!returned)continue;
                    candidate.BeforeFirstGrant=stage=="target-before-saved";
                    candidate.Original=head.Element("original")?.Value;var original=ReadRemoteActionOriginal(candidate.Original);var target=original.Element("electrical");
                    string pending=ActionPendingTargetPath(save,operation);
                    if(File.Exists(pending)&&!ActionEnvelopeFileEquals(pending,new UTF8Encoding(false,true).GetBytes(candidate.Original)))continue;
                    if(original.Name!="resourceActionOriginal"||(string)original.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("world")!=world.worldState.Guid||
                        (string)original.Attribute("save")!=save||(ulong?)original.Attribute("recovery")!=candidate.Recovery||target==null)continue;
                    candidate.Position=new Vector3i((int)target.Attribute("x"),(int)target.Attribute("y"),(int)target.Attribute("z"));candidate.MarkerKey=RebirthElectricalSourceIdentity.NativeKey(candidate.Position);
                    string afterArtifact=Path.Combine(directory,"target-after-saved.native.xml");
                    if(returned&&File.Exists(afterArtifact))
                    {
                        ActionPath(afterArtifact);artifactLength=new FileInfo(afterArtifact).Length;
                        if(artifactLength<1||artifactLength>48L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(candidate.Memory,checked(8*headLength+6*artifactLength+65536)))continue;
                        artifactPath=afterArtifact;candidate.LoadedAfterPublication=true;
                    }
                    byte[] artifact=ReadActionMaterial(artifactPath,(int)artifactLength);if(artifact.LongLength!=artifactLength)continue;
                    var image=ReadActionBoundedXml(artifact,48*1024*1024);
                    if(image.Name!="nativeActionSource"||(int?)image.Attribute("version")!=1||image.Elements().Count()!=4)continue;
                    candidate.Offset=(long)image.Attribute("offset");candidate.Snapshot=ActionNativeBase64(image.Element("snapshot"),RebirthStationSnapshotEvidence.MaximumSnapshotBytes);
                    candidate.Tile=ActionNativeBase64(image.Element("tile"),4*1024*1024);candidate.Payload=ActionNativeBase64(image.Element("payload"),RebirthStationRegionPayload.MaximumPayloadBytes);
                    var markers=image.Element("markers")?.Elements("marker").ToArray();if(markers?.Length!=1||(string)markers[0].Attribute("key")!=candidate.MarkerKey)continue;
                    candidate.Marker=ActionNativeBase64(markers[0].Element("data"),32768);byte[] markerSpan=ActionNativeBase64(markers[0].Element("span"),65536);long markerOffset=(long)markers[0].Attribute("offset");
                    if(candidate.Offset<9||candidate.Offset>candidate.Snapshot.Length-candidate.Tile.Length||candidate.Marker.Length<22||markerOffset<8||markerOffset>candidate.Snapshot.Length-markerSpan.Length||
                        !Guid.TryParseExact((string)target.Attribute("incarnation"),"N",out var birth)||!candidate.Marker.Take(16).SequenceEqual(birth.ToByteArray())||
                        BitConverter.ToUInt16(candidate.Marker,16)!=(int)target.Attribute("block"))continue;
                    if(candidate.LoadedAfterPublication&&!candidate.Marker.SequenceEqual(RebirthElectricalSourceIdentity.DescribeDeferredMarker(ReadActionElectricalCandidate(target,candidate.Position))))continue;
                    using(var stream=new MemoryStream(candidate.Snapshot,false))if(!ActionNativeSpan(stream,candidate.Offset,candidate.Tile)||!ActionNativeSpan(stream,markerOffset,markerSpan))continue;
                    if(!RebirthStationChunkInflate.TryDecode(candidate.Payload,candidate.Snapshot.Length-8,out var inflated)||!inflated.SequenceEqual(candidate.Snapshot.Skip(8)))continue;
                    candidate.Type=(TileEntityType)candidate.Snapshot[(int)candidate.Offset-1];
                    long retained=checked(candidate.Head.LongLength+candidate.Snapshot.LongLength+candidate.Tile.LongLength+candidate.Payload.LongLength+candidate.Marker.LongLength+2L*candidate.Original.Length+65536);
                    if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(candidate.Memory,retained))continue;
                    actionTargetNativeOriginals.Add(candidate);candidate=null;
                }
                catch{/* Unknown saved original never grants a live target. */}
                finally{candidate?.Memory?.Dispose();}
            }
        }
    }
    internal static object EnterActionPaymentTargetLoad(ChunkSnapshotUtil util,string dir,long key,bool ran)
    {
        if(!ran||actionTargetLoading!=null)return null;
        lock(actionTargetLoadGate)
        {
            var matches=actionTargetNativeOriginals.Where(value=>value.Active&&!value.Installed&&ReferenceEquals(value.Util,util)&&value.Region==Path.GetFullPath(dir)&&
                (value.Position.x>>4)==WorldChunkCache.extractX(key)&&(value.Position.z>>4)==WorldChunkCache.extractZ(key)).ToArray();
            if(matches.Length<1||matches.Length>4)return null;
            foreach(var match in matches)match.Loads++;
            return actionTargetLoading=matches.Select(value=>new ActionTargetNativeLoading{Original=value}).ToArray();
        }
    }
    internal static void ObserveActionPaymentTargetLoadStream(RegionFileChunkReader source,PooledBinaryReader reader,string dir,int x,int z,string ext,uint version,bool ran)
    {
        if(!ran||actionTargetLoading==null||reader?.BaseStream==null)return;
        foreach(var loading in actionTargetLoading)
        {
            var original=loading.Original;
            if(!original.Active||original.Util is not ChunkSnapshotUtil nativeUtil||!ReferenceEquals(nativeUtil.chunkReader,source)||!ReferenceEquals(source.regionFileAccess,original.Access)||Path.GetFullPath(dir)!=original.Region||
                x!=(original.Position.x>>4)||z!=(original.Position.z>>4)||ext!="7rg"||version!=47||!ReferenceEquals(reader,source.loadChunkReader)||
                !ReferenceEquals(reader.BaseStream,source.loadChunkMemoryStream)||reader.BaseStream.Position!=0||reader.BaseStream.Length!=original.Snapshot.Length-8)continue;
            // Compare the actual complete decompressed native load, preserving
            // its stream position; archive bytes are never fed to native read.
            if(ActionNativeSpan(reader.BaseStream,0,original.Snapshot,8,original.Snapshot.Length-8)){loading.Reader=reader;loading.Stream=true;}
        }
    }
    internal static object EnterActionPaymentTargetInstantiate(PooledBinaryReader reader,global::StreamModeRead mode,TileEntityType type,Chunk chunk,bool ran)
    {
        if(!ran||mode!=global::StreamModeRead.Persistency||actionTargetLoading==null||actionTargetInstantiating!=null||chunk==null)return null;
        var matches=actionTargetLoading.Where(value=>value.Stream&&!value.Instantiated&&ReferenceEquals(value.Reader,reader)&&value.Original.Type==type&&
            reader.BaseStream.Position==value.Original.Offset-8&&ActionNativeSpan(reader.BaseStream,reader.BaseStream.Position,value.Original.Tile)).ToArray();
        if(matches.Length!=1)return null;var loading=matches[0];loading.Chunk=chunk;loading.Factory=true;actionTargetInstantiating=loading;return loading;
    }
    internal static void ObserveActionPaymentPoweredNativeRead(TileEntityPowered tile,PooledBinaryReader reader,global::StreamModeRead mode,bool ran)
        =>ObserveActionPaymentTargetRead(tile,reader,mode,AccessTools.Method(typeof(TileEntityPowered),"read",new[]{typeof(PooledBinaryReader),typeof(global::StreamModeRead)}),ran);
    private static void ObserveActionPaymentTargetRead(TileEntityPowered tile,PooledBinaryReader reader,global::StreamModeRead mode,MethodBase method,bool ran)
    {
        var loading=actionTargetInstantiating;
        if(loading==null||!ran||!loading.Factory||mode!=global::StreamModeRead.Persistency||!ReferenceEquals(reader,loading.Reader)||!ReferenceEquals(tile.GetChunk(),loading.Chunk))return;
        if(method.DeclaringType==typeof(TileEntityPowered))loading.BaseRead=true;
        if(method==AccessTools.Method(tile.GetType(),"read",new[]{typeof(PooledBinaryReader),typeof(global::StreamModeRead)})&&reader.BaseStream.Position==loading.Original.Offset-8+loading.Original.Tile.Length)
        {loading.Body=tile;loading.TopRead=true;}
    }
    internal static void ObserveActionPaymentTargetInstantiate(TileEntity result,PooledBinaryReader reader,Chunk chunk,bool ran,object state)
    {
        if(state is ActionTargetNativeLoading loading&&ReferenceEquals(loading,actionTargetInstantiating)&&ran&&loading.Factory&&loading.BaseRead&&loading.TopRead&&
            ReferenceEquals(result,loading.Body)&&ReferenceEquals(result?.GetChunk(),chunk)&&ReferenceEquals(chunk,loading.Chunk)&&ReferenceEquals(reader,loading.Reader)&&
            reader.BaseStream.Position==loading.Original.Offset-8+loading.Original.Tile.Length)loading.Instantiated=true;
    }
    internal static void EndActionPaymentTargetInstantiate(object state,Exception error)
    {if(state is ActionTargetNativeLoading loading){if(error!=null)loading.Instantiated=false;loading.Factory=false;if(ReferenceEquals(actionTargetInstantiating,loading))actionTargetInstantiating=null;}}
    internal static void ObserveActionPaymentTargetLoad(Chunk result,bool ran,object state)
    {
        if(state is not ActionTargetNativeLoading[] entries||!ReferenceEquals(entries,actionTargetLoading)||!ran)return;
        foreach(var loading in entries)
        {
            var original=loading.Original;
            if(!original.Active||!loading.Instantiated||!loading.BaseRead||!loading.TopRead||!ReferenceEquals(result,loading.Chunk)||!ReferenceEquals(result?.GetTileEntity(loading.Body.localChunkPos),loading.Body)||
                !ReferenceEquals(loading.Body.GetChunk(),result)||loading.Reader.BaseStream.Position!=original.Snapshot.Length-8||
                !result.ChunkCustomData.dict.TryGetValue(original.MarkerKey,out var marker)||marker.TriggerWriteDataDelegate!=null||marker.isSavedToNetwork||marker.expiresInWorldTime!=ulong.MaxValue||marker.data==null||!marker.data.SequenceEqual(original.Marker))continue;
            original.Body=loading.Body;original.Chunk=result;original.NativeMarker=marker;original.Installed=true;
        }
    }
    internal static void EndActionPaymentTargetLoad(object state)
    {
        if(state is not ActionTargetNativeLoading[] entries)return;
        if(ReferenceEquals(entries,actionTargetLoading))actionTargetLoading=null;actionTargetInstantiating=null;
        lock(actionTargetLoadGate)foreach(var entry in entries)
        {var original=entry.Original;entry.Original=null;entry.Reader=null;entry.Chunk=null;entry.Body=null;if(original!=null){original.Loads--;ReleaseActionTargetNativeGraph(original);}}
    }
    [HarmonyPatch]
    private static class ActionPaymentDerivedPoweredRead
    {
        private static IEnumerable<MethodBase> TargetMethods()=>typeof(TileEntityPowered).Assembly.GetTypes().Where(type=>type!=typeof(TileEntityPowered)&&typeof(TileEntityPowered).IsAssignableFrom(type))
            .Select(type=>AccessTools.DeclaredMethod(type,"read",new[]{typeof(PooledBinaryReader),typeof(global::StreamModeRead)})).Where(method=>method!=null).Distinct();
        [HarmonyPriority(Priority.Last)]private static void Postfix(TileEntityPowered __instance,PooledBinaryReader _br,global::StreamModeRead _eStreamMode,MethodBase __originalMethod,bool __runOriginal)
        {try{ObserveActionPaymentTargetRead(__instance,_br,_eStreamMode,__originalMethod,__runOriginal);}catch{}}
    }
}
