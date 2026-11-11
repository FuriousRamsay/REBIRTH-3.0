using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Owner 2026-11-11: personal queue rows saved by the old saved-admission route carry an owner-action marker and are
/// held until their paid proof is found. On a dedicated server that proof lives only in the SERVER save
/// (RemoteResources outcome journal), never in the client's local save folder, so the front card stayed frozen forever
/// and blocked every card behind it. A remote client now asks the server; the server answers "used" only for an
/// operation of that same player whose outcome is terminal (phase 4), disposition "used", with the same binding.
/// Nothing is refunded or charged again: the row was already paid, it is simply allowed to finish natively.
/// </summary>
internal static class RebirthLegacyCraftServerRelease
{
    private const float RetrySeconds = 5f;
    private static readonly Dictionary<Guid, float> nextAsk = new Dictionary<Guid, float>();
    private static readonly HashSet<Guid> confirmed = new HashSet<Guid>();
    private static World boundWorld;

    private static void Bind()
    {
        var world = GameManager.Instance?.World;
        if (ReferenceEquals(world, boundWorld)) return;
        boundWorld = world; nextAsk.Clear(); confirmed.Clear();
    }

    /// <summary>Client: true once the server confirmed this row was paid and used. Otherwise asks (throttled).</summary>
    internal static bool ServerConfirmsUsed(EntityPlayerLocal player, Guid operation, string binding)
    {
        Bind();
        if (confirmed.Contains(operation)) return true;
        if (player?.world?.IsRemote() != true || string.IsNullOrEmpty(binding)) return false;
        float now = Time.realtimeSinceStartup;
        if (nextAsk.TryGetValue(operation, out float next) && now < next) return false;
        nextAsk[operation] = now + RetrySeconds;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return false;
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthLegacyCraftStatus>().Setup(player.entityId, operation, binding));
        RebirthDebug.Log("crafting", $"[REBIRTH ContainerCraft] asked server about saved recipe operation={operation:N}");
        return false;
    }

    internal static void ReceiveAnswer(Guid operation, bool used)
    {
        Bind();
        RebirthDebug.Log("crafting", $"[REBIRTH ContainerCraft] server answered saved recipe operation={operation:N} used={used}");
        if (used) confirmed.Add(operation);
    }

    /// <summary>Server: answer only for the asking player's own terminal, used outcome with the exact binding.</summary>
    internal static void Answer(World world, int entityId, Guid operation, string binding)
    {
        bool used = false;
        try
        {
            if (world.GetEntity(entityId) is EntityPlayer actor &&
                RebirthStablePlayerIdentity.TryResolveServerEntity(actor, out var owner) &&
                RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey, operation, out var record, out bool unknown) &&
                !unknown && record != null && record.Phase == 4 && record.Binding == binding)
                used = (string)record.Copy().Attribute("disposition") == "used";
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
        { used = false; }
        var client = SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(entityId);
        client?.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthLegacyCraftStatusReply>().Setup(operation, used));
    }
}

[Preserve]
public sealed class NetPackageRebirthLegacyCraftStatus : NetPackage
{
    private int player; private string operation, binding;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthLegacyCraftStatus Setup(int p, Guid op, string b)
    { player = p; operation = op.ToString("N"); binding = b; return this; }
    public override void write(PooledBinaryWriter w)
    { base.write(w); w.Write(player); w.Write(operation); w.Write(binding); }
    public override void read(PooledBinaryReader r)
    { player = r.ReadInt32(); operation = r.ReadString(); binding = r.ReadString(); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(player) ||
            !Guid.TryParseExact(operation, "N", out var op) || string.IsNullOrEmpty(binding) || binding.Length > 256) return;
        RebirthLegacyCraftServerRelease.Answer(world, player, op, binding);
    }
    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthLegacyCraftStatusReply : NetPackage
{
    private string operation; private bool used;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthLegacyCraftStatusReply Setup(Guid op, bool u)
    { operation = op.ToString("N"); used = u; return this; }
    public override void write(PooledBinaryWriter w)
    { base.write(w); w.Write(operation); w.Write(used); }
    public override void read(PooledBinaryReader r)
    { operation = r.ReadString(); used = r.ReadBoolean(); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || !world.IsRemote() || !Guid.TryParseExact(operation, "N", out var op)) return;
        RebirthLegacyCraftServerRelease.ReceiveAnswer(op, used);
    }
    public int GetLength() => 0;
}
