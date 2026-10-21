using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static bool PublishRemoteNativeTools(RebirthProductiveEarnedArtifact.Prepared artifact,RebirthProductiveEarnedNativeOutputPlan.Description paid,
        RebirthProductiveEarnedRemoteToolPlan.Description tools,byte[] plan,string header,RebirthProductiveEarnedObservationBudget memory,
        RebirthRemoteResourceNativePublication.Watch watch,Func<bool> current)
    {
        if(current?.Invoke()!=true||artifact?.Current()!=true||paid.PendingIndex<0||watch==null)return false;
        byte[] record=RebirthProductiveEarnedRemoteToolRebaseRecord.Encode(artifact.CopyImage(),plan,header);
        if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(artifact.OriginalPath+".remote-tools-rebased",record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||!current())return false;
        return RebirthProductiveEarnedSavedNativeSource.Publish(artifact,memory,watch,tools.After,tools.AfterOffset,true,current,
            true,paid.PendingIndex,paid.PendingBefore,paid.PendingTarget,false,false,true);
    }
}

// Historical native owner-save provenance, distinct from the local RTG journal.
// Only the actual host owner/native-station observers publish this description.
internal static class RebirthProductiveEarnedRemoteToolRebaseRecord
{
    internal sealed class Description:IDisposable
    {internal RebirthProductiveEarnedRemoteToolPlan.Description Plan;internal RebirthProductiveEarnedNativeOutputPlan.Description Paid;internal byte[] PlanImage,BeforeOwner,AfterOwner,ViewerSource,ViewerTerminal,HeaderImage;internal string Header;internal IDisposable Memory;public void Dispose(){Memory?.Dispose();Memory=null;BeforeOwner=AfterOwner=ViewerSource=ViewerTerminal=HeaderImage=null;}}
    internal static byte[] Encode(byte[] original,byte[] plan,string header)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write(0x31544252u);writer.Write(Hash(original));writer.Write(header);writer.Write(Hash(Read(header,2048)));
            writer.Write(Hash(Read(header+".nativeTools.ownerBefore",16*1024*1024)));writer.Write(Hash(Read(header+".nativeTools.ownerAfter",16*1024*1024)));
            writer.Write(plan.Length);writer.Write(plan);
            if(Path.GetFileName(Path.GetDirectoryName(header))=="native-viewer")writer.Write(Hash(Read(header+".nativeViewerSource",RebirthProductiveEarnedRowProof.MaximumArtifactBytes)));
            writer.Flush();if(stream.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();return stream.ToArray();
        }
    }
    internal static bool TryRead(string originalPath,byte[] original,byte[] after,int offset,out Description description,out byte[] record)
    {
        description=null;record=null;IDisposable held=null;
        try
        {
            string recordPath=originalPath+".remote-tools-rebased";
            if(!RebirthProductiveEarnedArtifact.SafePath(recordPath)||File.Exists(recordPath+".tmp"))return false;
            long recordLength=new FileInfo(recordPath).Length;
            if(recordLength<1||recordLength>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(64L*1024*1024+8L*recordLength),out held))return false;
            record=Read(originalPath+".remote-tools-rebased",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x31544252u||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Hash(original))return false;
                string header=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,1024),head=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64),beforeHash=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64),afterHash=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                var frame=RebirthProductiveEarnedRowProof.Decode(original);
                string save=Path.GetFullPath(GameIO.GetSaveGameDir()),remote=Path.Combine(save,"RebirthProductiveEarned","remote")+Path.DirectorySeparatorChar;
                if(!Path.GetFullPath(header).StartsWith(remote,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(header)!=frame.Operation.ToString("N")+".header"||Hash(Read(header,2048))!=head)return false;
                byte[] before=Read(header+".nativeTools.ownerBefore",16*1024*1024),ownerAfter=Read(header+".nativeTools.ownerAfter",16*1024*1024);
                if(Hash(before)!=beforeHash||Hash(ownerAfter)!=afterHash)return false;
                int length=reader.ReadInt32();if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||length>stream.Length-stream.Position)return false;
                byte[] plan=reader.ReadBytes(length);
                // The root output plan is independently immutable and remains
                // the paid full Before of this native tools-only successor.
                bool viewer=Path.GetFileName(Path.GetDirectoryName(header))=="native-viewer";
                var paidRecord=Read(header+(viewer?".nativeViewerSource":".nativeOutput"),RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(viewer&&RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Hash(paidRecord)||stream.Position!=stream.Length)return false;
                Guid nonce;string owner,creation;int deaths;
                using(var hs=new MemoryStream(Read(header,2048),false))using(var hr=new BinaryReader(hs))
                {
                    if(hr.ReadByte()!=1)return false;owner=RebirthSurvivorNetworkCodec.ReadBoundedString(hr,64);creation=RebirthSurvivorNetworkCodec.ReadBoundedString(hr,512);deaths=hr.ReadInt32();
                    if(!RebirthOriginalActionScope.Validate(owner,creation)||deaths<0||Path.GetFileName(viewer?Path.GetDirectoryName(Path.GetDirectoryName(header)):Path.GetDirectoryName(header))!=owner||new Guid(hr.ReadBytes(16))!=frame.Operation||new Guid(hr.ReadBytes(16))!=frame.Birth)return false;
                    nonce=new Guid(hr.ReadBytes(16));if(RebirthSurvivorNetworkCodec.ReadBoundedString(hr,44)!=frame.World||RebirthSurvivorNetworkCodec.ReadBoundedString(hr,64)!=Hash(original))return false;
                    hr.ReadInt32();hr.ReadInt32();hr.ReadInt32();if(hs.Position!=hs.Length)return false;
                }
                RebirthProductiveEarnedNativeOutputPlan.Description paid;
                if(!(viewer?RebirthProductiveEarnedViewerToolSource.TryRead(originalPath,original,paidRecord,nonce,out paid):RebirthProductiveEarnedNativeOutputPlan.TryRead(paidRecord,frame,nonce,Hash(original),out paid))||paid.PendingIndex<0||
                    !RebirthProductiveEarnedRemoteToolPlan.TryRead(plan,frame.Operation,frame.Birth,nonce,Hash(original),paid.Native,paid.Offset,out var value)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(value.After,value.AfterOffset,after,offset))return false;
                var beforeData=ReadOwner(before);var afterData=ReadOwner(ownerAfter);
                if(beforeData.id!=afterData.id||beforeData.deaths!=deaths||afterData.deaths!=deaths||!RebirthProductiveEarnedRemoteToolPlan.MatchesOwner(beforeData,value,false)||
                    !RebirthProductiveEarnedRemoteToolPlan.MatchesOwner(afterData,value,true))return false;
                description=new Description{Plan=value,Paid=paid,PlanImage=plan,Header=header,BeforeOwner=before,AfterOwner=ownerAfter,Memory=held,
                    ViewerSource=viewer?paidRecord:null,ViewerTerminal=viewer?Read(originalPath+".native-terminal",2*RebirthProductiveEarnedRowProof.MaximumArtifactBytes):null,HeaderImage=Read(header,2048)};held=null;return true;
            }
        }
        catch{return false;}
        finally{held?.Dispose();}
    }
    private static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static PlayerDataFile ReadOwner(byte[] bytes)
    {
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadChar()!='\0')throw new InvalidDataException();
            uint version=reader.ReadByte();if(version!=PlayerDataFile.cFileVersion)throw new InvalidDataException();
            var data=new PlayerDataFile();data.Read(reader,version,StreamModeRead.Persistency);if(stream.Position!=stream.Length)throw new InvalidDataException();return data;
        }
    }
    private static byte[] Read(string path,int maximum)
    {
        if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(stream.Length<1||stream.Length>maximum)throw new InvalidDataException();byte[] bytes=new byte[(int)stream.Length];int count=0;while(count<bytes.Length){int n=stream.Read(bytes,count,bytes.Length-count);if(n<1)throw new EndOfStreamException();count+=n;}return bytes;}
    }
}
