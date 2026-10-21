using System;using System.IO;using System.Linq;using System.Text;
internal sealed class RebirthCamperEnergyMaintenanceReceipt
{
    internal Guid Target,Operation,Vehicle;
    internal string World,Owner,Creation;
    internal Vector3i Position;
    internal uint BlockRaw;
    internal int BeforeDamage,AfterDamage;
    internal bool Applied;
    internal byte[] Encode()
    {
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Encoding.UTF8,true))
        {
            w.Write(0x314D4352);w.Write(Target.ToByteArray());w.Write(Operation.ToByteArray());w.Write(Vehicle.ToByteArray());
            Text(w,World);Text(w,Owner);Text(w,Creation);w.Write(Position.x);w.Write(Position.y);w.Write(Position.z);
            w.Write(BlockRaw);w.Write(BeforeDamage);w.Write(AfterDamage);w.Write(Applied);w.Flush();return s.ToArray();
        }
    }
    private static void Text(BinaryWriter w,string value){if(string.IsNullOrEmpty(value)||value.Length>512)throw new InvalidDataException("Maintenance binding invalid.");w.Write(value);}
    internal static RebirthCamperEnergyMaintenanceReceipt Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length>4096)throw new InvalidDataException("Maintenance receipt bound exceeded.");
        using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s,Encoding.UTF8,true))
        {
            if(r.ReadInt32()!=0x314D4352)throw new InvalidDataException("Maintenance receipt format invalid.");
            var v=new RebirthCamperEnergyMaintenanceReceipt {Target=new Guid(r.ReadBytes(16)),Operation=new Guid(r.ReadBytes(16)),Vehicle=new Guid(r.ReadBytes(16)),
                World=r.ReadString(),Owner=r.ReadString(),Creation=r.ReadString(),Position=new Vector3i(r.ReadInt32(),r.ReadInt32(),r.ReadInt32()),
                BlockRaw=r.ReadUInt32(),BeforeDamage=r.ReadInt32(),AfterDamage=r.ReadInt32(),Applied=r.ReadBoolean()};
            if(s.Position!=s.Length||v.Target==Guid.Empty||v.Operation==Guid.Empty||v.Vehicle==Guid.Empty||v.BeforeDamage<=0||v.AfterDamage<0||v.AfterDamage>=v.BeforeDamage||
                string.IsNullOrEmpty(v.World)||v.World.Length>512||string.IsNullOrEmpty(v.Owner)||v.Owner.Length>512||string.IsNullOrEmpty(v.Creation)||v.Creation.Length>512)
                throw new InvalidDataException("Maintenance receipt fields invalid.");return v;
        }
    }
}

