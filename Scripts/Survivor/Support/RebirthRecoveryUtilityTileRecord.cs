using System;
using System.IO;

public enum RebirthRecoveryTileKind{Collector,Light,Sleeper,PoweredBlock,PowerSource,PoweredTrigger,RangedTrap,MeleeTrap,Forge}
// Current native tile body including base19, without tile constructors/read callbacks.
public static class RebirthRecoveryUtilityTileRecord
{
    public static bool TrySkip(byte[] payload,int offset,RebirthRecoveryTileKind kind,int itemsStart,Func<int,int> itemKind,out int endOffset)
    {
        endOffset=0;if(payload==null||payload.Length>RebirthRecoveryChunkPayload.MaximumExpandedBytes||offset<0||offset>=payload.Length||itemsStart<=0||itemKind==null)return false;
        try{using(var stream=new MemoryStream(payload,false))using(var r=new BinaryReader(stream))
        {
            stream.Position=offset;var c=new RebirthRecoveryForeignRecord.Cursor(r,itemsStart,itemKind);if(r.ReadUInt16()!=19)return false;c.Skip(20);
            switch(kind)
            {
                case RebirthRecoveryTileKind.Collector:
                    if(r.ReadUInt16()!=21)return false;Dictionary(r,c,8);Dictionary(r,c,12);c.Flag();c.Flag();Dictionary(r,c,1);Dictionary(r,c,1);for(int i=0;i<3;i++)Slots(r,c);break;
                case RebirthRecoveryTileKind.Forge:
                    Schema(r);c.Skip(8);for(int group=0;group<2;group++){int count=r.ReadByte();for(int i=0;i<count;i++)c.Stack();}c.Stack();c.Stack();c.Skip(8);c.Item(0);break;
                case RebirthRecoveryTileKind.Light:
                    Schema(r);c.Float();c.Float();c.Skip(5);c.Float();c.Skip(2);c.Float();c.Float();break;
                case RebirthRecoveryTileKind.Sleeper:
                    Schema(r);c.Float();c.Skip(2);c.Float();c.Skip(2);break;
                case RebirthRecoveryTileKind.PoweredBlock:
                case RebirthRecoveryTileKind.PowerSource:
                case RebirthRecoveryTileKind.PoweredTrigger:
                case RebirthRecoveryTileKind.RangedTrap:
                case RebirthRecoveryTileKind.MeleeTrap:
                    if(r.ReadInt32()!=1)return false;c.Flag();c.Skip(1);c.Skip(r.ReadByte()*12);c.Skip(12);c.Float();c.Float();Schema(r);
                    if(kind==RebirthRecoveryTileKind.RangedTrap||kind==RebirthRecoveryTileKind.MeleeTrap){Schema(r);Owner(r,c);}
                    else if(kind==RebirthRecoveryTileKind.PoweredTrigger){int trigger=r.ReadByte();if(trigger>4)return false;if(trigger==3)Owner(r,c);}
                    break;
                default:return false;
            }
            endOffset=checked((int)stream.Position);return true;
        }}catch{return false;}
    }
    private static void Schema(BinaryReader r){if(r.ReadUInt16()!=18)throw new InvalidDataException();}
    private static void Owner(BinaryReader r,RebirthRecoveryForeignRecord.Cursor c){if(c.Flag()){c.Skip(1);c.String();c.String();}}
    private static void Slots(BinaryReader r,RebirthRecoveryForeignRecord.Cursor c){int count=r.ReadInt16();if(count<0||count>4096)throw new InvalidDataException();for(int i=0;i<count;i++)c.Stack();}
    private static void Dictionary(BinaryReader r,RebirthRecoveryForeignRecord.Cursor c,int bytes){int count=r.ReadUInt16();if(count>4096)throw new InvalidDataException();for(int i=0;i<count;i++){c.String();c.Skip(bytes);}}
}