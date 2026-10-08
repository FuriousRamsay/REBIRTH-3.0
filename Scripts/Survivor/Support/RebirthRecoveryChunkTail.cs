using System;
using System.IO;

// Entire installed chunk46 Persistency suffix. Tile parser returns bounded next offset.
public static class RebirthRecoveryChunkTail
{
    public static bool TryValidate(byte[] payload,uint version,int offset,Func<int,int,int> skipTile)
    {
        if(payload==null||payload.Length>RebirthRecoveryChunkPayload.MaximumExpandedBytes||version!=46||offset<0||offset>=payload.Length||skipTile==null)return false;
        try{using(var stream=new MemoryStream(payload,false))using(var r=new BinaryReader(stream))
        {
            stream.Position=offset;var c=new RebirthRecoveryForeignRecord.Cursor(r,1,id=>-1);int tiles=r.ReadInt32();if(tiles<0||tiles>4096)return false;
            for(int i=0;i<tiles;i++){int type=r.ReadInt32(),body=checked((int)stream.Position);int end=skipTile(type,body);if(end<=body||end>payload.Length)return false;stream.Position=end;}
            if(c.Flag())c.Skip(32);
            for(int group=0;group<3;group++){int count=r.ReadByte();for(int i=0;i<count;i++)if(r.ReadInt32()<0)return false;}
            int devices=r.ReadInt16();if(devices<0)return false;
            while(devices>0){int x=r.ReadByte(),z=r.ReadByte(),run=r.ReadByte();if(x>15||z>15||run<=0||run>devices)return false;c.Skip(run);devices-=run;}
            c.Flag();int triggers=r.ReadInt16();if(triggers<0||triggers>4096)return false;
            for(int i=0;i<triggers;i++){int x=r.ReadInt32(),y=r.ReadInt32(),z=r.ReadInt32();if(x<0||x>15||y<0||y>255||z<0||z>15||r.ReadUInt16()!=5)return false;c.Skip(1);for(int j=0;j<3;j++)c.Skip(r.ReadByte());c.Flag();c.Flag();c.Flag();}
            return stream.Position==stream.Length;
        }}catch{return false;}
    }
}