using System;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthSurvivorCreationRequest : NetPackage
{
    private RebirthSurvivorCreationNetworkRequest request = new RebirthSurvivorCreationNetworkRequest();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthSurvivorCreationRequest Setup(RebirthSurvivorCreationNetworkRequest value)
    {
        request = value != null ? value.Clone() : new RebirthSurvivorCreationNetworkRequest { Malformed = true, MalformedReason = "null-request" };
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        request = new RebirthSurvivorCreationNetworkRequest();
        request.ProtocolVersion = binary.ReadInt32();
        request.PlayerEntityId = binary.ReadInt32();
        request.RequestId = binary.ReadUInt64();
        request.Operation = (RebirthSurvivorCreationNetworkOperation)binary.ReadByte();
        request.ClientDefinitionHash = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxHashLength, request, "definition-hash");
        request.ClientDefinitionVersion = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxVersionLength, request, "definition-version");
        request.SourceProfileId = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxIdLength, request, "profile-id");
        request.SourceProfileName = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxProfileNameLength, request, "profile-name");
        request.BackgroundId = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxIdLength, request, "background-id");
        request.DietId = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxIdLength, request, "diet-id");

        int traitCount = binary.ReadUInt16();
        if (traitCount > RebirthSurvivorNetworkProtocol.MaxTraitIds)
            throw new InvalidDataException("Survivor creation request exceeds trait-count protocol limit.");
        for (int i = 0; i < traitCount; i++)
        {
            string id = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxIdLength, request, "trait-id");
            if (i < RebirthSurvivorNetworkProtocol.MaxTraitIds) request.TraitIds.Add(id);
        }

        int choiceCount = binary.ReadUInt16();
        if (choiceCount > RebirthSurvivorNetworkProtocol.MaxCreationChoices)
            throw new InvalidDataException("Survivor creation request exceeds creation-choice protocol limit.");
        for (int i = 0; i < choiceCount; i++)
        {
            string key = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxChoiceKeyLength, request, "choice-key");
            string value = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxChoiceValueLength, request, "choice-value");
            if (i < RebirthSurvivorNetworkProtocol.MaxCreationChoices && key.Length > 0 && !request.CreationChoices.ContainsKey(key))
                request.CreationChoices[key] = value;
        }
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        RebirthSurvivorCreationNetworkRequest value = request ?? new RebirthSurvivorCreationNetworkRequest();
        binary.Write(value.ProtocolVersion);
        binary.Write(value.PlayerEntityId);
        binary.Write(value.RequestId);
        binary.Write((byte)value.Operation);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.ClientDefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.ClientDefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.SourceProfileId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.SourceProfileName, RebirthSurvivorNetworkProtocol.MaxProfileNameLength);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.BackgroundId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.DietId, RebirthSurvivorNetworkProtocol.MaxIdLength);

        int traitCount = Math.Min(value.TraitIds.Count, RebirthSurvivorNetworkProtocol.MaxTraitIds);
        binary.Write((ushort)traitCount);
        for (int i = 0; i < traitCount; i++) RebirthSurvivorNetworkCodec.WriteString(binary, value.TraitIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength);

        string[] keys = new string[value.CreationChoices.Count];
        value.CreationChoices.Keys.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.Ordinal);
        int choiceCount = Math.Min(keys.Length, RebirthSurvivorNetworkProtocol.MaxCreationChoices);
        binary.Write((ushort)choiceCount);
        for (int i = 0; i < choiceCount; i++)
        {
            string key = keys[i];
            RebirthSurvivorNetworkCodec.WriteString(binary, key, RebirthSurvivorNetworkProtocol.MaxChoiceKeyLength);
            RebirthSurvivorNetworkCodec.WriteString(binary, value.CreationChoices[key], RebirthSurvivorNetworkProtocol.MaxChoiceValueLength);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || request == null || !ValidEntityIdForSender(request.PlayerEntityId))
            return;
        EntityPlayer player = world.GetEntity(request.PlayerEntityId) as EntityPlayer;
        if (player == null) return;
        RebirthSurvivorNetworkService.HandleServerCreationRequest(player, request);
    }

    public int GetLength() { return RebirthSurvivorNetworkCodec.EstimateCreationRequest(request); }
}

[Preserve]
public sealed class NetPackageRebirthSurvivorCreationResult : NetPackage
{
    private RebirthSurvivorCreationNetworkResponse response = new RebirthSurvivorCreationNetworkResponse();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRebirthSurvivorCreationResult Setup(RebirthSurvivorCreationNetworkResponse value)
    {
        response = value != null ? value.Clone() : new RebirthSurvivorCreationNetworkResponse();
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        response = new RebirthSurvivorCreationNetworkResponse();
        response.ProtocolVersion = binary.ReadInt32();
        response.RequestId = binary.ReadUInt64();
        response.Operation = (RebirthSurvivorCreationNetworkOperation)binary.ReadByte();
        response.Status = (RebirthSurvivorCreationNetworkStatus)binary.ReadByte();
        response.WasReplay = binary.ReadBoolean();
        response.ServerDefinitionHash = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxHashLength);
        response.ServerDefinitionVersion = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        response.CharacterRevision = binary.ReadInt64();
        response.MessageCode = RebirthSurvivorNetworkCodec.ReadString(binary, RebirthSurvivorNetworkProtocol.MaxIdLength);
        response.ValidationResult = RebirthSurvivorNetworkCodec.ReadCreationResult(binary);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        RebirthSurvivorCreationNetworkResponse value = response ?? new RebirthSurvivorCreationNetworkResponse();
        binary.Write(value.ProtocolVersion);
        binary.Write(value.RequestId);
        binary.Write((byte)value.Operation);
        binary.Write((byte)value.Status);
        binary.Write(value.WasReplay);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.ServerDefinitionHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.ServerDefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        binary.Write(value.CharacterRevision);
        RebirthSurvivorNetworkCodec.WriteString(binary, value.MessageCode, RebirthSurvivorNetworkProtocol.MaxIdLength);
        RebirthSurvivorNetworkCodec.WriteCreationResult(binary, value.ValidationResult);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthSurvivorClientState.ReceiveCreationResult(response);
    }

    public int GetLength() { return RebirthSurvivorNetworkCodec.EstimateCreationResponse(response); }
}

[Preserve]
public sealed class NetPackageRebirthSurvivorStateRequest : NetPackage
{
    private int protocolVersion = RebirthSurvivorNetworkProtocol.Version;
    private int playerEntityId;
    private long knownRevision;
    private bool force;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthSurvivorStateRequest Setup(int entityId, long revision, bool forceRefresh)
    {
        protocolVersion = RebirthSurvivorNetworkProtocol.Version;
        playerEntityId = entityId;
        knownRevision = Math.Max(0L, revision);
        force = forceRefresh;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        protocolVersion = binary.ReadInt32();
        playerEntityId = binary.ReadInt32();
        knownRevision = binary.ReadInt64();
        force = binary.ReadBoolean();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(protocolVersion);
        binary.Write(playerEntityId);
        binary.Write(knownRevision);
        binary.Write(force);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote()) return;
        if (protocolVersion != RebirthSurvivorNetworkProtocol.Version)
        {
            Log.Warning("[REBIRTH Survivor] rejected owner-state request protocol mismatch entity=" + playerEntityId
                + " client=" + protocolVersion + " server=" + RebirthSurvivorNetworkProtocol.Version);
            return;
        }
        if (!ValidEntityIdForSender(playerEntityId)) return;
        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        if (player == null) return;
        RebirthSurvivorNetworkService.SendOwnerState(player, knownRevision, force, "client-state-request");
    }

    public int GetLength() { return 20; }
}

[Preserve]
public sealed class NetPackageRebirthSurvivorOwnerState : NetPackage
{
    private RebirthSurvivorOwnerStateSnapshot snapshot = new RebirthSurvivorOwnerStateSnapshot();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRebirthSurvivorOwnerState Setup(RebirthSurvivorOwnerStateSnapshot value)
    {
        snapshot = value != null ? value.Clone() : new RebirthSurvivorOwnerStateSnapshot();
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        snapshot = RebirthSurvivorNetworkCodec.ReadOwnerState((BinaryReader)reader);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        RebirthSurvivorNetworkCodec.WriteOwnerState((BinaryWriter)writer, snapshot);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthSurvivorClientState.ReceiveOwnerState(snapshot);
    }

    public int GetLength() { return RebirthSurvivorNetworkCodec.EstimateOwnerState(snapshot); }
}
