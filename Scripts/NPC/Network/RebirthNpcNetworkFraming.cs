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
public static partial class RebirthNpcNetworkEpoch
{
    private static readonly object Sync = new object();
    private static World serverWorld;
    private static World clientWorld;
    private static ConnectionManager serverManager,clientManager;
    private static INetConnection clientTransport;
    private static string clientDirectory;
    private static uint nextServerEpoch = 1;
    private static uint activeServerEpoch;
    private static uint clientEpoch;
    private static uint nextConnectionEpoch = 1;
    private static uint clientConnectionEpoch;
    private static Dictionary<int,uint> ServerConnectionEpochs = new Dictionary<int,uint>();

    public static uint GetServerEpoch()
    {
        var observedWorld=GameManager.Instance?.World;var observedManager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||observedWorld?.IsRemote()!=false||observedManager?.IsServer!=true||!RebirthNpcLifecycle.PrepareNativeEpochBoundary())return 0;
        lock (Sync)
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||manager?.IsServer!=true||
                !ReferenceEquals(world,observedWorld)||!ReferenceEquals(manager,observedManager))return 0;
            if(clientWorld!=null||clientManager!=null||clientTransport!=null||clientDirectory!=null||clientEpoch!=0||clientConnectionEpoch!=0)return 0;
            // An occupied original is never silently rebound to the next World.
            if(serverWorld!=null&&(!ReferenceEquals(serverWorld,world)||!ReferenceEquals(serverManager,manager)))return 0;
            if (activeServerEpoch == 0)
            {
                if(RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||ServerConnectionEpochs.Count!=0||nextServerEpoch==0)return 0;
                serverWorld = world;
                serverManager=manager;
                activeServerEpoch = TakeEpoch(ref nextServerEpoch);
            }
            return activeServerEpoch;
        }
    }

    public static uint IssueConnectionEpoch(int playerEntityId)
    {
        if (!ThreadManager.IsMainThread()||playerEntityId<=0||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||GetServerEpoch()==0) return 0;
        lock (Sync)
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var peer=manager?.Clients.ForEntityId(playerEntityId);
            if(manager?.IsServer!=true||peer==null||peer.entityId!=playerEntityId||!peer.loginDone||!peer.bAttachedToEntity||peer.disconnecting||peer.netConnection==null||peer.netConnection.Length==0||peer.netConnection[0]==null||peer.netConnection[0].IsDisconnected()||nextConnectionEpoch==0||(!ServerConnectionEpochs.ContainsKey(playerEntityId)&&ServerConnectionEpochs.Count>=8192))return 0;
            uint epoch = TakeEpoch(ref nextConnectionEpoch);
            ServerConnectionEpochs[playerEntityId] = epoch;
            return epoch;
        }
    }

    internal static bool TryGetServerConnectionEpoch(int entity,out uint epoch)
    {lock(Sync){epoch=0;return CurrentServerWorld()&&ServerConnectionEpochs.TryGetValue(entity,out epoch)&&epoch!=0;}}
    public static bool ValidateConnectionEpoch(int playerEntityId, uint epoch)
    {
        lock (Sync)
        {
            uint expected;
            return CurrentServerWorld()&&epoch != 0 && ServerConnectionEpochs.TryGetValue(playerEntityId, out expected) && expected == epoch;
        }
    }

    public static void BeginClientEpoch(uint epoch, uint connectionEpoch)
    {
        var world=GameManager.Instance?.World;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var transports=manager?.GetConnectionToServer();
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||manager==null||manager.IsServer||transports==null||transports.Length==0||transports[0]==null||transports[0].IsDisconnected()||epoch==0||connectionEpoch==0)return;
        if(!RebirthNpcLifecycle.PrepareNativeEpochBoundary()||!ReferenceEquals(world,GameManager.Instance?.World)||
            !ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance))return;
        lock(Sync)
        {
            var currentTransports=manager.GetConnectionToServer();
            if(manager.IsServer||world.IsRemote()!=true||currentTransports==null||currentTransports.Length==0||
                !ReferenceEquals(currentTransports[0],transports[0])||transports[0].IsDisconnected())return;
            if(serverWorld!=null||serverManager!=null||activeServerEpoch!=0||ServerConnectionEpochs.Count!=0)return;
            if(clientWorld!=null&&(!ReferenceEquals(clientWorld,world)||!ReferenceEquals(clientManager,manager)||!ReferenceEquals(clientTransport,transports[0])||clientDirectory!=GameIO.GetSaveGameDir()||clientEpoch!=epoch||clientConnectionEpoch!=connectionEpoch))return;
            clientWorld=world;clientManager=manager;clientTransport=transports[0];clientDirectory=GameIO.GetSaveGameDir();clientEpoch=epoch;clientConnectionEpoch=connectionEpoch;
        }
    }

    public static bool AcceptClientEpoch(uint epoch)
    {
        lock (Sync) return CurrentClientWorld()&&epoch != 0 && clientEpoch != 0 && epoch == clientEpoch;
    }

    public static uint ClientEpoch { get { lock (Sync) return CurrentClientWorld()?clientEpoch:0; } }
    public static uint ClientConnectionEpoch { get { lock (Sync) return CurrentClientWorld()?clientConnectionEpoch:0; } }
    private static bool CurrentServerWorld()=>ThreadManager.IsMainThread()&&serverWorld?.IsRemote()==false&&ReferenceEquals(serverWorld,GameManager.Instance?.World)&&ReferenceEquals(serverManager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&serverManager?.IsServer==true&&activeServerEpoch!=0;
    private static bool CurrentClientWorld()
    {
        if(!ThreadManager.IsMainThread()||clientWorld?.IsRemote()!=true||!ReferenceEquals(clientWorld,GameManager.Instance?.World)||!ReferenceEquals(clientManager,SingletonMonoBehaviour<ConnectionManager>.Instance)||clientManager==null||clientManager.IsServer||clientTransport==null||clientTransport.IsDisconnected()||clientDirectory!=GameIO.GetSaveGameDir())return false;
        var transports=clientManager.GetConnectionToServer();return transports!=null&&transports.Length>0&&ReferenceEquals(transports[0],clientTransport);
    }
    // Zero is exhausted, not an invitation to wrap and reissue a prior token.
    internal static uint TakeEpoch(ref uint next){uint value=next;if(value!=0)next=value==uint.MaxValue?0:value+1;return value;}
    internal static bool TryObserveCurrentOriginal(World world,out uint epoch)
    {
        lock(Sync)
        {
            epoch=0;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World)||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)return false;
            if(serverWorld==null)return serverManager==null&&activeServerEpoch==0&&ServerConnectionEpochs.Count==0;
            if(!CurrentServerWorld())return false;epoch=activeServerEpoch;return true;
        }
    }
    internal static bool MatchesOriginalObservation(World world,uint epoch)
    {lock(Sync)return ThreadManager.IsMainThread()&&(epoch==0?serverWorld==null&&serverManager==null&&activeServerEpoch==0&&ServerConnectionEpochs.Count==0:ReferenceEquals(serverWorld,world)&&serverManager!=null&&activeServerEpoch==epoch);}

    public static void ResetForWorldChange()
    {
        if(!RebirthNpcNetworkRetirementPreflight.CanReset)throw new InvalidOperationException("Original NPC network state requires qualified retirement");
        lock (Sync)
        {
            serverWorld = null;
            clientWorld = null;
            serverManager=clientManager=null;clientTransport=null;clientDirectory=null;
            activeServerEpoch = 0;
            clientEpoch = 0;
            clientConnectionEpoch = 0;
            ServerConnectionEpochs.Clear();
        }
    }
}
