using System.IO;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Sends the external-name registry used by ordinary world containers whose composite
/// schemas cannot safely be extended.  A full snapshot is sent on join; single-entry
/// deltas are broadcast after a rename.
/// </summary>
[Preserve]
public sealed class NetPackageRebirthContainerNameSync : NetPackage
{
    private const int MaxEntries = 128;
    private const int MaxBlockNameLength = 256;
    private const int MaxDisplayNameLength = 256;
    private bool replace;
    private RebirthContainerNameRegistry.Entry[] entries =
        new RebirthContainerNameRegistry.Entry[0];

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToClient; }
    }

    public NetPackageRebirthContainerNameSync Setup(
        bool replaceExisting,
        RebirthContainerNameRegistry.Entry[] values)
    {
        replace = replaceExisting;
        entries = values ?? new RebirthContainerNameRegistry.Entry[0];
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        replace = binary.ReadBoolean();
        int count = binary.ReadInt32();
        if (count < 0 || count > MaxEntries)
            throw new InvalidDataException("Invalid container-name entry count.");

        entries = new RebirthContainerNameRegistry.Entry[count];
        for (int i = 0; i < count; i++)
        {
            entries[i] = new RebirthContainerNameRegistry.Entry
            {
                Position = StreamUtils.ReadVector3i(binary),
                BlockName = RebirthSurvivorNetworkCodec.ReadBoundedString(binary, MaxBlockNameLength),
                Name = RebirthContainerRenameService.NormalizeName(RebirthSurvivorNetworkCodec.ReadBoundedString(binary, MaxDisplayNameLength))
            };
        }
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(replace);
        int count = entries != null ? entries.Length : 0;
        if (count > MaxEntries) throw new InvalidDataException("Container-name packet exceeds entry bound.");
        binary.Write(count);
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            RebirthContainerNameRegistry.Entry entry = entries[i] ??
                new RebirthContainerNameRegistry.Entry();
            StreamUtils.Write(binary, entry.Position);
            RebirthSurvivorNetworkCodec.WriteString(binary, entry.BlockName, MaxBlockNameLength);
            RebirthSurvivorNetworkCodec.WriteString(binary, entry.Name, MaxDisplayNameLength);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthContainerNameRegistry.ApplyNetworkSnapshot(replace, entries);
    }

    public int GetLength()
    {
        int length = 12;
        if (entries == null)
            return length;
        for (int i = 0; i < entries.Length; i++)
        {
            RebirthContainerNameRegistry.Entry entry = entries[i];
            length += 20 + RebirthSurvivorNetworkCodec.EstimateString(entry != null ? entry.BlockName : string.Empty, MaxBlockNameLength)
                + RebirthSurvivorNetworkCodec.EstimateString(entry != null ? entry.Name : string.Empty, MaxDisplayNameLength);
        }
        return length;
    }
}
