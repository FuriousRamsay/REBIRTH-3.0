using System;using System.IO;using System.Linq;using System.Text;
internal static class CamperFrame
{
    internal static byte[] Read(BinaryReader reader)
    {
        int length=reader.ReadInt32();
        if(length<0 || length>RebirthCamperRemoteTransport.MaximumFrame)throw new InvalidDataException("Invalid camper frame size.");
        byte[] bytes=reader.ReadBytes(length);
        if(bytes.Length!=length)throw new EndOfStreamException("Incomplete camper frame.");
        return bytes;
    }
    internal static void Write(BinaryWriter writer,byte[] bytes)
    {RebirthCamperRemoteTransport.AssertFrame(bytes);writer.Write(bytes.Length);writer.Write(bytes);}
}
