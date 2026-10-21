using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // A scoped call to the actual native local-save producer. Neither a later
    // equal file nor a setter permit consumed before original execution suffices.
    private sealed class CancellationOwnerSave
    {
        internal CancellationOwnerProgress Progress;internal CancellationOwnerStep Step;
        internal string Expected,Directory,PlayerId,Path;internal PlayerDataFile File;
        internal byte[] Native;internal bool Entered,Finished,Saved;
        internal bool Current=>ThreadManager.IsMainThread()&&Progress?.Current==true&&
            ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),Progress.Original.Actor)&&
            RebirthRemoteResourceClientOriginal.ReadActor(Expected).Matches(RebirthCamperEnergyActorImage.Capture(Progress.Original.Actor));
    }
    [ThreadStatic]private static CancellationOwnerSave cancellationOwnerSave;
    private static bool SaveCancellationOwnerCheckpoint(CancellationOwnerProgress progress,CancellationOwnerStep step)
    {
        if(!ThreadManager.IsMainThread()||cancellationOwnerSave!=null||progress?.Current!=true||step!=null&&!step.NativeReturned)return false;
        var scope=new CancellationOwnerSave{Progress=progress,Step=step,
            Expected=step?.After??progress.Before};
        try
        {
            // Reserve bounded native serializer residency BEFORE any native image capture.
            if(!progress.Original.Memory.Reserve(Math.Max(progress.Original.Memory.ReservedBytes,checked(progress.ReadFloor+64L*1024*1024)))||!scope.Current)return false;
            scope.Directory=Path.GetFullPath(GameIO.GetPlayerDataDir());scope.PlayerId=progress.Original.Original.Owner;
            scope.Path=Path.Combine(scope.Directory,scope.PlayerId+".ttp");
            if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(scope.Path))return false;
            cancellationOwnerSave=scope;
            GameManager.Instance.SaveLocalPlayerData();
            if(!scope.Saved||!scope.Current||scope.Native==null)return false;
            var bytes=Encoding.UTF8.GetBytes(new XElement("placedCancellationOwnerCheckpoint",new XAttribute("version",1),
                new XAttribute("cancel",progress.Original.Operation.ToString("N")),new XAttribute("ordinal",step?.Ordinal??-1),
                new XAttribute("setterReturned",step!=null),new XElement("completePlan",progress.Plan),new XElement("actor",scope.Expected),
                new XElement("nativeOwner",Convert.ToBase64String(scope.Native))).ToString(SaveOptions.DisableFormatting));
            return WriteOwnerProgress(progress,OwnerProgressKey(progress.Original.Operation,step?.Ordinal??-1),"OwnerCheckpoint",bytes,RebirthCamperTransferJournal.Phase.Applied);
        }
        catch{return false;}
        finally
        {
            if(ReferenceEquals(cancellationOwnerSave,scope))cancellationOwnerSave=null;
            scope.File=null;scope.Native=null;scope.Progress=null;scope.Step=null;
        }
    }
    internal static void ObserveCancellationOwnerFromPlayer(PlayerDataFile data,EntityPlayer actor,bool runOriginal)
    {
        if(!ThreadManager.IsMainThread())return;var scope=cancellationOwnerSave;
        if(scope==null)return;
        try
        {
            if(!runOriginal||scope.File!=null||!scope.Current||!ReferenceEquals(actor,scope.Progress.Original.Actor))return;
            var image=CaptureCancellationOwnerNative(scope,data);
            if(image==null||image.Length<1||image.Length>16*1024*1024)return;
            // Reserve the complete forthcoming native-image/checkpoint working
            // set before any financial setter (baseline) and retain that budget.
            long bytes=checked(scope.Progress.ReadFloor+32L*(scope.Progress.PlanBytes.Length+2L*image.Length+2L*scope.Expected.Length+65536));
            if(!scope.Progress.Original.Memory.Reserve(Math.Max(bytes,scope.Progress.Original.Memory.ReservedBytes)))return;
            scope.Native=image;scope.File=data;
        }
        catch{/* No positive native event; retain the pending original. */}
    }
    internal static void EnterCancellationOwnerSave(PlayerDataFile data,string directory,string playerId,bool runOriginal,out object state)
    {
        state=null;if(!ThreadManager.IsMainThread())return;var scope=cancellationOwnerSave;if(scope==null)return;
        try
        {
            if(!runOriginal||scope.Entered||!ReferenceEquals(scope.File,data)||scope.Native==null||!scope.Current||
                Path.GetFullPath(directory)!=scope.Directory||playerId!=scope.PlayerId||
                !CancellationOwnerNativeMatches(data,scope.Native))return;
            scope.Entered=true;state=scope;
        }
        catch{scope.File=null;scope.Native=null;}
    }
    internal static void ObserveCancellationOwnerSave(bool runOriginal,object state)
    {
        if(!ThreadManager.IsMainThread()||!(state is CancellationOwnerSave scope)||scope.Finished)return;
        scope.Finished=true;
        try
        {
            scope.Saved=runOriginal&&ReferenceEquals(cancellationOwnerSave,scope)&&scope.Entered&&scope.Current&&scope.Native!=null&&
                CancellationOwnerNativeMatches(scope.File,scope.Native)&&
                RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(scope.Path,scope.Native);
        }
        catch{scope.Saved=false;}
    }
    private sealed class CancellationOwnerNativeStream:Stream
    {
        private readonly byte[] image;private readonly bool compare;private int position;
        internal CancellationOwnerNativeStream(byte[] value,bool matching){image=value;compare=matching;}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>position;public override long Position{get=>position;set=>throw new NotSupportedException();}
        public override void Write(byte[] buffer,int offset,int count)
        {
            if(offset<0||count<0||offset>buffer.Length-count||position>image.Length-count)throw new InvalidDataException();
            if(compare){for(int i=0;i<count;i++)if(image[position+i]!=buffer[offset+i])throw new InvalidDataException();}
            else Buffer.BlockCopy(buffer,offset,image,position,count);position+=count;
        }
        public override void WriteByte(byte value){if(position>=image.Length||compare&&image[position]!=value)throw new InvalidDataException();if(!compare)image[position]=value;position++;}
        public override void Flush(){}public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long value,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    private static void WriteCancellationOwnerNative(PlayerDataFile data,Stream stream)
    {
        using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            writer.SetBaseStream(stream);writer.Write('t');writer.Write('t');writer.Write('p');writer.Write((byte)0);writer.Write((byte)PlayerDataFile.cFileVersion);
            data.Write(writer,global::StreamModeWrite.Persistency);writer.Flush();
        }
    }
    private static byte[] CaptureCancellationOwnerNative(CancellationOwnerSave scope,PlayerDataFile data)
    {
        int size;using(var counter=new RebirthRemoteResourcePlacedStationStreams.Counter(16*1024*1024))
        {WriteCancellationOwnerNative(data,counter);size=checked((int)counter.Length);}
        // The fixed array has no geometric MemoryStream growth or ToArray copy.
        // Count pass itself already owns the reserved 64MiB serializer scratch.
        long workspace=checked(scope.Progress.ReadFloor+64L*1024*1024+32L*(scope.Progress.PlanBytes.Length+2L*size+2L*scope.Expected.Length+65536));
        if(size<1||!scope.Progress.Original.Memory.Reserve(Math.Max(workspace,scope.Progress.Original.Memory.ReservedBytes)))return null;
        var image=new byte[size];using(var stream=new CancellationOwnerNativeStream(image,false))
        {WriteCancellationOwnerNative(data,stream);if(stream.Length!=size)throw new InvalidDataException();}return image;
    }
    private static bool CancellationOwnerNativeMatches(PlayerDataFile data,byte[] image)
    {
        if(image==null)return false;using(var stream=new CancellationOwnerNativeStream(image,true))
        {WriteCancellationOwnerNative(data,stream);return stream.Length==image.Length;}
    }
}
