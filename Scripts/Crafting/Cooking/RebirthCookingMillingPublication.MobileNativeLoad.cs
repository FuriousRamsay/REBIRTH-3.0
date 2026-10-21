using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;

internal static partial class RebirthCookingMillingPublication
{
    private sealed class MobileFileLoad
    {internal VehicleManager Manager;internal string Save;internal MobileFileRead Read;internal bool Returned,Closed;}
    private sealed class MobileFileRead
    {internal MobileFileLoad Load;internal Stream Stream;internal PooledBinaryReader Reader;internal string Path,Digest;internal int Count;internal bool Returned;}
    private sealed class MobileSavedRecord
    {internal MobileFileRead Read;internal string Bag,Entity;internal long BagLength,EntityLength;internal int Class;internal byte Version;}
    private sealed class MobileFactoryRead
    {internal MobileSavedRecord Record;internal EntityCreationData Data;internal World World;internal EntityVehicle Vehicle;internal int Reads;internal bool Returned;internal string Identity,Modules;internal IDisposable Memory;internal Dictionary<Guid,byte[]> Bodies;}
    private sealed class MobileLoadedSource
    {
        internal MobileSavedRecord Record;internal World World;internal EntityVehicle Vehicle;
        internal string Identity,Modules;internal IDisposable Memory;internal MobileColdScan Scan;
        internal Dictionary<Guid,byte[]> Bodies;
        ~MobileLoadedSource(){Memory?.Dispose();Scan?.Dispose();}
        internal bool Current()=>Record.Read.Load.Returned&&!Record.Read.Load.Closed&&ReferenceEquals(Record.Read.Load.Read,Record.Read)&&
            ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&
            Path.GetFullPath(GameIO.GetSaveGameDir())==Record.Read.Load.Save&&ReferenceEquals(VehicleManager.Instance,Record.Read.Load.Manager)&&
            ReferenceEquals(Vehicle.world,World)&&ReferenceEquals(World.GetEntity(Vehicle.entityId),Vehicle)&&
            Vehicle.entityClass==Record.Class&&Record.Read.Load.Manager.vehiclesActive.Contains(Vehicle);
    }
    [ThreadStatic]private static MobileFileLoad mobileFileLoading;
    [ThreadStatic]private static MobileFactoryRead mobileFactoryReading;
    private static readonly ConditionalWeakTable<EntityCreationData,MobileSavedRecord> mobileSavedRecords=new ConditionalWeakTable<EntityCreationData,MobileSavedRecord>();
    private static readonly ConditionalWeakTable<EntityVehicle,MobileLoadedSource> mobileLoadedSources=new ConditionalWeakTable<EntityVehicle,MobileLoadedSource>();
    private static string MobileStreamDigest(Stream stream)
    {
        if(stream==null||!stream.CanRead||!stream.CanSeek)throw new InvalidDataException();
        long position=stream.Position;try{stream.Position=0;using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(stream));}
        finally{stream.Position=position;}
    }
    private sealed class MobileNativeHash:Stream
    {
        private readonly SHA256 hash=SHA256.Create();private readonly byte[] single=new byte[1];internal long Count;private bool finished;
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>!finished;
        public override long Length=>Count;public override long Position{get=>Count;set=>throw new NotSupportedException();}
        public override void Flush(){}public override int Read(byte[] bytes,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long length)=>throw new NotSupportedException();
        public override void Write(byte[] bytes,int offset,int count){if(finished||count<0)throw new InvalidDataException();hash.TransformBlock(bytes,offset,count,null,0);Count=checked(Count+count);}
        public override void WriteByte(byte value){single[0]=value;Write(single,0,1);}
        internal string Finish(){if(!finished){hash.TransformFinalBlock(Array.Empty<byte>(),0,0);finished=true;}return Convert.ToBase64String(hash.Hash);}
        protected override void Dispose(bool disposing){if(disposing)hash.Dispose();base.Dispose(disposing);}
    }
    private static bool MobileLoadedBagMatches(EntityVehicle vehicle,MobileSavedRecord record)
    {
        if(vehicle.bag==null)return false;
        using(var stream=new MobileNativeHash())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(false))
        {writer.SetBaseStream(stream);vehicle.bag.Write(writer,StreamModeWrite.Persistency);writer.Flush();return stream.Count==record.BagLength&&stream.Finish()==record.Bag;}
    }
    internal static void ObserveMobileNativeOpen(string path,Stream stream,bool ran)
    {
        var load=mobileFileLoading;
        if(!ran||load==null||stream==null||!stream.CanRead||!stream.CanSeek||stream.Position!=0)return;
        string full=Path.GetFullPath(path),primary=Path.Combine(load.Save,"vehicles.dat");
        if(full!=primary&&full!=primary+".bak")return;
        load.Read=new MobileFileRead{Load=load,Path=full,Stream=stream};
    }
    internal static void InstallMobileNativeSourceObservers()
    {
        RebirthDogLifecycleService.InstallSharedNativeFileOpenObserver();
        var harmony=new Harmony("rebirth.cooking.milling.mobile.native-source");
        foreach(var type in new[]{typeof(MobileNativeVehicleLoad),typeof(MobileNativeVehicleFileRead),typeof(MobileNativeVehicleFactory),typeof(MobileNativeVehicleRead),typeof(MobileNativeMillingTick)})
            RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
    }
    [HarmonyPatch(typeof(VehicleManager),nameof(VehicleManager.Load))]
    private static class MobileNativeVehicleLoad
    {
        private static void Prefix(VehicleManager __instance,bool __runOriginal,out object __state)
        {
            __state=null;
            try
            {
                if(!__runOriginal||mobileFileLoading!=null)return;
                __state=mobileFileLoading=new MobileFileLoad{Manager=__instance,Save=Path.GetFullPath(GameIO.GetSaveGameDir())};
            }
            catch{/* Independent actual native load observer only. */}
        }
        private static void Postfix(bool __runOriginal,object __state)
        {
            if(__state is MobileFileLoad load&&ReferenceEquals(load,mobileFileLoading)&&__runOriginal&&load.Read?.Returned==true)load.Returned=true;
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {
            if(__state is MobileFileLoad load){if(__exception!=null){load.Returned=false;load.Closed=true;}if(load.Read!=null){load.Read.Reader=null;load.Read.Stream=null;}if(ReferenceEquals(mobileFileLoading,load))mobileFileLoading=null;}
            return __exception;
        }
    }
    [HarmonyPatch(typeof(VehicleManager),nameof(VehicleManager.read))]
    private static class MobileNativeVehicleFileRead
    {
        private static void Prefix(VehicleManager __instance,PooledBinaryReader _br,bool __runOriginal,out object __state)
        {
            __state=null;
            try
            {
                var read=mobileFileLoading?.Read;var stream=_br?.BaseStream;
                if(!__runOriginal||read==null||!ReferenceEquals(read.Load.Manager,__instance)||!ReferenceEquals(stream,read.Stream)||stream.Position!=0||stream.Length<9)return;
                var header=new byte[9];long position=stream.Position;
                try{if(stream.Read(header,0,9)!=9)return;}finally{stream.Position=position;}
                int count=header[5]|header[6]<<8|header[7]<<16|header[8]<<24;
                if(header[0]!='v'||header[1]!='d'||header[2]!='a'||header[3]!=0||header[4]!=1||count<0||count>65536)return;
                read.Reader=_br;read.Count=count;read.Digest=MobileStreamDigest(stream);__state=read;
            }
            catch{/* No native reader behavior changes. */}
        }
        private static void Postfix(VehicleManager __instance,PooledBinaryReader _br,bool __runOriginal,object __state)
        {
            try
            {
                if(__state is not MobileFileRead read||!__runOriginal||!ReferenceEquals(read.Load,mobileFileLoading)||!ReferenceEquals(read.Load.Read,read)||
                    !ReferenceEquals(read.Reader,_br)||_br.BaseStream.Position!=_br.BaseStream.Length||__instance.vehiclesUnloaded.Count!=read.Count||
                    MobileStreamDigest(_br.BaseStream)!=read.Digest)return;
                foreach(var data in __instance.vehiclesUnloaded)
                {
                    if(data==null||data.readStreamMode!=StreamModeRead.Persistency||data.bagData==null||data.entityData==null||data.entityData.Length<1)continue;
                    mobileSavedRecords.Remove(data);mobileSavedRecords.Add(data,new MobileSavedRecord{Read=read,Class=data.entityClass,Version=data.readFileVersion,
                        BagLength=data.bagData.Length,EntityLength=data.entityData.Length,Bag=MobileStreamDigest(data.bagData),Entity=MobileStreamDigest(data.entityData)});
                }
                read.Returned=true;
            }
            catch{/* A partial list or changed native file issues no capability. */}
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {if(__exception!=null&&__state is MobileFileRead read)read.Returned=false;return __exception;}
    }
    [HarmonyPatch(typeof(EntityFactory),nameof(EntityFactory.CreateEntity),new[]{typeof(EntityCreationData)})]
    private static class MobileNativeVehicleFactory
    {
        private static void Prefix(EntityCreationData __0,bool __runOriginal,out object __state)
        {
            __state=null;
            try
            {
                if(!__runOriginal||mobileFactoryReading!=null||__0==null||!mobileSavedRecords.TryGetValue(__0,out var record)||
                    !record.Read.Load.Returned||record.Read.Load.Closed||!ReferenceEquals(record.Read.Load.Read,record.Read)||
                    !ReferenceEquals(VehicleManager.Instance,record.Read.Load.Manager)||!record.Read.Load.Manager.vehiclesUnloaded.Any(value=>ReferenceEquals(value,__0))||
                    __0.readStreamMode!=StreamModeRead.Persistency||__0.readFileVersion!=record.Version||__0.entityClass!=record.Class||
                    __0.bagData.Length!=record.BagLength||__0.entityData.Length!=record.EntityLength||
                    MobileStreamDigest(__0.bagData)!=record.Bag||MobileStreamDigest(__0.entityData)!=record.Entity)return;
                var world=GameManager.Instance?.World;if(world==null||world.IsRemote()||Path.GetFullPath(GameIO.GetSaveGameDir())!=record.Read.Load.Save)return;
                __state=mobileFactoryReading=new MobileFactoryRead{Record=record,Data=__0,World=world};
            }
            catch{/* A parsed equal clone is not a native record reference. */}
        }
        private static void Postfix(Entity __result,bool __runOriginal,object __state)
        {
            try
            {
                if(__state is not MobileFactoryRead read||!ReferenceEquals(read,mobileFactoryReading)||!__runOriginal||!read.Returned||read.Reads!=1||
                    __result is not EntityVehicle vehicle||!ReferenceEquals(read.Vehicle,vehicle)||!ReferenceEquals(vehicle.world,read.World)||!RebirthCamperRadial.IsCamper(vehicle))return;
                mobileLoadedSources.Remove(vehicle);mobileLoadedSources.Add(vehicle,new MobileLoadedSource{Record=read.Record,Vehicle=vehicle,World=read.World,
                    Identity=read.Identity,Modules=read.Modules,Memory=read.Memory,Bodies=read.Bodies});read.Memory=null;read.Bodies=null;
            }
            catch{/* Native factory outcome stays independent. */}
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {
            if(__state is MobileFactoryRead read){if(__exception!=null&&read.Vehicle!=null)mobileLoadedSources.Remove(read.Vehicle);read.Memory?.Dispose();read.Memory=null;if(ReferenceEquals(read,mobileFactoryReading))mobileFactoryReading=null;}
            return __exception;
        }
    }
    [HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.Read),new[]{typeof(byte),typeof(PooledBinaryReader),typeof(StreamModeRead)})]
    private static class MobileNativeVehicleRead
    {
        private static void Prefix(EntityVehicle __instance,byte _version,PooledBinaryReader _br,StreamModeRead _eStreamMode,bool __runOriginal,out object __state)
        {
            __state=null;var read=mobileFactoryReading;
            if(!__runOriginal||read==null||read.Reads!=0||_version!=read.Record.Version||_eStreamMode!=StreamModeRead.Persistency||
                !ReferenceEquals(_br?.BaseStream,read.Data.entityData)||_br.BaseStream.Position!=0||!ReferenceEquals(__instance.world,read.World))return;
            read.Reads++;read.Vehicle=__instance;__state=read;
        }
        private static void Postfix(EntityVehicle __instance,PooledBinaryReader _br,bool __runOriginal,object __state)
        {
            try
            {
                if(__state is MobileFactoryRead read&&ReferenceEquals(read,mobileFactoryReading)&&__runOriginal&&ReferenceEquals(read.Vehicle,__instance)&&
                    ReferenceEquals(_br.BaseStream,read.Data.entityData)&&_br.BaseStream.Position==read.Record.EntityLength&&
                    MobileLoadedBagMatches(__instance,read.Record))
                {
                    var item=__instance.vehicle?.GetUpdatedItemValue();
                    if(item==null||!item.TryGetMetadata("RebirthCamperIdentity",out string identity)||!Guid.TryParse(identity,out var id)||id==Guid.Empty||
                        !item.TryGetMetadata("RebirthCamperModules",out string modules)||modules==null||modules.Length>16*1024*1024||
                        !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*modules.Length+65536),out var memory))return;
                    read.Memory=memory;
                    if(!RebirthCamperModuleItemCandidate.TryRestore(id,item,out var storage,out var stations))return;
                    var bodies=new Dictionary<Guid,byte[]>();
                    for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
                    {var installed=stations.GetSlot(slot);if(installed!=null)bodies.Add(installed.Id,RebirthCamperStationSnapshot.Capture(installed.Id,installed.State));}
                    read.Identity=identity;read.Modules=modules;read.Bodies=bodies;read.Returned=true;
                }
            }
            catch{/* Swallowed native Read errors still produce no purpose. */}
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {if(__exception!=null&&__state is MobileFactoryRead read)read.Returned=false;return __exception;}
    }
}
