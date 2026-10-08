using System.IO;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Persistent custom display name for player-owned composite storage.
/// Activation is handled by RebirthContainerRenameService so this feature does
/// not need to expose ReadOnlySpan signatures to the mod compiler.
/// </summary>
[Preserve]
public sealed class TEFeatureRebirthContainerName : TEFeatureAbs
{
    public const string FeatureName = "TEFeatureRebirthContainerName";
    public const string EditCommand = FeatureName + ":edit";
    private const ushort SaveVersion = 1;

    private string customName = string.Empty;

    public string CustomName
    {
        get { return customName ?? string.Empty; }
    }

    public void SetCustomName(string value)
    {
        string normalized = RebirthContainerRenameService.NormalizeName(value);
        if (string.Equals(customName, normalized, System.StringComparison.Ordinal))
            return;

        customName = normalized;
        SetModified();
    }

    public override void CopyFromInternal(TileEntityComposite other)
    {
        TEFeatureRebirthContainerName source = other != null
            ? other.GetFeature<TEFeatureRebirthContainerName>()
            : null;
        customName = source != null ? source.CustomName : string.Empty;
    }

    public override void UpgradeDowngradeFrom(TileEntityComposite other)
    {
        base.UpgradeDowngradeFrom(other);
        CopyFromInternal(other);
    }

    public override void OnAdded(Vector3i blockPos, BlockValue blockValue)
    {
        base.OnAdded(blockPos, blockValue);
        customName = string.Empty;
    }

    public override void Reset(FastTags<TagGroup.Global> questTags)
    {
        base.Reset(questTags);
        customName = string.Empty;
    }

    public override void InitBlockActivationCommands(
        System.Action<BlockActivationCommand, TileEntityComposite.EBlockCommandOrder, TileEntityFeatureData> addCallback)
    {
        base.InitBlockActivationCommands(addCallback);
        addCallback(
            new BlockActivationCommand("edit", "pen", true),
            TileEntityComposite.EBlockCommandOrder.Normal,
            FeatureData);
    }

    public override void Read(PooledBinaryReader reader, StreamModeRead streamMode)
    {
        base.Read(reader, streamMode);
        BinaryReader binaryReader = (BinaryReader)reader;
        if (streamMode == StreamModeRead.Persistency)
        {
            if (Parent.UseLocalVersioning())
                binaryReader.ReadUInt16();
            else
                Parent.GetLegacyForkVersion();
        }
        customName = RebirthContainerRenameService.NormalizeName(binaryReader.ReadString());
    }

    public override void Write(PooledBinaryWriter writer, StreamModeWrite streamMode)
    {
        base.Write(writer, streamMode);
        BinaryWriter binaryWriter = (BinaryWriter)writer;
        if (streamMode == StreamModeWrite.Persistency)
            binaryWriter.Write(SaveVersion);
        binaryWriter.Write(CustomName);
    }
}
