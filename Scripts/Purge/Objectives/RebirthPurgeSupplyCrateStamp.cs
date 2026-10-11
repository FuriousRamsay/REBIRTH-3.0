using System;
using System.IO;
using System.Text;

// Fixed-size trailer for the native per-entity blob. It adds no fields to the
// outer native save envelope and does not depend on unsaved crate buff variables.
internal sealed class RebirthPurgeSupplyCrateStamp
{
    internal const uint Magic=0x44535052; // RPSD in native little-endian order
    internal const int EncodedLength=77;
    internal readonly Guid World,Token;
    internal readonly string Player;
    internal readonly long Sequence;
    internal RebirthPurgeSupplyCrateStamp(Guid world,string player,long sequence,Guid token)
    {
        if(token!=RebirthPurgeSupplyAccount.DeliveryToken(world,player,sequence))throw new ArgumentException("Supply token differs from original saved-world owner sequence.");
        World=world;Player=player;Sequence=sequence;Token=token;
    }
    internal void Write(BinaryWriter writer)
    {
        if(writer==null)throw new ArgumentNullException(nameof(writer));
        writer.Write(Magic);writer.Write((byte)1);writer.Write(World.ToByteArray());
        var owner=new byte[32];for(int n=0;n<32;n++)owner[n]=Convert.ToByte(Player.Substring(n*2,2),16);
        writer.Write(owner);writer.Write(Sequence);writer.Write(Token.ToByteArray());
    }
    internal static bool TryRead(BinaryReader reader,out RebirthPurgeSupplyCrateStamp stamp)
    {
        stamp=null;if(reader==null||!reader.BaseStream.CanSeek)return false;
        long start=reader.BaseStream.Position;
        try
        {
            // An old/native-only blob has no trailer. Unknown or malformed extensions
            // are left untouched and cannot establish owner or delivery completion.
            if(reader.BaseStream.Length-start!=EncodedLength||reader.ReadUInt32()!=Magic||reader.ReadByte()!=1)return false;
            var world=reader.ReadBytes(16);var owner=reader.ReadBytes(32);long sequence=reader.ReadInt64();var token=reader.ReadBytes(16);
            if(world.Length!=16||owner.Length!=32||token.Length!=16)return false;
            string player=BitConverter.ToString(owner).Replace("-","").ToLowerInvariant();
            stamp=new RebirthPurgeSupplyCrateStamp(new Guid(world),player,sequence,new Guid(token));return true;
        }
        catch(ArgumentException){return false;}catch(IOException){return false;}
        finally{if(stamp==null)reader.BaseStream.Position=start;}
    }
}