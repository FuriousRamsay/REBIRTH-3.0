using System;
using System.IO;

// Read detached native save records only. Never call ApplyToEntity or load a
// replacement vehicle just to acknowledge persistence.
public static class RebirthCamperSavedVehicle
{
    public sealed class Expected
    {
        internal int Id,Class;
        internal byte[] Bag,Entity;
        internal World World;
        internal string SaveDirectory;
        internal bool DumpedMismatch;
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
    public static Expected RequestSave(EntityVehicle vehicle)
    {
        var expected=Capture(vehicle);
        var manager=VehicleManager.Instance;
        if(manager==null || string.IsNullOrEmpty(expected.SaveDirectory))
            throw new InvalidOperationException("Native vehicle persistence is unavailable.");
        // Use native Update's single-writer scheduling. Calling Save directly
        // can start another pooled writer before the previous worker registers.
        // Only Verify proves that this particular snapshot reached disk.
        manager.TriggerSave();
        // Let native Update start the one writer on its next frame rather than
        // holding every UI transfer for TriggerSave's ten-second countdown.
        manager.saveTime=0f;
        return expected;
    }
    public static bool Verify(EntityVehicle vehicle,Expected expected,Func<bool> currentSession)
    {
        if(!ThreadManager.IsMainThread() || vehicle==null || expected==null || currentSession==null || !currentSession()
            || vehicle.world==null || vehicle.world.IsRemote() || vehicle.entityId!=expected.Id
            || !ReferenceEquals(vehicle.world,expected.World)
            || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle))return false;
        string directory=GameIO.GetSaveGameDir();
        if(string.IsNullOrEmpty(directory) || directory!=expected.SaveDirectory)return false;
        var writer=VehicleManager.Instance?.saveThread;
        if(writer!=null && !writer.HasTerminated())return false;
        try
        {
            string path=Path.Combine(directory,"vehicles.dat");
            if(!SdFile.Exists(path))return false;
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
                    var record=new EntityCreationData();record.read(reader,StreamModeRead.Persistency);
                    if(record.id!=expected.Id)continue;
                    if(found || record.entityClass!=expected.Class || record.bagData==null || record.entityData==null)return false;
                    byte[] actualBag=record.bagData.ToArray(),actualEntity=record.entityData.ToArray();
                    bool bagMatches=Equal(actualBag,expected.Bag),entityMatches=Equal(actualEntity,expected.Entity);
                    if(!bagMatches||!entityMatches)
                    {
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
                if(stream.Position!=stream.Length)return false;
            }
            return found && currentSession() && directory==GameIO.GetSaveGameDir();
        }
        catch(Exception){return false;}
    }
    private static bool Equal(byte[] left,byte[] right)
    {if(left.Length!=right.Length)return false;for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false;return true;}
    private static int FirstDifference(byte[] left,byte[] right)
    {for(int i=0;i<Math.Min(left.Length,right.Length);i++)if(left[i]!=right[i])return i;return left.Length==right.Length?-1:Math.Min(left.Length,right.Length);}
}
