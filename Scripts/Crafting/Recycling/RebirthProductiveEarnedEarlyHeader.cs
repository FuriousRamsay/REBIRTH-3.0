using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// A header reserves only one exact native target. It is not Before publication,
// productive work, an attempted wear, payment, or completion authority.
internal static partial class RebirthProductiveEarnedRemoteHost
{
    private sealed class Early
    {
        internal Guid Operation,Birth,Nonce;internal string Owner,Creation,World,Path;
        internal int Deaths;internal Vector3i Position;internal byte[] Manifest,Bytes;
    }
    private static readonly Dictionary<Guid,Early> early=new Dictionary<Guid,Early>();
    private static readonly List<Early> coldEarly=new List<Early>();
    private static World earlyWorld;private static string earlySave;private static bool earlyUnknown;
    internal static string EarlyManifest(RebirthProductiveEarnedRowProof.Frame frame,byte[] image)
    {
        if(frame==null||!frame.HasNativeBodies||image==null||image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();
        using(var stream=new MemoryStream(89))using(var writer=new BinaryWriter(stream))
        {
            writer.Write((byte)1);writer.Write(frame.Birth.ToByteArray());writer.Write(frame.BeforeElapsedOffset);writer.Write(frame.BeforeNative.Length);
            writer.Write(RebirthProductiveEarnedNativeBody.ComparisonDigest(frame.BeforeNative,frame.BeforeElapsedOffset));
            using(var sha=System.Security.Cryptography.SHA256.Create())writer.Write(sha.ComputeHash(image));
            writer.Flush();return Convert.ToBase64String(stream.ToArray());
        }
    }
    private static bool Manifest(byte[] bytes,out Guid birth,out int offset,out int length,out byte[] comparison,out string digest)
    {
        birth=Guid.Empty;offset=-1;length=0;comparison=null;digest=null;
        if(bytes==null||bytes.Length!=89||bytes[0]!=1)return false;
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {
            reader.ReadByte();birth=new Guid(reader.ReadBytes(16));offset=reader.ReadInt32();length=reader.ReadInt32();comparison=reader.ReadBytes(32);
            digest=BitConverter.ToString(reader.ReadBytes(32)).Replace("-","").ToLowerInvariant();
            return birth!=Guid.Empty&&length>=30&&length<=RebirthProductiveEarnedNativeBody.MaximumBodyBytes&&offset>=13&&offset<=length-8;
        }
    }
    private static byte[] EarlyBytes(Early item)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write((byte)1);writer.Write(item.Operation.ToByteArray());writer.Write(item.Nonce.ToByteArray());writer.Write(item.World);
            writer.Write(item.Owner);writer.Write(item.Creation);writer.Write(item.Deaths);writer.Write(item.Position.x);writer.Write(item.Position.y);writer.Write(item.Position.z);
            writer.Write(item.Manifest);writer.Flush();if(stream.Length>2048)throw new InvalidDataException();return stream.ToArray();
        }
    }
    private static Early ReadEarly(string path)
    {
        byte[] bytes=Read(path);
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
        {
            if(reader.ReadByte()!=1)throw new InvalidDataException();
            var item=new Early{Operation=new Guid(reader.ReadBytes(16)),Nonce=new Guid(reader.ReadBytes(16)),
                World=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44),Owner=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64),
                Creation=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512),Deaths=reader.ReadInt32(),
                Position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32()),Manifest=reader.ReadBytes(89),Path=path,Bytes=bytes};
            if(item.Operation==Guid.Empty||item.Nonce==Guid.Empty||item.Deaths<0||item.Owner.Length!=64||
                !RebirthOriginalActionScope.Validate(item.Owner,item.Creation)||
                !Manifest(item.Manifest,out item.Birth,out _,out _,out _,out _)||stream.Position!=stream.Length||
                !EarlyBytes(item).SequenceEqual(bytes)||Path.GetFileName(path)!=item.Operation.ToString("N")+".hold")throw new InvalidDataException();
            return item;
        }
    }
    private static void ObserveEarly()
    {
        var world=GameManager.Instance?.World;string save=Path.GetFullPath(GameIO.GetSaveGameDir());
        if(ReferenceEquals(world,earlyWorld)&&save==earlySave)return;
        early.Clear();coldEarly.Clear();earlyWorld=world;earlySave=save;earlyUnknown=false;
        if(world?.IsRemote()!=false)return;
        string root=Path.Combine(save,"RebirthProductiveEarned","remote");
        try
        {
            if(!Safe(root))throw new InvalidDataException();if(!Directory.Exists(root))return;
            var directories=new Queue<string>();directories.Enqueue(root);int entries=1;long total=0;
            while(directories.Count>0)
            foreach(string path in Directory.EnumerateFileSystemEntries(directories.Dequeue()))
            {
                if(!Safe(path))throw new InvalidDataException();
                if(Directory.Exists(path)){if(++entries>512)throw new InvalidDataException();directories.Enqueue(path);continue;}
                if(path.EndsWith(".hold.tmp",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();
                if(!path.EndsWith(".hold",StringComparison.Ordinal))continue;
                var item=ReadEarly(path);
                if(TryClosedEarly(item,out _)||TryNativeInheritedCustody(item))continue; // Positive immutable closure/native custody; no native hydration.
                long size=new FileInfo(path).Length;if(++entries>512||size<0||size>8192||(total+=size)>4*1024*1024)throw new InvalidDataException();
                if(item.World!=RebirthProductiveEarnedArtifact.WorldIdentity(world)||coldEarly.Count>=128||
                    coldEarly.Any(old=>old.Operation==item.Operation||old.Position.Equals(item.Position)))throw new InvalidDataException();
                coldEarly.Add(item);
            }
        }
        catch{earlyUnknown=true;} // Keep identifiable original target holds; refuse new admission.
    }
    private static bool EarlyBlocks(TileEntityWorkstation station)
    {
        if(!RebirthProductiveEarnedCoordinator.Enabled&&!RebirthProductiveEarnedUiOriginal.Enabled||station==null||GameManager.Instance?.World?.IsRemote()!=false)return false;
        ObserveEarly();var position=station.ToWorldPos();return coldEarly.Any(value=>value.Position.Equals(position));
    }
    internal static bool TryHoldEarly(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,Guid operation,
        string digest,string manifestText,out Guid nonce,out Guid birth)
    {
        nonce=birth=Guid.Empty;
        try
        {
            if(!RebirthProductiveEarnedCoordinator.Enabled&&!RebirthProductiveEarnedUiRemote.IsPreparing(actor,sender,station,null)||!installed||operation==Guid.Empty||manifestText==null||manifestText.Length>120||
                !RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return false;
            ObserveEarly();if(earlyUnknown)return false;
            byte[] manifest=Convert.FromBase64String(manifestText);
            if(Convert.ToBase64String(manifest)!=manifestText||!Manifest(manifest,out birth,out int offset,out int length,out var comparison,out var expectedDigest)||expectedDigest!=digest||
                !TryDescribe(actor,sender,station,creation,out var actualBirth)||birth!=actualBirth)return false;
            var existing=coldEarly.FirstOrDefault(value=>value.Operation==operation);
            if(coldEarly.Any(value=>value.Position.Equals(station.ToWorldPos())&&value.Operation!=operation))return false;
            if(existing!=null)
            {
                if(existing.Owner!=identity.StorageKey||existing.Creation!=creation||existing.Deaths!=actor.Died||existing.World!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||
                    !existing.Position.Equals(station.ToWorldPos())||!existing.Manifest.SequenceEqual(manifest)||!Read(existing.Path).SequenceEqual(existing.Bytes))return false;
                early[operation]=existing;nonce=existing.Nonce;return true; // Status only; no native upload replay.
            }
            if(coldEarly.Count>=4||RebirthRemoteResourceSourceCustody.BlocksStation(station)||RebirthRemoteResourcePlacedStationClientCustody.Blocks(station)||
                RebirthRemoteResourcePlacedStationCustody.Blocks(station)||RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out _)||
                !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out var actual,out int actualOffset)||actual.Length!=length||actualOffset!=offset||
                !RebirthProductiveEarnedNativeBody.ComparisonDigest(actual,actualOffset).SequenceEqual(comparison))return false;
            string path=Path.Combine(earlySave,"RebirthProductiveEarned","remote",identity.StorageKey,operation.ToString("N")+".hold");
            if(!Safe(path)||File.Exists(path)||File.Exists(path+".tmp"))return false;
            var item=new Early{Operation=operation,Nonce=Guid.NewGuid(),Birth=birth,Owner=identity.StorageKey,Creation=creation,Deaths=actor.Died,
                World=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world),Position=station.ToWorldPos(),Manifest=manifest,Path=path};item.Bytes=EarlyBytes(item);
            Directory.CreateDirectory(Path.GetDirectoryName(path));if(!Safe(path))return false;
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(item.Bytes,0,item.Bytes.Length);stream.Flush(true);}
            if(!Read(path+".tmp").SequenceEqual(item.Bytes))return false;File.Move(path+".tmp",path);
            var saved=ReadEarly(path);coldEarly.Add(saved);early.Add(operation,saved);nonce=saved.Nonce;return true;
        }
        catch{return false;}
    }
    internal static bool TryReadEarlyStatus(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,Guid operation,string digest,out Guid nonce,out Guid birth)
    {
        nonce=birth=Guid.Empty;
        try
        {
            if(!RebirthProductiveEarnedCoordinator.Enabled||!RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return false;
            string path=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned","remote",identity.StorageKey,operation.ToString("N")+".hold");
            if(!Safe(path)||!File.Exists(path)||File.Exists(path+".tmp"))return false;
            var item=ReadEarly(path);
            if(item.Operation!=operation||item.Owner!=identity.StorageKey||item.Creation!=creation||item.Deaths!=actor.Died||
                item.World!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||!item.Position.Equals(station.ToWorldPos())||
                !Manifest(item.Manifest,out birth,out _,out _,out _,out var actualDigest)||actualDigest!=digest)return false;
            nonce=item.Nonce;return true; // No header creation, admission or native effect.
        }
        catch{return false;}
    }
    private static bool MatchesEarly(EntityPlayer actor,TileEntityWorkstation station,string creation,RebirthProductiveEarnedRowProof.Frame frame,byte[] image,out Guid nonce)
    {
        nonce=Guid.Empty;ObserveEarly();
        if(earlyUnknown||!early.TryGetValue(frame.Operation,out var item)||item.Deaths!=actor.Died||item.Creation!=creation||
            !item.Position.Equals(station.ToWorldPos())||item.World!=frame.World||item.Birth!=frame.Birth||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||item.Owner!=identity.StorageKey||
            !item.Manifest.SequenceEqual(Convert.FromBase64String(EarlyManifest(frame,image)))||!Read(item.Path).SequenceEqual(item.Bytes))return false;
        nonce=item.Nonce;return true;
    }
}
