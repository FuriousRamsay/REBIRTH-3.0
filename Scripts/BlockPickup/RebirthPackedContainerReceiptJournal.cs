using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using HarmonyLib;

// This journal reserves an original native destination; it never authorizes a
// second destination or substitutes sidecar publication for a native chunk save.
internal static class RebirthPackedContainerReceiptJournal
{
    internal enum Phase { Reserved=1, Applied=2, Saved=3 }
    internal sealed class Record
    {
        public int Version=1;public string Token,World,Incarnation,Payload,NativeOwner;
        public int X,Y,Z;public uint Block;public Phase State;
        public string NativeSnapshotDigest,NativeTileDigest;public long NativeGeneration;
    }
    private static readonly object Gate=new object();
    internal static bool IsIssued(string path)=>File.Exists(path)&&!File.Exists(path+".placed")&&!File.Exists(path+".restore")&&!File.Exists(path+".restore.bak");
    internal static Record Read(string path)
    {
        if(!File.Exists(path+".restore"))
        {
            if(File.Exists(path+".restore.bak"))throw new InvalidDataException("Packed journal primary missing; recovery required.");
            return null;
        }
        var text=File.ReadAllText(path+".restore");
        if(text.Length>RebirthContainerPackUp.MaxPayload*2+8192)throw new InvalidDataException("Packed journal too large.");
        var r=JsonConvert.DeserializeObject<Record>(text);
        if(r==null||r.Version!=1||!Guid.TryParseExact(r.Token,"N",out _)||!Guid.TryParse(r.World,out _)||!Guid.TryParseExact(r.Incarnation,"N",out _)||string.IsNullOrEmpty(r.Payload)||r.Payload.Length>RebirthContainerPackUp.MaxPayload||!Enum.IsDefined(typeof(Phase),r.State))throw new InvalidDataException("Invalid packed journal.");
        if(r.State==Phase.Saved&&(r.NativeGeneration<=0||!DigestValid(r.NativeSnapshotDigest)||!DigestValid(r.NativeTileDigest)))throw new InvalidDataException("Packed saved proof missing.");
        return r;
    }
    internal static bool Matches(Record r,string token,string world,string incarnation,int x,int y,int z,uint block,string payload)
        =>r!=null&&r.Token==token&&r.World==world&&r.Incarnation==incarnation&&r.X==x&&r.Y==y&&r.Z==z&&r.Block==block&&r.Payload==payload;
    internal static Record Reserve(string path,Record requested)
    {
        lock(Gate)
        {
            if(requested==null||!Guid.TryParseExact(requested.Token,"N",out _)||!Guid.TryParse(requested.World,out _)||!Guid.TryParseExact(requested.Incarnation,"N",out _))throw new InvalidDataException("Invalid packed reservation scope.");
            var prior=Read(path);
            if(prior!=null){if(prior.NativeOwner!=requested.NativeOwner||!Matches(prior,requested.Token,requested.World,requested.Incarnation,requested.X,requested.Y,requested.Z,requested.Block,requested.Payload))throw new InvalidOperationException("Packed token reserved for another destination/incarnation.");return prior;}
            if(!IsIssued(path)||File.ReadAllText(path)!=requested.Payload)throw new InvalidDataException("Packed issued receipt unavailable or changed.");
            requested.State=Phase.Reserved;Write(path,requested);return requested;
        }
    }
    internal static void Advance(string path,Record record,Phase phase)
    {
        lock(Gate)
        {
            var current=Read(path);
            if(current==null||current.NativeOwner!=record.NativeOwner||!Matches(current,record.Token,record.World,record.Incarnation,record.X,record.Y,record.Z,record.Block,record.Payload)||phase<current.State)throw new InvalidDataException("Packed journal transition mismatch.");
            if(phase==Phase.Saved&&(record.NativeGeneration<=0||!DigestValid(record.NativeSnapshotDigest)||!DigestValid(record.NativeTileDigest)))throw new InvalidDataException("Packed native publication proof missing.");
            record.State=phase;Write(path,record);
        }
    }
    private static bool DigestValid(string text)
    {if(text==null||text.Length!=64)return false;foreach(char c in text)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;return true;}
    private static void Write(string path,Record record)
    {
        string journal=path+".restore",temp=journal+".tmp";
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        using(var writer=new StreamWriter(file)){writer.Write(JsonConvert.SerializeObject(record));writer.Flush();file.Flush(true);}
        string error;if(!RebirthDurableFileCommit.TryPublish(temp,journal,out error))throw new IOException(error);
    }
}

// Demand-scoped native snapshot/readback witness, shared with the station
// serializer evidence. A successful SetModified or serializer call is not enough.
internal static class RebirthPackedContainerPublication
{
    // Parent settings stores supply a real primary-publication checkpoint.
    // Missing capability retains the destination hold rather than claiming success.
    internal static Func<bool> SettingsCheckpoint;
    [ThreadStatic] internal static bool Recovering;
    private static bool installed;
    private static float nextUpdate;
    private static readonly List<Pending> pending=new List<Pending>();
    private static readonly HashSet<TEFeatureRebirthContainerName> recover=new HashSet<TEFeatureRebirthContainerName>();
    private static World indexedWorld;
    private static Dictionary<string,List<RebirthPackedContainerReceiptJournal.Record>> recoveryIndex;
    internal static void InvalidateRecoveryIndex(){recoveryIndex=null;}
    private sealed class Pending
    {
        internal World World;internal TileEntityComposite Tile;internal TEFeatureStorage Storage;
        internal TEFeatureRebirthContainerName Name;internal RebirthPackedContainerReceiptJournal.Record Record;
        internal string Path,Items,SaveDirectory;internal int Width,Height;internal RebirthStationSnapshotEvidence.Request Request;
        internal int Span=-1;internal byte[] NativeBytes;
        internal float RequestedAt;
        internal Func<bool> ProviderCurrent;
        internal bool Current()=>ReferenceEquals(World,GameManager.Instance?.World)&&GameIO.GetSaveGameDir()==SaveDirectory&&World.worldState?.Guid==Record.World&&
            ReferenceEquals(World.GetTileEntity(new Vector3i(Record.X,Record.Y,Record.Z)),Tile)&&
            ReferenceEquals(Tile.GetFeature<TEFeatureStorage>(),Storage)&&ReferenceEquals(Tile.GetFeature<TEFeatureRebirthContainerName>(),Name)&&
            World.GetBlock(new Vector3i(Record.X,Record.Y,Record.Z)).rawData==Record.Block&&Name.Incarnation==Record.Incarnation&&Name.PackedReceipt==Record.Token&&
            (Tile.Owner?.CombinedString??string.Empty)==Record.NativeOwner&&
            Storage.ItemGrid.ContainerSize.x==Width&&Storage.ItemGrid.ContainerSize.y==Height&&RebirthContainerPackUp.EncodeItems(Storage.ItemGrid.items)==Items&&(ProviderCurrent==null||ProviderCurrent());
    }
    internal static void Install(Harmony harmony)
    {
        if(installed)return;
        SettingsCheckpoint=()=>{bool categories=false,remote=false;try{categories=QuickStackAcceptedCategoryRegistry.TryFlushForCheckpoint();}catch{}try{remote=RemoteResourceStateStore.TryFlushForCheckpoint();}catch{}return categories&&remote;};
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationSnapshotUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationChunkSerializedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationSnapshotResetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationSnapshotWritePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthStationRegionPublishedPatch));
        harmony.Patch(AccessTools.Method(typeof(TileEntityComposite),nameof(TileEntityComposite.write),new[]{typeof(PooledBinaryWriter),typeof(StreamModeWrite)}),
            prefix:new HarmonyMethod(typeof(RebirthPackedContainerPublication),nameof(BeforeWrite)),postfix:new HarmonyMethod(typeof(RebirthPackedContainerPublication),nameof(AfterWrite)));
        harmony.Patch(AccessTools.Method(typeof(TileEntity),nameof(TileEntity.OnLockRequestServer),new[]{typeof(int),typeof(PooledBinaryReader),typeof(ushort)}),prefix:new HarmonyMethod(typeof(RebirthPackedContainerPublication),nameof(BeforeLock)));
        harmony.Patch(AccessTools.Method(typeof(RebirthSecureAccessPolicy),nameof(RebirthSecureAccessPolicy.CanAccess),new[]{typeof(EntityPlayer),typeof(TileEntityComposite),typeof(RebirthSecureAccessPurpose),typeof(string).MakeByRefType()}),prefix:new HarmonyMethod(typeof(RebirthPackedContainerPublication),nameof(BeforeAccess)));
        harmony.Patch(AccessTools.Method(typeof(RemoteResourceAccess),nameof(RemoteResourceAccess.AuthorizeComposite)),prefix:new HarmonyMethod(typeof(RebirthPackedContainerPublication),nameof(BeforeRemoteAccess)));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Update));
        installed=true;
    }
    internal static void RecoverAfterRead(TEFeatureRebirthContainerName feature)
    {if(feature!=null)lock(recover)recover.Add(feature);}
    private static string FindReserved(World world,TileEntityComposite tile,TEFeatureRebirthContainerName name)
    {
        if(!string.IsNullOrEmpty(name.PackedReceipt))return name.PackedReceipt;
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","PackedContainers");
        if(!Directory.Exists(directory))return null;
        var pos=tile.ToWorldPos();string selected=null;
        // Recovery before the first native stamp write must use the journal's
        // original incarnation, never merely a new empty block at that address.
        if(!ReferenceEquals(indexedWorld,world)||recoveryIndex==null)
        {
            indexedWorld=world;recoveryIndex=new Dictionary<string,List<RebirthPackedContainerReceiptJournal.Record>>(StringComparer.Ordinal);
            foreach(string journal in Directory.EnumerateFiles(directory,"*.json.restore"))try
            {
                var r=RebirthPackedContainerReceiptJournal.Read(journal.Substring(0,journal.Length-".restore".Length));
                if(r.State==RebirthPackedContainerReceiptJournal.Phase.Saved)continue;
                if(!recoveryIndex.TryGetValue(r.Incarnation,out var records))recoveryIndex[r.Incarnation]=records=new List<RebirthPackedContainerReceiptJournal.Record>();records.Add(r);
            }
            catch(Exception ex){Log.Warning("[REBIRTH Pack Up] unreadable recovery journal retained: "+ex.Message);}
        }
        if(!recoveryIndex.TryGetValue(name.Incarnation,out var candidates))return null;
        foreach(var r in candidates)
        {
            if(r.World!=world.worldState?.Guid||r.Incarnation!=name.Incarnation||r.X!=pos.x||r.Y!=pos.y||r.Z!=pos.z||r.Block!=world.GetBlock(pos).rawData||r.State==RebirthPackedContainerReceiptJournal.Phase.Saved)continue;
            if(selected!=null&&selected!=r.Token)throw new InvalidDataException("Multiple packed reservations for one incarnation.");
            selected=r.Token;
        }
        return selected;
    }
    internal static bool Held(TileEntityComposite tile)
    {
        // Clients do not own the server receipt directory. Native server lock
        // admission and authoritative remote-resource checks enforce the hold.
        if(GameManager.Instance?.World==null||GameManager.Instance.World.IsRemote())return false;
        var token=tile?.GetFeature<TEFeatureRebirthContainerName>()?.PackedReceipt;
        if(string.IsNullOrEmpty(token))return false;
        try{var r=RebirthPackedContainerReceiptJournal.Read(RebirthContainerPackUp.RecordPath(token));var pos=tile.ToWorldPos();return r==null||r.Token!=token||r.State!=RebirthPackedContainerReceiptJournal.Phase.Saved||r.X!=pos.x||r.Y!=pos.y||r.Z!=pos.z||r.World!=GameManager.Instance?.World?.worldState?.Guid||r.Incarnation!=tile.GetFeature<TEFeatureRebirthContainerName>().Incarnation;}
        catch{return true;}
    }
    private static bool BeforeLock(TileEntity __instance,ref bool __result)
    {if(!Held(__instance as TileEntityComposite))return true;__result=false;return false;}
    private static bool BeforeAccess(TileEntityComposite target,ref string reason,ref bool __result)
    {if(Recovering||!Held(target))return true;reason="packed restoration awaiting native save/recovery";__result=false;return false;}
    private static bool BeforeRemoteAccess(TileEntityComposite composite,ref string reason,ref bool __result)
    {return BeforeAccess(composite,ref reason,ref __result);}
    internal static void Observe(World world,TileEntityComposite tile,TEFeatureStorage storage,TEFeatureRebirthContainerName name,RebirthPackedContainerReceiptJournal.Record record,string path)
    {
        foreach(var existing in pending)if(ReferenceEquals(existing.Tile,tile)&&existing.Record.Token==record.Token)return;
        pending.Add(new Pending{World=world,Tile=tile,Storage=storage,Name=name,Record=record,Path=path,SaveDirectory=GameIO.GetSaveGameDir(),
            Items=RebirthContainerPackUp.EncodeItems(storage.ItemGrid.items),Width=storage.ItemGrid.ContainerSize.x,Height=storage.ItemGrid.ContainerSize.y});
    }
    private static void BeforeWrite(TileEntityComposite __instance,PooledBinaryWriter _bw,StreamModeWrite _eStreamMode,out long __state)
    {__state=-1;if(_eStreamMode==StreamModeWrite.Persistency)__state=RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream);}
    private static void AfterWrite(TileEntityComposite __instance,PooledBinaryWriter _bw,long __state)
    {
        if(__state<8)return;
        foreach(var p in pending)if(ReferenceEquals(p.Tile,__instance)&&p.Request!=null&&p.Current())
        {
            var stream=_bw.BaseStream;long end=stream.Position,length=end-__state;
            if(length<=0||length>512*1024||!stream.CanRead||!stream.CanSeek)return;
            var bytes=new byte[(int)length];try{stream.Position=__state;int offset=0;while(offset<bytes.Length){int n=stream.Read(bytes,offset,bytes.Length-offset);if(n<=0)return;offset+=n;}p.Span=(int)__state;p.NativeBytes=bytes;}finally{stream.Position=end;}
        }
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;if(world==null||world.IsRemote())return;
        if(UnityEngine.Time.realtimeSinceStartup<nextUpdate)return;nextUpdate=UnityEngine.Time.realtimeSinceStartup+1f;
        TEFeatureRebirthContainerName[] queue;lock(recover){queue=new TEFeatureRebirthContainerName[recover.Count];recover.CopyTo(queue);recover.Clear();}
        foreach(var name in queue)try{var tile=name.Parent;if(tile!=null&&ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()),tile)){var token=FindReserved(world,tile,name);if(token!=null)RebirthContainerPackUp.Recover(world,tile.ToWorldPos(),token);}}catch(Exception ex){RecoverAfterRead(name);Log.Warning("[REBIRTH Pack Up] native recovery retained: "+ex.Message);}
        for(int i=pending.Count-1;i>=0;i--)
        {
            var p=pending[i];
            try
            {
                if(!p.Current()){p.Request?.Dispose();pending.RemoveAt(i);continue;}
                if(p.Request!=null)
                {
                    if(!RebirthStationSnapshotEvidence.HasPublished(p.Request)||!RebirthStationSnapshotEvidence.TryCopy(p.Request,out var bytes)||p.NativeBytes==null||p.Span<8||p.Span>bytes.Length-p.NativeBytes.Length)
                    {if(UnityEngine.Time.realtimeSinceStartup-p.RequestedAt>15f){p.Request.Dispose();p.Request=null;p.NativeBytes=null;p.Span=-1;}continue;}
                    bool same=true;for(int n=0;n<p.NativeBytes.Length;n++)if(bytes[p.Span+n]!=p.NativeBytes[n]){same=false;break;}
                    if(!same)continue;
                    if(SettingsCheckpoint==null||!SettingsCheckpoint()||!p.Current())continue;
                    using(var hash=System.Security.Cryptography.SHA256.Create())
                    {p.Record.NativeSnapshotDigest=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();p.Record.NativeTileDigest=BitConverter.ToString(hash.ComputeHash(p.NativeBytes)).Replace("-","").ToLowerInvariant();}
                    p.Record.NativeGeneration=p.Request.Generation;
                    RebirthPackedContainerReceiptJournal.Advance(p.Path,p.Record,RebirthPackedContainerReceiptJournal.Phase.Saved);
                    p.Request.Dispose();pending.RemoveAt(i);continue;
                }
                var chunk=p.Tile.GetChunk();var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
                if(chunk==null||chunk.InProgressUnloading||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased))continue;
                var access=regions.regionFileAccess;string regionDirectory=Path.GetFullPath(regions.saveDirectory);
                p.ProviderCurrent=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==regionDirectory&&ReferenceEquals(p.Tile.GetChunk(),chunk)&&!chunk.InProgressUnloading;
                p.Request=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,p.Current,p.Tile);
                if(p.Request==null)continue;
                p.RequestedAt=UnityEngine.Time.realtimeSinceStartup;
                lock(chunk){if(chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){p.Request.Dispose();p.Request=null;continue;}chunk.InProgressSaving=true;}
                try{regions.SaveChunkSnapshot(chunk,true);}finally{lock(chunk)chunk.InProgressSaving=false;}
            }
            catch(Exception ex){p.Request?.Dispose();p.Request=null;p.NativeBytes=null;p.Span=-1;Log.Warning("[REBIRTH Pack Up] save witness retained: "+ex.Message);}
        }
    }
}
