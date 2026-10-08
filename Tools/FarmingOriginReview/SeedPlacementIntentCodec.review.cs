// TOOLS ONLY. Versioned command intent and bounded FULL seed identity; never native ItemValue.Write/Clone.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
public static class SeedPlacementIntentCodecReview
{
    public const int MaxSeedBytes = 8192;
    public const int MaxOriginalCount = 1000000; // Proposed command-image admission ceiling; NOT native authored max-stack.
    public struct Intent { public int Slot, OriginalCount; public Vector3i Position; public BlockValue Target, ExpectedOld; public byte Flags; public sbyte Density; public long Texture; public byte[] Seed; }
    public static bool TryRead(byte[] bytes, out Intent intent)
    {
        intent = default(Intent);
        if (bytes == null || bytes.Length < 1+8+12+16+10+4 || bytes.Length > MaxSeedBytes+51) return false;
        try { using (var stream=new MemoryStream(bytes,false)) using(var reader=new BinaryReader(stream)) {
            if(reader.ReadByte()!=3)return false;
            var result=new Intent();result.Slot=reader.ReadInt32();result.OriginalCount=reader.ReadInt32();
            if(result.OriginalCount<=0||result.OriginalCount>MaxOriginalCount)return false;
            result.Position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());
            result.Target=new BlockValue(reader.ReadUInt32(),reader.ReadInt32());
            result.ExpectedOld=new BlockValue(reader.ReadUInt32(),reader.ReadInt32());
            result.Flags=reader.ReadByte();result.Density=reader.ReadSByte();result.Texture=reader.ReadInt64();
            if(result.Flags!=0x15&&result.Flags!=0x11&&result.Flags!=0x21)return false;
            if((result.Flags&4)==0&&result.Density!=0)return false;
            if((result.Flags&32)==0&&result.Texture!=0)return false;
            int count=reader.ReadInt32();if(count<=0||count>MaxSeedBytes||count!=stream.Length-stream.Position)return false;
            result.Seed=reader.ReadBytes(count);intent=result;return true;
        }} catch(IOException){return false;} catch(ArgumentException){return false;}
    }
    public static byte[] Write(int slot, int originalCount, Vector3i pos, BlockValue target, BlockValue old, ItemValue seed)
    {
        if(originalCount<=0||originalCount>MaxOriginalCount)throw new InvalidDataException("Invalid original client count.");
        byte flags; sbyte density; long texture;
        if(!TryExpectedChange(seed,target,out flags,out density,out texture))throw new InvalidDataException("Unsupported native placement route.");
        byte[] identity; if(!TrySeedIdentity(seed,out identity))throw new InvalidDataException("Unsupported seed payload.");
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
            writer.Write((byte)3);writer.Write(slot);writer.Write(originalCount);writer.Write(pos.x);writer.Write(pos.y);writer.Write(pos.z);
            writer.Write(target.rawData);writer.Write(target.damage);writer.Write(old.rawData);writer.Write(old.damage);
            writer.Write(flags);writer.Write(density);writer.Write(texture);
            writer.Write(identity.Length);writer.Write(identity);return stream.ToArray();
        }
    }
    public static bool TryExpectedChange(ItemValue seed,BlockValue target,out byte flags,out sbyte density,out long texture)
    {
        flags=0;density=0;texture=0;if(seed==null)return false;
        if(!seed.TextureFullArray.IsDefault){flags=0x21;texture=seed.TextureFullArray[0];return true;}
        Block block=target.Block;
        // Only exact ordinary plant inheritance qualified. Terrain/other override routes stay closed.
        if(!(block is BlockPlantGrowing)||block.shape.IsTerrain())return false;
        if(block.IsTerrainDecoration){flags=0x11;return true;}
        flags=0x15;density=MarchingCubes.DensityAir;return true;
    }
    public static bool MatchesChange(Intent intent,BlockChangeInfo change)
    {
        if(change==null)return false;
        byte flags=(byte)((change.bChangeBlockValue?1:0)|(change.bChangeDamage?2:0)|(change.bChangeDensity?4:0)|
            (change.bForceDensity?8:0)|(change.bUpdateLight?16:0)|(change.bChangeTexture?32:0));
        return flags==intent.Flags&&change.blockValue.rawData==intent.Target.rawData&&change.blockValue.damage==intent.Target.damage&&
            ((flags&4)==0||change.density==intent.Density)&&((flags&32)==0||change.textureFull[0]==intent.Texture);
    }
    public static bool TrySeedIdentity(ItemValue source,out byte[] bytes)
    {
        bytes=null;if(source==null)return false;
        try {
            // Fixed-capacity stream refuses before expanding beyond the proposed byte budget.
            var buffer=new byte[MaxSeedBytes];using(var stream=new MemoryStream(buffer,0,buffer.Length,true,true)) {
                stream.SetLength(0);using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)) {
                    int nodes=256;WriteValue(writer,source,new HashSet<ItemValue>(ReferenceComparer.Instance),0,ref nodes);writer.Flush();
                    bytes=new byte[stream.Length];Array.Copy(buffer,bytes,bytes.Length);return true;
                }
            }
        }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(NotSupportedException){return false;}
    }
    private sealed class ReferenceComparer : IEqualityComparer<ItemValue>
    {
        internal static readonly ReferenceComparer Instance = new ReferenceComparer();
        public bool Equals(ItemValue a,ItemValue b){return object.ReferenceEquals(a,b);}
        public int GetHashCode(ItemValue value){return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);}
    }
    private static void WriteValue(BinaryWriter writer,ItemValue value,HashSet<ItemValue> path,int depth,ref int nodes)
    {
        if(value==null){writer.Write((byte)0);return;}
        if(depth>16||--nodes<0||!path.Add(value))throw new InvalidDataException("Seed graph exceeds bounds.");
        try {
            writer.Write((byte)1);writer.Write(value.type);writer.Write(value.Meta);writer.Write(value.UseTimes);
            writer.Write(value.Quality);writer.Write(value.Seed);writer.Write(value.Flags);writer.Write(value.SelectedAmmoTypeIndex);
            writer.Write(value.TextureFullArray[0]);
            int count=value.Stats==null?-1:value.Stats.Length;BoundCount(count);writer.Write((short)count);
            if(value.Stats!=null)foreach(var stat in value.Stats){writer.Write((int)stat.type);writer.Write(stat.isBoosted);writer.Write(stat.value);}
            WriteArray(writer,value.modifications,path,depth,ref nodes);WriteArray(writer,value.cosmeticMods,path,depth,ref nodes);
            count=value.Metadata==null?-1:value.Metadata.Count;BoundCount(count);writer.Write((short)count);
            if(value.Metadata!=null) {
                byte comparer;
                if(value.Metadata.Comparer.Equals(StringComparer.Ordinal))comparer=1;
                else if(value.Metadata.Comparer.Equals(StringComparer.OrdinalIgnoreCase))comparer=2;
                else if(value.Metadata.Comparer.Equals(EqualityComparer<string>.Default))comparer=0;
                else throw new InvalidDataException("Unsupported metadata comparer.");
                writer.Write(comparer);var keys=new List<string>(value.Metadata.Keys);keys.Sort(StringComparer.Ordinal);
                foreach(var key in keys) {
                    WriteString(writer,key);var metadata=value.Metadata[key];
                    writer.Write(metadata!=null);if(metadata==null)continue;
                    var tag=metadata.GetTypeTag();writer.Write((byte)tag);object item=metadata.GetValue();
                    writer.Write(item!=null);if(item==null)continue;
                    if(tag==TypedMetadataValue.TypeTag.Float&&item is float)writer.Write((float)item);
                    else if(tag==TypedMetadataValue.TypeTag.Integer&&item is int)writer.Write((int)item);
                    else if(tag==TypedMetadataValue.TypeTag.String&&item is string)WriteString(writer,(string)item);
                    else throw new InvalidDataException("Unsupported metadata value.");
                }
            }
        }finally{path.Remove(value);}
    }
    private static void WriteArray(BinaryWriter writer,ItemValue[] values,HashSet<ItemValue> path,int depth,ref int nodes)
    {int count=values==null?-1:values.Length;BoundCount(count);writer.Write((short)count);if(values!=null)foreach(var value in values)WriteValue(writer,value,path,depth+1,ref nodes);}
    private static void BoundCount(int count){if(count>255)throw new InvalidDataException("Seed container exceeds native count.");}
    private static void WriteString(BinaryWriter writer,string value)
    {if(value==null||value.Length>1024)throw new InvalidDataException("Seed string exceeds bound.");int length=new UTF8Encoding(false,true).GetByteCount(value);if(length>2048)throw new InvalidDataException("Seed UTF8 exceeds bound.");writer.Write((ushort)length);writer.Write(new UTF8Encoding(false,true).GetBytes(value));}
    public static bool ValidateSenderSnapshot(ClientInfo sender,World world,GameManager callbacks,Intent intent,NetPackageSetBlock packet)
    {
        if(sender==null||world==null||callbacks==null||callbacks.IsEditMode()||world.IsRemote()||
            !object.ReferenceEquals(callbacks.World,world)||packet==null||packet.blockChanges==null||packet.blockChanges.Count!=1)return false;
        var player=world.GetEntity(sender.entityId) as EntityPlayer;
        if(player==null||player.inventory==null||intent.Slot<0||intent.Slot>=player.inventory.SlotCount||
            player.inventory.SelectedSlot!=intent.Slot||player.inventory.holdingItemStack.count<=0)return false;
        Vector3i position;var change=packet.blockChanges[0];
        if(!change.blockValueRef.TryGetBlockPos(out position)||position.x!=intent.Position.x||position.y!=intent.Position.y||position.z!=intent.Position.z||
            change.changedByEntityId!=sender.entityId||change.blockValue.rawData!=intent.Target.rawData||change.blockValue.damage!=intent.Target.damage)return false;
        if(!MatchesChange(intent,change))return false;
        BlockValue observed=world.GetBlock(position);
        if(observed.rawData!=intent.ExpectedOld.rawData||observed.damage!=intent.ExpectedOld.damage)return false;
        var held=player.inventory.holdingItemItemValue;
        if(held==null||held.ToBlockValue().type!=intent.Target.type)return false;
        byte flags;sbyte density;long texture;
        if(!TryExpectedChange(held,intent.Target,out flags,out density,out texture)||
            flags!=intent.Flags||density!=intent.Density||texture!=intent.Texture)return false;
        byte[] actual;if(!TrySeedIdentity(held,out actual)||intent.Seed==null||actual.Length!=intent.Seed.Length)return false;
        for(int i=0;i<actual.Length;i++)if(actual[i]!=intent.Seed[i])return false;
        return true; // Snapshot consistency ONLY; delayed-debit ordering and authored seed policy remain owner admission.
    }
}





