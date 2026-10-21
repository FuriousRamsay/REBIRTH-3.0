using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthRecyclerClosedGrantV1:NetPackage,IMemoryPoolableObject
{
    internal Guid Group;internal int Total,Index,Count;internal byte[] Payload;private int charged;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthRecyclerClosedGrantV1 SetupSlice(Guid group,byte[] source,int index)
    {
        Reset();if(group==Guid.Empty||source==null||source.Length<1||source.Length>12*1024*1024||index<0||index>=(source.Length+8999)/9000)throw new InvalidDataException();
        int length=Math.Min(9000,source.Length-index*9000);RebirthRecyclerPlacedWireBudget.Reserve(ref charged,length);
        try{Payload=new byte[length];Buffer.BlockCopy(source,index*9000,Payload,0,length);Group=group;Total=source.Length;Index=index;Count=(Total+8999)/9000;return this;}
        catch{Reset();throw;}
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(Group.ToByteArray());writer.Write(Total);writer.Write(Index);writer.Write(Count);writer.Write((ushort)Payload.Length);writer.Write(Payload);}
    public override void read(PooledBinaryReader reader)
    {
        Reset();try
        {
            if(reader.ReadByte()!=1)throw new InvalidDataException();var id=reader.ReadBytes(16);if(id.Length!=16)throw new InvalidDataException();Group=new Guid(id);
            Total=reader.ReadInt32();Index=reader.ReadInt32();Count=reader.ReadInt32();int length=reader.ReadUInt16();
            if(Group==Guid.Empty||Total<1||Total>12*1024*1024||Count!=(Total+8999)/9000||Index<0||Index>=Count||length!=Math.Min(9000,Total-Index*9000))throw new InvalidDataException();
            RebirthRecyclerPlacedWireBudget.Reserve(ref charged,length);Payload=reader.ReadBytes(length);if(Payload.Length!=length)throw new InvalidDataException();
        }
        catch{Reset();throw;}
    }
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthRecyclerPlacedSettlement.ReceiveClosedAuthorityGrant(this,world);
    public void Reset(){RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);Group=Guid.Empty;Total=Index=Count=0;Payload=null;RebirthRecyclerPlacedWireBudget.Release(ref charged);}
    public void Cleanup()=>Reset();public static int GetPoolSize()=>32;
}
