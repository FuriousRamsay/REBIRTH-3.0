using System;
using System.IO;

// Current installed ECD38 Persistency grammar only. No native objects or callbacks.
public static class RebirthRecoveryForeignRecord
{
    // special IDs: item, falling block, falling blocks, falling tree, male, female, drone.
    // itemKind: -1 unresolved, 0 ordinary, 1 ItemClassModifier; exact loaded definitions.
    public static bool TrySkip(byte[] payload,int offset,int[] specialIds,int itemsStart,Func<int,int> itemKind,out int endOffset)
    {
        endOffset=0;
        if(payload==null||payload.Length>RebirthRecoveryChunkPayload.MaximumExpandedBytes||offset<0||offset>=payload.Length||specialIds==null||specialIds.Length!=7||itemsStart<=0||itemKind==null)return false;
        for(int i=0;i<7;i++)for(int j=0;j<i;j++)if(specialIds[i]==specialIds[j])return false;
        try{using(var stream=new MemoryStream(payload,false))using(var reader=new BinaryReader(stream))
        {
            var cursor=new Cursor(reader,itemsStart,itemKind);stream.Position=offset;
            if(reader.ReadByte()!=38) return false;int type=reader.ReadInt32();if(reader.ReadInt32()<=0)return false;
            for(int i=0;i<7;i++)cursor.Float();cursor.Flag();if(reader.ReadInt32()!=4)return false;cursor.Skip(8);
            bool player=type==specialIds[4]||type==specialIds[5];
            if(cursor.Flag()){if(reader.ReadInt32()!=11)return false;for(int i=0;i<(player?4:1);i++)cursor.Stat();if(player)cursor.Skip(1);}
            cursor.Skip(2);if(cursor.Flag()){int length=reader.ReadInt32();if(length<0||length>4*1024*1024)return false;cursor.Skip(length);}
            cursor.Skip(15);
            if(type==specialIds[0]){cursor.Skip(8);cursor.Stack();cursor.Skip(1);}
            else if(type==specialIds[1])cursor.Skip(12);
            else if(type==specialIds[2]){int count=reader.ReadInt32();if(count<0||count>4096)return false;cursor.Skip(checked(count*24));}
            else if(type==specialIds[3])cursor.Skip(24);
            else if(player){cursor.Stack();cursor.Skip(1);cursor.String();cursor.String();if(cursor.Flag())cursor.Profile();}
            cursor.Skip(reader.ReadUInt16());if(cursor.Flag())cursor.Trader();
            if(type==specialIds[6])cursor.Skip(8);
            cursor.Float();cursor.Skip(24);endOffset=checked((int)stream.Position);return true;
        }}catch{return false;}
    }
    internal sealed class Cursor
    {
        private readonly BinaryReader reader;private readonly int itemsStart;private readonly Func<int,int> itemKind;
        private int nodes,stringBytes;
        internal Cursor(BinaryReader value,int start,Func<int,int> kinds){reader=value;itemsStart=start;itemKind=kinds;}
        internal void Skip(int bytes){var s=reader.BaseStream;if(bytes<0||bytes>s.Length-s.Position)throw new EndOfStreamException();s.Position+=bytes;}
        internal bool Flag(){byte b=reader.ReadByte();if(b>1)throw new InvalidDataException();return b==1;}
        internal void Float(){float f=reader.ReadSingle();if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidDataException();}
        internal void Stat(){if(reader.ReadInt32()!=6)throw new InvalidDataException();for(int i=0;i<5;i++)Float();}
        internal void String(){uint length=0;for(int i=0;i<5;i++){byte b=reader.ReadByte();if(i==4&&(b&240)!=0)throw new InvalidDataException();length|=(uint)(b&127)<<(7*i);if((b&128)==0){if(i>0&&b==0)throw new InvalidDataException();int bytes=checked((int)length);if(bytes>1024*1024||(stringBytes=checked(stringBytes+bytes))>4*1024*1024)throw new InvalidDataException();Skip(bytes);return;}}throw new InvalidDataException();}
        internal void Stack(){if(reader.ReadUInt16()>0)Item(0);}
        internal void Item(int depth)
        {
            if(depth>16||++nodes>8192)throw new InvalidDataException();int version=reader.ReadByte();if(version==0)return;if(version!=9)throw new InvalidDataException();
            int flags=reader.ReadByte();if((flags&~3)!=0)throw new InvalidDataException();int type=reader.ReadUInt16();if((flags&1)!=0)type=checked(type+itemsStart);
            int kind=itemKind(type);if(kind!=0&&kind!=1)throw new InvalidDataException();Float();Skip(4);
            int metadata=reader.ReadByte();for(int i=0;i<metadata;i++){String();int tag=reader.ReadInt32();switch(tag){case 0:break;case 1:Float();break;case 2:Skip(4);break;case 3:String();break;default:throw new InvalidDataException();}}
            if((flags&2)!=0)Skip(reader.ReadByte()*5);
            if(kind==0)for(int group=0;group<2;group++){int count=reader.ReadByte();for(int i=0;i<count;i++)if(Flag())Item(depth+1);}
            Skip(4);if(Flag())Skip(8);
        }
        internal void Profile(){if(reader.ReadInt32()!=5)throw new InvalidDataException();String();Flag();String();Skip(1);for(int i=0;i<6;i++)String();}
        internal void Trader(){Skip(12);if(reader.ReadByte()!=2)throw new InvalidDataException();int count=reader.ReadInt32();if(count<0||count>4096)throw new InvalidDataException();for(int i=0;i<count;i++){Stack();Skip(1);Flag();}int tiers=reader.ReadByte();for(int i=0;i<tiers;i++){int slots=reader.ReadUInt16();if(slots>4096)throw new InvalidDataException();for(int j=0;j<slots;j++)Stack();}Skip(4);}
    }
}