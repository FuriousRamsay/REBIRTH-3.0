using System;
using System.Collections.Generic;
using System.IO;

// Parses detached native buff bytes without EntityBuffs.Read (which changes stats).
// A successfully parsed missing receipt is Pending, never evidence of rejection.
public static class RebirthVehicleReceiptReader
{
    public static bool TryRead(byte[] bytes, IEnumerable<Guid> receiptIds,
        out Dictionary<Guid, RebirthVehicleOwnerTransferResult> receipts)
    {
        receipts = null;
        if (bytes == null || receiptIds == null) return false;
        var names = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var result = new Dictionary<Guid, RebirthVehicleOwnerTransferResult>();
        foreach (Guid id in receiptIds)
        {
            if (id == Guid.Empty || result.Count >= 256 || result.ContainsKey(id)) return false;
            names.Add("rbVehicle_" + id.ToString("N"), id);
            result.Add(id, RebirthVehicleOwnerTransferResult.Pending);
        }
        try
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                int version = reader.ReadByte();
                if (version != EntityBuffs.Version || version < 2) return false;
                int buffs = reader.ReadUInt16();
                for (int i = 0; i < buffs; i++) new BuffValue().Read(reader, version);
                int variables = reader.ReadUInt16();
                var seen = new HashSet<Guid>();
                for (int i = 0; i < variables; i++)
                {
                    string name = reader.ReadString();
                    float value = reader.ReadSingle();
                    Guid id;
                    if (!names.TryGetValue(name, out id)) continue;
                    if (!seen.Add(id)) return false;
                    if (value == 1f) result[id] = RebirthVehicleOwnerTransferResult.Applied;
                    else if (value == -1f) result[id] = RebirthVehicleOwnerTransferResult.Rejected;
                    else if (value == 2f) result[id] = RebirthVehicleOwnerTransferResult.Indeterminate;
                    else if (value != 0f) return false;
                }
                if (stream.Position != stream.Length) return false;
            }
            receipts = result;
            return true;
        }
        catch (Exception e) when (e is IOException || e is ArgumentException || e is FormatException)
        { return false; }
    }
}
