using System;
using System.IO;

public enum RebirthGearOwnerReceipt { Rejected = -1, Applied = 1, Applying = 2 }

// Detached native blob only. Authentication, original world and exact inventory
// evidence belong to the caller. Missing is stage0, not applied or rejection.
public static class RebirthGearReceiptReader
{
    public static bool Contains(byte[] bytes,string transactionId,RebirthGearOwnerReceipt expected)
        => (expected==RebirthGearOwnerReceipt.Applying||expected==RebirthGearOwnerReceipt.Applied||expected==RebirthGearOwnerReceipt.Rejected)&&
            TryReadStage(bytes,transactionId,out var value)&&value==(float)expected;
    public static bool TryReadStage(byte[] bytes,string transactionId,out float stage)
    {
        stage=0;
        if(bytes==null||bytes.Length==0||bytes.Length>1024*1024||
            !Guid.TryParseExact(transactionId,"N",out var transaction)||transaction==Guid.Empty)return false;
        try
        {
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);int version=reader.ReadByte();
                if(version!=EntityBuffs.Version||version<2)return false;
                int buffs=reader.ReadUInt16();
                for(int i=0;i<buffs;i++)new BuffValue().Read(reader,version);
                int variables=reader.ReadUInt16();string key="rbGear_"+transaction.ToString("N");bool found=false;float candidate=0;
                for(int i=0;i<variables;i++)
                {
                    string name=reader.ReadString();float value=reader.ReadSingle();
                    if(!string.Equals(name,key,StringComparison.OrdinalIgnoreCase))continue;
                    if(name!=key||found||(value!=-1f&&value!=1f&&value!=2f))return false;
                    found=true;candidate=value;
                }
                if(stream.Position!=stream.Length)return false;
                stage=candidate;return true;
            }
        }
        catch{return false;}
    }
}