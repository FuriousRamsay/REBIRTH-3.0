using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// RMO1 is an immutable full-count mobile source description. Parsing it never
// installs a station, issues an owner, or grants a native write.
internal static class RebirthProductiveEarnedMobileOrigin
{
    internal const int MaximumBytes=16*1024*1024;
    internal sealed class Description
    {
        internal Guid Installation,Vehicle,Operation;
        internal string World,InstalledItem;
        internal byte[] Before,After;
    }
    internal static bool TryCapture(RebirthCamperStationState station,RebirthProductiveEarnedRowProof.Frame frame,
        ItemStack[] afterTools,float target,out byte[] record)
    {
        record=null;
        try
        {
            var vehicle=station?.BoundVehicle;
            if(vehicle==null||!ReferenceEquals(vehicle.world,GameManager.Instance?.World)||vehicle.world.IsRemote()||
                !(RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||RebirthProductiveEarnedCoordinator.IsMobileSettlementSource(station)))return false;
            var runtime=RebirthCamperModuleRuntime.For(vehicle);
            var installation=runtime.Stations.Find(station.InstallationId);
            if(!ReferenceEquals(installation?.State,station)||frame.Birth!=station.InstallationId||
                !RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{installation.InstalledItem},196608,262144,out var item))return false;
            int measured=RebirthCamperStationSnapshot.MeasureLength(station.InstallationId,station,MaximumBytes/3);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*measured+1048576),out var workspace))return false;
            using(workspace)
            {
                byte[] before=Canonical(station,installation.InstalledItem);
                var detached=RebirthCamperStationSnapshot.Restore(station.InstallationId,installation.InstalledItem,before);
                if(detached.Tools.Length!=afterTools.Length||frame.RowIndex!=detached.Queue.Length-1)return false;
                for(int i=0;i<afterTools.Length;i++)detached.Tools[i]=afterTools[i]?.Clone();
                detached.Queue[frame.RowIndex].CraftingTimeLeft=target;
                var value=new Description{Installation=station.InstallationId,Vehicle=runtime.Identity,Operation=frame.Operation,
                    World=frame.World,InstalledItem=item,Before=before,After=Canonical(detached,installation.InstalledItem)};
                record=Encode(value);return record.Length<=MaximumBytes;
            }
        }
        catch{record=null;return false;}
    }
    internal static byte[] Canonical(RebirthCamperStationState state,ItemStack item)
    {
        var body=RebirthCamperStationSnapshot.Capture(state.InstallationId,state);
        var detached=RebirthCamperStationSnapshot.Restore(state.InstallationId,item,body);
        detached.lastTickTime=0;detached.RestoreRevision(0);
        // Marker is compared independently against the original SHA capability.
        detached.RestoreProductiveEarnedMarker(true,Array.Empty<byte>());
        return RebirthCamperStationSnapshot.Capture(state.InstallationId,detached);
    }
    internal static byte[] Digest(byte[] bytes){using(var sha=SHA256.Create())return sha.ComputeHash(bytes);}
    internal static bool TryReadRecord(string path,out byte[] bytes)
    {
        bytes=null;
        if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp")||!File.Exists(path))return false;
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            long length=input.Length;
            if(length<1||length>MaximumBytes)return false;
            var image=new byte[(int)length];int offset=0;
            while(offset<image.Length)
            {int count=input.Read(image,offset,image.Length-offset);if(count<=0)return false;offset+=count;}
            if(input.Length!=length||input.ReadByte()!=-1||File.Exists(path+".tmp"))return false;
            bytes=image;return true;
        }
    }
    internal static bool Publish(RebirthProductiveEarnedArtifact.Prepared artifact,byte[] record)
        =>artifact?.Current()==true&&record!=null&&RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(artifact.OriginalPath+".mobile-origin",record,MaximumBytes);
    internal static bool TryRead(RebirthProductiveEarnedArtifact.Prepared artifact,RebirthProductiveEarnedRowProof.Frame frame,out Description source)
    {
        source=null;
        try
        {
            string path=artifact.OriginalPath+".mobile-origin";
            if(!frame.HasMobileOrigin||!TryReadRecord(path,out var bytes))return false;
            if(!Digest(bytes).SequenceEqual(frame.MobileOriginDigest)||!artifact.BindMobileDependency(false,bytes))return false;
            var value=Decode(bytes);
            if(value.Installation!=frame.Birth||value.Operation!=frame.Operation||value.World!=frame.World)return false;
            source=value;return true;
        }
        catch{return false;}
    }
    internal static bool MatchesAfter(RebirthCamperStationState station,RebirthProductiveEarnedArtifact.Prepared artifact,
        RebirthProductiveEarnedRowProof.Frame frame)
    {
        try
        {
            if(!TryRead(artifact,frame,out var source)||station.BoundVehicle==null)return false;
            var runtime=RebirthCamperModuleRuntime.For(station.BoundVehicle);
            var installation=runtime.Stations.Find(source.Installation);
            return runtime.Identity==source.Vehicle&&ReferenceEquals(installation?.State,station)&&
                RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{installation.InstalledItem},196608,262144,out var item)&&
                item==source.InstalledItem&&TryEffectiveAfter(artifact,source,out var after)&&
                Canonical(station,installation.InstalledItem).SequenceEqual(after);
        }
        catch{return false;}
    }
    internal static bool TryEffectiveAfter(RebirthProductiveEarnedArtifact.Prepared artifact,Description source,out byte[] body)
    {
        body=null;
        try
        {
            string path=artifact.OriginalPath+".mobile-initial-suffix";
            if(File.Exists(path+".tmp"))return false;
            if(!File.Exists(path)){body=source.After;return true;}
            if(!TryReadRecord(path,out var bytes))return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=0x31494d52||!reader.ReadBytes(32).SequenceEqual(artifact.OriginalDigest()))return false;
                body=ReadBody(reader);return stream.Position==stream.Length&&artifact.BindMobileDependency(true,bytes);
            }
        }
        catch{body=null;return false;}
    }
    internal static bool PublishInitialReturned(RebirthCamperStationState station,RebirthProductiveEarnedArtifact.Prepared artifact,
        RebirthProductiveEarnedRowProof.Frame frame,byte[] frozen=null)
    {
        try
        {
            if(!TryRead(artifact,frame,out var source))return false;
            var runtime=RebirthCamperModuleRuntime.For(station.BoundVehicle);
            var installed=runtime.Stations.Find(source.Installation);
            if(runtime.Identity!=source.Vehicle||!ReferenceEquals(installed?.State,station)||
                !artifact.MatchesMarker(station.CopyProductiveEarnedMarker())||
                !RebirthProductiveEarnedRowProof.CaptureRow(station.Queue[frame.RowIndex]).SequenceEqual(frame.AfterRow)||
                !RebirthProductiveEarnedArtifact.SameTools(station.Tools,frame.AfterTools))return false;
            byte[] body=frozen==null?Canonical(station,installed.InstalledItem):
                Canonical(RebirthCamperStationSnapshot.Restore(source.Installation,installed.InstalledItem,frozen),installed.InstalledItem),record;
            if(!Canonical(station,installed.InstalledItem).SequenceEqual(body))return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(0x31494d52);writer.Write(artifact.OriginalDigest());writer.Write(body.Length);writer.Write(body);
                writer.Flush();record=stream.ToArray();
            }
            return RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(artifact.OriginalPath+".mobile-initial-suffix",record,MaximumBytes)&&
                MatchesAfter(station,artifact,frame);
        }
        catch{return false;}
    }
    internal static byte[] Encode(Description value)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x314f4d52);writer.Write(value.Installation.ToByteArray());writer.Write(value.Vehicle.ToByteArray());
            writer.Write(value.Operation.ToByteArray());writer.Write(value.World);writer.Write(value.InstalledItem);
            writer.Write(value.Before.Length);writer.Write(value.Before);writer.Write(value.After.Length);writer.Write(value.After);
            writer.Flush();if(stream.Length>MaximumBytes)throw new InvalidDataException("Mobile original too large.");return stream.ToArray();
        }
    }
    internal static Description Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length<80||bytes.Length>MaximumBytes)throw new InvalidDataException("Mobile original size.");
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadInt32()!=0x314f4d52)throw new InvalidDataException("Mobile original version.");
            var value=new Description{Installation=new Guid(reader.ReadBytes(16)),Vehicle=new Guid(reader.ReadBytes(16)),
                Operation=new Guid(reader.ReadBytes(16)),World=reader.ReadString(),InstalledItem=reader.ReadString()};
            if(value.Installation==Guid.Empty||value.Vehicle==Guid.Empty||value.Operation==Guid.Empty||value.World.Length!=44||
                value.InstalledItem.Length>262144)throw new InvalidDataException("Mobile original identity.");
            value.Before=ReadBody(reader);value.After=ReadBody(reader);
            if(stream.Position!=stream.Length)throw new InvalidDataException("Mobile original trailing bytes.");return value;
        }
    }
    private static byte[] ReadBody(BinaryReader reader)
    {
        int length=reader.ReadInt32();if(length<22||length>MaximumBytes/2||length>reader.BaseStream.Length-reader.BaseStream.Position)
            throw new InvalidDataException("Mobile original body.");return reader.ReadBytes(length);
    }
}
