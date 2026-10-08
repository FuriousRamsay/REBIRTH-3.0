using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

#nullable disable

/// <summary>Shared framing limits for NPC packets. Readers reject malformed counts/strings
/// before allocating attacker-controlled collection sizes.</summary>
public static class RebirthNpcNetworkFraming
{
    public const int MaxId = 256;
    public const int MaxLabel = 512;
    public const int MaxPayload = 8192;
    public const int MaxInteractionEntries = 32;
    public const int MaxEquipmentSlots = 32;
    public const int MaxUnlockedCosmetics = 64;
    public const int MaxProgressionEntries = 256;
    public const int MaxProgressionStrings = 256;
    public const int MaxDogMarkers = 128;
    public const int MaxCompanions = 128;

    public static string ReadString(BinaryReader reader, int maxChars)
    {
        if (reader == null) throw new ArgumentNullException("reader");
        string value = reader.ReadString() ?? string.Empty;
        if (value.Length > maxChars || Encoding.UTF8.GetByteCount(value) > Encoding.UTF8.GetMaxByteCount(maxChars))
            throw new InvalidDataException("NPC network string exceeds configured bound.");
        return value;
    }

    public static void WriteString(BinaryWriter writer, string value, int maxChars)
    {
        if (writer == null) throw new ArgumentNullException("writer");
        string clean = value ?? string.Empty;
        if (clean.Length > maxChars) clean = clean.Substring(0, maxChars);
        writer.Write(clean);
    }

    public static int ReadCount(BinaryReader reader, int maximum, string field)
    {
        int value = reader.ReadInt32();
        if (value < 0 || value > maximum) throw new InvalidDataException((field ?? "count") + " exceeds configured bound.");
        return value;
    }

    public static int ReadByteCount(BinaryReader reader, int maximum, string field)
    {
        int value = reader.ReadByte();
        if (value > maximum) throw new InvalidDataException((field ?? "count") + " exceeds configured bound.");
        return value;
    }

    public static int ReadUShortCount(BinaryReader reader, int maximum, string field)
    {
        int value = reader.ReadUInt16();
        if (value > maximum) throw new InvalidDataException((field ?? "count") + " exceeds configured bound.");
        return value;
    }
}

/// <summary>
/// Server epochs identify an actual World incarnation. Clients learn the epoch from the
/// baseline-start packet before accepting any presentation snapshot from that world.
/// </summary>
public static class RebirthNpcNetworkEpoch
{
    private static readonly object Sync = new object();
    private static World serverWorld;
    private static uint nextServerEpoch = 1;
    private static uint activeServerEpoch;
    private static uint clientEpoch;
    private static uint nextConnectionEpoch = 1;
    private static uint clientConnectionEpoch;
    private static readonly Dictionary<int,uint> ServerConnectionEpochs = new Dictionary<int,uint>();

    public static uint GetServerEpoch()
    {
        lock (Sync)
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null) return activeServerEpoch;
            if (!object.ReferenceEquals(serverWorld, world) || activeServerEpoch == 0)
            {
                serverWorld = world;
                activeServerEpoch = nextServerEpoch++;
                if (activeServerEpoch == 0) activeServerEpoch = nextServerEpoch++;
            }
            return activeServerEpoch;
        }
    }

    public static uint IssueConnectionEpoch(int playerEntityId)
    {
        if (playerEntityId < 0) return 0;
        lock (Sync)
        {
            uint epoch = nextConnectionEpoch++;
            if (epoch == 0) epoch = nextConnectionEpoch++;
            ServerConnectionEpochs[playerEntityId] = epoch;
            return epoch;
        }
    }

    public static bool ValidateConnectionEpoch(int playerEntityId, uint epoch)
    {
        lock (Sync)
        {
            uint expected;
            return epoch != 0 && ServerConnectionEpochs.TryGetValue(playerEntityId, out expected) && expected == epoch;
        }
    }

    public static void BeginClientEpoch(uint epoch, uint connectionEpoch)
    {
        if (epoch == 0 || connectionEpoch == 0) return;
        lock (Sync) { clientEpoch = epoch; clientConnectionEpoch = connectionEpoch; }
    }

    public static bool AcceptClientEpoch(uint epoch)
    {
        lock (Sync) return epoch != 0 && clientEpoch != 0 && epoch == clientEpoch;
    }

    public static uint ClientEpoch { get { lock (Sync) return clientEpoch; } }
    public static uint ClientConnectionEpoch { get { lock (Sync) return clientConnectionEpoch; } }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            serverWorld = null;
            activeServerEpoch = 0;
            clientEpoch = 0;
            clientConnectionEpoch = 0;
            ServerConnectionEpochs.Clear();
        }
    }
}
