using System;
using System.IO;
using System.Text;

// BinaryWriter-compatible string framing, bounded before allocating decoded bytes.
internal static class RebirthGearRecoveryStringWire
{
    internal const int MaximumCharacters=4096,MaximumUtf8Bytes=16384;
    private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    internal static void Validate(string value)
    {
        if(value==null||value.Length>MaximumCharacters||Utf8.GetByteCount(value)>MaximumUtf8Bytes)throw new InvalidDataException("Invalid recovery text.");
    }
    internal static string Read(BinaryReader reader)
    {
        uint length=0;bool ended=false;
        for(int i=0;i<5;i++)
        {
            byte part=reader.ReadByte();if(i==4&&(part&0xf0)!=0)throw new InvalidDataException("Invalid recovery text length.");
            length|=(uint)(part&0x7f)<<(7*i);
            if((part&0x80)==0){if(i>0&&part==0)throw new InvalidDataException("Noncanonical recovery text length.");ended=true;break;}
        }
        if(!ended||length>MaximumUtf8Bytes)throw new InvalidDataException("Recovery text too large.");
        var bytes=reader.ReadBytes((int)length);if(bytes.Length!=(int)length)throw new EndOfStreamException();
        string value=Utf8.GetString(bytes);Validate(value);return value;
    }
}