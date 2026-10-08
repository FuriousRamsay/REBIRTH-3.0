using System;
using System.IO;
using System.Text;

// Display data only, separate from transaction receipts and authoritative custody images.
public static class RebirthBackpackSellStashViewCodec
{
    public const int MaxBytes=262144;
    private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    private static void WriteText(BinaryWriter writer,string text,int maximum)
    {
        var bytes=Utf8.GetBytes(text??string.Empty);if(bytes.Length>maximum)throw new InvalidDataException("Sell Stash display text too large.");
        writer.Write(bytes.Length);writer.Write(bytes);
    }
    private static string ReadText(BinaryReader reader,int maximum)
    {
        int length=reader.ReadInt32();if(length<0||length>maximum)throw new InvalidDataException("Sell Stash display text too large.");
        var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return Utf8.GetString(bytes);
    }
    public static bool TryEncode(RebirthBackpackSellStashView view,out byte[] bytes)
    {
        bytes=null;if(view==null)return false;
        try
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                // Keep GUID payloads byte-compatible with version 1. Version 2 carries
                // the complete migrated creation identity using bounded UTF-8 text.
                writer.Write((byte)83); // Explicit Sell display discriminator; never a Theory view.
                bool guid=Guid.TryParse(view.CreationId,out var creation);
                writer.Write((byte)(guid?1:2));
                if(guid)writer.Write(creation.ToByteArray());else WriteText(writer,view.CreationId,71);
                writer.Write(view.GearRevision);
                writer.Write((byte)(view.TransferPending?1:0));WriteText(writer,view.BackpackItemId,256);writer.Write((byte)view.Capacity);
                for(int i=0;i<view.Capacity;i++)
                {
                    if(!view.TryGetSlot(i,out var stack))return false;
                    writer.Write((ushort)stack.count);WriteText(writer,stack.count==0?string.Empty:RebirthNativeItemCodec.Encode(stack.itemValue),MaxBytes);
                    if(stream.Length>MaxBytes)return false;
                }
                writer.Flush();bytes=stream.ToArray();return true;
            }
        }
        catch{return false;}
    }
    public static bool TryDecode(byte[] bytes,out RebirthBackpackSellStashView view)
    {
        view=null;if(bytes==null||bytes.Length==0||bytes.Length>MaxBytes)return false;
        try
        {
            using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadByte()!=83)return false;
                byte version=reader.ReadByte();string creation;
                if(version==1)creation=new Guid(reader.ReadBytes(16)).ToString("N");
                else if(version==2)
                {
                    creation=ReadText(reader,71);
                    if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||
                        normalized!=creation||Guid.TryParse(creation,out _))return false;
                }
                else return false;
                long revision=reader.ReadInt64();
                byte pending=reader.ReadByte();if(pending>1)return false;string itemId=ReadText(reader,256);int capacity=reader.ReadByte();
                if(capacity<=0||capacity>RebirthBackpackSellStashPolicy.MaxSlots||RebirthBackpackSellStashPolicy.CapacityForBackpack(itemId)!=capacity)return false;
                var slots=new ItemStack[capacity];
                for(int i=0;i<capacity;i++)
                {
                    int count=reader.ReadUInt16();string data=ReadText(reader,MaxBytes);
                    if(count==0){if(data.Length!=0)return false;slots[i]=ItemStack.Empty.Clone();}
                    else{if(!RebirthNativeItemCodec.TryDecode(data,out var value))return false;slots[i]=new ItemStack(value,count);}
                }
                return stream.Position==stream.Length&&RebirthBackpackSellStashView.TryCreateDisplay(creation,revision,itemId,pending==1,slots,out view);
            }
        }
        catch{return false;}
    }
}