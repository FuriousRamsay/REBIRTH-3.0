using System;
using System.IO;

// Exact item payload for pre-debit comparison; never substitutes or reconstructs an item.
internal static class RebirthResourceItemFingerprint
{
    internal static byte[] Capture(ItemValue item)
    {
        if(item==null||item.IsEmpty())return null;
        try
        {
            using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            { writer.SetBaseStream(stream);ItemValue.Write(item,writer);writer.Flush();return stream.ToArray(); }
        }
        catch{return null;}
    }
    internal static bool Matches(ItemValue item,byte[] admitted)
    {
        if(admitted==null)return false;
        byte[] current=Capture(item);
        if(current==null||current.Length!=admitted.Length)return false;
        for(int i=0;i<current.Length;i++)if(current[i]!=admitted[i])return false;
        return true;
    }
}
