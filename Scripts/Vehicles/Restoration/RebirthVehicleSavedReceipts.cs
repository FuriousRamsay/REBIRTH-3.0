using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class RebirthVehicleSavedReceipts
{
    public static bool TryRead(EntityPlayer player, ClientInfo sender, RebirthVehicleTransferPlan plan,
        Func<bool> isCurrentSession, out Dictionary<Guid, RebirthVehicleOwnerTransferResult> receipts)
    {
        receipts = null;
        if (player == null || sender?.InternalId == null || plan == null || isCurrentSession == null
            || !isCurrentSession() || sender.entityId != player.entityId
            || !RebirthWorldCharacterRepository.IsServerAuthority
            || !string.Equals(sender.InternalId.CombinedString, plan.ActorId, StringComparison.Ordinal)) return false;
        RebirthWorldCharacterRecord record;

        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete
            || record.Origin == null || !RebirthSurvivorRequestScope.Matches(plan.CreationId,record.Origin.CreationId)) return false;
        string directory = GameIO.GetPlayerDataDir();
        if (string.IsNullOrEmpty(directory)) return false;
        try
        {
            var saved = new PlayerDataFile();
            string path = Path.Combine(directory, sender.InternalId.CombinedString + "." + PlayerDataFile.EXT);
            // PlayerDataFile.Load can silently use .bak. Read only the current
            // primary so a failed native save cannot be acknowledged from backup.
            if (!SdFile.Exists(path)) return false;
            using (var stream = SdFile.OpenRead(path))
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(false))
            {
                reader.SetBaseStream(stream);
                if (reader.ReadChar() != 't' || reader.ReadChar() != 't' || reader.ReadChar() != 'p'
                    || reader.ReadChar() != '\0') return false;
                byte version = reader.ReadByte();
                if (version != PlayerDataFile.cFileVersion) return false;
                saved.Read(reader, version, StreamModeRead.Persistency);
                if (stream.Position != stream.Length) return false;
            }
            if (!isCurrentSession() || directory != GameIO.GetPlayerDataDir() || saved.buffData == null) return false;
            return RebirthVehicleReceiptReader.TryRead(saved.buffData.ToArray(),
                plan.Items.Select(item => item.ReceiptId), out receipts);
        }
        catch (Exception)
        {
            receipts = null;
            return false;
        }
    }
}
