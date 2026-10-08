using System; using System.IO;public static class RebirthNpcPersistenceCodec
{
    private const uint Magic = 0x52424E50; // RBNP

    public static void Write(BinaryWriter writer, RebirthNpcRuntimeState state)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        if (state == null) throw new ArgumentNullException(nameof(state));
        writer.Write(Magic);
        writer.Write(RebirthNpcRuntimeState.CurrentSchemaVersion);
        writer.Write(state.StableId.High);
        writer.Write(state.StableId.Low);
        writer.Write(state.ProfileId ?? string.Empty);
        writer.Write((byte)state.Presence);
        writer.Write((byte)state.OwnershipKind);
        writer.Write(state.OwnerId ?? string.Empty);
        writer.Write((byte)state.Order);
        writer.Write((byte)state.Travel);
        writer.Write(state.HasGuardPosition);
        if (state.HasGuardPosition)
        {
            writer.Write(state.GuardPosition.x);
            writer.Write(state.GuardPosition.y);
            writer.Write(state.GuardPosition.z);
        }
        writer.Write(state.Revision);
        writer.Write(state.HasHumanAppearance);
        if (state.HasHumanAppearance)
        {
            RebirthNpcAppearanceBinaryCodec.Write(writer, state.HumanAppearance);
        }
        // Serialization is not a durable acknowledgement. The enclosing persistence owner
        // may clear Dirty only after its stream/file commit succeeds.
    }

    public static RebirthNpcRuntimeState Read(BinaryReader reader)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        if (reader.ReadUInt32() != Magic) throw new InvalidDataException("Invalid REBIRTH NPC persistence record.");
        ushort version = reader.ReadUInt16();
        if (version == 0 || version > RebirthNpcRuntimeState.CurrentSchemaVersion)
            throw new InvalidDataException("Unsupported REBIRTH NPC schema version: " + version);
        RebirthNpcRuntimeState state = new RebirthNpcRuntimeState(
            new RebirthNpcStableId(reader.ReadUInt64(), reader.ReadUInt64()),
            reader.ReadString());
        state.Presence = (RebirthNpcPresenceState)reader.ReadByte();
        state.OwnershipKind = (RebirthNpcOwnershipKind)reader.ReadByte();
        state.OwnerId = reader.ReadString();
        if (version >= 3)
        {
            state.Order = (RebirthNpcOrderState)reader.ReadByte();
            state.Travel = (RebirthNpcTravelState)reader.ReadByte();
            state.HasGuardPosition = reader.ReadBoolean();
            if (state.HasGuardPosition)
                state.GuardPosition = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
        state.Revision = reader.ReadUInt32();
        if (version >= 2)
        {
            state.HasHumanAppearance = reader.ReadBoolean();
            if (state.HasHumanAppearance)
            {
                state.HumanAppearance = RebirthNpcAppearanceBinaryCodec.Read(reader, version >= 4);
            }
        }
        if (!Enum.IsDefined(typeof(RebirthNpcPresenceState), state.Presence) ||
            !Enum.IsDefined(typeof(RebirthNpcOwnershipKind), state.OwnershipKind) ||
            !Enum.IsDefined(typeof(RebirthNpcOrderState), state.Order) ||
            !Enum.IsDefined(typeof(RebirthNpcTravelState), state.Travel))
            throw new InvalidDataException("REBIRTH NPC persistence contains an invalid enum value.");
        if (state.HasGuardPosition && (float.IsNaN(state.GuardPosition.x) || float.IsInfinity(state.GuardPosition.x) ||
            float.IsNaN(state.GuardPosition.y) || float.IsInfinity(state.GuardPosition.y) ||
            float.IsNaN(state.GuardPosition.z) || float.IsInfinity(state.GuardPosition.z)))
            throw new InvalidDataException("REBIRTH NPC persistence contains a non-finite guard position.");
        state.MarkPersisted();
        return state;
    }
}

