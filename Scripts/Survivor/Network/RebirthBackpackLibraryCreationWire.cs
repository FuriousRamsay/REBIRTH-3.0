using System;
using System.IO;
using System.Text;

// Existing nonzero GUID frames remain byte-identical. The previously invalid
// zero GUID introduces a versioned, bounded full migrated identity extension.
public static class RebirthBackpackLibraryCreationWire
{
    public static int Length(string creation)=>Guid.TryParse(creation,out var id)&&id!=Guid.Empty?16:89;
    public static void Write(BinaryWriter writer,string creation)
    {
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized))throw new InvalidDataException("Invalid library creation identity.");
        if(Guid.TryParse(normalized,out var id)){writer.Write(id.ToByteArray());return;}
        writer.Write(Guid.Empty.ToByteArray());writer.Write((byte)2);writer.Write((byte)71);
        writer.Write(Encoding.ASCII.GetBytes(normalized));
    }
    public static string Read(BinaryReader reader)
    {
        var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new InvalidDataException("Truncated library creation identity.");
        var id=new Guid(bytes);if(id!=Guid.Empty)return id.ToString("N");
        if(reader.ReadByte()!=2||reader.ReadByte()!=71)throw new InvalidDataException("Invalid library identity extension.");
        bytes=reader.ReadBytes(71);if(bytes.Length!=71)throw new InvalidDataException("Truncated library identity extension.");
        var value=Encoding.ASCII.GetString(bytes);
        if(!RebirthSurvivorRequestScope.TryNormalize(value,out var normalized)||normalized!=value||Guid.TryParse(value,out _))
            throw new InvalidDataException("Invalid migrated library identity.");
        return normalized;
    }
}