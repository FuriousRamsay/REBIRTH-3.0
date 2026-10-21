using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using HarmonyLib;

// Each distinct native child chunk must positively publish its typed original
// marker and all exact child postimages. No missing chunk is a terminal result.
internal static class RebirthPlacedOriginalFootprintPublication
{
    private sealed class Watch{internal World World;internal Chunk Chunk;internal string Directory,Image;internal RebirthPlacedOriginal.Record Original;internal RebirthPlacedOriginalFootprint.Cell[] Cells;internal RebirthStationSnapshotEvidence.Request Request;internal float Started;internal string Proof;internal Func<bool> ProviderCurrent;}
    private static readonly ConcurrentQueue<WeakReference<Chunk>> loaded=new ConcurrentQueue<WeakReference<Chunk>>();
    private static readonly Dictionary<string,Watch> watches=new Dictionary<string,Watch>();
    private static string Key(RebirthPlacedOriginal.Record r)=>"rebirth.placed.footprint.v1:"+r.Operation.ToString("N");
    private static byte[] Image(RebirthPlacedOriginal.Record r)=>Encoding.UTF8.GetBytes(RebirthPlacedOriginal.Digest(r)+"|"+r.Footprint);
    private static bool Current(Watch w)
    {
        if(w.ProviderCurrent?.Invoke()!=true||!ReferenceEquals(GameManager.Instance?.World,w.World)||GameIO.GetSaveGameDir()!=w.Directory||!ReferenceEquals(w.World.GetChunkSync(w.Chunk.X,w.Chunk.Z),w.Chunk)||w.Chunk.InProgressUnloading)return false;
        foreach(var c in w.Cells)if(w.World.GetBlock(c.Position).rawData!=c.After||!RebirthOriginalNativeBlockIdentity.TryRead(w.World,c.Position,out var token)||token!=c.AfterBirth||!Convert.FromBase64String(c.Marker).SequenceEqual(RebirthOriginalNativeBlockIdentity.CopyMarker(w.World,c.Position)??Array.Empty<byte>()))return false;
        return w.Chunk.ChunkCustomData.dict.TryGetValue(Key(w.Original),out var marker)&&marker?.data!=null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null&&marker.data.SequenceEqual(Image(w.Original));
    }
    internal static bool Observe(World world,RebirthPlacedOriginal.Record r,out string proof)
    {
        proof=null;try
        {
            var cells=RebirthPlacedOriginalFootprint.Decode(r.Footprint);if(cells.Count==0){proof=string.Empty;return true;}if(cells.Any(c=>c.AfterBirth.Length!=32||c.Marker.Length==0))return false;
            var proofs=new List<string>();
            foreach(var group in cells.GroupBy(c=>World.toChunkXZ(c.Position.x)+":"+World.toChunkXZ(c.Position.z)))
            {
                string id=r.World+"|"+r.Operation.ToString("N")+"|"+group.Key;
                if(!watches.TryGetValue(id,out var w))
                {
                    if(watches.Count>=128)return false;var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;if(regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased))return false;var access=regions.regionFileAccess;string regionDirectory=Path.GetFullPath(regions.saveDirectory);
                    var first=group.First();var chunk=world.GetChunkSync(World.toChunkXZ(first.Position.x),World.toChunkXZ(first.Position.z)) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;
                    Prune(world,chunk);byte[] image=Image(r);if(image.Length>ushort.MaxValue)return false;string key=Key(r);
                    if(chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)){if(marker?.data==null||!marker.data.SequenceEqual(image)||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null)return false;}
                    else{if(chunk.ChunkCustomData.dict.Count>=ushort.MaxValue)return false;chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=image});chunk.isModified=true;}
                    w=new Watch{World=world,Chunk=chunk,Original=r,Cells=group.ToArray(),Directory=GameIO.GetSaveGameDir(),Image=r.Footprint,ProviderCurrent=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==regionDirectory};if(!Current(w))return false;watches.Add(id,w);
                }
                if(!ReferenceEquals(w.World,world)||w.Image!=r.Footprint)return false;
                if(w.Proof!=null){proofs.Add(w.Proof);continue;}if(w.ProviderCurrent?.Invoke()!=true){w.Request?.Dispose();watches.Remove(id);return false;}
                if(w.Request!=null)
                {
                    if(RebirthStationSnapshotEvidence.HasPublished(w.Request)&&RebirthStationSnapshotEvidence.TryCopy(w.Request,out var bytes))
                    {
                        string hash=null;bool valid=true;foreach(var c in w.Cells)if(!RebirthPlacedOriginalPublication.TrySnapshot(bytes,w.Chunk.X,w.Chunk.Z,c.Position,c.After,Key(r),Image(r),out hash,RebirthOriginalNativeBlockIdentity.NativeKey(c.Position),Convert.FromBase64String(c.Marker))){valid=false;break;}
                        if(valid){w.Proof=hash;w.Request.Dispose();w.Request=null;proofs.Add(hash);continue;}
                    }
                    if(UnityEngine.Time.realtimeSinceStartup-w.Started<=15f)return false;w.Request.Dispose();w.Request=null;
                }
                if(!Current(w)){w.Request?.Dispose();watches.Remove(id);return false;}var watched=w;w.Request=RebirthStationSnapshotEvidence.Watch(world,w.Chunk,w.Chunk.X,w.Chunk.Z,()=>Current(watched));if(w.Request==null)return false;w.Started=UnityEngine.Time.realtimeSinceStartup;
                lock(w.Chunk){if(!Current(w)||w.Chunk.InProgressSaving||w.Chunk.IsLocked||w.Chunk.NeedsDecoration||w.Chunk.InProgressDecorating||w.Chunk.NeedsLightCalculation||w.Chunk.InProgressLighting){w.Request.Dispose();w.Request=null;return false;}w.Chunk.InProgressSaving=true;}
                try{var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;if(provider==null)return false;provider.m_RegionFileManager.SaveChunkSnapshot(w.Chunk,true);}finally{lock(w.Chunk)w.Chunk.InProgressSaving=false;}return false;
            }
            proof=Combine(string.Empty,string.Join("|",proofs));return true;
        }catch{return false;}
    }
    internal static string Combine(string parent,string children){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(parent+"|"+children))).Replace("-","").ToLowerInvariant();}
    internal static void Complete(RebirthPlacedOriginal.Record r)
    {foreach(var pair in watches.Where(pair=>pair.Value.Original.Operation==r.Operation&&pair.Value.Original.World==r.World).ToArray()){pair.Value.Request?.Dispose();watches.Remove(pair.Key);}}
    internal static void Reset(World world)
    {foreach(var pair in watches.Where(pair=>ReferenceEquals(pair.Value.World,world)).ToArray()){pair.Value.Request?.Dispose();watches.Remove(pair.Key);}}
    internal static void Retire(World world,RebirthPlacedOriginal.Record r)
    {
        if(r.Phase!=5)return;try{foreach(var c in RebirthPlacedOriginalFootprint.Decode(r.Footprint)){var chunk=world.GetChunkSync(World.toChunkXZ(c.Position.x),World.toChunkXZ(c.Position.z)) as Chunk;if(chunk==null||chunk.InProgressUnloading||!RebirthPlacedOriginal.HasSavedTerminal(world,r.Operation,RebirthPlacedOriginal.Digest(r),c.Position,c.Birth))continue;if(chunk.ChunkCustomData.dict.TryGetValue(Key(r),out var marker)&&marker?.data!=null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null&&marker.data.SequenceEqual(Image(r))){chunk.ChunkCustomData.Remove(Key(r));chunk.isModified=true;}}}catch{}Complete(r);
    }
    internal static void Queue(Chunk chunk){if(chunk!=null&&loaded.Count<128)loaded.Enqueue(new WeakReference<Chunk>(chunk));}
    internal static void Update(World world){for(int i=0;i<2&&loaded.TryDequeue(out var weak);i++)if(weak.TryGetTarget(out var chunk)&&ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)&&!chunk.InProgressUnloading)Prune(world,chunk);}
    private static void Prune(World world,Chunk chunk)
    {
        if(world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world))return;
        foreach(var key in chunk.ChunkCustomData.dict.Keys.Where(value=>value.StartsWith("rebirth.placed.footprint.v1:",StringComparison.Ordinal)).Take(128).ToArray())
        {string suffix=key.Substring("rebirth.placed.footprint.v1:".Length);if(!Guid.TryParseExact(suffix,"N",out var operation)||!RebirthPlacedOriginal.TryReadTerminal(world,operation,out var r))continue;Retire(world,r);}
    }

}

[HarmonyPatch(typeof(Chunk),"read",new[]{typeof(PooledBinaryReader),typeof(uint),typeof(bool)})]
internal static class RebirthPlacedOriginalFootprintChunkRead
{private static void Postfix(Chunk __instance,bool __2){if(!__2)RebirthPlacedOriginalFootprintPublication.Queue(__instance);}}
