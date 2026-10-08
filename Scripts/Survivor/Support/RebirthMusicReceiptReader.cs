using System;
using System.IO;

public static class RebirthMusicReceiptReader
{
    public static bool Contains(byte[] bytes, string transactionId, bool applied)
    {
        Guid transaction;
        if (bytes == null || !Guid.TryParse(transactionId, out transaction) || transaction == Guid.Empty) return false;
        try
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                int version = reader.ReadByte();
                if (version != EntityBuffs.Version || version < 2) return false;
                int buffs = reader.ReadUInt16();
                // EntityBuffs.Read changes live stats. Parse detached values only.
                for (int i = 0; i < buffs; i++) new BuffValue().Read(reader, version);
                int variables = reader.ReadUInt16();
                string receipt = "rbMusic_" + transaction.ToString("N");
                bool found = false;
                for (int i = 0; i < variables; i++)
                {
                    string name = reader.ReadString();
                    float value = reader.ReadSingle();
                    if (name != receipt) continue;
                    if (found || value != (applied ? 1f : -1f)) return false;
                    found = true;
                }
                return found && stream.Position == stream.Length;
            }
        }
        catch (Exception e) when (e is IOException || e is ArgumentException || e is FormatException)
        { return false; }
    }
}
