using System;
using System.IO;
using System.Linq;
using System.Text;

// Immutable server-authored held-vessel conversion. No bag/cursor after-image
// is ever applied; only the exact original native toolbelt slot may change.
internal sealed class RebirthCamperSinkFrame
{
    internal Guid Operation, Vehicle, World;
    internal int Actor, Slot, BeforeMl, AfterMl;
    internal long Revision;
    internal string Owner, OwnerKey, Creation;
    internal ItemStack Before, After;
    internal byte[] Write()
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(0x314B4E53);writer.Write(Operation.ToByteArray());writer.Write(Vehicle.ToByteArray());writer.Write(World.ToByteArray());
            writer.Write(Actor);writer.Write(Slot);writer.Write(BeforeMl);writer.Write(AfterMl);writer.Write(Revision);
            writer.Write(Owner);writer.Write(OwnerKey);writer.Write(Creation);
            CamperFrame.Write(writer,RebirthCamperEnergyItems.Write(new[]{Before,After}));writer.Flush();return stream.ToArray();
        }
    }
    internal static RebirthCamperSinkFrame Read(byte[] bytes)
    {
        if(bytes==null||bytes.Length>1024*1024)throw new InvalidDataException("Bounded sink frame required.");
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {
            if(reader.ReadInt32()!=0x314B4E53)throw new InvalidDataException("Sink frame version changed.");
            var frame=new RebirthCamperSinkFrame {Operation=new Guid(reader.ReadBytes(16)),Vehicle=new Guid(reader.ReadBytes(16)),World=new Guid(reader.ReadBytes(16)),
                Actor=reader.ReadInt32(),Slot=reader.ReadInt32(),BeforeMl=reader.ReadInt32(),AfterMl=reader.ReadInt32(),Revision=reader.ReadInt64(),
                Owner=reader.ReadString(),OwnerKey=reader.ReadString(),Creation=reader.ReadString()};
            var cells=RebirthCamperEnergyItems.Read(CamperFrame.Read(reader),2);
            if(cells.Length!=2||stream.Position!=stream.Length)throw new InvalidDataException("Sink frame tail invalid.");
            frame.Before=cells[0];frame.After=cells[1];
            if(frame.Operation==Guid.Empty||frame.Vehicle==Guid.Empty||frame.World==Guid.Empty||frame.Actor<=0||frame.Slot<0||frame.Slot>=20
                ||frame.BeforeMl<0||frame.BeforeMl>RebirthCamperSink.CapacityMl||frame.AfterMl<0||frame.AfterMl>RebirthCamperSink.CapacityMl
                ||frame.Revision<0||frame.Revision==long.MaxValue||string.IsNullOrEmpty(frame.Owner)||frame.Owner.Length>4096
                ||frame.OwnerKey?.Length!=64||string.IsNullOrEmpty(frame.Creation)||frame.Creation.Length>4096
                ||!RebirthCamperSink.TryConversion(frame.Before,out var converted,out int delta)
                ||frame.AfterMl-frame.BeforeMl!=delta||!RebirthCamperEnergyItems.Same(frame.After,converted)
                ||!frame.Write().SequenceEqual(bytes))throw new InvalidDataException("Sink frame scope/conversion invalid.");
            return frame;
        }
    }
}
