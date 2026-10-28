// PREPARED, NOT RUN. Actual new wire body, no game/session effects.
using System;
using System.IO;
internal static class RebirthCamperCommandReplyFixture
{
    internal static void Verify()
    {
        Guid token=Guid.NewGuid(),first=Guid.NewGuid(),second=Guid.NewGuid();
        byte[] a=Encode(new NetPackageRebirthCamperStationCommandResultV2().Setup(42,token,Guid.Empty,first,false,Array.Empty<byte>()));
        byte[] b=Encode(new NetPackageRebirthCamperStationCommandResultV2().Setup(42,token,Guid.Empty,second,false,Array.Empty<byte>()));
        var one=Decode(a);var two=Decode(b);
        Need(one.SessionToken==token&&two.SessionToken==token&&one.OriginalOperation==first&&two.OriginalOperation==second&&one.OriginalOperation!=two.OriginalOperation);
        var fuel=Decode(Encode(new NetPackageRebirthCamperStationCommandResultV2().Setup(42,token,Guid.Empty,second,false,Array.Empty<byte>(),RebirthCamperRemoteStationTransport.FuelSwitch)));
        Need(fuel.ReplyPurpose==RebirthCamperRemoteStationTransport.FuelSwitch&&one.ReplyPurpose==RebirthCamperRemoteStationTransport.StationCommand);
        var version=(byte[])a.Clone();version[0]=1;Reject(version);
        var empty=(byte[])a.Clone();Array.Clear(empty,37,16);Reject(empty);
        var purpose=(byte[])a.Clone();purpose[53]=255;Reject(purpose);
        var truncated=new byte[a.Length-1];Array.Copy(a,truncated,truncated.Length);Reject(truncated);
    }
    private static byte[] Encode(NetPackageRebirthCamperStationCommandResultV2 packet)
    {
        using(var stream=new MemoryStream())
        {
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(_bReset:false)){writer.SetBaseStream(stream);packet.WriteBody(writer);writer.Flush();}
            return stream.ToArray();
        }
    }
    private static NetPackageRebirthCamperStationCommandResultV2 Decode(byte[] bytes)
    {
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(_bReset:false))
        {reader.SetBaseStream(stream);var packet=new NetPackageRebirthCamperStationCommandResultV2();packet.ReadBody(reader);Need(stream.Position==stream.Length);return packet;}
    }
    private static void Reject(byte[] bytes)
    {bool failed=false;try{Decode(bytes);}catch(InvalidDataException){failed=true;}catch(EndOfStreamException){failed=true;}Need(failed);}
    private static void Need(bool value){if(!value)throw new InvalidOperationException("Station command result wire invariant failed.");}
}
