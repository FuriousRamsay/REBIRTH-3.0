using System;
using System.Collections.Generic;
using System.IO;

// Admission preflight for the exact native ItemStack array format. No live mutation.
public static class RebirthStationNativeInputCodec
{
    public static bool IsRoundTrippable(IList<ItemStack> input)
    {
        if(input==null||input.Count>9)return false;
        try
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
                {
                    writer.SetBaseStream(stream);writer.Write((byte)input.Count);
                    foreach(var stack in input)
                    {
                        if(stack==null||stack.itemValue==null||stack.count<0||stack.count>ushort.MaxValue)return false;
                        stack.Write(writer);
                        if(stream.Length>256*1024)return false;
                    }
                    writer.Flush();
                }
                stream.Position=0;
                using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    reader.SetBaseStream(stream);
                    if(reader.ReadByte()!=input.Count)return false;
                    foreach(var original in input)
                    {
                        var decoded=new ItemStack().Read(reader);
                        if(!RebirthStationGridIngredients.IsSameStackSnapshot(original,decoded))return false;
                    }
                    return stream.Position==stream.Length;
                }
            }
        }
        catch{return false;}
    }
}