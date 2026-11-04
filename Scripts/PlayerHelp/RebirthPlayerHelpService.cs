using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

// A deliberately closed action set. No command text, actor ID or target is accepted from clients.
internal static class RebirthPlayerHelpService
{
    internal enum Operation : byte { Permissions, Status, Start, Stop, Force }
    internal static event Action<long, bool, string> Reply;
    private const int MaxResult = 6000;

    internal static void Request(long token, Operation operation)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        var world = GameManager.Instance?.World;
        if (connection == null || world == null) { Receive(token, false, Localization.Get("xuiRebirthPlayerHelpSessionUnavailable")); return; }
        if (world.IsRemote())
        {
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlayerHelpRequest>().Setup(token, (byte)operation));
            return;
        }
        Process(world, null, token, operation);
    }

    internal static void Process(World world, ClientInfo sender, long token, Operation operation)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) || connection == null || !connection.IsServer) return;
        EntityPlayer player;
        if (sender != null)
        {
            if (!sender.loginDone || !ReferenceEquals(connection.Clients.ForEntityId(sender.entityId), sender)) return;
            player = world.GetEntity(sender.entityId) as EntityPlayer;
        }
        else
        {
            if (GameManager.IsDedicatedServer) return;
            player = world.GetPrimaryPlayer();
        }
        if (player == null || !ReferenceEquals(player.world, world) || !ReferenceEquals(world.GetEntity(player.entityId), player)) return;

        var console = SingletonMonoBehaviour<SdtdConsole>.Instance;
        var command = console?.GetCommand("poisleepers");
        // A local host has the same authority as the native local console. Remote users always use server policy.
        bool allowed = command != null && !command.IsExecuteOnClient && command.CanExecuteForDevice &&
            (sender == null || (GameManager.Instance.adminTools?.CommandAllowedFor(command.GetCommands(), sender) ?? false));
        string message;
        if (!allowed) message = Localization.Get("xuiRebirthPlayerHelpDenied");
        else if (operation == Operation.Permissions) message = Localization.Get("xuiRebirthPlayerHelpChooseAction");
        else
        {
            string arguments;
            switch (operation)
            {
                case Operation.Status: arguments = "status"; break;
                case Operation.Start: arguments = "start"; break;
                case Operation.Stop: arguments = "stop"; break;
                case Operation.Force: arguments = "start force"; break;
                default: return;
            }
            // Permission was checked above for this exact authenticated sender, never as the host on their behalf.
            try
            {
                List<string> lines = console.ExecuteSync("poisleepers " + arguments, sender);
                message = lines == null || lines.Count == 0 ? Localization.Get("xuiRebirthPlayerHelpNoMessage") : string.Join("\n", lines);
            }
            catch (Exception error) { message = Localization.Get("xuiRebirthPlayerHelpFailed") + error.Message; }
        }
        message = Bound(message);
        if (sender == null) Receive(token, allowed, message);
        else sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPlayerHelpReply>().Setup(token, allowed, message));
    }

    internal static string Bound(string text) => text == null ? string.Empty : text.Length <= MaxResult ? text : text.Substring(0, MaxResult);
    internal static void Receive(long token, bool allowed, string text) => Reply?.Invoke(token, allowed, Bound(text));
}

[Preserve]
public sealed class NetPackageRebirthPlayerHelpRequest : NetPackage
{
    private long token;
    private byte operation;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthPlayerHelpRequest Setup(long requestToken, byte requestedOperation)
    { token = requestToken; operation = requestedOperation; return this; }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); var binary = (BinaryWriter)writer; binary.Write(token); binary.Write(operation); }
    public override void read(PooledBinaryReader reader)
    { var binary = (BinaryReader)reader; token = binary.ReadInt64(); operation = binary.ReadByte(); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (Sender == null || operation > (byte)RebirthPlayerHelpService.Operation.Force || !ValidEntityIdForSender(Sender.entityId)) return;
        RebirthPlayerHelpService.Process(world, Sender, token, (RebirthPlayerHelpService.Operation)operation);
    }
}

[Preserve]
public sealed class NetPackageRebirthPlayerHelpReply : NetPackage
{
    private long token;
    private bool allowed;
    private string message;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthPlayerHelpReply Setup(long requestToken, bool permitted, string text)
    { token = requestToken; allowed = permitted; message = RebirthPlayerHelpService.Bound(text); return this; }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); var binary = (BinaryWriter)writer; binary.Write(token); binary.Write(allowed); binary.Write(message ?? string.Empty); }
    public override void read(PooledBinaryReader reader)
    { var binary = (BinaryReader)reader; token = binary.ReadInt64(); allowed = binary.ReadBoolean(); message = RebirthSurvivorNetworkCodec.ReadBoundedString(binary, 6000); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!ThreadManager.IsMainThread() || world == null || !world.IsRemote() || !ReferenceEquals(world, GameManager.Instance?.World)) return;
        RebirthPlayerHelpService.Receive(token, allowed, message);
    }
}
