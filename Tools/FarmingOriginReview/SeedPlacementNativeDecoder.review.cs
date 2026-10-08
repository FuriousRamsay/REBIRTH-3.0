// TOOLS ONLY. Exact installed voxel-only native payload decoder; no PlatformUserIdentifierAbs.FromStream.
using System;
using System.Collections.Generic;
using System.IO;
public static class SeedPlacementNativeDecoderReview
{
    public static bool TryDecode(byte[] bytes, ClientInfo sender, out NetPackageSetBlock original)
    {
        original = null;
        if (bytes == null || sender == null || bytes.Length > 65535 || bytes.Length < 2) return false;
        try
        {
            int id = bytes[0] | bytes[1] << 8;
            if (id != NetPackageManager.GetPackageId(typeof(NetPackageSetBlock))) return false;
            PlatformUserIdentifierAbs identity = null;
            int offset = 2;
            if (!MatchIdentity(bytes, sender.PlatformId, ref offset))
            {
                offset = 2;
                if (!MatchIdentity(bytes, sender.CrossplatformId, ref offset)) return false;
                identity = sender.CrossplatformId;
            }
            else identity = sender.PlatformId;
            // Count is native signed Int16. Exactly ONE voxel placement; no partial multi-change support.
            if (bytes.Length - offset < 2 || bytes[offset] != 1 || bytes[offset + 1] != 0) return false;
            offset += 2;
            // BlockValueRef tag1 + XYZ Int32 (13), actor Int32 (4), flags1.
            if (bytes.Length - offset < 18 + 4 || bytes[offset] != 1) return false;
            byte flags = bytes[offset + 17];
            // Require block replacement, forbid damage-only/reserved flags; density/texture/light preserved.
            if ((flags & 1) == 0 || (flags & 0xC2) != 0 || ((flags & 8) != 0 && (flags & 4) == 0)) return false;
            int changeLength = 18 + 6 + ((flags & 4) != 0 ? 1 : 0) + ((flags & 32) != 0 ? 8 : 0);
            if (bytes.Length - offset != changeLength + 4) return false; // Exact EOF BEFORE native Read.
            if (ReadInt32(bytes, offset + 13) != sender.entityId ||
                ReadInt32(bytes, offset + changeLength) != sender.entityId) return false;
            BlockChangeInfo change = null;
            int localActor = -1;
            using (MemoryStream blob = new MemoryStream(bytes, writable: false))
                StreamUtils.FromBlob(blob, reader =>
                {
                    reader.BaseStream.Position = offset;
                    change = new BlockChangeInfo();
                    change.Read(reader); // Proven tag1 path and fixed length; never PropRef/string/array decoder.
                    localActor = reader.ReadInt32();
                    if (reader.BaseStream.Position != bytes.Length) throw new InvalidDataException();
                });
            if (change == null || change.changedByEntityId != sender.entityId || localActor != sender.entityId) return false;
            // Native NetPackageSetBlock is NOT IMemoryPoolableObject; exact manager has null pool,
            // constructs parameterless via ctor.Invoke, and FreePackage is a no-op for this class.
            original = NetPackageManager.GetPackage<NetPackageSetBlock>();
            original.persistentPlayerId = identity;
            original.localPlayerThatChanged = localActor;
            original.blockChanges = new List<BlockChangeInfo>(1) { change };
            original.Sender = sender;
            return true;
        }
        catch (IOException) { return false; }
        catch (ArgumentException) { return false; }
        catch (OverflowException) { return false; }
    }
    private static int ReadInt32(byte[] bytes, int offset)
    {
        return bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;
    }
    private static bool MatchIdentity(byte[] packet, PlatformUserIdentifierAbs identity, ref int offset)
    {
        if (identity == null) return false;
        // Only authenticated SERVER-owned identity is serialized; attacker strings are never decoded.
        using (MemoryStream blob = StreamUtils.ToBlob(writer => identity.ToStream(writer)))
        {
            byte[] canonical = blob.ToArray();
            if (canonical.Length == 0 || canonical.Length > packet.Length - offset) return false;
            for (int i = 0; i < canonical.Length; i++) if (packet[offset + i] != canonical[i]) return false;
            offset += canonical.Length;
            return true;
        }
    }
}

