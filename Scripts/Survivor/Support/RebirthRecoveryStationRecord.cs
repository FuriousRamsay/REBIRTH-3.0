using System;
using System.IO;

// Native current workstation Persistency body, including base19. Read-only framing.
public static class RebirthRecoveryStationRecord
{
    public static bool TrySkip(byte[] payload,int offset,int itemsStart,Func<int,int> itemKind,out int endOffset)
    {
        endOffset=0;if(payload==null||payload.Length>RebirthRecoveryChunkPayload.MaximumExpandedBytes||offset<0||offset>=payload.Length||itemsStart<=0||itemKind==null)return false;
        try{using(var stream=new MemoryStream(payload,false))using(var reader=new BinaryReader(stream))
        {
            stream.Position=offset;var c=new RebirthRecoveryForeignRecord.Cursor(reader,itemsStart,itemKind);
            if(reader.ReadUInt16()!=19)return false;c.Skip(20);if(reader.ReadByte()!=50)return false;c.Skip(8);
            for(int group=0;group<4;group++)Array(reader,c);
            int queue=reader.ReadByte();for(int i=0;i<queue;i++)
            {
                if(reader.ReadUInt16()!=2)return false;c.Skip(2);c.Flag();c.Float();
                if(c.Flag()){c.Item(0);c.Skip(2);}c.Skip(5);c.Float();
                if(c.Flag())Recipe(reader,c);
            }
            int complete=reader.ReadInt16();if(complete<0||complete>4096)return false;
            for(int i=0;i<complete;i++){if(reader.ReadUInt16()!=1)return false;c.Skip(4);c.Stack();c.String();c.Skip(6);c.String();}
            c.Flag();c.Float();int melt=reader.ReadByte();for(int i=0;i<melt;i++)c.Float();c.Flag();Array(reader,c);
            endOffset=checked((int)stream.Position);return true;
        }}catch{return false;}
    }
    private static void Array(BinaryReader reader,RebirthRecoveryForeignRecord.Cursor c){int count=reader.ReadByte();for(int i=0;i<count;i++)c.Stack();}
    private static void Recipe(BinaryReader reader,RebirthRecoveryForeignRecord.Cursor c)
    {
        if(reader.ReadUInt16()!=1)throw new InvalidDataException();c.Skip(8);c.Flag();c.Float();c.Skip(4);c.String();int count=reader.ReadInt32();if(count<0||count>4096)throw new InvalidDataException();for(int i=0;i<count;i++)c.Stack();
    }
}