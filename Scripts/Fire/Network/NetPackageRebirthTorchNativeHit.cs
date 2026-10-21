using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthTorchNativeHit:NetPackage
{
    private int actor,slot,deaths;private long ordinal;private string worldGuid;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthTorchNativeHit Setup(int actorId,int heldSlot,int originalDeaths,string originalWorld,long actionOrdinal)
    {actor=actorId;slot=heldSlot;deaths=originalDeaths;worldGuid=originalWorld;ordinal=actionOrdinal;return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadByte()!=1)throw new InvalidDataException("Torch action courier version differs");actor=reader.ReadInt32();slot=reader.ReadInt32();deaths=reader.ReadInt32();worldGuid=RebirthCookingGridCustody.ReadWireString(reader,128);ordinal=reader.ReadInt64();}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(actor);writer.Write(slot);writer.Write(deaths);RebirthCookingGridCustody.WriteWireString(writer,worldGuid,128);writer.Write(ordinal);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {try{RebirthTorchNativeHit.Receive(this,world,actor,slot,deaths,worldGuid,ordinal);}catch(Exception error){Log.Warning("[REBIRTH Fire] Torch action failed without replay: "+error.Message);}}
    public int GetLength()=>32+System.Text.Encoding.UTF8.GetByteCount(worldGuid??string.Empty);
}
