using System;
using System.IO;

// Fixed-size description, never authority. Packet direction/native session and
// retained original/grant are independently qualified by their handlers.
internal static class RebirthHydrationOperationFrame
{
    internal const int Size=96;
    internal static byte[] Encode(Guid operation,string digest)
    {
        if(operation==Guid.Empty||digest==null||digest.Length!=64)throw new InvalidDataException("Invalid hydration operation frame");
        foreach(char value in digest)if(!Hex(value))throw new InvalidDataException("Noncanonical hydration digest");
        return System.Text.Encoding.ASCII.GetBytes(operation.ToString("N")+digest);
    }
    internal static bool TryDecode(byte[] bytes,out Guid operation,out string digest)
    {
        operation=Guid.Empty;digest=null;
        if(bytes==null||bytes.Length!=Size)return false;
        foreach(byte value in bytes)if(!Hex((char)value))return false;
        string frame=System.Text.Encoding.ASCII.GetString(bytes);
        if(!Guid.TryParseExact(frame.Substring(0,32),"N",out var id)||id==Guid.Empty)return false;
        operation=id;digest=frame.Substring(32);return true;
    }
    private static bool Hex(char value)=>value>='0'&&value<='9'||value>='a'&&value<='f';
}
