using System;
using System.IO;
using System.Linq;
using System.Text;

internal sealed class RebirthCamperEnergyMaintenanceFrame
{
    internal RebirthCamperEnergyMaintenanceReceipt Receipt;
    internal ItemStack[] Cargo;
    internal bool[] Locks;
    internal byte[] Encode()
    {
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
        {w.Write(0x31464D43);CamperFrame.Write(w,Receipt.Encode());CamperFrame.Write(w,CargoBytes(Cargo));w.Write(Locks!=null);
            if(Locks!=null){w.Write(Locks.Length);foreach(bool bit in Locks)w.Write(bit);}w.Flush();return s.ToArray();}
    }
    internal static RebirthCamperEnergyMaintenanceFrame Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length>32*1024*1024)throw new InvalidDataException("Maintenance frame bound exceeded.");
        using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s,Encoding.UTF8,true))
        {
            if(r.ReadInt32()!=0x31464D43)throw new InvalidDataException("Maintenance frame format invalid.");
            var f=new RebirthCamperEnergyMaintenanceFrame{Receipt=RebirthCamperEnergyMaintenanceReceipt.Decode(CamperFrame.Read(r)),Cargo=ReadCargo(CamperFrame.Read(r))};
            if(r.ReadBoolean()){int count=r.ReadInt32();if(count!=f.Cargo.Length)throw new InvalidDataException("Maintenance lock dimensions invalid.");f.Locks=new bool[count];for(int i=0;i<count;i++)f.Locks[i]=r.ReadBoolean();}
            if(s.Position!=s.Length)throw new InvalidDataException("Trailing maintenance frame.");return f;
        }
    }
    internal static byte[] CargoBytes(ItemStack[] items)
    {
        if(items==null||items.Length>4096)throw new InvalidDataException("Maintenance cargo dimensions invalid.");
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
        {w.Write(items.Length);foreach(var item in items){if(item?.itemValue==null||item.count<0)throw new InvalidDataException("Native cargo invalid.");w.Write(item.count);w.Write(RebirthNativeItemCodec.Encode(item.itemValue));}w.Flush();if(s.Length>16*1024*1024)throw new InvalidDataException("Maintenance cargo image too large.");return s.ToArray();}
    }
    private static ItemStack[] ReadCargo(byte[] bytes)
    {
        if(bytes==null||bytes.Length>16*1024*1024)throw new InvalidDataException("Maintenance cargo bound exceeded.");
        using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s,Encoding.UTF8,true))
        {int count=r.ReadInt32();if(count<0||count>4096)throw new InvalidDataException("Maintenance cargo dimensions invalid.");var result=new ItemStack[count];
            string empty=RebirthNativeItemCodec.Encode(ItemValue.None);
            for(int i=0;i<count;i++){int amount=r.ReadInt32();string raw=r.ReadString();ItemValue value;
                if(amount<0||raw.Length>262144)throw new InvalidDataException("Maintenance native item refused.");
                if(raw==empty){if(amount!=0)throw new InvalidDataException("Maintenance empty count invalid.");value=ItemValue.None.Clone();}
                else if(!RebirthNativeItemCodec.TryDecode(raw,out value)||value==null||value.IsEmpty()||RebirthNativeItemCodec.Encode(value)!=raw)
                    throw new InvalidDataException("Maintenance native item refused.");result[i]=new ItemStack(value,amount);}
            if(s.Position!=s.Length)throw new InvalidDataException("Trailing maintenance cargo.");return result;}
    }
    internal static bool Same(ItemStack[] a,ItemStack[] b)=>CargoBytes(a).SequenceEqual(CargoBytes(b));
    internal bool Matches(Bag bag)=>Same(Cargo,bag.ItemGrid.items)&&SameLocks(Locks,RebirthCamperEnergyItems.Locks(bag.LockedSlots));
    internal static bool SameLocks(bool[] a,bool[] b)=>a==null||b==null?a==b:a.SequenceEqual(b);
    internal static void ValidatePair(RebirthCamperTransferJournal.Record record,out RebirthCamperEnergyMaintenanceFrame before,out RebirthCamperEnergyMaintenanceFrame after)
    {
        before=Decode(record.Before);after=Decode(record.After);var a=before.Receipt;var b=after.Receipt;
        if(a.Applied||!b.Applied||record.Revision!=0||record.OwnerCreation!=a.Owner+":"+a.Creation||a.Operation!=record.Operation||a.Vehicle!=record.Vehicle||b.Operation!=a.Operation||
            b.Vehicle!=a.Vehicle||a.Target!=b.Target||a.World!=b.World||a.Owner!=b.Owner||a.Creation!=b.Creation||
            a.Position!=b.Position||a.BlockRaw!=b.BlockRaw||a.BeforeDamage!=b.BeforeDamage||a.AfterDamage!=b.AfterDamage||
            before.Cargo.Length!=after.Cargo.Length||!SameLocks(before.Locks,after.Locks))throw new InvalidDataException("Maintenance frame identity changed.");
        bool paid=false;
        for(int i=0;i<before.Cargo.Length;i++)
        {
            var old=before.Cargo[i];var next=after.Cargo[i];if(RebirthCamperEnergyItems.Same(old,next))continue;
            if(before.Locks!=null&&i<before.Locks.Length&&before.Locks[i]||old.IsEmpty()||next.count>=old.count||
                !next.IsEmpty()&&RebirthNativeItemCodec.Encode(old.itemValue)!=RebirthNativeItemCodec.Encode(next.itemValue))throw new InvalidDataException("Maintenance cargo delta refused.");paid=true;
        }
        if(!paid)throw new InvalidDataException("Maintenance requires paid resources.");
    }
}
