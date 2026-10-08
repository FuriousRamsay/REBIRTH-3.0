using System;
using System.IO;
using System.Text;

// Shared native ItemValue serialization; callers enforce their own item/slot policy.
public static class RebirthNativeItemCodec
{
    public static string Encode(ItemValue value)
    {
        using (var stream = new MemoryStream())
        using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            Encoding previous = writer.Encoding;
            try
            {
                writer.Encoding = new UTF8Encoding(false, false);
                writer.SetBaseStream(stream);
                ItemValue.Write(value, writer);
                writer.Flush();
                return Convert.ToBase64String(stream.ToArray());
            }
            finally { writer.Encoding = previous; }
        }
    }

    public static bool TryDecode(string encoded, out ItemValue value)
    {
        // Canonical current-version images are preflighted before native allocation and
        // rebound to their complete texture data before exact reencoding and publication.
        return RebirthNativeItemConformanceReader.TryDecodeCanonicalV9(encoded, out value);
    }
}