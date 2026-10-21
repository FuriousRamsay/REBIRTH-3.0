using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

// Owner's positive never-attempted intent. The server journal remains tank authority.
internal static class RebirthCamperSinkOwnerIntent
{
    private const int Maximum=16*1024*1024;
    private sealed class Intent
    {internal Guid Operation,Vehicle,World;internal string Owner,Creation;internal int Deaths,Slot;internal RebirthCamperEnergyActorImage Before;}
    private static string PathFor(Guid operation)=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperSinkOwnerIntents",operation.ToString("N")+".intent");
    private static byte[] Encode(Intent intent)
    {
        using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory,Encoding.UTF8,true))
        {
            writer.Write(0x31495352);writer.Write(intent.Operation.ToByteArray());writer.Write(intent.Vehicle.ToByteArray());writer.Write(intent.World.ToByteArray());
            writer.Write(intent.Owner);writer.Write(intent.Creation);writer.Write(intent.Deaths);writer.Write(intent.Slot);intent.Before.Write(writer);writer.Flush();
            using(var hash=SHA256.Create())writer.Write(hash.ComputeHash(memory.ToArray()));writer.Flush();
            if(memory.Length>Maximum)throw new InvalidDataException("Sink owner intent too large.");return memory.ToArray();
        }
    }
    private static Intent Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length<32||bytes.Length>Maximum)throw new InvalidDataException("Sink owner intent size.");
        using(var hash=SHA256.Create())
        {var digest=hash.ComputeHash(bytes,0,bytes.Length-32);for(int i=0;i<32;i++)if(digest[i]!=bytes[bytes.Length-32+i])throw new InvalidDataException("Sink owner intent checksum.");}
        using(var memory=new MemoryStream(bytes,0,bytes.Length-32,false))using(var reader=new BinaryReader(memory,Encoding.UTF8,true))
        {
            if(reader.ReadInt32()!=0x31495352)throw new InvalidDataException("Sink owner intent version.");
            var intent=new Intent{Operation=new Guid(reader.ReadBytes(16)),Vehicle=new Guid(reader.ReadBytes(16)),World=new Guid(reader.ReadBytes(16)),
                Owner=reader.ReadString(),Creation=reader.ReadString(),Deaths=reader.ReadInt32(),Slot=reader.ReadInt32(),Before=RebirthCamperEnergyActorImage.Read(reader)};
            if(memory.Position!=memory.Length||intent.Operation==Guid.Empty||intent.Vehicle==Guid.Empty||intent.World==Guid.Empty||intent.Owner.Length!=64||
                string.IsNullOrEmpty(intent.Creation)||intent.Creation.Length>4096||intent.Deaths<0||intent.Slot<0||intent.Slot>=intent.Before.Belt.Length||
                !Encode(intent).SequenceEqual(bytes))throw new InvalidDataException("Sink owner intent scope.");return intent;
        }
    }
    private static byte[] Read(string path)
    {
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<=0||stream.Length>Maximum)throw new InvalidDataException("Bounded sink intent required.");
            var bytes=new byte[(int)stream.Length];int offset=0;
            while(offset<bytes.Length){int n=stream.Read(bytes,offset,bytes.Length-offset);if(n<=0)throw new EndOfStreamException();offset+=n;}
            if(stream.ReadByte()!=-1)throw new InvalidDataException("Sink intent changed.");return bytes;
        }
    }
    private static void Save(string path,byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if(File.Exists(path)){if(!Read(path).SequenceEqual(bytes))throw new InvalidDataException("Conflicting sink intent.");return;}
        if(File.Exists(path+".bak"))throw new InvalidDataException("Unresolved sink intent backup.");
        var candidates=Directory.GetFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp");string temporary;
        if(candidates.Length>0)
        {if(candidates.Length!=1||!Read(candidates[0]).SequenceEqual(bytes))throw new InvalidDataException("Unresolved sink intent candidate.");temporary=candidates[0];}
        else
        {temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}
        if(!RebirthDurableFileCommit.TryPublish(temporary,path,out var error)||!Read(path).SequenceEqual(bytes))throw new IOException("Sink intent publication failed: "+error);
    }
    private static Intent ReadIntent(string path)
    {
        var bytes=Read(path);var intent=Decode(bytes);
        if(File.Exists(path+".bak")&&!Read(path+".bak").SequenceEqual(bytes))throw new InvalidDataException("Conflicting sink intent backup.");
        foreach(var candidate in Directory.GetFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp"))
            if(!Read(candidate).SequenceEqual(bytes))throw new InvalidDataException("Conflicting sink intent candidate.");
        return intent;
    }
    internal static void Prepare(EntityPlayerLocal player,Guid operation,Guid vehicle,Guid world,string creation,int slot)
    {
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))throw new InvalidOperationException("Native sink owner required.");
        var intent=new Intent{Operation=operation,Vehicle=vehicle,World=world,Creation=creation,Owner=owner.StorageKey,Deaths=player.Died,Slot=slot,Before=RebirthCamperEnergyActorImage.Capture(player)};
        var bytes=Encode(intent);Decode(bytes);Save(PathFor(operation),bytes);
    }
    internal static bool CanResume(EntityPlayerLocal player,RebirthCamperSinkFrame frame)
    {
        try
        {
            string path=PathFor(frame.Operation),attempt=path+".attempt";
            if(File.Exists(attempt)||File.Exists(attempt+".bak")||Directory.GetFiles(Path.GetDirectoryName(path),Path.GetFileName(attempt)+".*.tmp").Length!=0)return false;
            string cancelled=path+".cancel";
            if(File.Exists(cancelled)||File.Exists(cancelled+".bak")||Directory.GetFiles(Path.GetDirectoryName(path),Path.GetFileName(cancelled)+".*.tmp").Length!=0)return false;
            var intent=ReadIntent(path);
            return intent.Operation==frame.Operation&&intent.Vehicle==frame.Vehicle&&intent.World==frame.World&&intent.Owner==frame.OwnerKey&&
                intent.Creation==frame.Creation&&intent.Deaths==player.Died&&intent.Slot==frame.Slot&&
                RebirthCamperEnergyItems.Same(intent.Before.Belt[intent.Slot],frame.Before)&&intent.Before.Matches(RebirthCamperEnergyActorImage.Capture(player));
        }
        catch{return false;}
    }
    internal static void MarkAttempt(EntityPlayerLocal player,RebirthCamperSinkFrame frame)
    {
        if(!CanResume(player,frame))throw new InvalidOperationException("Sink original cannot be rearmed.");
        Save(PathFor(frame.Operation)+".attempt",frame.Write());
    }
    internal static bool CancelUnattempted(EntityPlayerLocal player,RebirthCamperSinkFrame frame)
    {
        try
        {
            string path=PathFor(frame.Operation),attempt=path+".attempt";
            if(File.Exists(attempt)||File.Exists(attempt+".bak")||Directory.GetFiles(Path.GetDirectoryName(path),Path.GetFileName(attempt)+".*.tmp").Length!=0)return false;
            var intent=ReadIntent(path);
            if(intent.Operation!=frame.Operation||intent.Vehicle!=frame.Vehicle||intent.World!=frame.World||intent.Owner!=frame.OwnerKey||
                intent.Creation!=frame.Creation||intent.Deaths!=player.Died||intent.Slot!=frame.Slot||
                !RebirthCamperEnergyItems.Same(intent.Before.Belt[intent.Slot],frame.Before))return false;
            // This monotone record precedes the native cancellation receipt.
            // Future attempts must reject it even if an old Apply arrives.
            Save(path+".cancel",frame.Write());return true;
        }
        catch{return false;}
    }
}
