using System;
using System.IO;
using System.Text;

internal sealed class RebirthCamperEnergyTransferFrame
{
    private const uint Magic=0x31454E52;
    internal Guid Operation,Vehicle;
    internal int Actor;
    internal string Owner,OwnerKey,Creation;
    internal bool Survivor;
    internal RebirthCamperEnergyActorImage Before,After;
    internal RebirthCamperEnergyRuntime.Change Change;
    internal byte[] Write(bool after)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(Magic);writer.Write(Operation.ToByteArray());writer.Write(Vehicle.ToByteArray());writer.Write(Actor);
            writer.Write(Owner);writer.Write(OwnerKey);writer.Write(Creation);writer.Write(Survivor);writer.Write(Change.ExpectedRevision);
            writer.Write(after);(after?After:Before).Write(writer);
            CamperFrame.Write(writer,after?Change.After:Change.Before);
            CamperFrame.Write(writer,RebirthCamperEnergyItems.Write(new[]{Change.Removed??ItemStack.Empty}));
            writer.Flush();if(stream.Length>16*1024*1024)throw new InvalidDataException("Energy transfer frame too large.");return stream.ToArray();
        }
    }
    internal static RebirthCamperEnergyTransferFrame Read(RebirthCamperTransferJournal.Record record)
    {
        if(record==null)throw new InvalidDataException("Energy receipt absent.");
        var before=ReadSide(record.Before,false,out var beforeState,out var beforeRemoved,out long revision);
        var after=ReadSide(record.After,true,out var afterState,out var afterRemoved,out long afterRevision);
        if(before.Operation!=record.Operation||before.Vehicle!=record.Vehicle||revision!=record.Revision||revision!=afterRevision||
            before.Creation!=record.OwnerCreation||before.Operation!=after.Operation||before.Vehicle!=after.Vehicle||before.Actor!=after.Actor||
            before.Owner!=after.Owner||before.OwnerKey!=after.OwnerKey||before.Creation!=after.Creation||before.Survivor!=after.Survivor||
            !RebirthCamperEnergyItems.Same(beforeRemoved,afterRemoved))throw new InvalidDataException("Energy receipt scope changed.");
        before.After=after.After;before.Change=RebirthCamperEnergyRuntime.RestoreChange(before.Vehicle,beforeState,afterState,beforeRemoved);
        if(before.Change.ExpectedRevision!=revision)throw new InvalidDataException("Energy revision changed.");return before;
    }
    private static RebirthCamperEnergyTransferFrame ReadSide(byte[] bytes,bool after,out byte[] state,out ItemStack removed,out long revision)
    {
        if(bytes==null||bytes.Length>16*1024*1024)throw new InvalidDataException("Bounded energy transfer required.");
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {
            if(reader.ReadUInt32()!=Magic)throw new InvalidDataException("Energy frame version changed.");
            var result=new RebirthCamperEnergyTransferFrame {Operation=new Guid(reader.ReadBytes(16)),Vehicle=new Guid(reader.ReadBytes(16)),Actor=reader.ReadInt32(),
                Owner=reader.ReadString(),OwnerKey=reader.ReadString(),Creation=reader.ReadString(),Survivor=reader.ReadBoolean()};
            revision=reader.ReadInt64();
            if(result.Operation==Guid.Empty||result.Vehicle==Guid.Empty||result.Actor<=0||revision<0||result.Owner.Length<1||result.Owner.Length>4096||
                result.OwnerKey.Length!=64||result.Creation.Length<1||result.Creation.Length>4096||reader.ReadBoolean()!=after)
                throw new InvalidDataException("Energy owner scope invalid.");
            var actor=RebirthCamperEnergyActorImage.Read(reader);if(after)result.After=actor;else result.Before=actor;
            state=CamperFrame.Read(reader);var items=RebirthCamperEnergyItems.Read(CamperFrame.Read(reader),1);
            if(items.Length!=1||stream.Position!=stream.Length)throw new InvalidDataException("Energy frame tail invalid.");removed=items[0];return result;
        }
    }
}
