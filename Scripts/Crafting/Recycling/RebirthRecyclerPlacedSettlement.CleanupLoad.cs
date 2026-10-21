using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // This source can retire settled metadata only. It has no owner/station
    // mutation permit and never reconstructs a financial Original.
    private sealed class CleanupNativeRead
    {
        internal ColdStationBatch Batch;internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;
        internal long Start,End,Fence;internal int Reads;internal bool Returned,Instantiated,Published;
        internal IDisposable Memory;
        internal byte[] Native;
        internal bool Unused;
        internal void Dispose(){Reader=null;Batch=null;Chunk=null;Tile=null;Native=null;Memory?.Dispose();Memory=null;}
    }
    private sealed class LoadedCleanup:IDisposable
    {
        internal readonly World World;internal readonly TileEntityWorkstation Station;internal readonly Chunk Chunk;
        internal readonly ChunkSnapshotUtil Util;internal readonly RegionFileAccessAbstract Access;internal readonly string Directory;internal readonly long Fence;
        private readonly string[] originalMarkerKeys;private readonly string worldKey,save;
        private IDisposable memory;
        internal byte[] Native{get;private set;}
        internal bool UnusedPending;
        private LoadedCleanup(CleanupNativeRead read)
        {World=read.Batch.World;Station=read.Tile;Chunk=read.Chunk;Util=read.Batch.Util;Access=read.Batch.Access;Directory=read.Batch.Directory;Fence=read.Fence;
            originalMarkerKeys=Chunk.ChunkCustomData.dict.Keys.Where(key=>key.StartsWith("RebirthRecyclerPlacedV1:",StringComparison.Ordinal)).ToArray();
            worldKey=RebirthWorkstationCompletionOutbox.CurrentGuid(World);save=Path.GetFullPath(GameIO.GetSaveGameDir());UnusedPending=read.Unused;Native=read.Native;read.Native=null;memory=read.Memory;read.Memory=null;}
        internal static LoadedCleanup Issue(CleanupNativeRead read)
        {
            if(!ReferenceEquals(loadingRecyclerStations,read.Batch)||!read.Returned||!read.Instantiated||read.Reads!=1)return null;
            int keys=0;foreach(string key in read.Chunk.ChunkCustomData.dict.Keys)
                if(key.StartsWith("RebirthRecyclerPlacedV1:",StringComparison.Ordinal)&&(++keys>128||key.Length>128))return null;
            return new LoadedCleanup(read);
        }
        internal bool ObservedAbsent(string key)=>!originalMarkerKeys.Contains(key);
        // Historical saved-load metadata, never a current mutation permit.
        // Later legitimate native reads cannot erase this actual load event.
        internal bool Current()=>memory!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&worldKey==RebirthWorkstationCompletionOutbox.CurrentGuid(World)&&
            save==Path.GetFullPath(GameIO.GetSaveGameDir())&&World.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&
            ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,Util)&&ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,Access)&&
            Path.GetFullPath(provider.m_RegionFileManager.saveDirectory)==Directory;
        public void Dispose(){loadedRecyclerCleanups.Remove(this);Native=null;memory?.Dispose();memory=null;}
    }
    [ThreadStatic]private static CleanupNativeRead instantiatingRecyclerCleanup;
    private static readonly List<LoadedCleanup> loadedRecyclerCleanups=new List<LoadedCleanup>();
    private static object EnterCleanupNativeInstantiate(PooledBinaryReader reader,Chunk chunk)
    {
        var batch=loadingRecyclerStations;
        if(batch==null||instantiatingRecyclerCleanup!=null||!ReferenceEquals(reader,batch.CleanupReader)||batch.CleanupReads.Count>=4)return null;
        var read=new CleanupNativeRead{Batch=batch,Reader=reader,Chunk=chunk,Start=reader.BaseStream.Position};
        instantiatingRecyclerCleanup=read;return read;
    }
    private static object EnterCleanupNativeRead(TileEntityWorkstation tile,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        var read=instantiatingRecyclerCleanup;
        if(!run||read==null||mode!=StreamModeRead.Persistency||!ReferenceEquals(reader,read.Reader)||!ReferenceEquals(tile.GetChunk(),read.Chunk)||
            read.Reads!=0||reader.BaseStream.Position!=read.Start)return null;
        read.Reads++;read.Tile=tile;return read;
    }
    private static void ObserveCleanupNativeRead(PooledBinaryReader reader,bool run,CleanupNativeRead read)
    {
        if(!run||!ReferenceEquals(read,instantiatingRecyclerCleanup)||!ReferenceEquals(reader,read.Reader)||read.Reads!=1||
            reader.BaseStream.Position<=read.Start||reader.BaseStream.Position>read.Batch.CleanupLength)return;
        read.End=reader.BaseStream.Position;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out var memory))return;
        read.Memory=memory;
        if(!HasPendingCleanupAt(read.Batch,read.Tile.ToWorldPos(),out read.Unused)){read.Dispose();return;}
        long length=read.End-read.Start;
        if(length<1||length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes+32||!reader.BaseStream.CanSeek||
            !RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(12L*length+65536))){read.Dispose();return;}
        try
        {
            var bytes=new byte[(int)length];reader.BaseStream.Position=read.Start;int copied=0;
            while(copied<bytes.Length){int count=reader.BaseStream.Read(bytes,copied,bytes.Length-copied);if(count<=0){read.Dispose();return;}copied+=count;}
            read.Native=bytes;
        }
        finally{reader.BaseStream.Position=read.End;}
        read.Returned=RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeReadFence(read.Tile,out read.Fence);
        if(read.Returned)read.Batch.CleanupReads.Add(read);else read.Dispose();
    }
    private static void ObserveCleanupNativeInstantiate(TileEntity tile,PooledBinaryReader reader,Chunk chunk,bool run,CleanupNativeRead read)
    {
        if(run&&ReferenceEquals(read,instantiatingRecyclerCleanup)&&read.Returned&&ReferenceEquals(tile,read.Tile)&&ReferenceEquals(chunk,read.Chunk)&&
            ReferenceEquals(reader,read.Reader)&&reader.BaseStream.Position==read.End)read.Instantiated=true;
    }
    private static void EndCleanupNativeInstantiate(CleanupNativeRead read,Exception error)
    {if(error!=null)read.Instantiated=false;if(ReferenceEquals(instantiatingRecyclerCleanup,read))instantiatingRecyclerCleanup=null;
        if(!read.Returned)read.Dispose();}
    private static void ObserveCleanupNativeLoad(Chunk chunk,ColdStationBatch batch)
    {
        foreach(var read in batch.CleanupReads)
        {
            if(!read.Returned||!read.Instantiated||!ReferenceEquals(chunk,read.Chunk)||!ReferenceEquals(chunk.GetTileEntity(read.Tile.localChunkPos),read.Tile)||
                !ReferenceEquals(batch.CleanupReader,read.Reader)||read.Reader.BaseStream.Position!=batch.CleanupLength)continue;
            var loaded=LoadedCleanup.Issue(read);if(loaded==null)continue;
            loadedRecyclerCleanups.Add(loaded);read.Published=true;
        }
    }
    private static void EndCleanupNativeLoad(ColdStationBatch batch)
    {foreach(var read in batch.CleanupReads)read.Dispose();batch.CleanupReads.Clear();instantiatingRecyclerCleanup=null;}
    private static bool HasPendingCleanupAt(ColdStationBatch batch,Vector3i position,out bool unusedPending)
    {
        unusedPending=false;
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced");
        // Completed history is skipped by its explicit positive outcome; there
        // is no arbitrary first-N archive scan that can hide a pending source.
        foreach(string path in Directory.EnumerateFiles(root,"*.unused-marker-attempted.xml").Concat(Directory.EnumerateFiles(root,"*.cleanup.xml")))
        {
            string name=Path.GetFileName(path);bool unused=name.EndsWith(".unused-marker-attempted.xml",StringComparison.Ordinal);
            string suffix=unused?".unused-marker-attempted.xml":".cleanup.xml";
            if(!Guid.TryParseExact(name.Substring(0,name.Length-suffix.Length),"N",out var operation))continue;
            if(ArtifactNodeExists(Path.Combine(root,operation.ToString("N")+(unused?".remote-unused-host.xml":".cleanup-saved.xml"))))continue;
            if(!unused&&(ArtifactNodeExists(Path.Combine(root,operation.ToString("N")+".remote-completed.xml"))||
                ArtifactNodeExists(Path.Combine(root,operation.ToString("N")+".remote-received.xml"))))continue;
            if(RebirthWorkstationCompletionPublication.PublishedLength(path)<1)continue;
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var xml=XmlReader.Create(file,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=48L*1024*1024,MaxCharactersFromEntities=0}))
                while(xml.Read())if(xml.NodeType==XmlNodeType.Element&&xml.Depth==2&&xml.Name=="placedRecyclerOriginal")
                {
                    if(xml.GetAttribute("operation")==operation.ToString("N")&&xml.GetAttribute("world")==RebirthWorkstationCompletionOutbox.CurrentGuid(batch.World)&&
                        int.TryParse(xml.GetAttribute("x"),out int x)&&int.TryParse(xml.GetAttribute("y"),out int y)&&int.TryParse(xml.GetAttribute("z"),out int z)&&
                        position==new Vector3i(x,y,z)){unusedPending=unused;return true;}
                    break;
                }
        }
        return false;
    }
    private static void PumpLoadedRecyclerCleanup(World world)
    {
        foreach(var loaded in loadedRecyclerCleanups.ToArray())
        {
            if(!ReferenceEquals(loaded.World,world)){loaded.Dispose();continue;}
            try
            {
                if(!loaded.Current()){loaded.Dispose();continue;}
                PumpRemoteLoadedRecyclerCleanup(loaded);
                PumpRemoteLoadedRecyclerUnused(loaded);
                if(!TryObserveLoadedRecyclerOwner(world.GetPrimaryPlayer(),out var owner))continue;
                foreach(Guid operation in owner.Pending.ToArray())
                    if(operation!=Guid.Empty&&TryRetireLoadedRecyclerCleanup(owner,loaded,operation))owner.Pending=owner.Pending.Where(value=>value!=operation).ToArray();
            }
            catch{/* Neither an absent marker nor an invalid terminal releases custody. */}
        }
    }
    private static bool TryRetireLoadedRecyclerCleanup(LoadedOwner owner,LoadedCleanup native,Guid operation)
    {
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),terminalPath=Path.Combine(root,operation.ToString("N")+".terminal.xml"),cleanupPath=Path.Combine(root,operation.ToString("N")+".cleanup.xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(terminalPath)+RebirthWorkstationCompletionPublication.PublishedLength(cleanupPath);
        if(length<2||length>48L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out var memory))return false;
        using(memory)
        {
            var terminal=RebirthWorkstationCompletionPublication.ReadPrimary(terminalPath);var cleanup=RebirthWorkstationCompletionPublication.ReadPrimary(cleanupPath);
            var original=terminal?.Element("before")?.Elements().SingleOrDefault();var retirement=cleanup?.Element("recyclerTerminalMarkerRetirement");
            if(terminal?.Name!="workstationCompletionIntent"||cleanup?.Name!="workstationCompletionIntent"||original?.Name!="placedRecyclerOriginal"||
                (string)terminal.Attribute("operation")!=operation.ToString("N")||(string)cleanup.Attribute("operation")!=operation.ToString("N")||
                !XNode.DeepEquals(original,cleanup.Element("before")?.Elements().SingleOrDefault())||terminal.Element("recyclerPlacedTerminal")==null||retirement==null||
                (string)original.Attribute("owner")!=owner.owner||(string)original.Attribute("account")!=owner.account||(string)original.Attribute("creation")!=owner.creation||
                (int?)original.Attribute("deaths")!=owner.deaths||(string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(native.World)||
                new Vector3i((int)original.Attribute("x"),(int)original.Attribute("y"),(int)original.Attribute("z"))!=native.Station.ToWorldPos()||
                !PhaseBytes(owner.buffs,"rbRecyclerPlaced_"+operation.ToString("N"),out float phase)||phase!=3f)return false;
            var bag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBag"),4*1024*1024));
            var belt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("afterBelt"),4*1024*1024));
            if(!SameOwnerGrid(bag,owner.bag)||!SameOwnerGrid(belt,owner.belt))return false;
            var beforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBag"),4*1024*1024));
            var beforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(original.Element("beforeBelt"),4*1024*1024));
            if(beforeBag?.Length!=bag.Length||beforeBelt?.Length!=belt.Length)return false;
            int slots=beforeBag.Zip(bag,(a,b)=>Same(a,b)?0:1).Sum()+beforeBelt.Zip(belt,(a,b)=>Same(a,b)?0:1).Sum();
            if(!PhaseBytes(owner.buffs,"rbRecyclerSlot_"+operation.ToString("N"),out float completed)||completed!=slots||
                owner.actor.Buffs.GetCustomVar("rbRecyclerPlaced_"+operation.ToString("N"))!=3f||
                owner.actor.Buffs.GetCustomVar("rbRecyclerSlot_"+operation.ToString("N"))!=slots)return false;
            byte[] terminalBytes=ColdBytes(retirement.Element("terminal"),32*1024*1024);
            if(terminalBytes==null||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(terminalPath,terminalBytes))return false;
            string marker="RebirthRecyclerPlacedV1:"+native.Station.localChunkPos.x+":"+native.Station.localChunkPos.y+":"+native.Station.localChunkPos.z+":"+operation.ToString("N");
            if(!native.ObservedAbsent(marker)||!native.Current()||!owner.Current())return false;
            var saved=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",(string)original.Attribute("world")),new XElement("before",new XElement(original)),
                new XElement("recyclerCleanupSaved",new XAttribute("actualNativeLoad",true),new XAttribute("tileReadFence",native.Fence),new XAttribute("terminal",RebirthStationPublicationRecord.Digest(terminalBytes)),new XAttribute("regionDirectory",native.Directory)));
            string path=Path.Combine(root,operation.ToString("N")+".cleanup-saved.xml");
            if(!RebirthWorkstationCompletionPublication.Write(path,saved)||!XNode.DeepEquals(saved,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                !native.Current()||!owner.Current()||!native.ObservedAbsent(marker))return false;
            owner.actor.Buffs.SetCustomVar("rbRecyclerPlaced_"+operation.ToString("N"),0f);owner.actor.Buffs.SetCustomVar("rbRecyclerSlot_"+operation.ToString("N"),0f);return true;
        }
    }
}
