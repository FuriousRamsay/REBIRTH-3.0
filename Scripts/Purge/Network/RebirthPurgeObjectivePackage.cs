using System.IO;
using UnityEngine.Scripting;
[Preserve]
public sealed class NetPackageRebirthPurgeObjectives : NetPackage
{
    private byte[] bytes;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeObjectives Setup(RebirthPurgeObjectiveFrame frame) { bytes=RebirthPurgeObjectiveFrame.Encode(frame);return this; }
    public override void read(PooledBinaryReader reader)
    {
        int length=reader.ReadInt32();if(length<79 || length>RebirthPurgeObjectiveFrame.MaximumBytes)throw new InvalidDataException("Invalid objective package.");
        bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
    }
    public override void write(PooledBinaryWriter writer) { base.write(writer);writer.Write(bytes.Length);writer.Write(bytes); }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge || connection==null || connection.IsServer || world==null || !world.IsRemote() || !ReferenceEquals(GameManager.Instance.World,world))return;
        RebirthPurgeObjectiveFrame frame;if(RebirthPurgeObjectiveFrame.TryDecode(bytes,out frame))RebirthPoiMapSync.ReceiveObjectives(world,frame);
    }
    public int GetLength() => 8+(bytes==null?0:bytes.Length);
}