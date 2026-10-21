using System;
using System.IO;
using System.Text;

// Read-only exact installed v47 persistency channel proof. The caller still
// owns actual original commit/payment authority; a matching block is no receipt.
internal static class RebirthOriginalNativeBlockSnapshot
{
    internal static bool TryProve(byte[] snapshot,int chunkX,int chunkZ,Vector3i position,uint expectedRaw,ushort expectedDamage,
        string markerKey,byte[] markerImage,out string proof,sbyte? expectedDensity=null,string birthKey=null,byte[] birthImage=null,bool requireClearTexture=false,bool? expectedPainted=null)
    {
        proof=null;
        try
        {
            // Both readers consume the SAME detached published byte array. The
            // first proves full raw block plus exact typed custom-data receipt;
            // this additionally proves native damage in that very snapshot.
            if(!RebirthPlacedOriginalPublication.TrySnapshot(snapshot,chunkX,chunkZ,position,expectedRaw,markerKey,markerImage,out var savedProof,
                birthKey,birthImage,expectedDensity.HasValue?(int?)expectedDensity.Value:null))return false;
            using(var stream=new MemoryStream(snapshot,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                Skip(reader,28);
                for(int layer=0;layer<64;layer++)
                {if(!Flag(reader))continue;Skip(reader,Flag(reader)?1024:1);if(Flag(reader))Skip(reader,3072);}
                Channel(reader,1,-1,0);Skip(reader,256+256+32+256+1536+2);
                int custom=reader.ReadUInt16();
                for(int index=0;index<custom;index++)
                {string name=reader.ReadString();if(name.Length>4096)return false;reader.ReadUInt64();Flag(reader);Skip(reader,reader.ReadUInt16());}
                Skip(reader,768);Channel(reader,1,-1,0);Channel(reader,1,-1,0);
                int offset=(position.x&15)+((position.z&15)<<4)+(position.y&3)*256;
                if(Channel(reader,2,position.y>>2,offset)!=expectedDamage)return false;
                if(requireClearTexture||expectedPainted.HasValue)
                {ulong texture=Channel(reader,6,position.y>>2,offset);if(requireClearTexture&&texture!=0UL||expectedPainted.HasValue&&(texture!=0UL)!=expectedPainted.Value)return false;}
                proof=savedProof;return true;
            }
        }
        catch{return false;}
    }
    private static bool Flag(BinaryReader reader)
    {byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static void Skip(BinaryReader reader,int count)
    {if(count<0||count>reader.BaseStream.Length-reader.BaseStream.Position)throw new EndOfStreamException();reader.BaseStream.Position+=count;}
    private static ulong Channel(BinaryReader reader,int bytesPerValue,int targetLayer,int offset)
    {
        ulong value=0;
        for(int layer=0;layer<64;layer++)
        {
            bool uniform=Flag(reader);
            for(int plane=0;plane<bytesPerValue;plane++)
            {
                int size=uniform?1:1024;
                if(layer!=targetLayer){Skip(reader,size);continue;}
                long start=reader.BaseStream.Position;if(!uniform)Skip(reader,offset);
                value|=(ulong)reader.ReadByte()<<(plane*8);
                reader.BaseStream.Position=start;Skip(reader,size);
            }
        }
        return value;
    }
}
