using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Recorded actual native tool transfer only. Parsed bytes grant no gesture,
// owner inventory setter or productive-work authority; actual native station
// publication/load and the existing pending original remain separate gates.
internal static class RebirthProductiveEarnedPendingToolRebaseRecord
{
    internal sealed class Description
    {
        internal Guid Birth,Source,Gesture;internal int Index,BeforeOffset,AfterOffset,Step;
        internal float BeforeTimer,TargetTimer;internal byte[] Original,JournalRoot,JournalHead,OwnerAfter,Before,After;
    }
    internal static byte[] Digest(byte[] bytes){using(var hash=SHA256.Create())return hash.ComputeHash(bytes);}
    internal static byte[] Encode(Description value)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write(0x31525452u);writer.Write(value.Birth.ToByteArray());writer.Write(value.Source.ToByteArray());writer.Write(value.Gesture.ToByteArray());
            writer.Write(value.Index);writer.Write(value.BeforeTimer);writer.Write(value.TargetTimer);writer.Write(value.Step);
            writer.Write(value.Original);writer.Write(value.JournalRoot);writer.Write(value.JournalHead);writer.Write(value.OwnerAfter);
            writer.Write(value.BeforeOffset);writer.Write(value.Before.Length);writer.Write(value.Before);writer.Write(value.AfterOffset);writer.Write(value.After.Length);writer.Write(value.After);writer.Flush();
            if(stream.Length>RebirthProductiveEarnedNativeSuccessorRecord.MaximumBytes)throw new InvalidDataException();return stream.ToArray();
        }
    }
    internal static bool TryRead(string originalPath,byte[] original,byte[] expectedAfter,int expectedOffset,out Description result,out byte[] record)
    {
        result=null;record=null;
        try
        {
            string path=originalPath+".tools-rebased";
            if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<225||stream.Length>RebirthProductiveEarnedNativeSuccessorRecord.MaximumBytes)return false;
                record=new byte[(int)stream.Length];int read=0;while(read<record.Length){int count=stream.Read(record,read,record.Length-read);if(count<1)return false;read+=count;}
            }
            var value=new Description();
            using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x31525452u)return false;
                value.Birth=new Guid(reader.ReadBytes(16));value.Source=new Guid(reader.ReadBytes(16));value.Gesture=new Guid(reader.ReadBytes(16));
                value.Index=reader.ReadInt32();value.BeforeTimer=reader.ReadSingle();value.TargetTimer=reader.ReadSingle();value.Step=reader.ReadInt32();
                value.Original=reader.ReadBytes(32);value.JournalRoot=reader.ReadBytes(32);value.JournalHead=reader.ReadBytes(32);value.OwnerAfter=reader.ReadBytes(32);
                value.BeforeOffset=reader.ReadInt32();value.Before=ReadBody(reader);value.AfterOffset=reader.ReadInt32();value.After=ReadBody(reader);
                if(stream.Position!=stream.Length||value.OwnerAfter.Length!=32||value.Gesture==Guid.Empty||value.Step<1||value.Step>64||value.Index<0||value.Index>255||
                    float.IsNaN(value.BeforeTimer)||float.IsInfinity(value.BeforeTimer)||float.IsNaN(value.TargetTimer)||float.IsInfinity(value.TargetTimer)||
                    !value.Original.SequenceEqual(Digest(original))||expectedOffset!=value.AfterOffset||!expectedAfter.SequenceEqual(value.After)||
                    !RebirthProductiveEarnedNativeBody.ValidPendingToolReplacement(value.Before,value.BeforeOffset,value.After,value.AfterOffset,value.Index,value.BeforeTimer))return false;
            }
            if(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(originalPath,out var journal))return false;
            using(journal)
            {
                if(journal.PendingInvocation||journal.StationPending||journal.Returned!=value.Step||journal.OwnerStep!=value.Step||journal.StationStep!=value.Step||
                    journal.Birth!=value.Birth||journal.SourceOperation!=value.Source||journal.Operation!=value.Gesture||journal.Index!=value.Index||
                    journal.BeforeTimer!=value.BeforeTimer||journal.TargetTimer!=value.TargetTimer||!journal.OriginalDigest.SequenceEqual(value.Original)||
                    !journal.RootDigest.SequenceEqual(value.JournalRoot)||!journal.HeadDigest.SequenceEqual(value.JournalHead)||!journal.OwnerAfterDigest.SequenceEqual(value.OwnerAfter)||
                    !journal.NativeDigest.SequenceEqual(Digest(value.Before))||!journal.StationAfterDigest.SequenceEqual(Digest(value.After)))return false;
            }
            if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path,record))return false;result=value;return true;
        }
        catch{return false;}
    }
    private static byte[] ReadBody(BinaryReader reader)
    {int length=reader.ReadInt32();if(length<1||length>2*1024*1024||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return bytes;}
}
