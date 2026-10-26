using System;
using System.IO;

// Read detached native save records only. Never call ApplyToEntity or load a
// replacement vehicle just to acknowledge persistence.
public static class RebirthCamperSavedVehicle
{
    public sealed class Expected : IDisposable
    {
        internal int Id,Class;
        internal byte[] Bag,Entity;
        internal World World;
        internal string SaveDirectory;
        internal bool DumpedMismatch;
        internal float LastDiagnosticAt;
        internal string LastDiagnostic;
        internal IRebirthStationSnapshotByteBudget Budget;
        internal IDisposable Allocation;
        public void Dispose(){Bag=Entity=null;var allocation=Allocation;Allocation=null;Budget=null;allocation?.Dispose();}
    }
    public static Expected Capture(EntityVehicle vehicle)
    {
        if(!ThreadManager.IsMainThread() || vehicle==null || vehicle.world==null || vehicle.world.IsRemote())
            throw new InvalidOperationException("Authoritative live vehicle required.");
        var record=new EntityCreationData(vehicle,StreamModeWrite.Persistency);
        if(record.bagData==null || record.entityData==null)throw new InvalidOperationException("Native vehicle save snapshot is incomplete.");
        return new Expected {Id=record.id,Class=record.entityClass,Bag=record.bagData.ToArray(),Entity=record.entityData.ToArray(),
            World=vehicle.world,SaveDirectory=GameIO.GetSaveGameDir()};
    }
    public static Expected RequestSave(EntityVehicle vehicle,IRebirthStationSnapshotByteBudget budget=null)
    {
        var expected=budget==null?Capture(vehicle):CaptureBudgeted(vehicle,budget);
        var manager=VehicleManager.Instance;
        if(manager==null || string.IsNullOrEmpty(expected.SaveDirectory))
        {expected.Dispose();throw new InvalidOperationException("Native vehicle persistence is unavailable.");}
        // Use native Update's single-writer scheduling. Calling Save directly
        // can start another pooled writer before the previous worker registers.
        // Only Verify proves that this particular snapshot reached disk.
        try
        {
            manager.TriggerSave();
            // Let native Update start the one writer on its next frame rather than
            // holding every UI transfer for TriggerSave's ten-second countdown.
            manager.saveTime=0f;return expected;
        }
        catch{expected.Dispose();throw;}
    }
    internal sealed class CountStream:Stream
    {
        private long position,length;internal readonly long Maximum;
        internal CountStream(long maximum){Maximum=maximum;}
        public override bool CanRead=>false;public override bool CanSeek=>true;public override bool CanWrite=>true;
        public override long Length=>length;public override long Position{get=>position;set{if(value<0||value>Maximum)throw new InvalidDataException();position=value;}}
        public override void Flush(){}public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin from){Position=checked((from==SeekOrigin.Begin?0:from==SeekOrigin.Current?position:length)+o);return position;}
        public override void SetLength(long value){if(value<0||value>Maximum)throw new InvalidDataException();length=value;}
        public override void Write(byte[] b,int o,int n){if(n<0||position>Maximum-n)throw new InvalidDataException();position+=n;length=Math.Max(length,position);}
        public override void WriteByte(byte value){if(position>=Maximum)throw new InvalidDataException();position++;length=Math.Max(length,position);}
    }
    // The native vehicle ECD constructor invokes exactly bag.Write then
    // vehicle.Write; vehicle is not EntityAlive and creates no cloned stats.
    // Count those actual serializers, then give them exact fixed buffers.
    private static Expected CaptureBudgeted(EntityVehicle vehicle,IRebirthStationSnapshotByteBudget budget)
    {
        if(!ThreadManager.IsMainThread()||vehicle?.world==null||vehicle.world.IsRemote())throw new InvalidOperationException("Authoritative vehicle required.");
        IDisposable scratch=null,allocation=null;
        try
        {
            if(!budget.TryReserveAllocation(128*1024,out scratch))throw new InvalidOperationException("Vehicle serializer allocation pressure.");
            long bag=Count(vehicle,true),entity=Count(vehicle,false);
            if(bag<0||entity<1||bag>int.MaxValue||entity>ushort.MaxValue)throw new InvalidDataException("Unsupported native vehicle body length.");
            if(!budget.TryReserveAllocation(checked(bag+entity+4096),out allocation))throw new InvalidOperationException("Vehicle snapshot allocation pressure.");
            var beforeWorld=vehicle.world;int id=vehicle.entityId,kind=vehicle.entityClass;
            byte[] bagBytes=Serialize(vehicle,true,(int)bag),entityBytes=Serialize(vehicle,false,(int)entity);
            if(!ReferenceEquals(vehicle.world,beforeWorld)||vehicle.entityId!=id||vehicle.entityClass!=kind)throw new InvalidOperationException("Vehicle changed during capture.");
            var result=new Expected{Id=id,Class=kind,Bag=bagBytes,Entity=entityBytes,World=beforeWorld,SaveDirectory=GameIO.GetSaveGameDir(),Budget=budget,Allocation=allocation};
            allocation=null;return result;
        }
        finally{allocation?.Dispose();scratch?.Dispose();}
    }
    private static long Count(EntityVehicle vehicle,bool bag)
    {
        using(var stream=new CountStream(int.MaxValue))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(false))
        {writer.SetBaseStream(stream);if(bag){if(vehicle.bag!=null)vehicle.bag.Write(writer,StreamModeWrite.Persistency);}else vehicle.Write(writer,StreamModeWrite.Persistency);writer.Flush();return stream.Length;}
    }
    private static byte[] Serialize(EntityVehicle vehicle,bool bag,int length)
    {
        var bytes=new byte[length];using(var stream=new MemoryStream(bytes,0,bytes.Length,true,true))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(false))
        {writer.SetBaseStream(stream);if(bag){if(vehicle.bag!=null)vehicle.bag.Write(writer,StreamModeWrite.Persistency);}else vehicle.Write(writer,StreamModeWrite.Persistency);writer.Flush();if(stream.Position!=length)throw new InvalidDataException("Native vehicle length changed.");}
        return bytes;
    }
    public static bool Verify(EntityVehicle vehicle,Expected expected,Func<bool> currentSession)
    {
        if(!ThreadManager.IsMainThread() || vehicle==null || expected==null || currentSession==null || !currentSession()
            || vehicle.world==null || vehicle.world.IsRemote() || vehicle.entityId!=expected.Id
            || !ReferenceEquals(vehicle.world,expected.World)
            || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle))
        {Diagnostic(expected,"session-or-entity-changed");return false;}
        string directory=GameIO.GetSaveGameDir();
        if(string.IsNullOrEmpty(directory) || directory!=expected.SaveDirectory)
        {Diagnostic(expected,"save-directory-changed");return false;}
        var writer=VehicleManager.Instance?.saveThread;
        if(writer!=null && !writer.HasTerminated())
        {Diagnostic(expected,"native-writer-running");return false;}
        try
        {
            string path=Path.Combine(directory,"vehicles.dat");
            if(!SdFile.Exists(path)){Diagnostic(expected,"vehicles-file-missing");return false;}
            bool found=false;
            using(var stream=SdFile.OpenRead(path))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
            {
                reader.SetBaseStream(stream);
                if(reader.ReadChar()!='v' || reader.ReadChar()!='d' || reader.ReadChar()!='a' || reader.ReadChar()!='\0'
                    || reader.ReadByte()!=1)return false;
                int count=reader.ReadInt32();
                if(count<0 || count>65536)return false;
                for(int index=0;index<count;index++)
                {
                    IDisposable recordAllocation=null;
                    long recordEnd=-1;
                    if(expected.Budget!=null)
                    {
                        if(!TryMeasureVehicleRecord(stream,out recordEnd,out long bytes)||
                            !expected.Budget.TryReserveAllocation(bytes,out recordAllocation))return false;
                    }
                    var record=new EntityCreationData();
                    try
                    {
                    record.read(reader,StreamModeRead.Persistency);
                    if(recordEnd>=0&&stream.Position!=recordEnd)return false;
                    if(record.id!=expected.Id)continue;
                    if(found || record.entityClass!=expected.Class || record.bagData==null || record.entityData==null)return false;
                    byte[] actualBag=record.bagData.ToArray(),actualEntity=record.entityData.ToArray();
                    bool bagMatches=Equal(actualBag,expected.Bag),entityMatches=Equal(actualEntity,expected.Entity);
                    if(!bagMatches||!entityMatches)
                    {
                        Diagnostic(expected,"bytes-differ bag="+bagMatches+" entity="+entityMatches+
                            " bagFirstDiff="+FirstDifference(actualBag,expected.Bag)+" entityFirstDiff="+FirstDifference(actualEntity,expected.Entity));
                        RebirthCamperTrace.Write("save.compare-mismatch",vehicle,"bag="+bagMatches+" entity="+entityMatches
                            +" bagFirstDiff="+FirstDifference(actualBag,expected.Bag)+" entityFirstDiff="+FirstDifference(actualEntity,expected.Entity));
                        if(RebirthCamperTrace.Enabled&&!expected.DumpedMismatch&&!string.IsNullOrEmpty(RebirthGameBridge.OutputDir))
                        {
                            expected.DumpedMismatch=true;
                            string folder=Path.Combine(RebirthGameBridge.OutputDir,"camper_save_diffs",expected.Id.ToString());
                            Directory.CreateDirectory(folder);
                            File.WriteAllBytes(Path.Combine(folder,"expected-bag.bin"),expected.Bag);
                            File.WriteAllBytes(Path.Combine(folder,"actual-bag.bin"),actualBag);
                            File.WriteAllBytes(Path.Combine(folder,"expected-entity.bin"),expected.Entity);
                            File.WriteAllBytes(Path.Combine(folder,"actual-entity.bin"),actualEntity);
                        }
                        return false;
                    }
                    found=true;
                    }
                    finally{record.bagData?.Dispose();record.entityData?.Dispose();recordAllocation?.Dispose();}
                }
                if(stream.Position!=stream.Length)return false;
            }
            if(!found)Diagnostic(expected,"vehicle-record-missing");
            return found && currentSession() && directory==GameIO.GetSaveGameDir();
        }
        catch(Exception error){Diagnostic(expected,"read-error "+error.GetType().Name+" "+error.Message);return false;}
    }
    private static void Diagnostic(Expected expected,string reason)
    {
        if(expected==null)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(expected.LastDiagnostic==reason&&now-expected.LastDiagnosticAt<10f)return;
        expected.LastDiagnostic=reason;expected.LastDiagnosticAt=now;
        Log.Out("[REBIRTH VehicleSaveProof] entity="+expected.Id+" reason="+reason);
    }
    // Budgeted read accepts the installed native v38 vehicle envelope only.
    // Preflight every variable allocation before EntityCreationData.read, and
    // retain default/legacy behavior for all callers without the optional budget.
    private static bool TryMeasureVehicleRecord(Stream stream,out long end,out long allocation)
    {
        end=allocation=0;long start=stream.Position;
        try
        {
            using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))
            {
                if(reader.ReadByte()!=38||!EntityClass.list.TryGetValue(reader.ReadInt32(),out var definition)||
                    !typeof(EntityVehicle).IsAssignableFrom(definition.classname))return false;
                Skip(stream,4+7*4);Flag(reader);
                if(reader.ReadInt32()!=4)return false;Skip(stream,8);
                if(Flag(reader))return false; // Vehicle native constructor has no EntityAlive Stats.
                Skip(stream,2);long bag=0;
                if(Flag(reader)){bag=reader.ReadInt32();if(bag<0)return false;Skip(stream,bag);}
                Skip(stream,15);long body=reader.ReadUInt16();Skip(stream,body);
                if(Flag(reader))return false;Skip(stream,28);
                end=stream.Position;allocation=checked(2*(bag+body)+128*1024);return true;
            }
        }
        catch{return false;}
        finally{stream.Position=start;}
    }
    private static void Skip(Stream stream,long bytes)
    {if(bytes<0||bytes>stream.Length-stream.Position)throw new EndOfStreamException();stream.Position+=bytes;}
    private static bool Flag(BinaryReader reader)
    {byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static bool Equal(byte[] left,byte[] right)
    {if(left.Length!=right.Length)return false;for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false;return true;}
    private static int FirstDifference(byte[] left,byte[] right)
    {for(int i=0;i<Math.Min(left.Length,right.Length);i++)if(left[i]!=right[i])return i;return left.Length==right.Length?-1:Math.Min(left.Length,right.Length);}
}
