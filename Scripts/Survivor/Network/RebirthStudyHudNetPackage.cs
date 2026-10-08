using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthStudyHudMode : byte
{
    None=0,
    Reading=1,
    Audiobook=2
}

[Preserve]
public sealed class NetPackageRebirthStudyHudSnapshot : NetPackage
{
    private RebirthStudyHudMode mode;
    private string itemId=string.Empty;
    private float progress;
    private float remainingSeconds;
    private bool slowAttention;
    private ulong sequence;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthStudyHudSnapshot Setup(RebirthStudyHudMode value,string id,float valueProgress,float remaining,bool slow,ulong valueSequence)
    {
        mode=value;
        itemId=id??string.Empty;
        progress=Mathf.Clamp01(valueProgress);
        remainingSeconds=Mathf.Max(0f,remaining);
        slowAttention=slow;
        sequence=valueSequence;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b=(BinaryReader)reader;
        mode=(RebirthStudyHudMode)b.ReadByte();
        itemId=RebirthSurvivorNetworkCodec.ReadString(b,RebirthSurvivorNetworkProtocol.MaxIdLength);
        progress=b.ReadSingle();
        remainingSeconds=b.ReadSingle();
        slowAttention=b.ReadBoolean();
        sequence=b.ReadUInt64();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter b=(BinaryWriter)writer;
        b.Write((byte)mode);
        RebirthSurvivorNetworkCodec.WriteString(b,itemId,RebirthSurvivorNetworkProtocol.MaxIdLength);
        b.Write(progress);
        b.Write(remainingSeconds);
        b.Write(slowAttention);
        b.Write(sequence);
    }

    public override void ProcessPackage(World world,GameManager callbacks)
    {
        RebirthStudyHudClientState.Receive(mode,itemId,progress,remainingSeconds,slowAttention,sequence);
    }

    public int GetLength()
    {
        return 26+RebirthSurvivorNetworkCodec.EstimateString(itemId,RebirthSurvivorNetworkProtocol.MaxIdLength);
    }
}

public static class RebirthStudyHudClientState
{
    private static RebirthStudyHudMode mode;
    private static string itemId=string.Empty;
    private static float progress;
    private static float remainingSeconds;
    private static bool slowAttention;
    private static float receivedAt;
    private static ulong lastSequence;
    internal const float SnapshotLifetimeSeconds=5f;

    internal static void Receive(RebirthStudyHudMode value,string id,float valueProgress,float remaining,bool slow,ulong sequence)
    {
        if(sequence<=lastSequence)return;
        lastSequence=sequence;
        mode=value;
        itemId=id??string.Empty;
        progress=Mathf.Clamp01(valueProgress);
        remainingSeconds=Mathf.Max(0f,remaining);
        slowAttention=slow;
        receivedAt=Time.unscaledTime;
    }

    public static bool TryGet(out RebirthStudyHudMode valueMode,out string id,out float valueProgress,out float remaining,out bool slow)
    {
        valueMode=mode;
        id=itemId;
        valueProgress=progress;
        slow=slowAttention;

        // Expiry hides presentation only; retain sequence to reject delayed duplicates.
        float age=Time.unscaledTime-receivedAt;
        if(mode==RebirthStudyHudMode.None || age<0f || age>=SnapshotLifetimeSeconds)
        {
            remaining=0f;
            return false;
        }

        float elapsed=Mathf.Max(0f,Time.unscaledTime-receivedAt);
        // Server remainingSeconds is already wall-clock time adjusted for reading speed.
        // Applying the attention multiplier again makes remote countdowns run too slowly.
        remaining=Mathf.Max(0f,remainingSeconds-elapsed);
        return true;
    }

    public static void Reset()
    {
        mode=RebirthStudyHudMode.None;
        itemId=string.Empty;
        progress=0f;
        remainingSeconds=0f;
        slowAttention=false;
        receivedAt=0f;
        lastSequence=0UL;
    }
}

public static class RebirthStudyHudNetworkService
{
    public const float SnapshotIntervalSeconds=0.25f;
    private static ulong nextSequence;

    public static void SendReading(EntityPlayer player,string itemId,float progress,float remaining,bool slow)
    {
        Send(player,RebirthStudyHudMode.Reading,itemId,progress,remaining,slow);
    }

    public static void SendAudiobook(EntityPlayer player,string sourceLiteratureId,float progress,float remaining)
    {
        Send(player,RebirthStudyHudMode.Audiobook,sourceLiteratureId,progress,remaining,false);
    }

    public static void SendClear(EntityPlayer player)
    {
        Send(player,RebirthStudyHudMode.None,string.Empty,0f,0f,false);
    }

    private static void Send(EntityPlayer player,RebirthStudyHudMode mode,string itemId,float progress,float remaining,bool slow)
    {
        if(player==null)return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c==null||!c.IsServer)return;
        if(player is EntityPlayerLocal)return;

        // HUD transport is presentation-only and must not interrupt study progress/completion.
        try
        {
            var packet=NetPackageManager.GetPackage<NetPackageRebirthStudyHudSnapshot>();
            if(packet==null)return;
            c.SendPackage(packet.Setup(mode,itemId,progress,remaining,slow,++nextSequence),
                _attachedToEntityId:player.entityId);
        }
        catch { }
    }
}
