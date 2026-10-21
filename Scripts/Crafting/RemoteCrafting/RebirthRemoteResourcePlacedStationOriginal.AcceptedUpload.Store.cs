using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private const uint AcceptedUploadMagic=0x31555052;
    private const long MaximumAcceptedUploadBytes=RebirthRemoteResourceClientOriginal.MaximumOriginalBytes+3L*RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes+131072;
    private static string AcceptedUploadPath(Guid source,string temporary=null)
    {
        if(source==Guid.Empty)throw new InvalidDataException();string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedAcceptedUploads");CompletionAncestors(directory);
        string name=source.ToString("N")+".rpu";int count=0;
        if(Directory.Exists(directory))foreach(string path in Directory.EnumerateFileSystemEntries(directory,source.ToString("N")+"*"))
            if(++count>32||Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||Path.GetFileName(path)!=name&&!string.Equals(path,temporary,StringComparison.Ordinal))throw new InvalidDataException();
        return Path.Combine(directory,name);
    }
    private static void WriteAcceptedUpload(BinaryWriter writer,UploadOriginal original,AcceptedUpload accepted)
    {
        writer.Write(AcceptedUploadMagic);writer.Write(original.Source.ToString("N"));writer.Write(original.Birth);writer.Write(original.Actor.entityId);writer.Write(accepted.Tick);
        CompletionBytes(writer,Encoding.UTF8.GetBytes(original.Original.Copy().ToString(SaveOptions.DisableFormatting)));
        CompletionBytes(writer,accepted.Before);CompletionBytes(writer,accepted.Raw);CompletionBytes(writer,accepted.After);
    }
    private static bool PublishAcceptedUpload(UploadOriginal original,AcceptedUpload accepted)
    {
        if(!original.Current())return false;
        string path=AcceptedUploadPath(original.Source);Directory.CreateDirectory(Path.GetDirectoryName(path));CompletionAncestors(Path.GetDirectoryName(path));
        if(!File.Exists(path))
        {
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new BinaryWriter(output,new UTF8Encoding(false,true),true))
            {WriteAcceptedUpload(writer,original,accepted);writer.Flush();if(output.Length>MaximumAcceptedUploadBytes)return false;output.Flush(true);}
            if(!original.Current()||AcceptedUploadPath(original.Source,temporary)!=path)return false;File.Move(temporary,path);
        }
        // Independent full readback, including all original/pre/raw/post bytes.
        // An interrupted candidate or different event cannot be overwritten.
        return ReadAcceptedUpload(original.Source,original.Original,original.Birth,original.Actor.entityId,out var actual)&&
            actual.Tick==accepted.Tick&&actual.Before.SequenceEqual(accepted.Before)&&actual.Raw.SequenceEqual(accepted.Raw)&&actual.After.SequenceEqual(accepted.After)&&original.Current();
    }
    private static bool ReadAcceptedUpload(Guid source,RebirthRemoteResourceClientOriginal.Intent original,string birth,int entity,out AcceptedUpload accepted,
        RebirthRemoteResourcePlacedStationAllocation.Admission allocation=null,RebirthRemoteResourcePlacedStationPreparation.ColdRead cold=null,long retainedWorkspace=0)
    {
        accepted=null;
        try
        {
            long buffers=6L*RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes;
            if(allocation!=null&&!allocation.ReserveReadWorkspace(buffers)||cold!=null&&!cold.ReserveReadWorkspace(checked(retainedWorkspace+buffers)))return false;
            string path=AcceptedUploadPath(source);
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var reader=new BinaryReader(input,new UTF8Encoding(false,true),true))
            {
                if(input.Length<1||input.Length>MaximumAcceptedUploadBytes||reader.ReadUInt32()!=AcceptedUploadMagic||CompletionText(reader,32)!=source.ToString("N")||
                    CompletionText(reader,32)!=birth||reader.ReadInt32()!=entity)return false;
                ulong tick=reader.ReadUInt64();
                byte[] fullOriginal=CompletionBytes(reader,RebirthRemoteResourceClientOriginal.MaximumOriginalBytes);
                if(!fullOriginal.SequenceEqual(Encoding.UTF8.GetBytes(original.Copy().ToString(SaveOptions.DisableFormatting))))return false;
                var before=CompletionBytes(reader,RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes);var raw=CompletionBytes(reader,RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes);
                var after=CompletionBytes(reader,RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes);
                if(input.Position!=input.Length||!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(before,out long beforeGraph,out long beforeScratch)||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(after,out long afterGraph,out long afterScratch)||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryMeasureToServer(raw,out _,out long rawGraph,out long rawScratch))return false;
                long workspace=checked(buffers+12*(beforeGraph+afterGraph+rawGraph)+Math.Max(beforeScratch,Math.Max(afterScratch,rawScratch)));
                if(allocation!=null&&!allocation.ReserveReadWorkspace(workspace)||cold!=null&&!cold.ReserveReadWorkspace(checked(retainedWorkspace+workspace)))return false;
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(before,out var previous)||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryRead(after,out var current)||!RebirthRemoteResourcePlacedStationNativeReader.TryMeasureToServer(raw,out _,out _,out _)||
                    !MatchesAcceptedNativeBody(raw,current,tick)||previous.HeatClock!=current.HeatClock||previous.LocalPosition!=current.LocalPosition||
                    !ValidateInput(original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out var inputImage)||
                    current.LocalPosition!=new Vector3i(position.x&15,position.y,position.z&15)||!SameGrid(current.Input,inputImage)||
                    RebirthCookingRemoteAdmission.ReadProtocolXml(original.Copy().Element("action").Element("queue").Value,RebirthRemoteResourcePlacedStationQueue.MaximumBytes).Element("native")?.Value!=RebirthCookingRemoteAdmissionBackend.Queue(current.Queue))return false;
                accepted=new AcceptedUpload(before,raw,after,tick);return true;
            }
        }
        catch{accepted=null;return false;}
    }
    private static bool ObserveColdAccepted(RebirthRemoteResourcePlacedStationColdRead.Original cold,RebirthRemoteResourcePlacedStationReference.Image reference)
    {
        var artifact=cold.Artifact;var root=cold.Source;var allocation=cold.Allocation;
        if(artifact==null||root==null||allocation==null||!allocation.ReserveReadWorkspace(checked(artifact.ReadWorkspace+6L*RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes))||
            !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.NativeBefore,out var scratch))return false;
        using(scratch)
        {
            if(!ReadAcceptedUpload(root.Operation,artifact.Original,reference.Birth,(int)artifact.Copy().Element("plan").Attribute("entity"),out var accepted,null,allocation,artifact.ReadWorkspace)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(accepted.After,out var native)||
                !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(accepted.After,native.ClockOffset,out var after,out _))return false;
            var saved=artifact.Copy().Element("savedBefore");var snapshot=Convert.FromBase64String(saved.Element("snapshot").Value);
            long offset=(long)saved.Attribute("tileOffset");int length=(int)saved.Attribute("tileLength");
            if(offset<8||length!=after.Length||offset>snapshot.LongLength-length)return false;
            var actual=new byte[length];Buffer.BlockCopy(snapshot,(int)offset,actual,0,length);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(actual,out var before)||
                !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(actual,before.ClockOffset,out var canonical,out _)||!canonical.SequenceEqual(after))return false;
            return cold.Current&&scratch.Current;
        }
    }
    private static bool HasActualAcceptedUpload(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,Guid source,TileEntityWorkstation station)
    {
        if(!uploadOriginals.TryGetValue(source,out var pending)||pending.Accepted==null||!ReferenceEquals(pending.Actor,actor)||!ReferenceEquals(pending.Station,station)||
            pending.Original.Digest!=original.Digest||!pending.Current()||
            !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(pending.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.NativeBefore,out var scratch))return false;
        using(scratch)
        {
            if(!ReadAcceptedUpload(source,original,pending.Birth,actor.entityId,out var full)||full.Tick!=pending.Accepted.Tick||!full.Before.SequenceEqual(pending.Accepted.Before)||
                !full.Raw.SequenceEqual(pending.Accepted.Raw)||!full.After.SequenceEqual(pending.Accepted.After)||!CaptureUploadNative(station,out var current)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(full.After,out var after)||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(current,out var actual)||
                !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(full.After,after.ClockOffset,out var saved,out _)||
                !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(current,actual.ClockOffset,out var canonical,out _)||!saved.SequenceEqual(canonical))return false;
            return pending.Current()&&scratch.Current;
        }
    }
    private static void RetireAdmittedUpload(Guid source)
    {
        if(!uploadOriginals.TryGetValue(source,out var original))return;
        original.Dispose();uploadOriginals.Remove(source); // Durable event remains immutable.
    }
    internal static bool TryObserveAcceptedAdmission(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,
        RebirthRemoteResourcePlacedStationCustody.Claim claim,RebirthRemoteResourcePlacedStationAllocation.Admission allocation)
    {
        if(!ServerScope(actor,original)||!claim.Matches(root,original)||!claim.ExactBefore()||
            !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.NativeBefore,out var scratch))return false;
        using(scratch)
        {
            if(!ReadAcceptedUpload(root.Operation,original,claim.Birth,actor.entityId,out var accepted,allocation)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(accepted.After,out var native)||
                !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(accepted.After,native.ClockOffset,out var post,out _)||!post.SequenceEqual(claim.CopyBefore()))return false;
            return claim.ExactBefore()&&scratch.Current;
        }
    }
}
