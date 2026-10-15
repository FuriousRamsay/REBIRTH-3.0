using System;
using System.IO;

internal static class RebirthCamperQuickTransferFrame
{
    internal static byte[] Capture(int slot,ItemStack stack)
    {
        if(slot<0||stack==null)throw new ArgumentException("Exact backpack cell required.");
        using(var stream=new MemoryStream())
        using(var writer=new BinaryWriter(stream))
        {writer.Write(slot);writer.Write(RebirthCamperInventorySnapshot.Capture(new[]{stack}));return stream.ToArray();}
    }
    internal static byte[] Read(byte[] frame,out int slot)
    {
        if(frame==null||frame.Length<=4)throw new InvalidDataException("Missing quick-transfer cell.");
        using(var stream=new MemoryStream(frame,false))
        using(var reader=new BinaryReader(stream))
        {slot=reader.ReadInt32();if(slot<0)throw new InvalidDataException("Invalid backpack cell.");return reader.ReadBytes(frame.Length-4);}
    }
}
