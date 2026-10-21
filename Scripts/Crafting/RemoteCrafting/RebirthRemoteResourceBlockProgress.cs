using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Detached native progress, including the actual list-reference decision that
// determines fractional repair costs. Restoration is only a detached state
// operation; the Prepared-original executor must independently authorize it.
internal sealed class RebirthRemoteResourceBlockProgress
{
    internal Vector3i Target,LastHit;
    internal int UpgradeHits,RepairType,LastDamage;
    internal bool UpgradeChanged,UseStarted,SameRepairList;
    internal float RepairAmount,HitOffset;
    internal ulong LastRaw;
    internal float[] Fractions;
    internal string[] Names;
    internal int[] Counts;
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    internal bool RestoreNative(ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,Vector3i target)
    {
        if(action==null||data?.invData?.world==null||action.repairAmount!=RepairAmount||action.hitCountOffset!=HitOffset)return false;
        var block=data.invData.world.GetBlock(target);var authored=block.Block.RepairItems;
        if(block.Block.RepairItemsMeshDamage!=null&&block.Block.shape.UseRepairDamageState(block))authored=block.Block.RepairItemsMeshDamage;
        // Retain the original reference-equality branch. A detached list must
        // never become the authored list simply because its values coincide.
        if(SameRepairList&&(authored==null||Names?.Length!=authored.Count||Fractions?.Length!=authored.Count||
            LastHit!=target||new BlockValue((uint)LastRaw).type!=block.type||
            authored.Where((item,index)=>item.ItemName!=Names[index]||item.Count!=Counts[index]).Any()))return false;
        List<Block.SItemNameCount> previous=null;
        if(SameRepairList)previous=authored;
        else if(Names!=null)
        {
            previous=new List<Block.SItemNameCount>(Names.Length);
            for(int i=0;i<Names.Length;i++)previous.Add(new Block.SItemNameCount{ItemName=Names[i],Count=Counts[i]});
        }
        var last=new BlockValue((uint)LastRaw);last.damage=(ushort)LastDamage;
        action.blockTargetPos=Target;action.blockUpgradeCount=UpgradeHits;action.bUpgradeCountChanged=UpgradeChanged;
        data.lastHitPosition=LastHit;data.lastHitBlockValue=last;data.lastRepairItems=previous;
        data.lastRepairItemsPercents=Fractions==null?null:(float[])Fractions.Clone();
        data.bUseStarted=UseStarted;data.repairType=(ItemActionRepair.EnumRepairType)RepairType;
        return Capture(action,data,target)==Encode();
    }
    internal static string Capture(ItemActionRepair action,ItemActionRepair.InventoryDataRepair data,Vector3i target)
    {
        var block=data.invData.world.GetBlock(target);var list=block.Block.RepairItems;
        if(block.Block.RepairItemsMeshDamage!=null&&block.Block.shape.UseRepairDamageState(block))list=block.Block.RepairItemsMeshDamage;
        var value=new RebirthRemoteResourceBlockProgress{Target=action.blockTargetPos,LastHit=data.lastHitPosition,LastRaw=Convert.ToUInt64(data.lastHitBlockValue.rawData),LastDamage=data.lastHitBlockValue.damage,
            UpgradeHits=action.blockUpgradeCount,UpgradeChanged=action.bUpgradeCountChanged,UseStarted=data.bUseStarted,RepairType=(int)data.repairType,RepairAmount=action.repairAmount,HitOffset=action.hitCountOffset,
            Fractions=data.lastRepairItemsPercents==null?null:(float[])data.lastRepairItemsPercents.Clone(),Names=data.lastRepairItems?.Select(item=>item.ItemName).ToArray(),Counts=data.lastRepairItems?.Select(item=>item.Count).ToArray(),
            SameRepairList=list!=null&&data.lastHitPosition==target&&data.lastHitBlockValue.type==block.type&&ReferenceEquals(data.lastRepairItems,list)&&data.lastRepairItemsPercents!=null&&data.lastRepairItemsPercents.Length==list.Count};
        return value.Encode();
    }
    internal string Encode()
    {
        if(!Finite(RepairAmount)||!Finite(HitOffset)||RepairType<0||RepairType>2||LastRaw>uint.MaxValue||LastDamage<0||LastDamage>ushort.MaxValue||
            Fractions?.Length>4096||Fractions?.Any(value=>!Finite(value))==true||Names?.Length>4096||(Names==null)!=(Counts==null)||Names!=null&&Names.Length!=Counts.Length)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write((byte)1);writer.Write(SameRepairList);writer.Write(Target.x);writer.Write(Target.y);writer.Write(Target.z);writer.Write(UpgradeHits);writer.Write(UpgradeChanged);
            writer.Write(RepairAmount);writer.Write(HitOffset);writer.Write(UseStarted);writer.Write(RepairType);writer.Write(LastHit.x);writer.Write(LastHit.y);writer.Write(LastHit.z);writer.Write(LastRaw);writer.Write(LastDamage);
            writer.Write(Fractions?.Length??-1);if(Fractions!=null)foreach(float value in Fractions)writer.Write(value);
            writer.Write(Names?.Length??-1);if(Names!=null)for(int i=0;i<Names.Length;i++){if(Names[i]==null||Encoding.UTF8.GetByteCount(Names[i])>512)throw new InvalidDataException();writer.Write(Names[i]);writer.Write(Counts[i]);}
            writer.Flush();if(stream.Length>65536)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());
        }
    }
    internal static bool TryRead(string text,out RebirthRemoteResourceBlockProgress value)
    {
        value=null;
        try
        {
            if(text==null||text.Length>87384)return false;byte[] bytes=Convert.FromBase64String(text);if(bytes.Length>65536||Convert.ToBase64String(bytes)!=text)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                if(reader.ReadByte()!=1)return false;
                bool Flag(){byte flag=reader.ReadByte();if(flag>1)throw new InvalidDataException();return flag==1;}
                var item=new RebirthRemoteResourceBlockProgress{SameRepairList=Flag(),Target=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32()),UpgradeHits=reader.ReadInt32(),UpgradeChanged=Flag(),
                    RepairAmount=reader.ReadSingle(),HitOffset=reader.ReadSingle(),UseStarted=Flag(),RepairType=reader.ReadInt32(),LastHit=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32()),LastRaw=reader.ReadUInt64(),LastDamage=reader.ReadInt32()};
                int count=reader.ReadInt32();if(count< -1||count>4096)return false;if(count>=0){item.Fractions=new float[count];for(int i=0;i<count;i++)item.Fractions[i]=reader.ReadSingle();}
                count=reader.ReadInt32();if(count< -1||count>4096)return false;if(count>=0){item.Names=new string[count];item.Counts=new int[count];for(int i=0;i<count;i++){item.Names[i]=reader.ReadString();item.Counts[i]=reader.ReadInt32();}}
                if(stream.Position!=stream.Length||item.Encode()!=text)return false;value=item;return true;
            }
        }
        catch{return false;}
    }
}
