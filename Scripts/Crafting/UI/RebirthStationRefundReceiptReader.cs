using System;
using System.IO;

// Detached native buff blob evidence only; caller must authenticate the owning final player file.
internal static class RebirthStationRefundReceiptReader
{
    internal static bool Contains(byte[] bytes,Guid delivery,bool applied)
    {
        if(bytes==null||bytes.Length==0||bytes.Length>1024*1024||delivery==Guid.Empty)return false;
        try
        {
            using(var stream=new MemoryStream(bytes,false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                int version=reader.ReadByte();
                if(version!=EntityBuffs.Version||version<2)return false;
                int buffs=reader.ReadUInt16();
                for(int i=0;i<buffs;i++)new BuffValue().Read(reader,version);
                int variables=reader.ReadUInt16();
                string key="rbStationRefund_"+delivery.ToString("N");
                bool found=false;
                for(int i=0;i<variables;i++)
                {
                    string name=reader.ReadString();float value=reader.ReadSingle();
                    if(name!=key)continue;
                    if(found||value!=(applied?1f:2f))return false;
                    found=true;
                }
                return found&&stream.Position==stream.Length;
            }
        }
        catch{return false;}
    }
}