using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthNpcMetabolismCustody
{
    private sealed class BoundedOwnerStream:MemoryStream
    {
        public override void Write(byte[] bytes,int offset,int count){if(count<0||Position+count>16L*1024*1024)throw new InvalidDataException("NPC owner PDS exceeds reserved image bound");base.Write(bytes,offset,count);}
        public override void WriteByte(byte value){if(Position>=16L*1024*1024)throw new InvalidDataException("NPC owner PDS exceeds reserved image bound");base.WriteByte(value);}
    }
    private static byte[] CaptureNativeOwnerImage(PlayerDataFile data)
    {
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024,out var scratch))throw new InvalidDataException("NPC owner native serialization scratch unavailable");
        using var retainedScratch=scratch;
        using var stream=new BoundedOwnerStream();using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);writer.Write('t');writer.Write('t');writer.Write('p');writer.Write((byte)0);writer.Write((byte)PlayerDataFile.cFileVersion);data.Write(writer,StreamModeWrite.Persistency);writer.Flush();}
        return stream.ToArray();
    }
    // Same bounded native serializer for the explicit player-source purpose.
    // This returns bytes only; it grants no source/effect/reentry permission.
    internal static byte[] CapturePlayerConsumptionNative(PlayerDataFile data)=>CaptureNativeOwnerImage(data);
    private sealed class NpcOwnerLoad
    {
        internal PlayerDataFile Data;internal World World;internal EntityPlayer Actor;internal string Save,Path,Account;
        internal Stream Stream;internal byte[] Native,Belt,Bag,Buffs;internal int Deaths;internal bool Dead,Entered,ReadReturned,Loaded,HydrationEntered,Hydrated,PlayerConsumption;internal string ArchiveRoot;
        internal IDisposable Memory;internal readonly Dictionary<string,string> Originals=new Dictionary<string,string>();
        internal readonly HashSet<string> Lives=new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<Guid> Ready=new HashSet<Guid>();
        internal readonly Dictionary<Guid,string> MetadataCandidates=new Dictionary<Guid,string>();
        internal readonly Dictionary<string,string> LiveByDirectory=new Dictionary<string,string>();
    }
    [ThreadStatic]private static NpcOwnerLoad enteredNpcOwner;
    private static readonly Dictionary<PlayerDataFile,NpcOwnerLoad> nativeOwners=new Dictionary<PlayerDataFile,NpcOwnerLoad>();
    internal static object EnterNativeOwnerLoad(PlayerDataFile data,string directory,string account,bool run)
    {
        NpcOwnerLoad scope=null;
        try
        {
            if(!run||!ThreadManager.IsMainThread()||enteredNpcOwner!=null||data==null)return null;
            var world=GameManager.Instance?.World;if(world==null||world.IsRemote())return null;
            RetireNativeOwnerObservations();if(nativeOwners.Count>=4)return null;
            string save=Path.GetFullPath(GameIO.GetSaveGameDir()),root=Path.Combine(save,"RebirthData","NpcConsumption"),archiveRoot=Path.Combine(save,"RebirthData","NpcConsumptionTerminal");
            bool playerConsumption=RebirthConsumptionDurableDelivery.InventoryProtocolActive&&Directory.Exists(Path.Combine(save,"RebirthData","PlayerConsumption",RebirthStablePlayerIdentity.ComputeStorageKey(account)));
            if(!Directory.Exists(root)&&!Directory.Exists(archiveRoot)&&!playerConsumption)return null;
            RequirePath(root);RequirePath(archiveRoot);string path=Path.GetFullPath(Path.Combine(directory,account+"."+PlayerDataFile.EXT));
            if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var memory))return null;
            scope=new NpcOwnerLoad{Data=data,World=world,Save=save,Path=path,Account=account,Memory=memory,ArchiveRoot=archiveRoot,PlayerConsumption=playerConsumption};
            var directories=Directory.Exists(root)?Directory.EnumerateDirectories(root).Take(257).ToArray():Array.Empty<string>();if(directories.Length>256)throw new InvalidDataException("NPC consumption discovery cap exceeded");
            long retainedCharacters=0;
            foreach(string originalDirectory in directories)
            {
                var prepared=ReadRecord(Path.Combine(originalDirectory,"prepared.xml"));string text=(string)prepared.Element("original");if(!Stage(originalDirectory,"prepared",text))throw new InvalidDataException("Unqualified NPC original");
                using var parser=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});var original=XElement.Load(parser);
                if(original.Name!="npcConsumptionOriginal"||!Guid.TryParseExact((string)original.Attribute("operation"),"N",out var operation)||Path.GetFileName(originalDirectory)!=operation.ToString("N")||Path.GetFullPath((string)original.Attribute("save"))!=save)throw new InvalidDataException("NPC original save identity changed");
                if((string)original.Attribute("owner")==account)
                {
                    if(PositiveNoEffect(originalDirectory,text)){scope.MetadataCandidates.Add(operation,originalDirectory);continue;}
                    if(IsPositiveTerminal(originalDirectory,text,original)){scope.MetadataCandidates.Add(operation,originalDirectory);continue;}
                    retainedCharacters=checked(retainedCharacters+text.Length);
                    if(scope.Originals.Count>=32||retainedCharacters>8L*1024*1024)throw new InvalidDataException("NPC owner original residency cap exceeded");
                    scope.Originals.Add(originalDirectory,text);
                    scope.Lives.Add((string)original.Attribute("creation")+"\0"+(string)original.Attribute("deaths"));
                    scope.LiveByDirectory.Add(originalDirectory,(string)original.Attribute("creation")+"\0"+(string)original.Attribute("deaths"));
                }
            }
            if(scope.Originals.Count==0&&scope.MetadataCandidates.Count==0&&!Directory.Exists(archiveRoot)&&!playerConsumption)throw new InvalidDataException("No original owner registration");
            enteredNpcOwner=scope;return scope;
        }
        catch{scope?.Memory?.Dispose();return null;}
    }
    internal static void ObserveNativeOwnerOpen(string path,Stream stream,bool run)
    {
        var scope=enteredNpcOwner;if(scope==null||!run||stream==null||scope.Stream!=null)return;
        try{if(Path.GetFullPath(path)==scope.Path&&stream.CanRead&&stream.CanSeek&&stream.Position==0)scope.Stream=stream;}catch{}
    }
    internal static object EnterNativeOwnerRead(PlayerDataFile data,PooledBinaryReader reader,uint version,StreamModeRead mode,bool run)
    {
        if(mode==StreamModeRead.FromServer)return EnterClientNativeRead(data,reader,version,mode,run);
        var scope=enteredNpcOwner;
        if(!run||scope==null||scope.Entered||!ReferenceEquals(scope.Data,data)||mode!=StreamModeRead.Persistency||version!=PlayerDataFile.cFileVersion)return null;
        try
        {
            var stream=reader?.BaseStream;if(stream==null||!ReferenceEquals(stream,scope.Stream)||!stream.CanSeek||stream.Position!=5||stream.Length<6||stream.Length>16*1024*1024)return null;
            var bytes=new byte[checked((int)stream.Length)];try{stream.Position=0;int offset=0;while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count<1)throw new EndOfStreamException();offset+=count;}}finally{stream.Position=5;}
            scope.Native=bytes;scope.Entered=true;return scope;
        }
        catch{return null;}
    }
    internal static void ObserveNativeOwnerRead(PooledBinaryReader reader,bool run,object state)
    {
        if(state is NpcClientRead){ObserveClientNativeRead(reader,run,state);return;}
        if(state is not NpcOwnerLoad scope)return;
        try{scope.ReadReturned=run&&ReferenceEquals(scope,enteredNpcOwner)&&ReferenceEquals(reader.BaseStream,scope.Stream)&&scope.Stream.Position==scope.Stream.Length;}catch{}
    }
    internal static void ObserveNativeOwnerLoad(PlayerDataFile data,bool run,object state)
    {
        if(state is not NpcOwnerLoad scope)return;
        try
        {
            if(!run||!ReferenceEquals(scope,enteredNpcOwner)||!scope.ReadReturned||!data.bLoaded||!ReferenceEquals(scope.Data,data)||
                !scope.Native.SequenceEqual(CaptureNativeOwnerImage(data))||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(scope.Path,scope.Native))return;
            scope.Belt=data.inventoryData.ToArray();scope.Bag=data.bagData.ToArray();scope.Buffs=data.buffData.ToArray();scope.Deaths=data.deaths;scope.Dead=data.bDead;
            if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryNpcConsumptionKeys(scope.Buffs,out var operations))return;
            foreach(var operation in operations)
            {
                string archivedDirectory=Path.Combine(scope.ArchiveRoot,operation.ToString("N"));RequirePath(archivedDirectory);
                if(scope.Originals.Keys.Any(path=>Path.GetFileName(path)==operation.ToString("N")))
                {if(Directory.Exists(archivedDirectory))throw new InvalidDataException("Original pending/archive registrations conflict");continue;}
                if(!scope.MetadataCandidates.TryGetValue(operation,out var terminalDirectory))
                {if(!Directory.Exists(archivedDirectory))continue;terminalDirectory=archivedDirectory;}
                else if(Directory.Exists(archivedDirectory))throw new InvalidDataException("Original metadata namespaces conflict");
                var prepared=ReadRecord(Path.Combine(terminalDirectory,"prepared.xml"));string text=(string)prepared.Element("original");var original=ParseOriginal(text);
                if(!Stage(terminalDirectory,"prepared",text)||!Guid.TryParseExact((string)original.Attribute("operation"),"N",out var archivedOperation)||archivedOperation!=operation||(string)original.Attribute("save")!=scope.Save||
                    scope.Originals.Count>=32||scope.Originals.Values.Sum(value=>(long)value.Length)+text.Length>8L*1024*1024||!IsPositiveTerminal(terminalDirectory,text,original)&&!PositiveNoEffect(terminalDirectory,text)||(string)original.Attribute("owner")!=scope.Account)throw new InvalidDataException("NPC terminal registration changed");
                scope.Originals.Add(terminalDirectory,text);
            }
            scope.MetadataCandidates.Clear();if(scope.Originals.Count==0&&!scope.PlayerConsumption)return;
            nativeOwners.Add(data,scope);scope.Loaded=true;
        }
        catch{}
        finally{EndNativeOwnerLoad(state);}
    }
    internal static void EndNativeOwnerLoad(object state)
    {
        if(state is not NpcOwnerLoad scope)return;
        if(ReferenceEquals(enteredNpcOwner,scope))enteredNpcOwner=null;scope.Stream=null;
        if(!scope.Loaded)ReleaseNativeOwner(scope);
    }
    private static void ReleaseNativeOwner(NpcOwnerLoad scope)
    {
        if(scope.Data!=null)nativeOwners.Remove(scope.Data);scope.Memory?.Dispose();scope.Memory=null;scope.Native=null;scope.Belt=null;scope.Bag=null;scope.Buffs=null;
        scope.Originals.Clear();scope.Lives.Clear();scope.LiveByDirectory.Clear();scope.Ready.Clear();scope.MetadataCandidates.Clear();scope.Data=null;scope.Actor=null;scope.World=null;scope.Stream=null;
    }
    private static bool NativeOwnerInput(NpcOwnerLoad scope,PlayerDataFile data)=>data!=null&&data.deaths==scope.Deaths&&data.bDead==scope.Dead&&
        data.inventoryData.ToArray().SequenceEqual(scope.Belt)&&data.bagData.ToArray().SequenceEqual(scope.Bag)&&data.buffData.ToArray().SequenceEqual(scope.Buffs);
    internal static void EnterNativeOwnerHydration(PlayerDataFile data,EntityPlayer actor,StreamModeRead mode,bool run)
    {
        try{EnterClientNativeHydration(data,actor,mode,run);}catch{}
        if(!run||!ThreadManager.IsMainThread()||mode!=StreamModeRead.Persistency||actor==null)return;
        try{if(nativeOwners.TryGetValue(data,out var scope)&&scope.Loaded&&!scope.Hydrated&&!scope.HydrationEntered&&ReferenceEquals(scope.World,actor.world)&&ReferenceEquals(scope.World,GameManager.Instance?.World)&&NativeOwnerInput(scope,data)){scope.Actor=actor;scope.HydrationEntered=true;}}catch{}
    }
    internal static void ObserveNativeOwnerHydration(PlayerDataFile data,EntityPlayer actor,bool run)
    {
        try{ObserveClientNativeHydration(data,actor,run);}catch{}
        if(!ThreadManager.IsMainThread())return;
        try
        {
            if(nativeOwners.TryGetValue(data,out var scope)&&run&&scope.HydrationEntered&&ReferenceEquals(scope.Actor,actor)&&ReferenceEquals(actor.world,scope.World)&&NativeOwnerInput(scope,data))
            {
                scope.Hydrated=true;
                if(scope.PlayerConsumption&&RebirthConsumptionDurableDelivery.ObserveInventoryServerNativeHydration(actor,data,scope.Native,scope.Account,scope.Path)&&scope.Originals.Count==0)
                    ReleaseNativeOwner(scope); // explicit player load has its own retained copy; no NPC original is discarded.
            }
        }catch{}
    }
    internal static void EndNativeOwnerHydration(PlayerDataFile data)
    {if(data!=null&&nativeOwners.TryGetValue(data,out var scope)&&!scope.Hydrated){scope.HydrationEntered=false;scope.Actor=null;}}
    private static void RetireNativeOwnerObservations()
    {
        foreach(var scope in nativeOwners.Values.Where(value=>!ReferenceEquals(value.World,GameManager.Instance?.World)||value.Hydrated&&value.Actor!=null&&!ReferenceEquals(value.World.GetEntity(value.Actor.entityId),value.Actor)).ToArray())ReleaseNativeOwner(scope);
    }
    private static bool HoldsNativeOwner(EntityPlayer actor)=>actor!=null&&nativeOwners.Values.Any(scope=>ReferenceEquals(scope.Actor,actor)&&ReferenceEquals(scope.World,actor.world)&&
        scope.Lives.Contains(RebirthCamperCharacterAccess.CurrentCreation(actor)+"\0"+actor.Died));
}
