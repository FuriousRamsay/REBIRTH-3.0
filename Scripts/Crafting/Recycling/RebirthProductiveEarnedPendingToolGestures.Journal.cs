using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    // Original native gesture WAL. Disk records never grant replay or rebase.
    internal sealed class GestureJournal
    {
        private const long Maximum=64L*1024*1024;
        internal readonly string Path;
        private readonly Guid operation;
        private readonly byte[] rootDigest;
        private string head;private byte[] digest,invoking;private int returned;private bool pending;
        private int ownerReturned;private string ownerPath;private byte[] ownerDigest;
        private GestureJournal(string path,Guid id,byte[] hash){Path=head=path;operation=id;rootDigest=hash;digest=hash;}
        internal static bool TryStart(Observation scope,out GestureJournal journal)
        {
            journal=null;
            if(!scope.BeforeContext()||scope.Steps!=0||scope.OwnerBefore==null||scope.Source.OriginalPath==null)return false;
            var native=scope.Source.CopyNativeBefore();var original=scope.Source.CopyOriginalDigest();
            string world=RebirthProductiveEarnedArtifact.WorldIdentity(scope.World),path=scope.Source.OriginalPath+".tool-gesture";
            if(native==null||native.Length<1||native.Length>Maximum/2||original?.Length!=32||string.IsNullOrEmpty(world)||!Valid(scope.Before))return false;
            byte[] savedHash=null;
            bool saved=scope.OwnerBefore.WriteSavedBefore(scope,(owner,file)=>WriteNew(path,writer=>
            {
                Header(writer,0,scope.Operation);writer.Write(scope.Source.Birth.ToByteArray());writer.Write(scope.Source.Operation.ToByteArray());writer.Write(original);
                Text(writer,world);Text(writer,System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()));Text(writer,scope.Owner);Text(writer,scope.Creation);
                writer.Write(scope.Deaths);writer.Write(scope.Actor.entityId);writer.Write(scope.Session);
                writer.Write(scope.Source.Index);writer.Write(scope.Source.Offset);writer.Write(scope.Source.BeforeTimer);writer.Write(scope.Source.TargetTimer);
                Text(writer,scope.Source.Binding);Text(writer,scope.Source.Extras);writer.Write(native.Length);writer.Write(native);WriteFrame(writer,scope.Before);
                Text(writer,file);writer.Write(owner.Length);writer.Write(owner);
            },out savedHash));
            if(!saved)return false;journal=new GestureJournal(path,scope.Operation,savedHash);return true;
        }
        internal bool Enter(Observation scope,Frame before)
        {
            if(pending||scope.Operation!=operation||scope.Steps!=returned||returned>=64||!Valid(before)||!Current(Path,rootDigest)||!Current(head,digest))return false;
            string path=Path+"."+(returned+1).ToString("D2",System.Globalization.CultureInfo.InvariantCulture)+".invoking";
            if(!WriteNew(path,writer=>{Header(writer,1,operation);writer.Write(returned+1);writer.Write(digest);WriteFrame(writer,before);},out var next))return false;
            head=path;invoking=next;pending=true;return true;
        }
        internal bool Return(Observation scope,Frame after)
        {
            if(!pending||scope.Operation!=operation||scope.Steps!=returned||!Valid(after)||!Current(Path,rootDigest)||!Current(head,invoking))return false;
            string path=Path+"."+(returned+1).ToString("D2",System.Globalization.CultureInfo.InvariantCulture)+".returned";
            if(!WriteNew(path,writer=>{Header(writer,2,operation);writer.Write(returned+1);writer.Write(invoking);WriteFrame(writer,after);},out var next))return false;
            head=path;digest=next;invoking=null;pending=false;returned++;return true;
        }
        internal bool PublishOwnerAfter(Observation scope)
        {
            if(pending||returned<1||scope.Operation!=operation||scope.Steps!=returned||!scope.ReturnedContext()||
                !Valid(scope.After)||!Current(Path,rootDigest)||!Current(head,digest))return false;
            // A historical immutable publication is never rewritten with a
            // later equal-looking owner capture. It remains this exact event.
            if(ownerReturned==returned)return Current(ownerPath,ownerDigest);
            if(scope.OwnerAfter==null)return false;
            string path=Path+"."+returned.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)+".owner-after";
            byte[] saved=null;
            if(!scope.OwnerAfter.WriteSavedAfter(scope,(owner,file)=>WriteNew(path,writer=>
            {
                Header(writer,3,operation);writer.Write(returned);writer.Write(rootDigest);writer.Write(digest);
                WriteFrame(writer,scope.After);Text(writer,file);writer.Write(owner.Length);writer.Write(owner);
            },out saved)))return false;
            ownerReturned=returned;ownerPath=path;ownerDigest=saved;return true;
        }
        internal bool NeedsOwnerAfter(int steps)=>!pending&&returned>0&&returned==steps&&ownerReturned!=returned;
        internal bool EnterStationSync(Observation scope,byte[] before,int beforeOffset,byte[] after,int afterOffset,out byte[] entry)
        {
            entry=null;
            if(pending||returned<1||scope.Operation!=operation||scope.Steps!=returned||!Current(Path,rootDigest)||!Current(head,digest))return false;
            string path=Path+"."+returned.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)+".station-invoking";
            return WriteNew(path,writer=>
            {
                Header(writer,4,operation);writer.Write(returned);writer.Write(rootDigest);writer.Write(digest);
                writer.Write(beforeOffset);writer.Write(before.Length);writer.Write(before);writer.Write(afterOffset);writer.Write(after.Length);writer.Write(after);
            },out entry);
        }
        internal bool ReturnStationSync(Observation scope,byte[] entry,byte[] after,int afterOffset)
        {
            if(pending||returned<1||scope.Operation!=operation||scope.Steps!=returned||!Current(Path,rootDigest)||!Current(head,digest))return false;
            string path=Path+"."+returned.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)+".station-invoking";
            if(!Current(path,entry))return false;
            return WriteNew(Path+"."+returned.ToString("D2",System.Globalization.CultureInfo.InvariantCulture)+".station-returned",writer=>
            {Header(writer,5,operation);writer.Write(returned);writer.Write(entry);writer.Write(afterOffset);writer.Write(after.Length);writer.Write(after);},out _);
        }
        private static void Header(BinaryWriter writer,byte purpose,Guid operation)
        {writer.Write(new byte[]{(byte)'R',(byte)'T',(byte)'G',1});writer.Write(purpose);writer.Write(operation.ToByteArray());}
        private static void Text(BinaryWriter writer,string text)
        {if(text==null||text.Length>1048576)throw new InvalidDataException();writer.Write(text);}
        private static bool Valid(Frame frame)=>frame?.Cells?.Length==4&&frame.Counts?.Length==4&&
            frame.Cells.All(value=>value!=null&&value.Length>0&&value.Length<=349528)&&frame.Counts.All(count=>count>0&&count<=255);
        private static void WriteFrame(BinaryWriter writer,Frame frame)
        {for(int i=0;i<4;i++){writer.Write(frame.Counts[i]);writer.Write(frame.Cells[i]);}}
        private static bool Current(string path,byte[] expected)
        {
            try{return expected?.Length==32&&!File.Exists(path+".tmp")&&Safe(path)&&Hash(path).SequenceEqual(expected);}
            catch{return false;}
        }
        private static bool Safe(string path)=>RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path);
        private static byte[] Hash(string path)
        {
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var hash=SHA256.Create())
            {if(file.Length<1||file.Length>Maximum)throw new InvalidDataException();return hash.ComputeHash(file);}
        }
        private static bool WriteNew(string path,Action<BinaryWriter> write,out byte[] digest)
        {
            digest=null;IDisposable allocation=null;
            try
            {
                if(!ThreadManager.IsMainThread()||!Safe(path)||!Safe(path+".tmp")||File.Exists(path)||File.Exists(path+".tmp")||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out allocation))return false;
                using(var file=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var bounded=new BoundedWrite(file))
                using(var writer=new BinaryWriter(bounded,Encoding.UTF8,true))
                {write(writer);writer.Flush();file.Flush(true);}
                var expected=Hash(path+".tmp");
                if(!Safe(path)||!Safe(path+".tmp"))return false;
                File.Move(path+".tmp",path);
                if(!Current(path,expected))return false;digest=expected;return true;
            }
            catch{return false;}
            finally{allocation?.Dispose();}
        }
        private sealed class BoundedWrite:Stream
        {
            private readonly Stream target;private long written;
            internal BoundedWrite(Stream stream){target=stream;}
            public override void Write(byte[] buffer,int offset,int count)
            {if(count<0||written>Maximum-count)throw new InvalidDataException();target.Write(buffer,offset,count);written+=count;}
            public override void WriteByte(byte value){if(written>=Maximum)throw new InvalidDataException();target.WriteByte(value);written++;}
            public override void Flush()=>target.Flush();
            public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
            public override long Length=>written;public override long Position{get=>written;set=>throw new NotSupportedException();}
            public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();
            public override long Seek(long n,SeekOrigin origin)=>throw new NotSupportedException();
            public override void SetLength(long n)=>throw new NotSupportedException();
        }
    }
    private static void PumpJournalOwnerAfter()
    {
        if(!RebirthProductiveEarnedCoordinator.Enabled||executing!=null)return;
        foreach(var scope in observations.Values.ToArray())
        {
            if(scope.Journal==null||scope.OwnerAfter==null||scope.Unknown||!scope.Journal.NeedsOwnerAfter(scope.Steps)||
                !scope.OwnerAfter.TryBeginJournalPublication())continue;
            try
            {
                // Saved lookup alone does not compare all four current live
                // vectors. Reuse the actual returned/live-frame qualification.
                if(!TryObserveReturnedGesture(scope.Actor,scope.Station,out var returned)||!ReferenceEquals(scope,returned))continue;
                scope.Journal.PublishOwnerAfter(scope);
            }
            catch{/* Preserve existing primary/temporary and unresolved custody. */}
        }
    }
}
