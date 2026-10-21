using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;

// Only admitted original targets receive metadata. Tokens survive native save;
// no raw-air equality or newly minted recovery token can bind an old original.
internal static class RebirthOriginalNativeBlockIdentity
{
    private const string Prefix="rebirth.original.target.v1:";
    private const uint Magic=0x31494252;
    private const int MaximumClaims=8,MaximumChunkTargets=128,MaximumActive=128,MaximumSnapshots=8;
    internal const byte BlockPurpose=1,PlacementPurpose=2;
    private sealed class Claim{internal byte Purpose;internal Guid Operation,Before;internal string Digest;}
    private sealed class Image{internal Guid World,Token;internal ulong Raw;internal readonly List<Claim> Claims=new List<Claim>();}
    private static readonly List<Admission> pending=new List<Admission>();
    private struct Probe{internal bool Tracked,Reset;internal BlockValue Before;}
    private static Func<World,Guid,string,Vector3i,string,bool> blockTerminal,placementTerminal;
    private static bool installed;
    private static string Key(Vector3i position)=>Prefix+(position.x&15)+":"+position.y+":"+(position.z&15);
    private static bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static bool Purpose(byte purpose)=>purpose==BlockPurpose||purpose==PlacementPurpose;
    internal static bool RegisterBlockTerminalReader(Func<World,Guid,string,Vector3i,string,bool> reader)
    {if(reader==null)return false;if(blockTerminal!=null)return blockTerminal==reader;blockTerminal=reader;return true;}
    internal static bool RegisterPlacementTerminalReader(Func<World,Guid,string,Vector3i,string,bool> reader)
    {if(reader==null)return false;if(placementTerminal!=null)return placementTerminal==reader;placementTerminal=reader;return true;}
    private static bool Scope(World world,Vector3i position,out Chunk chunk)
    {
        chunk=null;if(!installed||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||position.y<0||position.y>=255)return false;
        chunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
        return chunk!=null&&!chunk.InProgressUnloading&&chunk.ChunkCustomData?.dict!=null;
    }
    private static byte[] Encode(Image value)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(Magic);writer.Write((byte)1);writer.Write(value.World.ToByteArray());writer.Write(value.Token.ToByteArray());writer.Write(value.Raw);writer.Write((byte)value.Claims.Count);
            foreach(var claim in value.Claims)
            {
                writer.Write(claim.Purpose);writer.Write(claim.Operation.ToByteArray());
                var digest=new byte[32];for(int index=0;index<digest.Length;index++)digest[index]=Convert.ToByte(claim.Digest.Substring(index*2,2),16);
                writer.Write(digest);writer.Write(claim.Before.ToByteArray());
            }
            writer.Flush();if(stream.Length>1024)throw new InvalidDataException();return stream.ToArray();
        }
    }
    private static Image Decode(ChunkCustomData marker,World world)
    {
        if(marker?.data==null||marker.data.Length>1024||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null||!Guid.TryParse(world.worldState?.Guid,out var worldId))return null;
        using(var stream=new MemoryStream(marker.data,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadUInt32()!=Magic||reader.ReadByte()!=1)return null;
            var result=new Image{World=new Guid(reader.ReadBytes(16)),Token=new Guid(reader.ReadBytes(16)),Raw=reader.ReadUInt64()};
            int count=reader.ReadByte();if(result.World!=worldId||result.Token==Guid.Empty||count<1||count>MaximumClaims)return null;
            for(int index=0;index<count;index++)
            {
                var claim=new Claim{Purpose=reader.ReadByte(),Operation=new Guid(reader.ReadBytes(16)),Digest=BitConverter.ToString(reader.ReadBytes(32)).Replace("-","").ToLowerInvariant(),Before=new Guid(reader.ReadBytes(16))};
                if(!Purpose(claim.Purpose)||claim.Operation==Guid.Empty||claim.Before==Guid.Empty||!Hex(claim.Digest)||result.Claims.Any(previous=>previous.Purpose==claim.Purpose&&previous.Operation==claim.Operation))return null;result.Claims.Add(claim);
            }
            return stream.Position==stream.Length&&Encode(result).SequenceEqual(marker.data)?result:null;
        }
    }
    private static bool Read(World world,Vector3i position,out Chunk chunk,out ChunkCustomData marker,out Image image)
    {
        marker=null;image=null;if(!Scope(world,position,out chunk)||!chunk.ChunkCustomData.dict.TryGetValue(Key(position),out marker))return false;
        image=Decode(marker,world);return image!=null&&image.Raw==Convert.ToUInt64(world.GetBlock(position).rawData);
    }
    internal static bool TryRead(World world,Vector3i position,out string token)
    {token=null;try{if(!Read(world,position,out _,out _,out var image))return false;token=image.Token.ToString("N");return true;}catch{return false;}}
    internal static string NativeKey(Vector3i position)=>Key(position);
    // Existing valid tracked payload only; never creates identity or a claim.
    // Native serialization additionally carries this key, max expiry, false
    // network flag and the payload length, all verified by the snapshot reader.
    internal static byte[] CopyMarker(World world,Vector3i position)
    {try{return Read(world,position,out _,out var marker,out _)?(byte[])marker.data.Clone():null;}catch{return null;}}
    // Detached recorded native marker syntax/context only, not a saved-effect
    // or current-target authority. The publication observer supplies that proof.
    internal static bool IsRecordedClaim(World world,byte[] payload,byte purpose,Guid operation,string digest,string beforeBirth,string afterBirth,uint raw)
    {
        try
        {
            if(world==null||payload==null||!Purpose(purpose))return false;
            var marker=new ChunkCustomData("recorded-original",ulong.MaxValue,false){data=(byte[])payload.Clone()};
            var image=Decode(marker,world);
            return image!=null&&image.Raw==raw&&image.Token.ToString("N")==afterBirth&&image.Claims.Any(claim=>claim.Purpose==purpose&&
                claim.Operation==operation&&claim.Digest==digest&&claim.Before.ToString("N")==beforeBirth);
        }
        catch{return false;}
    }
    internal static bool TryReadClaim(World world,Vector3i position,byte purpose,Guid operation,string digest,out string beforeBirth,out string currentBirth)
    {
        beforeBirth=currentBirth=null;
        try
        {
            if(!Purpose(purpose)||operation==Guid.Empty||!Hex(digest)||!Read(world,position,out _,out _,out var image))return false;
            var claim=image.Claims.SingleOrDefault(value=>value.Purpose==purpose&&value.Operation==operation&&value.Digest==digest);if(claim==null)return false;
            beforeBirth=claim.Before.ToString("N");currentBirth=image.Token.ToString("N");return true;
        }
        catch{return false;}
    }
    internal sealed class Admission:IDisposable
    {
        internal World World;internal Chunk Chunk;internal Vector3i Position;internal BlockValue Before;
        internal byte Purpose;internal Guid Operation;internal string Digest,Id,Directory,SavedProof;
        internal ChunkCustomData Marker;internal byte[] MarkerImage,Span;internal long Offset=-1;
        internal Func<bool> Authorized,ProviderCurrent;internal Func<byte[],bool> SnapshotProof;internal DateTime Expires,NextSave;internal bool Closed,Published;internal ChunkProviderGenerateWorld Provider;
        internal RebirthStationSnapshotEvidence.Request Request;
        internal bool Current()
        {
            try{return !Closed&&Expires>DateTime.UtcNow&&Authorized?.Invoke()==true&&ProviderCurrent?.Invoke()==true&&Directory==GameIO.GetSaveGameDir()&&
                Read(World,Position,out var chunk,out var marker,out var image)&&ReferenceEquals(chunk,Chunk)&&image.Token.ToString("N")==Id&&
                (Published||ReferenceEquals(marker,Marker)&&marker.data.SequenceEqual(MarkerImage))&&World.GetBlock(Position).Equals(Before)&&image.Claims.Any(claim=>claim.Purpose==Purpose&&claim.Operation==Operation&&claim.Digest==Digest&&claim.Before.ToString("N")==Id);}
            catch{return false;}
        }
        internal bool Saved()
        {
            if(Published)return Current();
            if(Request==null)TryStartSave(this);
            if(!Current()||Request==null||Span==null||!RebirthStationSnapshotEvidence.HasPublished(Request)||!RebirthStationSnapshotEvidence.TryCopy(Request,out var bytes)||Offset<8||Offset>bytes.Length-Span.Length)return false;
            for(int index=0;index<Span.Length;index++)if(bytes[(int)Offset+index]!=Span[index])return false;
            if(SnapshotProof!=null&&!SnapshotProof(bytes))return false;
            if(!Current())return false;
            if(SnapshotProof!=null)using(var hash=System.Security.Cryptography.SHA256.Create())SavedProof=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            Published=true;Request.Dispose();Request=null;Span=null;return Current();
        }
        public void Dispose(){if(Closed)return;Closed=true;Request?.Dispose();pending.Remove(this);}
    }
    internal static Admission Begin(World world,Vector3i position,byte purpose,Guid operation,string originalDigest,Func<bool> authorizedCurrent)
        =>Bind(world,position,purpose,operation,originalDigest,null,authorizedCurrent,true);
    internal static Admission Rebind(World world,Vector3i position,byte purpose,Guid operation,string originalDigest,string expectedToken,Func<bool> authorizedCurrent)
        =>Bind(world,position,purpose,operation,originalDigest,expectedToken,authorizedCurrent,false);
    private static Admission Bind(World world,Vector3i position,byte purpose,Guid operation,string digest,string expected,Func<bool> authorized,bool fresh)
    {
        try
        {
            if(!Purpose(purpose)||operation==Guid.Empty||!Hex(digest)||authorized?.Invoke()!=true||!Scope(world,position,out var chunk)||
                (purpose==BlockPurpose?blockTerminal:placementTerminal)==null||!Guid.TryParse(world.worldState.Guid,out var worldId))return null;
            if(!fresh&&(!Guid.TryParseExact(expected,"N",out var old)||old==Guid.Empty))return null;
            foreach(var expired in pending.ToArray())if(!expired.Current()||!ReferenceEquals(expired.World,world))expired.Dispose();
            foreach(var retained in pending.Where(value=>ReferenceEquals(value.World,world)&&value.Position==position&&value.Purpose==purpose&&value.Operation==operation).ToArray())
            {if(retained.Digest!=digest)return null;if(retained.Current()&&(retained.Published||retained.Request!=null)&&(fresh||retained.Id==expected))return retained;retained.Dispose();}
            if(pending.Count>=MaximumActive)return null;
            var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return null;
            if(fresh)
            {
                // Bounded loaded-chunk cleanup also frees target capacity left
                // by a terminal whose earlier Retire ran while unloaded.
                var keys=chunk.ChunkCustomData.dict.Keys.Where(name=>name.StartsWith(Prefix,StringComparison.Ordinal)).Take(MaximumChunkTargets+1).ToArray();
                if(keys.Length>MaximumChunkTargets)return null;
                foreach(string candidate in keys)
                {
                    var parts=candidate.Substring(Prefix.Length).Split(':');
                    if(parts.Length!=3||!int.TryParse(parts[0],out int x)||!int.TryParse(parts[1],out int y)||!int.TryParse(parts[2],out int z)||x<0||x>15||z<0||z>15||y<0||y>=255)continue;
                    var target=chunk.GetWorldPos()+new Vector3i(x,y,z);if(Key(target)!=candidate)continue;
                    PruneSavedTerminals(world,chunk,target);
                }
            }
            string key=Key(position);var before=world.GetBlock(position);Image image;
            if(chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))
            {image=Decode(marker,world);if(image==null||image.Raw!=Convert.ToUInt64(before.rawData))return null;}
            else
            {
                if(!fresh||chunk.ChunkCustomData.dict.Keys.Count(name=>name.StartsWith(Prefix,StringComparison.Ordinal))>=MaximumChunkTargets)return null;
                image=new Image{World=worldId,Token=Guid.NewGuid(),Raw=Convert.ToUInt64(before.rawData)};
            }
            var claim=image.Claims.SingleOrDefault(value=>value.Purpose==purpose&&value.Operation==operation);
            if(claim!=null){if(claim.Digest!=digest||claim.Before!=image.Token)return null;}
            else
            {
                if(!fresh||image.Claims.Count>=MaximumClaims)return null;
                claim=new Claim{Purpose=purpose,Operation=operation,Digest=digest,Before=image.Token};image.Claims.Add(claim);
            }
            if(!fresh&&image.Token.ToString("N")!=expected)return null;
            byte[] encoded=Encode(image);
            if(marker==null||!marker.data.SequenceEqual(encoded))
            {marker=new ChunkCustomData(key,ulong.MaxValue,false){data=encoded};chunk.ChunkCustomData.Set(key,marker);chunk.isModified=true;}
            string nativeDirectory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
            var result=new Admission{World=world,Chunk=chunk,Position=position,Before=before,Purpose=purpose,Operation=operation,Digest=digest,Id=image.Token.ToString("N"),
                Directory=GameIO.GetSaveGameDir(),Marker=marker,MarkerImage=(byte[])marker.data.Clone(),Authorized=authorized,Expires=DateTime.UtcNow.AddSeconds(30),Provider=provider};
            result.ProviderCurrent=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==nativeDirectory;
            if(!result.Current())return null;
            pending.Add(result);TryStartSave(result);return result;
        }
        catch{return null;}
    }
    private static void PruneSavedTerminals(World world,Chunk chunk,Vector3i admittedPosition)
    {
        // Only the admitted coordinate is inspected here. Its durable claims
        // carry the exact original birth even after a replacement rotated the
        // current token; neither current block equality nor age retires one.
        string key=Key(admittedPosition);
        if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))return;
        var image=Decode(marker,world);if(image==null)return;
        bool changed=false;
        foreach(var claim in image.Claims.ToArray())
        {
            var terminal=claim.Purpose==BlockPurpose?blockTerminal:placementTerminal;
            bool saved=false;try{saved=terminal?.Invoke(world,claim.Operation,claim.Digest,admittedPosition,claim.Before.ToString("N"))==true;}catch{}
            if(saved){image.Claims.Remove(claim);changed=true;}
        }
        if(!changed)return;
        foreach(var active in pending.Where(value=>ReferenceEquals(value.World,world)&&value.Position==admittedPosition&&!image.Claims.Any(claim=>claim.Purpose==value.Purpose&&claim.Operation==value.Operation&&claim.Digest==value.Digest)).ToArray())active.Dispose();
        if(image.Claims.Count==0)chunk.ChunkCustomData.Remove(key);
        else chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=Encode(image)});
        chunk.isModified=true;
    }
    private static void TryStartSave(Admission value)
    {
        if(value.Published||value.Request!=null||DateTime.UtcNow<value.NextSave||!value.Current()||pending.Count(item=>item.Request!=null)>=MaximumSnapshots)return;value.NextSave=DateTime.UtcNow.AddMilliseconds(250);
        try
        {
            var chunk=value.Chunk;value.Request=RebirthStationSnapshotEvidence.Watch(value.World,chunk,chunk.X,chunk.Z,value.Current,value.Marker);if(value.Request==null)return;
            lock(chunk)
            {
                if(!value.Current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)
                {value.Request.Dispose();value.Request=null;return;}
                chunk.InProgressSaving=true;
            }
            try{if(!value.Current()){value.Request.Dispose();value.Request=null;return;}value.Provider.m_RegionFileManager.SaveChunkSnapshot(chunk,true);}
            finally{lock(chunk)chunk.InProgressSaving=false;}
        }
        catch{value.Request?.Dispose();value.Request=null;} // The original Id/claim remains queryable and unpaid.
    }
    internal static bool Retire(World world,Vector3i position,byte purpose,Guid operation,string digest,string beforeBirth)
    {
        try
        {
            if(!Purpose(purpose)||!Hex(digest)||!Read(world,position,out var chunk,out var marker,out var image))return false;
            var claim=image.Claims.SingleOrDefault(value=>value.Purpose==purpose&&value.Operation==operation&&value.Digest==digest&&value.Before.ToString("N")==beforeBirth);
            var terminal=purpose==BlockPurpose?blockTerminal:placementTerminal;
            if(claim==null||terminal?.Invoke(world,operation,digest,position,beforeBirth)!=true)return false;
            image.Claims.Remove(claim);string key=Key(position);
            if(image.Claims.Count==0)chunk.ChunkCustomData.Remove(key);else chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=Encode(image)});
            chunk.isModified=true;foreach(var watch in pending.Where(value=>ReferenceEquals(value.World,world)&&value.Position==position&&value.Purpose==purpose&&value.Operation==operation).ToArray())watch.Dispose();return true;
        }
        catch{return false;}
    }
    internal static void Changed(Chunk chunk,WorldBase world,Vector3i local,BlockValue before,bool reset=false)
    {
        // O(1) lookup only: no metadata is minted for untracked world blocks,
        // including while cold originals have not rebound runtime observers.
        if(!installed||!ThreadManager.IsMainThread()||world is not World native||native.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,native)||
            local.y<0||local.y>=255||chunk==null||chunk.InProgressUnloading||chunk.ChunkCustomData?.dict==null||chunk.ChunkCustomData.dict.Count==0||!ReferenceEquals(native.GetChunkSync(chunk.X,chunk.Z),chunk))return;
        var after=chunk.GetBlock(local);reset=reset&&before.type==after.type;if(before.rawData==after.rawData&&!reset)return;var position=chunk.GetWorldPos()+local;string key=Key(position);
        if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))return;
        try{var image=Decode(marker,native);if(image==null||!reset&&image.Raw==Convert.ToUInt64(after.rawData))return;image.Token=Guid.NewGuid();image.Raw=Convert.ToUInt64(after.rawData);chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=Encode(image)});chunk.isModified=true;}
        catch{/* Corrupt tracked identity remains unknown, never silently replaced. */}
    }
    private static Probe Before(Chunk chunk,Vector3i local,bool reset=false)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!installed||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||local.y<0||local.y>=255||chunk==null||chunk.InProgressUnloading||
                chunk.ChunkCustomData?.dict==null||chunk.ChunkCustomData.dict.Count==0||!ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)||
                !chunk.ChunkCustomData.dict.ContainsKey(Key(chunk.GetWorldPos()+local)))return default;
            return new Probe{Tracked=true,Before=chunk.GetBlock(local),Reset=reset};
        }
        catch{return default;} // Preserve native invalid-coordinate/failure behavior.
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.original.block.target.identity.v1");
        foreach(var type in new[]{typeof(Commit),typeof(RawCommit),typeof(MarkerWrite),typeof(RebirthStationSnapshotUpdatePatch),typeof(RebirthStationChunkSerializedPatch),typeof(RebirthStationSnapshotResetPatch),typeof(RebirthStationSnapshotWritePatch),typeof(RebirthStationRegionPublishedPatch)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        installed=true;
    }
    [HarmonyPatch(typeof(Chunk),nameof(Chunk.SetBlockRaw))]
    private static class RawCommit
    {
        [HarmonyPriority(Priority.First+2)]private static void Prefix(Chunk __instance,int _x,int _y,int _z,out Probe __state)=>__state=Before(__instance,new Vector3i(_x,_y,_z));
        [HarmonyPriority(Priority.Last)]private static void Postfix(Chunk __instance,int _x,int _y,int _z,Probe __state){if(__state.Tracked)Changed(__instance,GameManager.Instance?.World,new Vector3i(_x,_y,_z),__state.Before);}
        private static Exception Finalizer(Chunk __instance,int _x,int _y,int _z,Probe __state,Exception __exception)
        {if(__exception!=null&&__state.Tracked)try{Changed(__instance,GameManager.Instance?.World,new Vector3i(_x,_y,_z),__state.Before);}catch{}return __exception;}
    }
    [HarmonyPatch(typeof(Chunk),"SetBlock",new[]{typeof(WorldBase),typeof(int),typeof(int),typeof(int),typeof(BlockValue),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(int)})]
    private static class Commit
    {
        [HarmonyPriority(Priority.First+2)]private static void Prefix(Chunk __instance,int __1,int __2,int __3,bool __5,bool __7,out Probe __state)=>__state=Before(__instance,new Vector3i(__1,__2,__3),__5&&__7);
        [HarmonyPriority(Priority.Last)]private static void Postfix(Chunk __instance,WorldBase __0,int __1,int __2,int __3,Probe __state,bool __runOriginal){if(__state.Tracked)Changed(__instance,__0,new Vector3i(__1,__2,__3),__state.Before,__runOriginal&&__state.Reset);}
        private static Exception Finalizer(Chunk __instance,WorldBase __0,int __1,int __2,int __3,Probe __state,Exception __exception)
        {if(__exception!=null&&__state.Tracked)try{Changed(__instance,__0,new Vector3i(__1,__2,__3),__state.Before,__state.Reset);}catch{}return __exception;}
    }
    [HarmonyPatch(typeof(ChunkCustomData),nameof(ChunkCustomData.Write))]
    private static class MarkerWrite
    {
        private static void Prefix(ChunkCustomData __instance,BinaryWriter _bw,out long __state)
        {__state=-1;try{if(pending.Any(value=>ReferenceEquals(value.Marker,__instance)&&value.Current()))__state=RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream);}catch{}}
        private static void Postfix(ChunkCustomData __instance,BinaryWriter _bw,long __state)
        {
            if(__state<8)return;
            try
            {
                foreach(var value in pending.Where(value=>ReferenceEquals(value.Marker,__instance)&&value.Current()).ToArray())
                {
                    var stream=_bw.BaseStream;long end=stream.Position,length=end-__state;if(length<1||length>2048||!stream.CanRead||!stream.CanSeek)return;
                    var bytes=new byte[(int)length];try{stream.Position=__state;int offset=0;while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)return;offset+=count;}}finally{stream.Position=end;}
                    value.Offset=__state;value.Span=bytes;
                }
            }
            catch{/* Observation cannot authorize or mutate a native snapshot. */}
        }
    }
}
