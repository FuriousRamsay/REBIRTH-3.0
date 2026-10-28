// AUTHORED_NOT_RUN. Complete production evidence sources are inserted by the driver.
// Native ECD serialization and physical publication are explicit adapters.
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
namespace HarmonyLib {
    class Harmony {internal Harmony(string id){}}
    [AttributeUsage(AttributeTargets.Class)] class HarmonyPatch:Attribute {internal HarmonyPatch(Type t,string name,params Type[] args){}}
}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthHarmonyBootstrap {internal static void PatchClassOnce(Harmony h,Type t){}}
class RebirthStationSnapshotUpdatePatch {} class RebirthStationChunkSerializedPatch {}
class RebirthStationSnapshotResetPatch {} class RebirthStationSnapshotWritePatch {} class RebirthStationRegionPublishedPatch {}
enum StreamModeWrite {Persistency,Network}
class PooledBinaryWriter {internal Stream BaseStream;}
class EntityCreationData {internal int id,entityClass;public void write(PooledBinaryWriter writer,StreamModeWrite mode){}}
public interface IRebirthStationSnapshotByteBudget {int MaximumSnapshotBytes{get;}bool TryReserveAllocation(long size,out IDisposable reservation);}
class Budget:IRebirthStationSnapshotByteBudget {
    public int MaximumSnapshotBytes=>1024*1024;internal long Live;internal bool Refuse;
    sealed class Allocation:IDisposable {internal Budget B;internal long N;public void Dispose(){if(B!=null){B.Live-=N;B=null;}}}
    public bool TryReserveAllocation(long n,out IDisposable allocation){allocation=null;if(Refuse)return false;Live+=n;allocation=new Allocation{B=this,N=n};return true;}
}
// ACTUAL_CORE
// ACTUAL_ENTITY
public static partial class RebirthStationSnapshotEvidence {
    // Fixture access only; invokes actual installed hook bodies rather than a model.
    internal static Exception FixtureWrite(EntityCreationData entity,Stream stream,StreamModeWrite mode,byte[] payload,Exception error=null){
        var writer=new PooledBinaryWriter{BaseStream=stream};
        var hook=typeof(RebirthStationSnapshotEvidence).GetNestedType("NativeEntitySerialization",System.Reflection.BindingFlags.NonPublic);
        var args=new object[]{entity,writer,mode,null};
        hook.GetMethod("Prefix",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,args);
        stream.Write(payload,0,payload.Length);
        return (Exception)hook.GetMethod("Finalizer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{writer,args[3],error});
    }
    internal static void FixturePublish(Request request,object snapshot)=>MarkPublished(request,snapshot,request.Generation,"adapter.region","adapter.digest");
}
class Program {
    static int assertions;
    static void Check(bool value,string label){assertions++;if(!value)throw new Exception(label);}
    sealed class FaultStream:MemoryStream {
        internal bool Fault;
        public override long Position {get{if(Fault)throw new IOException("observer position unavailable");return base.Position;}set{base.Position=value;}}
    }
    sealed class Case:IDisposable {
        internal object World=new object(),Chunk=new object(),Snapshot=new object();internal bool Current=true;
        internal Budget Budget=new Budget();internal RebirthStationSnapshotEvidence.Request Request;
        internal RebirthStationSnapshotEvidence.Frame Frame;internal MemoryStream Stream=new MemoryStream();
        internal byte[] Expected={1,2,3,4};
        internal RebirthStationSnapshotEvidence.EntityImage Image;internal int Captures;internal bool ThrowCapture;
        internal Case(){Request=RebirthStationSnapshotEvidence.WatchEntity(World,Chunk,2,3,7,9,Expected,()=>Current,Budget,image=>{Image=image;Captures++;if(ThrowCapture)throw new IOException("descriptor publication interrupted");});}
        internal void Begin(){Frame=RebirthStationSnapshotEvidence.Begin(Snapshot,Chunk,World,2,3,true);Stream.Write(new byte[]{116,116,99,0,47,0,0,0},0,8);}
        internal Exception Write(byte[] payload=null,int id=7,int type=9,StreamModeWrite mode=StreamModeWrite.Persistency,Exception error=null)=>RebirthStationSnapshotEvidence.FixtureWrite(new EntityCreationData{id=id,entityClass=type},Stream,mode,payload??new byte[]{1,2,3,4},error);
        internal void Finish(){RebirthStationSnapshotEvidence.Serialized(Chunk,Stream);Stream.Position=0;RebirthStationSnapshotEvidence.Finish(Frame,Stream,null);}
        internal bool Proof()=>RebirthStationSnapshotEvidence.TryGetPublishedEntity(Request,out _);
        internal void Publish()=>RebirthStationSnapshotEvidence.FixturePublish(Request,Snapshot);
        public void Dispose(){Request?.Dispose();Stream.Dispose();Check(Budget.Live==0,"all expected/snapshot allocations released");}
    }
    static void Main(){
        using(var c=new Case()){
            c.Expected[0]=99;c.Begin();c.Write();c.Finish();Check(!c.Proof(),"serialization alone is not publication");
            Check(c.Captures==1&&c.Image.Bytes==12&&c.Image.Offset==8&&c.Image.Length==4&&c.Image.X==2&&c.Image.Z==3&&c.Image.Digest.Length==64,"exact serialized entity descriptor captured before publication");
            c.Publish();Check(c.Proof(),"exact frozen entity frame plus adapter publication");
            c.Current=false;Check(!c.Proof(),"revoked original cannot expose proof");
        }
        for(int vector=0;vector<7;vector++)using(var c=new Case()){
            c.Begin();
            if(vector==0)c.Write(new byte[]{1,2,8,4});
            if(vector==1)c.Write(new byte[]{1,2,3});
            if(vector==2)c.Write(type:10);
            if(vector==3){c.Write();c.Write();}
            if(vector==4)c.Write(id:8);
            if(vector==5)c.Write(mode:StreamModeWrite.Network);
            if(vector==6){var error=new IOException("native failure");Check(ReferenceEquals(c.Write(error:error),error),"preserve original native exception");}
            c.Finish();c.Publish();Check(!c.Proof(),"invalid/missing/duplicate native frame "+vector);
        }
        using(var c=new Case()){
            c.Begin();c.Write(id:8);c.Write();c.Finish();c.Publish();Check(c.Proof(),"unrelated native entity is allowed");
            RebirthStationSnapshotEvidence.Invalidate(c.Snapshot);Check(!c.Proof(),"pooled snapshot reset revokes generation");
        }
        using(var c=new Case()){
            c.Begin();c.Write();c.Current=false;c.Finish();c.Current=true;c.Publish();Check(!c.Proof(),"scope loss at finish cannot revive proof");
        }
        using(var c=new Case()){
            c.Begin();c.Write();c.Budget.Refuse=true;c.Finish();c.Budget.Refuse=false;c.Publish();Check(!c.Proof(),"snapshot allocation refusal cannot publish");
        }
        using(var c=new Case()){
            c.Begin();c.Write();c.Finish();c.Publish();c.Request.Dispose();Check(!c.Proof(),"request disposal revokes proof");
        }
        using(var c=new Case()){
            c.ThrowCapture=true;c.Begin();c.Write();c.Finish();c.Publish();
            Check(c.Captures==1&&!c.Proof()&&!RebirthStationSnapshotEvidence.TryGetSerializedEntity(c.Request,out _),"capture callback failure revokes observer evidence without throwing into native save");
        }
        using(var c=new Case()){
            c.Begin();c.Write();RebirthStationSnapshotEvidence.Serialized(c.Chunk,c.Stream);
            using(var other=new MemoryStream(c.Stream.ToArray()))RebirthStationSnapshotEvidence.Finish(c.Frame,other,null);
            c.Publish();Check(!c.Proof(),"same bytes in a different final stream do not qualify");
        }
        using(var c=new Case()){
            c.Stream.Dispose();var broken=new FaultStream();c.Stream=broken;c.Begin();broken.Fault=true;
            var error=new IOException("native failure survives broken observer");
            Check(ReferenceEquals(c.Write(error:error),error),"observer position exception preserves native exception");
            broken.Fault=false;c.Finish();c.Publish();Check(!c.Proof(),"broken observer cannot leave valid span");
        }
        using(var c=new Case()){
            c.Begin();c.Write();c.Finish();c.Publish();Check(c.Proof(),"old generation initially published");
            // Reuse the same pooled snapshot, this time without the matching native entity.
            c.Stream.SetLength(0);c.Stream.Position=0;c.Begin();c.Write(id:8);c.Finish();c.Publish();
            Check(!c.Proof(),"pooled snapshot cannot reuse old matching entity evidence");
        }
        var world=new object();var chunk=new object();var refused=new Budget{Refuse=true};
        Check(RebirthStationSnapshotEvidence.WatchEntity(world,chunk,0,0,7,9,new byte[]{1},()=>true,refused)==null,"expected-frame reservation refusal");
        using(var retry=RebirthStationSnapshotEvidence.WatchEntity(world,chunk,0,0,7,9,new byte[]{1},()=>true))Check(retry!=null,"failed reservation frees chunk watch");
        Check(refused.Live==0,"refused budget has no retained bytes");
        Console.WriteLine("MP042 actual entity publication assertions="+assertions+"; native frames/publication are adapters, not native persistence validation");
    }
}
