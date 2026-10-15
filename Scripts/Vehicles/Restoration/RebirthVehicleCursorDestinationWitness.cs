using System;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;

internal static class RebirthVehicleCursorDestinationWitness
{
    private const int MaximumBytes=2*1024*1024;
    private static bool installed;
    private static readonly System.Collections.Generic.List<Witness> observing=new System.Collections.Generic.List<Witness>();
    [ThreadStatic] private static Witness writing;
    internal static void Install(Harmony harmony)
    {
        if(installed)return;
        var method=AccessTools.Method(typeof(TEFeatureStorage),nameof(TEFeatureStorage.Write),new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)});
        if(method==null)throw new MissingMethodException("Native storage persistency writer unavailable.");
        harmony.Patch(method,prefix:new HarmonyMethod(typeof(RebirthVehicleCursorDestinationWitness),nameof(BeforeWrite)),
            postfix:new HarmonyMethod(typeof(RebirthVehicleCursorDestinationWitness),nameof(AfterWrite)));
        harmony.Patch(AccessTools.Method(typeof(RebirthStationSnapshotEvidence),"MarkPublished"),
            postfix:new HarmonyMethod(typeof(RebirthVehicleCursorDestinationWitness),nameof(Published)));
        RebirthVehicleCursorStorageProtocol.Install(harmony);
        installed=true;
    }
    internal static bool TryCapture(XUiC_ItemStack target,ItemStack afterTarget,out IRebirthVehicleCursorDestinationWitness witness)
    {
        witness=null;
        try
        {
            var owner=target?.xui?.playerUI?.entityPlayer;var world=owner?.world as World;
            if(!installed||!ThreadManager.IsMainThread()||world==null||
                target.StackLocation!=XUiC_ItemStack.StackLocationTypes.LootContainer||afterTarget==null||target.IsLocked||target.StackLock||target.UserLockedSlot||
                !ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(world.GetPrimaryPlayer(),owner)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
                !XUiC_RebirthVehicleRestoration.TryGetRequestCharacter(owner,out var creation))return false;
            var storage=target.GetParentByType<XUiC_LootWindow>()?.te;
            var tile=storage?.Parent;
            if(tile==null||!ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()),tile)||
                !RemoteResourceAccess.AuthorizeComposite(tile,owner,false,out _)||
                (tile.ToWorldPos().ToVector3()-owner.position).sqrMagnitude>64f)return false;
            var grid=storage.ItemGrid;int slot=target.SlotNumber;
            if(slot<0||slot>=grid.Length||grid.IsLocked(slot)||!Same(target.ItemStack,grid.items[slot]))return false;
            var detached=ItemStackGrid.Create(grid.ContainerSize,grid.StackLocation,grid.SlotLocks!=null,grid.Preferences!=null,null);
            detached.CopyFrom(grid);detached.items[slot]=afterTarget.Clone();
            var result=new Witness{World=world.worldState?.Guid,Owner=identity.CanonicalId,Creation=creation,
                NativeOwner=tile.Owner?.CombinedString??string.Empty,UiGroup=target.windowGroup,UiGeneration=RebirthVehicleCustodyUiGeneration.Read(target.windowGroup),
                Position=tile.ToWorldPos(),Block=world.GetBlock(tile.ToWorldPos()).rawData,Slot=slot,
                Before=GridBytes(grid),After=GridBytes(detached),CapturedWorld=world,CapturedOwner=owner,Tile=tile,Storage=storage,Grid=grid};
            if(!Guid.TryParse(result.World,out _)||result.Before.Length>MaximumBytes||result.After.Length>MaximumBytes)return false;
            result.Encode(); // Enforce combined payload bound before admitting a gesture.
            witness=result;return true;
        }
        catch{return false;}
    }
    internal static bool TryRead(string payload,out IRebirthVehicleCursorDestinationWitness witness)
    {
        witness=null;
        try
        {
            if(string.IsNullOrEmpty(payload)||payload.Length>MaximumBytes*4/3+8)return false;
            byte[] bytes=Convert.FromBase64String(payload);if(bytes.Length>MaximumBytes)return false;
            using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s,Encoding.UTF8,true))
            {
                if(r.ReadInt32()!=0x31534456)return false;
                var w=new Witness{World=r.ReadString(),Owner=r.ReadString(),Creation=r.ReadString(),
                    NativeOwner=r.ReadString(),
                    Position=new Vector3i(r.ReadInt32(),r.ReadInt32(),r.ReadInt32()),Block=r.ReadUInt32(),Slot=r.ReadInt32(),
                    Before=ReadBytes(r),After=ReadBytes(r)};
                if(s.Position!=s.Length||!Guid.TryParse(w.World,out _)||string.IsNullOrEmpty(w.Owner)||w.Owner.Length>512||
                w.Creation.Length>512||w.NativeOwner.Length>512||w.Slot<0||w.Slot>=4096||w.Encode()!=payload)return false;
                // Decode retains custody evidence, not a fabricated live operation receipt.
                witness=w;return true;
            }
        }
        catch{return false;}
    }
    private static byte[] ReadBytes(BinaryReader r){int n=r.ReadInt32();if(n<1||n>MaximumBytes)throw new InvalidDataException();byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return b;}
    private static bool Same(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&RebirthNativeItemCodec.Encode(a.itemValue)==RebirthNativeItemCodec.Encode(b.itemValue);
    private static byte[] GridBytes(ItemStackGrid grid)
    {
        using(var s=new MemoryStream())using(var w=MemoryPools.poolBinaryWriter.AllocSync(true))
        {var previous=w.Encoding;try{w.Encoding=new UTF8Encoding(false,false);w.SetBaseStream(s);grid.Write(w,StreamModeWrite.Persistency);w.Flush();return s.ToArray();}finally{w.Encoding=previous;}}
    }
    private static void BeforeWrite(TEFeatureStorage __instance,PooledBinaryWriter _bw,StreamModeWrite _eStreamMode,out long __state)
    {
        __state=-1;var w=writing;
        if(_eStreamMode==StreamModeWrite.Persistency&&w!=null&&ReferenceEquals(w.Storage,__instance)&&w.CurrentAfter())
            __state=RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream);
    }
    private static void AfterWrite(TEFeatureStorage __instance,PooledBinaryWriter _bw,long __state)
    {
        var w=writing;if(w==null||__state<0||!ReferenceEquals(w.Storage,__instance))return;
        var stream=_bw.BaseStream;long end=stream.Position,length=end-__state;
        if(length<=0||length>MaximumBytes||!stream.CanRead||!stream.CanSeek)return;
        byte[] bytes=new byte[(int)length];try{stream.Position=__state;int offset=0;while(offset<bytes.Length){int n=stream.Read(bytes,offset,bytes.Length-offset);if(n<=0)return;offset+=n;}w.Span=(int)__state;w.Serialized=bytes;}finally{stream.Position=end;}
    }
    private static void Published(RebirthStationSnapshotEvidence.Request request,long generation,string path,string digest)
    {
        if(path==null||digest==null)return;
        lock(observing)foreach(var w in observing)if(ReferenceEquals(w.request,request))
        {w.RegionPath=path;w.PayloadDigest=digest;w.Generation=generation;}
    }
    internal sealed class Witness:IRebirthVehicleCursorDestinationWitness,IDisposable
    {
        internal string World,Owner,Creation,NativeOwner;internal Vector3i Position;internal uint Block;internal int Slot;
        internal XUiWindowGroup UiGroup;internal long UiGeneration;
        internal byte[] Before,After,Serialized;internal int Span=-1;
        internal World CapturedWorld;internal EntityPlayerLocal CapturedOwner;internal TileEntityComposite Tile;
        internal TEFeatureStorage Storage;internal ItemStackGrid Grid;
        internal Func<bool> ServerCurrent,ServerCurrentExtra,ServerFrozenCurrent;internal string RegionPath,PayloadDigest;internal long Generation,FrozenGeneration;
        internal Guid Original;internal string OriginalFrame;internal bool Prepared,Saved,Aborted,SnapshotAccepted;
        internal Guid operation;internal RebirthStationSnapshotEvidence.Request request;
        public string Encode()
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
            {w.Write(0x31534456);w.Write(World);w.Write(Owner);w.Write(Creation);w.Write(NativeOwner);w.Write(Position.x);w.Write(Position.y);w.Write(Position.z);w.Write(Block);w.Write(Slot);
                w.Write(Before.Length);w.Write(Before);w.Write(After.Length);w.Write(After);w.Flush();if(s.Length>MaximumBytes)throw new InvalidDataException();return Convert.ToBase64String(s.ToArray());}
        }
        public bool MatchesLive(EntityPlayerLocal owner)
        {
            if(Saved)return RebirthVehicleCursorStorageProtocol.OwnerMatches(this,owner);
            if(CapturedWorld==null&&RebirthVehicleCursorStorageProtocol.OwnerMatches(this,owner))
            {
                CapturedWorld=owner.world as World;CapturedOwner=owner;Tile=CapturedWorld.GetTileEntity(Position) as TileEntityComposite;
                Storage=Tile?.GetFeature<TEFeatureStorage>();Grid=Storage?.ItemGrid;
            }
            try{return ThreadManager.IsMainThread()&&CapturedWorld!=null&&ReferenceEquals(owner,CapturedOwner)&&
                ReferenceEquals(owner?.world,CapturedWorld)&&ReferenceEquals(GameManager.Instance?.World,CapturedWorld)&&ReferenceEquals(CapturedWorld.GetPrimaryPlayer(),owner)&&
                CapturedWorld.worldState?.Guid==World&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.CanonicalId==Owner&&
                XUiC_RebirthVehicleRestoration.TryGetRequestCharacter(owner,out var creation)&&creation==Creation&&
                ReferenceEquals(CapturedWorld.GetTileEntity(Position),Tile)&&ReferenceEquals(Tile.GetFeature<TEFeatureStorage>(),Storage)&&ReferenceEquals(Storage.ItemGrid,Grid)&&
                CapturedWorld.GetBlock(Position).rawData==Block&&(Tile.Owner?.CombinedString??string.Empty)==NativeOwner&&RemoteResourceAccess.AuthorizeComposite(Tile,owner,false,out _)&&
                GridBytes(Grid).SequenceEqual(After);}catch{return false;}
        }
        internal bool CurrentAfter()=>FrozenGeneration>0?ServerFrozenCurrent?.Invoke()==true:ServerCurrent!=null?ServerCurrent()&&(ServerCurrentExtra==null||ServerCurrentExtra()):MatchesLive(CapturedOwner);
        internal bool TryFreezeSnapshot()
        {
            if(FrozenGeneration>0)return true;
            if(!CurrentAfter()||request==null||Serialized==null||Span<8||!RebirthStationSnapshotEvidence.TryCopy(request,out var bytes)||Span>bytes.Length-Serialized.Length)return false;
            for(int i=0;i<Serialized.Length;i++)if(bytes[Span+i]!=Serialized[i])return false;
            FrozenGeneration=request.Generation;return FrozenGeneration>0;
        }
        internal bool MatchesBeforeLive(EntityPlayerLocal owner)
        {
            try{return ThreadManager.IsMainThread()&&ReferenceEquals(owner,CapturedOwner)&&ReferenceEquals(owner?.world,CapturedWorld)&&
                ReferenceEquals(GameManager.Instance?.World,CapturedWorld)&&ReferenceEquals(CapturedWorld.GetPrimaryPlayer(),owner)&&
                CapturedWorld.worldState?.Guid==World&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.CanonicalId==Owner&&
                XUiC_RebirthVehicleRestoration.TryGetRequestCharacter(owner,out var creation)&&creation==Creation&&
                ReferenceEquals(CapturedWorld.GetTileEntity(Position),Tile)&&ReferenceEquals(Tile.GetFeature<TEFeatureStorage>(),Storage)&&
                ReferenceEquals(Storage.ItemGrid,Grid)&&CapturedWorld.GetBlock(Position).rawData==Block&&(Tile.Owner?.CombinedString??string.Empty)==NativeOwner&&MatchesBefore()&&
                LockManager.Instance.IsLockedByLocalPlayer(Storage,0);}catch{return false;}
        }
        internal bool BindServer(World world,EntityPlayer actor)
        {
            CapturedWorld=world;Tile=world.GetTileEntity(Position) as TileEntityComposite;Storage=Tile?.GetFeature<TEFeatureStorage>();Grid=Storage?.ItemGrid;
            if(Grid==null||Slot>=Grid.Length||world.GetBlock(Position).rawData!=Block||(Tile.Owner?.CombinedString??string.Empty)!=NativeOwner)return false;
            ServerCurrent=()=>ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&world.worldState?.Guid==World&&
                ReferenceEquals(actor.world,world)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&ReferenceEquals(world.GetTileEntity(Position),Tile)&&
                ReferenceEquals(Tile.GetFeature<TEFeatureStorage>(),Storage)&&ReferenceEquals(Storage.ItemGrid,Grid)&&world.GetBlock(Position).rawData==Block&&(Tile.Owner?.CombinedString??string.Empty)==NativeOwner&&
                RebirthVehicleCursorStorageProtocol.CurrentCreation(actor,Creation)&&
                RemoteResourceAccess.AuthorizeComposite(Tile,actor,false,out _)&&GridBytes(Grid).SequenceEqual(After);
            return true;
        }
        internal bool MatchesBefore()=>Grid!=null&&GridBytes(Grid).SequenceEqual(Before);
        internal bool MatchesBoundBefore()=>ThreadManager.IsMainThread()&&CapturedWorld!=null&&ReferenceEquals(CapturedWorld,GameManager.Instance?.World)&&
            CapturedWorld.worldState?.Guid==World&&ReferenceEquals(CapturedWorld.GetTileEntity(Position),Tile)&&
            ReferenceEquals(Tile?.GetFeature<TEFeatureStorage>(),Storage)&&ReferenceEquals(Storage?.ItemGrid,Grid)&&
            CapturedWorld.GetBlock(Position).rawData==Block&&(Tile.Owner?.CombinedString??string.Empty)==NativeOwner&&MatchesBefore();
        internal bool MatchesAfterImage()=>Grid!=null&&GridBytes(Grid).SequenceEqual(After);
        internal bool ValidPickupDelta(ItemValue pickup)
        {
            if(Grid==null||pickup==null||pickup.IsEmpty()||Slot<0||Slot>=Grid.Length)return false;
            var detached=ItemStackGrid.Create(Grid.ContainerSize,Grid.StackLocation,Grid.SlotLocks!=null,Grid.Preferences!=null,null);
            detached.CopyFrom(Grid);detached.items[Slot]=new ItemStack(pickup.Clone(),1);
            if(GridBytes(detached).SequenceEqual(After)&&!Before.SequenceEqual(After))return true;
            var old=Grid.items[Slot];var item=pickup.ItemClass;
            if(old.IsEmpty()||item==null||!item.CanStack()||!old.itemValue.ItemClassOrMissing.CanStack()||
                pickup.HasQuality||old.itemValue.HasQuality||old.itemValue.type!=pickup.type||old.count>=item.MaxCount)return false;
            detached.items[Slot]=old.Clone();detached.items[Slot].count++;
            return GridBytes(detached).SequenceEqual(After);
        }
        public bool RequestAndVerifySaved(EntityPlayerLocal owner,Guid receipt)
        {
            if(ServerCurrent==null)return RebirthVehicleCursorStorageProtocol.Verify(this,owner,receipt);
            if(receipt==Guid.Empty||!CurrentAfter()||operation!=Guid.Empty&&operation!=receipt)return false;
            operation=receipt;
            if(request!=null)
            {
                if(!RebirthStationSnapshotEvidence.TryCopy(request,out var bytes)||!RebirthStationSnapshotEvidence.HasPublished(request)||Serialized==null||
                    Generation<=0||Generation!=request.Generation||FrozenGeneration>0&&Generation!=FrozenGeneration||Span<8||Span>bytes.Length-Serialized.Length)return false;
                for(int i=0;i<Serialized.Length;i++)if(bytes[Span+i]!=Serialized[i])return false;
                return CurrentAfter();
            }
            var chunk=Tile.GetChunk();var provider=CapturedWorld.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
            var regions=provider?.m_RegionFileManager;
            if(chunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return false;
            var access=regions.regionFileAccess;string root=Path.GetFullPath(regions.saveDirectory);
            Func<bool> scope=()=>CurrentAfter()&&ReferenceEquals(Tile.GetChunk(),chunk)&&ReferenceEquals(CapturedWorld.ChunkCache.ChunkProvider,provider)&&
                ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==root;
            request=RebirthStationSnapshotEvidence.Watch(CapturedWorld,chunk,Position.x>>4,Position.z>>4,scope,Storage);
            if(request==null)return false;
            lock(observing)observing.Add(this);
            try
            {
                lock(chunk){if(!scope()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){Dispose();return false;}chunk.InProgressSaving=true;}
                try{writing=this;regions.SaveChunkSnapshot(chunk,true);}finally{writing=null;lock(chunk)chunk.InProgressSaving=false;}
                return false; // Native worker publication must finish before credit is released.
            }
            catch{Dispose();return false;}
        }
        public void Dispose(){lock(observing)observing.Remove(this);request?.Dispose();request=null;Serialized=null;Span=-1;}
    }
    internal static bool PrepareForDrop(IRebirthVehicleCursorDestinationWitness witness,EntityPlayerLocal owner,Guid receipt,Guid original,string frame,Action<bool> completed)
        =>witness is Witness w&&RebirthVehicleCursorStorageProtocol.Prepare(w,owner,receipt,original,frame,completed);
    internal static bool CancelForDrop(IRebirthVehicleCursorDestinationWitness witness,EntityPlayerLocal owner,Guid receipt,Guid original,string frame,Action<bool> completed)
        =>witness is Witness w&&RebirthVehicleCursorStorageProtocol.Cancel(w,owner,receipt,original,frame,completed);
    internal static bool TryObserveAborted(IRebirthVehicleCursorDestinationWitness witness,EntityPlayerLocal owner,Guid receipt)
        =>witness is Witness w&&RebirthVehicleCursorStorageProtocol.ObserveAbort(w,owner,receipt);
    internal static bool CanCloseAfterDrop(IRebirthVehicleCursorDestinationWitness witness,EntityPlayerLocal owner,Guid receipt)
    {
        if(!(witness is Witness w)||!RebirthVehicleCursorStorageProtocol.OwnerMatches(w,owner))return false;
        if(w.SnapshotAccepted||w.Saved)return true;
        RebirthVehicleCursorStorageProtocol.Verify(w,owner,receipt);
        return w.SnapshotAccepted||w.Saved;
    }
}
