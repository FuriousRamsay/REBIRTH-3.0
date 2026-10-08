using System;
using System.IO;

// Current vending3/base19 grammar. Rentable flag is from exact loaded TraderInfo.
public static class RebirthRecoveryVendingRecord
{
    public static bool TrySkip(byte[] payload,int offset,int itemsStart,Func<int,int> itemKind,Func<int,int> traderRentable,out int endOffset)
    {
        endOffset=0;if(payload==null||payload.Length>RebirthRecoveryChunkPayload.MaximumExpandedBytes||offset<0||offset>=payload.Length||itemsStart<=0||itemKind==null||traderRentable==null)return false;
        try{using(var stream=new MemoryStream(payload,false))using(var r=new BinaryReader(stream))
        {
            stream.Position=offset;var c=new RebirthRecoveryForeignRecord.Cursor(r,itemsStart,itemKind);
            if(r.ReadUInt16()!=19)return false;c.Skip(20);if(r.ReadInt32()!=3)return false;c.Flag();Owner(c);c.String();
            int owners=r.ReadInt32();if(owners<0||owners>4096)return false;for(int i=0;i<owners;i++)Owner(c);c.Skip(4);
            int trader=r.ReadInt32();stream.Position-=4;int rentable=traderRentable(trader);if(rentable!=0&&rentable!=1)return false;c.Trader();if(rentable==1)c.Skip(8);
            endOffset=checked((int)stream.Position);return true;
        }}catch{return false;}
    }
    private static void Owner(RebirthRecoveryForeignRecord.Cursor c){if(c.Flag()){c.Skip(1);c.String();c.String();}}
}