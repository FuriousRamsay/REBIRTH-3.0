using System;
using System.IO;

#nullable disable

/// <summary>
/// Complete server-authoritative REBIRTH option snapshot. This is intentionally separate
/// from GameStats.SandboxCode and from the base game's NetPackageGameStats.
/// </summary>
public class NetPackageRebirthSandboxCode : NetPackage
{
    private const int MaxCodeLength = 4096;
    private string code;
    private int revision;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRebirthSandboxCode Setup(string rebirthSandboxCode, int rebirthRevision)
    {
        code = rebirthSandboxCode ?? string.Empty;
        revision = rebirthRevision;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        code = RebirthSurvivorNetworkCodec.ReadBoundedString((BinaryReader)reader, MaxCodeLength);
        revision = ((BinaryReader)reader).ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        RebirthSurvivorNetworkCodec.WriteString((BinaryWriter)writer, code, MaxCodeLength);
        ((BinaryWriter)writer).Write(revision);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        // Native ToClient dispatch enforces origin. Early join has no world yet.
        if (!ThreadManager.IsMainThread() ||
            world != null && !ReferenceEquals(world, GameManager.Instance?.World)) return;
        RebirthSandboxSyncService.ReceiveClientSnapshot(code, revision);
    }

    public int GetLength()
    {
        // BinaryWriter.Write(string) uses a length prefix plus UTF-8 bytes. REBIRTH codes
        // are ASCII, so this is a conservative small-payload estimate.
        return 8 + RebirthSurvivorNetworkCodec.EstimateString(code, MaxCodeLength);
    }
}

public static class RebirthSandboxSyncService
{
    private static bool hasReceivedClientSnapshot;
    private static string receivedClientCode = string.Empty;
    private static int receivedClientRevision;
    private static object receivedClientTransport;

    private static bool TryCurrentClientTransport(out object transport)
    {
        transport = null;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || connection == null || connection.IsServer || !connection.IsConnected)
            return false;
        var connections = connection.GetConnectionToServer();
        if (connections == null || connections.Length == 0 || connections[0] == null) return false;
        transport = connections[0];
        return true;
    }

    /// <summary>
    /// Sends the authoritative snapshot to one joining client. PlayerJoinedGame is invoked by
    /// base 3.1 at the beginning of GameManager.RequestToEnterGame, before WorldStaticData XML
    /// and NetPackageGameStats are queued, so REBIRTH policy is available during client setup.
    /// </summary>
    public static void SendSnapshot(ClientInfo clientInfo, string reason)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || clientInfo == null)
            return;

        RebirthSandboxOptionManager manager = RebirthSandboxOptionManager.Current;
        clientInfo.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthSandboxCode>()
                .Setup(manager.CurrentCode, manager.Revision));

        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[RebirthSandboxSync] Sent authoritative snapshot to client="
            + clientInfo.ClientNumber + " entity=" + clientInfo.entityId
            + " revision=" + manager.Revision + " reason=" + (reason ?? "unspecified")); }
    }

    public static void BroadcastSnapshot()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer)
            return;

        RebirthSandboxOptionManager manager = RebirthSandboxOptionManager.Current;
        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthSandboxCode>()
                .Setup(manager.CurrentCode, manager.Revision));
    }

    public static void ReceiveClientSnapshot(string code, int revision)
    {
        object transport;
        if (!TryCurrentClientTransport(out transport)) return;
        // Revisions are server-session local. A new server may legitimately start
        // lower, while early GameStarting within this transport must retain policy.
        bool newTransport = !ReferenceEquals(receivedClientTransport, transport);
        RebirthSandboxState ignored;
        if (!RebirthSandboxOptionManager.TryDecode(code, out ignored))
        {
            Log.Error("[RebirthSandboxSync] Rejected invalid authoritative sandbox code from server.");
            return;
        }
        // Invalid packets must not commit the transport or consume its first
        // valid force-load opportunity against a prior manager revision.
        if (newTransport)
        {
            ResetClientSnapshot();
            receivedClientTransport = transport;
        }

        // Keep the newest packet so an early join-phase packet survives the client's later
        // GameStarting callback. Equal revisions are accepted because the spawn-time resend is
        // intentionally idempotent and can repair a timing-sensitive client initialization.
        if (hasReceivedClientSnapshot && revision < receivedClientRevision)
            return;

        hasReceivedClientSnapshot = true;
        receivedClientCode = code;
        receivedClientRevision = Math.Max(0, revision);

        bool applied = newTransport
            ? RebirthSandboxOptionManager.Current.LoadAuthoritativeSnapshot(receivedClientCode, receivedClientRevision, true)
            : RebirthSandboxOptionManager.Current.ApplyNetworkSnapshot(receivedClientCode, receivedClientRevision);
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[RebirthSandboxSync] Received authoritative server snapshot revision="
            + receivedClientRevision + " applied=" + applied); }
    }

    public static bool TryApplyReceivedClientSnapshot(string reason)
    {
        object transport;
        if (!TryCurrentClientTransport(out transport) ||
            !ReferenceEquals(receivedClientTransport, transport))
        {
            ResetClientSnapshot();
            return false;
        }
        if (!hasReceivedClientSnapshot)
            return false;

        bool applied = RebirthSandboxOptionManager.Current.LoadAuthoritativeSnapshot(
            receivedClientCode, receivedClientRevision, true);
        if (applied)
        {
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[RebirthSandboxSync] Applied retained authoritative snapshot revision="
                + receivedClientRevision + " reason=" + (reason ?? "unspecified")); }
        }
        return applied;
    }

    public static void ResetClientSnapshot()
    {
        hasReceivedClientSnapshot = false;
        receivedClientCode = string.Empty;
        receivedClientRevision = 0;
        receivedClientTransport = null;
    }
}
