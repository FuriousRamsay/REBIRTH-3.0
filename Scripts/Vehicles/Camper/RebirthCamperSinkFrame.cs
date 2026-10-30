using System;
using System.IO;
using System.Linq;
using System.Text;

// Immutable server-authored held-vessel conversion. No bag/cursor after-image
// is ever applied; only the exact original native toolbelt slot may change.
internal sealed class RebirthCamperSinkFrame
{
    internal Guid Operation, Vehicle, World;
    internal int Version=1,Actor, Slot;internal double BeforeMl,AfterMl;
    internal long Revision;
    internal string Owner, OwnerKey, Creation;
    internal ItemStack Before, After;
    internal byte[] Write()
    {
        if(Version!=1&&Version!=2)throw new InvalidDataException("Sink frame version changed.");
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(Version==1?0x314B4E53:0x324B4E53);writer.Write(Operation.ToByteArray());writer.Write(Vehicle.ToByteArray());writer.Write(World.ToByteArray());
            writer.Write(Actor);writer.Write(Slot);
            if(Version==1){if(BeforeMl!=Math.Truncate(BeforeMl)||AfterMl!=Math.Truncate(AfterMl))throw new InvalidDataException("Whole-mL legacy frame required.");writer.Write(checked((int)BeforeMl));writer.Write(checked((int)AfterMl));}else{writer.Write(BeforeMl);writer.Write(AfterMl);}writer.Write(Revision);
            writer.Write(Owner);writer.Write(OwnerKey);writer.Write(Creation);
            CamperFrame.Write(writer,RebirthCamperEnergyItems.Write(new[]{Before,After}));writer.Flush();return stream.ToArray();
        }
    }
    internal static RebirthCamperSinkFrame Read(byte[] bytes)
    {
        if(bytes==null||bytes.Length>1024*1024)throw new InvalidDataException("Bounded sink frame required.");
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {
            int magic=reader.ReadInt32();int version=magic==0x314B4E53?1:magic==0x324B4E53?2:0;if(version==0)throw new InvalidDataException("Sink frame version changed.");
            var frame=new RebirthCamperSinkFrame {Version=version,Operation=new Guid(reader.ReadBytes(16)),Vehicle=new Guid(reader.ReadBytes(16)),World=new Guid(reader.ReadBytes(16)),
                Actor=reader.ReadInt32(),Slot=reader.ReadInt32(),BeforeMl=version==1?reader.ReadInt32():reader.ReadDouble(),AfterMl=version==1?reader.ReadInt32():reader.ReadDouble(),Revision=reader.ReadInt64(),
                Owner=reader.ReadString(),OwnerKey=reader.ReadString(),Creation=reader.ReadString()};
            var cells=RebirthCamperEnergyItems.Read(CamperFrame.Read(reader),2);
            if(cells.Length!=2||stream.Position!=stream.Length)throw new InvalidDataException("Sink frame tail invalid.");
            frame.Before=cells[0];frame.After=cells[1];
            bool convertedOk,refill=false;ItemStack converted;double delta;
            if(version==1){convertedOk=RebirthCamperSink.TryConversion(frame.Before,out converted,out int whole);delta=whole;}
            else convertedOk=RebirthCamperSink.TryCurrentConversion(frame.Before,out converted,out delta,out refill);
            if(frame.Operation==Guid.Empty||frame.Vehicle==Guid.Empty||frame.World==Guid.Empty||frame.Actor<=0||frame.Slot<0||frame.Slot>=20
                ||double.IsNaN(frame.BeforeMl)||double.IsInfinity(frame.BeforeMl)||double.IsNaN(frame.AfterMl)||double.IsInfinity(frame.AfterMl)
                ||frame.BeforeMl<0||frame.BeforeMl>RebirthCamperSink.CapacityMl||frame.AfterMl<0||frame.AfterMl>RebirthCamperSink.CapacityMl
                ||frame.Revision<0||frame.Revision==long.MaxValue||string.IsNullOrEmpty(frame.Owner)||frame.Owner.Length>4096
                ||frame.OwnerKey?.Length!=64||string.IsNullOrEmpty(frame.Creation)||frame.Creation.Length>4096
                ||!convertedOk
                ||frame.AfterMl!=frame.BeforeMl+delta||!MatchesOutput(frame.After,converted,!refill)
                ||!frame.Write().SequenceEqual(bytes))throw new InvalidDataException("Sink frame scope/conversion invalid.");
            return frame;
        }
    }
    private static bool MatchesOutput(ItemStack saved,ItemStack expected,bool legacySeed)
    {
        // Old receipts contain the native constructor's time-derived seed.
        // Preserve that exact output; validate every other native field.
        if(saved?.itemValue==null||expected?.itemValue==null)return false;
        if(legacySeed)expected.itemValue.Seed=saved.itemValue.Seed;
        return RebirthCamperEnergyItems.Same(saved,expected);
    }
}
