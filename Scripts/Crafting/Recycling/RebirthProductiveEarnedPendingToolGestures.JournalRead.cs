using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    // Detached description ONLY. It is not a source installation, accepted
    // native transfer, saved owner token, replay permit or rebase authority.
    internal sealed class ParsedGestureJournal:IDisposable
    {
        internal Guid Operation,Birth,SourceOperation;
        internal string WorldKey,SaveRoot,Owner,Creation,Binding,Extras,OwnerBeforePath,OwnerAfterPath;
        internal int Deaths,Entity,Index,Offset,NativeLength,Returned,OwnerStep;internal long Session;
        internal float BeforeTimer,TargetTimer;internal bool PendingInvocation;
        internal byte[] OriginalDigest,NativeDigest,RootDigest,HeadDigest,OwnerBeforeDigest,OwnerAfterDigest;
        internal Frame Before,After;private IDisposable memory;
        internal int StationStep;internal bool StationPending;
        internal byte[] StationBeforeDigest,StationAfterDigest;
        internal ParsedGestureJournal(IDisposable allocation){memory=allocation;}
        public void Dispose()
        {
            var held=System.Threading.Interlocked.Exchange(ref memory,null);if(held==null)return;
            WorldKey=SaveRoot=Owner=Creation=Binding=Extras=OwnerBeforePath=OwnerAfterPath=null;
            OriginalDigest=NativeDigest=RootDigest=HeadDigest=OwnerBeforeDigest=OwnerAfterDigest=null;Before=After=null;held.Dispose();
            StationBeforeDigest=StationAfterDigest=null;
        }
    }
    internal static bool TryInspectGestureJournal(string originalPath,out ParsedGestureJournal parsed)
    {
        parsed=null;ParsedGestureJournal value=null;IDisposable memory=null;
        try
        {
            if(!ThreadManager.IsMainThread()||executing!=null||string.IsNullOrEmpty(originalPath))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024,out memory))return false;
                string root=System.IO.Path.GetFullPath(originalPath)+".tool-gesture";
                if(!KnownJournalFiles(root))return false;
                value=new ParsedGestureJournal(memory);memory=null;
                if(!ReadJournalRecord(root,reader=>
                {
                    value.Operation=ReadJournalHeader(reader,0);value.Birth=ReadJournalGuid(reader);value.SourceOperation=ReadJournalGuid(reader);
                    value.OriginalDigest=Exact(reader,32);value.WorldKey=JournalText(reader,4096);value.SaveRoot=JournalText(reader,32768);
                    value.Owner=JournalText(reader,4096);value.Creation=JournalText(reader,4096);value.Deaths=reader.ReadInt32();value.Entity=reader.ReadInt32();value.Session=reader.ReadInt64();
                    value.Index=reader.ReadInt32();value.Offset=reader.ReadInt32();value.BeforeTimer=reader.ReadSingle();value.TargetTimer=reader.ReadSingle();
                    value.Binding=JournalText(reader,1048576);value.Extras=JournalText(reader,1048576);
                    if(value.Deaths<0||value.Session<1||value.Index<0||value.Index>255||value.Offset<0||value.Offset>32*1024*1024||
                        !Finite(value.BeforeTimer)||!Finite(value.TargetTimer)||
                        string.IsNullOrEmpty(value.WorldKey)||string.IsNullOrEmpty(value.Owner)||string.IsNullOrEmpty(value.Creation)||!CanonicalJournalPath(value.SaveRoot))throw new InvalidDataException();
                    value.NativeDigest=JournalBlobHash(reader,32*1024*1024,out value.NativeLength);value.Before=ReadJournalFrame(reader);value.After=value.Before;
                    value.OwnerBeforePath=JournalText(reader,32768);if(!CanonicalJournalPath(value.OwnerBeforePath))throw new InvalidDataException();
                    value.OwnerBeforeDigest=JournalBlobHash(reader,16*1024*1024,out _);
                    if(!EmptyJournalCursor(value.Before)||!Conserved(value.Before,value.Before))throw new InvalidDataException();
                },out value.RootDigest))return false;
                value.HeadDigest=value.RootDigest;bool gap=false;
                for(int step=1;step<=64;step++)
                {
                    string prefix=root+"."+step.ToString("D2",System.Globalization.CultureInfo.InvariantCulture);
                    string invoke=prefix+".invoking",returned=prefix+".returned",owner=prefix+".owner-after";
                    string stationInvoke=prefix+".station-invoking",stationReturn=prefix+".station-returned";
                    if(!File.Exists(invoke))
                    {if(File.Exists(returned)||File.Exists(owner)||File.Exists(stationInvoke)||File.Exists(stationReturn))return false;gap=true;continue;}
                    if(gap||value.PendingInvocation||value.StationPending)return false;
                    if(!ReadJournalRecord(invoke,reader=>
                    {
                        if(ReadJournalHeader(reader,1)!=value.Operation||reader.ReadInt32()!=step||!Exact(reader,32).SequenceEqual(value.HeadDigest)||
                            !ReadJournalFrame(reader).Same(value.After))throw new InvalidDataException();
                    },out var invokedDigest))return false;
                    value.HeadDigest=invokedDigest;
                    if(!File.Exists(returned))
                    {if(File.Exists(owner)||File.Exists(stationInvoke)||File.Exists(stationReturn))return false;value.PendingInvocation=true;continue;}
                    Frame next=null;
                    if(!ReadJournalRecord(returned,reader=>
                    {
                        if(ReadJournalHeader(reader,2)!=value.Operation||reader.ReadInt32()!=step||!Exact(reader,32).SequenceEqual(invokedDigest))throw new InvalidDataException();
                        next=ReadJournalFrame(reader);
                        if(!next.Counts.SequenceEqual(value.Before.Counts)||!Conserved(value.Before,next))throw new InvalidDataException();
                    },out var returnedDigest))return false;
                    value.After=next;value.Returned=step;value.HeadDigest=returnedDigest;
                    if(File.Exists(owner))
                    {
                        if(!ReadJournalRecord(owner,reader=>
                        {
                            if(ReadJournalHeader(reader,3)!=value.Operation||reader.ReadInt32()!=step||!Exact(reader,32).SequenceEqual(value.RootDigest)||
                                !Exact(reader,32).SequenceEqual(returnedDigest)||!ReadJournalFrame(reader).Same(value.After))throw new InvalidDataException();
                            value.OwnerAfterPath=JournalText(reader,32768);if(!CanonicalJournalPath(value.OwnerAfterPath))throw new InvalidDataException();
                            value.OwnerAfterDigest=JournalBlobHash(reader,16*1024*1024,out _);
                            if(!EmptyJournalCursor(value.After))throw new InvalidDataException();
                        },out _))return false;
                        value.OwnerStep=step;
                    }
                    if(!File.Exists(stationInvoke)){if(File.Exists(stationReturn))return false;continue;}
                    int afterOffset=-1;
                    if(!ReadJournalRecord(stationInvoke,reader=>
                    {
                        if(ReadJournalHeader(reader,4)!=value.Operation||reader.ReadInt32()!=step||!Exact(reader,32).SequenceEqual(value.RootDigest)||!Exact(reader,32).SequenceEqual(returnedDigest))throw new InvalidDataException();
                        int offset=reader.ReadInt32();value.StationBeforeDigest=JournalBlobHash(reader,8*1024*1024,out int length);if(offset<0||offset>=length)throw new InvalidDataException();
                        afterOffset=reader.ReadInt32();value.StationAfterDigest=JournalBlobHash(reader,8*1024*1024,out length);if(afterOffset<0||afterOffset>=length)throw new InvalidDataException();
                    },out var stationEntry))return false;
                    value.StationStep=step;value.StationPending=true;
                    if(!File.Exists(stationReturn))continue;
                    if(!ReadJournalRecord(stationReturn,reader=>
                    {
                        if(ReadJournalHeader(reader,5)!=value.Operation||reader.ReadInt32()!=step||!Exact(reader,32).SequenceEqual(stationEntry)||reader.ReadInt32()!=afterOffset||
                            !JournalBlobHash(reader,8*1024*1024,out _).SequenceEqual(value.StationAfterDigest))throw new InvalidDataException();
                    },out _))return false;
                    value.StationPending=false;
                }
                parsed=value;value=null;return true;
            }
        }
        catch{return false;}
        finally{value?.Dispose();memory?.Dispose();}
    }
    private static bool KnownJournalFiles(string root)
    {
        if(!File.Exists(root)||File.Exists(root+".tmp")||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(root))return false;
        string name=System.IO.Path.GetFileName(root);int count=0;
        foreach(string file in Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(root),name+"*"))
        {
            if(++count>321)return false;
            string actual=System.IO.Path.GetFileName(file);if(actual==name)continue;
            string suffix=actual.Substring(name.Length);
            if(suffix.Length<4||suffix[0]!='.'||suffix[1]<'0'||suffix[1]>'9'||suffix[2]<'0'||suffix[2]>'9')return false;
            int step=(suffix[1]-'0')*10+suffix[2]-'0';string kind=suffix.Substring(3);
            if(step<1||step>64||(kind!=".invoking"&&kind!=".returned"&&kind!=".owner-after"&&kind!=".station-invoking"&&kind!=".station-returned"))return false;
        }
        return true;
    }
    private static bool ReadJournalRecord(string path,Action<BinaryReader> read,out byte[] digest)
    {
        digest=null;
        try
        {
            if(File.Exists(path+".tmp")||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var hash=SHA256.Create())using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true),true))
            {
                if(stream.Length<21||stream.Length>64L*1024*1024)return false;
                var initial=hash.ComputeHash(stream);stream.Position=0;read(reader);if(stream.Position!=stream.Length)return false;
                stream.Position=0;var final=hash.ComputeHash(stream);
                if(!initial.SequenceEqual(final)||File.Exists(path+".tmp")||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return false;
                digest=initial;return true;
            }
        }
        catch{return false;}
    }
    private static Guid ReadJournalHeader(BinaryReader reader,byte purpose)
    {if(!Exact(reader,5).SequenceEqual(new byte[]{(byte)'R',(byte)'T',(byte)'G',1,purpose}))throw new InvalidDataException();return ReadJournalGuid(reader);}
    private static Guid ReadJournalGuid(BinaryReader reader)
    {var guid=new Guid(Exact(reader,16));if(guid==Guid.Empty)throw new InvalidDataException();return guid;}
    private static byte[] Exact(BinaryReader reader,int count)
    {if(count<0||count>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();var bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return bytes;}
    private static string JournalText(BinaryReader reader,int maximum)
    {
        int length=0,shift=0,parts=0;byte next;
        do
        {
            next=reader.ReadByte();if(++parts>5||(parts==5&&next>7))throw new InvalidDataException();
            length|=(next&127)<<shift;shift+=7;
        }while((next&128)!=0);
        if(parts>1&&next==0||length<0||length>3L*maximum)throw new InvalidDataException();
        string value=new UTF8Encoding(false,true).GetString(Exact(reader,length));if(value.Length>maximum)throw new InvalidDataException();return value;
    }
    private static byte[] JournalBlobHash(BinaryReader reader,int maximum,out int length)
    {
        length=reader.ReadInt32();if(length<1||length>maximum||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();
        using(var hash=SHA256.Create())
        {
            var buffer=new byte[8192];int remaining=length;
            while(remaining>0){int read=reader.Read(buffer,0,Math.Min(buffer.Length,remaining));if(read<1)throw new EndOfStreamException();hash.TransformBlock(buffer,0,read,buffer,0);remaining-=read;}
            hash.TransformFinalBlock(new byte[0],0,0);return hash.Hash;
        }
    }
    private static Frame ReadJournalFrame(BinaryReader reader)
    {
        var frame=new Frame{Cells=new string[4],Counts=new int[4]};int[] limits={169,20,255,1};
        for(int i=0;i<4;i++)
        {
            int count=reader.ReadInt32();if(count<1||count>limits[i])throw new InvalidDataException();frame.Counts[i]=count;
            string text=JournalText(reader,349528);byte[] bytes=Convert.FromBase64String(text);
            if(bytes.Length<1||bytes.Length>262144||Convert.ToBase64String(bytes)!=text||
                !RebirthProductiveEarnedObservationBudget.TryMeasureTools(bytes,out _,out _))throw new InvalidDataException();frame.Cells[i]=text;
        }
        return frame;
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    private static bool EmptyJournalCursor(Frame frame)=>RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{ItemStack.Empty},262144,349528,out var empty)&&frame.Cells[3]==empty;
    private static bool CanonicalJournalPath(string path)=>!string.IsNullOrEmpty(path)&&System.IO.Path.IsPathRooted(path)&&System.IO.Path.GetFullPath(path)==path;
}
